// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

import {
  emitActorEntryFile,
  emitCallPathEntryFile,
  emitEnumFile,
  ENUMS,
} from "../src/auth-context-emit.js";

// ---------------------------------------------------------------------------
// Byte-parity golden test: regenerate the auth-context enum + entry-type
// `.g.ts` files IN-MEMORY via the real pure emitters and assert each equals
// the committed bytes (LF-normalized). Each `.g.ts` carries a deliberate-drift
// proof so the byte-compare is non-vacuous. Mirrors
// tools/ts-codegen/tests/error-codes-byte-parity.test.ts.
// ---------------------------------------------------------------------------

const here = dirname(fileURLToPath(import.meta.url));
const repoRoot = resolve(here, "..", "..", "..");

function readGenerated(...parts: string[]): string {
  // LF-normalize so a checkout CRLF setting can't produce a spurious mismatch.
  return readFileSync(resolve(repoRoot, ...parts), "utf8").replace(
    /\r\n/g,
    "\n",
  );
}

const CTX_SRC = [
  "packages",
  "typescript",
  "auth",
  "context-abstractions",
  "src",
];

describe("auth-context byte-parity (in-memory regen == committed .g.ts)", () => {
  it("enums/call-path-kind.g.ts is byte-identical to committed", () => {
    const src = emitEnumFile("CallPathKind", ENUMS["call-path-kind"]);
    expect(src).toBe(readGenerated(...CTX_SRC, "enums", "call-path-kind.g.ts"));
  });

  it("enums/request-origin.g.ts is byte-identical to committed", () => {
    const src = emitEnumFile("RequestOrigin", ENUMS["request-origin"]);
    expect(src).toBe(readGenerated(...CTX_SRC, "enums", "request-origin.g.ts"));
  });

  it("types/actor-entry.g.ts is byte-identical to committed", () => {
    expect(emitActorEntryFile()).toBe(
      readGenerated(...CTX_SRC, "types", "actor-entry.g.ts"),
    );
  });

  it("types/call-path-entry.g.ts is byte-identical to committed", () => {
    expect(emitCallPathEntryFile()).toBe(
      readGenerated(...CTX_SRC, "types", "call-path-entry.g.ts"),
    );
  });

  it("deliberate-drift proof: an extra enum member changes the enum output", () => {
    const committedCallPathKind = readGenerated(
      ...CTX_SRC,
      "enums",
      "call-path-kind.g.ts",
    );
    const drifted = emitEnumFile("CallPathKind", [
      ...ENUMS["call-path-kind"],
      ["DriftMember", "Deliberate drift member."],
    ]);
    expect(drifted).not.toBe(committedCallPathKind);

    const committedRequestOrigin = readGenerated(
      ...CTX_SRC,
      "enums",
      "request-origin.g.ts",
    );
    const driftedOrigin = emitEnumFile("RequestOrigin", [
      ...ENUMS["request-origin"],
      ["DriftMember", "Deliberate drift member."],
    ]);
    expect(driftedOrigin).not.toBe(committedRequestOrigin);
  });

  it("deliberate-drift proof: the entry-type byte-compare is exact (non-vacuous)", () => {
    // The actor/call-path entry emitters take no spec input to mutate; prove the
    // byte-compare is exact by asserting a one-token corruption of the committed
    // bytes does NOT equal the fresh emit (i.e. real drift would be caught).
    const actorCommitted = readGenerated(
      ...CTX_SRC,
      "types",
      "actor-entry.g.ts",
    );
    expect(emitActorEntryFile()).not.toBe(
      actorCommitted.replace("readonly kind:", "readonly kindDrift:"),
    );

    const callPathCommitted = readGenerated(
      ...CTX_SRC,
      "types",
      "call-path-entry.g.ts",
    );
    expect(emitCallPathEntryFile()).not.toBe(
      callPathCommitted.replace("readonly id:", "readonly idDrift:"),
    );
  });
});
