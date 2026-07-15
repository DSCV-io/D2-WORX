// -----------------------------------------------------------------------
// <copyright file="DefaultEmailValidator.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Validation;

using System.Text.RegularExpressions;
using DcsvIo.D2.I18n;
using DcsvIo.D2.Result;
using DcsvIo.D2.Utilities.Extensions;
using DcsvIo.D2.Validation.Abstractions;

/// <summary>
/// Default <see cref="IEmailValidator"/> — regex structural check enforcing
/// RFC-5321/5322 practical limits: total length 1–254, local part 1–64 of
/// ASCII alphanumeric plus <c>. _ % + -</c>, at-sign, one or more domain
/// labels separated by dots with no leading/trailing hyphen per label.
/// </summary>
/// <remarks>
/// <para>
/// Normalization: the input is trimmed then lowercased before the regex runs
/// and before the normalized value is returned. The comparison is therefore
/// case-insensitive by design; <c>RegexOptions.IgnoreCase</c> covers the
/// regex side.
/// </para>
/// <para>
/// <b>ReDoS posture: anchored, bounded — no catastrophic backtracking (Bucket B1).</b>
/// The pattern is anchored at both ends. The total-length lookahead is bounded. Every
/// character class is bounded: local part <c>{1,64}</c>, each domain label
/// <c>{0,61}</c>. The structure is linear in input length — genuinely B1
/// shape — but a 50 ms match timeout and the static-constructor JIT
/// pre-warm are retained as defense-in-depth against future-author regex edits that
/// could silently push the pattern into catastrophic-backtracking territory.
/// </para>
/// <para>
/// The pattern string is exposed as <see cref="EMAIL_PATTERN"/> so a parity
/// test can assert byte-identity with the TypeScript side.
/// </para>
/// </remarks>
public sealed class DefaultEmailValidator : IEmailValidator
{
    /// <summary>
    /// The email validation regex pattern — the cross-language source of truth
    /// shared with the TypeScript <c>@dcsv-io/d2-validation</c> package. Any change
    /// here must be mirrored on the TypeScript side; a parity test asserts
    /// byte-identity.
    /// </summary>
    // long regex literal — cannot wrap
    public const string EMAIL_PATTERN =
        @"^(?=.{1,254}$)[A-Z0-9._%+\-]{1,64}@[A-Z0-9](?:[A-Z0-9\-]{0,61}[A-Z0-9])?(?:\.[A-Z0-9](?:[A-Z0-9\-]{0,61}[A-Z0-9])?)+$";

    private static readonly TimeSpan sr_MatchTimeout = TimeSpan.FromMilliseconds(50);

    private static readonly Regex sr_Email = new(
        EMAIL_PATTERN,
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        sr_MatchTimeout);

    static DefaultEmailValidator()
    {
        // JIT pre-warm — defense-in-depth posture retains the timeout even for
        // a B1-shape pattern so that future edits to the pattern cannot
        // inadvertently remove the guard-rail.
        _ = sr_Email.IsMatch("a@b.co");
    }

    /// <inheritdoc />
    public D2Result<string> Validate(string? email)
    {
        if (email.Falsey())
            return Invalid();

        var trimmed = email!.Trim();

        bool isMatch;
        try
        {
            isMatch = sr_Email.IsMatch(trimmed);
        }
        catch (RegexMatchTimeoutException)
        {
            return Invalid();
        }

        if (!isMatch)
            return Invalid();

        return D2Result<string>.Ok(trimmed.ToLowerInvariant());
    }

    private static D2Result<string> Invalid()
        => D2Result<string>.ValidationFailed(
            inputErrors: [new InputError("email", [TK.Common.Validation.EMAIL_INVALID])]);
}
