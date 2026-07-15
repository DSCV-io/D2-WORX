// -----------------------------------------------------------------------
// <copyright file="GrpcInboundForwardingIntegrationTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Auth.Inbound.Grpc;

using System.Collections.Generic;
using AwesomeAssertions;
using D2.Shared.Auth;
using D2.Shared.Auth.Abstractions;
using D2.Shared.Auth.Grpc;
using D2.Shared.Auth.Grpc.Endpoints;
using D2.Shared.Auth.Outbound.Grpc;
using D2.Shared.Auth.Validation;
using D2.Shared.Caching;
using D2.Shared.Caching.Local.Default;
using D2.Shared.Headers.Grpc;
using D2.Shared.Result;
using D2.Shared.Tests.Unit.Auth.Inbound.Grpc.Fixtures;
using D2.Shared.Tests.Unit.Auth.Inbound.Grpc.Protos;
using global::Grpc.Core;
using global::Grpc.Net.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;
using GrpcStatusCode = global::Grpc.Core.StatusCode;

/// <summary>
/// End-to-end proof that the WHOLE forwarded-JWT chain works for a gRPC-inbound
/// origin — the exact gap roadmap row B16 closes. Stands a real ASP.NET Core gRPC
/// server via <see cref="TestServer"/> wired with <c>AddD2AuthGrpc()</c> ONLY (NOT
/// <c>AddD2AuthHttp()</c>), so it proves the gRPC transport ALONE now self-wires the
/// ambient-scope adapter. Inside the gRPC request the test service resolves the
/// adapter, reads back the captured bearer the interceptor stashed
/// (<c>inbound bearer → interceptor capture → adapter read-back</c>), and closes the
/// loop to the OUTBOUND credential by building
/// <see cref="ForwardedJwtCallCredentials.FromAmbientRequestScope"/>, extracting its
/// <see cref="AsyncAuthInterceptor"/>, and invoking it — asserting the gRPC-inbound
/// request's own token is what the outbound hop would forward, verbatim — WITHOUT a
/// second running backend (the credential's interceptor is invoked directly, the way
/// the outbound forwarded-JWT credential tests do).
/// </summary>
/// <remarks>
/// Sibling to <c>GrpcAuthIntegrationTests</c>; reuses its in-process gRPC
/// <see cref="TestServer"/> pattern, the <c>TestJwtBuilder</c> /
/// <c>FakeJwksProvider</c> fixtures, and the test proto. The headline service
/// performs the read-back / forward inside the call and records the observed result
/// into a per-call <see cref="ForwardingProbe"/> the test inspects afterward.
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "TestHost lifetime is bounded by individual tests, not the class.")]
public sealed class GrpcInboundForwardingIntegrationTests
{
    private const string _ISSUER = "https://edge.internal";
    private const string _AUDIENCE = "files";
    private const string _SCOPE = "test.scope";

    [Fact]
    public async Task GrpcInbound_CapturesBearer_ThenForwardsVerbatim()
    {
        // The headline B16 proof. A gRPC-inbound host (AddD2AuthGrpc() only) must:
        //   1. capture the validated inbound bearer into the request-scoped holder
        //      (the interceptor's existing behavior), and
        //   2. let the outbound forwarding credential read THAT scope's holder and
        //      forward the exact bytes — the chain that was broken before B16
        //      because AddD2AuthGrpc() never registered the ambient-scope port.
        var probe = new ForwardingProbe();
        using var jwt = new TestJwtBuilder();
        using var host = await BuildHostAsync(jwt, probe);
        using var channel = CreateChannel(host);
        var client = new TestEcho.TestEchoClient(channel);
        var token = jwt.MintToken(
            _ISSUER,
            _AUDIENCE,
            extraClaims: new Dictionary<string, object>
            {
                ["scope"] = _SCOPE,
            });
        var headers = new Metadata { { "authorization", "Bearer " + token } };

        var reply = await client.EchoAsync(new EchoRequest { Payload = "fwd" }, headers);

        reply.Echoed.Should().Be("fwd");

        // The service ran the read-back + outbound-forward inside the gRPC call.
        probe.Observed.Should().BeTrue("the test service method must have run");
        probe.Error.Should().BeNull();

        // Read-back: the request-scoped holder carried the EXACT inbound bearer the
        // interceptor captured (JwtAuthInterceptor capture site).
        probe.HolderRevealedBytes.Should().Be(token);

        // Forward: the outbound credential built from the resolved port attached the
        // SAME bytes, verbatim, with exactly one Bearer scheme prefix.
        probe.ForwardedAuthorizationHeader.Should().Be("Bearer " + token);
    }

    [Fact]
    public async Task GrpcInbound_HarmlessEndpoint_NoCapture_OutboundHardFails()
    {
        // A harmless endpoint short-circuits the interceptor BEFORE capture, so the
        // holder's Current is null. An outbound credential built from the resolved
        // port must hard-fail Unauthenticated (no token to forward) rather than
        // silently send nothing. Proves the absent-token path is correct on a
        // gRPC-inbound origin too.
        var probe = new ForwardingProbe();
        using var jwt = new TestJwtBuilder();
        using var host = await BuildHostAsync(jwt, probe);
        using var channel = CreateChannel(host);
        var client = new TestHealth.TestHealthClient(channel);

        var reply = await client.HealthAsync(new HealthRequest());

        reply.Status.Should().Be("ok");

        probe.Observed.Should().BeTrue("the harmless service method must have run");
        probe.Error.Should().BeNull();

        // No capture happened — the holder is empty.
        probe.HolderRevealedBytes.Should().BeNull();

        // The outbound credential hard-fails rather than attaching nothing.
        probe.ForwardHardFailedUnauthenticated.Should().BeTrue();
        probe.ForwardedAuthorizationHeader.Should().BeNull();
    }

    [Fact]
    public void GrpcInbound_NoAmbientScope_OutboundHardFails()
    {
        // A genuinely system-initiated call (no inbound gRPC request, no HttpContext
        // on the AsyncLocal) — resolve the port from a ROOT provider and build the
        // credential. It must hard-fail Unauthenticated (parity with the HTTP-side
        // contract). No TestServer needed: the registration + the credential are
        // exercised directly off the root container.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddD2LocalCache();
        services.AddSingleton<ITieredCache, FakeTieredCacheStub>();
        services.AddD2Auth(opts =>
        {
            opts.Issuer = new Uri(_ISSUER);
            opts.Audience = _AUDIENCE;
        });
        services.AddD2AuthGrpc();
        using var root = services.BuildServiceProvider();

        var port = root.GetRequiredService<IAmbientRequestScopeAccessor>();
        port.Current.Should().BeNull("no inbound gRPC request is on the execution context");

        var credentials = ForwardedJwtCallCredentials.FromAmbientRequestScope(port);
        var (status, metadata) = InvokeCredentialExpectingRpc(credentials);

        status.StatusCode.Should().Be(GrpcStatusCode.Unauthenticated);
        metadata.Get(GrpcHeaders.AUTHORIZATION).Should().BeNull();
    }

    private static async Task<IHost> BuildHostAsync(TestJwtBuilder jwtBuilder, ForwardingProbe probe)
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
                        services.AddSingleton(probe);
                        services.AddD2Auth(opts =>
                        {
                            opts.Issuer = new Uri(_ISSUER);
                            opts.Audience = _AUDIENCE;
                        });

                        // Swap the network-touching JwksProvider for the in-memory
                        // fake (mirrors GrpcAuthIntegrationTests.BuildHostAsync).
                        services.RemoveAll<D2.Shared.Auth.Abstractions.Jwks.IJwksProvider>();
                        services.RemoveAll<D2.Shared.Auth.Jwks.HttpJwksProvider>();
                        services.AddSingleton<D2.Shared.Auth.Abstractions.Jwks.IJwksProvider>(
                            new FakeJwksProvider(jwtBuilder.PublicKey));
                        services.RemoveAll<JwtValidator>();
                        services.AddSingleton(sp => new JwtValidator(
                            sp.GetRequiredService<
                                D2.Shared.Auth.Abstractions.Jwks.IJwksProvider>(),
                            sp.GetRequiredService<IOptions<AuthOptions>>(),
                            sp.GetRequiredService<ClaimsToContextMapper>(),
                            Microsoft.Extensions.Logging.Abstractions
                                .NullLogger<JwtValidator>.Instance));

                        // gRPC stack + AddD2AuthGrpc() ONLY — the whole point of B16
                        // is that the gRPC path self-wires the ambient-scope adapter
                        // without AddD2AuthHttp().
                        services.AddGrpc();
                        services.AddD2AuthGrpc();
                    })
                    .Configure(app =>
                    {
                        app.UseRouting();
                        app.UseEndpoints(endpoints =>
                        {
                            endpoints.MapGrpcService<ForwardingEchoService>()
                                .RequireAnyScope(_SCOPE);
                            endpoints.MapGrpcService<ForwardingHealthService>()
                                .MarkAsD2HarmlessEndpoint();
                        });
                    });
            });

        var host = await hostBuilder.StartAsync();
        return host;
    }

    private static GrpcChannel CreateChannel(IHost host)
    {
        var testServer = host.GetTestServer();
        var httpClient = testServer.CreateClient();
        httpClient.BaseAddress = new Uri("http://localhost");
        return GrpcChannel.ForAddress(
            httpClient.BaseAddress,
            new GrpcChannelOptions { HttpClient = httpClient });
    }

    private static (global::Grpc.Core.Status Status, Metadata Metadata) InvokeCredentialExpectingRpc(
        CallCredentials credentials)
    {
        var interceptor = ExtractInterceptor(credentials);
        var metadata = new Metadata();
        var context = new AuthInterceptorContext("https://callee.internal", "/svc/Method", default);

        // The credential interceptor body is synchronous; guards throw RpcException
        // synchronously. Resolve the task to surface it.
        var ex = Assert.ThrowsAsync<RpcException>(() => interceptor(context, metadata))
            .GetAwaiter().GetResult();

        return (ex.Status, metadata);
    }

    // Pulls the AsyncAuthInterceptor the CallCredentials carries — same extraction
    // pattern as the ForwardedJwtCallCredentialsTests.
    private static AsyncAuthInterceptor ExtractInterceptor(CallCredentials credentials)
    {
        var configurator = new InterceptorCapturingConfigurator();
        credentials.InternalPopulateConfiguration(configurator, credentials);

        configurator.Captured.Should().NotBeNull(
            "the forwarding credential must be built from an AsyncAuthInterceptor");

        return configurator.Captured!;
    }

    /// <summary>
    /// Per-call sink the test gRPC service writes its read-back / forward
    /// observations into. Registered as a singleton; each test stands its own host,
    /// so there is no cross-test sharing.
    /// </summary>
    private sealed class ForwardingProbe
    {
        public bool Observed { get; set; }

        public string? HolderRevealedBytes { get; set; }

        public string? ForwardedAuthorizationHeader { get; set; }

        public bool ForwardHardFailedUnauthenticated { get; set; }

        public string? Error { get; set; }
    }

    /// <summary>
    /// Scope-protected test service. Inside the call it runs the full B16 chain:
    /// resolve the ambient-scope port from the per-call scope, read the captured
    /// bearer back from the holder, then build the outbound forwarding credential
    /// and invoke its interceptor to prove the verbatim forward.
    /// </summary>
    private sealed class ForwardingEchoService(
        IAmbientRequestScopeAccessor ambientRequestScopeAccessor,
        ForwardingProbe probe)
        : TestEcho.TestEchoBase
    {
        public override async Task<EchoReply> Echo(EchoRequest request, ServerCallContext context)
        {
            try
            {
                // Read-back: the same door the outbound credential reads. Under gRPC
                // the resolved port surfaces the per-call HttpContext.RequestServices.
                var scope = ambientRequestScopeAccessor.Current;
                var holder = scope?.GetService<IForwardedJwtAccessor>();
                var current = holder?.Current;
                if (current is { HasValue: true } jwt)
                    probe.HolderRevealedBytes = jwt.RevealForForwarding();

                // Forward: build the outbound credential exactly as
                // .AddD2ForwardedJwt() does, extract + invoke its interceptor, and
                // record the Authorization header it attaches.
                var credentials =
                    ForwardedJwtCallCredentials.FromAmbientRequestScope(ambientRequestScopeAccessor);
                var interceptor = ExtractInterceptor(credentials);
                var metadata = new Metadata();
                var authContext =
                    new AuthInterceptorContext("https://callee.internal", "/svc/Method", default);
                await interceptor(authContext, metadata);

                probe.ForwardedAuthorizationHeader = metadata.Get(GrpcHeaders.AUTHORIZATION)?.Value;
            }
            catch (Exception ex)
            {
                probe.Error = ex.GetType().Name + ": " + ex.Message;
            }
            finally
            {
                probe.Observed = true;
            }

            return new EchoReply { Echoed = request.Payload };
        }
    }

    /// <summary>
    /// Harmless test service. The interceptor short-circuits before capture, so the
    /// holder is empty; the outbound credential built from the resolved port must
    /// hard-fail Unauthenticated.
    /// </summary>
    private sealed class ForwardingHealthService(
        IAmbientRequestScopeAccessor ambientRequestScopeAccessor,
        ForwardingProbe probe)
        : TestHealth.TestHealthBase
    {
        public override Task<HealthReply> Health(HealthRequest request, ServerCallContext context)
        {
            try
            {
                var scope = ambientRequestScopeAccessor.Current;
                var holder = scope?.GetService<IForwardedJwtAccessor>();
                if (holder?.Current is { HasValue: true } jwt)
                    probe.HolderRevealedBytes = jwt.RevealForForwarding();

                var credentials =
                    ForwardedJwtCallCredentials.FromAmbientRequestScope(ambientRequestScopeAccessor);
                var interceptor = ExtractInterceptor(credentials);
                var metadata = new Metadata();
                var authContext =
                    new AuthInterceptorContext("https://callee.internal", "/svc/Method", default);

                try
                {
                    interceptor(authContext, metadata).GetAwaiter().GetResult();
                    probe.ForwardedAuthorizationHeader = metadata.Get(GrpcHeaders.AUTHORIZATION)?.Value;
                }
                catch (RpcException rpc) when (rpc.StatusCode == GrpcStatusCode.Unauthenticated)
                {
                    probe.ForwardHardFailedUnauthenticated = true;
                }
            }
            catch (Exception ex)
            {
                probe.Error = ex.GetType().Name + ": " + ex.Message;
            }
            finally
            {
                probe.Observed = true;
            }

            return Task.FromResult(new HealthReply { Status = "ok" });
        }
    }

    private sealed class InterceptorCapturingConfigurator : CallCredentialsConfiguratorBase
    {
        public AsyncAuthInterceptor? Captured { get; private set; }

        public override void SetAsyncAuthInterceptorCredentials(
            object? state,
            AsyncAuthInterceptor interceptor)
            => Captured = interceptor;

        public override void SetCompositeCredentials(
            object? state,
            IReadOnlyList<CallCredentials> credentials)
        {
            // The forwarding credential is built from a single interceptor, never a
            // composite — no-op for this scan.
        }
    }

    /// <summary>
    /// Stub <see cref="ITieredCache"/> required by <see cref="JwtValidator"/>'s
    /// transitive dependency tree (session liveness path). These tests don't
    /// exercise revocation; the stub no-ops everything.
    /// </summary>
    private sealed class FakeTieredCacheStub : ITieredCache
    {
        public ValueTask<D2Result<bool>> ExistsAsync(
            string key, CancellationToken ct = default)
            => new(D2Result<bool>.Ok(true));

        public ValueTask<D2Result<T?>> GetAsync<T>(
            string key, CancellationToken ct = default)
            => throw new NotImplementedException();

        public ValueTask<D2Result<IReadOnlyDictionary<string, T?>>>
            GetManyAsync<T>(
                IReadOnlyCollection<string> keys, CancellationToken ct = default)
            => throw new NotImplementedException();

        public ValueTask<D2Result> SetAsync<T>(
            string key, T value, TimeSpan? ttl = null, CancellationToken ct = default)
            => throw new NotImplementedException();

        public ValueTask<D2Result> SetManyAsync<T>(
            IReadOnlyDictionary<string, T> entries,
            TimeSpan? ttl = null,
            CancellationToken ct = default)
            => throw new NotImplementedException();

        public ValueTask<D2Result> RemoveAsync(
            string key, CancellationToken ct = default)
            => throw new NotImplementedException();

        public ValueTask<D2Result> RemoveManyAsync(
            IReadOnlyCollection<string> keys, CancellationToken ct = default)
            => throw new NotImplementedException();

        public ValueTask<D2Result<TimeSpan?>> GetTtlAsync(
            string key, CancellationToken ct = default)
            => throw new NotImplementedException();

        public ValueTask<D2Result<long>> IncrementAsync(
            string key,
            long delta = 1,
            TimeSpan? ttl = null,
            CancellationToken ct = default)
            => throw new NotImplementedException();

        public ValueTask<D2Result<bool>> SetNxAsync<T>(
            string key,
            T value,
            TimeSpan? ttl = null,
            CancellationToken ct = default)
            => throw new NotImplementedException();

        public ValueTask<D2Result<bool>> AcquireLockAsync(
            string key, string token, TimeSpan ttl, CancellationToken ct = default)
            => throw new NotImplementedException();

        public ValueTask<D2Result> ReleaseLockAsync(
            string key, string token, CancellationToken ct = default)
            => throw new NotImplementedException();

        public ValueTask<D2Result> SetAndBroadcastAsync<T>(
            string key, T value, TimeSpan? ttl = null, CancellationToken ct = default)
            => throw new NotImplementedException();

        public ValueTask<D2Result> SetManyAndBroadcastAsync<T>(
            IReadOnlyDictionary<string, T> entries,
            TimeSpan? ttl = null,
            CancellationToken ct = default)
            => throw new NotImplementedException();

        public ValueTask<D2Result> RemoveAndBroadcastAsync(
            string key, CancellationToken ct = default)
            => throw new NotImplementedException();

        public ValueTask<D2Result> RemoveManyAndBroadcastAsync(
            IReadOnlyCollection<string> keys, CancellationToken ct = default)
            => throw new NotImplementedException();
    }
}
