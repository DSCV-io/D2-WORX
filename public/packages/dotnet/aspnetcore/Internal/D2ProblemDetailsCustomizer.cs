// -----------------------------------------------------------------------
// <copyright file="D2ProblemDetailsCustomizer.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.AspNetCore.Internal;

using System.Diagnostics;
using DcsvIo.D2.ErrorCodes.Category;
using DcsvIo.D2.ProblemDetails;
using DcsvIo.D2.Result;
using DcsvIo.D2.Utilities.Extensions;
using Microsoft.AspNetCore.Http;

/// <summary>
/// Internal helper exposing the
/// <c>Action&lt;Microsoft.AspNetCore.Http.ProblemDetailsContext&gt;</c>
/// installed by
/// <see cref="ProblemDetailsServiceCollectionExtensions.AddD2ProblemDetails"/>.
/// Encapsulated so the customizer logic is unit-testable in isolation
/// (without spinning a TestHost) and so the public extension stays a thin
/// registration shim.
/// </summary>
/// <remarks>
/// <para>
/// FULL D2Result-aware path B emitter. When the request pipeline has stashed
/// a <see cref="D2Result"/> on
/// <see cref="HttpContext.Items"/> under
/// <see cref="D2ProblemDetailsContextItems.D2_RESULT"/>, the customizer
/// populates the following RFC 7807 Shape A body fields from spec-derived
/// constants in <see cref="D2ProblemDetailsKeys"/>:
/// </para>
/// <list type="bullet">
///   <item><c>Type</c> ← <c>D2ProblemDetailsKeys.TYPE_URI_PREFIX</c> +
///     kebab-cased <c>D2Result.ErrorCode</c> (fallback
///     <c>"unhandled-exception"</c> on empty error code).</item>
///   <item><c>Title</c> ← <c>D2ProblemDetailsKeys.TitleFor(StatusCode)</c>.</item>
///   <item><c>Status</c> ← <c>(int)D2Result.StatusCode</c>.</item>
///   <item><c>Extensions[EXTENSION_ERROR_CODE]</c> ← result.ErrorCode.</item>
///   <item><c>Extensions[EXTENSION_MESSAGES]</c> ← result.Messages.</item>
///   <item><c>Extensions[EXTENSION_INPUT_ERRORS]</c> ← result.InputErrors
///     (conditional — only when non-empty).</item>
///   <item><c>Extensions[EXTENSION_CATEGORY]</c> ← result.Category wire
///     string (conditional — only when non-null).</item>
/// </list>
/// <para>
/// The following fields are populated UNCONDITIONALLY (regardless of whether
/// a <see cref="D2Result"/> is stashed), so operators always have a
/// correlation surface for both the D2Result-aware and raw-exception paths:
/// </para>
/// <list type="bullet">
///   <item><c>Instance</c> ← <c>"{Method} {Path}"</c> (matches the path-A
///     emit shape exactly). Suppressed when
///     <c>D2ProblemDetailsOptions.IncludeRequestPath</c> is <c>false</c>.</item>
///   <item><c>Extensions[EXTENSION_TRACE_ID]</c> ← W3C trace id from
///     <see cref="System.Diagnostics.Activity.Current"/>, falling back to
///     <see cref="HttpContext.TraceIdentifier"/>.</item>
///   <item><c>Extensions[EXTENSION_CORRELATION_ID]</c> ← inbound correlation
///     header value (length-capped) or a freshly generated GUID; echoed back
///     via the configured response header when
///     <c>D2ProblemDetailsOptions.EchoCorrelationIdInResponse</c> is <c>true</c>.</item>
/// </list>
/// <para>
/// When NO D2Result is stashed (raw exception path; framework-internal
/// ProblemDetails generation), the customizer leaves <c>Type</c> /
/// <c>Title</c> / <c>Status</c> / <c>EXTENSION_ERROR_CODE</c> /
/// <c>EXTENSION_MESSAGES</c> / <c>EXTENSION_INPUT_ERRORS</c> alone
/// (framework defaults apply) while still emitting the unconditional fields
/// listed above.
/// </para>
/// </remarks>
internal static class D2ProblemDetailsCustomizer
{
    /// <summary>
    /// Applies the D² ProblemDetails enrichment to <paramref name="ctx"/>:
    /// when a <see cref="D2Result"/> is stashed under
    /// <see cref="D2ProblemDetailsContextItems.D2_RESULT"/>, populates the
    /// full Shape A body from <see cref="D2ProblemDetailsKeys"/>; otherwise
    /// populates <c>traceId</c> / <c>correlationId</c> only. Always sets the
    /// <c>instance</c> field (when enabled) and echoes a fresh correlation
    /// id back via the response header.
    /// </summary>
    /// <param name="ctx">The ProblemDetails context to enrich.</param>
    /// <param name="options">The configured options snapshot.</param>
    public static void Apply(
        ProblemDetailsContext ctx,
        D2ProblemDetailsOptions options)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ArgumentNullException.ThrowIfNull(options);

        var httpContext = ctx.HttpContext;
        var problem = ctx.ProblemDetails;

        // D2Result-aware path: when middleware / a handler stashed the
        // originating D2Result, populate Type+Title+Status+d2_error_code+
        // d2_messages+d2_input_errors from spec-derived constants. Keeps
        // path A + path B byte-identical on the D2Result-derived fields.
        // (Instance, traceId, correlationId are emitted unconditionally
        // below — they apply on both the D2Result-aware and raw-exception
        // paths so operators always have a correlation surface.)
        var d2Result = httpContext.GetD2Result();
        if (d2Result is not null && d2Result.Failed)
        {
            ApplyFromD2Result(problem, d2Result);
        }

        // traceId: prefer the W3C distributed-trace id from Activity.Current
        // (set by AspNetCore-instrumentation OR the application directly);
        // fall back to HttpContext.TraceIdentifier (per-instance request id
        // generated by Kestrel) so the field is never empty even when no
        // distributed tracer is wired.
        var activityTraceId = Activity.Current?.TraceId.ToString();
        var traceId = activityTraceId.Truthy()
            ? activityTraceId
            : httpContext.TraceIdentifier;
        problem.Extensions[D2ProblemDetailsKeys.EXTENSION_TRACE_ID] = traceId;

        // correlationId: read inbound header, validate length cap, fall back
        // to fresh GUID. The cap prevents an arbitrary-length user-supplied
        // header from inflating the response body.
        var inboundCorrelationId = ResolveInboundCorrelationId(
            httpContext,
            options.CorrelationIdHeaderName);
        var correlationId = inboundCorrelationId
            ?? Guid.NewGuid().ToString("N");
        problem.Extensions[D2ProblemDetailsKeys.EXTENSION_CORRELATION_ID] = correlationId;

        // Echo the resolved correlation id back via the response header so
        // the caller can include it in subsequent requests + log lines for
        // end-to-end correlation.
        if (options.EchoCorrelationIdInResponse)
            httpContext.Response.Headers[options.CorrelationIdHeaderName] = correlationId;

        // RFC 7807 instance field — operational metadata, never user input.
        // Set after ApplyFromD2Result so the customizer's view wins on the
        // {Method} {Path} shape regardless of whether D2Result-aware path
        // already populated it (both emit identical strings; assignment is
        // structurally idempotent).
        if (options.IncludeRequestPath)
        {
            var method = httpContext.Request.Method;
            var path = httpContext.Request.Path.Value ?? string.Empty;
            problem.Instance = $"{method} {path}";
        }
    }

    private static void ApplyFromD2Result(
        Microsoft.AspNetCore.Mvc.ProblemDetails problem,
        D2Result result)
    {
        var errorCode = result.ErrorCode ?? string.Empty;

        // Type: spec-driven URI prefix + kebab-cased error code. Empty error
        // code → "unhandled-exception" fallback (defensive — framework
        // pipelines plumbing a D2Result without an explicit errorCode is
        // rare but possible).
        problem.Type = errorCode.Length == 0
            ? D2ProblemDetailsKeys.TYPE_URI_PREFIX + "unhandled-exception"
            : D2ProblemDetailsKeys.TYPE_URI_PREFIX + KebabCase(errorCode);

        // Title: spec-driven switch keyed on the D2Result.StatusCode.
        problem.Title = D2ProblemDetailsKeys.TitleFor(result.StatusCode);

        // Status: D2Result.StatusCode wins over whatever the framework
        // pre-populated (operator-explicit intent).
        problem.Status = (int)result.StatusCode;

        // Extensions: spec-driven keys. errorCode + messages always written
        // (matches path A behavior); inputErrors only when non-empty.
        problem.Extensions[D2ProblemDetailsKeys.EXTENSION_ERROR_CODE] = errorCode;
        problem.Extensions[D2ProblemDetailsKeys.EXTENSION_MESSAGES] = result.Messages;
        if (result.InputErrors.Count > 0)
            problem.Extensions[D2ProblemDetailsKeys.EXTENSION_INPUT_ERRORS] = result.InputErrors;

        // Category: the closed-enum semantic class as its snake_case wire
        // string, so the HTTP body carries `category` exactly like the
        // D2Result envelope + the gRPC envelope (cross-transport parity).
        // Omitted when null — matches the path-A conditional-emit + the
        // inputErrors omit-when-absent discipline.
        if (result.Category is { } category)
            problem.Extensions[D2ProblemDetailsKeys.EXTENSION_CATEGORY] = category.ToWire();
    }

    /// <summary>
    /// Reads the inbound correlation-id header. Returns null when missing /
    /// empty / whitespace OR when the value exceeds
    /// <see cref="D2AspNetCoreConstants.MAX_CORRELATION_ID_LENGTH"/>
    /// — caller MUST treat null as "absent" and substitute a fresh GUID.
    /// </summary>
    private static string? ResolveInboundCorrelationId(
        HttpContext httpContext,
        string headerName)
    {
        if (!httpContext.Request.Headers.TryGetValue(headerName, out var values))
            return null;

        var raw = values.ToString().ToNullIfEmpty();

        if (raw is null)
            return null;

        if (raw.Length > D2AspNetCoreConstants.MAX_CORRELATION_ID_LENGTH)
            return null;

        return raw;
    }

    /// <summary>
    /// Kebab-cases an UPPER_SNAKE_CASE error code (<c>AUTH_BEARER_MISSING</c>
    /// → <c>auth-bearer-missing</c>) for the RFC 7807 <c>type</c> URI suffix.
    /// Local helper rather than a shared utility because the auth-http path
    /// uses an internal <c>AuthErrorCodes.KebabCase</c> that is not visible
    /// here (separate consuming-csproj boundary).
    /// </summary>
    private static string KebabCase(string upperSnake)
    {
        var sb = new System.Text.StringBuilder(upperSnake.Length);
        foreach (var c in upperSnake)
            sb.Append(c == '_' ? '-' : char.ToLowerInvariant(c));
        return sb.ToString();
    }
}
