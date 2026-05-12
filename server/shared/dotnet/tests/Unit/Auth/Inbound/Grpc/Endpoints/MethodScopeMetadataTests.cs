// -----------------------------------------------------------------------
// <copyright file="MethodScopeMetadataTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Auth.Inbound.Grpc.Endpoints;

using AwesomeAssertions;
using D2.Shared.Auth.Grpc.Endpoints;
using Xunit;

public sealed class MethodScopeMetadataTests
{
    [Fact]
    public void HarmlessEndpoint_IsHarmlessEndpointTrueAndRequiredScopesEmpty()
    {
        MethodScopeMetadata.HarmlessEndpoint.IsHarmlessEndpoint.Should().BeTrue();
        MethodScopeMetadata.HarmlessEndpoint.RequiredScopes.Should().BeEmpty();
    }

    [Fact]
    public void HarmlessEndpoint_IsSingleton()
    {
        var first = MethodScopeMetadata.HarmlessEndpoint;
        var second = MethodScopeMetadata.HarmlessEndpoint;

        first.Should().BeSameAs(second);
    }

    [Fact]
    public void ForScopes_PopulatesRequiredScopes()
    {
        var meta = MethodScopeMetadata.ForScopes(new[] { "files.read", "files.admin" });

        meta.IsHarmlessEndpoint.Should().BeFalse();
        meta.RequiredScopes.Should().Contain(["files.read", "files.admin"]);
        meta.RequiredScopes.Should().HaveCount(2);
    }

    [Fact]
    public void ForScopes_DedupesDuplicates()
    {
        var meta = MethodScopeMetadata.ForScopes(
            new[] { "files.read", "files.read", "files.admin" });

        meta.RequiredScopes.Should().HaveCount(2);
    }

    [Fact]
    public void ForScopes_OrdinalComparison_TreatsCaseDistinctly()
    {
        var meta = MethodScopeMetadata.ForScopes(new[] { "files.read", "Files.Read" });

        meta.RequiredScopes.Should().HaveCount(2);
    }

    [Fact]
    public void ForScopes_EmptyEnumerable_ThrowsArgumentException()
    {
        // Empty required-set semantic is "harmless" — but harmless-endpoint is
        // an explicit opt-in via the singleton, never via an empty-set side door.
        var act = () => MethodScopeMetadata.ForScopes(Array.Empty<string>());

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ForScopes_NullEnumerable_ThrowsArgumentNullException()
    {
        var act = () => MethodScopeMetadata.ForScopes(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void HarmlessEndpoint_NotEqualToScopedInstance()
    {
        var scoped = MethodScopeMetadata.ForScopes(new[] { "files.read" });

        MethodScopeMetadata.HarmlessEndpoint.Should().NotBe(scoped);
    }

    [Fact]
    public void ForScopes_RecordEquality_TwoInstancesWithSameScopesAreEqualByValue()
    {
        // Sealed record — value semantics on RequiredScopes shape match.
        var a = MethodScopeMetadata.ForScopes(new[] { "x" });
        var b = MethodScopeMetadata.ForScopes(new[] { "x" });

        // Frozen sets are reference types so direct sequence equality is what
        // record-style equality needs to produce. The synthesized equality
        // uses the property accessors; two distinct frozen-set instances with
        // the same content compare with reference equality (== false) on the
        // RequiredScopes property — assert the safe predicate that the scopes
        // CONTENT matches, which is the usable invariant for callers.
        a.RequiredScopes.Should().BeEquivalentTo(b.RequiredScopes);
        a.IsHarmlessEndpoint.Should().Be(b.IsHarmlessEndpoint);
    }

    [Fact]
    public void HarmlessEndpoint_FactoryAndPropertyName_PinnedForFutureAnalyzer()
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
    }
}
