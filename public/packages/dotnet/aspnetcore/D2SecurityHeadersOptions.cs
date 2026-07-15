// -----------------------------------------------------------------------
// <copyright file="D2SecurityHeadersOptions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.AspNetCore;

/// <summary>
/// Configuration for
/// <see cref="SecurityHeadersApplicationBuilderExtensions.UseD2SecurityHeaders"/>.
/// Per-header overrides — each property is a <c>string?</c>:
/// <c>null</c> uses the OWASP-aligned default;
/// empty string suppresses the header entirely (no <c>Set-Cookie</c>-style
/// "delete" — the header simply isn't written);
/// non-empty string overrides the default literal value.
/// </summary>
/// <remarks>
/// <para>
/// All defaults are sourced from the OWASP Secure Headers Project
/// (v0.20+) cross-checked against MDN HTTP Headers reference. Values that
/// are widely deprecated (e.g. <c>X-XSS-Protection</c>) are intentionally
/// NOT included in the default header set.
/// </para>
/// <para>
/// <see cref="StrictTransportSecurity"/> is conditionally written only when
/// the request scheme is HTTPS — HSTS over HTTP is meaningless and the spec
/// forbids preload submission for non-HTTPS-only origins.
/// </para>
/// <para>
/// The default does NOT include the <c>preload</c> directive on HSTS —
/// preload submission is a one-way door (once the apex domain is in the
/// browser-built-in preload list, removal is slow and incomplete). Each
/// service that wants preload submission opts in by setting
/// <see cref="StrictTransportSecurity"/> to a value that includes
/// <c>preload</c>.
/// </para>
/// </remarks>
public sealed record D2SecurityHeadersOptions
{
    /// <summary>
    /// Default for <c>X-Content-Type-Options</c> — <c>"nosniff"</c>.
    /// Prevents MIME-sniffing attacks; OWASP / MDN universal recommendation.
    /// </summary>
    public const string DEFAULT_X_CONTENT_TYPE_OPTIONS = "nosniff";

    /// <summary>
    /// Default for <c>X-Frame-Options</c> — <c>"DENY"</c>. Prevents
    /// clickjacking via framing. We're a JSON API + SPA — no service should
    /// be framed by default. Services that DO need to be framed override
    /// to <c>"SAMEORIGIN"</c> or rely on CSP <c>frame-ancestors</c>.
    /// </summary>
    public const string DEFAULT_X_FRAME_OPTIONS = "DENY";

    /// <summary>
    /// Default for <c>Referrer-Policy</c> — <c>"strict-origin-when-cross-origin"</c>.
    /// Preserves Referer on same-origin (useful for analytics) + only origin
    /// (no path) on cross-origin (avoids leaking internal URLs). MDN
    /// recommended default.
    /// </summary>
    public const string DEFAULT_REFERRER_POLICY = "strict-origin-when-cross-origin";

    /// <summary>
    /// Default for <c>X-Permitted-Cross-Domain-Policies</c> — <c>"none"</c>.
    /// Blocks legacy Adobe Flash / PDF cross-domain access (defense-in-depth).
    /// </summary>
    public const string DEFAULT_X_PERMITTED_CROSS_DOMAIN_POLICIES = "none";

    /// <summary>
    /// Default for <c>Cross-Origin-Resource-Policy</c> — <c>"same-origin"</c>.
    /// Prevents cross-origin embedding (CORB / CORP).
    /// </summary>
    public const string DEFAULT_CROSS_ORIGIN_RESOURCE_POLICY = "same-origin";

    /// <summary>
    /// Default for <c>Cross-Origin-Opener-Policy</c> — <c>"same-origin"</c>.
    /// Isolates browsing context (Spectre mitigation).
    /// </summary>
    public const string DEFAULT_CROSS_ORIGIN_OPENER_POLICY = "same-origin";

    /// <summary>
    /// Default for <c>Strict-Transport-Security</c> —
    /// <c>"max-age=31536000; includeSubDomains"</c>. 1-year HSTS with
    /// subdomain coverage. Only emitted on HTTPS requests. Intentionally
    /// does NOT include <c>preload</c> — services that want preload
    /// submission override this value explicitly.
    /// </summary>
    public const string DEFAULT_STRICT_TRANSPORT_SECURITY =
        "max-age=31536000; includeSubDomains";

    /// <summary>
    /// Gets or sets the override for the <c>X-Content-Type-Options</c> header.
    /// <c>null</c> → uses <see cref="DEFAULT_X_CONTENT_TYPE_OPTIONS"/>;
    /// empty → header NOT written;
    /// non-empty → that literal value written.
    /// </summary>
    public string? XContentTypeOptions { get; set; }

    /// <summary>
    /// Gets or sets the override for the <c>X-Frame-Options</c> header.
    /// <c>null</c> / empty / non-empty semantics same as
    /// <see cref="XContentTypeOptions"/>.
    /// </summary>
    public string? XFrameOptions { get; set; }

    /// <summary>
    /// Gets or sets the override for the <c>Referrer-Policy</c> header.
    /// <c>null</c> / empty / non-empty semantics same as
    /// <see cref="XContentTypeOptions"/>.
    /// </summary>
    public string? ReferrerPolicy { get; set; }

    /// <summary>
    /// Gets or sets the override for the <c>X-Permitted-Cross-Domain-Policies</c> header.
    /// <c>null</c> / empty / non-empty semantics same as
    /// <see cref="XContentTypeOptions"/>.
    /// </summary>
    public string? XPermittedCrossDomainPolicies { get; set; }

    /// <summary>
    /// Gets or sets the override for the <c>Cross-Origin-Resource-Policy</c> header.
    /// <c>null</c> / empty / non-empty semantics same as
    /// <see cref="XContentTypeOptions"/>.
    /// </summary>
    public string? CrossOriginResourcePolicy { get; set; }

    /// <summary>
    /// Gets or sets the override for the <c>Cross-Origin-Opener-Policy</c> header.
    /// <c>null</c> / empty / non-empty semantics same as
    /// <see cref="XContentTypeOptions"/>.
    /// </summary>
    public string? CrossOriginOpenerPolicy { get; set; }

    /// <summary>
    /// Gets or sets the override for the <c>Strict-Transport-Security</c> header. Only
    /// emitted when the request scheme is HTTPS regardless of value.
    /// <c>null</c> / empty / non-empty semantics same as
    /// <see cref="XContentTypeOptions"/>.
    /// </summary>
    public string? StrictTransportSecurity { get; set; }
}
