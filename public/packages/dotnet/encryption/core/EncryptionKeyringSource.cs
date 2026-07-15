// -----------------------------------------------------------------------
// <copyright file="EncryptionKeyringSource.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Encryption;

/// <summary>
/// Provenance of the keyring backing a registered <see cref="IPayloadCrypto"/>.
/// The type-zero value is the fail-closed DENY default: a registration whose
/// source cannot be determined (no marker, or the enum default) is treated as
/// <see cref="StaticFactory"/> so a hand-wired static key can never slip past
/// <see cref="EncryptionSourceStartupCheck"/> in a non-Development host.
/// </summary>
public enum EncryptionKeyringSource
{
    /// <summary>
    /// A hand-supplied key factory (the raw <c>AddD2EncryptionFor</c> seam).
    /// Rejected outside a Development host — this is the production footgun the
    /// startup check guards against, and the type-zero fail-closed default.
    /// </summary>
    StaticFactory = 0,

    /// <summary>
    /// A KeyCustodian-sourced, rotation-aware keyring. The only source
    /// <see cref="EncryptionSourceStartupCheck"/> accepts in a non-Development host.
    /// </summary>
    KeyCustodian = 1,
}
