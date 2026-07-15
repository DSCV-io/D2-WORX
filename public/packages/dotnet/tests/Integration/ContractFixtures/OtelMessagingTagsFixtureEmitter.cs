// -----------------------------------------------------------------------
// <copyright file="OtelMessagingTagsFixtureEmitter.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.ContractFixtures;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DcsvIo.D2.Messaging.RabbitMq;
using Xunit;

/// <summary>
/// Emits the OTel messaging activity-tags catalog fixture.
/// </summary>
public sealed class OtelMessagingTagsFixtureEmitter
{
    private const string CATALOG = "otel-messaging-tags";

    [Fact]
    [Trait("Category", "ContractFixtures")]
    public void Emit_Tags()
    {
        var data = EnumerateConstants(typeof(MessagingActivityTags));
        FixturePathHelpers.WriteFixture(CATALOG, "tags", data);
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
