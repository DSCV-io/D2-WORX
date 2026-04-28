import { describe, it, expect } from "vitest";
import { Hono } from "hono";
import { requireOrg, requireOrgType, requireRole, requireStaff, requireAdmin } from "@d2/auth-api";
import { OrgType, type IRequestContext } from "@d2/handler";

/**
 * Maps a domain (lowercase) org-type string to the `OrgType` enum value used
 * by `IRequestContext.targetOrgType`. Returns `undefined` for unrecognized
 * values — mirrors what the production scope middleware does, so policies
 * that read `targetOrgType` see `undefined` for invalid input rather than a
 * raw string they can't compare to the enum.
 */
function toOrgType(value: unknown): OrgType | undefined {
  if (typeof value !== "string") return undefined;
  switch (value) {
    case "admin":
      return OrgType.Admin;
    case "support":
      return OrgType.Support;
    case "customer":
      return OrgType.Customer;
    case "third_party":
      return OrgType.ThirdParty;
    case "affiliate":
      return OrgType.Affiliate;
    default:
      return undefined;
  }
}

/**
 * Builds an `IRequestContext` from the legacy BetterAuth session shape used
 * throughout this file (`activeOrganizationId/Type/Role`). Lets us keep the
 * existing test bodies unchanged while having the policies read identity
 * from `c.var.requestContext` (the new contract).
 */
function sessionToRequestContext(session: Record<string, unknown> | null): IRequestContext | null {
  if (!session) return null;
  const orgIdRaw = session["activeOrganizationId"];
  const orgTypeRaw = session["activeOrganizationType"];
  const roleRaw = session["activeOrganizationRole"];
  return {
    isAuthenticated: true,
    isTrustedService: false,
    isOrgEmulating: false,
    isUserImpersonating: false,
    userId: "user-1",
    email: "test@test.com",
    targetOrgId: typeof orgIdRaw === "string" ? orgIdRaw : undefined,
    targetOrgType: toOrgType(orgTypeRaw),
    targetOrgRole: typeof roleRaw === "string" ? roleRaw : undefined,
    agentOrgId: typeof orgIdRaw === "string" ? orgIdRaw : undefined,
    agentOrgType: toOrgType(orgTypeRaw),
    agentOrgRole: typeof roleRaw === "string" ? roleRaw : undefined,
  };
}

/**
 * Creates a test Hono app with the given middleware applied.
 * A setup middleware injects requestContext before the auth middleware runs.
 */
function createApp(
  session: Record<string, unknown> | null,
  ...middlewares: ReturnType<typeof requireOrg>[]
) {
  const app = new Hono();

  // Inject requestContext into c.var (simulates session+scope middleware)
  app.use("*", async (c, next) => {
    c.set("requestContext", sessionToRequestContext(session));
    await next();
  });

  for (const mw of middlewares) {
    app.use("*", mw);
  }

  app.get("/test", (c) => c.json({ ok: true }));
  return app;
}

/** Helper to build a session with active org fields (legacy shape, translated by sessionToRequestContext). */
function sessionWith(
  orgId: string,
  orgType: string,
  role: string,
  extra?: Record<string, unknown>,
): Record<string, unknown> {
  return {
    activeOrganizationId: orgId,
    activeOrganizationType: orgType,
    activeOrganizationRole: role,
    ...extra,
  };
}

describe("requireOrg", () => {
  it("should pass when session has active org", async () => {
    const app = createApp(sessionWith("org-1", "customer", "owner"), requireOrg());
    const res = await app.request("/test");
    expect(res.status).toBe(200);
  });

  it("should reject with 401 when no session", async () => {
    const app = createApp(null, requireOrg());
    const res = await app.request("/test");
    expect(res.status).toBe(401);
  });

  it("should reject with 403 when session has no active org", async () => {
    const app = createApp({}, requireOrg());
    const res = await app.request("/test");
    expect(res.status).toBe(403);
    const body = await res.json();
    expect(body.messages[0]).toContain("middleware_errors_NO_ACTIVE_ORGANIZATION");
  });

  it("should reject with 403 when orgType is invalid", async () => {
    const app = createApp(
      {
        activeOrganizationId: "org-1",
        activeOrganizationType: "bogus",
        activeOrganizationRole: "owner",
      },
      requireOrg(),
    );
    const res = await app.request("/test");
    expect(res.status).toBe(403);
  });

  it("should reject with 403 when role is invalid", async () => {
    const app = createApp(
      {
        activeOrganizationId: "org-1",
        activeOrganizationType: "customer",
        activeOrganizationRole: "bogus",
      },
      requireOrg(),
    );
    const res = await app.request("/test");
    expect(res.status).toBe(403);
  });
});

describe("requireOrgType", () => {
  it("should pass when orgType matches single allowed type", async () => {
    const app = createApp(sessionWith("org-1", "admin", "owner"), requireOrgType(OrgType.Admin));
    const res = await app.request("/test");
    expect(res.status).toBe(200);
  });

  it("should pass when orgType matches one of multiple allowed types", async () => {
    const app = createApp(
      sessionWith("org-1", "support", "officer"),
      requireOrgType(OrgType.Admin, OrgType.Support),
    );
    const res = await app.request("/test");
    expect(res.status).toBe(200);
  });

  it("should reject when orgType does not match", async () => {
    const app = createApp(sessionWith("org-1", "customer", "owner"), requireOrgType(OrgType.Admin));
    const res = await app.request("/test");
    expect(res.status).toBe(403);
    const body = await res.json();
    expect(body.messages[0]).toContain("middleware_errors_ORG_TYPE_NOT_AUTHORIZED");
  });

  it("should reject with 401 when no session", async () => {
    const app = createApp(null, requireOrgType(OrgType.Admin));
    const res = await app.request("/test");
    expect(res.status).toBe(401);
  });
});

describe("requireRole", () => {
  it("should pass when role equals minRole", async () => {
    const app = createApp(sessionWith("org-1", "customer", "officer"), requireRole("officer"));
    const res = await app.request("/test");
    expect(res.status).toBe(200);
  });

  it("should pass when role is above minRole", async () => {
    const app = createApp(sessionWith("org-1", "customer", "owner"), requireRole("officer"));
    const res = await app.request("/test");
    expect(res.status).toBe(200);
  });

  it("should reject when role is below minRole", async () => {
    const app = createApp(sessionWith("org-1", "customer", "agent"), requireRole("officer"));
    const res = await app.request("/test");
    expect(res.status).toBe(403);
    const body = await res.json();
    expect(body.messages[0]).toContain("middleware_errors_INSUFFICIENT_ROLE");
  });

  it("should reject auditor when minRole is agent", async () => {
    const app = createApp(sessionWith("org-1", "customer", "auditor"), requireRole("agent"));
    const res = await app.request("/test");
    expect(res.status).toBe(403);
  });

  it("should pass auditor when minRole is auditor", async () => {
    const app = createApp(sessionWith("org-1", "customer", "auditor"), requireRole("auditor"));
    const res = await app.request("/test");
    expect(res.status).toBe(200);
  });

  it("should reject with 401 when no session", async () => {
    const app = createApp(null, requireRole("agent"));
    const res = await app.request("/test");
    expect(res.status).toBe(401);
  });
});

describe("requireStaff", () => {
  it("should pass for admin orgType", async () => {
    const app = createApp(sessionWith("org-1", "admin", "owner"), requireStaff());
    const res = await app.request("/test");
    expect(res.status).toBe(200);
  });

  it("should pass for support orgType", async () => {
    const app = createApp(sessionWith("org-1", "support", "officer"), requireStaff());
    const res = await app.request("/test");
    expect(res.status).toBe(200);
  });

  it("should reject customer orgType", async () => {
    const app = createApp(sessionWith("org-1", "customer", "owner"), requireStaff());
    const res = await app.request("/test");
    expect(res.status).toBe(403);
  });
});

describe("requireAdmin", () => {
  it("should pass for admin orgType", async () => {
    const app = createApp(sessionWith("org-1", "admin", "owner"), requireAdmin());
    const res = await app.request("/test");
    expect(res.status).toBe(200);
  });

  it("should reject support orgType", async () => {
    const app = createApp(sessionWith("org-1", "support", "officer"), requireAdmin());
    const res = await app.request("/test");
    expect(res.status).toBe(403);
  });
});

describe("requireOrg — defensive edge cases", () => {
  it("should reject empty string orgType", async () => {
    const app = createApp(
      {
        activeOrganizationId: "org-1",
        activeOrganizationType: "",
        activeOrganizationRole: "owner",
      },
      requireOrg(),
    );
    const res = await app.request("/test");
    expect(res.status).toBe(403);
  });

  it("should reject empty string role", async () => {
    const app = createApp(
      {
        activeOrganizationId: "org-1",
        activeOrganizationType: "customer",
        activeOrganizationRole: "",
      },
      requireOrg(),
    );
    const res = await app.request("/test");
    expect(res.status).toBe(403);
  });

  it("should reject PascalCase orgType (case sensitivity)", async () => {
    const app = createApp(
      {
        activeOrganizationId: "org-1",
        activeOrganizationType: "Admin",
        activeOrganizationRole: "owner",
      },
      requireOrg(),
    );
    const res = await app.request("/test");
    expect(res.status).toBe(403);
  });

  it("should reject UPPERCASE orgType", async () => {
    const app = createApp(
      {
        activeOrganizationId: "org-1",
        activeOrganizationType: "CUSTOMER",
        activeOrganizationRole: "owner",
      },
      requireOrg(),
    );
    const res = await app.request("/test");
    expect(res.status).toBe(403);
  });

  it("should reject PascalCase role (case sensitivity)", async () => {
    const app = createApp(
      {
        activeOrganizationId: "org-1",
        activeOrganizationType: "customer",
        activeOrganizationRole: "Owner",
      },
      requireOrg(),
    );
    const res = await app.request("/test");
    expect(res.status).toBe(403);
  });

  it("should reject numeric orgType", async () => {
    const app = createApp(
      { activeOrganizationId: "org-1", activeOrganizationType: 1, activeOrganizationRole: "owner" },
      requireOrg(),
    );
    const res = await app.request("/test");
    expect(res.status).toBe(403);
  });

  it("should reject numeric role", async () => {
    const app = createApp(
      {
        activeOrganizationId: "org-1",
        activeOrganizationType: "customer",
        activeOrganizationRole: 3,
      },
      requireOrg(),
    );
    const res = await app.request("/test");
    expect(res.status).toBe(403);
  });

  it("should reject when orgId is present but orgType is undefined", async () => {
    const app = createApp(
      { activeOrganizationId: "org-1", activeOrganizationRole: "owner" },
      requireOrg(),
    );
    const res = await app.request("/test");
    expect(res.status).toBe(403);
  });

  it("should reject when orgId is present but role is undefined", async () => {
    const app = createApp(
      { activeOrganizationId: "org-1", activeOrganizationType: "customer" },
      requireOrg(),
    );
    const res = await app.request("/test");
    expect(res.status).toBe(403);
  });

  it("should reject null orgType", async () => {
    const app = createApp(
      {
        activeOrganizationId: "org-1",
        activeOrganizationType: null,
        activeOrganizationRole: "owner",
      },
      requireOrg(),
    );
    const res = await app.request("/test");
    expect(res.status).toBe(403);
  });

  it("should reject whitespace-only orgType", async () => {
    const app = createApp(
      {
        activeOrganizationId: "org-1",
        activeOrganizationType: "  ",
        activeOrganizationRole: "owner",
      },
      requireOrg(),
    );
    const res = await app.request("/test");
    expect(res.status).toBe(403);
  });
});

describe("requireRole — defensive edge cases", () => {
  it("should reject empty string role", async () => {
    const app = createApp(sessionWith("org-1", "customer", ""), requireRole("auditor"));
    const res = await app.request("/test");
    expect(res.status).toBe(403);
  });

  it("should reject PascalCase role when expecting lowercase", async () => {
    const app = createApp(sessionWith("org-1", "customer", "Officer"), requireRole("officer"));
    const res = await app.request("/test");
    expect(res.status).toBe(403);
  });

  it("should reject role with leading/trailing whitespace", async () => {
    const app = createApp(sessionWith("org-1", "customer", " owner "), requireRole("officer"));
    const res = await app.request("/test");
    expect(res.status).toBe(403);
  });

  it("should reject fabricated high-privilege role name", async () => {
    const app = createApp(sessionWith("org-1", "customer", "superadmin"), requireRole("auditor"));
    const res = await app.request("/test");
    expect(res.status).toBe(403);
  });

  it("should reject 'admin' as a role name (it is an orgType, not a role)", async () => {
    const app = createApp(sessionWith("org-1", "customer", "admin"), requireRole("auditor"));
    const res = await app.request("/test");
    expect(res.status).toBe(403);
  });
});

describe("requireOrgType — defensive edge cases", () => {
  it("should reject PascalCase orgType when expecting lowercase", async () => {
    const app = createApp(sessionWith("org-1", "Admin", "owner"), requireOrgType(OrgType.Admin));
    const res = await app.request("/test");
    expect(res.status).toBe(403);
  });

  it("should reject empty string orgType", async () => {
    const app = createApp(sessionWith("org-1", "", "owner"), requireOrgType(OrgType.Admin));
    const res = await app.request("/test");
    expect(res.status).toBe(403);
  });

  it("should reject orgType with trailing whitespace", async () => {
    const app = createApp(sessionWith("org-1", "admin ", "owner"), requireOrgType(OrgType.Admin));
    const res = await app.request("/test");
    expect(res.status).toBe(403);
  });
});

describe("middleware composition", () => {
  it("should pass when all composed middleware checks succeed", async () => {
    const app = createApp(
      sessionWith("org-1", "admin", "officer"),
      requireOrg(),
      requireStaff(),
      requireRole("officer"),
    );
    const res = await app.request("/test");
    expect(res.status).toBe(200);
  });

  it("should reject at first failing middleware in chain", async () => {
    // requireStaff() should fail (customer), even though role is sufficient
    const app = createApp(
      sessionWith("org-1", "customer", "owner"),
      requireOrg(),
      requireStaff(),
      requireRole("officer"),
    );
    const res = await app.request("/test");
    expect(res.status).toBe(403);
    const body = await res.json();
    expect(body.messages[0]).toContain("middleware_errors_ORG_TYPE_NOT_AUTHORIZED");
  });

  it("should reject at role check when orgType passes but role is too low", async () => {
    const app = createApp(
      sessionWith("org-1", "admin", "agent"),
      requireOrg(),
      requireStaff(),
      requireRole("officer"),
    );
    const res = await app.request("/test");
    expect(res.status).toBe(403);
    const body = await res.json();
    expect(body.messages[0]).toContain("middleware_errors_INSUFFICIENT_ROLE");
  });
});
