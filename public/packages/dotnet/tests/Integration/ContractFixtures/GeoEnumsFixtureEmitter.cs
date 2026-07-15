// -----------------------------------------------------------------------
// <copyright file="GeoEnumsFixtureEmitter.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.ContractFixtures;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;
using DcsvIo.D2.Geo.Abstractions;
using DcsvIo.D2.Utilities.Extensions;
using Xunit;

/// <summary>
/// Emits the parity fixture for the codegen-emitted geo enums reflected
/// off the <c>DcsvIo.D2.Geo.Abstractions</c> assembly. Each enum gets one
/// sub-map keyed by enum-member-name; the value is the enum's wire form
/// (the integer backing for value-typed enums such as
/// <see cref="GeopoliticalEntityType"/> whose TS counterpart uses the
/// integer-valued const-object form, OR the member name itself for the
/// string-wire enums such as <see cref="WritingDirection"/>,
/// <see cref="DateFormatPattern"/>, <see cref="MeasurementSystem"/>,
/// <see cref="CurrencyAcceptanceLevel"/>, <see cref="GeoDayOfWeek"/>,
/// <see cref="Country"/>, <see cref="Currency"/>, <see cref="Language"/>,
/// <see cref="GeopoliticalEntity"/> — all serialize via
/// <c>JsonStringEnumConverter</c> so the wire form is the member name).
/// The TS-side parity test loads <c>fixtures/geo/enums.json</c> and asserts
/// per-VALUE byte-equivalence against the TS const-object shapes.
/// </summary>
/// <remarks>
/// Only enums BOTH sides expose are covered — same symmetry rule as the
/// auth-scopes / auth-error-codes parity emitters. Helper methods on the
/// .NET enum side (e.g. extension methods, lookup tables) that have no TS
/// counterpart are NOT fixtured.
/// </remarks>
public sealed class GeoEnumsFixtureEmitter
{
    private const string _CATALOG = "geo";

    [Fact]
    [Trait("Category", "ContractFixtures")]
    public void Emit_Enums()
    {
        var data = new SortedDictionary<string, object?>(StringComparer.Ordinal)
        {
            // ISO-coded enums (wire = string-name via JsonStringEnumConverter;
            // TS uses string-valued const-objects of identical name shape).
            // Closed-set enum types carry the *Code suffix (CountryCode /
            // CurrencyCode / LanguageCode / GeopoliticalEntityCode); the bare
            // singular name denotes the record shape. Fixture keys are kept on
            // the bare names because that's the cross-language identifier the
            // TS const-object uses.
            ["Country"] = EnumerateStringWireEnum(typeof(CountryCode)),
            ["Currency"] = EnumerateStringWireEnum(typeof(CurrencyCode)),
            ["Language"] = EnumerateStringWireEnum(typeof(LanguageCode)),
            ["GeopoliticalEntity"] = EnumerateStringWireEnum(typeof(GeopoliticalEntityCode)),

            // Fixed-vocabulary enum with integer-valued TS const-object
            // counterpart — the integer backing IS the wire form on the
            // TS side, so we pin the per-member integer.
            ["GeopoliticalEntityType"] = EnumerateIntegerWireEnum(
                typeof(GeopoliticalEntityType)),

            // Fixed-vocabulary string-wire enums (JsonStringEnumConverter on
            // the .NET side; string-valued TS const-objects on the TS side).
            ["WritingDirection"] = EnumerateStringWireEnum(typeof(WritingDirection)),
            ["DateFormatPattern"] = EnumerateStringWireEnum(typeof(DateFormatPattern)),
            ["CurrencyAcceptanceLevel"] = EnumerateStringWireEnum(
                typeof(CurrencyAcceptanceLevel)),
            ["MeasurementSystem"] = EnumerateStringWireEnum(typeof(MeasurementSystem)),
            ["GeoDayOfWeek"] = EnumerateStringWireEnum(typeof(GeoDayOfWeek)),
        };

        FixturePathHelpers.WriteFixture(_CATALOG, "enums", data);
    }

    /// <summary>
    /// Reflect every member of a string-wire enum (the wire form is the
    /// member name via <c>JsonStringEnumConverter</c> — OR the value
    /// declared on <see cref="JsonStringEnumMemberNameAttribute"/> when
    /// present) into a sorted <c>{ wireValue: wireValue }</c> map keyed by
    /// the on-the-wire string (the canonical identity used cross-runtime).
    /// For most geo enums the wire form equals the C# member name
    /// (<c>{ AD: "AD", AE: "AE" }</c>); for <see cref="Language"/> the
    /// wire form is the lowercase ISO 639-1 code declared on
    /// <c>[JsonStringEnumMemberName]</c> (<c>{ en: "en", fr: "fr" }</c>).
    /// Keying by the wire form matches the TS-side const-object shape
    /// (TS object keys ARE the wire-form strings) — the parity test
    /// compares fixture keys vs TS keys directly.
    /// </summary>
    private static SortedDictionary<string, object?> EnumerateStringWireEnum(Type enumType)
    {
        var data = new SortedDictionary<string, object?>(StringComparer.Ordinal);
        var names = Enum.GetNames(enumType).OrderBy(n => n, StringComparer.Ordinal);
        foreach (var name in names)
        {
            var wire = GetWireName(enumType, name);
            data[wire] = wire;
        }

        return data;
    }

    /// <summary>
    /// Resolves the wire-form string for an enum member: returns the value of
    /// <see cref="JsonStringEnumMemberNameAttribute"/> when one is declared on
    /// the field, otherwise the C# member name itself (matches
    /// <c>JsonStringEnumConverter</c>'s default behavior).
    /// </summary>
    private static string GetWireName(Type enumType, string memberName)
    {
        var field = enumType.GetField(memberName, BindingFlags.Public | BindingFlags.Static);
        if (field is null)
            return memberName;

        var attribute = field.GetCustomAttribute<JsonStringEnumMemberNameAttribute>(inherit: false);
        if (attribute is null || attribute.Name.Falsey())
            return memberName;

        return attribute.Name;
    }

    /// <summary>
    /// Reflect every member of an integer-wire enum (the wire form is the
    /// integer backing — the TS-side counterpart is an integer-valued const-
    /// object) into a sorted <c>{ memberName: integerValue }</c> map.
    /// </summary>
    private static SortedDictionary<string, object?> EnumerateIntegerWireEnum(Type enumType)
    {
        var data = new SortedDictionary<string, object?>(StringComparer.Ordinal);
        foreach (var v in Enum.GetValues(enumType))
        {
            var name = Enum.GetName(enumType, v)!;

            // Normalize to int regardless of the underlying byte / ushort /
            // int backing so the on-disk JSON is consistent across enums.
            var underlying = Convert.ToInt32(v, System.Globalization.CultureInfo.InvariantCulture);
            data[name] = underlying;
        }

        return data;
    }
}
