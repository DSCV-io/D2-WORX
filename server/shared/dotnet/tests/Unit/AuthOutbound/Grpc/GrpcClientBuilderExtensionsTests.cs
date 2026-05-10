// -----------------------------------------------------------------------
// <copyright file="GrpcClientBuilderExtensionsTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.AuthOutbound.Grpc;

using AwesomeAssertions;
using D2.Shared.Auth.Outbound.Grpc;
using D2.Shared.Auth.Outbound.ServiceIdentity;
using D2.Shared.Result;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

/// <summary>
/// Coverage for the per-channel <c>.AddD2ServiceIdentity()</c> opt-in
/// extension. The extension is intentionally non-generic so it can chain
/// onto a bare <see cref="IHttpClientBuilder"/> after
/// <c>AddGrpcClient&lt;T&gt;()</c>; the tests pin that call shape and the
/// argument-validation contract.
/// </summary>
public sealed class GrpcClientBuilderExtensionsTests
{
    [Fact]
    public void AddD2ServiceIdentity_CompilesWithoutExplicitGeneric()
    {
        // The test's existence proves the call shape compiles — calling
        // .AddD2ServiceIdentity() on a bare IHttpClientBuilder must not
        // require an explicit type-arg from the caller. The runtime reaches
        // gRPC's ConfigureChannel infrastructure (which requires a gRPC
        // client builder, not a bare IHttpClientBuilder), so we expect that
        // specific InvalidOperationException — confirms the extension wires
        // through to gRPC and didn't silently no-op.
        var services = new ServiceCollection();
        services.AddSingleton<IServiceIdentityClient, NoopServiceIdentityClient>();
        var builder = services.AddHttpClient("test");

        var act = () => builder.AddD2ServiceIdentity();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*gRPC client*");
    }

    [Fact]
    public void AddD2ServiceIdentity_NullBuilder_Throws()
    {
        IHttpClientBuilder? builder = null;

        var act = () => builder!.AddD2ServiceIdentity();

        act.Should().Throw<ArgumentNullException>();
    }

    private sealed class NoopServiceIdentityClient : IServiceIdentityClient
    {
        public ValueTask<D2Result<string>> GetCurrentTokenAsync(CancellationToken ct = default) =>
            ValueTask.FromResult(D2Result<string>.Ok("noop-token"));
    }
}
