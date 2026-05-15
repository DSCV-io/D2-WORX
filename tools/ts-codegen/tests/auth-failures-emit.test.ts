// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import { type ErrorCodesSpec } from "../src/auth-error-codes-emit.js";
import { emitAuthFailures } from "../src/auth-failures-emit.js";

const spec: ErrorCodesSpec = {
  errorCodes: [
    {
      code: "AUTH_BEARER_MISSING",
      httpStatus: 401,
      category: "validation_failure",
      userMessageKey: "TK.Auth.Errors.UNAUTHORIZED",
      factoryName: "BearerMissing",
      doc: "Bearer missing.",
    },
    {
      code: "AUTH_JWKS_UNAVAILABLE",
      httpStatus: 503,
      category: "infrastructure_unavailable",
      userMessageKey: "TK.Auth.Errors.TEMPORARILY_UNAVAILABLE",
      factoryName: "JwksUnavailable",
      doc: "JWKS upstream unavailable.",
    },
  ],
};

describe("emitAuthFailures — snapshot pin", () => {
  it("emits factory functions calling unauthorized + serviceUnavailable", () => {
    const r = emitAuthFailures(spec);
    expect(r.diagnostics).toEqual([]);
    expect(r.source).toContain("export const AuthFailures = {");
    expect(r.source).toContain("bearerMissing(traceId?: string)");
    expect(r.source).toContain("return unauthorized");
    expect(r.source).toContain("jwksUnavailable(traceId?: string)");
    expect(r.source).toContain("return serviceUnavailable");
    expect(r.source).toContain(
      "errorCode: AuthErrorCodes.AUTH_BEARER_MISSING,",
    );
    expect(r.source).toContain(
      "errorCode: AuthErrorCodes.AUTH_JWKS_UNAVAILABLE,",
    );
  });

  it("renders userMessageKey as tk(...) call", () => {
    const r = emitAuthFailures(spec);
    expect(r.source).toContain('tk("TK.Auth.Errors.UNAUTHORIZED")');
    expect(r.source).toContain('tk("TK.Auth.Errors.TEMPORARILY_UNAVAILABLE")');
  });
});
