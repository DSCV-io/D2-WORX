// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { describe, expect, it, vi } from "vitest";
import {
  Metadata,
  status as GrpcStatus,
  type InterceptorOptions,
  type StatusObject,
} from "@grpc/grpc-js";
import { ok } from "@d2/result";
import { HttpHeaders } from "@d2/headers-http";
import { InternalTokenCache } from "../../src/internal-token-cache.js";
import { createInternalTokenInterceptor } from "../../src/interceptors/internal-token.js";
import type { InternalTokenClient } from "../../src/internal-token-client.js";
import type { InternalTokenSnapshot } from "../../src/types.js";

interface CapturedCall {
  metadata: Metadata;
  status?: StatusObject;
}

function makeNextCall(captured: CapturedCall, simulatedStatus?: StatusObject) {
  return () => ({
    cancelWithStatus() {},
    getPeer: () => "fake-peer",
    start(
      metadata: Metadata,
      listener?: { onReceiveStatus(s: StatusObject): void },
    ) {
      captured.metadata = metadata;
      // simulate the wire response
      if (simulatedStatus !== undefined) {
        captured.status = simulatedStatus;
        listener?.onReceiveStatus(simulatedStatus);
      }
    },
    sendMessageWithContext() {},
    sendMessage() {},
    startRead() {},
    halfClose() {},
    getAuthContext: () => null,
  });
}

const FAKE_OPTIONS: InterceptorOptions = {
  method_definition: {
    path: "/d2.v1.Edge/Hello",
    requestStream: false,
    responseStream: false,
    requestSerialize: () => Buffer.alloc(0),
    responseDeserialize: () => null,
    originalName: "hello",
  },
} as unknown as InterceptorOptions;

function snapshot(token = "fake.jwt.signature"): InternalTokenSnapshot {
  return {
    accessToken: token,
    expiresAtMs: Date.now() + 60_000,
    audience: "d2.edge",
  };
}

// Helper: a far-future token that is never in the refresh-ahead window
// (refreshLeadMs defaults to 60_000ms; we give it 2 hours of headroom).
function freshSnapshot(token = "cached.jwt"): InternalTokenSnapshot {
  return {
    accessToken: token,
    expiresAtMs: Date.now() + 7_200_000, // 2 hours
    audience: "d2.edge",
  };
}

// ---------------------------------------------------------------------------
// Happy path — basic attach + cache hit
// ---------------------------------------------------------------------------

describe("createInternalTokenInterceptor — happy path", () => {
  it("attaches Bearer token to outbound metadata", async () => {
    const cache = new InternalTokenCache();
    const tokenClient: InternalTokenClient = {
      acquireToken: vi.fn(async () => ok(snapshot())),
    };
    const interceptor = createInternalTokenInterceptor({ cache, tokenClient });
    const captured: CapturedCall = { metadata: new Metadata() };
    const call = interceptor(FAKE_OPTIONS, makeNextCall(captured) as never);
    call.start(new Metadata(), {
      onReceiveMetadata: () => {},
      onReceiveMessage: () => {},
      onReceiveStatus: () => {},
    });
    // Wait one microtask for the lazy token attach.
    await new Promise((r) => setImmediate(r));
    expect(captured.metadata.get(HttpHeaders.AUTHORIZATION)[0]).toBe(
      "Bearer fake.jwt.signature",
    );
  });

  it("uses cached token on subsequent calls (single fetch)", async () => {
    const cache = new InternalTokenCache();
    cache.set(freshSnapshot("cached.jwt"));
    const acquireSpy = vi.fn(async () => ok(snapshot("fresh.jwt")));
    const interceptor = createInternalTokenInterceptor({
      cache,
      tokenClient: { acquireToken: acquireSpy },
    });
    const captured: CapturedCall = { metadata: new Metadata() };
    const call = interceptor(FAKE_OPTIONS, makeNextCall(captured) as never);
    call.start(new Metadata(), {
      onReceiveMetadata: () => {},
      onReceiveMessage: () => {},
      onReceiveStatus: () => {},
    });
    await new Promise((r) => setImmediate(r));
    expect(acquireSpy).not.toHaveBeenCalled();
    expect(captured.metadata.get(HttpHeaders.AUTHORIZATION)[0]).toBe(
      "Bearer cached.jwt",
    );
  });

  it("clears cache on UNAUTHENTICATED response (next call refreshes)", async () => {
    const cache = new InternalTokenCache();
    cache.set(freshSnapshot());
    const tokenClient: InternalTokenClient = {
      acquireToken: vi.fn(async () => ok(snapshot())),
    };
    const interceptor = createInternalTokenInterceptor({ cache, tokenClient });
    const captured: CapturedCall = { metadata: new Metadata() };
    const call = interceptor(
      FAKE_OPTIONS,
      makeNextCall(captured, {
        code: GrpcStatus.UNAUTHENTICATED,
        details: "expired",
        metadata: new Metadata(),
      }) as never,
    );
    let receivedStatus: StatusObject | undefined;
    call.start(new Metadata(), {
      onReceiveMetadata: () => {},
      onReceiveMessage: () => {},
      onReceiveStatus: (s) => {
        receivedStatus = s;
      },
    });
    await new Promise((r) => setImmediate(r));
    expect(receivedStatus?.code).toBe(GrpcStatus.UNAUTHENTICATED);
    expect(cache.tryGet().snapshot).toBeUndefined();
  });

  it("non-UNAUTHENTICATED status passes through without clearing cache", async () => {
    const cache = new InternalTokenCache();
    cache.set(freshSnapshot());
    const tokenClient: InternalTokenClient = {
      acquireToken: vi.fn(async () => ok(snapshot())),
    };
    const interceptor = createInternalTokenInterceptor({ cache, tokenClient });
    const captured: CapturedCall = { metadata: new Metadata() };
    const call = interceptor(
      FAKE_OPTIONS,
      makeNextCall(captured, {
        code: GrpcStatus.OK,
        details: "ok",
        metadata: new Metadata(),
      }) as never,
    );
    call.start(new Metadata(), {
      onReceiveMetadata: () => {},
      onReceiveMessage: () => {},
      onReceiveStatus: () => {},
    });
    await new Promise((r) => setImmediate(r));
    expect(cache.tryGet().snapshot).not.toBeUndefined();
  });
});

// ---------------------------------------------------------------------------
// Token client unreachable
// ---------------------------------------------------------------------------

describe("createInternalTokenInterceptor — token client unreachable", () => {
  it("sends call with no Authorization metadata when token is null", async () => {
    const cache = new InternalTokenCache();
    const tokenClient: InternalTokenClient = {
      acquireToken: vi.fn(
        async () =>
          ({
            success: false,
            statusCode: 503,
            errorCode: "AUTH_JWKS_UNAVAILABLE",
          }) as unknown as ReturnType<
            InternalTokenClient["acquireToken"]
          > extends Promise<infer T>
            ? T
            : never,
      ),
    };
    const interceptor = createInternalTokenInterceptor({ cache, tokenClient });
    const captured: CapturedCall = { metadata: new Metadata() };
    const call = interceptor(FAKE_OPTIONS, makeNextCall(captured) as never);
    call.start(new Metadata(), {
      onReceiveMetadata: () => {},
      onReceiveMessage: () => {},
      onReceiveStatus: () => {},
    });
    await new Promise((r) => setImmediate(r));
    expect(captured.metadata.get(HttpHeaders.AUTHORIZATION)).toEqual([]);
  });

  it("sends call with no Authorization metadata when token client throws", async () => {
    const cache = new InternalTokenCache();
    const tokenClient: InternalTokenClient = {
      acquireToken: vi.fn(async () => {
        throw new Error("network down");
      }),
    };
    const interceptor = createInternalTokenInterceptor({ cache, tokenClient });
    const captured: CapturedCall = { metadata: new Metadata() };
    const call = interceptor(FAKE_OPTIONS, makeNextCall(captured) as never);
    call.start(new Metadata(), {
      onReceiveMetadata: () => {},
      onReceiveMessage: () => {},
      onReceiveStatus: () => {},
    });
    await new Promise((r) => setImmediate(r));
    expect(captured.metadata.get(HttpHeaders.AUTHORIZATION)).toEqual([]);
  });
});

// ---------------------------------------------------------------------------
// Listener pass-through
// ---------------------------------------------------------------------------

describe("createInternalTokenInterceptor — listener pass-through", () => {
  it("invokes onReceiveMetadata", async () => {
    const cache = new InternalTokenCache();
    cache.set(freshSnapshot());
    const interceptor = createInternalTokenInterceptor({
      cache,
      tokenClient: { acquireToken: vi.fn(async () => ok(snapshot())) },
    });
    const captured: CapturedCall = { metadata: new Metadata() };
    const onReceiveMetadata = vi.fn();
    const onReceiveMessage = vi.fn();
    const call = interceptor(FAKE_OPTIONS, (() => ({
      cancelWithStatus() {},
      getPeer: () => "x",
      start(
        _m: Metadata,
        listener?: {
          onReceiveMetadata(m: Metadata): void;
          onReceiveMessage(msg: unknown): void;
          onReceiveStatus(s: StatusObject): void;
        },
      ) {
        listener?.onReceiveMetadata(new Metadata());
        listener?.onReceiveMessage({ hello: "world" });
      },
      sendMessageWithContext() {},
      sendMessage() {},
      startRead() {},
      halfClose() {},
      getAuthContext: () => null,
    })) as never);
    call.start(new Metadata(), {
      onReceiveMetadata,
      onReceiveMessage,
      onReceiveStatus: () => {},
    });
    await new Promise((r) => setImmediate(r));
    expect(onReceiveMetadata).toHaveBeenCalled();
    expect(onReceiveMessage).toHaveBeenCalled();
    void captured;
  });
});

// ---------------------------------------------------------------------------
// Singleflight stress
// ---------------------------------------------------------------------------

describe("createInternalTokenInterceptor — Singleflight stress", () => {
  it("cache eventually populates after first token client acquire", async () => {
    const cache = new InternalTokenCache();
    let count = 0;
    const acquire = vi.fn(async () => {
      count++;
      return ok(snapshot(`token-${count}`));
    });
    const tokenClient: InternalTokenClient = { acquireToken: acquire };
    const interceptor = createInternalTokenInterceptor({ cache, tokenClient });

    const captured: CapturedCall = { metadata: new Metadata() };
    const call = interceptor(FAKE_OPTIONS, makeNextCall(captured) as never);
    call.start(new Metadata(), {
      onReceiveMetadata: () => {},
      onReceiveMessage: () => {},
      onReceiveStatus: () => {},
    });
    // Drain microtasks so the lazy token-client call completes.
    await new Promise((r) => setTimeout(r, 10));
    expect(cache.tryGet().snapshot).not.toBeUndefined();
    expect(count).toBeGreaterThanOrEqual(1);
  });
});

// ---------------------------------------------------------------------------
// Cleanup #2: refresh-ahead integration tests
// ---------------------------------------------------------------------------

describe("createInternalTokenInterceptor — refresh-ahead", () => {
  /**
   * Creates a fake InternalTokenCache that can be configured via `now` +
   * `skewMs` / `refreshLeadMs` — we exercise the interceptor's refresh-ahead
   * branch by constructing a cache whose token is explicitly in the aging window.
   */
  function agingCache(opts: {
    token: string;
    now: number;
    expiresAtMs: number;
    refreshLeadMs?: number;
    skewMs?: number;
  }): InternalTokenCache {
    const cache = new InternalTokenCache({
      clock: () => opts.now,
      skewMs: opts.skewMs ?? 5_000,
      refreshLeadMs: opts.refreshLeadMs ?? 60_000,
    });
    cache.set({
      accessToken: opts.token,
      expiresAtMs: opts.expiresAtMs,
      audience: "d2.edge",
    });
    return cache;
  }

  it("(a) fresh token → served, NO background refresh fired", async () => {
    // Token expires in 2 hours — well outside any refresh-ahead window.
    const now = 1_000_000;
    const cache = new InternalTokenCache({
      clock: () => now,
      skewMs: 5_000,
      refreshLeadMs: 60_000,
    });
    cache.set({
      accessToken: "fresh-tok",
      expiresAtMs: now + 7_200_000,
      audience: "d2.edge",
    });

    const acquireSpy = vi.fn(async () => ok(snapshot("should-not-be-called")));
    const interceptor = createInternalTokenInterceptor({
      cache,
      tokenClient: { acquireToken: acquireSpy },
    });
    const captured: CapturedCall = { metadata: new Metadata() };
    const call = interceptor(FAKE_OPTIONS, makeNextCall(captured) as never);
    call.start(new Metadata(), {
      onReceiveMetadata: () => {},
      onReceiveMessage: () => {},
      onReceiveStatus: () => {},
    });
    // Drain microtasks and a short async gap to ensure any bg refresh would complete.
    await new Promise((r) => setTimeout(r, 20));
    expect(captured.metadata.get(HttpHeaders.AUTHORIZATION)[0]).toBe(
      "Bearer fresh-tok",
    );
    expect(acquireSpy).not.toHaveBeenCalled();
  });

  it("(b) aging token → served immediately AND exactly ONE background refresh fired", async () => {
    const now = 150_000;
    // expiresAtMs = 200_000, refreshLeadMs = 60_000, skewMs = 5_000
    // lead boundary = 140_000 → now(150_000) is inside the aging window
    const cache = agingCache({
      token: "aging-tok",
      now,
      expiresAtMs: 200_000,
      refreshLeadMs: 60_000,
      skewMs: 5_000,
    });

    const acquireSpy = vi.fn(async () => ok(snapshot("refreshed-tok")));
    const interceptor = createInternalTokenInterceptor({
      cache,
      tokenClient: { acquireToken: acquireSpy },
    });
    const captured: CapturedCall = { metadata: new Metadata() };
    const call = interceptor(FAKE_OPTIONS, makeNextCall(captured) as never);
    call.start(new Metadata(), {
      onReceiveMetadata: () => {},
      onReceiveMessage: () => {},
      onReceiveStatus: () => {},
    });
    // After first microtask the in-flight request gets the aging token.
    await new Promise((r) => setImmediate(r));
    expect(captured.metadata.get(HttpHeaders.AUTHORIZATION)[0]).toBe(
      "Bearer aging-tok",
    );
    // Allow the background mint to complete.
    await new Promise((r) => setTimeout(r, 20));
    // Exactly ONE background mint fired (not one per concurrent call later).
    expect(acquireSpy).toHaveBeenCalledTimes(1);
  });

  it("(c) background-refresh failure → swallowed, current token still served, later call still works", async () => {
    const now = 150_000;
    const cache = agingCache({
      token: "aging-tok",
      now,
      expiresAtMs: 200_000,
      refreshLeadMs: 60_000,
      skewMs: 5_000,
    });

    const warnMessages: string[] = [];
    const fakeLogger = {
      trace: () => {},
      debug: () => {},
      info: () => {},
      warn: (msg: string) => warnMessages.push(msg),
      error: () => {},
      fatal: () => {},
      child: () => fakeLogger,
    };

    // Background refresh will fail.
    const acquireSpy = vi.fn(async () => {
      throw new Error("upstream down");
    });
    const interceptor = createInternalTokenInterceptor({
      cache,
      tokenClient: { acquireToken: acquireSpy },
      logger: fakeLogger,
    });
    const captured: CapturedCall = { metadata: new Metadata() };
    const call = interceptor(FAKE_OPTIONS, makeNextCall(captured) as never);
    call.start(new Metadata(), {
      onReceiveMetadata: () => {},
      onReceiveMessage: () => {},
      onReceiveStatus: () => {},
    });
    await new Promise((r) => setImmediate(r));
    // In-flight call got the aging (still-valid) token.
    expect(captured.metadata.get(HttpHeaders.AUTHORIZATION)[0]).toBe(
      "Bearer aging-tok",
    );
    // Allow background mint + error to propagate.
    await new Promise((r) => setTimeout(r, 20));
    // Error was swallowed — no unhandled rejection, logger captured a warn.
    expect(warnMessages.some((m) => m.includes("refresh-ahead failed"))).toBe(
      true,
    );
    // Cache still has the aging token (background mint failed → no cache.set).
    expect(cache.tryGet().snapshot?.accessToken).toBe("aging-tok");
  });

  it("(d) hard-expired token → synchronous mint, assert no double-mint", async () => {
    // Construct cache with expired token.
    const now = 200_000;
    const cache = new InternalTokenCache({
      clock: () => now,
      skewMs: 5_000,
      refreshLeadMs: 60_000,
    });
    // Token that is already past hard-expiry at `now`.
    cache.set({
      accessToken: "expired-tok",
      expiresAtMs: 100_000,
      audience: "d2.edge",
    });

    const acquireSpy = vi.fn(async () => ok(snapshot("fresh-tok")));
    const interceptor = createInternalTokenInterceptor({
      cache,
      tokenClient: { acquireToken: acquireSpy },
    });
    const captured: CapturedCall = { metadata: new Metadata() };
    const call = interceptor(FAKE_OPTIONS, makeNextCall(captured) as never);
    call.start(new Metadata(), {
      onReceiveMetadata: () => {},
      onReceiveMessage: () => {},
      onReceiveStatus: () => {},
    });
    await new Promise((r) => setTimeout(r, 20));
    // Exactly ONE mint (synchronous path — no double-mint from a stale ahead-refresh).
    expect(acquireSpy).toHaveBeenCalledTimes(1);
    expect(captured.metadata.get(HttpHeaders.AUTHORIZATION)[0]).toBe(
      "Bearer fresh-tok",
    );
  });

  it("(e) N concurrent reads in the aging window → all served immediately, singleflight dedup lives in the token client", async () => {
    // At the interceptor level, N concurrent aging-window reads each serve the
    // current token immediately AND each fire a background refresh call through
    // tokenClient.acquireToken(). Deduplication (collapsing N into 1 upstream
    // call) is the responsibility of HttpInternalTokenClient's Singleflight layer
    // — NOT the interceptor itself.
    //
    // What we can assert at the interceptor level:
    //   1. All N in-flight requests immediately get the aging token (no block).
    //   2. After background refreshes settle, the cache holds the new token.
    //
    // The Singleflight contract is tested by the HttpInternalTokenClient tests
    // (100-concurrent-calls → 1-fetch).
    const now = 150_000;
    const cache = agingCache({
      token: "aging-tok",
      now,
      expiresAtMs: 200_000,
      refreshLeadMs: 60_000,
      skewMs: 5_000,
    });

    const acquireSpy = vi.fn(async () => ok(snapshot("new-tok")));
    const tokenClient: InternalTokenClient = { acquireToken: acquireSpy };
    const interceptor = createInternalTokenInterceptor({ cache, tokenClient });

    // Fire 5 concurrent interceptor calls.
    const calls = Array.from({ length: 5 }, () => {
      const captured: CapturedCall = { metadata: new Metadata() };
      const call = interceptor(FAKE_OPTIONS, makeNextCall(captured) as never);
      call.start(new Metadata(), {
        onReceiveMetadata: () => {},
        onReceiveMessage: () => {},
        onReceiveStatus: () => {},
      });
      return captured;
    });

    // All 5 calls drain and serve the aging token without blocking.
    await new Promise((r) => setImmediate(r));
    for (const c of calls) {
      expect(c.metadata.get(HttpHeaders.AUTHORIZATION)[0]).toBe(
        "Bearer aging-tok",
      );
    }

    // Allow all background mints to settle.
    await new Promise((r) => setTimeout(r, 50));
    // Exactly 5 acquireToken() calls: this mock has no Singleflight, so each of
    // the 5 interceptor calls fires its own background mintAndCache() independently.
    // Production HttpInternalTokenClient collapses N concurrent callers to ONE
    // upstream fetch via its built-in Singleflight layer (proven by the
    // 100-concurrent-calls test in internal-token-client.test.ts).
    expect(acquireSpy.mock.calls.length).toBe(5);
    // After refresh, cache holds the new token.
    expect(cache.tryGet().snapshot?.accessToken).toBe("new-tok");
  });

  it("(g) aging token + background acquireToken resolves !success → aging token still served, cache unchanged, warn logged, no unhandled rejection", async () => {
    // Test (c) covers the throw path; this covers the resolve-!success path.
    // mintAndCache() returns undefined when result.success is false, and the
    // fire-and-forget does NOT call cache.set() — so the aging token remains.
    const now = 150_000;
    const cache = agingCache({
      token: "aging-tok",
      now,
      expiresAtMs: 200_000,
      refreshLeadMs: 60_000,
      skewMs: 5_000,
    });

    const warnMessages: Array<{ msg: string; data?: unknown }> = [];
    const fakeLogger = {
      trace: () => {},
      debug: () => {},
      info: () => {},
      warn: (msg: string, data?: unknown) => warnMessages.push({ msg, data }),
      error: () => {},
      fatal: () => {},
      child: () => fakeLogger,
    };

    // Background refresh resolves with !success (NOT throws).
    const acquireSpy = vi.fn(async () => ({
      success: false as const,
      statusCode: 503,
      errorCode: "AUTH_JWKS_UNAVAILABLE",
    }));
    const interceptor = createInternalTokenInterceptor({
      cache,
      tokenClient: {
        acquireToken:
          acquireSpy as unknown as InternalTokenClient["acquireToken"],
      },
      logger: fakeLogger,
    });
    const captured: CapturedCall = { metadata: new Metadata() };
    const call = interceptor(FAKE_OPTIONS, makeNextCall(captured) as never);

    // Track unhandled rejections — there must be none.
    const unhandledRejections: unknown[] = [];
    const onUnhandled = (reason: unknown) => unhandledRejections.push(reason);
    process.on("unhandledRejection", onUnhandled);

    call.start(new Metadata(), {
      onReceiveMetadata: () => {},
      onReceiveMessage: () => {},
      onReceiveStatus: () => {},
    });

    // In-flight request gets the aging (still-valid) token immediately.
    await new Promise((r) => setImmediate(r));
    expect(captured.metadata.get(HttpHeaders.AUTHORIZATION)[0]).toBe(
      "Bearer aging-tok",
    );

    // Allow background mint to complete.
    await new Promise((r) => setTimeout(r, 30));

    process.off("unhandledRejection", onUnhandled);

    // No unhandled rejections.
    expect(unhandledRejections).toHaveLength(0);
    // Logger warned with "token acquire failed" + errorCode (no token bytes).
    const warnEntry = warnMessages.find((w) =>
      w.msg.includes("token acquire failed"),
    );
    expect(warnEntry).toBeDefined();
    expect((warnEntry?.data as Record<string, unknown>)["errorCode"]).toBe(
      "AUTH_JWKS_UNAVAILABLE",
    );
    // Cache is UNCHANGED — aging token still present.
    expect(cache.tryGet().snapshot?.accessToken).toBe("aging-tok");
    // acquireToken was called exactly once (one background refresh).
    expect(acquireSpy).toHaveBeenCalledTimes(1);
  });

  it("(f) after a successful ahead-refresh, cache serves the NEW token", async () => {
    const now = 150_000;
    const cache = agingCache({
      token: "aging-tok",
      now,
      expiresAtMs: 200_000,
      refreshLeadMs: 60_000,
      skewMs: 5_000,
    });

    const newSnap: InternalTokenSnapshot = {
      accessToken: "brand-new-tok",
      expiresAtMs: 9_000_000, // far future
      audience: "d2.edge",
    };
    const acquireSpy = vi.fn(async () => ok(newSnap));
    const interceptor = createInternalTokenInterceptor({
      cache,
      tokenClient: { acquireToken: acquireSpy },
    });
    const captured: CapturedCall = { metadata: new Metadata() };
    const call = interceptor(FAKE_OPTIONS, makeNextCall(captured) as never);
    call.start(new Metadata(), {
      onReceiveMetadata: () => {},
      onReceiveMessage: () => {},
      onReceiveStatus: () => {},
    });
    await new Promise((r) => setImmediate(r));
    // In-flight got the aging token.
    expect(captured.metadata.get(HttpHeaders.AUTHORIZATION)[0]).toBe(
      "Bearer aging-tok",
    );
    // Let the background refresh complete.
    await new Promise((r) => setTimeout(r, 20));
    // Cache now holds the brand-new token.
    expect(cache.tryGet().snapshot?.accessToken).toBe("brand-new-tok");
    expect(cache.tryGet().shouldRefreshAhead).toBe(false);
  });
});
