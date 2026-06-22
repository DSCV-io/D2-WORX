// -----------------------------------------------------------------------
// <copyright file="PredicateParityTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.TypeSpecGrpcPredicate;

using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.Json;
using D2.Edge.Tests.TypeSpecGrpcPredicate.Generated;
using D2.Shared.ErrorCodes.Category;
using D2.Shared.Result;

/// <summary>
/// The C# half of the cross-language @d2Resilience predicate-behavior parity suite. Drives the
/// SAME shared fixture (<c>contracts/resilience/predicate-parity.fixture.json</c>) as the
/// TypeScript <c>predicate-parity.test.ts</c>, so an identically-shaped reconstructed
/// <see cref="D2Result{TData}"/> yields the SAME retry / fail booleans from the emitted C#
/// predicate (<see cref="PlaceOrderResiliencePredicates"/>) as from the emitted TS predicate.
/// Each row reconstructs a <see cref="D2Result{TData}"/> with the row's envelope fields + data
/// shape, evaluates <see cref="PlaceOrderResiliencePredicates.SR_RetryWhen"/> /
/// <see cref="PlaceOrderResiliencePredicates.SR_FailWhen"/>, and asserts the expected booleans.
/// A divergence between the two languages breaks the cross-language emission contract and MUST be
/// surfaced (NOT silently reconciled by editing an expected value).
/// </summary>
public sealed class PredicateParityTests
{
    private static readonly JsonSerializerOptions sr_jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static TheoryData<string> CaseNames()
    {
        var data = new TheoryData<string>();
        foreach (var c in LoadFixture().Cases)
            data.Add(c.Name);

        return data;
    }

    public static TheoryData<string> CaseNamesV2()
    {
        var data = new TheoryData<string>();
        foreach (var c in LoadFixtureV2().Cases)
            data.Add(c.Name);

        return data;
    }

    [Theory]
    [MemberData(nameof(CaseNames))]
    public void EmittedCsharpPredicate_MatchesExpected_RetryAndFail(string caseName)
    {
        var fixture = LoadFixture();
        var c = fixture.Cases.Single(x => x.Name == caseName);

        var result = Reconstruct(c);

        PlaceOrderResiliencePredicates.SR_RetryWhen(result).Should().Be(
            c.ExpectedRetry,
            $"retryWhen for case '{caseName}' must match the cross-language expectation");
        PlaceOrderResiliencePredicates.SR_FailWhen(result).Should().Be(
            c.ExpectedFail,
            $"failWhen for case '{caseName}' must match the cross-language expectation");
    }

    [Fact]
    public void Fixture_IsNonVacuous_CoversBothRetryAndFailTrueAndFalse()
    {
        var cases = LoadFixture().Cases;

        // A vacuous matrix (e.g. every row trivially true) would prove nothing — assert the
        // fixture exercises BOTH outcomes for BOTH predicates.
        cases.Should().Contain(x => x.ExpectedRetry, "the matrix must include a retryWhen-true row");
        cases.Should().Contain(x => !x.ExpectedRetry, "the matrix must include a retryWhen-false row");
        cases.Should().Contain(x => x.ExpectedFail, "the matrix must include a failWhen-true row");
        cases.Should().Contain(x => !x.ExpectedFail, "the matrix must include a failWhen-false row");
        cases.Should().Contain(
            x => x.ExpectedRetry && x.ExpectedFail,
            "the matrix must include a failWhen-wins row (both true)");
    }

    // -----------------------------------------------------------------------
    // placeOrderV2 — the NESTED-model + array-of-MODEL parity matrix.
    //
    // Drives the SAME shared nested fixture (predicate-parity-nested.fixture.json) as the
    // TypeScript predicate-parity.test.ts, so an identically-shaped reconstructed
    // D2Result<PlaceOrderV2Output?> yields the SAME retry / fail booleans from the emitted C#
    // predicate (PlaceOrderV2ResiliencePredicates, a deep ?.-chain over Customer.Tier + a LINQ
    // .Any(...) quantifier over Lines) as from the emitted TS predicate. The compiled emitted
    // predicate is EXECUTED here against real nested DTO instances — the behavioral cross-language
    // proof the flat placeOrder matrix cannot give.
    // -----------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(CaseNamesV2))]
    public void EmittedCsharpPredicateV2_NestedAndArrayOfModel_MatchesExpected(string caseName)
    {
        var fixture = LoadFixtureV2();
        var c = fixture.Cases.Single(x => x.Name == caseName);

        var result = ReconstructV2(c);

        PlaceOrderV2ResiliencePredicates.SR_RetryWhen(result).Should().Be(
            c.ExpectedRetry,
            $"retryWhen for case '{caseName}' must match the cross-language expectation");
        PlaceOrderV2ResiliencePredicates.SR_FailWhen(result).Should().Be(
            c.ExpectedFail,
            $"failWhen for case '{caseName}' must match the cross-language expectation");
    }

    [Fact]
    public void FixtureV2_IsNonVacuous_ExercisesNestedPathAndArrayQuantifier()
    {
        var cases = LoadFixtureV2().Cases;

        cases.Any(x => x.ExpectedRetry).Should().BeTrue("the nested matrix must include a retryWhen-true row");
        cases.Any(x => !x.ExpectedRetry).Should().BeTrue("the nested matrix must include a retryWhen-false row");
        cases.Any(x => x.ExpectedFail).Should().BeTrue("the nested matrix must include a failWhen-true row");
        cases.Any(x => !x.ExpectedFail).Should().BeTrue("the nested matrix must include a failWhen-false row");
        cases.Any(x => x.ExpectedRetry && x.ExpectedFail).Should().BeTrue(
            "the nested matrix must include a failWhen-wins row (both true)");

        // The deep ?.-chain is genuinely traversed: a present TRIAL customer with NO PENDING line
        // drives retry SOLELY via customer.tier (so Customer?.Tier must resolve, not vacuously pass).
        // Evaluated in-memory (NOT an expression tree) so `is`-pattern null navigation is allowed.
        var nestedPathDriven = cases.Any(
            x => x.Data is { Customer.Tier: "TRIAL" }
                && x.Data.Lines.All(l => l.Status != "PENDING")
                && x.ExpectedRetry);
        nestedPathDriven.Should().BeTrue("a row must drive retry via the nested Customer.Tier path alone");

        // The array-of-model quantifier is genuinely traversed: NO customer but a PENDING line
        // drives retry SOLELY via Lines.Any(...).
        var arrayQuantifierDriven = cases.Any(
            x => x.Data is { Customer: null }
                && x.Data.Lines.Any(l => l.Status == "PENDING")
                && x.ExpectedRetry);
        arrayQuantifierDriven.Should().BeTrue(
            "a row must drive retry via the array-of-model Lines.Any(...) quantifier alone");
    }

    private static D2Result<PlaceOrderOutput?> Reconstruct(ParityCase c)
    {
        ErrorCategory? category = null;
        if (c.Category is not null && ErrorCategoryWire.TryFromWire(c.Category, out var parsed))
            category = parsed;

        PlaceOrderOutput? data = c.Data is null
            ? null
            : new PlaceOrderOutput(c.Data.OrderCode, c.Data.ItemStatuses, c.Data.Partial);

        return new D2Result<PlaceOrderOutput?>(
            success: c.Success,
            data: data,
            statusCode: (HttpStatusCode)c.StatusCode,
            errorCode: c.ErrorCode,
            category: category);
    }

    private static D2Result<PlaceOrderV2Output?> ReconstructV2(ParityCaseV2 c)
    {
        PlaceOrderV2Output? data = c.Data is null
            ? null
            : new PlaceOrderV2Output(
                c.Data.OrderCode,
                c.Data.Lines.Select(l => new PlaceOrderLine(l.Status)).ToList(),
                c.Data.Customer is null ? null : new PlaceOrderV2Customer(c.Data.Customer.Tier));

        return new D2Result<PlaceOrderV2Output?>(
            success: c.Success,
            data: data,
            statusCode: (HttpStatusCode)c.StatusCode,
            errorCode: c.ErrorCode);
    }

    private static FixtureFile LoadFixture()
    {
        var json = File.ReadAllText(FindFixturePath("predicate-parity.fixture.json"));
        return JsonSerializer.Deserialize<FixtureFile>(json, sr_jsonOptions)!;
    }

    private static FixtureFileV2 LoadFixtureV2()
    {
        var json = File.ReadAllText(FindFixturePath("predicate-parity-nested.fixture.json"));
        return JsonSerializer.Deserialize<FixtureFileV2>(json, sr_jsonOptions)!;
    }

    private static string FindFixturePath(string fileName)
    {
        // Walk up from the test assembly's directory looking for the repo-root marker
        // (a directory containing 'contracts/resilience/') — robust to bin-path depth.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "contracts", "resilience", fileName);
            if (File.Exists(candidate))
                return candidate;

            dir = dir.Parent;
        }

        throw new FileNotFoundException(
            $"could not locate contracts/resilience/{fileName} by walking up from "
                + AppContext.BaseDirectory);
    }

    private sealed record FixtureFile(IReadOnlyList<ParityCase> Cases);

    private sealed record ParityCase(
        string Name,
        bool Success,
        int StatusCode,
        string? ErrorCode,
        string? Category,
        ParityData? Data,
        bool ExpectedRetry,
        bool ExpectedFail);

    private sealed record ParityData(
        string OrderCode,
        IReadOnlyList<string> ItemStatuses,
        bool Partial);

    private sealed record FixtureFileV2(IReadOnlyList<ParityCaseV2> Cases);

    private sealed record ParityCaseV2(
        string Name,
        bool Success,
        int StatusCode,
        string? ErrorCode,
        ParityDataV2? Data,
        bool ExpectedRetry,
        bool ExpectedFail);

    private sealed record ParityDataV2(
        string OrderCode,
        IReadOnlyList<ParityLineV2> Lines,
        ParityCustomerV2? Customer);

    private sealed record ParityLineV2(string Status);

    private sealed record ParityCustomerV2(string Tier);
}
