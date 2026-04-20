// -----------------------------------------------------------------------
// <copyright file="JwtFingerprintValidatorTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Gateway;

using System.Security.Cryptography;
using System.Text;
using D2.Shared.JwtAuth.Default;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

/// <summary>
/// Unit tests for the <see cref="JwtFingerprintValidator"/> static helper.
/// Validates the 2-header JWT fingerprint formula: SHA-256(User-Agent + "|" + Accept).
/// </summary>
public class JwtFingerprintValidatorTests
{
    /// <summary>
    /// Tests that ComputeFingerprint returns a valid 64-char hex SHA-256 hash.
    /// </summary>
    [Fact]
    public void ComputeFingerprint_ReturnsValid64CharHexString()
    {
        var context = CreateHttpContext();
        context.Request.Headers.UserAgent = "Mozilla/5.0";

        var result = JwtFingerprintValidator.ComputeFingerprint(context);

        result.Should().HaveLength(64);
        result.Should().MatchRegex("^[0-9a-f]{64}$");
    }

    /// <summary>
    /// Tests that same headers produce the same fingerprint.
    /// </summary>
    [Fact]
    public void ComputeFingerprint_WithSameHeaders_ProducesSameHash()
    {
        var context1 = CreateHttpContext();
        context1.Request.Headers.UserAgent = "Mozilla/5.0";
        context1.Request.Headers.Accept = "text/html";

        var context2 = CreateHttpContext();
        context2.Request.Headers.UserAgent = "Mozilla/5.0";
        context2.Request.Headers.Accept = "text/html";

        var result1 = JwtFingerprintValidator.ComputeFingerprint(context1);
        var result2 = JwtFingerprintValidator.ComputeFingerprint(context2);

        result1.Should().Be(result2);
    }

    /// <summary>
    /// Tests that different User-Agent produces a different hash.
    /// </summary>
    [Fact]
    public void ComputeFingerprint_WithDifferentUserAgent_ProducesDifferentHash()
    {
        var context1 = CreateHttpContext();
        context1.Request.Headers.UserAgent = "Chrome/120";
        context1.Request.Headers.Accept = "text/html";

        var context2 = CreateHttpContext();
        context2.Request.Headers.UserAgent = "Firefox/115";
        context2.Request.Headers.Accept = "text/html";

        var result1 = JwtFingerprintValidator.ComputeFingerprint(context1);
        var result2 = JwtFingerprintValidator.ComputeFingerprint(context2);

        result1.Should().NotBe(result2);
    }

    /// <summary>
    /// Tests that different Accept produces a different hash.
    /// </summary>
    [Fact]
    public void ComputeFingerprint_WithDifferentAccept_ProducesDifferentHash()
    {
        var context1 = CreateHttpContext();
        context1.Request.Headers.UserAgent = "Chrome/120";
        context1.Request.Headers.Accept = "text/html";

        var context2 = CreateHttpContext();
        context2.Request.Headers.UserAgent = "Chrome/120";
        context2.Request.Headers.Accept = "application/json";

        var result1 = JwtFingerprintValidator.ComputeFingerprint(context1);
        var result2 = JwtFingerprintValidator.ComputeFingerprint(context2);

        result1.Should().NotBe(result2);
    }

    /// <summary>
    /// Tests that missing headers are handled with empty string fallback.
    /// </summary>
    [Fact]
    public void ComputeFingerprint_WithNoHeaders_ReturnsValidHash()
    {
        var context = CreateHttpContext();

        var result = JwtFingerprintValidator.ComputeFingerprint(context);

        result.Should().HaveLength(64);
        result.Should().MatchRegex("^[0-9a-f]{64}$");
    }

    /// <summary>
    /// Tests that the result is always lowercase hex.
    /// </summary>
    [Fact]
    public void ComputeFingerprint_ReturnsLowercaseHex()
    {
        var context = CreateHttpContext();
        context.Request.Headers.UserAgent = "Test";

        var result = JwtFingerprintValidator.ComputeFingerprint(context);

        result.Should().Be(result.ToLowerInvariant());
    }

    /// <summary>
    /// Tests cross-platform parity: the .NET fingerprint matches the expected output
    /// from the Node.js formula SHA-256("Mozilla/5.0" + "|" + "text/html").
    /// </summary>
    [Fact]
    public void ComputeFingerprint_MatchesNodeJsOutput_ForIdenticalInputs()
    {
        var context = CreateHttpContext();
        context.Request.Headers.UserAgent = "Mozilla/5.0";
        context.Request.Headers.Accept = "text/html";

        var result = JwtFingerprintValidator.ComputeFingerprint(context);

        // Independently compute the expected hash using the same formula.
        var input = "Mozilla/5.0|text/html";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        var expected = Convert.ToHexStringLower(hash);

        result.Should().Be(expected);
    }

    /// <summary>
    /// Tests that the formula uses exactly 2 headers (UA + Accept), not the 4-header analytics fingerprint.
    /// </summary>
    [Fact]
    public void ComputeFingerprint_IgnoresAcceptLanguageAndAcceptEncoding()
    {
        var context1 = CreateHttpContext();
        context1.Request.Headers.UserAgent = "Chrome/120";
        context1.Request.Headers.Accept = "text/html";
        context1.Request.Headers.AcceptLanguage = "en-US";
        context1.Request.Headers.AcceptEncoding = "gzip";

        var context2 = CreateHttpContext();
        context2.Request.Headers.UserAgent = "Chrome/120";
        context2.Request.Headers.Accept = "text/html";
        context2.Request.Headers.AcceptLanguage = "de-DE";
        context2.Request.Headers.AcceptEncoding = "br";

        var result1 = JwtFingerprintValidator.ComputeFingerprint(context1);
        var result2 = JwtFingerprintValidator.ComputeFingerprint(context2);

        // Same UA + Accept → same fingerprint, regardless of other headers.
        result1.Should().Be(result2);
    }

    /// <summary>
    /// Creates a test HttpContext with default settings.
    /// </summary>
    private static DefaultHttpContext CreateHttpContext()
    {
        return new DefaultHttpContext();
    }
}
