// -----------------------------------------------------------------------
// <copyright file="RequestOriginUnestablishedDenyInterceptor.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Auth.Grpc.Interceptors;

using DcsvIo.D2.Auth.Abstractions;
using DcsvIo.D2.Auth.Abstractions.Http;
using DcsvIo.D2.Auth.Errors;
using DcsvIo.D2.Auth.Grpc.Endpoints;
using DcsvIo.D2.Auth.Grpc.Status;
using DcsvIo.D2.Auth.Grpc.Telemetry;
using DcsvIo.D2.Context.Abstractions;
using global::Grpc.Core;
using global::Grpc.Core.Interceptors;
using Microsoft.Extensions.Logging;

/// <summary>
/// gRPC server <see cref="Interceptor"/> that fail-closed denies product calls
/// whose scoped <see cref="IRequestContext.Origin"/> is still
/// <see cref="RequestOrigin.Unestablished"/> after
/// <see cref="RequestOriginCrossProcessInterceptor"/> ran.
/// </summary>
/// <remarks>
/// <para>
/// Platform law (not a per-handler check): internal-service product gRPC must
/// run on the mTLS plane with a positively established
/// <see cref="RequestOrigin.CrossProcessHop"/>. Registration is folded into
/// <c>AddD2RequestOriginGrpc()</c> AFTER the establishment interceptor so every
/// host that wires Origin gRPC gets deny-by-default.
/// </para>
/// <para>
/// Pipeline order (inbound): <c>JwtAuthInterceptor</c> →
/// <see cref="RequestOriginCrossProcessInterceptor"/> → this deny interceptor.
/// Harmless methods skip (same metadata path as JWT auth).
/// </para>
/// <para>
/// Streaming coverage invariant: all four server-side handler methods dispatch
/// to one shared deny path.
/// </para>
/// </remarks>
internal sealed class RequestOriginUnestablishedDenyInterceptor : Interceptor
{
    private readonly ILogger<RequestOriginUnestablishedDenyInterceptor> r_logger;

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="RequestOriginUnestablishedDenyInterceptor"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public RequestOriginUnestablishedDenyInterceptor(
        ILogger<RequestOriginUnestablishedDenyInterceptor> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);

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

        DenyIfUnestablished(context);
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

        DenyIfUnestablished(context);
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

        DenyIfUnestablished(context);
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

        DenyIfUnestablished(context);
        await continuation(requestStream, responseStream, context).ConfigureAwait(false);
    }

    private static IRequestContext? ResolveRequestContext(ServerCallContext context)
    {
        // Prefer the dual-written Items / UserState slots (same instance Jwt + Origin mutate).
        var fromUserState = context.GetD2RequestContext();

        if (fromUserState is not null)
            return fromUserState;

        var httpContext = MethodScopeMetadataResolver.TryResolveHttpContext(context);

        if (httpContext?.Items[D2HttpContextItems.REQUEST_CONTEXT] is IRequestContext fromItems)
            return fromItems;

        return null;
    }

    /// <summary>
    /// Shared deny path for all four RPC kind dispatch overrides.
    /// </summary>
    /// <param name="context">The gRPC server call context.</param>
    private void DenyIfUnestablished(ServerCallContext context)
    {
        var metadata = MethodScopeMetadataResolver.TryResolve(context);

        // Harmless-endpoint opt-in: skip origin deny (parity with JwtAuthInterceptor).
        if (metadata is { IsHarmlessEndpoint: true })
            return;

        var requestContext = ResolveRequestContext(context);

        // Missing context on a non-harmless product path is also fail-closed —
        // establishment never ran or JWT never populated identity.
        if (requestContext is null
            || requestContext.Origin == RequestOrigin.Unestablished)
        {
            r_logger.RequestOriginUnestablishedDenied();
            throw AuthFailures.RequestOriginUnestablished().ToRpcException();
        }
    }
}
