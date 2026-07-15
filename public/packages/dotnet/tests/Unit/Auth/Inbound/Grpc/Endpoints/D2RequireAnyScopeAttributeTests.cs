// -----------------------------------------------------------------------
// <copyright file="D2RequireAnyScopeAttributeTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Auth.Inbound.Grpc.Endpoints;

using AwesomeAssertions;
using DcsvIo.D2.Auth.Grpc.Endpoints;
using Xunit;

public sealed class D2RequireAnyScopeAttributeTests
{
    [Fact]
    public void Construct_SingleScope_ScopesContainsOne()
    {
        var attr = new D2RequireAnyScopeAttribute("files.read");

        attr.Scopes.Should().BeEquivalentTo(new[] { "files.read" });
    }

    [Fact]
    public void Construct_MultipleScopes_ScopesContainsAll()
    {
        var attr = new D2RequireAnyScopeAttribute(
            "files.read", "files.admin", "files.write");

        attr.Scopes.Should().BeEquivalentTo(
            new[] { "files.read", "files.admin", "files.write" });
    }

    [Fact]
    public void Construct_NullPrimaryScope_Throws()
    {
        var act = () => new D2RequireAnyScopeAttribute(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Construct_EmptyPrimaryScope_Throws()
    {
        var act = () => new D2RequireAnyScopeAttribute(string.Empty);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Construct_WhitespacePrimaryScope_Throws()
    {
        var act = () => new D2RequireAnyScopeAttribute("   ");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Construct_AdditionalScopeWhitespace_Throws()
    {
        var act = () => new D2RequireAnyScopeAttribute("files.read", "  ");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Construct_AdditionalScopeNull_Throws()
    {
        var act = () => new D2RequireAnyScopeAttribute("files.read", null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Attribute_TargetsAllowMethodAndClass()
    {
        // Pin the AttributeUsage so a future regression to AllowMultiple=true
        // or Inherited=true doesn't slip through silently — the auth model
        // depends on the precedence rules these flags enforce.
        var usage = (AttributeUsageAttribute?)Attribute.GetCustomAttribute(
            typeof(D2RequireAnyScopeAttribute), typeof(AttributeUsageAttribute));

        usage.Should().NotBeNull();
        usage.ValidOn.Should().HaveFlag(AttributeTargets.Method);
        usage.ValidOn.Should().HaveFlag(AttributeTargets.Class);
        usage.AllowMultiple.Should().BeFalse();
        usage.Inherited.Should().BeFalse();
    }

    [Fact]
    public void Attribute_TypeName_PinnedForFutureAnalyzer()
    {
        // A future Roslyn analyzer pins against the type-name string. A silent
        // rename without updating the analyzer would break the contract — this
        // test surfaces the breakage at test-run time instead.
        typeof(D2RequireAnyScopeAttribute).Name
            .Should().Be("D2RequireAnyScopeAttribute");
        typeof(D2RequireAnyScopeAttribute).FullName
            .Should().Be("DcsvIo.D2.Auth.Grpc.Endpoints.D2RequireAnyScopeAttribute");
    }

    [Fact]
    public void Attribute_AppliedToServiceClass_AffectsAllItsMethods()
    {
        // Reflection-driven proof: a class-level [D2RequireAnyScope] is read off
        // the type itself; method-level override is a separate read.
        var classAttr = (D2RequireAnyScopeAttribute?)Attribute.GetCustomAttribute(
            typeof(SampleAnyProtectedService), typeof(D2RequireAnyScopeAttribute));

        classAttr.Should().NotBeNull();
        classAttr.Scopes.Should().BeEquivalentTo(new[] { "svc.scope" });
    }

    [Fact]
    public void Attribute_MethodLevelOverridesClassLevel()
    {
        var methodInfo = typeof(SampleAnyProtectedService).GetMethod(
            nameof(SampleAnyProtectedService.MethodWithOwnScope))!;
        var methodAttr = (D2RequireAnyScopeAttribute?)Attribute.GetCustomAttribute(
            methodInfo, typeof(D2RequireAnyScopeAttribute));

        methodAttr.Should().NotBeNull();
        methodAttr.Scopes.Should().BeEquivalentTo(new[] { "method.specific" });
    }

    [D2RequireAnyScope("svc.scope")]
    private sealed class SampleAnyProtectedService
    {
        [D2RequireAnyScope("method.specific")]
        public static void MethodWithOwnScope()
        {
        }
    }
}
