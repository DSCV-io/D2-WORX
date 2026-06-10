// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

import {
  AUTH_CONFIG,
  AUTH_FAILURES_CONFIG,
  emitBaseFactoriesCatalog,
  emitErrorCodesCatalog,
  emitFailuresCatalog,
  type ErrorCodesSpec,
  GENERIC_CONFIG,
  GENERIC_FACTORIES_CONFIG,
} from "../src/error-codes-emit.js";

// ---------------------------------------------------------------------------
// Byte-parity golden test: regenerate each catalog IN-MEMORY from the real
// spec + the real en-US.json key set and assert it equals the committed
// `.g.ts` bytes (LF-normalized). Makes the byte-parity invariant a TEST rather
// than a manual `git diff` (makes the byte-parity invariant a CI test rather
// than a discipline-only check).
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

function enUsKeySet(): ReadonlySet<string> {
  const parsed = readJson<Record<string, unknown>>(
    "contracts",
    "messages",
    "en-US.json",
  );
  const keys = new Set<string>();
  for (const key of Object.keys(parsed)) if (key !== "$schema") keys.add(key);
  return keys;
}

const genericSpec = readJson<ErrorCodesSpec>(
  "contracts",
  "error-codes",
  "error-codes.spec.json",
);
const authSpec = readJson<ErrorCodesSpec>(
  "contracts",
  "auth-error-codes",
  "auth-error-codes.spec.json",
);

describe("error-codes byte-parity (in-memory regen == committed .g.ts)", () => {
  it("generic error-codes.g.ts is byte-identical to committed", () => {
    const r = emitErrorCodesCatalog(genericSpec, GENERIC_CONFIG);
    expect(r.diagnostics).toEqual([]);
    const committed = readGenerated(
      "server",
      "shared",
      "typescript",
      "result",
      "src",
      "error-codes.g.ts",
    );
    expect(r.source).toBe(committed);
  });

  it("auth-error-codes.g.ts is byte-identical to committed", () => {
    const r = emitErrorCodesCatalog(authSpec, AUTH_CONFIG, enUsKeySet());
    expect(r.diagnostics).toEqual([]);
    const committed = readGenerated(
      "server",
      "shared",
      "typescript",
      "auth",
      "abstractions",
      "src",
      "auth-error-codes.g.ts",
    );
    expect(r.source).toBe(committed);
  });

  it("auth-failures.g.ts is byte-identical to committed (the TK-constant-corrected output)", () => {
    const r = emitFailuresCatalog(
      authSpec,
      AUTH_CONFIG,
      AUTH_FAILURES_CONFIG,
      enUsKeySet(),
    );
    expect(r.diagnostics).toEqual([]);
    const committed = readGenerated(
      "server",
      "shared",
      "typescript",
      "auth",
      "abstractions",
      "src",
      "auth-failures.g.ts",
    );
    expect(r.source).toBe(committed);
  });

  // long test description — cannot wrap
  it("generic factories.g.ts is byte-identical to committed (the base/constructing factories)", () => {
    const r = emitBaseFactoriesCatalog(
      genericSpec,
      GENERIC_CONFIG,
      GENERIC_FACTORIES_CONFIG,
      enUsKeySet(),
    );
    expect(r.diagnostics).toEqual([]);
    const committed = readGenerated(
      "server",
      "shared",
      "typescript",
      "result",
      "src",
      "factories.g.ts",
    );
    expect(r.source).toBe(committed);
  });
});
