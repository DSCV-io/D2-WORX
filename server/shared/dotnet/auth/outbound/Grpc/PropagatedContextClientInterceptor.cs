// -----------------------------------------------------------------------
// <copyright file="PropagatedContextClientInterceptor.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Auth.Outbound.Grpc;

using System;
using D2.Shared.Auth.Abstractions;
using D2.Shared.Context.Abstractions;
using D2.Shared.Headers.Grpc;
using global::Grpc.Core;
using global::Grpc.Core.Interceptors;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Outbound gRPC client <see cref="Interceptor"/> that writes the
/// <c>x-d2-context</c> propagation header — the operational propagation subset PLUS the
/// accumulated call-path — on every outbound RPC. Closes the documented sync-hop
/// propagation gap: the <c>PROPAGATED_CONTEXT</c> header was carried on AMQP but unused
/// on gRPC.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Per-call resolution</strong>: the interceptor resolves the CURRENT inbound
/// request's scope through the framework-free <see cref="IAmbientRequestScopeAccessor"/>
/// port (the same door the forwarded-JWT credential uses), reads that scope's
/// <see cref="IRequestContext"/>, projects it via <c>ToPropagatedContext()</c> (which
/// includes the call-path the inbound boundaries appended), and adds the encoded header.
/// One long-lived channel therefore forwards each concurrent request's own context.
/// </para>
/// <para>
/// <strong>Opportunistic, never required</strong>: when no inbound scope is on the
/// execution context (a genuinely system-initiated call) or the scope holds no
/// request-context, or the projected context has no fields, the interceptor attaches no
/// header and never throws — propagation is best-effort telemetry. A client interceptor
/// (not <see cref="CallCredentials"/>) is used so it works on plaintext / loopback
/// channels too: the call-path is a non-secret operational header.
/// </para>
/// <para>
/// <strong>Streaming coverage</strong>: all client call shapes (blocking + async unary,
/// client-streaming, server-streaming, duplex) route through the single shared
/// <c>WithPropagatedHeader</c> so a call shape cannot silently skip propagation.
/// </para>
/// </remarks>
/// <param name="ambientRequestScopeAccessor">
/// The ambient-scope port resolving the current inbound request's DI scope.
/// </param>
public sealed class PropagatedContextClientInterceptor(
    IAmbientRequestScopeAccessor ambientRequestScopeAccessor) : Interceptor
{
    private readonly IAmbientRequestScopeAccessor r_ambient =
        ambientRequestScopeAccessor
            ?? throw new ArgumentNullException(nameof(ambientRequestScopeAccessor));

    /// <inheritdoc/>
    public override TResponse BlockingUnaryCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        BlockingUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        ArgumentNullException.ThrowIfNull(continuation);

        return continuation(request, WithPropagatedHeader(context));
    }

    /// <inheritdoc/>
    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        ArgumentNullException.ThrowIfNull(continuation);

        return continuation(request, WithPropagatedHeader(context));
    }

    /// <inheritdoc/>
    public override AsyncClientStreamingCall<TRequest, TResponse>
        AsyncClientStreamingCall<TRequest, TResponse>(
            ClientInterceptorContext<TRequest, TResponse> context,
            AsyncClientStreamingCallContinuation<TRequest, TResponse> continuation)
    {
        ArgumentNullException.ThrowIfNull(continuation);

        return continuation(WithPropagatedHeader(context));
    }

    /// <inheritdoc/>
    public override AsyncServerStreamingCall<TResponse>
        AsyncServerStreamingCall<TRequest, TResponse>(
            TRequest request,
            ClientInterceptorContext<TRequest, TResponse> context,
            AsyncServerStreamingCallContinuation<TRequest, TResponse> continuation)
    {
        ArgumentNullException.ThrowIfNull(continuation);

        return continuation(request, WithPropagatedHeader(context));
    }

    /// <inheritdoc/>
    public override AsyncDuplexStreamingCall<TRequest, TResponse>
        AsyncDuplexStreamingCall<TRequest, TResponse>(
            ClientInterceptorContext<TRequest, TResponse> context,
            AsyncDuplexStreamingCallContinuation<TRequest, TResponse> continuation)
    {
        ArgumentNullException.ThrowIfNull(continuation);

        return continuation(WithPropagatedHeader(context));
    }

    private ClientInterceptorContext<TRequest, TResponse> WithPropagatedHeader<TRequest, TResponse>(
        ClientInterceptorContext<TRequest, TResponse> context)
        where TRequest : class
        where TResponse : class
    {
        var scope = r_ambient.Current;
        var requestContext = scope?.GetService<IRequestContext>();

        // No inbound scope / no request-context ⇒ nothing to propagate (no header, no throw).
        if (requestContext is null)
            return context;

        var propagated = requestContext.ToPropagatedContext();

        if (!propagated.HasAnyField)
            return context;

        var headers = context.Options.Headers ?? new Metadata();

        headers.Add(
            GrpcHeaders.PROPAGATED_CONTEXT, PropagatedContextSerializer.Encode(propagated));

        return new ClientInterceptorContext<TRequest, TResponse>(
            context.Method, context.Host, context.Options.WithHeaders(headers));
    }
}
