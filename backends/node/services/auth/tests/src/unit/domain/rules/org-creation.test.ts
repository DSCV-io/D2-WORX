import { describe, it, expect } from "vitest";
import { canCreateOrgType } from "@d2/auth-domain";

describe("org-creation rules", () => {
  describe("canCreateOrgType", () => {
    it("should allow anyone to create a customer org (no active org)", () => {
      expect(canCreateOrgType("customer")).toBe(true);
      expect(canCreateOrgType("customer", undefined)).toBe(true);
      expect(canCreateOrgType("customer", undefined)).toBe(true);
    });

    it("should allow any org type to create a customer org", () => {
      expect(canCreateOrgType("customer", "admin")).toBe(true);
      expect(canCreateOrgType("customer", "support")).toBe(true);
      expect(canCreateOrgType("customer", "customer")).toBe(true);
    });

    it("should allow customer org to create third_party org", () => {
      expect(canCreateOrgType("third_party", "customer")).toBe(true);
    });

    it("should not allow non-customer to create third_party org", () => {
      expect(canCreateOrgType("third_party", "admin")).toBe(false);
      expect(canCreateOrgType("third_party", "support")).toBe(false);
      expect(canCreateOrgType("third_party", undefined)).toBe(false);
    });

    it("should allow admin org to create admin org", () => {
      expect(canCreateOrgType("admin", "admin")).toBe(true);
    });

    it("should allow admin org to create support org", () => {
      expect(canCreateOrgType("support", "admin")).toBe(true);
    });

    it("should allow admin org to create affiliate org", () => {
      expect(canCreateOrgType("affiliate", "admin")).toBe(true);
    });

    it("should not allow non-admin to create admin/support/affiliate orgs", () => {
      expect(canCreateOrgType("admin", "customer")).toBe(false);
      expect(canCreateOrgType("support", "customer")).toBe(false);
      expect(canCreateOrgType("affiliate", "customer")).toBe(false);
      expect(canCreateOrgType("admin", undefined)).toBe(false);
      expect(canCreateOrgType("support", undefined)).toBe(false);
      expect(canCreateOrgType("affiliate", undefined)).toBe(false);
    });
  });
});
