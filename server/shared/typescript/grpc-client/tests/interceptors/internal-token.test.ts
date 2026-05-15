// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
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
import type { KeyCustodianClient } from "../../src/key-custodian-client.js";
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

describe("createInternalTokenInterceptor — happy path", () => {
  it("attaches Bearer token to outbound metadata", async () => {
    const cache = new InternalTokenCache();
    const keyCustodian: KeyCustodianClient = {
      acquireToken: vi.fn(async () => ok(snapshot())),
    };
    const interceptor = createInternalTokenInterceptor({ cache, keyCustodian });
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
    cache.set(snapshot("cached.jwt"));
    const acquireSpy = vi.fn(async () => ok(snapshot("fresh.jwt")));
    const interceptor = createInternalTokenInterceptor({
      cache,
      keyCustodian: { acquireToken: acquireSpy },
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
    cache.set(snapshot());
    const keyCustodian: KeyCustodianClient = {
      acquireToken: vi.fn(async () => ok(snapshot())),
    };
    const interceptor = createInternalTokenInterceptor({ cache, keyCustodian });
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
    expect(cache.tryGet()).toBeNull();
  });

  it("non-UNAUTHENTICATED status passes through without clearing cache", async () => {
    const cache = new InternalTokenCache();
    cache.set(snapshot());
    const keyCustodian: KeyCustodianClient = {
      acquireToken: vi.fn(async () => ok(snapshot())),
    };
    const interceptor = createInternalTokenInterceptor({ cache, keyCustodian });
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
    expect(cache.tryGet()).not.toBeNull();
  });
});

describe("createInternalTokenInterceptor — KeyCustodian unreachable", () => {
  it("sends call with no Authorization metadata when token is null", async () => {
    const cache = new InternalTokenCache();
    const keyCustodian: KeyCustodianClient = {
      acquireToken: vi.fn(
        async () =>
          ({
            success: false,
            statusCode: 503,
            errorCode: "AUTH_JWKS_UNAVAILABLE",
          }) as unknown as ReturnType<
            KeyCustodianClient["acquireToken"]
          > extends Promise<infer T>
            ? T
            : never,
      ),
    };
    const interceptor = createInternalTokenInterceptor({ cache, keyCustodian });
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

  it("sends call with no Authorization metadata when KeyCustodian throws", async () => {
    const cache = new InternalTokenCache();
    const keyCustodian: KeyCustodianClient = {
      acquireToken: vi.fn(async () => {
        throw new Error("network down");
      }),
    };
    const interceptor = createInternalTokenInterceptor({ cache, keyCustodian });
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

describe("createInternalTokenInterceptor — listener pass-through", () => {
  it("invokes onReceiveMetadata", async () => {
    const cache = new InternalTokenCache();
    cache.set(snapshot());
    const interceptor = createInternalTokenInterceptor({
      cache,
      keyCustodian: { acquireToken: vi.fn(async () => ok(snapshot())) },
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

describe("createInternalTokenInterceptor — Singleflight stress", () => {
  it("cache eventually populates after first KeyCustodian acquire", async () => {
    const cache = new InternalTokenCache();
    let count = 0;
    const acquire = vi.fn(async () => {
      count++;
      return ok(snapshot(`token-${count}`));
    });
    const keyCustodian: KeyCustodianClient = { acquireToken: acquire };
    const interceptor = createInternalTokenInterceptor({ cache, keyCustodian });

    const captured: CapturedCall = { metadata: new Metadata() };
    const call = interceptor(FAKE_OPTIONS, makeNextCall(captured) as never);
    call.start(new Metadata(), {
      onReceiveMetadata: () => {},
      onReceiveMessage: () => {},
      onReceiveStatus: () => {},
    });
    // Drain microtasks so the lazy KeyCustodian call completes.
    await new Promise((r) => setTimeout(r, 10));
    expect(cache.tryGet()).not.toBeNull();
    expect(count).toBeGreaterThanOrEqual(1);
  });
});
