/**
 * Re-exports the canonical `Role` definition from `@d2/handler` so existing
 * `@d2/auth-domain` consumers don't break. The source of truth lives in
 * `@d2/handler/src/role.ts` to mirror `D2.Shared.Handler.Auth.RoleValues` —
 * cross-cutting auth primitives belong with the platform's handler layer,
 * not in the auth-service domain package.
 */
export { ROLES, ROLE_HIERARCHY, isValidRole, rolesAtOrAbove, type Role } from "@d2/handler";
