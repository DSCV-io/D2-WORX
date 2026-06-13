// -----------------------------------------------------------------------
// <copyright file="KeyMaterialEncrypted.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.Domain.ValueObjects;

/// <summary>
/// Value object wrapping the root-key-encrypted frame bytes of a managed key's
/// private or symmetric material.
/// </summary>
/// <remarks>
/// <b>Construction.</b> Use <see cref="FromTrusted"/> only — the App layer
/// constructs this after calling <c>IPayloadCrypto.Encrypt</c>. The domain never
/// touches raw plaintext key bytes.
///
/// <b>PII / <c>ToString</c> trap.</b> Records with <c>byte[]</c> / memory fields
/// auto-generate <c>ToString</c>/<c>PrintMembers</c> that would dump the bytes.
/// This class overrides both so encrypted material is never emitted in logs.
/// <see cref="Bytes"/> is additionally marked <c>[RedactData(SecretInformation)]</c>
/// for the Serilog destructuring layer.
///
/// <b>Value equality.</b> Two instances are equal when their byte sequences are
/// identical (<c>SequenceEqual</c>). Default record equality on
/// <c>ReadOnlyMemory&lt;byte&gt;</c> is reference-based (backing array + offset +
/// length), so the override is required for round-trip mapper tests and any
/// collection-membership check.
///
/// <b>Retention.</b> Material is retained through <c>RetiredKey</c> and
/// <c>CompromisedKey</c> — retired keys must still decrypt historical payloads
/// (overlap decryption); compromised key material is needed for forensics.
/// Erasure of key bytes is NOT a GDPR right-to-erasure concern here: these are
/// cryptographic key bytes, not subject PII.
/// </remarks>
public sealed record KeyMaterialEncrypted
{
    /// <summary>
    /// Gets the root-key-encrypted key-frame bytes.
    /// Never logged or printed — see class remarks.
    /// </summary>
    [RedactData(Reason = RedactReason.SecretInformation)]
    public required ReadOnlyMemory<byte> Bytes { get; init; }

    /// <summary>
    /// Reconstructs a <see cref="KeyMaterialEncrypted"/> from already-encrypted
    /// bytes produced by the App layer. For trusted sources only — no
    /// user-input validation is performed.
    /// </summary>
    /// <param name="bytes">Non-empty encrypted frame bytes.</param>
    /// <returns>A <see cref="KeyMaterialEncrypted"/> wrapping <paramref name="bytes"/>.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="bytes"/> is empty.
    /// — §5.1a bespoke-message carve-out: empty ciphertext is a programmer error,
    ///   not user-supplied invalid data.
    /// </exception>
    public static KeyMaterialEncrypted FromTrusted(ReadOnlyMemory<byte> bytes)
    {
        if (bytes.IsEmpty)
            throw new ArgumentException("Encrypted key material must not be empty.", nameof(bytes));

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
    public bool Equals(KeyMaterialEncrypted? other)
    {
        if (other is null)
            return false;

        if (ReferenceEquals(this, other))
            return true;

        return Bytes.Span.SequenceEqual(other.Bytes.Span);
    }

    /// <summary>
    /// Content-based hash code consistent with <see cref="Equals(KeyMaterialEncrypted?)"/>.
    /// </summary>
    /// <returns>A hash code derived from the byte content.</returns>
    public override int GetHashCode()
    {
        var hash = default(HashCode);
        hash.AddBytes(Bytes.Span);
        return hash.ToHashCode();
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        var len = Bytes.Length;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"KeyMaterialEncrypted {{ Bytes = [REDACTED:SecretInformation, {len} bytes] }}");
    }

    /// <summary>
    /// Overrides auto-generated <c>PrintMembers</c> to prevent raw bytes from
    /// appearing in record equality / debug output.
    /// </summary>
    private bool PrintMembers(StringBuilder builder)
    {
        var len = Bytes.Length;
        builder.Append(
            string.Create(
                CultureInfo.InvariantCulture,
                $"Bytes = [REDACTED:SecretInformation, {len} bytes]"));
        return true;
    }
}
