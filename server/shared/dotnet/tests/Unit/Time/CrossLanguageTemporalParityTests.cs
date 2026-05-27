// -----------------------------------------------------------------------
// <copyright file="CrossLanguageTemporalParityTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Time;

using System.IO;
using System.Text.Json;
using AwesomeAssertions;
using D2.Shared.Time;
using NodaTime;
using NodaTime.Text;
using Xunit;

/// <summary>
/// Cross-language parity tests for ZonedInstant + LocalAnchoredEvent. Each
/// fixture in <c>contracts/temporal/temporal-adversarial.fixture.json</c>
/// pins a (scheduledLocal, iana) → expectedUtc mapping that BOTH the .NET
/// NodaTime engine AND the TypeScript Temporal engine must produce
/// identically. If a fixture's expectedUtc diverges between languages, the
/// adversarial cross-language contract has broken and the divergence must
/// be surfaced (NOT silently reconciled by adjusting expectedUtc).
/// </summary>
public sealed class CrossLanguageTemporalParityTests
{
    private static readonly JsonSerializerOptions sr_jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    [Fact]
    public void Fixture_USSpringForward_NetMatchesExpectedUtc()
    {
        var fx = LoadFixture("us-spring-forward-skipped-2-30");

        var fire = ComputeFire(fx);

        fire.Should().Be(ParseInstant(fx.ExpectedUtc));
    }

    [Fact]
    public void Fixture_USFallBack_NetMatchesExpectedUtc()
    {
        var fx = LoadFixture("us-fall-back-ambiguous-1-30-picks-earlier");

        var fire = ComputeFire(fx);

        fire.Should().Be(ParseInstant(fx.ExpectedUtc));
    }

    [Fact]
    public void Fixture_EuropeanSpringForward_NetMatchesExpectedUtc()
    {
        var fx = LoadFixture("european-spring-forward-skipped");

        var fire = ComputeFire(fx);

        fire.Should().Be(ParseInstant(fx.ExpectedUtc));
    }

    [Fact]
    public void Fixture_EuropeanFallBack_NetMatchesExpectedUtc()
    {
        var fx = LoadFixture("european-fall-back-ambiguous-picks-earlier");

        var fire = ComputeFire(fx);

        fire.Should().Be(ParseInstant(fx.ExpectedUtc));
    }

    [Fact]
    public void Fixture_AustralianSpringForward_NetMatchesExpectedUtc()
    {
        var fx = LoadFixture("australian-spring-forward-skipped");

        var fire = ComputeFire(fx);

        fire.Should().Be(ParseInstant(fx.ExpectedUtc));
    }

    [Fact]
    public void Fixture_USPacificAlias_NormalizesToAmericaLosAngeles()
    {
        var fx = LoadFixture("iana-normalization-us-pacific-alias");

        var ev = LocalAnchoredEvent
            .Create(ParseLocalDateTime(fx.ScheduledLocal), fx.Iana)
            .Data!;

        ev.IANAIdentifier.Should().Be(fx.ExpectedCanonicalIana);
        ev.ComputeNextFire().Data.Should().Be(ParseInstant(fx.ExpectedUtc));
    }

    [Fact]
    public void Fixture_AsiaSaigonRenamed_NormalizesToAsiaHoChiMinh()
    {
        var fx = LoadFixture("iana-normalization-asia-saigon-renamed");

        var ev = LocalAnchoredEvent
            .Create(ParseLocalDateTime(fx.ScheduledLocal), fx.Iana)
            .Data!;

        ev.IANAIdentifier.Should().Be(fx.ExpectedCanonicalIana);
        ev.ComputeNextFire().Data.Should().Be(ParseInstant(fx.ExpectedUtc));
    }

    [Theory]
    [ClassData(typeof(AllFixturesTheoryData))]
    public void AllFixtures_NetProducesExpectedUtc_TheoryOverEntireFixtureFile(
        string fixtureId)
    {
        var fx = LoadFixture(fixtureId);
        var fire = ComputeFire(fx);

        fire.Should().Be(
            ParseInstant(fx.ExpectedUtc),
            $"fixture '{fx.Id}' must produce expectedUtc identical to TS-side");
    }

    private static Instant ComputeFire(TemporalFixture fx)
    {
        var ev = LocalAnchoredEvent
            .Create(ParseLocalDateTime(fx.ScheduledLocal), fx.Iana)
            .Data!;
        return ev.ComputeNextFire().Data;
    }

    private static LocalDateTime ParseLocalDateTime(string isoLocal) =>
        LocalDateTimePattern.ExtendedIso.Parse(isoLocal).Value;

    private static Instant ParseInstant(string isoZ) =>
        InstantPattern.ExtendedIso.Parse(isoZ).Value;

    private static TemporalFixture LoadFixture(string id)
    {
        var doc = LoadFixtureDoc();
        foreach (var fx in doc.Fixtures)
        {
            if (fx.Id == id)
                return fx;
        }

        throw new KeyNotFoundException($"fixture '{id}' not found");
    }

    private static FixtureFile LoadFixtureDoc()
    {
        var path = FindFixturePath();
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<FixtureFile>(json, sr_jsonOptions)
            ?? throw new InvalidDataException($"could not parse {path}");
    }

    private static string FindFixturePath()
    {
        // Walk up from the test assembly's directory looking for the repo
        // root marker (a directory containing 'contracts/temporal/').
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(
                dir.FullName,
                "contracts",
                "temporal",
                "temporal-adversarial.fixture.json");
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }

        throw new FileNotFoundException(
            "could not locate contracts/temporal/temporal-adversarial.fixture.json " +
                "by walking up from " + AppContext.BaseDirectory);
    }

    public sealed class AllFixturesTheoryData : TheoryData<string>
    {
        public AllFixturesTheoryData()
        {
            var doc = LoadFixtureDoc();
            foreach (var fx in doc.Fixtures)
                Add(fx.Id);
        }
    }

    public sealed record FixtureFile(int SchemaVersion, TemporalFixture[] Fixtures);

    public sealed record TemporalFixture(
        string Id,
        string ScheduledLocal,
        string Iana,
        string ExpectedUtc,
        string? ExpectedCanonicalIana)
    {
        public override string ToString() => Id;
    }
}
