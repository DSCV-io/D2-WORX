import { describe, it, expect } from "vitest";
import { toDomainOrganization } from "@d2/auth-infra";

describe("toDomainOrganization", () => {
  it("should map camelCase org to domain Organization", () => {
    const raw = {
      id: "org-123",
      name: "Acme Corp",
      slug: "acme-corp",
      orgType: "customer",
      logo: "https://example.com/logo.png",
      metadata: '{"plan":"pro"}',
      createdAt: new Date("2026-01-01"),
      updatedAt: new Date("2026-01-02"),
    };

    const org = toDomainOrganization(raw);

    expect(org.id).toBe("org-123");
    expect(org.name).toBe("Acme Corp");
    expect(org.slug).toBe("acme-corp");
    expect(org.orgType).toBe("customer");
    expect(org.logo).toBe("https://example.com/logo.png");
    expect(org.metadata).toBe('{"plan":"pro"}');
  });

  it("should map snake_case org_type field", () => {
    const raw = {
      id: "org-456",
      name: "Support HQ",
      slug: "support-hq",
      org_type: "support",
      logo: null,
      metadata: null,
      created_at: new Date(),
      updated_at: new Date(),
    };

    const org = toDomainOrganization(raw);
    expect(org.orgType).toBe("support");
    expect(org.logo).toBeUndefined();
    expect(org.metadata).toBeUndefined();
  });

  it("should default orgType to customer when missing", () => {
    const raw = {
      id: "org-789",
      name: "Default Org",
      slug: "default-org",
      createdAt: new Date(),
      updatedAt: new Date(),
    };

    const org = toDomainOrganization(raw);
    expect(org.orgType).toBe("customer");
  });
});
