/**
 * Organization membership roles. Mirrors `D2.Shared.Handler.Auth.RoleValues`
 * in .NET — both sides MUST use the same names and the same hierarchy
 * ordering or cross-platform authorization decisions will diverge.
 *
 * Stored as plain text in PostgreSQL (not a PG enum). The TS string union
 * here provides compile-time safety; `isValidRole` enforces it at runtime.
 */
export const ROLES = ["owner", "officer", "agent", "auditor"] as const;

export type Role = (typeof ROLES)[number];

export function isValidRole(value: unknown): value is Role {
  return typeof value === "string" && ROLES.includes(value as Role);
}

/**
 * Role hierarchy — higher numeric value means more privileges. Used by
 * `requireRole(min)` (and `.RequireRole(min)` on .NET) to compare a session's
 * role to a minimum threshold.
 */
export const ROLE_HIERARCHY: Readonly<Record<Role, number>> = {
  auditor: 0,
  agent: 1,
  officer: 2,
  owner: 3,
};

/**
 * Returns the list of roles that are at-or-above `minRole` in the hierarchy.
 * Mirrors `D2.Shared.Handler.Auth.RoleValues.AtOrAbove(string)`.
 */
export function rolesAtOrAbove(minRole: Role): readonly Role[] {
  const minLevel = ROLE_HIERARCHY[minRole];
  return ROLES.filter((r) => ROLE_HIERARCHY[r] >= minLevel);
}
