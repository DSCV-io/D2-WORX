// -----------------------------------------------------------------------
// <copyright file="EmailAddress.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Contacts.ValueObjects;

using DcsvIo.D2.I18n;
using DcsvIo.D2.Result;
using DcsvIo.D2.Utilities.Attributes;
using DcsvIo.D2.Utilities.Enums;
using DcsvIo.D2.Utilities.Extensions;
using DcsvIo.D2.Validation.Abstractions;

/// <summary>
/// Immutable email-address value object wrapping a single normalized value.
/// Validation runs in one of two modes: a structural floor (trim, collapse,
/// lowercase, basic <c>local@domain.tld</c> shape, length cap) when no validator
/// is supplied, or a caller-injected <see cref="IEmailValidator"/> whose
/// normalized output and failure messages are used verbatim.
/// </summary>
/// <remarks>
/// <b>Self-redacting PII.</b> <see cref="Value"/> is marked
/// <c>[RedactData(PersonalInformation)]</c> — an email address is directly
/// identifying and is masked automatically by the Serilog destructuring policy.
/// </remarks>
public sealed record EmailAddress
{
    /// <summary>Gets the normalized email address (trimmed, lowercased).</summary>
    [RedactData(Reason = RedactReason.PersonalInformation)]
    public required string Value { get; init; }

    /// <summary>
    /// Creates an <see cref="EmailAddress"/> from raw text. When
    /// <paramref name="validator"/> is supplied it is the sole authority (its
    /// normalized output is trusted and no additional length cap is applied);
    /// otherwise the structural floor validates the shape and enforces the
    /// maximum address length.
    /// </summary>
    /// <param name="value">The raw email address (may be null or whitespace).</param>
    /// <param name="validator">
    /// Optional smart validator. When supplied, its
    /// <see cref="D2Result{TData}"/> (normalized value on success, failure
    /// messages on failure) is propagated verbatim.
    /// </param>
    /// <returns>
    /// <c>Ok</c> wrapping the normalized address on success; the validator's
    /// failure (validator mode) or
    /// <see cref="D2Result{TData}.ValidationFailed"/> carrying the common
    /// invalid-email key / the contacts too-long key (floor mode) otherwise.
    /// </returns>
    public static D2Result<EmailAddress> Create(string? value, IEmailValidator? validator = null)
    {
        string normalized;
        if (validator is not null)
        {
            var validation = validator.Validate(value);

            if (!validation.Success)
                return D2Result<EmailAddress>.BubbleFail(validation);

            normalized = validation.Data!;
        }
        else
        {
            // Floor — reuse the shared TryParseEmail helper (trim + collapse +
            // lowercase + local@domain.tld shape; bubbles common_validation_EMAIL_INVALID).
            var parse = value.TryParseEmail();

            if (!parse.Success)
                return D2Result<EmailAddress>.BubbleFail(parse);

            normalized = parse.Data!;

            // The helper does not enforce the address-length ceiling — add it here.
            if (normalized.Length > FieldConstraints.EMAIL_MAX)
            {
                return D2Result<EmailAddress>.ValidationFailed(
                    messages: [TK.Contacts.Validation.EMAIL_TOO_LONG]);
            }
        }

        return D2Result<EmailAddress>.Ok(new EmailAddress { Value = normalized });
    }

    /// <summary>
    /// Reconstructs an <see cref="EmailAddress"/> from a trusted, previously-validated
    /// store value WITHOUT re-running validation. For the EF Core value-converter read
    /// side only — use <see cref="Create"/> for all user-supplied input.
    /// </summary>
    /// <param name="value">
    /// The stored email string retrieved from the database.
    /// </param>
    /// <returns>An <see cref="EmailAddress"/> whose <see cref="Value"/> is set verbatim.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="value"/> is <see langword="null"/>.
    /// </exception>
    public static EmailAddress FromTrusted(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new() { Value = value };
    }
}
