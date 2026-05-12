// -----------------------------------------------------------------------
// <copyright file="EndpointScopeMetadataTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Auth.Inbound.Http.Endpoints;

using AwesomeAssertions;
using D2.Shared.Auth.Http.Endpoints;
using Xunit;

public sealed class EndpointScopeMetadataTests
{
    [Fact]
    public void HarmlessEndpoint_IsHarmlessEndpointTrueAndRequiredScopesEmpty()
    {
        EndpointScopeMetadata.HarmlessEndpoint.IsHarmlessEndpoint.Should().BeTrue();
        EndpointScopeMetadata.HarmlessEndpoint.RequiredScopes.Should().BeEmpty();
    }

    [Fact]
    public void HarmlessEndpoint_IsSingleton()
    {
        var first = EndpointScopeMetadata.HarmlessEndpoint;
        var second = EndpointScopeMetadata.HarmlessEndpoint;

        first.Should().BeSameAs(second);
    }

    [Fact]
    public void ForScopes_PopulatesRequiredScopes()
    {
        var meta = EndpointScopeMetadata.ForScopes(new[] { "files.read", "files.admin" });

        meta.IsHarmlessEndpoint.Should().BeFalse();
        meta.RequiredScopes.Should().Contain(["files.read", "files.admin"]);
        meta.RequiredScopes.Should().HaveCount(2);
    }

    [Fact]
    public void ForScopes_DedupesDuplicates()
    {
        var meta = EndpointScopeMetadata.ForScopes(
            new[] { "files.read", "files.read", "files.admin" });

        meta.RequiredScopes.Should().HaveCount(2);
    }

    [Fact]
    public void ForScopes_OrdinalComparison_TreatsCaseDistinctly()
    {
        // Scope names are case-sensitive per the codegen catalog convention
        // (lowercase). Don't conflate variants — ordinal comparison preserves
        // both as distinct entries.
        var meta = EndpointScopeMetadata.ForScopes(new[] { "files.read", "Files.Read" });

        meta.RequiredScopes.Should().HaveCount(2);
    }

    [Fact]
    public void ForScopes_EmptyEnumerable_ThrowsArgumentException()
    {
        // Empty required-set semantic is "harmless" — but harmless-endpoint is
        // an explicit opt-in via the singleton, never via an empty-set side door.
        var act = () => EndpointScopeMetadata.ForScopes(Array.Empty<string>());

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ForScopes_NullEnumerable_ThrowsArgumentNullException()
    {
        var act = () => EndpointScopeMetadata.ForScopes(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void HarmlessEndpoint_NotEqualToScopedInstance()
    {
        var scoped = EndpointScopeMetadata.ForScopes(new[] { "files.read" });

        EndpointScopeMetadata.HarmlessEndpoint.Should().NotBe(scoped);
    }

    [Fact]
    public void HarmlessEndpoint_FactoryAndPropertyName_PinnedForFutureAnalyzer()
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
    }
}
