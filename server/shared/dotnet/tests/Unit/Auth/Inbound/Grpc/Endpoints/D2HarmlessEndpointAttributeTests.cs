// -----------------------------------------------------------------------
// <copyright file="D2HarmlessEndpointAttributeTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Auth.Inbound.Grpc.Endpoints;

using AwesomeAssertions;
using D2.Shared.Auth.Grpc.Endpoints;
using Xunit;

public sealed class D2HarmlessEndpointAttributeTests
{
    [Fact]
    public void Construct_DoesNotThrow()
    {
        var act = () => new D2HarmlessEndpointAttribute();

        act.Should().NotThrow();
    }

    [Fact]
    public void Attribute_TargetsAllowMethodAndClass()
    {
        var usage = (AttributeUsageAttribute?)Attribute.GetCustomAttribute(
            typeof(D2HarmlessEndpointAttribute), typeof(AttributeUsageAttribute));

        usage.Should().NotBeNull();
        usage.ValidOn.Should().HaveFlag(AttributeTargets.Method);
        usage.ValidOn.Should().HaveFlag(AttributeTargets.Class);
        usage.AllowMultiple.Should().BeFalse();
        usage.Inherited.Should().BeFalse();
    }

    [Fact]
    public void MethodLevel_MarksHarmlessOnAClassWithRequireAnyScope()
    {
        // Mirrors the BCL [AllowAnonymous]-over-[Authorize] precedence: a
        // method-level [D2HarmlessEndpoint] on a class with class-level
        // [D2RequireAnyScope] resolves to harmless-endpoint at the method.
        var method = typeof(SampleClassRequireAnyScopeServ).GetMethod(
            nameof(SampleClassRequireAnyScopeServ.HarmlessMethod))!;
        var harmless = (D2HarmlessEndpointAttribute?)Attribute.GetCustomAttribute(
            method, typeof(D2HarmlessEndpointAttribute));
        var classScope = (D2RequireAnyScopeAttribute?)Attribute.GetCustomAttribute(
            typeof(SampleClassRequireAnyScopeServ), typeof(D2RequireAnyScopeAttribute));

        harmless.Should().NotBeNull();
        classScope.Should().NotBeNull();
    }

    [Fact]
    public void MethodLevel_MarksHarmlessOnAClassWithRequireAllScopes()
    {
        // Same semantics with [D2RequireAllScopes]: a method-level
        // [D2HarmlessEndpoint] overrides a class-level all-scopes requirement.
        var method = typeof(SampleClassRequireAllScopesServ).GetMethod(
            nameof(SampleClassRequireAllScopesServ.HarmlessMethod))!;
        var harmless = (D2HarmlessEndpointAttribute?)Attribute.GetCustomAttribute(
            method, typeof(D2HarmlessEndpointAttribute));
        var classScope = (D2RequireAllScopesAttribute?)Attribute.GetCustomAttribute(
            typeof(SampleClassRequireAllScopesServ), typeof(D2RequireAllScopesAttribute));

        harmless.Should().NotBeNull();
        classScope.Should().NotBeNull();
    }

    [Fact]
    public void Attribute_TypeName_PinnedForFutureAnalyzer()
    {
        // A future Roslyn analyzer will error on [D2HarmlessEndpoint] use
        // outside an allowlist of legitimate endpoint types (probes / OIDC
        // discovery / intra-cluster health). The analyzer pins against the
        // type-name string. A silent rename without updating the analyzer
        // would break the contract — this test surfaces the breakage at
        // test-run time instead.
        typeof(D2HarmlessEndpointAttribute).Name.Should().Be("D2HarmlessEndpointAttribute");
        typeof(D2HarmlessEndpointAttribute).FullName.Should().Be(
            "D2.Shared.Auth.Grpc.Endpoints.D2HarmlessEndpointAttribute");
    }

    [D2RequireAnyScope("svc.scope")]
    private sealed class SampleClassRequireAnyScopeServ
    {
        [D2HarmlessEndpoint]
        public static void HarmlessMethod()
        {
        }
    }

    [D2RequireAllScopes("svc.read", "svc.write")]
    private sealed class SampleClassRequireAllScopesServ
    {
        [D2HarmlessEndpoint]
        public static void HarmlessMethod()
        {
        }
    }
}
