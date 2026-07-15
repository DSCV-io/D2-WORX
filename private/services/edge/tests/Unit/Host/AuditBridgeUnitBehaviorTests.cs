// -----------------------------------------------------------------------
// <copyright file="AuditBridgeUnitBehaviorTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.Host;

using System.Text.Json;
using System.Threading.Tasks;
using D2.Audit.Client;
using D2.Audit.Client.Ping;
using D2.Edge.Api.Bridges.Audit;
using D2.Edge.Tests.Unit.KeyCustodian.TypeSpecRoute.Fixtures;
using D2.Private.Auth;
using D2.Shared.Auth;
using D2.Shared.Auth.Abstractions.Jwks;
using D2.Shared.Auth.Abstractions.Sessions;
using D2.Shared.Auth.Http;
using D2.Shared.Caching;
using D2.Shared.Caching.Local.Default;
using D2.Shared.Resilience.Pipeline;
using D2.Shared.Result;
using D2.Shared.Result.Grpc;
using Grpc.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

/// <summary>
/// Cheap unit doubles for Edge HTTPΓåÆgRPC Audit bridge map behavior (typed NIE
/// vs transport-fault Audit-down vs mid-handler cancel). Not multiproc proof ΓÇö
/// dual-Kestrel OUT. ┬º1.32: doubles assert CallCount + input on the real
/// <see cref="IAuditGrpcClient"/> seam; replace-trigger is live
/// <c>AddD2AuditGrpcClients</c> on the Edge host.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "TestHost lifetime is bounded by individual tests.")]
[Trait("Category", "Unit")]
public sealed class AuditBridgeUnitBehaviorTests
{
    private const string _ISSUER = "https://edge.internal";
    private const string _AUDIENCE = "d2.internal";

    [Fact]
    public async Task AuditBridge_HttpToGrpc_ReturnsNieOrTypedNotImplemented()
    {
        // Production PingAudit handler returns typed ServiceUnavailable (NIE).
        using var jwt = new TestJwtBuilder();
        var auditStub = new TypedNieAuditGrpcClientStub();
        using var host = await BuildHostAsync(jwt, auditStub);
        var client = await CreateAuthedClientAsync(host, jwt);

        var response = await client.GetAsync(new Uri("/api/v1/audit/ping", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("status").GetInt32().Should().Be(503);
        auditStub.CallCount.Should().Be(1);
        auditStub.LastInput.Should().NotBeNull();
    }

    [Fact]
    public async Task AuditBridge_AuditDown_ReturnsServiceUnavailable()
    {
        // Distinct transport-fault path: maps RpcException via
        // ToTransportFaultResult (same surface as live AuditGrpcClient).
        using var jwt = new TestJwtBuilder();
        var auditStub = new TransportFaultAuditGrpcClientStub();
        using var host = await BuildHostAsync(jwt, auditStub);
        var client = await CreateAuthedClientAsync(host, jwt);

        var response = await client.GetAsync(new Uri("/api/v1/audit/ping", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        auditStub.CallCount.Should().Be(1);
        auditStub.LastInput.Should().NotBeNull();
    }

    [Fact]
    public async Task AuditBridge_Canceled_PropagatesCancellation()
    {
        // Mid-handler cancel: double must enter PingAuditAsync, then CTS cancel
        // so Map/client cancel path runs â€” never pre-cancel GetAsync.
        using var jwt = new TestJwtBuilder();
        var auditStub = new BlockingCancelAuditGrpcClientStub();
        using var host = await BuildHostAsync(jwt, auditStub);
        var client = await CreateAuthedClientAsync(host, jwt);
        using var cts = new CancellationTokenSource();

        var getTask = client.GetAsync(
            new Uri("/api/v1/audit/ping", UriKind.Relative),
            cts.Token);

        await auditStub.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await cts.CancelAsync();

        var thrown = false;

        try
        {
            await getTask;
        }
        catch (OperationCanceledException)
        {
            thrown = true;
        }

        thrown.Should().BeTrue("canceled token must surface OperationCanceledException");
        auditStub.CallCount.Should().Be(1);
        auditStub.LastInput.Should().NotBeNull();
    }

    private static Task<System.Net.Http.HttpClient> CreateAuthedClientAsync(
        IHost host,
        TestJwtBuilder jwt)
    {
        var client = host.GetTestClient();
        var token = jwt.MintToken(
            _ISSUER,
            _AUDIENCE,
            extraClaims: new Dictionary<string, object>
            {
                ["scope"] = ProductScopes.Internal.Audit.Ping,
            });
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return Task.FromResult(client);
    }

    private static async Task<IHost> BuildHostAsync(
        TestJwtBuilder jwtBuilder,
        IAuditGrpcClient auditClient)
    {
        var hostBuilder = new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost
                    .UseTestServer()
                    .ConfigureServices(services =>
                    {
                        services.AddLogging();
                        services.AddRouting();
                        services.AddD2LocalCache();
                        services.AddSingleton<ITieredCache, FakeTieredCacheStub>();
                        services.AddD2Auth(opts =>
                        {
                            opts.Issuer = new Uri(_ISSUER);
                            opts.Audience = _AUDIENCE;
                        });

                        services.RemoveAll<IJwksProvider>();
                        services.AddSingleton<IJwksProvider>(
                            new FakeJwksProvider(jwtBuilder.PublicKey));

                        services.RemoveAll<ISessionLivenessTracker>();
                        services.AddSingleton<ISessionLivenessTracker>(
                            new FakeSessionLivenessTracker());

                        services.AddD2AuthHttp();
                        services.AddSingleton(auditClient);
                    })
                    .Configure(app =>
                    {
                        app.UseRouting();
                        app.UseD2Auth();
                        app.UseEndpoints(endpoints =>
                        {
                            // Production bridge Map (generated) â€” not handler alone.
                            endpoints.MapPingAuditBridge();
                        });
                    });
            });

        return await hostBuilder.StartAsync();
    }

    /// <summary>
    /// Â§1.32 double â€” typed business NIE (ServiceUnavailable envelope).
    /// Replace-trigger: live <c>AddD2AuditGrpcClients</c> on Edge host.
    /// </summary>
    private sealed class TypedNieAuditGrpcClientStub : IAuditGrpcClient
    {
        public int CallCount { get; private set; }

        public PingAuditInput? LastInput { get; private set; }

        public ValueTask<D2Result<PingAuditOutput?>> PingAuditAsync(
            PingAuditInput input,
            ResilientPipeline<string, PingAuditOutput?>? pipelineOverride = null,
            CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            CallCount++;
            LastInput = input;
            return ValueTask.FromResult(D2Result<PingAuditOutput?>.ServiceUnavailable());
        }
    }

    /// <summary>
    /// Â§1.32 double â€” transport-fault path via
    /// <c>RpcException.ToTransportFaultResult</c> (live client mapping).
    /// Replace-trigger: live <c>AddD2AuditGrpcClients</c> on Edge host.
    /// </summary>
    private sealed class TransportFaultAuditGrpcClientStub : IAuditGrpcClient
    {
        public int CallCount { get; private set; }

        public PingAuditInput? LastInput { get; private set; }

        public ValueTask<D2Result<PingAuditOutput?>> PingAuditAsync(
            PingAuditInput input,
            ResilientPipeline<string, PingAuditOutput?>? pipelineOverride = null,
            CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            CallCount++;
            LastInput = input;

            // Mirror AuditGrpcClient transport-fault remap (Unavailable â†’ SU).
            var fault = new RpcException(new Status(StatusCode.Unavailable, "audit-down-stub"));
            return ValueTask.FromResult(fault.ToTransportFaultResult<PingAuditOutput?>());
        }
    }

    /// <summary>
    /// Â§1.32 double â€” blocks after entry so the test can cancel mid-handler.
    /// Replace-trigger: live <c>AddD2AuditGrpcClients</c> on Edge host.
    /// </summary>
    private sealed class BlockingCancelAuditGrpcClientStub : IAuditGrpcClient
    {
        public int CallCount { get; private set; }

        public PingAuditInput? LastInput { get; private set; }

        public TaskCompletionSource Entered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async ValueTask<D2Result<PingAuditOutput?>> PingAuditAsync(
            PingAuditInput input,
            ResilientPipeline<string, PingAuditOutput?>? pipelineOverride = null,
            CancellationToken ct = default)
        {
            CallCount++;
            LastInput = input;
            Entered.TrySetResult();

            // Hold until RequestAborted / client cancel â€” proves Map hit client.
            await Task.Delay(Timeout.Infinite, ct);

            return D2Result<PingAuditOutput?>.ServiceUnavailable();
        }
    }
}
