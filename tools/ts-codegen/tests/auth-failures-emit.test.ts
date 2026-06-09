// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import {
  AUTH_CONFIG,
  AUTH_FAILURES_CONFIG,
  emitFailuresCatalog,
  type ErrorCodesSpec,
} from "../src/error-codes-emit.js";

const spec: ErrorCodesSpec = {
  errorCodes: [
    {
      code: "AUTH_BEARER_MISSING",
      httpStatus: 401,
      category: "validation_failure",
      userMessageKey: "TK.Auth.Errors.UNAUTHORIZED",
      factoryName: "BearerMissing",
      factoryShape: "with_error_code",
      doc: "Bearer missing.",
    },
    {
      code: "AUTH_JWKS_UNAVAILABLE",
      httpStatus: 503,
      category: "infrastructure_unavailable",
      userMessageKey: "TK.Auth.Errors.TEMPORARILY_UNAVAILABLE",
      factoryName: "JwksUnavailable",
      factoryShape: "with_error_code",
      doc: "JWKS upstream unavailable.",
    },
  ],
};

const EN_US_KEYS = new Set([
  "auth_errors_UNAUTHORIZED",
  "auth_errors_TEMPORARILY_UNAVAILABLE",
]);

describe("emitFailuresCatalog (auth) — snapshot pin", () => {
  it("emits factory functions calling unauthorized + serviceUnavailable", () => {
    const r = emitFailuresCatalog(
      spec,
      AUTH_CONFIG,
      AUTH_FAILURES_CONFIG,
      EN_US_KEYS,
    );
    expect(r.diagnostics).toEqual([]);
    expect(r.source).toContain("export const AuthFailures = {");
    // Each method is generic with a `void` default so the untyped call
    // (`AuthFailures.bearerMissing()` → `D2Result<void>`) and the typed call
    // (`AuthFailures.bearerMissing<User>()` → `D2Result<User>`) share one
    // method — the TS equivalent of the .NET two-class domain-failures split.
    expect(r.source).toContain("bearerMissing<T = void>(traceId?: string)");
    expect(r.source).toContain("return unauthorized<T>");
    expect(r.source).toContain("jwksUnavailable<T = void>(traceId?: string)");
    expect(r.source).toContain("return serviceUnavailable<T>");
    expect(r.source).toContain(
      "errorCode: AuthErrorCodes.AUTH_BEARER_MISSING,",
    );
    expect(r.source).toContain(
      "errorCode: AuthErrorCodes.AUTH_JWKS_UNAVAILABLE,",
    );
  });

  it("renders userMessageKey as a TK.* CONSTANT reference, not a string literal", () => {
    const r = emitFailuresCatalog(
      spec,
      AUTH_CONFIG,
      AUTH_FAILURES_CONFIG,
      EN_US_KEYS,
    );
    // TK constant-reference rule: the emitter must reference the generated TS
    // TK constant (e.g. `TK.auth.errors.UNAUTHORIZED`), itself a TKMessage
    // instance, never a raw PascalCase symbol-path string literal. The
    // constant's `.key` is the snake wire key (`auth_errors_UNAUTHORIZED`) that
    // the TS Translator resolves; a string literal silently bypasses the
    // catalog and rides the wire un-renderable.
    expect(r.source).toContain('import { TK } from "@d2/i18n-keys";');
    expect(r.source).toContain("messages: [TK.auth.errors.UNAUTHORIZED],");
    expect(r.source).toContain(
      "messages: [TK.auth.errors.TEMPORARILY_UNAVAILABLE],",
    );
    // The constant IS the message — no tk() wrapper is emitted, and the raw
    // PascalCase symbol-path string literal must never appear.
    expect(r.source).not.toContain("tk(TK.auth.errors.UNAUTHORIZED)");
    expect(r.source).not.toContain('tk("TK.Auth.Errors.UNAUTHORIZED")');
    expect(r.source).not.toContain(
      'tk("TK.Auth.Errors.TEMPORARILY_UNAVAILABLE")',
    );
  });

  it("selects the base factory by httpStatus (401 -> unauthorized, 503 -> serviceUnavailable)", () => {
    const r = emitFailuresCatalog(
      spec,
      AUTH_CONFIG,
      AUTH_FAILURES_CONFIG,
      EN_US_KEYS,
    );
    const bearerIdx = r.source.indexOf("bearerMissing");
    const bearerBody = r.source.slice(bearerIdx, bearerIdx + 200);
    expect(bearerBody).toContain("return unauthorized");
    const jwksIdx = r.source.indexOf("jwksUnavailable");
    const jwksBody = r.source.slice(jwksIdx, jwksIdx + 200);
    expect(jwksBody).toContain("return serviceUnavailable");
  });
});
