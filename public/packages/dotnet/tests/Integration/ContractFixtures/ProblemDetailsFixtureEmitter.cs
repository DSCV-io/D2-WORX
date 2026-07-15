// -----------------------------------------------------------------------
// <copyright file="ProblemDetailsFixtureEmitter.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.ContractFixtures;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DcsvIo.D2.ProblemDetails;
using Xunit;

/// <summary>
/// Emits one fixture per ProblemDetails sub-catalog reflected off the .NET
/// codegen-emitted static class <see cref="D2ProblemDetailsKeys"/>. Four
/// fixtures: the singular <c>typeUriPrefix</c>, the singular
/// <c>contentType</c>, the <c>EXTENSION_*</c> wire-value map, and the
/// <c>TITLE_*</c> wire-value map. The TS-side parity test asserts
/// byte-equality against the codegen-emitted TS catalog.
/// </summary>
public sealed class ProblemDetailsFixtureEmitter
{
    private const string CATALOG = "problem-details";

    [Fact]
    [Trait("Category", "ContractFixtures")]
    public void Emit_UriPrefix()
    {
        var data = new SortedDictionary<string, object?>(StringComparer.Ordinal)
        {
            [nameof(D2ProblemDetailsKeys.TYPE_URI_PREFIX)] =
                D2ProblemDetailsKeys.TYPE_URI_PREFIX,
        };
        FixturePathHelpers.WriteFixture(CATALOG, "uri-prefix", data);
    }

    [Fact]
    [Trait("Category", "ContractFixtures")]
    public void Emit_ContentType()
    {
        var data = new SortedDictionary<string, object?>(StringComparer.Ordinal)
        {
            [nameof(D2ProblemDetailsKeys.CONTENT_TYPE)] =
                D2ProblemDetailsKeys.CONTENT_TYPE,
        };
        FixturePathHelpers.WriteFixture(CATALOG, "content-type", data);
    }

    [Fact]
    [Trait("Category", "ContractFixtures")]
    public void Emit_ExtensionKeys()
    {
        var data = EnumerateConstants(
            typeof(D2ProblemDetailsKeys),
            prefix: "EXTENSION_");
        FixturePathHelpers.WriteFixture(CATALOG, "extension-keys", data);
    }

    [Fact]
    [Trait("Category", "ContractFixtures")]
    public void Emit_Titles()
    {
        var data = EnumerateConstants(
            typeof(D2ProblemDetailsKeys),
            prefix: "TITLE_");
        FixturePathHelpers.WriteFixture(CATALOG, "titles", data);
    }

    /// <summary>
    /// Reflect every <c>public const string</c> on the catalog type whose
    /// name starts with <paramref name="prefix"/>; produce a sorted map
    /// keyed by the post-prefix name (e.g. <c>EXTENSION_ERROR_CODE</c> →
    /// <c>ERROR_CODE</c>) so the fixture mirrors the TS-side const-map
    /// shape one-to-one.
    /// </summary>
    private static SortedDictionary<string, object?> EnumerateConstants(
        Type type, string prefix)
    {
        var fields = type
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(f => f.IsLiteral
                && f.FieldType == typeof(string)
                && f.Name.StartsWith(prefix, StringComparison.Ordinal))
            .OrderBy(f => f.Name, StringComparer.Ordinal);
        var data = new SortedDictionary<string, object?>(StringComparer.Ordinal);
        foreach (var f in fields)
            data[f.Name.Substring(prefix.Length)] = (string)f.GetValue(null)!;

        return data;
    }
}
