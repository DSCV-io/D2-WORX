// -----------------------------------------------------------------------
// <copyright file="Kid.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.Domain.ValueObjects;

using System.Text.RegularExpressions;

/// <summary>
/// Strong-typed key identifier used as the JWKS <c>kid</c> claim and the
/// primary key of <c>EncryptionKey</c>.
/// </summary>
/// <remarks>
/// <b>Not PII.</b> A <c>Kid</c> is an opaque, non-reversible identifier — it
/// is intentionally visible in JWKS responses, audit logs, and telemetry.
/// Do NOT apply <c>[RedactData]</c> to this type.
///
/// Validation enforces:
/// <list type="bullet">
///   <item>Non-null, non-empty, non-whitespace.</item>
///   <item>Maximum length of <see cref="_KID_MAX"/> characters.</item>
///   <item>JWKS-safe charset: <c>[A-Za-z0-9_-]</c> only (no dots, slashes, or
///     whitespace that could break JWKS JSON or HTTP headers).</item>
/// </list>
/// </remarks>
public sealed partial record Kid
{
    private const int _KID_MAX = 64;

    // Bucket 1 — no-backtracking pattern: single anchored character class with no
    // alternation or repetition that could backtrack. Input is length-capped to
    // _KID_MAX before the match so no timeout is required (§5.20).
    private static readonly Regex sr_kidCharset = KidCharsetRegex();

    /// <summary>Gets the raw <c>kid</c> string value.</summary>
    public required string Value { get; init; }

    /// <summary>
    /// Validates and constructs a <see cref="Kid"/> from raw user-supplied input.
    /// </summary>
    /// <param name="value">Raw <c>kid</c> string (may be null or whitespace).</param>
    /// <returns>
    /// <c>Ok</c> with the validated <see cref="Kid"/> on success;
    /// <c>ValidationFailed</c> carrying <c>KEYCUSTODIAN_KID_INVALID</c> or
    /// <c>KEYCUSTODIAN_KID_TOO_LONG</c> on failure.
    /// </returns>
    public static D2Result<Kid> Create(string? value)
    {
        var normalized = value.ToNullIfEmpty();
        if (normalized is null)
            return KeyCustodianFailures<Kid>.KidInvalid();

        if (normalized.Length > _KID_MAX)
            return KeyCustodianFailures<Kid>.KidTooLong();

        if (!sr_kidCharset.IsMatch(normalized))
            return KeyCustodianFailures<Kid>.KidInvalid();

        return D2Result<Kid>.Ok(new Kid { Value = normalized });
    }

    /// <summary>
    /// Reconstructs a <see cref="Kid"/> from a trusted, previously-validated
    /// store value WITHOUT re-running validation. For the EF Core value-converter
    /// read side only — use <see cref="Create"/> for all user-supplied input.
    /// </summary>
    /// <param name="value">The stored <c>kid</c> string.</param>
    /// <returns>A <see cref="Kid"/> whose <see cref="Value"/> is set verbatim.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="value"/> is <see langword="null"/>, empty, or whitespace.
    /// A corrupt DB row with an empty <c>kid</c> is a data-corruption error, not valid input.
    /// </exception>
    public static Kid FromTrusted(string value)
    {
        value.ThrowIfFalsey();
        return new() { Value = value };
    }

    /// <summary>
    /// JWKS-safe charset: letters, digits, hyphens, underscores only.
    /// Bucket 1 (§5.20) — no-backtracking: single anchored character class, no
    /// alternation/repetition that could backtrack; no timeout required.
    /// </summary>
    [GeneratedRegex(@"^[A-Za-z0-9_-]+$", RegexOptions.None)]
    private static partial Regex KidCharsetRegex();
}
