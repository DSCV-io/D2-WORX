import type { OrgType } from "../enums/org-type.js";

/**
 * Determines whether a user with the given active org type can create an organization
 * of the target type.
 *
 * Rules:
 * - `customer`: Self-service (anyone can create, even with no active org)
 * - `third_party`: Only users in a `customer` org
 * - `admin`, `support`, `affiliate`: Only users in an `admin` org
 */
export function canCreateOrgType(targetType: OrgType, creatorActiveOrgType?: OrgType): boolean {
  switch (targetType) {
    case "customer":
      return true;
    case "third_party":
      return creatorActiveOrgType === "customer";
    case "admin":
    case "support":
    case "affiliate":
      return creatorActiveOrgType === "admin";
  }
}
