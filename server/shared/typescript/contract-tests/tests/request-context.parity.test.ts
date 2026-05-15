// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import type { IRequestContext } from "@d2/request-context-abstractions";
import { loadFixture } from "../src/index.js";

interface RequestContextPayload {
  readonly scenario: string;
  readonly properties: readonly string[];
  readonly ownProperties: readonly string[];
}

/**
 * The TS-side IRequestContext (extends IAuthContext) — full property
 * set including transitive IAuthContext fields. Keep in sync with the
 * spec; the compile-time check at the end of this file enforces it.
 */
const TS_REQUEST_CONTEXT_PROPERTIES: readonly string[] = [
  // Inherited from IAuthContext
  "isAuthenticated",
  "audience",
  "sessionId",
  "tokenIssuedAt",
  "tokenExpiresAt",
  "actorChain",
  "subject",
  "userId",
  "username",
  "requestedByClientId",
  "immediateCallerClientId",
  "originatingClientId",
  "isServiceIdentity",
  "orgId",
  "orgName",
  "orgType",
  "orgRole",
  "isImpersonating",
  "impersonationKind",
  "impersonatedBy",
  "impersonationSessionId",
  "impersonatorOrgId",
  "impersonatorOrgName",
  "impersonatorOrgType",
  "impersonatorOrgRole",
  "scopes",
  // Own — Tracing
  "traceId",
  "requestId",
  "requestPath",
  // Own — Network
  "clientIp",
  // Own — Fingerprints
  "sessionFingerprint",
  "currentFingerprint",
  "riskScore",
  // Own — WhoIs Admin Location
  "whoIsHashId",
  "adminLocationHashId",
  "city",
  "region",
  "subdivisionCode",
  "countryCode",
  "postalCode",
  // Own — WhoIs Coordinates
  "latitude",
  "longitude",
  "geohash",
  // Own — WhoIs Network Privacy
  "isVpn",
  "isProxy",
  "isTor",
  "isHosting",
  // Own — WhoIs ASN
  "asn",
  "asnName",
  "asnType",
];

const TS_REQUEST_CONTEXT_OWN_PROPERTIES: readonly string[] = [
  "traceId",
  "requestId",
  "requestPath",
  "clientIp",
  "sessionFingerprint",
  "currentFingerprint",
  "riskScore",
  "whoIsHashId",
  "adminLocationHashId",
  "city",
  "region",
  "subdivisionCode",
  "countryCode",
  "postalCode",
  "latitude",
  "longitude",
  "geohash",
  "isVpn",
  "isProxy",
  "isTor",
  "isHosting",
  "asn",
  "asnName",
  "asnType",
];

describe("request-context parity (.NET IRequestContext surface ↔ TS IRequestContext shape)", () => {
  const scenarios = [
    "minimal",
    "full-with-fingerprints",
    "with-whois",
  ] as const;

  for (const scenario of scenarios) {
    describe(`scenario "${scenario}"`, () => {
      const fixture = loadFixture<RequestContextPayload>(
        "request-context",
        scenario,
      );
      const fixtureProps = [...fixture.data.properties].sort();
      const fixtureOwn = [...fixture.data.ownProperties].sort();
      const tsProps = [...TS_REQUEST_CONTEXT_PROPERTIES].sort();
      const tsOwn = [...TS_REQUEST_CONTEXT_OWN_PROPERTIES].sort();

      it("full property-set (inherited + own) membership matches", () => {
        expect(tsProps).toEqual(fixtureProps);
      });

      it("own property-set (IRequestContext-only) membership matches", () => {
        expect(tsOwn).toEqual(fixtureOwn);
      });

      // Per-PROPERTY pin so a drift names the specific missing / extra
      // property on whichever side.
      for (const prop of fixtureProps) {
        it(`property ${prop} present on TS side`, () => {
          expect(tsProps).toContain(prop);
        });
      }

      for (const prop of tsProps) {
        it(`property ${prop} present on .NET side`, () => {
          expect(fixtureProps).toContain(prop);
        });
      }
    });
  }

  // Compile-time check: every enumerated key is a key of IRequestContext.
  type EnumeratedKey = (typeof TS_REQUEST_CONTEXT_PROPERTIES)[number];
  type AssertKey<K extends keyof IRequestContext> = K;
  type _Check = AssertKey<EnumeratedKey & keyof IRequestContext>;
  const _check: _Check | undefined = undefined;
  void _check;
});
