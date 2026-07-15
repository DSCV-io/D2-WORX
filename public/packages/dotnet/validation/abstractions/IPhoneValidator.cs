// -----------------------------------------------------------------------
// <copyright file="IPhoneValidator.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Validation.Abstractions;

using DcsvIo.D2.Geo.Abstractions;
using DcsvIo.D2.Result;

/// <summary>
/// Validates a phone number and returns an E.164-normalized form on success.
/// </summary>
public interface IPhoneValidator
{
    /// <summary>
    /// Validates <paramref name="phone"/> and returns the E.164-normalized number on success.
    /// </summary>
    /// <param name="phone">The phone number to validate (may be null or whitespace).</param>
    /// <param name="defaultRegion">
    /// Optional ISO 3166-1 alpha-2 country code used to interpret national-format input
    /// (e.g. <c>"2125551234"</c> with <c>defaultRegion = CountryCode.US</c> resolves
    /// to <c>"+12125551234"</c>). When <see langword="null"/>, the input must already
    /// carry an explicit international dialing prefix.
    /// </param>
    /// <returns>
    /// <c>Ok</c> wrapping the E.164-normalized number (e.g. <c>"+12125551234"</c>) on success;
    /// <see cref="D2Result{TData}.ValidationFailed"/> with a per-field
    /// <see cref="DcsvIo.D2.Result.InputError"/> on null, empty, whitespace, or
    /// structurally invalid input.
    /// </returns>
    D2Result<string> Validate(string? phone, CountryCode? defaultRegion = null);
}
