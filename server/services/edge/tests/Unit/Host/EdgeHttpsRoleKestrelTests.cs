// -----------------------------------------------------------------------
// <copyright file="EdgeHttpsRoleKestrelTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.Host;

using System.Net;
using System.Reflection;
using D2.Edge.Api.Composition;
using D2.Edge.Api.Kestrel;
using D2.Shared.AspNetCore.Mtls;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

/// <summary>
/// Per-endpoint three-bind pins: Issuer :8443 does not require a client
/// certificate; mTLS :9443 does. Not defaults-only without a per-role assert.
/// </summary>
[Trait("Category", "Unit")]
public sealed class EdgeHttpsRoleKestrelTests : IDisposable
{
    private readonly EdgeHostTestKit r_kit = new();

    public void Dispose() => r_kit.Dispose();

    [Fact]
    public void EdgeHost_IssuerHttpsBind_DoesNotRequireClientCertificate()
    {
        // Simulate MutualTls defaults first (RequireCertificate), then Issuer role
        // override — proves the per-listen policy wins for :8443 / HttpsIssuer.
        var https = new HttpsConnectionAdapterOptions
        {
            ClientCertificateMode = ClientCertificateMode.RequireCertificate,
        };

        EdgeHttpsRolePolicies.ApplyIssuerHttps(https);

        https.ClientCertificateMode.Should().Be(ClientCertificateMode.NoCertificate);

        https.ClientCertificateMode.Should()
            .NotBe(ClientCertificateMode.RequireCertificate);

        https.ClientCertificateValidation.Should().BeNull();
        EdgeHttpsRolePolicies.ISSUER_HTTPS_PORT.Should().Be(8443);
        EdgeHttpsRolePolicies.HTTPS_ISSUER_ENDPOINT_NAME.Should().Be("HttpsIssuer");
    }

    [Fact]
    public void EdgeHost_MtlsHttpsBind_RequiresClientCertificate()
    {
        // mTLS listen uses bare UseHttps() so MutualTls ConfigureHttpsDefaults apply.
        // Pin: MutualTls options Enabled + RequireCertificate mode + port 9443.
        using var sp = BuildProvider();
        var mtls = sp.GetRequiredService<IOptions<D2MutualTlsOptions>>().Value;

        mtls.Enabled.Should().BeTrue();
        mtls.TrustAnchorsProvider.Should().NotBeNull();

        EdgeHttpsRolePolicies.MtlsClientCertificateMode
            .Should().Be(ClientCertificateMode.RequireCertificate);

        EdgeHttpsRolePolicies.MTLS_HTTPS_PORT.Should().Be(9443);
        EdgeHttpsRolePolicies.HTTPS_MTLS_ENDPOINT_NAME.Should().Be("HttpsMtls");

        // EdgeHttpsRoleKestrelConfigure is registered alongside MutualTls's
        // IConfigureOptions — mTLS listen inherits defaults (RequireCertificate).
        sp.GetServices<IConfigureOptions<KestrelServerOptions>>()
            .Select(c => c.GetType().Name)
            .Should().Contain(n => n.Contains("MutualTls", StringComparison.Ordinal));

        sp.GetServices<IConfigureOptions<KestrelServerOptions>>()
            .OfType<EdgeHttpsRoleKestrelConfigure>()
            .Should().ContainSingle();
    }

    [Fact]
    public void EdgeHttpsRoleKestrelConfigure_IsRegistered_AndPortsAreDistinct()
    {
        using var sp = BuildProvider();

        sp.GetServices<IConfigureOptions<KestrelServerOptions>>()
            .OfType<EdgeHttpsRoleKestrelConfigure>()
            .Should().ContainSingle();

        EdgeHttpsRolePolicies.HTTP_PORT.Should().Be(8080);

        EdgeHttpsRolePolicies.ISSUER_HTTPS_PORT.Should()
            .NotBe(EdgeHttpsRolePolicies.MTLS_HTTPS_PORT);

        EdgeHttpsRolePolicies.IssuerClientCertificateMode.Should()
            .NotBe(ClientCertificateMode.RequireCertificate);

        EdgeHttpsRolePolicies.MtlsClientCertificateMode.Should()
            .Be(ClientCertificateMode.RequireCertificate);
    }

    [Fact]
    public void EdgeHttpsRoleKestrelConfigure_Configure_ListensThreePorts()
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
                            EdgeHttpsRoleKestrelConfigure>();
                    })
                    .Configure(_ => { });
            })
            .Build();

        var options = host.Services
            .GetRequiredService<IOptions<KestrelServerOptions>>().Value;

        var ports = GetListenPorts(options).OrderBy(p => p).ToArray();

        ports.Should().Equal(
            EdgeHttpsRolePolicies.HTTP_PORT,
            EdgeHttpsRolePolicies.ISSUER_HTTPS_PORT,
            EdgeHttpsRolePolicies.MTLS_HTTPS_PORT);

        // Configure wires Issuer via ApplyIssuerHttps (NoCertificate).
        var configureSource = File.ReadAllText(
            EdgeHostTestKit.ResolveEdgeApiSourceFile(
                "Kestrel", "EdgeHttpsRoleKestrelConfigure.cs"));

        configureSource.Should().Contain("ListenAnyIP");
        configureSource.Should().Contain("ApplyIssuerHttps");
        configureSource.Should().Contain("EdgeHttpsRolePolicies.HTTP_PORT");
        configureSource.Should().Contain("EdgeHttpsRolePolicies.ISSUER_HTTPS_PORT");
        configureSource.Should().Contain("EdgeHttpsRolePolicies.MTLS_HTTPS_PORT");
    }

    [Fact]
    public void EdgeHttpsRoleKestrelConfigure_Configure_NullOptions_Throws()
    {
        var act = () => new EdgeHttpsRoleKestrelConfigure().Configure(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ApplyIssuerHttps_NullOptions_Throws()
    {
        var act = () => EdgeHttpsRolePolicies.ApplyIssuerHttps(null!);

        act.Should().Throw<ArgumentNullException>();
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

    private ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddD2EdgeHost(r_kit.BuildConfiguration());

        return services.BuildServiceProvider();
    }
}
