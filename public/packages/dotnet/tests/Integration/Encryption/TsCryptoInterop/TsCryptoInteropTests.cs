// -----------------------------------------------------------------------
// <copyright file="TsCryptoInteropTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Integration.Encryption.TsCryptoInterop;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using D2.Shared.Encryption;
using D2.Shared.Tests.Unit.Auth;
using Xunit;

/// <summary>
/// TS → .NET cross-runtime interop gate: opens/decrypts frames PRODUCED by the
/// TypeScript <c>@d2/encryption</c> emitters (committed under
/// <c>fixtures/</c>) with the REAL .NET <see cref="PayloadOpener"/> /
/// <see cref="PayloadCrypto"/>, proving the TS encoder is byte-compatible with
/// the .NET decoder. Paired with the .NET → TS
/// <c>sealed-crypto-kat</c>/<c>symmetric-crypto-kat</c> parity suites, this
/// closes the two-way crypto agreement. Regenerate the fixtures via
/// <c>pnpm --filter @d2/encryption emit-crypto-fixtures</c>.
/// </summary>
public sealed class TsCryptoInteropTests
{
    private static readonly JsonSerializerOptions sr_json = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    [Fact]
    [Trait("Category", "Unit")]
    public void Opens_TsProduced_SealedFrames()
    {
        var manifest = LoadManifest<SealedManifest>("sealed-frames.manifest.fixture.json");
        manifest.Frames.Should().NotBeEmpty();

        using var keyring = new RecipientPrivateKeyring(
            manifest.RecipientServiceId,
            new Dictionary<string, byte[]>
            {
                [manifest.RecipientKid] =
                    Convert.FromBase64String(manifest.RecipientPrivatePkcs8Base64),
            });
        var opener = new PayloadOpener(keyring);

        foreach (var frame in manifest.Frames)
        {
            var opened = opener.Open(Convert.FromHexString(frame.FrameHex));
            Encoding.UTF8.GetString(opened).Should().Be(frame.PlaintextUtf8);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Decrypts_TsProduced_SymmetricFrames()
    {
        var manifest = LoadManifest<SymmetricManifest>(
            "symmetric-frames.manifest.fixture.json");
        manifest.Frames.Should().NotBeEmpty();

        using var keyring = new PayloadCryptoKeyring(
            manifest.Kid,
            new Dictionary<string, byte[]>
            {
                [manifest.Kid] = Convert.FromBase64String(manifest.KeyBase64),
            },
            Convert.FromBase64String(manifest.AadContextBase64));
        var crypto = new PayloadCrypto(keyring);

        foreach (var frame in manifest.Frames)
        {
            var decrypted = crypto.Decrypt(Convert.FromHexString(frame.FrameHex));
            Encoding.UTF8.GetString(decrypted).Should().Be(frame.PlaintextUtf8);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void FixturesDirectory_ContainsOnly_TheKnownManifests()
    {
        // Manifest-drift closure: a stray or renamed fixture file is caught —
        // every committed fixture must be one of the two manifests the interop
        // tests above consume.
        var present = Directory
            .GetFiles(FixturesDir(), "*.json")
            .Select(Path.GetFileName)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        present.Should().Equal(
            "sealed-frames.manifest.fixture.json",
            "symmetric-frames.manifest.fixture.json");
    }

    private static string FixturesDir() => Path.Combine(
        TestPaths.RepoRoot(),
        "public",
        "packages",
        "dotnet",
        "tests",
        "Integration",
        "Encryption",
        "TsCryptoInterop",
        "fixtures");

    private static T LoadManifest<T>(string file)
    {
        var json = File.ReadAllText(Path.Combine(FixturesDir(), file));

        return JsonSerializer.Deserialize<T>(json, sr_json)
            ?? throw new InvalidOperationException($"Manifest {file} deserialized to null.");
    }

    private sealed record FrameEntry(string PlaintextUtf8, string FrameHex);

    private sealed record SealedManifest(
        string RecipientServiceId,
        string RecipientKid,
        string RecipientPrivatePkcs8Base64,
        FrameEntry[] Frames);

    private sealed record SymmetricManifest(
        string Kid,
        string KeyBase64,
        string AadContextBase64,
        FrameEntry[] Frames);
}
