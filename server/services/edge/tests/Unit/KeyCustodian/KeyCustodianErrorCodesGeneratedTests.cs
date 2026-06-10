// -----------------------------------------------------------------------
// <copyright file="KeyCustodianErrorCodesGeneratedTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian;

using System.Linq;
using AwesomeAssertions;
using D2.Edge.KeyCustodian.Domain.Errors;
using D2.Shared.ErrorCodes.Category;
using D2.Shared.I18n;
using Xunit;

/// <summary>
/// First-pass coverage for the generated <see cref="KeyCustodianErrorCodes"/>
/// constants class and the non-generic <see cref="KeyCustodianFailures"/> factory
/// class. Pins constant wire-string values, <c>AllCodes</c> membership,
/// <c>GetHttpStatus</c> return values, and non-generic factory result shape.
/// </summary>
public sealed class KeyCustodianErrorCodesGeneratedTests
{
    // -----------------------------------------------------------------------
    // Constant value pins (F2) — each constant equals its exact wire literal
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(KeyCustodianErrorCodes.KEYCUSTODIAN_KID_INVALID, "KEYCUSTODIAN_KID_INVALID")]
    [InlineData(KeyCustodianErrorCodes.KEYCUSTODIAN_KID_TOO_LONG, "KEYCUSTODIAN_KID_TOO_LONG")]
    [InlineData(KeyCustodianErrorCodes.KEYCUSTODIAN_UNKNOWN_KEY_DOMAIN, "KEYCUSTODIAN_UNKNOWN_KEY_DOMAIN")]
    [InlineData(KeyCustodianErrorCodes.KEYCUSTODIAN_INVALID_ROTATION_POLICY, "KEYCUSTODIAN_INVALID_ROTATION_POLICY")]
    [InlineData(KeyCustodianErrorCodes.KEYCUSTODIAN_SOAK_NOT_ELAPSED, "KEYCUSTODIAN_SOAK_NOT_ELAPSED")]
    [InlineData(KeyCustodianErrorCodes.KEYCUSTODIAN_SMOKE_PROOF_TYPE_MISMATCH, "KEYCUSTODIAN_SMOKE_PROOF_TYPE_MISMATCH")]
    [InlineData(KeyCustodianErrorCodes.KEYCUSTODIAN_GRACE_NOT_ELAPSED, "KEYCUSTODIAN_GRACE_NOT_ELAPSED")]
    [InlineData(KeyCustodianErrorCodes.KEYCUSTODIAN_PRECONDITION_VIOLATED, "KEYCUSTODIAN_PRECONDITION_VIOLATED")]
    public void Constant_ValueEqualsWireLiteral(string constant, string expected_wire_literal)
    {
        constant.Should().Be(expected_wire_literal);
    }

    // -----------------------------------------------------------------------
    // AllCodes membership (F2) — set equals the 7 spec codes in spec order
    // -----------------------------------------------------------------------

    [Fact]
    public void AllCodes_SetEqualToSpecCodeList()
    {
        var expected_codes = new[]
        {
            "KEYCUSTODIAN_KID_INVALID",
            "KEYCUSTODIAN_KID_TOO_LONG",
            "KEYCUSTODIAN_UNKNOWN_KEY_DOMAIN",
            "KEYCUSTODIAN_INVALID_ROTATION_POLICY",
            "KEYCUSTODIAN_SOAK_NOT_ELAPSED",
            "KEYCUSTODIAN_SMOKE_PROOF_TYPE_MISMATCH",
            "KEYCUSTODIAN_GRACE_NOT_ELAPSED",
            "KEYCUSTODIAN_PRECONDITION_VIOLATED",
        };

        KeyCustodianErrorCodes.AllCodes.Should().BeEquivalentTo(
            expected_codes,
            options => options.WithStrictOrdering());
    }

    [Fact]
    public void AllCodes_CountIsEightCodes()
    {
        KeyCustodianErrorCodes.AllCodes.Should().HaveCount(8);
    }

    // -----------------------------------------------------------------------
    // GetHttpStatus (F2) — known codes return 400; unknown code returns 500
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("KEYCUSTODIAN_KID_INVALID", 400)]
    [InlineData("KEYCUSTODIAN_KID_TOO_LONG", 400)]
    [InlineData("KEYCUSTODIAN_SOAK_NOT_ELAPSED", 400)]
    [InlineData("KEYCUSTODIAN_GRACE_NOT_ELAPSED", 400)]
    [InlineData("KEYCUSTODIAN_UNKNOWN_KEY_DOMAIN", 400)]
    [InlineData("KEYCUSTODIAN_PRECONDITION_VIOLATED", 500)]
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
        // Regression pin: every code returns its declared status — the 400 input
        // validation codes return 400, the 500 precondition-violation code returns
        // 500. A generator regression that drops a code from the switch or maps it
        // to the wrong status fails here.
        foreach (var code in KeyCustodianErrorCodes.AllCodes)
        {
            var expected = code == "KEYCUSTODIAN_PRECONDITION_VIOLATED" ? 500 : 400;
            KeyCustodianErrorCodes.GetHttpStatus(code)
                .Should().Be(expected, because: $"spec code {code} declares status {expected}");
        }
    }

    // -----------------------------------------------------------------------
    // Non-generic KeyCustodianFailures factories (F3)
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
    // PreconditionViolated — the 500 / internal_error delegating factory
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
    // PreconditionViolated — optional messages override (non-generic + generic)
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
            messages: [TK.Keycustodian.Internal.PRECONDITION_VIOLATED.With("arg", "clock")]);

        // Override honored: the bound arg rides the result's message.
        var message = result.Messages.Single(
            m => m.Key == "keycustodian_internal_PRECONDITION_VIOLATED");
        message.Parameters.Should().NotBeNull();
        message.Parameters!["arg"].Should().Be("clock");

        // The stamped code + category are unchanged by the override.
        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_PRECONDITION_VIOLATED);
        result.Category.Should().Be(ErrorCategory.InternalError);
    }

    [Fact]
    public void PreconditionViolated_Generic_MessagesOverride_BindsTheOffendingArg()
    {
        var result = KeyCustodianFailures<int>.PreconditionViolated(
            messages: [TK.Keycustodian.Internal.PRECONDITION_VIOLATED.With("arg", "proof")]);

        var message = result.Messages.Single(
            m => m.Key == "keycustodian_internal_PRECONDITION_VIOLATED");
        message.Parameters.Should().NotBeNull();
        message.Parameters!["arg"].Should().Be("proof");
        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_PRECONDITION_VIOLATED);
        result.Category.Should().Be(ErrorCategory.InternalError);
    }
}
