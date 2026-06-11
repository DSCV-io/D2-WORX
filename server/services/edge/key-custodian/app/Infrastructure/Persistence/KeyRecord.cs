// -----------------------------------------------------------------------
// <copyright file="KeyRecord.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.App.Infrastructure.Persistence;

using D2.Edge.KeyCustodian.Domain.Enums;
using NodaTime;

/// <summary>
/// Flat, non-polymorphic EF persistence row for a managed encryption key.
/// </summary>
/// <remarks>
/// <b>Why a flat record and not the aggregate.</b> The domain models a key as
/// an immutable sum type (abstract <c>EncryptionKey</c> + sealed per-state
/// records). EF Core cannot morph a tracked entity's runtime CLR type on a
/// transition (a same-PK remove+add silently merges into a stale-column UPDATE,
/// and a get-only status discriminator fails model-build). So persistence uses
/// this single flat record whose CLR type never changes; <see cref="Status"/>
/// is an ordinary settable value column, and every per-state timestamp is a
/// nullable primitive column. The aggregate rehydrates from this row via the
/// pure mapper (<c>KeyRecordMapper.ToDomain</c>); a transition writes back via
/// <c>EncryptionKey.ProjectOnto(record)</c> (an ordinary UPDATE).
///
/// <b>All-primitive columns.</b> Every property is a primitive / BCL type or a
/// closed enum — there are no value-object columns, so the EF model needs no
/// value converters. Value objects are reconstructed inside the mapper via the
/// domain's <c>FromTrusted</c> factories.
///
/// <b>No key material in logs.</b> <see cref="KeyMaterialEncrypted"/> holds
/// root-wrapped ciphertext; the mapper never logs it and the handlers never
/// surface it. <see cref="PublicKeyMaterial"/> is the unencrypted SPKI public
/// key (asymmetric keys only) and is intentionally loggable — it is published
/// via JWKS.
/// </remarks>
public sealed class KeyRecord
{
    /// <summary>Gets or sets the unique key identifier (JWKS <c>kid</c> claim). Primary key.</summary>
    public required string Kid { get; set; }

    /// <summary>Gets or sets the logical keyring this key belongs to.</summary>
    public required string KeyDomain { get; set; }

    /// <summary>Gets or sets the cryptographic algorithm category of this key.</summary>
    public required KeyType KeyType { get; set; }

    /// <summary>Gets or sets the root-key-encrypted key-material bytes. Never logged.</summary>
    [RedactData(Reason = RedactReason.SecretInformation)]
    public required byte[] KeyMaterialEncrypted { get; set; }

    /// <summary>
    /// Gets or sets the unencrypted SPKI public-key bytes for asymmetric
    /// (<c>RsaSigning</c>) keys; <see langword="null"/> for symmetric keys.
    /// </summary>
    public byte[]? PublicKeyMaterial { get; set; }

    /// <summary>
    /// Gets or sets the UTC instant at which this key was generated.
    /// </summary>
    /// <remarks>Cat 2 bare <see cref="Instant"/> (§25.3) — generic UTC timestamp; no wall-clock context to preserve.</remarks>
    public required Instant CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the lifecycle status value of this key. A settable value
    /// column — NOT a TPH type discriminator. Always derived from the aggregate's
    /// concrete sealed state via the mapper; never written by hand.
    /// </summary>
    public required KeyStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the UTC instant at which this key was activated; <see langword="null"/>
    /// while the key is still pending (or was compromised before activation).
    /// </summary>
    /// <remarks>Cat 2 bare <see cref="Instant"/> (§25.3) — generic UTC timestamp; no wall-clock context to preserve.</remarks>
    public Instant? ActivatedAt { get; set; }

    /// <summary>
    /// Gets or sets the UTC instant at which rotation began (the key entered the
    /// retiring state); <see langword="null"/> outside the retiring/retired states.
    /// </summary>
    /// <remarks>Cat 2 bare <see cref="Instant"/> (§25.3) — generic UTC timestamp; no wall-clock context to preserve.</remarks>
    public Instant? RetiringAt { get; set; }

    /// <summary>
    /// Gets or sets the UTC instant at which this key was fully retired;
    /// <see langword="null"/> outside the retired state.
    /// </summary>
    /// <remarks>Cat 2 bare <see cref="Instant"/> (§25.3) — generic UTC timestamp; no wall-clock context to preserve.</remarks>
    public Instant? RetiredAt { get; set; }

    /// <summary>
    /// Gets or sets the UTC instant at which this key was marked compromised;
    /// <see langword="null"/> outside the compromised state.
    /// </summary>
    /// <remarks>Cat 2 bare <see cref="Instant"/> (§25.3) — generic UTC timestamp; no wall-clock context to preserve.</remarks>
    public Instant? CompromisedAt { get; set; }

    /// <summary>
    /// Gets or sets the operator-supplied compromise reason; <see langword="null"/>
    /// outside the compromised state. May contain operator context — never logged
    /// or echoed into an audit breadcrumb.
    /// </summary>
    [RedactData(Reason = RedactReason.PersonalInformation)]
    public string? CompromiseReason { get; set; }

    /// <summary>
    /// Gets or sets the PostgreSQL <c>xmin</c> system column used as the optimistic
    /// concurrency token. Mapped as the EF concurrency token in the Infra-layer
    /// entity configuration; ensures exactly-one-winner on a leaderless
    /// rotation race. Default <c>0</c> for an in-memory / not-yet-persisted row.
    /// </summary>
    public uint Xmin { get; set; }
}
