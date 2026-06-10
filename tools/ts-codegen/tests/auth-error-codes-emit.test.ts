// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import {
  AUTH_CONFIG,
  emitErrorCodesCatalog,
  type ErrorCodesSpec,
  validateErrorCodesSpec,
} from "../src/error-codes-emit.js";

const validSpec: ErrorCodesSpec = {
  errorCodes: [
    {
      code: "AUTH_BEARER_MISSING",
      httpStatus: 401,
      category: "validation_failure",
      userMessageKey: "TK.X",
      factoryName: "BearerMissing",
      factoryShape: "standard",
      doc: "Bearer missing",
    },
    {
      code: "AUTH_JWKS_UNAVAILABLE",
      httpStatus: 503,
      category: "infrastructure_unavailable",
      userMessageKey: "TK.Y",
      factoryName: "JwksUnavailable",
      factoryShape: "standard",
    },
  ],
};

describe("validateErrorCodesSpec (auth catalog)", () => {
  it("happy path returns all entries with no diagnostics", () => {
    // No en-US key set supplied → the TK-existence check is skipped, so the
    // synthetic TK.X/TK.Y keys do not trip D2ERC002 here.
    const v = validateErrorCodesSpec(validSpec, AUTH_CONFIG);
    expect(v.entries).toHaveLength(2);
    expect(v.diagnostics).toEqual([]);
  });

  it("flags duplicate codes", () => {
    const v = validateErrorCodesSpec(
      { errorCodes: [validSpec.errorCodes[0]!, validSpec.errorCodes[0]!] },
      AUTH_CONFIG,
    );
    expect(v.diagnostics[0]?.id).toBe("D2AEC001");
  });

  it("flags duplicate factory names", () => {
    const v = validateErrorCodesSpec(
      {
        errorCodes: [
          validSpec.errorCodes[0]!,
          { ...validSpec.errorCodes[0]!, code: "AUTH_OTHER" },
        ],
      },
      AUTH_CONFIG,
    );
    expect(v.diagnostics[0]?.id).toBe("D2AEC002");
  });

  it("flags unknown category", () => {
    const v = validateErrorCodesSpec(
      { errorCodes: [{ ...validSpec.errorCodes[0]!, category: "weird" }] },
      AUTH_CONFIG,
    );
    expect(v.diagnostics[0]?.id).toBe("D2AEC003");
  });

  it("flags unsupported httpStatus", () => {
    const v = validateErrorCodesSpec(
      { errorCodes: [{ ...validSpec.errorCodes[0]!, httpStatus: 418 }] },
      AUTH_CONFIG,
    );
    expect(v.diagnostics[0]?.id).toBe("D2AEC004");
  });
});

describe("emitErrorCodesCatalog (auth) — snapshot pin", () => {
  it("emits sorted constants + http-status switch", () => {
    const r = emitErrorCodesCatalog(validSpec, AUTH_CONFIG);
    expect(r.diagnostics).toEqual([]);
    expect(r.source).toContain('AUTH_BEARER_MISSING: "AUTH_BEARER_MISSING"');
    expect(r.source).toContain(
      'AUTH_JWKS_UNAVAILABLE: "AUTH_JWKS_UNAVAILABLE"',
    );
    expect(r.source).toContain(
      "export const ALL_AUTH_ERROR_CODES: readonly string[]",
    );
    expect(r.source).toContain('case "AUTH_BEARER_MISSING": return 401;');
    expect(r.source).toContain('case "AUTH_JWKS_UNAVAILABLE": return 503;');
    // Sort defends order independence — alphabetical → BEARER_MISSING before JWKS.
    expect(
      r.source.indexOf("AUTH_BEARER_MISSING") <
        r.source.indexOf("AUTH_JWKS_UNAVAILABLE"),
    ).toBe(true);
  });

  it("emits NO per-code JSDoc on the auth constants (unlike generic)", () => {
    const r = emitErrorCodesCatalog(validSpec, AUTH_CONFIG);
    const constBlockStart = r.source.indexOf("export const AuthErrorCodes = {");
    const constBlockEnd = r.source.indexOf("} as const;", constBlockStart);
    const constBlock = r.source.slice(constBlockStart, constBlockEnd);
    expect(constBlock).not.toContain("/**");
  });

  it("blocks emit on validation diagnostics", () => {
    const r = emitErrorCodesCatalog(
      { errorCodes: [{ ...validSpec.errorCodes[0]!, httpStatus: 999 }] },
      AUTH_CONFIG,
    );
    expect(r.source).toBe("");
    expect(r.diagnostics).not.toEqual([]);
  });
});
