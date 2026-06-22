// -----------------------------------------------------------------------
// <copyright file="KeyCustodianErrorCodesSchemaDriftTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.SpecsConsistency;

using System.Text.Json;

/// <summary>
/// Drift guard for the keycustodian error-codes schema. The keycustodian
/// <c>contracts/keycustodian-error-codes/schema.json</c> is a domain-specialized
/// COPY of <c>contracts/error-codes/error-codes.canonical.schema.json</c> (NOT a
/// <c>$ref</c> — keycustodian legitimately narrows the canonical's <c>code</c>
/// prefix / <c>httpStatus</c> / <c>category</c> constraints, which a <c>$ref</c>
/// would discard). This test asserts the copy stays field-aligned with the
/// canonical (same <c>errorCode</c> field-set + required-list +
/// <c>factoryShape</c> enum) so a canonical field addition the keycustodian copy
/// misses surfaces as a red test, and that every keycustodian spec entry carries
/// the universal standard shape on one of its two legal (status, category) pairs.
/// </summary>
public sealed class KeyCustodianErrorCodesSchemaDriftTests
{
    [Fact]
    public void KcSchema_ErrorCodeRequiredList_MatchesCanonical()
    {
        var kcRequired = ErrorCodeRequired(KcSchema());
        var canonicalRequired = ErrorCodeRequired(CanonicalSchema());

        kcRequired.Should().BeEquivalentTo(
            canonicalRequired,
            because: "the keycustodian schema is a specialized copy of the canonical field-set");
    }

    [Fact]
    public void KcSchema_ErrorCodePropertyNames_MatchCanonical()
    {
        var kcProps = ErrorCodePropertyNames(KcSchema());
        var canonicalProps = ErrorCodePropertyNames(CanonicalSchema());

        kcProps.Should().BeEquivalentTo(
            canonicalProps,
            because: "the keycustodian schema declares the same 7 fields as the canonical");
    }

    [Fact]
    public void KcSchema_FactoryShapeEnum_MatchesCanonical()
    {
        var kcEnum = FactoryShapeEnum(KcSchema());
        var canonicalEnum = FactoryShapeEnum(CanonicalSchema());

        kcEnum.Should().BeEquivalentTo(
            canonicalEnum,
            because: "factoryShape carries the canonical 2-value enum verbatim");
    }

    [Fact]
    public void KcSchema_KeepsTighterKeyCustodianConstraints()
    {
        var props = KcSchema()
            .GetProperty("definitions").GetProperty("errorCode").GetProperty("properties");

        props.GetProperty("code").GetProperty("pattern").GetString()
            .Should().Be("^KEYCUSTODIAN_[A-Z][A-Z0-9_]*$");

        // KC narrows the canonical status set to the {400 input-validation,
        // 404 key-not-found, 409 lifecycle-conflict, 500 precondition / smoke-test
        // / cert-build, 503 no-active-issuing-CA} subset.
        var statuses = props.GetProperty("httpStatus").GetProperty("enum")
            .EnumerateArray().Select(e => e.GetInt32()).ToList();
        int[] expectedStatuses = [400, 404, 409, 500, 503];
        statuses.Should().BeEquivalentTo(expectedStatuses);

        // KC narrows the canonical category set to the {validation_failure,
        // not_found, conflict, internal_error, infrastructure_unavailable} subset.
        var categories = props.GetProperty("category").GetProperty("enum")
            .EnumerateArray().Select(e => e.GetString()).ToList();
        string[] expectedCategories =
        [
            "validation_failure", "not_found", "conflict", "internal_error",
            "infrastructure_unavailable",
        ];
        categories.Should().BeEquivalentTo(expectedCategories);
    }

    [Fact]
    public void KcSpec_EveryEntry_IsStandardAndDeclaredStatusCategoryPair()
    {
        // Every KC entry is the universal standard shape; each is one of five legal
        // (status, category) pairs: 400/validation_failure (input validation),
        // 404/not_found (key lookup), 409/conflict (illegal transition / duplicate
        // pending), 500/internal_error (precondition violation / smoke-test / cert
        // build), or 503/infrastructure_unavailable (no active issuing CA).
        var legalPairs = new Dictionary<int, string>
        {
            [400] = "validation_failure",
            [404] = "not_found",
            [409] = "conflict",
            [500] = "internal_error",
            [503] = "infrastructure_unavailable",
        };

        var entries = KcSpecEntries();

        entries.Should().NotBeEmpty();

        foreach (var entry in entries)
        {
            entry.GetProperty("factoryShape").GetString().Should().Be("standard");

            var status = entry.GetProperty("httpStatus").GetInt32();
            var category = entry.GetProperty("category").GetString();
            var code = entry.GetProperty("code").GetString();

            legalPairs.Should().ContainKey(
                status,
                because: $"KC code '{code}' must declare one of the four legal KC statuses");
            category.Should().Be(
                legalPairs[status],
                because: $"the {status} KC code '{code}' must carry its paired category");
        }
    }

    /// <summary>
    /// Asserts that every keycustodian spec entry's <c>factoryShape</c>,
    /// <c>category</c>, and <c>httpStatus</c> values are members of the enums
    /// declared in <c>contracts/keycustodian-error-codes/schema.json</c>. Catches
    /// a copy typo in the schema (an enum value present in the spec but not in the
    /// schema) without requiring a full JSON Schema validator dependency.
    /// </summary>
    [Fact]
    public void KcSpec_AllEntryValues_ConformToSchemaEnums()
    {
        var props = KcSchema()
            .GetProperty("definitions").GetProperty("errorCode").GetProperty("properties");

        var allowedFactoryShapes = props.GetProperty("factoryShape").GetProperty("enum")
            .EnumerateArray().Select(e => e.GetString()!).ToHashSet(StringComparer.Ordinal);

        var allowedCategories = props.GetProperty("category").GetProperty("enum")
            .EnumerateArray().Select(e => e.GetString()!).ToHashSet(StringComparer.Ordinal);

        var allowedStatuses = props.GetProperty("httpStatus").GetProperty("enum")
            .EnumerateArray().Select(e => e.GetInt32()).ToHashSet();

        foreach (var entry in KcSpecEntries())
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

    [Fact]
    public void KcSpec_EveryCode_IsKeyCustodianPrefixed()
    {
        foreach (var entry in KcSpecEntries())
        {
            entry.GetProperty("code").GetString()
                .Should().StartWith(
                    "KEYCUSTODIAN_",
                    "every code in the keycustodian catalog must carry the domain prefix");
        }
    }

    private static JsonElement KcSchema() => Load(TestPaths.KeyCustodianErrorCodesSchema());

    private static JsonElement CanonicalSchema() => Load(TestPaths.CanonicalErrorCodesSchema());

    private static IReadOnlyList<JsonElement> KcSpecEntries() =>
        Load(TestPaths.KeyCustodianErrorCodesSpec())
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
