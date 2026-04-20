// -----------------------------------------------------------------------
// <copyright file="AuthPolicyExtensions.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.AuthPolicy.Default;

using D2.Shared.Handler.Auth;
using Microsoft.AspNetCore.Authorization;

/// <summary>
/// Extension methods for configuring D2 authorization policies.
/// </summary>
public static class AuthPolicyExtensions
{
    /// <summary>
    /// Extension methods for <see cref="AuthorizationOptions"/>.
    /// </summary>
    extension(AuthorizationOptions options)
    {
        /// <summary>
        /// Registers all standard D2 authorization policies.
        /// </summary>
        public void AddD2Policies()
        {
            // Basic: authenticated user.
            options.AddPolicy(AuthPolicies.AUTHENTICATED, p =>
                p.RequireAuthenticatedUser());

            // Has an active org (orgId, orgType, role all present).
            options.AddPolicy(AuthPolicies.HAS_ACTIVE_ORG, p =>
                p.RequireAuthenticatedUser()
                 .RequireClaim(JwtClaimTypes.ORG_ID)
                 .RequireClaim(JwtClaimTypes.ORG_TYPE)
                 .RequireClaim(JwtClaimTypes.ROLE));

            // Staff only (admin or support org types).
            options.AddPolicy(AuthPolicies.STAFF_ONLY, p =>
                p.RequireAuthenticatedUser()
                 .RequireClaim(JwtClaimTypes.ORG_TYPE, OrgTypeValues.SR_Staff));

            // Admin only.
            options.AddPolicy(AuthPolicies.ADMIN_ONLY, p =>
                p.RequireAuthenticatedUser()
                 .RequireClaim(JwtClaimTypes.ORG_TYPE, OrgTypeValues.ADMIN));
        }

        /// <summary>
        /// Creates a policy requiring specific org types.
        /// </summary>
        /// <param name="policyName">The name for the new policy.</param>
        /// <param name="orgTypes">The allowed org type values.</param>
        public void RequireOrgType(string policyName, params string[] orgTypes)
        {
            options.AddPolicy(policyName, p =>
                p.RequireAuthenticatedUser()
                 .RequireClaim(JwtClaimTypes.ORG_TYPE, orgTypes));
        }

        /// <summary>
        /// Creates a policy requiring a minimum org role.
        /// </summary>
        /// <param name="policyName">The name for the new policy.</param>
        /// <param name="minRole">The minimum role in the hierarchy.</param>
        public void RequireRole(string policyName, string minRole)
        {
            options.AddPolicy(policyName, p =>
                p.RequireAuthenticatedUser()
                 .RequireClaim(JwtClaimTypes.ROLE, RoleValues.AtOrAbove(minRole)));
        }

        /// <summary>
        /// Creates a policy requiring specific org type AND minimum role.
        /// </summary>
        /// <param name="policyName">The name for the new policy.</param>
        /// <param name="orgTypes">The allowed org type values.</param>
        /// <param name="minRole">The minimum role in the hierarchy.</param>
        public void RequireOrgTypeAndRole(string policyName, string[] orgTypes, string minRole)
        {
            options.AddPolicy(policyName, p =>
                p.RequireAuthenticatedUser()
                 .RequireClaim(JwtClaimTypes.ORG_TYPE, orgTypes)
                 .RequireClaim(JwtClaimTypes.ROLE, RoleValues.AtOrAbove(minRole)));
        }
    }
}
