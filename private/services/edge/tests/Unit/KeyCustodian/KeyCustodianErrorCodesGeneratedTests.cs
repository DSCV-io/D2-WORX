// -----------------------------------------------------------------------
// <copyright file="KeyCustodianErrorCodesGeneratedTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian;

using D2.Private.I18n;

/// <summary>
/// First-pass coverage for the generated <see cref="KeyCustodianErrorCodes"/>
/// constants class and the non-generic <see cref="KeyCustodianFailures"/> factory
/// class. Pins constant wire-string values, <c>AllCodes</c> membership,
/// <c>GetHttpStatus</c> return values, and non-generic factory result shape.
/// </summary>
public sealed class KeyCustodianErrorCodesGeneratedTests
{
    // -----------------------------------------------------------------------
    // Constant value pins â€” each constant equals its exact wire literal
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(KeyCustodianErrorCodes.KEYCUSTODIAN_KID_INVALID, "KEYCUSTODIAN_KID_INVALID")]
    [InlineData(KeyCustodianErrorCodes.KEYCUSTODIAN_KID_TOO_LONG, "KEYCUSTODIAN_KID_TOO_LONG")]
    [InlineData(
        KeyCustodianErrorCodes.KEYCUSTODIAN_UNKNOWN_KEY_DOMAIN,
        "KEYCUSTODIAN_UNKNOWN_KEY_DOMAIN")]
    [InlineData(
        KeyCustodianErrorCodes.KEYCUSTODIAN_INVALID_ROTATION_POLICY,
        "KEYCUSTODIAN_INVALID_ROTATION_POLICY")]
    [InlineData(
        KeyCustodianErrorCodes.KEYCUSTODIAN_SOAK_NOT_ELAPSED,
        "KEYCUSTODIAN_SOAK_NOT_ELAPSED")]
    [InlineData(
        KeyCustodianErrorCodes.KEYCUSTODIAN_SMOKE_PROOF_TYPE_MISMATCH,
        "KEYCUSTODIAN_SMOKE_PROOF_TYPE_MISMATCH")]
    [InlineData(
        KeyCustodianErrorCodes.KEYCUSTODIAN_GRACE_NOT_ELAPSED,
        "KEYCUSTODIAN_GRACE_NOT_ELAPSED")]
    [InlineData(
        KeyCustodianErrorCodes.KEYCUSTODIAN_PRECONDITION_VIOLATED,
        "KEYCUSTODIAN_PRECONDITION_VIOLATED")]
    [InlineData(KeyCustodianErrorCodes.KEYCUSTODIAN_KEY_NOT_FOUND, "KEYCUSTODIAN_KEY_NOT_FOUND")]
    [InlineData(
        KeyCustodianErrorCodes.KEYCUSTODIAN_KEY_STATE_CONFLICT,
        "KEYCUSTODIAN_KEY_STATE_CONFLICT")]
    [InlineData(
        KeyCustodianErrorCodes.KEYCUSTODIAN_PENDING_KEY_ALREADY_EXISTS,
        "KEYCUSTODIAN_PENDING_KEY_ALREADY_EXISTS")]
    [InlineData(
        KeyCustodianErrorCodes.KEYCUSTODIAN_SMOKE_TEST_FAILED,
        "KEYCUSTODIAN_SMOKE_TEST_FAILED")]
    [InlineData(
        KeyCustodianErrorCodes.KEYCUSTODIAN_INVALID_WORKLOAD_IDENTITY,
        "KEYCUSTODIAN_INVALID_WORKLOAD_IDENTITY")]
    [InlineData(
        KeyCustodianErrorCodes.KEYCUSTODIAN_INVALID_CERTIFICATE_REQUEST,
        "KEYCUSTODIAN_INVALID_CERTIFICATE_REQUEST")]
    [InlineData(
        KeyCustodianErrorCodes.KEYCUSTODIAN_NO_ACTIVE_ISSUING_CA,
        "KEYCUSTODIAN_NO_ACTIVE_ISSUING_CA")]
    [InlineData(
        KeyCustodianErrorCodes.KEYCUSTODIAN_CROSS_PROCESS_DOMAIN_REJECTED,
        "KEYCUSTODIAN_CROSS_PROCESS_DOMAIN_REJECTED")]
    [InlineData(
        KeyCustodianErrorCodes.KEYCUSTODIAN_SIGNING_DOMAIN_NOT_AUTHORIZED,
        "KEYCUSTODIAN_SIGNING_DOMAIN_NOT_AUTHORIZED")]
    [InlineData(
        KeyCustodianErrorCodes.KEYCUSTODIAN_REQUEST_ORIGIN_UNESTABLISHED,
        "KEYCUSTODIAN_REQUEST_ORIGIN_UNESTABLISHED")]
    [InlineData(
        KeyCustodianErrorCodes.KEYCUSTODIAN_MINTER_CAPABILITY_REQUIRED,
        "KEYCUSTODIAN_MINTER_CAPABILITY_REQUIRED")]
    [InlineData(
        KeyCustodianErrorCodes.KEYCUSTODIAN_SIGNING_KEY_UNAVAILABLE,
        "KEYCUSTODIAN_SIGNING_KEY_UNAVAILABLE")]
    [InlineData(
        KeyCustodianErrorCodes.KEYCUSTODIAN_EMPTY_SIGNING_INPUT,
        "KEYCUSTODIAN_EMPTY_SIGNING_INPUT")]
    [InlineData(
        KeyCustodianErrorCodes.KEYCUSTODIAN_KEY_TYPE_DOMAIN_MISMATCH,
        "KEYCUSTODIAN_KEY_TYPE_DOMAIN_MISMATCH")]
    [InlineData(
        KeyCustodianErrorCodes.KEYCUSTODIAN_SIGNING_INPUT_TOO_LARGE,
        "KEYCUSTODIAN_SIGNING_INPUT_TOO_LARGE")]
    [InlineData(
        KeyCustodianErrorCodes.KEYCUSTODIAN_ISSUANCE_NOT_AUTHORIZED,
        "KEYCUSTODIAN_ISSUANCE_NOT_AUTHORIZED")]
    [InlineData(
        KeyCustodianErrorCodes.KEYCUSTODIAN_INVALID_CSR,
        "KEYCUSTODIAN_INVALID_CSR")]
    [InlineData(
        KeyCustodianErrorCodes.KEYCUSTODIAN_CA_CERTIFICATE_NOT_AUTHORIZED,
        "KEYCUSTODIAN_CA_CERTIFICATE_NOT_AUTHORIZED")]
    public void Constant_ValueEqualsWireLiteral(string constant, string expected_wire_literal)
    {
        constant.Should().Be(expected_wire_literal);
    }

    // -----------------------------------------------------------------------
    // AllCodes membership â€” set equals the 30 spec codes in spec order
    // -----------------------------------------------------------------------

    [Fact]
    public void AllCodes_SetEqualToSpecCodeList()
    {
        string[] expectedCodes =
        [
            "KEYCUSTODIAN_KID_INVALID",
            "KEYCUSTODIAN_KID_TOO_LONG",
            "KEYCUSTODIAN_UNKNOWN_KEY_DOMAIN",
            "KEYCUSTODIAN_INVALID_ROTATION_POLICY",
            "KEYCUSTODIAN_SOAK_NOT_ELAPSED",
            "KEYCUSTODIAN_SMOKE_PROOF_TYPE_MISMATCH",
            "KEYCUSTODIAN_GRACE_NOT_ELAPSED",
            "KEYCUSTODIAN_PRECONDITION_VIOLATED",
            "KEYCUSTODIAN_KEY_NOT_FOUND",
            "KEYCUSTODIAN_KEY_STATE_CONFLICT",
            "KEYCUSTODIAN_PENDING_KEY_ALREADY_EXISTS",
            "KEYCUSTODIAN_SMOKE_TEST_FAILED",
            "KEYCUSTODIAN_INVALID_WORKLOAD_IDENTITY",
            "KEYCUSTODIAN_INVALID_CERTIFICATE_REQUEST",
            "KEYCUSTODIAN_NO_ACTIVE_ISSUING_CA",
            "KEYCUSTODIAN_CROSS_PROCESS_DOMAIN_REJECTED",
            "KEYCUSTODIAN_SIGNING_DOMAIN_NOT_AUTHORIZED",
            "KEYCUSTODIAN_REQUEST_ORIGIN_UNESTABLISHED",
            "KEYCUSTODIAN_MINTER_CAPABILITY_REQUIRED",
            "KEYCUSTODIAN_SIGNING_KEY_UNAVAILABLE",
            "KEYCUSTODIAN_EMPTY_SIGNING_INPUT",
            "KEYCUSTODIAN_KEY_TYPE_DOMAIN_MISMATCH",
            "KEYCUSTODIAN_SIGNING_INPUT_TOO_LARGE",
            "KEYCUSTODIAN_KEYRING_KEY_UNAVAILABLE",
            "KEYCUSTODIAN_KEYRING_DOMAIN_NOT_AUTHORIZED",
            "KEYCUSTODIAN_SEAL_NOT_AUTHORIZED",
            "KEYCUSTODIAN_SEAL_KEY_UNAVAILABLE",
            "KEYCUSTODIAN_ISSUANCE_NOT_AUTHORIZED",
            "KEYCUSTODIAN_INVALID_CSR",
            "KEYCUSTODIAN_CA_CERTIFICATE_NOT_AUTHORIZED",
        ];

        KeyCustodianErrorCodes.AllCodes.Should().BeEquivalentTo(
            expectedCodes,
            options => options.WithStrictOrdering());
    }

    [Fact]
    public void AllCodes_CountIsThirtyCodes()
    {
        KeyCustodianErrorCodes.AllCodes.Should().HaveCount(30);
    }

    // -----------------------------------------------------------------------
    // GetHttpStatus â€” known codes return 400; unknown code returns 500
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("KEYCUSTODIAN_KID_INVALID", 400)]
    [InlineData("KEYCUSTODIAN_KID_TOO_LONG", 400)]
    [InlineData("KEYCUSTODIAN_SOAK_NOT_ELAPSED", 400)]
    [InlineData("KEYCUSTODIAN_GRACE_NOT_ELAPSED", 400)]
    [InlineData("KEYCUSTODIAN_UNKNOWN_KEY_DOMAIN", 400)]
    [InlineData("KEYCUSTODIAN_PRECONDITION_VIOLATED", 500)]
    [InlineData("KEYCUSTODIAN_KEY_NOT_FOUND", 404)]
    [InlineData("KEYCUSTODIAN_KEY_STATE_CONFLICT", 409)]
    [InlineData("KEYCUSTODIAN_PENDING_KEY_ALREADY_EXISTS", 409)]
    [InlineData("KEYCUSTODIAN_SMOKE_TEST_FAILED", 500)]
    [InlineData("KEYCUSTODIAN_INVALID_WORKLOAD_IDENTITY", 400)]
    [InlineData("KEYCUSTODIAN_INVALID_CERTIFICATE_REQUEST", 500)]
    [InlineData("KEYCUSTODIAN_NO_ACTIVE_ISSUING_CA", 503)]
    [InlineData("KEYCUSTODIAN_CROSS_PROCESS_DOMAIN_REJECTED", 403)]
    [InlineData("KEYCUSTODIAN_SIGNING_DOMAIN_NOT_AUTHORIZED", 403)]
    [InlineData("KEYCUSTODIAN_REQUEST_ORIGIN_UNESTABLISHED", 403)]
    [InlineData("KEYCUSTODIAN_MINTER_CAPABILITY_REQUIRED", 403)]
    [InlineData("KEYCUSTODIAN_SIGNING_KEY_UNAVAILABLE", 503)]
    [InlineData("KEYCUSTODIAN_EMPTY_SIGNING_INPUT", 400)]
    [InlineData("KEYCUSTODIAN_KEY_TYPE_DOMAIN_MISMATCH", 400)]
    [InlineData("KEYCUSTODIAN_SIGNING_INPUT_TOO_LARGE", 400)]
    [InlineData("KEYCUSTODIAN_SEAL_NOT_AUTHORIZED", 403)]
    [InlineData("KEYCUSTODIAN_SEAL_KEY_UNAVAILABLE", 503)]
    public void GetHttpStatus_KnownCode_ReturnsDeclaredStatus(string code, int expected_status)
    {
        KeyCustodianErrorCodes.GetHttpStatus(code).Should().Be(expected_status);
    }

    [Fact]
    public void GetHttpStatus_UnknownCode_Returns500()
    {
        const int expected_status = 500;
        KeyCustodianErrorCodes.GetHttpStatus("KEYCUSTODIAN_DOES_NOT_EXIST")
            .Should().Be(expected_status);
    }

    [Fact]
    public void GetHttpStatus_EmptyString_Returns500()
    {
        const int expected_status = 500;
        KeyCustodianErrorCodes.GetHttpStatus(string.Empty).Should().Be(expected_status);
    }

    [Fact]
    public void GetHttpStatus_AllSpecCodes_ReturnDeclaredStatus()
    {
        // Regression pin: every code returns its declared status â€” the 400 input
        // validation codes return 400, the 403 capability-authority-denial codes return
        // 403, the 404 not-found code returns 404, the 409 conflict codes return 409,
        // and the 500 internal-error codes (precondition violation + smoke-test failure)
        // return 500. A generator regression that drops a code from the switch or maps
        // it to the wrong status fails here.
        var expectedStatuses = new Dictionary<string, int>(System.StringComparer.Ordinal)
        {
            ["KEYCUSTODIAN_KID_INVALID"] = 400,
            ["KEYCUSTODIAN_KID_TOO_LONG"] = 400,
            ["KEYCUSTODIAN_UNKNOWN_KEY_DOMAIN"] = 400,
            ["KEYCUSTODIAN_INVALID_ROTATION_POLICY"] = 400,
            ["KEYCUSTODIAN_SOAK_NOT_ELAPSED"] = 400,
            ["KEYCUSTODIAN_SMOKE_PROOF_TYPE_MISMATCH"] = 400,
            ["KEYCUSTODIAN_GRACE_NOT_ELAPSED"] = 400,
            ["KEYCUSTODIAN_PRECONDITION_VIOLATED"] = 500,
            ["KEYCUSTODIAN_KEY_NOT_FOUND"] = 404,
            ["KEYCUSTODIAN_KEY_STATE_CONFLICT"] = 409,
            ["KEYCUSTODIAN_PENDING_KEY_ALREADY_EXISTS"] = 409,
            ["KEYCUSTODIAN_SMOKE_TEST_FAILED"] = 500,
            ["KEYCUSTODIAN_INVALID_WORKLOAD_IDENTITY"] = 400,
            ["KEYCUSTODIAN_INVALID_CERTIFICATE_REQUEST"] = 500,
            ["KEYCUSTODIAN_NO_ACTIVE_ISSUING_CA"] = 503,
            ["KEYCUSTODIAN_CROSS_PROCESS_DOMAIN_REJECTED"] = 403,
            ["KEYCUSTODIAN_SIGNING_DOMAIN_NOT_AUTHORIZED"] = 403,
            ["KEYCUSTODIAN_REQUEST_ORIGIN_UNESTABLISHED"] = 403,
            ["KEYCUSTODIAN_MINTER_CAPABILITY_REQUIRED"] = 403,
            ["KEYCUSTODIAN_SIGNING_KEY_UNAVAILABLE"] = 503,
            ["KEYCUSTODIAN_EMPTY_SIGNING_INPUT"] = 400,
            ["KEYCUSTODIAN_KEY_TYPE_DOMAIN_MISMATCH"] = 400,
            ["KEYCUSTODIAN_SIGNING_INPUT_TOO_LARGE"] = 400,
            ["KEYCUSTODIAN_KEYRING_KEY_UNAVAILABLE"] = 503,
            ["KEYCUSTODIAN_KEYRING_DOMAIN_NOT_AUTHORIZED"] = 403,
            ["KEYCUSTODIAN_SEAL_NOT_AUTHORIZED"] = 403,
            ["KEYCUSTODIAN_SEAL_KEY_UNAVAILABLE"] = 503,
            ["KEYCUSTODIAN_ISSUANCE_NOT_AUTHORIZED"] = 403,
            ["KEYCUSTODIAN_INVALID_CSR"] = 400,
            ["KEYCUSTODIAN_CA_CERTIFICATE_NOT_AUTHORIZED"] = 403,
        };

        foreach (var code in KeyCustodianErrorCodes.AllCodes)
        {
            expectedStatuses.Should().ContainKey(
                code, because: $"the test's expected-status map must cover spec code {code}");
            var expected = expectedStatuses[code];
            KeyCustodianErrorCodes.GetHttpStatus(code)
                .Should().Be(expected, because: $"spec code {code} declares status {expected}");
        }
    }

    // -----------------------------------------------------------------------
    // Non-generic KeyCustodianFailures factories
    // -----------------------------------------------------------------------

    [Fact]
    public void KidInvalid_NonGeneric_ReturnsFailureWithExpectedMetadata()
    {
        var result = KeyCustodianFailures.KidInvalid();

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_KID_INVALID);
        result.Category.Should().Be(ErrorCategory.ValidationFailure);
    }

    [Fact]
    public void KidTooLong_NonGeneric_ReturnsFailureWithExpectedMetadata()
    {
        var result = KeyCustodianFailures.KidTooLong();

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_KID_TOO_LONG);
        result.Category.Should().Be(ErrorCategory.ValidationFailure);
    }

    [Fact]
    public void UnknownKeyDomain_NonGeneric_ReturnsFailureWithExpectedMetadata()
    {
        var result = KeyCustodianFailures.UnknownKeyDomain();

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_UNKNOWN_KEY_DOMAIN);
        result.Category.Should().Be(ErrorCategory.ValidationFailure);
    }

    [Fact]
    public void InvalidRotationPolicy_NonGeneric_ReturnsFailureWithExpectedMetadata()
    {
        var result = KeyCustodianFailures.InvalidRotationPolicy();

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_INVALID_ROTATION_POLICY);
        result.Category.Should().Be(ErrorCategory.ValidationFailure);
    }

    [Fact]
    public void SoakNotElapsed_NonGeneric_ReturnsFailureWithExpectedMetadata()
    {
        var result = KeyCustodianFailures.SoakNotElapsed();

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_SOAK_NOT_ELAPSED);
        result.Category.Should().Be(ErrorCategory.ValidationFailure);
    }

    [Fact]
    public void SmokeProofTypeMismatch_NonGeneric_ReturnsFailureWithExpectedMetadata()
    {
        var result = KeyCustodianFailures.SmokeProofTypeMismatch();

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_SMOKE_PROOF_TYPE_MISMATCH);
        result.Category.Should().Be(ErrorCategory.ValidationFailure);
    }

    [Fact]
    public void GraceNotElapsed_NonGeneric_ReturnsFailureWithExpectedMetadata()
    {
        var result = KeyCustodianFailures.GraceNotElapsed();

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_GRACE_NOT_ELAPSED);
        result.Category.Should().Be(ErrorCategory.ValidationFailure);
    }

    // -----------------------------------------------------------------------
    // Lifecycle 404 / 409 + smoke-test 500 factories (non-generic + generic) â€”
    // these delegate to the NotFound / Conflict / UnhandledException base
    // factories per the spec's httpStatus, stamping the KC code + category.
    // -----------------------------------------------------------------------

    [Fact]
    public void KeyNotFound_NonGeneric_ReturnsNotFoundFailure()
    {
        var result = KeyCustodianFailures.KeyNotFound();

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_KEY_NOT_FOUND);
        result.Category.Should().Be(ErrorCategory.NotFound);
        result.Messages.Should().Contain(m => m.Key == "keycustodian_lifecycle_KEY_NOT_FOUND");
    }

    [Fact]
    public void KeyNotFound_Generic_ReturnsTypedNotFoundFailure()
    {
        var result = KeyCustodianFailures<int>.KeyNotFound();

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_KEY_NOT_FOUND);
        result.Category.Should().Be(ErrorCategory.NotFound);
    }

    [Fact]
    public void KeyStateConflict_NonGeneric_ReturnsConflictFailure()
    {
        var result = KeyCustodianFailures.KeyStateConflict();

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(System.Net.HttpStatusCode.Conflict);
        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_KEY_STATE_CONFLICT);
        result.Category.Should().Be(ErrorCategory.Conflict);
        result.Messages.Should().Contain(m => m.Key == "keycustodian_lifecycle_KEY_STATE_CONFLICT");
    }

    [Fact]
    public void PendingKeyAlreadyExists_NonGeneric_ReturnsConflictFailure()
    {
        var result = KeyCustodianFailures.PendingKeyAlreadyExists();

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(System.Net.HttpStatusCode.Conflict);
        result.ErrorCode.Should().Be(
            KeyCustodianErrorCodes.KEYCUSTODIAN_PENDING_KEY_ALREADY_EXISTS);
        result.Category.Should().Be(ErrorCategory.Conflict);
        result.Messages.Should().Contain(
            m => m.Key == "keycustodian_lifecycle_PENDING_KEY_ALREADY_EXISTS");
    }

    [Fact]
    public void SmokeTestFailed_NonGeneric_ReturnsInternalErrorFailure()
    {
        var result = KeyCustodianFailures.SmokeTestFailed();

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(System.Net.HttpStatusCode.InternalServerError);
        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_SMOKE_TEST_FAILED);
        result.Category.Should().Be(ErrorCategory.InternalError);
        result.Messages.Should().Contain(m => m.Key == "keycustodian_internal_SMOKE_TEST_FAILED");
    }

    [Fact]
    public void SmokeTestFailed_Generic_ReturnsTypedInternalErrorFailure()
    {
        var result = KeyCustodianFailures<int>.SmokeTestFailed();

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(System.Net.HttpStatusCode.InternalServerError);
        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_SMOKE_TEST_FAILED);
        result.Category.Should().Be(ErrorCategory.InternalError);
    }

    // -----------------------------------------------------------------------
    // PreconditionViolated â€” the 500 / internal_error delegating factory
    // (non-generic + generic). Delegates to D2Result.UnhandledException with the
    // stamped KC code + InternalError category.
    // -----------------------------------------------------------------------

    [Fact]
    public void PreconditionViolated_NonGeneric_ReturnsInternalErrorFailure()
    {
        var result = KeyCustodianFailures.PreconditionViolated();

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(System.Net.HttpStatusCode.InternalServerError);
        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_PRECONDITION_VIOLATED);
        result.Category.Should().Be(ErrorCategory.InternalError);
        result.Messages.Should().Contain(
            m => m.Key == "keycustodian_internal_PRECONDITION_VIOLATED");
    }

    [Fact]
    public void PreconditionViolated_Generic_ReturnsTypedInternalErrorFailure()
    {
        var result = KeyCustodianFailures<int>.PreconditionViolated();

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(System.Net.HttpStatusCode.InternalServerError);
        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_PRECONDITION_VIOLATED);
        result.Category.Should().Be(ErrorCategory.InternalError);
        result.Messages.Should().Contain(
            m => m.Key == "keycustodian_internal_PRECONDITION_VIOLATED");
    }

    // -----------------------------------------------------------------------
    // PreconditionViolated â€” optional messages override (non-generic + generic)
    // -----------------------------------------------------------------------

    [Fact]
    public void PreconditionViolated_NonGeneric_NoOverride_DefaultsToBareTkWithoutParams()
    {
        var result = KeyCustodianFailures.PreconditionViolated();

        var message = result.Messages.Single(
            m => m.Key == "keycustodian_internal_PRECONDITION_VIOLATED");
        message.Parameters.Should().BeNull(
            because: "the no-arg factory defaults to the bare spec TK with no bound arg");
    }

    [Fact]
    public void PreconditionViolated_NonGeneric_MessagesOverride_BindsTheOffendingArg()
    {
        var result = KeyCustodianFailures.PreconditionViolated(
            messages: [ProductTK.Keycustodian.Internal.PRECONDITION_VIOLATED.With("arg", "clock")]);

        // Override honored: the bound arg rides the result's message.
        var message = result.Messages.Single(
            m => m.Key == "keycustodian_internal_PRECONDITION_VIOLATED");
        message.Parameters.Should().NotBeNull();
        message.Parameters["arg"].Should().Be("clock");

        // The stamped code + category are unchanged by the override.
        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_PRECONDITION_VIOLATED);
        result.Category.Should().Be(ErrorCategory.InternalError);
    }

    [Fact]
    public void PreconditionViolated_Generic_MessagesOverride_BindsTheOffendingArg()
    {
        var result = KeyCustodianFailures<int>.PreconditionViolated(
            messages: [ProductTK.Keycustodian.Internal.PRECONDITION_VIOLATED.With("arg", "proof")]);

        var message = result.Messages.Single(
            m => m.Key == "keycustodian_internal_PRECONDITION_VIOLATED");
        message.Parameters.Should().NotBeNull();
        message.Parameters["arg"].Should().Be("proof");
        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_PRECONDITION_VIOLATED);
        result.Category.Should().Be(ErrorCategory.InternalError);
    }

    // -----------------------------------------------------------------------
    // mTLS CA / workload-issuance factories (the 0022 additions)
    // -----------------------------------------------------------------------

    [Fact]
    public void InvalidWorkloadIdentity_NonGeneric_ReturnsValidationFailure()
    {
        var result = KeyCustodianFailures.InvalidWorkloadIdentity();

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_INVALID_WORKLOAD_IDENTITY);
        result.Category.Should().Be(ErrorCategory.ValidationFailure);
        result.Messages.Should().Contain(
            m => m.Key == "keycustodian_validation_INVALID_WORKLOAD_IDENTITY");
    }

    [Fact]
    public void InvalidWorkloadIdentity_Generic_ReturnsTypedValidationFailure()
    {
        var result = KeyCustodianFailures<int>.InvalidWorkloadIdentity();

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_INVALID_WORKLOAD_IDENTITY);
        result.Category.Should().Be(ErrorCategory.ValidationFailure);
    }

    [Fact]
    public void InvalidCertificateRequest_NonGeneric_ReturnsInternalErrorFailure()
    {
        var result = KeyCustodianFailures.InvalidCertificateRequest();

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(System.Net.HttpStatusCode.InternalServerError);
        result.ErrorCode.Should().Be(
            KeyCustodianErrorCodes.KEYCUSTODIAN_INVALID_CERTIFICATE_REQUEST);
        result.Category.Should().Be(ErrorCategory.InternalError);
        result.Messages.Should().Contain(
            m => m.Key == "keycustodian_internal_INVALID_CERTIFICATE_REQUEST");
    }

    [Fact]
    public void NoActiveIssuingCa_NonGeneric_ReturnsServiceUnavailableFailure()
    {
        var result = KeyCustodianFailures.NoActiveIssuingCa();

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(
            System.Net.HttpStatusCode.ServiceUnavailable,
            "no active issuing CA is a retryable not-ready-yet condition, not a client error");
        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_NO_ACTIVE_ISSUING_CA);
        result.Category.Should().Be(ErrorCategory.InfrastructureUnavailable);
        result.Messages.Should().Contain(
            m => m.Key == "keycustodian_infrastructure_NO_ACTIVE_ISSUING_CA");
    }

    [Fact]
    public void NoActiveIssuingCa_Generic_ReturnsTypedServiceUnavailableFailure()
    {
        var result = KeyCustodianFailures<int>.NoActiveIssuingCa();

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(System.Net.HttpStatusCode.ServiceUnavailable);
        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_NO_ACTIVE_ISSUING_CA);
        result.Category.Should().Be(ErrorCategory.InfrastructureUnavailable);
    }

    // -----------------------------------------------------------------------
    // Capability-authority denial factories â€” 403 / policy_denied. Both delegate
    // to D2Result.Forbidden, stamping the KC code + PolicyDenied category.
    // -----------------------------------------------------------------------

    [Fact]
    public void CrossProcessDomainRejected_NonGeneric_ReturnsForbiddenPolicyDenied()
    {
        var result = KeyCustodianFailures.CrossProcessDomainRejected();

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(
            System.Net.HttpStatusCode.Forbidden,
            "a cross-process caller requesting an in-process-only signing domain is denied");
        result.ErrorCode.Should().Be(
            KeyCustodianErrorCodes.KEYCUSTODIAN_CROSS_PROCESS_DOMAIN_REJECTED);
        result.Category.Should().Be(ErrorCategory.PolicyDenied);
        result.Messages.Should().Contain(
            m => m.Key == "keycustodian_authorization_CROSS_PROCESS_DOMAIN_REJECTED");
    }

    [Fact]
    public void CrossProcessDomainRejected_Generic_ReturnsTypedForbiddenPolicyDenied()
    {
        var result = KeyCustodianFailures<int>.CrossProcessDomainRejected();

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(System.Net.HttpStatusCode.Forbidden);
        result.ErrorCode.Should().Be(
            KeyCustodianErrorCodes.KEYCUSTODIAN_CROSS_PROCESS_DOMAIN_REJECTED);
        result.Category.Should().Be(ErrorCategory.PolicyDenied);
    }

    [Fact]
    public void SigningDomainNotAuthorized_NonGeneric_ReturnsForbiddenPolicyDenied()
    {
        var result = KeyCustodianFailures.SigningDomainNotAuthorized();

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(
            System.Net.HttpStatusCode.Forbidden,
            "a domain not in the caller's allowed-signing-domains policy set is denied");
        result.ErrorCode.Should().Be(
            KeyCustodianErrorCodes.KEYCUSTODIAN_SIGNING_DOMAIN_NOT_AUTHORIZED);
        result.Category.Should().Be(ErrorCategory.PolicyDenied);
        result.Messages.Should().Contain(
            m => m.Key == "keycustodian_authorization_SIGNING_DOMAIN_NOT_AUTHORIZED");
    }

    [Fact]
    public void SigningDomainNotAuthorized_Generic_ReturnsTypedForbiddenPolicyDenied()
    {
        var result = KeyCustodianFailures<int>.SigningDomainNotAuthorized();

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(System.Net.HttpStatusCode.Forbidden);
        result.ErrorCode.Should().Be(
            KeyCustodianErrorCodes.KEYCUSTODIAN_SIGNING_DOMAIN_NOT_AUTHORIZED);
        result.Category.Should().Be(ErrorCategory.PolicyDenied);
    }

    // -----------------------------------------------------------------------
    // Authority-refinement denial factories â€” 403 / policy_denied. Both delegate
    // to D2Result.Forbidden, stamping the KC code + PolicyDenied category.
    // -----------------------------------------------------------------------

    [Fact]
    public void RequestOriginUnestablished_NonGeneric_ReturnsForbiddenPolicyDenied()
    {
        var result = KeyCustodianFailures.RequestOriginUnestablished();

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(
            System.Net.HttpStatusCode.Forbidden,
            "an unestablished request origin is fail-closed and denies signing");
        result.ErrorCode.Should().Be(
            KeyCustodianErrorCodes.KEYCUSTODIAN_REQUEST_ORIGIN_UNESTABLISHED);
        result.Category.Should().Be(ErrorCategory.PolicyDenied);
        result.Messages.Should().Contain(
            m => m.Key == "keycustodian_authorization_REQUEST_ORIGIN_UNESTABLISHED");
    }

    [Fact]
    public void RequestOriginUnestablished_Generic_ReturnsTypedForbiddenPolicyDenied()
    {
        var result = KeyCustodianFailures<int>.RequestOriginUnestablished();

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(System.Net.HttpStatusCode.Forbidden);
        result.ErrorCode.Should().Be(
            KeyCustodianErrorCodes.KEYCUSTODIAN_REQUEST_ORIGIN_UNESTABLISHED);
        result.Category.Should().Be(ErrorCategory.PolicyDenied);
    }

    [Fact]
    public void MinterCapabilityRequired_NonGeneric_ReturnsForbiddenPolicyDenied()
    {
        var result = KeyCustodianFailures.MinterCapabilityRequired();

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(
            System.Net.HttpStatusCode.Forbidden,
            "the cluster-signing root is reachable only via the minter capability");
        result.ErrorCode.Should().Be(
            KeyCustodianErrorCodes.KEYCUSTODIAN_MINTER_CAPABILITY_REQUIRED);
        result.Category.Should().Be(ErrorCategory.PolicyDenied);
        result.Messages.Should().Contain(
            m => m.Key == "keycustodian_authorization_MINTER_CAPABILITY_REQUIRED");
    }

    [Fact]
    public void MinterCapabilityRequired_Generic_ReturnsTypedForbiddenPolicyDenied()
    {
        var result = KeyCustodianFailures<int>.MinterCapabilityRequired();

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(System.Net.HttpStatusCode.Forbidden);
        result.ErrorCode.Should().Be(
            KeyCustodianErrorCodes.KEYCUSTODIAN_MINTER_CAPABILITY_REQUIRED);
        result.Category.Should().Be(ErrorCategory.PolicyDenied);
    }

    // -----------------------------------------------------------------------
    // Sign-op factories. SigningKeyUnavailable is a
    // 503 / infrastructure_unavailable retryable not-ready condition;
    // EmptySigningInput is a 400 / validation_failure client error.
    // -----------------------------------------------------------------------

    [Fact]
    public void SigningKeyUnavailable_NonGeneric_ReturnsServiceUnavailableFailure()
    {
        var result = KeyCustodianFailures.SigningKeyUnavailable();

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(
            System.Net.HttpStatusCode.ServiceUnavailable,
            "no active signing key for the domain is a retryable not-ready-yet condition");
        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_SIGNING_KEY_UNAVAILABLE);
        result.Category.Should().Be(ErrorCategory.InfrastructureUnavailable);
        result.Messages.Should().Contain(
            m => m.Key == "keycustodian_infrastructure_SIGNING_KEY_UNAVAILABLE");
    }

    [Fact]
    public void SigningKeyUnavailable_Generic_ReturnsTypedServiceUnavailableFailure()
    {
        var result = KeyCustodianFailures<int>.SigningKeyUnavailable();

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(System.Net.HttpStatusCode.ServiceUnavailable);
        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_SIGNING_KEY_UNAVAILABLE);
        result.Category.Should().Be(ErrorCategory.InfrastructureUnavailable);
    }

    [Fact]
    public void EmptySigningInput_NonGeneric_ReturnsValidationFailure()
    {
        var result = KeyCustodianFailures.EmptySigningInput();

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_EMPTY_SIGNING_INPUT);
        result.Category.Should().Be(ErrorCategory.ValidationFailure);
        result.Messages.Should().Contain(
            m => m.Key == "keycustodian_validation_EMPTY_SIGNING_INPUT");
    }

    [Fact]
    public void EmptySigningInput_Generic_ReturnsTypedValidationFailure()
    {
        var result = KeyCustodianFailures<int>.EmptySigningInput();

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_EMPTY_SIGNING_INPUT);
        result.Category.Should().Be(ErrorCategory.ValidationFailure);
    }

    [Fact]
    public void SigningInputTooLarge_NonGeneric_ReturnsValidationFailure()
    {
        var result = KeyCustodianFailures.SigningInputTooLarge();

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_SIGNING_INPUT_TOO_LARGE);
        result.Category.Should().Be(ErrorCategory.ValidationFailure);
        result.Messages.Should().Contain(m => m.Key == "common_errors_TOO_LONG");
    }

    [Fact]
    public void SigningInputTooLarge_Generic_ReturnsTypedValidationFailure()
    {
        var result = KeyCustodianFailures<int>.SigningInputTooLarge();

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_SIGNING_INPUT_TOO_LARGE);
        result.Category.Should().Be(ErrorCategory.ValidationFailure);
    }

    // -----------------------------------------------------------------------
    // Certificate-authority consumer-surface factories: the issuance +
    // CA-certificate plane denials (403 / policy_denied) and the coarse
    // invalid-CSR rejection (400 / validation_failure).
    // -----------------------------------------------------------------------

    [Fact]
    public void IssuanceNotAuthorized_NonGeneric_ReturnsForbiddenPolicyDenied()
    {
        var result = KeyCustodianFailures.IssuanceNotAuthorized();

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(
            System.Net.HttpStatusCode.Forbidden,
            "leaf issuance is cross-process-only; every other plane is denied");
        result.ErrorCode.Should().Be(
            KeyCustodianErrorCodes.KEYCUSTODIAN_ISSUANCE_NOT_AUTHORIZED);
        result.Category.Should().Be(ErrorCategory.PolicyDenied);
        result.Messages.Should().Contain(
            m => m.Key == "keycustodian_authorization_ISSUANCE_NOT_AUTHORIZED");
    }

    [Fact]
    public void IssuanceNotAuthorized_Generic_ReturnsTypedForbiddenPolicyDenied()
    {
        var result = KeyCustodianFailures<int>.IssuanceNotAuthorized();

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(System.Net.HttpStatusCode.Forbidden);
        result.ErrorCode.Should().Be(
            KeyCustodianErrorCodes.KEYCUSTODIAN_ISSUANCE_NOT_AUTHORIZED);
        result.Category.Should().Be(ErrorCategory.PolicyDenied);
    }

    [Fact]
    public void InvalidCsr_NonGeneric_ReturnsValidationFailure()
    {
        var result = KeyCustodianFailures.InvalidCsr();

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(
            System.Net.HttpStatusCode.BadRequest,
            "an oversized / malformed / possession-unproven / wrong-curve CSR is a client error");
        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_INVALID_CSR);
        result.Category.Should().Be(ErrorCategory.ValidationFailure);
        result.Messages.Should().Contain(
            m => m.Key == "keycustodian_validation_INVALID_CSR");
    }

    [Fact]
    public void InvalidCsr_Generic_ReturnsTypedValidationFailure()
    {
        var result = KeyCustodianFailures<int>.InvalidCsr();

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_INVALID_CSR);
        result.Category.Should().Be(ErrorCategory.ValidationFailure);
    }

    [Fact]
    public void CaCertificateNotAuthorized_NonGeneric_ReturnsForbiddenPolicyDenied()
    {
        var result = KeyCustodianFailures.CaCertificateNotAuthorized();

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(
            System.Net.HttpStatusCode.Forbidden,
            "the CA chain is distributed over already-trusted internal channels only");
        result.ErrorCode.Should().Be(
            KeyCustodianErrorCodes.KEYCUSTODIAN_CA_CERTIFICATE_NOT_AUTHORIZED);
        result.Category.Should().Be(ErrorCategory.PolicyDenied);
        result.Messages.Should().Contain(
            m => m.Key == "keycustodian_authorization_CA_CERTIFICATE_NOT_AUTHORIZED");
    }

    [Fact]
    public void CaCertificateNotAuthorized_Generic_ReturnsTypedForbiddenPolicyDenied()
    {
        var result = KeyCustodianFailures<int>.CaCertificateNotAuthorized();

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(System.Net.HttpStatusCode.Forbidden);
        result.ErrorCode.Should().Be(
            KeyCustodianErrorCodes.KEYCUSTODIAN_CA_CERTIFICATE_NOT_AUTHORIZED);
        result.Category.Should().Be(ErrorCategory.PolicyDenied);
    }
}
