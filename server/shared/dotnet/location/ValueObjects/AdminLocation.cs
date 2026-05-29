// -----------------------------------------------------------------------
// <copyright file="AdminLocation.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Location.ValueObjects;

using System.Security.Cryptography;
using System.Text;
using D2.Shared.Geo.Abstractions;
using D2.Shared.I18n;
using D2.Shared.Result;
using D2.Shared.Utilities.Extensions;

/// <summary>
/// Immutable administrative-hierarchy location value object: country,
/// subdivision, city, postal code (any subset). Coherence is enforced
/// when both country and subdivision are supplied — a mismatch returns
/// <see cref="D2Result{TData}.ValidationFailed"/>. A subdivision-only
/// caller has country auto-populated from
/// <see cref="SubdivisionCode.ParentCountry"/>. A country-only caller
/// is valid (city / postal optional). The all-null caller is rejected
/// as a degenerate empty record.
/// </summary>
/// <remarks>
/// <b>PII.</b> <see cref="City"/> + <see cref="PostalCode"/> are PII when
/// combined with other identifying fields (per GDPR; city + postal beyond
/// country level). Consumers MUST apply <c>[RedactData]</c> on any field
/// of this type before it reaches a logger / serializer sink.
/// </remarks>
public sealed record AdminLocation
{
    /// <summary>Gets the optional city (post-normalization).</summary>
    public string? City { get; init; }

    /// <summary>Gets the optional postal code (post-validation).</summary>
    public string? PostalCode { get; init; }

    /// <summary>Gets the optional ISO 3166-2 subdivision code.</summary>
    public SubdivisionCode? SubdivisionIso31662Code { get; init; }

    /// <summary>
    /// Gets the optional ISO 3166-1 alpha-2 country code. Auto-populated
    /// from <see cref="SubdivisionIso31662Code"/> when the caller supplies a
    /// subdivision but no explicit country.
    /// </summary>
    public CountryCode? CountryIso31661Alpha2Code { get; init; }

    /// <summary>
    /// Gets the stable hash identifier:
    /// <c>"v1." + SHA-256(NormalizeForHash(City) | NormalizeForHash(PostalCode)
    /// | SubdivisionIso31662Code | CountryIso31661Alpha2Code)</c>
    /// as lowercase hex. Missing slots contribute <c>""</c>.
    /// </summary>
    public required string HashId { get; init; }

    /// <summary>
    /// Creates an <see cref="AdminLocation"/> from any subset of the four
    /// administrative fields. Enforces country/subdivision coherence and
    /// auto-populates country from subdivision when applicable.
    /// </summary>
    /// <param name="countryIso31661Alpha2Code">Optional ISO 3166-1 alpha-2 country code.</param>
    /// <param name="subdivisionIso31662Code">Optional ISO 3166-2 subdivision code.</param>
    /// <param name="city">Optional city name (free text).</param>
    /// <param name="postalCode">Optional postal code (free text).</param>
    /// <param name="postalCodeValidator">
    /// Optional validator. When supplied with a non-null
    /// <paramref name="postalCode"/>, the validator's
    /// <see cref="D2Result{TData}"/> failure is propagated. When null,
    /// no postal-code validation is performed.
    /// </param>
    /// <returns>
    /// <c>Ok</c> on success;
    /// <see cref="D2Result{TData}.ValidationFailed"/> for: all-null inputs,
    /// country/subdivision mismatch, or postal-code validation failure.
    /// </returns>
    public static D2Result<AdminLocation> Create(
        CountryCode? countryIso31661Alpha2Code = null,
        SubdivisionCode? subdivisionIso31662Code = null,
        string? city = null,
        string? postalCode = null,
        IPostalCodeValidator? postalCodeValidator = null)
    {
        var cleanedCity = city.CleanStr();
        var cleanedPostal = postalCode.CleanStr();

        // Coherence + auto-populate.
        var effectiveCountry = countryIso31661Alpha2Code;
        if (subdivisionIso31662Code is { } sub)
        {
            var derived = sub.ParentCountry;
            if (countryIso31661Alpha2Code is { } supplied && supplied != derived)
            {
                return D2Result<AdminLocation>.ValidationFailed(
                    messages: [TK.Geo.Validation.ADMIN_COUNTRY_SUBDIVISION_MISMATCH]);
            }

            effectiveCountry ??= derived;
        }

        // Degenerate empty record — all four fields null/empty after cleaning.
        if (effectiveCountry is null && subdivisionIso31662Code is null
            && cleanedCity.Falsey() && cleanedPostal.Falsey())
        {
            return D2Result<AdminLocation>.ValidationFailed(
                messages: [TK.Geo.Validation.ADMIN_EMPTY_RECORD]);
        }

        // Postal-code validation (only when both validator + value supplied).
        var validatedPostal = cleanedPostal;
        if (cleanedPostal.Truthy() && postalCodeValidator is not null)
        {
            var validation = postalCodeValidator.Validate(cleanedPostal, effectiveCountry);
            if (!validation.Success)
                return D2Result<AdminLocation>.ValidationFailed(messages: validation.Messages);

            validatedPostal = validation.Data;
        }

        // Hash inputs:
        //   city + postal — pass through NormalizeForHash (Unicode-category-aware filter).
        //   subdivision  — code-form (e.g. "US-NY").
        //   country      — code-form (alpha-2 enum name, e.g. "US").
        var hashInput =
            StreetAddress.NormalizeForHash(cleanedCity) + "|" +
            StreetAddress.NormalizeForHash(validatedPostal) + "|" +
            (subdivisionIso31662Code?.Value ?? string.Empty) + "|" +
            (effectiveCountry?.ToString() ?? string.Empty);

        // BCL static one-shot per §15.8 — no IDisposable instance to manage.
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(hashInput));
        var hashId = "v1." + Convert.ToHexStringLower(hashBytes);

        return D2Result<AdminLocation>.Ok(new AdminLocation
        {
            City = cleanedCity,
            PostalCode = validatedPostal,
            SubdivisionIso31662Code = subdivisionIso31662Code,
            CountryIso31661Alpha2Code = effectiveCountry,
            HashId = hashId,
        });
    }
}
