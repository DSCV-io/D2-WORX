// -----------------------------------------------------------------------
// <copyright file="EncryptionKey.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.Domain.Keys;

using D2.Edge.KeyCustodian.Domain.Enums;
using D2.Edge.KeyCustodian.Domain.ValueObjects;
using D2.Shared.Result;
using D2.Shared.Utilities.Extensions;
using NodaTime;

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
    /// Gets the unencrypted public key bytes for asymmetric (<c>RsaSigning</c>)
    /// keys; <see langword="null"/> for symmetric keys.
    /// </summary>
    /// <remarks>
    /// Public keys are intentionally NOT redacted — they are published via the
    /// JWKS endpoint and must be visible in logs and telemetry.
    /// </remarks>
    public PublicKeyMaterial? PublicKeyMaterial { get; init; }

    /// <summary>
    /// Gets the UTC instant at which this key was generated.
    /// </summary>
    /// <remarks>Cat 2 bare <see cref="Instant"/> (§25.3) — generic UTC timestamp; no wall-clock context to preserve.</remarks>
    public required Instant CreatedAt { get; init; }

    /// <summary>
    /// Gets the lifecycle status of this key. Each sealed state overrides this
    /// property with a compile-time constant — it is NEVER directly settable by
    /// business logic.
    /// </summary>
    public abstract KeyStatus Status { get; }

    /// <summary>
    /// Validates the asymmetric/symmetric material shape invariant.
    /// <c>RsaSigning</c> keys require a non-null <see cref="PublicKeyMaterial"/>;
    /// symmetric keys (<c>AesPayload</c>, <c>Secret</c>) must have a null one.
    /// </summary>
    /// <param name="type">The key type being validated.</param>
    /// <param name="pub">The public key material (may be null).</param>
    /// <returns>
    /// <c>Ok</c> when the shape is consistent;
    /// failure with a bespoke message when it is not.
    /// </returns>
    private protected static D2Result EnsureMaterialShape(KeyType type, PublicKeyMaterial? pub)
    {
        if (type == KeyType.RsaSigning && pub is null)
        {
            // §5.1a bespoke-message carve-out: RSA key missing public material is a programmer error.
            throw new ArgumentException(
                "RsaSigning keys require a non-null PublicKeyMaterial.",
                nameof(pub));
        }

        if (type != KeyType.RsaSigning && pub is not null)
        {
            // §5.1a bespoke-message carve-out: symmetric key with public material is a programmer error.
            throw new ArgumentException(
                "Symmetric keys (AesPayload, Secret) must have a null PublicKeyMaterial.",
                nameof(pub));
        }

        return D2Result.Ok();
    }

    /// <summary>
    /// Constructs a <see cref="CompromisedKey"/> from the current key's core
    /// fields. Called by each live state's <c>Compromise</c> method to avoid
    /// duplication.
    /// </summary>
    /// <param name="reason">
    /// Non-empty, length-capped operator reason string. MUST be non-null/non-whitespace.
    /// </param>
    /// <param name="at">The instant at which the compromise was recorded.</param>
    /// <returns>A <see cref="CompromisedKey"/> carrying the current key's identity.</returns>
    private protected CompromisedKey ToCompromised(string reason, Instant at)
    {
        reason.ThrowIfFalsey();

        if (reason.Length > CompromisedKey.REASON_MAX)
        {
            reason = reason[..CompromisedKey.REASON_MAX];
        }

        return new CompromisedKey
        {
            Kid = Kid,
            KeyDomain = KeyDomain,
            KeyType = KeyType,
            KeyMaterialEncrypted = KeyMaterialEncrypted,
            PublicKeyMaterial = PublicKeyMaterial,
            CreatedAt = CreatedAt,
            CompromisedAt = at,
            Reason = reason,
        };
    }
}
