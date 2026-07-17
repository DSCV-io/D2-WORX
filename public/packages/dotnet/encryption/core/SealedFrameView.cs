// -----------------------------------------------------------------------
// <copyright file="SealedFrameView.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Encryption;

/// <summary>
/// Decoded view over a sealed (version-2) frame buffer. All spans alias the
/// source — the caller must not mutate the source while the view is in use.
/// Sibling of <see cref="FrameView"/> for the sealed frame family.
/// </summary>
internal readonly ref struct SealedFrameView
{
    /// <summary>Initializes a new <see cref="SealedFrameView"/> over the given component spans.</summary>
    /// <param name="version">Frame version byte.</param>
    /// <param name="recipientKid">Decoded recipient kid string.</param>
    /// <param name="ephemeralPublicSpki">Slice over the ephemeral public key (SPKI DER).</param>
    /// <param name="nonce">Slice over the GCM nonce.</param>
    /// <param name="ciphertextWithTag">Slice over the ciphertext + auth tag.</param>
    internal SealedFrameView(
        byte version,
        string recipientKid,
        ReadOnlySpan<byte> ephemeralPublicSpki,
        ReadOnlySpan<byte> nonce,
        ReadOnlySpan<byte> ciphertextWithTag)
    {
        Version = version;
        RecipientKid = recipientKid;
        EphemeralPublicSpki = ephemeralPublicSpki;
        Nonce = nonce;
        CiphertextWithTag = ciphertextWithTag;
    }

    /// <summary>Gets the frame's version byte.</summary>
    internal byte Version { get; }

    /// <summary>Gets the recipient kid declared in the frame header.</summary>
    internal string RecipientKid { get; }

    /// <summary>Gets the ephemeral public key bytes (SubjectPublicKeyInfo DER).</summary>
    internal ReadOnlySpan<byte> EphemeralPublicSpki { get; }

    /// <summary>Gets the GCM nonce.</summary>
    internal ReadOnlySpan<byte> Nonce { get; }

    /// <summary>Gets the ciphertext bytes followed by the auth tag.</summary>
    internal ReadOnlySpan<byte> CiphertextWithTag { get; }
}
