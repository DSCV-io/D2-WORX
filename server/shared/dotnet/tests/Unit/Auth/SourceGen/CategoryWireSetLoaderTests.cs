// -----------------------------------------------------------------------
// <copyright file="CategoryWireSetLoaderTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Auth.SourceGen;

using System.IO;
using System.Linq;
using System.Text.Json;
using AwesomeAssertions;
using D2.Shared.ErrorCodes.SourceGen;
using D2.Shared.Tests.Unit.Auth;
using Xunit;

/// <summary>
/// Pure-logic tests for <see cref="CategoryWireSetLoader"/> — the spec-derived
/// closed category-set parser. Drives the loader directly (no Roslyn host) and
/// asserts the empty-degrades-to-no-op contract: a malformed or shapeless spec
/// yields an empty set so the category-membership check downstream never fires
/// a false unknown-category diagnostic, while a well-formed spec yields exactly
/// the declared <c>categories[].wire</c> values.
/// </summary>
public sealed class CategoryWireSetLoaderTests
{
    [Fact]
    public void LoadWireSet_ValidSpec_ReturnsEveryWireValue()
    {
        const string json = """
        {
          "categories": [
            { "wire": "validation_failure", "doc": "x" },
            { "wire": "not_found", "doc": "y" },
            { "wire": "internal_error", "doc": "z" }
          ]
        }
        """;

        var set = CategoryWireSetLoader.LoadWireSet(json);

        set.Should().BeEquivalentTo(
            ["validation_failure", "not_found", "internal_error"]);
    }

    [Fact]
    public void LoadWireSet_MalformedJson_ReturnsEmpty()
    {
        var set = CategoryWireSetLoader.LoadWireSet("{not valid json");

        set.Should().BeEmpty();
    }

    [Fact]
    public void LoadWireSet_NonObjectRoot_ReturnsEmpty()
    {
        var set = CategoryWireSetLoader.LoadWireSet("[\"a\", \"b\"]");

        set.Should().BeEmpty();
    }

    [Fact]
    public void LoadWireSet_MissingCategoriesArray_ReturnsEmpty()
    {
        var set = CategoryWireSetLoader.LoadWireSet("""{ "other": 1 }""");

        set.Should().BeEmpty();
    }

    [Fact]
    public void LoadWireSet_CategoriesNotAnArray_ReturnsEmpty()
    {
        var set = CategoryWireSetLoader.LoadWireSet("""{ "categories": "nope" }""");

        set.Should().BeEmpty();
    }

    [Fact]
    public void LoadWireSet_EntryWithoutStringWire_IsSkipped()
    {
        // Non-string `wire`, missing `wire`, and non-object entries are all
        // skipped; only the well-formed entry survives.
        const string json = """
        {
          "categories": [
            { "wire": 123, "doc": "non-string wire" },
            { "doc": "missing wire" },
            "not an object",
            { "wire": "conflict", "doc": "ok" }
          ]
        }
        """;

        var set = CategoryWireSetLoader.LoadWireSet(json);

        set.Should().BeEquivalentTo(["conflict"]);
    }

    [Fact]
    public void LoadWireSet_EmptyCategoriesArray_ReturnsEmpty()
    {
        var set = CategoryWireSetLoader.LoadWireSet("""{ "categories": [] }""");

        set.Should().BeEmpty();
    }

    /// <summary>
    /// Drift guard: the loader's derived set MUST equal the live
    /// <c>error-category.spec.json</c> <c>categories[].wire</c> values exactly.
    /// This pins the spec-derived category-validation surface to the 9 declared
    /// categories so neither a stale hand-maintained subset nor a parser bug can
    /// silently widen or narrow the accepted set.
    /// </summary>
    [Fact]
    public void LoadWireSet_RealSpec_EqualsEveryDeclaredWireValue()
    {
        var json = File.ReadAllText(TestPaths.ErrorCategorySpec());
        var derived = CategoryWireSetLoader.LoadWireSet(json);

        using var doc = JsonDocument.Parse(json);
        var declared = doc.RootElement
            .GetProperty("categories")
            .EnumerateArray()
            .Select(e => e.GetProperty("wire").GetString()!)
            .ToList();

        derived.Should().BeEquivalentTo(
            declared,
            because: "the spec-derived category set must match error-category.spec.json verbatim");
        derived.Count.Should().Be(9);
    }
}
