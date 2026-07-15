// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import { CallPathKind } from "@d2/auth-context-abstractions";
import { PropagatedContextSerializer } from "../src/PropagatedContextSerializer.g.js";
import type { IPropagatedContext } from "../src/IPropagatedContext.g.js";
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
    expect((dec as unknown as Record<string, unknown>)["requestId"]).toBe(
      "abc",
    );
    expect((dec as unknown as Record<string, unknown>)["riskScore"]).toBe(17);
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

  it("null/undefined fields are omitted from the serialized envelope", () => {
    // Null/undefined values must not become the string "null" — they must
    // be entirely absent from the wire envelope (WhenWritingNull semantics).
    // When decoded, absent fields surface as undefined (not null).
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
    const parsed = JSON.parse(enc) as Record<string, unknown>;
    // Wire: key must not exist at all (absent, not null string).
    expect(Object.prototype.hasOwnProperty.call(parsed, "requestId")).toBe(
      false,
    );
    // Decode: absent fields surface as undefined.
    const dec = PropagatedContextSerializer.tryDecode(enc);
    expect(
      (dec as unknown as Record<string, unknown>)["requestId"],
    ).toBeUndefined();
  });
});

describe("PropagatedContextSerializer — new propagated fields (Surface 3/4)", () => {
  // Surface 4: TS-side propagation round-trips for 7 new propagated fields.

  it("round-trips localeIetfBcp47Tag when set", () => {
    const enc = PropagatedContextSerializer.serialize({
      localeIetfBcp47Tag: "en-US",
    } as unknown as Parameters<
      typeof PropagatedContextSerializer.serialize
    >[0]);
    const dec = PropagatedContextSerializer.tryDecode(enc) as Record<
      string,
      unknown
    >;
    expect(dec).toBeDefined();
    expect(dec["localeIetfBcp47Tag"]).toBe("en-US");
  });

  it("round-trips localeIetfBcp47Tag as undefined when absent", () => {
    const enc = PropagatedContextSerializer.serialize({
      localeIetfBcp47Tag: undefined,
    } as unknown as Parameters<
      typeof PropagatedContextSerializer.serialize
    >[0]);
    const dec = PropagatedContextSerializer.tryDecode(enc) as Record<
      string,
      unknown
    >;
    expect(dec).toBeDefined();
    expect(dec["localeIetfBcp47Tag"]).toBeUndefined();
  });

  it("round-trips timezoneIanaName when set", () => {
    const enc = PropagatedContextSerializer.serialize({
      timezoneIanaName: "America/New_York",
    } as unknown as Parameters<
      typeof PropagatedContextSerializer.serialize
    >[0]);
    const dec = PropagatedContextSerializer.tryDecode(enc) as Record<
      string,
      unknown
    >;
    expect(dec).toBeDefined();
    expect(dec["timezoneIanaName"]).toBe("America/New_York");
  });

  it("round-trips timezoneIanaName as undefined when absent", () => {
    const enc = PropagatedContextSerializer.serialize({
      timezoneIanaName: undefined,
    } as unknown as Parameters<
      typeof PropagatedContextSerializer.serialize
    >[0]);
    const dec = PropagatedContextSerializer.tryDecode(enc) as Record<
      string,
      unknown
    >;
    expect(dec).toBeDefined();
    expect(dec["timezoneIanaName"]).toBeUndefined();
  });

  it("round-trips currencyIso4217Code when set", () => {
    const enc = PropagatedContextSerializer.serialize({
      currencyIso4217Code: "USD",
    } as unknown as Parameters<
      typeof PropagatedContextSerializer.serialize
    >[0]);
    const dec = PropagatedContextSerializer.tryDecode(enc) as Record<
      string,
      unknown
    >;
    expect(dec).toBeDefined();
    expect(dec["currencyIso4217Code"]).toBe("USD");
  });

  it("round-trips currencyIso4217Code as undefined when absent", () => {
    const enc = PropagatedContextSerializer.serialize({
      currencyIso4217Code: undefined,
    } as unknown as Parameters<
      typeof PropagatedContextSerializer.serialize
    >[0]);
    const dec = PropagatedContextSerializer.tryDecode(enc) as Record<
      string,
      unknown
    >;
    expect(dec).toBeDefined();
    expect(dec["currencyIso4217Code"]).toBeUndefined();
  });

  it("round-trips edgeNodeId when set", () => {
    const enc = PropagatedContextSerializer.serialize({
      edgeNodeId: "edge-pod-01",
    } as unknown as Parameters<
      typeof PropagatedContextSerializer.serialize
    >[0]);
    const dec = PropagatedContextSerializer.tryDecode(enc) as Record<
      string,
      unknown
    >;
    expect(dec).toBeDefined();
    expect(dec["edgeNodeId"]).toBe("edge-pod-01");
  });

  it("round-trips edgeNodeId as undefined when absent", () => {
    const enc = PropagatedContextSerializer.serialize({
      edgeNodeId: undefined,
    } as unknown as Parameters<
      typeof PropagatedContextSerializer.serialize
    >[0]);
    const dec = PropagatedContextSerializer.tryDecode(enc) as Record<
      string,
      unknown
    >;
    expect(dec).toBeDefined();
    expect(dec["edgeNodeId"]).toBeUndefined();
  });

  it("round-trips orgPlanTier when set", () => {
    const enc = PropagatedContextSerializer.serialize({
      orgPlanTier: "Enterprise",
    } as unknown as Parameters<
      typeof PropagatedContextSerializer.serialize
    >[0]);
    const dec = PropagatedContextSerializer.tryDecode(enc) as Record<
      string,
      unknown
    >;
    expect(dec).toBeDefined();
    expect(dec["orgPlanTier"]).toBe("Enterprise");
  });

  it("round-trips orgPlanTier as undefined when absent", () => {
    const enc = PropagatedContextSerializer.serialize({
      orgPlanTier: undefined,
    } as unknown as Parameters<
      typeof PropagatedContextSerializer.serialize
    >[0]);
    const dec = PropagatedContextSerializer.tryDecode(enc) as Record<
      string,
      unknown
    >;
    expect(dec).toBeDefined();
    expect(dec["orgPlanTier"]).toBeUndefined();
  });

  it("round-trips featureFlagsCsv when set", () => {
    const enc = PropagatedContextSerializer.serialize({
      featureFlagsCsv: "new-billing,risk-v2",
    } as unknown as Parameters<
      typeof PropagatedContextSerializer.serialize
    >[0]);
    const dec = PropagatedContextSerializer.tryDecode(enc) as Record<
      string,
      unknown
    >;
    expect(dec).toBeDefined();
    expect(dec["featureFlagsCsv"]).toBe("new-billing,risk-v2");
  });

  it("round-trips featureFlagsCsv as undefined when absent", () => {
    const enc = PropagatedContextSerializer.serialize({
      featureFlagsCsv: undefined,
    } as unknown as Parameters<
      typeof PropagatedContextSerializer.serialize
    >[0]);
    const dec = PropagatedContextSerializer.tryDecode(enc) as Record<
      string,
      unknown
    >;
    expect(dec).toBeDefined();
    expect(dec["featureFlagsCsv"]).toBeUndefined();
  });

  it("round-trips idempotencyKey when set", () => {
    const enc = PropagatedContextSerializer.serialize({
      idempotencyKey: "idem-xyz-789",
    } as unknown as Parameters<
      typeof PropagatedContextSerializer.serialize
    >[0]);
    const dec = PropagatedContextSerializer.tryDecode(enc) as Record<
      string,
      unknown
    >;
    expect(dec).toBeDefined();
    expect(dec["idempotencyKey"]).toBe("idem-xyz-789");
  });

  it("round-trips idempotencyKey as undefined when absent", () => {
    const enc = PropagatedContextSerializer.serialize({
      idempotencyKey: undefined,
    } as unknown as Parameters<
      typeof PropagatedContextSerializer.serialize
    >[0]);
    const dec = PropagatedContextSerializer.tryDecode(enc) as Record<
      string,
      unknown
    >;
    expect(dec).toBeDefined();
    expect(dec["idempotencyKey"]).toBeUndefined();
  });

  // Surface 7 (TS): temporal adversarial — requestStartedAt (ISO 8601 string? wire form).
  it("round-trips requestStartedAt ISO string when set", () => {
    const isoValue = "2026-05-27T14:30:00.000Z";
    const enc = PropagatedContextSerializer.serialize({
      requestStartedAt: isoValue,
    } as unknown as Parameters<
      typeof PropagatedContextSerializer.serialize
    >[0]);
    const dec = PropagatedContextSerializer.tryDecode(enc) as Record<
      string,
      unknown
    >;
    expect(dec).toBeDefined();
    expect(dec["requestStartedAt"]).toBe(isoValue);
  });

  it("round-trips requestStartedAt as undefined when absent", () => {
    const enc = PropagatedContextSerializer.serialize({
      requestStartedAt: undefined,
    } as unknown as Parameters<
      typeof PropagatedContextSerializer.serialize
    >[0]);
    const dec = PropagatedContextSerializer.tryDecode(enc) as Record<
      string,
      unknown
    >;
    expect(dec).toBeDefined();
    expect(dec["requestStartedAt"]).toBeUndefined();
  });

  it("round-trips requestStartedAt leap year date", () => {
    // Leap year / day: 2024-02-29 must survive round-trip without date
    // arithmetic corruption.
    const leapDay = "2024-02-29T12:00:00.000Z";
    const enc = PropagatedContextSerializer.serialize({
      requestStartedAt: leapDay,
    } as unknown as Parameters<
      typeof PropagatedContextSerializer.serialize
    >[0]);
    const dec = PropagatedContextSerializer.tryDecode(enc) as Record<
      string,
      unknown
    >;
    expect(dec).toBeDefined();
    expect(dec["requestStartedAt"]).toBe(leapDay);
  });

  it("round-trips requestStartedAt year boundary value", () => {
    // Year boundary: 2025-12-31T23:59:59.999Z preserved exactly.
    const yearEnd = "2025-12-31T23:59:59.999Z";
    const enc = PropagatedContextSerializer.serialize({
      requestStartedAt: yearEnd,
    } as unknown as Parameters<
      typeof PropagatedContextSerializer.serialize
    >[0]);
    const dec = PropagatedContextSerializer.tryDecode(enc) as Record<
      string,
      unknown
    >;
    expect(dec).toBeDefined();
    expect(dec["requestStartedAt"]).toBe(yearEnd);
  });

  it("full new-field envelope round-trips all 13 propagated fields", () => {
    // Full round-trip smoke covering all new propagated fields together.
    const fullCtx = {
      requestId: "req-abc",
      requestPath: "/api/v1/test",
      requestStartedAt: "2026-05-27T10:00:00.000Z",
      idempotencyKey: "idem-xyz-789",
      sessionFingerprint: "fp-session-aaa",
      currentFingerprint: "fp-current-bbb",
      riskScore: 42,
      edgeNodeId: "edge-pod-01",
      localeIetfBcp47Tag: "en-US",
      timezoneIanaName: "America/Chicago",
      currencyIso4217Code: "USD",
      orgPlanTier: "Pro",
      featureFlagsCsv: "new-billing,risk-v2",
      whoIsHashId: "hash-deadbeef",
    };
    const enc = PropagatedContextSerializer.serialize(
      fullCtx as unknown as Parameters<
        typeof PropagatedContextSerializer.serialize
      >[0],
    );
    const dec = PropagatedContextSerializer.tryDecode(enc) as Record<
      string,
      unknown
    >;
    expect(dec).toBeDefined();
    for (const [key, value] of Object.entries(fullCtx))
      expect(dec[key]).toBe(value);
  });

  it("new fields are omitted from serialized envelope when undefined", () => {
    // WhenWritingNull semantics: undefined values must not appear on the wire.
    const enc = PropagatedContextSerializer.serialize({
      localeIetfBcp47Tag: undefined,
      timezoneIanaName: undefined,
      currencyIso4217Code: undefined,
      edgeNodeId: undefined,
      orgPlanTier: undefined,
      featureFlagsCsv: undefined,
      idempotencyKey: undefined,
      requestStartedAt: undefined,
    } as unknown as Parameters<
      typeof PropagatedContextSerializer.serialize
    >[0]);
    const parsed = JSON.parse(enc) as Record<string, unknown>;
    expect(
      Object.prototype.hasOwnProperty.call(parsed, "localeIetfBcp47Tag"),
    ).toBe(false);
    expect(
      Object.prototype.hasOwnProperty.call(parsed, "timezoneIanaName"),
    ).toBe(false);
    expect(
      Object.prototype.hasOwnProperty.call(parsed, "currencyIso4217Code"),
    ).toBe(false);
    expect(Object.prototype.hasOwnProperty.call(parsed, "edgeNodeId")).toBe(
      false,
    );
    expect(Object.prototype.hasOwnProperty.call(parsed, "orgPlanTier")).toBe(
      false,
    );
    expect(
      Object.prototype.hasOwnProperty.call(parsed, "featureFlagsCsv"),
    ).toBe(false);
    expect(Object.prototype.hasOwnProperty.call(parsed, "idempotencyKey")).toBe(
      false,
    );
    expect(
      Object.prototype.hasOwnProperty.call(parsed, "requestStartedAt"),
    ).toBe(false);
  });
});

describe("IRequestContextRedactPaths", () => {
  it("includes annotated PII fields", () => {
    expect(IRequestContextRedactPaths).toContain("clientIp");
    expect(IRequestContextRedactPaths).toContain("city");
    expect(IRequestContextRedactPaths).toContain("subdivisionIso31662Code");
    expect(IRequestContextRedactPaths).toContain("postalCode");
    expect(IRequestContextRedactPaths).toContain("latitude");
    expect(IRequestContextRedactPaths).toContain("longitude");
    expect(IRequestContextRedactPaths).toContain("geohash");
  });

  it("excludes fingerprint fields (redacted at the logging layer, not via Pino paths)", () => {
    // SessionFingerprint and CurrentFingerprint are redacted at the
    // D2RequestContextEnricher level (not emitted as log properties at
    // all), so they are not present in the Pino redact-paths array.
    expect(IRequestContextRedactPaths).not.toContain("sessionFingerprint");
    expect(IRequestContextRedactPaths).not.toContain("currentFingerprint");
  });

  it("excludes network-category fields that are not personal identifiers", () => {
    // asn, asnName, asnType — these are network-infrastructure fields.
    // asn and asnName identify ISPs/organizations (not individual users),
    // and asnType is a closed-vocabulary category. None are redacted via
    // Pino path redaction; risk-scoring logic consumes them directly.
    expect(IRequestContextRedactPaths).not.toContain("asn");
    expect(IRequestContextRedactPaths).not.toContain("asnName");
    expect(IRequestContextRedactPaths).not.toContain("asnType");
  });

  it("does not include the removed 'region' field", () => {
    // 'region' was dropped in favor of the standards-explicit
    // subdivisionIso31662Code field. The redact paths array must not
    // contain the old name.
    expect(IRequestContextRedactPaths).not.toContain("region");
  });
});

describe("PropagatedContextSerializer — call-path (propagated list-of-records)", () => {
  // The first propagated list-of-records field. Mirrors the .NET CallPath
  // depth-bound (max entry count = 16) + per-entry-id cap (128).

  const buildPath = (count: number): IPropagatedContext["callPath"] =>
    Array.from({ length: count }, (_unused, i) => ({
      id: `svc-${i}`,
      kind: CallPathKind.WorkloadHop,
      timestamp: `2026-05-27T10:00:0${i % 10}.0000000+00:00`,
    }));

  it("round-trips a multi-entry call-path preserving order + fields", () => {
    const callPath = [
      {
        id: "edge",
        kind: CallPathKind.Edge,
        timestamp: "2026-05-27T10:00:00.0000000+00:00",
      },
      {
        id: "key-custodian",
        kind: CallPathKind.WorkloadHop,
        timestamp: "2026-05-27T10:00:01.0000000+00:00",
      },
      {
        id: "audit",
        kind: CallPathKind.ModuleHop,
        timestamp: "2026-05-27T10:00:02.0000000+00:00",
      },
    ];
    const enc = PropagatedContextSerializer.serialize({ callPath });
    const dec = PropagatedContextSerializer.tryDecode(enc);

    expect(dec).toBeDefined();
    expect(dec!.callPath).toEqual(callPath);
  });

  it("serializes the entry kind as its human-readable string name", () => {
    const enc = PropagatedContextSerializer.serialize({
      callPath: [
        {
          id: "kc",
          kind: CallPathKind.WorkloadHop,
          timestamp: "2026-05-27T10:00:00.0000000+00:00",
        },
      ],
    });
    expect(enc).toContain('"kind":"WorkloadHop"');
    expect(enc).not.toContain('"kind":1');
  });

  it("omits the call-path from the wire when undefined", () => {
    const enc = PropagatedContextSerializer.serialize({
      requestId: "r",
      callPath: undefined,
    });
    const parsed = JSON.parse(enc) as Record<string, unknown>;
    expect(Object.prototype.hasOwnProperty.call(parsed, "callPath")).toBe(
      false,
    );
  });

  it("accepts a path at the depth bound (16 entries)", () => {
    const enc = PropagatedContextSerializer.serialize({
      callPath: buildPath(16),
    });
    const dec = PropagatedContextSerializer.tryDecode(enc);
    expect(dec).toBeDefined();
    expect(dec!.callPath).toHaveLength(16);
  });

  it("drops the context when the call-path exceeds the depth bound (17 entries)", () => {
    const enc = PropagatedContextSerializer.serialize({
      callPath: buildPath(17),
    });
    expect(PropagatedContextSerializer.tryDecode(enc)).toBeUndefined();
  });

  it("accepts an entry id at the per-entry cap (128 chars)", () => {
    const enc = PropagatedContextSerializer.serialize({
      callPath: [
        {
          id: "a".repeat(128),
          kind: CallPathKind.Edge,
          timestamp: "2026-05-27T10:00:00.0000000+00:00",
        },
      ],
    });
    expect(PropagatedContextSerializer.tryDecode(enc)).toBeDefined();
  });

  it("drops the context when an entry id exceeds the per-entry cap (129 chars)", () => {
    const enc = PropagatedContextSerializer.serialize({
      callPath: [
        {
          id: "a".repeat(129),
          kind: CallPathKind.Edge,
          timestamp: "2026-05-27T10:00:00.0000000+00:00",
        },
      ],
    });
    expect(PropagatedContextSerializer.tryDecode(enc)).toBeUndefined();
  });

  it("drops the context when callPath is not an array", () => {
    expect(
      PropagatedContextSerializer.tryDecode(
        JSON.stringify({ callPath: "not-an-array" }),
      ),
    ).toBeUndefined();
  });

  it("drops the context when an entry is malformed (missing/typed id, kind, timestamp)", () => {
    expect(
      PropagatedContextSerializer.tryDecode(
        JSON.stringify({ callPath: [{ kind: "Edge", timestamp: "t" }] }),
      ),
    ).toBeUndefined();
    expect(
      PropagatedContextSerializer.tryDecode(
        JSON.stringify({ callPath: [{ id: "x", timestamp: "t" }] }),
      ),
    ).toBeUndefined();
    expect(
      PropagatedContextSerializer.tryDecode(
        JSON.stringify({ callPath: [{ id: "x", kind: "Edge" }] }),
      ),
    ).toBeUndefined();
    expect(
      PropagatedContextSerializer.tryDecode(
        JSON.stringify({ callPath: ["not-an-object"] }),
      ),
    ).toBeUndefined();
  });
});
