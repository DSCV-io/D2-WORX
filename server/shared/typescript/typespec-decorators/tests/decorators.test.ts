// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Tests for @d2/typespec-decorators.
//
// Direct unit tests call each $fn with a lightweight mock DecoratorContext,
// giving V8 source-level coverage. Integration tests use the TypeSpec test
// host to verify the full compile + stateMap round-trip.
// Adversarial rejection tests (bad tier, empty scope, redact-on-op, etc.)
// require the validation layer.

import {
  createTestLibrary,
  createTestWrapper,
  findTestPackageRoot,
} from "@typespec/compiler/testing";
import { describe, it, expect, beforeAll } from "vitest";
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
} from "../src/index.js";
import type { GrpcMethodPayload, IdempotentPayload } from "../src/index.js";
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
    @d2RequireAnyScope("orders:read", "orders:write")
    op listOrders(): void;
  `);
  const values = [
    ...runner.program.stateMap(D2_REQUIRE_ANY_SCOPE_KEY).values(),
  ] as string[][];
  expect(values).toHaveLength(1);
  expect(values[0]).toEqual(["orders:read", "orders:write"]);
});

it("d2RequireAllScopes_StoresScopesArrayUnderAllKey", async () => {
  await runner.compile(`
    @d2RequireAllScopes("admin:read", "admin:write")
    op adminAction(): void;
  `);
  const values = [
    ...runner.program.stateMap(D2_REQUIRE_ALL_SCOPES_KEY).values(),
  ] as string[][];
  expect(values).toHaveLength(1);
  expect(values[0]).toEqual(["admin:read", "admin:write"]);
});

it("d2RateLimitTier_StoresTierStringUnderTierKey", async () => {
  await runner.compile(`
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
// Gate test: all 12 decorators co-apply and round-trip independently
// @d2Harmless and the scope decorators are mutually exclusive (enforced by the
// validation layer); the co-apply test does not model that illegal combination.
// ---------------------------------------------------------------------------

it("allTwelveDecorators_CoApplyAndRoundTripIndependently", async () => {
  await runner.compile(`
    model RequestBody {
      @d2Redact sensitiveField: string;
    }

    @d2RequireAnyScope("orders:write", "orders:admin")
    @d2RateLimitTier("Elevated")
    @d2Audience("d2-edge")
    @d2ServedBy("OrderService")
    @d2GrpcMethod("Orders", "CreateOrder")
    @d2ServerPush("session")
    @d2Idempotent("derived", 300, "orgId", "requestId")
    @d2Resilience("retry(circuitBreaker(singleflight()))")
    @d2Csrf("required")
    op createOrder(body: RequestBody): void;

    @d2RequireAllScopes("admin:read", "admin:write")
    op adminOp(): void;

    @d2Harmless
    op healthCheck(): void;
  `);

  const program = runner.program;

  const anyValues = [
    ...program.stateMap(D2_REQUIRE_ANY_SCOPE_KEY).values(),
  ] as string[][];
  expect(
    anyValues.some(
      (v) => v.includes("orders:write") && v.includes("orders:admin"),
    ),
  ).toBe(true);

  const allValues = [
    ...program.stateMap(D2_REQUIRE_ALL_SCOPES_KEY).values(),
  ] as string[][];
  expect(
    allValues.some(
      (v) => v.includes("admin:read") && v.includes("admin:write"),
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
  expect(grpcEntries).toHaveLength(1);
  expect(grpcEntries[0]).toEqual({
    service: "Orders",
    method: "CreateOrder",
    streaming: "unary",
  });

  const redactValues = [...program.stateMap(D2_REDACT_KEY).values()];
  expect(redactValues).toContain(true);

  const serverPushValues = [...program.stateMap(D2_SERVER_PUSH_KEY).values()];
  expect(serverPushValues).toContain("session");

  const idempotentEntries = [
    ...program.stateMap(D2_IDEMPOTENT_KEY).values(),
  ] as IdempotentPayload[];
  expect(idempotentEntries).toHaveLength(1);
  expect(idempotentEntries[0]).toEqual({
    keySource: "derived",
    ttlSeconds: 300,
    fields: ["orgId", "requestId"],
  });

  const resilienceValues = [...program.stateMap(D2_RESILIENCE_KEY).values()];
  expect(resilienceValues).toContain("retry(circuitBreaker(singleflight()))");

  const csrfValues = [...program.stateMap(D2_CSRF_KEY).values()];
  expect(csrfValues).toContain("required");

  const harmlessValues = [...program.stateMap(D2_HARMLESS_KEY).values()];
  expect(harmlessValues).toContain(true);
});
