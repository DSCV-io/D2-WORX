// -----------------------------------------------------------------------
// <copyright file="AddD2EdgeHostAuditBridgeTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.Tests.Unit.Host;

using DcsvIo.D2.Private.Audit.Client;
using DcsvIo.D2.Private.Edge.Api.Composition;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Edge host Audit bridge registration pins: <see cref="IAuditGrpcClient"/>
/// resolves; channel Address https pin; missing/non-https fails loud.
/// </summary>
[Trait("Category", "Unit")]
public sealed class AddD2EdgeHostAuditBridgeTests : IDisposable
{
    private readonly EdgeHostTestKit r_kit = new();

    public void Dispose() => r_kit.Dispose();

    [Fact]
    public void AddD2EdgeHost_RegistersIAuditGrpcClient()
    {
        var descriptors = new ServiceCollection();
        descriptors.AddD2EdgeHost(r_kit.BuildConfiguration());

        descriptors.Any(d => d.ServiceType == typeof(IAuditGrpcClient))
            .Should().BeTrue();
    }

    [Fact]
    public void AddD2EdgeHost_ResolvesIAuditGrpcClient()
    {
        // Descriptor presence ≠ resolvability (§1.3). https Address from kit
        // (production default https://d2-audit:8443); channel is lazy.
        var services = new ServiceCollection();
        services.AddD2EdgeHost(r_kit.BuildConfiguration());

        using var sp = services.BuildServiceProvider();

        sp.GetRequiredService<IAuditGrpcClient>()
            .Should().BeAssignableTo<IAuditGrpcClient>();
    }

    [Fact]
    public void AuditBridge_ChannelAddress_IsHttps_DefaultConfig()
    {
        // Composition accepts the default https://d2-audit:8443 from the kit.
        var services = new ServiceCollection();
        var act = () => services.AddD2EdgeHost(r_kit.BuildConfiguration());

        act.Should().NotThrow();
    }

    [Fact]
    public void AddD2EdgeHost_MissingAuditGrpcAddress_Throws()
    {
        var services = new ServiceCollection();
        var config = r_kit.BuildConfiguration(
            new Dictionary<string, string?>
            {
                ["AUDIT_GRPC:Address"] = null,
                ["AUDIT_GRPC__ADDRESS"] = null,
            });

        var act = () => services.AddD2EdgeHost(config);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*AUDIT_GRPC*");
    }

    [Fact]
    public void AddD2EdgeHost_NonHttpsAuditGrpcAddress_Throws()
    {
        var services = new ServiceCollection();
        var config = r_kit.BuildConfiguration(
            new Dictionary<string, string?>
            {
                ["AUDIT_GRPC:Address"] = "http://d2-audit:8443",
            });

        var act = () => services.AddD2EdgeHost(config);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*https*");
    }

    [Fact]
    public void MapD2EdgeEndpoints_Source_ContainsMapAllAuditBridges()
    {
        var path = EdgeHostTestKit.ResolveEdgeApiSourceFile(
            "Composition", "EdgeEndpointRouteBuilderExtensions.cs");

        File.Exists(path).Should().BeTrue($"Map source at {path}");

        var source = File.ReadAllText(path);

        source.Should().Contain("MapAllAuditBridges()");
    }
}
