// -----------------------------------------------------------------------
// <copyright file="ForwardedJwtCallCredentials.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Auth.Outbound.Grpc;

using D2.Shared.Auth.Abstractions;
using D2.Shared.Headers.Grpc;
using global::Grpc.Core;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// gRPC <see cref="CallCredentials"/> that replays the inbound request's
/// forwarded transaction-token on every outbound cross-process RPC. On each call
/// it resolves the request-scoped <see cref="IForwardedJwtAccessor"/> through the
/// ambient-scope <see cref="IAmbientRequestScopeAccessor"/> port, reveals the
/// held bearer bytes, and attaches them as
/// <c>Authorization: Bearer &lt;bytes&gt;</c> — it does NOT capture a token at
/// channel construction.
/// </summary>
/// <remarks>
/// <para>
/// <b>Per-channel singleton, per-request token (the load-bearing distinction).</b>
/// A <see cref="CallCredentials"/> built by
/// <see cref="GrpcClientBuilderExtensions.AddD2ForwardedJwt"/> is one object bound
/// to the channel and reused for every RPC. The forwarded token, however, is the
/// CURRENT inbound request's token — different on every concurrent request sharing
/// a long-lived channel. So the credential closes over the AMBIENT-SCOPE PORT (a
/// stateless singleton), never over a resolved holder or token; per call it
/// re-derives the current request's scope and reads that scope's holder. Because
/// the port is backed by an <c>AsyncLocal</c>-flowed accessor, two concurrent
/// requests each observe their own scope, their own holder, and their own token —
/// no cross-request bleed. (Capturing a resolved holder at channel build would
/// forward the first request's token to every subsequent request: the cross-user
/// leak this design prevents.)
/// </para>
/// <para>
/// <b>The sole reveal seam.</b> This is the single production caller of
/// <see cref="ForwardedJwt.RevealForForwarding"/> — a source-text scan pins that
/// no other production type reveals the raw bearer. The reveal result flows ONLY
/// into the metadata write below; it is never logged, never placed in an
/// exception message, never returned.
/// </para>
/// <para>
/// <b>Hard-fail on absent token.</b> No ambient request scope, no registered
/// holder, or an empty/absent <see cref="IForwardedJwtAccessor.Current"/> all
/// raise <see cref="RpcException"/> with <see cref="StatusCode.Unauthenticated"/>
/// and a fixed, token-free message — never a silent no-header send. The guard
/// runs BEFORE <see cref="ForwardedJwt.RevealForForwarding"/>, so the wrapper's
/// own empty-reveal <see cref="System.InvalidOperationException"/> is unreachable
/// from this path: the credential owns the typed <c>Unauthenticated</c> mapping.
/// A genuinely system-initiated call (a future scheduled job with no inbound
/// request) has no ambient scope and hard-fails here — the correct fail-loud
/// behavior; such callers carry their own identity when they exist.
/// </para>
/// </remarks>
public static class ForwardedJwtCallCredentials
{
    private const string _BEARER_SCHEME = "Bearer";

    /// <summary>
    /// Creates a <see cref="CallCredentials"/> that, per outbound RPC, resolves
    /// the current request's forwarded JWT through
    /// <paramref name="ambientRequestScopeAccessor"/> and attaches it as an
    /// <c>Authorization: Bearer</c> header.
    /// </summary>
    /// <param name="ambientRequestScopeAccessor">
    /// The ambient-scope port used to reach the current request's
    /// <see cref="IForwardedJwtAccessor"/> on each call. Resolved once at channel
    /// build (it is a stateless singleton); the per-request resolution happens
    /// inside the returned interceptor.
    /// </param>
    /// <returns>
    /// Credentials suitable for composing onto a <c>GrpcChannelOptions</c> via
    /// <see cref="GrpcClientBuilderExtensions.AddD2ForwardedJwt"/>.
    /// </returns>
    public static CallCredentials FromAmbientRequestScope(
        IAmbientRequestScopeAccessor ambientRequestScopeAccessor)
    {
        ArgumentNullException.ThrowIfNull(ambientRequestScopeAccessor);

        // Non-async lambda: the body is wholly synchronous (resolve + guard +
        // metadata write — no awaitable I/O), so an async lambda would trip
        // CS1998 and the AuthInterceptorContext (cancellation token, service URL)
        // is unused — there is no awaitable operation to cancel between resolving
        // the holder and writing the header. Guards throw RpcException
        // synchronously; gRPC's credential machinery surfaces it to the caller as
        // an Unauthenticated RPC. Success returns a completed task.
        return CallCredentials.FromInterceptor((_, metadata) =>
        {
            var scope = ambientRequestScopeAccessor.Current
                ?? throw new RpcException(new Status(
                    StatusCode.Unauthenticated,
                    "No ambient request scope: the outbound forwarded-JWT hop ran "
                    + "outside an inbound request."));

            var accessor = scope.GetService<IForwardedJwtAccessor>()
                ?? throw new RpcException(new Status(
                    StatusCode.Unauthenticated,
                    "Forwarded-JWT holder is not registered in the inbound request "
                    + "scope."));

            // Guard BEFORE reveal so RevealForForwarding()'s own empty-reveal
            // InvalidOperationException can never escape this path.
            if (accessor.Current is not { HasValue: true } jwt)
            {
                throw new RpcException(new Status(
                    StatusCode.Unauthenticated,
                    "No forwarded JWT to attach on this internal hop."));
            }

            metadata.Add(GrpcHeaders.AUTHORIZATION, $"{_BEARER_SCHEME} {jwt.RevealForForwarding()}");

            return Task.CompletedTask;
        });
    }
}
