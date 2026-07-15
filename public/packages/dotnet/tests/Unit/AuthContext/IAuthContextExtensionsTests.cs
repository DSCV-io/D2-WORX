// -----------------------------------------------------------------------
// <copyright file="IAuthContextExtensionsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.AuthContext;

using System;
using System.Collections.Generic;
using AwesomeAssertions;
using DcsvIo.D2.Auth.Abstractions;
using DcsvIo.D2.AuthContext.Abstractions;
using Xunit;

public sealed class IAuthContextExtensionsTests
{
    // ----------------------------------------------------------------------
    // HasScope
    // ----------------------------------------------------------------------

    [Fact]
    public void HasScope_PresentInSet_ReturnsTrue()
    {
        var ctx = new TestAuthContext { Scopes = ScopeSet("self.read", "self.write") };

        ctx.HasScope("self.read").Should().BeTrue();
    }

    [Fact]
    public void HasScope_AbsentFromSet_ReturnsFalse()
    {
        var ctx = new TestAuthContext { Scopes = ScopeSet("self.read") };

        ctx.HasScope("auth.password.change").Should().BeFalse();
    }

    [Fact]
    public void HasScope_EmptyScopeSet_ReturnsFalse()
    {
        // Adversarial: pre-auth context (no scopes resolved yet) must not
        // accidentally grant anything.
        var ctx = new TestAuthContext { Scopes = ScopeSet() };

        ctx.HasScope("self.read").Should().BeFalse();
    }

    [Fact]
    public void HasScope_DifferentCasing_ReturnsFalse()
    {
        // Adversarial: scope names are case-sensitive (RFC 6749 §3.3). The
        // backing HashSet uses StringComparer.Ordinal in our test factory and
        // in MutableRequestContext.
        var ctx = new TestAuthContext { Scopes = ScopeSet("self.read") };

        ctx.HasScope("Self.Read").Should().BeFalse();
        ctx.HasScope("SELF.READ").Should().BeFalse();
    }

    // ----------------------------------------------------------------------
    // HasAnyScope
    // ----------------------------------------------------------------------

    [Fact]
    public void HasAnyScope_NoArgs_ReturnsFalse()
    {
        // Adversarial: empty params array — `Any()` over empty returns false,
        // which is the right semantics ("require ANY of nothing" → unmet).
        var ctx = new TestAuthContext { Scopes = ScopeSet("self.read") };

        ctx.HasAnyScope().Should().BeFalse();
    }

    [Fact]
    public void HasAnyScope_SingleScopeMatch_ReturnsTrue()
    {
        var ctx = new TestAuthContext { Scopes = ScopeSet("self.read") };

        ctx.HasAnyScope("self.read").Should().BeTrue();
    }

    [Fact]
    public void HasAnyScope_MultiPartialMatch_ReturnsTrue()
    {
        var ctx = new TestAuthContext { Scopes = ScopeSet("self.read") };

        ctx.HasAnyScope("auth.password.change", "self.read", "billing.payment.charge")
            .Should().BeTrue();
    }

    [Fact]
    public void HasAnyScope_MultiAllMatch_ReturnsTrue()
    {
        var ctx = new TestAuthContext
        {
            Scopes = ScopeSet("self.read", "self.write", "auth.password.change"),
        };

        ctx.HasAnyScope("self.read", "self.write").Should().BeTrue();
    }

    [Fact]
    public void HasAnyScope_NoneMatch_ReturnsFalse()
    {
        var ctx = new TestAuthContext { Scopes = ScopeSet("self.read") };

        ctx.HasAnyScope("auth.password.change", "billing.payment.charge").Should().BeFalse();
    }

    // ----------------------------------------------------------------------
    // HasAllScopes
    // ----------------------------------------------------------------------

    [Fact]
    public void HasAllScopes_NoArgs_ReturnsTrue()
    {
        // Adversarial: empty params — `All()` over empty returns true. Document
        // the vacuous-truth behavior. Callers passing a dynamic empty array
        // get an "all of nothing" pass; if that matters for an authz check,
        // the caller must guard at the call site.
        var ctx = new TestAuthContext { Scopes = ScopeSet() };

        ctx.HasAllScopes().Should().BeTrue();
    }

    [Fact]
    public void HasAllScopes_SinglePresent_ReturnsTrue()
    {
        var ctx = new TestAuthContext { Scopes = ScopeSet("self.read") };

        ctx.HasAllScopes("self.read").Should().BeTrue();
    }

    [Fact]
    public void HasAllScopes_PartialMatch_ReturnsFalse()
    {
        var ctx = new TestAuthContext { Scopes = ScopeSet("self.read") };

        ctx.HasAllScopes("self.read", "auth.password.change").Should().BeFalse();
    }

    [Fact]
    public void HasAllScopes_AllPresent_ReturnsTrue()
    {
        var ctx = new TestAuthContext
        {
            Scopes = ScopeSet("self.read", "self.write", "auth.password.change"),
        };

        ctx.HasAllScopes("self.read", "self.write").Should().BeTrue();
    }

    [Fact]
    public void HasAllScopes_NoneMatch_ReturnsFalse()
    {
        var ctx = new TestAuthContext { Scopes = ScopeSet("self.read") };

        ctx.HasAllScopes("auth.password.change", "billing.payment.charge").Should().BeFalse();
    }

    // ----------------------------------------------------------------------
    // IsStaff
    // ----------------------------------------------------------------------

    [Theory]
    [InlineData(OrgType.Admin, true)]
    [InlineData(OrgType.Support, true)]
    [InlineData(OrgType.Customer, false)]
    [InlineData(OrgType.ThirdParty, false)]
    [InlineData(OrgType.Affiliate, false)]
    public void IsStaff_MatchesAdminAndSupport(OrgType orgType, bool expected)
    {
        var ctx = new TestAuthContext { OrgType = orgType };

        ctx.IsStaff().Should().Be(expected);
    }

    [Fact]
    public void IsStaff_NullOrgType_ReturnsFalse()
    {
        // Adversarial: pre-auth context (OrgType not yet resolved) must not
        // be classified as staff.
        var ctx = new TestAuthContext { OrgType = null };

        ctx.IsStaff().Should().BeFalse();
    }

    // ----------------------------------------------------------------------
    // IsAdmin
    // ----------------------------------------------------------------------

    [Theory]
    [InlineData(OrgType.Admin, true)]
    [InlineData(OrgType.Support, false)]
    [InlineData(OrgType.Customer, false)]
    [InlineData(OrgType.ThirdParty, false)]
    [InlineData(OrgType.Affiliate, false)]
    public void IsAdmin_MatchesOnlyAdmin(OrgType orgType, bool expected)
    {
        var ctx = new TestAuthContext { OrgType = orgType };

        ctx.IsAdmin().Should().Be(expected);
    }

    [Fact]
    public void IsAdmin_NullOrgType_ReturnsFalse()
    {
        var ctx = new TestAuthContext { OrgType = null };

        ctx.IsAdmin().Should().BeFalse();
    }

    // ----------------------------------------------------------------------
    // IsForcedImpersonation / IsConsentImpersonation
    // ----------------------------------------------------------------------

    [Fact]
    public void IsForcedImpersonation_ImpersonationKindForce_ReturnsTrue()
    {
        var ctx = new TestAuthContext { ImpersonationKind = ImpersonationKind.Force };

        ctx.IsForcedImpersonation().Should().BeTrue();
    }

    [Fact]
    public void IsForcedImpersonation_ImpersonationKindConsent_ReturnsFalse()
    {
        // Adversarial: critical not to confuse Consent (OTP-authorized,
        // available to staff) with Force (silent, admin-only).
        var ctx = new TestAuthContext { ImpersonationKind = ImpersonationKind.Consent };

        ctx.IsForcedImpersonation().Should().BeFalse();
    }

    [Fact]
    public void IsForcedImpersonation_NullKind_ReturnsFalse()
    {
        var ctx = new TestAuthContext { ImpersonationKind = null };

        ctx.IsForcedImpersonation().Should().BeFalse();
    }

    [Fact]
    public void IsConsentImpersonation_ImpersonationKindConsent_ReturnsTrue()
    {
        var ctx = new TestAuthContext { ImpersonationKind = ImpersonationKind.Consent };

        ctx.IsConsentImpersonation().Should().BeTrue();
    }

    [Fact]
    public void IsConsentImpersonation_ImpersonationKindForce_ReturnsFalse()
    {
        var ctx = new TestAuthContext { ImpersonationKind = ImpersonationKind.Force };

        ctx.IsConsentImpersonation().Should().BeFalse();
    }

    [Fact]
    public void IsConsentImpersonation_NullKind_ReturnsFalse()
    {
        var ctx = new TestAuthContext { ImpersonationKind = null };

        ctx.IsConsentImpersonation().Should().BeFalse();
    }

    // ----------------------------------------------------------------------
    // IsImpersonatorStaff / IsImpersonatorAdmin
    // ----------------------------------------------------------------------

    [Theory]
    [InlineData(OrgType.Admin, true)]
    [InlineData(OrgType.Support, true)]
    [InlineData(OrgType.Customer, false)]
    [InlineData(OrgType.ThirdParty, false)]
    [InlineData(OrgType.Affiliate, false)]
    public void IsImpersonatorStaff_MatchesAdminAndSupport(OrgType orgType, bool expected)
    {
        var ctx = new TestAuthContext { ImpersonatorOrgType = orgType };

        ctx.IsImpersonatorStaff().Should().Be(expected);
    }

    [Fact]
    public void IsImpersonatorStaff_NullImpersonatorOrgType_ReturnsFalse()
    {
        // Adversarial: NOT impersonating → ImpersonatorOrgType is null →
        // IsImpersonatorStaff() is false. Must not silently report true.
        var ctx = new TestAuthContext { ImpersonatorOrgType = null };

        ctx.IsImpersonatorStaff().Should().BeFalse();
    }

    [Theory]
    [InlineData(OrgType.Admin, true)]
    [InlineData(OrgType.Support, false)]
    [InlineData(OrgType.Customer, false)]
    [InlineData(OrgType.ThirdParty, false)]
    [InlineData(OrgType.Affiliate, false)]
    public void IsImpersonatorAdmin_MatchesOnlyAdmin(OrgType orgType, bool expected)
    {
        var ctx = new TestAuthContext { ImpersonatorOrgType = orgType };

        ctx.IsImpersonatorAdmin().Should().Be(expected);
    }

    [Fact]
    public void IsImpersonatorAdmin_NullImpersonatorOrgType_ReturnsFalse()
    {
        var ctx = new TestAuthContext { ImpersonatorOrgType = null };

        ctx.IsImpersonatorAdmin().Should().BeFalse();
    }

    [Fact]
    public void IsImpersonatorAdmin_AgentOrgIsAdminButImpersonatedOrgIsCustomer_StillReportsAdmin()
    {
        // Adversarial: an Admin-org agent impersonating a Customer-org user
        // should report IsImpersonatorAdmin()=true (the AGENT is admin) and
        // IsAdmin()=false (the OPERATING org is customer). The two must NOT
        // be confused.
        var ctx = new TestAuthContext
        {
            OrgType = OrgType.Customer,
            ImpersonatorOrgType = OrgType.Admin,
        };

        ctx.IsAdmin().Should().BeFalse("operating org is Customer");
        ctx.IsImpersonatorAdmin().Should().BeTrue("agent's home org is Admin");
        ctx.IsStaff().Should().BeFalse();
        ctx.IsImpersonatorStaff().Should().BeTrue();
    }

    private static IReadOnlySet<string> ScopeSet(params string[] scopes) =>
        new HashSet<string>(scopes, StringComparer.Ordinal);
}
