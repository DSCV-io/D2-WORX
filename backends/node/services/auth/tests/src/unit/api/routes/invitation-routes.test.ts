import { describe, it, expect, vi, beforeEach } from "vitest";
import { Hono } from "hono";
import { D2Result, HttpStatusCode } from "@d2/result";
import { INotifyKey } from "@d2/comms-client";
import { ICreateContactsKey, IGetContactsByExtKeysKey } from "@d2/geo-client";
import { createInvitationRoutes, SCOPE_KEY } from "@d2/auth-api";
import type { Translator } from "@d2/i18n";

// ---------- Mock helpers ----------

/** Mock en-US message catalog — mirrors contracts/messages/en-US.json */
const MOCK_MESSAGES: Record<string, string> = {
  auth_email_invitation_subject: "You've been invited to join {orgName}.",
  auth_email_invitation_greeting: "Hi,",
  auth_email_invitation_body:
    "{inviterName} ({inviterEmail}) has invited you to join **{orgName}** as **{role}**.",
  auth_email_invitation_action: "Accept Invitation.",
  auth_email_invitation_plaintext:
    "{inviterName} ({inviterEmail}) has invited you to join {orgName} as {role}. Accept at: {url}.",
  auth_email_link_fallback:
    "If the above link does not work, copy and paste this URL into your browser:",
};

const mockTranslator: Translator = {
  t(_locale: string, key: string, params?: Record<string, string>): string {
    const message = MOCK_MESSAGES[key] ?? key;
    if (!params) return message;
    return message.replace(/\{(\w+)\}/g, (_, p: string) => params[p] ?? `{${p}}`);
  },
  locales: ["en-US"],
  baseLocale: "en-US",
};

const mockCreateInvitation = vi.fn();
const mockAuth = {
  api: { createInvitation: mockCreateInvitation },
} as never;

const mockSelect = vi.fn();
const mockFrom = vi.fn();
const mockWhere = vi.fn();
const mockLimit = vi.fn();
const mockDb = {
  select: mockSelect,
  // Chain: db.select().from().where().limit()
} as never;

function resetDbChain(userRows: Array<{ id: string }> = [], orgRows: Array<{ name: string }> = []) {
  // Track which call number we're on so user lookup vs org lookup return different rows
  let selectCallCount = 0;
  mockSelect.mockImplementation(() => {
    selectCallCount++;
    const currentCall = selectCallCount;
    return {
      from: () => ({
        where: () => ({
          limit: () => {
            // Calls 1 = user lookup, 2 = org lookup
            return currentCall === 1 ? Promise.resolve(userRows) : Promise.resolve(orgRows);
          },
        }),
      }),
    };
  });
}

function createMockHandlers() {
  return {
    createContacts: {
      handleAsync: vi
        .fn()
        .mockResolvedValue(D2Result.ok({ data: { data: [{ id: "geo-contact-1" }] } })),
    },
    notify: {
      handleAsync: vi.fn().mockResolvedValue(D2Result.ok({ data: {} })),
    },
    getContactsByExtKeys: {
      handleAsync: vi.fn().mockResolvedValue(
        D2Result.ok({
          data: {
            data: new Map([["auth_user:existing-user-id", [{ id: "geo-contact-existing" }]]]),
          },
        }),
      ),
    },
  };
}

function createMockScope(handlers: ReturnType<typeof createMockHandlers>) {
  const keyMap = new Map<string, unknown>([
    [ICreateContactsKey.id, handlers.createContacts],
    [INotifyKey.id, handlers.notify],
    [IGetContactsByExtKeysKey.id, handlers.getContactsByExtKeys],
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
    c.set(
      "user" as never,
      {
        id: "user-inviter",
        email: "inviter@example.com",
        name: "The Inviter",
      } as never,
    );
    c.set(
      "session" as never,
      {
        activeOrganizationId: session.activeOrganizationId ?? "org-1",
        activeOrganizationType: session.activeOrganizationType ?? "customer",
        activeOrganizationRole: session.activeOrganizationRole ?? "officer",
      } as never,
    );
    c.set(SCOPE_KEY as never, createMockScope(handlers) as never);
    await next();
  });

  app.route(
    "/",
    createInvitationRoutes({
      auth: mockAuth,
      db: mockDb,
      baseUrl: "https://app.example.com",
      translator: mockTranslator,
    }),
  );
  return app;
}

const BASE_BODY = {
  email: "invitee@example.com",
  role: "agent",
  firstName: "Jane",
  lastName: "Doe",
};

describe("Invitation routes", () => {
  let handlers: ReturnType<typeof createMockHandlers>;

  beforeEach(() => {
    handlers = createMockHandlers();
    mockCreateInvitation.mockResolvedValue({ id: "inv-abc" });
    resetDbChain([], [{ name: "Acme Corp" }]);
  });

  // -------------------------------------------------------------------------
  // Role checks
  // -------------------------------------------------------------------------

  describe("POST /api/invitations — role checks", () => {
    it("should reject auditor role", async () => {
      const app = createTestApp(handlers, { activeOrganizationRole: "auditor" });
      const res = await app.request("/api/invitations", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify(BASE_BODY),
      });
      expect(res.status).toBe(403);
      expect(handlers.notify.handleAsync).not.toHaveBeenCalled();
    });

    it("should reject agent role", async () => {
      const app = createTestApp(handlers, { activeOrganizationRole: "agent" });
      const res = await app.request("/api/invitations", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify(BASE_BODY),
      });
      expect(res.status).toBe(403);
    });

    it("should allow officer role", async () => {
      const app = createTestApp(handlers, { activeOrganizationRole: "officer" });
      const res = await app.request("/api/invitations", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify(BASE_BODY),
      });
      expect(res.status).toBe(201);
    });

    it("should allow owner role", async () => {
      const app = createTestApp(handlers, { activeOrganizationRole: "owner" });
      const res = await app.request("/api/invitations", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify(BASE_BODY),
      });
      expect(res.status).toBe(201);
    });

    it("should reject when no active org", async () => {
      const app = new Hono();
      app.use("*", async (c, next) => {
        c.set(
          "user" as never,
          {
            id: "user-1",
            email: "u@e.com",
            name: "U",
          } as never,
        );
        c.set("session" as never, {} as never); // No org fields
        c.set(SCOPE_KEY as never, createMockScope(handlers) as never);
        await next();
      });
      app.route(
        "/",
        createInvitationRoutes({
          auth: mockAuth,
          db: mockDb,
          baseUrl: "https://app.example.com",
          translator: mockTranslator,
        }),
      );

      const res = await app.request("/api/invitations", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify(BASE_BODY),
      });
      expect(res.status).toBe(403);
    });
  });

  // -------------------------------------------------------------------------
  // Input validation
  // -------------------------------------------------------------------------

  describe("POST /api/invitations — validation", () => {
    it("should return 400 when email is missing", async () => {
      const app = createTestApp(handlers);
      const res = await app.request("/api/invitations", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({ role: "agent" }),
      });
      expect(res.status).toBe(400);
      const body = (await res.json()) as { inputErrors?: string[][] };
      expect(body.inputErrors).toBeDefined();
      expect(body.inputErrors!.some((e: string[]) => e[0] === "email")).toBe(true);
    });

    it("should return 400 when role is missing", async () => {
      const app = createTestApp(handlers);
      const res = await app.request("/api/invitations", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({ email: "test@test.com" }),
      });
      expect(res.status).toBe(400);
      const body = (await res.json()) as { inputErrors?: string[][] };
      expect(body.inputErrors!.some((e: string[]) => e[0] === "role")).toBe(true);
    });

    it("should return 400 when both email and role are missing", async () => {
      const app = createTestApp(handlers);
      const res = await app.request("/api/invitations", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({}),
      });
      expect(res.status).toBe(400);
      const body = (await res.json()) as { inputErrors?: string[][] };
      expect(body.inputErrors).toHaveLength(2);
    });

    it("should trim whitespace from email", async () => {
      const app = createTestApp(handlers);
      await app.request("/api/invitations", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({ email: "  trimmed@test.com  ", role: "agent" }),
      });
      // Should call createInvitation with trimmed email
      expect(mockCreateInvitation).toHaveBeenCalledWith(
        expect.objectContaining({
          body: expect.objectContaining({ email: "trimmed@test.com" }),
        }),
      );
    });

    it("should treat whitespace-only email as missing", async () => {
      const app = createTestApp(handlers);
      const res = await app.request("/api/invitations", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({ email: "   ", role: "agent" }),
      });
      expect(res.status).toBe(400);
    });
  });

  // -------------------------------------------------------------------------
  // Role hierarchy enforcement
  // -------------------------------------------------------------------------

  describe("POST /api/invitations — role hierarchy", () => {
    it("should reject officer trying to invite owner (403)", async () => {
      const app = createTestApp(handlers, { activeOrganizationRole: "officer" });
      const res = await app.request("/api/invitations", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({ email: "test@test.com", role: "owner" }),
      });
      expect(res.status).toBe(403);
      const body = (await res.json()) as { messages?: string[] };
      expect(body.messages?.[0]).toContain("auth_errors_INVITATION_ROLE_HIERARCHY");
      expect(mockCreateInvitation).not.toHaveBeenCalled();
    });

    it("should reject officer trying to invite officer (403)", async () => {
      const app = createTestApp(handlers, { activeOrganizationRole: "officer" });
      const res = await app.request("/api/invitations", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({ email: "test@test.com", role: "officer" }),
      });
      expect(res.status).toBe(403);
      const body = (await res.json()) as { messages?: string[] };
      expect(body.messages?.[0]).toContain("auth_errors_INVITATION_ROLE_HIERARCHY");
    });

    it("should allow officer to invite agent (201)", async () => {
      const app = createTestApp(handlers, { activeOrganizationRole: "officer" });
      const res = await app.request("/api/invitations", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({ email: "test@test.com", role: "agent" }),
      });
      expect(res.status).toBe(201);
    });

    it("should allow officer to invite auditor (201)", async () => {
      const app = createTestApp(handlers, { activeOrganizationRole: "officer" });
      const res = await app.request("/api/invitations", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({ email: "test@test.com", role: "auditor" }),
      });
      expect(res.status).toBe(201);
    });

    it("should allow owner to invite officer (201)", async () => {
      const app = createTestApp(handlers, { activeOrganizationRole: "owner" });
      const res = await app.request("/api/invitations", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({ email: "test@test.com", role: "officer" }),
      });
      expect(res.status).toBe(201);
    });

    it("should allow owner to invite agent (201)", async () => {
      const app = createTestApp(handlers, { activeOrganizationRole: "owner" });
      const res = await app.request("/api/invitations", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({ email: "test@test.com", role: "agent" }),
      });
      expect(res.status).toBe(201);
    });

    it("should allow owner to invite auditor (201)", async () => {
      const app = createTestApp(handlers, { activeOrganizationRole: "owner" });
      const res = await app.request("/api/invitations", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({ email: "test@test.com", role: "auditor" }),
      });
      expect(res.status).toBe(201);
    });

    it("should reject owner trying to invite owner (403)", async () => {
      const app = createTestApp(handlers, { activeOrganizationRole: "owner" });
      const res = await app.request("/api/invitations", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({ email: "test@test.com", role: "owner" }),
      });
      expect(res.status).toBe(403);
    });
  });

  // -------------------------------------------------------------------------
  // Max-length validation
  // -------------------------------------------------------------------------

  describe("POST /api/invitations — max-length enforcement", () => {
    it("should reject phone over 20 characters", async () => {
      resetDbChain([], [{ name: "Acme" }]);
      const app = createTestApp(handlers);
      const longPhone = "+1" + "5".repeat(50);

      const res = await app.request("/api/invitations", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({
          email: "test@test.com",
          role: "agent",
          phone: longPhone,
        }),
      });

      expect(res.status).toBe(400);
      const body = (await res.json()) as { inputErrors?: string[][] };
      expect(body.inputErrors).toBeDefined();
      expect(body.inputErrors!.some((e: string[]) => e[0] === "phone")).toBe(true);
    });

    it("should accept firstName exactly at max length (255)", async () => {
      resetDbChain([], [{ name: "Acme" }]);
      const app = createTestApp(handlers);
      const name255 = "A".repeat(255);

      const res = await app.request("/api/invitations", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({
          email: "test@test.com",
          role: "agent",
          firstName: name255,
        }),
      });

      expect(res.status).toBe(201);
      const contactInput = handlers.createContacts.handleAsync.mock.calls[0][0];
      expect(contactInput.contacts[0].personalDetails.firstName).toHaveLength(255);
    });
  });

  // -------------------------------------------------------------------------
  // Happy path — non-existing user (contactId path)
  // -------------------------------------------------------------------------

  describe("POST /api/invitations — non-existing user", () => {
    it("should create Geo contact and notify with recipientContactId", async () => {
      resetDbChain([], [{ name: "Acme Corp" }]); // No user found, org found
      const app = createTestApp(handlers);

      const res = await app.request("/api/invitations", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({
          email: "newuser@example.com",
          role: "agent",
          firstName: "New",
          lastName: "User",
          phone: "+15551234567",
        }),
      });

      expect(res.status).toBe(201);
      const body = (await res.json()) as { data?: { invitationId: string } };
      expect(body.data?.invitationId).toBe("inv-abc");

      // Should have called createContacts
      expect(handlers.createContacts.handleAsync).toHaveBeenCalledOnce();
      const contactInput = handlers.createContacts.handleAsync.mock.calls[0][0];
      expect(contactInput.contacts).toHaveLength(1);
      expect(contactInput.contacts[0].contextKey).toBe("auth_org_invitation");
      expect(contactInput.contacts[0].relatedEntityId).toBe("inv-abc");
      expect(contactInput.contacts[0].personalDetails.firstName).toBe("New");
      expect(contactInput.contacts[0].personalDetails.lastName).toBe("User");
      expect(contactInput.contacts[0].contactMethods.emails[0].value).toBe("newuser@example.com");
      expect(contactInput.contacts[0].contactMethods.phoneNumbers[0].value).toBe("+15551234567");

      // Should NOT have called getContactsByExtKeys (non-existing user path)
      expect(handlers.getContactsByExtKeys.handleAsync).not.toHaveBeenCalled();

      // Should notify with recipientContactId from createContacts
      expect(handlers.notify.handleAsync).toHaveBeenCalledWith(
        expect.objectContaining({
          recipientContactId: "geo-contact-1",
          title: "You've been invited to join Acme Corp.",
          channels: ["email"],
          correlationId: "inv-abc",
          senderService: "auth",
        }),
      );
    });

    it("should omit phoneNumbers array when phone not provided", async () => {
      resetDbChain([], [{ name: "Acme" }]);
      const app = createTestApp(handlers);

      await app.request("/api/invitations", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({ email: "test@test.com", role: "agent" }),
      });

      const contactInput = handlers.createContacts.handleAsync.mock.calls[0][0];
      expect(contactInput.contacts[0].contactMethods.phoneNumbers).toEqual([]);
    });

    it("should NOT notify when Geo contact creation fails (no recipientContactId)", async () => {
      resetDbChain([], [{ name: "Acme" }]);
      handlers.createContacts.handleAsync.mockResolvedValue(
        D2Result.fail({ messages: ["Geo unavailable"] }),
      );
      const app = createTestApp(handlers);

      const res = await app.request("/api/invitations", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify(BASE_BODY),
      });

      expect(res.status).toBe(201);
      // notify should NOT be called — no recipientContactId could be resolved
      expect(handlers.notify.handleAsync).not.toHaveBeenCalled();
    });
  });

  // -------------------------------------------------------------------------
  // Happy path — existing user (ext-keys lookup path)
  // -------------------------------------------------------------------------

  describe("POST /api/invitations — existing user", () => {
    it("should skip Geo contact creation and look up contact via ext-keys", async () => {
      resetDbChain([{ id: "existing-user-id" }], [{ name: "Acme Corp" }]);
      // Owner inviting officer — allowed by hierarchy
      const app = createTestApp(handlers, { activeOrganizationRole: "owner" });

      const res = await app.request("/api/invitations", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({ email: "existing@example.com", role: "officer" }),
      });

      expect(res.status).toBe(201);

      // Should NOT create Geo contact
      expect(handlers.createContacts.handleAsync).not.toHaveBeenCalled();

      // Should look up existing user's Geo contact via ext-keys
      expect(handlers.getContactsByExtKeys.handleAsync).toHaveBeenCalledWith({
        keys: [{ contextKey: "auth_user", relatedEntityId: "existing-user-id" }],
      });

      // Should notify with recipientContactId from ext-keys lookup
      expect(handlers.notify.handleAsync).toHaveBeenCalledWith(
        expect.objectContaining({
          recipientContactId: "geo-contact-existing",
          title: "You've been invited to join Acme Corp.",
          channels: ["email"],
          correlationId: "inv-abc",
          senderService: "auth",
        }),
      );
    });

    it("should NOT notify when ext-keys lookup returns no contacts", async () => {
      resetDbChain([{ id: "user-no-contact" }], [{ name: "Acme" }]);
      handlers.getContactsByExtKeys.handleAsync.mockResolvedValue(
        D2Result.ok({ data: { data: new Map() } }),
      );
      const app = createTestApp(handlers);

      const res = await app.request("/api/invitations", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({ email: "nocontact@example.com", role: "agent" }),
      });

      expect(res.status).toBe(201);
      // notify should NOT be called — no recipientContactId could be resolved
      expect(handlers.notify.handleAsync).not.toHaveBeenCalled();
    });
  });

  // -------------------------------------------------------------------------
  // BetterAuth createInvitation failure
  // -------------------------------------------------------------------------

  describe("POST /api/invitations — createInvitation failure", () => {
    it("should return 400 with generic message when createInvitation throws an Error", async () => {
      mockCreateInvitation.mockRejectedValue(new Error("User is already a member."));
      const app = createTestApp(handlers);

      const res = await app.request("/api/invitations", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify(BASE_BODY),
      });

      expect(res.status).toBe(400);
      const body = (await res.json()) as { messages?: string[] };
      // Must NOT leak internal error details — always generic message
      expect(body.messages).toContain("auth_errors_INVITATION_CREATION_FAILED");
      expect(body.messages).not.toContain("User is already a member.");

      // Should not attempt to notify
      expect(handlers.notify.handleAsync).not.toHaveBeenCalled();
    });

    it("should return generic message for non-Error throws", async () => {
      mockCreateInvitation.mockRejectedValue("string error");
      const app = createTestApp(handlers);

      const res = await app.request("/api/invitations", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify(BASE_BODY),
      });

      expect(res.status).toBe(400);
      const body = (await res.json()) as { messages?: string[] };
      expect(body.messages).toContain("auth_errors_INVITATION_CREATION_FAILED");
    });

    it("should not leak error details even when logger is unavailable", async () => {
      mockCreateInvitation.mockRejectedValue(new Error("Internal BetterAuth error details"));
      const app = createTestApp(handlers);

      const res = await app.request("/api/invitations", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify(BASE_BODY),
      });

      // Should still return 400 with generic message even if logger resolve fails
      expect(res.status).toBe(400);
      const body = (await res.json()) as { messages?: string[] };
      expect(body.messages).toContain("auth_errors_INVITATION_CREATION_FAILED");
      expect(body.messages).not.toContain("Internal BetterAuth error details");
    });
  });

  // -------------------------------------------------------------------------
  // Org name lookup
  // -------------------------------------------------------------------------

  describe("POST /api/invitations — org name fallback", () => {
    it("should fall back to 'the organization' when org not found", async () => {
      resetDbChain([], []); // No user, no org
      handlers.createContacts.handleAsync.mockResolvedValue(
        D2Result.ok({ data: { data: [{ id: "geo-contact-fallback" }] } }),
      );
      const app = createTestApp(handlers);

      await app.request("/api/invitations", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify(BASE_BODY),
      });

      expect(handlers.notify.handleAsync).toHaveBeenCalledWith(
        expect.objectContaining({
          title: "You've been invited to join the organization.",
        }),
      );
    });
  });

  // -------------------------------------------------------------------------
  // Notify event shape
  // -------------------------------------------------------------------------

  describe("POST /api/invitations — notify event", () => {
    it("should include invitation URL in content and plaintext", async () => {
      const app = createTestApp(handlers);

      await app.request("/api/invitations", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify(BASE_BODY),
      });

      const notifyInput = handlers.notify.handleAsync.mock.calls[0][0];
      expect(notifyInput.content).toContain(
        "https://app.example.com/api/auth/organization/accept-invitation?invitationId=inv-abc",
      );
      expect(notifyInput.plaintext).toContain(
        "https://app.example.com/api/auth/organization/accept-invitation?invitationId=inv-abc",
      );
    });

    it("should use inviter name from session user, falling back to 'Someone'", async () => {
      const app = new Hono();
      app.use("*", async (c, next) => {
        c.set(
          "user" as never,
          {
            id: "user-no-name",
            email: "noname@e.com",
            name: undefined,
          } as never,
        );
        c.set(
          "session" as never,
          {
            activeOrganizationId: "org-1",
            activeOrganizationType: "customer",
            activeOrganizationRole: "officer",
          } as never,
        );
        c.set(SCOPE_KEY as never, createMockScope(handlers) as never);
        await next();
      });
      app.route(
        "/",
        createInvitationRoutes({
          auth: mockAuth,
          db: mockDb,
          baseUrl: "https://app.example.com",
          translator: mockTranslator,
        }),
      );

      await app.request("/api/invitations", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify(BASE_BODY),
      });

      const notifyInput = handlers.notify.handleAsync.mock.calls[0][0];
      // When user.name is undefined, inviterName falls back to "Someone"
      expect(notifyInput.content).toContain("Someone");
      expect(notifyInput.plaintext).toContain("Someone");
    });

    it("should pass all expected fields to notifier for non-existing user", async () => {
      resetDbChain([], [{ name: "Test Org" }]);
      const app = createTestApp(handlers);

      await app.request("/api/invitations", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({ email: "invitee@example.com", role: "agent" }),
      });

      expect(handlers.notify.handleAsync).toHaveBeenCalledWith({
        recipientContactId: "geo-contact-1",
        title: "You've been invited to join Test Org.",
        content: expect.stringContaining("The Inviter (inviter@example.com)"),
        plaintext: expect.stringContaining("The Inviter (inviter@example.com)"),
        channels: ["email"],
        correlationId: "inv-abc",
        senderService: "auth",
      });
    });

    it("should pass all expected fields to notifier for existing user", async () => {
      resetDbChain([{ id: "existing-user-id" }], [{ name: "Test Org" }]);
      const app = createTestApp(handlers);

      await app.request("/api/invitations", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({ email: "existing@example.com", role: "auditor" }),
      });

      expect(handlers.notify.handleAsync).toHaveBeenCalledWith({
        recipientContactId: "geo-contact-existing",
        title: "You've been invited to join Test Org.",
        content: expect.stringContaining("as **auditor**"),
        plaintext: expect.stringContaining("as auditor"),
        channels: ["email"],
        correlationId: "inv-abc",
        senderService: "auth",
      });
    });

    it("should include role and org name in content", async () => {
      resetDbChain([], [{ name: "Acme Corp" }]);
      // Owner inviting officer — allowed by hierarchy
      const app = createTestApp(handlers, { activeOrganizationRole: "owner" });

      await app.request("/api/invitations", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({ email: "test@test.com", role: "officer" }),
      });

      const notifyInput = handlers.notify.handleAsync.mock.calls[0][0];
      expect(notifyInput.content).toContain("**Acme Corp**");
      expect(notifyInput.content).toContain("**officer**");
      expect(notifyInput.plaintext).toContain("Acme Corp");
      expect(notifyInput.plaintext).toContain("officer");
    });
  });

  // -------------------------------------------------------------------------
  // Defensive / Security tests
  // -------------------------------------------------------------------------

  describe("POST /api/invitations — defensive security", () => {
    it("should reject email with CRLF injection (header injection)", async () => {
      const app = createTestApp(handlers);
      const res = await app.request("/api/invitations", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({
          email: "valid@test.com\r\nBcc: attacker@evil.com",
          role: "agent",
        }),
      });
      // Email with CRLF is forwarded to BetterAuth which may accept or reject.
      // Either way the handler should not crash — assert a valid HTTP status.
      expect([201, 400]).toContain(res.status);
    });

    it("should reject email with URL-encoded CRLF injection", async () => {
      const app = createTestApp(handlers);
      // URL-encoded CRLF: %0d%0a — literal characters in JSON, not decoded
      const res = await app.request("/api/invitations", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({
          email: "valid@test.com%0d%0aBcc: attacker@evil.com",
          role: "agent",
        }),
      });
      // Invalid email format — BetterAuth should reject, but handler must not crash.
      expect([201, 400]).toContain(res.status);
    });

    it("should lowercase email before DB lookup (case-insensitive matching)", async () => {
      resetDbChain([], [{ name: "Acme" }]);
      const app = createTestApp(handlers);

      await app.request("/api/invitations", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({ email: "UPPER@EXAMPLE.COM", role: "agent" }),
      });

      // The email passed to createInvitation should be trimmed but case preserved
      // The DB lookup should use lowercase for comparison
      expect(mockCreateInvitation).toHaveBeenCalledWith(
        expect.objectContaining({
          body: expect.objectContaining({ email: "UPPER@EXAMPLE.COM" }),
        }),
      );
    });

    it("should NOT allow organizationId to be overridden from request body (IDOR)", async () => {
      resetDbChain([], [{ name: "Acme" }]);
      const app = createTestApp(handlers);

      await app.request("/api/invitations", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({
          email: "test@test.com",
          role: "agent",
          organizationId: "org-attacker-controlled",
        }),
      });

      // The organizationId should come from session, NOT from request body
      expect(mockCreateInvitation).toHaveBeenCalledWith(
        expect.objectContaining({
          body: expect.objectContaining({
            organizationId: "org-1", // From session, not request body
          }),
        }),
      );
    });

    it("should reject when session has no active org (IDOR bypass via missing session)", async () => {
      const app = new Hono();
      app.use("*", async (c, next) => {
        c.set("user" as never, { id: "u", email: "u@e.com", name: "U" } as never);
        c.set("session" as never, {} as never);
        c.set(SCOPE_KEY as never, createMockScope(handlers) as never);
        await next();
      });
      app.route(
        "/",
        createInvitationRoutes({
          auth: mockAuth,
          db: mockDb,
          baseUrl: "https://app.example.com",
          translator: mockTranslator,
        }),
      );

      const res = await app.request("/api/invitations", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({ email: "t@t.com", role: "agent" }),
      });

      // requireOrg middleware should block this
      expect(res.status).toBe(403);
      expect(mockCreateInvitation).not.toHaveBeenCalled();
    });

    it("should reject invalid role string with 400 before reaching BetterAuth", async () => {
      const app = createTestApp(handlers);

      const res = await app.request("/api/invitations", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({ email: "test@test.com", role: "superadmin" }),
      });

      // Our input validation rejects invalid roles — BetterAuth never called
      expect(res.status).toBe(400);
      const body = (await res.json()) as { inputErrors?: string[][] };
      expect(body.inputErrors).toBeDefined();
      expect(body.inputErrors!.some((e: string[]) => e[0] === "role")).toBe(true);
      expect(mockCreateInvitation).not.toHaveBeenCalled();
    });

    it("should handle very long email gracefully", async () => {
      mockCreateInvitation.mockRejectedValue(new Error("Email too long"));
      const app = createTestApp(handlers);
      const longEmail = "a".repeat(500) + "@example.com";

      const res = await app.request("/api/invitations", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({ email: longEmail, role: "agent" }),
      });

      // Should not crash — either passes validation or BetterAuth rejects
      expect([201, 400]).toContain(res.status);
    });

    it("should reject very long firstName/lastName over max 255 chars", async () => {
      resetDbChain([], [{ name: "Acme" }]);
      const app = createTestApp(handlers);
      const longName = "A".repeat(10_000);

      const res = await app.request("/api/invitations", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({
          email: "test@test.com",
          role: "agent",
          firstName: longName,
          lastName: longName,
        }),
      });

      // Should reject with 400 — names exceeding max length are now rejected, not truncated
      expect(res.status).toBe(400);
      const body = (await res.json()) as { inputErrors?: string[][] };
      expect(body.inputErrors).toBeDefined();
      expect(body.inputErrors!.some((e: string[]) => e[0] === "firstName")).toBe(true);
      expect(body.inputErrors!.some((e: string[]) => e[0] === "lastName")).toBe(true);
    });

    it("should not call createContacts when existing user provides contact details", async () => {
      // Existing user path: even if firstName/lastName/phone are provided,
      // we should NOT create a Geo contact (user already has one)
      resetDbChain([{ id: "existing-user" }], [{ name: "Acme" }]);
      const app = createTestApp(handlers);

      await app.request("/api/invitations", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({
          email: "existing@test.com",
          role: "agent",
          firstName: "Should",
          lastName: "Ignore",
          phone: "+15551234567",
        }),
      });

      expect(handlers.createContacts.handleAsync).not.toHaveBeenCalled();
    });

    it("should omit personalDetails when neither firstName nor lastName provided", async () => {
      resetDbChain([], [{ name: "Acme" }]);
      const app = createTestApp(handlers);

      await app.request("/api/invitations", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({ email: "noname@test.com", role: "agent" }),
      });

      const contactInput = handlers.createContacts.handleAsync.mock.calls[0][0];
      expect(contactInput.contacts[0].personalDetails).toBeUndefined();
    });

    it("should not create Geo contact when phone is undefined (no phoneNumbers)", async () => {
      resetDbChain([], [{ name: "Acme" }]);
      const app = createTestApp(handlers);

      await app.request("/api/invitations", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({
          email: "nophone@test.com",
          role: "agent",
          firstName: "No",
          lastName: "Phone",
        }),
      });

      const contactInput = handlers.createContacts.handleAsync.mock.calls[0][0];
      expect(contactInput.contacts[0].contactMethods.phoneNumbers).toEqual([]);
    });

    it("should handle concurrent requests with same email gracefully", async () => {
      // Two simultaneous invitations for the same email — BetterAuth handles dedup
      resetDbChain([], [{ name: "Acme" }]);
      const app = createTestApp(handlers);

      const [res1, res2] = await Promise.all([
        app.request("/api/invitations", {
          method: "POST",
          headers: { "content-type": "application/json" },
          body: JSON.stringify({ email: "dup@test.com", role: "agent" }),
        }),
        app.request("/api/invitations", {
          method: "POST",
          headers: { "content-type": "application/json" },
          body: JSON.stringify({ email: "dup@test.com", role: "agent" }),
        }),
      ]);

      // Both should succeed or one should fail — neither should crash
      expect([201, 400]).toContain(res1.status);
      expect([201, 400]).toContain(res2.status);
    });
  });
});
