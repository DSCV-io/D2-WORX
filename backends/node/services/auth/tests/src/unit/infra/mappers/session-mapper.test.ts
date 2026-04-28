import { describe, it, expect } from "vitest";
import { toDomainSession } from "@d2/auth-infra";

describe("toDomainSession", () => {
  it("should map a full BetterAuth session with custom extensions", () => {
    const raw = {
      id: "session-123",
      userId: "user-456",
      token: "token-abc",
      expiresAt: new Date("2026-02-15"),
      ipAddress: "192.168.1.1",
      userAgent: "Mozilla/5.0",
      createdAt: new Date("2026-02-08"),
      updatedAt: new Date("2026-02-08"),
      activeOrganizationId: "org-789",
      activeOrganizationType: "customer",
      activeOrganizationRole: "owner",
      emulatedOrganizationId: null,
      emulatedOrganizationType: null,
    };

    const session = toDomainSession(raw);

    expect(session.id).toBe("session-123");
    expect(session.userId).toBe("user-456");
    expect(session.token).toBe("token-abc");
    expect(session.activeOrganizationId).toBe("org-789");
    expect(session.activeOrganizationType).toBe("customer");
    expect(session.activeOrganizationRole).toBe("owner");
    expect(session.emulatedOrganizationId).toBeNull();
    expect(session.emulatedOrganizationType).toBeNull();
  });

  it("should map snake_case session fields", () => {
    const raw = {
      id: "session-456",
      user_id: "user-789",
      token: "token-def",
      expires_at: "2026-03-01T00:00:00Z",
      ip_address: "10.0.0.1",
      user_agent: "curl/7.0",
      created_at: "2026-02-08T00:00:00Z",
      updated_at: "2026-02-08T00:00:00Z",
      active_organization_id: "org-abc",
      active_organization_type: "support",
      active_organization_role: "agent",
      emulated_organization_id: "org-def",
      emulated_organization_type: "customer",
    };

    const session = toDomainSession(raw);

    expect(session.userId).toBe("user-789");
    expect(session.ipAddress).toBe("10.0.0.1");
    expect(session.activeOrganizationId).toBe("org-abc");
    expect(session.emulatedOrganizationId).toBe("org-def");
    expect(session.emulatedOrganizationType).toBe("customer");
  });

  it("should default optional fields to undefined and auth-state fields to null", () => {
    const raw = {
      id: "session-minimal",
      userId: "user-1",
      token: "token-min",
      expiresAt: new Date(),
      createdAt: new Date(),
      updatedAt: new Date(),
    };

    const session = toDomainSession(raw);

    expect(session.ipAddress).toBeUndefined();
    expect(session.userAgent).toBeUndefined();
    expect(session.activeOrganizationId).toBeNull();
    expect(session.activeOrganizationType).toBeNull();
    expect(session.activeOrganizationRole).toBeNull();
    expect(session.emulatedOrganizationId).toBeNull();
    expect(session.emulatedOrganizationType).toBeNull();
  });
});
