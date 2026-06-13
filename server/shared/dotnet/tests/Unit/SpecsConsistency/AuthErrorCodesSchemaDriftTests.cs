// -----------------------------------------------------------------------
// <copyright file="AuthErrorCodesSchemaDriftTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.SpecsConsistency;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using AwesomeAssertions;
using D2.Shared.Tests.Unit.Auth;
using Xunit;

/// <summary>
/// Drift guard for the auth error-codes schema. The auth
/// <c>contracts/auth-error-codes/schema.json</c> is a domain-specialized COPY
/// of <c>contracts/error-codes/error-codes.canonical.schema.json</c> (NOT a
/// <c>$ref</c> — auth legitimately narrows the canonical's <c>code</c>
/// prefix / <c>httpStatus</c> / <c>category</c> constraints, which a
/// <c>$ref</c> would discard). This test asserts the copy stays field-aligned
/// with the canonical (same <c>errorCode</c> field-set + required-list +
/// <c>factoryShape</c> enum) so a canonical field addition that the auth copy
/// misses surfaces as a red test, and that every auth spec entry carries a
/// valid <c>factoryShape</c>.
/// </summary>
public sealed class AuthErrorCodesSchemaDriftTests
{
    [Fact]
    public void AuthSchema_ErrorCodeRequiredList_MatchesCanonical()
    {
        var authRequired = ErrorCodeRequired(AuthSchema());
        var canonicalRequired = ErrorCodeRequired(CanonicalSchema());

        authRequired.Should().BeEquivalentTo(
            canonicalRequired,
            because: "the auth schema is a specialized copy of the canonical field-set");
    }

    [Fact]
    public void AuthSchema_ErrorCodePropertyNames_MatchCanonical()
    {
        var authProps = ErrorCodePropertyNames(AuthSchema());
        var canonicalProps = ErrorCodePropertyNames(CanonicalSchema());

        authProps.Should().BeEquivalentTo(
            canonicalProps,
            because: "the auth schema declares the same 7 fields as the canonical");
    }

    [Fact]
    public void AuthSchema_FactoryShapeEnum_MatchesCanonical()
    {
        var authEnum = FactoryShapeEnum(AuthSchema());
        var canonicalEnum = FactoryShapeEnum(CanonicalSchema());

        authEnum.Should().BeEquivalentTo(
            canonicalEnum,
            because: "factoryShape carries the canonical 2-value enum verbatim");
    }

    [Fact]
    public void AuthSchema_KeepsTighterAuthConstraints()
    {
        var authSchema = AuthSchema();
        var props = authSchema
            .GetProperty("definitions").GetProperty("errorCode").GetProperty("properties");

        props.GetProperty("code").GetProperty("pattern").GetString()
            .Should().Be("^AUTH_[A-Z][A-Z0-9_]*$");

        var statuses = props.GetProperty("httpStatus").GetProperty("enum")
            .EnumerateArray().Select(e => e.GetInt32()).ToList();
        statuses.Should().BeEquivalentTo(new[] { 401, 503 });

        var categories = props.GetProperty("category").GetProperty("enum")
            .EnumerateArray().Select(e => e.GetString()).ToList();
        categories.Should().BeEquivalentTo(
            new[] { "validation_failure", "infrastructure_unavailable", "policy_denied" });
    }

    [Fact]
    public void AuthSpec_EveryEntryHasStandardFactoryShape()
    {
        var shapes = AuthSpecEntries()
            .Select(e => e.GetProperty("factoryShape").GetString())
            .ToList();

        shapes.Should().NotBeEmpty();
        shapes.Should().AllBe(
            "standard",
            "every auth factory (401 Unauthorized + 503 ServiceUnavailable) shares the "
            + "universal standard signature; the typed <T> overload on 503 is httpStatus-driven");
    }

    /// <summary>
    /// Asserts that every auth spec entry's <c>factoryShape</c>, <c>category</c>,
    /// and <c>httpStatus</c> values are members of the enums declared in
    /// <c>contracts/auth-error-codes/schema.json</c>. This catches a copy typo
    /// in the schema (e.g. an enum value present in the spec but not in the
    /// schema definition) without requiring a full JSON Schema validator in the
    /// test project's dependencies.
    /// </summary>
    /// <remarks>
    /// Approach: enum-membership assertion (no JSON Schema library needed).
    /// Catches the class of drift where spec entries introduce a new value that
    /// the schema doesn't declare, or a schema update forgets to add a value.
    /// </remarks>
    [Fact]
    public void AuthSpec_AllEntryValues_ConformToSchemaEnums()
    {
        var schema = AuthSchema();
        var props = schema
            .GetProperty("definitions").GetProperty("errorCode").GetProperty("properties");

        var allowedFactoryShapes = props.GetProperty("factoryShape").GetProperty("enum")
            .EnumerateArray().Select(e => e.GetString()!).ToHashSet(StringComparer.Ordinal);

        var allowedCategories = props.GetProperty("category").GetProperty("enum")
            .EnumerateArray().Select(e => e.GetString()!).ToHashSet(StringComparer.Ordinal);

        var allowedStatuses = props.GetProperty("httpStatus").GetProperty("enum")
            .EnumerateArray().Select(e => e.GetInt32()).ToHashSet();

        foreach (var entry in AuthSpecEntries())
        {
            var code = entry.GetProperty("code").GetString()!;

            entry.GetProperty("factoryShape").GetString()
                .Should().BeOneOf(
                    [.. allowedFactoryShapes],
                    because:
                        $"spec entry '{code}' factoryShape must be a member of "
                        + "schema.json factoryShape enum");

            entry.GetProperty("category").GetString()
                .Should().BeOneOf(
                    [.. allowedCategories],
                    because:
                        $"spec entry '{code}' category must be a member of "
                        + "schema.json category enum");

            entry.GetProperty("httpStatus").GetInt32()
                .Should().BeOneOf(
                    [.. allowedStatuses],
                    because:
                        $"spec entry '{code}' httpStatus must be a member of "
                        + "schema.json httpStatus enum");
        }
    }

    private static JsonElement AuthSchema() =>
        Load(Path.Combine(
            TestPaths.RepoRoot(), "contracts", "auth-error-codes", "schema.json"));

    private static JsonElement CanonicalSchema() =>
        Load(Path.Combine(
            TestPaths.RepoRoot(),
            "contracts",
            "error-codes",
            "error-codes.canonical.schema.json"));

    private static IEnumerable<JsonElement> AuthSpecEntries() =>
        Load(Path.Combine(
                TestPaths.RepoRoot(),
                "contracts",
                "auth-error-codes",
                "auth-error-codes.spec.json"))
            .GetProperty("errorCodes")
            .EnumerateArray()
            .ToList();

    private static List<string> ErrorCodeRequired(JsonElement schema) =>
        schema.GetProperty("definitions").GetProperty("errorCode").GetProperty("required")
            .EnumerateArray().Select(e => e.GetString()!).ToList();

    private static List<string> ErrorCodePropertyNames(JsonElement schema) =>
        schema.GetProperty("definitions").GetProperty("errorCode").GetProperty("properties")
            .EnumerateObject().Select(p => p.Name).ToList();

    private static List<string> FactoryShapeEnum(JsonElement schema) =>
        schema.GetProperty("definitions").GetProperty("errorCode").GetProperty("properties")
            .GetProperty("factoryShape").GetProperty("enum")
            .EnumerateArray().Select(e => e.GetString()!).ToList();

    private static JsonElement Load(string path)
    {
        var json = File.ReadAllText(path);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }
}
