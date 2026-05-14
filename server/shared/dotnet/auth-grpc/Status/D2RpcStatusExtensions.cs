// -----------------------------------------------------------------------
// <copyright file="D2RpcStatusExtensions.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Auth.Grpc.Status;

using System.Diagnostics;
using System.Net;
using System.Text.Json;
using D2.Shared.Auth.Telemetry;
using D2.Shared.I18n;
using D2.Shared.Result;
using global::Grpc.Core;
using GrpcStatus = global::Grpc.Core.Status;
using GrpcStatusCode = global::Grpc.Core.StatusCode;

/// <summary>
/// Builds <see cref="RpcException"/> instances from a failure
/// <see cref="D2Result"/>. The single emit point for every auth-interceptor-
/// produced gRPC failure — keeps the <c>d2.auth.problem.emitted</c> counter
/// total accurate across both transport bindings.
/// </summary>
/// <remarks>
/// <para>
/// <strong>StatusCode mapping</strong>:
/// </para>
/// <list type="bullet">
///   <item><see cref="HttpStatusCode.Unauthorized"/> (401) →
///     <see cref="GrpcStatusCode.Unauthenticated"/> (16).</item>
///   <item><see cref="HttpStatusCode.ServiceUnavailable"/> (503) →
///     <see cref="GrpcStatusCode.Unavailable"/> (14). gRPC retry policies
///     treat <see cref="GrpcStatusCode.Unavailable"/> as transient-retriable,
///     which matches the semantic of JWKS / liveness outage.</item>
///   <item>Any other failure status code (defensive fallback) →
///     <see cref="GrpcStatusCode.Internal"/>. Should never happen given
///     <c>AuthFailures</c> factory shapes; defensive.</item>
/// </list>
/// <para>
/// <strong>No info-leak surface</strong>:
/// </para>
/// <list type="bullet">
///   <item><see cref="GrpcStatus.Detail"/> is DELIBERATELY EMPTY. Telling an
///     attacker which validation step failed (signature vs expired vs claim
///     missing) is an info leak; the granular <c>d2_error_code</c> trailer
///     carries the machine-readable taxonomy for legitimate operators /
///     dashboards. Same omission policy as the HTTP middleware's
///     <c>Detail</c>.</item>
///   <item>Trailers carry only:
///     <list type="bullet">
///       <item><c>d2_error_code</c> — the closed-enum
///         <see cref="D2.Shared.Auth.Errors.AuthErrorCodes"/> constant.</item>
///       <item><c>d2_messages</c> — the <see cref="D2Result.Messages"/> array
///         serialized as JSON (TK keys + bounded params; ASCII-safe;
///         inspectable in tools like grpcurl).</item>
///       <item><c>traceid</c> — <see cref="Activity.Current"/>'s W3C trace id
///         when present, OMITTED otherwise (parity with HTTP middleware's
///         "absent on no-Activity" choice).</item>
///     </list>
///   </item>
/// </list>
/// <para>
/// <strong>Trailer-size bound</strong>: gRPC trailers are typically capped
/// at 8 KB total. <c>d2_error_code</c> is a small constant; <c>traceid</c>
/// is 32 hex chars; <c>d2_messages</c> is bounded by current
/// <c>AuthFailures</c> factory shapes (one TK key + zero params for every
/// in-scope failure today). No size-bound concern under the current factory
/// surface.
/// </para>
/// </remarks>
public static class D2RpcStatusExtensions
{
    /// <param name="result">The failure result to translate.</param>
    extension(D2Result? result)
    {
        /// <summary>
        /// Builds an <see cref="RpcException"/> from a failure
        /// <see cref="D2Result"/>. Side-effect: increments
        /// <see cref="AuthTelemetry.ProblemEmitted"/> tagged with the
        /// result's error code.
        /// </summary>
        /// <returns>The populated <see cref="RpcException"/>.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when the result is <see langword="null"/>.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the result is a success — this extension is
        /// failure-only.
        /// </exception>
        public RpcException ToRpcException()
        {
            ArgumentNullException.ThrowIfNull(result);

            if (result.Success)
            {
                throw new InvalidOperationException(
                    "ToRpcException is failure-only; received a success result. "
                        + "Caller must short-circuit on success before invoking this extension.");
            }

            var grpcCode = MapStatusCode(result.StatusCode);
            var errorCode = result.ErrorCode ?? string.Empty;

            var trailers = new Metadata
            {
                { TRAILER_ERROR_CODE, errorCode },
                { TRAILER_MESSAGES, SerializeMessages(result.Messages) },
            };

            var traceId = Activity.Current?.TraceId.ToString();
            if (traceId is not null)
                trailers.Add(TRAILER_TRACE_ID, traceId);

            AuthTelemetry.ProblemEmitted.Add(
                1,
                new KeyValuePair<string, object?>(
                    AuthTelemetryTags.ProblemEmitted.TAG_D2_ERROR_CODE, errorCode));

            // Status.Detail is empty (info-leak parity with HTTP middleware's
            // omitted ProblemDetails Detail). The granular d2_error_code
            // trailer carries the machine-readable taxonomy.
            return new RpcException(new GrpcStatus(grpcCode, string.Empty), trailers);
        }
    }

    /// <summary>The trailer key carrying the machine-readable error code.</summary>
    public const string TRAILER_ERROR_CODE = "d2_error_code";

    /// <summary>The trailer key carrying the array of TK message objects (JSON text).</summary>
    public const string TRAILER_MESSAGES = "d2_messages";

    /// <summary>The trailer key carrying the W3C trace id (lower-hex, 32 chars).</summary>
    public const string TRAILER_TRACE_ID = "traceid";

    // Pinned so JsonSerializerOptions caching applies; default options match
    // the codebase's Web defaults (camelCase property names, ignore null).
    // Static so we allocate once.
    private static readonly JsonSerializerOptions sr_jsonOptions =
        new(JsonSerializerDefaults.Web);

    private static GrpcStatusCode MapStatusCode(HttpStatusCode statusCode) =>
        statusCode switch
        {
            HttpStatusCode.Unauthorized => GrpcStatusCode.Unauthenticated,
            HttpStatusCode.ServiceUnavailable => GrpcStatusCode.Unavailable,
            _ => GrpcStatusCode.Internal,
        };

    private static string SerializeMessages(IReadOnlyList<TKMessage> messages)
    {
        // TKMessage carries its own JsonConverter (TKMessageJsonConverter)
        // emitting { "key": "...", "params": { ... } } — pass through
        // verbatim so the wire shape matches HTTP ProblemDetails'
        // d2_messages extension exactly.
        return JsonSerializer.Serialize(messages, sr_jsonOptions);
    }
}
