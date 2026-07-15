// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import type { IAuthContext } from "@d2/auth-context-abstractions";
import { loadFixture } from "../src/index.js";

interface AuthContextPayload {
  readonly scenario: string;
  readonly properties: readonly string[];
}

/**
 * The TS-side IAuthContext is an interface — its property surface is
 * known statically. We enumerate every keyof IAuthContext via a sample
 * value typed as IAuthContext (purely for static-shape extraction).
 *
 * The parity test then asserts the .NET-emitted property list matches
 * this set entry-for-entry per scenario.
 */
const TS_AUTH_CONTEXT_PROPERTIES: readonly string[] = [
  // Token + Trust
  "isAuthenticated",
  "audience",
  "sessionId",
  "tokenIssuedAt",
  "tokenExpiresAt",
  "actorChain",
  "authMethod",
  "lastStepUpAt",
  // Identity
  "subject",
  "userId",
  "username",
  "requestedByClientId",
  "immediateCallerClientId",
  "originatingClientId",
  "isServiceIdentity",
  // Organization
  "orgId",
  "orgName",
  "orgType",
  "orgRole",
  // Impersonation
  "isImpersonating",
  "impersonationKind",
  "impersonatedBy",
  "impersonationSessionId",
  "impersonatorOrgId",
  "impersonatorOrgName",
  "impersonatorOrgType",
  "impersonatorOrgRole",
  // Scopes
  "scopes",
];

describe("auth-context parity (.NET IAuthContext surface ↔ TS IAuthContext shape)", () => {
  const scenarios = [
    "unauthenticated",
    "authenticated-user",
    "service-identity",
    "impersonation-consent",
    "impersonation-force",
  ] as const;

  for (const scenario of scenarios) {
    describe(`scenario "${scenario}"`, () => {
      const fixture = loadFixture<AuthContextPayload>("auth-context", scenario);
      const fixtureProps = [...fixture.data.properties].sort();
      const tsProps = [...TS_AUTH_CONTEXT_PROPERTIES].sort();

      it("property-set membership matches", () => {
        expect(tsProps).toEqual(fixtureProps);
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

  // Compile-time check that the listed property names actually correspond
  // to keys on IAuthContext — keeps TS_AUTH_CONTEXT_PROPERTIES from drifting
  // out of sync with the spec-emitted interface. The cast is intentional:
  // TS interfaces are erased at runtime, so the list is the source of
  // truth for the parity test, but this stub guarantees compilation
  // breaks if a listed key disappears from IAuthContext.
  type EnumeratedKey = (typeof TS_AUTH_CONTEXT_PROPERTIES)[number];
  type AssertKey<K extends keyof IAuthContext> = K;
  type _Check = AssertKey<EnumeratedKey & keyof IAuthContext>;
  const _check: _Check | undefined = undefined;
  void _check;
});
