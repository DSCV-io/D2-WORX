// -----------------------------------------------------------------------
// <copyright file="PhoneNumber.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Contacts.ValueObjects;

using DcsvIo.D2.Geo.Abstractions;
using DcsvIo.D2.I18n;
using DcsvIo.D2.Result;
using DcsvIo.D2.Utilities.Attributes;
using DcsvIo.D2.Utilities.Enums;
using DcsvIo.D2.Utilities.Extensions;
using DcsvIo.D2.Validation.Abstractions;

/// <summary>
/// Immutable phone-number value object wrapping a single normalized value.
/// Validation runs in one of two modes: a structural floor (strip non-digits,
/// enforce the 7-15 digit envelope and the raw-length cap) when no validator is
/// supplied, or a caller-injected <see cref="IPhoneValidator"/> whose
/// E.164-normalized output and failure messages are used verbatim. The stored
/// value is a digit string in floor mode and the validator's normalized form
/// (typically E.164) in validator mode.
/// </summary>
/// <remarks>
/// <b>Self-redacting PII.</b> <see cref="Value"/> is marked
/// <c>[RedactData(PersonalInformation)]</c> — a phone number is directly
/// identifying and is masked automatically by the Serilog destructuring policy.
/// </remarks>
public sealed record PhoneNumber
{
    /// <summary>
    /// Gets the normalized phone number (digit string in floor mode; the
    /// validator's normalized form, typically E.164, in validator mode).
    /// </summary>
    [RedactData(Reason = RedactReason.PersonalInformation)]
    public required string Value { get; init; }

    /// <summary>
    /// Creates a <see cref="PhoneNumber"/> from raw text. When
    /// <paramref name="validator"/> is supplied it is the sole authority (its
    /// normalized output is trusted and no additional length cap is applied) and
    /// <paramref name="region"/> is forwarded to it; otherwise the structural
    /// floor strips non-digits, enforces the digit envelope, and caps the raw
    /// input length (<paramref name="region"/> is ignored in floor mode — there
    /// is no national-format interpretation without a validator).
    /// </summary>
    /// <param name="value">The raw phone number (may be null or whitespace).</param>
    /// <param name="validator">
    /// Optional smart validator. When supplied, its
    /// <see cref="D2Result{TData}"/> (E.164-normalized value on success, failure
    /// messages on failure) is propagated verbatim.
    /// </param>
    /// <param name="region">
    /// Optional ISO 3166-1 alpha-2 region forwarded to the validator to interpret
    /// national-format input. Ignored in floor mode.
    /// </param>
    /// <returns>
    /// <c>Ok</c> wrapping the normalized number on success; the validator's
    /// failure (validator mode) or
    /// <see cref="D2Result{TData}.ValidationFailed"/> carrying the common
    /// invalid-phone key / the contacts too-long key (floor mode) otherwise.
    /// </returns>
    public static D2Result<PhoneNumber> Create(
        string? value,
        IPhoneValidator? validator = null,
        CountryCode? region = null)
    {
        string normalized;
        if (validator is not null)
        {
            var validation = validator.Validate(value, region);

            if (!validation.Success)
                return D2Result<PhoneNumber>.BubbleFail(validation);

            normalized = validation.Data!;
        }
        else
        {
            // Raw-input guard — the 32-char ceiling is on the raw string,
            // independent of the digit count enforced by the helper below.
            var trimmed = value.ToNullIfEmpty();
            if (trimmed is not null && trimmed.Length > FieldConstraints.PHONE_E164_MAX)
            {
                return D2Result<PhoneNumber>.ValidationFailed(
                    messages: [TK.Contacts.Validation.PHONE_TOO_LONG]);
            }

            // Floor — reuse the shared TryParsePhoneNumber helper (strip non-digits,
            // 7-15 envelope; bubbles common_validation_PHONE_INVALID).
            // Use trimmed (same string the length guard ran against) so guard + parse
            // operate on one value, not two.
            var parse = (trimmed ?? value).TryParsePhoneNumber();

            if (!parse.Success)
                return D2Result<PhoneNumber>.BubbleFail(parse);

            normalized = parse.Data!;
        }

        return D2Result<PhoneNumber>.Ok(new PhoneNumber { Value = normalized });
    }

    /// <summary>
    /// Reconstructs a <see cref="PhoneNumber"/> from a trusted, previously-validated
    /// store value WITHOUT re-running validation. For the EF Core value-converter read
    /// side only — use <see cref="Create"/> for all user-supplied input.
    /// </summary>
    /// <param name="value">
    /// The stored phone string retrieved from the database.
    /// </param>
    /// <returns>A <see cref="PhoneNumber"/> whose <see cref="Value"/> is set verbatim.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="value"/> is <see langword="null"/>.
    /// </exception>
    public static PhoneNumber FromTrusted(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new() { Value = value };
    }
}
