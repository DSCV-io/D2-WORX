// -----------------------------------------------------------------------
// <copyright file="PhoneValidatorParityTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Validation;

using AwesomeAssertions;
using DcsvIo.D2.Geo.Abstractions;
using DcsvIo.D2.Utilities.Extensions;
using DcsvIo.D2.Validation;
using Xunit;

/// <summary>
/// Asserts the .NET <see cref="DefaultPhoneValidator"/> produces the SAME
/// verdict + E.164 normalization as the hand-authored cross-language corpus
/// at <c>contracts/validation/fixtures/phone.json</c>. The TypeScript
/// <c>@dcsv-io/d2-validation</c> parity test asserts the identical corpus, so any
/// metadata or normalization drift between libphonenumber-csharp and
/// libphonenumber-js fails one side.
/// </summary>
public sealed class PhoneValidatorParityTests
{
    private static readonly ValidationCorpus sr_Corpus = ValidationFixture.Load("phone");

    public static TheoryData<string> RowNames()
    {
        var data = new TheoryData<string>();
        if (sr_Corpus.Rows.Falsey())
        {
            throw new InvalidOperationException(
                $"Phone corpus loaded with zero rows — version='{sr_Corpus.Version}'.");
        }

        foreach (var row in sr_Corpus.Rows)
            data.Add(row.Name);
        return data;
    }

    [Theory]
    [MemberData(nameof(RowNames))]
    public void Phone_Parity(string rowName)
    {
        var row = sr_Corpus.Rows.Single(r => r.Name == rowName);
        var input = ValidationFixture.SynthInput(row);

        // An absent / unparseable country maps to null — the no-default-region
        // path. (Every country string in the phone corpus is a real alpha-2.)
        _ = row.Country.TryParseTruthyNull<CountryCode>(out var country);

        var result = new DefaultPhoneValidator().Validate(input, country);

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
}
