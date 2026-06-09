// -----------------------------------------------------------------------
// <copyright file="KeyStatus.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.Domain.Enums;

/// <summary>
/// Derived, state-machine-driven discriminator for an encryption key.
/// Each value is the fixed, never-directly-settable status of a particular sealed
/// state type in the <c>EncryptionKey</c> sum-type hierarchy.
/// </summary>
/// <remarks>
/// This enum is persisted as the flat <c>KeyRecord</c>'s <c>status</c> value
/// column (not a TPH type discriminator). It is always derived from the concrete
/// sealed type (each sealed state overrides <c>EncryptionKey.Status</c> with a
/// constant), so it is NEVER assigned directly from business logic — only from the
/// type system.
/// </remarks>
public enum KeyStatus
{
    /// <summary>Generated, not yet smoke-tested and activated.</summary>
    Pending,

    /// <summary>Smoke-tested and in active use for encryption/signing.</summary>
    Active,

    /// <summary>Being phased out; still serves in-flight decryptions and JWKS responses.</summary>
    Retiring,

    /// <summary>Fully retired; retained for forensics and overlap decryption of historical payloads.</summary>
    Retired,

    /// <summary>Marked compromised; removed from active use. Material retained for forensics.</summary>
    Compromised,
}
