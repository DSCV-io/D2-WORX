// -----------------------------------------------------------------------
// <copyright file="JwtFingerprintValidator.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.JwtAuth.Default;

using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;

/// <summary>
/// Computes a client fingerprint from HTTP request headers for JWT binding validation.
/// </summary>
/// <remarks>
/// Uses the same formula as the Node.js auth service (<c>session-fingerprint.ts</c>):
/// <c>SHA-256(User-Agent + "|" + Accept)</c>.
///
/// This is intentionally different from <see cref="D2.Shared.RequestEnrichment.Default.FingerprintBuilder"/>
/// which uses 4 headers for analytics/logging purposes.
/// </remarks>
public static class JwtFingerprintValidator
{
    /// <summary>
    /// Computes the JWT-binding fingerprint from the request's User-Agent and Accept headers.
    /// </summary>
    ///
    /// <param name="context">
    /// The HTTP context to compute the fingerprint from.
    /// </param>
    ///
    /// <returns>
    /// A 64-character lowercase hex string (SHA-256 hash).
    /// </returns>
    public static string ComputeFingerprint(HttpContext context)
    {
        var headers = context.Request.Headers;

        var userAgent = headers.UserAgent.FirstOrDefault() ?? string.Empty;
        var accept = headers.Accept.FirstOrDefault() ?? string.Empty;

        var input = $"{userAgent}|{accept}";

        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexStringLower(hash);
    }
}
