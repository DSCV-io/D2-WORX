// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Compile-time registry reader for scope and audience names.
//
// Reads contracts/auth-scopes/scopes.spec.json and
// contracts/auth-audiences/audiences.spec.json at process startup.
// These are the single sources of truth for valid scope and audience values.
//
// Resolution uses import.meta.url to locate the repo root regardless of cwd
// (mirrors tools/ts-codegen/src/lib/paths.ts).  The anchor is the compiled
// dist/spec-registry.js file: dist/ → package/ → typescript/ → shared/ →
// server/ → repo-root = 5 ".." steps.
//
// The narrow { name: string } read-projections here are NOT spec-mirror DTOs
// (rules.md §26.1 applies to full published shapes).  They are membership-check
// projections of a single field from each spec — only the name set is needed.

import { readFileSync } from "fs";
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

// ----------------------------------------------------------------
// Module-level cache — read once per compiler process
// ----------------------------------------------------------------

let _scopeNames: ReadonlySet<string> | undefined;
let _audienceNames: ReadonlySet<string> | undefined;

/** Returns the set of declared scope names from scopes.spec.json. */
export function loadScopeNames(): ReadonlySet<string> {
  if (_scopeNames) return _scopeNames;
  const raw = readFileSync(
    contractsPath("auth-scopes", "scopes.spec.json"),
    "utf8",
  );
  const spec = JSON.parse(raw) as ScopesSpec;
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
  _audienceNames = new Set(spec.audiences.map((a) => a.name));
  return _audienceNames;
}

/** Reset module-level cache (used in tests to force a reload). */
export function _resetSpecRegistryCache(): void {
  _scopeNames = undefined;
  _audienceNames = undefined;
}
