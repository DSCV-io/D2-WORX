// -----------------------------------------------------------------------
// <copyright file="IPostalCodeValidator.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Location;

using DcsvIo.D2.Geo.Abstractions;
using DcsvIo.D2.Result;

/// <summary>
/// Boundary validator for postal codes invoked by
/// <see cref="ValueObjects.AdminLocation"/> (and any other consumer
/// that needs structured postal-code validation). Lives in
/// <c>DcsvIo.D2.Location</c> (NOT Abstractions) so the DI seam stays
/// out of pure-vocabulary projects per the Plan §4.5 layer policy.
/// </summary>
public interface IPostalCodeValidator
{
    /// <summary>
    /// Validates a postal code. <paramref name="countryCode"/> is
    /// optional — the default implementation ignores it (global-range
    /// regex only); consumer implementations may use it for
    /// country-specific validation.
    /// </summary>
    /// <param name="postalCode">The postal code to validate (may be null / whitespace).</param>
    /// <param name="countryCode">
    /// Optional ISO 3166-1 alpha-2 country code for country-aware validation.
    /// </param>
    /// <returns>
    /// <c>Ok</c> wrapping the normalized postal code on success;
    /// <see cref="D2Result{TData}.ValidationFailed"/> for null / empty /
    /// out-of-range / out-of-shape inputs.
    /// </returns>
    D2Result<string> Validate(string? postalCode, CountryCode? countryCode = null);
}
