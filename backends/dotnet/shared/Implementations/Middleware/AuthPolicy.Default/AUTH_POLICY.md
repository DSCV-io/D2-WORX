# D2.Shared.Implementations.Middleware.AuthPolicy.Default

Declarative route-level authorization helpers for ASP.NET Core minimal APIs.
Mirrors the Hono `@d2/auth-policy` package on Node — same method names, same
enforcement semantics, same status codes.

## When to use

Apply at the route or route-group level so the gate runs **before** the
handler. Handlers downstream don't re-check identity; the policy already
401/403'd anything that doesn't qualify.

```csharp
var notificationPrefs = erb.MapGroup("/api/v1/notification-preferences")
    .RequireAuth();

notificationPrefs.MapGet(string.Empty, GetMyPreferencesAsync);
notificationPrefs.MapPut(string.Empty, SetMyPreferencesAsync);

var orgScoped = erb.MapGroup("/api/org-contacts").RequireOrg();
orgScoped.MapPost(string.Empty, CreateOrgContact).RequireRole(RoleValues.OFFICER);

var adminOnly = erb.MapGroup("/api/admin").RequireStaff();
```

## Identity source

All policies read identity from JWT claims populated by the JWT bearer
middleware (`@d2/auth-default` namespace `D2.Shared.JwtAuth.Default`). The
`RequireTrustedService()` policy reads `IRequestContext.IsTrustedService`
from `HttpContext.Features` — that flag is set by `ServiceKeyMiddleware`
in the `D2.Shared.ServiceKey.Default` package.

## Policy reference

| Method                                | Enforces                                              | Fail status | Notes                                                         |
| ------------------------------------- | ----------------------------------------------------- | ----------- | ------------------------------------------------------------- |
| `.RequireAuth()`                      | `User.Identity.IsAuthenticated`                       | 401         | Wraps the named `AuthPolicies.AUTHENTICATED` policy           |
| `.RequireTrustedService()`            | `IRequestContext.IsTrustedService == true`            | 401/403     | Custom `RequireAssertion` reading from `HttpContext.Features` |
| `.RequireOrg()`                       | authenticated AND `orgId/orgType/role` claims present | 401/403     | Wraps `AuthPolicies.HAS_ACTIVE_ORG`                           |
| `.RequireOrgType("admin", "support")` | active `orgType` claim ∈ allowed                      | 401/403     | Builds a one-off policy per call                              |
| `.RequireRole("officer")`             | active `role` claim ≥ min in hierarchy                | 401/403     | Builds a one-off policy per call                              |
| `.RequireStaff()`                     | shorthand for `RequireOrgType("admin", "support")`    | 401/403     | Wraps `AuthPolicies.STAFF_ONLY`                               |
| `.RequireAdmin()`                     | shorthand for `RequireOrgType("admin")`               | 401/403     | Wraps `AuthPolicies.ADMIN_ONLY`                               |

Role hierarchy (low → high): `auditor < agent < officer < owner`.

Org types: `admin`, `support`, `customer`, `third_party`, `affiliate`.

## Wiring the named policies

`RequireAuth/RequireOrg/RequireStaff/RequireAdmin` reference named policies
that must be registered at startup. The shared `AddD2Policies()` extension
on `AuthorizationOptions` does this:

```csharp
services.AddAuthorization(options => options.AddD2Policies());
```

`AddJwtAuth(...)` from `D2.Shared.JwtAuth.Default` calls this internally,
so most consumers don't need to call it themselves.

## Node parity

| `@d2/auth-policy` (Node)  | `D2.Shared.AuthPolicy.Default` (.NET) |
| ------------------------- | ------------------------------------- |
| `requireAuth()`           | `.RequireAuth()`                      |
| `requireTrustedService()` | `.RequireTrustedService()`            |
| `requireOrg()`            | `.RequireOrg()`                       |
| `requireOrgType(...)`     | `.RequireOrgType(...)`                |
| `requireRole(min)`        | `.RequireRole(min)`                   |
| `requireStaff()`          | `.RequireStaff()`                     |
| `requireAdmin()`          | `.RequireAdmin()`                     |

## File layout

| File                       | Purpose                                                                               |
| -------------------------- | ------------------------------------------------------------------------------------- |
| `AuthPolicies.cs`          | String constants for the named policies                                               |
| `AuthPolicyExtensions.cs`  | `AddD2Policies()` — registers the named policies on `AuthorizationOptions` at startup |
| `RoutePolicyExtensions.cs` | The fluent route-level helpers (`.RequireAuth()` etc.)                                |
