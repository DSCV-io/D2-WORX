// -----------------------------------------------------------------------
// <copyright file="EncryptionDomainsFixtureEmitter.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Integration.ContractFixtures;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using D2.Shared.Encryption;
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
