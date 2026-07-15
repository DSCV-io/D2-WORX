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
  it("emits only propagate=true fields with `?:` shorthand (rules.md §6.15)", () => {
    const src = emitPropagatedContextInterface(spec);
    expect(src).toContain("readonly requestId?: string;");
    expect(src).toContain("readonly riskScore?: number;");
    expect(src).toContain("readonly isVpn?: boolean;");
    expect(src).not.toContain("notPropagated");
    // Negative pin: never emits `| null` or `| undefined` on field declarations.
    expect(src).not.toContain("readonly requestId:");
    expect(src).not.toContain("| null");
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

describe("call-path (propagated list-of-records) emission", () => {
  const callPathSpec: ContextSpec = {
    name: "IFakeReqContext",
    namespace: "D2.Shared.Fake",
    extends: "D2.Shared.AuthContext.Abstractions.IAuthContext",
    sections: [
      {
        name: "Tracing",
        properties: [
          {
            name: "RequestId",
            type: "string?",
            propagate: true,
            maxLength: 256,
          },
        ],
      },
      {
        name: "Establishment",
        properties: [
          { name: "Origin", type: "RequestOrigin", doc: "Origin." },
          { name: "ImmediateCaller", type: "string?", doc: "Caller." },
          {
            name: "CallPath",
            type: "IReadOnlyList<CallPathEntry>",
            propagate: true,
            maxLength: 16,
            entryIdMaxLength: 128,
            doc: "Call path.",
          },
        ],
      },
    ],
  };

  it("propagated interface emits callPath as an optional CallPathEntry[] + the import", () => {
    const src = emitPropagatedContextInterface(callPathSpec);
    expect(src).toContain(
      'import type { CallPathEntry } from "@d2/auth-context-abstractions";',
    );
    expect(src).toContain("readonly callPath?: readonly CallPathEntry[];");
    // Non-propagated establishment fields never enter the propagated subset.
    expect(src).not.toContain("origin");
    expect(src).not.toContain("immediateCaller");
  });

  it("serializer enforces the depth bound + per-entry-id cap + entry shape", () => {
    const src = emitPropagatedSerializer(callPathSpec);
    expect(src).toContain("if (!Array.isArray(v)) return undefined;");
    expect(src).toContain("if (v.length > 16) return undefined;");
    expect(src).toContain(
      'if (typeof entryId !== "string" || entryId.length > 128) return undefined;',
    );
    expect(src).toContain(
      'if (typeof entry["kind"] !== "string") return undefined;',
    );
    expect(src).toContain(
      'if (typeof entry["timestamp"] !== "string") return undefined;',
    );
  });

  it("fails loud when a propagated list-of-records field omits entryIdMaxLength (no hard-coded cap)", () => {
    const missingCapSpec: ContextSpec = {
      name: "IFakeReqContext",
      namespace: "D2.Shared.Fake",
      extends: "D2.Shared.AuthContext.Abstractions.IAuthContext",
      sections: [
        {
          name: "Establishment",
          properties: [
            {
              name: "CallPath",
              type: "IReadOnlyList<CallPathEntry>",
              propagate: true,
              maxLength: 16,
              // entryIdMaxLength deliberately omitted — the cap is single-sourced
              // from the spec, so the emitter must fail loud rather than default.
              doc: "Call path.",
            },
          ],
        },
      ],
    };
    expect(() => emitPropagatedSerializer(missingCapSpec)).toThrow(
      /entryIdMaxLength/,
    );
  });

  it("full interface (package mode) emits required Origin + CallPath + their imports", () => {
    const r = emitAuthContext(callPathSpec, "package");
    expect(r.diagnostics).toEqual([]);
    expect(r.source).toContain(
      'import type { CallPathEntry, RequestOrigin } from "@d2/auth-context-abstractions";',
    );
    // Non-nullable on the full interface — required (no `?:`), mirroring .NET.
    expect(r.source).toContain("readonly origin: RequestOrigin;");
    expect(r.source).toContain("readonly callPath: readonly CallPathEntry[];");
    expect(r.source).toContain("readonly immediateCaller?: string;");
  });
});
