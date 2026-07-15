// -----------------------------------------------------------------------
// <copyright file="SealedKeyDerivation.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Encryption;

using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

/// <summary>
/// The frozen key-derivation and AEAD-binding conventions of the sealed
/// (ECDH-ES hybrid) encryption mode. Both <see cref="PayloadSealer"/> and
/// <see cref="PayloadOpener"/> derive through this single implementation so
/// producer and consumer can never disagree byte-for-byte.
/// </summary>
/// <remarks>
/// <para>
/// <strong>WIRE-PERMANENT — do not change.</strong> Every value below is
/// baked into the content-encryption-key derivation of every sealed frame
/// ever produced. Changing the label, the length-delimited info encoding,
/// the salt, or the AAD breaks decryption of everything sealed before the
/// change. The freeze is pinned by known-answer tests.
/// </para>
/// <para>
/// The conventions (all anchored on the RECIPIENT SERVICE, never the
/// message domain, so one opener per service covers every sealed domain
/// routed to it):
/// </para>
/// <list type="bullet">
/// <item>HKDF <c>info</c> = <c>"d2-seal-v1"</c> ‖ len16BE(serviceId) ‖
/// UTF-8(serviceId) ‖ len16BE(ephSPKI) ‖ ephSPKI — each variable component
/// prefixed by its 2-byte big-endian length, so component boundaries are
/// unambiguous by construction (no concatenation ambiguity between the
/// variable-length service id and the DER SPKI).</item>
/// <item>HKDF <c>salt</c> = UTF-8(serviceId) — non-secret; recipient
/// separation at zero cost.</item>
/// <item>AES-GCM <c>aad</c> = UTF-8(serviceId) — a frame sealed for one
/// service can never authenticate as sealed for another.</item>
/// </list>
/// </remarks>
internal static class SealedKeyDerivation
{
    /// <summary>
    /// The frozen domain-separation label leading the HKDF <c>info</c> input.
    /// </summary>
    internal const string INFO_LABEL = "d2-seal-v1";

    /// <summary>Derived content-encryption-key size in bytes (AES-256).</summary>
    internal const int DEK_SIZE_BYTES = 32;

    // Width of the big-endian length prefix delimiting each variable
    // component inside the HKDF info encoding. Deliberately its own frozen
    // constant (not a reference to the frame-layout spec constant): the info
    // encoding is wire-permanent independently of frame-layout evolution.
    private const int _INFO_LENGTH_PREFIX_SIZE = 2;

    /// <summary>
    /// Encodes the recipient service id to the exact bytes used as the HKDF
    /// salt AND the AES-GCM additional authenticated data.
    /// </summary>
    /// <param name="recipientServiceId">The validated recipient service id.</param>
    /// <returns>UTF-8 bytes of the service id.</returns>
    internal static byte[] ServiceIdBytes(string recipientServiceId)
        => Encoding.UTF8.GetBytes(recipientServiceId);

    /// <summary>
    /// Builds the frozen length-delimited HKDF <c>info</c> input:
    /// <c>"d2-seal-v1"</c> ‖ len16BE(serviceId) ‖ serviceId ‖
    /// len16BE(ephSPKI) ‖ ephSPKI.
    /// </summary>
    /// <param name="serviceIdBytes">UTF-8 bytes of the recipient service id.</param>
    /// <param name="ephemeralPublicSpki">The per-message ephemeral public key (SPKI DER).</param>
    /// <returns>The complete info byte string.</returns>
    internal static byte[] BuildInfo(
        ReadOnlySpan<byte> serviceIdBytes, ReadOnlySpan<byte> ephemeralPublicSpki)
    {
        var label = INFO_LABEL.Length; // ASCII label — 1 byte per char.
        var info = new byte[
            label
            + _INFO_LENGTH_PREFIX_SIZE + serviceIdBytes.Length
            + _INFO_LENGTH_PREFIX_SIZE + ephemeralPublicSpki.Length];
        var span = info.AsSpan();

        Encoding.UTF8.GetBytes(INFO_LABEL, span);
        var offset = label;

        BinaryPrimitives.WriteUInt16BigEndian(span[offset..], (ushort)serviceIdBytes.Length);
        offset += _INFO_LENGTH_PREFIX_SIZE;
        serviceIdBytes.CopyTo(span[offset..]);
        offset += serviceIdBytes.Length;

        BinaryPrimitives.WriteUInt16BigEndian(
            span[offset..], (ushort)ephemeralPublicSpki.Length);
        offset += _INFO_LENGTH_PREFIX_SIZE;
        ephemeralPublicSpki.CopyTo(span[offset..]);

        return info;
    }

    /// <summary>
    /// Derives the per-message AES-256-GCM content-encryption key from the
    /// raw ECDH shared secret via HKDF-SHA256 under the frozen salt + info
    /// conventions. The caller owns zeroizing both the shared secret and the
    /// derived key.
    /// </summary>
    /// <param name="sharedSecret">The raw ECDH agreement output.</param>
    /// <param name="serviceIdBytes">UTF-8 bytes of the recipient service id (the salt).</param>
    /// <param name="ephemeralPublicSpki">The per-message ephemeral public key (SPKI DER).</param>
    /// <param name="destination">
    /// Receives the derived key; must be exactly <see cref="DEK_SIZE_BYTES"/> long.
    /// </param>
    internal static void DeriveDek(
        ReadOnlySpan<byte> sharedSecret,
        ReadOnlySpan<byte> serviceIdBytes,
        ReadOnlySpan<byte> ephemeralPublicSpki,
        Span<byte> destination)
    {
        if (destination.Length != DEK_SIZE_BYTES)
        {
            throw new ArgumentException(
                $"destination must be exactly {DEK_SIZE_BYTES} bytes " +
                $"(got {destination.Length}).",
                nameof(destination));
        }

        var info = BuildInfo(serviceIdBytes, ephemeralPublicSpki);
        HKDF.DeriveKey(
            HashAlgorithmName.SHA256,
            ikm: sharedSecret,
            output: destination,
            salt: serviceIdBytes,
            info: info);
    }
}
