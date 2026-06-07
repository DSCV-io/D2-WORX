// -----------------------------------------------------------------------
// <copyright file="PublicKeyMaterial.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.Domain.ValueObjects;

using System.Globalization;
using System.Text;

/// <summary>
/// Value object wrapping the unencrypted public key bytes for an asymmetric
/// (<c>RsaSigning</c>) encryption key.
/// </summary>
/// <remarks>
/// <b>Not PII.</b> Public keys are not secret — they are published via the JWKS
/// endpoint. <see cref="Bytes"/> is intentionally NOT marked <c>[RedactData]</c>
/// so the JWKS assembler can log and use the raw bytes freely.
///
/// <b><c>ToString</c> override.</b> Even though the bytes are not secret, the
/// raw byte dump from a record's auto-generated <c>ToString</c>/<c>PrintMembers</c>
/// is verbose and unhelpful. The override emits a byte-count sentinel instead.
///
/// <b>Presence invariant.</b> Only <c>RsaSigning</c> keys carry public material.
/// Symmetric (<c>AesPayload</c>, <c>Secret</c>) keys MUST NOT have a
/// <c>PublicKeyMaterial</c> — the base <c>EncryptionKey.EnsureMaterialShape</c>
/// invariant check enforces this at construction time.
/// </remarks>
public sealed record PublicKeyMaterial
{
    /// <summary>Gets the raw DER/SPKI-encoded public key bytes.</summary>
    public required ReadOnlyMemory<byte> Bytes { get; init; }

    /// <summary>
    /// Reconstructs a <see cref="PublicKeyMaterial"/> from trusted public key bytes
    /// produced by the App layer.
    /// </summary>
    /// <param name="bytes">Non-empty public key bytes.</param>
    /// <returns>A <see cref="PublicKeyMaterial"/> wrapping <paramref name="bytes"/>.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="bytes"/> is empty.
    /// — §5.1a bespoke-message carve-out: empty public key is a programmer error.
    /// </exception>
    public static PublicKeyMaterial FromTrusted(ReadOnlyMemory<byte> bytes)
    {
        if (bytes.IsEmpty)
            throw new ArgumentException("Public key material must not be empty.", nameof(bytes));

        return new() { Bytes = bytes };
    }

    /// <inheritdoc/>
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"PublicKeyMaterial {{ Bytes = [{Bytes.Length} bytes] }}");

    /// <summary>
    /// Overrides auto-generated <c>PrintMembers</c> to emit a byte-count instead
    /// of the raw byte sequence.
    /// </summary>
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append(
            string.Create(CultureInfo.InvariantCulture, $"Bytes = [{Bytes.Length} bytes]"));
        return true;
    }
}
