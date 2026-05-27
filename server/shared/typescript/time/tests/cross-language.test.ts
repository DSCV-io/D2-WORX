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
});
