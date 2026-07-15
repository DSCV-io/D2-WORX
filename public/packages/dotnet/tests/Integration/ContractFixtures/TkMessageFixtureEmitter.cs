// -----------------------------------------------------------------------
// <copyright file="TkMessageFixtureEmitter.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.ContractFixtures;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using DcsvIo.D2.I18n;
using Xunit;

/// <summary>
/// Emits one fixture per TKMessage wire-shape scenario reflected off the
/// .NET codegen-emitted static class <see cref="TkMessageWireShape"/>.
/// Two fixture families:
/// <list type="bullet">
///   <item>
///     <c>shape</c> — the property-name catalog (KEY / PARAMS → wire
///     values). TS-side parity test asserts byte-equality against the
///     codegen-emitted TS catalog.
///   </item>
///   <item>
///     <c>round-trip-*</c> — round-trip JSON fixtures produced by
///     serializing real <see cref="TKMessage"/> instances via
///     <see cref="TKMessageJsonConverter"/>. The TS-side parity test
///     reads the JSON bytes, parses them, and asserts the resulting
///     shape matches the spec — closes the cross-language wire-shape
///     loop end-to-end (serializer ↔ parser).
///   </item>
/// </list>
/// </summary>
public sealed class TkMessageFixtureEmitter
{
    private const string CATALOG = "tk-message";

    [Fact]
    [Trait("Category", "ContractFixtures")]
    public void Emit_Shape()
    {
        var data = EnumerateConstants(typeof(TkMessageWireShape));
        FixturePathHelpers.WriteFixture(CATALOG, "shape", data);
    }

    [Fact]
    [Trait("Category", "ContractFixtures")]
    public void Emit_RoundTripNoParams()
    {
        // Round-trip a no-params TKMessage via the .NET serializer.
        // The fixture captures the exact JSON bytes the TS-side parser
        // must accept and the same bytes the TS-side serializer must
        // produce. Byte-equal on both sides → wire parity proven.
        var message = new TKMessage("common_errors_NOT_FOUND");
        WriteRoundTripFixture("round-trip-no-params", message);
    }

    [Fact]
    [Trait("Category", "ContractFixtures")]
    public void Emit_RoundTripWithParams()
    {
        // Round-trip a TKMessage carrying a single parameter binding.
        // String values exclusively per the spec
        // (params: Record<string, string>).
        var message = new TKMessage(
            "common_errors_LIMIT_EXCEEDED",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["maxLength"] = "256",
            });
        WriteRoundTripFixture("round-trip-with-params", message);
    }

    [Fact]
    [Trait("Category", "ContractFixtures")]
    public void Emit_RoundTripMultipleParams()
    {
        // Round-trip a TKMessage with multiple parameters, exercising
        // dictionary serialization + ordering tolerance on the TS side.
        var message = new TKMessage(
            "auth_errors_PASSWORD_WEAK",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["minLength"] = "12",
                ["maxLength"] = "128",
            });
        WriteRoundTripFixture("round-trip-with-multiple-params", message);
    }

    private static void WriteRoundTripFixture(string scenario, TKMessage message)
    {
        // Serialize via the converter that ships in production.
        var json = JsonSerializer.Serialize(message);

        // Parse back to a JsonElement so the fixture's "data" payload is a
        // structured value (not a stringified JSON-in-JSON nest).
        using var doc = JsonDocument.Parse(json);
        var data = JsonElementToObject(doc.RootElement);
        FixturePathHelpers.WriteFixture(CATALOG, scenario, data!);
    }

    private static object? JsonElementToObject(JsonElement el)
    {
        return el.ValueKind switch
        {
            JsonValueKind.Object => JsonObjectToDict(el),
            JsonValueKind.Array => JsonArrayToList(el),
            JsonValueKind.String => el.GetString(),
            JsonValueKind.Number => el.TryGetInt64(out var i) ? (object)i : el.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null,
        };
    }

    private static Dictionary<string, object?> JsonObjectToDict(JsonElement obj)
    {
        var d = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var p in obj.EnumerateObject())
            d[p.Name] = JsonElementToObject(p.Value);

        return d;
    }

    private static List<object?> JsonArrayToList(JsonElement arr)
    {
        var l = new List<object?>();
        foreach (var item in arr.EnumerateArray()) l.Add(JsonElementToObject(item));

        return l;
    }

    /// <summary>
    /// Reflect every <c>public const string</c> on the wire-shape catalog
    /// type; produce a sorted map keyed by the constant name (e.g.
    /// <c>KEY</c>) so the fixture mirrors the TS-side const-map shape
    /// one-to-one.
    /// </summary>
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
