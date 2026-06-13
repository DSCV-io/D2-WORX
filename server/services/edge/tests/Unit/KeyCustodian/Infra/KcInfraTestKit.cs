// -----------------------------------------------------------------------
// <copyright file="KcInfraTestKit.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.Infra;

using System.Security.Cryptography;
using D2.Shared.Encryption;
using Microsoft.Extensions.Configuration;

/// <summary>
/// Shared helpers for the KeyCustodian Infra unit tests: a throwaway root-key
/// directory (valid hex primary key) and an in-memory configuration carrying the
/// <c>KEYCUSTODIAN_APP__*</c> / <c>KEYCUSTODIAN_INFRA__*</c> sections. Tests
/// generate their OWN throwaway key material in a temp dir — never the deny-ruled
/// <c>secrets/</c> tree.
/// </summary>
internal static class KcInfraTestKit
{
    /// <summary>A non-connecting placeholder connection string for registration tests.</summary>
    public const string FAKE_CONNECTION_STRING =
        "Host=localhost;Port=1;Database=keycustodian_db;Username=u;Password=p";

    /// <summary>
    /// Creates a fresh temp directory containing a valid <c>root.key</c> (and
    /// optionally a valid <c>root-next.key</c>) with random 32-byte hex material.
    /// </summary>
    /// <param name="withSuccessor">Whether to also write a valid successor key.</param>
    /// <returns>The created directory path (caller deletes when done).</returns>
    public static string CreateRootKeyDir(bool withSuccessor = false)
    {
        var dir = Path.Combine(Path.GetTempPath(), "kc-itk-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        System.IO.File.WriteAllText(Path.Combine(dir, "root.key"), ValidHex());
        if (withSuccessor)
            System.IO.File.WriteAllText(Path.Combine(dir, "root-next.key"), ValidHex());

        return dir;
    }

    /// <summary>Builds an in-memory configuration with valid KC app + infra sections.</summary>
    /// <param name="rootKeyDir">The root-key directory to wire into the infra section.</param>
    /// <returns>The configuration root.</returns>
    public static IConfiguration BuildConfiguration(string rootKeyDir) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["KEYCUSTODIAN_APP:Default:Cadence"] = "30.00:00:00",
                ["KEYCUSTODIAN_APP:Default:Grace"] = "7.00:00:00",
                ["KEYCUSTODIAN_APP:Default:SmokeSoak"] = "01:00:00",
                ["KEYCUSTODIAN_INFRA:RootKeyPath"] = rootKeyDir,
                ["KEYCUSTODIAN_INFRA:RotationCheckInterval"] = "00:05:00",
                ["KEYCUSTODIAN_INFRA:DbCommandTimeoutSeconds"] = "30",
            })
            .Build();

    private static string ValidHex() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(PayloadCryptoKeyring.KEY_SIZE_BYTES));
}
