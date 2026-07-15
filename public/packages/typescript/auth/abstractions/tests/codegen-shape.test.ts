// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import { tk } from "@dcsv-io/d2-i18n-abstractions";
import {
  ALL_AUTH_ERROR_CODES,
  AuthErrorCodes,
  getAuthErrorHttpStatus,
} from "../src/auth-error-codes.g.js";
import { AuthFailures } from "../src/auth-failures.g.js";
import { ALL_SCOPES, Scopes } from "../src/scopes.g.js";

describe("AuthErrorCodes — per-VALUE pin (mirrors .NET 1:1)", () => {
  it.each([
    "AUTH_BEARER_MISSING",
    "AUTH_BEARER_MALFORMED",
    "AUTH_JWT_SIGNATURE_INVALID",
    "AUTH_JWT_EXPIRED",
    "AUTH_JWT_NOT_YET_VALID",
    "AUTH_JWT_ISSUER_MISMATCH",
    "AUTH_JWT_AUDIENCE_MISMATCH",
    "AUTH_JWT_CLAIM_MISSING",
    "AUTH_JWT_ACT_CHAIN_MALFORMED",
    "AUTH_JWT_KID_NOT_FOUND",
    "AUTH_JWKS_UNAVAILABLE",
    "AUTH_SESSION_REVOKED",
    "AUTH_SESSION_LIVENESS_UNAVAILABLE",
    "AUTH_SCOPE_INSUFFICIENT",
    "AUTH_REQUEST_ORIGIN_UNESTABLISHED",
  ])("AuthErrorCodes.%s = same string", (code) => {
    expect(AuthErrorCodes[code as keyof typeof AuthErrorCodes]).toBe(code);
    expect(ALL_AUTH_ERROR_CODES).toContain(code);
  });

  it("getAuthErrorHttpStatus returns 401 for validation/policy codes", () => {
    expect(getAuthErrorHttpStatus("AUTH_BEARER_MISSING")).toBe(401);
    expect(getAuthErrorHttpStatus("AUTH_SCOPE_INSUFFICIENT")).toBe(401);
    expect(getAuthErrorHttpStatus("AUTH_REQUEST_ORIGIN_UNESTABLISHED")).toBe(
      401,
    );
  });

  it("getAuthErrorHttpStatus returns 503 for infra codes", () => {
    expect(getAuthErrorHttpStatus("AUTH_JWKS_UNAVAILABLE")).toBe(503);
    expect(getAuthErrorHttpStatus("AUTH_SESSION_LIVENESS_UNAVAILABLE")).toBe(
      503,
    );
  });

  it("getAuthErrorHttpStatus returns 500 for unknown code (defensive default)", () => {
    expect(getAuthErrorHttpStatus("UNKNOWN_CODE")).toBe(500);
  });
});

describe("AuthFailures — factory shape (mirrors .NET 1:1)", () => {
  it("bearerMissing returns Unauthorized with the right errorCode", () => {
    const r = AuthFailures.bearerMissing();
    expect(r.failed).toBe(true);
    expect(r.statusCode).toBe(401);
    expect(r.errorCode).toBe("AUTH_BEARER_MISSING");
    // The emitted factory references the TS TK constant (TK.auth.errors.*),
    // which resolves to the snake wire key — NOT the PascalCase symbol path
    // (a literal string of which would silently bypass the TK catalog and ride
    // the wire un-renderable). Mirrors the .NET wire key.
    expect(r.messages[0]?.key).toBe("auth_errors_UNAUTHORIZED");
  });

  it("jwksUnavailable returns ServiceUnavailable", () => {
    const r = AuthFailures.jwksUnavailable();
    expect(r.statusCode).toBe(503);
    expect(r.errorCode).toBe("AUTH_JWKS_UNAVAILABLE");
  });

  it("stamps the auth code's OWN category, overriding the base factory's", () => {
    // AUTH_BEARER_MISSING is validation_failure even though it delegates to
    // unauthorized (whose own UNAUTHORIZED code is policy_denied) — the auth
    // code's category wins. Mirrors .NET AuthFailures.BearerMissing().Category.
    expect(AuthFailures.bearerMissing().category).toBe("validation_failure");
    // AUTH_JWKS_UNAVAILABLE is infrastructure_unavailable (matches its 503 base).
    expect(AuthFailures.jwksUnavailable().category).toBe(
      "infrastructure_unavailable",
    );
    // AUTH_SESSION_REVOKED is policy_denied.
    expect(AuthFailures.sessionRevoked().category).toBe("policy_denied");
  });

  it("traceId pass-through", () => {
    expect(AuthFailures.bearerMissing({ traceId: "trace-1" }).traceId).toBe(
      "trace-1",
    );
  });

  it("messages override is honored; omitted defaults to the spec TK", () => {
    // The delegating factory's opts object carries an optional `messages`
    // override (the TS twin of .NET's `IReadOnlyList<TKMessage>? messages`).
    // Omitted → the spec's default TK; supplied → the override rides the wire.
    const def = AuthFailures.bearerMissing();
    expect(def.messages[0]?.key).toBe("auth_errors_UNAUTHORIZED");
    expect(def.messages[0]?.params).toBeUndefined();

    const overridden = AuthFailures.bearerMissing({
      messages: [tk("auth_errors_UNAUTHORIZED", { arg: "token" })],
    });
    expect(overridden.messages[0]?.key).toBe("auth_errors_UNAUTHORIZED");
    expect(overridden.messages[0]?.params).toEqual({ arg: "token" });
  });
});

describe("Scopes — per-VALUE pin (public dual-values half only)", () => {
  it("self.read", () => {
    expect(Scopes.self.read).toBe("self.read");
  });
  it("self.write", () => {
    expect(Scopes.self.write).toBe("self.write");
  });
  it("anon.public.health", () => {
    expect(Scopes.anon.public.health).toBe("anon.public.health");
  });
  it("ALL_SCOPES contains every public-framework scope and no product rows", () => {
    expect(ALL_SCOPES).toContain("self.read");
    expect(ALL_SCOPES).toContain("self.write");
    expect(ALL_SCOPES).toContain("anon.public.health");
    // Product dual-values half lives under private emit — never on public ship surface.
    expect(ALL_SCOPES.some((s) => s.startsWith("internal.kc"))).toBe(false);
    expect(ALL_SCOPES).not.toContain("billing.payment.charge");
  });
});
