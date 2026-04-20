import { OrgType } from "@d2/handler";
import { requireOrgType } from "./require-org-type.js";

/**
 * Shorthand for `requireOrgType(OrgType.Admin)`. Mirrors `.RequireAdmin()` on
 * .NET. Used by routes that should only be reachable by admins.
 */
export function requireAdmin() {
  return requireOrgType(OrgType.Admin);
}
