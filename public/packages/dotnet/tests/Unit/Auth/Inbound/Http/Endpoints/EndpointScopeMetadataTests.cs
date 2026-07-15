// -----------------------------------------------------------------------
// <copyright file="EndpointScopeMetadataTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Auth.Inbound.Http.Endpoints;

using AwesomeAssertions;
using DcsvIo.D2.Auth.Abstractions;
using DcsvIo.D2.Auth.Http.Endpoints;
using Xunit;

public sealed class EndpointScopeMetadataTests
{
    [Fact]
    public void HarmlessEndpoint_IsHarmlessEndpointTrueAndScopesEmpty()
    {
        EndpointScopeMetadata.HarmlessEndpoint.IsHarmlessEndpoint.Should().BeTrue();
        EndpointScopeMetadata.HarmlessEndpoint.Scopes.Should().BeEmpty();
    }

    [Fact]
    public void HarmlessEndpoint_IsSingleton()
    {
        var first = EndpointScopeMetadata.HarmlessEndpoint;
        var second = EndpointScopeMetadata.HarmlessEndpoint;

        first.Should().BeSameAs(second);
    }

    [Fact]
    public void ForScopes_Any_PopulatesScopesAndMatch()
    {
        var meta = EndpointScopeMetadata.ForScopes(
            new[] { "files.read", "files.admin" }, ScopeMatch.Any);

        meta.IsHarmlessEndpoint.Should().BeFalse();
        meta.Match.Should().Be(ScopeMatch.Any);
        meta.Scopes.Should().Contain(["files.read", "files.admin"]);
        meta.Scopes.Should().HaveCount(2);
    }

    [Fact]
    public void ForScopes_All_PopulatesScopesAndMatch()
    {
        var meta = EndpointScopeMetadata.ForScopes(
            new[] { "files.read", "files.write" }, ScopeMatch.All);

        meta.IsHarmlessEndpoint.Should().BeFalse();
        meta.Match.Should().Be(ScopeMatch.All);
        meta.Scopes.Should().Contain(["files.read", "files.write"]);
        meta.Scopes.Should().HaveCount(2);
    }

    [Fact]
    public void ForScopes_DedupesDuplicates()
    {
        var meta = EndpointScopeMetadata.ForScopes(
            new[] { "files.read", "files.read", "files.admin" }, ScopeMatch.Any);

        meta.Scopes.Should().HaveCount(2);
    }

    [Fact]
    public void ForScopes_OrdinalComparison_TreatsCaseDistinctly()
    {
        // Scope names are case-sensitive per the codegen catalog convention
        // (lowercase). Don't conflate variants — ordinal comparison preserves
        // both as distinct entries.
        var meta = EndpointScopeMetadata.ForScopes(
            new[] { "files.read", "Files.Read" }, ScopeMatch.Any);

        meta.Scopes.Should().HaveCount(2);
    }

    [Fact]
    public void ForScopes_EmptyEnumerable_ThrowsArgumentException()
    {
        // Empty required-set semantic is "harmless" — but harmless-endpoint is
        // an explicit opt-in via the singleton, never via an empty-set side door.
        var act = () => EndpointScopeMetadata.ForScopes(Array.Empty<string>(), ScopeMatch.Any);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ForScopes_EmptyEnumerable_All_ThrowsArgumentException()
    {
        var act = () => EndpointScopeMetadata.ForScopes(Array.Empty<string>(), ScopeMatch.All);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ForScopes_NullEnumerable_ThrowsArgumentNullException()
    {
        var act = () => EndpointScopeMetadata.ForScopes(null!, ScopeMatch.Any);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ForScopes_NullEnumerable_All_ThrowsArgumentNullException()
    {
        // Null-guard must apply regardless of the match mode — mode selection
        // never bypasses the ArgumentNullException thrown before enumeration.
        var act = () => EndpointScopeMetadata.ForScopes(null!, ScopeMatch.All);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void HarmlessEndpoint_NotEqualToScopedInstance()
    {
        var scoped = EndpointScopeMetadata.ForScopes(new[] { "files.read" }, ScopeMatch.Any);

        EndpointScopeMetadata.HarmlessEndpoint.Should().NotBe(scoped);
    }

    [Fact]
    public void HarmlessEndpoint_FactoryAndPropertyNames_PinnedForFutureAnalyzer()
    {
        // A future Roslyn analyzer will error on
        // .MarkAsD2HarmlessEndpoint() / EndpointScopeMetadata.HarmlessEndpoint
        // use outside an allowlist of legitimate endpoint types. The analyzer
        // pins against the property + factory names as LITERAL STRINGS — a
        // silent rename without updating the analyzer would break the contract.
        // We deliberately do NOT use nameof() here: nameof() is compile-time
        // resolved and would silently follow any rename, defeating the pin.
        typeof(EndpointScopeMetadata).GetField(
            "HarmlessEndpoint",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Should().NotBeNull();
        typeof(EndpointScopeMetadata).GetProperty("IsHarmlessEndpoint")
            .Should().NotBeNull();

        // Pin the renamed property ("Scopes", not "RequiredScopes") and the
        // new property ("Match") for the same analyzer-contract reason.
        typeof(EndpointScopeMetadata).GetProperty("Scopes")
            .Should().NotBeNull();
        typeof(EndpointScopeMetadata).GetProperty("Match")
            .Should().NotBeNull();
    }
}
