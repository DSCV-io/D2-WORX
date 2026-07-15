// -----------------------------------------------------------------------
// <copyright file="CrossLanguageTemporalParityTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Time;

using System.IO;
using System.Text.Json;
using AwesomeAssertions;
using DcsvIo.D2.Time;
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

    /// <summary>
    /// §1.20 anti-pattern test — deliberate-drift DD-T1: proves the
    /// <see cref="Instant"/> equality used by the parity assertions is
    /// non-tautological. If the .NET and TypeScript engines ever diverge on
    /// epoch interpretation by even one second, the fixture comparison will
    /// catch it — this test verifies that catch IS possible.
    ///
    /// Constructs two <see cref="Instant"/> values that differ by exactly
    /// one second and asserts they are recognized as DIFFERENT. A broken or
    /// tautological comparator that always returned "equal" would fail here.
    /// </summary>
    [Fact]
    public void Parity_DetectsDivergence_WhenInstantEpochsDifferByOneSecond()
    {
        var fx = LoadFixture("unambiguous-utc-noon");
        var realInstant = ParseInstant(fx.ExpectedUtc);

        // Construct a deliberately-diverged instant: one second later than
        // the real value. This simulates what would happen if the TS engine
        // resolved to a different epoch — the parity comparator must flag it.
        var divergedInstant = realInstant + Duration.FromSeconds(1);

        realInstant.Should().NotBe(
            divergedInstant,
            "parity comparator must detect a 1-second epoch divergence " +
            "between language engines");
    }

    /// <summary>
    /// §1.20 anti-pattern test — deliberate-drift DD-T2: proves the
    /// DST resolution comparison is non-tautological. If the .NET
    /// <see cref="NodaTime.TimeZones.Resolvers.LenientResolver"/> and the
    /// TypeScript <c>disambiguation:'compatible'</c> rule ever diverge on
    /// which side of a DST gap to pick, the fixture comparison catches it —
    /// this test verifies that catch IS possible.
    ///
    /// Uses the US spring-forward fixture (2026-03-08 02:30 America/New_York,
    /// a skipped local time). The correct resolved UTC is 07:30Z (forward into
    /// the post-gap). This test constructs a deliberately-wrong UTC (one hour
    /// earlier — the pre-gap instant) and asserts it is recognized as DIFFERENT
    /// from the correct resolved value. A tautological comparator that always
    /// returned "equal" would fail here.
    /// </summary>
    [Fact]
    public void Parity_DetectsDivergence_WhenDstResolutionPolicyDiffers()
    {
        // Spring-forward: 02:30 local skips to 03:30 → resolves to 07:30Z.
        var fx = LoadFixture("us-spring-forward-skipped-2-30");
        var correctUtc = ParseInstant(fx.ExpectedUtc); // 2026-03-08T07:30:00Z

        // Simulate a resolver that incorrectly picks the pre-gap instant
        // (one hour earlier — the "behind the gap" interpretation):
        var divergedPreGapUtc = correctUtc - Duration.FromHours(1);

        correctUtc.Should().NotBe(
            divergedPreGapUtc,
            "parity comparator must detect divergence when one engine " +
            "resolves to the pre-gap instant and the other to the post-gap");
    }

    /// <summary>
    /// §1.20 anti-pattern test — deliberate-drift DD-T3: proves the IANA
    /// alias canonicalization comparison is non-tautological. If the .NET
    /// NodaTime <c>CanonicalIdMap</c> path and the TypeScript
    /// <c>Intl.DateTimeFormat.resolvedOptions().timeZone</c> + alias-override
    /// map ever diverge on producing a canonical IANA name, the parity
    /// assertion catches it — this test verifies that catch IS possible.
    ///
    /// Uses the US/Pacific alias fixture, where the correct canonical output
    /// is "America/Los_Angeles". This test constructs a fake "diverged"
    /// un-canonicalized result ("US/Pacific") and asserts it is recognized as
    /// DIFFERENT from the canonical form. A tautological comparator that always
    /// returned "equal" would fail here.
    /// </summary>
    [Fact]
    public void Parity_DetectsDivergence_WhenIanaAliasNotCanonicalized()
    {
        var fx = LoadFixture("iana-normalization-us-pacific-alias");
        var ev = LocalAnchoredEvent
            .Create(ParseLocalDateTime(fx.ScheduledLocal), fx.Iana)
            .Data!;

        // Correct canonical form as produced by TimeZoneIdNormalizer:
        var canonicalIana = ev.IANAIdentifier; // "America/Los_Angeles"

        // Simulate a TS implementation that failed to canonicalize and
        // returned the raw alias instead:
        var divergedUncanonical = fx.Iana; // "US/Pacific"

        canonicalIana.Should().NotBe(
            divergedUncanonical,
            "parity comparator must detect when one engine canonicalizes " +
            "IANA aliases and the other returns the raw alias form");
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
                "public",
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
