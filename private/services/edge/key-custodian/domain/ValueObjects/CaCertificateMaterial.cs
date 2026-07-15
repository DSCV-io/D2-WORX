// -----------------------------------------------------------------------
// <copyright file="CaCertificateMaterial.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.KeyCustodian.Domain.ValueObjects;

/// <summary>
/// Value object wrapping the DER-encoded X.509 certificate of a
/// certificate-authority (<c>X509CaCertificate</c>) key.
/// </summary>
/// <remarks>
/// <b>Not PII.</b> A certificate is not secret — it is presented on the wire in
/// the TLS handshake and pinned as a trust anchor. <see cref="Bytes"/> is
/// intentionally NOT marked <c>[RedactData]</c> so it can be logged and used
/// freely, exactly like the JWKS-signing key's public material.
///
/// <b>Distinct from <c>PublicKeyMaterial</c>.</b> This is a full X.509
/// certificate (the DER encoding of the whole cert), feeding the TLS chain — not
/// the bare SPKI public key that <c>PublicKeyMaterial</c> carries to feed the
/// JWKS endpoint. The two artifacts are kept in separate slots so the
/// JWKS-projection invariant stays tied to <c>PublicKeyMaterial</c> alone.
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
/// <b>Presence invariant.</b> Only <c>X509CaCertificate</c> keys carry CA
/// certificate material. All other key types MUST NOT have a
/// <c>CaCertificateMaterial</c> — the base
/// <c>EncryptionKey.EnsureMaterialShape</c> invariant check enforces this at
/// construction time.
/// </remarks>
public sealed record CaCertificateMaterial
{
    /// <summary>Gets the raw DER-encoded X.509 certificate bytes.</summary>
    public required ReadOnlyMemory<byte> Bytes { get; init; }

    /// <summary>
    /// Reconstructs a <see cref="CaCertificateMaterial"/> from trusted certificate
    /// bytes produced by the App layer.
    /// </summary>
    /// <param name="bytes">Non-empty DER-encoded certificate bytes.</param>
    /// <returns>A <see cref="CaCertificateMaterial"/> wrapping <paramref name="bytes"/>.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="bytes"/> is empty.
    /// — §5.1a bespoke-message carve-out: empty certificate material is a programmer error.
    /// </exception>
    public static CaCertificateMaterial FromTrusted(ReadOnlyMemory<byte> bytes)
    {
        if (bytes.IsEmpty)
            throw new ArgumentException("CA certificate material must not be empty.", nameof(bytes));

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
    public bool Equals(CaCertificateMaterial? other)
    {
        if (other is null)
            return false;

        if (ReferenceEquals(this, other))
            return true;

        return Bytes.Span.SequenceEqual(other.Bytes.Span);
    }

    /// <summary>
    /// Content-based hash code consistent with <see cref="Equals(CaCertificateMaterial?)"/>.
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
            $"CaCertificateMaterial {{ Bytes = [{Bytes.Length} bytes] }}");

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
