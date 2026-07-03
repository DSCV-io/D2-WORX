// -----------------------------------------------------------------------
// <copyright file="EncryptionSourceMarker.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Encryption;

using D2.Shared.Utilities.Extensions;

/// <summary>
/// Records the provenance (<see cref="EncryptionKeyringSource"/>) of one
/// encryption registration. Registered as a keyed singleton under the same
/// service key as the keyring it describes, so <see cref="EncryptionSourceStartupCheck"/>
/// can assert the source WITHOUT changing the shape of the consumable
/// <see cref="EncryptionRegistration"/> record. Public only so DI can resolve
/// it — callers mark a source via <c>MarkD2EncryptionSource</c> rather than
/// constructing this directly.
/// </summary>
public sealed class EncryptionSourceMarker
{
    /// <summary>Initializes a new <see cref="EncryptionSourceMarker"/>.</summary>
    /// <param name="serviceKey">The keyed-services discriminator (typically the domain).</param>
    /// <param name="source">The provenance of the registration under that key.</param>
    public EncryptionSourceMarker(string serviceKey, EncryptionKeyringSource source)
    {
        serviceKey.ThrowIfFalsey();
        ServiceKey = serviceKey;
        Source = source;
    }

    /// <summary>Gets the keyed-services discriminator this marker covers.</summary>
    public string ServiceKey { get; }

    /// <summary>Gets the provenance of the registration under <see cref="ServiceKey"/>.</summary>
    public EncryptionKeyringSource Source { get; }
}
