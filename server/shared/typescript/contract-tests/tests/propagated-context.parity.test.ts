// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import {
  type IPropagatedContext,
  PropagatedContextSerializer,
} from "@d2/request-context-abstractions";
import { canonicalize, loadFixture } from "../src/index.js";

interface PropagatedShape {
  readonly requestId?: string;
  readonly requestPath?: string;
  readonly requestStartedAt?: string;
  readonly idempotencyKey?: string;
  readonly sessionFingerprint?: string;
  readonly currentFingerprint?: string;
  readonly riskScore?: number;
  readonly edgeNodeId?: string;
  readonly localeIetfBcp47Tag?: string;
  readonly timezoneIanaName?: string;
  readonly currencyIso4217Code?: string;
  readonly orgPlanTier?: string;
  readonly featureFlagsCsv?: string;
  readonly whoIsHashId?: string;
}

/**
 * Per-scenario known inputs — MUST stay synchronized with the
 * matching scenario in the .NET PropagatedContextFixtureEmitter. The
 * parity test asserts that feeding the SAME inputs through the TS-side
 * serializer produces the same wire shape the .NET-emitted fixture
 * carries. Drift on either side surfaces as an inequality.
 */
const SCENARIO_INPUTS: Readonly<Record<string, IPropagatedContext>> = {
  empty: {
    requestId: undefined,
    requestPath: undefined,
    requestStartedAt: undefined,
    idempotencyKey: undefined,
    sessionFingerprint: undefined,
    currentFingerprint: undefined,
    riskScore: undefined,
    edgeNodeId: undefined,
    localeIetfBcp47Tag: undefined,
    timezoneIanaName: undefined,
    currencyIso4217Code: undefined,
    orgPlanTier: undefined,
    featureFlagsCsv: undefined,
    whoIsHashId: undefined,
  },
  full: {
    requestId: "req-00000001",
    requestPath: "/api/v1/synthetic/users/00000000-0000-0000-0000-000000000001",
    requestStartedAt: "2026-05-01T12:00:00.0000000+00:00",
    idempotencyKey: "idem-key-0000000000000001",
    sessionFingerprint: "v1.c1.c2.c3.c4.c5.s1.s2.s3.s4.s5",
    currentFingerprint: "v1.c1.c2.c3.c4.c5.s1.s2.s3.s4.s6",
    riskScore: 42,
    edgeNodeId: "edge-node-0001",
    localeIetfBcp47Tag: "en-US",
    timezoneIanaName: "America/New_York",
    currencyIso4217Code: "USD",
    orgPlanTier: "Pro",
    featureFlagsCsv: "new-billing,risk-v2",
    whoIsHashId: "whois-0000000000000001",
  },
  "null-fields-omitted": {
    requestId: "req-partial",
    requestPath: undefined,
    requestStartedAt: undefined,
    idempotencyKey: undefined,
    sessionFingerprint: undefined,
    currentFingerprint: undefined,
    riskScore: 7,
    edgeNodeId: undefined,
    localeIetfBcp47Tag: undefined,
    timezoneIanaName: undefined,
    currencyIso4217Code: undefined,
    orgPlanTier: undefined,
    featureFlagsCsv: undefined,
    whoIsHashId: undefined,
  },
  "at-cap-boundaries": {
    requestId: "r".repeat(256),
    requestPath: "p".repeat(2048),
    idempotencyKey: "k".repeat(255),
    sessionFingerprint: "s".repeat(512),
    currentFingerprint: "c".repeat(512),
    riskScore: 100,
    edgeNodeId: "e".repeat(256),
    localeIetfBcp47Tag: "l".repeat(35),
    timezoneIanaName: "t".repeat(64),
    currencyIso4217Code: "u".repeat(3),
    orgPlanTier: "o".repeat(64),
    featureFlagsCsv: "f".repeat(2048),
    whoIsHashId: "w".repeat(128),
  },
};

const ALL_FIELDS = [
  "requestId",
  "requestPath",
  "requestStartedAt",
  "idempotencyKey",
  "sessionFingerprint",
  "currentFingerprint",
  "riskScore",
  "edgeNodeId",
  "localeIetfBcp47Tag",
  "timezoneIanaName",
  "currencyIso4217Code",
  "orgPlanTier",
  "featureFlagsCsv",
  "whoIsHashId",
] as const;

describe("propagated-context parity (.NET emit ↔ TS PropagatedContextSerializer)", () => {
  const scenarios = Object.keys(SCENARIO_INPUTS).sort();

  for (const scenario of scenarios) {
    describe(`scenario "${scenario}"`, () => {
      const fixture = loadFixture<PropagatedShape>(
        "propagated-context",
        scenario,
      );
      const fixtureData = fixture.data;
      const tsInputs = SCENARIO_INPUTS[scenario]!;

      it("TS-side serializer produces the same wire shape the fixture carries", () => {
        const reSerialized = PropagatedContextSerializer.serialize(tsInputs);
        const reParsed = JSON.parse(reSerialized) as Record<string, unknown>;
        // Strip undefined-valued fields so the shape matches the fixture's
        // omit-undefined encoding (which mirrors the .NET WhenWritingNull).
        const reParsedStripped: Record<string, unknown> = {};
        for (const [k, v] of Object.entries(reParsed))
          if (v !== null && v !== undefined) reParsedStripped[k] = v;

        expect(canonicalize(reParsedStripped)).toEqual(
          canonicalize(fixtureData),
        );
      });

      // Per-VALUE pin per spec field — the fixture's value must equal
      // the TS-side scenario input's value (for non-null fields). A
      // failure names the specific field that drifted.
      for (const field of ALL_FIELDS) {
        const tsValue = tsInputs[field];
        if (tsValue === null || tsValue === undefined) {
          it(`field ${field} is omitted from fixture (matches undefined input)`, () => {
            expect(fixtureData[field]).toBeUndefined();
          });
        } else {
          it(`field ${field} value matches fixture`, () => {
            expect(fixtureData[field]).toBe(tsValue);
          });
        }
      }

      it("TS-side decoder accepts the fixture's wire shape (cap enforcement)", () => {
        // The TS-side tryDecode takes a JSON string directly (the
        // base64url envelope is the .NET-side concern; the JSON shape
        // is the parity-tested surface). Feeding the fixture's data
        // JSON through tryDecode confirms every per-spec-field cap is
        // respected (the at-cap-boundaries scenario verifies the caps
        // are "<=" not "<").
        const json = JSON.stringify(fixtureData);
        const decoded = PropagatedContextSerializer.tryDecode(json);
        expect(decoded).not.toBeUndefined();
      });
    });
  }
});
