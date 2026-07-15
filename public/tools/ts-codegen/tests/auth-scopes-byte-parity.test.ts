// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

import { emitAuthScopes, type ScopesSpec } from "../src/auth-scopes-emit.js";

// ---------------------------------------------------------------------------
// Byte-parity golden test: regenerate scopes.g.ts IN-MEMORY from the real
// contracts/auth-scopes/scopes.spec.json and assert it equals the committed
// `.g.ts` bytes (LF-normalized). A deliberate-drift proof mutates a spec input
// and asserts the output changes, so the byte-compare is non-vacuous. Mirrors
// tools/ts-codegen/tests/error-codes-byte-parity.test.ts.
// ---------------------------------------------------------------------------

const here = dirname(fileURLToPath(import.meta.url));
const repoRoot = resolve(here, "..", "..", "..");

function readJson<T>(...parts: string[]): T {
  return JSON.parse(readFileSync(resolve(repoRoot, ...parts), "utf8")) as T;
}

function readGenerated(...parts: string[]): string {
  // LF-normalize so a checkout CRLF setting can't produce a spurious mismatch.
  return readFileSync(resolve(repoRoot, ...parts), "utf8").replace(
    /\r\n/g,
    "\n",
  );
}

const spec = readJson<ScopesSpec>(
  "contracts",
  "auth-scopes",
  "scopes.spec.json",
);

const SCOPES_G_TS = [
  "packages",
  "typescript",
  "auth",
  "abstractions",
  "src",
  "scopes.g.ts",
];

describe("auth-scopes byte-parity (in-memory regen == committed .g.ts)", () => {
  it("scopes.g.ts is byte-identical to committed", () => {
    const r = emitAuthScopes(spec);
    expect(r.diagnostics).toEqual([]);
    expect(r.source).toBe(readGenerated(...SCOPES_G_TS));
  });

  it("deliberate-drift proof: renaming a scope changes the output (non-vacuous)", () => {
    const committed = readGenerated(...SCOPES_G_TS);
    const drifted: ScopesSpec = {
      ...spec,
      scopes: spec.scopes.map((s, i) =>
        i === 0 ? { ...s, name: `${s.name}.drift_marker` } : s,
      ),
    };
    const r = emitAuthScopes(drifted);
    expect(r.diagnostics).toEqual([]);
    expect(r.source).not.toBe(committed);
  });
});
