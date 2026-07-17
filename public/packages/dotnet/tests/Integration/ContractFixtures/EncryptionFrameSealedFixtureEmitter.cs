// -----------------------------------------------------------------------
// <copyright file="EncryptionFrameSealedFixtureEmitter.cs" company="DCSV">
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
/// Emits the SEALED encryption-frame binary-layout catalog fixture. Each
/// entry is the integer value of a public const int/byte field on
/// <see cref="SealedFrameLayout"/>. Sibling of
/// <see cref="EncryptionFrameFixtureEmitter"/> — a separate fixture file so
/// the version-1 fixture stays byte-identical.
/// </summary>
public sealed class EncryptionFrameSealedFixtureEmitter
{
    private const string CATALOG = "encryption-frame-sealed";

    [Fact]
    [Trait("Category", "ContractFixtures")]
    public void Emit_Layout()
    {
        var data = EnumerateConstants(typeof(SealedFrameLayout));
        FixturePathHelpers.WriteFixture(CATALOG, "layout", data);
    }

    private static SortedDictionary<string, object?> EnumerateConstants(Type type)
    {
        // Sealed-frame catalog uses int + byte constants — collect both.
        var fields = type
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(f => f.IsLiteral && (f.FieldType == typeof(int) || f.FieldType == typeof(byte)))
            .OrderBy(f => f.Name, StringComparer.Ordinal);
        var data = new SortedDictionary<string, object?>(StringComparer.Ordinal);
        foreach (var f in fields)
        {
            var raw = f.GetValue(null);
            data[f.Name] = raw is byte b ? (int)b : raw;
        }

        return data;
    }
}
