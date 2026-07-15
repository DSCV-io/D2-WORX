// -----------------------------------------------------------------------
// <copyright file="ScopesGeneratedTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Auth;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AwesomeAssertions;
using DcsvIo.D2.Auth.Abstractions;
using Xunit;

/// <summary>
/// End-to-end smoke tests for the codegen-emitted <c>Scopes.g.cs</c> static
/// partial class on the **public** dual-values half (Anon + Self only).
/// Product scopes (Auth / Billing / internal.kc.*) live on
/// <c>DcsvIo.D2.Private.Auth.ProductScopes</c> and are not pinned here.
/// </summary>
public sealed class ScopesGeneratedTests
{
    [Fact]
    public void Scopes_TypeExists()
    {
        var scopesType = typeof(Scopes);

        scopesType.Should().NotBeNull();
        scopesType.IsAbstract.Should().BeTrue("static classes are abstract+sealed at IL");
        scopesType.IsSealed.Should().BeTrue();
    }

    [Theory]
    [InlineData("Anon")]
    [InlineData("Self")]
    public void Scopes_HasNestedNamespaceClass(string nestedName)
    {
        // Adversarial: the nested-class structure is the discoverability
        // mechanism. Emitting a flat list would defeat the purpose.
        var nested = typeof(Scopes).GetNestedType(nestedName, BindingFlags.Public);

        nested.Should().NotBeNull(nestedName + " nested class must be emitted");
        nested.IsAbstract.Should().BeTrue();
        nested.IsSealed.Should().BeTrue();
    }

    [Theory]
    [InlineData("Auth")]
    [InlineData("Billing")]
    [InlineData("Internal")]
    public void Scopes_ProductNestedClasses_AreAbsentOnPublicCatalog(string nestedName)
    {
        // Dual-values split: product trees emit only on ProductScopes.
        typeof(Scopes).GetNestedType(nestedName, BindingFlags.Public).Should().BeNull(
            nestedName + " must not appear on public Scopes after dual-values strip");
    }

    [Fact]
    public void Scopes_SpecificConstants_HaveExpectedStringValues()
    {
        Scopes.Self.Read.Should().Be("self.read");
        Scopes.Self.Write.Should().Be("self.write");
        Scopes.Anon.Public.Health.Should().Be("anon.public.health");
    }

    // ----------------------------------------------------------------------
    // Helper: GetActionSensitivity
    // ----------------------------------------------------------------------

    [Theory]
    [InlineData("self.read", ActionSensitivity.Routine)]
    [InlineData("self.write", ActionSensitivity.Routine)]
    [InlineData("anon.public.health", ActionSensitivity.Routine)]
    public void GetActionSensitivity_KnownPublicScope_ReturnsClassifiedValue(
        string scope, ActionSensitivity expected)
    {
        Scopes.GetActionSensitivity(scope).Should().Be(expected);
    }

    [Fact]
    public void GetActionSensitivity_UnknownScope_ReturnsRoutineDefensiveDefault()
    {
        // Adversarial: defensive default is Routine — least restrictive.
        // Product scopes are unknown on the public catalog and must not
        // auto-escalate (they resolve via ProductScopes on private hosts).
        Scopes.GetActionSensitivity("totally.fake.scope").Should().Be(ActionSensitivity.Routine);
        Scopes.GetActionSensitivity("billing.payment.charge").Should().Be(ActionSensitivity.Routine);
        Scopes.GetActionSensitivity(string.Empty).Should().Be(ActionSensitivity.Routine);
    }

    // ----------------------------------------------------------------------
    // Helper: IsImpersonationBlocked
    // ----------------------------------------------------------------------

    [Theory]
    [InlineData("self.read", false)]
    [InlineData("self.write", false)]
    [InlineData("anon.public.health", false)]
    public void IsImpersonationBlocked_PublicScopes_MatchSpec(string scope, bool expected)
    {
        Scopes.IsImpersonationBlocked(scope).Should().Be(expected);
    }

    [Fact]
    public void IsImpersonationBlocked_UnknownScope_ReturnsFalse()
    {
        Scopes.IsImpersonationBlocked("not.a.real.scope").Should().BeFalse();
        Scopes.IsImpersonationBlocked("auth.password.change").Should().BeFalse(
            "product scopes are not on the public catalog");
    }

    // ----------------------------------------------------------------------
    // Helper: IsAnonymous
    // ----------------------------------------------------------------------

    [Theory]
    [InlineData("anon.public.health", true)]
    [InlineData("self.read", false)]
    [InlineData("self.write", false)]
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
    [InlineData("self.write", true)]
    [InlineData("anon.public.health", true)]
    [InlineData("billing.payment.charge", false)]
    [InlineData("internal.kc.sign", false)]
    [InlineData("not.a.real.scope", false)]
    [InlineData("self.READ", false)] // case-sensitive
    public void IsKnown_OnlyMatchesPublicSpecScopes(string scope, bool expected)
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
    [InlineData(OrgType.Customer, Role.Owner, "self.read", true)]
    [InlineData(OrgType.Customer, Role.Agent, "self.write", true)]
    [InlineData(OrgType.Admin, Role.Owner, "self.read", true)]
    [InlineData(OrgType.ThirdParty, Role.Owner, "self.write", true)]
    [InlineData(OrgType.Customer, Role.Owner, "billing.payment.charge", false)]
    [InlineData(OrgType.Admin, Role.Owner, "auth.user.impersonate.force", false)]
    public void IsGrantedTo_MatchesPublicSpec(
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
    public void AllScopes_ContainsEveryPublicSpecEntry()
    {
        Scopes.AllScopes.Should().BeEquivalentTo(
            ["anon.public.health", "self.read", "self.write"]);
    }

    [Fact]
    public void AllScopes_ExcludesProductCatalogValues()
    {
        Scopes.AllScopes.Should().NotContain([
            "auth.password.change",
            "auth.user.impersonate.consent",
            "auth.user.impersonate.force",
            "billing.payment.charge",
            "internal.kc.sign",
            "internal.kc.keyring",
            "internal.kc.issue",
            "internal.kc.cacert",
        ]);
    }

    [Fact]
    public void AllAnonymousScopes_ContainsOnlyAnonPrefixedScopes()
    {
        Scopes.AllAnonymousScopes.Should().NotBeEmpty();
        Scopes.AllAnonymousScopes.Should().AllSatisfy(s =>
            s.Should().StartWith("anon.", "AllAnonymousScopes must contain only anon.* entries"));
        Scopes.AllAnonymousScopes.Should().BeEquivalentTo(["anon.public.health"]);
    }

    [Fact]
    public void AllImpersonationBlockedScopes_IsEmptyOnPublicCatalog()
    {
        // Public open half has no impersonation-blocked scopes; product
        // blocked scopes live on ProductScopes.
        Scopes.AllImpersonationBlockedScopes.Should().BeEmpty();
    }

    [Fact]
    public void AllScopes_IsSuperSetOfAnonAndBlockedAndGranted()
    {
        // Sanity: every anon, every blocked, and every granted scope must be
        // a known scope. Catches generator inconsistencies.
        Scopes.AllAnonymousScopes.Should().BeSubsetOf(Scopes.AllScopes);
        Scopes.AllImpersonationBlockedScopes.Should().BeSubsetOf(Scopes.AllScopes);

        var allGranted = Scopes.GrantedScopes.Values.SelectMany(s => s).ToHashSet();
        allGranted.Should().BeSubsetOf(Scopes.AllScopes);
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
        var expectedCount = Enum.GetValues<OrgType>().Length
            * Enum.GetValues<Role>().Length;

        Scopes.GrantedScopes.Should().HaveCount(expectedCount);
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
    public void GrantedScopes_ContainsNoProductScopes()
    {
        // Dual-values residual pin: product grants must not leak onto public
        // GrantedScopes after the open-half strip.
        var allGranted = Scopes.GrantedScopes.Values.SelectMany(s => s).ToHashSet(StringComparer.Ordinal);

        allGranted.Should().NotContain("billing.payment.charge");
        allGranted.Should().NotContain("auth.user.impersonate.force");
        allGranted.Should().NotContain("internal.kc.sign");
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
