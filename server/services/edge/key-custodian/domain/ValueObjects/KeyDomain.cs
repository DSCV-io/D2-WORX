// -----------------------------------------------------------------------
// <copyright file="KeyDomain.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.Domain.ValueObjects;

using D2.Shared.Encryption;
using D2.Shared.Result;
using D2.Shared.Utilities.Extensions;
using TK = D2.Shared.I18n.TK;

/// <summary>
/// Strong-typed value object identifying which independently-rotated keyring
/// a managed key belongs to.
/// </summary>
/// <remarks>
/// <b>Not PII.</b> A key domain is a logical label such as <c>"audit"</c> or
/// <c>"jwks-signing"</c> — not personally identifying. Do NOT apply
/// <c>[RedactData]</c> to this type.
///
/// <b>Catalog.</b> <see cref="All"/> is the closed catalog — the union of
/// <see cref="EncryptionDomains.AllDomains"/> (minus <c>"plaintext"</c>, which
/// is a no-encrypt sentinel, not a real keyring) and the three KeyCustodian-only
/// domains (<c>jwks-signing</c>, <c>cookie</c>, <c>client-secret</c>).
/// Adding a new domain requires updating the catalog here AND provisioning the
/// corresponding keyring.
///
/// <b>Wire-format constants.</b> The three KC-only literal strings are
/// wire/spec-anchored constants (§5.25 exemption — the literal value IS the wire
/// format). Use these constants instead of raw strings to avoid typos that would
/// silently route material to a non-existent keyring.
/// </remarks>
public sealed record KeyDomain
{
    // KC-only domain wire values — spec-anchored constants (§5.25 exemption).

    /// <summary>Wire value for the JWKS-signing asymmetric key domain.</summary>
    public const string JWKS_SIGNING = "jwks-signing";

    /// <summary>Wire value for the session-cookie signing key domain.</summary>
    public const string COOKIE = "cookie";

    /// <summary>Wire value for the client-secret key domain.</summary>
    public const string CLIENT_SECRET = "client-secret";

    /// <summary>Gets the normalized domain string (lowercase, trimmed).</summary>
    public required string Value { get; init; }

    // -----------------------------------------------------------------------
    // Named static accessors for KC-only domains
    // -----------------------------------------------------------------------

    /// <summary>Gets the JWKS-signing domain (<c>"jwks-signing"</c>).</summary>
    public static KeyDomain JwksSigning { get; } = new() { Value = JWKS_SIGNING };

    /// <summary>Gets the cookie-signing domain (<c>"cookie"</c>).</summary>
    public static KeyDomain Cookie { get; } = new() { Value = COOKIE };

    /// <summary>Gets the client-secret domain (<c>"client-secret"</c>).</summary>
    public static KeyDomain ClientSecret { get; } = new() { Value = CLIENT_SECRET };

    /// <summary>
    /// Gets the closed catalog of all recognized key domains: all
    /// <see cref="EncryptionDomains.AllDomains"/> entries except
    /// <c>"plaintext"</c>, plus the three KeyCustodian-only domains.
    /// </summary>
    /// <remarks>
    /// <c>"plaintext"</c> is excluded because it is a no-encrypt sentinel, not
    /// a real keyring — KeyCustodian manages <em>keys</em>, so a
    /// <c>"plaintext"</c> key is nonsensical by definition.
    /// </remarks>
    public static IReadOnlyList<KeyDomain> All { get; } = BuildCatalog();

    /// <summary>
    /// Validates and constructs a <see cref="KeyDomain"/> from raw input by
    /// normalizing (trim + lowercase) and checking catalog membership.
    /// </summary>
    /// <param name="value">Raw domain string (may be null or whitespace).</param>
    /// <returns>
    /// <c>Ok</c> with the normalized <see cref="KeyDomain"/> on success;
    /// <c>ValidationFailed</c> with
    /// <c>key_custodian_validation_UNKNOWN_KEY_DOMAIN</c> on failure.
    /// </returns>
    public static D2Result<KeyDomain> Create(string? value)
    {
        var normalized = value.ToNullIfEmpty()?.ToLowerInvariant();
        if (normalized is null)
        {
            return D2Result<KeyDomain>.ValidationFailed(
                messages: [TK.Key.Custodian.VALIDATION_UNKNOWN_KEY_DOMAIN]);
        }

        var domain = All.FirstOrDefault(d =>
            string.Equals(d.Value, normalized, StringComparison.Ordinal));

        if (domain is null)
        {
            return D2Result<KeyDomain>.ValidationFailed(
                messages: [TK.Key.Custodian.VALIDATION_UNKNOWN_KEY_DOMAIN]);
        }

        return D2Result<KeyDomain>.Ok(domain);
    }

    /// <summary>
    /// Reconstructs a <see cref="KeyDomain"/> from a trusted, previously-validated
    /// store value WITHOUT re-running catalog membership validation. For the EF
    /// Core value-converter read side only — use <see cref="Create"/> for all
    /// user-supplied input.
    /// </summary>
    /// <param name="value">The stored domain string.</param>
    /// <returns>A <see cref="KeyDomain"/> whose <see cref="Value"/> is set verbatim.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="value"/> is <see langword="null"/>.
    /// </exception>
    public static KeyDomain FromTrusted(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new() { Value = value };
    }

    private static IReadOnlyList<KeyDomain> BuildCatalog()
    {
        var encDomains = EncryptionDomains.AllDomains
            .Where(d => !string.Equals(d, EncryptionDomains.PLAINTEXT, StringComparison.Ordinal))
            .Select(d => new KeyDomain { Value = d });

        return [.. encDomains, JwksSigning, Cookie, ClientSecret];
    }
}
