// -----------------------------------------------------------------------
// <copyright file="RetiredKey.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.KeyCustodian.Domain.Entities;

/// <summary>
/// A fully retired managed encryption key. This is a terminal state — no further
/// lifecycle transitions are permitted.
/// </summary>
/// <remarks>
/// <b>Terminal state.</b> <c>RetiredKey</c> exposes NO transition methods.
/// Attempting to call <c>Activate</c>, <c>Rotate</c>, <c>Retire</c>, or
/// <c>Compromise</c> on a <c>RetiredKey</c> does not compile — making those
/// illegal operations unrepresentable at the type level (§9.31).
///
/// <b>Material retention.</b> The encrypted key material is retained even in
/// this terminal state so that historical payloads encrypted before rotation
/// can still be decrypted (overlap decryption guarantee).
/// </remarks>
public sealed record RetiredKey : EncryptionKey
{
    /// <inheritdoc/>
    public override KeyStatus Status => KeyStatus.Retired;

    /// <summary>
    /// Gets the UTC instant at which this key was originally activated.
    /// </summary>
    /// <remarks>
    /// Cat 2 bare <see cref="Instant"/> (§25.3) — generic UTC timestamp;
    /// no wall-clock context to preserve.
    /// </remarks>
    public required Instant ActivatedAt { get; init; }

    /// <summary>
    /// Gets the UTC instant at which rotation began (the key entered the retiring state).
    /// </summary>
    /// <remarks>
    /// Cat 2 bare <see cref="Instant"/> (§25.3) — generic UTC timestamp;
    /// no wall-clock context to preserve.
    /// </remarks>
    public required Instant RetiringAt { get; init; }

    /// <summary>
    /// Gets the UTC instant at which this key was fully retired.
    /// </summary>
    /// <remarks>
    /// Cat 2 bare <see cref="Instant"/> (§25.3) — generic UTC timestamp;
    /// no wall-clock context to preserve.
    /// </remarks>
    public required Instant RetiredAt { get; init; }
}
