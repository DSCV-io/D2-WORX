// -----------------------------------------------------------------------
// <copyright file="RoutePolicyExtensions.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.AuthPolicy.Default;

using D2.Shared.Handler;
using D2.Shared.Handler.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

/// <summary>
/// Fluent route-level authorization helpers. Each method gates the route at
/// registration time — handlers downstream don't re-check identity. Mirrors
/// the Hono `@d2/auth-policy` package on Node, method-name-for-method-name.
/// </summary>
/// <remarks>
/// Apply to <see cref="RouteHandlerBuilder"/> (single endpoint) or
/// <see cref="RouteGroupBuilder"/> (whole MapGroup). The named-policy helpers
/// (<c>RequireAuth</c>, <c>RequireOrg</c>, <c>RequireStaff</c>,
/// <c>RequireAdmin</c>) require <c>AddD2Policies()</c> to have been called on
/// the application's <see cref="AuthorizationOptions"/> at startup. The
/// parameterized helpers (<c>RequireOrgType</c>, <c>RequireRole</c>) build a
/// one-off <see cref="AuthorizationPolicy"/> per call — no startup
/// registration needed.
/// </remarks>
public static class RoutePolicyExtensions
{
    /// <summary>
    /// Extension methods for <see cref="RouteHandlerBuilder"/> — a single
    /// endpoint such as <c>app.MapGet(path, handler).RequireAuth()</c>.
    /// </summary>
    /// <param name="builder">The endpoint builder being extended.</param>
    extension(RouteHandlerBuilder builder)
    {
        /// <summary>Requires an authenticated user (any role, any org).</summary>
        /// <returns>The builder for chaining.</returns>
        public RouteHandlerBuilder RequireAuth() =>
            builder.RequireAuthorization(AuthPolicies.AUTHENTICATED);

        /// <summary>Requires the request to be from a trusted service (S2S).</summary>
        /// <returns>The builder for chaining.</returns>
        public RouteHandlerBuilder RequireTrustedService() =>
            builder.RequireAuthorization(BuildTrustedServicePolicy());

        /// <summary>Requires an authenticated user with an active org membership.</summary>
        /// <returns>The builder for chaining.</returns>
        public RouteHandlerBuilder RequireOrg() =>
            builder.RequireAuthorization(AuthPolicies.HAS_ACTIVE_ORG);

        /// <summary>Requires the active org type to be one of the listed values.</summary>
        /// <param name="orgTypes">Allowed org-type values (use <see cref="OrgTypeValues"/>).</param>
        /// <returns>The builder for chaining.</returns>
        public RouteHandlerBuilder RequireOrgType(params string[] orgTypes) =>
            builder.RequireAuthorization(BuildOrgTypePolicy(orgTypes));

        /// <summary>Requires the active role to be at-or-above <paramref name="minRole"/> in the hierarchy.</summary>
        /// <param name="minRole">Minimum role (use <see cref="RoleValues"/>).</param>
        /// <returns>The builder for chaining.</returns>
        public RouteHandlerBuilder RequireRole(string minRole) =>
            builder.RequireAuthorization(BuildRolePolicy(minRole));

        /// <summary>Shorthand for <c>RequireOrgType("admin", "support")</c>.</summary>
        /// <returns>The builder for chaining.</returns>
        public RouteHandlerBuilder RequireStaff() =>
            builder.RequireAuthorization(AuthPolicies.STAFF_ONLY);

        /// <summary>Shorthand for <c>RequireOrgType("admin")</c>.</summary>
        /// <returns>The builder for chaining.</returns>
        public RouteHandlerBuilder RequireAdmin() =>
            builder.RequireAuthorization(AuthPolicies.ADMIN_ONLY);
    }

    /// <summary>
    /// Extension methods for <see cref="RouteGroupBuilder"/> — apply once to a
    /// whole route group, e.g. <c>group.RequireAuth()</c>.
    /// </summary>
    /// <param name="group">The route group being extended.</param>
    extension(RouteGroupBuilder group)
    {
        /// <summary>Requires an authenticated user (any role, any org).</summary>
        /// <returns>The group for chaining.</returns>
        public RouteGroupBuilder RequireAuth() =>
            group.RequireAuthorization(AuthPolicies.AUTHENTICATED);

        /// <summary>Requires the request to be from a trusted service (S2S).</summary>
        /// <returns>The group for chaining.</returns>
        public RouteGroupBuilder RequireTrustedService() =>
            group.RequireAuthorization(BuildTrustedServicePolicy());

        /// <summary>Requires an authenticated user with an active org membership.</summary>
        /// <returns>The group for chaining.</returns>
        public RouteGroupBuilder RequireOrg() =>
            group.RequireAuthorization(AuthPolicies.HAS_ACTIVE_ORG);

        /// <summary>Requires the active org type to be one of the listed values.</summary>
        /// <param name="orgTypes">Allowed org-type values (use <see cref="OrgTypeValues"/>).</param>
        /// <returns>The group for chaining.</returns>
        public RouteGroupBuilder RequireOrgType(params string[] orgTypes) =>
            group.RequireAuthorization(BuildOrgTypePolicy(orgTypes));

        /// <summary>Requires the active role to be at-or-above <paramref name="minRole"/> in the hierarchy.</summary>
        /// <param name="minRole">Minimum role (use <see cref="RoleValues"/>).</param>
        /// <returns>The group for chaining.</returns>
        public RouteGroupBuilder RequireRole(string minRole) =>
            group.RequireAuthorization(BuildRolePolicy(minRole));

        /// <summary>Shorthand for <c>RequireOrgType("admin", "support")</c>.</summary>
        /// <returns>The group for chaining.</returns>
        public RouteGroupBuilder RequireStaff() =>
            group.RequireAuthorization(AuthPolicies.STAFF_ONLY);

        /// <summary>Shorthand for <c>RequireOrgType("admin")</c>.</summary>
        /// <returns>The group for chaining.</returns>
        public RouteGroupBuilder RequireAdmin() =>
            group.RequireAuthorization(AuthPolicies.ADMIN_ONLY);
    }

    private static AuthorizationPolicy BuildOrgTypePolicy(string[] orgTypes) =>
        new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .RequireClaim(JwtClaimTypes.ORG_TYPE, orgTypes)
            .Build();

    private static AuthorizationPolicy BuildRolePolicy(string minRole) =>
        new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .RequireClaim(JwtClaimTypes.ROLE, RoleValues.AtOrAbove(minRole))
            .Build();

    /// <summary>
    /// `IsTrustedService` is set by `ServiceKeyMiddleware` on the
    /// <see cref="IRequestContext"/> stored in <see cref="HttpContext.Features"/>
    /// — not a JWT claim. We use <see cref="AuthorizationPolicyBuilder.RequireAssertion(System.Func{AuthorizationHandlerContext, bool})"/>
    /// to read the flag at policy-evaluation time.
    /// </summary>
    private static AuthorizationPolicy BuildTrustedServicePolicy() =>
        new AuthorizationPolicyBuilder()
            .RequireAssertion(authContext =>
            {
                var http = authContext.Resource as HttpContext;
                var rc = http?.Features.Get<IRequestContext>();
                return rc?.IsTrustedService == true;
            })
            .Build();
}
