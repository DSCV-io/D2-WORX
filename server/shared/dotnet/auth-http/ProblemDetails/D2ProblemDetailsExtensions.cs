// -----------------------------------------------------------------------
// <copyright file="D2ProblemDetailsExtensions.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Auth.Http.ProblemDetails;

using System.Diagnostics;
using System.Net;
using D2.Shared.Auth.Errors;
using D2.Shared.Auth.Telemetry;
using D2.Shared.I18n;
using D2.Shared.Result;
using Microsoft.AspNetCore.Http;
using MvcProblemDetails = Microsoft.AspNetCore.Mvc.ProblemDetails;

/// <summary>
/// Builds RFC 7807 <see cref="MvcProblemDetails"/> from a failure
/// <see cref="D2Result"/>. The single emit point for every auth-middleware-
/// produced response — keeps the <c>d2.auth.problem.emitted</c> counter
/// total accurate.
/// </summary>
/// <remarks>
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
///   <item><c>Instance</c> is <see cref="HttpRequest.Path"/> only — no query
///     string (query strings carry referrers, search terms, sometimes
///     session-binding params).</item>
/// </list>
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
        /// <see cref="AuthTelemetry.ProblemEmitted"/> tagged with the result's
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
        /// failure-only.
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
            var errorCode = result.ErrorCode ?? string.Empty;
            var problem = new MvcProblemDetails
            {
                Status = statusCode,
                Title = TitleFor(result.StatusCode),
                Type = TypeUriFor(errorCode),
                Instance = context.Request.Path.Value,
            };

            problem.Extensions[EXTENSION_ERROR_CODE] = errorCode;
            problem.Extensions[EXTENSION_MESSAGES] = MaterializeMessages(result.Messages);

            var traceId = Activity.Current?.TraceId.ToString();
            if (traceId is not null)
                problem.Extensions[EXTENSION_TRACE_ID] = traceId;

            AuthTelemetry.ProblemEmitted.Add(
                1,
                new KeyValuePair<string, object?>(
                    AuthTelemetryTags.ProblemEmitted.TAG_D2_ERROR_CODE, errorCode));

            return problem;
        }
    }

    /// <summary>The closed-enum coarse <c>Title</c> for 401 responses.</summary>
    public const string TITLE_UNAUTHORIZED = "Unauthorized";

    /// <summary>The closed-enum coarse <c>Title</c> for 503 responses.</summary>
    public const string TITLE_SERVICE_UNAVAILABLE = "Service Unavailable";

    /// <summary>The fallback <c>Title</c> for any other failure status code.</summary>
    public const string TITLE_REQUEST_FAILED = "Request Failed";

    /// <summary>The base URL for the <c>Type</c> URI scheme.</summary>
    public const string TYPE_URI_PREFIX = "https://problems.d2-worx.com/auth/";

    /// <summary>The extension key carrying the machine-readable error code.</summary>
    public const string EXTENSION_ERROR_CODE = "d2_error_code";

    /// <summary>The extension key carrying the array of TK message objects.</summary>
    public const string EXTENSION_MESSAGES = "d2_messages";

    /// <summary>The extension key carrying the W3C trace id.</summary>
    public const string EXTENSION_TRACE_ID = "traceId";

    private static string TitleFor(HttpStatusCode statusCode) =>
        statusCode switch
        {
            HttpStatusCode.Unauthorized => TITLE_UNAUTHORIZED,
            HttpStatusCode.ServiceUnavailable => TITLE_SERVICE_UNAVAILABLE,
            _ => TITLE_REQUEST_FAILED,
        };

    private static string TypeUriFor(string errorCode)
    {
        // Empty error code falls back to a generic auth-error URI rather than
        // emitting a malformed `https://.../auth/` (trailing slash). Defensive
        // — every AuthFailures helper carries an error code, so empty would
        // only happen on a manually-built D2Result going through this path.
        if (errorCode.Length == 0)
            return TYPE_URI_PREFIX + "unknown";
        return TYPE_URI_PREFIX + AuthErrorCodes.KebabCase(errorCode);
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
