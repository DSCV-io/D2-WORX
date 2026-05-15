// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import { Metadata, type InterceptorOptions } from "@grpc/grpc-js";
import { CommonHeaders } from "@d2/headers-common";
import {
  PropagatedContextSerializer,
  type IPropagatedContext,
} from "@d2/request-context-abstractions";
import { createContextPropagationInterceptor } from "../../src/interceptors/context-propagation.js";

interface CapturedCall {
  metadata: Metadata;
}

function makeNextCall(captured: CapturedCall) {
  return () => ({
    cancelWithStatus() {},
    getPeer: () => "fake-peer",
    start(metadata: Metadata) {
      captured.metadata = metadata;
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

const FULL_CTX: IPropagatedContext = {
  requestId: "req-abc",
  requestPath: "/api/v1/notify",
  sessionFingerprint: "v1.aaaa",
  currentFingerprint: "v1.bbbb",
  riskScore: 12,
  whoIsHashId: "whois-xx",
};

describe("createContextPropagationInterceptor — happy path", () => {
  it("attaches base64url-of-JSON x-d2-context metadata", () => {
    const interceptor = createContextPropagationInterceptor({
      getCurrentContext: () => FULL_CTX,
    });
    const captured: CapturedCall = { metadata: new Metadata() };
    const call = interceptor(FAKE_OPTIONS, makeNextCall(captured) as never);
    call.start(new Metadata(), {
      onReceiveMetadata: () => {},
      onReceiveMessage: () => {},
      onReceiveStatus: () => {},
    });
    const headerValue = captured.metadata.get(
      CommonHeaders.PROPAGATED_CONTEXT,
    )[0];
    expect(headerValue).toBeDefined();
    const decoded = Buffer.from(String(headerValue), "base64url").toString(
      "utf8",
    );
    const roundTrip = PropagatedContextSerializer.tryDecode(decoded);
    expect(roundTrip?.requestId).toBe(FULL_CTX.requestId);
    expect(roundTrip?.sessionFingerprint).toBe(FULL_CTX.sessionFingerprint);
    expect(roundTrip?.riskScore).toBe(FULL_CTX.riskScore);
  });

  it("forwards traceparent when provided", () => {
    const interceptor = createContextPropagationInterceptor({
      getCurrentContext: () => undefined,
      getCurrentTraceparent: () => "00-trace-span-01",
    });
    const captured: CapturedCall = { metadata: new Metadata() };
    const call = interceptor(FAKE_OPTIONS, makeNextCall(captured) as never);
    call.start(new Metadata(), {
      onReceiveMetadata: () => {},
      onReceiveMessage: () => {},
      onReceiveStatus: () => {},
    });
    expect(captured.metadata.get(CommonHeaders.TRACEPARENT)[0]).toBe(
      "00-trace-span-01",
    );
  });

  it("forwards tracestate when provided", () => {
    const interceptor = createContextPropagationInterceptor({
      getCurrentContext: () => undefined,
      getCurrentTracestate: () => "vendor=val",
    });
    const captured: CapturedCall = { metadata: new Metadata() };
    const call = interceptor(FAKE_OPTIONS, makeNextCall(captured) as never);
    call.start(new Metadata(), {
      onReceiveMetadata: () => {},
      onReceiveMessage: () => {},
      onReceiveStatus: () => {},
    });
    expect(captured.metadata.get(CommonHeaders.TRACESTATE)[0]).toBe(
      "vendor=val",
    );
  });
});

describe("createContextPropagationInterceptor — pass-through branches", () => {
  it("no x-d2-context metadata when context is undefined", () => {
    const interceptor = createContextPropagationInterceptor({
      getCurrentContext: () => undefined,
    });
    const captured: CapturedCall = { metadata: new Metadata() };
    const call = interceptor(FAKE_OPTIONS, makeNextCall(captured) as never);
    call.start(new Metadata(), {
      onReceiveMetadata: () => {},
      onReceiveMessage: () => {},
      onReceiveStatus: () => {},
    });
    expect(captured.metadata.get(CommonHeaders.PROPAGATED_CONTEXT)).toEqual([]);
  });

  it("no traceparent metadata when getCurrentTraceparent returns empty", () => {
    const interceptor = createContextPropagationInterceptor({
      getCurrentContext: () => undefined,
      getCurrentTraceparent: () => "",
    });
    const captured: CapturedCall = { metadata: new Metadata() };
    const call = interceptor(FAKE_OPTIONS, makeNextCall(captured) as never);
    call.start(new Metadata(), {
      onReceiveMetadata: () => {},
      onReceiveMessage: () => {},
      onReceiveStatus: () => {},
    });
    expect(captured.metadata.get(CommonHeaders.TRACEPARENT)).toEqual([]);
  });

  it("no traceparent when getCurrentTraceparent absent (undefined return)", () => {
    const interceptor = createContextPropagationInterceptor({
      getCurrentContext: () => undefined,
      getCurrentTraceparent: () => undefined,
    });
    const captured: CapturedCall = { metadata: new Metadata() };
    const call = interceptor(FAKE_OPTIONS, makeNextCall(captured) as never);
    call.start(new Metadata(), {
      onReceiveMetadata: () => {},
      onReceiveMessage: () => {},
      onReceiveStatus: () => {},
    });
    expect(captured.metadata.get(CommonHeaders.TRACEPARENT)).toEqual([]);
  });

  it("no tracestate when getCurrentTracestate returns empty", () => {
    const interceptor = createContextPropagationInterceptor({
      getCurrentContext: () => undefined,
      getCurrentTracestate: () => "",
    });
    const captured: CapturedCall = { metadata: new Metadata() };
    const call = interceptor(FAKE_OPTIONS, makeNextCall(captured) as never);
    call.start(new Metadata(), {
      onReceiveMetadata: () => {},
      onReceiveMessage: () => {},
      onReceiveStatus: () => {},
    });
    expect(captured.metadata.get(CommonHeaders.TRACESTATE)).toEqual([]);
  });
});

describe("createContextPropagationInterceptor — diagnostic logging", () => {
  it("logs metadata SHAPE not VALUES (PII redaction)", () => {
    const logged: Array<{ msg: string; bindings: unknown }> = [];
    const fakeLogger = {
      trace: () => {},
      debug: (msg: string, bindings: unknown) => logged.push({ msg, bindings }),
      info: () => {},
      warn: () => {},
      error: () => {},
      fatal: () => {},
      child: () => fakeLogger,
    };
    const interceptor = createContextPropagationInterceptor({
      getCurrentContext: () => FULL_CTX,
      getCurrentTraceparent: () => "00-trace-x-01",
      getCurrentTracestate: () => "vendor=val",
      logger: fakeLogger,
    });
    const captured: CapturedCall = { metadata: new Metadata() };
    const call = interceptor(FAKE_OPTIONS, makeNextCall(captured) as never);
    call.start(new Metadata(), {
      onReceiveMetadata: () => {},
      onReceiveMessage: () => {},
      onReceiveStatus: () => {},
    });
    expect(logged).toHaveLength(1);
    const bindings = logged[0]?.bindings as Record<string, unknown>;
    expect(bindings["hasContext"]).toBe(true);
    expect(bindings["hasTraceparent"]).toBe(true);
    expect(bindings["hasTracestate"]).toBe(true);
    // Verify the actual context value is NOT in the log bindings.
    expect(JSON.stringify(bindings)).not.toContain(FULL_CTX.requestId);
    expect(JSON.stringify(bindings)).not.toContain("vendor=val");
  });

  it("logs hasTraceparent=false when traceparent absent", () => {
    const logged: Array<Record<string, unknown>> = [];
    const fakeLogger = {
      trace: () => {},
      debug: (_msg: string, bindings: unknown) =>
        logged.push(bindings as Record<string, unknown>),
      info: () => {},
      warn: () => {},
      error: () => {},
      fatal: () => {},
      child: () => fakeLogger,
    };
    const interceptor = createContextPropagationInterceptor({
      getCurrentContext: () => undefined,
      logger: fakeLogger,
    });
    const captured: CapturedCall = { metadata: new Metadata() };
    const call = interceptor(FAKE_OPTIONS, makeNextCall(captured) as never);
    call.start(new Metadata(), {
      onReceiveMetadata: () => {},
      onReceiveMessage: () => {},
      onReceiveStatus: () => {},
    });
    const b = logged[0] as Record<string, unknown>;
    expect(b["hasTraceparent"]).toBe(false);
    expect(b["hasTracestate"]).toBe(false);
    expect(b["hasContext"]).toBe(false);
  });
});

describe("createContextPropagationInterceptor — wire pin (cross-language parity seed)", () => {
  it("x-d2-context metadata key matches CommonHeaders.PROPAGATED_CONTEXT byte-for-byte", () => {
    expect(CommonHeaders.PROPAGATED_CONTEXT).toBe("x-d2-context");
  });

  it("traceparent metadata key matches CommonHeaders.TRACEPARENT byte-for-byte", () => {
    expect(CommonHeaders.TRACEPARENT).toBe("traceparent");
  });

  it("tracestate metadata key matches CommonHeaders.TRACESTATE byte-for-byte", () => {
    expect(CommonHeaders.TRACESTATE).toBe("tracestate");
  });

  it("PropagatedContextSerializer round-trip is byte-stable", () => {
    const ctx: IPropagatedContext = {
      requestId: "r-1",
      requestPath: "/p",
      sessionFingerprint: "v1.s",
      currentFingerprint: "v1.c",
      riskScore: 50,
      whoIsHashId: "h",
    };
    const json = PropagatedContextSerializer.serialize(ctx);
    const decoded = PropagatedContextSerializer.tryDecode(json);
    expect(decoded).toEqual(ctx);
  });
});
