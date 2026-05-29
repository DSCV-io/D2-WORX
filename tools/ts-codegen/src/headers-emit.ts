// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

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
import { loadSpec } from "./lib/spec-loader.js";
import { StringBuilder } from "./lib/string-builder.js";

/** One header entry parsed from `contracts/headers/headers.spec.json`. */
export interface HeaderEntry {
  readonly name: string;
  readonly constName: string;
  readonly applicability: readonly Transport[];
  readonly convention: string;
  readonly description: string;
}

/** Top-level shape of `headers.spec.json`. */
export interface HeadersSpec {
  readonly headers: readonly HeaderEntry[];
}

/** Closed enum of supported transports. */
export type Transport = "http" | "grpc" | "amqp";

const VALID_TRANSPORTS: ReadonlySet<string> = new Set(["http", "grpc", "amqp"]);

const VALID_CONVENTIONS: ReadonlySet<string> = new Set([
  "d2",
  "rfc",
  "w3c",
  "stripe",
  "amqp",
  "amqp-x",
  "oauth",
]);

const CONST_NAME_RE = /^[A-Z][A-Z0-9_]*$/;

/**
 * Catalog filter — selects which spec entries belong in a given catalog.
 *
 * - `common`: entries with applicability count >= 2 (cross-transport).
 * - `http` / `amqp` / `grpc`: entries whose applicability includes that
 *   transport (cross-transport entries appear in multiple catalogs at
 *   identical wire value).
 */
export type CatalogFilter = "common" | "http" | "amqp" | "grpc";

/** Result of validating the spec — partition of the entries plus diagnostics. */
export interface ValidatedHeaders {
  readonly entries: readonly HeaderEntry[];
  readonly diagnostics: readonly EmitDiagnostic[];
}

/**
 * Validate the spec — surface duplicate constant names within any catalog
 * the entry would belong to, unknown transports, unknown conventions
 * (warning only — see DiagnosticIds.HDR_UNKNOWN_CONVENTION), invalid
 * constName patterns, and empty applicability arrays.
 */
export function validateHeadersSpec(spec: HeadersSpec): ValidatedHeaders {
  const diagnostics: EmitDiagnostic[] = [];
  const valid: HeaderEntry[] = [];
  const seenConstNamesPerCatalog: Record<CatalogFilter, Set<string>> = {
    common: new Set<string>(),
    http: new Set<string>(),
    amqp: new Set<string>(),
    grpc: new Set<string>(),
  };
  for (const entry of spec.headers) {
    if (!CONST_NAME_RE.test(entry.constName)) {
      diagnostics.push(
        diagError(
          DiagnosticIds.HDR_INVALID_CONST_NAME,
          `header '${entry.name}' has invalid constName '${entry.constName}' — ` +
            `must match ${CONST_NAME_RE.source}`,
        ),
      );
      continue;
    }
    if (entry.applicability.length === 0) {
      diagnostics.push(
        diagError(
          DiagnosticIds.HDR_EMPTY_APPLICABILITY,
          `header '${entry.constName}' has empty applicability — ` +
            `every header must belong to at least one transport`,
        ),
      );
      continue;
    }
    let badTransport = false;
    for (const t of entry.applicability) {
      if (!VALID_TRANSPORTS.has(t)) {
        diagnostics.push(
          diagError(
            DiagnosticIds.HDR_UNKNOWN_TRANSPORT,
            `header '${entry.constName}' has unknown transport '${t}' (valid: ${[
              ...VALID_TRANSPORTS,
            ]
              .sort()
              .join(", ")})`,
          ),
        );
        badTransport = true;
        break;
      }
    }
    if (badTransport) continue;
    if (!VALID_CONVENTIONS.has(entry.convention)) {
      // Warning per Plan; emitter falls back to documenting verbatim.
      diagnostics.push({
        id: DiagnosticIds.HDR_UNKNOWN_CONVENTION,
        severity: "warning",
        message: `header '${entry.constName}' has unrecognized convention '${entry.convention}'`,
      });
    }
    // Per-catalog duplicate check.
    let duplicate = false;
    for (const cat of catalogsForEntry(entry)) {
      if (seenConstNamesPerCatalog[cat].has(entry.constName)) {
        diagnostics.push(
          diagError(
            DiagnosticIds.HDR_DUPLICATE,
            `header constName '${entry.constName}' duplicated in catalog '${cat}'`,
          ),
        );
        duplicate = true;
        break;
      }
    }
    if (duplicate) continue;
    for (const cat of catalogsForEntry(entry))
      seenConstNamesPerCatalog[cat].add(entry.constName);
    valid.push(entry);
  }
  return { entries: valid, diagnostics };
}

/** Returns every catalog an entry belongs to. */
export function catalogsForEntry(entry: HeaderEntry): readonly CatalogFilter[] {
  const result: CatalogFilter[] = [];
  if (entry.applicability.length >= 2) result.push("common");
  for (const t of entry.applicability) result.push(t);
  return result;
}

/** Selects spec entries belonging to the given catalog. */
export function entriesForCatalog(
  spec: HeadersSpec,
  catalog: CatalogFilter,
): readonly HeaderEntry[] {
  if (catalog === "common")
    return spec.headers.filter((e) => e.applicability.length >= 2);
  return spec.headers.filter((e) =>
    (e.applicability as readonly string[]).includes(catalog),
  );
}

/**
 * Emit one catalog's `.g.ts` source. Stateless and unit-testable in
 * isolation. Sorts entries by constName for deterministic output.
 */
export function emitHeadersCatalog(
  spec: HeadersSpec,
  catalog: CatalogFilter,
): EmitResult {
  const v = validateHeadersSpec(spec);
  const errors = v.diagnostics.filter((d) => d.severity === "error");
  if (errors.length > 0) return { source: "", diagnostics: v.diagnostics };
  const filtered = entriesForCatalog({ headers: v.entries }, catalog);
  const sorted = [...filtered].sort((a, b) =>
    a.constName.localeCompare(b.constName),
  );

  const className = catalogClassName(catalog);
  const sb = new StringBuilder();
  sb.appendLine(buildHeader("contracts/headers/headers.spec.json"));
  sb.appendLine("/* eslint-disable */");
  sb.appendLine();
  sb.appendLine("/**");
  sb.appendLine(
    ` * D2 wire-protocol headers applicable to the ${catalog} catalog.`,
  );
  sb.appendLine(
    ` * Generated from headers.spec.json. Mirrors .NET ` +
      `D2.Shared.Headers.${capitalize(catalog)}.${className}.`,
  );
  sb.appendLine(" */");
  sb.appendLine(`export const ${className} = {`);
  sb.increaseIndent();
  for (const e of sorted) {
    sb.appendLine("/**");
    for (const line of e.description.split("\n"))
      sb.appendLine(` * ${escapeJsDoc(line)}`);
    sb.appendLine(` * Convention: ${e.convention}.`);
    sb.appendLine(
      ` * Applicability: ${[...e.applicability].sort().join(", ")}.`,
    );
    sb.appendLine(" */");
    sb.appendLine(`${e.constName}: "${escapeStringLiteral(e.name)}",`);
  }
  sb.decreaseIndent();
  sb.appendLine("} as const;");
  sb.appendLine();
  sb.appendLine(`export type ${className.replace(/s$/, "")}Name =`);
  sb.increaseIndent();
  sb.appendLine(`(typeof ${className})[keyof typeof ${className}];`);
  sb.decreaseIndent();
  sb.appendLine();
  sb.appendLine(
    `export const ALL_${snakeUpper(className)}: readonly string[] = [`,
  );
  sb.increaseIndent();
  for (const e of sorted) sb.appendLine(`"${escapeStringLiteral(e.name)}",`);
  sb.decreaseIndent();
  sb.appendLine("];");
  sb.appendLine();

  return { source: sb.toString(), diagnostics: v.diagnostics };
}

/** Emit class name for a catalog (CommonHeaders, HttpHeaders, ...). */
export function catalogClassName(catalog: CatalogFilter): string {
  return `${capitalize(catalog)}Headers`;
}

function capitalize(s: string): string {
  if (s.length === 0) return s;
  return s[0]!.toUpperCase() + s.slice(1);
}

function snakeUpper(camel: string): string {
  return camel
    .replace(/([A-Z])/g, "_$1")
    .toUpperCase()
    .replace(/^_/, "");
}

function escapeStringLiteral(value: string): string {
  return value.replace(/\\/g, "\\\\").replace(/"/g, '\\"');
}

function escapeJsDoc(value: string): string {
  return value.replace(/\*\//g, "*\\/");
}

const SPEC_PATH = contractsPath("headers", "headers.spec.json");

const PACKAGE_DIR_FOR_CATALOG: Record<CatalogFilter, string> = {
  common: "headers/common",
  http: "headers/http",
  amqp: "headers/amqp",
  grpc: "headers/grpc",
};

const FILE_BASENAME_FOR_CATALOG: Record<CatalogFilter, string> = {
  common: "common-headers.g.ts",
  http: "http-headers.g.ts",
  amqp: "amqp-headers.g.ts",
  grpc: "grpc-headers.g.ts",
};

function targetPath(catalog: CatalogFilter): string {
  return tsPackagePath(
    PACKAGE_DIR_FOR_CATALOG[catalog],
    "src",
    FILE_BASENAME_FOR_CATALOG[catalog],
  );
}

const ALL_CATALOGS: readonly CatalogFilter[] = [
  "common",
  "http",
  "amqp",
  "grpc",
];

/**
 * Run the headers emitter for one or all catalogs. Per-catalog mtime
 * check skips emit when the output is newer than the spec; pass
 * `force=true` to bypass.
 */
export function runHeadersEmit(
  force = false,
  filter?: CatalogFilter,
): readonly EmitDiagnostic[] {
  const catalogs = filter === undefined ? ALL_CATALOGS : [filter];
  if (!force) {
    const allUpToDate = catalogs.every((c) =>
      isOutputUpToDate(targetPath(c), [SPEC_PATH]),
    );
    if (allUpToDate) return [];
  }
  const loadResult = loadSpec<HeadersSpec>(
    SPEC_PATH,
    DiagnosticIds.HDR_MALFORMED_SPEC,
  );
  if (loadResult.spec === undefined) return loadResult.diagnostics;
  const allDiagnostics: EmitDiagnostic[] = [];
  for (const catalog of catalogs) {
    const result = emitHeadersCatalog(loadResult.spec, catalog);
    allDiagnostics.push(...result.diagnostics);
    if (result.diagnostics.some((d) => d.severity === "error")) continue;
    writeGeneratedFile(targetPath(catalog), result.source);
  }
  return allDiagnostics;
}

const isMain =
  import.meta.url === `file://${process.argv[1]}` ||
  process.argv[1]?.endsWith("headers-emit.ts") === true;
if (isMain) {
  const force = process.argv.includes("--force");
  const targetArg = process.argv.find((a) => a.startsWith("--target="));
  const filter =
    targetArg === undefined
      ? undefined
      : (targetArg.slice("--target=".length) as CatalogFilter);
  const diagnostics = runHeadersEmit(force, filter);
  for (const d of diagnostics) console.error(formatDiagnostic(d));
  if (diagnostics.some((d) => d.severity === "error")) process.exit(1);
}
