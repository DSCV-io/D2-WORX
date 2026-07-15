// -----------------------------------------------------------------------
// <copyright file="RequestOriginCrossProcessInterceptorTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Auth.Inbound.Grpc.Establishment;

using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using AwesomeAssertions;
using DcsvIo.D2.Auth.Abstractions;
using DcsvIo.D2.Auth.Abstractions.Http;
using DcsvIo.D2.Auth.Grpc.Interceptors;
using DcsvIo.D2.Context.Abstractions;
using DcsvIo.D2.Headers.Grpc;
using DcsvIo.D2.Tests.Unit.Auth.Inbound.Grpc.Fixtures;
using DcsvIo.D2.Tests.Unit.Handler;
using DcsvIo.D2.Tests.Unit.Mtls;
using DcsvIo.D2.Time;
using global::Grpc.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
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
    public async Task Establish_NoPeerCert_LeavesOriginUnestablishedAndCallerNull_FailClosed()
    {
        var ctx = new MutableRequestContext { IsAuthenticated = true };
        var scc = BuildContext(ctx, clientCertificate: null);

        await InvokeAsync(scc);

        // Both authority-grade facts are derived ATOMICALLY from the one peer identity: no
        // validated peer cert ⇒ NEITHER is set, so the origin stays the fail-closed
        // Unestablished default (never the contradictory CrossProcessHop-with-null-caller
        // state) and every downstream authority rule denies at its first arm.
        ctx.Origin.Should().Be(
            RequestOrigin.Unestablished,
            "a call with no validated peer certificate does NOT establish the cross-process "
            + "origin — the origin + caller are derived together from the single peer fact");
        ctx.ImmediateCaller.Should().BeNull(
            "a call with no validated peer certificate yields a null caller — fail-closed");
    }

    [Fact]
    public async Task Establish_NoPeerCert_StillAppendsCallPathAndLogsPeerAbsentWarning()
    {
        // Telemetry (the call-path hop append) is structurally excluded from authority and
        // runs unconditionally even when the origin stays unestablished; and a misconfigured
        // non-mTLS hop is observable (a Warning), never silent.
        var logger = new TestLogger<RequestOriginCrossProcessInterceptor>();
        var interceptor = new RequestOriginCrossProcessInterceptor(
            Options.Create(new D2WorkloadIdentityOptions { ServiceId = _SELF_ID }),
            new TestClock(sr_now),
            logger);
        var ctx = new MutableRequestContext { IsAuthenticated = true };
        var scc = BuildContext(ctx, clientCertificate: null);

        await interceptor.UnaryServerHandler<string, string>(
            "req", scc, (_, _) => Task.FromResult("resp"));

        ctx.Origin.Should().Be(RequestOrigin.Unestablished);
        ctx.CallPath.Should().ContainSingle("the call-path append is unconditional telemetry");
        ctx.CallPath[0].Id.Should().Be(_SELF_ID);
        logger.Entries.Should().Contain(
            e => e.EventId.Id == 4104 && e.Level == LogLevel.Warning,
            "an absent peer identity on a cross-process hop fires the observability warning");
    }

    [Fact]
    public async Task Establish_InboundContext_AppliesOperationalSubsetAndAppendsThisHop()
    {
        // The inbound A hop's x-d2-context carries an operational field (RequestId) and a
        // one-entry call-path. The interceptor must apply both, then append ITSELF.
        var inbound = new MutableRequestContext
        {
            RequestId = "req-from-A",
            CallPath =
            [
                new CallPathEntry("service-a", CallPathKind.Edge, sr_now.ToDateTimeOffset()),
            ],
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
        // is opportunistic — but the origin + this hop are still established fresh from the
        // validated peer certificate (which is what establishes the origin now).
        using var ca = new TestCertificateAuthority();
        using var leaf = ca.IssueLeaf("edge");
        var ctx = new MutableRequestContext { IsAuthenticated = true };
        var scc = BuildContext(
            ctx, clientCertificate: leaf, propagatedHeader: "!!!not-base64url!!!");

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
