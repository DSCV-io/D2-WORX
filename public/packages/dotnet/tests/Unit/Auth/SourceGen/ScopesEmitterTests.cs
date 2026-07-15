// -----------------------------------------------------------------------
// <copyright file="ScopesEmitterTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Auth.SourceGen;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using AwesomeAssertions;
using DcsvIo.D2.Auth.Scopes.SourceGen;
using Xunit;

/// <summary>
/// Adversarial coverage for <see cref="ScopesEmitter.Emit"/>. Drives the
/// per-scope semantic validation (D2SCP002–008) and asserts the emitted source
/// shape (constants, helper methods, wildcard expansion).
/// </summary>
public sealed class ScopesEmitterTests
{
    private static readonly IReadOnlyList<string> sr_orgTypes =
        ["Admin", "Support", "Customer", "ThirdParty", "Affiliate"];

    private static readonly IReadOnlyList<string> sr_roles =
        ["Auditor", "Agent", "Officer", "Owner"];

    // ----------------------------------------------------------------------
    // Happy-path snapshots
    // ----------------------------------------------------------------------

    [Fact]
    public void Emit_SingleAnonScope_NoGrantedToBlock()
    {
        var spec = SpecOf(Scope("anon.public.health", anonOk: true));

        var result = ScopesEmitter.Emit(spec, sr_orgTypes, sr_roles);

        result.Diagnostics.Should().BeEmpty();

        // Constant was emitted.
        result.GeneratedSource.Should().Contain(
            "public const string Health = \"anon.public.health\";");

        // AnonymousScopes set carries the entry.
        result.GeneratedSource.Should().Contain("sr_anonymousScopes")
            .And.Contain("\"anon.public.health\",");

        // No granted-to entries in the dictionary backing for this anon scope.
        result.GeneratedSource.Should().NotContain("OrgType.Admin, Role.Owner");
    }

    [Fact]
    public void Emit_NonAnonWithFullWildcard_ExpandsToCartesianProduct()
    {
        var spec = SpecOf(Scope("self.read", grantedTo: Granted(("*", ["*"]))));

        var result = ScopesEmitter.Emit(spec, sr_orgTypes, sr_roles);

        result.Diagnostics.Should().BeEmpty();

        // Every (OrgType × Role) pair must have a grantedTo entry.
        foreach (var org in sr_orgTypes)
        {
            foreach (var role in sr_roles)
                result.GeneratedSource.Should().Contain($"[(OrgType.{org}, Role.{role})]");
        }
    }

    [Fact]
    public void Emit_NonAnonWithSingleEntry_EmitsExactPair()
    {
        var spec = SpecOf(
            Scope("auth.user.impersonate.consent", grantedTo: Granted(("Customer", ["Owner"]))));

        var result = ScopesEmitter.Emit(spec, sr_orgTypes, sr_roles);

        result.Diagnostics.Should().BeEmpty();

        // Exactly one (OrgType.Customer, Role.Owner) entry.
        result.GeneratedSource.Should().Contain("[(OrgType.Customer, Role.Owner)]");

        // Other org types must NOT appear in any granted-to dictionary entry.
        result.GeneratedSource.Should().NotContain("[(OrgType.Admin,");
        result.GeneratedSource.Should().NotContain("[(OrgType.Affiliate,");
    }

    [Fact]
    public void Emit_OrgWildcardWithSpecificRole_ExpandsAllOrgs()
    {
        var spec = SpecOf(Scope("billing.read", grantedTo: Granted(("*", ["Owner"]))));

        var result = ScopesEmitter.Emit(spec, sr_orgTypes, sr_roles);

        result.Diagnostics.Should().BeEmpty();

        foreach (var org in sr_orgTypes)
            result.GeneratedSource.Should().Contain($"[(OrgType.{org}, Role.Owner)]");

        // Non-Owner roles must not appear paired with these orgs for THIS scope.
        result.GeneratedSource.Should().NotContain("[(OrgType.Admin, Role.Auditor)]");
    }

    [Fact]
    public void Emit_RoleWildcardWithSpecificOrg_ExpandsAllRoles()
    {
        var spec = SpecOf(Scope("ops.read", grantedTo: Granted(("Customer", ["*"]))));

        var result = ScopesEmitter.Emit(spec, sr_orgTypes, sr_roles);

        result.Diagnostics.Should().BeEmpty();

        foreach (var role in sr_roles)
            result.GeneratedSource.Should().Contain($"[(OrgType.Customer, Role.{role})]");

        // Other org types must not show up.
        result.GeneratedSource.Should().NotContain("[(OrgType.Admin, Role.Owner)]");
    }

    // ----------------------------------------------------------------------
    // Helpers / generated-API surface
    // ----------------------------------------------------------------------

    [Fact]
    public void Emit_HelperMethodsAndCollections_AllPresent()
    {
        var spec = SpecOf(
            Scope("anon.public.health", anonOk: true),
            Scope("auth.password.change", impBlocked: true, grantedTo: Granted(("*", ["*"]))));

        var result = ScopesEmitter.Emit(spec, sr_orgTypes, sr_roles);

        result.Diagnostics.Should().BeEmpty();

        // Helper functions.
        result.GeneratedSource.Should().Contain(
            "public static ActionSensitivity GetActionSensitivity(string scope)");
        result.GeneratedSource.Should().Contain(
            "public static bool IsImpersonationBlocked(string scope)");
        result.GeneratedSource.Should().Contain("public static bool IsAnonymous(string scope)");
        result.GeneratedSource.Should().Contain("public static bool IsKnown(string scope)");
        result.GeneratedSource.Should().Contain(
            "public static bool IsGrantedTo(string scope, OrgType orgType, Role role)");

        // Public read-only set surfaces.
        result.GeneratedSource.Should().Contain("public static IReadOnlySet<string> AllScopes");
        result.GeneratedSource.Should().Contain(
            "public static IReadOnlySet<string> AllAnonymousScopes");
        result.GeneratedSource.Should().Contain(
            "public static IReadOnlySet<string> AllImpersonationBlockedScopes");
        result.GeneratedSource.Should().Contain("GrantedScopes");
    }

    // ----------------------------------------------------------------------
    // D2SCP002 — unknown enum values
    // ----------------------------------------------------------------------

    [Fact]
    public void Emit_UnknownActionSensitivity_EmitsD2SCP002()
    {
        var spec = SpecOf(Scope(
            "self.read",
            sensitivity: "NotARealSensitivity",
            grantedTo: Granted(("*", ["*"]))));

        var result = ScopesEmitter.Emit(spec, sr_orgTypes, sr_roles);

        result.Diagnostics.Should().ContainSingle(
            d => d.DescriptorId == DiagnosticIds.UnknownEnumValue);
        var diag = result.Diagnostics.Single(d => d.DescriptorId == DiagnosticIds.UnknownEnumValue);
        ((string)diag.Args[2]).Should().Be("ActionSensitivity");
        ((string)diag.Args[3]).Should().Be("NotARealSensitivity");
    }

    [Fact]
    public void Emit_UnknownOrgTypeInGrantedTo_EmitsD2SCP002()
    {
        var spec = SpecOf(
            Scope("self.read", grantedTo: Granted(("UnknownOrg", ["Owner"]))));

        var result = ScopesEmitter.Emit(spec, sr_orgTypes, sr_roles);

        var diag = result.Diagnostics.Single(d => d.DescriptorId == DiagnosticIds.UnknownEnumValue);
        ((string)diag.Args[2]).Should().Be("OrgType");
        ((string)diag.Args[3]).Should().Be("UnknownOrg");
    }

    [Fact]
    public void Emit_UnknownRoleInGrantedTo_EmitsD2SCP002()
    {
        var spec = SpecOf(
            Scope("self.read", grantedTo: Granted(("Admin", ["Wizard"]))));

        var result = ScopesEmitter.Emit(spec, sr_orgTypes, sr_roles);

        var diag = result.Diagnostics.Single(d => d.DescriptorId == DiagnosticIds.UnknownEnumValue);
        ((string)diag.Args[2]).Should().Be("Role");
        ((string)diag.Args[3]).Should().Be("Wizard");
    }

    // ----------------------------------------------------------------------
    // D2SCP003 — invalid scope name
    // ----------------------------------------------------------------------

    [Theory]
    [InlineData("UPPER.case", "lowercase letter")]
    [InlineData("foo..bar", "consecutive dots")]
    [InlineData(".leading.dot", "leading or trailing dot")]
    [InlineData("trailing.dot.", "leading or trailing dot")]
    [InlineData("1numeric.start", "lowercase letter")]
    [InlineData("foo.bar-baz", "invalid character")]
    [InlineData("foo.bar baz", "invalid character")]
    [InlineData("singlesegment", "at least 2 dot-separated segments")]
    [InlineData("foo.b@d", "invalid character")]
    public void Emit_InvalidScopeName_EmitsD2SCP003(string scopeName, string reasonContains)
    {
        var spec = SpecOf(Scope(scopeName, anonOk: true));

        var result = ScopesEmitter.Emit(spec, sr_orgTypes, sr_roles);

        var diag = result.Diagnostics.Single(d => d.DescriptorId == DiagnosticIds.InvalidScopeName);
        ((string)diag.Args[0]).Should().Be(scopeName);
        ((string)diag.Args[1]).Should().Contain(reasonContains);
    }

    // ----------------------------------------------------------------------
    // D2SCP004 — duplicate scope name
    // ----------------------------------------------------------------------

    [Fact]
    public void Emit_DuplicateScopeName_EmitsD2SCP004()
    {
        var spec = SpecOf(
            Scope("self.read", grantedTo: Granted(("*", ["*"]))),
            Scope("self.read", grantedTo: Granted(("*", ["*"]))));

        var result = ScopesEmitter.Emit(spec, sr_orgTypes, sr_roles);

        result.Diagnostics.Should().ContainSingle(
            d => d.DescriptorId == DiagnosticIds.DuplicateScope);
        var diag = result.Diagnostics.Single(d => d.DescriptorId == DiagnosticIds.DuplicateScope);
        ((string)diag.Args[0]).Should().Be("self.read");
    }

    // ----------------------------------------------------------------------
    // D2SCP005 — anon scope marked impersonationBlocked (warning)
    // ----------------------------------------------------------------------

    [Fact]
    public void Emit_AnonScopeWithImpersonationBlocked_EmitsD2SCP005Warning()
    {
        var spec = SpecOf(Scope("anon.public.health", anonOk: true, impBlocked: true));

        var result = ScopesEmitter.Emit(spec, sr_orgTypes, sr_roles);

        result.Diagnostics.Should().ContainSingle(
            d => d.DescriptorId == DiagnosticIds.AnonImpersonationBlockedNoise);

        // Warning is non-fatal — the scope still emits.
        result.GeneratedSource.Should().Contain("\"anon.public.health\"");
    }

    // ----------------------------------------------------------------------
    // D2SCP006 — empty role array
    // ----------------------------------------------------------------------

    [Fact]
    public void Emit_EmptyRoleArray_EmitsD2SCP006AndDropsScope()
    {
        var spec = SpecOf(Scope("self.read", grantedTo: Granted(("*", []))));

        var result = ScopesEmitter.Emit(spec, sr_orgTypes, sr_roles);

        var diag = result.Diagnostics.Single(d => d.DescriptorId == DiagnosticIds.EmptyRoleArray);
        ((string)diag.Args[0]).Should().Be("self.read");
        ((string)diag.Args[1]).Should().Be("*");

        // Scope was dropped — must not appear as a constant.
        result.GeneratedSource.Should().NotContain("public const string Read = \"self.read\";");
    }

    // ----------------------------------------------------------------------
    // D2SCP008 — non-anon scope missing grantedTo (separate from "anon" rule)
    // ----------------------------------------------------------------------

    [Fact]
    public void Emit_NonAnonMissingGrantedTo_EmitsD2SCP008()
    {
        var spec = SpecOf(Scope("self.read"));

        var result = ScopesEmitter.Emit(spec, sr_orgTypes, sr_roles);

        result.Diagnostics.Should().ContainSingle(
            d => d.DescriptorId == DiagnosticIds.MissingGrantedTo);
    }

    [Fact]
    public void Emit_InternalScopeMissingGrantedTo_IsExemptFromD2SCP008_AndEmitsConstant()
    {
        // internal.* workload scopes are granted by the internal transaction-token mint at
        // the Edge boundary, NOT the org-role grant matrix, so omitting grantedTo is
        // legitimate (no D2SCP008) — yet they are NOT anonymous (no user org-role can ever
        // hold them, which is the intended reachability for a service-to-service scope).
        var spec = SpecOf(Scope("internal.kc.sign", sensitivity: "Critical", impBlocked: true));

        var result = ScopesEmitter.Emit(spec, sr_orgTypes, sr_roles);

        result.Diagnostics.Should().NotContain(
            d => d.DescriptorId == DiagnosticIds.MissingGrantedTo,
            "internal.* workload scopes are token-granted, not org-role-granted");
        result.GeneratedSource.Should().Contain(
            "\"internal.kc.sign\"",
            "the internal scope constant is still emitted despite omitting grantedTo");
    }

    // ----------------------------------------------------------------------
    // D2SCP007 — tree-position collision
    // ----------------------------------------------------------------------

    [Fact]
    public void Emit_TreePositionCollision_EmitsD2SCP007AndDropsParent()
    {
        var spec = SpecOf(
            Scope("auth.user.impersonate", grantedTo: Granted(("Admin", ["Owner"]))),
            Scope("auth.user.impersonate.force", grantedTo: Granted(("Admin", ["Owner"]))));

        var result = ScopesEmitter.Emit(spec, sr_orgTypes, sr_roles);

        result.Diagnostics.Should().ContainSingle(
            d => d.DescriptorId == DiagnosticIds.TreePositionCollision);

        // Parent is dropped; child remains.
        result.GeneratedSource.Should().Contain("\"auth.user.impersonate.force\"");
        result.GeneratedSource.Should().NotContain("\"auth.user.impersonate\",");
    }

    // ----------------------------------------------------------------------
    // Adversarial — unusual scope shapes still emit
    // ----------------------------------------------------------------------

    [Fact]
    public void Emit_VeryLongScopeName_EmitsCleanly()
    {
        // 200+ char single segment (just dots between two-segment minimums).
        const string longSegment =
            "abcdefghijklmnopqrstuvwxyz0123456789abcdefghijklmnopqrstuvwxyz0123456789"
            + "abcdefghijklmnopqrstuvwxyz0123456789abcdefghijklmnopqrstuvwxyz0123456789";
        var name = $"foo.{longSegment}";

        var spec = SpecOf(Scope(name, grantedTo: Granted(("*", ["*"]))));

        var result = ScopesEmitter.Emit(spec, sr_orgTypes, sr_roles);

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSource.Should().Contain($"\"{name}\"");
    }

    [Fact]
    public void Emit_FiveSegmentScopeName_EmitsNestedClassChain()
    {
        var spec = SpecOf(Scope("a.b.c.d.e", grantedTo: Granted(("*", ["*"]))));

        var result = ScopesEmitter.Emit(spec, sr_orgTypes, sr_roles);

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSource.Should().Contain("public static class A");
        result.GeneratedSource.Should().Contain("public static class B");
        result.GeneratedSource.Should().Contain("public static class C");
        result.GeneratedSource.Should().Contain("public static class D");
        result.GeneratedSource.Should().Contain("public const string E = \"a.b.c.d.e\";");
    }

    [Fact]
    public void Emit_ZeroScopes_StillEmitsClassShell()
    {
        var spec = new ScopesSpec(ImmutableArray<ScopeEntry>.Empty);

        var result = ScopesEmitter.Emit(spec, sr_orgTypes, sr_roles);

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSource.Should().Contain("public static partial class Scopes");
    }

    [Fact]
    public void Emit_HundredScopes_DoesNotDegrade()
    {
        var entries = ImmutableArray.CreateBuilder<ScopeEntry>();
        for (var i = 0; i < 100; i++)
        {
            entries.Add(new ScopeEntry(
                Name: $"bulk.scope{i}",
                Description: $"Bulk scope {i}",
                ActionSensitivity: "Routine",
                ImpersonationBlocked: false,
                GrantedTo: new Dictionary<string, ImmutableArray<string>>(StringComparer.Ordinal)
                {
                    ["*"] = ["Owner"],
                }));
        }

        var spec = new ScopesSpec(entries.ToImmutable());

        var result = ScopesEmitter.Emit(spec, sr_orgTypes, sr_roles);

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSource.Should().Contain("\"bulk.scope0\"");
        result.GeneratedSource.Should().Contain("\"bulk.scope99\"");
    }

    // ----------------------------------------------------------------------
    // Determinism / cache stability — identical inputs produce identical output
    // ----------------------------------------------------------------------

    [Fact]
    public void Emit_IdenticalInputs_ProduceIdenticalSource()
    {
        // Critical for incremental-generator caching: re-runs with the same
        // input must produce the same output (else cache is defeated).
        var spec = SpecOf(
            Scope("self.read", grantedTo: Granted(("*", ["*"]))),
            Scope("self.write", grantedTo: Granted(("*", ["*"]))));

        var first = ScopesEmitter.Emit(spec, sr_orgTypes, sr_roles);
        var second = ScopesEmitter.Emit(spec, sr_orgTypes, sr_roles);

        Normalize(second.GeneratedSource).Should().Be(Normalize(first.GeneratedSource));
    }

    [Fact]
    public void Emit_ScopesInDifferentOrder_ProduceIdenticalSource()
    {
        // Ordering invariance is what makes downstream caches stable.
        var forward = SpecOf(
            Scope("a.read", grantedTo: Granted(("*", ["*"]))),
            Scope("z.read", grantedTo: Granted(("*", ["*"]))));
        var reverse = SpecOf(
            Scope("z.read", grantedTo: Granted(("*", ["*"]))),
            Scope("a.read", grantedTo: Granted(("*", ["*"]))));

        var forwardResult = ScopesEmitter.Emit(forward, sr_orgTypes, sr_roles);
        var reverseResult = ScopesEmitter.Emit(reverse, sr_orgTypes, sr_roles);

        Normalize(reverseResult.GeneratedSource).Should()
            .Be(Normalize(forwardResult.GeneratedSource));
    }

    // ----------------------------------------------------------------------
    // Helpers
    // ----------------------------------------------------------------------

    private static ScopesSpec SpecOf(params ScopeEntry[] entries) =>
        new([.. entries]);

    private static ScopeEntry Scope(
        string name,
        string sensitivity = "Routine",
        bool impBlocked = false,
        bool anonOk = false,
        IReadOnlyDictionary<string, ImmutableArray<string>>? grantedTo = null)
    {
        // Anonymous scopes legitimately have no grantedTo; everything else
        // gets the supplied (or null — which then triggers D2SCP008).
        var effectiveGranted = anonOk ? null : grantedTo;
        return new ScopeEntry(
            Name: name,
            Description: null,
            ActionSensitivity: sensitivity,
            ImpersonationBlocked: impBlocked,
            GrantedTo: effectiveGranted);
    }

    private static IReadOnlyDictionary<string, ImmutableArray<string>> Granted(
        params (string Org, string[] Roles)[] entries)
    {
        var dict = new Dictionary<string, ImmutableArray<string>>(StringComparer.Ordinal);
        foreach (var (org, roles) in entries)
            dict[org] = [.. roles];

        return dict;
    }

    private static string Normalize(string s) =>
        s.Replace("\r\n", "\n").Trim();
}
