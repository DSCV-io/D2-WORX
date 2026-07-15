// -----------------------------------------------------------------------
// <copyright file="DeprecationMarkerSchemaConsistencyTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.SpecsConsistency;

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using AwesomeAssertions;
using DcsvIo.D2.Tests.Unit.Auth;
using Xunit;

/// <summary>
/// Drift guard for the contract-side deprecate-not-delete marker. Every
/// entry-bearing <c>contracts/**/schema.json</c> declares the same four
/// OPTIONAL marker properties (<c>deprecated</c>, <c>deprecatedReason</c>,
/// <c>replacedBy</c>, <c>sunset</c>) on every entry-object definition (the
/// per-item shape referenced as an array <c>items</c> target), declared
/// IDENTICALLY across all catalogs so no schema can silently diverge and
/// reject a valid marker the codegen would honor. The marker is ALLOWED, never
/// required — these tests also pin that none of the four is added to any
/// <c>required</c> list.
/// </summary>
public sealed class DeprecationMarkerSchemaConsistencyTests
{
    private static readonly string[] sr_markerKeys =
        ["deprecated", "deprecatedReason", "replacedBy", "sunset"];

    [Fact]
    public void EveryEntryDefinition_DeclaresTheFourMarkerProperties()
    {
        var entryDefs = AllEntryDefinitions().ToList();

        entryDefs.Should().NotBeEmpty(
            because: "the contracts tree carries entry-bearing catalog schemas");

        foreach (var (file, defName, def) in entryDefs)
        {
            var props = def.GetProperty("properties");
            foreach (var key in sr_markerKeys)
            {
                props.TryGetProperty(key, out _).Should().BeTrue(
                    because:
                        $"{Path.GetFileName(file)} definition '{defName}' must declare "
                        + $"the optional '{key}' marker property");
            }
        }
    }

    [Fact]
    public void MarkerBlock_IsDeclaredIdentically_AcrossEveryEntryDefinition()
    {
        var shapes = AllEntryDefinitions()
            .Select(e => CanonicalMarkerBlock(e.Definition))
            .Distinct(System.StringComparer.Ordinal)
            .ToList();

        shapes.Should().HaveCount(
            1,
            because:
                "the four marker properties must be declared byte-identically on every "
                + "entry definition so no catalog silently diverges");
    }

    [Fact]
    public void MarkerProperties_AreNeverRequired()
    {
        foreach (var (file, defName, def) in AllEntryDefinitions())
        {
            if (!def.TryGetProperty("required", out var required))
                continue;

            var requiredNames = required.EnumerateArray()
                .Select(e => e.GetString())
                .ToHashSet(System.StringComparer.Ordinal);

            foreach (var key in sr_markerKeys)
            {
                requiredNames.Should().NotContain(
                    key,
                    because:
                        $"{Path.GetFileName(file)} definition '{defName}' must keep the "
                        + $"'{key}' marker OPTIONAL (additive, never required)");
            }
        }
    }

    /// <summary>
    /// Every entry-object definition across all schema files: a
    /// <c>definitions/*</c> object that is referenced as an array <c>items</c>
    /// target (the per-item deprecate-not-delete unit). Excludes the root
    /// object and singleton sub-objects (e.g. an inline config block) that are
    /// not catalog entries.
    /// </summary>
    private static IEnumerable<(string File, string DefName, JsonElement Definition)>
        AllEntryDefinitions()
    {
        var contractsDir = Path.Combine(TestPaths.RepoRoot(), "public", "contracts");
        var schemaFiles = Directory.EnumerateFiles(
            contractsDir, "schema.json", SearchOption.AllDirectories);

        foreach (var file in schemaFiles.OrderBy(f => f, System.StringComparer.Ordinal))
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(file));
            var root = doc.RootElement.Clone();

            if (!root.TryGetProperty("definitions", out var definitions))
                continue;

            var itemTargets = ArrayItemTargets(root);

            foreach (var def in definitions.EnumerateObject())
            {
                if (!itemTargets.Contains(def.Name))
                    continue;

                if (def.Value.ValueKind != JsonValueKind.Object
                    || !def.Value.TryGetProperty("properties", out _))
                    continue;

                yield return (file, def.Name, def.Value);
            }
        }
    }

    /// <summary>
    /// Collects every <c>definitions/X</c> name reachable as an array
    /// <c>items: { "$ref": "#/definitions/X" }</c> anywhere in the schema.
    /// </summary>
    private static HashSet<string> ArrayItemTargets(JsonElement element)
    {
        var targets = new HashSet<string>(System.StringComparer.Ordinal);
        Walk(element, targets);
        return targets;

        static void Walk(JsonElement el, HashSet<string> acc)
        {
            switch (el.ValueKind)
            {
                case JsonValueKind.Object:
                    if (el.TryGetProperty("items", out var items)
                        && items.ValueKind == JsonValueKind.Object
                        && items.TryGetProperty("$ref", out var refEl)
                        && refEl.ValueKind == JsonValueKind.String)
                    {
                        var name = refEl.GetString()!.Split('/').Last();
                        acc.Add(name);
                    }

                    foreach (var prop in el.EnumerateObject())
                        Walk(prop.Value, acc);

                    break;

                case JsonValueKind.Array:
                    foreach (var item in el.EnumerateArray())
                        Walk(item, acc);

                    break;
            }
        }
    }

    /// <summary>
    /// Serializes ONLY the four marker properties of an entry definition into a
    /// stable canonical string (key order fixed by <see cref="sr_markerKeys"/>)
    /// so two definitions compare equal iff their marker blocks are identical.
    /// </summary>
    private static string CanonicalMarkerBlock(JsonElement definition)
    {
        var props = definition.GetProperty("properties");
        var parts = new List<string>();
        foreach (var key in sr_markerKeys)
        {
            props.TryGetProperty(key, out var value).Should().BeTrue(
                because: $"the '{key}' marker property must be present to canonicalize");
            parts.Add($"{key}={value.GetRawText()}");
        }

        return string.Join("\n", parts);
    }
}
