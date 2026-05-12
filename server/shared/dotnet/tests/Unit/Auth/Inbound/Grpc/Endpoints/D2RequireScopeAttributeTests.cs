// -----------------------------------------------------------------------
// <copyright file="D2RequireScopeAttributeTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Auth.Inbound.Grpc.Endpoints;

using AwesomeAssertions;
using D2.Shared.Auth.Grpc.Endpoints;
using Xunit;

public sealed class D2RequireScopeAttributeTests
{
    [Fact]
    public void Construct_SingleScope_ScopesContainsOne()
    {
        var attr = new D2RequireScopeAttribute("files.read");

        attr.Scopes.Should().BeEquivalentTo(new[] { "files.read" });
    }

    [Fact]
    public void Construct_MultipleScopes_ScopesContainsAll()
    {
        var attr = new D2RequireScopeAttribute("files.read", "files.admin", "files.write");

        attr.Scopes.Should().BeEquivalentTo(
            new[] { "files.read", "files.admin", "files.write" });
    }

    [Fact]
    public void Construct_NullPrimaryScope_Throws()
    {
        var act = () => new D2RequireScopeAttribute(null!);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Construct_EmptyPrimaryScope_Throws()
    {
        var act = () => new D2RequireScopeAttribute(string.Empty);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Construct_WhitespacePrimaryScope_Throws()
    {
        var act = () => new D2RequireScopeAttribute("   ");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Construct_AdditionalScopeWhitespace_Throws()
    {
        var act = () => new D2RequireScopeAttribute("files.read", "  ");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Construct_AdditionalScopeNull_Throws()
    {
        var act = () => new D2RequireScopeAttribute("files.read", null!);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Attribute_TargetsAllowMethodAndClass()
    {
        // Pin the AttributeUsage so a future regression to AllowMultiple=true
        // or Inherited=true doesn't slip through silently — the auth model
        // depends on the precedence rules these flags enforce.
        var usage = (AttributeUsageAttribute?)Attribute.GetCustomAttribute(
            typeof(D2RequireScopeAttribute), typeof(AttributeUsageAttribute));

        usage.Should().NotBeNull();
        usage.ValidOn.Should().HaveFlag(AttributeTargets.Method);
        usage.ValidOn.Should().HaveFlag(AttributeTargets.Class);
        usage.AllowMultiple.Should().BeFalse();
        usage.Inherited.Should().BeFalse();
    }

    [Fact]
    public void Attribute_AppliedToServiceClass_AffectsAllItsMethods()
    {
        // Reflection-driven proof: a class-level [D2RequireScope] is read off
        // the type itself; method-level override is a separate read.
        var classAttr = (D2RequireScopeAttribute?)Attribute.GetCustomAttribute(
            typeof(SampleProtectedService), typeof(D2RequireScopeAttribute));

        classAttr.Should().NotBeNull();
        classAttr.Scopes.Should().BeEquivalentTo(new[] { "svc.scope" });
    }

    [Fact]
    public void Attribute_MethodLevelOverridesClassLevel()
    {
        var methodInfo = typeof(SampleProtectedService).GetMethod(
            nameof(SampleProtectedService.MethodWithOwnScope))!;
        var methodAttr = (D2RequireScopeAttribute?)Attribute.GetCustomAttribute(
            methodInfo, typeof(D2RequireScopeAttribute));

        methodAttr.Should().NotBeNull();
        methodAttr.Scopes.Should().BeEquivalentTo(new[] { "method.specific" });
    }

    [D2RequireScope("svc.scope")]
    private sealed class SampleProtectedService
    {
        [D2RequireScope("method.specific")]
        public static void MethodWithOwnScope()
        {
        }
    }
}
