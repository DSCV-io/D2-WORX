// -----------------------------------------------------------------------
// <copyright file="AuthPolicies.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.AuthPolicy.Default;

/// <summary>
/// Named authorization policy constants used with <c>RequireAuthorization</c>.
/// Shared between gateway and any endpoint that needs declarative authorization.
/// </summary>
public static class AuthPolicies
{
    /// <summary>Requires a valid JWT (any authenticated user).</summary>
    public const string AUTHENTICATED = "Authenticated";

    /// <summary>Requires orgId + orgType + role claims (user has selected an org).</summary>
    public const string HAS_ACTIVE_ORG = "HasActiveOrg";

    /// <summary>Requires orgType to be admin or support.</summary>
    public const string STAFF_ONLY = "StaffOnly";

    /// <summary>Requires orgType to be admin.</summary>
    public const string ADMIN_ONLY = "AdminOnly";
}
