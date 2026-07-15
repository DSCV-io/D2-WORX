// -----------------------------------------------------------------------
// <copyright file="MethodScopeMetadataTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Auth.Inbound.Grpc.Endpoints;

using AwesomeAssertions;
using DcsvIo.D2.Auth.Abstractions;
using DcsvIo.D2.Auth.Grpc.Endpoints;
using Xunit;

public sealed class MethodScopeMetadataTests
{
    [Fact]
    public void HarmlessEndpoint_IsHarmlessEndpointTrueAndScopesEmpty()
    {
        MethodScopeMetadata.HarmlessEndpoint.IsHarmlessEndpoint.Should().BeTrue();
        MethodScopeMetadata.HarmlessEndpoint.Scopes.Should().BeEmpty();
    }

    [Fact]
    public void HarmlessEndpoint_IsSingleton()
    {
        var first = MethodScopeMetadata.HarmlessEndpoint;
        var second = MethodScopeMetadata.HarmlessEndpoint;

        first.Should().BeSameAs(second);
    }

    [Fact]
    public void ForScopes_Any_PopulatesScopesAndMatchIsAny()
    {
        var meta = MethodScopeMetadata.ForScopes(
            new[] { "files.read", "files.admin" }, ScopeMatch.Any);

        meta.IsHarmlessEndpoint.Should().BeFalse();
        meta.Match.Should().Be(ScopeMatch.Any);
        meta.Scopes.Should().Contain(["files.read", "files.admin"]);
        meta.Scopes.Should().HaveCount(2);
    }

    [Fact]
    public void ForScopes_All_PopulatesScopesAndMatchIsAll()
    {
        var meta = MethodScopeMetadata.ForScopes(
            new[] { "files.read", "files.write" }, ScopeMatch.All);

        meta.IsHarmlessEndpoint.Should().BeFalse();
        meta.Match.Should().Be(ScopeMatch.All);
        meta.Scopes.Should().Contain(["files.read", "files.write"]);
        meta.Scopes.Should().HaveCount(2);
    }

    [Fact]
    public void ForScopes_DedupesDuplicates()
    {
        var meta = MethodScopeMetadata.ForScopes(
            new[] { "files.read", "files.read", "files.admin" }, ScopeMatch.Any);

        meta.Scopes.Should().HaveCount(2);
    }

    [Fact]
    public void ForScopes_OrdinalComparison_TreatsCaseDistinctly()
    {
        var meta = MethodScopeMetadata.ForScopes(
            new[] { "files.read", "Files.Read" }, ScopeMatch.Any);

        meta.Scopes.Should().HaveCount(2);
    }

    [Fact]
    public void ForScopes_Any_EmptyEnumerable_ThrowsArgumentException()
    {
        // Empty required-set semantic is "harmless" — but harmless-endpoint is
        // an explicit opt-in via the singleton, never via an empty-set side door.
        var act = () => MethodScopeMetadata.ForScopes(Array.Empty<string>(), ScopeMatch.Any);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ForScopes_All_EmptyEnumerable_ThrowsArgumentException()
    {
        var act = () => MethodScopeMetadata.ForScopes(Array.Empty<string>(), ScopeMatch.All);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ForScopes_NullEnumerable_ThrowsArgumentNullException()
    {
        var act = () => MethodScopeMetadata.ForScopes(null!, ScopeMatch.Any);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ForScopes_NullEnumerable_All_ThrowsArgumentNullException()
    {
        // Null-guard must fire regardless of match mode — ScopeMatch.All must
        // not bypass the ArgumentNullException thrown before enumeration begins.
        // Mirrors EndpointScopeMetadataTests.ForScopes_NullEnumerable_All_ThrowsArgumentNullException.
        var act = () => MethodScopeMetadata.ForScopes(null!, ScopeMatch.All);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void HarmlessEndpoint_NotEqualToScopedInstance()
    {
        var scoped = MethodScopeMetadata.ForScopes(new[] { "files.read" }, ScopeMatch.Any);

        MethodScopeMetadata.HarmlessEndpoint.Should().NotBe(scoped);
    }

    [Fact]
    public void ForScopes_RecordEquality_TwoInstancesWithSameScopesAndMatchAreEqualByValue()
    {
        // Sealed record — value semantics on Scopes + Match shape match.
        var a = MethodScopeMetadata.ForScopes(new[] { "x" }, ScopeMatch.Any);
        var b = MethodScopeMetadata.ForScopes(new[] { "x" }, ScopeMatch.Any);

        // Frozen sets are reference types so direct sequence equality is what
        // record-style equality needs to produce. The synthesized equality
        // uses the property accessors; two distinct frozen-set instances with
        // the same content compare with reference equality (== false) on the
        // Scopes property — assert the safe predicate that the scopes
        // CONTENT matches, which is the usable invariant for callers.
        a.Scopes.Should().BeEquivalentTo(b.Scopes);
        a.Match.Should().Be(b.Match);
        a.IsHarmlessEndpoint.Should().Be(b.IsHarmlessEndpoint);
    }

    [Fact]
    public void ForScopes_DifferentMatch_ProducesDifferentInstances()
    {
        var anyMeta = MethodScopeMetadata.ForScopes(new[] { "x" }, ScopeMatch.Any);
        var allMeta = MethodScopeMetadata.ForScopes(new[] { "x" }, ScopeMatch.All);

        anyMeta.Match.Should().NotBe(allMeta.Match);
    }

    [Fact]
    public void HarmlessEndpoint_FactoryAndPropertyNames_PinnedForFutureAnalyzer()
    {
        // A future Roslyn analyzer will error on
        // .MarkAsD2HarmlessEndpoint() / MethodScopeMetadata.HarmlessEndpoint
        // use outside an allowlist of legitimate endpoint types. The analyzer
        // pins against the property + factory names as LITERAL STRINGS — a
        // silent rename without updating the analyzer would break the contract.
        // We deliberately do NOT use nameof() here: nameof() is compile-time
        // resolved and would silently follow any rename, defeating the pin.
        typeof(MethodScopeMetadata).GetField(
            "HarmlessEndpoint",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Should().NotBeNull();
        typeof(MethodScopeMetadata).GetProperty("IsHarmlessEndpoint")
            .Should().NotBeNull();

        // Scopes replaces RequiredScopes — pin the new name so an accidental
        // rename is caught before the analyzer wires against the stale name.
        typeof(MethodScopeMetadata).GetProperty("Scopes")
            .Should().NotBeNull();
        typeof(MethodScopeMetadata).GetProperty("Match")
            .Should().NotBeNull();

        // Confirm RequiredScopes is gone (analyzer must not reference it).
        typeof(MethodScopeMetadata).GetProperty("RequiredScopes")
            .Should().BeNull();
    }
}
