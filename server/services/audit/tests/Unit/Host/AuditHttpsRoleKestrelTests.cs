// -----------------------------------------------------------------------
// <copyright file="AuditHttpsRoleKestrelTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Audit.Tests.Unit.Host;

using System.Net;
using System.Reflection;
using D2.Audit.Api.Composition;
using D2.Audit.Api.Kestrel;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

/// <summary>
/// Dual-bind Kestrel pins for Audit: HTTP :8080 + mTLS HTTPS :8443
/// (RequireCertificate via MutualTls defaults / policy constant).
/// </summary>
[Trait("Category", "Unit")]
public sealed class AuditHttpsRoleKestrelTests : IDisposable
{
    private readonly AuditHostTestKit r_kit = new();

    public void Dispose() => r_kit.Dispose();

    [Fact]
    public void AuditHttpsRolePolicies_PublicConstants_ArePinned()
    {
        AuditHttpsRolePolicies.HTTP_PORT.Should().Be(8080);
        AuditHttpsRolePolicies.MTLS_HTTPS_PORT.Should().Be(8443);
        AuditHttpsRolePolicies.MtlsClientCertificateMode
            .Should().Be(ClientCertificateMode.RequireCertificate);
    }

    [Fact]
    public void AuditHttpsRoleKestrelConfigure_Configure_ListensHttpAndMtlsPorts()
    {
        // HostBuilder + UseKestrel registers IHttpsConfigurationService so
        // production Configure can call UseHttps without a live bind/Start.
        using var host = new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseKestrel()
                    .ConfigureServices(services =>
                    {
                        services.AddLogging();
                        services.AddSingleton<
                            IConfigureOptions<KestrelServerOptions>,
                            AuditHttpsRoleKestrelConfigure>();
                    })
                    .Configure(_ => { });
            })
            .Build();

        var options = host.Services
            .GetRequiredService<IOptions<KestrelServerOptions>>().Value;

        var ports = GetListenPorts(options).OrderBy(p => p).ToArray();

        ports.Should().Equal(
            AuditHttpsRolePolicies.HTTP_PORT,
            AuditHttpsRolePolicies.MTLS_HTTPS_PORT);

        var configureSource = File.ReadAllText(
            AuditHostTestKit.ResolveAuditApiSourceFile(
                "Kestrel", "AuditHttpsRoleKestrelConfigure.cs"));

        configureSource.Should().Contain("ListenAnyIP");
        configureSource.Should().Contain("AuditHttpsRolePolicies.HTTP_PORT");
        configureSource.Should().Contain("AuditHttpsRolePolicies.MTLS_HTTPS_PORT");
        configureSource.Should().Contain("UseHttps()");
    }

    [Fact]
    public void AuditHttpsRoleKestrelConfigure_Configure_NullOptions_Throws()
    {
        var act = () => new AuditHttpsRoleKestrelConfigure().Configure(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AuditHttpsRoleKestrelConfigure_IsRegistered_OnAddD2AuditHost()
    {
        var services = new ServiceCollection();
        services.AddD2AuditHost(r_kit.BuildConfiguration());

        services.Any(d =>
                d.ServiceType == typeof(IConfigureOptions<KestrelServerOptions>)
                && d.ImplementationType == typeof(AuditHttpsRoleKestrelConfigure))
            .Should().BeTrue();
    }

    private static IReadOnlyList<int> GetListenPorts(KestrelServerOptions options)
    {
        // KestrelServerOptions.ListenOptions is internal — reflect by element type.
        var prop = typeof(KestrelServerOptions)
            .GetProperties(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .FirstOrDefault(p =>
                p.PropertyType.IsGenericType
                && p.PropertyType.GetGenericArguments() is { Length: 1 } args
                && args[0].Name == "ListenOptions");

        prop.Should().NotBeNull(
            "KestrelServerOptions must expose a ListenOptions collection");

        var list = (System.Collections.IEnumerable)prop.GetValue(options)!;
        var ports = new List<int>();

        foreach (var item in list)
        {
            if (item is null)
                continue;

            var endPointProp = item.GetType().GetProperty("EndPoint")
                ?? item.GetType().GetProperty(
                    "IPEndPoint",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var endPoint = endPointProp?.GetValue(item) as IPEndPoint;

            if (endPoint is not null)
                ports.Add(endPoint.Port);
        }

        return ports;
    }
}
