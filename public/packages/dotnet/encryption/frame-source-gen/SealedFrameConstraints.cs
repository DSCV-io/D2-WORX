// -----------------------------------------------------------------------
// <copyright file="SealedFrameConstraints.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.EncryptionFrame.SourceGen;

/// <summary>
/// Frame-level numeric constraints parsed from the <c>constraints</c> block
/// of the sealed-frame spec.
/// </summary>
/// <param name="MinKidLength">Minimum allowed recipient-kid length in UTF-8 bytes.</param>
/// <param name="MaxKidLength">Maximum allowed recipient-kid length in UTF-8 bytes.</param>
/// <param name="EphPubLengthPrefixSize">
/// Byte width of the big-endian length prefix in front of the ephemeral
/// public key (always 2 — uint16).
/// </param>
/// <param name="MaxEphPubLength">
/// Upper cap on the declared ephemeral-public-key length (allocation guard).
/// </param>
/// <param name="NonceLength">AES-GCM nonce length in bytes (always 12).</param>
/// <param name="TagLength">AES-GCM authentication tag length in bytes (always 16).</param>
/// <param name="MinFrameSize">Smallest valid sealed frame size in bytes.</param>
internal sealed record SealedFrameConstraints(
    int MinKidLength,
    int MaxKidLength,
    int EphPubLengthPrefixSize,
    int MaxEphPubLength,
    int NonceLength,
    int TagLength,
    int MinFrameSize);
