// -----------------------------------------------------------------------
// <copyright file="ActiveKey.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.Domain.Entities;

/// <summary>
/// A managed encryption key that has been smoke-tested and is in active use
/// for encryption and/or signing. The legal forward transitions are
/// <see cref="Rotate"/> and <see cref="Compromise"/>.
/// </summary>
/// <remarks>
/// No <c>Activate</c> or <c>Retire</c> method exists on this type — calling
/// them would not compile (illegal-state-unrepresentable, §9.31).
/// </remarks>
public sealed record ActiveKey : EncryptionKey
{
    /// <inheritdoc/>
    public override KeyStatus Status => KeyStatus.Active;

    /// <summary>
    /// Gets the UTC instant at which this key was activated.
    /// </summary>
    /// <remarks>
    /// Cat 2 bare <see cref="Instant"/> (§25.3) — generic UTC timestamp;
    /// no wall-clock context to preserve.
    /// </remarks>
    public required Instant ActivatedAt { get; init; }

    /// <summary>
    /// Rotates this active key by accepting a freshly-built pending successor.
    /// Returns the current key in the <c>Retiring</c> state together with the
    /// unmodified successor, so the App can persist both in a single atomic write.
    /// </summary>
    /// <param name="successor">
    /// A <see cref="PendingKey"/> whose <see cref="EncryptionKey.KeyDomain"/>
    /// and <see cref="EncryptionKey.KeyType"/> match this key. Must be non-null.
    /// </param>
    /// <param name="clock">The current-time source. Must be non-null.</param>
    /// <returns>
    /// <c>Ok</c> with a tuple of the transitioning key (now
    /// <see cref="RetiringKey"/>) and the unmodified <paramref name="successor"/>;
    /// a flagged <c>KEYCUSTODIAN_PRECONDITION_VIOLATED</c> failure when
    /// <paramref name="successor"/> or <paramref name="clock"/> is
    /// <see langword="null"/>, or when <paramref name="successor"/> has a different
    /// <see cref="EncryptionKey.KeyDomain"/> or <see cref="EncryptionKey.KeyType"/>
    /// than this key. The mismatch is a programmer/precondition error (the App
    /// built the wrong successor) surfaced as a telemetry-flagged internal-error
    /// result rather than a thrown exception.
    /// </returns>
    public D2Result<(RetiringKey Retiring, PendingKey Successor)> Rotate(
        PendingKey? successor,
        IClock? clock)
    {
        if (successor is null)
            return KeyCustodianFailures<(RetiringKey, PendingKey)>.PreconditionViolated();

        if (clock is null)
            return KeyCustodianFailures<(RetiringKey, PendingKey)>.PreconditionViolated();

        if (!Equals(successor.KeyDomain, KeyDomain) || successor.KeyType != KeyType)
            return KeyCustodianFailures<(RetiringKey, PendingKey)>.PreconditionViolated();

        var now = clock.GetCurrentInstant();

        var retiring = new RetiringKey
        {
            Kid = Kid,
            KeyDomain = KeyDomain,
            KeyType = KeyType,
            KeyMaterialEncrypted = KeyMaterialEncrypted,
            PublicKeyMaterial = PublicKeyMaterial,
            CaCertificateMaterial = CaCertificateMaterial,
            CreatedAt = CreatedAt,
            ActivatedAt = ActivatedAt,
            RetiringAt = now,
        };

        return D2Result<(RetiringKey, PendingKey)>.Ok((retiring, successor));
    }

    /// <summary>
    /// Immediately marks this active key as compromised. Requires a non-empty
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
