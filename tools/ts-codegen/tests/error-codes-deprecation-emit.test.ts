// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";

import {
  AUTH_CONFIG,
  AUTH_FAILURES_CONFIG,
  emitBaseFactoriesCatalog,
  emitErrorCodesCatalog,
  emitFailuresCatalog,
  GENERIC_CONFIG,
  GENERIC_FACTORIES_CONFIG,
  type ErrorCodeEntry,
  type ErrorCodesSpec,
} from "../src/error-codes-emit.js";

// ---------------------------------------------------------------------------
// Contract-side deprecate-not-delete marker on the TS error-code emitters.
// A deprecated entry MUST emit a `@deprecated <reason>. Use <replacedBy>
// instead.` JSDoc tag on the emitted constant + factory (mirroring the .NET
// [Obsolete] message); a non-deprecated entry MUST emit no `@deprecated` tag.
// Driven by SYNTHETIC fixture entries — no real production spec is deprecated.
// ---------------------------------------------------------------------------

const EXPECTED_TAG =
  "@deprecated Ambiguous between resource-missing and route-missing;" +
  " split into two codes. Use RESOURCE_NOT_FOUND instead.";

const GENERIC_EN_US_KEYS = new Set(["common_errors_NOT_FOUND"]);
const AUTH_EN_US_KEYS = new Set(["auth_errors_UNAUTHORIZED"]);

function deprecatedGenericEntry(
  overrides: Partial<ErrorCodeEntry> = {},
): ErrorCodeEntry {
  return {
    code: "NOT_FOUND",
    httpStatus: 404,
    category: "not_found",
    userMessageKey: "TK.Common.Errors.NOT_FOUND",
    factoryName: "NotFound",
    factoryShape: "standard",
    doc: "Indicates that the requested resource was not found.",
    deprecated: true,
    deprecatedReason:
      "Ambiguous between resource-missing and route-missing; split into two codes.",
    replacedBy: "RESOURCE_NOT_FOUND",
    sunset: "2027-01-01",
    ...overrides,
  };
}

describe("emitErrorCodesCatalog — deprecation marker (generic constants)", () => {
  it("emits the @deprecated tag inside the per-code JSDoc", () => {
    const spec: ErrorCodesSpec = { errorCodes: [deprecatedGenericEntry()] };
    const r = emitErrorCodesCatalog(spec, GENERIC_CONFIG, GENERIC_EN_US_KEYS);

    expect(r.diagnostics.filter((d) => d.severity === "error")).toEqual([]);
    expect(r.source).toContain(EXPECTED_TAG);
    expect(r.source).toContain('NOT_FOUND: "NOT_FOUND",');
    // The tag lives in the JSDoc block, before the constant member.
    expect(r.source.indexOf(EXPECTED_TAG)).toBeLessThan(
      r.source.indexOf('NOT_FOUND: "NOT_FOUND"'),
    );
  });

  it("reason-only (no replacedBy) omits the 'Use ... instead.' suffix", () => {
    const spec: ErrorCodesSpec = {
      errorCodes: [deprecatedGenericEntry({ replacedBy: undefined })],
    };
    const r = emitErrorCodesCatalog(spec, GENERIC_CONFIG, GENERIC_EN_US_KEYS);

    expect(r.source).toContain(
      "@deprecated Ambiguous between resource-missing and route-missing;" +
        " split into two codes.",
    );
    expect(r.source).not.toContain("Use RESOURCE_NOT_FOUND instead.");
  });

  it("non-deprecated entry emits no @deprecated tag", () => {
    const spec: ErrorCodesSpec = {
      errorCodes: [deprecatedGenericEntry({ deprecated: false })],
    };
    const r = emitErrorCodesCatalog(spec, GENERIC_CONFIG, GENERIC_EN_US_KEYS);

    expect(r.source).not.toContain("@deprecated");
    expect(r.source).toContain('NOT_FOUND: "NOT_FOUND",');
  });
});

describe("emitErrorCodesCatalog — deprecation marker (auth constants, no per-code doc)", () => {
  it("emits a standalone @deprecated JSDoc even though auth omits per-code docs", () => {
    const spec: ErrorCodesSpec = {
      errorCodes: [
        {
          code: "AUTH_BEARER_MISSING",
          httpStatus: 401,
          category: "validation_failure",
          userMessageKey: "TK.Auth.Errors.UNAUTHORIZED",
          factoryName: "BearerMissing",
          factoryShape: "standard",
          doc: "Bearer missing.",
          deprecated: true,
          deprecatedReason: "Superseded by a clearer split.",
          replacedBy: "AUTH_BEARER_ABSENT",
        },
      ],
    };
    const r = emitErrorCodesCatalog(spec, AUTH_CONFIG, AUTH_EN_US_KEYS);

    expect(r.diagnostics.filter((d) => d.severity === "error")).toEqual([]);
    expect(r.source).toContain(
      "@deprecated Superseded by a clearer split. Use AUTH_BEARER_ABSENT instead.",
    );
    expect(r.source).toContain('AUTH_BEARER_MISSING: "AUTH_BEARER_MISSING",');
  });
});

describe("emitFailuresCatalog — deprecation marker (delegating factory)", () => {
  it("emits the @deprecated tag on the deprecated factory's JSDoc", () => {
    const spec: ErrorCodesSpec = {
      errorCodes: [
        {
          code: "AUTH_BEARER_MISSING",
          httpStatus: 401,
          category: "validation_failure",
          userMessageKey: "TK.Auth.Errors.UNAUTHORIZED",
          factoryName: "BearerMissing",
          factoryShape: "standard",
          doc: "Bearer missing.",
          deprecated: true,
          deprecatedReason: "Superseded by a clearer split.",
          replacedBy: "AUTH_BEARER_ABSENT",
        },
      ],
    };
    const r = emitFailuresCatalog(
      spec,
      AUTH_CONFIG,
      AUTH_FAILURES_CONFIG,
      AUTH_EN_US_KEYS,
    );

    expect(r.diagnostics.filter((d) => d.severity === "error")).toEqual([]);
    expect(r.source).toContain(
      "@deprecated Superseded by a clearer split. Use AUTH_BEARER_ABSENT instead.",
    );
    expect(r.source).toContain("bearerMissing<T = void>");
  });

  it("non-deprecated factory emits no @deprecated tag", () => {
    const spec: ErrorCodesSpec = {
      errorCodes: [
        {
          code: "AUTH_BEARER_MISSING",
          httpStatus: 401,
          category: "validation_failure",
          userMessageKey: "TK.Auth.Errors.UNAUTHORIZED",
          factoryName: "BearerMissing",
          factoryShape: "standard",
          doc: "Bearer missing.",
        },
      ],
    };
    const r = emitFailuresCatalog(
      spec,
      AUTH_CONFIG,
      AUTH_FAILURES_CONFIG,
      AUTH_EN_US_KEYS,
    );

    expect(r.source).not.toContain("@deprecated");
  });
});

describe("emitBaseFactoriesCatalog — deprecation marker (constructing factory)", () => {
  it("emits the @deprecated tag on the deprecated base factory's JSDoc", () => {
    const spec: ErrorCodesSpec = { errorCodes: [deprecatedGenericEntry()] };
    const r = emitBaseFactoriesCatalog(
      spec,
      GENERIC_CONFIG,
      GENERIC_FACTORIES_CONFIG,
      GENERIC_EN_US_KEYS,
    );

    expect(r.diagnostics.filter((d) => d.severity === "error")).toEqual([]);
    expect(r.source).toContain(EXPECTED_TAG);
    expect(r.source).toContain("export function notFound<T = void>");
    // The tag precedes the function declaration.
    expect(r.source.indexOf(EXPECTED_TAG)).toBeLessThan(
      r.source.indexOf("export function notFound"),
    );
  });

  it("non-deprecated base factory emits no @deprecated tag", () => {
    const spec: ErrorCodesSpec = {
      errorCodes: [deprecatedGenericEntry({ deprecated: false })],
    };
    const r = emitBaseFactoriesCatalog(
      spec,
      GENERIC_CONFIG,
      GENERIC_FACTORIES_CONFIG,
      GENERIC_EN_US_KEYS,
    );

    expect(r.source).not.toContain("@deprecated");
    expect(r.source).toContain("export function notFound<T = void>");
  });
});

// ---------------------------------------------------------------------------
// sunset field is inert — no emitter reads it; its presence on an entry
// MUST NOT affect the emitted output in any way (§26.8 inertness assertion).
// The field is declared on ErrorCodeEntry as a forward-registration slot for
// the RFC 8594 Sunset response header; no emitter function reads it today.
// ---------------------------------------------------------------------------

describe("sunset field — inert; never appears in emitted output", () => {
  it("emitErrorCodesCatalog: sunset entry produces identical output to entry without sunset", () => {
    const withSunset: ErrorCodesSpec = {
      errorCodes: [deprecatedGenericEntry({ sunset: "2027-01-01" })],
    };
    const withoutSunset: ErrorCodesSpec = {
      errorCodes: [deprecatedGenericEntry({ sunset: undefined })],
    };

    const rWith = emitErrorCodesCatalog(
      withSunset,
      GENERIC_CONFIG,
      GENERIC_EN_US_KEYS,
    );
    const rWithout = emitErrorCodesCatalog(
      withoutSunset,
      GENERIC_CONFIG,
      GENERIC_EN_US_KEYS,
    );

    expect(rWith.source).toBe(rWithout.source);
    expect(rWith.source).not.toContain("sunset");
  });

  it("emitFailuresCatalog: sunset does not appear in emitted output", () => {
    const spec: ErrorCodesSpec = {
      errorCodes: [
        {
          code: "AUTH_BEARER_MISSING",
          httpStatus: 401,
          category: "validation_failure",
          userMessageKey: "TK.Auth.Errors.UNAUTHORIZED",
          factoryName: "BearerMissing",
          factoryShape: "standard",
          doc: "Bearer missing.",
          deprecated: true,
          deprecatedReason: "Superseded.",
          replacedBy: "AUTH_BEARER_ABSENT",
          sunset: "2027-06-01",
        },
      ],
    };
    const r = emitFailuresCatalog(
      spec,
      AUTH_CONFIG,
      AUTH_FAILURES_CONFIG,
      AUTH_EN_US_KEYS,
    );

    expect(r.source).not.toContain("sunset");
  });

  it("emitBaseFactoriesCatalog: sunset does not appear in emitted output", () => {
    const spec: ErrorCodesSpec = {
      errorCodes: [deprecatedGenericEntry({ sunset: "2028-12-31" })],
    };
    const r = emitBaseFactoriesCatalog(
      spec,
      GENERIC_CONFIG,
      GENERIC_FACTORIES_CONFIG,
      GENERIC_EN_US_KEYS,
    );

    expect(r.source).not.toContain("sunset");
  });
});
