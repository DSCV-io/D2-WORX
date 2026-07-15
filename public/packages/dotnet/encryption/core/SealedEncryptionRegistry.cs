// -----------------------------------------------------------------------
// <copyright file="SealedEncryptionRegistry.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Encryption;

using System.Linq;

/// <summary>
/// Aggregates every <see cref="SealedEncryptionRegistration"/> into a single
/// queryable registry, resolved by <see cref="SealedEncryptionStartupCheck"/>.
/// Sealed sibling of <see cref="EncryptionRegistry"/>. Deliberately
/// <c>internal</c> (minimal surface — see
/// <see cref="SealedEncryptionRegistration"/>).
/// </summary>
internal sealed class SealedEncryptionRegistry
{
    /// <summary>Initializes a new <see cref="SealedEncryptionRegistry"/>.</summary>
    /// <param name="registrations">
    /// All registrations made via <c>AddD2SealedEncryptionRecipient</c>.
    /// </param>
    public SealedEncryptionRegistry(IEnumerable<SealedEncryptionRegistration> registrations)
    {
        ArgumentNullException.ThrowIfNull(registrations);
        RecipientServiceIds = registrations
            .Select(r => r.RecipientServiceId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>Gets every recipient service id that has been registered.</summary>
    public IReadOnlyList<string> RecipientServiceIds { get; }
}
