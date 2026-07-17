// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import {
  AuthErrorCodes,
  JwtClaimTypes,
  type JwtPayload,
} from "@dcsv-io/d2-auth-abstractions";
import { parseAuthHeader } from "../src/parse-auth-header.js";

function buildJwt(claims: Record<string, unknown>): string {
  const header = Buffer.from(
    JSON.stringify({ alg: "RS256", typ: "JWT" }),
  ).toString("base64url");
  const payload = Buffer.from(JSON.stringify(claims)).toString("base64url");
  // Signature segment is shape-checked but not verified.
  return `${header}.${payload}.fake-sig`;
}

const VALID_CLAIMS = {
  sub: "00000000-0000-0000-0000-000000000001",
  aud: "d2.edge",
  iat: 1_700_000_000,
  exp: 1_700_000_900,
  scope: "auth.user.read auth.user.write",
  d2_session_id: "00000000-0000-0000-0000-000000000002",
  d2_username: "alice",
  d2_org_id: "00000000-0000-0000-0000-000000000003",
  d2_org_name: "Acme",
  d2_org_type: "Customer",
  d2_org_role: "Owner",
};

describe("parseAuthHeader — happy path", () => {
  it("decodes a well-formed Bearer JWT into a JwtPayload", () => {
    const header = `Bearer ${buildJwt(VALID_CLAIMS)}`;
    const result = parseAuthHeader(header);
    expect(result.success).toBe(true);
    const payload = result.data!;
    expect(payload.sub).toBe(VALID_CLAIMS.sub);
    expect(payload.aud).toEqual([VALID_CLAIMS.aud]);
    expect(payload.iat).toBe(VALID_CLAIMS.iat);
    expect(payload.exp).toBe(VALID_CLAIMS.exp);
    expect(payload.scope).toBe(VALID_CLAIMS.scope);
    expect(payload.d2_session_id).toBe(VALID_CLAIMS.d2_session_id);
    expect(payload.d2_username).toBe(VALID_CLAIMS.d2_username);
    expect(payload.d2_org_type).toBe(VALID_CLAIMS.d2_org_type);
  });

  it("accepts case-insensitive Bearer scheme per RFC 6750", () => {
    const header = `bEaReR  ${buildJwt(VALID_CLAIMS)}`;
    const result = parseAuthHeader(header);
    expect(result.success).toBe(true);
  });

  it("normalizes aud as array per RFC 7519 §4.1.3 (string form)", () => {
    const header = `Bearer ${buildJwt({ ...VALID_CLAIMS, aud: "d2.edge" })}`;
    const result = parseAuthHeader(header);
    expect(result.success).toBe(true);
    expect(result.data!.aud).toEqual(["d2.edge"]);
  });

  it("normalizes aud as array per RFC 7519 §4.1.3 (array form)", () => {
    const header = `Bearer ${buildJwt({ ...VALID_CLAIMS, aud: ["d2.edge", "d2.files"] })}`;
    const result = parseAuthHeader(header);
    expect(result.success).toBe(true);
    expect(result.data!.aud).toEqual(["d2.edge", "d2.files"]);
  });

  it("preserves raw claims for forward-compat", () => {
    const claims = { ...VALID_CLAIMS, custom_claim: "custom-value" };
    const header = `Bearer ${buildJwt(claims)}`;
    const result = parseAuthHeader(header);
    expect(result.success).toBe(true);
    expect(result.data!.raw["custom_claim"]).toBe("custom-value");
  });

  it("absent claims surface as undefined", () => {
    const minimal = { sub: VALID_CLAIMS.sub, aud: "d2.edge" };
    const header = `Bearer ${buildJwt(minimal)}`;
    const result = parseAuthHeader(header);
    expect(result.success).toBe(true);
    const payload = result.data as JwtPayload;
    expect(payload.iat).toBeUndefined();
    expect(payload.exp).toBeUndefined();
    expect(payload.azp).toBeUndefined();
    expect(payload.scope).toBeUndefined();
    expect(payload.act).toBeUndefined();
    expect(payload.d2_username).toBeUndefined();
  });

  it("propagates traceId on success", () => {
    const header = `Bearer ${buildJwt(VALID_CLAIMS)}`;
    const result = parseAuthHeader(header, { traceId: "abc-123" });
    expect(result.traceId).toBe("abc-123");
  });
});

describe("parseAuthHeader — adversarial / null + empty + whitespace", () => {
  it.each([null, undefined, "", "   ", "\t\t"])(
    "rejects falsey/whitespace input %j with bearerMissing",
    (header) => {
      const result = parseAuthHeader(header as string | null | undefined);
      expect(result.success).toBe(false);
      expect(result.errorCode).toBe(AuthErrorCodes.AUTH_BEARER_MISSING);
    },
  );

  it("rejects oversized header with bearerMalformed", () => {
    const huge = "a".repeat(20_000);
    const result = parseAuthHeader(`Bearer ${huge}`);
    expect(result.success).toBe(false);
    expect(result.errorCode).toBe(AuthErrorCodes.AUTH_BEARER_MALFORMED);
  });

  it("rejects header containing CR-LF (injection probe)", () => {
    const header = `Bearer abc\r\nX-Evil: 1`;
    const result = parseAuthHeader(header);
    expect(result.success).toBe(false);
    expect(result.errorCode).toBe(AuthErrorCodes.AUTH_BEARER_MALFORMED);
  });

  it("rejects missing Bearer scheme", () => {
    const result = parseAuthHeader(`Basic ${buildJwt(VALID_CLAIMS)}`);
    expect(result.success).toBe(false);
    expect(result.errorCode).toBe(AuthErrorCodes.AUTH_BEARER_MALFORMED);
  });

  it("rejects empty token after Bearer prefix", () => {
    const result = parseAuthHeader("Bearer ");
    expect(result.success).toBe(false);
  });

  it.each([1, 2, 4, 5])(
    "rejects JWT with wrong segment count (%i)",
    (count) => {
      const token =
        "a".repeat(count - 1) +
        "." +
        Array(count - 1)
          .fill("x")
          .join(".");
      const segments = Array(count).fill("eyJh").join(".");
      const result = parseAuthHeader(`Bearer ${segments}`);
      expect(result.success).toBe(false);
      expect(result.errorCode).toBe(AuthErrorCodes.AUTH_BEARER_MALFORMED);
      // unused — silence TS
      void token;
    },
  );

  it("rejects empty payload segment", () => {
    const result = parseAuthHeader("Bearer eyJh..xyz");
    expect(result.success).toBe(false);
    expect(result.errorCode).toBe(AuthErrorCodes.AUTH_BEARER_MALFORMED);
  });

  it("rejects oversized JWT payload segment (>4KB decoded but <8KB total header)", () => {
    // Decoded payload ~4500 bytes triggers _MAX_SEGMENT_BYTES but encoded
    // segment (~6000 chars) keeps total Authorization header under 8192.
    const bigClaims = { sub: "a", padding: "x".repeat(4_500) };
    const header = `Bearer ${buildJwt(bigClaims)}`;
    expect(header.length).toBeLessThan(8 * 1024);
    const result = parseAuthHeader(header);
    expect(result.success).toBe(false);
    expect(result.errorCode).toBe(AuthErrorCodes.AUTH_BEARER_MALFORMED);
  });

  it("rejects payload that does not decode to JSON", () => {
    const header = "Bearer eyJh.notbase64orjson.xyz";
    const result = parseAuthHeader(header);
    expect(result.success).toBe(false);
    expect(result.errorCode).toBe(AuthErrorCodes.AUTH_BEARER_MALFORMED);
  });

  it("rejects payload whose JSON is not an object (string)", () => {
    const header = `Bearer eyJh.${Buffer.from('"not-an-object"').toString("base64url")}.xyz`;
    const result = parseAuthHeader(header);
    expect(result.success).toBe(false);
    expect(result.errorCode).toBe(AuthErrorCodes.AUTH_BEARER_MALFORMED);
  });

  it("rejects payload whose JSON is not an object (array)", () => {
    const header = `Bearer eyJh.${Buffer.from("[1, 2, 3]").toString("base64url")}.xyz`;
    const result = parseAuthHeader(header);
    expect(result.success).toBe(false);
    expect(result.errorCode).toBe(AuthErrorCodes.AUTH_BEARER_MALFORMED);
  });

  it("rejects payload whose JSON is null", () => {
    const header = `Bearer eyJh.${Buffer.from("null").toString("base64url")}.xyz`;
    const result = parseAuthHeader(header);
    expect(result.success).toBe(false);
    expect(result.errorCode).toBe(AuthErrorCodes.AUTH_BEARER_MALFORMED);
  });

  it("rejects sub claim with non-string type", () => {
    const header = `Bearer ${buildJwt({ ...VALID_CLAIMS, sub: 123 })}`;
    const result = parseAuthHeader(header);
    expect(result.success).toBe(false);
    expect(result.errorCode).toBe(AuthErrorCodes.AUTH_BEARER_MALFORMED);
  });

  it("rejects aud claim with malformed array (mixed types)", () => {
    const header = `Bearer ${buildJwt({ ...VALID_CLAIMS, aud: ["d2.edge", 42] })}`;
    const result = parseAuthHeader(header);
    expect(result.success).toBe(false);
    expect(result.errorCode).toBe(AuthErrorCodes.AUTH_BEARER_MALFORMED);
  });

  it("rejects aud claim with non-string scalar", () => {
    const header = `Bearer ${buildJwt({ ...VALID_CLAIMS, aud: 42 })}`;
    const result = parseAuthHeader(header);
    expect(result.success).toBe(false);
  });

  it("filters empty strings from aud array", () => {
    const header = `Bearer ${buildJwt({ ...VALID_CLAIMS, aud: ["d2.edge", "", "d2.files"] })}`;
    const result = parseAuthHeader(header);
    expect(result.success).toBe(true);
    expect(result.data!.aud).toEqual(["d2.edge", "d2.files"]);
  });

  it("aud absent surfaces as empty array", () => {
    const claims = { ...VALID_CLAIMS } as Record<string, unknown>;
    delete claims["aud"];
    const header = `Bearer ${buildJwt(claims)}`;
    const result = parseAuthHeader(header);
    expect(result.success).toBe(true);
    expect(result.data!.aud).toEqual([]);
  });

  it("aud as empty string surfaces as empty array", () => {
    const header = `Bearer ${buildJwt({ ...VALID_CLAIMS, aud: "" })}`;
    const result = parseAuthHeader(header);
    expect(result.success).toBe(true);
    expect(result.data!.aud).toEqual([]);
  });

  it("non-finite iat/exp surface as undefined", () => {
    const header = `Bearer ${buildJwt({ ...VALID_CLAIMS, iat: NaN, exp: Infinity })}`;
    const result = parseAuthHeader(header);
    expect(result.success).toBe(true);
    expect(result.data!.iat).toBeUndefined();
    expect(result.data!.exp).toBeUndefined();
  });

  it("non-string non-numeric claim values surface as undefined", () => {
    const header = `Bearer ${buildJwt({ ...VALID_CLAIMS, d2_username: 99 })}`;
    const result = parseAuthHeader(header);
    expect(result.success).toBe(true);
    expect(result.data!.d2_username).toBeUndefined();
  });

  it("empty-string claim values surface as undefined (not '')", () => {
    const header = `Bearer ${buildJwt({ ...VALID_CLAIMS, d2_username: "" })}`;
    const result = parseAuthHeader(header);
    expect(result.success).toBe(true);
    expect(result.data!.d2_username).toBeUndefined();
  });

  it("act claim with non-object value surfaces as undefined", () => {
    const header = `Bearer ${buildJwt({ ...VALID_CLAIMS, act: "not-an-object" })}`;
    const result = parseAuthHeader(header);
    expect(result.success).toBe(true);
    expect(result.data!.act).toBeUndefined();
  });

  it("act claim as array surfaces as undefined (RFC 8693 says nested object)", () => {
    const header = `Bearer ${buildJwt({ ...VALID_CLAIMS, act: [{ sub: "x" }] })}`;
    const result = parseAuthHeader(header);
    expect(result.success).toBe(true);
    expect(result.data!.act).toBeUndefined();
  });
});

describe("parseAuthHeader — JWT claim consumption parity", () => {
  it("uses JwtClaimTypes constants (per-key pin)", () => {
    // Verify each codegen constant maps to its expected JwtPayload field.
    const claims = {
      [JwtClaimTypes.SUB]: VALID_CLAIMS.sub,
      [JwtClaimTypes.AUD]: VALID_CLAIMS.aud,
      [JwtClaimTypes.IAT]: VALID_CLAIMS.iat,
      [JwtClaimTypes.EXP]: VALID_CLAIMS.exp,
      [JwtClaimTypes.SCOPE]: VALID_CLAIMS.scope,
      [JwtClaimTypes.SESSION_ID]: VALID_CLAIMS.d2_session_id,
      [JwtClaimTypes.USERNAME]: VALID_CLAIMS.d2_username,
      [JwtClaimTypes.ORG_ID]: VALID_CLAIMS.d2_org_id,
      [JwtClaimTypes.ORG_NAME]: VALID_CLAIMS.d2_org_name,
      [JwtClaimTypes.ORG_TYPE]: VALID_CLAIMS.d2_org_type,
      [JwtClaimTypes.ORG_ROLE]: VALID_CLAIMS.d2_org_role,
    };
    const header = `Bearer ${buildJwt(claims)}`;
    const result = parseAuthHeader(header);
    expect(result.success).toBe(true);
    expect(result.data!.d2_session_id).toBe(VALID_CLAIMS.d2_session_id);
    expect(result.data!.d2_org_id).toBe(VALID_CLAIMS.d2_org_id);
    expect(result.data!.d2_org_role).toBe(VALID_CLAIMS.d2_org_role);
  });
});
