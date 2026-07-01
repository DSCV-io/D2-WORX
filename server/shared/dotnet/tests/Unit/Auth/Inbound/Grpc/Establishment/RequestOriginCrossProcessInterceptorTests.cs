// -----------------------------------------------------------------------
// <copyright file="RequestOriginCrossProcessInterceptorTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Auth.Inbound.Grpc.Establishment;

using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using AwesomeAssertions;
using D2.Shared.Auth.Abstractions;
using D2.Shared.Auth.Abstractions.Http;
using D2.Shared.Auth.Grpc.Interceptors;
using D2.Shared.Context.Abstractions;
using D2.Shared.Headers.Grpc;
using D2.Shared.Tests.Unit.Auth.Inbound.Grpc.Fixtures;
using D2.Shared.Tests.Unit.Mtls;
using D2.Shared.Time;
using global::Grpc.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NodaTime;
using Xunit;

/// <summary>
/// Unit matrix for the cross-process establishment interceptor — the
/// anti-spoofing boundary. Proven over a real KC-issued leaf placed on
/// <c>HttpContext.Connection.ClientCertificate</c> (no socket, the codebase's
/// established mTLS-without-handshake pattern): a valid leaf surfaces
/// <see cref="RequestOrigin.CrossProcessHop"/> + the cert-derived caller; no cert ⇒ null
/// caller (fail-closed); the inbound <c>x-d2-context</c> (operational subset + inherited
/// call-path) is applied and THIS hop is appended; and a context with no established
/// identity is a no-op.
/// </summary>
[Trait("Category", "Unit")]
public sealed class RequestOriginCrossProcessInterceptorTests
{
    private const string _SELF_ID = "key-custodian";
    private static readonly Instant sr_now = Instant.FromUtc(2026, 6, 30, 12, 0, 0);

    // ---- Constructor null guards ----

    [Fact]
    public void Constructor_NullWorkloadIdentity_Throws()
    {
        var act = () => new RequestOriginCrossProcessInterceptor(
            null!,
            new TestClock(sr_now),
            NullLogger<RequestOriginCrossProcessInterceptor>.Instance);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullClock_Throws()
    {
        var act = () => new RequestOriginCrossProcessInterceptor(
            Options.Create(new D2WorkloadIdentityOptions { ServiceId = _SELF_ID }),
            null!,
            NullLogger<RequestOriginCrossProcessInterceptor>.Instance);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullLogger_Throws()
    {
        var act = () => new RequestOriginCrossProcessInterceptor(
            Options.Create(new D2WorkloadIdentityOptions { ServiceId = _SELF_ID }),
            new TestClock(sr_now),
            null!);

        act.Should().Throw<ArgumentNullException>();
    }

    // ---- Call-shape / establishment matrix ----

    [Fact]
    public async Task Establish_ValidPeerCert_SetsCrossProcessOriginAndCertDerivedCaller()
    {
        using var ca = new TestCertificateAuthority();
        using var leaf = ca.IssueLeaf("edge");
        var ctx = new MutableRequestContext { IsAuthenticated = true };
        var scc = BuildContext(ctx, clientCertificate: leaf);

        await InvokeAsync(scc);

        ctx.Origin.Should().Be(RequestOrigin.CrossProcessHop);
        ctx.ImmediateCaller.Should().Be(
            "edge", "the caller is the validated mTLS peer cert SPIFFE id, never a header");
    }

    [Fact]
    public async Task Establish_NoPeerCert_LeavesCallerNull_FailClosed()
    {
        var ctx = new MutableRequestContext { IsAuthenticated = true };
        var scc = BuildContext(ctx, clientCertificate: null);

        await InvokeAsync(scc);

        ctx.Origin.Should().Be(RequestOrigin.CrossProcessHop);
        ctx.ImmediateCaller.Should().BeNull(
            "a call with no validated peer certificate yields a null caller — fail-closed");
    }

    [Fact]
    public async Task Establish_InboundContext_AppliesOperationalSubsetAndAppendsThisHop()
    {
        // The inbound A hop's x-d2-context carries an operational field (RequestId) and a
        // one-entry call-path. The interceptor must apply both, then append ITSELF.
        var inbound = new MutableRequestContext
        {
            RequestId = "req-from-A",
            CallPath = [new CallPathEntry("service-a", CallPathKind.Edge, sr_now.ToDateTimeOffset())],
        };
        var encoded = PropagatedContextSerializer.Encode(inbound.ToPropagatedContext());
        var ctx = new MutableRequestContext { IsAuthenticated = true };
        var scc = BuildContext(ctx, clientCertificate: null, propagatedHeader: encoded);

        await InvokeAsync(scc);

        ctx.RequestId.Should().Be("req-from-A", "the operational propagation subset is inherited");
        ctx.CallPath.Should().HaveCount(2);
        ctx.CallPath[0].Id.Should().Be("service-a");
        ctx.CallPath[1].Id.Should().Be(_SELF_ID);
        ctx.CallPath[1].Kind.Should().Be(CallPathKind.WorkloadHop);
    }

    [Fact]
    public async Task Establish_NoInboundHeader_StartsCallPathWithThisHop()
    {
        var ctx = new MutableRequestContext { IsAuthenticated = true };
        var scc = BuildContext(ctx, clientCertificate: null);

        await InvokeAsync(scc);

        ctx.CallPath.Should().ContainSingle();
        ctx.CallPath[0].Id.Should().Be(_SELF_ID);
        ctx.CallPath[0].Kind.Should().Be(CallPathKind.WorkloadHop);
    }

    [Fact]
    public async Task Establish_MalformedInboundHeader_IsIgnoredButHopStillEstablished()
    {
        // A forged / oversize / garbage header decodes to null and is a no-op — propagation
        // is opportunistic — but the origin + this hop are still established fresh.
        var ctx = new MutableRequestContext { IsAuthenticated = true };
        var scc = BuildContext(ctx, clientCertificate: null, propagatedHeader: "!!!not-base64url!!!");

        await InvokeAsync(scc);

        ctx.Origin.Should().Be(RequestOrigin.CrossProcessHop);
        ctx.CallPath.Should().ContainSingle();
        ctx.CallPath[0].Id.Should().Be(_SELF_ID);
    }

    [Fact]
    public async Task Establish_NoEstablishedIdentity_IsNoOp()
    {
        // No MutableRequestContext on HttpContext.Items (e.g. a harmless endpoint the auth
        // interceptor short-circuited) ⇒ nothing to enrich; the call proceeds.
        var http = new DefaultHttpContext();
        var scc = new TestServerCallContext(httpContext: http);

        var response = await InvokeAsync(scc);

        response.Should().Be("resp", "the continuation still runs when there is nothing to enrich");
    }

    [Fact]
    public async Task ClientStreamingServerHandler_ValidPeerCert_RoutesThroughSharedEstablishPath()
    {
        using var ca = new TestCertificateAuthority();
        using var leaf = ca.IssueLeaf("edge");
        var ctx = new MutableRequestContext { IsAuthenticated = true };
        var scc = BuildContext(ctx, clientCertificate: leaf);
        var interceptor = MakeInterceptor();

        await interceptor.ClientStreamingServerHandler<string, string>(
            new EmptyAsyncStreamReader<string>(),
            scc,
            (_, _) => Task.FromResult("resp"));

        ctx.Origin.Should().Be(RequestOrigin.CrossProcessHop);
        ctx.ImmediateCaller.Should().Be(
            "edge", "ClientStreamingServerHandler must route through the same Establish "
                + "path as UnaryServerHandler");
        ctx.CallPath.Should().ContainSingle();
        ctx.CallPath[0].Id.Should().Be(_SELF_ID);
    }

    [Fact]
    public async Task ServerStreamingServerHandler_ValidPeerCert_RoutesThroughSharedEstablishPath()
    {
        using var ca = new TestCertificateAuthority();
        using var leaf = ca.IssueLeaf("edge");
        var ctx = new MutableRequestContext { IsAuthenticated = true };
        var scc = BuildContext(ctx, clientCertificate: leaf);
        var interceptor = MakeInterceptor();

        await interceptor.ServerStreamingServerHandler<string, string>(
            "req",
            new DiscardingServerStreamWriter<string>(),
            scc,
            (_, _, _) => Task.CompletedTask);

        ctx.Origin.Should().Be(RequestOrigin.CrossProcessHop);
        ctx.ImmediateCaller.Should().Be(
            "edge", "ServerStreamingServerHandler must route through the same Establish "
                + "path as UnaryServerHandler");
        ctx.CallPath.Should().ContainSingle();
        ctx.CallPath[0].Id.Should().Be(_SELF_ID);
    }

    [Fact]
    public async Task DuplexStreamingServerHandler_ValidPeerCert_RoutesThroughSharedEstablishPath()
    {
        using var ca = new TestCertificateAuthority();
        using var leaf = ca.IssueLeaf("edge");
        var ctx = new MutableRequestContext { IsAuthenticated = true };
        var scc = BuildContext(ctx, clientCertificate: leaf);
        var interceptor = MakeInterceptor();

        await interceptor.DuplexStreamingServerHandler(
            new EmptyAsyncStreamReader<string>(),
            new DiscardingServerStreamWriter<string>(),
            scc,
            (_, _, _) => Task.CompletedTask);

        ctx.Origin.Should().Be(RequestOrigin.CrossProcessHop);
        ctx.ImmediateCaller.Should().Be(
            "edge", "DuplexStreamingServerHandler must route through the same Establish "
                + "path as UnaryServerHandler");
        ctx.CallPath.Should().ContainSingle();
        ctx.CallPath[0].Id.Should().Be(_SELF_ID);
    }

    private static RequestOriginCrossProcessInterceptor MakeInterceptor() =>
        new(
            Options.Create(new D2WorkloadIdentityOptions { ServiceId = _SELF_ID }),
            new TestClock(sr_now),
            NullLogger<RequestOriginCrossProcessInterceptor>.Instance);

    private static TestServerCallContext BuildContext(
        MutableRequestContext ctx,
        X509Certificate2? clientCertificate,
        string? propagatedHeader = null)
    {
        var http = new DefaultHttpContext();
        http.Items[D2HttpContextItems.REQUEST_CONTEXT] = ctx;

        if (clientCertificate is not null)
            http.Connection.ClientCertificate = clientCertificate;

        var headers = new Metadata();
        if (propagatedHeader is not null)
            headers.Add(GrpcHeaders.PROPAGATED_CONTEXT, propagatedHeader);

        return new TestServerCallContext(requestHeaders: headers, httpContext: http);
    }

    private static async Task<string> InvokeAsync(ServerCallContext context)
    {
        var interceptor = new RequestOriginCrossProcessInterceptor(
            Options.Create(new D2WorkloadIdentityOptions { ServiceId = _SELF_ID }),
            new TestClock(sr_now),
            NullLogger<RequestOriginCrossProcessInterceptor>.Instance);

        return await interceptor.UnaryServerHandler<string, string>(
            "req",
            context,
            (_, _) => Task.FromResult("resp"));
    }

    /// <summary>Empty <see cref="IAsyncStreamReader{T}"/> stand-in.</summary>
    private sealed class EmptyAsyncStreamReader<T> : IAsyncStreamReader<T>
        where T : class
    {
        public T Current => null!;

        public Task<bool> MoveNext(CancellationToken cancellationToken) =>
            Task.FromResult(false);
    }

    /// <summary>Discarding <see cref="IServerStreamWriter{T}"/> stand-in.</summary>
    private sealed class DiscardingServerStreamWriter<T> : IServerStreamWriter<T>
    {
        public WriteOptions? WriteOptions { get; set; }

        public Task WriteAsync(T message) => Task.CompletedTask;
    }
}
