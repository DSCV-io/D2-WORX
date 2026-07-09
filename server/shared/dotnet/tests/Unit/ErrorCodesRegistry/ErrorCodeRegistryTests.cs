// -----------------------------------------------------------------------
// <copyright file="ErrorCodeRegistryTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.ErrorCodesRegistry;

using AwesomeAssertions;
using D2.Shared.ErrorCodes.Category;
using D2.Shared.ErrorCodes.Registry;
using D2.Shared.I18n;
using Xunit;

/// <summary>
/// Unit tests for <see cref="ErrorCodeRegistry"/> — the merged cross-catalog
/// resolution API. Covers every public path: <see cref="ErrorCodeRegistry.TryResolve"/>,
/// <see cref="ErrorCodeRegistry.Resolve"/>, and <see cref="ErrorCodeRegistry.All"/>,
/// plus adversarial inputs (unknown codes, null, empty, wrong-case) and full-record
/// fidelity assertions for representative codes from both catalogs.
/// </summary>
public sealed class ErrorCodeRegistryTests
{
    // -----------------------------------------------------------------------
    // TryResolve — unknown / adversarial inputs
    // -----------------------------------------------------------------------

    [Fact]
    public void TryResolve_UnknownCode_ReturnsFalse()
    {
        var found = ErrorCodeRegistry.TryResolve("NOPE", out _);
        found.Should().BeFalse();
    }

    [Fact]
    public void TryResolve_NullCode_ReturnsFalse()
    {
        var found = ErrorCodeRegistry.TryResolve(null, out _);
        found.Should().BeFalse();
    }

    [Fact]
    public void TryResolve_EmptyCode_ReturnsFalse()
    {
        var found = ErrorCodeRegistry.TryResolve(string.Empty, out _);
        found.Should().BeFalse();
    }

    [Fact]
    public void TryResolve_LowercaseCode_ReturnsFalse()
    {
        // Codes are ordinal case-sensitive SCREAMING_SNAKE — lowercase is unknown.
        var found = ErrorCodeRegistry.TryResolve("not_found", out _);
        found.Should().BeFalse();
    }

    [Fact]
    public void TryResolve_MixedCaseCode_ReturnsFalse()
    {
        var found = ErrorCodeRegistry.TryResolve("Not_Found", out _);
        found.Should().BeFalse();
    }

    [Fact]
    public void TryResolve_WhitespaceCode_ReturnsFalse()
    {
        var found = ErrorCodeRegistry.TryResolve("   ", out _);
        found.Should().BeFalse();
    }

    [Fact]
    public void TryResolve_KnownCodeWithTrailingSpace_ReturnsFalse()
    {
        // Exact match only — trailing whitespace = unknown.
        var found = ErrorCodeRegistry.TryResolve("NOT_FOUND ", out _);
        found.Should().BeFalse();
    }

    // -----------------------------------------------------------------------
    // TryResolve — known generic-catalog codes
    // -----------------------------------------------------------------------

    [Fact]
    public void TryResolve_NotFound_ReturnsTrue_WithCorrectFields()
    {
        var found = ErrorCodeRegistry.TryResolve("NOT_FOUND", out var info);

        found.Should().BeTrue();
        info.Code.Should().Be("NOT_FOUND");
        info.HttpStatus.Should().Be(404);
        info.Category.Should().Be(ErrorCategory.NotFound);
        info.Category.ToWire().Should().Be("not_found");
        info.UserMessageKey.Key.Should().Be("common_errors_NOT_FOUND");
        info.FactoryName.Should().Be("NotFound");
        info.FactoryShape.Should().Be("standard");
        info.Domain.Should().Be("common");
    }

    // The unified factory-shape rename: every non-`none` registry entry now
    // carries the universal `standard` shape value (the `with_error_code` +
    // `validation` shapes folded in).

    [Fact]
    public void TryResolve_Forbidden_ReturnsTrue_WithCorrectFields()
    {
        var found = ErrorCodeRegistry.TryResolve("FORBIDDEN", out var info);

        found.Should().BeTrue();
        info.Code.Should().Be("FORBIDDEN");
        info.HttpStatus.Should().Be(403);
        info.Category.Should().Be(ErrorCategory.PolicyDenied);
        info.Category.ToWire().Should().Be("policy_denied");
        info.FactoryShape.Should().Be("standard");
        info.Domain.Should().Be("common");
    }

    [Fact]
    public void TryResolve_ValidationFailed_ReturnsTrue_WithStandardShape()
    {
        var found = ErrorCodeRegistry.TryResolve("VALIDATION_FAILED", out var info);

        found.Should().BeTrue();
        info.Category.Should().Be(ErrorCategory.ValidationFailure);
        info.FactoryShape.Should().Be("standard");
        info.Domain.Should().Be("common");
    }

    [Fact]
    public void TryResolve_Canceled_ReturnsTrue_WithCorrectFields()
    {
        var found = ErrorCodeRegistry.TryResolve("CANCELED", out var info);

        found.Should().BeTrue();
        info.HttpStatus.Should().Be(400);
        info.Category.Should().Be(ErrorCategory.ValidationFailure);
        info.Domain.Should().Be("common");
    }

    [Fact]
    public void TryResolve_SomeFound_ReturnsTrue_PartialSuccessCategory()
    {
        var found = ErrorCodeRegistry.TryResolve("SOME_FOUND", out var info);

        found.Should().BeTrue();
        info.HttpStatus.Should().Be(206);
        info.Category.Should().Be(ErrorCategory.PartialSuccess);
        info.FactoryShape.Should().Be("none");
        info.Domain.Should().Be("common");
    }

    [Fact]
    public void TryResolve_RateLimited_ReturnsTrue_RateLimitedCategory()
    {
        var found = ErrorCodeRegistry.TryResolve("RATE_LIMITED", out var info);

        found.Should().BeTrue();
        info.HttpStatus.Should().Be(429);
        info.Category.Should().Be(ErrorCategory.RateLimited);
        info.Domain.Should().Be("common");
    }

    [Fact]
    public void TryResolve_ServiceUnavailable_ReturnsTrue_InfrastructureCategory()
    {
        var found = ErrorCodeRegistry.TryResolve("SERVICE_UNAVAILABLE", out var info);

        found.Should().BeTrue();
        info.HttpStatus.Should().Be(503);
        info.Category.Should().Be(ErrorCategory.InfrastructureUnavailable);
        info.Domain.Should().Be("common");
    }

    [Fact]
    public void TryResolve_PayloadTooLarge_ReturnsTrue_PayloadTooLargeCategory()
    {
        var found = ErrorCodeRegistry.TryResolve("PAYLOAD_TOO_LARGE", out var info);

        found.Should().BeTrue();
        info.HttpStatus.Should().Be(413);
        info.Category.Should().Be(ErrorCategory.PayloadTooLarge);
        info.Domain.Should().Be("common");
    }

    // -----------------------------------------------------------------------
    // TryResolve — known auth-catalog codes
    // -----------------------------------------------------------------------

    [Fact]
    public void TryResolve_AuthBearerMissing_ReturnsTrue_WithCorrectFields()
    {
        var found = ErrorCodeRegistry.TryResolve("AUTH_BEARER_MISSING", out var info);

        found.Should().BeTrue();
        info.Code.Should().Be("AUTH_BEARER_MISSING");
        info.HttpStatus.Should().Be(401);
        info.Category.Should().Be(ErrorCategory.ValidationFailure);
        info.Category.ToWire().Should().Be("validation_failure");
        info.UserMessageKey.Key.Should().Be("auth_errors_UNAUTHORIZED");
        info.FactoryName.Should().Be("BearerMissing");
        info.FactoryShape.Should().Be("standard");
        info.Domain.Should().Be("auth");
    }

    [Fact]
    public void TryResolve_AuthJwksUnavailable_ReturnsTrue_InfrastructureCategory()
    {
        var found = ErrorCodeRegistry.TryResolve("AUTH_JWKS_UNAVAILABLE", out var info);

        found.Should().BeTrue();
        info.HttpStatus.Should().Be(503);
        info.Category.Should().Be(ErrorCategory.InfrastructureUnavailable);
        info.UserMessageKey.Key.Should().Be("auth_errors_TEMPORARILY_UNAVAILABLE");
        info.Domain.Should().Be("auth");
    }

    [Fact]
    public void TryResolve_AuthSessionRevoked_ReturnsTrue_PolicyDeniedCategory()
    {
        var found = ErrorCodeRegistry.TryResolve("AUTH_SESSION_REVOKED", out var info);

        found.Should().BeTrue();
        info.Category.Should().Be(ErrorCategory.PolicyDenied);
        info.Domain.Should().Be("auth");
    }

    [Fact]
    public void TryResolve_AuthScopeInsufficient_ReturnsTrue_PolicyDeniedCategory()
    {
        var found = ErrorCodeRegistry.TryResolve("AUTH_SCOPE_INSUFFICIENT", out var info);

        found.Should().BeTrue();
        info.Category.Should().Be(ErrorCategory.PolicyDenied);
        info.Domain.Should().Be("auth");
    }

    // -----------------------------------------------------------------------
    // Resolve — nullable convenience wrapper
    // -----------------------------------------------------------------------

    [Fact]
    public void Resolve_KnownCode_ReturnsNonNull()
    {
        var result = ErrorCodeRegistry.Resolve("NOT_FOUND");
        result.Should().NotBeNull();
        result.Value.Code.Should().Be("NOT_FOUND");
    }

    [Fact]
    public void Resolve_UnknownCode_ReturnsNull()
    {
        var result = ErrorCodeRegistry.Resolve("ABSOLUTELY_UNKNOWN");
        result.Should().BeNull();
    }

    [Fact]
    public void Resolve_NullCode_ReturnsNull()
    {
        var result = ErrorCodeRegistry.Resolve(null);
        result.Should().BeNull();
    }

    [Fact]
    public void Resolve_EmptyCode_ReturnsNull()
    {
        var result = ErrorCodeRegistry.Resolve(string.Empty);
        result.Should().BeNull();
    }

    // -----------------------------------------------------------------------
    // All — completeness assertions
    // -----------------------------------------------------------------------

    [Fact]
    public void All_Count_EqualsExpectedTotalSpecCodes()
    {
        // The merged registry globs every *-error-codes.spec.json surfaced to the
        // D2.Shared.ErrorCodes.Registry build (its csproj AdditionalFiles pattern
        // is contracts/**/*-error-codes.spec.json): 15 generic + 14 auth +
        // 30 keycustodian = 59 total. Update if new spec entries are added.
        const int expected_count = 59;
        ErrorCodeRegistry.All.Count.Should().Be(expected_count);
    }

    [Fact]
    public void All_ContainsEveryGenericCode()
    {
        var codes = ErrorCodeRegistry.All;
        codes.Should().Contain(i => i.Code == "NOT_FOUND");
        codes.Should().Contain(i => i.Code == "FORBIDDEN");
        codes.Should().Contain(i => i.Code == "UNAUTHORIZED");
        codes.Should().Contain(i => i.Code == "VALIDATION_FAILED");
        codes.Should().Contain(i => i.Code == "CONFLICT");
        codes.Should().Contain(i => i.Code == "UNHANDLED_EXCEPTION");
        codes.Should().Contain(i => i.Code == "COULD_NOT_BE_SERIALIZED");
        codes.Should().Contain(i => i.Code == "COULD_NOT_BE_DESERIALIZED");
        codes.Should().Contain(i => i.Code == "SERVICE_UNAVAILABLE");
        codes.Should().Contain(i => i.Code == "SOME_FOUND");
        codes.Should().Contain(i => i.Code == "PARTIAL_SUCCESS");
        codes.Should().Contain(i => i.Code == "RATE_LIMITED");
        codes.Should().Contain(i => i.Code == "IDEMPOTENCY_IN_FLIGHT");
        codes.Should().Contain(i => i.Code == "PAYLOAD_TOO_LARGE");
        codes.Should().Contain(i => i.Code == "CANCELED");
    }

    [Fact]
    public void All_ContainsEveryAuthCode()
    {
        var codes = ErrorCodeRegistry.All;
        codes.Should().Contain(i => i.Code == "AUTH_BEARER_MISSING");
        codes.Should().Contain(i => i.Code == "AUTH_BEARER_MALFORMED");
        codes.Should().Contain(i => i.Code == "AUTH_JWT_SIGNATURE_INVALID");
        codes.Should().Contain(i => i.Code == "AUTH_JWT_EXPIRED");
        codes.Should().Contain(i => i.Code == "AUTH_JWT_NOT_YET_VALID");
        codes.Should().Contain(i => i.Code == "AUTH_JWT_ISSUER_MISMATCH");
        codes.Should().Contain(i => i.Code == "AUTH_JWT_AUDIENCE_MISMATCH");
        codes.Should().Contain(i => i.Code == "AUTH_JWT_CLAIM_MISSING");
        codes.Should().Contain(i => i.Code == "AUTH_JWT_ACT_CHAIN_MALFORMED");
        codes.Should().Contain(i => i.Code == "AUTH_JWT_KID_NOT_FOUND");
        codes.Should().Contain(i => i.Code == "AUTH_JWKS_UNAVAILABLE");
        codes.Should().Contain(i => i.Code == "AUTH_SESSION_REVOKED");
        codes.Should().Contain(i => i.Code == "AUTH_SESSION_LIVENESS_UNAVAILABLE");
        codes.Should().Contain(i => i.Code == "AUTH_SCOPE_INSUFFICIENT");
    }

    [Fact]
    public void All_ContainsEveryKeyCustodianCode()
    {
        var codes = ErrorCodeRegistry.All;
        codes.Should().Contain(i => i.Code == "KEYCUSTODIAN_KID_INVALID");
        codes.Should().Contain(i => i.Code == "KEYCUSTODIAN_KID_TOO_LONG");
        codes.Should().Contain(i => i.Code == "KEYCUSTODIAN_UNKNOWN_KEY_DOMAIN");
        codes.Should().Contain(i => i.Code == "KEYCUSTODIAN_INVALID_ROTATION_POLICY");
        codes.Should().Contain(i => i.Code == "KEYCUSTODIAN_SOAK_NOT_ELAPSED");
        codes.Should().Contain(i => i.Code == "KEYCUSTODIAN_SMOKE_PROOF_TYPE_MISMATCH");
        codes.Should().Contain(i => i.Code == "KEYCUSTODIAN_GRACE_NOT_ELAPSED");
        codes.Should().Contain(i => i.Code == "KEYCUSTODIAN_PRECONDITION_VIOLATED");
        codes.Should().Contain(i => i.Code == "KEYCUSTODIAN_KEY_NOT_FOUND");
        codes.Should().Contain(i => i.Code == "KEYCUSTODIAN_KEY_STATE_CONFLICT");
        codes.Should().Contain(i => i.Code == "KEYCUSTODIAN_PENDING_KEY_ALREADY_EXISTS");
        codes.Should().Contain(i => i.Code == "KEYCUSTODIAN_SMOKE_TEST_FAILED");
        codes.Should().Contain(i => i.Code == "KEYCUSTODIAN_INVALID_WORKLOAD_IDENTITY");
        codes.Should().Contain(i => i.Code == "KEYCUSTODIAN_INVALID_CERTIFICATE_REQUEST");
        codes.Should().Contain(i => i.Code == "KEYCUSTODIAN_NO_ACTIVE_ISSUING_CA");
        codes.Should().Contain(i => i.Code == "KEYCUSTODIAN_CROSS_PROCESS_DOMAIN_REJECTED");
        codes.Should().Contain(i => i.Code == "KEYCUSTODIAN_SIGNING_DOMAIN_NOT_AUTHORIZED");
        codes.Should().Contain(i => i.Code == "KEYCUSTODIAN_REQUEST_ORIGIN_UNESTABLISHED");
        codes.Should().Contain(i => i.Code == "KEYCUSTODIAN_MINTER_CAPABILITY_REQUIRED");
    }

    [Fact]
    public void TryResolve_KeyCustodianPreconditionViolated_ReturnsInternalError()
    {
        var found = ErrorCodeRegistry.TryResolve("KEYCUSTODIAN_PRECONDITION_VIOLATED", out var info);

        found.Should().BeTrue();
        info.Code.Should().Be("KEYCUSTODIAN_PRECONDITION_VIOLATED");
        info.HttpStatus.Should().Be(500);
        info.Category.Should().Be(ErrorCategory.InternalError);
        info.Category.ToWire().Should().Be("internal_error");
        info.FactoryName.Should().Be("PreconditionViolated");
        info.FactoryShape.Should().Be("standard");
        info.UserMessageKey.Key.Should().Be("keycustodian_internal_PRECONDITION_VIOLATED");
        info.Domain.Should().Be("keycustodian");
    }

    [Fact]
    public void TryResolve_KeyCustodianKeyNotFound_ReturnsNotFound()
    {
        var found = ErrorCodeRegistry.TryResolve("KEYCUSTODIAN_KEY_NOT_FOUND", out var info);

        found.Should().BeTrue();
        info.Code.Should().Be("KEYCUSTODIAN_KEY_NOT_FOUND");
        info.HttpStatus.Should().Be(404);
        info.Category.Should().Be(ErrorCategory.NotFound);
        info.Category.ToWire().Should().Be("not_found");
        info.FactoryName.Should().Be("KeyNotFound");
        info.FactoryShape.Should().Be("standard");
        info.UserMessageKey.Key.Should().Be("keycustodian_lifecycle_KEY_NOT_FOUND");
        info.Domain.Should().Be("keycustodian");
    }

    [Fact]
    public void TryResolve_KeyCustodianKeyStateConflict_ReturnsConflict()
    {
        var found = ErrorCodeRegistry.TryResolve("KEYCUSTODIAN_KEY_STATE_CONFLICT", out var info);

        found.Should().BeTrue();
        info.Code.Should().Be("KEYCUSTODIAN_KEY_STATE_CONFLICT");
        info.HttpStatus.Should().Be(409);
        info.Category.Should().Be(ErrorCategory.Conflict);
        info.Category.ToWire().Should().Be("conflict");
        info.FactoryName.Should().Be("KeyStateConflict");
        info.FactoryShape.Should().Be("standard");
        info.UserMessageKey.Key.Should().Be("keycustodian_lifecycle_KEY_STATE_CONFLICT");
        info.Domain.Should().Be("keycustodian");
    }

    [Fact]
    public void All_AllCodes_HaveNonEmptyRequiredFields()
    {
        foreach (var info in ErrorCodeRegistry.All)
        {
            info.Code.Should().NotBeNullOrWhiteSpace(
                because: $"code at entry {info.Code} must be non-empty");
            info.Category.ToWire().Should().NotBeNullOrWhiteSpace(
                because: $"Category.ToWire() at {info.Code} must be non-empty");
            info.UserMessageKey.Key.Should().NotBeNullOrWhiteSpace(
                because: $"UserMessageKey at {info.Code} must be non-empty");
            info.FactoryName.Should().NotBeNullOrWhiteSpace(
                because: $"FactoryName at {info.Code} must be non-empty");
            info.FactoryShape.Should().NotBeNullOrWhiteSpace(
                because: $"FactoryShape at {info.Code} must be non-empty");
            info.Doc.Should().NotBeNullOrWhiteSpace(
                because: $"Doc at {info.Code} must be non-empty");
            info.Domain.Should().NotBeNullOrWhiteSpace(
                because: $"Domain at {info.Code} must be non-empty");
        }
    }

    [Fact]
    public void All_GenericCodes_HaveDomainCommon()
    {
        var genericCodes = new[]
        {
            "NOT_FOUND", "FORBIDDEN", "UNAUTHORIZED", "VALIDATION_FAILED",
            "CONFLICT", "UNHANDLED_EXCEPTION", "COULD_NOT_BE_SERIALIZED",
            "COULD_NOT_BE_DESERIALIZED", "SERVICE_UNAVAILABLE", "SOME_FOUND",
            "PARTIAL_SUCCESS", "RATE_LIMITED", "IDEMPOTENCY_IN_FLIGHT",
            "PAYLOAD_TOO_LARGE", "CANCELED",
        };
        foreach (var code in genericCodes)
        {
            var found = ErrorCodeRegistry.TryResolve(code, out var info);
            found.Should().BeTrue(because: $"generic code '{code}' should be registered");
            info.Domain.Should().Be(
                "common",
                because: $"generic code '{code}' must have domain 'common'");
        }
    }

    [Fact]
    public void All_AuthCodes_HaveDomainAuth()
    {
        foreach (var info in ErrorCodeRegistry.All)
        {
            if (info.Code.StartsWith("AUTH_", StringComparison.Ordinal))
                info.Domain.Should().Be("auth", because: $"'{info.Code}' must have domain 'auth'");
        }
    }

    // -----------------------------------------------------------------------
    // ErrorCategory enum — all 9 schema values covered
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(ErrorCategory.ValidationFailure, "validation_failure")]
    [InlineData(ErrorCategory.NotFound, "not_found")]
    [InlineData(ErrorCategory.Conflict, "conflict")]
    [InlineData(ErrorCategory.PolicyDenied, "policy_denied")]
    [InlineData(ErrorCategory.RateLimited, "rate_limited")]
    [InlineData(ErrorCategory.PayloadTooLarge, "payload_too_large")]
    [InlineData(ErrorCategory.InfrastructureUnavailable, "infrastructure_unavailable")]
    [InlineData(ErrorCategory.InternalError, "internal_error")]
    [InlineData(ErrorCategory.PartialSuccess, "partial_success")]
    public void ErrorCategory_AllSchemaValues_PresentInRegistry(
        ErrorCategory category, string wireString)
    {
        // At least one code with this category must exist in All.
        ErrorCodeRegistry.All
            .Should().Contain(
                i => i.Category == category,
                because: $"registry must contain at least one code with category '{wireString}'");

        // Every entry with this category should have the matching wire string.
        foreach (var info in ErrorCodeRegistry.All)
        {
            if (info.Category == category)
                info.Category.ToWire().Should().Be(wireString);
        }
    }

    // -----------------------------------------------------------------------
    // UserMessageKey — typed TKMessage, not raw string
    // -----------------------------------------------------------------------

    [Fact]
    public void TryResolve_NotFound_UserMessageKey_IsTypedTkMessage()
    {
        ErrorCodeRegistry.TryResolve("NOT_FOUND", out var info);

        // The UserMessageKey must be the exact same object reference as
        // TK.Common.Errors.NOT_FOUND (generated static readonly field).
        info.UserMessageKey.Should().BeSameAs(TK.Common.Errors.NOT_FOUND);
    }

    [Fact]
    public void TryResolve_AuthBearerMissing_UserMessageKey_IsTypedTkMessage()
    {
        ErrorCodeRegistry.TryResolve("AUTH_BEARER_MISSING", out var info);
        info.UserMessageKey.Should().BeSameAs(TK.Auth.Errors.UNAUTHORIZED);
    }
}
