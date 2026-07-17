// -----------------------------------------------------------------------
// <copyright file="EmailValidatorParityTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Validation;

using AwesomeAssertions;
using DcsvIo.D2.Utilities.Extensions;
using DcsvIo.D2.Validation;
using Xunit;

/// <summary>
/// Asserts the .NET <see cref="DefaultEmailValidator"/> produces the SAME
/// verdict + normalized form as the hand-authored cross-language corpus at
/// <c>contracts/validation/fixtures/email.json</c>. The TypeScript
/// <c>@dcsv-io/d2-validation</c> parity test asserts the identical corpus, so any
/// behavioral drift between the two runtimes fails one side.
/// </summary>
public sealed class EmailValidatorParityTests
{
    /// <summary>
    /// The exact email-validation pattern literal. The TypeScript parity test
    /// holds the identical string; a drift on EITHER side fails its test.
    /// </summary>
    // long regex literal — cannot wrap
    private const string _EXPECTED_EMAIL_PATTERN =
        @"^(?=.{1,254}$)[A-Z0-9._%+\-]{1,64}@[A-Z0-9](?:[A-Z0-9\-]{0,61}[A-Z0-9])?(?:\.[A-Z0-9](?:[A-Z0-9\-]{0,61}[A-Z0-9])?)+$";

    private static readonly ValidationCorpus sr_Corpus = ValidationFixture.Load("email");

    public static TheoryData<string> RowNames()
    {
        var data = new TheoryData<string>();
        if (sr_Corpus.Rows.Falsey())
        {
            throw new InvalidOperationException(
                $"Email corpus loaded with zero rows — version='{sr_Corpus.Version}'.");
        }

        foreach (var row in sr_Corpus.Rows)
            data.Add(row.Name);
        return data;
    }

    [Theory]
    [MemberData(nameof(RowNames))]
    public void Email_Parity(string rowName)
    {
        var row = sr_Corpus.Rows.Single(r => r.Name == rowName);
        var input = ValidationFixture.SynthInput(row);

        var result = new DefaultEmailValidator().Validate(input);

        if (row.Valid)
        {
            result.Success.Should().BeTrue(rowName);
            result.Data.Should().Be(row.Normalized, rowName);
        }
        else
        {
            result.Success.Should().BeFalse(rowName);
            result.ErrorCode.Should().Be("VALIDATION_FAILED", rowName);
            result.InputErrors.SelectMany(e => e.Errors).Select(m => m.Key)
                .Should().Contain(row.ErrorKey, rowName);
        }
    }

    [Fact]
    public void EmailPattern_MatchesCrossLanguageLiteral()
        => DefaultEmailValidator.EMAIL_PATTERN.Should().Be(_EXPECTED_EMAIL_PATTERN);
}
