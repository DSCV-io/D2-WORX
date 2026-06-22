// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Compile-time registry reader for scope, audience, error-code, and
// error-category names.
//
// Reads contracts/auth-scopes/scopes.spec.json,
// contracts/auth-audiences/audiences.spec.json, every
// contracts/*-error-codes/*.spec.json, and
// contracts/error-category/error-category.spec.json at process startup.
// These are the single sources of truth for valid scope / audience / error-code
// / error-category values.
//
// Resolution uses import.meta.url to locate the repo root regardless of cwd
// (mirrors tools/ts-codegen/src/lib/paths.ts).  The anchor is the compiled
// dist/spec-registry.js file: dist/ → package/ → typescript/ → shared/ →
// server/ → repo-root = 5 ".." steps.
//
// The narrow { name } / { code } / { wire } read-projections here are NOT
// spec-mirror DTOs (rules.md §26.1 applies to full published shapes).  They are
// membership-check projections of a single field from each spec — only the name
// set is needed.

import { readFileSync, readdirSync } from "fs";
import { resolve, dirname } from "path";
import { fileURLToPath } from "url";

const here = dirname(fileURLToPath(import.meta.url));
// dist/ → typespec-decorators/ → typescript/ → shared/ → server/ → repo-root
const REPO_ROOT = resolve(here, "..", "..", "..", "..", "..");

function contractsPath(...segments: string[]): string {
  return resolve(REPO_ROOT, "contracts", ...segments);
}

// ----------------------------------------------------------------
// Narrow read-projection types (membership check only — not full spec mirrors)
// ----------------------------------------------------------------

interface ScopesSpec {
  readonly scopes: ReadonlyArray<{ readonly name: string }>;
}

interface AudiencesSpec {
  readonly audiences: ReadonlyArray<{ readonly name: string }>;
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
let _errorCodeNames: ReadonlySet<string> | undefined;
let _errorCategoryNames: ReadonlySet<string> | undefined;

/** Returns the set of declared scope names from scopes.spec.json. */
export function loadScopeNames(): ReadonlySet<string> {
  if (_scopeNames) return _scopeNames;
  const raw = readFileSync(
    contractsPath("auth-scopes", "scopes.spec.json"),
    "utf8",
  );
  const spec = JSON.parse(raw) as ScopesSpec;
  if (!Array.isArray((spec as { scopes?: unknown }).scopes))
    throw new Error(
      "contracts/auth-scopes/scopes.spec.json has unexpected shape — " +
        "expected { scopes: [{ name: string }] }",
    );
  _scopeNames = new Set(spec.scopes.map((s) => s.name));
  return _scopeNames;
}

/** Returns the set of declared audience names from audiences.spec.json. */
export function loadAudienceNames(): ReadonlySet<string> {
  if (_audienceNames) return _audienceNames;
  const raw = readFileSync(
    contractsPath("auth-audiences", "audiences.spec.json"),
    "utf8",
  );
  const spec = JSON.parse(raw) as AudiencesSpec;
  if (!Array.isArray((spec as { audiences?: unknown }).audiences))
    throw new Error(
      "contracts/auth-audiences/audiences.spec.json has unexpected shape — " +
        "expected { audiences: [{ name: string }] }",
    );
  _audienceNames = new Set(spec.audiences.map((a) => a.name));
  return _audienceNames;
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

  const contractsRoot = resolve(REPO_ROOT, "contracts");
  // The generic dir is exactly `error-codes`; per-domain dirs are
  // `<domain>-error-codes`. Both forms end in `error-codes`.
  const dirs = readdirSync(contractsRoot, { withFileTypes: true })
    .filter((e) => e.isDirectory() && e.name.endsWith("error-codes"))
    .map((e) => e.name);

  const codes = new Set<string>();
  for (const dir of dirs) {
    const files = readdirSync(contractsPath(dir)).filter((f) =>
      f.endsWith(".spec.json"),
    );

    for (const file of files) {
      const raw = readFileSync(contractsPath(dir, file), "utf8");
      const spec = JSON.parse(raw) as ErrorCodesSpec;

      if (!Array.isArray((spec as { errorCodes?: unknown }).errorCodes))
        throw new Error(
          `contracts/${dir}/${file} has unexpected shape — ` +
            "expected { errorCodes: [{ code: string }] }",
        );

      for (const entry of spec.errorCodes) codes.add(entry.code);
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
  _errorCodeNames = undefined;
  _errorCategoryNames = undefined;
}
