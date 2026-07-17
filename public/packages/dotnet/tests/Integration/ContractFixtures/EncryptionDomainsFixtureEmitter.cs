// -----------------------------------------------------------------------
// <copyright file="EncryptionDomainsFixtureEmitter.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.ContractFixtures;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DcsvIo.D2.Encryption;
using Xunit;

/// <summary>
/// Emits the encryption-domains catalog fixture.
/// </summary>
public sealed class EncryptionDomainsFixtureEmitter
{
    private const string CATALOG = "encryption-domains";

    [Fact]
    [Trait("Category", "ContractFixtures")]
    public void Emit_Domains()
    {
        var data = EnumerateConstants(typeof(EncryptionDomains));
        FixturePathHelpers.WriteFixture(CATALOG, "domains", data);
    }

    [Fact]
    [Trait("Category", "ContractFixtures")]
    public void Emit_Modes()
    {
        // Per-domain mode + consumerService, keyed by wire value, mirroring
        // the emitted EncryptionDomainModes as-const twin in
        // @dcsv-io/d2-encryption-abstractions. Only sealed domains carry a
        // consumerService.
        var data = new SortedDictionary<string, object?>(StringComparer.Ordinal);
        foreach (var domain in EncryptionDomains.AllDomains)
        {
            var entry = new SortedDictionary<string, object?>(StringComparer.Ordinal)
            {
                ["mode"] =
                    EncryptionDomainModes.ModeFor(domain) == EncryptionDomainMode.Sealed
                        ? "sealed"
                        : "symmetric",
            };
            if (EncryptionDomainModes.TryGetConsumerService(domain, out var consumer))
                entry["consumerService"] = consumer;

            data[domain] = entry;
        }

        FixturePathHelpers.WriteFixture(CATALOG, "modes", data);
    }

    private static SortedDictionary<string, object?> EnumerateConstants(Type type)
    {
        var fields = type
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .OrderBy(f => f.Name, StringComparer.Ordinal);
        var data = new SortedDictionary<string, object?>(StringComparer.Ordinal);
        foreach (var f in fields)
            data[f.Name] = (string)f.GetValue(null)!;

        return data;
    }
}
