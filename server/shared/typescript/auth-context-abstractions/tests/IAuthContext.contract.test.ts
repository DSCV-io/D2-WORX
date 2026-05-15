// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import {
  ActorKind,
  IAuthContextRedactPaths,
  ImpersonationKind,
  OrgType,
  Role,
} from "../src/index.js";

describe("@d2/auth-context-abstractions — emitted shape pin", () => {
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

  it("IAuthContextRedactPaths includes annotated PII fields", () => {
    expect(IAuthContextRedactPaths).toContain("userId");
    expect(IAuthContextRedactPaths).toContain("username");
  });
});
