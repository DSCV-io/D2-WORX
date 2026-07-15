// -----------------------------------------------------------------------
// <copyright file="DefaultPostalCodeValidator.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Validation;

using System.Text.RegularExpressions;
using DcsvIo.D2.Geo.Abstractions;
using DcsvIo.D2.I18n;
using DcsvIo.D2.Result;
using DcsvIo.D2.Utilities.Extensions;
using DcsvIo.D2.Validation.Abstractions;

/// <summary>
/// Default <see cref="IPostalCodeValidator"/> — country-aware structural check
/// using a per-country regex map ported from <c>postcode-validator@3.10.9</c>.
/// Mirrors the TypeScript <c>@dcsv-io/d2-validation</c> <c>DefaultPostalCodeValidator</c>
/// in contract and behavior.
/// </summary>
/// <remarks>
/// <para>
/// <b>Fail-closed policy.</b> When the country code is <see langword="null"/> or
/// its alpha-2 key is absent from the dataset, validation fails with
/// <see cref="TK.Common.Validation.POSTAL_CODE_INVALID"/>. There is no fallback
/// to a permissive global-range pattern — unknown-country input always fails closed.
/// </para>
/// <para>
/// <b>Normalization.</b> The input is trimmed and uppercased before the regex
/// runs and before the normalized value is returned.
/// </para>
/// <para>
/// <b>ReDoS posture: anchored, bounded — no catastrophic backtracking
/// (Bucket B2 + defense-in-depth).</b>
/// All per-country patterns are anchored at both ends, compiled, and carry a 50 ms
/// <c>matchTimeout</c>. The regexes are sourced verbatim from
/// <c>postcode-validator@3.10.9</c> (patterns that lacked anchors in the
/// source were wrapped as <c>^(?:...)$</c>). Because a future dataset update
/// could introduce a pattern with super-linear backtracking, the 50 ms timeout
/// is retained as a mandatory guard-rail on every entry. The map is built once
/// at static initialization via <see cref="PostalCodeRegexData.SR_Map"/>.
/// </para>
/// </remarks>
public sealed class DefaultPostalCodeValidator : IPostalCodeValidator
{
    static DefaultPostalCodeValidator()
    {
        // Pre-warm the static map so the first real validation call does not
        // incur the JSON deserialization + FrozenDictionary build cost.
        // PostalCodeRegexData.SR_Map access triggers the type initializer.
        _ = PostalCodeRegexData.SR_Map.Count;
    }

    /// <inheritdoc />
    public D2Result<string> Validate(string? postalCode, CountryCode? countryCode = null)
    {
        if (postalCode.Falsey())
            return Invalid();

        var normalized = postalCode!.Trim().ToUpperInvariant();

        if (countryCode is null)
            return Invalid();

        // CountryCode enum names are the ISO 3166-1 alpha-2 codes (e.g. US, GB).
        var key = countryCode.Value.ToString();

        if (!PostalCodeRegexData.SR_Map.TryGetValue(key, out var regex))
            return Invalid();

        bool isMatch;
        try
        {
            isMatch = regex.IsMatch(normalized);
        }
        catch (RegexMatchTimeoutException)
        {
            return Invalid();
        }

        if (!isMatch)
            return Invalid();

        return D2Result<string>.Ok(normalized);
    }

    private static D2Result<string> Invalid()
        => D2Result<string>.ValidationFailed(
            inputErrors:
            [
                new InputError("postalCode", [TK.Common.Validation.POSTAL_CODE_INVALID]),
            ]);
}
