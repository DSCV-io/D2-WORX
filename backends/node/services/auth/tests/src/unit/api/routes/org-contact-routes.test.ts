import { describe, it, expect, vi, beforeEach } from "vitest";
import { Hono } from "hono";
import { D2Result, HttpStatusCode } from "@d2/result";
import {
  ICreateOrgContactKey,
  IUpdateOrgContactKey,
  IDeleteOrgContactKey,
  IGetOrgContactsKey,
} from "@d2/auth-app";
import { createOrgContactRoutes, SCOPE_KEY } from "@d2/auth-api";
import { OrgType } from "@d2/handler";

/** Maps the legacy lowercase org-type strings used by the test fixture to the OrgType enum. */
function toOrgTypeEnum(value: string): OrgType | undefined {
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

function createMockHandlers() {
  return {
    create: {
      handleAsync: vi
        .fn()
        .mockResolvedValue(
          D2Result.ok({ data: { contact: { id: "oc-1" }, geoContact: { id: "geo-1" } } }),
        ),
    },
    update: {
      handleAsync: vi.fn().mockResolvedValue(D2Result.ok({ data: { contact: { id: "oc-1" } } })),
    },
    delete: {
      handleAsync: vi.fn().mockResolvedValue(D2Result.ok({ data: {} })),
    },
    getByOrg: {
      handleAsync: vi.fn().mockResolvedValue(D2Result.ok({ data: { contacts: [] } })),
    },
  };
}

/** Creates a mock DI scope that returns the mock handlers on resolve(). */
function createMockScope(handlers: ReturnType<typeof createMockHandlers>) {
  const keyMap = new Map<string, unknown>([
    [ICreateOrgContactKey.id, handlers.create],
    [IUpdateOrgContactKey.id, handlers.update],
    [IDeleteOrgContactKey.id, handlers.delete],
    [IGetOrgContactsKey.id, handlers.getByOrg],
  ]);
  return {
    resolve: (key: { id: string }) => {
      const handler = keyMap.get(key.id);
      if (!handler) throw new Error(`No mock handler for key: ${key.id}`);
      return handler;
    },
  };
}

function createTestApp(
  handlers: ReturnType<typeof createMockHandlers>,
  session: {
    activeOrganizationId?: string;
    activeOrganizationType?: string;
    activeOrganizationRole?: string;
  } = {},
) {
  const app = new Hono();

  app.use("*", async (c, next) => {
    const orgId = session.activeOrganizationId ?? "org-1";
    const orgType = session.activeOrganizationType ?? "customer";
    const orgRole = session.activeOrganizationRole ?? "owner";
    c.set("user", { id: "user-123", email: "test@test.com", name: "Test" });
    c.set("session", {
      activeOrganizationId: orgId,
      activeOrganizationType: orgType,
      activeOrganizationRole: orgRole,
    });
    c.set(
      "requestContext" as never,
      {
        isAuthenticated: true,
        isTrustedService: false,
        isOrgEmulating: false,
        isUserImpersonating: false,
        userId: "user-123",
        email: "test@test.com",
        targetOrgId: orgId,
        targetOrgType: toOrgTypeEnum(orgType),
        targetOrgRole: orgRole,
        agentOrgId: orgId,
        agentOrgType: toOrgTypeEnum(orgType),
        agentOrgRole: orgRole,
      } as never,
    );
    // Mock DI scope — routes resolve handlers from c.get("scope")
    c.set(SCOPE_KEY as never, createMockScope(handlers) as never);
    await next();
  });

  app.route("/", createOrgContactRoutes());
  return app;
}

const VALID_UUID = "01234567-89ab-cdef-0123-456789abcdef";

const VALID_CONTACT_BODY = {
  label: "HQ",
  contact: {
    personalDetails: { firstName: "John", lastName: "Doe" },
    contactMethods: { emails: [{ value: "john@example.com" }] },
  },
};

describe("Org contact routes", () => {
  let handlers: ReturnType<typeof createMockHandlers>;

  beforeEach(() => {
    handlers = createMockHandlers();
  });

  describe("POST /api/org-contacts — role checks", () => {
    it("should reject auditor role on create", async () => {
      const app = createTestApp(handlers, { activeOrganizationRole: "auditor" });
      const res = await app.request("/api/org-contacts", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify(VALID_CONTACT_BODY),
      });
      expect(res.status).toBe(403);
      expect(handlers.create.handleAsync).not.toHaveBeenCalled();
    });

    it("should reject agent role on create", async () => {
      const app = createTestApp(handlers, { activeOrganizationRole: "agent" });
      const res = await app.request("/api/org-contacts", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify(VALID_CONTACT_BODY),
      });
      expect(res.status).toBe(403);
    });

    it("should allow officer role on create", async () => {
      const app = createTestApp(handlers, { activeOrganizationRole: "officer" });
      const res = await app.request("/api/org-contacts", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify(VALID_CONTACT_BODY),
      });
      expect(res.status).toBe(201);
    });

    it("should allow owner role on create", async () => {
      const app = createTestApp(handlers);
      const res = await app.request("/api/org-contacts", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify(VALID_CONTACT_BODY),
      });
      expect(res.status).toBe(201);
    });

    it("should reject spoofed role string", async () => {
      const app = createTestApp(handlers, { activeOrganizationRole: "root" });
      const res = await app.request("/api/org-contacts", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify(VALID_CONTACT_BODY),
      });
      expect(res.status).toBe(403);
    });
  });

  describe("POST /api/org-contacts — input mapping", () => {
    it("should use session orgId, ignoring any orgId in request body", async () => {
      const app = createTestApp(handlers, { activeOrganizationId: "session-org-1" });
      await app.request("/api/org-contacts", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({
          ...VALID_CONTACT_BODY,
          organizationId: "attacker-org-id",
        }),
      });
      expect(handlers.create.handleAsync).toHaveBeenCalledWith(
        expect.objectContaining({ organizationId: "session-org-1" }),
      );
    });

    it("should pass label, isPrimary, and contact from body", async () => {
      const app = createTestApp(handlers);
      await app.request("/api/org-contacts", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({
          label: "Branch Office",
          isPrimary: true,
          contact: {
            personalDetails: { firstName: "Jane" },
          },
        }),
      });
      expect(handlers.create.handleAsync).toHaveBeenCalledWith(
        expect.objectContaining({
          label: "Branch Office",
          isPrimary: true,
          contact: expect.objectContaining({
            personalDetails: expect.objectContaining({ firstName: "Jane" }),
          }),
        }),
      );
    });

    it("should default contact to empty object when not provided", async () => {
      const app = createTestApp(handlers);
      await app.request("/api/org-contacts", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({ label: "Minimal" }),
      });
      expect(handlers.create.handleAsync).toHaveBeenCalledWith(
        expect.objectContaining({ contact: {} }),
      );
    });
  });

  describe("POST /api/org-contacts — status code mapping", () => {
    it("should return 201 on success", async () => {
      const app = createTestApp(handlers);
      const res = await app.request("/api/org-contacts", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify(VALID_CONTACT_BODY),
      });
      expect(res.status).toBe(201);
    });

    it("should forward handler failure status code", async () => {
      handlers.create.handleAsync.mockResolvedValue(
        D2Result.fail({
          messages: ["Validation failed."],
          statusCode: HttpStatusCode.BadRequest,
        }),
      );
      const app = createTestApp(handlers);
      const res = await app.request("/api/org-contacts", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify(VALID_CONTACT_BODY),
      });
      expect(res.status).toBe(400);
    });
  });

  describe("PATCH /api/org-contacts/:id — role checks", () => {
    it("should reject auditor role on update", async () => {
      const app = createTestApp(handlers, { activeOrganizationRole: "auditor" });
      const res = await app.request(`/api/org-contacts/${VALID_UUID}`, {
        method: "PATCH",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({ label: "Updated" }),
      });
      expect(res.status).toBe(403);
      expect(handlers.update.handleAsync).not.toHaveBeenCalled();
    });

    it("should allow officer role on update", async () => {
      const app = createTestApp(handlers, { activeOrganizationRole: "officer" });
      const res = await app.request(`/api/org-contacts/${VALID_UUID}`, {
        method: "PATCH",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({ label: "Updated" }),
      });
      expect(res.status).toBe(200);
    });
  });

  describe("PATCH /api/org-contacts/:id — input mapping", () => {
    it("should pass id, session orgId, and updates to handler", async () => {
      const app = createTestApp(handlers, { activeOrganizationId: "my-org" });
      await app.request(`/api/org-contacts/${VALID_UUID}`, {
        method: "PATCH",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({ label: "Updated", isPrimary: false }),
      });
      expect(handlers.update.handleAsync).toHaveBeenCalledWith({
        id: VALID_UUID,
        organizationId: "my-org",
        updates: { label: "Updated", isPrimary: false, contact: undefined },
      });
    });

    it("should pass contact details in updates when provided", async () => {
      const app = createTestApp(handlers, { activeOrganizationId: "my-org" });
      await app.request(`/api/org-contacts/${VALID_UUID}`, {
        method: "PATCH",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({
          label: "New HQ",
          contact: {
            personalDetails: { firstName: "Updated" },
          },
        }),
      });
      expect(handlers.update.handleAsync).toHaveBeenCalledWith({
        id: VALID_UUID,
        organizationId: "my-org",
        updates: {
          label: "New HQ",
          isPrimary: undefined,
          contact: expect.objectContaining({
            personalDetails: expect.objectContaining({ firstName: "Updated" }),
          }),
        },
      });
    });
  });

  describe("DELETE /api/org-contacts/:id — role checks", () => {
    it("should reject auditor role on delete", async () => {
      const app = createTestApp(handlers, { activeOrganizationRole: "auditor" });
      const res = await app.request(`/api/org-contacts/${VALID_UUID}`, {
        method: "DELETE",
      });
      expect(res.status).toBe(403);
      expect(handlers.delete.handleAsync).not.toHaveBeenCalled();
    });

    it("should allow officer role on delete", async () => {
      const app = createTestApp(handlers, { activeOrganizationRole: "officer" });
      const res = await app.request(`/api/org-contacts/${VALID_UUID}`, {
        method: "DELETE",
      });
      expect(res.status).toBe(200);
    });
  });

  describe("DELETE /api/org-contacts/:id — input mapping", () => {
    it("should pass id and session orgId to handler", async () => {
      const app = createTestApp(handlers, { activeOrganizationId: "my-org" });
      await app.request(`/api/org-contacts/${VALID_UUID}`, {
        method: "DELETE",
      });
      expect(handlers.delete.handleAsync).toHaveBeenCalledWith({
        id: VALID_UUID,
        organizationId: "my-org",
      });
    });
  });

  describe("GET /api/org-contacts — read role access", () => {
    it("should allow auditor to read contacts (no role minimum)", async () => {
      const app = createTestApp(handlers, { activeOrganizationRole: "auditor" });
      const res = await app.request("/api/org-contacts");
      expect(res.status).toBe(200);
    });

    it("should allow agent to read contacts", async () => {
      const app = createTestApp(handlers, { activeOrganizationRole: "agent" });
      const res = await app.request("/api/org-contacts");
      expect(res.status).toBe(200);
    });

    it("should reject unrecognized role on read", async () => {
      const app = createTestApp(handlers, { activeOrganizationRole: "hacker" });
      const res = await app.request("/api/org-contacts");
      expect(res.status).toBe(403);
    });
  });

  describe("GET /api/org-contacts — pagination", () => {
    it("should apply default pagination", async () => {
      const app = createTestApp(handlers);
      await app.request("/api/org-contacts");
      expect(handlers.getByOrg.handleAsync).toHaveBeenCalledWith(
        expect.objectContaining({ limit: 50, offset: 0 }),
      );
    });

    it("should cap limit at 100", async () => {
      const app = createTestApp(handlers);
      await app.request("/api/org-contacts?limit=999");
      expect(handlers.getByOrg.handleAsync).toHaveBeenCalledWith(
        expect.objectContaining({ limit: 100 }),
      );
    });

    it("should handle negative offset gracefully", async () => {
      const app = createTestApp(handlers);
      await app.request("/api/org-contacts?offset=-10");
      expect(handlers.getByOrg.handleAsync).toHaveBeenCalledWith(
        expect.objectContaining({ offset: 0 }),
      );
    });
  });

  describe("No active organization", () => {
    it("should reject POST when no active org in session", async () => {
      const app = new Hono();
      app.use("*", async (c, next) => {
        c.set("user", { id: "user-123", email: "test@test.com", name: "Test" });
        c.set("session", {}); // No org fields
        c.set(
          "requestContext" as never,
          {
            isAuthenticated: true,
            isTrustedService: false,
            isOrgEmulating: false,
            isUserImpersonating: false,
            userId: "user-123",
            email: "test@test.com",
          } as never,
        );
        c.set(SCOPE_KEY as never, createMockScope(handlers) as never);
        await next();
      });
      app.route("/", createOrgContactRoutes());

      const res = await app.request("/api/org-contacts", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify(VALID_CONTACT_BODY),
      });
      expect(res.status).toBe(403);
      const body = await res.json();
      expect(body.messages[0]).toContain("middleware_errors_NO_ACTIVE_ORGANIZATION");
    });

    it("should reject GET when no active org in session", async () => {
      const app = new Hono();
      app.use("*", async (c, next) => {
        c.set("user", { id: "user-123", email: "test@test.com", name: "Test" });
        c.set("session", {});
        c.set(
          "requestContext" as never,
          {
            isAuthenticated: true,
            isTrustedService: false,
            isOrgEmulating: false,
            isUserImpersonating: false,
            userId: "user-123",
            email: "test@test.com",
          } as never,
        );
        c.set(SCOPE_KEY as never, createMockScope(handlers) as never);
        await next();
      });
      app.route("/", createOrgContactRoutes());

      const res = await app.request("/api/org-contacts");
      expect(res.status).toBe(403);
    });
  });
});
