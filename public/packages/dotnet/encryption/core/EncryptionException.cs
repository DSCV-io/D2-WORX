// -----------------------------------------------------------------------
// <copyright file="EncryptionException.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Encryption;

/// <summary>
/// Base type for every exception raised by this lib's framing or keyring
/// path. AEAD authentication failures surface as the BCL
/// <see cref="System.Security.Cryptography.AuthenticationTagMismatchException"/>
/// and are not wrapped — that distinction matters for callers (a tag
/// mismatch is "tampering or wrong AAD or wrong key for kid", a frame
/// error is "garbage bytes that never came from us").
/// </summary>
public abstract class EncryptionException : Exception
{
    /// <summary>Initializes a new exception with a message.</summary>
    /// <param name="message">
    /// Human-readable error description. Must not include any ciphertext or key bytes.
    /// </param>
    protected EncryptionException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new exception with a message and an inner exception.</summary>
    /// <param name="message">Human-readable error description.</param>
    /// <param name="inner">Underlying cause.</param>
    protected EncryptionException(string message, Exception inner)
        : base(message, inner)
    {
    }
}
