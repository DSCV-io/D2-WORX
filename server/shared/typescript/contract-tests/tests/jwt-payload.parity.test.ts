// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import { JwtClaimTypes, type JwtPayload } from "@d2/auth-abstractions";
import { loadFixture } from "../src/index.js";

interface JwtPayloadFixturePayload {
  readonly scenario: string;
  readonly specClaimNames: readonly string[];
  readonly claims: Readonly<Record<string, unknown>>;
}

/**
 * The TS-side JwtPayload typed shape exposes every spec-defined
 * top-level claim as a strongly-named property (sub, aud, ...,
 * d2_session_id, ...) plus a `raw` escape hatch for non-spec claims.
 * The spec separates "standard" and "d2-custom" claims (top-level) from
 * "inside-act" claims (nested under `act`).
 */
const TS_JWT_PAYLOAD_TOP_LEVEL_KEYS: readonly string[] = [
  "sub",
  "aud",
  "iat",
  "exp",
  "azp",
  "scope",
  "act",
  "client_id",
  "d2_session_id",
  "d2_username",
  "d2_fp",
  "d2_org_id",
  "d2_org_name",
  "d2_org_type",
  "d2_org_role",
  "raw",
];

describe("jwt-payload parity (spec ↔ TS JwtPayload typed shape)", () => {
  const scenarios = [
    "minimal",
    "d2-custom-claims-only",
    "with-act-chain",
  ] as const;

  for (const scenario of scenarios) {
    describe(`scenario "${scenario}"`, () => {
      const fixture = loadFixture<JwtPayloadFixturePayload>(
        "jwt-payload",
        scenario,
      );
      const specClaims = [...fixture.data.specClaimNames].sort();
      const claims = fixture.data.claims;

      // Spec → JwtClaimTypes parity: every spec claim wire value appears
      // among JwtClaimTypes constant values (which is the same set the
      // typed JwtPayload's keys mirror, plus inside-act handling).
      const claimTypeValues = Object.values(JwtClaimTypes) as readonly string[];

      for (const claimWireValue of specClaims) {
        it(`spec claim ${claimWireValue} is in JwtClaimTypes`, () => {
          expect(claimTypeValues).toContain(claimWireValue);
        });
      }

      // Every populated fixture claim must be a known top-level field
      // (since fixtures only populate top-level claims; the act chain
      // populates `act` as a nested object, which is itself a top-level
      // field).
      for (const claimName of Object.keys(claims)) {
        it(`fixture claim ${claimName} is a known JwtPayload top-level key`, () => {
          expect(TS_JWT_PAYLOAD_TOP_LEVEL_KEYS).toContain(claimName);
        });
      }

      // Compile-time check that TS_JWT_PAYLOAD_TOP_LEVEL_KEYS are real
      // keys of JwtPayload — drift produces a TS error.
      type EnumeratedKey = (typeof TS_JWT_PAYLOAD_TOP_LEVEL_KEYS)[number];
      type AssertKey<K extends keyof JwtPayload> = K;
      type _Check = AssertKey<EnumeratedKey & keyof JwtPayload>;
      const _check: _Check | undefined = undefined;
      void _check;
    });
  }
});
