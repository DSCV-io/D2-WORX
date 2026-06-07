// -----------------------------------------------------------------------
// <copyright file="RetiringKey.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.Domain.Keys;

using D2.Edge.KeyCustodian.Domain.Enums;
using D2.Edge.KeyCustodian.Domain.ValueObjects;
using D2.Shared.Result;
using NodaTime;
using IClock = D2.Shared.Time.IClock;
using TK = D2.Shared.I18n.TK;

/// <summary>
/// A managed encryption key in the retirement overlap window. It continues to
/// serve in-flight decryptions and JWKS responses while the new active key
/// takes over. The legal forward transitions are <see cref="Retire"/> and
/// <see cref="Compromise"/>.
/// </summary>
/// <remarks>
/// No <c>Activate</c> or <c>Rotate</c> method exists on this type — calling
/// them would not compile (illegal-state-unrepresentable, §9.31).
/// </remarks>
public sealed record RetiringKey : EncryptionKey
{
    /// <inheritdoc/>
    public override KeyStatus Status => KeyStatus.Retiring;

    /// <summary>
    /// Gets the UTC instant at which this key was originally activated.
    /// </summary>
    /// <remarks>Cat 2 bare <see cref="Instant"/> (§25.3) — generic UTC timestamp; no wall-clock context to preserve.</remarks>
    public required Instant ActivatedAt { get; init; }

    /// <summary>
    /// Gets the UTC instant at which rotation began (the key entered the retiring state).
    /// </summary>
    /// <remarks>Cat 2 bare <see cref="Instant"/> (§25.3) — generic UTC timestamp; no wall-clock context to preserve.</remarks>
    public required Instant RetiringAt { get; init; }

    /// <summary>
    /// Attempts to retire this key. Requires that the grace window has elapsed
    /// since rotation began.
    /// </summary>
    /// <param name="policy">
    /// The rotation policy governing this key's grace window. Must be non-null.
    /// </param>
    /// <param name="clock">The current-time source. Must be non-null.</param>
    /// <returns>
    /// <c>Ok(<see cref="RetiredKey"/>)</c> when the grace window has elapsed;
    /// <c>ValidationFailed([GRACE_NOT_ELAPSED])</c> when the window is still open.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="policy"/> or <paramref name="clock"/> is
    /// <see langword="null"/>.
    /// </exception>
    public D2Result<RetiredKey> Retire(RotationPolicy policy, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(clock);

        var now = clock.GetCurrentInstant();
        var elapsed = now - RetiringAt;

        if (elapsed < policy.Grace)
        {
            return D2Result<RetiredKey>.ValidationFailed(
                messages: [TK.Key.Custodian.VALIDATION_GRACE_NOT_ELAPSED]);
        }

        return D2Result<RetiredKey>.Ok(new RetiredKey
        {
            Kid = Kid,
            KeyDomain = KeyDomain,
            KeyType = KeyType,
            KeyMaterialEncrypted = KeyMaterialEncrypted,
            PublicKeyMaterial = PublicKeyMaterial,
            CreatedAt = CreatedAt,
            ActivatedAt = ActivatedAt,
            RetiringAt = RetiringAt,
            RetiredAt = now,
        });
    }

    /// <summary>
    /// Immediately marks this retiring key as compromised. Requires a non-empty
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
