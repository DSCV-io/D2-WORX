// -----------------------------------------------------------------------
// <copyright file="AuthPolicyTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Gateway;

using D2.Shared.AuthPolicy.Default;
using D2.Shared.Handler.Auth;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;

/// <summary>
/// Unit tests for auth constants, role hierarchy, and D2 authorization policies.
/// </summary>
public class AuthPolicyTests
{
    #region RoleValues.AtOrAbove

    /// <summary>
    /// Tests that AtOrAbove("auditor") returns all roles.
    /// </summary>
    [Fact]
    public void AtOrAbove_Auditor_ReturnsAllRoles()
    {
        var result = RoleValues.AtOrAbove(RoleValues.AUDITOR);

        result.Should().Equal([RoleValues.AUDITOR, RoleValues.AGENT, RoleValues.OFFICER, RoleValues.OWNER]);
    }

    /// <summary>
    /// Tests that AtOrAbove("agent") returns agent, officer, owner.
    /// </summary>
    [Fact]
    public void AtOrAbove_Agent_ReturnsAgentOfficerOwner()
    {
        var result = RoleValues.AtOrAbove(RoleValues.AGENT);

        result.Should().Equal([RoleValues.AGENT, RoleValues.OFFICER, RoleValues.OWNER]);
    }

    /// <summary>
    /// Tests that AtOrAbove("officer") returns officer and owner.
    /// </summary>
    [Fact]
    public void AtOrAbove_Officer_ReturnsOfficerAndOwner()
    {
        var result = RoleValues.AtOrAbove(RoleValues.OFFICER);

        result.Should().Equal([RoleValues.OFFICER, RoleValues.OWNER]);
    }

    /// <summary>
    /// Tests that AtOrAbove("owner") returns only owner.
    /// </summary>
    [Fact]
    public void AtOrAbove_Owner_ReturnsOnlyOwner()
    {
        var result = RoleValues.AtOrAbove(RoleValues.OWNER);

        result.Should().Equal([RoleValues.OWNER]);
    }

    /// <summary>
    /// Tests that AtOrAbove with an invalid role returns empty array.
    /// </summary>
    [Fact]
    public void AtOrAbove_InvalidRole_ReturnsEmpty()
    {
        var result = RoleValues.AtOrAbove("invalid");

        result.Should().BeEmpty();
    }

    #endregion

    #region OrgTypeValues

    /// <summary>
    /// Tests that STAFF contains admin and support.
    /// </summary>
    [Fact]
    public void OrgTypeValues_Staff_ContainsAdminAndSupport()
    {
        OrgTypeValues.SR_Staff.Should().Equal([OrgTypeValues.ADMIN, OrgTypeValues.SUPPORT]);
    }

    /// <summary>
    /// Tests that ALL contains all five org types.
    /// </summary>
    [Fact]
    public void OrgTypeValues_All_ContainsAllFiveTypes()
    {
        OrgTypeValues.SR_All.Should().HaveCount(5);
        OrgTypeValues.SR_All.Should().Contain(OrgTypeValues.ADMIN);
        OrgTypeValues.SR_All.Should().Contain(OrgTypeValues.SUPPORT);
        OrgTypeValues.SR_All.Should().Contain(OrgTypeValues.CUSTOMER);
        OrgTypeValues.SR_All.Should().Contain(OrgTypeValues.THIRD_PARTY);
        OrgTypeValues.SR_All.Should().Contain(OrgTypeValues.AFFILIATE);
    }

    #endregion

    #region AddD2Policies

    /// <summary>
    /// Tests that AddD2Policies registers the Authenticated policy.
    /// </summary>
    [Fact]
    public void AddD2Policies_RegistersAuthenticatedPolicy()
    {
        var options = new AuthorizationOptions();

        options.AddD2Policies();

        var policy = options.GetPolicy(AuthPolicies.AUTHENTICATED);
        policy.Should().NotBeNull();
        policy.AuthenticationSchemes.Should().BeEmpty(); // RequireAuthenticatedUser doesn't add schemes
    }

    /// <summary>
    /// Tests that AddD2Policies registers the HasActiveOrg policy with required claims.
    /// </summary>
    [Fact]
    public void AddD2Policies_RegistersHasActiveOrgPolicy_WithRequiredClaims()
    {
        var options = new AuthorizationOptions();

        options.AddD2Policies();

        var policy = options.GetPolicy(AuthPolicies.HAS_ACTIVE_ORG);
        policy.Should().NotBeNull();

        // The policy should require orgId, orgType, and role claims.
        var claimTypes = policy.Requirements
            .OfType<ClaimsAuthorizationRequirement>()
            .Select(r => r.ClaimType)
            .ToList();

        claimTypes.Should().Contain(JwtClaimTypes.ORG_ID);
        claimTypes.Should().Contain(JwtClaimTypes.ORG_TYPE);
        claimTypes.Should().Contain(JwtClaimTypes.ROLE);
    }

    /// <summary>
    /// Tests that AddD2Policies registers the StaffOnly policy.
    /// </summary>
    [Fact]
    public void AddD2Policies_RegistersStaffOnlyPolicy()
    {
        var options = new AuthorizationOptions();

        options.AddD2Policies();

        var policy = options.GetPolicy(AuthPolicies.STAFF_ONLY);
        policy.Should().NotBeNull();

        var orgTypeReq = policy.Requirements
            .OfType<ClaimsAuthorizationRequirement>()
            .FirstOrDefault(r => r.ClaimType == JwtClaimTypes.ORG_TYPE);
        orgTypeReq.Should().NotBeNull();
        orgTypeReq.AllowedValues.Should().Contain(OrgTypeValues.ADMIN);
        orgTypeReq.AllowedValues.Should().Contain(OrgTypeValues.SUPPORT);
    }

    /// <summary>
    /// Tests that AddD2Policies registers the AdminOnly policy.
    /// </summary>
    [Fact]
    public void AddD2Policies_RegistersAdminOnlyPolicy()
    {
        var options = new AuthorizationOptions();

        options.AddD2Policies();

        var policy = options.GetPolicy(AuthPolicies.ADMIN_ONLY);
        policy.Should().NotBeNull();

        var orgTypeReq = policy.Requirements
            .OfType<ClaimsAuthorizationRequirement>()
            .FirstOrDefault(r => r.ClaimType == JwtClaimTypes.ORG_TYPE);
        orgTypeReq.Should().NotBeNull();
        orgTypeReq.AllowedValues.Should().Contain(OrgTypeValues.ADMIN);
        orgTypeReq.AllowedValues.Should().NotContain(OrgTypeValues.SUPPORT);
    }

    #endregion

    #region RequireOrgType

    /// <summary>
    /// Tests that RequireOrgType creates a policy with the specified org types.
    /// </summary>
    [Fact]
    public void RequireOrgType_CreatesPolicy_WithSpecifiedTypes()
    {
        var options = new AuthorizationOptions();

        options.RequireOrgType("CustomersOnly", [OrgTypeValues.CUSTOMER, OrgTypeValues.THIRD_PARTY]);

        var policy = options.GetPolicy("CustomersOnly");
        policy.Should().NotBeNull();

        var orgTypeReq = policy.Requirements
            .OfType<ClaimsAuthorizationRequirement>()
            .FirstOrDefault(r => r.ClaimType == JwtClaimTypes.ORG_TYPE);
        orgTypeReq.Should().NotBeNull();
        orgTypeReq.AllowedValues.Should().Contain(OrgTypeValues.CUSTOMER);
        orgTypeReq.AllowedValues.Should().Contain(OrgTypeValues.THIRD_PARTY);
        orgTypeReq.AllowedValues.Should().NotContain(OrgTypeValues.ADMIN);
    }

    #endregion

    #region RequireRole

    /// <summary>
    /// Tests that RequireRole creates a policy with correct hierarchy roles.
    /// </summary>
    [Fact]
    public void RequireRole_Officer_IncludesOfficerAndOwner()
    {
        var options = new AuthorizationOptions();

        options.RequireRole("OfficerPlus", RoleValues.OFFICER);

        var policy = options.GetPolicy("OfficerPlus");
        policy.Should().NotBeNull();

        var roleReq = policy.Requirements
            .OfType<ClaimsAuthorizationRequirement>()
            .FirstOrDefault(r => r.ClaimType == JwtClaimTypes.ROLE);
        roleReq.Should().NotBeNull();
        roleReq.AllowedValues.Should().Contain(RoleValues.OFFICER);
        roleReq.AllowedValues.Should().Contain(RoleValues.OWNER);
        roleReq.AllowedValues.Should().NotContain(RoleValues.AGENT);
        roleReq.AllowedValues.Should().NotContain(RoleValues.AUDITOR);
    }

    #endregion

    #region RequireOrgTypeAndRole

    /// <summary>
    /// Tests that RequireOrgTypeAndRole creates a policy combining both checks.
    /// </summary>
    [Fact]
    public void RequireOrgTypeAndRole_CombinesBothChecks()
    {
        var options = new AuthorizationOptions();

        options.RequireOrgTypeAndRole("StaffOfficer", OrgTypeValues.SR_Staff, RoleValues.OFFICER);

        var policy = options.GetPolicy("StaffOfficer");
        policy.Should().NotBeNull();

        var requirements = policy.Requirements
            .OfType<ClaimsAuthorizationRequirement>()
            .ToList();

        var orgTypeReq = requirements.FirstOrDefault(r => r.ClaimType == JwtClaimTypes.ORG_TYPE);
        orgTypeReq.Should().NotBeNull();
        orgTypeReq.AllowedValues.Should().Contain(OrgTypeValues.ADMIN);
        orgTypeReq.AllowedValues.Should().Contain(OrgTypeValues.SUPPORT);

        var roleReq = requirements.FirstOrDefault(r => r.ClaimType == JwtClaimTypes.ROLE);
        roleReq.Should().NotBeNull();
        roleReq.AllowedValues.Should().Contain(RoleValues.OFFICER);
        roleReq.AllowedValues.Should().Contain(RoleValues.OWNER);
        roleReq.AllowedValues.Should().NotContain(RoleValues.AGENT);
    }

    #endregion
}
