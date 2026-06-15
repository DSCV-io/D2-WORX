// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Tests for @d2/typespec-decorators.
//
// Direct unit tests call each $fn with a lightweight mock DecoratorContext,
// giving V8 source-level coverage. Integration tests use the TypeSpec test
// host to verify the full compile + stateMap round-trip.
// Adversarial rejection tests (bad tier, empty scope, redact-on-op, etc.)
// exercise the full validation layer — each asserts both the diagnostic code
// and program.hasError() === true so that a severity regression is detectable.

import {
  createTestLibrary,
  createTestWrapper,
  findTestPackageRoot,
} from "@typespec/compiler/testing";
import { HttpTestLibrary } from "@typespec/http/testing";
import { describe, it, expect, beforeAll, afterEach } from "vitest";
import type { BasicTestRunner } from "@typespec/compiler/testing";
import { createTestHost } from "@typespec/compiler/testing";
import {
  D2_REQUIRE_ANY_SCOPE_KEY,
  D2_REQUIRE_ALL_SCOPES_KEY,
  D2_RATE_LIMIT_TIER_KEY,
  D2_AUDIENCE_KEY,
  D2_SERVED_BY_KEY,
  D2_GRPC_METHOD_KEY,
  D2_REDACT_KEY,
  D2_SERVER_PUSH_KEY,
  D2_IDEMPOTENT_KEY,
  D2_RESILIENCE_KEY,
  D2_CSRF_KEY,
  D2_HARMLESS_KEY,
  $lib,
  $decorators,
  type ResilienceDiagnosticCode,
} from "../src/index.js";
import type { GrpcMethodPayload, IdempotentPayload } from "../src/index.js";
import {
  loadScopeNames,
  loadAudienceNames,
  _resetSpecRegistryCache,
} from "../src/spec-registry.js";
import {
  validateRateLimitTier,
  validateGrpcStreaming,
  validatePushTarget,
  validateCsrfPosture,
  validateIdempotent,
  validateScopes,
  validateAudience,
  validateServedBy,
  validateResilience,
} from "../src/validators.js";
import { $onValidate } from "../src/onvalidate.js";
import {
  $d2RequireAnyScope,
  $d2RequireAllScopes,
  $d2RateLimitTier,
  $d2Audience,
  $d2ServedBy,
  $d2GrpcMethod,
  $d2Redact,
  $d2ServerPush,
  $d2Idempotent,
  $d2Resilience,
  $d2Csrf,
  $d2Harmless,
} from "../src/decorators.js";
import type {
  DecoratorContext,
  ModelProperty,
  Operation,
  Program,
} from "@typespec/compiler";

// ---------------------------------------------------------------------------
// Helper: build a minimal mock DecoratorContext whose stateMap() returns a
// real Map keyed by the same Symbol, so $fn calls can be verified directly.
// ---------------------------------------------------------------------------

function makeMockContext(): {
  ctx: DecoratorContext;
  maps: Map<symbol, Map<object, unknown>>;
} {
  const maps = new Map<symbol, Map<object, unknown>>();
  const ctx = {
    program: {
      stateMap(key: symbol): Map<object, unknown> {
        if (!maps.has(key)) maps.set(key, new Map());
        return maps.get(key)!;
      },
      // reportDiagnostic is a no-op in direct-unit tests; the mock context
      // uses scope names / values that would be invalid in real compiles but
      // are intentionally used here to verify storage, not validation.
      reportDiagnostic(): void {
        // no-op in mock context
      },
    },
  } as unknown as DecoratorContext;
  return { ctx, maps };
}

const mockTarget = {} as unknown as Operation;
const mockProperty = {} as unknown as ModelProperty;

// ---------------------------------------------------------------------------
// Direct unit tests: $fn bodies (gives V8 source-level coverage of decorators.ts)
// ---------------------------------------------------------------------------

describe("directUnit_$d2RequireAnyScope", () => {
  it("stores the scopes array under D2_REQUIRE_ANY_SCOPE_KEY", () => {
    const { ctx, maps } = makeMockContext();
    $d2RequireAnyScope(ctx, mockTarget, "orders:read", "orders:write");
    expect(maps.get(D2_REQUIRE_ANY_SCOPE_KEY)!.get(mockTarget)).toEqual([
      "orders:read",
      "orders:write",
    ]);
  });
});

describe("directUnit_$d2RequireAllScopes", () => {
  it("stores the scopes array under D2_REQUIRE_ALL_SCOPES_KEY", () => {
    const { ctx, maps } = makeMockContext();
    $d2RequireAllScopes(ctx, mockTarget, "admin:read", "admin:write");
    expect(maps.get(D2_REQUIRE_ALL_SCOPES_KEY)!.get(mockTarget)).toEqual([
      "admin:read",
      "admin:write",
    ]);
  });
});

describe("directUnit_$d2RateLimitTier", () => {
  it("stores the tier string under D2_RATE_LIMIT_TIER_KEY", () => {
    const { ctx, maps } = makeMockContext();
    $d2RateLimitTier(ctx, mockTarget, "Standard");
    expect(maps.get(D2_RATE_LIMIT_TIER_KEY)!.get(mockTarget)).toBe("Standard");
  });
});

describe("directUnit_$d2Audience", () => {
  it("stores the audience string under D2_AUDIENCE_KEY", () => {
    const { ctx, maps } = makeMockContext();
    $d2Audience(ctx, mockTarget, "d2-edge");
    expect(maps.get(D2_AUDIENCE_KEY)!.get(mockTarget)).toBe("d2-edge");
  });
});

describe("directUnit_$d2ServedBy", () => {
  it("stores the owner string under D2_SERVED_BY_KEY", () => {
    const { ctx, maps } = makeMockContext();
    $d2ServedBy(ctx, mockTarget, "Edge");
    expect(maps.get(D2_SERVED_BY_KEY)!.get(mockTarget)).toBe("Edge");
  });
});

describe("directUnit_$d2GrpcMethod", () => {
  it("stores { service, method, streaming: 'unary' } when streaming omitted", () => {
    const { ctx, maps } = makeMockContext();
    $d2GrpcMethod(ctx, mockTarget, "Push", "PushNotificationCreated");
    expect(maps.get(D2_GRPC_METHOD_KEY)!.get(mockTarget)).toEqual({
      service: "Push",
      method: "PushNotificationCreated",
      streaming: "unary",
    });
  });

  it("stores { service, method, streaming } when streaming is explicit", () => {
    const { ctx, maps } = makeMockContext();
    $d2GrpcMethod(ctx, mockTarget, "Events", "StreamEvents", "serverStream");
    expect(maps.get(D2_GRPC_METHOD_KEY)!.get(mockTarget)).toEqual({
      service: "Events",
      method: "StreamEvents",
      streaming: "serverStream",
    });
  });
});

describe("directUnit_$d2Redact", () => {
  it("stores true under D2_REDACT_KEY on the model property", () => {
    const { ctx, maps } = makeMockContext();
    $d2Redact(ctx, mockProperty);
    expect(maps.get(D2_REDACT_KEY)!.get(mockProperty)).toBe(true);
  });
});

describe("directUnit_$d2ServerPush", () => {
  it("stores the pushTarget string under D2_SERVER_PUSH_KEY", () => {
    const { ctx, maps } = makeMockContext();
    $d2ServerPush(ctx, mockTarget, "user");
    expect(maps.get(D2_SERVER_PUSH_KEY)!.get(mockTarget)).toBe("user");
  });
});

describe("directUnit_$d2Idempotent", () => {
  it("stores header policy with empty fields under D2_IDEMPOTENT_KEY", () => {
    const { ctx, maps } = makeMockContext();
    $d2Idempotent(ctx, mockTarget, "header", 300);
    expect(maps.get(D2_IDEMPOTENT_KEY)!.get(mockTarget)).toEqual({
      keySource: "header",
      ttlSeconds: 300,
      fields: [],
    });
  });

  it("stores derived policy with ordered fields under D2_IDEMPOTENT_KEY", () => {
    const { ctx, maps } = makeMockContext();
    $d2Idempotent(ctx, mockTarget, "derived", 600, "orgId", "userId");
    expect(maps.get(D2_IDEMPOTENT_KEY)!.get(mockTarget)).toEqual({
      keySource: "derived",
      ttlSeconds: 600,
      fields: ["orgId", "userId"],
    });
  });
});

describe("directUnit_$d2Resilience", () => {
  it("stores a bare-defaults expression string under D2_RESILIENCE_KEY", () => {
    const { ctx, maps } = makeMockContext();
    $d2Resilience(ctx, mockTarget, "retry()");
    expect(maps.get(D2_RESILIENCE_KEY)!.get(mockTarget)).toBe("retry()");
  });

  it("stores an inline-tunables expression string under D2_RESILIENCE_KEY", () => {
    const { ctx, maps } = makeMockContext();
    $d2Resilience(ctx, mockTarget, "retry(3, circuitBreaker(threshold: 5))");
    expect(maps.get(D2_RESILIENCE_KEY)!.get(mockTarget)).toBe(
      "retry(3, circuitBreaker(threshold: 5))",
    );
  });
});

describe("directUnit_$d2Csrf", () => {
  it("stores the posture string under D2_CSRF_KEY", () => {
    const { ctx, maps } = makeMockContext();
    $d2Csrf(ctx, mockTarget, "exempt");
    expect(maps.get(D2_CSRF_KEY)!.get(mockTarget)).toBe("exempt");
  });
});

describe("directUnit_$d2Harmless", () => {
  it("stores true under D2_HARMLESS_KEY on the operation", () => {
    const { ctx, maps } = makeMockContext();
    $d2Harmless(ctx, mockTarget);
    expect(maps.get(D2_HARMLESS_KEY)!.get(mockTarget)).toBe(true);
  });
});

// ---------------------------------------------------------------------------
// Test library registration — mounts the real built package into the host
// ---------------------------------------------------------------------------

const D2DecoratorTestLibrary = createTestLibrary({
  name: "@d2/typespec-decorators",
  packageRoot: await findTestPackageRoot(import.meta.url),
  // Override: repo uses rootDir=src → outDir=dist (no src/ segment in dist/)
  jsFileFolder: "dist",
  typespecFileFolder: "lib",
});

// ---------------------------------------------------------------------------
// Shared runner — created once, reused across tests (compile is idempotent)
// ---------------------------------------------------------------------------

let runner: BasicTestRunner;

beforeAll(async () => {
  const host = await createTestHost({ libraries: [D2DecoratorTestLibrary] });
  runner = createTestWrapper(host, {
    autoImports: ["@d2/typespec-decorators"],
    autoUsings: ["D2"],
  });
});

// ---------------------------------------------------------------------------
// Unit tests: state-key symbol identity (§1.18 per-VALUE pin)
// ---------------------------------------------------------------------------

describe("stateKeys_AreProcessGlobalSymbolFor", () => {
  it("D2_REQUIRE_ANY_SCOPE_KEY equals Symbol.for('D2.d2RequireAnyScope')", () => {
    expect(D2_REQUIRE_ANY_SCOPE_KEY).toBe(Symbol.for("D2.d2RequireAnyScope"));
  });

  it("D2_REQUIRE_ALL_SCOPES_KEY equals Symbol.for('D2.d2RequireAllScopes')", () => {
    expect(D2_REQUIRE_ALL_SCOPES_KEY).toBe(Symbol.for("D2.d2RequireAllScopes"));
  });

  it("D2_RATE_LIMIT_TIER_KEY equals Symbol.for('D2.d2RateLimitTier')", () => {
    expect(D2_RATE_LIMIT_TIER_KEY).toBe(Symbol.for("D2.d2RateLimitTier"));
  });

  it("D2_AUDIENCE_KEY equals Symbol.for('D2.d2Audience')", () => {
    expect(D2_AUDIENCE_KEY).toBe(Symbol.for("D2.d2Audience"));
  });

  it("D2_SERVED_BY_KEY equals Symbol.for('D2.d2ServedBy')", () => {
    expect(D2_SERVED_BY_KEY).toBe(Symbol.for("D2.d2ServedBy"));
  });

  it("D2_GRPC_METHOD_KEY equals Symbol.for('D2.d2GrpcMethod')", () => {
    expect(D2_GRPC_METHOD_KEY).toBe(Symbol.for("D2.d2GrpcMethod"));
  });

  it("D2_REDACT_KEY equals Symbol.for('D2.d2Redact')", () => {
    expect(D2_REDACT_KEY).toBe(Symbol.for("D2.d2Redact"));
  });

  it("D2_SERVER_PUSH_KEY equals Symbol.for('D2.d2ServerPush')", () => {
    expect(D2_SERVER_PUSH_KEY).toBe(Symbol.for("D2.d2ServerPush"));
  });

  it("D2_IDEMPOTENT_KEY equals Symbol.for('D2.d2Idempotent')", () => {
    expect(D2_IDEMPOTENT_KEY).toBe(Symbol.for("D2.d2Idempotent"));
  });

  it("D2_RESILIENCE_KEY equals Symbol.for('D2.d2Resilience')", () => {
    expect(D2_RESILIENCE_KEY).toBe(Symbol.for("D2.d2Resilience"));
  });

  it("D2_CSRF_KEY equals Symbol.for('D2.d2Csrf')", () => {
    expect(D2_CSRF_KEY).toBe(Symbol.for("D2.d2Csrf"));
  });

  it("D2_HARMLESS_KEY equals Symbol.for('D2.d2Harmless')", () => {
    expect(D2_HARMLESS_KEY).toBe(Symbol.for("D2.d2Harmless"));
  });
});

// ---------------------------------------------------------------------------
// Unit tests: $lib descriptor
// ---------------------------------------------------------------------------

it("lib_HasExpectedPackageName", () => {
  expect($lib.name).toBe("@d2/typespec-decorators");
});

// ---------------------------------------------------------------------------
// Unit tests: $decorators registry key-set pin (§1.18 per-VALUE pin)
// ---------------------------------------------------------------------------

it("decorators_RegistryMapsAllTwelveDecoratorsUnderD2Namespace", () => {
  const keys = Object.keys($decorators.D2).sort();
  expect(keys).toEqual([
    "d2Audience",
    "d2Csrf",
    "d2GrpcMethod",
    "d2Harmless",
    "d2Idempotent",
    "d2RateLimitTier",
    "d2Redact",
    "d2RequireAllScopes",
    "d2RequireAnyScope",
    "d2Resilience",
    "d2ServedBy",
    "d2ServerPush",
  ]);
  for (const key of keys)
    expect(typeof $decorators.D2[key as keyof typeof $decorators.D2]).toBe(
      "function",
    );
});

// ---------------------------------------------------------------------------
// Integration tests: decorator round-trips via TypeSpec compile + stateMap
// ---------------------------------------------------------------------------

it("d2RequireAnyScope_StoresScopesArrayUnderAnyKey", async () => {
  await runner.compile(`
    @d2RequireAnyScope("self.read", "self.write")
    op listOrders(): void;
  `);
  const values = [
    ...runner.program.stateMap(D2_REQUIRE_ANY_SCOPE_KEY).values(),
  ] as string[][];
  expect(values).toHaveLength(1);
  expect(values[0]).toEqual(["self.read", "self.write"]);
});

it("d2RequireAllScopes_StoresScopesArrayUnderAllKey", async () => {
  await runner.compile(`
    @d2RequireAllScopes("auth.password.change", "self.write")
    op adminAction(): void;
  `);
  const values = [
    ...runner.program.stateMap(D2_REQUIRE_ALL_SCOPES_KEY).values(),
  ] as string[][];
  expect(values).toHaveLength(1);
  expect(values[0]).toEqual(["auth.password.change", "self.write"]);
});

it("d2RateLimitTier_StoresTierStringUnderTierKey", async () => {
  // Use diagnose() rather than compile() because @d2RateLimitTier on an op
  // without @route emits a rate-tier-requires-route error from $onValidate.
  // We verify the tier value is stored despite the diagnostic (validate-and-continue).
  await runner.diagnose(`
    @d2RateLimitTier("Standard")
    op getProduct(): void;
  `);
  const values = [...runner.program.stateMap(D2_RATE_LIMIT_TIER_KEY).values()];
  expect(values).toContain("Standard");
});

it("d2Audience_StoresAudienceStringUnderAudienceKey", async () => {
  await runner.compile(`
    @d2Audience("d2-edge")
    op checkHealth(): void;
  `);
  const values = [...runner.program.stateMap(D2_AUDIENCE_KEY).values()];
  expect(values).toContain("d2-edge");
});

it("d2ServedBy_StoresOwnerStringUnderServedByKey", async () => {
  await runner.compile(`
    @d2ServedBy("Edge")
    op authenticate(): void;
  `);
  const values = [...runner.program.stateMap(D2_SERVED_BY_KEY).values()];
  expect(values).toContain("Edge");
});

it("d2GrpcMethod_DefaultsStreamingToUnary", async () => {
  await runner.compile(`
    @d2GrpcMethod("Push", "PushNotificationCreated")
    op pushNotification(): void;
  `);
  const entries = [
    ...runner.program.stateMap(D2_GRPC_METHOD_KEY).values(),
  ] as GrpcMethodPayload[];
  expect(entries).toHaveLength(1);
  expect(entries[0]).toEqual({
    service: "Push",
    method: "PushNotificationCreated",
    streaming: "unary",
  });
});

it("d2GrpcMethod_StoresExplicitStreamingMode", async () => {
  await runner.compile(`
    @d2GrpcMethod("Events", "StreamEvents", "serverStream")
    op streamEvents(): void;
  `);
  const entries = [
    ...runner.program.stateMap(D2_GRPC_METHOD_KEY).values(),
  ] as GrpcMethodPayload[];
  expect(entries).toHaveLength(1);
  expect(entries[0]).toEqual({
    service: "Events",
    method: "StreamEvents",
    streaming: "serverStream",
  });
});

it("d2Redact_StoresTrueUnderRedactKeyOnModelProperty", async () => {
  await runner.compile(`
    model UserInput {
      @d2Redact email: string;
    }
  `);
  const values = [...runner.program.stateMap(D2_REDACT_KEY).values()];
  expect(values).toContain(true);
});

it("d2ServerPush_StoresTargetStringUnderServerPushKey", async () => {
  await runner.compile(`
    @d2ServerPush("user")
    op notifyUser(): void;
  `);
  const values = [...runner.program.stateMap(D2_SERVER_PUSH_KEY).values()];
  expect(values).toContain("user");
});

it("d2Idempotent_StoresHeaderPolicy", async () => {
  await runner.compile(`
    @d2Idempotent("header", 300)
    op createOrder(): void;
  `);
  const entries = [
    ...runner.program.stateMap(D2_IDEMPOTENT_KEY).values(),
  ] as IdempotentPayload[];
  expect(entries).toHaveLength(1);
  expect(entries[0]).toEqual({
    keySource: "header",
    ttlSeconds: 300,
    fields: [],
  });
});

it("d2Idempotent_StoresDerivedPolicyWithFields", async () => {
  await runner.compile(`
    @d2Idempotent("derived", 600, "orgId", "userId")
    op createPayment(): void;
  `);
  const entries = [
    ...runner.program.stateMap(D2_IDEMPOTENT_KEY).values(),
  ] as IdempotentPayload[];
  expect(entries).toHaveLength(1);
  expect(entries[0]).toEqual({
    keySource: "derived",
    ttlSeconds: 600,
    fields: ["orgId", "userId"],
  });
});

it("d2Resilience_StoresBareDefaultsExpressionAsRawString", async () => {
  await runner.compile(`
    @d2Resilience("retry()")
    op callExternalService(): void;
  `);
  const values = [...runner.program.stateMap(D2_RESILIENCE_KEY).values()];
  expect(values).toContain("retry()");
});

it("d2Resilience_StoresInlineTunablesExpressionAsRawString", async () => {
  await runner.compile(`
    @d2Resilience("retry(3, circuitBreaker(threshold: 5))")
    op callCriticalService(): void;
  `);
  const values = [...runner.program.stateMap(D2_RESILIENCE_KEY).values()];
  expect(values).toContain("retry(3, circuitBreaker(threshold: 5))");
});

it("d2Csrf_StoresPostureStringUnderCsrfKey", async () => {
  await runner.compile(`
    @d2Csrf("exempt")
    op getPublicData(): void;
  `);
  const values = [...runner.program.stateMap(D2_CSRF_KEY).values()];
  expect(values).toContain("exempt");
});

it("d2Harmless_StoresTrueUnderHarmlessKeyOnOperation", async () => {
  await runner.compile(`
    @d2Harmless
    op healthCheck(): void;
  `);
  const values = [...runner.program.stateMap(D2_HARMLESS_KEY).values()];
  expect(values).toContain(true);
});

// ---------------------------------------------------------------------------
// Gate test: all 12 decorators co-apply and round-trip independently.
// Uses the httpRunner (which includes HttpTestLibrary) so @d2RateLimitTier on
// a @route-bound op does not trigger rate-tier-requires-route.
// Uses real scope names from scopes.spec.json; @d2Harmless is on a separate
// op without scope decorators (the harmless/scope conflict is enforced by
// $onValidate and verified in the $onValidate tests above).
// ---------------------------------------------------------------------------

it("allTwelveDecorators_CoApplyAndRoundTripIndependently", async () => {
  await httpRunner.compile(`
    model RequestBody {
      @d2Redact sensitiveField: string;
    }

    @d2RequireAnyScope("self.write", "auth.password.change")
    @d2RateLimitTier("Elevated")
    @d2Audience("d2-edge")
    @d2ServedBy("OrderService")
    @d2GrpcMethod("Orders", "CreateOrder")
    @d2ServerPush("session")
    @d2Idempotent("derived", 300, "orgId", "requestId")
    @d2Resilience("retry(circuitBreaker(singleflight()))")
    @d2Csrf("required")
    @get @route("/gatetest-createorder")
    op gateTestCreateOrder(body: RequestBody): void;

    @d2RequireAllScopes("auth.password.change", "self.write")
    @get @route("/gatetest-adminop")
    op gateTestAdminOp(): void;

    @d2Harmless
    @get @route("/gatetest-health")
    op gateTestHealthCheck(): void;
  `);

  const program = httpRunner.program;

  const anyValues = [
    ...program.stateMap(D2_REQUIRE_ANY_SCOPE_KEY).values(),
  ] as string[][];
  expect(
    anyValues.some(
      (v) => v.includes("self.write") && v.includes("auth.password.change"),
    ),
  ).toBe(true);

  const allValues = [
    ...program.stateMap(D2_REQUIRE_ALL_SCOPES_KEY).values(),
  ] as string[][];
  expect(
    allValues.some(
      (v) => v.includes("auth.password.change") && v.includes("self.write"),
    ),
  ).toBe(true);

  const tierValues = [...program.stateMap(D2_RATE_LIMIT_TIER_KEY).values()];
  expect(tierValues).toContain("Elevated");

  const audienceValues = [...program.stateMap(D2_AUDIENCE_KEY).values()];
  expect(audienceValues).toContain("d2-edge");

  const servedByValues = [...program.stateMap(D2_SERVED_BY_KEY).values()];
  expect(servedByValues).toContain("OrderService");

  const grpcEntries = [
    ...program.stateMap(D2_GRPC_METHOD_KEY).values(),
  ] as GrpcMethodPayload[];
  expect(
    grpcEntries.some(
      (e) =>
        e.service === "Orders" &&
        e.method === "CreateOrder" &&
        e.streaming === "unary",
    ),
  ).toBe(true);

  const redactValues = [...program.stateMap(D2_REDACT_KEY).values()];
  expect(redactValues).toContain(true);

  const serverPushValues = [...program.stateMap(D2_SERVER_PUSH_KEY).values()];
  expect(serverPushValues).toContain("session");

  const idempotentEntries = [
    ...program.stateMap(D2_IDEMPOTENT_KEY).values(),
  ] as IdempotentPayload[];
  expect(
    idempotentEntries.some(
      (e) =>
        e.keySource === "derived" &&
        e.ttlSeconds === 300 &&
        e.fields.includes("orgId") &&
        e.fields.includes("requestId"),
    ),
  ).toBe(true);

  const resilienceValues = [...program.stateMap(D2_RESILIENCE_KEY).values()];
  expect(resilienceValues).toContain("retry(circuitBreaker(singleflight()))");

  const csrfValues = [...program.stateMap(D2_CSRF_KEY).values()];
  expect(csrfValues).toContain("required");

  const harmlessValues = [...program.stateMap(D2_HARMLESS_KEY).values()];
  expect(harmlessValues).toContain(true);
});

// ===========================================================================
// SPEC-REGISTRY ANCHOR GUARD (MANDATORY — R2)
// These tests prove the repo-root anchor in spec-registry.ts resolves correctly.
// A wrong segment count silently yields an empty set, making every scope/audience
// appear unknown and producing false "unknown-scope" / "unknown-audience" errors.
// ===========================================================================

describe("specRegistry_LoadsKnownScope", () => {
  afterEach(() => _resetSpecRegistryCache());

  it("loadScopeNames() contains 'self.read' — proves repo-root anchor is correct", () => {
    const names = loadScopeNames();
    expect(names.has("self.read")).toBe(true);
  });

  it("loadScopeNames() contains 'anon.public.health'", () => {
    const names = loadScopeNames();
    expect(names.has("anon.public.health")).toBe(true);
  });

  it("loadScopeNames() returns a non-empty set", () => {
    const names = loadScopeNames();
    expect(names.size).toBeGreaterThan(0);
  });
});

describe("specRegistry_LoadsKnownAudience", () => {
  afterEach(() => _resetSpecRegistryCache());

  it("loadAudienceNames() contains 'Files' — proves repo-root anchor is correct", () => {
    const names = loadAudienceNames();
    expect(names.has("Files")).toBe(true);
  });

  it("loadAudienceNames() does NOT contain 'd2-edge' (it is the self-audience special-case)", () => {
    const names = loadAudienceNames();
    // d2-edge is not declared in audiences.spec.json; it is handled as a special case in the validator
    expect(names.has("d2-edge")).toBe(false);
  });

  it("loadAudienceNames() returns a non-empty set", () => {
    const names = loadAudienceNames();
    expect(names.size).toBeGreaterThan(0);
  });
});

// ===========================================================================
// CATALOG INTEGRITY (drift guard — every ResilienceDiagnosticCode in $lib)
// ===========================================================================

// ===========================================================================
// DIRECT UNIT TESTS for validators.ts — exercises src/ for coverage
// These call validators directly with mock contexts so V8 sees the source file.
// ===========================================================================

function makeMockCtxWithDiags(): {
  ctx: DecoratorContext;
  target: Operation;
  diags: Array<{ code: string }>;
} {
  const diags: Array<{ code: string }> = [];
  const storeMap = new Map<symbol, Map<object, unknown>>();
  const ctx = {
    program: {
      stateMap(key: symbol): Map<object, unknown> {
        if (!storeMap.has(key)) storeMap.set(key, new Map());
        return storeMap.get(key)!;
      },
      reportDiagnostic(diag: { code: string }): void {
        diags.push(diag);
      },
    },
  } as unknown as DecoratorContext;
  const target = {} as unknown as Operation;
  return { ctx, target, diags };
}

describe("directUnit_validateRateLimitTier", () => {
  it("no diagnostic for valid tier 'Elevated'", () => {
    const { ctx, target, diags } = makeMockCtxWithDiags();
    validateRateLimitTier(ctx, target, "Elevated");
    expect(diags).toHaveLength(0);
  });

  it("emits invalid-rate-limit-tier for 'bad'", () => {
    const { ctx, target, diags } = makeMockCtxWithDiags();
    validateRateLimitTier(ctx, target, "bad");
    expect(diags.some((d) => d.code.endsWith("invalid-rate-limit-tier"))).toBe(
      true,
    );
  });
});

describe("directUnit_validateGrpcStreaming", () => {
  it("no diagnostic for 'unary'", () => {
    const { ctx, target, diags } = makeMockCtxWithDiags();
    validateGrpcStreaming(ctx, target, "unary");
    expect(diags).toHaveLength(0);
  });

  it("no diagnostic for 'clientStream'", () => {
    const { ctx, target, diags } = makeMockCtxWithDiags();
    validateGrpcStreaming(ctx, target, "clientStream");
    expect(diags).toHaveLength(0);
  });

  it("no diagnostic for 'bidiStream'", () => {
    const { ctx, target, diags } = makeMockCtxWithDiags();
    validateGrpcStreaming(ctx, target, "bidiStream");
    expect(diags).toHaveLength(0);
  });

  it("emits invalid-grpc-streaming for 'bad'", () => {
    const { ctx, target, diags } = makeMockCtxWithDiags();
    validateGrpcStreaming(ctx, target, "bad");
    expect(diags.some((d) => d.code.endsWith("invalid-grpc-streaming"))).toBe(
      true,
    );
  });
});

describe("directUnit_validatePushTarget", () => {
  it("no diagnostic for 'session'", () => {
    const { ctx, target, diags } = makeMockCtxWithDiags();
    validatePushTarget(ctx, target, "session");
    expect(diags).toHaveLength(0);
  });

  it("emits invalid-push-target for 'team'", () => {
    const { ctx, target, diags } = makeMockCtxWithDiags();
    validatePushTarget(ctx, target, "team");
    expect(diags.some((d) => d.code.endsWith("invalid-push-target"))).toBe(
      true,
    );
  });
});

describe("directUnit_validateCsrfPosture", () => {
  it("no diagnostic for 'required'", () => {
    const { ctx, target, diags } = makeMockCtxWithDiags();
    validateCsrfPosture(ctx, target, "required");
    expect(diags).toHaveLength(0);
  });

  it("emits invalid-csrf-posture for 'optional'", () => {
    const { ctx, target, diags } = makeMockCtxWithDiags();
    validateCsrfPosture(ctx, target, "optional");
    expect(diags.some((d) => d.code.endsWith("invalid-csrf-posture"))).toBe(
      true,
    );
  });
});

describe("directUnit_validateIdempotent", () => {
  it("no diagnostic for valid header config", () => {
    const { ctx, target, diags } = makeMockCtxWithDiags();
    validateIdempotent(ctx, target, "header", 300, []);
    expect(diags).toHaveLength(0);
  });

  it("no diagnostic for valid derived config with fields", () => {
    const { ctx, target, diags } = makeMockCtxWithDiags();
    validateIdempotent(ctx, target, "derived", 600, ["orgId"]);
    expect(diags).toHaveLength(0);
  });

  it("emits invalid-idempotent-key-source for unknown keySource", () => {
    const { ctx, target, diags } = makeMockCtxWithDiags();
    validateIdempotent(ctx, target, "cookie", 300, []);
    expect(
      diags.some((d) => d.code.endsWith("invalid-idempotent-key-source")),
    ).toBe(true);
  });

  it("emits invalid-idempotent-ttl for ttlSeconds = -1", () => {
    const { ctx, target, diags } = makeMockCtxWithDiags();
    validateIdempotent(ctx, target, "header", -1, []);
    expect(diags.some((d) => d.code.endsWith("invalid-idempotent-ttl"))).toBe(
      true,
    );
  });

  it("emits idempotent-derived-requires-fields for derived with no fields", () => {
    const { ctx, target, diags } = makeMockCtxWithDiags();
    validateIdempotent(ctx, target, "derived", 300, []);
    expect(
      diags.some((d) => d.code.endsWith("idempotent-derived-requires-fields")),
    ).toBe(true);
  });

  it("emits idempotent-header-forbids-fields for header with fields", () => {
    const { ctx, target, diags } = makeMockCtxWithDiags();
    validateIdempotent(ctx, target, "header", 300, ["x"]);
    expect(
      diags.some((d) => d.code.endsWith("idempotent-header-forbids-fields")),
    ).toBe(true);
  });
});

describe("directUnit_validateScopes", () => {
  it("no diagnostic for known scope 'self.read'", () => {
    const { ctx, target, diags } = makeMockCtxWithDiags();
    validateScopes(ctx, target, ["self.read"]);
    expect(diags).toHaveLength(0);
  });

  it("emits unknown-scope for unknown scope", () => {
    const { ctx, target, diags } = makeMockCtxWithDiags();
    validateScopes(ctx, target, ["not.a.scope"]);
    expect(diags.some((d) => d.code.endsWith("unknown-scope"))).toBe(true);
  });

  it("emits unknown-scope for empty string scope", () => {
    const { ctx, target, diags } = makeMockCtxWithDiags();
    validateScopes(ctx, target, [""]);
    expect(diags.some((d) => d.code.endsWith("unknown-scope"))).toBe(true);
  });

  it("multiple scopes — emits per-unknown scope", () => {
    const { ctx, target, diags } = makeMockCtxWithDiags();
    validateScopes(ctx, target, ["self.read", "not.real"]);
    expect(diags.filter((d) => d.code.endsWith("unknown-scope"))).toHaveLength(
      1,
    );
  });
});

describe("directUnit_validateAudience", () => {
  it("no diagnostic for 'd2-edge' (self-audience special case)", () => {
    const { ctx, target, diags } = makeMockCtxWithDiags();
    validateAudience(ctx, target, "d2-edge");
    expect(diags).toHaveLength(0);
  });

  it("no diagnostic for 'Files'", () => {
    const { ctx, target, diags } = makeMockCtxWithDiags();
    validateAudience(ctx, target, "Files");
    expect(diags).toHaveLength(0);
  });

  it("emits unknown-audience for 'BadAudience'", () => {
    const { ctx, target, diags } = makeMockCtxWithDiags();
    validateAudience(ctx, target, "BadAudience");
    expect(diags.some((d) => d.code.endsWith("unknown-audience"))).toBe(true);
  });
});

describe("directUnit_validateServedBy", () => {
  it("no diagnostic for non-empty string", () => {
    const { ctx, target, diags } = makeMockCtxWithDiags();
    validateServedBy(ctx, target, "Edge");
    expect(diags).toHaveLength(0);
  });

  it("emits empty-served-by for empty string", () => {
    const { ctx, target, diags } = makeMockCtxWithDiags();
    validateServedBy(ctx, target, "");
    expect(diags.some((d) => d.code.endsWith("empty-served-by"))).toBe(true);
  });

  it("emits empty-served-by for whitespace-only string", () => {
    const { ctx, target, diags } = makeMockCtxWithDiags();
    validateServedBy(ctx, target, "   ");
    expect(diags.some((d) => d.code.endsWith("empty-served-by"))).toBe(true);
  });
});

describe("directUnit_validateResilience", () => {
  it("no diagnostic for valid 'retry()'", () => {
    const { ctx, target, diags } = makeMockCtxWithDiags();
    validateResilience(ctx, target, "retry()");
    expect(diags).toHaveLength(0);
  });

  it("no diagnostic for 'retry(circuitBreaker(singleflight()))'", () => {
    const { ctx, target, diags } = makeMockCtxWithDiags();
    validateResilience(ctx, target, "retry(circuitBreaker(singleflight()))");
    expect(diags).toHaveLength(0);
  });

  it("emits resilience-malformed for empty string", () => {
    const { ctx, target, diags } = makeMockCtxWithDiags();
    validateResilience(ctx, target, "");
    expect(diags.some((d) => d.code.endsWith("resilience-malformed"))).toBe(
      true,
    );
  });

  it("emits resilience-unknown-policy for 'breaker()'", () => {
    const { ctx, target, diags } = makeMockCtxWithDiags();
    validateResilience(ctx, target, "breaker()");
    expect(
      diags.some((d) => d.code.endsWith("resilience-unknown-policy")),
    ).toBe(true);
  });

  it("emits resilience-unknown-arg for 'retry(foo: 1)'", () => {
    const { ctx, target, diags } = makeMockCtxWithDiags();
    validateResilience(ctx, target, "retry(foo: 1)");
    expect(diags.some((d) => d.code.endsWith("resilience-unknown-arg"))).toBe(
      true,
    );
  });

  it("emits resilience-bad-arg for 'retry(maxAttempts: 0)'", () => {
    const { ctx, target, diags } = makeMockCtxWithDiags();
    validateResilience(ctx, target, "retry(maxAttempts: 0)");
    expect(diags.some((d) => d.code.endsWith("resilience-bad-arg"))).toBe(true);
  });

  it("emits resilience-multiple-inner for 'retry(circuitBreaker(), singleflight())'", () => {
    const { ctx, target, diags } = makeMockCtxWithDiags();
    validateResilience(ctx, target, "retry(circuitBreaker(), singleflight())");
    expect(
      diags.some((d) => d.code.endsWith("resilience-multiple-inner")),
    ).toBe(true);
  });

  it("emits resilience-positional-after-named for 'retry(maxAttempts: 3, 1000)'", () => {
    const { ctx, target, diags } = makeMockCtxWithDiags();
    validateResilience(ctx, target, "retry(maxAttempts: 3, 1000)");
    expect(
      diags.some((d) => d.code.endsWith("resilience-positional-after-named")),
    ).toBe(true);
  });
});

// ===========================================================================
// DIRECT UNIT TESTS for $onValidate — exercises src/onvalidate.ts for coverage
// ===========================================================================

describe("directUnit_$onValidate", () => {
  it("emits rate-tier-requires-route when op has tier but no route", () => {
    const diags: Array<{ code: string }> = [];
    const op = {} as unknown as Operation;
    const stateMaps = new Map<symbol, Map<object, unknown>>();
    const tierMap = new Map<object, unknown>();
    tierMap.set(op, "Standard");

    // Inject tier key map; harmless key has no entries
    const D2_RATE_LIMIT_TIER_KEY_SYM = Symbol.for("D2.d2RateLimitTier");
    stateMaps.set(D2_RATE_LIMIT_TIER_KEY_SYM, tierMap as Map<object, unknown>);

    const mockProgram = {
      stateMap(key: symbol): Map<object, unknown> {
        return stateMaps.get(key) ?? new Map();
      },
      reportDiagnostic(diag: { code: string }): void {
        diags.push(diag);
      },
      hasError(): boolean {
        return diags.length > 0;
      },
    };

    // @typespec/http's getRoutePath will be called with the mock program;
    // it will return undefined for any op not registered in its state.
    // This causes $onValidate to emit the rate-tier-requires-route diagnostic.
    $onValidate(mockProgram as unknown as Program);
    expect(diags.some((d) => d.code.endsWith("rate-tier-requires-route"))).toBe(
      true,
    );
  });

  it("emits harmless-scope-conflict when op has both harmless and scope key", () => {
    const diags: Array<{ code: string }> = [];
    const op = {} as unknown as Operation;
    const stateMaps = new Map<symbol, Map<object, unknown>>();

    const D2_HARMLESS_KEY_SYM = Symbol.for("D2.d2Harmless");
    const D2_REQUIRE_ANY_SCOPE_KEY_SYM = Symbol.for("D2.d2RequireAnyScope");
    const D2_RATE_LIMIT_TIER_KEY_SYM = Symbol.for("D2.d2RateLimitTier");
    const D2_REQUIRE_ALL_SCOPES_KEY_SYM = Symbol.for("D2.d2RequireAllScopes");

    const harmlessMap = new Map<object, unknown>();
    harmlessMap.set(op, true);
    stateMaps.set(D2_HARMLESS_KEY_SYM, harmlessMap as Map<object, unknown>);

    const anyMap = new Map<object, unknown>();
    anyMap.set(op, ["self.read"]);
    stateMaps.set(D2_REQUIRE_ANY_SCOPE_KEY_SYM, anyMap as Map<object, unknown>);

    // No rate-limit tier entries, no all-scopes entries
    stateMaps.set(D2_RATE_LIMIT_TIER_KEY_SYM, new Map());
    stateMaps.set(D2_REQUIRE_ALL_SCOPES_KEY_SYM, new Map());

    const mockProgram = {
      stateMap(key: symbol): Map<object, unknown> {
        return stateMaps.get(key) ?? new Map();
      },
      reportDiagnostic(diag: { code: string }): void {
        diags.push(diag);
      },
      hasError(): boolean {
        return diags.length > 0;
      },
    };

    $onValidate(mockProgram as unknown as Program);
    expect(diags.some((d) => d.code.endsWith("harmless-scope-conflict"))).toBe(
      true,
    );
  });

  it("no diagnostic when rate-tier op has a route (getRoutePath returns non-undefined)", () => {
    const diags: Array<{ code: string }> = [];
    const op = { kind: "Operation", name: "routedOp" } as unknown as Operation;
    const stateMaps = new Map<symbol, Map<object, unknown>>();

    const D2_RATE_LIMIT_TIER_KEY_SYM = Symbol.for("D2.d2RateLimitTier");
    const D2_HARMLESS_KEY_SYM = Symbol.for("D2.d2Harmless");
    // @typespec/http route state key — what getRoutePath looks up
    const HTTP_ROUTES_KEY = Symbol.for("@typespec/http/routes");
    const HTTP_SHARED_ROUTES_KEY = Symbol.for("@typespec/http/sharedRoutes");

    const tierMap = new Map<object, unknown>();
    tierMap.set(op, "Standard");
    stateMaps.set(D2_RATE_LIMIT_TIER_KEY_SYM, tierMap as Map<object, unknown>);
    stateMaps.set(D2_HARMLESS_KEY_SYM, new Map());

    // Inject a route path so getRoutePath returns { path, shared }
    const routeMap = new Map<object, unknown>();
    routeMap.set(op, "/test-route");
    stateMaps.set(HTTP_ROUTES_KEY, routeMap as Map<object, unknown>);
    stateMaps.set(HTTP_SHARED_ROUTES_KEY, new Map());

    const mockProgram = {
      stateMap(key: symbol): Map<object, unknown> {
        return stateMaps.get(key) ?? new Map();
      },
      reportDiagnostic(diag: { code: string }): void {
        diags.push(diag);
      },
      hasError(): boolean {
        return diags.length > 0;
      },
    };

    $onValidate(mockProgram as unknown as Program);
    // rate-tier op has a route → no rate-tier-requires-route diagnostic
    expect(diags.some((d) => d.code.endsWith("rate-tier-requires-route"))).toBe(
      false,
    );
  });

  it("no diagnostics when harmless op has no scope decorators and no tier ops", () => {
    const diags: Array<{ code: string }> = [];
    const op = {} as unknown as Operation;
    const stateMaps = new Map<symbol, Map<object, unknown>>();

    const D2_HARMLESS_KEY_SYM = Symbol.for("D2.d2Harmless");
    const D2_RATE_LIMIT_TIER_KEY_SYM = Symbol.for("D2.d2RateLimitTier");
    const D2_REQUIRE_ANY_SCOPE_KEY_SYM = Symbol.for("D2.d2RequireAnyScope");
    const D2_REQUIRE_ALL_SCOPES_KEY_SYM = Symbol.for("D2.d2RequireAllScopes");

    const harmlessMap = new Map<object, unknown>();
    harmlessMap.set(op, true);
    stateMaps.set(D2_HARMLESS_KEY_SYM, harmlessMap as Map<object, unknown>);
    stateMaps.set(D2_RATE_LIMIT_TIER_KEY_SYM, new Map());
    stateMaps.set(D2_REQUIRE_ANY_SCOPE_KEY_SYM, new Map());
    stateMaps.set(D2_REQUIRE_ALL_SCOPES_KEY_SYM, new Map());

    const mockProgram = {
      stateMap(key: symbol): Map<object, unknown> {
        return stateMaps.get(key) ?? new Map();
      },
      reportDiagnostic(_diag: { code: string }): void {
        diags.push(_diag);
      },
      hasError(): boolean {
        return diags.length > 0;
      },
    };

    $onValidate(mockProgram as unknown as Program);
    expect(diags).toHaveLength(0);
  });

  it("emits harmless-scope-conflict when op has both harmless and all-scopes", () => {
    const diags: Array<{ code: string }> = [];
    const op = {} as unknown as Operation;
    const stateMaps = new Map<symbol, Map<object, unknown>>();

    const D2_HARMLESS_KEY_SYM = Symbol.for("D2.d2Harmless");
    const D2_RATE_LIMIT_TIER_KEY_SYM = Symbol.for("D2.d2RateLimitTier");
    const D2_REQUIRE_ANY_SCOPE_KEY_SYM = Symbol.for("D2.d2RequireAnyScope");
    const D2_REQUIRE_ALL_SCOPES_KEY_SYM = Symbol.for("D2.d2RequireAllScopes");

    const harmlessMap = new Map<object, unknown>();
    harmlessMap.set(op, true);
    stateMaps.set(D2_HARMLESS_KEY_SYM, harmlessMap as Map<object, unknown>);

    const allMap = new Map<object, unknown>();
    allMap.set(op, ["self.read"]);
    stateMaps.set(
      D2_REQUIRE_ALL_SCOPES_KEY_SYM,
      allMap as Map<object, unknown>,
    );
    stateMaps.set(D2_RATE_LIMIT_TIER_KEY_SYM, new Map());
    stateMaps.set(D2_REQUIRE_ANY_SCOPE_KEY_SYM, new Map());

    const mockProgram = {
      stateMap(key: symbol): Map<object, unknown> {
        return stateMaps.get(key) ?? new Map();
      },
      reportDiagnostic(diag: { code: string }): void {
        diags.push(diag);
      },
      hasError(): boolean {
        return diags.length > 0;
      },
    };

    $onValidate(mockProgram as unknown as Program);
    expect(diags.some((d) => d.code.endsWith("harmless-scope-conflict"))).toBe(
      true,
    );
  });
});

it("lib_DiagnosticsCatalogCoversAllResilienceCodes", () => {
  // The ResilienceDiagnosticCode union lists every code the parser can emit.
  // Each must have a corresponding entry in $lib.diagnostics so that
  // reportDiagnostic never throws on an unknown code.
  const resilienceCodes: ResilienceDiagnosticCode[] = [
    "resilience-malformed",
    "resilience-unknown-policy",
    "resilience-unknown-arg",
    "resilience-bad-arg",
    "resilience-multiple-inner",
    "resilience-positional-after-named",
  ];
  const catalog = $lib.diagnostics as Record<string, unknown>;
  for (const code of resilienceCodes) expect(catalog).toHaveProperty(code);
});

it("lib_AllDiagnosticsHaveErrorSeverity", () => {
  // Every diagnostic must be severity "error" — a warning would NOT fail the
  // build, which violates the hard requirement that invalid configs fail loud.
  const catalog = $lib.diagnostics as Record<
    string,
    { severity: string; messages: Record<string, unknown> }
  >;
  for (const [code, diag] of Object.entries(catalog))
    expect(diag.severity, `code '${code}' should be severity "error"`).toBe(
      "error",
    );
});

// ===========================================================================
// IN-DECORATOR REJECTION + ACCEPTANCE TESTS
// Each rejection test asserts BOTH the diagnostic code AND program.hasError()
// (an invalid config must fail the build, not merely surface a diagnostic code)
// ===========================================================================

// ---------------------------------------------------------------------------
// Shared runner is already set up above (D2DecoratorTestLibrary + runner).
// ---------------------------------------------------------------------------

function getDiagCodes(r: BasicTestRunner): string[] {
  return r.program.diagnostics.map((d) => d.code);
}

// --- @d2RateLimitTier ---

describe("d2RateLimitTier_RejectsInvalidTier", () => {
  it("emits invalid-rate-limit-tier and hasError() for 'Standardx'", async () => {
    await runner.diagnose(`
      @d2RateLimitTier("Standardx")
      op badOp(): void;
    `);
    expect(getDiagCodes(runner)).toContain(
      "@d2/typespec-decorators/invalid-rate-limit-tier",
    );
    expect(runner.program.hasError()).toBe(true);
  });

  it("emits invalid-rate-limit-tier for wrong-case 'standard'", async () => {
    await runner.diagnose(`
      @d2RateLimitTier("standard")
      op badOp(): void;
    `);
    expect(getDiagCodes(runner)).toContain(
      "@d2/typespec-decorators/invalid-rate-limit-tier",
    );
    expect(runner.program.hasError()).toBe(true);
  });
});

describe("d2RateLimitTier_AcceptsValidTiers", () => {
  it("produces no invalid-rate-limit-tier diagnostic for 'Standard'", async () => {
    await runner.diagnose(`
      @d2RateLimitTier("Standard")
      op goodOp(): void;
    `);
    expect(getDiagCodes(runner)).not.toContain(
      "@d2/typespec-decorators/invalid-rate-limit-tier",
    );
  });

  it("produces no diagnostic for 'Elevated'", async () => {
    await runner.diagnose(`
      @d2RateLimitTier("Elevated")
      op goodOp(): void;
    `);
    expect(getDiagCodes(runner)).not.toContain(
      "@d2/typespec-decorators/invalid-rate-limit-tier",
    );
  });

  it("produces no diagnostic for 'Restricted'", async () => {
    await runner.diagnose(`
      @d2RateLimitTier("Restricted")
      op goodOp(): void;
    `);
    expect(getDiagCodes(runner)).not.toContain(
      "@d2/typespec-decorators/invalid-rate-limit-tier",
    );
  });
});

// --- @d2GrpcMethod streaming ---

describe("d2GrpcMethod_RejectsInvalidStreaming", () => {
  it("emits invalid-grpc-streaming for 'duplexStream'", async () => {
    await runner.diagnose(`
      @d2GrpcMethod("Svc", "Method", "duplexStream")
      op badOp(): void;
    `);
    expect(getDiagCodes(runner)).toContain(
      "@d2/typespec-decorators/invalid-grpc-streaming",
    );
    expect(runner.program.hasError()).toBe(true);
  });
});

describe("d2GrpcMethod_AcceptsServerStream", () => {
  it("produces no invalid-grpc-streaming diagnostic for 'serverStream'", async () => {
    await runner.diagnose(`
      @d2GrpcMethod("Svc", "Method", "serverStream")
      op goodOp(): void;
    `);
    expect(getDiagCodes(runner)).not.toContain(
      "@d2/typespec-decorators/invalid-grpc-streaming",
    );
  });
});

// --- @d2ServerPush ---

describe("d2ServerPush_RejectsInvalidTarget", () => {
  it("emits invalid-push-target for 'org'", async () => {
    await runner.diagnose(`
      @d2ServerPush("org")
      op badOp(): void;
    `);
    expect(getDiagCodes(runner)).toContain(
      "@d2/typespec-decorators/invalid-push-target",
    );
    expect(runner.program.hasError()).toBe(true);
  });
});

describe("d2ServerPush_AcceptsUser", () => {
  it("produces no invalid-push-target diagnostic for 'user'", async () => {
    await runner.diagnose(`
      @d2ServerPush("user")
      op goodOp(): void;
    `);
    expect(getDiagCodes(runner)).not.toContain(
      "@d2/typespec-decorators/invalid-push-target",
    );
  });
});

// --- @d2Csrf ---

describe("d2Csrf_RejectsInvalidPosture", () => {
  it("emits invalid-csrf-posture for 'maybe'", async () => {
    await runner.diagnose(`
      @d2Csrf("maybe")
      op badOp(): void;
    `);
    expect(getDiagCodes(runner)).toContain(
      "@d2/typespec-decorators/invalid-csrf-posture",
    );
    expect(runner.program.hasError()).toBe(true);
  });
});

describe("d2Csrf_AcceptsExempt", () => {
  it("produces no invalid-csrf-posture diagnostic for 'exempt'", async () => {
    await runner.diagnose(`
      @d2Csrf("exempt")
      op goodOp(): void;
    `);
    expect(getDiagCodes(runner)).not.toContain(
      "@d2/typespec-decorators/invalid-csrf-posture",
    );
  });
});

// --- @d2Idempotent ---

describe("d2Idempotent_RejectsInvalidKeySource", () => {
  it("emits invalid-idempotent-key-source for 'cookie'", async () => {
    await runner.diagnose(`
      @d2Idempotent("cookie", 300)
      op badOp(): void;
    `);
    expect(getDiagCodes(runner)).toContain(
      "@d2/typespec-decorators/invalid-idempotent-key-source",
    );
    expect(runner.program.hasError()).toBe(true);
  });
});

describe("d2Idempotent_RejectsNonPositiveTtl", () => {
  it("emits invalid-idempotent-ttl for ttlSeconds 0", async () => {
    await runner.diagnose(`
      @d2Idempotent("header", 0)
      op badOp(): void;
    `);
    expect(getDiagCodes(runner)).toContain(
      "@d2/typespec-decorators/invalid-idempotent-ttl",
    );
    expect(runner.program.hasError()).toBe(true);
  });
});

describe("d2Idempotent_DerivedWithoutFieldsRejected", () => {
  it("emits idempotent-derived-requires-fields when no fields supplied for 'derived'", async () => {
    await runner.diagnose(`
      @d2Idempotent("derived", 300)
      op badOp(): void;
    `);
    expect(getDiagCodes(runner)).toContain(
      "@d2/typespec-decorators/idempotent-derived-requires-fields",
    );
    expect(runner.program.hasError()).toBe(true);
  });
});

describe("d2Idempotent_HeaderWithFieldsRejected", () => {
  it("emits idempotent-header-forbids-fields when fields supplied for 'header'", async () => {
    await runner.diagnose(`
      @d2Idempotent("header", 300, "orgId")
      op badOp(): void;
    `);
    expect(getDiagCodes(runner)).toContain(
      "@d2/typespec-decorators/idempotent-header-forbids-fields",
    );
    expect(runner.program.hasError()).toBe(true);
  });
});

// --- @d2RequireAnyScope ---

describe("d2RequireAnyScope_RejectsUnknownScope", () => {
  it("emits unknown-scope for 'made.up.scope'", async () => {
    await runner.diagnose(`
      @d2RequireAnyScope("made.up.scope")
      op badOp(): void;
    `);
    expect(getDiagCodes(runner)).toContain(
      "@d2/typespec-decorators/unknown-scope",
    );
    expect(runner.program.hasError()).toBe(true);
  });
});

describe("d2RequireAnyScope_AcceptsKnownScope", () => {
  it("produces no unknown-scope diagnostic for 'self.read'", async () => {
    await runner.diagnose(`
      @d2RequireAnyScope("self.read")
      op goodOp(): void;
    `);
    expect(getDiagCodes(runner)).not.toContain(
      "@d2/typespec-decorators/unknown-scope",
    );
  });
});

// --- @d2RequireAllScopes ---

describe("d2RequireAllScopes_RejectsUnknownScope", () => {
  it("emits unknown-scope for 'not.a.scope'", async () => {
    await runner.diagnose(`
      @d2RequireAllScopes("not.a.scope")
      op badOp(): void;
    `);
    expect(getDiagCodes(runner)).toContain(
      "@d2/typespec-decorators/unknown-scope",
    );
    expect(runner.program.hasError()).toBe(true);
  });
});

describe("d2RequireAllScopes_AcceptsKnownScope", () => {
  it("produces no unknown-scope diagnostic for 'self.write'", async () => {
    await runner.diagnose(`
      @d2RequireAllScopes("self.write")
      op goodOp(): void;
    `);
    expect(getDiagCodes(runner)).not.toContain(
      "@d2/typespec-decorators/unknown-scope",
    );
  });
});

// --- @d2Audience ---

describe("d2Audience_RejectsUnknownAudience", () => {
  it("emits unknown-audience for 'Nope'", async () => {
    await runner.diagnose(`
      @d2Audience("Nope")
      op badOp(): void;
    `);
    expect(getDiagCodes(runner)).toContain(
      "@d2/typespec-decorators/unknown-audience",
    );
    expect(runner.program.hasError()).toBe(true);
  });
});

describe("d2Audience_AcceptsKnownAudience", () => {
  it("produces no unknown-audience diagnostic for 'Files'", async () => {
    await runner.diagnose(`
      @d2Audience("Files")
      op goodOp(): void;
    `);
    expect(getDiagCodes(runner)).not.toContain(
      "@d2/typespec-decorators/unknown-audience",
    );
  });
});

describe("d2Audience_AcceptsD2EdgeSpecialCase", () => {
  it("produces no unknown-audience diagnostic for 'd2-edge'", async () => {
    await runner.diagnose(`
      @d2Audience("d2-edge")
      op goodOp(): void;
    `);
    expect(getDiagCodes(runner)).not.toContain(
      "@d2/typespec-decorators/unknown-audience",
    );
  });
});

// --- @d2ServedBy ---

describe("d2ServedBy_RejectsEmptyOwner", () => {
  it("emits empty-served-by for empty string", async () => {
    await runner.diagnose(`
      @d2ServedBy("")
      op badOp(): void;
    `);
    expect(getDiagCodes(runner)).toContain(
      "@d2/typespec-decorators/empty-served-by",
    );
    expect(runner.program.hasError()).toBe(true);
  });

  it("emits empty-served-by for whitespace-only string", async () => {
    await runner.diagnose(`
      @d2ServedBy("   ")
      op badOp(): void;
    `);
    expect(getDiagCodes(runner)).toContain(
      "@d2/typespec-decorators/empty-served-by",
    );
    expect(runner.program.hasError()).toBe(true);
  });
});

describe("d2ServedBy_AcceptsNonEmpty", () => {
  it("produces no empty-served-by diagnostic for non-empty owner", async () => {
    await runner.diagnose(`
      @d2ServedBy("EdgeModule")
      op goodOp(): void;
    `);
    expect(getDiagCodes(runner)).not.toContain(
      "@d2/typespec-decorators/empty-served-by",
    );
  });
});

// --- @d2Resilience round-trip (compile + assert diagnostics + hasError) ---

describe("d2Resilience_RejectsMalformed", () => {
  it("emits resilience-malformed and hasError() for an unclosed '('", async () => {
    await runner.diagnose(`
      @d2Resilience("retry(")
      op badOp(): void;
    `);
    expect(getDiagCodes(runner)).toContain(
      "@d2/typespec-decorators/resilience-malformed",
    );
    expect(runner.program.hasError()).toBe(true);
  });

  it("emits resilience-malformed for empty string", async () => {
    await runner.diagnose(`
      @d2Resilience("")
      op badOp(): void;
    `);
    expect(getDiagCodes(runner)).toContain(
      "@d2/typespec-decorators/resilience-malformed",
    );
    expect(runner.program.hasError()).toBe(true);
  });

  it("emits resilience-malformed and hasError() for bare policy name 'retry(circuitBreaker)' — missing parens must fail loud", async () => {
    // A bare policy name used as a positional arg is never a valid literal —
    // an invalid config must fail the build, not merely surface a diagnostic code.
    await runner.diagnose(`
      @d2Resilience("retry(circuitBreaker)")
      op badOp(): void;
    `);
    expect(getDiagCodes(runner)).toContain(
      "@d2/typespec-decorators/resilience-malformed",
    );
    expect(runner.program.hasError()).toBe(true);
  });
});

describe("d2Resilience_RejectsUnknownPolicy", () => {
  it("emits resilience-unknown-policy for 'breaker()'", async () => {
    await runner.diagnose(`
      @d2Resilience("breaker()")
      op badOp(): void;
    `);
    expect(getDiagCodes(runner)).toContain(
      "@d2/typespec-decorators/resilience-unknown-policy",
    );
    expect(runner.program.hasError()).toBe(true);
  });
});

describe("d2Resilience_RejectsUnknownArg", () => {
  it("emits resilience-unknown-arg for retry(foo: 3)", async () => {
    await runner.diagnose(`
      @d2Resilience("retry(foo: 3)")
      op badOp(): void;
    `);
    expect(getDiagCodes(runner)).toContain(
      "@d2/typespec-decorators/resilience-unknown-arg",
    );
    expect(runner.program.hasError()).toBe(true);
  });

  it("emits resilience-unknown-arg for singleflight(3)", async () => {
    await runner.diagnose(`
      @d2Resilience("singleflight(3)")
      op badOp(): void;
    `);
    expect(getDiagCodes(runner)).toContain(
      "@d2/typespec-decorators/resilience-unknown-arg",
    );
    expect(runner.program.hasError()).toBe(true);
  });
});

describe("d2Resilience_RejectsBadArg", () => {
  it("emits resilience-bad-arg for retry(maxAttempts: 0) — out of range", async () => {
    await runner.diagnose(`
      @d2Resilience("retry(maxAttempts: 0)")
      op badOp(): void;
    `);
    expect(getDiagCodes(runner)).toContain(
      "@d2/typespec-decorators/resilience-bad-arg",
    );
    expect(runner.program.hasError()).toBe(true);
  });

  it("emits resilience-bad-arg for retry(jitter: 5) — bool expected", async () => {
    await runner.diagnose(`
      @d2Resilience("retry(jitter: 5)")
      op badOp(): void;
    `);
    expect(getDiagCodes(runner)).toContain(
      "@d2/typespec-decorators/resilience-bad-arg",
    );
    expect(runner.program.hasError()).toBe(true);
  });
});

describe("d2Resilience_RejectsMultipleInner", () => {
  it("emits resilience-multiple-inner for retry(circuitBreaker(), singleflight())", async () => {
    await runner.diagnose(`
      @d2Resilience("retry(circuitBreaker(), singleflight())")
      op badOp(): void;
    `);
    expect(getDiagCodes(runner)).toContain(
      "@d2/typespec-decorators/resilience-multiple-inner",
    );
    expect(runner.program.hasError()).toBe(true);
  });
});

describe("d2Resilience_RejectsPositionalAfterNamed", () => {
  it("emits resilience-positional-after-named for retry(maxAttempts: 3, 1000)", async () => {
    await runner.diagnose(`
      @d2Resilience("retry(maxAttempts: 3, 1000)")
      op badOp(): void;
    `);
    expect(getDiagCodes(runner)).toContain(
      "@d2/typespec-decorators/resilience-positional-after-named",
    );
    expect(runner.program.hasError()).toBe(true);
  });
});

describe("d2Resilience_AcceptsValidExpressions", () => {
  it("produces no resilience-* diagnostics for bare 'retry()'", async () => {
    await runner.diagnose(`
      @d2Resilience("retry()")
      op goodOp(): void;
    `);
    const codes = getDiagCodes(runner);
    const resilienceCodes = codes.filter((c) => c.includes("resilience-"));
    expect(resilienceCodes).toHaveLength(0);
  });

  it("produces no resilience-* diagnostics for 'retry(circuitBreaker(singleflight()))'", async () => {
    await runner.diagnose(`
      @d2Resilience("retry(circuitBreaker(singleflight()))")
      op goodOp(): void;
    `);
    const codes = getDiagCodes(runner);
    const resilienceCodes = codes.filter((c) => c.includes("resilience-"));
    expect(resilienceCodes).toHaveLength(0);
  });

  it("produces no resilience-* diagnostics for 'retry(3, circuitBreaker(threshold: 5))'", async () => {
    await runner.diagnose(`
      @d2Resilience("retry(3, circuitBreaker(threshold: 5))")
      op goodOp(): void;
    `);
    const codes = getDiagCodes(runner);
    const resilienceCodes = codes.filter((c) => c.includes("resilience-"));
    expect(resilienceCodes).toHaveLength(0);
  });
});

// --- @d2Redact on Operation — compiler-enforced target check ---

describe("redact_OnOperationIsCompilerRejected", () => {
  it("program has errors when @d2Redact is applied to an op (wrong target type)", async () => {
    await runner.diagnose(`
      @d2Redact
      op badOp(): void;
    `);
    // The TypeSpec compiler enforces the extern dec target: ModelProperty constraint.
    // We assert program.hasError() (not a specific code) since the compiler's internal
    // diagnostic code for target-type mismatch is not part of the public API surface.
    expect(runner.program.hasError()).toBe(true);
  });
});

// ===========================================================================
// $onValidate CROSS-DECORATOR TESTS
// These use a SEPARATE createTestHost that includes HttpTestLibrary so @route works.
// The @route check needs a separate http test host — reusing the shared runner
// would not have HttpTestLibrary available, so @route would not be recognized.
// ===========================================================================

let httpRunner: BasicTestRunner;

beforeAll(async () => {
  const httpHost = await createTestHost({
    libraries: [D2DecoratorTestLibrary, HttpTestLibrary],
  });
  httpRunner = createTestWrapper(httpHost, {
    autoImports: ["@d2/typespec-decorators", "@typespec/http"],
    autoUsings: ["D2", "TypeSpec.Http"],
  });
});

function getHttpDiagCodes(): string[] {
  return httpRunner.program.diagnostics.map((d) => d.code);
}

describe("onValidate_RateTierOnInternalOpReportsDiagnostic", () => {
  it("emits rate-tier-requires-route and hasError() when @d2RateLimitTier is on an op with no @route", async () => {
    await httpRunner.diagnose(`
      @d2RateLimitTier("Standard")
      op internalOp(): void;
    `);
    expect(getHttpDiagCodes()).toContain(
      "@d2/typespec-decorators/rate-tier-requires-route",
    );
    expect(httpRunner.program.hasError()).toBe(true);
  });
});

describe("onValidate_RateTierOnRoutedOpPasses", () => {
  it("produces no rate-tier-requires-route diagnostic when @d2RateLimitTier is on a routed op", async () => {
    await httpRunner.diagnose(`
      @d2RateLimitTier("Standard")
      @get @route("/items")
      op listItems(): void;
    `);
    expect(getHttpDiagCodes()).not.toContain(
      "@d2/typespec-decorators/rate-tier-requires-route",
    );
  });
});

describe("onValidate_HarmlessPlusAnyScopeReportsConflict", () => {
  it("emits harmless-scope-conflict and hasError() for @d2Harmless + @d2RequireAnyScope", async () => {
    await httpRunner.diagnose(`
      @d2Harmless
      @d2RequireAnyScope("self.read")
      op conflictOp(): void;
    `);
    expect(getHttpDiagCodes()).toContain(
      "@d2/typespec-decorators/harmless-scope-conflict",
    );
    expect(httpRunner.program.hasError()).toBe(true);
  });
});

describe("onValidate_HarmlessPlusAllScopesReportsConflict", () => {
  it("emits harmless-scope-conflict and hasError() for @d2Harmless + @d2RequireAllScopes", async () => {
    await httpRunner.diagnose(`
      @d2Harmless
      @d2RequireAllScopes("self.read")
      op conflictOp(): void;
    `);
    expect(getHttpDiagCodes()).toContain(
      "@d2/typespec-decorators/harmless-scope-conflict",
    );
    expect(httpRunner.program.hasError()).toBe(true);
  });
});

describe("onValidate_HarmlessAlonePasses", () => {
  it("produces no harmless-scope-conflict diagnostic for @d2Harmless without scope decorators", async () => {
    await httpRunner.diagnose(`
      @d2Harmless
      op healthCheck(): void;
    `);
    expect(getHttpDiagCodes()).not.toContain(
      "@d2/typespec-decorators/harmless-scope-conflict",
    );
  });
});
