// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import {
  emitProtocolAudiences,
  type ProtocolAudiencesSpec,
} from "../src/protocol-audiences-emit.js";

const spec: ProtocolAudiencesSpec = {
  protocolAudiences: [
    {
      name: "D2_INTERNAL_AUDIENCE",
      value: "d2.internal",
      description: "The universal internal receive audience.",
    },
    {
      name: "D2_EDGE_SELF_AUDIENCE",
      value: "d2-edge",
      description: "The Edge self-audience.",
    },
  ],
};

describe("emitProtocolAudiences", () => {
  it("emits the ProtocolAudiences const-object with both values", () => {
    const r = emitProtocolAudiences(spec);
    expect(r.diagnostics).toEqual([]);
    expect(r.source).toContain("export const ProtocolAudiences = {");
    expect(r.source).toContain('D2_INTERNAL_AUDIENCE: "d2.internal"');
    expect(r.source).toContain('D2_EDGE_SELF_AUDIENCE: "d2-edge"');
    expect(r.source).toContain("} as const;");
  });

  it("emits the ProtocolAudience type alias", () => {
    const r = emitProtocolAudiences(spec);
    expect(r.source).toContain(
      "export type ProtocolAudience = (typeof ProtocolAudiences)[keyof typeof ProtocolAudiences];",
    );
  });

  it("emits ALL_PROTOCOL_AUDIENCES with the bare-token values", () => {
    const r = emitProtocolAudiences(spec);
    expect(r.source).toContain(
      "export const ALL_PROTOCOL_AUDIENCES: readonly string[] = [",
    );
    expect(r.source).toContain('"d2-edge"');
    expect(r.source).toContain('"d2.internal"');
  });

  it("sorts deterministically by name regardless of input order", () => {
    // D2_EDGE_SELF_AUDIENCE sorts before D2_INTERNAL_AUDIENCE — assert that
    // ordering holds even though the spec lists internal first.
    const r = emitProtocolAudiences(spec);
    const edgeIdx = r.source.indexOf("D2_EDGE_SELF_AUDIENCE:");
    const internalIdx = r.source.indexOf("D2_INTERNAL_AUDIENCE:");
    expect(edgeIdx).toBeGreaterThanOrEqual(0);
    expect(internalIdx).toBeGreaterThan(edgeIdx);
  });

  it("flags a duplicate protocol-audience name (D2PAUD001)", () => {
    const r = emitProtocolAudiences({
      protocolAudiences: [
        spec.protocolAudiences[0]!,
        spec.protocolAudiences[0]!,
      ],
    });
    expect(r.diagnostics[0]?.id).toBe("D2PAUD001");
    expect(r.source).toBe("");
  });

  it("flags a non-SCREAMING_SNAKE name (D2PAUD002)", () => {
    const r = emitProtocolAudiences({
      protocolAudiences: [{ name: "d2Internal", value: "d2.internal" }],
    });
    expect(r.diagnostics[0]?.id).toBe("D2PAUD002");
  });

  it("flags a duplicate value (D2PAUD003)", () => {
    const r = emitProtocolAudiences({
      protocolAudiences: [
        { name: "FIRST", value: "same.value" },
        { name: "SECOND", value: "same.value" },
      ],
    });
    expect(r.diagnostics.some((d) => d.id === "D2PAUD003")).toBe(true);
  });

  it("flags an empty value (D2PAUD004)", () => {
    const r = emitProtocolAudiences({
      protocolAudiences: [{ name: "EMPTY_ONE", value: "" }],
    });
    expect(r.diagnostics.some((d) => d.id === "D2PAUD004")).toBe(true);
  });

  it("emits a description-less entry with no per-entry JSDoc doc comment", () => {
    // A VALID entry (SCREAMING_SNAKE name, non-empty unique value) carrying no
    // `description` — exercises the false arm of the emit loop's
    // `description !== undefined && length > 0` guard: no `/** … */` line is
    // written immediately before the entry.
    const r = emitProtocolAudiences({
      protocolAudiences: [{ name: "NO_DESC_AUDIENCE", value: "d2.nodesc" }],
    });
    expect(r.diagnostics).toEqual([]);
    expect(r.source).toContain('NO_DESC_AUDIENCE: "d2.nodesc"');

    const lines = r.source.split("\n");
    const entryIdx = lines.findIndex((l) =>
      l.includes('NO_DESC_AUDIENCE: "d2.nodesc"'),
    );
    expect(entryIdx).toBeGreaterThan(0);
    expect(lines[entryIdx - 1]).not.toContain("/**");
  });
});
