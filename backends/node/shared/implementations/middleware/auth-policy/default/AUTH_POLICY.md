# @d2/auth-policy

Declarative route-level authorization middleware for Hono apps. Mirrors
`D2.Shared.Implementations.Middleware.AuthPolicy.Default` on .NET — same
method names, same enforcement semantics, same status codes and i18n keys.

## When to use

Apply at the route or route-group level so the gate runs **before** the
handler. Handlers downstream don't need to re-check auth — the policy
already 401/403'd anything that doesn't qualify.

```ts
import { requireAuth, requireOrg, requireRole, requireStaff } from "@d2/auth-policy";

const account = new Hono();
account.use("*", requireAuth());
account.patch("/api/account/name", async (c) => {
  // c.get("requestContext").userId is guaranteed populated
});

const orgRoutes = new Hono();
orgRoutes.use("*", requireOrg());
orgRoutes.post("/api/org-contacts", requireRole("officer"), async (c) => { ... });

const adminRoutes = new Hono();
adminRoutes.use("*", requireStaff());
```

## Identity source

All policies read from `c.get("requestContext")` — an `IRequestContext` from
`@d2/handler`. Populate it upstream via either:

- `@d2/jwt-auth` middleware (JWT-validated public services)
- the auth service's session middleware (cookie-session-validated routes)

Both populate the same shape so the policies are agnostic to which
authentication mechanism was used.

## Policy reference

| Method                     | Enforces                                              | Fail status | Fail i18n key                               |
| -------------------------- | ----------------------------------------------------- | ----------- | ------------------------------------------- |
| `requireAuth()`            | `isAuthenticated === true` AND `userId` present       | 401         | `common_errors_UNAUTHORIZED`                |
| `requireTrustedService()`  | `isTrustedService === true` (S2S call)                | 401         | `common_errors_UNAUTHORIZED`                |
| `requireOrg()`             | authenticated AND `targetOrgId/Type/Role` all present | 401 / 403   | `middleware_errors_NO_ACTIVE_ORGANIZATION`  |
| `requireOrgType(...types)` | authenticated AND `targetOrgType ∈ types`             | 401 / 403   | `middleware_errors_ORG_TYPE_NOT_AUTHORIZED` |
| `requireRole(min)`         | authenticated AND `targetOrgRole >= min` in hierarchy | 401 / 403   | `middleware_errors_INSUFFICIENT_ROLE`       |
| `requireStaff()`           | shorthand for `requireOrgType(Admin, Support)`        | 401 / 403   | `middleware_errors_ORG_TYPE_NOT_AUTHORIZED` |
| `requireAdmin()`           | shorthand for `requireOrgType(Admin)`                 | 401 / 403   | `middleware_errors_ORG_TYPE_NOT_AUTHORIZED` |

Role hierarchy (low → high): `auditor < agent < officer < owner`.

Org types: `admin`, `support`, `customer`, `third_party`, `affiliate`.

## .NET parity

| `@d2/auth-policy`         | `D2.Shared.Implementations.Middleware.AuthPolicy.Default` |
| ------------------------- | --------------------------------------------------------- |
| `requireAuth()`           | `.RequireAuth()`                                          |
| `requireTrustedService()` | `.RequireTrustedService()`                                |
| `requireOrg()`            | `.RequireOrg()`                                           |
| `requireOrgType(...)`     | `.RequireOrgType(...)`                                    |
| `requireRole(min)`        | `.RequireRole(min)`                                       |
| `requireStaff()`          | `.RequireStaff()`                                         |
| `requireAdmin()`          | `.RequireAdmin()`                                         |
