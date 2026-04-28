import { describe, it, expect } from "vitest";
import { createOrganization, updateOrganization, AuthValidationError } from "@d2/auth-domain";

describe("Organization", () => {
  const validInput = { name: "Acme Corp", slug: "acme-corp", orgType: "customer" as const };

  describe("createOrganization", () => {
    it("should create an organization with valid input", () => {
      const org = createOrganization(validInput);
      expect(org.name).toBe("Acme Corp");
      expect(org.slug).toBe("acme-corp");
      expect(org.orgType).toBe("customer");
      expect(org.logo).toBeUndefined();
      expect(org.metadata).toBeUndefined();
      expect(org.id).toHaveLength(36);
      expect(org.createdAt).toBeInstanceOf(Date);
    });

    it("should accept a pre-generated ID", () => {
      const org = createOrganization({ ...validInput, id: "custom-id" });
      expect(org.id).toBe("custom-id");
    });

    it("should lowercase the slug", () => {
      const org = createOrganization({ ...validInput, slug: "  ACME-Corp  " });
      expect(org.slug).toBe("acme-corp");
    });

    it("should clean whitespace in name", () => {
      const org = createOrganization({ ...validInput, name: "  Acme   Corp  " });
      expect(org.name).toBe("Acme Corp");
    });

    it("should throw AuthValidationError for empty name", () => {
      expect(() => createOrganization({ ...validInput, name: "" })).toThrow(AuthValidationError);
    });

    it("should throw AuthValidationError for empty slug", () => {
      expect(() => createOrganization({ ...validInput, slug: "  " })).toThrow(AuthValidationError);
    });

    it("should throw AuthValidationError for invalid orgType", () => {
      expect(() => createOrganization({ ...validInput, orgType: "invalid" as never })).toThrow(
        AuthValidationError,
      );
    });

    it.each(["admin", "support", "customer", "third_party", "affiliate"] as const)(
      "should accept valid orgType '%s'",
      (orgType) => {
        const org = createOrganization({ ...validInput, orgType });
        expect(org.orgType).toBe(orgType);
      },
    );

    it("should accept logo and metadata", () => {
      const org = createOrganization({
        ...validInput,
        logo: "https://example.com/logo.png",
        metadata: '{"key":"value"}',
      });
      expect(org.logo).toBe("https://example.com/logo.png");
      expect(org.metadata).toBe('{"key":"value"}');
    });
  });

  describe("updateOrganization", () => {
    const baseOrg = createOrganization(validInput);

    it("should update the name", () => {
      const updated = updateOrganization(baseOrg, { name: "New Name" });
      expect(updated.name).toBe("New Name");
      expect(updated.slug).toBe(baseOrg.slug);
      expect(updated.orgType).toBe(baseOrg.orgType);
    });

    it("should throw AuthValidationError for empty name update", () => {
      expect(() => updateOrganization(baseOrg, { name: "" })).toThrow(AuthValidationError);
    });

    it("should update logo", () => {
      const updated = updateOrganization(baseOrg, { logo: "https://new-logo.png" });
      expect(updated.logo).toBe("https://new-logo.png");
    });

    it("should preserve logo when update passes undefined", () => {
      const withLogo = createOrganization({ ...validInput, logo: "https://logo.png" });
      const updated = updateOrganization(withLogo, { logo: undefined });
      expect(updated.logo).toBe("https://logo.png");
    });

    it("should set updatedAt to a new timestamp", () => {
      const updated = updateOrganization(baseOrg, { name: "Updated" });
      expect(updated.updatedAt.getTime()).toBeGreaterThanOrEqual(baseOrg.updatedAt.getTime());
    });

    it("should update metadata", () => {
      const updated = updateOrganization(baseOrg, { metadata: '{"plan":"pro"}' });
      expect(updated.metadata).toBe('{"plan":"pro"}');
    });

    it("should preserve metadata when update passes undefined", () => {
      const withMeta = createOrganization({ ...validInput, metadata: '{"x":1}' });
      const updated = updateOrganization(withMeta, { metadata: undefined });
      expect(updated.metadata).toBe('{"x":1}');
    });

    it("should preserve immutable fields (slug, orgType)", () => {
      const updated = updateOrganization(baseOrg, { name: "Changed" });
      expect(updated.slug).toBe(baseOrg.slug);
      expect(updated.orgType).toBe(baseOrg.orgType);
      expect(updated.id).toBe(baseOrg.id);
      expect(updated.createdAt).toBe(baseOrg.createdAt);
    });
  });
});
