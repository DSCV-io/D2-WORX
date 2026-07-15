// -----------------------------------------------------------------------
// <copyright file="FrameVersionMismatchException.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Encryption;

/// <summary>
/// Raised when a frame's version byte is not the current version supported
/// by this lib.
/// </summary>
/// <remarks>
/// Frames are versioned so that a future format revision can ship without
/// silently misinterpreting old (or attacker-crafted) frames. There is no
/// "best effort" decode path — unrecognized versions are rejected.
/// </remarks>
public sealed class FrameVersionMismatchException : EncryptionException
{
    /// <summary>Initializes a new <see cref="FrameVersionMismatchException"/>.</summary>
    /// <param name="version">The unrecognized version byte.</param>
    public FrameVersionMismatchException(byte version)
        : base($"Frame version {version} is not supported by this lib.")
    {
        Version = version;
    }

    /// <summary>Gets the unrecognized version byte from the frame.</summary>
    public byte Version { get; }
}
