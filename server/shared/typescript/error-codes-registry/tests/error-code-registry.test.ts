// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import {
  buildRegistry,
  type ErrorCategory,
  type ErrorCodeFactoryShape,
  type ErrorCodeInfo,
} from "../src/error-code-registry.js";
import { errorCodeRegistry } from "../src/generated/error-code-registry.g.js";
import { TK } from "@d2/i18n-keys";

// ---------------------------------------------------------------------------
// buildRegistry — unit tests
// ---------------------------------------------------------------------------

describe("buildRegistry — resolution API", () => {
  const entries: readonly ErrorCodeInfo[] = [
    {
      code: "NOT_FOUND",
      httpStatus: 404,
      category: "not_found",
      userMessageKey: TK.common.errors.NOT_FOUND,
      factoryName: "NotFound",
      factoryShape: "standard",
      doc: "Not found.",
      domain: "common",
    },
    {
      code: "AUTH_BEARER_MISSING",
      httpStatus: 401,
      category: "validation_failure",
      userMessageKey: TK.auth.errors.UNAUTHORIZED,
      factoryName: "BearerMissing",
      factoryShape: "standard",
      doc: "Bearer missing.",
      domain: "auth",
    },
  ];
  const registry = buildRegistry(entries);

  it("resolve: known code returns full 8-field ErrorCodeInfo", () => {
    const info = registry.resolve("NOT_FOUND");
    expect(info).toBeDefined();
    expect(info?.code).toBe("NOT_FOUND");
    expect(info?.httpStatus).toBe(404);
    expect(info?.category).toBe("not_found");
    expect(info?.userMessageKey).toBe(TK.common.errors.NOT_FOUND);
    expect(info?.factoryName).toBe("NotFound");
    expect(info?.factoryShape).toBe("standard");
    expect(info?.doc).toBe("Not found.");
    expect(info?.domain).toBe("common");
  });

  it("resolve: unknown code returns undefined (hard not-found)", () => {
    expect(registry.resolve("NOPE")).toBeUndefined();
    expect(registry.resolve("NOT_EXISTING")).toBeUndefined();
  });

  it("resolve: empty string returns undefined", () => {
    expect(registry.resolve("")).toBeUndefined();
  });

  it("resolve: whitespace-only string returns undefined", () => {
    expect(registry.resolve("   ")).toBeUndefined();
  });

  it("resolve: lowercase code returns undefined (ordinal case-sensitive)", () => {
    // Codes are SCREAMING_SNAKE — lowercase must not resolve.
    expect(registry.resolve("not_found")).toBeUndefined();
    expect(registry.resolve("auth_bearer_missing")).toBeUndefined();
  });

  it("resolve: mixed-case code returns undefined", () => {
    expect(registry.resolve("Not_Found")).toBeUndefined();
  });

  it("has: known code returns true", () => {
    expect(registry.has("NOT_FOUND")).toBe(true);
    expect(registry.has("AUTH_BEARER_MISSING")).toBe(true);
  });

  it("has: unknown code returns false", () => {
    expect(registry.has("NOPE")).toBe(false);
  });

  it("has: empty string returns false", () => {
    expect(registry.has("")).toBe(false);
  });

  it("has: lowercase returns false (ordinal case-sensitive)", () => {
    expect(registry.has("not_found")).toBe(false);
  });

  it("all: contains every registered entry", () => {
    expect(registry.all).toHaveLength(2);
    expect(registry.all.map((e) => e.code)).toContain("NOT_FOUND");
    expect(registry.all.map((e) => e.code)).toContain("AUTH_BEARER_MISSING");
  });

  it("all: is frozen (immutable)", () => {
    expect(Object.isFrozen(registry.all)).toBe(true);
  });

  it("resolve: auth entry has correct 8 fields", () => {
    const info = registry.resolve("AUTH_BEARER_MISSING");
    expect(info?.code).toBe("AUTH_BEARER_MISSING");
    expect(info?.httpStatus).toBe(401);
    expect(info?.category).toBe("validation_failure");
    expect(info?.userMessageKey).toBe(TK.auth.errors.UNAUTHORIZED);
    expect(info?.factoryName).toBe("BearerMissing");
    expect(info?.factoryShape).toBe("standard");
    expect(info?.doc).toBe("Bearer missing.");
    expect(info?.domain).toBe("auth");
  });

  it("buildRegistry with empty entries produces an empty registry", () => {
    const empty = buildRegistry([]);
    expect(empty.all).toHaveLength(0);
    expect(empty.resolve("NOT_FOUND")).toBeUndefined();
    expect(empty.has("NOT_FOUND")).toBe(false);
  });
});

// ---------------------------------------------------------------------------
// ErrorCategory type — all 9 schema values are valid
// ---------------------------------------------------------------------------

describe("ErrorCategory type — 9 schema values", () => {
  it("covers all 9 canonical category values", () => {
    const categories: ErrorCategory[] = [
      "validation_failure",
      "not_found",
      "conflict",
      "policy_denied",
      "rate_limited",
      "payload_too_large",
      "infrastructure_unavailable",
      "internal_error",
      "partial_success",
    ];
    // All 9 values — if the type is narrowed incorrectly, a TS error will fire.
    expect(categories).toHaveLength(9);
  });
});

// ---------------------------------------------------------------------------
// ErrorCodeFactoryShape type — the 2 schema values are valid
// ---------------------------------------------------------------------------

describe("ErrorCodeFactoryShape type — 2 schema values", () => {
  it("covers both canonical factoryShape values", () => {
    const shapes: ErrorCodeFactoryShape[] = ["standard", "none"];
    expect(shapes).toHaveLength(2);
  });
});

// ---------------------------------------------------------------------------
// Generated errorCodeRegistry — the real merged registry
// ---------------------------------------------------------------------------

describe("errorCodeRegistry — generated merged registry", () => {
  it("all: contains the expected total count (14 auth + 15 generic + 23 keycustodian = 52)", () => {
    expect(errorCodeRegistry.all).toHaveLength(52);
  });

  it("resolve: every generic code resolves with domain 'common'", () => {
    const genericCodes = [
      "NOT_FOUND",
      "FORBIDDEN",
      "UNAUTHORIZED",
      "VALIDATION_FAILED",
      "CONFLICT",
      "UNHANDLED_EXCEPTION",
      "COULD_NOT_BE_SERIALIZED",
      "COULD_NOT_BE_DESERIALIZED",
      "SERVICE_UNAVAILABLE",
      "SOME_FOUND",
      "PARTIAL_SUCCESS",
      "RATE_LIMITED",
      "IDEMPOTENCY_IN_FLIGHT",
      "PAYLOAD_TOO_LARGE",
      "CANCELED",
    ];
    for (const code of genericCodes) {
      const info = errorCodeRegistry.resolve(code);
      expect(info, `${code} should resolve`).toBeDefined();
      expect(info?.domain, `${code} domain should be 'common'`).toBe("common");
    }
  });

  it("resolve: every auth code resolves with domain 'auth'", () => {
    const authCodes = [
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
    ];
    for (const code of authCodes) {
      const info = errorCodeRegistry.resolve(code);
      expect(info, `${code} should resolve`).toBeDefined();
      expect(info?.domain, `${code} domain should be 'auth'`).toBe("auth");
    }
  });

  it("resolve: every keycustodian code resolves with domain 'keycustodian'", () => {
    const keycustodianCodes = [
      "KEYCUSTODIAN_KID_INVALID",
      "KEYCUSTODIAN_KID_TOO_LONG",
      "KEYCUSTODIAN_UNKNOWN_KEY_DOMAIN",
      "KEYCUSTODIAN_INVALID_ROTATION_POLICY",
      "KEYCUSTODIAN_SOAK_NOT_ELAPSED",
      "KEYCUSTODIAN_SMOKE_PROOF_TYPE_MISMATCH",
      "KEYCUSTODIAN_GRACE_NOT_ELAPSED",
      "KEYCUSTODIAN_PRECONDITION_VIOLATED",
      "KEYCUSTODIAN_KEY_NOT_FOUND",
      "KEYCUSTODIAN_KEY_STATE_CONFLICT",
      "KEYCUSTODIAN_PENDING_KEY_ALREADY_EXISTS",
      "KEYCUSTODIAN_SMOKE_TEST_FAILED",
      "KEYCUSTODIAN_INVALID_WORKLOAD_IDENTITY",
      "KEYCUSTODIAN_INVALID_CERTIFICATE_REQUEST",
      "KEYCUSTODIAN_NO_ACTIVE_ISSUING_CA",
      "KEYCUSTODIAN_CROSS_PROCESS_DOMAIN_REJECTED",
      "KEYCUSTODIAN_SIGNING_DOMAIN_NOT_AUTHORIZED",
    ];
    for (const code of keycustodianCodes) {
      const info = errorCodeRegistry.resolve(code);
      expect(info, `${code} should resolve`).toBeDefined();
      expect(info?.domain, `${code} domain should be 'keycustodian'`).toBe(
        "keycustodian",
      );
    }
  });

  it("resolve: unknown code returns undefined", () => {
    expect(errorCodeRegistry.resolve("NOPE")).toBeUndefined();
    expect(errorCodeRegistry.resolve("UNKNOWN_CODE")).toBeUndefined();
  });

  it("resolve: empty string returns undefined", () => {
    expect(errorCodeRegistry.resolve("")).toBeUndefined();
  });

  it("resolve: whitespace-only returns undefined", () => {
    expect(errorCodeRegistry.resolve("  ")).toBeUndefined();
  });

  it("resolve: lowercase returns undefined (case-sensitive)", () => {
    expect(errorCodeRegistry.resolve("not_found")).toBeUndefined();
    expect(errorCodeRegistry.resolve("auth_bearer_missing")).toBeUndefined();
  });

  it("has: all registered codes return true", () => {
    for (const info of errorCodeRegistry.all) {
      expect(
        errorCodeRegistry.has(info.code),
        `has(${info.code}) should be true`,
      ).toBe(true);
    }
  });

  it("has: unknown code returns false", () => {
    expect(errorCodeRegistry.has("TOTALLY_UNKNOWN")).toBe(false);
  });

  it("resolve NOT_FOUND: full 8-field correctness", () => {
    const info = errorCodeRegistry.resolve("NOT_FOUND");
    expect(info).toBeDefined();
    expect(info?.code).toBe("NOT_FOUND");
    expect(info?.httpStatus).toBe(404);
    expect(info?.category).toBe("not_found");
    expect(info?.userMessageKey).toBe(TK.common.errors.NOT_FOUND);
    expect(info?.factoryName).toBe("NotFound");
    expect(info?.factoryShape).toBe("standard");
    expect(info?.doc).toMatch(/resource was not found/i);
    expect(info?.domain).toBe("common");
  });

  it("resolve AUTH_JWKS_UNAVAILABLE: full 8-field correctness", () => {
    const info = errorCodeRegistry.resolve("AUTH_JWKS_UNAVAILABLE");
    expect(info).toBeDefined();
    expect(info?.code).toBe("AUTH_JWKS_UNAVAILABLE");
    expect(info?.httpStatus).toBe(503);
    expect(info?.category).toBe("infrastructure_unavailable");
    expect(info?.userMessageKey).toBe(TK.auth.errors.TEMPORARILY_UNAVAILABLE);
    expect(info?.factoryName).toBe("JwksUnavailable");
    expect(info?.factoryShape).toBe("standard");
    expect(info?.domain).toBe("auth");
  });

  it("resolve KEYCUSTODIAN_PRECONDITION_VIOLATED: full 8-field correctness", () => {
    const info = errorCodeRegistry.resolve(
      "KEYCUSTODIAN_PRECONDITION_VIOLATED",
    );
    expect(info).toBeDefined();
    expect(info?.code).toBe("KEYCUSTODIAN_PRECONDITION_VIOLATED");
    expect(info?.httpStatus).toBe(500);
    expect(info?.category).toBe("internal_error");
    expect(info?.userMessageKey).toBe(
      TK.keycustodian.internal.PRECONDITION_VIOLATED,
    );
    expect(info?.factoryName).toBe("PreconditionViolated");
    expect(info?.factoryShape).toBe("standard");
    expect(info?.domain).toBe("keycustodian");
  });

  it("resolve KEYCUSTODIAN_KEY_NOT_FOUND: full 8-field correctness", () => {
    const info = errorCodeRegistry.resolve("KEYCUSTODIAN_KEY_NOT_FOUND");
    expect(info).toBeDefined();
    expect(info?.code).toBe("KEYCUSTODIAN_KEY_NOT_FOUND");
    expect(info?.httpStatus).toBe(404);
    expect(info?.category).toBe("not_found");
    expect(info?.userMessageKey).toBe(TK.keycustodian.lifecycle.KEY_NOT_FOUND);
    expect(info?.factoryName).toBe("KeyNotFound");
    expect(info?.factoryShape).toBe("standard");
    expect(info?.domain).toBe("keycustodian");
  });

  it("resolve KEYCUSTODIAN_KEY_STATE_CONFLICT: full 8-field correctness", () => {
    const info = errorCodeRegistry.resolve("KEYCUSTODIAN_KEY_STATE_CONFLICT");
    expect(info).toBeDefined();
    expect(info?.code).toBe("KEYCUSTODIAN_KEY_STATE_CONFLICT");
    expect(info?.httpStatus).toBe(409);
    expect(info?.category).toBe("conflict");
    expect(info?.userMessageKey).toBe(
      TK.keycustodian.lifecycle.KEY_STATE_CONFLICT,
    );
    expect(info?.factoryName).toBe("KeyStateConflict");
    expect(info?.factoryShape).toBe("standard");
    expect(info?.domain).toBe("keycustodian");
  });

  it("every userMessageKey is a TKMessage with a non-empty key string", () => {
    for (const info of errorCodeRegistry.all) {
      expect(
        typeof info.userMessageKey.key === "string" &&
          info.userMessageKey.key.length > 0,
        `${info.code}: userMessageKey.key must be a non-empty string`,
      ).toBe(true);
    }
  });

  it("every category value is one of the 9 canonical values", () => {
    const valid = new Set<string>([
      "validation_failure",
      "not_found",
      "conflict",
      "policy_denied",
      "rate_limited",
      "payload_too_large",
      "infrastructure_unavailable",
      "internal_error",
      "partial_success",
    ]);
    for (const info of errorCodeRegistry.all) {
      expect(
        valid.has(info.category),
        `${info.code}: category '${info.category}' is not a known value`,
      ).toBe(true);
    }
  });

  it("every factoryShape value is one of the 2 canonical values", () => {
    const valid = new Set<string>(["standard", "none"]);
    for (const info of errorCodeRegistry.all) {
      expect(
        valid.has(info.factoryShape),
        `${info.code}: factoryShape '${info.factoryShape}' is not a known value`,
      ).toBe(true);
    }
  });

  it("every code in 'all' matches SCREAMING_SNAKE pattern", () => {
    const re = /^[A-Z][A-Z0-9_]*$/;
    for (const info of errorCodeRegistry.all) {
      expect(
        re.test(info.code),
        `${info.code}: does not match SCREAMING_SNAKE`,
      ).toBe(true);
    }
  });

  it("no duplicate codes in 'all'", () => {
    const codes = errorCodeRegistry.all.map((e) => e.code);
    const unique = new Set(codes);
    expect(unique.size).toBe(codes.length);
  });
});
