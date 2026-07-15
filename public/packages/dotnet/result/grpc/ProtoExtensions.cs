// -----------------------------------------------------------------------
// <copyright file="ProtoExtensions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Result.Grpc;

using System.Net;
using DcsvIo.D2.ErrorCodes.Category;
using DcsvIo.D2.I18n;
using DcsvIo.D2.Utilities.Diagnostics;
using DcsvIo.D2.Utilities.Extensions;
using global::D2.Services.Protos.Common.V1;
using global::Grpc.Core;
using Microsoft.Extensions.Logging;

/// <summary>
/// Extension methods for the faithful in-memory → wire → in-memory
/// <see cref="D2Result"/> round-trip over a gRPC <see cref="D2ResultProto"/>
/// response-envelope. Mirrors v1's <c>ProtoExtensions</c> shape with v2
/// additions: typed <see cref="ErrorCategory"/>, full <see cref="TKMessage"/>
/// fidelity (key + params) via <see cref="TKMessageProto"/>, and PII-safe
/// exception logging that never touches <see cref="Exception.Message"/>.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Usage pattern</strong>: every service response message embeds a
/// <see cref="D2ResultProto"/> as field 1 alongside typed payload fields.
/// The server handler calls <c>result.ToProto()</c>; the consumer calls
/// <c>await client.Method(req).HandleAsync(r =&gt; r.Result, r =&gt; r.Data)</c>.
/// Business failures return a normal gRPC response (<c>StatusCode.OK</c>);
/// the failure detail rides inside <see cref="D2ResultProto"/>, not in
/// gRPC trailers/status-code. Transport faults (<see cref="RpcException"/>)
/// are fail-opened to pre-built factory results.
/// </para>
/// <para>
/// <strong>Boundary note</strong>: this lib handles <em>business</em> result
/// wrapping. Auth-middleware transport rejections (JWT validation failure,
/// JWKS unavailable) correctly stay as <see cref="RpcException"/> +
/// <see cref="StatusCode.Unauthenticated"/> / <see cref="StatusCode.Unavailable"/>
/// via <c>D2RpcStatusExtensions</c> in <c>DcsvIo.D2.Auth.Grpc</c> — those are
/// genuine transport/auth faults, not business results.
/// </para>
/// </remarks>
public static partial class ProtoExtensions
{
    // ── D2Result → D2ResultProto ─────────────────────────────────────────
    extension(D2Result result)
    {
        /// <summary>
        /// Converts the <see cref="D2Result"/> to its
        /// <see cref="D2ResultProto"/> wire representation. Every field — including
        /// <see cref="D2Result.Category"/> (as the snake_case wire string) and
        /// full-fidelity <see cref="TKMessage"/> key+params — is mapped.
        /// </summary>
        /// <returns>A populated <see cref="D2ResultProto"/>.</returns>
        public D2ResultProto ToProto()
        {
            var proto = new D2ResultProto
            {
                Success = result.Success,
                StatusCode = (int)result.StatusCode,
            };

            if (!result.ErrorCode.Falsey())
                proto.ErrorCode = result.ErrorCode!;

            if (!result.TraceId.Falsey())
                proto.TraceId = result.TraceId!;

            if (result.Category.HasValue)
                proto.Category = result.Category.Value.ToWire();

            proto.Messages.AddRange(result.Messages.Select(ToTKMessageProto));

            foreach (var inputError in result.InputErrors)
            {
                var errorProto = new InputErrorProto { Field = inputError.Field };
                errorProto.Errors.AddRange(inputError.Errors.Select(ToTKMessageProto));
                proto.InputErrors.Add(errorProto);
            }

            return proto;
        }
    }

    // ── D2ResultProto → D2Result<TData> ─────────────────────────────────
    extension(D2ResultProto proto)
    {
        /// <summary>
        /// Converts the <see cref="D2ResultProto"/> wire envelope back into a
        /// typed <c>D2Result{TData}</c>. Unknown category wire strings
        /// (future-proofed) produce a <see langword="null"/> category — never a
        /// throw. Optional-string proto3 fields (<c>error_code</c>,
        /// <c>trace_id</c>, <c>category</c>) are rehydrated as
        /// <see langword="null"/> when the wire value is absent or whitespace-only.
        /// </summary>
        /// <param name="data">
        /// The typed payload to stitch in; travels in sibling response fields,
        /// NOT inside <see cref="D2ResultProto"/>. Defaults to
        /// <see langword="default"/> when no payload is present (failures).
        /// </param>
        /// <typeparam name="TData">
        /// The type of the payload returned by the gRPC call.
        /// </typeparam>
        /// <returns>A faithfully reconstructed <c>D2Result{TData}</c>.</returns>
        public D2Result<TData> ToD2Result<TData>(TData? data = default)
        {
            var messages = proto.Messages.Select(FromTKMessageProto).ToList();
            var inputErrors = proto.InputErrors
                .Select(ie => new InputError(ie.Field, ie.Errors.Select(FromTKMessageProto).ToList()))
                .ToList();

            ErrorCategory? category = null;
            if (!proto.Category.Falsey() &&
                ErrorCategoryWire.TryFromWire(proto.Category!, out var parsed))
                category = parsed;

            return new D2Result<TData>(
                success: proto.Success,
                data: data,
                messages: messages,
                inputErrors: inputErrors,
                statusCode: (HttpStatusCode)proto.StatusCode,
                errorCode: proto.ErrorCode.Falsey() ? null : proto.ErrorCode,
                traceId: proto.TraceId.Falsey() ? null : proto.TraceId,
                category: category);
        }
    }

    // ── AsyncUnaryCall<TProto> → D2Result<TData> resilience wrapper ──────
    extension<TProto>(AsyncUnaryCall<TProto> call)
    {
        /// <summary>
        /// Awaits the gRPC call and re-materializes its embedded
        /// <see cref="D2ResultProto"/> + typed payload into a
        /// <c>D2Result{TData}</c>. Transport faults
        /// (<see cref="RpcException"/>, <see cref="Exception"/>) are
        /// fail-opened to pre-built factory results — the user-facing
        /// <c>Messages</c> stays the factory TK constant; transport detail
        /// is logged sanitized (type name + first frame + status code, NEVER
        /// <see cref="Exception.Message"/>).
        /// </summary>
        /// <param name="resultSelector">
        /// Selects the <see cref="D2ResultProto"/> from the response message.
        /// For a response with <c>D2ResultProto result = 1;</c>, this is
        /// <c>r =&gt; r.Result</c>.
        /// </param>
        /// <param name="dataSelector">
        /// Selects the typed payload from the response. For a response with
        /// <c>repeated Foo data = 2;</c> this is <c>r =&gt; r.Data</c>.
        /// </param>
        /// <param name="logger">Optional logger for transport-fault diagnostics.</param>
        /// <param name="traceId">
        /// Trace identifier threaded into the fail-open factory results so the
        /// caller can correlate the failure.
        /// </param>
        /// <typeparam name="TData">The type of the response payload.</typeparam>
        /// <returns>
        /// A <c>D2Result{TData}</c> that is either the faithfully re-materialized
        /// business result or a pre-built transport-fault result:
        /// <c>ServiceUnavailable</c> for <see cref="RpcException"/>,
        /// <c>Canceled</c> for <see cref="OperationCanceledException"/> (client-side
        /// cancellation that never reached the gRPC layer as an
        /// <see cref="RpcException"/>), and
        /// <c>UnhandledException</c> for any other exception.
        /// <see cref="StatusCode.Cancelled"/> maps to <c>Canceled</c>.
        /// </returns>
        public async Task<D2Result<TData>> HandleAsync<TData>(
            Func<TProto, D2ResultProto> resultSelector,
            Func<TProto, TData?> dataSelector,
            ILogger? logger = null,
            string? traceId = null)
        {
            try
            {
                var response = await call;
                var proto = resultSelector(response);
                var data = dataSelector(response);
                return proto.ToD2Result(data);
            }
            catch (RpcException ex)
            {
                return ex.ToTransportFaultResult<TData>(logger, traceId);
            }
            catch (OperationCanceledException ex)
            {
                if (logger is not null)
                {
                    LogGrpcCanceled(
                        logger,
                        SanitizedExceptionRender.TypeName(ex),
                        SanitizedExceptionRender.FirstFrame(ex),
                        traceId);
                }

                return D2Result<TData>.Canceled(traceId: traceId);
            }
            catch (Exception ex)
            {
                if (logger is not null)
                {
                    LogGrpcUnexpectedError(
                        logger,
                        SanitizedExceptionRender.TypeName(ex),
                        SanitizedExceptionRender.FirstFrame(ex),
                        traceId);
                }

                return D2Result<TData>.UnhandledException(traceId: traceId);
            }
        }
    }

    // ── RpcException → D2Result<TData> transport-fault mapping ───────────
    extension(RpcException ex)
    {
        /// <summary>
        /// Maps a transport <see cref="RpcException"/> to its fail-open
        /// <c>D2Result{TData}</c>: <see cref="StatusCode.Cancelled"/> →
        /// <c>Canceled</c>; every other status → <c>ServiceUnavailable</c>
        /// (downstream unavailable — a 503, NOT a 500; the caller's own logic
        /// is fine, the gRPC peer faulted). This is the SAME mapping
        /// <see cref="HandleAsync"/> applies in its <see cref="RpcException"/>
        /// catch arm — extracted so callers that run the throwing stub call
        /// through a resilience pipeline (which would otherwise classify an
        /// <see cref="RpcException"/> via the gRPC-agnostic generic path and
        /// mis-map it to <c>UnhandledException</c>) can reconstruct the
        /// gRPC-aware code after the pipeline returns.
        /// </summary>
        /// <param name="logger">
        /// Optional logger for sanitized transport-fault diagnostics (type name
        /// + first frame + status code, NEVER <see cref="Exception.Message"/>).
        /// </param>
        /// <param name="traceId">
        /// Trace identifier threaded into the fail-open factory result so the
        /// caller can correlate the failure.
        /// </param>
        /// <typeparam name="TData">The type of the response payload.</typeparam>
        /// <returns>
        /// <c>Canceled</c> for <see cref="StatusCode.Cancelled"/>;
        /// <c>ServiceUnavailable</c> for every other status code.
        /// </returns>
        public D2Result<TData> ToTransportFaultResult<TData>(
            ILogger? logger = null,
            string? traceId = null)
        {
            if (logger is not null)
            {
                LogGrpcTransportFailure(
                    logger,
                    ex.StatusCode,
                    SanitizedExceptionRender.TypeName(ex),
                    SanitizedExceptionRender.FirstFrame(ex),
                    traceId);
            }

            return ex.StatusCode is StatusCode.Cancelled
                ? D2Result<TData>.Canceled(traceId: traceId)
                : D2Result<TData>.ServiceUnavailable(traceId: traceId);
        }
    }

    /// <summary>
    /// Returns <see langword="true"/> when the <see cref="RpcException"/>'s
    /// <see cref="RpcException.StatusCode"/> indicates a transient condition
    /// that is safe to retry with exponential back-off.
    /// </summary>
    /// <remarks>
    /// Transient set (mirrors v1 + gRPC retry guidance):
    /// <list type="bullet">
    ///   <item><see cref="StatusCode.DeadlineExceeded"/> (4) — timeout; transient.</item>
    ///   <item><see cref="StatusCode.ResourceExhausted"/> (8) — quota / rate; back off.</item>
    ///   <item><see cref="StatusCode.Aborted"/> (10) — concurrency conflict; retry.</item>
    ///   <item><see cref="StatusCode.Internal"/> (13) — server crash; may be transient.</item>
    ///   <item><see cref="StatusCode.Unavailable"/> (14) — service unavailable; transient.</item>
    /// </list>
    /// Non-transient examples (not in the set): <see cref="StatusCode.NotFound"/> (5),
    /// <see cref="StatusCode.InvalidArgument"/> (3),
    /// <see cref="StatusCode.PermissionDenied"/> (7),
    /// <see cref="StatusCode.Unauthenticated"/> (16) — retrying without fixing the
    /// root cause would never succeed.
    /// </remarks>
    /// <param name="ex">The gRPC exception to classify.</param>
    /// <returns>
    /// <see langword="true"/> for transient codes; <see langword="false"/> otherwise.
    /// </returns>
    public static bool IsTransientGrpcException(RpcException ex)
    {
        ArgumentNullException.ThrowIfNull(ex);
        return ex.StatusCode is
            StatusCode.DeadlineExceeded or
            StatusCode.ResourceExhausted or
            StatusCode.Aborted or
            StatusCode.Internal or
            StatusCode.Unavailable;
    }

    private static TKMessageProto ToTKMessageProto(TKMessage msg)
    {
        var proto = new TKMessageProto { Key = msg.Key };
        if (msg.Parameters is not null)
        {
            foreach (var kv in msg.Parameters)
                proto.Params.Add(kv.Key, kv.Value);
        }

        return proto;
    }

    private static TKMessage FromTKMessageProto(TKMessageProto proto)
    {
        // TKMessage.ctor is internal; DcsvIo.D2.I18n.Abstractions grants
        // InternalsVisibleTo("DcsvIo.D2.Result.Grpc") for deserialization
        // partners — the same pattern as DcsvIo.D2.I18n.Keys.
        IReadOnlyDictionary<string, string>? parameters =
            proto.Params.Count > 0
                ? new Dictionary<string, string>(proto.Params)
                : null;
        return new TKMessage(proto.Key, parameters);
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message = "gRPC transport failure. StatusCode: {StatusCode}, ExceptionType: {ExceptionType}, Frame: {Frame}, TraceId: {TraceId}")]
    private static partial void LogGrpcTransportFailure(
        ILogger logger,
        StatusCode statusCode,
        string exceptionType,
        string frame,
        string? traceId);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Error,
        Message = "Unexpected error during gRPC call. ExceptionType: {ExceptionType}, Frame: {Frame}, TraceId: {TraceId}")]
    private static partial void LogGrpcUnexpectedError(
        ILogger logger,
        string exceptionType,
        string frame,
        string? traceId);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Information,
        Message = "gRPC call canceled (OperationCanceledException). ExceptionType: {ExceptionType}, Frame: {Frame}, TraceId: {TraceId}")]
    private static partial void LogGrpcCanceled(
        ILogger logger,
        string exceptionType,
        string frame,
        string? traceId);
}
