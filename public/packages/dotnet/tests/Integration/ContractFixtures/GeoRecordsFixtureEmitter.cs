// -----------------------------------------------------------------------
// <copyright file="GeoRecordsFixtureEmitter.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.ContractFixtures;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DcsvIo.D2.Geo.Abstractions;
using Xunit;

/// <summary>
/// Emits the parity fixture for cross-language geo record SHAPES — every
/// record type + every field name + every field type, captured by
/// reflecting over the codegen-emitted record types in
/// <see cref="Country"/>'s assembly. The TS-side
/// <c>geo-records.parity.test.ts</c> loads the fixture and compares
/// field name sets modulo casing.
/// </summary>
/// <remarks>
/// SHAPE parity is distinct from OUTCOME parity. The OUTCOME parity
/// surface is covered by <c>confusables.fixture.json</c> + the
/// <c>ConfusablesTests</c> in both runtimes. SHAPE parity guards against
/// silent field-rename / field-add / field-removal drift between
/// generators.
/// </remarks>
public sealed class GeoRecordsFixtureEmitter
{
    private const string _CATALOG = "geo";

    private static readonly IReadOnlyList<Type> sr_recordTypes =
    [
        typeof(Country),
        typeof(Subdivision),
        typeof(Currency),
        typeof(Language),
        typeof(Locale),
        typeof(Timezone),
        typeof(GeopoliticalEntity),
        typeof(CountryCurrencyAcceptance),
    ];

    [Fact]
    [Trait("Category", "ContractFixtures")]
    public void Emit_Records()
    {
        var data = new SortedDictionary<string, object?>(StringComparer.Ordinal);
        foreach (var type in sr_recordTypes)
            data[type.Name] = ExtractRecordFields(type);

        FixturePathHelpers.WriteFixture(_CATALOG, "records", data);
    }

    /// <summary>
    /// Reflect a record's public init / set properties (the codegen
    /// emits required-init scalars + internal-set nav refs). Each entry
    /// captures the property name (PascalCase) + the resolved type name
    /// (with nullability stripped) + the nullability flag.
    /// </summary>
    private static List<SortedDictionary<string, object?>> ExtractRecordFields(Type type)
    {
        var fields = new List<SortedDictionary<string, object?>>();
        var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .OrderBy(p => p.Name, StringComparer.Ordinal);
        foreach (var prop in props)
        {
            var entry = new SortedDictionary<string, object?>(StringComparer.Ordinal)
            {
                ["name"] = prop.Name,
                ["type"] = FormatTypeName(prop.PropertyType),
                ["nullable"] = IsPropertyNullable(prop),
            };
            fields.Add(entry);
        }

        return fields;
    }

    /// <summary>
    /// Render a type name in a TS-compatible shorthand:
    /// <c>IReadOnlySet&lt;CountryCode&gt;</c> → <c>"Set&lt;CountryCode&gt;"</c>,
    /// <c>IReadOnlyList&lt;Subdivision&gt;</c> → <c>"List&lt;Subdivision&gt;"</c>,
    /// nullable value types unwrap to their underlying form, and other
    /// types use their bare name. The TS-side parity test consumes only
    /// the simple name to compare; rich type-shape matching is out of
    /// scope (different languages have different generic-constraint
    /// surfaces).
    /// </summary>
    private static string FormatTypeName(Type t)
    {
        var underlying = Nullable.GetUnderlyingType(t);
        if (underlying is not null) return FormatTypeName(underlying);

        if (t.IsGenericType)
        {
            var def = t.GetGenericTypeDefinition();
            var args = string.Join(",", t.GetGenericArguments().Select(FormatTypeName));
            var name = def.Name switch
            {
                "IReadOnlyList`1" => "List",
                "IReadOnlySet`1" => "Set",
                "IReadOnlyDictionary`2" => "Dictionary",
                "IReadOnlyCollection`1" => "List",
                _ => StripArity(def.Name),
            };
            return $"{name}<{args}>";
        }

        return t.Name;
    }

    private static string StripArity(string name)
    {
        var tick = name.IndexOf('`');
        return tick < 0 ? name : name[..tick];
    }

    /// <summary>
    /// Determine whether a property type is nullable in the C#
    /// reference-type sense. Value types use <see cref="Nullable{T}"/>;
    /// reference types use the <c>NullableAttribute</c> emitted by the
    /// compiler (read via <see cref="NullabilityInfoContext"/>).
    /// </summary>
    private static bool IsPropertyNullable(PropertyInfo prop)
    {
        if (Nullable.GetUnderlyingType(prop.PropertyType) is not null) return true;
        if (prop.PropertyType.IsValueType) return false;
        var nullability = new NullabilityInfoContext().Create(prop);
        return nullability.ReadState == NullabilityState.Nullable;
    }
}
