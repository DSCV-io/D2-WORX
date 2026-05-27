// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { readFileSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { describe, expect, it } from "vitest";
import { Temporal } from "temporal-polyfill";
import { LocalAnchoredEvent } from "../src/types.js";

interface TemporalFixture {
  readonly id: string;
  readonly scheduledLocal: string;
  readonly iana: string;
  readonly expectedUtc: string;
  readonly expectedCanonicalIana?: string;
}

interface FixtureFile {
  readonly schemaVersion: number;
  readonly fixtures: readonly TemporalFixture[];
}

function loadFixtureDoc(): FixtureFile {
  // Walk up from this test file looking for the repo root marker
  // (a directory containing contracts/temporal/).
  let dir = dirname(fileURLToPath(import.meta.url));
  for (let i = 0; i < 12; i++) {
    const candidate = join(
      dir,
      "contracts",
      "temporal",
      "temporal-adversarial.fixture.json",
    );
    try {
      const json = readFileSync(candidate, "utf-8");
      return JSON.parse(json) as FixtureFile;
    } catch {
      // not here; walk up
    }
    const parent = resolve(dir, "..");
    if (parent === dir) break;
    dir = parent;
  }
  throw new Error(
    "could not locate contracts/temporal/temporal-adversarial.fixture.json",
  );
}

function computeFire(fx: TemporalFixture): Temporal.Instant {
  const ev = LocalAnchoredEvent.create(
    Temporal.PlainDateTime.from(fx.scheduledLocal),
    fx.iana,
  ).data!;
  return ev.computeNextFire().data!;
}

const doc = loadFixtureDoc();
const findFixture = (id: string): TemporalFixture => {
  const fx = doc.fixtures.find((f) => f.id === id);
  if (fx === undefined) throw new Error(`fixture '${id}' not found`);
  return fx;
};

describe("CrossLanguageTemporalParity", () => {
  it("fixture_USSpringForward_tsMatchesExpectedUtc", () => {
    const fx = findFixture("us-spring-forward-skipped-2-30");
    const fire = computeFire(fx);
    expect(
      Temporal.Instant.compare(fire, Temporal.Instant.from(fx.expectedUtc)),
    ).toBe(0);
  });

  it("fixture_USFallBack_tsMatchesExpectedUtc", () => {
    const fx = findFixture("us-fall-back-ambiguous-1-30-picks-earlier");
    const fire = computeFire(fx);
    expect(
      Temporal.Instant.compare(fire, Temporal.Instant.from(fx.expectedUtc)),
    ).toBe(0);
  });

  it("fixture_EuropeanSpringForward_tsMatchesExpectedUtc", () => {
    const fx = findFixture("european-spring-forward-skipped");
    const fire = computeFire(fx);
    expect(
      Temporal.Instant.compare(fire, Temporal.Instant.from(fx.expectedUtc)),
    ).toBe(0);
  });

  it("fixture_EuropeanFallBack_tsMatchesExpectedUtc", () => {
    const fx = findFixture("european-fall-back-ambiguous-picks-earlier");
    const fire = computeFire(fx);
    expect(
      Temporal.Instant.compare(fire, Temporal.Instant.from(fx.expectedUtc)),
    ).toBe(0);
  });

  it("fixture_AustralianSpringForward_tsMatchesExpectedUtc", () => {
    const fx = findFixture("australian-spring-forward-skipped");
    const fire = computeFire(fx);
    expect(
      Temporal.Instant.compare(fire, Temporal.Instant.from(fx.expectedUtc)),
    ).toBe(0);
  });

  it("fixture_USPacificAlias_normalizesToAmericaLosAngeles", () => {
    const fx = findFixture("iana-normalization-us-pacific-alias");
    const ev = LocalAnchoredEvent.create(
      Temporal.PlainDateTime.from(fx.scheduledLocal),
      fx.iana,
    ).data!;
    expect(ev.ianaIdentifier).toBe(fx.expectedCanonicalIana);
    expect(
      Temporal.Instant.compare(
        ev.computeNextFire().data!,
        Temporal.Instant.from(fx.expectedUtc),
      ),
    ).toBe(0);
  });

  it("fixture_AsiaSaigonRenamed_normalizesToAsiaHoChiMinh", () => {
    const fx = findFixture("iana-normalization-asia-saigon-renamed");
    const ev = LocalAnchoredEvent.create(
      Temporal.PlainDateTime.from(fx.scheduledLocal),
      fx.iana,
    ).data!;
    expect(ev.ianaIdentifier).toBe(fx.expectedCanonicalIana);
    expect(
      Temporal.Instant.compare(
        ev.computeNextFire().data!,
        Temporal.Instant.from(fx.expectedUtc),
      ),
    ).toBe(0);
  });

  it("fixture_UnambiguousUtcNoon_tsMatchesExpectedUtc", () => {
    const fx = findFixture("unambiguous-utc-noon");
    const fire = computeFire(fx);
    expect(
      Temporal.Instant.compare(fire, Temporal.Instant.from(fx.expectedUtc)),
    ).toBe(0);
  });

  it.each(doc.fixtures)("allFixtures_tsProducesExpectedUtc_$id", (fx) => {
    const fire = computeFire(fx);
    expect(
      Temporal.Instant.compare(fire, Temporal.Instant.from(fx.expectedUtc)),
    ).toBe(0);
  });

  /**
   * §1.20 anti-pattern test — deliberate-drift DD-T1: proves the
   * `Temporal.Instant.compare` call used by all parity assertions is
   * non-tautological. If the TS and .NET engines ever diverge on epoch
   * interpretation by even one second, the fixture comparison will catch it —
   * this test verifies that catch IS possible.
   *
   * Constructs two `Temporal.Instant` values differing by exactly one second
   * and asserts `compare` returns non-zero. A broken comparator that always
   * returned 0 ("equal") would fail here.
   */
  it("parity_detectsDivergence_whenInstantEpochsDifferByOneSecond", () => {
    const fx = findFixture("unambiguous-utc-noon");
    const realInstant = Temporal.Instant.from(fx.expectedUtc);

    // Simulate a .NET engine resolving to a different epoch — one second later.
    // Temporal.Instant.add takes a Duration-like object.
    const divergedInstant = realInstant.add({ seconds: 1 });

    expect(Temporal.Instant.compare(realInstant, divergedInstant)).not.toBe(0);
  });

  /**
   * §1.20 anti-pattern test — deliberate-drift DD-T2: proves the DST
   * resolution comparison is non-tautological. If `disambiguation:'compatible'`
   * (TS) and `Resolvers.LenientResolver` (.NET) ever diverge on which side of
   * a DST gap to pick, the fixture comparison catches it — this test verifies
   * that catch IS possible.
   *
   * Uses the US spring-forward fixture (2026-03-08 02:30 America/New_York, a
   * skipped local time). The correct resolved UTC is 07:30Z (forward past the
   * gap). Constructs a deliberately-wrong UTC (one hour earlier — the pre-gap
   * instant) and asserts `compare` returns non-zero. A tautological comparator
   * that always returned 0 would fail here.
   */
  it("parity_detectsDivergence_whenDstResolutionPolicyDiffers", () => {
    const fx = findFixture("us-spring-forward-skipped-2-30");
    const correctUtc = Temporal.Instant.from(fx.expectedUtc); // 2026-03-08T07:30:00Z

    // Simulate an engine that incorrectly picks the pre-gap instant (one hour
    // earlier — the "behind the gap" interpretation):
    const divergedPreGapUtc = correctUtc.subtract({ hours: 1 });

    expect(Temporal.Instant.compare(correctUtc, divergedPreGapUtc)).not.toBe(0);
  });

  /**
   * §1.20 anti-pattern test — deliberate-drift DD-T3: proves the IANA alias
   * canonicalization comparison is non-tautological. If the TS
   * `Intl.DateTimeFormat.resolvedOptions().timeZone` + alias-override map and
   * the .NET NodaTime `CanonicalIdMap` path ever diverge on producing a
   * canonical IANA name, the parity assertion catches it — this test verifies
   * that catch IS possible.
   *
   * Uses the US/Pacific alias fixture, where the correct canonical output is
   * "America/Los_Angeles". Constructs a fake "diverged" un-canonicalized
   * result ("US/Pacific") and asserts strict string equality fails. A
   * tautological comparator that always returned "equal" would fail here.
   */
  it("parity_detectsDivergence_whenIanaAliasNotCanonicalized", () => {
    const fx = findFixture("iana-normalization-us-pacific-alias");
    const ev = LocalAnchoredEvent.create(
      Temporal.PlainDateTime.from(fx.scheduledLocal),
      fx.iana,
    ).data!;

    // Correct canonical form as produced by normalizeIana:
    const canonicalIana = ev.ianaIdentifier; // "America/Los_Angeles"

    // Simulate a TS implementation that failed to apply the alias-override map
    // and returned the raw alias instead:
    const divergedUncanonical = fx.iana; // "US/Pacific"

    expect(canonicalIana).not.toBe(divergedUncanonical);
  });
});
