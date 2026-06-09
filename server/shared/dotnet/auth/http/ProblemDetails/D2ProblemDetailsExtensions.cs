// -----------------------------------------------------------------------
// <copyright file="D2ProblemDetailsExtensions.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Auth.Http.ProblemDetails;

using System.Diagnostics;
using D2.Shared.Auth.Errors;
using D2.Shared.Auth.Telemetry;
using D2.Shared.ErrorCodes.Category;
using D2.Shared.I18n;
using D2.Shared.ProblemDetails;
using D2.Shared.Result;
using Microsoft.AspNetCore.Http;
using MvcProblemDetails = Microsoft.AspNetCore.Mvc.ProblemDetails;

/// <summary>
/// Builds RFC 7807 <see cref="MvcProblemDetails"/> from a failure
/// <see cref="D2Result"/>. The single emit point for every auth-middleware-
/// produced response (path A) — keeps the <c>d2.auth.problem.emitted</c>
/// counter total accurate.
/// </summary>
/// <remarks>
/// <para>
/// The wire-format catalog (<c>TYPE_URI_PREFIX</c>, <c>CONTENT_TYPE</c>,
/// <c>EXTENSION_*</c>, <c>TITLE_*</c>, + <see cref="D2ProblemDetailsKeys.TitleFor"/>
/// switch) lives in <see cref="D2ProblemDetailsKeys"/> (codegen-emitted into
/// <c>D2.Shared.ProblemDetails.Abstractions</c> from
/// <c>contracts/problem-details/problem-details.spec.json</c>). The same spec
/// drives the TS-side <c>@d2/problem-details-abstractions</c> catalog (re-exported from <c>@d2/headers</c>) AND the path-B
/// emitter (<c>D2ProblemDetailsCustomizer</c> in <c>D2.Shared.AspNetCore</c>),
/// so the three emitters produce byte-identical Shape A bodies for identical
/// inputs.
/// </para>
/// <para>
/// <strong>No info-leak surface</strong>:
/// </para>
/// <list type="bullet">
///   <item><c>Title</c> is locale-NEUTRAL English from a closed enumeration
///     (e.g. <c>"Unauthorized"</c> for 401, <c>"Service Unavailable"</c> for
///     503). Locale-aware translation is the client's job via the
///     <c>d2_messages</c> extension.</item>
///   <item><c>Detail</c> is DELIBERATELY OMITTED. RFC 7807 §3.1 says
///     <c>detail</c> should help solve the problem; for auth failures,
///     telling an attacker WHICH validation step failed (signature vs
///     expired vs claim missing) is an info leak. The granular
///     <c>d2_error_code</c> carries the machine-readable taxonomy for
///     legitimate operators / dashboards.</item>
///   <item><c>Instance</c> is <c>"{Method} {Path}"</c> — method + path only;
///     query string is excluded (query strings carry referrers, search
///     terms, sometimes session-binding params). RFC 7807 §3.1 frames
///     <c>instance</c> as a URI reference (MAY); the space-separated form
///     is operator-diagnostic-friendly and matches the sibling path-B
///     <see cref="D2ProblemDetailsKeys"/>-consuming emitter for cross-path
///     wire-shape parity.</item>
/// </list>
/// <para>
/// <strong>2xx guard</strong>: <see cref="ToProblemDetails"/> throws
/// <see cref="InvalidOperationException"/> when
/// <c>(int)result.StatusCode &lt; 400</c>. RFC 7807 §3 frames the
/// ProblemDetails wire around error responses (4xx / 5xx); a 2xx partial
/// success (e.g. <c>SomeFound</c>) belongs on the D2Result envelope, not the
/// ProblemDetails body. The guard converts a silent semantic mismatch into a
/// loud runtime exception.
/// </para>
/// <para>
/// <strong>Trace correlation</strong>: when an OpenTelemetry
/// <see cref="Activity"/> is on the current execution context, its trace id
/// is surfaced via <c>Extensions["traceId"]</c> in W3C lower-hex format. When
/// no Activity is active, the extension is OMITTED — never surfaced as null.
/// </para>
/// </remarks>
public static class D2ProblemDetailsExtensions
{
    /// <param name="result">The failure result to translate.</param>
    extension(D2Result? result)
    {
        /// <summary>
        /// Builds an RFC 7807 ProblemDetails from a failure
        /// <see cref="D2Result"/>. Side-effect: increments
        /// <see cref="AuthTelemetry.SR_ProblemEmitted"/> tagged with the result's
        /// error code.
        /// </summary>
        /// <param name="context">The HTTP context (for path → Instance).</param>
        /// <returns>The populated <see cref="MvcProblemDetails"/>.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when the result or <paramref name="context"/> is
        /// <see langword="null"/>.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the result is a success — this extension is
        /// failure-only — OR when the result's status code is 2xx (3xx are
        /// out of the failure shape too). RFC 7807 frames ProblemDetails
        /// around 4xx / 5xx responses; success / partial-success bodies
        /// belong on the D2Result envelope.
        /// </exception>
        public MvcProblemDetails ToProblemDetails(HttpContext context)
        {
            ArgumentNullException.ThrowIfNull(result);
            ArgumentNullException.ThrowIfNull(context);

            if (result.Success)
            {
                throw new InvalidOperationException(
                    "ToProblemDetails is failure-only; received a success result. "
                        + "Caller must short-circuit on success before invoking this extension.");
            }

            var statusCode = (int)result.StatusCode;
            if (statusCode < 400)
            {
                throw new InvalidOperationException(
                    $"ToProblemDetails received a non-error status code {statusCode}. "
                        + "RFC 7807 frames the ProblemDetails wire around 4xx / 5xx; "
                        + "partial-success (e.g. SomeFound / 206) belongs on the "
                        + "D2Result envelope, not the ProblemDetails body.");
            }

            var errorCode = result.ErrorCode ?? string.Empty;
            var method = context.Request.Method;
            var path = context.Request.Path.Value ?? string.Empty;
            var problem = new MvcProblemDetails
            {
                Status = statusCode,
                Title = D2ProblemDetailsKeys.TitleFor(result.StatusCode),
                Type = TypeUriFor(errorCode),
                Instance = $"{method} {path}",
            };

            problem.Extensions[D2ProblemDetailsKeys.EXTENSION_ERROR_CODE] = errorCode;
            problem.Extensions[D2ProblemDetailsKeys.EXTENSION_MESSAGES] =
                MaterializeMessages(result.Messages);
            if (result.InputErrors.Count > 0)
            {
                problem.Extensions[D2ProblemDetailsKeys.EXTENSION_INPUT_ERRORS] =
                    result.InputErrors;
            }

            // Category: the closed-enum semantic class, emitted as its
            // snake_case wire string so the HTTP body carries `category`
            // exactly like the D2Result envelope + the gRPC envelope
            // (cross-transport parity). Omitted when null — matching the
            // omit-when-absent discipline of the inputErrors / traceId
            // extensions.
            if (result.Category is { } category)
                problem.Extensions[D2ProblemDetailsKeys.EXTENSION_CATEGORY] = category.ToWire();

            var traceId = Activity.Current?.TraceId.ToString();
            if (traceId is not null)
                problem.Extensions[D2ProblemDetailsKeys.EXTENSION_TRACE_ID] = traceId;

            AuthTelemetry.SR_ProblemEmitted.Add(
                1,
                new KeyValuePair<string, object?>(
                    AuthTelemetryTags.ProblemEmitted.TAG_D2_ERROR_CODE, errorCode));

            return problem;
        }
    }

    private static string TypeUriFor(string errorCode)
    {
        // Empty error code falls back to a generic URI rather than emitting a
        // malformed `https://problems.d2.dcsv.io/` (bare prefix). Defensive
        // — every AuthFailures helper carries an error code, so empty would
        // only happen on a manually-built D2Result going through this path.
        if (errorCode.Length == 0)
            return D2ProblemDetailsKeys.TYPE_URI_PREFIX + "unknown";
        return D2ProblemDetailsKeys.TYPE_URI_PREFIX + AuthErrorCodes.KebabCase(errorCode);
    }

    private static IReadOnlyList<TKMessage> MaterializeMessages(
        IReadOnlyList<TKMessage> messages)
    {
        // TKMessage already carries its own JSON converter
        // (TKMessageJsonConverter) producing { "key": "...", "params": { ... } }
        // — pass through verbatim. Wrapping in a private DTO would diverge from
        // the canonical wire shape used everywhere else in the codebase
        // (D2Result.Messages serialization, notification refs, etc.).
        return messages;
    }
}
