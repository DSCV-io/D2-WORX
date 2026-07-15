// -----------------------------------------------------------------------
// <copyright file="IsoDuration.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Time;

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using DcsvIo.D2.I18n;
using DcsvIo.D2.Result;
using DcsvIo.D2.Utilities.Extensions;
using NodaTime;

/// <summary>
/// Lossless ISO-8601 duration ↔ NodaTime <see cref="Duration"/> bridge,
/// including sub-second decimal-fraction seconds to nanosecond precision.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists.</b> NodaTime exposes no built-in pattern that parses
/// the ISO-8601 duration form (<c>P[n]DT[n]H[n]M[n]S</c>). Its
/// <see cref="NodaTime.Text.DurationPattern.Roundtrip"/> uses the
/// colon-separated <c>-D:hh:mm:ss.fffffffff</c> form, and
/// <see cref="NodaTime.Text.PeriodPattern"/> uses explicit unit fields with
/// no decimal-fraction seconds. The TypeScript wire counterpart
/// (<c>Temporal.Duration</c>) round-trips ISO-8601 — including a decimal
/// fraction on the seconds field (<c>"PT0.123456789S"</c>) — natively. This
/// helper is the .NET side of that cross-language wire contract: an
/// ISO-8601 duration STRING that materializes to the equivalent
/// <see cref="Duration"/> value in both languages, sub-second precision
/// included.
/// </para>
/// <para>
/// <b>No floating point.</b> Every quantity is computed as an
/// <see cref="System.Int128"/> count of nanoseconds — days, hours, minutes,
/// and whole seconds are multiplied by their exact nanosecond factors, and
/// the fractional-seconds field is right-padded to exactly nine digits and
/// added as an integer. The result is handed to
/// <see cref="Duration.FromNanoseconds(System.Int128)"/>, which is exact.
/// <see cref="Format"/> decomposes via
/// <see cref="Duration.ToInt128Nanoseconds"/> the same way. No
/// <see cref="double"/> / <see cref="float"/> appears anywhere in the math,
/// so no binary-floating-point rounding can corrupt a value.
/// </para>
/// <para>
/// <b>Canonical output.</b> <see cref="Format"/> emits a balanced
/// hours/minutes/seconds form (<c>PT[n]H[n]M[n]S</c>) carrying only the
/// non-zero components, with the fractional-seconds field trimmed to its
/// minimal digit count, and <c>"PT0S"</c> for a zero duration. Because a
/// <see cref="Duration"/> is a bare nanosecond quantity (it carries no
/// memory of which units the caller authored), the round-trip contract the
/// suites assert is VALUE equality (total nanoseconds), not byte-identical
/// strings — a <c>"PT90M"</c> input round-trips to the same value as its
/// balanced <c>"PT1H30M"</c> rendering. The sub-second decimal-fraction case
/// is byte-stable (<c>"PT0.123456789S"</c> → value → <c>"PT0.123456789S"</c>)
/// because there is no larger unit to balance into.
/// </para>
/// </remarks>
public static partial class IsoDuration
{
    private const long _NANOS_PER_SECOND = 1_000_000_000L;
    private const long _NANOS_PER_MINUTE = 60L * _NANOS_PER_SECOND;
    private const long _NANOS_PER_HOUR = 60L * _NANOS_PER_MINUTE;
    private const long _NANOS_PER_DAY = 24L * _NANOS_PER_HOUR;
    private const int _FRACTION_DIGITS = 9;

    /// <summary>
    /// Parses an ISO-8601 duration string (<c>P[n]DT[n]H[n]M[n[.fffffffff]]S</c>)
    /// into a NodaTime <see cref="Duration"/> losslessly, including a
    /// decimal-fraction seconds field to nanosecond precision.
    /// </summary>
    /// <param name="iso">
    /// The ISO-8601 duration string. May carry a leading <c>-</c> sign,
    /// whole days, and a time section with hours / minutes / seconds; the
    /// seconds field accepts up to nine fractional digits. Unbalanced
    /// components are accepted (e.g. <c>"PT90M"</c>, <c>"PT3600S"</c>,
    /// <c>"PT26H"</c>) — the <c>Temporal.Duration</c> wire emits these. Year /
    /// month / week designators are rejected (calendar-relative, never on the
    /// wire). At least one component must be present (a bare <c>"P"</c> /
    /// <c>"PT"</c> is rejected).
    /// </param>
    /// <returns>
    /// <c>D2Result&lt;Duration&gt;.Ok(...)</c> with the exact value, or
    /// <see cref="D2Result{TData}.ValidationFailed"/> carrying
    /// <c>TK.Common.Time.INVALID_DURATION</c> when <paramref name="iso"/> is
    /// null / empty / whitespace, syntactically malformed, or resolves to a
    /// magnitude outside NodaTime's representable
    /// <see cref="Duration"/> range. Error-as-value — never throws on bad
    /// input (the codebase's smart-constructor contract).
    /// </returns>
    public static D2Result<Duration> Parse(string? iso)
    {
        if (iso.Falsey())
            return Invalid();

        var match = IsoDurationRegex().Match(iso!);
        if (!match.Success)
            return Invalid();

        // Reject the no-component forms ("P", "PT", "-P", "-PT") that the
        // optional groups would otherwise let through — a duration must name
        // at least one unit.
        if (!HasAnyComponent(match))
            return Invalid();

        var totalNanos = ComputeTotalNanoseconds(match, out var overflow);
        if (overflow)
            return Invalid();

        // Duration.FromNanoseconds(Int128) is exact within NodaTime's
        // representable range. A nanosecond count that fits in Int128 but
        // exceeds the Duration range throws ArgumentOutOfRangeException;
        // convert that to the wrapped failure so the contract stays
        // error-as-value (never throws on bad / out-of-range input).
        try
        {
            return D2Result<Duration>.Ok(Duration.FromNanoseconds(totalNanos));
        }
        catch (ArgumentOutOfRangeException)
        {
            return Invalid();
        }
    }

    /// <summary>
    /// Formats a NodaTime <see cref="Duration"/> to its canonical ISO-8601
    /// string (<c>PT[n]H[n]M[n[.fffffffff]]S</c>), balanced into
    /// hours / minutes / seconds with only the non-zero components emitted and
    /// the fractional-seconds field trimmed to its minimal digit count.
    /// </summary>
    /// <param name="duration">The duration to render. Any value in NodaTime's representable range.</param>
    /// <returns>
    /// The ISO-8601 string. A zero duration renders as <c>"PT0S"</c>; a
    /// negative duration carries a leading <c>-</c>. Fractional seconds are
    /// emitted only when non-zero, with trailing zeros stripped
    /// (e.g. <c>500_000_000</c> ns → <c>"PT0.5S"</c>,
    /// <c>123_456_789</c> ns → <c>"PT0.123456789S"</c>).
    /// </returns>
    public static string Format(Duration duration)
    {
        var totalNanos = duration.ToInt128Nanoseconds();
        if (totalNanos == Int128.Zero)
            return "PT0S";

        var negative = totalNanos < Int128.Zero;
        var magnitude = negative ? -totalNanos : totalNanos;

        var hours = (long)(magnitude / _NANOS_PER_HOUR);
        var afterHours = magnitude % _NANOS_PER_HOUR;
        var minutes = (long)(afterHours / _NANOS_PER_MINUTE);
        var afterMinutes = afterHours % _NANOS_PER_MINUTE;
        var seconds = (long)(afterMinutes / _NANOS_PER_SECOND);
        var fraction = (long)(afterMinutes % _NANOS_PER_SECOND);

        var sb = new StringBuilder();
        if (negative)
            sb.Append('-');

        sb.Append("PT");

        if (hours != 0)
            sb.Append(hours.ToString(CultureInfo.InvariantCulture)).Append('H');

        if (minutes != 0)
            sb.Append(minutes.ToString(CultureInfo.InvariantCulture)).Append('M');

        // Emit the seconds field when it is non-zero OR when no higher unit
        // was emitted (so a sub-second-only duration still names a unit, and a
        // value that balances to exactly N hours/minutes never produces a
        // trailing bare "PT").
        var emittedHigher = hours != 0 || minutes != 0;
        if (seconds != 0 || fraction != 0 || !emittedHigher)
        {
            sb.Append(seconds.ToString(CultureInfo.InvariantCulture));

            if (fraction != 0)
                AppendFraction(sb, fraction);

            sb.Append('S');
        }

        return sb.ToString();
    }

    private static bool HasAnyComponent(Match match) =>
        match.Groups["days"].Success
        || match.Groups["hours"].Success
        || match.Groups["minutes"].Success
        || match.Groups["seconds"].Success;

    private static Int128 ComputeTotalNanoseconds(Match match, out bool overflow)
    {
        overflow = false;

        // Each component is bounded by the regex to a string of decimal
        // digits, but an attacker could supply an arbitrarily long run; parse
        // each as Int128 and surface an overflow rather than throwing.
        if (!TryComponent(match, "days", out var days)
            || !TryComponent(match, "hours", out var hours)
            || !TryComponent(match, "minutes", out var minutes)
            || !TryComponent(match, "seconds", out var seconds))
        {
            overflow = true;
            return Int128.Zero;
        }

        // Fractional seconds: right-pad the captured digits to exactly nine
        // (nanosecond resolution), then parse as an integer count of
        // nanoseconds. No division, no float — a right-pad is the exact
        // decimal-to-nanosecond conversion.
        var fractionNanos = Int128.Zero;
        var fractionGroup = match.Groups["fraction"];
        if (fractionGroup.Success)
        {
            var padded = fractionGroup.Value.PadRight(_FRACTION_DIGITS, '0');
            fractionNanos = Int128.Parse(padded, CultureInfo.InvariantCulture);
        }

        try
        {
            var total = checked(
                (days * _NANOS_PER_DAY)
                + (hours * _NANOS_PER_HOUR)
                + (minutes * _NANOS_PER_MINUTE)
                + (seconds * _NANOS_PER_SECOND)
                + fractionNanos);

            // Apply the leading-sign to the whole magnitude.
            return match.Groups["sign"].Success ? -total : total;
        }
        catch (OverflowException)
        {
            overflow = true;
            return Int128.Zero;
        }
    }

    private static bool TryComponent(Match match, string name, out Int128 value)
    {
        var group = match.Groups[name];
        if (!group.Success)
        {
            value = Int128.Zero;
            return true;
        }

        return Int128.TryParse(group.Value, NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }

    private static void AppendFraction(StringBuilder sb, long fraction)
    {
        // fraction is in [1, 999_999_999]; render it as exactly nine digits,
        // then strip trailing zeros to the minimal canonical form.
        var digits = fraction.ToString("D9", CultureInfo.InvariantCulture).AsSpan().TrimEnd('0');
        sb.Append('.').Append(digits);
    }

    private static D2Result<Duration> Invalid() =>
        D2Result<Duration>.ValidationFailed(
            inputErrors: [new InputError(
                "iso",
                [TK.Common.Time.INVALID_DURATION])]);

    /// <summary>
    /// Anchored ISO-8601 duration grammar restricted to the date/time fields
    /// the wire actually carries: an optional leading sign, optional whole
    /// days, then an optional time section with optional hours / minutes /
    /// seconds, where the seconds field may carry up to nine fractional
    /// digits. Year / month / week designators are intentionally unsupported —
    /// they are calendar-relative (not a fixed nanosecond quantity) and the
    /// wire never emits them. The empty-component case ("P" / "PT") is
    /// rejected by <see cref="HasAnyComponent"/> after the match.
    /// </summary>
    /// <remarks>
    /// Bucket 2 (linear, bounded input): the pattern has no nested quantifiers
    /// or alternation that can backtrack super-linearly, and the wire string
    /// is bounded — no <c>matchTimeout</c> is required per the regex-ReDoS
    /// discipline. Source-generated (compile-time) for zero per-call
    /// construction cost.
    /// </remarks>
    [GeneratedRegex(
        @"^(?<sign>-)?P(?:(?<days>\d+)D)?(?:T(?:(?<hours>\d+)H)?(?:(?<minutes>\d+)M)?(?:(?<seconds>\d+)(?:\.(?<fraction>\d{1,9}))?S)?)?$",
        RegexOptions.CultureInvariant)]
    private static partial Regex IsoDurationRegex();
}
