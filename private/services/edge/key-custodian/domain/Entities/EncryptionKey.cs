// -----------------------------------------------------------------------
// <copyright file="EncryptionKey.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.KeyCustodian.Domain.Entities;

/// <summary>
/// Abstract base record for a managed encryption key.
/// </summary>
/// <remarks>
/// <b>Sum-type lifecycle.</b> Each sealed state type (<see cref="PendingKey"/>,
/// <see cref="ActiveKey"/>, <see cref="RetiringKey"/>, <see cref="RetiredKey"/>,
/// <see cref="CompromisedKey"/>) overrides <see cref="Status"/> with a
/// compile-time constant — illegal lifecycle transitions are unrepresentable
/// at the type level (§9.31). Transitions live as instance methods on the
/// appropriate sealed state; terminal states expose no transition methods.
///
/// <b>NOT sealed</b> — §5.7 carve-out: this is the base record for the sealed
/// per-state hierarchy. Sealing a base class whose derived types must be
/// reachable from outside the assembly would prevent the hierarchy.
///
/// <b>Key material retention.</b> Material is retained through <c>RetiredKey</c>
/// and <c>CompromisedKey</c>: retired keys must be able to decrypt historical
/// payloads (overlap decryption); compromised key material is needed for
/// forensics. Dropping material on retire would break these guarantees.
/// </remarks>
public abstract record EncryptionKey
{
    /// <summary>Gets the unique key identifier (JWKS <c>kid</c> claim).</summary>
    public required Kid Kid { get; init; }

    /// <summary>Gets the logical keyring this key belongs to.</summary>
    public required KeyDomain KeyDomain { get; init; }

    /// <summary>Gets the cryptographic algorithm category of this key.</summary>
    public required KeyType KeyType { get; init; }

    /// <summary>Gets the root-key-encrypted key material bytes.</summary>
    public required KeyMaterialEncrypted KeyMaterialEncrypted { get; init; }

    /// <summary>
    /// Gets the unencrypted public key bytes for asymmetric keys that carry a bare
    /// SPKI public component (<c>RsaSigning</c> — published via JWKS;
    /// <c>EcdhSealing</c> — fetched to seal payloads to the owning service);
    /// <see langword="null"/> for symmetric and certificate-authority keys.
    /// </summary>
    /// <remarks>
    /// Public keys are intentionally NOT redacted — asymmetric keys carry public
    /// material that must be visible in logs and telemetry (the JWKS signing key's
    /// public half is published; a sealing key's SPKI is handed to any workload that
    /// wants to seal to the owner).
    /// </remarks>
    public PublicKeyMaterial? PublicKeyMaterial { get; init; }

    /// <summary>
    /// Gets the DER-encoded CA certificate for certificate-authority
    /// (<c>X509CaCertificate</c>) keys; <see langword="null"/> for all other key
    /// types.
    /// </summary>
    /// <remarks>
    /// A CA certificate is intentionally NOT redacted — it is pinned as a trust
    /// anchor and presented on the wire during the TLS handshake, so it must be
    /// visible in logs and telemetry, the same treatment the JWKS signing key's
    /// public material receives.
    /// </remarks>
    public CaCertificateMaterial? CaCertificateMaterial { get; init; }

    /// <summary>
    /// Gets the UTC instant at which this key was generated.
    /// </summary>
    /// <remarks>
    /// Cat 2 bare <see cref="Instant"/> (§25.3) — generic UTC timestamp;
    /// no wall-clock context to preserve.
    /// </remarks>
    public required Instant CreatedAt { get; init; }

    /// <summary>
    /// Gets the lifecycle status of this key. Each sealed state overrides this
    /// property with a compile-time constant — it is NEVER directly settable by
    /// business logic.
    /// </summary>
    public abstract KeyStatus Status { get; }

    /// <summary>
    /// Validates the per-type material shape invariant.
    /// <c>RsaSigning</c> and <c>EcdhSealing</c> keys carry a non-null
    /// <see cref="PublicKeyMaterial"/> (a bare SPKI public key) and no
    /// <see cref="CaCertificateMaterial"/>; <c>X509CaCertificate</c> keys carry
    /// a non-null <see cref="CaCertificateMaterial"/> and no
    /// <see cref="PublicKeyMaterial"/>; symmetric keys (<c>AesPayload</c>,
    /// <c>Secret</c>) carry neither.
    /// </summary>
    /// <param name="type">The key type being validated.</param>
    /// <param name="pub">The public key material (may be null).</param>
    /// <param name="caCert">The CA certificate material (may be null).</param>
    /// <returns>
    /// <c>Ok</c> when the shape is consistent; a flagged
    /// <c>KEYCUSTODIAN_PRECONDITION_VIOLATED</c> failure when it is not. A
    /// mismatched material shape is a programmer/precondition error surfaced as a
    /// telemetry-flagged internal-error result rather than a thrown exception.
    /// </returns>
    private protected static D2Result EnsureMaterialShape(
        KeyType type, PublicKeyMaterial? pub, CaCertificateMaterial? caCert) =>
        type switch
        {
            KeyType.RsaSigning or KeyType.EcdhSealing =>
                pub is not null && caCert is null
                    ? D2Result.Ok()
                    : KeyCustodianFailures.PreconditionViolated(),
            KeyType.X509CaCertificate =>
                caCert is not null && pub is null
                    ? D2Result.Ok()
                    : KeyCustodianFailures.PreconditionViolated(),
            KeyType.AesPayload or KeyType.Secret =>
                pub is null && caCert is null
                    ? D2Result.Ok()
                    : KeyCustodianFailures.PreconditionViolated(),
            _ => KeyCustodianFailures.PreconditionViolated(),
        };

    /// <summary>
    /// Constructs a <see cref="CompromisedKey"/> from the current key's core
    /// fields. Called by each live state's <c>Compromise</c> method to avoid
    /// duplication.
    /// </summary>
    /// <param name="reason">
    /// Non-empty, length-capped operator reason string. MUST be non-null/non-whitespace.
    /// </param>
    /// <param name="at">The instant at which the compromise was recorded.</param>
    /// <returns>
    /// <c>Ok(<see cref="CompromisedKey"/>)</c> carrying the current key's
    /// identity; a flagged <c>KEYCUSTODIAN_PRECONDITION_VIOLATED</c> failure when
    /// <paramref name="reason"/> is null, empty, or whitespace.
    /// </returns>
    private protected D2Result<CompromisedKey> ToCompromised(string reason, Instant at)
    {
        if (reason.Falsey())
            return KeyCustodianFailures<CompromisedKey>.PreconditionViolated();

        if (reason.Length > CompromisedKey.REASON_MAX)
            reason = reason[..CompromisedKey.REASON_MAX];

        return D2Result<CompromisedKey>.Ok(new CompromisedKey
        {
            Kid = Kid,
            KeyDomain = KeyDomain,
            KeyType = KeyType,
            KeyMaterialEncrypted = KeyMaterialEncrypted,
            PublicKeyMaterial = PublicKeyMaterial,
            CaCertificateMaterial = CaCertificateMaterial,
            CreatedAt = CreatedAt,
            CompromisedAt = at,
            Reason = reason,
        });
    }
}
