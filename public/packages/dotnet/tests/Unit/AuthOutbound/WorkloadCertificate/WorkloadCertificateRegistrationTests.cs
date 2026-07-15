// -----------------------------------------------------------------------
// <copyright file="WorkloadCertificateRegistrationTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.AuthOutbound.WorkloadCertificate;

using AwesomeAssertions;
using DcsvIo.D2.Auth.Outbound;
using DcsvIo.D2.Auth.Outbound.Grpc;
using DcsvIo.D2.Auth.Outbound.WorkloadCertificate;
using DcsvIo.D2.Result;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

/// <summary>
/// DI-resolution + per-channel-opt-in coverage for the workload-certificate
/// outbound stack — every registered seam resolves via
/// <c>GetRequiredService&lt;&gt;</c>, and the gRPC builder opt-in compiles +
/// guards null. The compose-don't-clobber coexistence of the leaf opt-in with
/// the forwarded-JWT credential opt-in is covered by
/// <c>AddD2ForwardedJwtExtensionTests</c>.
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
        public ValueTask<D2Result<WorkloadLeafMaterial>> IssueAsync(
            byte[] csrDer, CancellationToken ct = default) =>
            ValueTask.FromResult(D2Result<WorkloadLeafMaterial>.ServiceUnavailable());
    }
}
