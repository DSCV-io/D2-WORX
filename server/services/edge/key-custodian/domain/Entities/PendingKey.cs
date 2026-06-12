// -----------------------------------------------------------------------
// <copyright file="PendingKey.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.Domain.Entities;

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
    /// <returns>
    /// <c>Ok(<see cref="PendingKey"/>)</c> on success; a flagged
    /// <c>KEYCUSTODIAN_PRECONDITION_VIOLATED</c> failure when <paramref name="kid"/>,
    /// <paramref name="keyDomain"/>, or <paramref name="encryptedMaterial"/> is
    /// <see langword="null"/>, or when the <paramref name="publicMaterial"/> shape
    /// is inconsistent with <paramref name="keyType"/> (RSA must have public,
    /// symmetric must not). Null arguments and shape mismatches are
    /// programmer/precondition errors surfaced as telemetry-flagged internal-error
    /// results rather than thrown exceptions.
    /// </returns>
    public static D2Result<PendingKey> Create(
        Kid? kid,
        KeyDomain? keyDomain,
        KeyType keyType,
        KeyMaterialEncrypted? encryptedMaterial,
        PublicKeyMaterial? publicMaterial,
        Instant createdAt)
    {
        if (kid is null)
            return KeyCustodianFailures<PendingKey>.PreconditionViolated();

        if (keyDomain is null)
            return KeyCustodianFailures<PendingKey>.PreconditionViolated();

        if (encryptedMaterial is null)
            return KeyCustodianFailures<PendingKey>.PreconditionViolated();

        // Enforce RSA⇒pub non-null; symmetric⇒pub null. Propagate the
        // PreconditionViolated result from EnsureMaterialShape directly.
        var shape = EnsureMaterialShape(keyType, publicMaterial);
        if (!shape.Success)
            return KeyCustodianFailures<PendingKey>.PreconditionViolated(messages: shape.Messages);

        return D2Result<PendingKey>.Ok(new PendingKey
        {
            Kid = kid,
            KeyDomain = keyDomain,
            KeyType = keyType,
            KeyMaterialEncrypted = encryptedMaterial,
            PublicKeyMaterial = publicMaterial,
            CreatedAt = createdAt,
        });
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
    /// when the proof was issued for a different key type;
    /// a flagged <c>KEYCUSTODIAN_PRECONDITION_VIOLATED</c> failure when
    /// <paramref name="proof"/>, <paramref name="policy"/>, or
    /// <paramref name="clock"/> is <see langword="null"/>.
    /// </returns>
    public D2Result<ActiveKey> Activate(SmokeProof? proof, RotationPolicy? policy, IClock? clock)
    {
        if (proof is null)
            return KeyCustodianFailures<ActiveKey>.PreconditionViolated();

        if (policy is null)
            return KeyCustodianFailures<ActiveKey>.PreconditionViolated();

        if (clock is null)
            return KeyCustodianFailures<ActiveKey>.PreconditionViolated();

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
    /// <returns>
    /// <c>Ok(<see cref="CompromisedKey"/>)</c> recording this key's identity; a
    /// flagged <c>KEYCUSTODIAN_PRECONDITION_VIOLATED</c> failure when
    /// <paramref name="clock"/> is <see langword="null"/> or
    /// <paramref name="reason"/> is null, empty, or whitespace.
    /// </returns>
    public D2Result<CompromisedKey> Compromise(string reason, IClock? clock)
    {
        if (clock is null)
            return KeyCustodianFailures<CompromisedKey>.PreconditionViolated();

        return ToCompromised(reason, clock.GetCurrentInstant());
    }
}
