// -----------------------------------------------------------------------
// <copyright file="KeyMaterialEncrypted.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.Domain.ValueObjects;

using System.Globalization;
using System.Text;
using D2.Shared.Utilities.Attributes;
using D2.Shared.Utilities.Enums;

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

    /// <inheritdoc/>
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"KeyMaterialEncrypted {{ Bytes = [REDACTED:SecretInformation, {Bytes.Length} bytes] }}");

    /// <summary>
    /// Overrides auto-generated <c>PrintMembers</c> to prevent raw bytes from
    /// appearing in record equality / debug output.
    /// </summary>
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append(
            string.Create(CultureInfo.InvariantCulture, $"Bytes = [REDACTED:SecretInformation, {Bytes.Length} bytes]"));
        return true;
    }
}
