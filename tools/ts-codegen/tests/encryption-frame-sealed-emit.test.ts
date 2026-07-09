// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";

import {
  emitSealedFrame,
  validateSealedFrameSpec,
  type SealedFrameSpec,
} from "../src/encryption-frame-sealed-emit.js";
import { DiagnosticIds } from "../src/lib/diagnostics.js";

const CONSTRAINTS = {
  minKidLength: 1,
  maxKidLength: 64,
  ephPubLengthPrefixSize: 2,
  maxEphPubLength: 256,
  nonceLength: 12,
  tagLength: 16,
  minFrameSize: 34,
} as const;

function makeSpec(
  version: number,
  fields: SealedFrameSpec["fields"],
): SealedFrameSpec {
  return { version, fields, constraints: CONSTRAINTS };
}

describe("encryption-frame-sealed emitter", () => {
  it("emits fields and the sealed constraint set for a valid spec", () => {
    const spec = makeSpec(2, [
      {
        constName: "VERSION",
        offset: 0,
        length: 1,
        kind: "byte_fixed",
        doc: "Version doc.",
      },
      {
        constName: "EPH_PUB_LENGTH",
        offset: -1,
        length: 2,
        kind: "byte_fixed",
        doc: "Prefix doc.",
      },
      {
        constName: "EPH_PUB",
        offset: -1,
        length: -1,
        kind: "variable_binary_u16be",
        doc: "Eph pub doc.",
      },
    ]);

    const result = emitSealedFrame(spec);

    expect(result.diagnostics).toEqual([]);
    expect(result.source).toContain("export const SealedFrame = {");
    expect(result.source).toContain("CURRENT_VERSION: 2,");
    expect(result.source).toContain("VERSION_OFFSET: 0,");
    expect(result.source).toContain("EPH_PUB_LENGTH_LENGTH: 2,");
    expect(result.source).toContain("EPH_PUB_OFFSET: -1,");
    expect(result.source).toContain(
      "CONSTRAINT_EPH_PUB_LENGTH_PREFIX_SIZE: 2,",
    );
    expect(result.source).toContain("CONSTRAINT_MAX_EPH_PUB_LENGTH: 256,");
    expect(result.source).toContain("CONSTRAINT_NONCE_LENGTH: 12,");
    expect(result.source).toContain("CONSTRAINT_TAG_LENGTH: 16,");
    expect(result.source).toContain("CONSTRAINT_MIN_FRAME_SIZE: 34,");
  });

  it("run-twice determinism — identical source for identical input", () => {
    const spec = makeSpec(2, [
      {
        constName: "VERSION",
        offset: 0,
        length: 1,
        kind: "byte_fixed",
        doc: "doc",
      },
    ]);

    expect(emitSealedFrame(spec).source).toBe(emitSealedFrame(spec).source);
  });

  // -----------------------------------------------------------------
  // Deliberate-drift fail paths (mirror the .NET SealedFrameEmitter).
  // -----------------------------------------------------------------

  it("rejects version 1 (the symmetric frame's discriminator)", () => {
    const spec = makeSpec(1, []);

    const v = validateSealedFrameSpec(spec);

    expect(v.diagnostics).toHaveLength(1);
    expect(v.diagnostics[0]!.id).toBe(DiagnosticIds.EFS_INVALID_VERSION);
    expect(emitSealedFrame(spec).source).toBe("");
  });

  it("rejects a duplicate constName", () => {
    const spec = makeSpec(2, [
      { constName: "X", offset: 0, length: 1, kind: "byte_fixed", doc: "d" },
      { constName: "X", offset: 1, length: 1, kind: "byte_fixed", doc: "d" },
    ]);

    const v = validateSealedFrameSpec(spec);

    expect(
      v.diagnostics.some(
        (d) => d.id === DiagnosticIds.EFS_DUPLICATE_FIELD_NAME,
      ),
    ).toBe(true);
  });

  it("rejects overlapping fixed-offset fields", () => {
    const spec = makeSpec(2, [
      { constName: "A", offset: 0, length: 4, kind: "byte_fixed", doc: "d" },
      { constName: "B", offset: 2, length: 4, kind: "byte_fixed", doc: "d" },
    ]);

    const v = validateSealedFrameSpec(spec);

    expect(
      v.diagnostics.some((d) => d.id === DiagnosticIds.EFS_OVERLAPPING_FIELDS),
    ).toBe(true);
  });

  it("rejects a zero length", () => {
    const spec = makeSpec(2, [
      { constName: "A", offset: 0, length: 0, kind: "byte_fixed", doc: "d" },
    ]);

    const v = validateSealedFrameSpec(spec);

    expect(
      v.diagnostics.some((d) => d.id === DiagnosticIds.EFS_INVALID_LENGTH),
    ).toBe(true);
  });

  it("rejects an unknown field kind", () => {
    const spec = makeSpec(2, [
      {
        constName: "A",
        offset: 0,
        length: 1,
        kind: "hexadecimal_string",
        doc: "d",
      },
    ]);

    const v = validateSealedFrameSpec(spec);

    expect(
      v.diagnostics.some((d) => d.id === DiagnosticIds.EFS_UNKNOWN_FIELD_KIND),
    ).toBe(true);
  });

  it("rejects a variable_binary_u16be field without its 2-byte length prefix", () => {
    const spec = makeSpec(2, [
      {
        constName: "VERSION",
        offset: 0,
        length: 1,
        kind: "byte_fixed",
        doc: "d",
      },
      {
        constName: "PAYLOAD",
        offset: -1,
        length: -1,
        kind: "variable_binary_u16be",
        doc: "d",
      },
    ]);

    const v = validateSealedFrameSpec(spec);

    expect(
      v.diagnostics.some(
        (d) => d.id === DiagnosticIds.EFS_BINARY_LENGTH_PREFIX_MISSING,
      ),
    ).toBe(true);
  });

  it("rejects a variable_binary_u16be field behind a wrong-width prefix", () => {
    const spec = makeSpec(2, [
      {
        constName: "PREFIX",
        offset: -1,
        length: 1,
        kind: "byte_fixed",
        doc: "d",
      },
      {
        constName: "PAYLOAD",
        offset: -1,
        length: -1,
        kind: "variable_binary_u16be",
        doc: "d",
      },
    ]);

    const v = validateSealedFrameSpec(spec);

    expect(
      v.diagnostics.some(
        (d) => d.id === DiagnosticIds.EFS_BINARY_LENGTH_PREFIX_MISSING,
      ),
    ).toBe(true);
  });

  it("accepts a variable_binary_u16be field behind its 2-byte prefix", () => {
    const spec = makeSpec(2, [
      {
        constName: "PREFIX",
        offset: -1,
        length: 2,
        kind: "byte_fixed",
        doc: "d",
      },
      {
        constName: "PAYLOAD",
        offset: -1,
        length: -1,
        kind: "variable_binary_u16be",
        doc: "d",
      },
    ]);

    const v = validateSealedFrameSpec(spec);

    expect(v.diagnostics).toEqual([]);
    expect(v.fields).toHaveLength(2);
  });

  it("rejects a variable_binary_u16be field as the very first field", () => {
    // The new-kind structural rule walks each field looking at its predecessor;
    // when the binary field is at index 0 there IS no predecessor — exercises the
    // `i > 0 ? … : undefined` false arm (previous === undefined → prefix missing).
    const spec = makeSpec(2, [
      {
        constName: "PAYLOAD",
        offset: -1,
        length: -1,
        kind: "variable_binary_u16be",
        doc: "d",
      },
    ]);

    const v = validateSealedFrameSpec(spec);

    expect(
      v.diagnostics.some(
        (d) => d.id === DiagnosticIds.EFS_BINARY_LENGTH_PREFIX_MISSING,
      ),
    ).toBe(true);
  });

  it("accepts two non-overlapping fixed-offset fields", () => {
    // Two fixed-offset fields whose byte ranges abut but do not overlap
    // (A=[0,2), B=[2,4)) — exercises the false arm of the overlap predicate
    // `a.offset < bEnd && b.offset < aEnd` (b.offset === aEnd → not overlapping).
    const spec = makeSpec(2, [
      { constName: "A", offset: 0, length: 2, kind: "byte_fixed", doc: "d" },
      { constName: "B", offset: 2, length: 2, kind: "byte_fixed", doc: "d" },
    ]);

    const v = validateSealedFrameSpec(spec);

    expect(
      v.diagnostics.some((d) => d.id === DiagnosticIds.EFS_OVERLAPPING_FIELDS),
    ).toBe(false);
  });
});
