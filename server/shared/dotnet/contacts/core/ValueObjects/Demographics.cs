// -----------------------------------------------------------------------
// <copyright file="Demographics.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Contacts.ValueObjects;

using D2.Shared.I18n;
using D2.Shared.Result;
using D2.Shared.Utilities.Attributes;
using D2.Shared.Utilities.Enums;
using D2.Shared.Validation.Abstractions;

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
    public DateOnly? DateOfBirth { get; init; }

    /// <summary>Gets the optional biological-sex classification.</summary>
    [RedactData(Reason = RedactReason.PersonalInformation)]
    public BiologicalSex? BiologicalSex { get; init; }

    /// <summary>
    /// Creates a <see cref="Demographics"/> from an optional date of birth and
    /// optional biological sex. The date of birth is bounded against the current
    /// date resolved from <paramref name="timeProvider"/>.
    /// </summary>
    /// <param name="dateOfBirth">
    /// Optional date of birth; must not be in the future and must not be more
    /// than 150 years in the past.
    /// </param>
    /// <param name="biologicalSex">Optional biological-sex classification.</param>
    /// <param name="timeProvider">
    /// Optional clock used to resolve the current date for the date-of-birth
    /// bounds. Defaults to <see cref="TimeProvider.System"/>; tests inject a
    /// fixed provider for deterministic boundary coverage.
    /// </param>
    /// <returns>
    /// <c>Ok</c> on success;
    /// <see cref="D2Result{TData}.ValidationFailed"/> for: all-null inputs, a
    /// future date of birth, or a date of birth more than 150 years in the past.
    /// </returns>
    public static D2Result<Demographics> Create(
        DateOnly? dateOfBirth = null,
        BiologicalSex? biologicalSex = null,
        TimeProvider? timeProvider = null)
    {
        // Degenerate empty record — both fields absent.
        if (dateOfBirth is null && biologicalSex is null)
        {
            return D2Result<Demographics>.ValidationFailed(
                messages: [TK.Contacts.Validation.DEMOGRAPHICS_EMPTY_RECORD]);
        }

        if (dateOfBirth is { } dob)
        {
            var clock = timeProvider ?? TimeProvider.System;
            var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

            // Future — strictly greater than today (born-today is valid).
            if (dob > today)
            {
                return D2Result<Demographics>.ValidationFailed(
                    messages: [TK.Contacts.Validation.DOB_FUTURE]);
            }

            // Too old — older than 150 years. The exactly-150-years boundary is
            // valid (floor is inclusive; comparison is strictly less-than).
            var floor = today.AddYears(-150);
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
