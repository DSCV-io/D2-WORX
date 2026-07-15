// -----------------------------------------------------------------------
// <copyright file="PayloadCryptoKeyring.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Encryption;

using System.Security.Cryptography;
using System.Text;

/// <summary>
/// Immutable JWKS-style keyring holding the active key plus any retiring
/// keys for a single encryption purpose (one domain, the root key, etc.).
/// Holds raw key bytes — never log, serialize, or otherwise expose a
/// keyring instance through any I/O path.
/// </summary>
/// <remarks>
/// Constructor copies all key bytes into private buffers so callers may
/// dispose their own copies immediately. <see cref="Dispose"/> zeroes the
/// internal buffers via <see cref="CryptographicOperations.ZeroMemory"/>;
/// any <see cref="PayloadCrypto"/> still referencing this keyring at
/// dispose time will throw <see cref="ObjectDisposedException"/> on its
/// next call.
/// </remarks>
public sealed class PayloadCryptoKeyring : IDisposable
{
    /// <summary>Required key length in bytes (256-bit AES key).</summary>
    public const int KEY_SIZE_BYTES = 32;

    /// <summary>Minimum kid length (must be at least one character).</summary>
    public const int MIN_KID_LENGTH = 1;

    /// <summary>
    /// Maximum kid length in UTF-8 bytes. Mirrors
    /// <see cref="EncryptionFrameLayout.CONSTRAINT_MAX_KID_LENGTH"/> — the spec
    /// is the source of truth; this constant delegates to keep them in sync.
    /// </summary>
    public const int MAX_KID_LENGTH = EncryptionFrameLayout.CONSTRAINT_MAX_KID_LENGTH;

    private readonly Dictionary<string, byte[]> r_keys;
    private readonly byte[] r_aadContext;
    private bool _disposed;

    /// <summary>
    /// Initializes a new keyring.
    /// </summary>
    /// <param name="activeKid">
    /// The kid used for new encryptions. Must be present in <paramref name="keys"/>.
    /// </param>
    /// <param name="keys">
    /// All kids the keyring can decrypt. Each value must be exactly <see cref="KEY_SIZE_BYTES"/>.
    /// </param>
    /// <param name="aadContext">
    /// AEAD additional-authenticated-data bound to every (en|de)crypt operation.
    /// Must be non-empty so the binding is meaningful — the caller (KeyCustodian,
    /// Messaging bus, etc.) decides what bytes carry domain semantics.
    /// </param>
    /// <exception cref="ArgumentException">
    /// A constructor argument violates a stated invariant.
    /// </exception>
    public PayloadCryptoKeyring(
        string activeKid, IReadOnlyDictionary<string, byte[]> keys, ReadOnlyMemory<byte> aadContext)
    {
        ArgumentNullException.ThrowIfNull(activeKid);
        ArgumentNullException.ThrowIfNull(keys);

        var activeKidUtf8Length = Encoding.UTF8.GetByteCount(activeKid);
        if (activeKidUtf8Length is < MIN_KID_LENGTH or > MAX_KID_LENGTH)
        {
            throw new ArgumentException(
                $"activeKid UTF-8 byte length must be in [{MIN_KID_LENGTH}, {MAX_KID_LENGTH}].",
                nameof(activeKid));
        }

        if (aadContext.IsEmpty)
        {
            throw new ArgumentException(
                "aadContext must be non-empty so AEAD binding is meaningful.", nameof(aadContext));
        }

        if (!keys.ContainsKey(activeKid))
        {
            throw new ArgumentException(
                $"activeKid '{activeKid}' is not present in keys.", nameof(activeKid));
        }

        r_keys = new Dictionary<string, byte[]>(keys.Count, StringComparer.Ordinal);
        foreach (var (kid, key) in keys)
        {
            ArgumentNullException.ThrowIfNull(kid);
            ArgumentNullException.ThrowIfNull(key);

            var kidUtf8Length = Encoding.UTF8.GetByteCount(kid);
            if (kidUtf8Length is < MIN_KID_LENGTH or > MAX_KID_LENGTH)
            {
                throw new ArgumentException(
                    $"kid '{kid}' UTF-8 byte length must be in [{MIN_KID_LENGTH}, {MAX_KID_LENGTH}].",
                    nameof(keys));
            }

            if (key.Length != KEY_SIZE_BYTES)
            {
                throw new ArgumentException(
                    $"key for kid '{kid}' must be exactly {KEY_SIZE_BYTES} bytes " +
                    $"(got {key.Length}).",
                    nameof(keys));
            }

            // Defensive copy — caller may dispose / mutate their original.
            var copy = new byte[KEY_SIZE_BYTES];
            key.CopyTo(copy, 0);
            r_keys[kid] = copy;
        }

        r_aadContext = aadContext.ToArray();
        ActiveKid = activeKid;
    }

    /// <summary>
    /// Gets the kid used for new encryptions. Always present in the keyring.
    /// </summary>
    public string ActiveKid { get; }

    /// <summary>
    /// Gets the AEAD additional-authenticated-data bound to every operation.
    /// Caller decides what bytes carry domain semantics; this lib treats the
    /// value as opaque.
    /// </summary>
    public ReadOnlyMemory<byte> AadContext
    {
        get
        {
            ThrowIfDisposed();
            return r_aadContext;
        }
    }

    /// <summary>
    /// Gets every kid in the keyring (active + retiring). Diagnostic only —
    /// never log alongside data that could correlate with key material.
    /// </summary>
    public IReadOnlyCollection<string> AllKids
    {
        get
        {
            ThrowIfDisposed();
            return r_keys.Keys;
        }
    }

    /// <summary>
    /// Resolves a kid to its key bytes.
    /// </summary>
    /// <param name="kid">
    /// The kid to look up. Null returns false
    /// (defensive — callers may pass externally-sourced strings).
    /// </param>
    /// <param name="key">The key bytes when found; default otherwise.</param>
    /// <returns>True when the kid is present; false otherwise.</returns>
    public bool TryGetKey(string? kid, out ReadOnlyMemory<byte> key)
    {
        ThrowIfDisposed();
        if (kid is not null && r_keys.TryGetValue(kid, out var bytes))
        {
            key = bytes;
            return true;
        }

        key = default;
        return false;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        foreach (var key in r_keys.Values)
            CryptographicOperations.ZeroMemory(key);
        CryptographicOperations.ZeroMemory(r_aadContext);
        r_keys.Clear();
        _disposed = true;
    }

    /// <summary>
    /// Returns a redacted string — never includes key bytes or AAD bytes.
    /// </summary>
    public override string ToString()
        => _disposed
            ? "PayloadCryptoKeyring(disposed)"
            : $"PayloadCryptoKeyring(ActiveKid={ActiveKid}, Kids={r_keys.Count})";

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
