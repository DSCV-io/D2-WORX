// -----------------------------------------------------------------------
// <copyright file="RequireD2ScopeExtensionsTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Auth.Inbound.Http.Endpoints;

using System.Linq;
using AwesomeAssertions;
using D2.Shared.Auth.Http.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Xunit;

public sealed class RequireD2ScopeExtensionsTests
{
    [Fact]
    public void RequireD2Scope_SingleScope_AttachesMetadata()
    {
        var builder = new TestEndpointConventionBuilder();

        builder.RequireD2Scope("files.read");

        var meta = ApplyAndExtract(builder);
        meta.Should().NotBeNull();
        meta.IsHarmlessEndpoint.Should().BeFalse();
        meta.RequiredScopes.Should().BeEquivalentTo(new[] { "files.read" });
    }

    [Fact]
    public void RequireD2Scope_MultipleScopes_AttachesAllAsAnyOf()
    {
        var builder = new TestEndpointConventionBuilder();

        builder.RequireD2Scope("files.read", "files.admin");

        var meta = ApplyAndExtract(builder);
        meta!.RequiredScopes.Should().BeEquivalentTo(new[] { "files.read", "files.admin" });
    }

    [Fact]
    public void RequireD2Scope_EmptyScope_Throws()
    {
        var builder = new TestEndpointConventionBuilder();

        var act = () => builder.RequireD2Scope(string.Empty);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RequireD2Scope_NullScope_Throws()
    {
        var builder = new TestEndpointConventionBuilder();

        var act = () => builder.RequireD2Scope(null!);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RequireD2Scope_WhitespaceScope_Throws()
    {
        var builder = new TestEndpointConventionBuilder();

        var act = () => builder.RequireD2Scope("   ");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RequireD2Scope_AdditionalScopeWhitespace_Throws()
    {
        var builder = new TestEndpointConventionBuilder();

        var act = () => builder.RequireD2Scope("files.read", "   ");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RequireD2Scope_NullBuilder_Throws()
    {
        TestEndpointConventionBuilder? builder = null;

        var act = () => builder!.RequireD2Scope("files.read");

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RequireD2Scope_NullAdditionalScopesArray_Throws()
    {
        var builder = new TestEndpointConventionBuilder();

        var act = () => builder.RequireD2Scope("files.read", null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RequireD2Scope_FluentReturn_IsSameBuilder()
    {
        var builder = new TestEndpointConventionBuilder();

        var returned = builder.RequireD2Scope("files.read");

        returned.Should().BeSameAs(builder);
    }

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
