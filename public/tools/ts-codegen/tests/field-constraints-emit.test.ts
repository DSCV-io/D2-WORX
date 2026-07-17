// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import {
  emitConstraints,
  emitTaxonomy,
  type FieldConstraintsSpec,
  runFieldConstraintsEmit,
  validateConstraints,
  validateEnums,
} from "../src/field-constraints-emit.js";

const validSpec: FieldConstraintsSpec = {
  constraints: [
    { name: "EMAIL_MAX", value: 254, doc: "Email max." },
    { name: "PHONE_MIN_DIGITS", value: 7, doc: "Phone digit floor." },
  ],
  enums: [
    {
      name: "BiologicalSex",
      backing: "byte",
      doc: "Sex.",
      members: [
        { name: "Male", doc: "M." },
        { name: "Female", doc: "F." },
      ],
    },
  ],
};

describe("validateConstraints", () => {
  it("happy path returns all entries with no diagnostics", () => {
    const v = validateConstraints(validSpec);
    expect(v.entries).toHaveLength(2);
    expect(v.diagnostics).toEqual([]);
  });

  it.each(["lowercase", "", "9NOPE", "   ", "Has-Dash"])(
    "flags invalid constant name '%s'",
    (name) => {
      const v = validateConstraints({
        ...validSpec,
        constraints: [{ name, value: 1, doc: "x" }],
      });
      expect(v.diagnostics[0]?.id).toBe("D2FC003");
    },
  );

  it("flags a duplicate constant name", () => {
    const v = validateConstraints({
      ...validSpec,
      constraints: [
        { name: "DUPE", value: 1, doc: "x" },
        { name: "DUPE", value: 2, doc: "y" },
      ],
    });
    expect(v.diagnostics[0]?.id).toBe("D2FC002");
  });

  it.each([0, -1, -255])("flags non-positive value %s", (value) => {
    const v = validateConstraints({
      ...validSpec,
      constraints: [{ name: "X_MAX", value, doc: "x" }],
    });
    expect(v.diagnostics[0]?.id).toBe("D2FC004");
  });

  it("flags a non-integer value", () => {
    const v = validateConstraints({
      ...validSpec,
      constraints: [{ name: "X_MAX", value: 1.5, doc: "x" }],
    });
    expect(v.diagnostics[0]?.id).toBe("D2FC004");
  });
});

describe("validateEnums", () => {
  it("happy path returns all entries with no diagnostics", () => {
    const v = validateEnums(validSpec);
    expect(v.entries).toHaveLength(1);
    expect(v.diagnostics).toEqual([]);
  });

  it("flags a non-byte backing type", () => {
    const v = validateEnums({
      ...validSpec,
      enums: [
        {
          name: "BadBacking",
          backing: "int" as unknown as "byte",
          doc: "x",
          members: [{ name: "A", doc: "a" }],
        },
      ],
    });
    expect(v.diagnostics[0]?.id).toBe("D2FC001");
  });

  it.each(["lower", "", "Has_Underscore", "9Digit", "   ", "Has-Dash"])(
    "flags invalid enum name '%s'",
    (name) => {
      const v = validateEnums({
        ...validSpec,
        enums: [
          {
            name,
            backing: "byte",
            doc: "x",
            members: [{ name: "A", doc: "a" }],
          },
        ],
      });
      expect(v.diagnostics[0]?.id).toBe("D2FC006");
    },
  );

  it("flags a duplicate enum name", () => {
    const v = validateEnums({
      ...validSpec,
      enums: [
        {
          name: "Dupe",
          backing: "byte",
          doc: "x",
          members: [{ name: "A", doc: "a" }],
        },
        {
          name: "Dupe",
          backing: "byte",
          doc: "y",
          members: [{ name: "B", doc: "b" }],
        },
      ],
    });
    expect(v.diagnostics[0]?.id).toBe("D2FC005");
  });

  it("flags an empty member list", () => {
    const v = validateEnums({
      ...validSpec,
      enums: [{ name: "Empty", backing: "byte", doc: "x", members: [] }],
    });
    expect(v.diagnostics[0]?.id).toBe("D2FC007");
  });

  it.each(["has-dash", "", "9leading", "has space"])(
    "flags invalid member name '%s'",
    (memberName) => {
      const v = validateEnums({
        ...validSpec,
        enums: [
          {
            name: "X",
            backing: "byte",
            doc: "x",
            members: [{ name: memberName, doc: "m" }],
          },
        ],
      });
      expect(v.diagnostics[0]?.id).toBe("D2FC009");
    },
  );

  it("flags a duplicate member", () => {
    const v = validateEnums({
      ...validSpec,
      enums: [
        {
          name: "X",
          backing: "byte",
          doc: "x",
          members: [
            { name: "A", doc: "a" },
            { name: "A", doc: "b" },
          ],
        },
      ],
    });
    expect(v.diagnostics[0]?.id).toBe("D2FC008");
  });

  it("flags absent backing (undefined cast to byte) with D2FC001", () => {
    const v = validateEnums({
      ...validSpec,
      enums: [
        {
          name: "X",
          backing: undefined as unknown as "byte",
          doc: "x",
          members: [{ name: "A", doc: "a" }],
        },
      ],
    });
    expect(v.diagnostics[0]?.id).toBe("D2FC001");
  });

  it("flags numeric backing (42) with D2FC001", () => {
    const v = validateEnums({
      ...validSpec,
      enums: [
        {
          name: "X",
          backing: 42 as unknown as "byte",
          doc: "x",
          members: [{ name: "A", doc: "a" }],
        },
      ],
    });
    expect(v.diagnostics[0]?.id).toBe("D2FC001");
  });
});

describe("emitConstraints", () => {
  it("emits the FieldConstraints const object in spec order", () => {
    const r = emitConstraints(validSpec);
    expect(r.diagnostics).toEqual([]);
    expect(r.source).toContain("export const FieldConstraints = {");
    expect(r.source).toContain("EMAIL_MAX: 254,");
    expect(r.source).toContain("PHONE_MIN_DIGITS: 7,");
    expect(r.source).toContain("export type FieldConstraint =");
    // Spec order: EMAIL_MAX before PHONE_MIN_DIGITS.
    expect(
      r.source.indexOf("EMAIL_MAX") < r.source.indexOf("PHONE_MIN_DIGITS"),
    ).toBe(true);
  });

  it("blocks emit on validation diagnostics (empty source)", () => {
    const r = emitConstraints({
      ...validSpec,
      constraints: [{ name: "lowercase", value: 1, doc: "x" }],
    });
    expect(r.source).toBe("");
    expect(r.diagnostics).not.toEqual([]);
  });

  it("produces identical source across two runs (idempotency)", () => {
    const first = emitConstraints(validSpec).source;
    const second = emitConstraints(validSpec).source;
    expect(second).toBe(first);
  });

  it("escapes JSDoc-terminator sequences in doc text", () => {
    const r = emitConstraints({
      ...validSpec,
      constraints: [{ name: "TRICKY_MAX", value: 1, doc: "Has */ inside." }],
    });
    expect(r.diagnostics).toEqual([]);
    expect(r.source).toContain("Has *\\/ inside.");
  });
});

describe("emitTaxonomy", () => {
  it("emits the const object, branded type, set, and Zod schema in spec order", () => {
    const r = emitTaxonomy({
      ...validSpec,
      enums: [
        {
          name: "BiologicalSex",
          backing: "byte",
          doc: "Sex.",
          members: [
            { name: "Male", doc: "M." },
            { name: "Female", doc: "F." },
          ],
        },
      ],
    });
    expect(r.diagnostics).toEqual([]);
    expect(r.source).toContain('import { z } from "zod";');
    expect(r.source).toContain("export const BiologicalSex = {");
    expect(r.source).toContain('Male: "Male",');
    expect(r.source).toContain('Female: "Female",');
    expect(r.source).toContain(
      "export const ALL_BIOLOGICAL_SEX_SET: ReadonlySet<string>",
    );
    expect(r.source).toContain("export const BiologicalSexSchema = z.enum([");
    expect(r.source).toContain('{ readonly __brand: "BiologicalSex" }');
    // Spec order: Male before Female.
    expect(r.source.indexOf('"Male"') < r.source.indexOf('"Female"')).toBe(
      true,
    );
  });

  it("blocks emit on validation diagnostics (empty source)", () => {
    const r = emitTaxonomy({
      ...validSpec,
      enums: [{ name: "Empty", backing: "byte", doc: "x", members: [] }],
    });
    expect(r.source).toBe("");
    expect(r.diagnostics).not.toEqual([]);
  });

  it("produces identical source across two runs (idempotency)", () => {
    const first = emitTaxonomy(validSpec).source;
    const second = emitTaxonomy(validSpec).source;
    expect(second).toBe(first);
  });
});

describe("runFieldConstraintsEmit (against the committed spec)", () => {
  it("short-circuits to no diagnostics when outputs are up-to-date", () => {
    // The committed .g.ts are emitted from the committed spec, so a non-force
    // run sees the outputs newer-or-equal than the spec and skips work.
    expect(runFieldConstraintsEmit(false)).toEqual([]);
  });

  it("force re-emit completes without error diagnostics and stays deterministic", () => {
    // force=true bypasses the mtime check and re-emits; the writer is a no-op
    // when content is byte-identical, so this must not error and must leave the
    // committed outputs unchanged (verified separately by `pnpm codegen` diff).
    const diagnostics = runFieldConstraintsEmit(true);
    expect(diagnostics.filter((d) => d.severity === "error")).toEqual([]);
  });
});
