// -----------------------------------------------------------------------
// <copyright file="RequestHeaders.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Auth.Abstractions;

/// <summary>
/// Custom HTTP request header name constants. D²-specific headers use the
/// <c>X-D2-*</c> prefix; widely-deployed conventional headers (e.g.
/// <c>Idempotency-Key</c>) use their canonical names without prefix.
/// </summary>
/// <remarks>
/// CORS configuration must include every header listed here in the
/// <c>allowHeaders</c> set — missing one breaks browser preflight for any
/// request that sets it.
/// </remarks>
public static class RequestHeaders
{
    /// <summary>
    /// Idempotency key for request deduplication. Conventional Stripe-style header name.
    /// </summary>
    public const string IDEMPOTENCY_KEY = "Idempotency-Key";

    /// <summary>Client-computed device fingerprint for rate limiting + adaptive auth.</summary>
    public const string CLIENT_FINGERPRINT = "X-D2-Client-Fingerprint";
}
