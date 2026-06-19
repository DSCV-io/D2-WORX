// -----------------------------------------------------------------------
// <copyright file="WorkloadCertificateRegistrationTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.AuthOutbound.WorkloadCertificate;

using AwesomeAssertions;
using D2.Shared.Auth.Outbound;
using D2.Shared.Auth.Outbound.Grpc;
using D2.Shared.Auth.Outbound.ServiceIdentity;
using D2.Shared.Auth.Outbound.WorkloadCertificate;
using D2.Shared.Result;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

/// <summary>
/// DI-resolution + per-channel-opt-in coverage for the workload-certificate
/// outbound stack — every registered seam resolves via
/// <c>GetRequiredService&lt;&gt;</c>, the gRPC builder opt-in compiles + guards
/// null, and the two channel opt-ins (<c>AddD2ServiceIdentity</c> +
/// <c>AddD2WorkloadCertificate</c>) coexist (compose-don't-clobber).
/// </summary>
[Trait("Category", "Unit")]
public sealed class WorkloadCertificateRegistrationTests
{
    // -----------------------------------------------------------------------
    // DI resolution — every seam resolves
    // -----------------------------------------------------------------------

    [Fact]
    public void AddD2WorkloadCertificateOutbound_ResolvesEverySeam()
    {
        using var provider = BuildProvider();

        provider.GetRequiredService<WorkloadLeafCache>().Should().NotBeNull();
        provider.GetRequiredService<WorkloadLeafClient>().Should().NotBeNull();
        provider.GetRequiredService<IWorkloadLeafSource>().Should().NotBeNull();
    }

    [Fact]
    public void AddD2WorkloadCertificateOutbound_RegistersTheRefreshHostedService()
    {
        using var provider = BuildProvider();

        var hosted = provider.GetServices<IHostedService>();

        hosted.Should().ContainSingle(h => h is WorkloadLeafRefreshHostedService);
    }

    [Fact]
    public void IWorkloadLeafSource_AndClient_ResolveToTheSameSingleton()
    {
        using var provider = BuildProvider();

        var source = provider.GetRequiredService<IWorkloadLeafSource>();
        var client = provider.GetRequiredService<WorkloadLeafClient>();

        source.Should().BeSameAs(client);
    }

    // -----------------------------------------------------------------------
    // gRPC builder opt-in — call shape + null guard + compose-don't-clobber
    // -----------------------------------------------------------------------

    [Fact]
    public void AddD2WorkloadCertificate_CompilesWithoutExplicitGeneric()
    {
        // Proves the call shape compiles on a bare IHttpClientBuilder; reaching gRPC's
        // ConfigureChannel infrastructure (which needs a gRPC client builder) yields
        // the gRPC InvalidOperationException — confirms the extension wires through.
        var services = new ServiceCollection();
        services.AddSingleton<WorkloadLeafCache>();
        services.AddSingleton(TimeProvider.System);
        var builder = services.AddHttpClient("test");

        var act = () => builder.AddD2WorkloadCertificate();

        act.Should().Throw<InvalidOperationException>().WithMessage("*gRPC client*");
    }

    [Fact]
    public void AddD2WorkloadCertificate_NullBuilder_Throws()
    {
        IHttpClientBuilder? builder = null;

        var act = () => builder!.AddD2WorkloadCertificate();

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void BothChannelOptIns_Coexist_OnTheSameBuilder()
    {
        // The leaf opt-in (handler SslOptions) and the token opt-in (call
        // credentials) are orthogonal; chaining both must not throw at registration.
        var services = new ServiceCollection();
        services.AddSingleton<WorkloadLeafCache>();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IServiceIdentityClient, NoopServiceIdentityClient>();
        var builder = services.AddHttpClient("composed");

        var act = () => builder.AddD2ServiceIdentity().AddD2WorkloadCertificate();

        // Both ConfigureChannel registrations apply; the throw (if any) only comes
        // from the gRPC infra at channel-build time, not from a clobber here.
        act.Should().Throw<InvalidOperationException>().WithMessage("*gRPC client*");
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IWorkloadCertificateIssuer, NoopIssuer>();
        services.AddD2WorkloadCertificateOutbound();

        return services.BuildServiceProvider();
    }

    private sealed class NoopIssuer : IWorkloadCertificateIssuer
    {
        public ValueTask<D2Result<WorkloadLeafMaterial>> IssueAsync(CancellationToken ct = default) =>
            ValueTask.FromResult(D2Result<WorkloadLeafMaterial>.ServiceUnavailable());
    }

    private sealed class NoopServiceIdentityClient : IServiceIdentityClient
    {
        public ValueTask<D2Result<string>> GetCurrentTokenAsync(CancellationToken ct = default) =>
            ValueTask.FromResult(D2Result<string>.Ok("noop-token"));
    }
}
