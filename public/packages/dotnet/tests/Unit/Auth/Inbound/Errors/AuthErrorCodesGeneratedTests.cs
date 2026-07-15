// -----------------------------------------------------------------------
// <copyright file="AuthErrorCodesGeneratedTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Auth.Inbound.Errors;

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using AwesomeAssertions;
using DcsvIo.D2.Auth.Errors;
using DcsvIo.D2.Tests.Unit.Auth;
using Xunit;

/// <summary>
/// Per-public-VALUE pin for every <see cref="AuthErrorCodes"/> constant
/// emitted by the codegen — every spec entry's <c>code</c> literal is
/// asserted against the generated constant value, and the generated
/// <c>AllCodes</c> set is asserted set-equal to the spec's code list.
/// </summary>
public sealed class AuthErrorCodesGeneratedTests
{
    [Theory]
    [InlineData(nameof(AuthErrorCodes.AUTH_BEARER_MISSING), "AUTH_BEARER_MISSING")]
    [InlineData(nameof(AuthErrorCodes.AUTH_BEARER_MALFORMED), "AUTH_BEARER_MALFORMED")]
    [InlineData(nameof(AuthErrorCodes.AUTH_JWT_SIGNATURE_INVALID), "AUTH_JWT_SIGNATURE_INVALID")]
    [InlineData(nameof(AuthErrorCodes.AUTH_JWT_EXPIRED), "AUTH_JWT_EXPIRED")]
    [InlineData(nameof(AuthErrorCodes.AUTH_JWT_NOT_YET_VALID), "AUTH_JWT_NOT_YET_VALID")]
    [InlineData(nameof(AuthErrorCodes.AUTH_JWT_ISSUER_MISMATCH), "AUTH_JWT_ISSUER_MISMATCH")]
    [InlineData(nameof(AuthErrorCodes.AUTH_JWT_AUDIENCE_MISMATCH), "AUTH_JWT_AUDIENCE_MISMATCH")]
    [InlineData(nameof(AuthErrorCodes.AUTH_JWT_CLAIM_MISSING), "AUTH_JWT_CLAIM_MISSING")]
    [InlineData(
        nameof(AuthErrorCodes.AUTH_JWT_ACT_CHAIN_MALFORMED), "AUTH_JWT_ACT_CHAIN_MALFORMED")]
    [InlineData(nameof(AuthErrorCodes.AUTH_JWT_KID_NOT_FOUND), "AUTH_JWT_KID_NOT_FOUND")]
    [InlineData(nameof(AuthErrorCodes.AUTH_JWKS_UNAVAILABLE), "AUTH_JWKS_UNAVAILABLE")]
    [InlineData(nameof(AuthErrorCodes.AUTH_SESSION_REVOKED), "AUTH_SESSION_REVOKED")]
    [InlineData(
        nameof(AuthErrorCodes.AUTH_SESSION_LIVENESS_UNAVAILABLE),
        "AUTH_SESSION_LIVENESS_UNAVAILABLE")]
    [InlineData(nameof(AuthErrorCodes.AUTH_SCOPE_INSUFFICIENT), "AUTH_SCOPE_INSUFFICIENT")]
    [InlineData(
        nameof(AuthErrorCodes.AUTH_REQUEST_ORIGIN_UNESTABLISHED),
        "AUTH_REQUEST_ORIGIN_UNESTABLISHED")]
    public void AuthErrorCode_ConstantValuesPinnedToWireFormat(
        string constantName, string expectedValue)
    {
        var actual = typeof(AuthErrorCodes)
            .GetField(constantName)
            !.GetRawConstantValue();

        actual.Should().Be(expectedValue);
    }

    [Fact]
    public void AllCodes_SetEqualToSpecCodeList()
    {
        var specCodes = LoadSpecCodes();

        AuthErrorCodes.AllCodes.Should().BeEquivalentTo(
            specCodes,
            opt => opt.WithStrictOrdering());
    }

    [Theory]
    [InlineData("AUTH_BEARER_MISSING", 401)]
    [InlineData("AUTH_JWT_EXPIRED", 401)]
    [InlineData("AUTH_JWKS_UNAVAILABLE", 503)]
    [InlineData("AUTH_SESSION_LIVENESS_UNAVAILABLE", 503)]
    [InlineData("AUTH_SCOPE_INSUFFICIENT", 401)]
    [InlineData("AUTH_REQUEST_ORIGIN_UNESTABLISHED", 401)]
    [InlineData("UNKNOWN_NONSENSE_CODE", 500)]
    public void GetHttpStatus_ReturnsExpectedValue(string code, int expected)
    {
        AuthErrorCodes.GetHttpStatus(code).Should().Be(expected);
    }

    [Theory]
    [InlineData("AUTH_BEARER_MISSING", "auth-bearer-missing")]
    [InlineData("AUTH_JWT_ACT_CHAIN_MALFORMED", "auth-jwt-act-chain-malformed")]
    [InlineData("AUTH_SCOPE_INSUFFICIENT", "auth-scope-insufficient")]
    public void KebabCase_ConvertsScreamingSnakeToKebab(string input, string expected)
    {
        AuthErrorCodes.KebabCase(input).Should().Be(expected);
    }

    private static List<string> LoadSpecCodes()
    {
        var path = Path.Combine(
            TestPaths.RepoRoot(),
            "public",
            "contracts",
            "auth-error-codes",
            "auth-error-codes.spec.json");
        var json = File.ReadAllText(path);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement
            .GetProperty("errorCodes")
            .EnumerateArray()
            .Select(e => e.GetProperty("code").GetString()!)
            .ToList();
    }
}
