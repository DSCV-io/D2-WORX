// -----------------------------------------------------------------------
// <copyright file="ActiveKey.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.Domain.Keys;

using D2.Edge.KeyCustodian.Domain.Enums;
using NodaTime;
using IClock = D2.Shared.Time.IClock;

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
    /// <remarks>Cat 2 bare <see cref="Instant"/> (§25.3) — generic UTC timestamp; no wall-clock context to preserve.</remarks>
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
    /// A tuple of the transitioning key (now <see cref="RetiringKey"/>) and
    /// the unmodified <paramref name="successor"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="successor"/> or <paramref name="clock"/> is
    /// <see langword="null"/>.
    /// — §5.1a plain reference-type null-guard; BCL ThrowIfNull used.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="successor"/> has a different
    /// <see cref="EncryptionKey.KeyDomain"/> or <see cref="EncryptionKey.KeyType"/>
    /// than this key. This is a programmer error (the App built the wrong
    /// successor) — a throw is correct here.
    /// — §5.1a bespoke-message carve-out.
    /// </exception>
    public (RetiringKey Retiring, PendingKey Successor) Rotate(PendingKey successor, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(successor);
        ArgumentNullException.ThrowIfNull(clock);

        if (!Equals(successor.KeyDomain, KeyDomain) || successor.KeyType != KeyType)
        {
            // §5.1a bespoke-message carve-out: domain/type mismatch is a programmer error.
            throw new ArgumentException(
                $"Successor must belong to the same KeyDomain ('{KeyDomain.Value}') and KeyType ('{KeyType}') " +
                $"as the key being rotated. Got domain='{successor.KeyDomain.Value}', type='{successor.KeyType}'.",
                nameof(successor));
        }

        var now = clock.GetCurrentInstant();
        var retiring = new RetiringKey
        {
            Kid = Kid,
            KeyDomain = KeyDomain,
            KeyType = KeyType,
            KeyMaterialEncrypted = KeyMaterialEncrypted,
            PublicKeyMaterial = PublicKeyMaterial,
            CreatedAt = CreatedAt,
            ActivatedAt = ActivatedAt,
            RetiringAt = now,
        };

        return (retiring, successor);
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
