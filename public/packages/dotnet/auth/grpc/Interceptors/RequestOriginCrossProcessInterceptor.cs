// -----------------------------------------------------------------------
// <copyright file="RequestOriginCrossProcessInterceptor.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Auth.Grpc.Interceptors;

using DcsvIo.D2.AspNetCore.Mtls;
using DcsvIo.D2.Auth.Abstractions;
using DcsvIo.D2.Auth.Abstractions.Http;
using DcsvIo.D2.Auth.Grpc.Endpoints;
using DcsvIo.D2.Auth.Grpc.Telemetry;
using DcsvIo.D2.Context.Abstractions;
using DcsvIo.D2.Headers.Grpc;
using DcsvIo.D2.Time;
using global::Grpc.Core;
using global::Grpc.Core.Interceptors;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// gRPC server <see cref="Interceptor"/> that establishes the
/// <see cref="RequestOrigin.CrossProcessHop"/> plane on the scoped
/// <see cref="IRequestContext"/> for an inbound cross-process gRPC call. Sibling to
/// <see cref="JwtAuthInterceptor"/>; registered AFTER it (per the per-layer
/// security-concern ordering) so the validated identity is already on
/// <c>HttpContext.Items</c> before this interceptor enriches it.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Establishment, per inbound call</strong> (on the SAME
/// <see cref="MutableRequestContext"/> the <see cref="JwtAuthInterceptor"/> populated):
/// </para>
/// <list type="number">
///   <item>Apply the inbound <c>x-d2-context</c> (the operational propagation subset
///     PLUS the inherited call-path) via <c>ApplyPropagatedContext</c> — the gRPC half
///     of the cross-hop propagation rail.</item>
///   <item>Derive <see cref="IRequestContext.Origin"/> and
///     <see cref="IRequestContext.ImmediateCaller"/> ATOMICALLY from the single
///     unforgeable local fact — the validated mutual-TLS peer certificate
///     (<c>GetD2PeerWorkloadIdentity()</c> — reads <c>Connection.ClientCertificate</c>,
///     never a header/claim/payload). A wire-supplied Origin or caller is structurally
///     IGNORED: both are non-propagated and recomputed FRESH here every hop. Only when a
///     peer identity is present are BOTH set together (<c>Origin</c> =
///     <see cref="RequestOrigin.CrossProcessHop"/>, <c>ImmediateCaller</c> = the peer id);
///     when absent, BOTH are left unset so <c>Origin</c> stays the fail-closed
///     <see cref="RequestOrigin.Unestablished"/> — every downstream authority rule then
///     denies at its first arm. The contradictory "cross-process plane with no
///     authenticated caller" state is never produced.</item>
///   <item>Append THIS hop's OWN identity (the configured workload service id) to the
///     call-path as a <see cref="CallPathKind.WorkloadHop"/> entry.</item>
///   <item>Log the received call-path's entry count (every hop logs the field on receipt).</item>
/// </list>
/// <para>
/// <strong>Streaming coverage invariant</strong>: all four server-side handler methods
/// dispatch to the single shared <c>Establish</c> path, so a streaming method added
/// later cannot silently bypass establishment.
/// </para>
/// <para>
/// <strong>No-op safety</strong>: when no <see cref="MutableRequestContext"/> is on the
/// per-call <c>HttpContext.Items</c> (e.g. a harmless endpoint the auth interceptor
/// short-circuited, or a hand-rolled test context with no established identity), the
/// interceptor enriches nothing and proceeds.
/// </para>
/// <para>
/// <strong>Thread-safety</strong>: registered as a singleton — stateless; all injected
/// deps are singletons. Per-call mutable state lives on the resolved request context.
/// </para>
/// </remarks>
internal sealed class RequestOriginCrossProcessInterceptor : Interceptor
{
    private readonly string r_selfServiceId;
    private readonly IClock r_clock;
    private readonly ILogger<RequestOriginCrossProcessInterceptor> r_logger;

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="RequestOriginCrossProcessInterceptor"/> class.
    /// </summary>
    /// <param name="workloadIdentity">The host's own workload identity (its service id).</param>
    /// <param name="clock">The clock used to timestamp the appended hop.</param>
    /// <param name="logger">The logger.</param>
    public RequestOriginCrossProcessInterceptor(
        IOptions<D2WorkloadIdentityOptions> workloadIdentity,
        IClock clock,
        ILogger<RequestOriginCrossProcessInterceptor> logger)
    {
        ArgumentNullException.ThrowIfNull(workloadIdentity);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);

        r_selfServiceId = workloadIdentity.Value.ServiceId;
        r_clock = clock;
        r_logger = logger;
    }

    /// <inheritdoc/>
    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(continuation);

        Establish(context);
        return await continuation(request, context).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public override async Task<TResponse> ClientStreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream,
        ServerCallContext context,
        ClientStreamingServerMethod<TRequest, TResponse> continuation)
    {
        ArgumentNullException.ThrowIfNull(requestStream);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(continuation);

        Establish(context);
        return await continuation(requestStream, context).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public override async Task ServerStreamingServerHandler<TRequest, TResponse>(
        TRequest request,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        ServerStreamingServerMethod<TRequest, TResponse> continuation)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(responseStream);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(continuation);

        Establish(context);
        await continuation(request, responseStream, context).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public override async Task DuplexStreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        DuplexStreamingServerMethod<TRequest, TResponse> continuation)
    {
        ArgumentNullException.ThrowIfNull(requestStream);
        ArgumentNullException.ThrowIfNull(responseStream);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(continuation);

        Establish(context);
        await continuation(requestStream, responseStream, context).ConfigureAwait(false);
    }

    private static string? ReadPropagatedHeader(ServerCallContext context)
    {
        foreach (var entry in context.RequestHeaders)
        {
            if (string.Equals(
                    entry.Key, GrpcHeaders.PROPAGATED_CONTEXT, StringComparison.OrdinalIgnoreCase)
                && !entry.IsBinary)
                return entry.Value;
        }

        return null;
    }

    /// <summary>
    /// Single shared establishment path used by all four RPC kind dispatch overrides.
    /// No-op when no <see cref="MutableRequestContext"/> is present on the resolved
    /// <see cref="HttpContext.Items"/> slot.
    /// </summary>
    /// <param name="context">The gRPC server call context.</param>
    private void Establish(ServerCallContext context)
    {
        var httpContext = MethodScopeMetadataResolver.TryResolveHttpContext(context);

        if (httpContext?.Items[D2HttpContextItems.REQUEST_CONTEXT] is MutableRequestContext ctx)
        {
            var propagated = PropagatedContextSerializer.TryDecode(ReadPropagatedHeader(context));
            ctx.ApplyPropagatedContext(propagated);

            // Derive the two authority-grade facts ATOMICALLY from the ONE unforgeable
            // local fact (the validated mTLS peer identity). Both are set together only
            // when a peer identity is present; when absent, BOTH stay unset so Origin
            // remains the fail-closed RequestOrigin.Unestablished (every downstream
            // authority rule denies at its first arm — §9.41/§9.42). A "CrossProcessHop +
            // null caller" state — an authority-bearing plane with no authenticated peer —
            // is never produced.
            var peer = httpContext.GetD2PeerWorkloadIdentity();

            if (peer is not null)
            {
                ctx.Origin = RequestOrigin.CrossProcessHop;
                ctx.ImmediateCaller = peer;
            }
            else
            {
                r_logger.CrossProcessPeerIdentityAbsent(r_selfServiceId);
            }

            // Telemetry-only (§9.43): the call-path hop append + received-log are
            // structurally excluded from authority and run unconditionally every hop,
            // independent of whether the peer identity established the origin.
            var timestamp = r_clock.GetCurrentInstant().ToDateTimeOffset();

            ctx.CallPath = CallPathOps.Append(
                ctx.CallPath, r_selfServiceId, CallPathKind.WorkloadHop, timestamp);

            r_logger.CallPathReceived(ctx.CallPath.Count, r_selfServiceId);
        }
    }
}
