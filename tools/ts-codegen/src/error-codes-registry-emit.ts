// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { readdirSync, readFileSync } from "node:fs";
import { basename, join } from "node:path";

import {
  diagError,
  type EmitDiagnostic,
  type EmitResult,
  DiagnosticIds,
  formatDiagnostic,
} from "./lib/diagnostics.js";
import {
  buildHeader,
  isOutputUpToDate,
  writeGeneratedFile,
} from "./lib/file-emit.js";
import { contractsPath, tsPackagePath } from "./lib/paths.js";
import { parseTkKey } from "./lib/tk-key-transform.js";
import { StringBuilder } from "./lib/string-builder.js";
import {
  type ErrorCodesSpec,
  type ErrorCodeEntry,
} from "./error-codes-emit.js";

// ---------------------------------------------------------------------------
// Merged error-code registry emitter. Globs ALL error-code specs under
// `contracts/` (generic `error-codes/error-codes.spec.json` + every
// `*-error-codes.spec.json`), aggregates them into a unified registry, runs
// cross-catalog collision + reserved-namespace checks (D2ERC004 / D2ERC005),
// and emits one `error-code-registry.g.ts` into
// `server/shared/typescript/error-codes-registry/src/generated/`.
//
// Mirrors the .NET D2.Shared.ErrorCodes.Registry.SourceGen shape and the geo
// GeoGenerator aggregate-then-collision-check precedent.
//
// Collision semantics:
//   D2ERC004 — same `code` declared in two or more catalogs (cross-catalog).
//   D2ERC005 — reserved-namespace violation:
//              (a) an unprefixed code (no `_`) in a per-domain spec, OR
//              (b) a domain-prefixed code in the generic spec.
//
// Both are Error severity and hard-fail generation — no registry is emitted.
// ---------------------------------------------------------------------------

// ---------------------------------------------------------------------------
// Domain derivation (D6): spec filename → domain token.
//   "error-codes.spec.json"       → "common"
//   "auth-error-codes.spec.json"  → "auth"
//   "geo-error-codes.spec.json"   → "geo"   (future)
// ---------------------------------------------------------------------------
function domainFromSpecPath(specPath: string): string {
  const name = basename(specPath);
  // The generic spec is the special sentinel.
  if (name === "error-codes.spec.json") return "common";
  // Pattern: <domain>-error-codes.spec.json → <domain>.
  const match = /^([a-z][a-z0-9-]*)-error-codes\.spec\.json$/.exec(name);
  return match?.[1] ?? "unknown";
}

// ---------------------------------------------------------------------------
// Content-based spec discovery — mirrors the walkSpecs / isErrorCodeSpec
// logic in error-codes-locale-completeness.parity.test.ts so both use the
// same detection heuristic (first entry carries a `userMessageKey` field).
// ---------------------------------------------------------------------------
function isErrorCodeSpec(data: unknown): data is ErrorCodesSpec {
  if (typeof data !== "object" || data === null) return false;
  const obj = data as Record<string, unknown>;
  if (!Array.isArray(obj["errorCodes"]) || obj["errorCodes"].length === 0)
    return false;
  const first = obj["errorCodes"][0] as Record<string, unknown>;
  return typeof first["userMessageKey"] === "string";
}

function walkSpecPaths(dir: string): string[] {
  const results: string[] = [];
  for (const entry of readdirSync(dir, { withFileTypes: true })) {
    const full = join(dir, entry.name);
    if (entry.isDirectory()) results.push(...walkSpecPaths(full));
    else if (entry.name.endsWith(".spec.json")) results.push(full);
  }
  return results;
}

/** One catalog's worth of validated entries + the derived domain token. */
export interface CatalogEntry {
  readonly specPath: string;
  readonly domain: string;
  readonly entries: readonly ErrorCodeEntry[];
}

/**
 * Result of {@link discoverCatalogs}. Carries both the discovered catalogs
 * and any parse-level diagnostics (D2ERC006) that fired for malformed spec
 * files. A non-empty `diagnostics` list (with severity `"error"`) means the
 * discovery phase failed and the caller must NOT proceed with emission.
 */
export interface DiscoverResult {
  readonly catalogs: readonly CatalogEntry[];
  readonly diagnostics: readonly EmitDiagnostic[];
}

/**
 * Discover and load all error-code specs under `contracts/`. Returns a
 * {@link DiscoverResult} — the ordered list of {@link CatalogEntry} objects
 * (sorted by spec path for deterministic output) plus any parse-level
 * diagnostics. Only includes specs that pass the content-based
 * `isErrorCodeSpec` check — schema specs, fixture specs, etc. are ignored.
 *
 * D2ERC006 fires (Error severity) when a `.spec.json` file is found but
 * cannot be parsed as JSON — mirrors the .NET `RegistryGenerator` D2ERC006
 * build error, which emits nothing on parse failure. Callers must check
 * `diagnostics` for error-severity entries before proceeding with emission.
 */
export function discoverCatalogs(contractsDir: string): DiscoverResult {
  const specPaths = walkSpecPaths(contractsDir).sort();
  const catalogs: CatalogEntry[] = [];
  const diagnostics: EmitDiagnostic[] = [];
  for (const specPath of specPaths) {
    let data: unknown;
    try {
      data = JSON.parse(readFileSync(specPath, "utf8"));
    } catch {
      diagnostics.push(
        diagError(
          DiagnosticIds.ERC_MALFORMED_REGISTRY_SPEC,
          `D2ERC006: catalog spec '${specPath}' could not be parsed as JSON` +
            ` — fix or remove the malformed file`,
          specPath,
        ),
      );
      continue;
    }
    if (!isErrorCodeSpec(data)) continue;
    catalogs.push({
      specPath,
      domain: domainFromSpecPath(specPath),
      entries: (data as ErrorCodesSpec).errorCodes,
    });
  }
  return { catalogs, diagnostics };
}

/**
 * Returns the enforced domain prefix token for a per-domain catalog
 * (e.g. `"AUTH_"` for domain `"auth"`), or `undefined` for the generic catalog.
 */
function domainPrefix(domain: string): string | undefined {
  if (domain === "common") return undefined;
  return `${domain.toUpperCase()}_`;
}

/**
 * Aggregate all catalog entries + run cross-catalog collision checks.
 * Returns all valid entries (flattened, preserving per-catalog order) OR a
 * non-empty diagnostics list if any collision / namespace violation fires.
 *
 * D2ERC004 — duplicate code across two or more catalogs.
 * D2ERC005 — reserved-namespace violation:
 *   (a) per-domain spec declares a code that does not start with its
 *       enforced domain prefix (e.g. `NOT_FOUND` in the auth catalog).
 *   (b) generic spec declares a code that starts with any per-domain
 *       catalog's enforced prefix (e.g. `AUTH_BEARER_MISSING` in generic).
 */
export function aggregateAndCheck(catalogs: readonly CatalogEntry[]): {
  entries: readonly (ErrorCodeEntry & { domain: string })[];
  diagnostics: readonly EmitDiagnostic[];
} {
  const diagnostics: EmitDiagnostic[] = [];
  // code → specPath of first declaration (for collision messages).
  const seenCode = new Map<string, string>();
  const result: (ErrorCodeEntry & { domain: string })[] = [];

  // Build the set of all per-domain prefixes so the generic-spec check can
  // detect any of them (not just the currently known `AUTH_`).
  const perDomainPrefixes = new Set<string>();
  for (const catalog of catalogs) {
    const prefix = domainPrefix(catalog.domain);
    if (prefix !== undefined) perDomainPrefixes.add(prefix);
  }

  for (const catalog of catalogs) {
    const isGeneric = catalog.domain === "common";
    const enforcedPrefix = domainPrefix(catalog.domain);

    for (const entry of catalog.entries) {
      let skipEntry = false;

      // D2ERC005 (b) — generic spec declares a domain-prefixed code.
      if (isGeneric) {
        for (const prefix of perDomainPrefixes) {
          if (entry.code.startsWith(prefix)) {
            diagnostics.push(
              diagError(
                DiagnosticIds.ERC_RESERVED_NAMESPACE_VIOLATION,
                `D2ERC005: generic spec '${catalog.specPath}' declares` +
                  ` domain-prefixed code '${entry.code}'` +
                  ` (starts with '${prefix}') — the generic catalog owns` +
                  ` only the unprefixed reserved namespace`,
                catalog.specPath,
              ),
            );
            skipEntry = true;
            break;
          }
        }
      }
      if (skipEntry) continue;

      // D2ERC005 (a) — per-domain spec declares a code without the required prefix.
      if (
        !isGeneric &&
        enforcedPrefix !== undefined &&
        !entry.code.startsWith(enforcedPrefix)
      ) {
        diagnostics.push(
          diagError(
            DiagnosticIds.ERC_RESERVED_NAMESPACE_VIOLATION,
            `D2ERC005: per-domain spec '${catalog.specPath}' declares code '${entry.code}' ` +
              `that does not start with the required domain prefix '${enforcedPrefix}'`,
            catalog.specPath,
          ),
        );
        continue;
      }

      // D2ERC004 — cross-catalog duplicate.
      const priorSpec = seenCode.get(entry.code);
      if (priorSpec !== undefined) {
        diagnostics.push(
          diagError(
            DiagnosticIds.ERC_CROSS_CATALOG_DUPLICATE_CODE,
            `D2ERC004: error code '${entry.code}' is declared in multiple catalogs: ` +
              `'${priorSpec}' and '${catalog.specPath}'`,
            catalog.specPath,
          ),
        );
        continue;
      }
      seenCode.set(entry.code, catalog.specPath);
      result.push({ ...entry, domain: catalog.domain });
    }
  }

  return { entries: result, diagnostics };
}

/**
 * Emit the merged `error-code-registry.g.ts` source. Returns an
 * {@link EmitResult} — either the generated source or an empty string + error
 * diagnostics. Never throws on collision (surfaced as diagnostics instead).
 *
 * @param catalogs        - Discovered catalogs from {@link discoverCatalogs}.
 * @param enUsKeys        - Optional en-US key set for D2ERC002 cross-check.
 *                          When provided, every `userMessageKey` must resolve
 *                          to a key present in the set.
 * @param validCategorySet - Optional closed set of valid category wire strings
 *                          (from `error-category.spec.json`). When provided,
 *                          every entry's `category` is validated against it;
 *                          an unknown category fires D2ERC007 (Error) and
 *                          blocks emission — mirrors the .NET
 *                          `CategorySpecLoader.Check()` validation.
 */
export function emitErrorCodeRegistry(
  catalogs: readonly CatalogEntry[],
  enUsKeys?: ReadonlySet<string>,
  validCategorySet?: ReadonlySet<string>,
): EmitResult {
  // Aggregate + collision check.
  const { entries, diagnostics: collisionDiags } = aggregateAndCheck(catalogs);
  if (collisionDiags.length > 0)
    return { source: "", diagnostics: collisionDiags };

  // D2ERC007 — validate each entry's category against the closed set (mirrors
  // the .NET CategorySpecLoader.Check()). Only runs when validCategorySet is
  // provided; the param is optional for callers that don't have the category
  // spec available.
  if (validCategorySet !== undefined) {
    const categoryDiags: EmitDiagnostic[] = [];
    for (const entry of entries) {
      if (entry.category !== undefined && !validCategorySet.has(entry.category))
        categoryDiags.push(
          diagError(
            DiagnosticIds.ERC_UNKNOWN_CATEGORY,
            `D2ERC007: error code '${entry.code}' has unknown category '${entry.category}' — ` +
              `not in the closed set from error-category.spec.json`,
          ),
        );
    }
    if (categoryDiags.length > 0)
      return { source: "", diagnostics: categoryDiags };
  }

  // Validate TK keys (D2ERC002-style cross-check) — warn if a key can't be
  // parsed; the per-catalog emitter already enforced this per-entry, so here
  // we just skip entries that don't parse (shouldn't happen in practice).
  const resolvedEntries: {
    entry: ErrorCodeEntry & { domain: string };
    tkConstantPath: string;
  }[] = [];
  const tkDiags: EmitDiagnostic[] = [];

  for (const entry of entries) {
    if (!entry.userMessageKey) {
      tkDiags.push(
        diagError(
          DiagnosticIds.ERC_TK_KEY_NOT_FOUND,
          `error code '${entry.code}' is missing userMessageKey`,
        ),
      );
      continue;
    }
    const parts = parseTkKey(entry.userMessageKey);
    if (parts === undefined) {
      tkDiags.push(
        diagError(
          DiagnosticIds.ERC_TK_KEY_NOT_FOUND,
          `error code '${entry.code}' has unparseable userMessageKey '${entry.userMessageKey}'`,
        ),
      );
      continue;
    }
    if (enUsKeys !== undefined && !enUsKeys.has(parts.snakeKey)) {
      tkDiags.push(
        diagError(
          DiagnosticIds.ERC_TK_KEY_NOT_FOUND,
          `error code '${entry.code}' references userMessageKey '${entry.userMessageKey}' ` +
            `which does not resolve to a key in en-US.json (expected '${parts.snakeKey}')`,
        ),
      );
      continue;
    }
    resolvedEntries.push({ entry, tkConstantPath: parts.tkConstantPath });
  }

  if (tkDiags.some((d) => d.severity === "error"))
    return { source: "", diagnostics: tkDiags };

  // Emit the generated file.
  const specRelativePath =
    "contracts/error-codes/error-codes.spec.json + contracts/*-error-codes/*.spec.json";

  const sb = new StringBuilder();
  sb.appendLine(buildHeader(specRelativePath));
  sb.appendLine("/* eslint-disable */");
  sb.appendLine();
  sb.appendLine('import { TK } from "@d2/i18n-keys";');
  sb.appendLine(
    'import { buildRegistry, type ErrorCodeInfo } from "../error-code-registry.js";',
  );
  sb.appendLine();
  sb.appendLine("/**");
  sb.appendLine(
    " * Merged cross-catalog error-code registry. Aggregates every",
  );
  sb.appendLine(
    " * `*-error-codes.spec.json` from `contracts/` into one frozen lookup",
  );
  sb.appendLine(
    " * table. Generated — do not edit. Run `pnpm --filter ts-codegen run codegen`",
  );
  sb.appendLine(" * to regenerate.");
  sb.appendLine(" */");
  sb.appendLine("const _entries: readonly ErrorCodeInfo[] = [");
  sb.increaseIndent();
  for (const { entry, tkConstantPath } of resolvedEntries) {
    sb.appendLine("{");
    sb.increaseIndent();
    sb.appendLine(`code: "${escapeStringLiteral(entry.code)}",`);
    sb.appendLine(`httpStatus: ${entry.httpStatus},`);
    sb.appendLine(`category: "${escapeStringLiteral(entry.category ?? "")}",`);
    sb.appendLine(`userMessageKey: ${tkConstantPath},`);
    sb.appendLine(
      `factoryName: "${escapeStringLiteral(entry.factoryName ?? "")}",`,
    );
    sb.appendLine(
      `factoryShape: "${escapeStringLiteral(entry.factoryShape ?? "")}",`,
    );
    sb.appendLine(`doc: "${escapeStringLiteral(entry.doc ?? "")}",`);
    sb.appendLine(
      `domain: "${escapeStringLiteral((entry as ErrorCodeEntry & { domain: string }).domain)}",`,
    );
    sb.decreaseIndent();
    sb.appendLine("},");
  }
  sb.decreaseIndent();
  sb.appendLine("];");
  sb.appendLine();
  sb.appendLine("/**");
  sb.appendLine(
    " * Merged error-code registry. Resolve a wire code to its full metadata,",
  );
  sb.appendLine(" * check membership, or iterate every registered code.");
  sb.appendLine(" */");
  sb.appendLine("export const errorCodeRegistry = buildRegistry(_entries);");
  sb.appendLine();

  return { source: sb.toString(), diagnostics: [] };
}

function escapeStringLiteral(value: string): string {
  return value.replace(/\\/g, "\\\\").replace(/"/g, '\\"');
}

// ---------------------------------------------------------------------------
// CLI-runner section — mtime-check, disk-write, isMain guard.
// Excluded from unit-test coverage (requires process/fs mocking to exercise);
// the exported library functions above (discoverCatalogs, aggregateAndCheck,
// emitErrorCodeRegistry) ARE fully unit-tested in error-codes-registry-emit.test.ts.
// ---------------------------------------------------------------------------

/* v8 ignore start */
const CONTRACTS_DIR = contractsPath();
const REGISTRY_TARGET = tsPackagePath(
  "error-codes-registry",
  "src",
  "generated",
  "error-code-registry.g.ts",
);

/** Load the en-US key set for the D2ERC002 cross-check. */
function loadEnUsKeys(): ReadonlySet<string> {
  const path = contractsPath("messages", "en-US.json");
  const parsed = JSON.parse(readFileSync(path, "utf8")) as Record<
    string,
    unknown
  >;
  const keys = new Set<string>();
  for (const key of Object.keys(parsed)) if (key !== "$schema") keys.add(key);
  return keys;
}

/**
 * Runner for the merged error-code registry. Discovers all error-code specs,
 * aggregates, collision-checks, and emits `error-code-registry.g.ts`.
 * Pass `force=true` to bypass the mtime up-to-date check.
 */
export function runErrorCodesRegistryEmit(
  force = false,
): readonly EmitDiagnostic[] {
  const { catalogs, diagnostics: discoverDiags } =
    discoverCatalogs(CONTRACTS_DIR);
  // Surface any parse-level diagnostics (D2ERC006) immediately — a malformed
  // spec file is a hard failure; do not proceed with emission.
  if (discoverDiags.some((d) => d.severity === "error")) return discoverDiags;

  // Build the mtime-check source list from all discovered spec paths.
  const sourcePaths = catalogs.map((c) => c.specPath);
  const enUsPath = contractsPath("messages", "en-US.json");
  sourcePaths.push(enUsPath);

  if (!force && isOutputUpToDate(REGISTRY_TARGET, sourcePaths)) return [];

  const enUsKeys = loadEnUsKeys();
  const result = emitErrorCodeRegistry(catalogs, enUsKeys);
  if (result.diagnostics.some((d) => d.severity === "error"))
    return result.diagnostics;

  writeGeneratedFile(REGISTRY_TARGET, result.source);
  return result.diagnostics;
}

const isMain =
  import.meta.url === `file://${process.argv[1]}` ||
  process.argv[1]?.endsWith("error-codes-registry-emit.ts") === true;
if (isMain) {
  const force = process.argv.includes("--force");
  const diagnostics = [...runErrorCodesRegistryEmit(force)];
  for (const d of diagnostics) console.error(formatDiagnostic(d));
  if (diagnostics.some((d) => d.severity === "error")) process.exit(1);
}
/* v8 ignore stop */
