// -----------------------------------------------------------------------
// <copyright file="GrpcTestHost.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.TypeSpecGrpc;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>
/// Shared real-socket gRPC test-host plumbing for the over-the-wire harnesses
/// (<see cref="MutualTlsSignerHarnessTests"/> + <see cref="OverTheWireResilienceTests"/>).
/// Both stand up a real Kestrel HTTPS endpoint on <c>127.0.0.1:0</c> (an OS-assigned
/// ephemeral loopback port, so a real TCP socket + real TLS handshake), map a generated
/// gRPC service, resolve the bound endpoint, and dial it over a <see cref="GrpcChannel"/>.
/// This helper factors out that identical skeleton, leaving each harness to supply its
/// own specifics through delegates: the service registration body (mTLS wires
/// <c>AddD2MutualTls</c> + the façade; the resilience harness wires a bare shim),
/// the <c>MapGrpcService&lt;T&gt;</c> call (generic over a compile-time type), and the
/// optional client-certificate SSL hook (mTLS presents a leaf chain; the resilience
/// harness presents none). It does NOT collapse those specifics — the mTLS-vs-server-TLS
/// and fault/cert logic stays in each harness.
/// </summary>
internal static class GrpcTestHost
{
    /// <summary>
    /// Starts a real Kestrel HTTPS host on <c>127.0.0.1:0</c> presenting
    /// <paramref name="serverCert"/>, hosting whatever the caller maps. The shared
    /// skeleton — clear-providers, routing + gRPC services, the per-listener
    /// <c>UseHttps</c>, build, start, endpoint-resolve — is run here once.
    /// </summary>
    /// <remarks>
    /// <paramref name="configureServices"/> is invoked BEFORE
    /// <c>ConfigureKestrel</c>/<c>UseHttps</c> so that an <c>AddD2MutualTls</c>
    /// registration (whose <c>ConfigureHttpsDefaults</c> action sets RequireCertificate
    /// + the validation callback) is in place when the per-listener HTTPS defaults apply
    /// — the per-listener server cert composes with, and does not reset, the
    /// client-certificate require + validate. Preserving this ordering is load-bearing:
    /// it is the difference between the mTLS harness actually requiring a client
    /// certificate and silently accepting an uncredentialed connection.
    /// </remarks>
    /// <param name="serverCert">The Kestrel server certificate (a leaf with server EKU).</param>
    /// <param name="configureServices">Per-harness DI registration (façade + mTLS, or a bare shim).</param>
    /// <param name="mapServices">Per-harness <c>MapGrpcService&lt;T&gt;</c> call(s).</param>
    /// <returns>The running host + the loopback endpoint the channel dials.</returns>
    internal static async Task<RunningServer> StartAsync(
        X509Certificate2 serverCert,
        Action<IServiceCollection> configureServices,
        Action<WebApplication> mapServices)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.Services.AddRouting();
        builder.Services.AddGrpc();

        // Per-harness DI registration runs BEFORE ConfigureKestrel so an AddD2MutualTls
        // ConfigureHttpsDefaults action (RequireCertificate + validation callback) is in
        // place when the per-listener UseHttps below applies the HTTPS defaults.
        configureServices(builder.Services);

        builder.WebHost.ConfigureKestrel(kestrel =>
            kestrel.Listen(
                IPAddress.Loopback,
                0,
                listen => listen.UseHttps(serverCert)));

        var app = builder.Build();
        mapServices(app);

        await app.StartAsync();

        return new RunningServer(app, ResolveEndpoint(app));
    }

    /// <summary>
    /// Builds a gRPC channel that dials <paramref name="endpoint"/> over a real socket,
    /// trusting the loopback self-signed SERVER certificate via
    /// <c>RemoteCertificateValidationCallback</c> — client-side test plumbing for a cert
    /// the machine store does not know. This is the CLIENT validating the SERVER; it does
    /// NOT relax any server-side mutual-TLS client-certificate validation.
    /// </summary>
    /// <remarks>
    /// <paramref name="configureSsl"/> is an optional hook the mTLS harness uses to attach
    /// its <see cref="SslClientAuthenticationOptions.ClientCertificateContext"/> (the
    /// presented client leaf chain); when null (the server-TLS-only resilience harness) no
    /// client certificate is presented, so .NET builds no client-cert context and the
    /// Windows-Schannel limitation that gates the mTLS cert-presenting cases does NOT apply.
    /// </remarks>
    /// <param name="endpoint">The loopback HTTPS endpoint.</param>
    /// <param name="configureSsl">Optional per-harness SSL options hook (e.g. attach the client leaf).</param>
    /// <returns>A configured <see cref="GrpcChannel"/>.</returns>
    [SuppressMessage(
        "Security",
        "CA5359:Do not disable certificate validation",
        Justification = "Client-side trust of the loopback self-signed SERVER cert only "
            + "(it is not in the machine store). This is the CLIENT validating the SERVER; "
            + "it does NOT relax the SERVER's mutual-TLS client-certificate validation, "
            + "which is the property under test. Test harness, loopback only.")]
    internal static GrpcChannel BuildChannel(
        Uri endpoint, Action<SslClientAuthenticationOptions>? configureSsl = null)
    {
        var sslOptions = new SslClientAuthenticationOptions
        {
            // Client-side trust of the loopback self-signed server cert ONLY (it is not in
            // the machine store). Does NOT relax the server's client-cert validation.
            RemoteCertificateValidationCallback = (_, _, _, _) => true,
        };

        configureSsl?.Invoke(sslOptions);

        var handler = new SocketsHttpHandler { SslOptions = sslOptions };

        return GrpcChannel.ForAddress(
            endpoint,
            new GrpcChannelOptions { HttpHandler = handler });
    }

    /// <summary>
    /// Reads the OS-assigned loopback endpoint from the started host's server-address
    /// feature (the ephemeral port chosen for <c>127.0.0.1:0</c>).
    /// </summary>
    /// <param name="app">The started host.</param>
    /// <returns>The <c>https://127.0.0.1:&lt;port&gt;</c> endpoint URI.</returns>
    internal static Uri ResolveEndpoint(WebApplication app)
    {
        var addresses = app.Services
            .GetRequiredService<IServer>()
            .Features
            .Get<IServerAddressesFeature>();

        var address = addresses?.Addresses.FirstOrDefault()
            ?? throw new InvalidOperationException(
                "Kestrel did not report a bound address after StartAsync.");

        return new Uri(address);
    }

    /// <summary>
    /// A running Kestrel host + its resolved loopback endpoint. Disposing stops the
    /// host and releases the bound socket.
    /// </summary>
    internal sealed class RunningServer(WebApplication app, Uri endpoint) : IAsyncDisposable
    {
        public Uri Endpoint => endpoint;

        public async ValueTask DisposeAsync()
        {
            await app.StopAsync();
            await app.DisposeAsync();
        }
    }
}
