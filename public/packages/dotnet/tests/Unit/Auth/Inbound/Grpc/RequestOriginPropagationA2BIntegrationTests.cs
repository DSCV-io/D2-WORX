// -----------------------------------------------------------------------
// <copyright file="RequestOriginPropagationA2BIntegrationTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Auth.Inbound.Grpc;

using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using AwesomeAssertions;
using DcsvIo.D2.Auth;
using DcsvIo.D2.Auth.Abstractions;
using DcsvIo.D2.Auth.Abstractions.Sessions;
using DcsvIo.D2.Auth.Errors;
using DcsvIo.D2.Auth.Grpc;
using DcsvIo.D2.Auth.Grpc.Endpoints;
using DcsvIo.D2.Auth.Grpc.Interceptors;
using DcsvIo.D2.Auth.Grpc.Status;
using DcsvIo.D2.Auth.Outbound.Grpc;
using DcsvIo.D2.Auth.Validation;
using DcsvIo.D2.Context.Abstractions;
using DcsvIo.D2.Tests.Unit.Auth.Inbound.Grpc.Fixtures;
using DcsvIo.D2.Tests.Unit.Auth.Inbound.Grpc.Protos;
using DcsvIo.D2.Tests.Unit.Mtls;
using global::Grpc.Core;
using global::Grpc.Core.Interceptors;
using global::Grpc.Net.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NodaTime;
using Xunit;

/// <summary>
/// The headline two-endpoint in-memory gRPC <c>TestServer</c> propagation proof (A → B):
/// A's outbound client interceptor writes the <c>x-d2-context</c> header (carrying A's
/// operational subset + call-path); B's cross-process server interceptor reads it,
/// applies the subset + inherited call-path, recomputes Origin FRESH (ignoring any wire
/// value), and appends B's own hop. Proves outbound-write → inbound-read → append
/// end-to-end with no socket / real mTLS.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "TestHost lifetime is bounded by individual tests, not the class.")]
[Trait("Category", "Unit")]
public sealed class RequestOriginPropagationA2BIntegrationTests
{
    private const string _ISSUER = "https://edge.internal";
    private const string _AUDIENCE = "files";
    private const string _SCOPE = "test.scope";
    private const string _B_SERVICE_ID = "key-custodian";

    private static readonly Instant sr_t0 = Instant.FromUtc(2026, 6, 30, 12, 0, 0);

    [Fact]
    public async Task OutboundWrite_InboundRead_AppendsCallPath_AndRecomputesOriginAtB()
    {
        var probe = new EstablishmentProbe();
        using var jwt = new TestJwtBuilder();
        using var ca = new TestCertificateAuthority();
        using var peerLeaf = ca.IssueLeaf("edge");
        using var host = await BuildBHostAsync(jwt, probe, peerLeaf);

        // A's request-scoped context: an operational field + a one-entry Edge call-path.
        var aContext = new MutableRequestContext
        {
            RequestId = "req-from-A",
            CallPath = [new CallPathEntry("service-a", CallPathKind.Edge, sr_t0.ToDateTimeOffset())],
        };
        using var aScope = new ServiceCollection()
            .AddSingleton<IRequestContext>(aContext)
            .BuildServiceProvider();
        var ambient = new StubAmbientScope(aScope);

        using var channel = CreateChannel(host);
        var invoker = channel.Intercept(new PropagatedContextClientInterceptor(ambient));
        var client = new TestEcho.TestEchoClient(invoker);
        var token = jwt.MintToken(
            _ISSUER,
            _AUDIENCE,
            extraClaims: new Dictionary<string, object> { ["scope"] = _SCOPE });
        var headers = new Metadata { { "authorization", "Bearer " + token } };

        var reply = await client.EchoAsync(new EchoRequest { Payload = "a2b" }, headers);

        reply.Echoed.Should().Be("a2b");
        probe.Observed.Should().BeTrue("B's service method must have run");

        // Origin is recomputed FRESH at B from the transport — never taken from the wire
        // (it is not a propagated field). B derives Origin + ImmediateCaller ATOMICALLY
        // from the mTLS peer cert (seeded on Connection.ClientCertificate in this harness
        // so platform Unestablished deny does not fire — real mTLS is unit-tested).
        probe.Origin.Should().Be(RequestOrigin.CrossProcessHop);
        probe.ImmediateCaller.Should().Be("edge");

        // The operational propagation subset crossed A → B.
        probe.RequestId.Should().Be("req-from-A");

        // The call-path shows A's hop THEN B's appended hop, oldest-first.
        probe.CallPathIds.Should().Equal("service-a", _B_SERVICE_ID);
        probe.CallPathKinds.Should().Equal(CallPathKind.Edge, CallPathKind.WorkloadHop);
    }

    [Fact]
    public async Task NoPeerCert_PlatformDeny_EmitsAuthRequestOriginUnestablished()
    {
        // Without a peer cert, establish leaves Origin Unestablished and the
        // platform deny interceptor (folded into AddD2RequestOriginGrpc) fails closed
        // before the product handler runs.
        var probe = new EstablishmentProbe();
        using var jwt = new TestJwtBuilder();
        using var host = await BuildBHostAsync(jwt, probe, peerLeaf: null);

        var aContext = new MutableRequestContext
        {
            RequestId = "req-from-A",
            CallPath = [new CallPathEntry("service-a", CallPathKind.Edge, sr_t0.ToDateTimeOffset())],
        };
        using var aScope = new ServiceCollection()
            .AddSingleton<IRequestContext>(aContext)
            .BuildServiceProvider();
        var ambient = new StubAmbientScope(aScope);

        using var channel = CreateChannel(host);
        var invoker = channel.Intercept(new PropagatedContextClientInterceptor(ambient));
        var client = new TestEcho.TestEchoClient(invoker);
        var token = jwt.MintToken(
            _ISSUER,
            _AUDIENCE,
            extraClaims: new Dictionary<string, object> { ["scope"] = _SCOPE });
        var headers = new Metadata { { "authorization", "Bearer " + token } };

        var act = async () => await client.EchoAsync(new EchoRequest { Payload = "a2b" }, headers);

        var ex = await act.Should().ThrowAsync<RpcException>();
        ex.Which.StatusCode.Should().Be(StatusCode.Unauthenticated);
        ex.Which.Trailers.GetValue(D2GrpcTrailers.ERROR_CODE)
            .Should().Be(AuthErrorCodes.AUTH_REQUEST_ORIGIN_UNESTABLISHED);
        probe.Observed.Should().BeFalse("product handler must not run when Origin is Unestablished");
    }

    private static async Task<IHost> BuildBHostAsync(
        TestJwtBuilder jwtBuilder,
        EstablishmentProbe probe,
        X509Certificate2? peerLeaf)
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
                        services.AddSingleton(probe);
                        services.AddD2Auth(opts =>
                        {
                            opts.Issuer = new Uri(_ISSUER);
                            opts.Audience = _AUDIENCE;
                        });

                        // In-memory JWKS + an explicit validator (mirrors GrpcAuthIntegrationTests).
                        services.RemoveAll<DcsvIo.D2.Auth.Abstractions.Jwks.IJwksProvider>();
                        services.RemoveAll<DcsvIo.D2.Auth.Jwks.HttpJwksProvider>();
                        services.AddSingleton<DcsvIo.D2.Auth.Abstractions.Jwks.IJwksProvider>(
                            new FakeJwksProvider(jwtBuilder.PublicKey));
                        services.RemoveAll<JwtValidator>();
                        services.AddSingleton(sp => new JwtValidator(
                            sp.GetRequiredService<DcsvIo.D2.Auth.Abstractions.Jwks.IJwksProvider>(),
                            sp.GetRequiredService<IOptions<AuthOptions>>(),
                            sp.GetRequiredService<ClaimsToContextMapper>(),
                            Microsoft.Extensions.Logging.Abstractions
                                .NullLogger<JwtValidator>.Instance));

                        // Fake liveness tracker (always alive) ⇒ no ITieredCache dependency.
                        services.RemoveAll<ISessionLivenessTracker>();
                        services.AddSingleton<ISessionLivenessTracker>(new FakeSessionLivenessTracker());

                        services.AddGrpc();
                        services.AddD2AuthGrpc();
                        services.AddD2RequestOriginGrpc(o => o.ServiceId = _B_SERVICE_ID);
                    })
                    .Configure(app =>
                    {
                        // Seed Connection.ClientCertificate when the harness supplies a leaf
                        // (mTLS-without-handshake pattern used across the codebase).
                        if (peerLeaf is not null)
                        {
                            app.Use(async (httpContext, next) =>
                            {
                                httpContext.Connection.ClientCertificate = peerLeaf;
                                await next().ConfigureAwait(false);
                            });
                        }

                        app.UseRouting();
                        app.UseEndpoints(endpoints =>
                        {
                            endpoints.MapGrpcService<RecordingEchoService>()
                                .RequireAnyScope(_SCOPE);
                        });
                    });
            });

        return await hostBuilder.StartAsync();
    }

    private static GrpcChannel CreateChannel(IHost host)
    {
        var testServer = host.GetTestServer();
        var httpClient = testServer.CreateClient();
        httpClient.BaseAddress = new Uri("http://localhost");

        // DisposeHttpClient = true ties the TestServer-issued HttpClient's lifetime to
        // the channel's — the channel is the only handle the caller disposes
        // (`using var channel = CreateChannel(host);`), so without this the HttpClient
        // leaks.
        return GrpcChannel.ForAddress(
            httpClient.BaseAddress,
            new GrpcChannelOptions { HttpClient = httpClient, DisposeHttpClient = true });
    }

    /// <summary>Per-call sink B's service writes its established-context observations into.</summary>
    private sealed class EstablishmentProbe
    {
        public bool Observed { get; set; }

        public RequestOrigin Origin { get; set; }

        public string? ImmediateCaller { get; set; }

        public string? RequestId { get; set; }

        public IReadOnlyList<string> CallPathIds { get; set; } = [];

        public IReadOnlyList<CallPathKind> CallPathKinds { get; set; } = [];
    }

    /// <summary>B's gRPC service. Records the established request-context the interceptors produced.</summary>
    private sealed class RecordingEchoService(EstablishmentProbe probe) : TestEcho.TestEchoBase
    {
        public override Task<EchoReply> Echo(EchoRequest request, ServerCallContext context)
        {
            var ctx = context.GetD2RequestContext();

            if (ctx is not null)
            {
                probe.Origin = ctx.Origin;
                probe.ImmediateCaller = ctx.ImmediateCaller;
                probe.RequestId = ctx.RequestId;
                probe.CallPathIds = ctx.CallPath.Select(e => e.Id).ToList();
                probe.CallPathKinds = ctx.CallPath.Select(e => e.Kind).ToList();
            }

            probe.Observed = true;

            return Task.FromResult(new EchoReply { Echoed = request.Payload });
        }
    }

    /// <summary>Ambient-scope stub returning a fixed provider (A's request scope).</summary>
    private sealed class StubAmbientScope(IServiceProvider? current) : IAmbientRequestScopeAccessor
    {
        public IServiceProvider? Current => current;
    }
}
