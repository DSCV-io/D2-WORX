// -----------------------------------------------------------------------
// <copyright file="KidNotInKeyringException.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Encryption;

/// <summary>
/// Raised when a frame's declared kid is not present in the current keyring.
/// </summary>
/// <remarks>
/// On the encrypt path this signals programmer error (the keyring no longer
/// contains its declared active kid). On the decrypt path this is the
/// expected outcome when a message was encrypted under a key that has been
/// retired and removed from the running keyring — the caller (typically the
/// messaging bus) routes the message to a DLQ for forensic decrypt by the
/// ops CLI.
/// </remarks>
public sealed class KidNotInKeyringException : EncryptionException
{
    /// <summary>
    /// Initializes a new <see cref="KidNotInKeyringException"/> for the given kid.
    /// </summary>
    /// <param name="kid">The kid that was not found.</param>
    public KidNotInKeyringException(string kid)
        : base($"Kid '{kid}' is not present in the current keyring.")
    {
        Kid = kid;
    }

    /// <summary>Gets the kid that was not found in the keyring.</summary>
    public string Kid { get; }
}
