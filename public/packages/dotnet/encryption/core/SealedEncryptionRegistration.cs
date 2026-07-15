// -----------------------------------------------------------------------
// <copyright file="SealedEncryptionRegistration.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Encryption;

/// <summary>
/// Marker registered once per sealed recipient wired into the container
/// (via <c>AddD2SealedEncryptionRecipient</c>). The
/// <see cref="SealedEncryptionRegistry"/> aggregates these so the sealed
/// startup self-check knows which recipients to verify. Deliberately
/// <c>internal</c>: the sealed registration surface stays minimal until the
/// registration-by-service sources populate it, so it can grow additively
/// without a public-API break.
/// </summary>
internal sealed class SealedEncryptionRegistration
{
    /// <summary>Initializes a new <see cref="SealedEncryptionRegistration"/>.</summary>
    /// <param name="recipientServiceId">
    /// The recipient service id the sealer/opener registrations are keyed by.
    /// </param>
    public SealedEncryptionRegistration(string recipientServiceId)
    {
        SealedKeyringValidation.ValidateServiceId(
            recipientServiceId, nameof(recipientServiceId));
        RecipientServiceId = recipientServiceId;
    }

    /// <summary>Gets the recipient service id this registration covers.</summary>
    public string RecipientServiceId { get; }
}
