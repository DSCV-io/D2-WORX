// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import {
  emitAuthErrorCodes,
  type ErrorCodesSpec,
  validateErrorCodesSpec,
} from "../src/auth-error-codes-emit.js";

const validSpec: ErrorCodesSpec = {
  errorCodes: [
    {
      code: "AUTH_BEARER_MISSING",
      httpStatus: 401,
      category: "validation_failure",
      userMessageKey: "TK.X",
      factoryName: "BearerMissing",
      doc: "Bearer missing",
    },
    {
      code: "AUTH_JWKS_UNAVAILABLE",
      httpStatus: 503,
      category: "infrastructure_unavailable",
      userMessageKey: "TK.Y",
      factoryName: "JwksUnavailable",
    },
  ],
};

describe("validateErrorCodesSpec", () => {
  it("happy path returns all entries with no diagnostics", () => {
    const v = validateErrorCodesSpec(validSpec);
    expect(v.entries).toHaveLength(2);
    expect(v.diagnostics).toEqual([]);
  });

  it("flags duplicate codes", () => {
    const v = validateErrorCodesSpec({
      errorCodes: [validSpec.errorCodes[0]!, validSpec.errorCodes[0]!],
    });
    expect(v.diagnostics[0]?.id).toBe("D2AEC001");
  });

  it("flags duplicate factory names", () => {
    const v = validateErrorCodesSpec({
      errorCodes: [
        validSpec.errorCodes[0]!,
        { ...validSpec.errorCodes[0]!, code: "AUTH_OTHER" },
      ],
    });
    expect(v.diagnostics[0]?.id).toBe("D2AEC002");
  });

  it("flags unknown category", () => {
    const v = validateErrorCodesSpec({
      errorCodes: [{ ...validSpec.errorCodes[0]!, category: "weird" }],
    });
    expect(v.diagnostics[0]?.id).toBe("D2AEC003");
  });

  it("flags unsupported httpStatus", () => {
    const v = validateErrorCodesSpec({
      errorCodes: [{ ...validSpec.errorCodes[0]!, httpStatus: 418 }],
    });
    expect(v.diagnostics[0]?.id).toBe("D2AEC004");
  });
});

describe("emitAuthErrorCodes — snapshot pin", () => {
  it("emits sorted constants + http-status switch", () => {
    const r = emitAuthErrorCodes(validSpec);
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

  it("blocks emit on validation diagnostics", () => {
    const r = emitAuthErrorCodes({
      errorCodes: [{ ...validSpec.errorCodes[0]!, httpStatus: 999 }],
    });
    expect(r.source).toBe("");
    expect(r.diagnostics).not.toEqual([]);
  });
});
