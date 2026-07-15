// -----------------------------------------------------------------------
// <copyright file="InputErrorFixtureEmitter.cs" company="DCSV">
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
using DcsvIo.D2.Result;
using Xunit;

/// <summary>
/// Emits one fixture per InputError wire-shape scenario reflected off
/// the .NET codegen-emitted static class <see cref="InputErrorWireShape"/>.
/// Two fixture families:
/// <list type="bullet">
///   <item>
///     <c>shape</c> — the property-name catalog (FIELD / ERRORS → wire
///     values). TS-side parity test asserts byte-equality against the
///     codegen-emitted TS catalog.
///   </item>
///   <item>
///     <c>round-trip-*</c> — round-trip JSON fixtures produced by
///     serializing real <see cref="InputError"/> instances via
///     <see cref="System.Text.Json"/>. The TS-side parity test reads
///     the JSON bytes, parses them, and asserts the resulting shape
///     matches the spec — closes the cross-language wire-shape loop
///     end-to-end (serializer ↔ parser) for the nested TKMessage[]
///     errors collection.
///   </item>
/// </list>
/// </summary>
public sealed class InputErrorFixtureEmitter
{
    private const string CATALOG = "input-error";

    [Fact]
    [Trait("Category", "ContractFixtures")]
    public void Emit_Shape()
    {
        var data = EnumerateConstants(typeof(InputErrorWireShape));
        FixturePathHelpers.WriteFixture(CATALOG, "shape", data);
    }

    [Fact]
    [Trait("Category", "ContractFixtures")]
    public void Emit_RoundTripSingleError()
    {
        // Round-trip an InputError with one TKMessage describing what's
        // wrong with the field. Captures the canonical
        // `{field, errors: [{key}]}` shape.
        var ie = new InputError(
            "email",
            [new TKMessage("common_validation_EMAIL_INVALID")]);
        WriteRoundTripFixture("round-trip-single-error", ie);
    }

    [Fact]
    [Trait("Category", "ContractFixtures")]
    public void Emit_RoundTripMultipleErrors()
    {
        // Round-trip an InputError with multiple TKMessages — multi-error
        // arrays are the common case for fields that fail multiple
        // validation predicates (required + format violation).
        var ie = new InputError(
            "password",
            [
                new TKMessage("common_validation_PASSWORD_REQUIRED"),
                new TKMessage(
                    "auth_validation_PASSWORD_TOO_SHORT",
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["minLength"] = "12",
                    }),
            ]);
        WriteRoundTripFixture("round-trip-multiple-errors", ie);
    }

    [Fact]
    [Trait("Category", "ContractFixtures")]
    public void Emit_RoundTripDotNotationField()
    {
        // Round-trip an InputError with a dot-notation field name —
        // mirrors deeply-nested form field paths
        // (e.g. `address.city`, `contacts[0].email`).
        var ie = new InputError(
            "address.city",
            [new TKMessage("common_validation_FIELD_REQUIRED")]);
        WriteRoundTripFixture("round-trip-dot-notation-field", ie);
    }

    private static void WriteRoundTripFixture(string scenario, InputError inputError)
    {
        var json = JsonSerializer.Serialize(inputError);
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
    /// <c>FIELD</c>) so the fixture mirrors the TS-side const-map shape
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
