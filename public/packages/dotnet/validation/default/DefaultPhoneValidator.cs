// -----------------------------------------------------------------------
// <copyright file="DefaultPhoneValidator.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Validation;

using DcsvIo.D2.Geo.Abstractions;
using DcsvIo.D2.I18n;
using DcsvIo.D2.Result;
using DcsvIo.D2.Utilities.Extensions;
using DcsvIo.D2.Validation.Abstractions;
using PhoneNumbers;

/// <summary>
/// Default <see cref="IPhoneValidator"/> — delegates to
/// <see cref="PhoneNumberUtil"/> (libphonenumber-csharp, Apache-2.0) for
/// parsing, validation, and E.164 normalization. Mirrors the TypeScript
/// <c>@dcsv-io/d2-validation</c> <c>DefaultPhoneValidator</c> in contract and behavior.
/// </summary>
/// <remarks>
/// <para>
/// When <c>defaultRegion</c> is <see langword="null"/> and the input does not
/// start with an explicit international dialing prefix (<c>+</c>),
/// <see cref="PhoneNumberUtil.Parse(string, string)"/> throws
/// <see cref="NumberParseException"/>
/// — this is treated as a validation failure (not an exception escape).
/// </para>
/// <para>
/// The singleton <see cref="PhoneNumberUtil"/> instance is thread-safe; the
/// validator is safe to register as a DI singleton.
/// </para>
/// </remarks>
public sealed class DefaultPhoneValidator : IPhoneValidator
{
    private static readonly PhoneNumberUtil sr_PhoneUtil = PhoneNumberUtil.GetInstance();

    static DefaultPhoneValidator()
    {
        // JIT pre-warm — force the libphonenumber metadata to load at startup
        // so the first real call does not incur the metadata-initialization cost.
        try
        {
            _ = sr_PhoneUtil.Parse("+12125551234", null);
        }
        catch (NumberParseException)
        {
            // Pre-warm only; parse errors during warm-up are irrelevant.
        }
    }

    /// <inheritdoc />
    public D2Result<string> Validate(string? phone, CountryCode? defaultRegion = null)
    {
        if (phone.Falsey())
            return Invalid();

        // CountryCode enum names are the ISO 3166-1 alpha-2 codes (e.g. US, GB).
        // ToString() produces the exact region string libphonenumber expects.
        var region = defaultRegion?.ToString();

        PhoneNumber parsed;
        try
        {
            parsed = sr_PhoneUtil.Parse(phone, region);
        }
        catch (NumberParseException)
        {
            return Invalid();
        }

        if (!sr_PhoneUtil.IsValidNumber(parsed))
            return Invalid();

        return D2Result<string>.Ok(sr_PhoneUtil.Format(parsed, PhoneNumberFormat.E164));
    }

    private static D2Result<string> Invalid()
        => D2Result<string>.ValidationFailed(
            inputErrors: [new InputError("phone", [TK.Common.Validation.PHONE_INVALID])]);
}
