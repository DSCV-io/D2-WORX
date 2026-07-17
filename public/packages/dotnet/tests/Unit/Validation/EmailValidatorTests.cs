// -----------------------------------------------------------------------
// <copyright file="EmailValidatorTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Validation;

using System.Diagnostics;
using AwesomeAssertions;
using DcsvIo.D2.I18n;
using DcsvIo.D2.Validation;
using Xunit;

/// <summary>
/// Adversarial + property tests for <see cref="DefaultEmailValidator"/> beyond
/// the cross-language parity corpus — idempotency, ReDoS timing, and the
/// null / empty / whitespace falsey paths.
/// </summary>
public sealed class EmailValidatorTests
{
    private static readonly DefaultEmailValidator sr_Validator = new();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejects_NullEmptyWhitespace(string? input)
    {
        var result = sr_Validator.Validate(input);
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("VALIDATION_FAILED");
        result.InputErrors[0].Field.Should().Be("email");
        result.InputErrors[0].Errors[0].Key.Should().Be(TK.Common.Validation.EMAIL_INVALID.Key);
    }

    [Fact]
    public void Normalizes_TrimThenLowercase()
    {
        var result = sr_Validator.Validate("  User@Example.COM  ");
        result.Success.Should().BeTrue();
        result.Data.Should().Be("user@example.com");
    }

    [Fact]
    public void IsIdempotent_OnNormalizedValue()
    {
        var first = sr_Validator.Validate("  User@Example.COM  ");
        first.Success.Should().BeTrue();

        var second = sr_Validator.Validate(first.Data);
        second.Success.Should().BeTrue();
        second.Data.Should().Be(first.Data);
    }

    [Fact]
    public void ReturnsFast_OnPathologicalInput()
    {
        // A long local-part run with a lone '@' and no valid domain — the
        // shape a backtracking-vulnerable regex would hang on. The anchored,
        // bounded pattern + 50 ms matchTimeout must short-circuit. A timeout
        // is caught and mapped to a validation failure (Invalid()), not a hang.
        var pathological = new string('a', 50_000) + "@";
        var sw = Stopwatch.StartNew();
        var result = sr_Validator.Validate(pathological);
        sw.Stop();

        result.Success.Should().BeFalse();

        // 500 ms = 10x the 50 ms matchTimeout; generous enough to absorb CI
        // scheduling jitter while still catching a genuine ReDoS hang.
        sw.ElapsedMilliseconds.Should().BeLessThan(500);
    }
}
