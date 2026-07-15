// -----------------------------------------------------------------------
// <copyright file="WellKnownAudiencesTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Auth;

using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using AwesomeAssertions;
using D2.Shared.Auth.Abstractions;
using Xunit;

/// <summary>
/// Unit tests for the hand-declared <see cref="WellKnownAudiences"/> protocol
/// constants — the universal internal <i>receive</i> audience the forward-unchanged
/// model pivots on. Pins the wire value, the reachable-constant shape, and (the
/// load-bearing guard) that the value is hand-declared rather than spec-derived:
/// it must NOT appear in the codegen-emitted <see cref="Audiences"/> surface, which
/// is the codegen boundary the design rests on.
/// </summary>
[Trait("Category", "Unit")]
public sealed class WellKnownAudiencesTests
{
    [Fact]
    public void D2InternalAudience_PinsTheWireValue()
    {
        // Pin the receive-audience wire value. Changing it is a breaking change to
        // every hop's `aud == d2.internal` check AND the future Edge minter.
        WellKnownAudiences.D2_INTERNAL_AUDIENCE.Should().Be("d2.internal");
    }

    [Fact]
    public void WellKnownAudiences_IsAStaticClass()
    {
        var type = typeof(WellKnownAudiences);

        type.IsAbstract.Should().BeTrue("static classes are abstract+sealed at IL");
        type.IsSealed.Should().BeTrue();
    }

    [Fact]
    public void D2InternalAudience_IsAPublicCompileTimeConstant()
    {
        // Reachable from the abstractions package as a plain compile-time constant
        // (public + static + literal), not a runtime-computed value.
        var field = typeof(WellKnownAudiences).GetField(
            nameof(WellKnownAudiences.D2_INTERNAL_AUDIENCE),
            BindingFlags.Public | BindingFlags.Static);

        field.Should().NotBeNull();
        field.IsLiteral.Should().BeTrue("a const is a compile-time literal");
        field.IsInitOnly.Should().BeFalse("a const is not a runtime readonly field");
        field.FieldType.Should().Be<string>();
    }

    // ----------------------------------------------------------------------
    // Codegen boundary: the value is hand-declared, NOT spec-derived. It must
    // be absent from every projection of the generated Audiences catalog.
    // ----------------------------------------------------------------------

    [Fact]
    public void D2InternalAudience_IsNotAKnownSpecAudience()
    {
        // The spec-generated known-set (the inbound aud-validation surface) does
        // NOT contain the internal receive audience — proving it is hand-declared
        // and not an audiences.spec.json entry.
        Audiences.IsKnown(WellKnownAudiences.D2_INTERNAL_AUDIENCE).Should().BeFalse();
    }

    [Fact]
    public void D2InternalAudience_IsAbsentFromEverySpecProjection()
    {
        Audiences.AllUrls.Should().NotContain(WellKnownAudiences.D2_INTERNAL_AUDIENCE);
        Audiences.ByName.Values.Should().NotContain(WellKnownAudiences.D2_INTERNAL_AUDIENCE);
    }

    [Fact]
    public void D2InternalAudience_DoesNotResolveThroughTheSpecHelpers()
    {
        // Neither name→url nor url→name resolution surfaces it — there is no spec
        // entry it could be mirroring.
        Audiences.Resolve(WellKnownAudiences.D2_INTERNAL_AUDIENCE).Should().BeNull();
        Audiences.ResolveByUrl(WellKnownAudiences.D2_INTERNAL_AUDIENCE).Should().BeNull();
    }

    // ----------------------------------------------------------------------
    // Adversarial: exactly one hand-declared constant carries this value, and
    // it collides with no generated spec URL.
    // ----------------------------------------------------------------------

    [Fact]
    public void OnlyOneHandDeclaredConstant_CarriesTheInternalAudienceValue()
    {
        // Reflection walk over every public-const-string on WellKnownAudiences:
        // exactly one must equal "d2.internal" (no accidental duplicate copy).
        var matches = typeof(WellKnownAudiences)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string?)f.GetRawConstantValue())
            .Count(value => value == "d2.internal");

        matches.Should().Be(1, "exactly one hand-declared constant holds the internal receive audience");
    }

    [Fact]
    public void D2InternalAudience_DoesNotCollideWithAnyGeneratedAudienceUrl()
    {
        // The hand-declared receive audience must be distinct from every
        // spec-derived token-exchange target URL — different role, different value.
        Audiences.AllUrls.Should().NotContain(WellKnownAudiences.D2_INTERNAL_AUDIENCE);
    }

    [Fact]
    public void D2InternalAudience_IsABareTokenNotAUrl()
    {
        // Format sanity: the receive audience is a bare token, intentionally NOT
        // URL-shaped like the spec entries (https://files.internal etc.). No scheme
        // separator, no whitespace, non-empty.
        var value = WellKnownAudiences.D2_INTERNAL_AUDIENCE;

        value.Should().NotBeNullOrWhiteSpace();
        value.Should().NotContain(":");
        value.Should().NotContainAny(" ", "\t", "\n", "\r");
    }

    // ----------------------------------------------------------------------
    // The Edge self-audience constant — now spec-driven alongside d2.internal,
    // replacing the magic-string literal the @d2Audience validator used to carry.
    // ----------------------------------------------------------------------

    [Fact]
    public void D2EdgeSelfAudience_PinsTheWireValue()
    {
        WellKnownAudiences.D2_EDGE_SELF_AUDIENCE.Should().Be("d2-edge");
    }

    [Fact]
    public void D2EdgeSelfAudience_IsAPublicCompileTimeConstant()
    {
        var field = typeof(WellKnownAudiences).GetField(
            nameof(WellKnownAudiences.D2_EDGE_SELF_AUDIENCE),
            BindingFlags.Public | BindingFlags.Static);

        field.Should().NotBeNull();
        field.IsLiteral.Should().BeTrue("a const is a compile-time literal");
        field.FieldType.Should().Be<string>();
    }

    // ----------------------------------------------------------------------
    // Single-source parity: the generated C# constants are byte-identical to the
    // protocol-audiences spec (the source of truth) AND to the ts-codegen-emitted
    // TypeScript ProtocolAudiences const. A drift on either runtime is impossible
    // because both derive from the same spec — these tests pin that contract.
    // ----------------------------------------------------------------------

    [Fact]
    public void GeneratedConstants_MatchTheProtocolAudiencesSpecValues()
    {
        var spec = ReadProtocolAudiencesSpec();

        // Each spec entry's name → a C# const of that name carrying the spec value.
        foreach (var (name, value) in spec)
        {
            var field = typeof(WellKnownAudiences).GetField(
                name,
                BindingFlags.Public | BindingFlags.Static);

            field.Should().NotBeNull(
                $"every protocol-audience spec entry '{name}' must emit a WellKnownAudiences constant");
            ((string?)field.GetRawConstantValue()).Should().Be(
                value,
                $"WellKnownAudiences.{name} must carry the spec value byte-for-byte");
        }
    }

    [Fact]
    public void GeneratedConstants_ByteMatchTheTypeScriptProtocolAudiences()
    {
        // The TS const-object is emitted from the same spec into
        // @d2/auth-abstractions. Assert each C# const value appears in the TS file
        // under the same SCREAMING_SNAKE name — cross-runtime byte identity.
        var spec = ReadProtocolAudiencesSpec();
        var tsPath = Path.Combine(
            RepoRoot(),
            "public",
            "packages",
            "typescript",
            "auth",
            "abstractions",
            "src",
            "protocol-audiences.g.ts");

        File.Exists(tsPath).Should().BeTrue(
            "the ts-codegen ProtocolAudiences artifact must be committed at " + tsPath);
        var tsSource = File.ReadAllText(tsPath);

        foreach (var (name, value) in spec)
            tsSource.Should().Contain(
                $"{name}: \"{value}\"",
                $"the TS ProtocolAudiences must carry {name} = {value} byte-identically to C#");
    }

    private static (string Name, string Value)[] ReadProtocolAudiencesSpec()
    {
        var specPath = Path.Combine(
            RepoRoot(),
            "public",
            "contracts",
            "auth-protocol-audiences",
            "protocol-audiences.spec.json");

        using var doc = JsonDocument.Parse(File.ReadAllText(specPath));
        var entries = doc.RootElement.GetProperty("protocolAudiences");
        var result = new (string, string)[entries.GetArrayLength()];
        var i = 0;
        foreach (var entry in entries.EnumerateArray())
        {
            result[i] = (
                entry.GetProperty("name").GetString()!,
                entry.GetProperty("value").GetString()!);
            i++;
        }

        return result;
    }

    private static string RepoRoot()
    {
        // Walk up from the test assembly location to the repo root (the dir holding
        // the contracts/ folder). Mirrors the spec-registry anchor convention.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null &&
            !Directory.Exists(Path.Combine(dir.FullName, "public", "contracts")))
        {
            dir = dir.Parent;
        }

        dir.Should().NotBeNull("the repo root (with a contracts/ dir) must be locatable");
        return dir.FullName;
    }
}
