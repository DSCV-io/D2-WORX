// -----------------------------------------------------------------------
// <copyright file="ScopesGeneratedTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Auth;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AwesomeAssertions;
using D2.Shared.Auth.Abstractions;
using Xunit;

/// <summary>
/// End-to-end smoke tests for the codegen-emitted <c>Scopes.g.cs</c> static
/// partial class. Probes structure (nested classes), specific constants,
/// helper behaviour, and wildcard expansion in the GrantedScopes dictionary.
/// </summary>
public sealed class ScopesGeneratedTests
{
    [Fact]
    public void Scopes_TypeExists()
    {
        var scopes_type = typeof(Scopes);

        scopes_type.Should().NotBeNull();
        scopes_type.IsAbstract.Should().BeTrue("static classes are abstract+sealed at IL");
        scopes_type.IsSealed.Should().BeTrue();
    }

    [Theory]
    [InlineData("Anon")]
    [InlineData("Self")]
    [InlineData("Auth")]
    [InlineData("Billing")]
    public void Scopes_HasNestedNamespaceClass(string nested_name)
    {
        // Adversarial: the nested-class structure is the discoverability
        // mechanism. Emitting a flat list would defeat the purpose.
        var nested = typeof(Scopes).GetNestedType(nested_name, BindingFlags.Public);

        nested.Should().NotBeNull(nested_name + " nested class must be emitted");
        nested.IsAbstract.Should().BeTrue();
        nested.IsSealed.Should().BeTrue();
    }

    [Fact]
    public void Scopes_SpecificConstants_HaveExpectedStringValues()
    {
        Scopes.Self.Read.Should().Be("self.read");
        Scopes.Self.Write.Should().Be("self.write");
        Scopes.Auth.Password.Change.Should().Be("auth.password.change");
        Scopes.Auth.User.Impersonate.Consent.Should().Be("auth.user.impersonate.consent");
        Scopes.Auth.User.Impersonate.Force.Should().Be("auth.user.impersonate.force");
        Scopes.Anon.Public.Health.Should().Be("anon.public.health");
        Scopes.Anon.Auth.Signin.Attempt.Should().Be("anon.auth.signin.attempt");
        Scopes.Billing.Payment.Charge.Should().Be("billing.payment.charge");
    }

    // ----------------------------------------------------------------------
    // Helper: GetActionSensitivity
    // ----------------------------------------------------------------------

    [Theory]
    [InlineData("self.read", ActionSensitivity.Routine)]
    [InlineData("auth.password.change", ActionSensitivity.Sensitive)]
    [InlineData("auth.user.impersonate.consent", ActionSensitivity.Sensitive)]
    [InlineData("auth.user.impersonate.force", ActionSensitivity.Critical)]
    [InlineData("billing.payment.charge", ActionSensitivity.Critical)]
    public void GetActionSensitivity_KnownScope_ReturnsClassifiedValue(
        string scope, ActionSensitivity expected)
    {
        Scopes.GetActionSensitivity(scope).Should().Be(expected);
    }

    [Fact]
    public void GetActionSensitivity_UnknownScope_ReturnsRoutineDefensiveDefault()
    {
        // Adversarial: defensive default is Routine — least restrictive.
        // Document the choice; an unknown scope shouldn't auto-escalate audit
        // verbosity.
        Scopes.GetActionSensitivity("totally.fake.scope").Should().Be(ActionSensitivity.Routine);
        Scopes.GetActionSensitivity(string.Empty).Should().Be(ActionSensitivity.Routine);
    }

    // ----------------------------------------------------------------------
    // Helper: IsImpersonationBlocked
    // ----------------------------------------------------------------------

    [Theory]
    [InlineData("auth.password.change", true)]
    [InlineData("auth.user.impersonate.consent", true)]
    [InlineData("auth.user.impersonate.force", true)]
    [InlineData("billing.payment.charge", true)]
    [InlineData("self.read", false)]
    [InlineData("self.write", false)]
    [InlineData("anon.public.health", false)]
    public void IsImpersonationBlocked_MatchesSpec(string scope, bool expected)
    {
        Scopes.IsImpersonationBlocked(scope).Should().Be(expected);
    }

    [Fact]
    public void IsImpersonationBlocked_UnknownScope_ReturnsFalse()
    {
        Scopes.IsImpersonationBlocked("not.a.real.scope").Should().BeFalse();
    }

    // ----------------------------------------------------------------------
    // Helper: IsAnonymous
    // ----------------------------------------------------------------------

    [Theory]
    [InlineData("anon.public.health", true)]
    [InlineData("anon.auth.signin.attempt", true)]
    [InlineData("self.read", false)]
    [InlineData("auth.password.change", false)]
    public void IsAnonymous_PrefixCheck(string scope, bool expected)
    {
        Scopes.IsAnonymous(scope).Should().Be(expected);
    }

    [Fact]
    public void IsAnonymous_CaseSensitive()
    {
        // Adversarial: scope names are case-sensitive (RFC 6749 §3.3).
        Scopes.IsAnonymous("ANON.public.health").Should().BeFalse();
    }

    [Fact]
    public void IsAnonymous_EmptyAndGarbage_ReturnsFalse()
    {
        Scopes.IsAnonymous(string.Empty).Should().BeFalse();
        Scopes.IsAnonymous("anonymous").Should().BeFalse(
            "anonymous must NOT match — only the literal anon. prefix counts");
    }

    // ----------------------------------------------------------------------
    // Helper: IsKnown
    // ----------------------------------------------------------------------

    [Theory]
    [InlineData("self.read", true)]
    [InlineData("billing.payment.charge", true)]
    [InlineData("anon.public.health", true)]
    [InlineData("not.a.real.scope", false)]
    [InlineData("self.READ", false)] // case-sensitive
    public void IsKnown_OnlyMatchesSpecScopes(string scope, bool expected)
    {
        Scopes.IsKnown(scope).Should().Be(expected);
    }

    [Fact]
    public void IsKnown_EmptyString_ReturnsFalse()
    {
        Scopes.IsKnown(string.Empty).Should().BeFalse();
    }

    // ----------------------------------------------------------------------
    // Helper: IsGrantedTo
    // ----------------------------------------------------------------------

    [Theory]
    [InlineData(OrgType.Customer, Role.Owner, "billing.payment.charge", true)]
    [InlineData(OrgType.Admin, Role.Owner, "auth.user.impersonate.force", true)]
    [InlineData(OrgType.Admin, Role.Auditor, "auth.user.impersonate.force", false)]
    [InlineData(OrgType.Support, Role.Officer, "auth.user.impersonate.consent", true)]
    [InlineData(OrgType.Support, Role.Owner, "auth.user.impersonate.force", false)]
    [InlineData(OrgType.Customer, Role.Agent, "billing.payment.charge", false)]
    [InlineData(OrgType.Customer, Role.Owner, "self.read", true)]
    [InlineData(OrgType.ThirdParty, Role.Owner, "self.write", true)]
    public void IsGrantedTo_MatchesSpec(
        OrgType orgType, Role role, string scope, bool expected)
    {
        Scopes.IsGrantedTo(scope, orgType, role).Should().Be(expected);
    }

    [Fact]
    public void IsGrantedTo_UnknownScope_ReturnsFalse()
    {
        Scopes.IsGrantedTo("not.a.real.scope", OrgType.Admin, Role.Owner).Should().BeFalse();
    }

    // ----------------------------------------------------------------------
    // AllScopes / AllAnonymousScopes / AllImpersonationBlockedScopes
    // ----------------------------------------------------------------------

    [Fact]
    public void AllScopes_ContainsEverySpecEntry()
    {
        Scopes.AllScopes.Should().Contain([
            "self.read", "self.write",
            "auth.password.change",
            "auth.user.impersonate.consent",
            "auth.user.impersonate.force",
            "anon.public.health",
            "anon.auth.signin.attempt",
            "billing.payment.charge",
        ]);
    }

    [Fact]
    public void AllAnonymousScopes_ContainsOnlyAnonPrefixedScopes()
    {
        Scopes.AllAnonymousScopes.Should().NotBeEmpty();
        Scopes.AllAnonymousScopes.Should().AllSatisfy(s =>
            s.Should().StartWith("anon.", "AllAnonymousScopes must contain only anon.* entries"));
    }

    [Fact]
    public void AllImpersonationBlockedScopes_NeverContainsAnonScopes()
    {
        // Adversarial: anon.* scopes don't have an authenticated context, so
        // impersonation blocking is meaningless for them. Blocking an anon
        // scope would indicate a spec error.
        Scopes.AllImpersonationBlockedScopes
            .Where(s => s.StartsWith("anon.", StringComparison.Ordinal))
            .Should().BeEmpty();
    }

    [Fact]
    public void AllScopes_IsSuperSetOfAnonAndBlockedAndGranted()
    {
        // Sanity: every anon, every blocked, and every granted scope must be
        // a known scope. Catches generator inconsistencies.
        Scopes.AllAnonymousScopes.Should().BeSubsetOf(Scopes.AllScopes);
        Scopes.AllImpersonationBlockedScopes.Should().BeSubsetOf(Scopes.AllScopes);

        var all_granted = Scopes.GrantedScopes.Values.SelectMany(s => s).ToHashSet();
        all_granted.Should().BeSubsetOf(Scopes.AllScopes);
    }

    // ----------------------------------------------------------------------
    // GrantedScopes — wildcard expansion
    // ----------------------------------------------------------------------

    [Fact]
    public void GrantedScopes_HasEntryForEveryOrgTypeRoleCombination()
    {
        // Adversarial: the spec uses `{ "*": ["*"] }` for self.* scopes — the
        // emitter must expand wildcards against the full OrgType x Role
        // cross product (5 * 4 = 20 keys). Missing pairs would silently
        // deny scopes for some (orgType, role) combos.
        var expected_count = Enum.GetValues<OrgType>().Length
            * Enum.GetValues<Role>().Length;

        Scopes.GrantedScopes.Should().HaveCount(expected_count);
    }

    [Fact]
    public void GrantedScopes_EveryOrgTypeRolePair_HasSelfReadWrite()
    {
        // Adversarial: self.* uses { "*": ["*"] } in the spec — every pair
        // must end up with self.read + self.write. If wildcard expansion
        // dropped a tuple, this test catches it.
        foreach (var orgType in Enum.GetValues<OrgType>())
        {
            foreach (var role in Enum.GetValues<Role>())
            {
                var key = (orgType, role);
                Scopes.GrantedScopes.Should().ContainKey(key);
                Scopes.GrantedScopes[key].Should().Contain(
                    ["self.read", "self.write"],
                    $"({orgType},{role}) must inherit wildcard self.* grants");
            }
        }
    }

    [Fact]
    public void GrantedScopes_BillingChargeOnlyOnCustomerOwner()
    {
        // Adversarial: billing.payment.charge is { "Customer": ["Owner"] }.
        // If wildcard expansion accidentally fanned this out, every org would
        // be charging cards.
        var pairs_with_billing = Scopes.GrantedScopes
            .Where(kvp => kvp.Value.Contains("billing.payment.charge"))
            .Select(kvp => kvp.Key)
            .ToList();

        pairs_with_billing.Should().HaveCount(1);
        pairs_with_billing.Should().Contain((OrgType.Customer, Role.Owner));
    }

    [Fact]
    public void GrantedScopes_ForceImpersonationOnlyOnAdminOrgs()
    {
        // Adversarial: spec restricts force-impersonation to Admin org only.
        // Any leak to Support / Customer / ThirdParty / Affiliate is a
        // critical security defect.
        var pairs_with_force = Scopes.GrantedScopes
            .Where(kvp => kvp.Value.Contains("auth.user.impersonate.force"))
            .Select(kvp => kvp.Key)
            .ToList();

        pairs_with_force.Should().NotBeEmpty();
        pairs_with_force.Should().AllSatisfy(pair =>
            pair.Item1.Should().Be(
                OrgType.Admin,
                "force-impersonation must be Admin-only"));
    }

    [Fact]
    public void GrantedScopes_DictionaryIsReadOnly()
    {
        // Adversarial: callers must not be able to mutate the granted-scopes
        // dictionary at runtime. The exposed view is IReadOnlyDictionary; the
        // value-sets are IReadOnlySet. Any editable surface would be a security
        // hole (one rogue handler could grant itself anything).
        var view = Scopes.GrantedScopes;

        view.Should().BeAssignableTo<IReadOnlyDictionary<(OrgType, Role), IReadOnlySet<string>>>();

        var first = view.First();
        first.Value.Should().BeAssignableTo<IReadOnlySet<string>>();
    }
}
