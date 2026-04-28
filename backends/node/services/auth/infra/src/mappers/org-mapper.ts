import type { Organization, OrgType } from "@d2/auth-domain";

/**
 * Maps a BetterAuth organization record to the domain Organization type.
 *
 * The orgType custom field is stored as metadata or a custom column
 * depending on BetterAuth's organization plugin configuration.
 */
export function toDomainOrganization(raw: Record<string, unknown>): Organization {
  return {
    id: raw["id"] as string,
    name: raw["name"] as string,
    slug: raw["slug"] as string,
    orgType: (raw["orgType"] ?? raw["org_type"] ?? "customer") as OrgType,
    logo: (raw["logo"] as string | undefined) ?? undefined,
    metadata: (raw["metadata"] as string | undefined) ?? undefined,
    createdAt: toDate(raw["createdAt"] ?? raw["created_at"]),
    updatedAt: toDate(raw["updatedAt"] ?? raw["updated_at"]),
  };
}

function toDate(value: unknown): Date {
  if (value instanceof Date) return value;
  if (typeof value === "string" || typeof value === "number") {
    return new Date(value);
  }
  return new Date();
}
