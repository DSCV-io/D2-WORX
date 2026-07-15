// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import { type ContextSpec, emitAuthContext } from "../src/auth-context-emit.js";

const minSpec: ContextSpec = {
  name: "IFakeContext",
  namespace: "DcsvIo.D2.Fake",
  description: "Fake context for testing.",
  extends: null,
  sections: [
    {
      name: "Identity",
      properties: [
        {
          name: "UserId",
          type: "Guid?",
          claim: "sub",
          redact: true,
          doc: "User identifier.",
        },
        {
          name: "Subject",
          type: "string?",
          claim: "sub",
          doc: "Raw subject.",
        },
      ],
    },
  ],
};

describe("emitAuthContext", () => {
  // long test description — cannot wrap
  it("emits a TS interface with property names camelCased + `?:` shorthand (rules.md §6.15)", () => {
    const r = emitAuthContext(minSpec);
    expect(r.diagnostics).toEqual([]);
    expect(r.source).toContain("export interface IFakeContext {");
    expect(r.source).toContain("readonly userId?: string;");
    expect(r.source).toContain("readonly subject?: string;");
  });

  it("emits IFakeContextRedactPaths covering only redact-marked fields", () => {
    const r = emitAuthContext(minSpec);
    expect(r.source).toContain(
      "export const IFakeContextRedactPaths: readonly string[]",
    );
    expect(r.source).toContain('"userId"');
    expect(r.source).not.toContain('"subject"');
  });

  it("flags duplicate property names", () => {
    const r = emitAuthContext({
      ...minSpec,
      sections: [
        {
          name: "X",
          properties: [
            minSpec.sections[0]!.properties[0]!,
            minSpec.sections[0]!.properties[0]!,
          ],
        },
      ],
    });
    expect(r.diagnostics[0]?.id).toBe("D2CTX001");
  });

  it("flags unsupported type", () => {
    const r = emitAuthContext({
      ...minSpec,
      sections: [
        {
          name: "X",
          properties: [
            { ...minSpec.sections[0]!.properties[0]!, type: "DateTime" },
          ],
        },
      ],
    });
    expect(r.diagnostics[0]?.id).toBe("D2CTX002");
  });

  it("description renders as JSDoc comment", () => {
    const r = emitAuthContext(minSpec);
    expect(r.source).toContain("Fake context for testing.");
  });

  it("emits plain interface (no extends clause) when spec.extends is null", () => {
    const r = emitAuthContext(minSpec);
    expect(r.diagnostics).toEqual([]);
    expect(r.source).toContain("export interface IFakeContext {");
    expect(r.source).not.toContain("extends");
  });
});

describe("emitAuthContext — extends honor (Fix A)", () => {
  const extendingSpec: ContextSpec = {
    name: "IDerivedContext",
    namespace: "DcsvIo.D2.Fake",
    description: "Derived context for testing.",
    extends: "DcsvIo.D2.AuthContext.Abstractions.IAuthContext",
    sections: [
      {
        name: "Tracing",
        properties: [
          {
            name: "TraceId",
            type: "string?",
            doc: "Trace id.",
          },
        ],
      },
    ],
  };

  it("emits extends clause + import when spec.extends is set (package mode)", () => {
    const r = emitAuthContext(extendingSpec, "package");
    expect(r.diagnostics).toEqual([]);
    expect(r.source).toContain(
      'import type { IAuthContext } from "@dcsv-io/d2-auth-context-abstractions";',
    );
    expect(r.source).toContain(
      "export interface IDerivedContext extends IAuthContext {",
    );
  });

  it("emits extends clause + import in relative mode too", () => {
    // Sanity check: extends-resolution is independent of import mode for
    // enums; the extends import always uses the package path.
    const r = emitAuthContext(extendingSpec, "relative");
    expect(r.diagnostics).toEqual([]);
    expect(r.source).toContain(
      'import type { IAuthContext } from "@dcsv-io/d2-auth-context-abstractions";',
    );
    expect(r.source).toContain(
      "export interface IDerivedContext extends IAuthContext {",
    );
  });

  it("kebab-cases multi-segment package names correctly", () => {
    const r = emitAuthContext(
      {
        ...extendingSpec,
        extends: "DcsvIo.D2.SomeNested.Pkg.Abstractions.IBase",
      },
      "package",
    );
    expect(r.diagnostics).toEqual([]);
    expect(r.source).toContain(
      'import type { IBase } from "@dcsv-io/d2-some-nested-pkg-abstractions";',
    );
    expect(r.source).toContain(
      "export interface IDerivedContext extends IBase {",
    );
  });

  it("flags D2CTX005 for FQN missing the 'DcsvIo.D2.' prefix", () => {
    const r = emitAuthContext(
      {
        ...extendingSpec,
        extends: "Other.Namespace.IBase",
      },
      "package",
    );
    expect(r.diagnostics[0]?.id).toBe("D2CTX005");
    expect(r.source).toBe("");
  });

  it("flags D2CTX005 for FQN with no segments after 'DcsvIo.D2.'", () => {
    const r = emitAuthContext(
      {
        ...extendingSpec,
        extends: "DcsvIo.D2.IBareInterface",
      },
      "package",
    );
    // After stripping 'DcsvIo.D2.' the rest is 'IBareInterface' (single
    // segment) — there's no package segment, so the resolver fails.
    expect(r.diagnostics[0]?.id).toBe("D2CTX005");
  });

  it("treats spec.extends === '' as absent (no extends clause)", () => {
    const r = emitAuthContext(
      {
        ...extendingSpec,
        extends: "",
      },
      "package",
    );
    expect(r.diagnostics).toEqual([]);
    expect(r.source).not.toContain("extends");
  });
});
