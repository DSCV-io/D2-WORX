// -----------------------------------------------------------------------
// <copyright file="IPostalCodeValidator.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Validation.Abstractions;

using DcsvIo.D2.Geo.Abstractions;
using DcsvIo.D2.Result;

/// <summary>
/// Validates a postal code against the given country's format and returns a
/// normalized form on success.
/// </summary>
/// <remarks>
/// <para>
/// This interface (<c>DcsvIo.D2.Validation.Abstractions.IPostalCodeValidator</c>) is the
/// <em>country-aware</em> member of the validator family — it accepts an explicit
/// <see cref="CountryCode"/> to apply country-specific format rules and normalization.
/// <b>Fail-closed contract:</b> a null or unrecognized country code causes validation to
/// fail immediately; there is no permissive global-range fallback.
/// </para>
/// <para>
/// A deliberately distinct twin exists at
/// <c>DcsvIo.D2.Location.IPostalCodeValidator</c>: that interface is the
/// <em>country-blind</em> boundary validator used by value-object construction
/// (global-range regex only, no country-specific rules). The two interfaces share
/// a short name but live in separate namespaces by design. Consumers that require
/// both may alias one with a <c>using</c> directive.
/// </para>
/// </remarks>
public interface IPostalCodeValidator
{
    /// <summary>
    /// Validates <paramref name="postalCode"/> and returns the normalized value on success.
    /// </summary>
    /// <param name="postalCode">The postal code to validate (may be null or whitespace).</param>
    /// <param name="countryCode">
    /// Optional ISO 3166-1 alpha-2 country code used to apply country-specific format rules.
    /// When <see langword="null"/>, validation fails closed: the method returns
    /// <see cref="D2Result{TData}.ValidationFailed"/> with
    /// <c>POSTAL_CODE_INVALID</c>. There is no permissive global-range fallback.
    /// </param>
    /// <returns>
    /// <c>Ok</c> wrapping the trimmed and uppercased postal code on success;
    /// <see cref="D2Result{TData}.ValidationFailed"/> with a per-field
    /// <see cref="DcsvIo.D2.Result.InputError"/> on null, empty, whitespace, or
    /// format-invalid input.
    /// </returns>
    D2Result<string> Validate(string? postalCode, CountryCode? countryCode = null);
}
