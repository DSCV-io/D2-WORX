// -----------------------------------------------------------------------
// <copyright file="PropagatedContextOutboundExtensionsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Auth.Outbound;

using System;
using System.Linq;
using AwesomeAssertions;
using DcsvIo.D2.Auth.Abstractions;
using DcsvIo.D2.Auth.Outbound.Grpc;
using DcsvIo.D2.Tests.Unit.Auth.Inbound.Grpc.Protos;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

/// <summary>
/// DI-resolution matrix for the outbound propagated-context extensions:
/// <c>AddD2PropagatedContextOutbound()</c> registers a resolvable interceptor singleton
/// (idempotently), and <c>AddD2PropagatedContext()</c> attaches it to a gRPC client
/// builder.
/// </summary>
[Trait("Category", "Unit")]
public sealed class PropagatedContextOutboundExtensionsTests
{
    [Fact]
    public void AddD2PropagatedContextOutbound_NullServices_Throws()
    {
        IServiceCollection? services = null;

        var act = () => services!.AddD2PropagatedContextOutbound();

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddD2PropagatedContext_NullBuilder_Throws()
    {
        IHttpClientBuilder? builder = null;

        var act = () => builder!.AddD2PropagatedContext();

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddD2PropagatedContextOutbound_RegistersResolvableInterceptor()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAmbientRequestScopeAccessor>(new StubAmbientScope());
        services.AddD2PropagatedContextOutbound();

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<PropagatedContextClientInterceptor>().Should().NotBeNull();
    }

    [Fact]
    public void AddD2PropagatedContextOutbound_CalledTwice_RegistersSingleInterceptor()
    {
        var services = new ServiceCollection();
        services.AddD2PropagatedContextOutbound();
        services.AddD2PropagatedContextOutbound();

        var count = services.Count(d => d.ServiceType == typeof(PropagatedContextClientInterceptor));

        count.Should().Be(1, "the registration is idempotent (TryAdd)");
    }

    [Fact]
    public void AddD2PropagatedContext_OnClientBuilder_AttachesInterceptorAndChains()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAmbientRequestScopeAccessor>(new StubAmbientScope());
        services.AddD2PropagatedContextOutbound();

        var builder = services
            .AddGrpcClient<TestEcho.TestEchoClient>(o => o.Address = new Uri("https://localhost"))
            .AddD2PropagatedContext();

        builder.Should().NotBeNull("the per-channel extension chains fluently");

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<TestEcho.TestEchoClient>().Should().NotBeNull(
            "the client with the attached propagation interceptor resolves");
    }

    private sealed class StubAmbientScope : IAmbientRequestScopeAccessor
    {
        public IServiceProvider? Current => null;
    }
}
