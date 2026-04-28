import { describe, it, expect } from "vitest";
import { ORG_TYPES, isValidOrgType } from "@d2/auth-domain";
import { OrgType } from "@d2/handler";

describe("OrgType", () => {
  it("should have exactly 5 organization types", () => {
    expect(ORG_TYPES).toHaveLength(5);
  });

  it("should contain all expected types", () => {
    expect(ORG_TYPES).toContain("admin");
    expect(ORG_TYPES).toContain("support");
    expect(ORG_TYPES).toContain("customer");
    expect(ORG_TYPES).toContain("third_party");
    expect(ORG_TYPES).toContain("affiliate");
  });

  // Regression for A1: the auth-domain ORG_TYPES (DB wire format) and the
  // handler-side OrgType enum (used in JWT claims + IRequestContext) must
  // share identical string values. Drift between them silently downgrades
  // `isAgentStaff`/`isAgentAdmin` to false on every Node service consuming
  // a D2 JWT — fail-closed, but legit staff get denied access. Previously
  // `OrgType.Admin = "Admin"` (PascalCase) and the DB wrote `"admin"`
  // (lowercase); now both are lowercase.
  it("should have OrgType enum values exactly matching ORG_TYPES (cross-package wire format parity)", () => {
    const handlerValues = new Set(Object.values(OrgType));
    const domainValues = new Set(ORG_TYPES);
    expect(handlerValues).toEqual(domainValues);
  });

  it.each([
    [OrgType.Admin, "admin"],
    [OrgType.Support, "support"],
    [OrgType.Customer, "customer"],
    [OrgType.ThirdParty, "third_party"],
    [OrgType.Affiliate, "affiliate"],
  ])("OrgType.%s should serialize to lowercase wire string '%s'", (enumValue, wireValue) => {
    expect(enumValue).toBe(wireValue);
  });

  describe("isValidOrgType", () => {
    it.each(["admin", "support", "customer", "third_party", "affiliate"])(
      "should return true for valid type '%s'",
      (type) => {
        expect(isValidOrgType(type)).toBe(true);
      },
    );

    it.each(["Admin", "SUPPORT", "unknown", "", 42, null, undefined, true])(
      "should return false for invalid value '%s'",
      (value) => {
        expect(isValidOrgType(value)).toBe(false);
      },
    );
  });
});
