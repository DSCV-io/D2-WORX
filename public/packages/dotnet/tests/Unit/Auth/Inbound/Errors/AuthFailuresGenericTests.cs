// -----------------------------------------------------------------------
// <copyright file="AuthFailuresGenericTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Auth.Inbound.Errors;

using System.Net;
using System.Reflection;
using AwesomeAssertions;
using D2.Shared.Auth.Errors;
using D2.Shared.ErrorCodes.Category;
using D2.Shared.I18n;
using D2.Shared.Result;
using Xunit;

/// <summary>
/// Pins the typed twin <see cref="AuthFailures{T}"/> — the generic auth
/// failures class added so callers can produce a typed
/// <see cref="D2Result{T}"/> domain failure (e.g.
/// <c>AuthFailures&lt;Session&gt;.BearerMissing()</c>). Each method must stamp
/// the SAME (status code, error code, TK key) triple as its non-generic
/// <see cref="AuthFailures"/> sibling, with <c>Data</c> defaulted.
/// </summary>
public sealed class AuthFailuresGenericTests
{
    // ------------------------------------------------------------------ //
    // Data-driven Theory covering ALL 15 AuthFailures<T> methods.         //
    // Asserts (StatusCode, ErrorCode, Messages[0]) triple matches the     //
    // non-generic sibling for every method — including the 9 that were    //
    // not covered by the original targeted facts below.                   //
    // ------------------------------------------------------------------ //

    [Theory]
    [InlineData(
        nameof(AuthFailures.BearerMissing),
        AuthErrorCodes.AUTH_BEARER_MISSING,
        (int)HttpStatusCode.Unauthorized,
        false)] // false = UNAUTHORIZED TK
    [InlineData(
        nameof(AuthFailures.BearerMalformed),
        AuthErrorCodes.AUTH_BEARER_MALFORMED,
        (int)HttpStatusCode.Unauthorized,
        false)]
    [InlineData(
        nameof(AuthFailures.JwtSignatureInvalid),
        AuthErrorCodes.AUTH_JWT_SIGNATURE_INVALID,
        (int)HttpStatusCode.Unauthorized,
        false)]
    [InlineData(
        nameof(AuthFailures.JwtExpired),
        AuthErrorCodes.AUTH_JWT_EXPIRED,
        (int)HttpStatusCode.Unauthorized,
        false)]
    [InlineData(
        nameof(AuthFailures.JwtNotYetValid),
        AuthErrorCodes.AUTH_JWT_NOT_YET_VALID,
        (int)HttpStatusCode.Unauthorized,
        false)]
    [InlineData(
        nameof(AuthFailures.JwtIssuerMismatch),
        AuthErrorCodes.AUTH_JWT_ISSUER_MISMATCH,
        (int)HttpStatusCode.Unauthorized,
        false)]
    [InlineData(
        nameof(AuthFailures.JwtAudienceMismatch),
        AuthErrorCodes.AUTH_JWT_AUDIENCE_MISMATCH,
        (int)HttpStatusCode.Unauthorized,
        false)]
    [InlineData(
        nameof(AuthFailures.JwtClaimMissing),
        AuthErrorCodes.AUTH_JWT_CLAIM_MISSING,
        (int)HttpStatusCode.Unauthorized,
        false)]
    [InlineData(
        nameof(AuthFailures.JwtActChainMalformed),
        AuthErrorCodes.AUTH_JWT_ACT_CHAIN_MALFORMED,
        (int)HttpStatusCode.Unauthorized,
        false)]
    [InlineData(
        nameof(AuthFailures.JwtKidNotFound),
        AuthErrorCodes.AUTH_JWT_KID_NOT_FOUND,
        (int)HttpStatusCode.Unauthorized,
        false)]
    [InlineData(
        nameof(AuthFailures.SessionRevoked),
        AuthErrorCodes.AUTH_SESSION_REVOKED,
        (int)HttpStatusCode.Unauthorized,
        false)]
    [InlineData(
        nameof(AuthFailures.ScopeInsufficient),
        AuthErrorCodes.AUTH_SCOPE_INSUFFICIENT,
        (int)HttpStatusCode.Unauthorized,
        false)]
    [InlineData(
        nameof(AuthFailures.RequestOriginUnestablished),
        AuthErrorCodes.AUTH_REQUEST_ORIGIN_UNESTABLISHED,
        (int)HttpStatusCode.Unauthorized,
        false)]
    [InlineData(
        nameof(AuthFailures.JwksUnavailable),
        AuthErrorCodes.AUTH_JWKS_UNAVAILABLE,
        (int)HttpStatusCode.ServiceUnavailable,
        true)] // true = TEMPORARILY_UNAVAILABLE TK
    [InlineData(
        nameof(AuthFailures.SessionLivenessUnavailable),
        AuthErrorCodes.AUTH_SESSION_LIVENESS_UNAVAILABLE,
        (int)HttpStatusCode.ServiceUnavailable,
        true)]
    public void AllMethods_TypedTwin_MatchesNonGenericTriple(
        string methodName,
        string expectedErrorCode,
        int expectedHttpStatus,
        bool expectTemporarilyUnavailableTk)
    {
        // Invoke via reflection on the closed generic type AuthFailures<string>.
        // Each factory carries one optional `messages` override param; passing
        // null exercises the default-omitted (spec-TK) path.
        var genericType = typeof(AuthFailures<string>);
        var method = genericType
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(m => m.Name == methodName && m.GetParameters().Length == 1);
        var typed = (D2Result<string>)method.Invoke(null, [null])!;

        // Non-generic sibling — filter out any generic-definition overloads
        // (JwksUnavailable / SessionLivenessUnavailable have a non-generic AND
        // a generic<T> overload on AuthFailures; pick the non-generic one).
        var nonGenericMethod = typeof(AuthFailures)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(m => m.Name == methodName
                && !m.IsGenericMethodDefinition
                && m.GetParameters().Length == 1);
        var nonGeneric = (D2Result)nonGenericMethod.Invoke(null, [null])!;

        // (StatusCode, ErrorCode, Messages[0]) triple.
        typed.Success.Should().BeFalse();
        typed.StatusCode.Should().Be((HttpStatusCode)expectedHttpStatus);
        typed.ErrorCode.Should().Be(expectedErrorCode);

        var expectedTk = expectTemporarilyUnavailableTk
            ? TK.Auth.Errors.TEMPORARILY_UNAVAILABLE
            : TK.Auth.Errors.UNAUTHORIZED;
        typed.Messages.Should().ContainSingle().Which.Should().Be(expectedTk);

        // Same triple as the non-generic sibling.
        typed.StatusCode.Should().Be(nonGeneric.StatusCode);
        typed.ErrorCode.Should().Be(nonGeneric.ErrorCode);
        typed.Messages.Should().BeEquivalentTo(nonGeneric.Messages);

        // Data must be defaulted (null for reference type T = string).
        typed.Data.Should().BeNull();
    }

    // ------------------------------------------------------------------ //
    // Targeted facts kept for documentation clarity.                      //
    // ------------------------------------------------------------------ //

    [Fact]
    public void BearerMissing_TypedTwin_MatchesNonGenericTripleWithDefaultData()
    {
        var typed = AuthFailures<Session>.BearerMissing();
        var nonGeneric = AuthFailures.BearerMissing();

        typed.Success.Should().BeFalse();
        typed.Data.Should().BeNull();
        typed.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        typed.ErrorCode.Should().Be(AuthErrorCodes.AUTH_BEARER_MISSING);
        typed.Messages.Should().ContainSingle().Which.Should().Be(TK.Auth.Errors.UNAUTHORIZED);

        // Same triple as the non-generic sibling.
        typed.StatusCode.Should().Be(nonGeneric.StatusCode);
        typed.ErrorCode.Should().Be(nonGeneric.ErrorCode);
    }

    [Fact]
    public void ScopeInsufficient_TypedTwin_Surfaces401NotForbidden()
    {
        var typed = AuthFailures<Session>.ScopeInsufficient();

        typed.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        typed.ErrorCode.Should().Be(AuthErrorCodes.AUTH_SCOPE_INSUFFICIENT);
        typed.Data.Should().BeNull();
    }

    [Fact]
    public void JwksUnavailable_TypedTwin_Surfaces503WithTemporarilyUnavailableTk()
    {
        var typed = AuthFailures<Session>.JwksUnavailable();

        typed.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        typed.ErrorCode.Should().Be(AuthErrorCodes.AUTH_JWKS_UNAVAILABLE);
        typed.Messages.Should().ContainSingle()
            .Which.Should().Be(TK.Auth.Errors.TEMPORARILY_UNAVAILABLE);
    }

    [Fact]
    public void SessionLivenessUnavailable_TypedTwin_Surfaces503()
    {
        var typed = AuthFailures<int>.SessionLivenessUnavailable();

        typed.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        typed.ErrorCode.Should().Be(AuthErrorCodes.AUTH_SESSION_LIVENESS_UNAVAILABLE);
        typed.Data.Should().Be(0);
    }

    [Fact]
    public void TypedTwin_IsDistinctTypeFromNonGeneric_BothCoexist()
    {
        // AuthFailures (non-generic) and AuthFailures<T> (generic) are distinct
        // types — both resolve. A value-type T defaults to its zero value.
        D2Result nonGeneric = AuthFailures.BearerMalformed();
        D2Result<string> typed = AuthFailures<string>.BearerMalformed();

        nonGeneric.ErrorCode.Should().Be(AuthErrorCodes.AUTH_BEARER_MALFORMED);
        typed.ErrorCode.Should().Be(AuthErrorCodes.AUTH_BEARER_MALFORMED);
        typed.Data.Should().BeNull();
    }

    // ------------------------------------------------------------------ //
    // Category Theory — ALL 15 AuthFailures<T> methods stamp their own   //
    // code's category, overriding the base factory's default.             //
    // ------------------------------------------------------------------ //

    [Theory]
    [InlineData(
        nameof(AuthFailures.BearerMissing), ErrorCategory.ValidationFailure)]
    [InlineData(
        nameof(AuthFailures.BearerMalformed), ErrorCategory.ValidationFailure)]
    [InlineData(
        nameof(AuthFailures.JwtSignatureInvalid), ErrorCategory.ValidationFailure)]
    [InlineData(
        nameof(AuthFailures.JwtExpired), ErrorCategory.ValidationFailure)]
    [InlineData(
        nameof(AuthFailures.JwtNotYetValid), ErrorCategory.ValidationFailure)]
    [InlineData(
        nameof(AuthFailures.JwtIssuerMismatch), ErrorCategory.ValidationFailure)]
    [InlineData(
        nameof(AuthFailures.JwtAudienceMismatch), ErrorCategory.ValidationFailure)]
    [InlineData(
        nameof(AuthFailures.JwtClaimMissing), ErrorCategory.ValidationFailure)]
    [InlineData(
        nameof(AuthFailures.JwtActChainMalformed), ErrorCategory.ValidationFailure)]
    [InlineData(
        nameof(AuthFailures.JwtKidNotFound), ErrorCategory.ValidationFailure)]
    [InlineData(
        nameof(AuthFailures.SessionRevoked), ErrorCategory.PolicyDenied)]
    [InlineData(
        nameof(AuthFailures.ScopeInsufficient), ErrorCategory.PolicyDenied)]
    [InlineData(
        nameof(AuthFailures.RequestOriginUnestablished),
        ErrorCategory.PolicyDenied)]
    [InlineData(
        nameof(AuthFailures.JwksUnavailable), ErrorCategory.InfrastructureUnavailable)]
    [InlineData(
        nameof(AuthFailures.SessionLivenessUnavailable),
        ErrorCategory.InfrastructureUnavailable)]
    public void AllMethods_TypedTwin_StampsSameCategory(
        string methodName,
        ErrorCategory expectedCategory)
    {
        // The typed twin must stamp the IDENTICAL category as the non-generic
        // sibling — both are generated from the same spec entry. The lone param
        // is the optional `messages` override (passed null for the default path).
        var genericType = typeof(AuthFailures<string>);
        var method = genericType
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(m => m.Name == methodName && m.GetParameters().Length == 1);
        var typed = (D2Result<string>)method.Invoke(null, [null])!;

        typed.Category.Should().Be(expectedCategory);
    }

    private sealed record Session;
}
