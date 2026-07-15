// -----------------------------------------------------------------------
// <copyright file="TestJwt.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.AuthOutbound.Fixtures;

using System.Text;
using System.Text.Json;

/// <summary>
/// Builds valid-shaped (but unsigned) JWT strings for testing the
/// parse-without-validation paths. The signature segment is a fixed
/// placeholder — the consuming code under test does NOT validate it.
/// </summary>
internal static class TestJwt
{
    /// <summary>Builds a JWT carrying the supplied claim payload.</summary>
    /// <param name="payloadClaims">The payload claim set as a key/value dict.</param>
    /// <returns>A three-segment dot-separated JWT string.</returns>
    public static string Build(Dictionary<string, object?> payloadClaims)
    {
        var header = Base64UrlEncode("""{"alg":"none","typ":"JWT"}"""u8.ToArray());
        var payloadJson = JsonSerializer.Serialize(payloadClaims);
        var payload = Base64UrlEncode(Encoding.UTF8.GetBytes(payloadJson));
        const string signature = "test-signature-not-validated";
        return $"{header}.{payload}.{signature}";
    }

    /// <summary>Builds a JWT with a single <c>d2_session_id</c> claim.</summary>
    /// <param name="sessionId">The session id to embed.</param>
    /// <returns>A three-segment dot-separated JWT string.</returns>
    public static string WithSessionId(Guid sessionId) =>
        Build(new() { ["d2_session_id"] = sessionId.ToString() });

    /// <summary>Builds a JWT that is structurally valid but lacks the session-id claim.</summary>
    /// <returns>A three-segment dot-separated JWT string.</returns>
    public static string WithoutSessionId() =>
        Build(new() { ["sub"] = "user-123" });

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
