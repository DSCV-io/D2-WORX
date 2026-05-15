// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import { type ContextSpec, emitAuthContext } from "../src/auth-context-emit.js";
import {
  emitPropagatedContextInterface,
  emitPropagatedSerializer,
} from "../src/request-context-emit.js";

const spec: ContextSpec = {
  name: "IFakeReqContext",
  namespace: "D2.Shared.Fake",
  extends: null,
  sections: [
    {
      name: "Section",
      properties: [
        {
          name: "RequestId",
          type: "string?",
          propagate: true,
          maxLength: 256,
          doc: "Per-request id.",
        },
        {
          name: "RiskScore",
          type: "int?",
          propagate: true,
          doc: "Risk score.",
        },
        {
          name: "IsVpn",
          type: "bool?",
          propagate: true,
          doc: "VPN flag.",
        },
        {
          name: "NotPropagated",
          type: "string?",
          doc: "Not in envelope.",
        },
      ],
    },
  ],
};

describe("emitPropagatedContextInterface", () => {
  it("emits only propagate=true fields", () => {
    const src = emitPropagatedContextInterface(spec);
    expect(src).toContain("readonly requestId: string | null;");
    expect(src).toContain("readonly riskScore: number | null;");
    expect(src).toContain("readonly isVpn: boolean | null;");
    expect(src).not.toContain("notPropagated");
  });
});

describe("emitAuthContext via request-context emit path (extends integration)", () => {
  // Sanity check that the request-context emitter's call into emitAuthContext
  // with importMode='package' produces the extends clause + import for the
  // production-shape spec where extends is set.
  const reqSpec: ContextSpec = {
    name: "IFakeReqContextWithExtends",
    namespace: "D2.Shared.Fake",
    extends: "D2.Shared.AuthContext.Abstractions.IAuthContext",
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

  it("emits extends clause + IAuthContext import in package mode", () => {
    const r = emitAuthContext(reqSpec, "package");
    expect(r.diagnostics).toEqual([]);
    expect(r.source).toContain(
      'import type { IAuthContext } from "@d2/auth-context-abstractions";',
    );
    expect(r.source).toContain(
      "export interface IFakeReqContextWithExtends extends IAuthContext {",
    );
  });
});

describe("emitPropagatedSerializer", () => {
  it("emits serialize + tryDecode methods", () => {
    const src = emitPropagatedSerializer(spec);
    expect(src).toContain("export class PropagatedContextSerializer");
    expect(src).toContain("static serialize(ctx: IPropagatedContext)");
    expect(src).toContain("static tryDecode(input: string | null | undefined)");
  });

  it("enforces maxLength on string-typed fields", () => {
    const src = emitPropagatedSerializer(spec);
    expect(src).toContain("if (v.length > 256) return undefined;");
  });

  it("type-checks bool/number fields", () => {
    const src = emitPropagatedSerializer(spec);
    expect(src).toContain('if (typeof v !== "boolean") return undefined;');
    expect(src).toContain(
      'if (typeof v !== "number" || !Number.isFinite(v)) return undefined;',
    );
  });
});
