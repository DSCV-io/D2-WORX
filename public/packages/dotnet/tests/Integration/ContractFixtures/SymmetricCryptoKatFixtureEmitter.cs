// -----------------------------------------------------------------------
// <copyright file="SymmetricCryptoKatFixtureEmitter.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.ContractFixtures;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using DcsvIo.D2.Encryption;
using Xunit;

/// <summary>
/// Emits the SYMMETRIC (version-1) crypto known-answer fixture — the .NET → TS
/// cross-runtime gate for AES-256-GCM. Fixed key / kid / AAD context / nonce /
/// plaintext (all synthetic, fixture-only) produce a deterministic v1 frame via
/// the REAL production AEAD + codec, so the TS twin reproducing the frame proves
/// byte-for-byte agreement and opening it proves the full decrypt path.
/// </summary>
public sealed class SymmetricCryptoKatFixtureEmitter
{
    private const string CATALOG = "symmetric-crypto-kat";

    private const string _KAT_KID = "sym-kat-kid";
    private const string _KAT_PLAINTEXT = "d2-symmetric-known-answer";

    private static readonly byte[] sr_key =
        [.. Enumerable.Range(0, PayloadCryptoKeyring.KEY_SIZE_BYTES).Select(i => (byte)(i + 1))];

    private static readonly byte[] sr_aadContext = Encoding.UTF8.GetBytes("d2/audit-payload");

    private static readonly byte[] sr_nonce =
        [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11];

    [Fact]
    [Trait("Category", "ContractFixtures")]
    public void Emit_KnownAnswer()
    {
        var plaintext = Encoding.UTF8.GetBytes(_KAT_PLAINTEXT);
        var ctWithTag = new byte[plaintext.Length + EncryptionFrame.TAG_SIZE];

        using (var gcm = new AesGcm(sr_key, EncryptionFrame.TAG_SIZE))
        {
            gcm.Encrypt(
                sr_nonce,
                plaintext,
                ctWithTag.AsSpan(0, plaintext.Length),
                ctWithTag.AsSpan(plaintext.Length, EncryptionFrame.TAG_SIZE),
                sr_aadContext);
        }

        var frame = EncryptionFrame.Encode(_KAT_KID, sr_nonce, ctWithTag);

        var data = new SortedDictionary<string, object?>(StringComparer.Ordinal)
        {
            ["kid"] = _KAT_KID,
            ["plaintextUtf8"] = _KAT_PLAINTEXT,
            ["keyBase64"] = Convert.ToBase64String(sr_key),
            ["aadContextBase64"] = Convert.ToBase64String(sr_aadContext),
            ["nonceHex"] = Convert.ToHexString(sr_nonce),
            ["frameHex"] = Convert.ToHexString(frame),
        };

        FixturePathHelpers.WriteFixture(CATALOG, "known-answer", data);
    }
}
