// -----------------------------------------------------------------------
// <copyright file="PostalCodeValidatorParityTests.cs" company="DCSV">
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
/// Asserts the .NET <see cref="DefaultPostalCodeValidator"/> produces the SAME
/// verdict + normalized (trim + uppercase) form as the hand-authored
/// cross-language corpus at <c>contracts/validation/fixtures/postcode.json</c>.
/// The TypeScript <c>@dcsv-io/d2-validation</c> parity test asserts the identical
/// corpus. The unknown-country (<c>ZZ</c>) and no-country rows exercise the
/// shared FAIL-CLOSED policy: an unparseable / absent country yields a
/// <c>null</c> <see cref="CountryCode"/>, which the validator rejects with no
/// permissive fallback — matching the TS side (where an unknown country throws
/// inside <c>postcode-validator</c> and is caught as a validation failure).
/// </summary>
public sealed class PostalCodeValidatorParityTests
{
    private static readonly ValidationCorpus sr_Corpus = ValidationFixture.Load("postcode");

    public static TheoryData<string> RowNames()
    {
        var data = new TheoryData<string>();
        if (sr_Corpus.Rows.Falsey())
        {
            throw new InvalidOperationException(
                $"Postcode corpus loaded with zero rows — version='{sr_Corpus.Version}'.");
        }

        foreach (var row in sr_Corpus.Rows)
            data.Add(row.Name);
        return data;
    }

    [Theory]
    [MemberData(nameof(RowNames))]
    public void PostalCode_Parity(string rowName)
    {
        var row = sr_Corpus.Rows.Single(r => r.Name == rowName);
        var input = ValidationFixture.SynthInput(row);

        // "ZZ" (and any unknown alpha-2) does NOT parse to a CountryCode member,
        // so country resolves to null — exercising the fail-closed path. The
        // "no country" row likewise has a null Country string.
        _ = row.Country.TryParseTruthyNull<CountryCode>(out var country);

        var result = new DefaultPostalCodeValidator().Validate(input, country);

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
