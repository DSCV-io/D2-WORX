// -----------------------------------------------------------------------
// <copyright file="Demographics.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Contacts.ValueObjects;

using DcsvIo.D2.I18n;
using DcsvIo.D2.Result;
using DcsvIo.D2.Time;
using DcsvIo.D2.Utilities.Attributes;
using DcsvIo.D2.Utilities.Enums;
using DcsvIo.D2.Validation.Abstractions;
using NodaTime;

// NodaTime also exposes IClock and SystemClock; alias the D2 seams to prevent CS0104.
using IClock = DcsvIo.D2.Time.IClock;
using SystemClock = DcsvIo.D2.Time.SystemClock;

/// <summary>
/// Immutable demographic value object: an optional date of birth and an optional
/// biological-sex classification. When supplied, the date of birth must not be in
/// the future and must fall within a 150-year window of the current date. The
/// all-null caller is rejected as a degenerate empty record.
/// </summary>
/// <remarks>
/// <b>Self-redacting special-category PII.</b> Both <see cref="DateOfBirth"/> and
/// <see cref="BiologicalSex"/> are marked <c>[RedactData(PersonalInformation)]</c>
/// — date of birth is directly identifying and biological sex is special-category
/// data; both are masked automatically by the Serilog destructuring policy.
/// </remarks>
public sealed record Demographics
{
    /// <summary>Gets the optional date of birth.</summary>
    [RedactData(Reason = RedactReason.PersonalInformation)]
    public LocalDate? DateOfBirth { get; init; }

    /// <summary>Gets the optional biological-sex classification.</summary>
    [RedactData(Reason = RedactReason.PersonalInformation)]
    public BiologicalSex? BiologicalSex { get; init; }

    /// <summary>
    /// Creates a <see cref="Demographics"/> from an optional date of birth and
    /// optional biological sex. The date of birth is bounded against the current
    /// date resolved from <paramref name="clock"/>.
    /// </summary>
    /// <param name="dateOfBirth">
    /// Optional date of birth; must not be in the future and must not be more
    /// than 150 years in the past.
    /// </param>
    /// <param name="biologicalSex">Optional biological-sex classification.</param>
    /// <param name="clock">
    /// Optional clock used to resolve the current date for the date-of-birth
    /// bounds. Defaults to <see cref="SystemClock"/>; tests inject a
    /// <see cref="TestClock"/> fixed to a deterministic instant for boundary
    /// coverage.
    /// </param>
    /// <returns>
    /// <c>Ok</c> on success;
    /// <see cref="D2Result{TData}.ValidationFailed"/> for: all-null inputs, a
    /// future date of birth, or a date of birth more than 150 years in the past.
    /// </returns>
    public static D2Result<Demographics> Create(
        LocalDate? dateOfBirth = null,
        BiologicalSex? biologicalSex = null,
        IClock? clock = null)
    {
        // Degenerate empty record — both fields absent.
        if (dateOfBirth is null && biologicalSex is null)
        {
            return D2Result<Demographics>.ValidationFailed(
                messages: [TK.Contacts.Validation.DEMOGRAPHICS_EMPTY_RECORD]);
        }

        if (dateOfBirth is { } dob)
        {
            var today = (clock ?? new SystemClock()).GetCurrentInstant().InUtc().Date;

            // Future — strictly greater than today (born-today is valid).
            if (dob > today)
            {
                return D2Result<Demographics>.ValidationFailed(
                    messages: [TK.Contacts.Validation.DOB_FUTURE]);
            }

            // Too old — older than 150 years. The exactly-150-years boundary is
            // valid (floor is inclusive; comparison is strictly less-than).
            var floor = today.PlusYears(-150);
            if (dob < floor)
            {
                return D2Result<Demographics>.ValidationFailed(
                    messages: [TK.Contacts.Validation.DOB_TOO_OLD]);
            }
        }

        return D2Result<Demographics>.Ok(new Demographics
        {
            DateOfBirth = dateOfBirth,
            BiologicalSex = biologicalSex,
        });
    }
}
