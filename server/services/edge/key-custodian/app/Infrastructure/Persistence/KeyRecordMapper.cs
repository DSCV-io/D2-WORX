// -----------------------------------------------------------------------
// <copyright file="KeyRecordMapper.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.App.Infrastructure.Persistence;

using System;
using D2.Edge.KeyCustodian.Domain.Entities;
using D2.Edge.KeyCustodian.Domain.Enums;
using D2.Edge.KeyCustodian.Domain.ValueObjects;
using NodaTime;

/// <summary>
/// Pure, stateless mapper between the flat persistence records
/// (<see cref="KeyRecord"/> / <see cref="KeyAuditRecord"/>) and the immutable
/// domain sum type (<see cref="EncryptionKey"/> + sealed states /
/// <see cref="EncryptionKeyAudit"/>).
/// </summary>
/// <remarks>
/// <b>Why a static mapper and not a DI service.</b> Re-interfacing the mapper
/// would reconstitute the per-op Repository layer the EF-as-DDD convention
/// retires. The mapping is pure (no I/O, no DI, no clock) so it lives as
/// extension members.
///
/// <b>Trusted-store carve-out.</b> <see cref="ToDomain"/> rehydrates value
/// objects via their <c>FromTrusted</c> factories (no re-validation — the row
/// was validated on the way in). A structurally corrupt row (a status whose
/// required timestamp column is null, or an out-of-range enum) is a
/// trusted-store-corruption bug, not user input — it throws
/// <see cref="InvalidOperationException"/>, the same carve-out class as the
/// <c>FromTrusted</c> factories themselves. This is pinned by tests.
///
/// <b>Anti-stale-column discipline.</b> <see cref="ProjectOnto"/> nulls EVERY
/// per-state timestamp / reason column before setting only the ones the new
/// state owns, so a transition can never leave a stale column from a prior
/// state (e.g. a compromised key must not retain its <c>ActivatedAt</c>).
/// </remarks>
public static class KeyRecordMapper
{
    extension(KeyRecord record)
    {
        /// <summary>
        /// Rehydrates the immutable domain aggregate from this flat row.
        /// </summary>
        /// <returns>The sealed <see cref="EncryptionKey"/> state matching <see cref="KeyRecord.Status"/>.</returns>
        /// <exception cref="InvalidOperationException">
        /// The row is structurally corrupt — a status whose required timestamp
        /// column is null, or an unrecognized <see cref="KeyStatus"/> value
        /// (trusted-store-corruption carve-out).
        /// </exception>
        public EncryptionKey ToDomain()
        {
            var kid = Kid.FromTrusted(record.Kid);
            var keyDomain = KeyDomain.FromTrusted(record.KeyDomain);
            var material = KeyMaterialEncrypted.FromTrusted(record.KeyMaterialEncrypted);
            var publicMaterial = record.PublicKeyMaterial is { } pub
                ? PublicKeyMaterial.FromTrusted(pub)
                : null;

            return record.Status switch
            {
                KeyStatus.Pending => new PendingKey
                {
                    Kid = kid,
                    KeyDomain = keyDomain,
                    KeyType = record.KeyType,
                    KeyMaterialEncrypted = material,
                    PublicKeyMaterial = publicMaterial,
                    CreatedAt = record.CreatedAt,
                },
                KeyStatus.Active => new ActiveKey
                {
                    Kid = kid,
                    KeyDomain = keyDomain,
                    KeyType = record.KeyType,
                    KeyMaterialEncrypted = material,
                    PublicKeyMaterial = publicMaterial,
                    CreatedAt = record.CreatedAt,
                    ActivatedAt = record.RequireInstant(record.ActivatedAt, nameof(KeyRecord.ActivatedAt)),
                },
                KeyStatus.Retiring => new RetiringKey
                {
                    Kid = kid,
                    KeyDomain = keyDomain,
                    KeyType = record.KeyType,
                    KeyMaterialEncrypted = material,
                    PublicKeyMaterial = publicMaterial,
                    CreatedAt = record.CreatedAt,
                    ActivatedAt = record.RequireInstant(record.ActivatedAt, nameof(KeyRecord.ActivatedAt)),
                    RetiringAt = record.RequireInstant(record.RetiringAt, nameof(KeyRecord.RetiringAt)),
                },
                KeyStatus.Retired => new RetiredKey
                {
                    Kid = kid,
                    KeyDomain = keyDomain,
                    KeyType = record.KeyType,
                    KeyMaterialEncrypted = material,
                    PublicKeyMaterial = publicMaterial,
                    CreatedAt = record.CreatedAt,
                    ActivatedAt = record.RequireInstant(record.ActivatedAt, nameof(KeyRecord.ActivatedAt)),
                    RetiringAt = record.RequireInstant(record.RetiringAt, nameof(KeyRecord.RetiringAt)),
                    RetiredAt = record.RequireInstant(record.RetiredAt, nameof(KeyRecord.RetiredAt)),
                },
                KeyStatus.Compromised => new CompromisedKey
                {
                    Kid = kid,
                    KeyDomain = keyDomain,
                    KeyType = record.KeyType,
                    KeyMaterialEncrypted = material,
                    PublicKeyMaterial = publicMaterial,
                    CreatedAt = record.CreatedAt,
                    CompromisedAt = record.RequireInstant(record.CompromisedAt, nameof(KeyRecord.CompromisedAt)),
                    Reason = record.CompromiseReason
                        ?? throw record.Corrupt(nameof(KeyRecord.CompromiseReason)),
                },
                _ => throw record.Corrupt($"unrecognized status {record.Status}"),
            };
        }

        /// <summary>
        /// Reads a required timestamp column, throwing the trusted-store-corruption
        /// exception when it is null for a state that requires it.
        /// </summary>
        private Instant RequireInstant(Instant? value, string column) =>
            value ?? throw record.Corrupt(column);

        /// <summary>
        /// Builds the trusted-store-corruption exception naming the offending row + detail.
        /// </summary>
        private InvalidOperationException Corrupt(string detail) =>
            new(
                $"KeyRecord '{record.Kid}' (status {record.Status}) is structurally corrupt: {detail}. "
                + "The persistence store violated an invariant the domain guarantees on write.");
    }

    extension(EncryptionKey key)
    {
        /// <summary>
        /// Projects this aggregate's state onto a tracked <see cref="KeyRecord"/>,
        /// nulling EVERY per-state column first, then setting only the columns the
        /// current state owns. The immutable identity columns (<c>Kid</c>,
        /// <c>KeyDomain</c>, <c>KeyType</c>, <c>KeyMaterialEncrypted</c>,
        /// <c>PublicKeyMaterial</c>, <c>CreatedAt</c>) are left untouched on a
        /// transition (they never change) but ARE written on a freshly-built row
        /// via <see cref="ToNewRecord"/>.
        /// </summary>
        /// <param name="record">The tracked record to mutate in place.</param>
        public void ProjectOnto(KeyRecord record)
        {
            // Anti-stale-column discipline: clear ALL per-state columns first.
            record.Status = key.Status;
            record.ActivatedAt = null;
            record.RetiringAt = null;
            record.RetiredAt = null;
            record.CompromisedAt = null;
            record.CompromiseReason = null;

            switch (key)
            {
                case PendingKey:
                    break;
                case ActiveKey active:
                    record.ActivatedAt = active.ActivatedAt;
                    break;
                case RetiringKey retiring:
                    record.ActivatedAt = retiring.ActivatedAt;
                    record.RetiringAt = retiring.RetiringAt;
                    break;
                case RetiredKey retired:
                    record.ActivatedAt = retired.ActivatedAt;
                    record.RetiringAt = retired.RetiringAt;
                    record.RetiredAt = retired.RetiredAt;
                    break;
                case CompromisedKey compromised:
                    record.CompromisedAt = compromised.CompromisedAt;
                    record.CompromiseReason = compromised.Reason;
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Unhandled EncryptionKey state '{key.GetType().Name}' in ProjectOnto — "
                        + "add a case arm when introducing a new sealed state.");
            }
        }

        /// <summary>
        /// Builds a brand-new <see cref="KeyRecord"/> for an INSERT (a freshly
        /// generated or successor key). Sets the identity columns and projects the
        /// current state's per-state columns.
        /// </summary>
        /// <returns>A new, fully-populated <see cref="KeyRecord"/>.</returns>
        public KeyRecord ToNewRecord()
        {
            var record = new KeyRecord
            {
                Kid = key.Kid.Value,
                KeyDomain = key.KeyDomain.Value,
                KeyType = key.KeyType,
                KeyMaterialEncrypted = key.KeyMaterialEncrypted.Bytes.ToArray(),
                PublicKeyMaterial = key.PublicKeyMaterial?.Bytes.ToArray(),
                CreatedAt = key.CreatedAt,
                Status = key.Status,
            };

            key.ProjectOnto(record);
            return record;
        }
    }

    extension(EncryptionKeyAudit audit)
    {
        /// <summary>
        /// Flattens an in-domain audit entry to its persistence row.
        /// </summary>
        /// <returns>A new <see cref="KeyAuditRecord"/>. <c>Id</c> is database-generated.</returns>
        public KeyAuditRecord ToRecord() =>
            new()
            {
                Kid = audit.Kid.Value,
                Action = audit.Action,
                ResultingStatus = audit.ResultingStatus,
                OccurredAt = audit.OccurredAt,
                Detail = audit.Detail,
            };
    }
}
