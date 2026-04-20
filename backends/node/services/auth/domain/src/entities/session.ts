import type { OrgType } from "../enums/org-type.js";
import type { Role } from "../enums/role.js";

/**
 * Represents an active user session.
 *
 * BetterAuth-managed — no factory function. This interface is the read contract only.
 * Includes custom session extension fields for org context and emulation.
 */
export interface Session {
  readonly id: string;
  readonly userId: string;
  readonly token: string;
  readonly expiresAt: Date;
  readonly ipAddress?: string;
  readonly userAgent?: string;
  readonly createdAt: Date;
  readonly updatedAt: Date;

  // Custom session extension fields
  readonly activeOrganizationId: string | null;
  readonly activeOrganizationType: OrgType | null;
  readonly activeOrganizationRole: Role | null;
  readonly emulatedOrganizationId: string | null;
  readonly emulatedOrganizationType: OrgType | null;
  /** Resolved at sign-in via cross-service Geo FindWhoIs (async, fail-open). */
  readonly whoIsId?: string;
}
