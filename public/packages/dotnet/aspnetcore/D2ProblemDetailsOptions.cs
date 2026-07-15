// -----------------------------------------------------------------------
// <copyright file="D2ProblemDetailsOptions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.AspNetCore;

using DcsvIo.D2.Headers.Http;

/// <summary>
/// Configuration for
/// <see cref="ProblemDetailsServiceCollectionExtensions.AddD2ProblemDetails"/>.
/// </summary>
/// <remarks>
/// <para>
/// Customizes ASP.NET Core's built-in
/// <c>Microsoft.AspNetCore.Mvc.ProblemDetails</c> output (RFC 7807 JSON
/// problem-details response body) by populating
/// <see cref="System.Diagnostics.Activity"/>-derived
/// <c>traceId</c> + caller-supplied / lib-generated
/// <c>correlationId</c> into the <c>extensions</c> dictionary AND echoing
/// the resolved correlation id back via the response header.
/// </para>
/// <para>
/// PII discipline: the customizer NEVER reads
/// <c>HttpContext.Request.QueryString</c>,
/// <c>Request.Body</c>, or any user-input source. Request path is
/// included as the RFC 7807 <c>instance</c> field (route templates are
/// operational metadata, not PII); the request method is included for
/// disambiguation when the same instance carries different verbs.
/// </para>
/// </remarks>
public sealed record D2ProblemDetailsOptions
{
    /// <summary>
    /// Gets or sets the header name read for the inbound correlation id
    /// and written for the
    /// outbound echo. Default
    /// <see cref="HttpHeaders.CORRELATION_ID"/>
    /// (<c>"X-Correlation-Id"</c>). Override only for legacy compatibility
    /// with services that consume a different header name.
    /// </summary>
    public string CorrelationIdHeaderName { get; set; } =
        HttpHeaders.CORRELATION_ID;

    /// <summary>
    /// Gets or sets a value indicating whether a freshly-generated
    /// correlation id (used when the request did not carry one) is echoed
    /// back to the response. When <c>true</c> (the default), the freshly
    /// (used when the request did not carry one) is written back to the
    /// response via <see cref="CorrelationIdHeaderName"/> so the caller
    /// can include it in subsequent requests / log lines for end-to-end
    /// correlation. Mirrors the <c>Idempotency-Key</c> echo precedent.
    /// </summary>
    public bool EchoCorrelationIdInResponse { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the customizer populates
    /// the RFC 7807 instance field. When <c>true</c> (the default), the
    /// customizer populates the RFC 7807
    /// <c>instance</c> field with <c>{Method} {Path}</c> (e.g.
    /// <c>"GET /api/users/123"</c>). PII-safe: route templates + method
    /// are operational metadata, never user input. Set <c>false</c> to
    /// preserve ASP.NET Core's default null instance (rarely useful).
    /// </summary>
    public bool IncludeRequestPath { get; set; } = true;
}
