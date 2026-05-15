// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import { PropagatedContextSerializer } from "../src/PropagatedContextSerializer.g.js";
import { IRequestContextRedactPaths } from "../src/IRequestContext.g.js";

describe("PropagatedContextSerializer", () => {
  it("round-trips a known envelope", () => {
    const ctx = {
      requestId: "abc",
      requestPath: "/x",
      sessionFingerprint: null,
      currentFingerprint: null,
      riskScore: 17,
      whoIsHashId: null,
    } as unknown as Parameters<typeof PropagatedContextSerializer.serialize>[0];
    const enc = PropagatedContextSerializer.serialize(ctx);
    const dec = PropagatedContextSerializer.tryDecode(enc);
    expect(dec).toBeDefined();
    expect((dec as Record<string, unknown>)["requestId"]).toBe("abc");
    expect((dec as Record<string, unknown>)["riskScore"]).toBe(17);
  });

  it("returns undefined on null/empty input", () => {
    expect(PropagatedContextSerializer.tryDecode(null)).toBeUndefined();
    expect(PropagatedContextSerializer.tryDecode(undefined)).toBeUndefined();
    expect(PropagatedContextSerializer.tryDecode("")).toBeUndefined();
  });

  it("returns undefined on malformed JSON", () => {
    expect(PropagatedContextSerializer.tryDecode("{bad json")).toBeUndefined();
  });

  it("returns undefined on non-object payload", () => {
    expect(PropagatedContextSerializer.tryDecode('"a string"')).toBeUndefined();
    expect(PropagatedContextSerializer.tryDecode("42")).toBeUndefined();
  });

  it("returns undefined when string field exceeds maxLength cap", () => {
    const big = "x".repeat(257);
    expect(
      PropagatedContextSerializer.tryDecode(JSON.stringify({ requestId: big })),
    ).toBeUndefined();
  });

  it("returns undefined when bool field has wrong type", () => {
    expect(
      PropagatedContextSerializer.tryDecode(
        JSON.stringify({ requestId: "ok", riskScore: "not-a-number" }),
      ),
    ).toBeUndefined();
  });

  it("nulls survive round-trip without becoming string 'null'", () => {
    const enc = PropagatedContextSerializer.serialize({
      requestId: null,
      requestPath: null,
      sessionFingerprint: null,
      currentFingerprint: null,
      riskScore: null,
      whoIsHashId: null,
    } as unknown as Parameters<
      typeof PropagatedContextSerializer.serialize
    >[0]);
    const dec = PropagatedContextSerializer.tryDecode(enc);
    expect((dec as Record<string, unknown>)["requestId"]).toBeNull();
  });
});

describe("IRequestContextRedactPaths", () => {
  it("includes annotated PII fields", () => {
    expect(IRequestContextRedactPaths).toContain("clientIp");
    expect(IRequestContextRedactPaths).toContain("sessionFingerprint");
    expect(IRequestContextRedactPaths).toContain("currentFingerprint");
    expect(IRequestContextRedactPaths).toContain("city");
    expect(IRequestContextRedactPaths).toContain("region");
    expect(IRequestContextRedactPaths).toContain("postalCode");
    expect(IRequestContextRedactPaths).toContain("latitude");
    expect(IRequestContextRedactPaths).toContain("longitude");
    expect(IRequestContextRedactPaths).toContain("geohash");
    expect(IRequestContextRedactPaths).toContain("asn");
    expect(IRequestContextRedactPaths).toContain("asnName");
    expect(IRequestContextRedactPaths).toContain("asnType");
  });
});
