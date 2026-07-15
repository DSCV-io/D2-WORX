// -----------------------------------------------------------------------
// <copyright file="EncryptionRegistration.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Encryption;

using DcsvIo.D2.Utilities.Extensions;

/// <summary>
/// Marker registered once per call to
/// <see cref="EncryptionServiceCollectionExtensions.AddD2EncryptionFor"/>.
/// The <see cref="EncryptionRegistry"/> aggregates these so the startup
/// self-test knows what to round-trip. Public only so DI can resolve it —
/// callers should not construct this directly.
/// </summary>
public sealed class EncryptionRegistration
{
    /// <summary>Initializes a new <see cref="EncryptionRegistration"/>.</summary>
    /// <param name="serviceKey">The keyed-services discriminator.</param>
    public EncryptionRegistration(string serviceKey)
    {
        serviceKey.ThrowIfFalsey();
        ServiceKey = serviceKey;
    }

    /// <summary>Gets the keyed-services discriminator the registration uses.</summary>
    public string ServiceKey { get; }
}
