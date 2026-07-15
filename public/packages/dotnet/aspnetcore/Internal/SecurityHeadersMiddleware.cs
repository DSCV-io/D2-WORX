// -----------------------------------------------------------------------
// <copyright file="SecurityHeadersMiddleware.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.AspNetCore.Internal;

using DcsvIo.D2.Utilities.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

/// <summary>
/// Internal middleware impl behind
/// <see cref="SecurityHeadersApplicationBuilderExtensions.UseD2SecurityHeaders"/>.
/// Registers an <c>HttpResponse.OnStarting</c> callback so the headers are
/// written into the response before the body is flushed (writing during
/// <c>InvokeAsync</c> would race with downstream middleware that has already
/// started the response).
/// </summary>
internal sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate r_next;
    private readonly D2SecurityHeadersOptions r_options;

    /// <summary>
    /// Initializes a new instance of the <see cref="SecurityHeadersMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <param name="options">The configured security-headers options snapshot.</param>
    public SecurityHeadersMiddleware(
        RequestDelegate next,
        IOptions<D2SecurityHeadersOptions> options)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(options);

        r_next = next;
        r_options = options.Value;
    }

    /// <summary>
    /// Pipeline entry point. Registers the OnStarting callback then invokes
    /// the next middleware.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>A task representing the async pipeline continuation.</returns>
    public Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var state = (context.Response, r_options, context.Request.IsHttps);
        context.Response.OnStarting(WriteHeadersCallback, state);
        return r_next(context);
    }

    /// <summary>
    /// OnStarting callback writing the security-header set into the
    /// response. Static so the closure carries no captures (state is
    /// passed in via the second OnStarting argument).
    /// </summary>
    private static Task WriteHeadersCallback(object state)
    {
        var (response, options, isHttps) =
            ((HttpResponse, D2SecurityHeadersOptions, bool))state;

        ApplyHeader(
            response,
            "X-Content-Type-Options",
            options.XContentTypeOptions,
            D2SecurityHeadersOptions.DEFAULT_X_CONTENT_TYPE_OPTIONS);

        ApplyHeader(
            response,
            "X-Frame-Options",
            options.XFrameOptions,
            D2SecurityHeadersOptions.DEFAULT_X_FRAME_OPTIONS);

        ApplyHeader(
            response,
            "Referrer-Policy",
            options.ReferrerPolicy,
            D2SecurityHeadersOptions.DEFAULT_REFERRER_POLICY);

        ApplyHeader(
            response,
            "X-Permitted-Cross-Domain-Policies",
            options.XPermittedCrossDomainPolicies,
            D2SecurityHeadersOptions.DEFAULT_X_PERMITTED_CROSS_DOMAIN_POLICIES);

        ApplyHeader(
            response,
            "Cross-Origin-Resource-Policy",
            options.CrossOriginResourcePolicy,
            D2SecurityHeadersOptions.DEFAULT_CROSS_ORIGIN_RESOURCE_POLICY);

        ApplyHeader(
            response,
            "Cross-Origin-Opener-Policy",
            options.CrossOriginOpenerPolicy,
            D2SecurityHeadersOptions.DEFAULT_CROSS_ORIGIN_OPENER_POLICY);

        // HSTS only on HTTPS — HSTS over HTTP is meaningless and the spec
        // forbids preload submission for non-HTTPS-only origins.
        if (isHttps)
        {
            ApplyHeader(
                response,
                "Strict-Transport-Security",
                options.StrictTransportSecurity,
                D2SecurityHeadersOptions.DEFAULT_STRICT_TRANSPORT_SECURITY);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Applies the per-header tri-state semantic:
    /// <c>null override</c> → write the default value;
    /// empty string override → suppress (do not write);
    /// non-empty override → write the override literal.
    /// </summary>
    private static void ApplyHeader(
        HttpResponse response,
        string headerName,
        string? overrideValue,
        string defaultValue)
    {
        // Tri-state: null = use default; "" = suppress; non-empty = override.
        // overrideValue is null  → write default.
        // overrideValue is ""    → suppress (return without write).
        // overrideValue is " "   → suppress (Falsey() returns true on whitespace too).
        // overrideValue is "foo" → write "foo".
        if (overrideValue is null)
        {
            response.Headers[headerName] = defaultValue;
            return;
        }

        if (overrideValue.Falsey())
            return;

        response.Headers[headerName] = overrideValue;
    }
}
