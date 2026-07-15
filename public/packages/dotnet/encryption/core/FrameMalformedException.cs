// -----------------------------------------------------------------------
// <copyright file="FrameMalformedException.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Encryption;

/// <summary>
/// Raised when a frame buffer is structurally invalid — too short for the
/// minimum, declared kid_length overruns the buffer, kid is not valid
/// UTF-8, etc.
/// </summary>
/// <remarks>
/// Distinct from <see cref="System.Security.Cryptography.AuthenticationTagMismatchException"/>:
/// a malformed frame never reached the AEAD primitive; an auth-tag
/// mismatch did. The ops response differs (garbage bytes vs. tampered or
/// mis-routed ciphertext).
/// </remarks>
public sealed class FrameMalformedException : EncryptionException
{
    /// <summary>Initializes a new <see cref="FrameMalformedException"/>.</summary>
    /// <param name="message">
    /// Description of the structural error. Must not include any frame bytes.
    /// </param>
    public FrameMalformedException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new <see cref="FrameMalformedException"/> with an
    /// underlying cause (e.g. a cryptographic import failure on a
    /// frame-borne key).
    /// </summary>
    /// <param name="message">
    /// Description of the structural error. Must not include any frame bytes.
    /// </param>
    /// <param name="inner">Underlying cause.</param>
    public FrameMalformedException(string message, Exception inner)
        : base(message, inner)
    {
    }
}
