import type { JWTPayload } from "jose";
import type { IRequestContext } from "@d2/handler";
import { OrgType } from "@d2/handler";

/**
 * Maps JWT claim names to IRequestContext fields.
 *
 * Claims follow the D2 JWT convention defined in `@d2/auth-domain` JWT_CLAIM_TYPES:
 * - `sub` → userId
 * - `email` → email
 * - `username` → username
 * - `orgId` → agentOrgId + targetOrgId
 * - `orgName` → agentOrgName + targetOrgName
 * - `orgType` → agentOrgType + targetOrgType
 * - `role` → agentOrgRole + targetOrgRole
 * - `isEmulating` / `emulatedOrgId/Name/Type` → emulation fields
 * - `isImpersonating` / `impersonatedBy` → impersonation fields
 */
export function populateRequestContext(payload: JWTPayload): IRequestContext {
  const orgType = parseOrgType(payload["orgType"]);
  const emulatedOrgType = parseOrgType(payload["emulatedOrgType"]);
  const isEmulating = payload["isEmulating"] === true || payload["isEmulating"] === "true";
  const isImpersonating =
    payload["isImpersonating"] === true || payload["isImpersonating"] === "true";

  // Build base context from JWT claims
  const context: IRequestContext = {
    isAuthenticated: true,
    isTrustedService: false,
    isOrgEmulating: isEmulating,
    isUserImpersonating: isImpersonating,

    // User identity
    userId: stringClaim(payload, "sub"),
    email: stringClaim(payload, "email"),
    username: stringClaim(payload, "username"),

    // Agent org (user's actual membership)
    agentOrgId: stringClaim(payload, "orgId"),
    agentOrgName: stringClaim(payload, "orgName"),
    agentOrgType: orgType,
    agentOrgRole: stringClaim(payload, "role"),

    // Target org (defaults to agent org, overridden if emulating)
    targetOrgId: isEmulating
      ? stringClaim(payload, "emulatedOrgId")
      : stringClaim(payload, "orgId"),
    targetOrgName: isEmulating
      ? stringClaim(payload, "emulatedOrgName")
      : stringClaim(payload, "orgName"),
    targetOrgType: isEmulating ? emulatedOrgType : orgType,
    targetOrgRole: isEmulating ? "auditor" : stringClaim(payload, "role"),

    // Impersonation
    impersonatedBy: stringClaim(payload, "impersonatedBy"),
    impersonatingEmail: stringClaim(payload, "impersonatingEmail"),
    impersonatingUsername: stringClaim(payload, "impersonatingUsername"),

    // Computed helpers
    isAgentStaff: orgType === OrgType.Support || orgType === OrgType.Admin,
    isAgentAdmin: orgType === OrgType.Admin,
    isTargetingStaff: isEmulating
      ? emulatedOrgType === OrgType.Support || emulatedOrgType === OrgType.Admin
      : orgType === OrgType.Support || orgType === OrgType.Admin,
    isTargetingAdmin: isEmulating ? emulatedOrgType === OrgType.Admin : orgType === OrgType.Admin,
  };

  return context;
}

function stringClaim(payload: JWTPayload, key: string): string | undefined {
  const value = payload[key];
  return typeof value === "string" ? value : undefined;
}

function parseOrgType(value: unknown): OrgType | undefined {
  if (typeof value !== "string") return undefined;
  // JWT stores PascalCase org types matching the OrgType enum
  if (Object.values(OrgType).includes(value as OrgType)) {
    return value as OrgType;
  }
  return undefined;
}
