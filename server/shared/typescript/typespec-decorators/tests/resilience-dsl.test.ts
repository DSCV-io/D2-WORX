// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Pure-unit tests for the @d2Resilience DSL parser (src/resilience-dsl.ts).
// These tests run the parser directly without a TypeSpec compile host.
// Valid expressions: assert exact AST structure (policy, tunables, inner chain).
// Invalid expressions: assert each error code is present AND ok === false.

import { describe, it, expect } from "vitest";
import { parse } from "../src/resilience-dsl.js";
import type { ResiliencePolicyNode } from "../src/resilience-dsl.js";

// ----------------------------------------------------------------
// Helpers
// ----------------------------------------------------------------

function assertOk(result: ReturnType<typeof parse>): ResiliencePolicyNode {
  expect(result.ok).toBe(true);
  if (!result.ok) throw new Error("Expected ok");
  return result.root;
}

function assertFail(
  result: ReturnType<typeof parse>,
  expectedCode: string,
): void {
  expect(result.ok).toBe(false);
  if (result.ok) throw new Error("Expected failure");
  const codes = result.errors.map((e) => e.code);
  expect(codes).toContain(expectedCode);
}

// ----------------------------------------------------------------
// Valid expressions — structure decomposition
// ----------------------------------------------------------------

describe("parse_RetryBareDefaults_Ok", () => {
  it("parses retry() to correct leaf AST", () => {
    const root = assertOk(parse("retry()"));
    expect(root.policy).toBe("retry");
    expect(root.tunables).toEqual({});
    expect(root.inner).toBeUndefined();
  });
});

describe("parse_SingleflightBare_Ok", () => {
  it("parses singleflight() to leaf AST", () => {
    const root = assertOk(parse("singleflight()"));
    expect(root.policy).toBe("singleflight");
    expect(root.tunables).toEqual({});
    expect(root.inner).toBeUndefined();
  });
});

describe("parse_CircuitBreakerBare_Ok", () => {
  it("parses circuitBreaker() to leaf AST", () => {
    const root = assertOk(parse("circuitBreaker()"));
    expect(root.policy).toBe("circuitBreaker");
    expect(root.tunables).toEqual({});
    expect(root.inner).toBeUndefined();
  });
});

describe("parse_NestedThreeDeep_Ok", () => {
  it("parses retry(circuitBreaker(singleflight())) to full 3-deep chain", () => {
    const root = assertOk(parse("retry(circuitBreaker(singleflight()))"));
    // Top-level: retry
    expect(root.policy).toBe("retry");
    expect(root.tunables).toEqual({});
    // Middle: circuitBreaker
    expect(root.inner).toBeDefined();
    expect(root.inner!.policy).toBe("circuitBreaker");
    expect(root.inner!.tunables).toEqual({});
    // Assert all the way down to the singleflight leaf
    expect(root.inner!.inner).toBeDefined();
    expect(root.inner!.inner!.policy).toBe("singleflight");
    expect(root.inner!.inner!.tunables).toEqual({});
    expect(root.inner!.inner!.inner).toBeUndefined();
  });
});

describe("parse_RetryPositionalTunable_Ok", () => {
  it("parses retry(3) with maxAttempts positional binding", () => {
    const root = assertOk(parse("retry(3)"));
    expect(root.policy).toBe("retry");
    expect(root.tunables).toEqual({ maxAttempts: 3 });
  });
});

describe("parse_CircuitBreakerNamedTunable_Ok", () => {
  it("parses circuitBreaker(threshold: 5) with alias resolved to failureThreshold", () => {
    const root = assertOk(parse("circuitBreaker(threshold: 5)"));
    expect(root.policy).toBe("circuitBreaker");
    expect(root.tunables).toEqual({ failureThreshold: 5 });
    expect(root.inner).toBeUndefined();
  });
});

describe("parse_CircuitBreakerCanonicalName_Ok", () => {
  it("parses circuitBreaker(failureThreshold: 5) with canonical name", () => {
    const root = assertOk(parse("circuitBreaker(failureThreshold: 5)"));
    expect(root.tunables).toEqual({ failureThreshold: 5 });
  });
});

describe("parse_MixedTunablesAndInner_Ok", () => {
  it("parses retry(3, circuitBreaker(threshold: 5)) with both tunables and inner", () => {
    const root = assertOk(parse("retry(3, circuitBreaker(threshold: 5))"));
    expect(root.policy).toBe("retry");
    expect(root.tunables).toEqual({ maxAttempts: 3 });
    expect(root.inner).toBeDefined();
    expect(root.inner!.policy).toBe("circuitBreaker");
    expect(root.inner!.tunables).toEqual({ failureThreshold: 5 });
  });
});

describe("parse_DurationLiteralMs_Ok", () => {
  it("parses retry(baseDelay: 200ms) with ms duration normalized to ms", () => {
    const root = assertOk(parse("retry(baseDelay: 200ms)"));
    expect(root.tunables).toEqual({ baseDelayMs: 200 });
  });
});

describe("parse_DurationLiteralS_BreakerCooldown_Ok", () => {
  it("parses circuitBreaker(cooldown: 30s) with seconds normalized to seconds", () => {
    const root = assertOk(parse("circuitBreaker(cooldown: 30s)"));
    expect(root.tunables).toEqual({ cooldownSeconds: 30 });
  });
});

describe("parse_CooldownSecondsInteger_Ok", () => {
  it("parses circuitBreaker(cooldownSeconds: 30) with integer seconds", () => {
    const root = assertOk(parse("circuitBreaker(cooldownSeconds: 30)"));
    expect(root.tunables).toEqual({ cooldownSeconds: 30 });
  });
});

describe("parse_RetryJitterBool_Ok", () => {
  it("parses retry(jitter: false) with bool tunable", () => {
    const root = assertOk(parse("retry(jitter: false)"));
    expect(root.tunables).toEqual({ jitter: false });
  });
});

describe("parse_WhitespaceAroundTokens_Ok", () => {
  it("parses expression with extra whitespace (whitespace is insignificant)", () => {
    const root = assertOk(
      parse("  retry( 3 , circuitBreaker( threshold : 5 ) )  "),
    );
    expect(root.policy).toBe("retry");
    expect(root.tunables).toEqual({ maxAttempts: 3 });
    expect(root.inner!.tunables).toEqual({ failureThreshold: 5 });
  });
});

describe("parse_MultipleNamedTunables_Ok", () => {
  it("parses retry(maxAttempts: 3, jitter: false) with two named tunables", () => {
    const root = assertOk(parse("retry(maxAttempts: 3, jitter: false)"));
    expect(root.tunables).toEqual({ maxAttempts: 3, jitter: false });
  });
});

describe("parse_RetryAllPositionals_Ok", () => {
  it("parses retry with all five positional tunables", () => {
    const root = assertOk(parse("retry(3, 1000, 2, 30000, false)"));
    expect(root.tunables).toEqual({
      maxAttempts: 3,
      baseDelayMs: 1000,
      backoffMultiplier: 2,
      maxDelayMs: 30000,
      jitter: false,
    });
  });
});

describe("parse_RetryWrapsCircuitBreaker_Ok", () => {
  it("parses retry(circuitBreaker()) — inner without tunables", () => {
    const root = assertOk(parse("retry(circuitBreaker())"));
    expect(root.policy).toBe("retry");
    expect(root.inner).toBeDefined();
    expect(root.inner!.policy).toBe("circuitBreaker");
  });
});

describe("parse_BaseDelayWithSuffix_Ok", () => {
  it("parses retry(baseDelayMs: 500ms) treating ms duration for ms tunable", () => {
    const root = assertOk(parse("retry(baseDelayMs: 500ms)"));
    expect(root.tunables).toEqual({ baseDelayMs: 500 });
  });
});

// ----------------------------------------------------------------
// Invalid expressions — each asserts ok===false + the error code
// ----------------------------------------------------------------

describe("parse_EmptyString_Reject", () => {
  it("rejects empty string with resilience-malformed", () => {
    assertFail(parse(""), "resilience-malformed");
  });
});

describe("parse_WhitespaceOnly_Reject", () => {
  it("rejects whitespace-only string with resilience-malformed", () => {
    assertFail(parse("   "), "resilience-malformed");
  });
});

describe("parse_PureGarbage_Reject", () => {
  it("rejects '!!!@#$' with resilience-malformed", () => {
    assertFail(parse("!!!@#$"), "resilience-malformed");
  });
});

describe("parse_StringWhereIntExpected_Reject", () => {
  it('rejects retry(maxAttempts: "three") with resilience-bad-arg', () => {
    assertFail(parse('retry(maxAttempts: "three")'), "resilience-bad-arg");
  });
});

describe("parse_DurationWhereIntExpected_Reject", () => {
  it("rejects retry(backoffMultiplier: 30s) — int-only tunable — with resilience-bad-arg", () => {
    assertFail(parse("retry(backoffMultiplier: 30s)"), "resilience-bad-arg");
  });
});

describe("parse_UnbalancedParen_Reject", () => {
  it("rejects 'retry(' with resilience-malformed", () => {
    assertFail(parse("retry("), "resilience-malformed");
  });

  it("rejects 'retry())' with resilience-malformed", () => {
    assertFail(parse("retry())"), "resilience-malformed");
  });

  it("rejects 'retry' with no parens with resilience-malformed", () => {
    assertFail(parse("retry"), "resilience-malformed");
  });

  it("rejects 'retry(,)' with resilience-malformed", () => {
    assertFail(parse("retry(,)"), "resilience-malformed");
  });
});

describe("parse_UnknownPolicy_Reject", () => {
  it("rejects 'breaker()' with resilience-unknown-policy", () => {
    assertFail(parse("breaker()"), "resilience-unknown-policy");
  });

  it("rejects 'retryx()' with resilience-unknown-policy", () => {
    assertFail(parse("retryx()"), "resilience-unknown-policy");
  });
});

describe("parse_UnknownArg_Reject", () => {
  it("rejects retry(foo: 3) with resilience-unknown-arg", () => {
    assertFail(parse("retry(foo: 3)"), "resilience-unknown-arg");
  });

  it("rejects singleflight(3) with resilience-unknown-arg", () => {
    assertFail(parse("singleflight(3)"), "resilience-unknown-arg");
  });

  it("rejects singleflight(foo: 1) with resilience-unknown-arg", () => {
    assertFail(parse("singleflight(foo: 1)"), "resilience-unknown-arg");
  });
});

describe("parse_BadArgValue_Reject", () => {
  it("rejects retry(maxAttempts: 0) — below minimum 1 — with resilience-bad-arg", () => {
    assertFail(parse("retry(maxAttempts: 0)"), "resilience-bad-arg");
  });

  it("rejects retry(jitter: 5) — bool expected — with resilience-bad-arg", () => {
    assertFail(parse("retry(jitter: 5)"), "resilience-bad-arg");
  });

  it("rejects retry(maxAttempts: true) — int expected — with resilience-bad-arg", () => {
    assertFail(parse("retry(maxAttempts: true)"), "resilience-bad-arg");
  });

  it("rejects retry(backoffMultiplier: 0) — below minimum 1 — with resilience-bad-arg", () => {
    assertFail(parse("retry(backoffMultiplier: 0)"), "resilience-bad-arg");
  });

  it("rejects circuitBreaker(threshold: 0) — below minimum 1 — with resilience-bad-arg", () => {
    assertFail(parse("circuitBreaker(threshold: 0)"), "resilience-bad-arg");
  });
});

describe("parse_MultipleInner_Reject", () => {
  it("rejects retry(circuitBreaker(), singleflight()) with resilience-multiple-inner", () => {
    assertFail(
      parse("retry(circuitBreaker(), singleflight())"),
      "resilience-multiple-inner",
    );
  });
});

describe("parse_PositionalAfterNamed_Reject", () => {
  it("rejects retry(maxAttempts: 3, 1000) with resilience-positional-after-named", () => {
    assertFail(
      parse("retry(maxAttempts: 3, 1000)"),
      "resilience-positional-after-named",
    );
  });
});

describe("parse_TrailingComma_Reject", () => {
  it("rejects retry(3,) with resilience-malformed", () => {
    assertFail(parse("retry(3,)"), "resilience-malformed");
  });
});

describe("parse_TooManyPositionals_Reject", () => {
  it("rejects retry with more positional args than slots with resilience-unknown-arg", () => {
    // retry has 5 positional slots; a 6th is rejected
    assertFail(
      parse("retry(3, 1000, 2, 30000, false, 99)"),
      "resilience-unknown-arg",
    );
  });
});

describe("parse_SingflightWithPolicyArg_Reject", () => {
  it("rejects singleflight(retry()) with resilience-unknown-arg", () => {
    assertFail(parse("singleflight(retry())"), "resilience-unknown-arg");
  });
});

describe("parse_BreakerDurationWhereIntExpected_Reject", () => {
  it("rejects circuitBreaker(threshold: false) — int expected — with resilience-bad-arg", () => {
    assertFail(parse("circuitBreaker(threshold: false)"), "resilience-bad-arg");
  });
});

// ----------------------------------------------------------------
// Branch-coverage tests — exercises parser paths not hit above
// ----------------------------------------------------------------

describe("parse_NonNameFirstToken_Reject", () => {
  it("rejects '42()' — expression starts with number not policy name — with resilience-malformed", () => {
    // Covers parsePolicyCall() 'expected a policy name' branch (nameTok.kind !== 'name')
    assertFail(parse("42()"), "resilience-malformed");
  });
});

describe("parse_BarePolicyNameAsPositional_Reject", () => {
  it("rejects 'retry(circuitBreaker)' — bare policy name without () must fail with resilience-malformed and a parens hint", () => {
    // An invalid config must fail the build, not merely surface a diagnostic code;
    // bare policy name used as a positional arg is never a valid literal — always an error.
    const result = parse("retry(circuitBreaker)");
    expect(result.ok).toBe(false);
    if (result.ok) throw new Error("Expected failure");
    const codes = result.errors.map((e) => e.code);
    expect(codes).toContain("resilience-malformed");
    // The error message must hint the missing parens
    const msgs = result.errors.map((e) => e.message);
    expect(
      msgs.some((m) => m.includes("did you mean 'circuitBreaker()'")),
    ).toBe(true);
  });

  it("rejects 'retry(singleflight)' — bare singleflight name without () — resilience-malformed with hint", () => {
    const result = parse("retry(singleflight)");
    expect(result.ok).toBe(false);
    if (result.ok) throw new Error("Expected failure");
    const codes = result.errors.map((e) => e.code);
    expect(codes).toContain("resilience-malformed");
    const msgs = result.errors.map((e) => e.message);
    expect(msgs.some((m) => m.includes("did you mean 'singleflight()'"))).toBe(
      true,
    );
  });
});

describe("parse_UnknownNameWithoutColonInArgs_Reject", () => {
  it("rejects 'retry(foo)' — bare unknown name without colon — with resilience-malformed", () => {
    // Covers the 'name without colon in arg list' branch (non-policy name, no following colon)
    assertFail(parse("retry(foo)"), "resilience-malformed");
  });
});

describe("parse_KnownPolicyWithoutParensAfterNamedArg_Reject", () => {
  it("rejects 'retry(maxAttempts: 3, circuitBreaker)' — bare policy name without () — with resilience-malformed", () => {
    // A bare policy name is always rejected with a parens hint regardless of
    // position (before or after named args) — it is never a valid literal.
    assertFail(
      parse("retry(maxAttempts: 3, circuitBreaker)"),
      "resilience-malformed",
    );
  });
});

describe("parse_DurationMsWhereUnknownTypeExpected_Reject", () => {
  it("rejects 'retry(baseDelayMs: foo)' — name token for duration-ms tunable — with resilience-bad-arg", () => {
    // Covers the 'neither number nor duration' fallback in validateTunableValue (duration-ms)
    assertFail(parse("retry(baseDelayMs: foo)"), "resilience-bad-arg");
  });
});

describe("parse_DurationSWhereUnknownTypeExpected_Reject", () => {
  it("rejects 'circuitBreaker(cooldownSeconds: foo)' — name token for duration-s tunable — with resilience-bad-arg", () => {
    // Covers the 'neither number nor duration' fallback in validateTunableValue (duration-s)
    assertFail(
      parse("circuitBreaker(cooldownSeconds: foo)"),
      "resilience-bad-arg",
    );
  });
});

describe("parse_DurationMsWithSSecondsSuffix_Ok", () => {
  it("parses 'retry(baseDelay: 30s)' — 's' suffix normalized to ms — succeeds", () => {
    // Covers parseDurationToMs() endsWith('s') branch
    const root = assertOk(parse("retry(baseDelay: 30s)"));
    expect(root.tunables).toEqual({ baseDelayMs: 30_000 });
  });
});

describe("parse_DurationSWithMsSuffix_Ok", () => {
  it("parses 'circuitBreaker(cooldown: 200ms)' — 'ms' suffix normalized to seconds — succeeds", () => {
    // Covers parseDurationToSeconds() endsWith('ms') branch
    const root = assertOk(parse("circuitBreaker(cooldown: 200ms)"));
    expect(root.tunables).toEqual({ cooldownSeconds: 0.2 });
  });
});

describe("parse_UnknownPolicyWithoutParens_Reject", () => {
  it("rejects 'breaker' — unknown policy with no '(' — with resilience-malformed", () => {
    // Covers the 'unknown policy without following (' branch in parsePolicyCall (else path)
    assertFail(parse("breaker"), "resilience-unknown-policy");
  });
});

describe("parse_PositionalBoolWhereIntExpected_Reject", () => {
  it("rejects 'retry(true)' — bool in positional slot that expects int — with resilience-bad-arg", () => {
    // Covers the validated===undefined branch in positional arg handling (line 435)
    assertFail(parse("retry(true)"), "resilience-bad-arg");
  });
});

describe("parse_UnknownPolicyWithInnerArgs_Reject", () => {
  it("rejects 'breaker(3)' — unknown policy with a plain arg — covers skipArgList non-paren token path", () => {
    // Covers skipArgList() encountering a non-paren, non-rparen token (else branch of lparen check)
    assertFail(parse("breaker(3)"), "resilience-unknown-policy");
  });

  it("rejects 'breaker(retry(3))' — unknown policy with nested policy — covers skipArgList lparen depth path", () => {
    // Covers skipArgList() lparen branch (depth++) AND the depth>0 after rparen (depth-- > 0)
    assertFail(parse("breaker(retry(3))"), "resilience-unknown-policy");
  });
});
