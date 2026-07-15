// -----------------------------------------------------------------------
// <copyright file="FileRootKeyProvider.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.KeyCustodian.Infra.Vault.File;

using System.IO;
using System.Security.Cryptography;
using DcsvIo.D2.Private.Edge.KeyCustodian.Infra.Configuration;
using DcsvIo.D2.Private.Edge.KeyCustodian.Infra.Observability;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// File-backed <see cref="IRootKeyProvider"/> that builds the KeyCustodian root
/// keyring from a root-key directory: a REQUIRED primary key
/// (<c>root.key</c>) and an OPTIONAL successor key (<c>root-next.key</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>Multi-key from day one.</b> Both files (when present) hex-decode to exactly
/// <see cref="PayloadCryptoKeyring.KEY_SIZE_BYTES"/> bytes and load into ONE
/// <see cref="PayloadCryptoKeyring"/> — the primary under kid
/// <see cref="RootKeyKids.PRIMARY_KID"/> as <c>ActiveKid</c> (all new wraps use
/// it) and the successor under <see cref="RootKeyKids.NEXT_KID"/> as a
/// decrypt-only kid. This makes zero-downtime root rotation available without a
/// later storage migration.
/// </para>
/// <para>
/// <b>Fail-fast.</b> A missing or corrupt PRIMARY fails host boot. A present-but-
/// corrupt SUCCESSOR also fails boot (a bad successor is an operator error mid-
/// rotation, not "treat as absent" — silently dropping it would mask a
/// misconfigured rotation). An ABSENT successor is normal steady state → a
/// single-kid keyring.
/// </para>
/// <para>
/// <b>Key-material safety.</b> Key bytes are NEVER logged. The provider logs only
/// the directory, successor present/absent, and a decoded byte length on a
/// length-mismatch failure (never content). The keyring is built once and cached;
/// the registered <c>EncryptionStartupCheck</c> round-trip is the runtime proof it
/// actually unwraps.
/// </para>
/// </remarks>
public sealed class FileRootKeyProvider : IRootKeyProvider
{
    // A 32-byte key is 64 hex chars. Cap at 256 to reject clearly-invalid oversized
    // files before stackalloc and to keep the stackalloc bounded.
    private const int _HEX_CAP = 256;

    private readonly string r_rootKeyDirectory;
    private readonly ILogger<FileRootKeyProvider> r_logger;
    private readonly Lock r_gate = new();
    private PayloadCryptoKeyring? _keyring;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileRootKeyProvider"/> class.
    /// </summary>
    /// <param name="options">The infra options carrying the root-key directory.</param>
    /// <param name="logger">Logger for path + present/absent + length-on-failure events.</param>
    public FileRootKeyProvider(
        IOptions<KeyCustodianInfraOptions> options,
        ILogger<FileRootKeyProvider> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        r_rootKeyDirectory = options.Value.RootKeyPath;
        r_logger = logger;
    }

    /// <inheritdoc/>
    public PayloadCryptoKeyring GetRootKeyring()
    {
        // Double-checked locking: one disk read, cached for the process lifetime.
        var existing = Volatile.Read(ref _keyring);
        if (existing is not null)
            return existing;

        lock (r_gate)
        {
            _keyring ??= BuildKeyring();
            return _keyring;
        }
    }

    private static ReadOnlySpan<byte> TrimAsciiWhitespace(byte[] bytes)
    {
        var span = bytes.AsSpan();
        var start = 0;
        while (start < span.Length && IsAsciiWhitespace(span[start]))
            start++;

        var end = span.Length - 1;
        while (end >= start && IsAsciiWhitespace(span[end]))
            end--;

        return span[start..(end + 1)];
    }

    private static bool IsAsciiWhitespace(byte b) =>
        b is 0x09 or 0x0A or 0x0D or 0x20;

    private PayloadCryptoKeyring BuildKeyring()
    {
        var primaryPath = Path.Combine(r_rootKeyDirectory, RootKeyKids.PRIMARY_FILE_NAME);
        var nextPath = Path.Combine(r_rootKeyDirectory, RootKeyKids.NEXT_FILE_NAME);

        var keys = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            [RootKeyKids.PRIMARY_KID] = ReadKeyFile(primaryPath, RootKeyKids.PRIMARY_KID),
        };

        if (System.IO.File.Exists(nextPath))
        {
            KeyCustodianInfraLog.RootSuccessorKeyPresent(r_logger, r_rootKeyDirectory);
            keys[RootKeyKids.NEXT_KID] = ReadKeyFile(nextPath, RootKeyKids.NEXT_KID);
        }
        else
        {
            KeyCustodianInfraLog.RootSuccessorKeyAbsent(r_logger, r_rootKeyDirectory);
        }

        // AAD domain-binding = the UTF-8 of the root service key (non-empty,
        // identical to the keyed-crypto binding the handlers wrap with).
        var aad = Encoding.UTF8.GetBytes(KeyCustodianRootKey.ROOT_SERVICE_KEY);

        return new PayloadCryptoKeyring(RootKeyKids.PRIMARY_KID, keys, aad);
    }

    /// <summary>
    /// Reads a hex-encoded key file without materializing the hex text as a managed
    /// <see cref="string"/>. The raw file bytes are read, leading/trailing ASCII
    /// whitespace bytes are trimmed, the ASCII hex chars are copied into a
    /// <c>stackalloc</c> <see cref="Span{T}"/>, and
    /// <see cref="Convert.FromHexString(ReadOnlySpan{char})"/> validates and decodes
    /// to the key <c>byte[]</c>. Both intermediate buffers are zeroed in a
    /// <c>finally</c> block; only the returned key bytes remain. Fail-fast on any
    /// error — a present root-key file MUST be valid.
    /// </summary>
    /// <remarks>
    /// A 32-byte key is 64 hex chars. The method rejects any trimmed content longer
    /// than <see cref="_HEX_CAP"/> bytes; this caps the stackalloc and also catches
    /// files that are clearly not a valid key before the hex-decode attempt.
    /// <see cref="Convert.FromHexString(ReadOnlySpan{char})"/> throws
    /// <see cref="FormatException"/> on non-hex chars and odd-length input; both are
    /// caught and re-thrown as <see cref="InvalidOperationException"/> without
    /// surfacing any hex snippet.
    /// Key bytes are NEVER logged.
    /// </remarks>
    private byte[] ReadKeyFile(string path, string kid)
    {
        if (!System.IO.File.Exists(path))
        {
            KeyCustodianInfraLog.RootKeyFileMissing(r_logger, kid, path);
            throw new InvalidOperationException(
                $"KeyCustodian root key file for kid '{kid}' not found at '{path}'. "
                + "The host cannot start without the primary root key.");
        }

        var fileBytes = System.IO.File.ReadAllBytes(path);
        try
        {
            // Trim leading/trailing ASCII whitespace bytes (space, tab, LF, CR).
            var hexSpan = TrimAsciiWhitespace(fileBytes);

            if (hexSpan.IsEmpty)
            {
                KeyCustodianInfraLog.RootKeyFileEmpty(r_logger, kid, path);
                throw new InvalidOperationException(
                    $"KeyCustodian root key file for kid '{kid}' at '{path}' is empty.");
            }

            // Reject oversized content before stackalloc (valid key = 64 hex chars).
            if (hexSpan.Length > _HEX_CAP)
            {
                KeyCustodianInfraLog.RootKeyFileNotHex(r_logger, kid, path);
                throw new InvalidOperationException(
                    $"KeyCustodian root key file for kid '{kid}' at '{path}' is not valid hex.");
            }

            // Copy ASCII hex bytes to a stack-allocated char buffer — no managed string.
            Span<char> charSpan = stackalloc char[hexSpan.Length];
            try
            {
                for (var i = 0; i < hexSpan.Length; i++)
                    charSpan[i] = (char)hexSpan[i];

                byte[] decoded;
                try
                {
                    decoded = Convert.FromHexString(charSpan);
                }
                catch (FormatException)
                {
                    KeyCustodianInfraLog.RootKeyFileNotHex(r_logger, kid, path);
                    throw new InvalidOperationException(
                        $"KeyCustodian root key file for kid '{kid}' at '{path}' "
                        + "is not valid hex.");
                }

                if (decoded.Length != PayloadCryptoKeyring.KEY_SIZE_BYTES)
                {
                    KeyCustodianInfraLog.RootKeyFileWrongLength(
                        r_logger, kid, path, decoded.Length, PayloadCryptoKeyring.KEY_SIZE_BYTES);
                    throw new InvalidOperationException(
                        $"KeyCustodian root key file for kid '{kid}' at '{path}' decoded to "
                        + $"{decoded.Length} bytes; expected exactly "
                        + $"{PayloadCryptoKeyring.KEY_SIZE_BYTES}.");
                }

                return decoded;
            }
            finally
            {
                // Zero the char buffer before the stack frame unwinds.
                charSpan.Clear();
            }
        }
        finally
        {
            // Zero the raw file bytes regardless of success or failure path.
            CryptographicOperations.ZeroMemory(fileBytes);
        }
    }
}
