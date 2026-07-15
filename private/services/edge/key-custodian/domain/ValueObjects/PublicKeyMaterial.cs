// -----------------------------------------------------------------------
// <copyright file="PublicKeyMaterial.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.Domain.ValueObjects;

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
/// <b>Value equality.</b> Two instances are equal when their byte sequences are
/// identical (<c>SequenceEqual</c>). Default record equality on
/// <c>ReadOnlyMemory&lt;byte&gt;</c> is reference-based (backing array + offset +
/// length), so the override is required for round-trip mapper tests and any
/// collection-membership check.
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

    /// <summary>
    /// Content-based equality: two instances are equal when their byte sequences
    /// are identical regardless of backing-array identity.
    /// </summary>
    /// <param name="other">The other instance to compare.</param>
    /// <returns>
    /// <see langword="true"/> if both instances wrap the same byte content;
    /// <see langword="false"/> otherwise.
    /// </returns>
    public bool Equals(PublicKeyMaterial? other)
    {
        if (other is null)
            return false;

        if (ReferenceEquals(this, other))
            return true;

        return Bytes.Span.SequenceEqual(other.Bytes.Span);
    }

    /// <summary>
    /// Content-based hash code consistent with <see cref="Equals(PublicKeyMaterial?)"/>.
    /// </summary>
    /// <returns>A hash code derived from the byte content.</returns>
    public override int GetHashCode()
    {
        var hash = default(HashCode);
        hash.AddBytes(Bytes.Span);
        return hash.ToHashCode();
    }

    /// <inheritdoc/>
    public override string ToString() =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"PublicKeyMaterial {{ Bytes = [{Bytes.Length} bytes] }}");

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
