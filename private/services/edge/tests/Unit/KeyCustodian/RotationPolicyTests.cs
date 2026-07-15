// -----------------------------------------------------------------------
// <copyright file="RotationPolicyTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.Tests.Unit.KeyCustodian;

/// <summary>
/// Adversarial unit tests for <see cref="RotationPolicy"/>.
/// Temporal note: <see cref="Duration"/> is zone-free — DST/IANA cases are N/A.
/// </summary>
public sealed class RotationPolicyTests
{
    private static readonly Duration sr_soak = Duration.FromHours(1);
    private static readonly Duration sr_grace = Duration.FromHours(2);
    private static readonly Duration sr_cadence = Duration.FromHours(4); // >= grace + soak = 3h

    // -----------------------------------------------------------------------
    // Create — valid
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_ValidPolicy_ReturnsOk()
    {
        var result = RotationPolicy.Create(sr_cadence, sr_grace, sr_soak);
        result.Success.Should().BeTrue();
        result.Data!.Cadence.Should().Be(sr_cadence);
        result.Data!.Grace.Should().Be(sr_grace);
        result.Data!.SmokeSoak.Should().Be(sr_soak);
    }

    [Fact]
    public void Create_CadenceExactlyGracePlusSoak_ReturnsOk()
    {
        // Boundary: Cadence == Grace + SmokeSoak is allowed (>=)
        var grace = Duration.FromHours(2);
        var soak = Duration.FromHours(1);
        var cadence = grace + soak; // exactly 3h

        var result = RotationPolicy.Create(cadence, grace, soak);
        result.Success.Should().BeTrue();
    }

    // -----------------------------------------------------------------------
    // Create — zero and negative durations
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_ZeroCadence_ReturnsValidationFailed()
    {
        var result = RotationPolicy.Create(Duration.Zero, sr_grace, sr_soak);
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_INVALID_ROTATION_POLICY);
        result.Category.Should().Be(ErrorCategory.ValidationFailure);
    }

    [Fact]
    public void Create_ZeroGrace_ReturnsValidationFailed()
    {
        var result = RotationPolicy.Create(sr_cadence, Duration.Zero, sr_soak);
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_INVALID_ROTATION_POLICY);
        result.Category.Should().Be(ErrorCategory.ValidationFailure);
    }

    [Fact]
    public void Create_ZeroSmokeSoak_ReturnsValidationFailed()
    {
        var result = RotationPolicy.Create(sr_cadence, sr_grace, Duration.Zero);
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_INVALID_ROTATION_POLICY);
        result.Category.Should().Be(ErrorCategory.ValidationFailure);
    }

    [Fact]
    public void Create_NegativeCadence_ReturnsValidationFailed()
    {
        var result = RotationPolicy.Create(Duration.FromSeconds(-1), sr_grace, sr_soak);
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_INVALID_ROTATION_POLICY);
        result.Category.Should().Be(ErrorCategory.ValidationFailure);
    }

    [Fact]
    public void Create_NegativeGrace_ReturnsValidationFailed()
    {
        var result = RotationPolicy.Create(sr_cadence, Duration.FromSeconds(-1), sr_soak);
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_INVALID_ROTATION_POLICY);
        result.Category.Should().Be(ErrorCategory.ValidationFailure);
    }

    [Fact]
    public void Create_NegativeSmokeSoak_ReturnsValidationFailed()
    {
        var result = RotationPolicy.Create(sr_cadence, sr_grace, Duration.FromSeconds(-1));
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_INVALID_ROTATION_POLICY);
        result.Category.Should().Be(ErrorCategory.ValidationFailure);
    }

    // -----------------------------------------------------------------------
    // Create — cadence < grace + soak
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_CadenceLessThanGracePlusSoak_ReturnsValidationFailed()
    {
        var grace = Duration.FromHours(2);
        var soak = Duration.FromHours(1);
        var cadence = Duration.FromMinutes(100); // < 3h

        var result = RotationPolicy.Create(cadence, grace, soak);
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_INVALID_ROTATION_POLICY);
        result.Category.Should().Be(ErrorCategory.ValidationFailure);
    }

    // -----------------------------------------------------------------------
    // Create — very large durations (overflow / boundary)
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_VeryLargeDurations_NoOverflow()
    {
        var huge = Duration.FromDays(3_000_000);

        // cadence must be >= grace + soak: use 2 * huge for cadence
        var result = RotationPolicy.Create(huge + huge, huge, huge);
        result.Success.Should().BeTrue();
    }

    [Fact]
    public void Create_VeryLargeDurations_CadenceEnforced()
    {
        // cadence == grace + soak with very large values — boundary passes
        var grace = Duration.FromDays(1_500_000);
        var soak = Duration.FromDays(1_000_000);
        var cadence = grace + soak;

        var result = RotationPolicy.Create(cadence, grace, soak);
        result.Success.Should().BeTrue();
    }
}
