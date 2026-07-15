// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

// Compile-time registry reader for scope, audience, error-code, and
// error-category names.
//
// Reads public/contracts/auth-scopes/scopes.spec.json,
// public/contracts/auth-audiences/audiences.spec.json,
// public/contracts/auth-protocol-audiences/protocol-audiences.spec.json, every
// public/contracts/*-error-codes/*.spec.json, and
// public/contracts/error-category/error-category.spec.json at process startup.
// These are the single sources of truth for valid scope / audience /
// protocol-audience / error-code / error-category values.
//
// Resolution uses import.meta.url to locate the repo root regardless of cwd
// (mirrors tools/ts-codegen/src/lib/paths.ts).  The anchor is the compiled
// dist/spec-registry.js file: dist/ → package/ → typescript/ → packages/ →
// public/ → repo-root = 5 ".." steps.
//
// The narrow { name } / { code } / { wire } read-projections here are NOT
// spec-mirror DTOs (rules.md §26.1 applies to full published shapes).  They are
// membership-check projections of a single field from each spec — only the name
// set is needed.

import { existsSync, readFileSync, readdirSync } from "fs";
import { resolve, dirname } from "path";
import { fileURLToPath } from "url";

const here = dirname(fileURLToPath(import.meta.url));
// dist/ → typespec-decorators/ → typescript/ → packages/ → public/ → repo-root
const REPO_ROOT = resolve(here, "..", "..", "..", "..", "..");

/** Public open contracts root (always present in dual-tree monorepo / export). */
function publicContractsPath(...segments: string[]): string {
  return resolve(REPO_ROOT, "public", "contracts", ...segments);
}

/**
 * Private product contracts root (monorepo only). Absent on pure-public
 * export trees — loaders must tolerate missing private/ and still succeed.
 */
function privateContractsPath(...segments: string[]): string {
  return resolve(REPO_ROOT, "private", "contracts", ...segments);
}

/** @deprecated Prefer publicContractsPath; kept as alias for call-site clarity. */
function contractsPath(...segments: string[]): string {
  return publicContractsPath(...segments);
}

/**
 * Union names from a public contracts JSON file with an optional private
 * sibling (same relative path under private/contracts). Private is additive —
 * public remains the baseline open catalog.
 */
function loadUnionNamedSet(
  relDir: string,
  fileName: string,
  arrayKey: string,
  nameField: string,
  shapeHint: string,
): ReadonlySet<string> {
  const names = new Set<string>();

  const loadOne = (absPath: string, label: string): void => {
    if (!existsSync(absPath)) return;
    const raw = readFileSync(absPath, "utf8");
    const spec = JSON.parse(raw) as Record<string, unknown>;
    const arr = spec[arrayKey];
    if (!Array.isArray(arr))
      throw new Error(`${label} has unexpected shape — expected ${shapeHint}`);
    for (const entry of arr) {
      if (entry === null || typeof entry !== "object") continue;
      const value = (entry as Record<string, unknown>)[nameField];
      if (typeof value === "string") names.add(value);
    }
  };

  loadOne(
    publicContractsPath(relDir, fileName),
    `public/contracts/${relDir}/${fileName}`,
  );
  loadOne(
    privateContractsPath(relDir, fileName),
    `private/contracts/${relDir}/${fileName}`,
  );

  if (names.size === 0) {
    throw new Error(
      `no entries loaded for ${relDir}/${fileName} under public|private contracts`,
    );
  }

  return names;
}

// ----------------------------------------------------------------
// Narrow read-projection types (membership check only — not full spec mirrors)
// ----------------------------------------------------------------

interface ProtocolAudiencesSpec {
  readonly protocolAudiences: ReadonlyArray<{ readonly value: string }>;
}

interface ErrorCodesSpec {
  readonly errorCodes: ReadonlyArray<{ readonly code: string }>;
}

interface ErrorCategorySpec {
  readonly categories: ReadonlyArray<{ readonly wire: string }>;
}

// ----------------------------------------------------------------
// Module-level cache — read once per compiler process
// ----------------------------------------------------------------

let _scopeNames: ReadonlySet<string> | undefined;
let _audienceNames: ReadonlySet<string> | undefined;
let _protocolAudienceValues: ReadonlySet<string> | undefined;
let _errorCodeNames: ReadonlySet<string> | undefined;
let _errorCategoryNames: ReadonlySet<string> | undefined;

/** Returns the set of declared scope names from scopes.spec.json (public∪private). */
export function loadScopeNames(): ReadonlySet<string> {
  if (_scopeNames) return _scopeNames;
  _scopeNames = loadUnionNamedSet(
    "auth-scopes",
    "scopes.spec.json",
    "scopes",
    "name",
    "{ scopes: [{ name: string }] }",
  );
  return _scopeNames;
}

/** Returns the set of declared audience names from audiences.spec.json (public∪private). */
export function loadAudienceNames(): ReadonlySet<string> {
  if (_audienceNames) return _audienceNames;
  _audienceNames = loadUnionNamedSet(
    "auth-audiences",
    "audiences.spec.json",
    "audiences",
    "name",
    "{ audiences: [{ name: string }] }",
  );
  return _audienceNames;
}

/**
 * Returns the set of declared PROTOCOL-audience VALUES from
 * protocol-audiences.spec.json (the bare-token aud values d2.internal, d2-edge).
 * These are the universal-receive / self protocol audiences — distinct from the
 * URL-shaped token-exchange targets in audiences.spec.json. `@d2Audience`
 * accepts a value iff it is one of these OR a token-exchange-target name; this
 * is the single source the validator reads instead of a hard-coded literal.
 */
export function loadProtocolAudienceValues(): ReadonlySet<string> {
  if (_protocolAudienceValues) return _protocolAudienceValues;
  const raw = readFileSync(
    contractsPath("auth-protocol-audiences", "protocol-audiences.spec.json"),
    "utf8",
  );
  const spec = JSON.parse(raw) as ProtocolAudiencesSpec;
  if (
    !Array.isArray((spec as { protocolAudiences?: unknown }).protocolAudiences)
  )
    throw new Error(
      "contracts/auth-protocol-audiences/protocol-audiences.spec.json has " +
        "unexpected shape — expected { protocolAudiences: [{ value: string }] }",
    );
  _protocolAudienceValues = new Set(spec.protocolAudiences.map((a) => a.value));
  return _protocolAudienceValues;
}

/**
 * Returns the set of declared error-code strings, aggregated across every
 * `contracts/*-error-codes/*.spec.json` (the generic `error-codes` dir plus any
 * per-domain `<domain>-error-codes` dir). Directory-discovered, NOT a hard-coded
 * list — a new per-domain dir is picked up automatically (mirrors the runtime
 * registry-merge convention). Each spec uses key `errorCodes`, field `code`.
 */
export function loadErrorCodeNames(): ReadonlySet<string> {
  if (_errorCodeNames) return _errorCodeNames;

  // The generic dir is exactly `error-codes`; per-domain dirs are
  // `<domain>-error-codes`. Both forms end in `error-codes`. Scan public and
  // (when present) private contracts trees so monorepo product codes validate.
  const codes = new Set<string>();
  const roots = [
    resolve(REPO_ROOT, "public", "contracts"),
    resolve(REPO_ROOT, "private", "contracts"),
  ];

  for (const contractsRoot of roots) {
    if (!existsSync(contractsRoot)) continue;

    const dirs = readdirSync(contractsRoot, { withFileTypes: true })
      .filter((e) => e.isDirectory() && e.name.endsWith("error-codes"))
      .map((e) => e.name);

    for (const dir of dirs) {
      const dirPath = resolve(contractsRoot, dir);
      const files = readdirSync(dirPath).filter((f) =>
        f.endsWith(".spec.json"),
      );

      for (const file of files) {
        const label = `${contractsRoot.includes("private") ? "private" : "public"}/contracts/${dir}/${file}`;
        const raw = readFileSync(resolve(dirPath, file), "utf8");
        const spec = JSON.parse(raw) as ErrorCodesSpec;

        if (!Array.isArray((spec as { errorCodes?: unknown }).errorCodes))
          throw new Error(
            `${label} has unexpected shape — ` +
              "expected { errorCodes: [{ code: string }] }",
          );

        for (const entry of spec.errorCodes) codes.add(entry.code);
      }
    }
  }

  _errorCodeNames = codes;
  return _errorCodeNames;
}

/**
 * Returns the set of declared ErrorCategory wire strings from
 * contracts/error-category/error-category.spec.json (key `categories`, field
 * `wire`). Loaded from the spec — NOT a frozen constant set — so a newly added
 * category stays in parity automatically.
 */
export function loadErrorCategoryNames(): ReadonlySet<string> {
  if (_errorCategoryNames) return _errorCategoryNames;

  const raw = readFileSync(
    contractsPath("error-category", "error-category.spec.json"),
    "utf8",
  );
  const spec = JSON.parse(raw) as ErrorCategorySpec;

  if (!Array.isArray((spec as { categories?: unknown }).categories))
    throw new Error(
      "contracts/error-category/error-category.spec.json has unexpected shape — " +
        "expected { categories: [{ wire: string }] }",
    );

  _errorCategoryNames = new Set(spec.categories.map((c) => c.wire));
  return _errorCategoryNames;
}

/** Reset module-level cache (used in tests to force a reload). */
export function _resetSpecRegistryCache(): void {
  _scopeNames = undefined;
  _audienceNames = undefined;
  _protocolAudienceValues = undefined;
  _errorCodeNames = undefined;
  _errorCategoryNames = undefined;
}
