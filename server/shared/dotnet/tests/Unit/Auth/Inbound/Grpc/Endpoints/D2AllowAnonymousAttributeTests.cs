// -----------------------------------------------------------------------
// <copyright file="D2AllowAnonymousAttributeTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Auth.Inbound.Grpc.Endpoints;

using AwesomeAssertions;
using D2.Shared.Auth.Grpc.Endpoints;
using Xunit;

public sealed class D2AllowAnonymousAttributeTests
{
    [Fact]
    public void Construct_DoesNotThrow()
    {
        var act = () => new D2AllowAnonymousAttribute();

        act.Should().NotThrow();
    }

    [Fact]
    public void Attribute_TargetsAllowMethodAndClass()
    {
        var usage = (AttributeUsageAttribute?)Attribute.GetCustomAttribute(
            typeof(D2AllowAnonymousAttribute), typeof(AttributeUsageAttribute));

        usage.Should().NotBeNull();
        usage.ValidOn.Should().HaveFlag(AttributeTargets.Method);
        usage.ValidOn.Should().HaveFlag(AttributeTargets.Class);
        usage.AllowMultiple.Should().BeFalse();
        usage.Inherited.Should().BeFalse();
    }

    [Fact]
    public void MethodLevel_AllowsAnonymousOnAClassWithRequireScope()
    {
        // Mirrors the BCL [AllowAnonymous]-over-[Authorize] precedence: a
        // method-level [D2AllowAnonymous] on a class with class-level
        // [D2RequireScope] resolves to anonymous at the method.
        var method = typeof(SampleClassRequireScopeServ).GetMethod(
            nameof(SampleClassRequireScopeServ.AnonMethod))!;
        var anon = (D2AllowAnonymousAttribute?)Attribute.GetCustomAttribute(
            method, typeof(D2AllowAnonymousAttribute));
        var classScope = (D2RequireScopeAttribute?)Attribute.GetCustomAttribute(
            typeof(SampleClassRequireScopeServ), typeof(D2RequireScopeAttribute));

        anon.Should().NotBeNull();
        classScope.Should().NotBeNull();
    }

    [D2RequireScope("svc.scope")]
    private sealed class SampleClassRequireScopeServ
    {
        [D2AllowAnonymous]
        public static void AnonMethod()
        {
        }
    }
}
