import { OrgType } from "@d2/handler";
import { requireOrgType } from "./require-org-type.js";

/**
 * Shorthand for `requireOrgType(OrgType.Admin, OrgType.Support)`. Mirrors
 * `.RequireStaff()` on .NET. Used by routes that should only be reachable
 * by internal staff (admin or support agents).
 */
export function requireStaff() {
  return requireOrgType(OrgType.Admin, OrgType.Support);
}
