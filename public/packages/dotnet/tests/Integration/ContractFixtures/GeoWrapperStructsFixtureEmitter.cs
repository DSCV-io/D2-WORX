// -----------------------------------------------------------------------
// <copyright file="GeoWrapperStructsFixtureEmitter.cs" company="DCSV">
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
/// Emits the parity fixture for the codegen-emitted geo wrapper structs
/// (<see cref="SubdivisionCode"/>, <see cref="LocaleCode"/>,
/// <see cref="TimezoneCode"/>) — specifically the closed-set validation
/// catalogs embedded in each struct's <c>JsonConverter</c>. The catalog
/// is the cross-language wire contract: a value rejected on one side
/// must be rejected on the other.
/// </summary>
/// <remarks>
/// The validation sets live as <c>private static readonly FrozenSet&lt;string&gt;</c>
/// fields on each <c>*JsonConverter</c> (e.g.
/// <c>SubdivisionCodeJsonConverter.sr_validSubdivisionCodes</c>) — exposed
/// publicly only via the boolean <c>IsKnown(string)</c> probe. Reflection
/// is the right hammer here: enumerating the full set lets the parity test
/// pin set equality (sorted string array), not just per-string probes.
/// The TS-side parity test loads <c>fixtures/geo/wrapper-structs.json</c>
/// and asserts byte-equivalence against the TS-side
/// <c>SUBDIVISION_CODE_SET</c> / <c>LOCALE_CODE_SET</c> /
/// <c>TIMEZONE_CODE_SET</c> exports.
/// </remarks>
public sealed class GeoWrapperStructsFixtureEmitter
{
    private const string _CATALOG = "geo";

    [Fact]
    [Trait("Category", "ContractFixtures")]
    public void Emit_WrapperStructs()
    {
        var data = new SortedDictionary<string, object?>(StringComparer.Ordinal)
        {
            ["SubdivisionCode"] = ExtractValidationSet(
                typeof(SubdivisionCodeJsonConverter),
                fieldName: "sr_validSubdivisionCodes"),
            ["LocaleCode"] = ExtractValidationSet(
                typeof(LocaleCodeJsonConverter),
                fieldName: "sr_validLocaleCodes"),
            ["TimezoneCode"] = ExtractValidationSet(
                typeof(TimezoneCodeJsonConverter),
                fieldName: "sr_validTimezoneCodes"),
        };

        FixturePathHelpers.WriteFixture(_CATALOG, "wrapper-structs", data);
    }

    /// <summary>
    /// Reflect the named private static <c>FrozenSet&lt;string&gt;</c> field
    /// off a JsonConverter type into a sorted list of strings (ordinal
    /// sort) so the on-disk fixture is stable across reorderings.
    /// </summary>
    /// <param name="converterType">
    /// The <c>*JsonConverter</c> class hosting the validation set.
    /// </param>
    /// <param name="fieldName">
    /// The private static readonly FrozenSet field name (e.g.
    /// <c>sr_validSubdivisionCodes</c>).
    /// </param>
    private static List<string> ExtractValidationSet(Type converterType, string fieldName)
    {
        var field = converterType.GetField(
            fieldName,
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException(
                $"Could not find private static field '{fieldName}' " +
                $"on {converterType.FullName} — " +
                "the geo source-gen emitter may have renamed the field; " +
                "update GeoWrapperStructsFixtureEmitter to match.");

        var raw = field.GetValue(null)
            ?? throw new InvalidOperationException(
                $"Field '{fieldName}' on {converterType.FullName} returned null — "
                + "the emitted set is missing.");

        var enumerable = (System.Collections.IEnumerable)raw;
        return enumerable
            .Cast<string>()
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToList();
    }
}
