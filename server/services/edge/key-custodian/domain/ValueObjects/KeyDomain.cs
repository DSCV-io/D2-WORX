// -----------------------------------------------------------------------
// <copyright file="KeyDomain.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.Domain.ValueObjects;

using System.Collections.Concurrent;
using D2.Shared.WorkloadIdentity;

/// <summary>
/// Strong-typed value object identifying which independently-rotated keyring
/// a managed key belongs to.
/// </summary>
/// <remarks>
/// <b>Not PII.</b> A key domain is a logical label such as <c>"audit"</c> or
/// <c>"jwks-signing"</c> — not personally identifying. Do NOT apply
/// <c>[RedactData]</c> to this type.
///
/// <b>Catalog.</b> <see cref="All"/> is the closed catalog — the union of the
/// SYMMETRIC-mode <see cref="EncryptionDomains.AllDomains"/> entries (minus
/// <c>"plaintext"</c>, a no-encrypt sentinel, AND minus every SEALED-mode domain —
/// a sealed domain has no symmetric keyring by construction) and the five
/// KeyCustodian-only domains (<c>jwks-signing</c>, <c>cookie</c>,
/// <c>client-secret</c>, <c>mtls-ca-root</c>, <c>mtls-ca-intermediate</c>).
/// Adding a new SYMMETRIC domain to the encryption-domains spec re-enters it here
/// automatically (zero KC code); a <c>sealed</c>-mode domain
/// (<c>audit</c> / <c>notifications</c> / <c>courier</c>) is one-way by
/// construction and is NOT a member — its per-service sealing keys live under the
/// <c>seal:&lt;serviceId&gt;</c> family, never the symmetric payload catalog. The
/// AES-payload slice of <see cref="All"/> is therefore EMPTY until a future
/// symmetric-mode domain is declared.
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
/// <b>Seal family (pattern-based, not in <see cref="All"/>).</b> The per-service
/// sealing keys live under a structured-prefix family <c>seal:&lt;serviceId&gt;</c>
/// (e.g. <c>"seal:audit"</c>) built by <see cref="ForSeal(string?)"/>, which validates the
/// service-id suffix against the shared workload-identity grammar and binds
/// <see cref="Enums.KeyType.EcdhSealing"/>. This family is UNBOUNDED (one domain per
/// service, provisioned lazily on first use), so it is NOT a member of the closed
/// <see cref="All"/> catalog — it is resolved by <see cref="TryResolveCatalogEntry"/>
/// via the <see cref="SEAL_PREFIX"/> pattern instead, so <see cref="Create"/> accepts
/// and <see cref="FromTrusted"/> rehydrates a <c>seal:&lt;id&gt;</c> value and every
/// lifecycle op (rotate / compromise / retire) works on a seal domain unchanged.
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

    /// <summary>
    /// Wire-value prefix for the per-service ECDH sealing key domain family
    /// (<c>seal:&lt;serviceId&gt;</c>). A structured-prefix, pattern-based family —
    /// see the type remarks. The literal is wire/spec-anchored (§5.25 exemption).
    /// </summary>
    public const string SEAL_PREFIX = "seal:";

    // Test-only fixture payload-domain registry (see RegisterFixturePayloadDomainForTesting).
    // Default-empty; consulted by TryResolveCatalogEntry so a registered value resolves to an
    // AesPayload domain. REFERENCE-COUNTED (value = active registration count) so two parallel
    // tests registering the same value are safe: a value is present while ANY scope holds it,
    // and removed only when the LAST scope disposes — a plain remove-by-value would let one
    // test's dispose yank a value a concurrent test still needs (a flaky-test hazard).
    private static readonly ConcurrentDictionary<string, int> sr_fixturePayloadDomains =
        new(StringComparer.Ordinal);

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
    /// Validates and constructs a per-service sealing <see cref="KeyDomain"/>
    /// (<c>seal:&lt;serviceId&gt;</c>, bound to <see cref="Enums.KeyType.EcdhSealing"/>)
    /// from a raw service identifier. The single seal-grammar seam: the suffix is
    /// validated against the shared workload-identity service-id grammar (lowercase
    /// <c>[a-z0-9-]</c>, at most 64 characters) — the recipient of a sealed frame IS a
    /// workload — and the normalized service id is prefixed with <see cref="SEAL_PREFIX"/>.
    /// </summary>
    /// <param name="serviceId">Raw service identifier (may be null or whitespace).</param>
    /// <returns>
    /// <c>Ok</c> with the normalized <c>seal:&lt;serviceId&gt;</c> <see cref="KeyDomain"/>
    /// on success; <c>ValidationFailed</c> carrying
    /// <c>KEYCUSTODIAN_UNKNOWN_KEY_DOMAIN</c> when the service id fails the grammar.
    /// </returns>
    public static D2Result<KeyDomain> ForSeal(string? serviceId)
    {
        var identity = SpiffeWorkloadIdentity.Create(serviceId);

        if (!identity.Success)
            return KeyCustodianFailures<KeyDomain>.UnknownKeyDomain();

        return D2Result<KeyDomain>.Ok(new KeyDomain
        {
            Value = SEAL_PREFIX + identity.Data!.ServiceId,
            KeyType = KeyType.EcdhSealing,
        });
    }

    /// <summary>
    /// Builds a per-service sealing <see cref="KeyDomain"/>
    /// (<c>seal:&lt;serviceId&gt;</c>, bound to <see cref="Enums.KeyType.EcdhSealing"/>)
    /// from an ALREADY-VALIDATED <see cref="WorkloadIdentity"/>. The identity's service-id
    /// grammar was enforced when the identity was created, so this overload does NOT re-run
    /// the grammar and cannot fail — it exists so a caller that already holds a validated
    /// identity validates the service id EXACTLY ONCE. Use <see cref="ForSeal(string?)"/>
    /// for a raw, untrusted service identifier.
    /// </summary>
    /// <param name="identity">A validated workload identity (its service id is trusted).</param>
    /// <returns>The normalized <c>seal:&lt;serviceId&gt;</c> <see cref="KeyDomain"/>.</returns>
    public static KeyDomain ForSeal(WorkloadIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);

        return new KeyDomain
        {
            Value = SEAL_PREFIX + identity.ServiceId,
            KeyType = KeyType.EcdhSealing,
        };
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
    /// Test-only seam: registers <paramref name="value"/> as a SYMMETRIC AES-payload domain so
    /// the preserved domain-generic symmetric machinery (getKeyring op + authority + validator
    /// + the whole consumer runtime) stays genuinely exercised even though NO production
    /// symmetric payload domain remains after audit/notifications/courier flipped to sealed.
    /// The single seam <see cref="TryResolveCatalogEntry"/> consults (the seam its doc comment
    /// anticipates). Default-empty — production behavior is byte-for-byte identical when unused.
    /// </summary>
    /// <remarks>
    /// Registration is thread-safe and SCOPED: dispose the returned handle to remove the
    /// registration (required so a test that registers a REAL sealed value like <c>"audit"</c>
    /// — to simulate the guarded-against re-admission regression — stays hermetic against a
    /// concurrently-running rejection pin; such tests MUST also be collection-isolated). Values
    /// carry a §7.23 fixture marker in the value itself (e.g. <c>payload-fixture-a</c>).
    /// </remarks>
    /// <param name="value">The fixture domain value (normalized to lowercase).</param>
    /// <returns>A handle whose disposal unregisters the fixture domain (idempotent).</returns>
    internal static IDisposable RegisterFixturePayloadDomainForTesting(string value)
    {
        value.ThrowIfFalsey();
        var normalized = value.ToLowerInvariant();
        sr_fixturePayloadDomains.AddOrUpdate(normalized, 1, static (_, count) => count + 1);

        return new FixturePayloadDomainRegistration(normalized);
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
    private static KeyDomain? TryResolveCatalogEntry(string normalized)
    {
        // Pattern-based seal family: a "seal:<serviceId>" value is not a closed-catalog
        // literal — it resolves by validating the service-id suffix against the shared
        // workload-identity grammar and binding EcdhSealing (the seam this method's doc
        // anticipates). A malformed suffix returns null (Create → UnknownKeyDomain;
        // FromTrusted → throw), the same fail-closed shape as an unknown catalog literal.
        if (normalized.StartsWith(SEAL_PREFIX, StringComparison.Ordinal))
        {
            var sealResult = ForSeal(normalized[SEAL_PREFIX.Length..]);

            return sealResult.Success ? sealResult.Data : null;
        }

        var catalogEntry = All.FirstOrDefault(
            d => string.Equals(d.Value, normalized, StringComparison.Ordinal));

        if (catalogEntry is not null)
            return catalogEntry;

        // Test-only fixture payload domains (default-empty in production): a registered value
        // resolves to a SYMMETRIC AesPayload domain, keeping the preserved symmetric machinery
        // exercisable now that no production AesPayload domain remains. See
        // RegisterFixturePayloadDomainForTesting.
        return sr_fixturePayloadDomains.ContainsKey(normalized)
            ? new KeyDomain { Value = normalized, KeyType = KeyType.AesPayload }
            : null;
    }

    private static IReadOnlyList<KeyDomain> BuildCatalog()
    {
        // Every SYMMETRIC-mode payload-encryption domain (the EncryptionDomains catalog minus
        // the "plaintext" no-encrypt sentinel AND minus every SEALED-mode domain) is bound to
        // AES-256-GCM payload keys. A sealed-mode domain has no symmetric keyring by
        // construction, so it is excluded — the AES-payload slice is empty until a future
        // symmetric-mode domain is declared in the spec.
        var encDomains = EncryptionDomains.AllDomains
            .Where(d => !string.Equals(d, EncryptionDomains.PLAINTEXT, StringComparison.Ordinal))
            .Where(d => EncryptionDomainModes.ModeFor(d) != EncryptionDomainMode.Sealed)
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

    private sealed class FixturePayloadDomainRegistration(string value) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            // Decrement the ref count; remove only when the LAST scope releases it. The CAS
            // loop keeps concurrent register/dispose of the same value race-safe.
            while (sr_fixturePayloadDomains.TryGetValue(value, out var count))
            {
                if (count <= 1)
                {
                    if (((ICollection<KeyValuePair<string, int>>)sr_fixturePayloadDomains)
                        .Remove(new KeyValuePair<string, int>(value, count)))
                    {
                        return;
                    }
                }
                else if (sr_fixturePayloadDomains.TryUpdate(value, count - 1, count))
                {
                    return;
                }
            }
        }
    }
}
