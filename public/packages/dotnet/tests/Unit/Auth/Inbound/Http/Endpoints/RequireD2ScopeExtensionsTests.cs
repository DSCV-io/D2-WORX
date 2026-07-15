// -----------------------------------------------------------------------
// <copyright file="RequireD2ScopeExtensionsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Auth.Inbound.Http.Endpoints;

using System.Linq;
using AwesomeAssertions;
using DcsvIo.D2.Auth.Abstractions;
using DcsvIo.D2.Auth.Http.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Xunit;

public sealed class RequireD2ScopeExtensionsTests
{
    // ── RequireAnyScope ────────────────────────────────────────────────────

    [Fact]
    public void RequireAnyScope_SingleScope_AttachesMetadataWithMatchAny()
    {
        var builder = new TestEndpointConventionBuilder();

        builder.RequireAnyScope("files.read");

        var meta = ApplyAndExtract(builder);
        meta.Should().NotBeNull();
        meta.IsHarmlessEndpoint.Should().BeFalse();
        meta.Match.Should().Be(ScopeMatch.Any);
        meta.Scopes.Should().BeEquivalentTo(new[] { "files.read" });
    }

    [Fact]
    public void RequireAnyScope_MultipleScopes_AttachesAllWithMatchAny()
    {
        var builder = new TestEndpointConventionBuilder();

        builder.RequireAnyScope("files.read", "files.admin");

        var meta = ApplyAndExtract(builder);
        meta!.Match.Should().Be(ScopeMatch.Any);
        meta.Scopes.Should().BeEquivalentTo(new[] { "files.read", "files.admin" });
    }

    [Fact]
    public void RequireAnyScope_EmptyScope_Throws()
    {
        var builder = new TestEndpointConventionBuilder();

        var act = () => builder.RequireAnyScope(string.Empty);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RequireAnyScope_NullScope_Throws()
    {
        var builder = new TestEndpointConventionBuilder();

        var act = () => builder.RequireAnyScope(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RequireAnyScope_WhitespaceScope_Throws()
    {
        var builder = new TestEndpointConventionBuilder();

        var act = () => builder.RequireAnyScope("   ");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RequireAnyScope_AdditionalScopeWhitespace_Throws()
    {
        var builder = new TestEndpointConventionBuilder();

        var act = () => builder.RequireAnyScope("files.read", "   ");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RequireAnyScope_NullBuilder_Throws()
    {
        TestEndpointConventionBuilder? builder = null;

        var act = () => builder!.RequireAnyScope("files.read");

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RequireAnyScope_NullAdditionalScopesArray_Throws()
    {
        var builder = new TestEndpointConventionBuilder();

        var act = () => builder.RequireAnyScope("files.read", null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RequireAnyScope_FluentReturn_IsSameBuilder()
    {
        var builder = new TestEndpointConventionBuilder();

        var returned = builder.RequireAnyScope("files.read");

        returned.Should().BeSameAs(builder);
    }

    // ── RequireAllScopes ───────────────────────────────────────────────────

    [Fact]
    public void RequireAllScopes_SingleScope_AttachesMetadataWithMatchAll()
    {
        var builder = new TestEndpointConventionBuilder();

        builder.RequireAllScopes("files.write");

        var meta = ApplyAndExtract(builder);
        meta.Should().NotBeNull();
        meta.IsHarmlessEndpoint.Should().BeFalse();
        meta.Match.Should().Be(ScopeMatch.All);
        meta.Scopes.Should().BeEquivalentTo(new[] { "files.write" });
    }

    [Fact]
    public void RequireAllScopes_MultipleScopes_AttachesAllWithMatchAll()
    {
        var builder = new TestEndpointConventionBuilder();

        builder.RequireAllScopes("files.read", "files.write");

        var meta = ApplyAndExtract(builder);
        meta!.Match.Should().Be(ScopeMatch.All);
        meta.Scopes.Should().BeEquivalentTo(new[] { "files.read", "files.write" });
    }

    [Fact]
    public void RequireAllScopes_EmptyScope_Throws()
    {
        var builder = new TestEndpointConventionBuilder();

        var act = () => builder.RequireAllScopes(string.Empty);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RequireAllScopes_NullScope_Throws()
    {
        var builder = new TestEndpointConventionBuilder();

        var act = () => builder.RequireAllScopes(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RequireAllScopes_WhitespaceScope_Throws()
    {
        var builder = new TestEndpointConventionBuilder();

        var act = () => builder.RequireAllScopes("   ");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RequireAllScopes_AdditionalScopeWhitespace_Throws()
    {
        var builder = new TestEndpointConventionBuilder();

        var act = () => builder.RequireAllScopes("files.read", "   ");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RequireAllScopes_NullBuilder_Throws()
    {
        TestEndpointConventionBuilder? builder = null;

        var act = () => builder!.RequireAllScopes("files.read");

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RequireAllScopes_NullAdditionalScopesArray_Throws()
    {
        var builder = new TestEndpointConventionBuilder();

        var act = () => builder.RequireAllScopes("files.read", null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RequireAllScopes_FluentReturn_IsSameBuilder()
    {
        var builder = new TestEndpointConventionBuilder();

        var returned = builder.RequireAllScopes("files.read");

        returned.Should().BeSameAs(builder);
    }

    // ── MarkAsD2HarmlessEndpoint ───────────────────────────────────────────

    [Fact]
    public void MarkAsD2HarmlessEndpoint_AttachesHarmlessEndpointSingleton()
    {
        var builder = new TestEndpointConventionBuilder();

        builder.MarkAsD2HarmlessEndpoint();

        var meta = ApplyAndExtract(builder);
        meta.Should().BeSameAs(EndpointScopeMetadata.HarmlessEndpoint);
    }

    [Fact]
    public void MarkAsD2HarmlessEndpoint_NullBuilder_Throws()
    {
        TestEndpointConventionBuilder? builder = null;

        var act = () => builder!.MarkAsD2HarmlessEndpoint();

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void MarkAsD2HarmlessEndpoint_FluentReturn_IsSameBuilder()
    {
        var builder = new TestEndpointConventionBuilder();

        var returned = builder.MarkAsD2HarmlessEndpoint();

        returned.Should().BeSameAs(builder);
    }

    private static EndpointScopeMetadata? ApplyAndExtract(TestEndpointConventionBuilder builder)
    {
        var endpointBuilder = new RouteEndpointBuilder(
            requestDelegate: _ => Task.CompletedTask,
            routePattern: Microsoft.AspNetCore.Routing.Patterns.RoutePatternFactory.Parse("/x"),
            order: 0);
        foreach (var convention in builder.Conventions)
            convention(endpointBuilder);
        return endpointBuilder.Metadata.OfType<EndpointScopeMetadata>().FirstOrDefault();
    }

    /// <summary>
    /// Minimal stand-in for an <see cref="IEndpointConventionBuilder"/>:
    /// captures every applied convention so the test can replay them against
    /// a fresh <see cref="EndpointBuilder"/> for assertion.
    /// </summary>
    private sealed class TestEndpointConventionBuilder : IEndpointConventionBuilder
    {
        public List<Action<EndpointBuilder>> Conventions { get; } = [];

        public void Add(Action<EndpointBuilder> convention)
            => Conventions.Add(convention);
    }
}
