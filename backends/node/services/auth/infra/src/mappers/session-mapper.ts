import type { Session, OrgType, Role } from "@d2/auth-domain";

/**
 * Maps a BetterAuth session record to the domain Session type.
 *
 * Includes custom extension fields for organization context and emulation.
 */
export function toDomainSession(raw: Record<string, unknown>): Session {
  return {
    id: raw["id"] as string,
    userId: (raw["userId"] ?? raw["user_id"]) as string,
    token: raw["token"] as string,
    expiresAt: toDate(raw["expiresAt"] ?? raw["expires_at"]),
    ipAddress: (raw["ipAddress"] ?? raw["ip_address"]) as string | undefined,
    userAgent: (raw["userAgent"] ?? raw["user_agent"]) as string | undefined,
    createdAt: toDate(raw["createdAt"] ?? raw["created_at"]),
    updatedAt: toDate(raw["updatedAt"] ?? raw["updated_at"]),
    // Custom session extension fields
    activeOrganizationId: (raw["activeOrganizationId"] ?? raw["active_organization_id"] ?? null) as
      | string
      | null,
    activeOrganizationType: (raw["activeOrganizationType"] ??
      raw["active_organization_type"] ??
      null) as OrgType | null,
    activeOrganizationRole: (raw["activeOrganizationRole"] ??
      raw["active_organization_role"] ??
      null) as Role | null,
    emulatedOrganizationId: (raw["emulatedOrganizationId"] ??
      raw["emulated_organization_id"] ??
      null) as string | null,
    emulatedOrganizationType: (raw["emulatedOrganizationType"] ??
      raw["emulated_organization_type"] ??
      null) as OrgType | null,
  };
}

function toDate(value: unknown): Date {
  if (value instanceof Date) return value;
  if (typeof value === "string" || typeof value === "number") {
    return new Date(value);
  }
  return new Date();
}
