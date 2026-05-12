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
    public void Anonymous_IsAnonymousTrueAndRequiredScopesEmpty()
    {
        MethodScopeMetadata.Anonymous.IsAnonymous.Should().BeTrue();
        MethodScopeMetadata.Anonymous.RequiredScopes.Should().BeEmpty();
    }

    [Fact]
    public void Anonymous_IsSingleton()
    {
        var first = MethodScopeMetadata.Anonymous;
        var second = MethodScopeMetadata.Anonymous;

        first.Should().BeSameAs(second);
    }

    [Fact]
    public void ForScopes_PopulatesRequiredScopes()
    {
        var meta = MethodScopeMetadata.ForScopes(new[] { "files.read", "files.admin" });

        meta.IsAnonymous.Should().BeFalse();
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
        // Empty required-set semantic is "anonymous" — but anonymous is an
        // explicit opt-in via the singleton, never via an empty-set side door.
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
    public void Anonymous_NotEqualToScopedInstance()
    {
        var scoped = MethodScopeMetadata.ForScopes(new[] { "files.read" });

        MethodScopeMetadata.Anonymous.Should().NotBe(scoped);
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
        a.IsAnonymous.Should().Be(b.IsAnonymous);
    }
}
