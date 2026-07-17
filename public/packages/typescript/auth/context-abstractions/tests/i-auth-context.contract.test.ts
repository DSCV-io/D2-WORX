// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import {
  ActorKind,
  IAuthContextRedactPaths,
  ImpersonationKind,
  OrgType,
  Role,
} from "../src/index.js";

describe("@dcsv-io/d2-auth-context-abstractions — emitted shape pin", () => {
  it("OrgType members match spec vocabulary", () => {
    expect(OrgType.Admin).toBe("Admin");
    expect(OrgType.Support).toBe("Support");
    expect(OrgType.Customer).toBe("Customer");
    expect(OrgType.ThirdParty).toBe("ThirdParty");
    expect(OrgType.Affiliate).toBe("Affiliate");
  });

  it("Role members match spec vocabulary", () => {
    expect(Role.Auditor).toBe("Auditor");
    expect(Role.Agent).toBe("Agent");
    expect(Role.Officer).toBe("Officer");
    expect(Role.Owner).toBe("Owner");
  });

  it("ImpersonationKind exposes Consent + Force", () => {
    expect(ImpersonationKind.Consent).toBe("Consent");
    expect(ImpersonationKind.Force).toBe("Force");
  });

  it("ActorKind exposes Service + Impersonation", () => {
    expect(ActorKind.Service).toBe("Service");
    expect(ActorKind.Impersonation).toBe("Impersonation");
  });

  it("IAuthContextRedactPaths excludes observability correlation identifiers", () => {
    // userId + username are standard observability correlation fields
    // (logs / traces / spans key on them). They are the surrogate
    // identifiers themselves — NOT personal data that references the user
    // (email / phone / IP / address) — so they must NOT be redacted from
    // logs. Spec markers `redact: true` are reserved for PII fields that
    // reference the user, not the user's correlation key.
    expect(IAuthContextRedactPaths).not.toContain("userId");
    expect(IAuthContextRedactPaths).not.toContain("username");
  });
});
