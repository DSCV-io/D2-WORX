// -----------------------------------------------------------------------
// <copyright file="D2RequireAllScopesAttributeTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Auth.Inbound.Grpc.Endpoints;

using AwesomeAssertions;
using DcsvIo.D2.Auth.Grpc.Endpoints;
using Xunit;

public sealed class D2RequireAllScopesAttributeTests
{
    [Fact]
    public void Construct_SingleScope_ScopesContainsOne()
    {
        var attr = new D2RequireAllScopesAttribute("files.read");

        attr.Scopes.Should().BeEquivalentTo(new[] { "files.read" });
    }

    [Fact]
    public void Construct_MultipleScopes_ScopesContainsAll()
    {
        var attr = new D2RequireAllScopesAttribute(
            "files.read", "files.write", "files.admin");

        attr.Scopes.Should().BeEquivalentTo(
            new[] { "files.read", "files.write", "files.admin" });
    }

    [Fact]
    public void Construct_NullPrimaryScope_Throws()
    {
        var act = () => new D2RequireAllScopesAttribute(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Construct_EmptyPrimaryScope_Throws()
    {
        var act = () => new D2RequireAllScopesAttribute(string.Empty);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Construct_WhitespacePrimaryScope_Throws()
    {
        var act = () => new D2RequireAllScopesAttribute("   ");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Construct_AdditionalScopeWhitespace_Throws()
    {
        var act = () => new D2RequireAllScopesAttribute("files.read", "  ");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Construct_AdditionalScopeNull_Throws()
    {
        var act = () => new D2RequireAllScopesAttribute("files.read", null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Attribute_TargetsAllowMethodAndClass()
    {
        // Pin the AttributeUsage so a future regression to AllowMultiple=true
        // or Inherited=true doesn't slip through silently — the auth model
        // depends on the precedence rules these flags enforce.
        var usage = (AttributeUsageAttribute?)Attribute.GetCustomAttribute(
            typeof(D2RequireAllScopesAttribute), typeof(AttributeUsageAttribute));

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
        typeof(D2RequireAllScopesAttribute).Name
            .Should().Be("D2RequireAllScopesAttribute");
        typeof(D2RequireAllScopesAttribute).FullName
            .Should().Be("DcsvIo.D2.Auth.Grpc.Endpoints.D2RequireAllScopesAttribute");
    }

    [Fact]
    public void Attribute_AppliedToServiceClass_AffectsAllItsMethods()
    {
        // Reflection-driven proof: a class-level [D2RequireAllScopes] is read off
        // the type itself; method-level override is a separate read.
        var classAttr = (D2RequireAllScopesAttribute?)Attribute.GetCustomAttribute(
            typeof(SampleAllProtectedService), typeof(D2RequireAllScopesAttribute));

        classAttr.Should().NotBeNull();
        classAttr.Scopes.Should().BeEquivalentTo(
            new[] { "svc.read", "svc.write" });
    }

    [Fact]
    public void Attribute_MethodLevelOverridesClassLevel()
    {
        var methodInfo = typeof(SampleAllProtectedService).GetMethod(
            nameof(SampleAllProtectedService.MethodWithOwnScope))!;
        var methodAttr = (D2RequireAllScopesAttribute?)Attribute.GetCustomAttribute(
            methodInfo, typeof(D2RequireAllScopesAttribute));

        methodAttr.Should().NotBeNull();
        methodAttr.Scopes.Should().BeEquivalentTo(
            new[] { "admin.read", "admin.write" });
    }

    [D2RequireAllScopes("svc.read", "svc.write")]
    private sealed class SampleAllProtectedService
    {
        [D2RequireAllScopes("admin.read", "admin.write")]
        public static void MethodWithOwnScope()
        {
        }
    }
}
