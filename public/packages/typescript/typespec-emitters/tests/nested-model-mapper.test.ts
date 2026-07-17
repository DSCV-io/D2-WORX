// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

// Direct-unit tests for nested-model-mapper.ts — the shared nested-model /
// array-of-model sub-mapper emission used by the gRPC service + client emitters.
//
// Covers: depth-N transitive collection + dedup + cycle termination, the two
// field-RHS builders (outbound / inbound) in single + array + optional shapes,
// the per-model sub-mapper extension-block emission (both directions), and the
// empty-nested-model edge case.

import { describe, it, expect } from "vitest";
import type { FieldInfo, NestedModel } from "../src/lib/model-walk.js";
import {
  collectFieldNestedModels,
  buildDtoToProtoNested,
  buildProtoToDtoNested,
  emitNestedModelMapperHelpers,
} from "../src/lib/nested-model-mapper.js";

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function scalar(name: string): FieldInfo {
  return {
    name,
    csName: name.charAt(0).toUpperCase() + name.slice(1),
    csType: "string",
    tsName: name,
    tsType: "string",
    protoType: "string",
    repeated: false,
    optional: false,
    redactReason: undefined,
  };
}

function single(
  name: string,
  nested: NestedModel,
  optional = false,
): FieldInfo {
  return {
    name,
    csName: name.charAt(0).toUpperCase() + name.slice(1),
    csType: optional ? `${nested.name}?` : nested.name,
    tsName: name,
    tsType: nested.name,
    protoType: undefined,
    repeated: false,
    optional,
    redactReason: undefined,
    nested,
  };
}

function array(name: string, nested: NestedModel, optional = false): FieldInfo {
  return {
    name,
    csName: name.charAt(0).toUpperCase() + name.slice(1),
    csType: `IReadOnlyList<${nested.name}>`,
    tsName: name,
    tsType: `readonly ${nested.name}[]`,
    protoType: undefined,
    repeated: true,
    optional,
    redactReason: undefined,
    nested,
  };
}

// ---------------------------------------------------------------------------
// collectFieldNestedModels — transitive closure, dedup, cycle termination
// ---------------------------------------------------------------------------

describe("collectFieldNestedModels_TransitiveClosure", () => {
  it("collects a single nested model", () => {
    const line: NestedModel = { name: "Line", fields: [scalar("status")] };
    const got = collectFieldNestedModels([scalar("id"), single("line", line)]);
    expect(got.map((m) => m.name)).toEqual(["Line"]);
  });

  it("collects depth-N: a nested model referenced inside a nested model (transitively)", () => {
    const part: NestedModel = { name: "Part", fields: [scalar("code")] };
    const widget: NestedModel = {
      name: "Widget",
      fields: [scalar("name"), array("parts", part)],
    };
    const got = collectFieldNestedModels([single("widget", widget, true)]);
    // BOTH the depth-2 Widget AND the depth-3 Part are collected.
    expect(got.map((m) => m.name).sort()).toEqual(["Part", "Widget"]);
  });

  it("dedups a model referenced by two fields (collected once)", () => {
    const c: NestedModel = { name: "C", fields: [scalar("x")] };
    const got = collectFieldNestedModels([single("a", c), single("b", c)]);
    expect(got).toHaveLength(1);
    expect(got[0]!.name).toBe("C");
  });

  it("terminates on a self-referential model (collected exactly once)", () => {
    // A model that references itself — the dedup guard runs BEFORE recursion, so
    // the walk terminates instead of looping forever.
    const node: NestedModel = { name: "Node", fields: [scalar("v")] };
    // Inject a self-reference field after construction.
    (node.fields as FieldInfo[]).push(single("next", node, true));
    const got = collectFieldNestedModels([single("root", node)]);
    expect(got.map((m) => m.name)).toEqual(["Node"]);
  });

  it("returns [] for a field list with no nested models", () => {
    expect(collectFieldNestedModels([scalar("a"), scalar("b")])).toEqual([]);
  });
});

// ---------------------------------------------------------------------------
// buildDtoToProtoNested — outbound field RHS
// ---------------------------------------------------------------------------

describe("buildDtoToProtoNested_Outbound", () => {
  const line: NestedModel = { name: "Line", fields: [scalar("status")] };

  it("scalar field → undefined (caller handles it)", () => {
    expect(buildDtoToProtoNested(scalar("id"), "output")).toBeUndefined();
  });

  it("single nested model → null-guarded assign to ToProto<Model>() (PascalCase prop)", () => {
    const got = buildDtoToProtoNested(single("line", line), "output");
    expect(got).toEqual({
      kind: "assign",
      expr: "output.Line is null ? null : output.Line.ToProtoLine()",
    });
  });

  it("array-of-model → collectionInit with .Select(ToProto<Model>())", () => {
    const got = buildDtoToProtoNested(array("lines", line), "output");
    expect(got).toEqual({
      kind: "collectionInit",
      expr: "output.Lines.Select(x => x.ToProtoLine())",
    });
  });

  it("OPTIONAL array-of-model → null-coalesced collectionInit (no NRE on null)", () => {
    const got = buildDtoToProtoNested(array("lines", line, true), "output");
    expect(got).toEqual({
      kind: "collectionInit",
      expr: "(output.Lines ?? []).Select(x => x.ToProtoLine())",
    });
  });
});

// ---------------------------------------------------------------------------
// buildProtoToDtoNested — inbound ctor arg
// ---------------------------------------------------------------------------

describe("buildProtoToDtoNested_Inbound", () => {
  const line: NestedModel = { name: "Line", fields: [scalar("status")] };

  it("scalar field → undefined (caller handles it)", () => {
    expect(buildProtoToDtoNested(scalar("id"), "data")).toBeUndefined();
  });

  it("single nested model → null-guarded To<Model>() (PascalCase prop)", () => {
    expect(buildProtoToDtoNested(single("line", line), "data")).toBe(
      "data.Line is null ? null : data.Line.ToLine()",
    );
  });

  it("array-of-model → .Select(To<Model>()).ToList()", () => {
    expect(buildProtoToDtoNested(array("lines", line), "data")).toBe(
      "data.Lines.Select(x => x.ToLine()).ToList()",
    );
  });
});

// ---------------------------------------------------------------------------
// emitNestedModelMapperHelpers — per-model extension blocks (both directions)
// ---------------------------------------------------------------------------

describe("emitNestedModelMapperHelpers_BothDirections", () => {
  const naming = {
    dtoTypeName: (m: string) => `Dto.${m}`,
    protoTypeName: (m: string) => `Proto.${m}`,
  };

  function emit(models: readonly NestedModel[]): string {
    const lines: string[] = [];
    emitNestedModelMapperHelpers((l) => lines.push(l), models, naming);

    return lines.join("\n");
  }

  it("emits a DTO→proto block AND a proto→DTO block per model", () => {
    const c = emit([{ name: "Line", fields: [scalar("status")] }]);
    expect(c).toContain("extension(Dto.Line source)");
    expect(c).toContain("internal Proto.Line ToProtoLine()");
    expect(c).toContain("extension(Proto.Line source)");
    expect(c).toContain("internal Dto.Line ToLine()");
    // The scalar field maps directly in both directions.
    expect(c).toContain("Status = source.Status,");
    expect(c).toContain("return new Dto.Line(source.Status);");
  });

  it("a nested-in-nested sub-mapper recurses through the deeper model's sub-mapper", () => {
    const part: NestedModel = { name: "Part", fields: [scalar("code")] };
    const widget: NestedModel = {
      name: "Widget",
      fields: [array("parts", part)],
    };
    // Both Widget AND Part blocks would be emitted (caller passes the closure); here
    // assert the Widget block recurses into Part's sub-mapper.
    const c = emit([widget, part]);
    expect(c).toContain(
      "Parts = { source.Parts.Select(x => x.ToProtoPart()) },",
    );
    expect(c).toContain("source.Parts.Select(x => x.ToPart()).ToList()");
  });

  it("an empty nested model emits a parameterless ctor / object-init in both directions", () => {
    const c = emit([{ name: "Empty", fields: [] }]);
    expect(c).toContain("return new Proto.Empty();");
    expect(c).toContain("return new Dto.Empty();");
  });

  it('a nested DateTimeOffset field maps outbound via ToString("O") (ISO-8601 round-trip)', () => {
    const temporal: FieldInfo = {
      name: "notAfter",
      csName: "NotAfter",
      csType: "DateTimeOffset",
      tsName: "notAfter",
      tsType: "string",
      protoType: "string",
      repeated: false,
      optional: false,
      redactReason: undefined,
    };
    const c = emit([{ name: "Window", fields: [temporal] }]);
    expect(c).toContain('NotAfter = source.NotAfter.ToString("O"),');
  });

  it("emits nothing for an empty model list", () => {
    expect(emit([])).toBe("");
  });
});
