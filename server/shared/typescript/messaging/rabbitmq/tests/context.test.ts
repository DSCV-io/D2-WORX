// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { trace } from "@opentelemetry/api";
import { type IPropagatedContext } from "@d2/request-context-abstractions";
import { describe, expect, it } from "vitest";

import {
  applyPropagatedContext,
  establishConsumeContext,
  type MutablePropagatedContext,
} from "../src/context/consume-context.js";
import {
  parentContextFrom,
  parseTraceparent,
} from "../src/context/trace-context.js";
import {
  encodePropagatedHeader,
  SAMPLE_PRODUCER_TRACE_ID,
  SAMPLE_TRACEPARENT,
} from "./helpers.js";

function base64Url(json: string): string {
  return Buffer.from(json, "utf8").toString("base64url");
}

describe("parseTraceparent — W3C linkage, fail-safe on malformed", () => {
  it("parses a valid traceparent into a remote span context", () => {
    const ctx = parseTraceparent(SAMPLE_TRACEPARENT);
    expect(ctx).toBeDefined();
    expect(ctx?.traceId).toBe(SAMPLE_PRODUCER_TRACE_ID);
    expect(ctx?.spanId).toBe("00f067aa0ba902b7");
    expect(ctx?.isRemote).toBe(true);
    expect(ctx?.traceFlags).toBe(1);
  });

  it.each([
    ["undefined", undefined],
    ["empty", ""],
    ["garbage", "not-a-traceparent"],
    [
      "wrong version",
      "01-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
    ],
    ["short trace id", "00-abc-00f067aa0ba902b7-01"],
    [
      "all-zero trace id",
      "00-00000000000000000000000000000000-00f067aa0ba902b7-01",
    ],
    [
      "all-zero span id",
      "00-4bf92f3577b34da6a3ce929d0e0e4736-0000000000000000-01",
    ],
  ])("returns undefined for %s", (_label, value) => {
    expect(parseTraceparent(value as string | undefined)).toBeUndefined();
  });

  it("builds a parent context that carries the parsed span context", () => {
    const sc = parseTraceparent(SAMPLE_TRACEPARENT);
    const parent = parentContextFrom(sc);
    expect(parent).toBeDefined();
    expect(trace.getSpanContext(parent!)?.traceId).toBe(
      SAMPLE_PRODUCER_TRACE_ID,
    );
  });

  it("returns undefined parent context when there is no parent", () => {
    expect(parentContextFrom(undefined)).toBeUndefined();
  });
});

describe("establishConsumeContext — §5.2a propagated-context apply", () => {
  const full: IPropagatedContext = {
    requestId: "req-123",
    requestPath: "/v2/keys/rotate",
    sessionFingerprint: "sfp",
    currentFingerprint: "cfp",
    whoIsHashId: "who-hash",
    localeIetfBcp47Tag: "en-US",
    callPath: [
      { id: "edge", kind: "Edge", timestamp: "2026-07-03T00:00:00Z" },
      {
        id: "keycustodian",
        kind: "WorkloadHop",
        timestamp: "2026-07-03T00:00:01Z",
      },
    ],
  };

  it("applies EVERY propagated operational field, including callPath", () => {
    const encoded = encodePropagatedHeader(full);
    const ctx = establishConsumeContext(encoded);

    expect(ctx.propagated.requestId).toBe("req-123");
    expect(ctx.propagated.requestPath).toBe("/v2/keys/rotate");
    expect(ctx.propagated.sessionFingerprint).toBe("sfp");
    expect(ctx.propagated.currentFingerprint).toBe("cfp");
    expect(ctx.propagated.whoIsHashId).toBe("who-hash");
    expect(ctx.propagated.localeIetfBcp47Tag).toBe("en-US");
    expect(ctx.propagated.callPath).toHaveLength(2);
    expect(ctx.propagated.callPath?.[1]?.id).toBe("keycustodian");
  });

  it("NEVER admits identity or origin from a crafted header (§9.41)", () => {
    // Smuggle identity + origin impostor keys into the wire JSON.
    const crafted = base64Url(
      JSON.stringify({
        requestId: "req-9",
        userId: "attacker",
        orgId: "evil-org",
        scopes: ["admin"],
        origin: "CrossProcessHop",
        RequestOrigin: "System",
      }),
    );
    const ctx = establishConsumeContext(crafted);
    const asRecord = ctx.propagated as Record<string, unknown>;

    expect(ctx.propagated.requestId).toBe("req-9");
    expect(asRecord["userId"]).toBeUndefined();
    expect(asRecord["orgId"]).toBeUndefined();
    expect(asRecord["scopes"]).toBeUndefined();
    expect(asRecord["origin"]).toBeUndefined();
    expect(asRecord["RequestOrigin"]).toBeUndefined();
  });

  it.each([
    ["absent", undefined],
    ["empty", ""],
    ["undecodable base64", base64Url("}{not json")],
    [
      "cap-busting requestId",
      base64Url(JSON.stringify({ requestId: "x".repeat(300) })),
    ],
    ["header with CR/LF (rejected pre-decode)", "abc\ndef"],
  ])(
    "yields an empty context for a %s header (fail-safe, no throw)",
    (_l, header) => {
      const ctx = establishConsumeContext(header as string | undefined);
      expect(
        Object.keys(ctx.propagated as Record<string, unknown>),
      ).toHaveLength(0);
    },
  );

  it("gives each message its OWN fresh context (no cross-message bleed)", () => {
    const a = establishConsumeContext(
      encodePropagatedHeader({ requestId: "a" }),
    );
    const b = establishConsumeContext(
      encodePropagatedHeader({ requestId: "b" }),
    );
    expect(a.propagated).not.toBe(b.propagated);
    expect(a.propagated.requestId).toBe("a");
    expect(b.propagated.requestId).toBe("b");
  });

  it("applyPropagatedContext copies the operational subset onto a fresh target", () => {
    const target: MutablePropagatedContext = {};
    applyPropagatedContext(target, { requestId: "r", callPath: full.callPath });
    expect(target.requestId).toBe("r");
    expect(target.callPath).toHaveLength(2);
  });
});
