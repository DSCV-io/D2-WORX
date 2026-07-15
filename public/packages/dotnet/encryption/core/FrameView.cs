// -----------------------------------------------------------------------
// <copyright file="FrameView.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Encryption;

/// <summary>
/// Decoded view over a frame buffer. All spans alias the source — the
/// caller must not mutate the source while the view is in use.
/// </summary>
internal readonly ref struct FrameView
{
    /// <summary>Initializes a new <see cref="FrameView"/> over the given component spans.</summary>
    /// <param name="version">Frame version byte.</param>
    /// <param name="kid">Decoded kid string.</param>
    /// <param name="nonce">Slice over the GCM nonce.</param>
    /// <param name="ciphertextWithTag">Slice over the ciphertext + auth tag.</param>
    internal FrameView(
        byte version, string kid, ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> ciphertextWithTag)
    {
        Version = version;
        Kid = kid;
        Nonce = nonce;
        CiphertextWithTag = ciphertextWithTag;
    }

    /// <summary>Gets the frame's version byte.</summary>
    internal byte Version { get; }

    /// <summary>Gets the kid declared in the frame header.</summary>
    internal string Kid { get; }

    /// <summary>Gets the GCM nonce.</summary>
    internal ReadOnlySpan<byte> Nonce { get; }

    /// <summary>Gets the ciphertext bytes followed by the auth tag.</summary>
    internal ReadOnlySpan<byte> CiphertextWithTag { get; }
}
