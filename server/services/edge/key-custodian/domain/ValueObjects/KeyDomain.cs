// -----------------------------------------------------------------------
// <copyright file="KeyDomain.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.Domain.ValueObjects;

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
/// is a no-encrypt sentinel, not a real keyring) and the five KeyCustodian-only
/// domains (<c>jwks-signing</c>, <c>cookie</c>, <c>client-secret</c>,
/// <c>mtls-ca-root</c>, <c>mtls-ca-intermediate</c>).
/// Adding a new domain requires updating the catalog here AND provisioning the
/// corresponding keyring.
///
/// <b>Domain→key-type binding.</b> Every catalog entry carries its bound
/// <see cref="Enums.KeyType"/> as a first-class domain fact: <c>jwks-signing</c>
/// → <see cref="Enums.KeyType.RsaSigning"/>; <c>cookie</c> / <c>client-secret</c> →
/// <see cref="Enums.KeyType.Secret"/>; the CA domains →
/// <see cref="Enums.KeyType.X509CaCertificate"/>; every payload-encryption domain →
/// <see cref="Enums.KeyType.AesPayload"/>. Consumers derive the type from the
/// binding — there is no second domain→type map anywhere. A <c>(domain, type)</c>
/// pair that disagrees with the binding is rejected with
/// <c>KEYCUSTODIAN_KEY_TYPE_DOMAIN_MISMATCH</c> by the generate/sign surfaces.
///
/// <b>Wire-format constants.</b> The KC-only literal strings are
/// wire/spec-anchored constants (§5.25 exemption — the literal value IS the wire
/// format). Use these constants instead of raw strings to avoid typos that would
/// silently route material to a non-existent keyring.
///
/// <b>mTLS CA domains.</b> The internal certificate authority is a two-tier
/// hierarchy persisted as managed keys. The root and the intermediate live in
/// SEPARATE domains (<c>mtls-ca-root</c> / <c>mtls-ca-intermediate</c>) so "the
/// active issuing CA" is a trivial <c>ForDomain(mtls-ca-intermediate).Active()</c>
/// query — the issuance path signs with the intermediate, never the root.
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

    /// <summary>Wire value for the mTLS root certificate-authority key domain.</summary>
    public const string MTLS_CA_ROOT = "mtls-ca-root";

    /// <summary>
    /// Wire value for the mTLS issuing-intermediate certificate-authority key domain.
    /// </summary>
    public const string MTLS_CA_INTERMEDIATE = "mtls-ca-intermediate";

    /// <summary>Gets the normalized domain string (lowercase, trimmed).</summary>
    public required string Value { get; init; }

    /// <summary>
    /// Gets the <see cref="Enums.KeyType"/> canonically bound to this domain — the ONLY
    /// cryptographic key type a key in this domain may be provisioned with. A domain
    /// fact, not a tunable: the generate surface rejects a mismatched pair, the sign
    /// surface sharply rejects a non-<see cref="Enums.KeyType.RsaSigning"/>-bound
    /// domain, and the rotation bootstrap map derives from it.
    /// </summary>
    public required KeyType KeyType { get; init; }

    /// <summary>Gets the JWKS-signing domain (<c>"jwks-signing"</c>).</summary>
    public static KeyDomain JwksSigning { get; } =
        new() { Value = JWKS_SIGNING, KeyType = KeyType.RsaSigning };

    /// <summary>Gets the cookie-signing domain (<c>"cookie"</c>).</summary>
    public static KeyDomain Cookie { get; } = new() { Value = COOKIE, KeyType = KeyType.Secret };

    /// <summary>Gets the client-secret domain (<c>"client-secret"</c>).</summary>
    public static KeyDomain ClientSecret { get; } =
        new() { Value = CLIENT_SECRET, KeyType = KeyType.Secret };

    /// <summary>Gets the mTLS root CA domain (<c>"mtls-ca-root"</c>).</summary>
    public static KeyDomain MtlsCaRoot { get; } =
        new() { Value = MTLS_CA_ROOT, KeyType = KeyType.X509CaCertificate };

    /// <summary>
    /// Gets the mTLS issuing-intermediate CA domain (<c>"mtls-ca-intermediate"</c>).
    /// </summary>
    public static KeyDomain MtlsCaIntermediate { get; } =
        new() { Value = MTLS_CA_INTERMEDIATE, KeyType = KeyType.X509CaCertificate };

    /// <summary>
    /// Gets the closed catalog of all recognized key domains: all
    /// <see cref="EncryptionDomains.AllDomains"/> entries except
    /// <c>"plaintext"</c>, plus the five KeyCustodian-only domains.
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
    /// <c>ValidationFailed</c> carrying
    /// <c>KEYCUSTODIAN_UNKNOWN_KEY_DOMAIN</c> on failure.
    /// </returns>
    public static D2Result<KeyDomain> Create(string? value)
    {
        var normalized = value.ToNullIfEmpty()?.ToLowerInvariant();

        if (normalized is null)
            return KeyCustodianFailures<KeyDomain>.UnknownKeyDomain();

        var domain = TryResolveCatalogEntry(normalized);

        if (domain is null)
            return KeyCustodianFailures<KeyDomain>.UnknownKeyDomain();

        return D2Result<KeyDomain>.Ok(domain);
    }

    /// <summary>
    /// Reconstructs a <see cref="KeyDomain"/> from a trusted, previously-validated
    /// store value by resolving the canonical catalog entry (carrying the bound
    /// <see cref="KeyType"/>). For the EF Core read side only — use
    /// <see cref="Create"/> for all user-supplied input.
    /// </summary>
    /// <remarks>
    /// Strict fail-loud: every row is written through <see cref="Create"/>-validated
    /// paths, so a stored value with no catalog entry (and therefore no key-type
    /// binding) is data corruption and THROWS rather than fabricating a bindingless
    /// domain. Resolution is case-insensitive (a legacy non-lowercase stored value
    /// still resolves to its canonical entry) but never trims — a padded stored
    /// value could not have been written by a validated path.
    /// </remarks>
    /// <param name="value">The stored domain string.</param>
    /// <returns>
    /// The canonical catalog <see cref="KeyDomain"/> for <paramref name="value"/>.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="value"/> is <see langword="null"/>, empty, whitespace,
    /// or not a member of the closed catalog. A corrupt DB row must fail loud, never
    /// silently produce a domain without a key-type binding.
    /// </exception>
    public static KeyDomain FromTrusted(string value)
    {
        value.ThrowIfFalsey();
        var domain = TryResolveCatalogEntry(value.ToLowerInvariant());

        if (domain is null)
        {
            throw new ArgumentException(
                $"Stored key-domain value '{value}' is not a member of the closed "
                + "key-domain catalog — the row is corrupt (every valid row is written "
                + "through Create-validated paths).",
                nameof(value));
        }

        return domain;
    }

    /// <summary>
    /// Resolves the canonical catalog entry for an already-normalized (lowercase)
    /// domain value, or <see langword="null"/> when the value is not in the catalog.
    /// The single resolution seam shared by <see cref="Create"/> and
    /// <see cref="FromTrusted"/> — a future pattern-based domain class (one that is
    /// not a closed-catalog literal) extends exactly this method.
    /// </summary>
    /// <param name="normalized">The normalized (lowercase, trimmed) domain value.</param>
    /// <returns>The canonical entry, or <see langword="null"/> when unknown.</returns>
    private static KeyDomain? TryResolveCatalogEntry(string normalized) =>
        All.FirstOrDefault(d => string.Equals(d.Value, normalized, StringComparison.Ordinal));

    private static IReadOnlyList<KeyDomain> BuildCatalog()
    {
        // Every payload-encryption domain (the EncryptionDomains catalog minus the
        // "plaintext" no-encrypt sentinel) is bound to AES-256-GCM payload keys.
        var encDomains = EncryptionDomains.AllDomains
            .Where(d => !string.Equals(d, EncryptionDomains.PLAINTEXT, StringComparison.Ordinal))
            .Select(d => new KeyDomain { Value = d, KeyType = KeyType.AesPayload });

        return
        [
            .. encDomains,
            JwksSigning,
            Cookie,
            ClientSecret,
            MtlsCaRoot,
            MtlsCaIntermediate,
        ];
    }
}
