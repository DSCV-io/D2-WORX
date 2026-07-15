// -----------------------------------------------------------------------
// <copyright file="TestKeyrings.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Encryption;

using System.Security.Cryptography;
using System.Text;
using DcsvIo.D2.Encryption;

/// <summary>
/// Test helpers for building keyrings with deterministic but realistic
/// shapes. Keys are crypto-random so tests exercise the real AesGcm path.
/// </summary>
internal static class TestKeyrings
{
    internal static byte[] RandomKey()
        => RandomNumberGenerator.GetBytes(PayloadCryptoKeyring.KEY_SIZE_BYTES);

    internal static ReadOnlyMemory<byte> AadFor(string label) => Encoding.UTF8.GetBytes(label);

    internal static PayloadCryptoKeyring SingleKey(string kid, string aad)
        => new(kid, new Dictionary<string, byte[]> { [kid] = RandomKey() }, AadFor(aad));

    internal static PayloadCryptoKeyring AuditSingleKey()
        => SingleKey("audit-2026q2", "audit");

    internal static PayloadCryptoKeyring AuditTwoKeys()
        => new(
            "audit-2026q2",
            new Dictionary<string, byte[]>
            {
                ["audit-2026q2"] = RandomKey(),
                ["audit-2026q1"] = RandomKey(),
            },
            AadFor("audit"));
}
