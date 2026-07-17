// -----------------------------------------------------------------------
// <copyright file="DefaultPostalCodeValidator.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Location;

using System.Text.RegularExpressions;
using DcsvIo.D2.Geo.Abstractions;
using DcsvIo.D2.I18n;
using DcsvIo.D2.Result;
using DcsvIo.D2.Utilities.Extensions;

/// <summary>
/// Default <see cref="IPostalCodeValidator"/> — global-range shape check
/// only (3-10 alphanumeric characters; internal spaces and hyphens
/// allowed; alphanumeric at both ends). Country-blind by design;
/// consumers requiring per-country strict validation register an override
/// via <c>services.Replace(...)</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>ReDoS posture (§5.20).</b> The pattern is anchored at both ends,
/// the character class is bounded, and the inner repetition is
/// bounded (<c>{1,8}</c>). Worst-case time is linear in input length —
/// genuinely Bucket B1 shape — but DELIBERATELY reclassified to
/// <b>B2 + defense-in-depth</b>: the <c>matchTimeout = 50ms</c> and the
/// JIT pre-warm in the static constructor are retained as a guard-rail
/// against future-author regex edits (a maintainer relaxing the class
/// or removing an anchor could silently push the pattern into B3
/// territory).
/// </para>
/// </remarks>
public sealed class DefaultPostalCodeValidator : IPostalCodeValidator
{
    private static readonly TimeSpan sr_MatchTimeout = TimeSpan.FromMilliseconds(50);

    private static readonly Regex sr_GlobalShape = new(
        @"^[A-Z0-9](?:[A-Z0-9 \-]{1,8}[A-Z0-9])$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        sr_MatchTimeout);

    static DefaultPostalCodeValidator()
    {
        // JIT pre-warm — defense-in-depth posture per §5.20.
        _ = sr_GlobalShape.IsMatch("90210");
    }

    /// <inheritdoc />
    public D2Result<string> Validate(string? postalCode, CountryCode? countryCode = null)
    {
        if (postalCode.Falsey())
        {
            return D2Result<string>.ValidationFailed(
                messages: [TK.Geo.Validation.POSTAL_CODE_INVALID]);
        }

        var trimmed = postalCode!.Trim();

        bool isMatch;
        try
        {
            isMatch = sr_GlobalShape.IsMatch(trimmed);
        }
        catch (RegexMatchTimeoutException)
        {
            return D2Result<string>.ValidationFailed(
                messages: [TK.Geo.Validation.POSTAL_CODE_INVALID]);
        }

        if (!isMatch)
        {
            return D2Result<string>.ValidationFailed(
                messages: [TK.Geo.Validation.POSTAL_CODE_INVALID]);
        }

        return D2Result<string>.Ok(trimmed);
    }
}
