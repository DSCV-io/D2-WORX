// -----------------------------------------------------------------------
// <copyright file="RequestOriginGrpcServiceCollectionExtensionsTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Auth.Inbound.Grpc.Establishment;

using System.Linq;
using AwesomeAssertions;
using D2.Shared.Auth.Abstractions;
using D2.Shared.Auth.Grpc;
using D2.Shared.Auth.Grpc.Interceptors;
using global::Grpc.AspNetCore.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

/// <summary>
/// DI-resolution matrix for <c>AddD2RequestOriginGrpc()</c>: it appends the
/// establishment interceptor to the gRPC interceptor pipeline AFTER the auth
/// interceptor, registers a resolvable interceptor instance, is idempotent, and
/// validates the required self-id at resolution.
/// </summary>
[Trait("Category", "Unit")]
public sealed class RequestOriginGrpcServiceCollectionExtensionsTests
{
    [Fact]
    public void AddD2RequestOriginGrpc_NullServices_Throws()
    {
        IServiceCollection? services = null;

        var act = () => services!.AddD2RequestOriginGrpc();

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddD2RequestOriginGrpc_AppendsInterceptorAfterJwtAuth()
    {
        var services = new ServiceCollection();
        services.AddGrpc();

        // Simulate AddD2AuthGrpc()'s prior registration of the auth interceptor.
        services.Configure<GrpcServiceOptions>(o => o.Interceptors.Add<JwtAuthInterceptor>());
        services.AddD2RequestOriginGrpc(o => o.ServiceId = "key-custodian");

        using var provider = services.BuildServiceProvider();
        var interceptors = provider
            .GetRequiredService<IOptions<GrpcServiceOptions>>()
            .Value.Interceptors
            .ToList();

        var jwtIndex = interceptors.FindIndex(r => r.Type == typeof(JwtAuthInterceptor));
        var originIndex = interceptors.FindIndex(
            r => r.Type == typeof(RequestOriginCrossProcessInterceptor));

        jwtIndex.Should().BeGreaterThanOrEqualTo(0);
        originIndex.Should().BeGreaterThanOrEqualTo(0);
        originIndex.Should().BeGreaterThan(
            jwtIndex, "the establishment interceptor must run AFTER the auth interceptor");
    }

    [Fact]
    public void AddD2RequestOriginGrpc_RegistersResolvableInterceptor()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddD2RequestOriginGrpc(o => o.ServiceId = "key-custodian");

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<RequestOriginCrossProcessInterceptor>().Should().NotBeNull();
    }

    [Fact]
    public void AddD2RequestOriginGrpc_CalledTwice_DoesNotDoubleRegister()
    {
        var services = new ServiceCollection();
        services.AddGrpc();
        services.AddD2RequestOriginGrpc(o => o.ServiceId = "key-custodian");
        services.AddD2RequestOriginGrpc(o => o.ServiceId = "key-custodian");

        using var provider = services.BuildServiceProvider();
        var count = provider
            .GetRequiredService<IOptions<GrpcServiceOptions>>()
            .Value.Interceptors
            .Count(r => r.Type == typeof(RequestOriginCrossProcessInterceptor));

        count.Should().Be(1, "repeat registrations are idempotent");
    }

    [Fact]
    public void AddD2RequestOriginGrpc_BlankServiceId_FailsValidationOnResolve()
    {
        var services = new ServiceCollection();
        services.AddD2RequestOriginGrpc();

        var act = () =>
        {
            using var provider = services.BuildServiceProvider();
            _ = provider.GetRequiredService<IOptions<D2WorkloadIdentityOptions>>().Value;
        };

        act.Should().Throw<OptionsValidationException>(
            "an unset ServiceId is a misconfiguration the registration rejects");
    }
}
