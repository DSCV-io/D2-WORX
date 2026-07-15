// -----------------------------------------------------------------------
// <copyright file="AuthFailuresTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Auth.Inbound.Errors;

using System.Linq;
using System.Net;
using System.Reflection;
using AwesomeAssertions;
using D2.Shared.Auth.Errors;
using D2.Shared.ErrorCodes.Category;
using D2.Shared.I18n;
using Xunit;

/// <summary>
/// Pins every <see cref="AuthFailures"/> helper — verifies the
/// (status code, error code, TK key) triple. User-facing message is
/// intentionally COARSE (one TK key per status bucket) so an attacker
/// can't deduce which validation step failed; the granular failure mode
/// surfaces only on the machine-readable <c>d2_error_code</c>.
/// </summary>
public sealed class AuthFailuresTests
{
    [Theory]
    [InlineData(nameof(AuthFailures.BearerMissing), AuthErrorCodes.AUTH_BEARER_MISSING)]
    [InlineData(nameof(AuthFailures.BearerMalformed), AuthErrorCodes.AUTH_BEARER_MALFORMED)]
    [InlineData(
        nameof(AuthFailures.JwtSignatureInvalid),
        AuthErrorCodes.AUTH_JWT_SIGNATURE_INVALID)]
    [InlineData(nameof(AuthFailures.JwtExpired), AuthErrorCodes.AUTH_JWT_EXPIRED)]
    [InlineData(nameof(AuthFailures.JwtNotYetValid), AuthErrorCodes.AUTH_JWT_NOT_YET_VALID)]
    [InlineData(
        nameof(AuthFailures.JwtIssuerMismatch),
        AuthErrorCodes.AUTH_JWT_ISSUER_MISMATCH)]
    [InlineData(
        nameof(AuthFailures.JwtAudienceMismatch),
        AuthErrorCodes.AUTH_JWT_AUDIENCE_MISMATCH)]
    [InlineData(nameof(AuthFailures.JwtClaimMissing), AuthErrorCodes.AUTH_JWT_CLAIM_MISSING)]
    [InlineData(
        nameof(AuthFailures.JwtActChainMalformed),
        AuthErrorCodes.AUTH_JWT_ACT_CHAIN_MALFORMED)]
    [InlineData(nameof(AuthFailures.JwtKidNotFound), AuthErrorCodes.AUTH_JWT_KID_NOT_FOUND)]
    [InlineData(nameof(AuthFailures.SessionRevoked), AuthErrorCodes.AUTH_SESSION_REVOKED)]
    [InlineData(
        nameof(AuthFailures.RequestOriginUnestablished),
        AuthErrorCodes.AUTH_REQUEST_ORIGIN_UNESTABLISHED)]
    public void UnauthorizedHelpers_Have401AndExpectedErrorCodeAndUnauthorizedTkKey(
        string methodName,
        string expectedErrorCode)
    {
        // Each delegating factory carries a single optional
        // `IReadOnlyList<TKMessage>? messages = null` parameter; passing an
        // explicit null exercises the default-omitted (spec-TK) path.
        var method = typeof(AuthFailures)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(m => m.Name == methodName
                && !m.IsGenericMethodDefinition
                && m.GetParameters().Length == 1);
        var result = (D2.Shared.Result.D2Result)method.Invoke(null, [null])!;

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        result.ErrorCode.Should().Be(expectedErrorCode);

        result.Messages.Should().ContainSingle()
            .Which.Should().Be(TK.Auth.Errors.UNAUTHORIZED);
    }

    [Theory]
    [InlineData(nameof(AuthFailures.JwksUnavailable), AuthErrorCodes.AUTH_JWKS_UNAVAILABLE)]
    [InlineData(
        nameof(AuthFailures.SessionLivenessUnavailable),
        AuthErrorCodes.AUTH_SESSION_LIVENESS_UNAVAILABLE)]
    public void ServiceUnavailableHelpers_Have503AndExpectedErrorCodeAndTemporarilyUnavailableTkKey(
        string methodName,
        string expectedErrorCode)
    {
        // Both helpers have a generic overload (e.g. JwksUnavailable<T>())
        // with the same single-optional-param arity, so GetMethod(name) is
        // ambiguous. Filter for the non-generic-definition overload explicitly;
        // its lone optional param is the `messages` override (passed null here
        // to exercise the default-omitted path).
        var method = typeof(AuthFailures)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(m => m.Name == methodName
                && !m.IsGenericMethodDefinition
                && m.GetParameters().Length == 1);
        var result = (D2.Shared.Result.D2Result)method.Invoke(null, [null])!;

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        result.ErrorCode.Should().Be(expectedErrorCode);

        result.Messages.Should().ContainSingle()
            .Which.Should().Be(TK.Auth.Errors.TEMPORARILY_UNAVAILABLE);
    }

    [Theory]
    [InlineData(nameof(AuthFailures.BearerMissing), ErrorCategory.ValidationFailure)]
    [InlineData(nameof(AuthFailures.BearerMalformed), ErrorCategory.ValidationFailure)]
    [InlineData(nameof(AuthFailures.JwtSignatureInvalid), ErrorCategory.ValidationFailure)]
    [InlineData(nameof(AuthFailures.JwtExpired), ErrorCategory.ValidationFailure)]
    [InlineData(nameof(AuthFailures.JwtNotYetValid), ErrorCategory.ValidationFailure)]
    [InlineData(nameof(AuthFailures.JwtIssuerMismatch), ErrorCategory.ValidationFailure)]
    [InlineData(nameof(AuthFailures.JwtAudienceMismatch), ErrorCategory.ValidationFailure)]
    [InlineData(nameof(AuthFailures.JwtClaimMissing), ErrorCategory.ValidationFailure)]
    [InlineData(nameof(AuthFailures.JwtActChainMalformed), ErrorCategory.ValidationFailure)]
    [InlineData(nameof(AuthFailures.JwtKidNotFound), ErrorCategory.ValidationFailure)]
    [InlineData(nameof(AuthFailures.SessionRevoked), ErrorCategory.PolicyDenied)]
    [InlineData(nameof(AuthFailures.ScopeInsufficient), ErrorCategory.PolicyDenied)]
    [InlineData(
        nameof(AuthFailures.RequestOriginUnestablished),
        ErrorCategory.PolicyDenied)]
    [InlineData(nameof(AuthFailures.JwksUnavailable), ErrorCategory.InfrastructureUnavailable)]
    [InlineData(
        nameof(AuthFailures.SessionLivenessUnavailable),
        ErrorCategory.InfrastructureUnavailable)]
    public void AuthHelpers_StampTheirOwnCodeCategory_NotTheBaseFactoryDefault(
        string methodName,
        ErrorCategory expectedCategory)
    {
        // The auth code's category OVERRIDES the base factory's default — e.g.
        // BearerMissing delegates to Unauthorized (whose own UNAUTHORIZED code
        // is policy_denied) but stamps validation_failure (its own category).
        // Covers all 15 factories: 10 validation_failure, 3 policy_denied,
        // 2 infrastructure_unavailable.
        var method = typeof(AuthFailures)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(m => m.Name == methodName
                && !m.IsGenericMethodDefinition
                && m.GetParameters().Length == 1);
        var result = (D2.Shared.Result.D2Result)method.Invoke(null, [null])!;

        result.Category.Should().Be(expectedCategory);
    }
}
