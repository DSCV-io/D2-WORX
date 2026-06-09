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
  type CatalogConfig,
  type ErrorCodesSpec,
  validateErrorCodesSpec,
} from "../src/error-codes-emit.js";

const validSpec: ErrorCodesSpec = {
  errorCodes: [
    {
      code: "NOT_FOUND",
      httpStatus: 404,
      doc: "Indicates that the requested resource was not found.",
    },
    {
      code: "SERVICE_UNAVAILABLE",
      httpStatus: 503,
      doc: "Indicates that the service is currently unavailable.",
    },
  ],
};

describe("validateErrorCodesSpec (generic catalog)", () => {
  it("happy path returns all entries with no diagnostics", () => {
    const v = validateErrorCodesSpec(validSpec, GENERIC_CONFIG);
    expect(v.entries).toHaveLength(2);
    expect(v.diagnostics).toEqual([]);
  });

  it("flags duplicate codes", () => {
    const v = validateErrorCodesSpec(
      { errorCodes: [validSpec.errorCodes[0]!, validSpec.errorCodes[0]!] },
      GENERIC_CONFIG,
    );
    expect(v.diagnostics[0]?.id).toBe("D2EC002");
  });

  it("flags unsupported httpStatus", () => {
    const v = validateErrorCodesSpec(
      { errorCodes: [{ ...validSpec.errorCodes[0]!, httpStatus: 418 }] },
      GENERIC_CONFIG,
    );
    expect(v.diagnostics[0]?.id).toBe("D2EC003");
  });

  it("flags invalid lowercase code", () => {
    const v = validateErrorCodesSpec(
      { errorCodes: [{ ...validSpec.errorCodes[0]!, code: "lowercase" }] },
      GENERIC_CONFIG,
    );
    expect(v.diagnostics[0]?.id).toBe("D2EC004");
  });

  it("flags empty code", () => {
    const v = validateErrorCodesSpec(
      { errorCodes: [{ ...validSpec.errorCodes[0]!, code: "" }] },
      GENERIC_CONFIG,
    );
    expect(v.diagnostics[0]?.id).toBe("D2EC004");
  });

  it("flags code starting with a digit", () => {
    const v = validateErrorCodesSpec(
      { errorCodes: [{ ...validSpec.errorCodes[0]!, code: "9NOPE" }] },
      GENERIC_CONFIG,
    );
    expect(v.diagnostics[0]?.id).toBe("D2EC004");
  });

  it("flags missing/empty doc", () => {
    const v = validateErrorCodesSpec(
      { errorCodes: [{ ...validSpec.errorCodes[0]!, doc: "" }] },
      GENERIC_CONFIG,
    );
    expect(v.diagnostics[0]?.id).toBe("D2EC005");
  });

  it("flags whitespace-only doc", () => {
    const v = validateErrorCodesSpec(
      { errorCodes: [{ ...validSpec.errorCodes[0]!, doc: "   " }] },
      GENERIC_CONFIG,
    );
    expect(v.diagnostics[0]?.id).toBe("D2EC005");
  });
});

describe("emitErrorCodesCatalog (generic) — snapshot pin", () => {
  it("emits constants + http-status switch in spec order", () => {
    const r = emitErrorCodesCatalog(validSpec, GENERIC_CONFIG);
    expect(r.diagnostics).toEqual([]);
    expect(r.source).toContain('NOT_FOUND: "NOT_FOUND"');
    expect(r.source).toContain('SERVICE_UNAVAILABLE: "SERVICE_UNAVAILABLE"');
    expect(r.source).toContain(
      "export const ALL_ERROR_CODES: readonly string[]",
    );
    expect(r.source).toContain('case "NOT_FOUND": return 404;');
    expect(r.source).toContain('case "SERVICE_UNAVAILABLE": return 503;');
    // Spec order: NOT_FOUND first, SERVICE_UNAVAILABLE second.
    expect(
      r.source.indexOf("NOT_FOUND") < r.source.indexOf("SERVICE_UNAVAILABLE"),
    ).toBe(true);
  });

  it("blocks emit on validation diagnostics", () => {
    const r = emitErrorCodesCatalog(
      { errorCodes: [{ ...validSpec.errorCodes[0]!, httpStatus: 999 }] },
      GENERIC_CONFIG,
    );
    expect(r.source).toBe("");
    expect(r.diagnostics).not.toEqual([]);
  });

  it("produces identical source across two runs (idempotency)", () => {
    const first = emitErrorCodesCatalog(validSpec, GENERIC_CONFIG).source;
    const second = emitErrorCodesCatalog(validSpec, GENERIC_CONFIG).source;
    expect(second).toBe(first);
  });

  it("escapes JSDoc-terminator sequences in doc text", () => {
    const r = emitErrorCodesCatalog(
      {
        errorCodes: [
          { code: "TRICKY", httpStatus: 400, doc: "Has */ inside." },
        ],
      },
      GENERIC_CONFIG,
    );
    expect(r.diagnostics).toEqual([]);
    expect(r.source).toContain("Has *\\/ inside.");
  });

  it("preserves spec order (generic does NOT sort)", () => {
    const r = emitErrorCodesCatalog(
      {
        errorCodes: [
          { code: "ZEBRA", httpStatus: 400, doc: "z." },
          { code: "ALPHA", httpStatus: 400, doc: "a." },
        ],
      },
      GENERIC_CONFIG,
    );
    expect(r.source.indexOf("ZEBRA") < r.source.indexOf("ALPHA")).toBe(true);
  });
});

describe("emitErrorCodesCatalog (generic) — per-VALUE pin for the shipping spec", () => {
  it.each([
    ["NOT_FOUND", 404],
    ["FORBIDDEN", 403],
    ["UNAUTHORIZED", 401],
    ["VALIDATION_FAILED", 400],
    ["CONFLICT", 409],
    ["UNHANDLED_EXCEPTION", 500],
    ["COULD_NOT_BE_SERIALIZED", 500],
    ["COULD_NOT_BE_DESERIALIZED", 500],
    ["SERVICE_UNAVAILABLE", 503],
    ["SOME_FOUND", 206],
    ["PARTIAL_SUCCESS", 207],
    ["RATE_LIMITED", 429],
    ["IDEMPOTENCY_IN_FLIGHT", 409],
    ["PAYLOAD_TOO_LARGE", 413],
    ["CANCELED", 400],
  ])("entry %s maps to httpStatus %s", (code, httpStatus) => {
    const r = emitErrorCodesCatalog(
      { errorCodes: [{ code, httpStatus, doc: `${code} doc.` }] },
      GENERIC_CONFIG,
    );
    expect(r.diagnostics).toEqual([]);
    expect(r.source).toContain(`${code}: "${code}"`);
    expect(r.source).toContain(`case "${code}": return ${httpStatus};`);
  });
});

// ---------------------------------------------------------------------------
// Shared-engine diagnostics: D2ERC001 (domain prefix), D2ERC002 (TK key
// existence), D2ERC003 (unsupported factoryShape).
// ---------------------------------------------------------------------------

const authFactorySpec: ErrorCodesSpec = {
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

const AUTH_EN_US_KEYS = new Set([
  "auth_errors_UNAUTHORIZED",
  "auth_errors_TEMPORARILY_UNAVAILABLE",
]);

describe("D2ERC001 — domain-prefix enforcement", () => {
  it("fires when a code lacks the catalog's domain prefix", () => {
    const geoConfig: CatalogConfig = { ...AUTH_CONFIG, domainPrefix: "GEO_" };
    const v = validateErrorCodesSpec(
      {
        errorCodes: [
          {
            code: "FOO_BAR",
            httpStatus: 401,
            category: "validation_failure",
            userMessageKey: "TK.Auth.Errors.UNAUTHORIZED",
            factoryName: "FooBar",
            factoryShape: "with_error_code",
          },
        ],
      },
      geoConfig,
      AUTH_EN_US_KEYS,
    );
    expect(v.diagnostics[0]?.id).toBe("D2ERC001");
  });

  it("does NOT fire on the real auth spec (all AUTH_*)", () => {
    const v = validateErrorCodesSpec(
      authFactorySpec,
      AUTH_CONFIG,
      AUTH_EN_US_KEYS,
    );
    expect(v.diagnostics).toEqual([]);
  });

  it("does NOT fire on the generic catalog (no prefix enforced)", () => {
    const v = validateErrorCodesSpec(validSpec, GENERIC_CONFIG);
    expect(v.diagnostics).toEqual([]);
  });
});

describe("D2ERC002 — TK-key existence", () => {
  it("fires when userMessageKey does not resolve to an en-US key", () => {
    const v = validateErrorCodesSpec(
      {
        errorCodes: [
          {
            code: "AUTH_BEARER_MISSING",
            httpStatus: 401,
            category: "validation_failure",
            userMessageKey: "TK.Auth.Errors.DOES_NOT_EXIST",
            factoryName: "BearerMissing",
            factoryShape: "with_error_code",
          },
        ],
      },
      AUTH_CONFIG,
      AUTH_EN_US_KEYS,
    );
    expect(v.diagnostics[0]?.id).toBe("D2ERC002");
  });

  it("does NOT fire on the real auth keys", () => {
    const v = validateErrorCodesSpec(
      authFactorySpec,
      AUTH_CONFIG,
      AUTH_EN_US_KEYS,
    );
    expect(v.diagnostics).toEqual([]);
  });

  it("is skipped when no en-US key set is supplied (constants-only path)", () => {
    const v = validateErrorCodesSpec(authFactorySpec, AUTH_CONFIG);
    expect(v.diagnostics).toEqual([]);
  });
});

describe("emitFailuresCatalog — factoryShape branch (D2ERC003 fail-loud)", () => {
  it("with_error_code emits the factory referencing the TK constant", () => {
    const r = emitFailuresCatalog(
      authFactorySpec,
      AUTH_CONFIG,
      AUTH_FAILURES_CONFIG,
      AUTH_EN_US_KEYS,
    );
    expect(r.diagnostics).toEqual([]);
    expect(r.source).toContain('import { TK } from "@d2/i18n-keys";');
    // Generic methods (`<T = void>`) so one method spans the untyped + typed
    // domain-failure cases (the TS equivalent of .NET's two-class split).
    expect(r.source).toContain("bearerMissing<T = void>(traceId?: string)");
    expect(r.source).toContain("return unauthorized<T>");
    expect(r.source).toContain("jwksUnavailable<T = void>(traceId?: string)");
    expect(r.source).toContain("return serviceUnavailable<T>");
    // TK constant-reference rule: the emitted message must reference the
    // generated TS TK constant (`TK.*`) — itself a TKMessage instance — never a
    // raw PascalCase string literal (which silently bypasses the catalog and
    // won't render) and never a redundant `tk()` wrapper (the constant IS the
    // message now).
    expect(r.source).toContain("messages: [TK.auth.errors.UNAUTHORIZED]");
    expect(r.source).toContain(
      "messages: [TK.auth.errors.TEMPORARILY_UNAVAILABLE]",
    );
    expect(r.source).not.toContain("tk(TK.auth.errors.UNAUTHORIZED)");
    expect(r.source).not.toContain('tk("TK.Auth.Errors.UNAUTHORIZED")');
    // Each delegating factory stamps its OWN code's category onto the base
    // factory it delegates to (validation_failure overrides unauthorized's
    // policy_denied; infrastructure_unavailable matches serviceUnavailable).
    expect(r.source).toContain(
      'import { ErrorCategoryWire } from "@d2/error-category";',
    );
    expect(r.source).toContain("category: ErrorCategoryWire.ValidationFailure");
    expect(r.source).toContain(
      "category: ErrorCategoryWire.InfrastructureUnavailable",
    );
  });

  it("none skips the factory (constant exists but no AuthFailures entry)", () => {
    const r = emitFailuresCatalog(
      {
        errorCodes: [
          { ...authFactorySpec.errorCodes[0]!, factoryShape: "none" },
          authFactorySpec.errorCodes[1]!,
        ],
      },
      AUTH_CONFIG,
      AUTH_FAILURES_CONFIG,
      AUTH_EN_US_KEYS,
    );
    expect(r.diagnostics).toEqual([]);
    expect(r.source).not.toContain("bearerMissing");
    expect(r.source).toContain("jwksUnavailable");
  });

  it("standard fails loud with D2ERC003 and blocks emit", () => {
    const r = emitFailuresCatalog(
      {
        errorCodes: [
          { ...authFactorySpec.errorCodes[0]!, factoryShape: "standard" },
        ],
      },
      AUTH_CONFIG,
      AUTH_FAILURES_CONFIG,
      AUTH_EN_US_KEYS,
    );
    expect(r.source).toBe("");
    expect(r.diagnostics.some((d) => d.id === "D2ERC003")).toBe(true);
  });

  it("validation fails loud with D2ERC003 and blocks emit", () => {
    const r = emitFailuresCatalog(
      {
        errorCodes: [
          { ...authFactorySpec.errorCodes[0]!, factoryShape: "validation" },
        ],
      },
      AUTH_CONFIG,
      AUTH_FAILURES_CONFIG,
      AUTH_EN_US_KEYS,
    );
    expect(r.source).toBe("");
    expect(r.diagnostics.some((d) => d.id === "D2ERC003")).toBe(true);
  });

  // ----- silent-skip branches: no diagnostic, but entry absent from output -----

  it("absent factoryShape is treated as with_error_code and emits the factory", () => {
    // The canonical spec schema requires factoryShape, so a conforming spec
    // always has it. For defense-in-depth this behavior (emit rather than skip)
    // is pinned here — if the policy changes to fail-loud, update this test.
    const r = emitFailuresCatalog(
      {
        errorCodes: [
          {
            code: "AUTH_BEARER_MISSING",
            httpStatus: 401,
            category: "validation_failure" as const,
            userMessageKey: "TK.Auth.Errors.UNAUTHORIZED",
            factoryName: "BearerMissing",
            // factoryShape intentionally absent
          },
        ],
      },
      AUTH_CONFIG,
      AUTH_FAILURES_CONFIG,
      AUTH_EN_US_KEYS,
    );
    expect(r.diagnostics).toEqual([]);
    // Entry emitted (no factoryShape → treated as with_error_code)
    expect(r.source).toContain("bearerMissing<T = void>(traceId?: string)");
  });

  it("entry missing userMessageKey is silently skipped with no diagnostic", () => {
    const r = emitFailuresCatalog(
      {
        errorCodes: [
          {
            code: "AUTH_BEARER_MISSING",
            httpStatus: 401,
            category: "validation_failure" as const,
            // userMessageKey intentionally absent
            factoryName: "BearerMissing",
            factoryShape: "with_error_code" as const,
          },
          authFactorySpec.errorCodes[1]!, // valid second entry
        ],
      },
      AUTH_CONFIG,
      AUTH_FAILURES_CONFIG,
      AUTH_EN_US_KEYS,
    );
    expect(r.diagnostics).toEqual([]);
    // The entry without userMessageKey is skipped; the valid entry is still emitted
    expect(r.source).not.toContain("bearerMissing");
    expect(r.source).toContain("jwksUnavailable");
  });

  it("entry with non-conforming userMessageKey (parseTkKey → undefined) is silently skipped", () => {
    // parseTkKey returns undefined for anything that doesn't match TK.<Domain>.<Category>.<CONST>
    const r = emitFailuresCatalog(
      {
        errorCodes: [
          {
            code: "AUTH_BEARER_MISSING",
            httpStatus: 401,
            category: "validation_failure" as const,
            userMessageKey: "not_a_tk_path", // non-conforming — parseTkKey returns undefined
            factoryName: "BearerMissing",
            factoryShape: "with_error_code" as const,
          },
          authFactorySpec.errorCodes[1]!, // valid second entry
        ],
      },
      AUTH_CONFIG,
      AUTH_FAILURES_CONFIG,
      AUTH_EN_US_KEYS,
    );
    expect(r.diagnostics).toEqual([]);
    // The non-conforming entry is skipped; the valid entry is still emitted
    expect(r.source).not.toContain("bearerMissing");
    expect(r.source).toContain("jwksUnavailable");
  });
});

// ---------------------------------------------------------------------------
// emitBaseFactoriesCatalog — the generic base/constructing factories (the
// D2Result semantic factories). Verifies the three factoryShape signatures,
// the constant-reference DEFAULTS (never key/path literals), the two
// name-mismatch quirks, the httpStatus → HttpStatusCode map, the none-skip,
// and idempotency.
// ---------------------------------------------------------------------------

const GENERIC_EN_US_KEYS = new Set([
  "common_errors_NOT_FOUND",
  "common_errors_FORBIDDEN",
  "common_errors_VALIDATION_FAILED",
  "common_errors_CONFLICT",
  "common_errors_UNKNOWN",
  "common_errors_SOME_FOUND",
  "common_errors_TOO_MANY_REQUESTS",
]);

const baseFactorySpec: ErrorCodesSpec = {
  errorCodes: [
    {
      code: "NOT_FOUND",
      httpStatus: 404,
      category: "not_found",
      userMessageKey: "TK.Common.Errors.NOT_FOUND",
      factoryName: "NotFound",
      factoryShape: "standard",
      doc: "Indicates that the requested resource was not found.",
    },
    {
      code: "FORBIDDEN",
      httpStatus: 403,
      category: "policy_denied",
      userMessageKey: "TK.Common.Errors.FORBIDDEN",
      factoryName: "Forbidden",
      factoryShape: "with_error_code",
      doc: "Forbidden.",
    },
    {
      code: "VALIDATION_FAILED",
      httpStatus: 400,
      category: "validation_failure",
      userMessageKey: "TK.Common.Errors.VALIDATION_FAILED",
      factoryName: "ValidationFailed",
      factoryShape: "validation",
      doc: "Validation failed.",
    },
    {
      code: "SOME_FOUND",
      httpStatus: 206,
      category: "partial_success",
      userMessageKey: "TK.Common.Errors.SOME_FOUND",
      factoryName: "SomeFound",
      factoryShape: "none",
      doc: "Some found.",
    },
  ],
};

describe("emitBaseFactoriesCatalog (generic base factories) — shapes", () => {
  function emit(spec: ErrorCodesSpec = baseFactorySpec) {
    return emitBaseFactoriesCatalog(
      spec,
      GENERIC_CONFIG,
      GENERIC_FACTORIES_CONFIG,
      GENERIC_EN_US_KEYS,
    );
  }

  it("imports the TK constants from the cycle-free @d2/i18n-keys package", () => {
    const r = emit();
    expect(r.diagnostics).toEqual([]);
    // The constants live in the shallow keys package so @d2/result references
    // them without recreating the result → i18n → result cycle.
    expect(r.source).toContain('import { TK } from "@d2/i18n-keys";');
    // NOT the @d2/i18n/keys re-export path (which transitively pulls @d2/i18n
    // back into @d2/result's graph — the cycle).
    expect(r.source).not.toContain('from "@d2/i18n/keys"');
  });

  it("imports the TKMessage type from @d2/i18n-abstractions (the moved primitive home)", () => {
    const r = emit();
    // TKMessage moved out of @d2/result into the zero-dep abstractions leaf;
    // the generated factories type their `messages` opts against it directly.
    expect(r.source).toContain(
      'import { type TKMessage } from "@d2/i18n-abstractions";',
    );
    // The old in-package ./tk-message.js source no longer exists in @d2/result.
    expect(r.source).not.toContain('from "./tk-message.js"');
  });

  it("standard shape: BasicOpts param, no errorCode override, bakes own category", () => {
    const r = emit();
    expect(r.source).toContain(
      "export function notFound<T = void>(opts: BasicOpts = {}): D2Result<T> {",
    );
    expect(r.source).toContain("statusCode: HttpStatusCode.NotFound,");
    expect(r.source).toContain("errorCode: ErrorCodes.NOT_FOUND,");
    // standard shape bakes its own category with no caller override.
    expect(r.source).toContain("category: ErrorCategoryWire.NotFound,");
  });

  it("with_error_code shape: CodedOpts param + errorCode + category override", () => {
    const r = emit();
    expect(r.source).toContain(
      "export function forbidden<T = void>(opts: CodedOpts = {}): D2Result<T> {",
    );
    expect(r.source).toContain(
      "errorCode: opts.errorCode ?? ErrorCodes.FORBIDDEN,",
    );
    expect(r.source).toContain(
      "category: opts.category ?? ErrorCategoryWire.PolicyDenied,",
    );
  });

  it("validation shape: ValidationFailedOpts param + inputErrors pass-through", () => {
    const r = emit();
    expect(r.source).toContain(
      "export function validationFailed<T = void>(opts: ValidationFailedOpts = {}): D2Result<T> {",
    );
    expect(r.source).toContain("inputErrors: opts.inputErrors,");
    expect(r.source).toContain("statusCode: HttpStatusCode.BadRequest,");
    expect(r.source).toContain(
      "errorCode: opts.errorCode ?? ErrorCodes.VALIDATION_FAILED,",
    );
  });

  it("none shape emits no factory (SomeFound stays hand-rolled)", () => {
    const r = emit();
    expect(r.source).not.toContain("export function someFound");
  });

  it("factory bodies reference the TK constant directly, never a tk() wrapper or key/path literal", () => {
    const r = emit();
    // The TK constant IS the default TKMessage now — the factory inlines it
    // into `messages: opts.messages ?? [TK.common.errors.NOT_FOUND]`. No
    // DEFAULTS map, no redundant tk() wrapper.
    expect(r.source).toContain(
      "messages: opts.messages ?? [TK.common.errors.NOT_FOUND],",
    );
    expect(r.source).toContain(
      "messages: opts.messages ?? [TK.common.errors.FORBIDDEN],",
    );
    expect(r.source).not.toContain("const DEFAULTS");
    expect(r.source).not.toContain("tk(TK.common.errors.NOT_FOUND)");
    // The render-bug regression guard: the raw PascalCase symbol path must
    // NOT appear as a string literal anywhere.
    expect(r.source).not.toContain('tk("TK.Common.Errors.NOT_FOUND")');
    expect(r.source).not.toContain('"TK.Common.Errors');
  });

  it("emits the BasicOpts / CodedOpts / ValidationFailedOpts interfaces", () => {
    const r = emit();
    expect(r.source).toContain("export interface BasicOpts {");
    expect(r.source).toContain(
      "export interface CodedOpts extends BasicOpts {",
    );
    expect(r.source).toContain(
      "export interface ValidationFailedOpts extends CodedOpts {",
    );
    // CodedOpts carries the optional category override (mirroring errorCode),
    // imported from the zero-dep @d2/error-category leaf.
    expect(r.source).toContain(
      'import { type ErrorCategory, ErrorCategoryWire } from "@d2/error-category";',
    );
    expect(r.source).toContain("category?: ErrorCategory;");
  });

  it("produces identical source across two runs (idempotency)", () => {
    expect(emit().source).toBe(emit().source);
  });
});

describe("emitBaseFactoriesCatalog — name-mismatch quirks + status map", () => {
  it("UNHANDLED_EXCEPTION defaults its TK to UNKNOWN (code ≠ default-TK)", () => {
    const r = emitBaseFactoriesCatalog(
      {
        errorCodes: [
          {
            code: "UNHANDLED_EXCEPTION",
            httpStatus: 500,
            category: "internal_error",
            userMessageKey: "TK.Common.Errors.UNKNOWN",
            factoryName: "UnhandledException",
            factoryShape: "standard",
            doc: "Unhandled.",
          },
        ],
      },
      GENERIC_CONFIG,
      GENERIC_FACTORIES_CONFIG,
      GENERIC_EN_US_KEYS,
    );
    expect(r.diagnostics).toEqual([]);
    // Name-mismatch quirk: the UNHANDLED_EXCEPTION code defaults its message to
    // the UNKNOWN TK constant — inlined directly into the factory body.
    expect(r.source).toContain(
      "messages: opts.messages ?? [TK.common.errors.UNKNOWN],",
    );
    expect(r.source).toContain("errorCode: ErrorCodes.UNHANDLED_EXCEPTION,");
    expect(r.source).toContain(
      "statusCode: HttpStatusCode.InternalServerError,",
    );
  });

  it("RATE_LIMITED → factory tooManyRequests + default-TK TOO_MANY_REQUESTS (three-way mismatch)", () => {
    const r = emitBaseFactoriesCatalog(
      {
        errorCodes: [
          {
            code: "RATE_LIMITED",
            httpStatus: 429,
            category: "rate_limited",
            userMessageKey: "TK.Common.Errors.TOO_MANY_REQUESTS",
            factoryName: "TooManyRequests",
            factoryShape: "with_error_code",
            doc: "Rate limited.",
          },
        ],
      },
      GENERIC_CONFIG,
      GENERIC_FACTORIES_CONFIG,
      GENERIC_EN_US_KEYS,
    );
    expect(r.diagnostics).toEqual([]);
    expect(r.source).toContain(
      "export function tooManyRequests<T = void>(opts: CodedOpts = {}): D2Result<T> {",
    );
    expect(r.source).toContain(
      "messages: opts.messages ?? [TK.common.errors.TOO_MANY_REQUESTS],",
    );
    expect(r.source).toContain(
      "errorCode: opts.errorCode ?? ErrorCodes.RATE_LIMITED,",
    );
    expect(r.source).toContain("statusCode: HttpStatusCode.TooManyRequests,");
  });

  it.each([
    [404, "NotFound"],
    [403, "Forbidden"],
    [401, "Unauthorized"],
    [400, "BadRequest"],
    [409, "Conflict"],
    [413, "RequestEntityTooLarge"],
    [429, "TooManyRequests"],
    [500, "InternalServerError"],
    [503, "ServiceUnavailable"],
  ])("httpStatus %s maps to HttpStatusCode.%s", (httpStatus, member) => {
    const r = emitBaseFactoriesCatalog(
      {
        errorCodes: [
          {
            code: "X_CODE",
            httpStatus,
            category: "internal_error",
            userMessageKey: "TK.Common.Errors.UNKNOWN",
            factoryName: "XCode",
            factoryShape: "standard",
            doc: "X.",
          },
        ],
      },
      GENERIC_CONFIG,
      GENERIC_FACTORIES_CONFIG,
      GENERIC_EN_US_KEYS,
    );
    expect(r.diagnostics).toEqual([]);
    expect(r.source).toContain(`statusCode: HttpStatusCode.${member},`);
  });
});
