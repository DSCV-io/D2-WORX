// -----------------------------------------------------------------------
// <copyright file="PendingKey.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.Domain.Keys;

using D2.Edge.KeyCustodian.Domain.Enums;
using D2.Edge.KeyCustodian.Domain.Errors;
using D2.Edge.KeyCustodian.Domain.ValueObjects;
using D2.Shared.Result;
using NodaTime;
using IClock = D2.Shared.Time.IClock;

/// <summary>
/// A managed encryption key that has been generated but not yet smoke-tested
/// and activated. The only legal forward transitions are
/// <see cref="Activate"/> and <see cref="Compromise"/>.
/// </summary>
/// <remarks>
/// Terminal preconditions that make illegal-state unrepresentable:
/// <list type="bullet">
///   <item><c>Activate</c> accepts a typed <see cref="SmokeProof"/> — a null
///     proof is uncompilable as a non-nullable parameter.</item>
///   <item>No <c>Rotate</c> or <c>Retire</c> method exists on this type —
///     calling them would not compile.</item>
/// </list>
/// </remarks>
public sealed record PendingKey : EncryptionKey
{
    /// <inheritdoc/>
    public override KeyStatus Status => KeyStatus.Pending;

    /// <summary>
    /// Constructs a new <see cref="PendingKey"/> with the validated material
    /// shape invariant applied.
    /// </summary>
    /// <param name="kid">The unique key identifier.</param>
    /// <param name="keyDomain">The keyring this key belongs to.</param>
    /// <param name="keyType">The cryptographic algorithm category.</param>
    /// <param name="encryptedMaterial">The root-key-wrapped key material.</param>
    /// <param name="publicMaterial">
    /// Public key bytes for <c>RsaSigning</c> keys; <see langword="null"/> for symmetric keys.
    /// </param>
    /// <param name="createdAt">The UTC instant of key generation.</param>
    /// <returns>A new <see cref="PendingKey"/>.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="kid"/>, <paramref name="keyDomain"/>, or
    /// <paramref name="encryptedMaterial"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when the <paramref name="publicMaterial"/> shape is inconsistent
    /// with <paramref name="keyType"/> (RSA must have public; symmetric must not).
    /// </exception>
    public static PendingKey Create(
        Kid kid,
        KeyDomain keyDomain,
        KeyType keyType,
        KeyMaterialEncrypted encryptedMaterial,
        PublicKeyMaterial? publicMaterial,
        Instant createdAt)
    {
        ArgumentNullException.ThrowIfNull(kid);
        ArgumentNullException.ThrowIfNull(keyDomain);
        ArgumentNullException.ThrowIfNull(encryptedMaterial);

        // Enforce RSA⇒pub non-null; symmetric⇒pub null. Throws on mismatch.
        EnsureMaterialShape(keyType, publicMaterial);

        return new PendingKey
        {
            Kid = kid,
            KeyDomain = keyDomain,
            KeyType = keyType,
            KeyMaterialEncrypted = encryptedMaterial,
            PublicKeyMaterial = publicMaterial,
            CreatedAt = createdAt,
        };
    }

    /// <summary>
    /// Attempts to activate this pending key. Requires that the smoke-soak window
    /// has elapsed and that the proof was issued for this key's type.
    /// </summary>
    /// <param name="proof">
    /// Evidence that a smoke test passed for this key. Must be non-null.
    /// </param>
    /// <param name="policy">
    /// The rotation policy governing this key's soak window. Must be non-null.
    /// </param>
    /// <param name="clock">The current-time source. Must be non-null.</param>
    /// <returns>
    /// <c>Ok(<see cref="ActiveKey"/>)</c> when both guards pass;
    /// <c>ValidationFailed</c> carrying <c>KEYCUSTODIAN_SOAK_NOT_ELAPSED</c> when
    /// the soak window has not yet elapsed;
    /// <c>ValidationFailed</c> carrying <c>KEYCUSTODIAN_SMOKE_PROOF_TYPE_MISMATCH</c>
    /// when the proof was issued for a different key type.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="proof"/>, <paramref name="policy"/>, or
    /// <paramref name="clock"/> is <see langword="null"/>.
    /// </exception>
    public D2Result<ActiveKey> Activate(SmokeProof proof, RotationPolicy policy, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(proof);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(clock);

        var now = clock.GetCurrentInstant();
        var elapsed = now - CreatedAt;

        if (elapsed < policy.SmokeSoak)
            return KeyCustodianFailures<ActiveKey>.SoakNotElapsed();

        if (proof.VerifiedType != KeyType)
            return KeyCustodianFailures<ActiveKey>.SmokeProofTypeMismatch();

        return D2Result<ActiveKey>.Ok(new ActiveKey
        {
            Kid = Kid,
            KeyDomain = KeyDomain,
            KeyType = KeyType,
            KeyMaterialEncrypted = KeyMaterialEncrypted,
            PublicKeyMaterial = PublicKeyMaterial,
            CreatedAt = CreatedAt,
            ActivatedAt = now,
        });
    }

    /// <summary>
    /// Immediately marks this pending key as compromised. Requires a non-empty
    /// operator reason.
    /// </summary>
    /// <param name="reason">
    /// Non-empty operator-supplied reason. Length-capped to
    /// <see cref="CompromisedKey.REASON_MAX"/> characters at construction.
    /// </param>
    /// <param name="clock">The current-time source. Must be non-null.</param>
    /// <returns>A <see cref="CompromisedKey"/> recording this key's identity.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="clock"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="reason"/> is null, empty, or whitespace.
    /// </exception>
    public CompromisedKey Compromise(string reason, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        return ToCompromised(reason, clock.GetCurrentInstant());
    }
}
