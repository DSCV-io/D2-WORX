// -----------------------------------------------------------------------
// <copyright file="DlqFailureMetadataFixtureEmitter.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.ContractFixtures;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DcsvIo.D2.Messaging;
using DcsvIo.D2.Messaging.RabbitMq.Subscribing;
using Xunit;

/// <summary>
/// Emits the DLQ failure-metadata catalogs as two fixtures —
/// <c>fields.json</c> (property-name catalog) and <c>causes.json</c>
/// (closed-enum cause-string catalog).
/// </summary>
public sealed class DlqFailureMetadataFixtureEmitter
{
    private const string CATALOG = "dlq-failure-metadata";

    [Fact]
    [Trait("Category", "ContractFixtures")]
    public void Emit_Fields()
    {
        var data = EnumerateConstants(typeof(DlqFailureMetadataFields));
        FixturePathHelpers.WriteFixture(CATALOG, "fields", data);
    }

    [Fact]
    [Trait("Category", "ContractFixtures")]
    public void Emit_Causes()
    {
        var data = EnumerateConstants(typeof(DlqFailureCauses));
        FixturePathHelpers.WriteFixture(CATALOG, "causes", data);
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
