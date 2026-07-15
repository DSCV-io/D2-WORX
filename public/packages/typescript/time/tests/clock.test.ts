// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import { Temporal } from "temporal-polyfill";
import { SystemClock, TestClock } from "../src/clock.js";

describe("SystemClock", () => {
  it("getInstant_returnsInstantGreaterThanEpoch", () => {
    const clock = new SystemClock();

    const now = clock.getInstant();

    expect(
      Temporal.Instant.compare(now, Temporal.Instant.fromEpochMilliseconds(0)),
    ).toBeGreaterThan(0);
  });

  it("getInstant_calledTwice_secondIsAfterOrEqualToFirst", () => {
    const clock = new SystemClock();

    const first = clock.getInstant();
    const second = clock.getInstant();

    expect(Temporal.Instant.compare(second, first)).toBeGreaterThanOrEqual(0);
  });
});

describe("TestClock", () => {
  const initial = Temporal.Instant.fromEpochMilliseconds(1_000_000);

  it("constructor_withInitialInstant_nowEqualsInitial", () => {
    const clock = new TestClock(initial);

    expect(Temporal.Instant.compare(clock.now, initial)).toBe(0);
  });

  it("getInstant_matchesNow", () => {
    const clock = new TestClock(initial);

    expect(Temporal.Instant.compare(clock.getInstant(), clock.now)).toBe(0);
  });

  it("advance_positiveDuration_shiftsNowForward", () => {
    const clock = new TestClock(Temporal.Instant.fromEpochMilliseconds(0));

    clock.advance(Temporal.Duration.from({ seconds: 10 }));

    expect(
      Temporal.Instant.compare(
        clock.now,
        Temporal.Instant.fromEpochMilliseconds(10_000),
      ),
    ).toBe(0);
  });

  it("advance_negativeDuration_shiftsNowBackward", () => {
    const clock = new TestClock(
      Temporal.Instant.fromEpochMilliseconds(100_000),
    );

    clock.advance(Temporal.Duration.from({ seconds: -30 }));

    expect(
      Temporal.Instant.compare(
        clock.now,
        Temporal.Instant.fromEpochMilliseconds(70_000),
      ),
    ).toBe(0);
  });

  it("advance_zeroDuration_nowUnchanged", () => {
    const clock = new TestClock(initial);

    clock.advance(Temporal.Duration.from({ seconds: 0 }));

    expect(Temporal.Instant.compare(clock.now, initial)).toBe(0);
  });

  it("setTo_explicitInstant_overridesNow", () => {
    const clock = new TestClock(Temporal.Instant.fromEpochMilliseconds(0));
    const target = Temporal.Instant.fromEpochMilliseconds(9_999_000);

    clock.setTo(target);

    expect(Temporal.Instant.compare(clock.now, target)).toBe(0);
  });

  it("setTo_calledTwice_usesSecondValue", () => {
    const clock = new TestClock(Temporal.Instant.fromEpochMilliseconds(0));
    const first = Temporal.Instant.fromEpochMilliseconds(100_000);
    const second = Temporal.Instant.fromEpochMilliseconds(200_000);

    clock.setTo(first);
    clock.setTo(second);

    expect(Temporal.Instant.compare(clock.now, second)).toBe(0);
  });

  it("setTo_sameValue_isNoOp", () => {
    const clock = new TestClock(initial);

    clock.setTo(initial);
    clock.setTo(initial);

    expect(Temporal.Instant.compare(clock.now, initial)).toBe(0);
  });

  it("advance_afterSetTo_appliesFromNewBase", () => {
    const clock = new TestClock(Temporal.Instant.fromEpochMilliseconds(0));
    const newBase = Temporal.Instant.fromEpochMilliseconds(500_000);

    clock.setTo(newBase);
    clock.advance(Temporal.Duration.from({ seconds: 50 }));

    const expected = Temporal.Instant.fromEpochMilliseconds(550_000);
    expect(Temporal.Instant.compare(clock.now, expected)).toBe(0);
  });

  // --- Adversarial: boundary + concurrency (single-threaded JS contract) ---

  it("advance_largeNegativeFromOrigin_handledCleanly", () => {
    const clock = new TestClock(
      Temporal.Instant.fromEpochMilliseconds(1_000_000),
    );

    clock.advance(Temporal.Duration.from({ seconds: -2000 }));

    expect(
      Temporal.Instant.compare(
        clock.now,
        Temporal.Instant.fromEpochMilliseconds(-1_000_000),
      ),
    ).toBe(0);
  });

  it("setTo_temporalInstantBoundary_storedCorrectly", () => {
    // Temporal.Instant supports a range from -271821-04-20 to 275760-09-13.
    // Use a near-max instant well inside the supported range.
    const farFuture = Temporal.Instant.from("9999-12-31T23:59:59Z");
    const clock = new TestClock(Temporal.Instant.fromEpochMilliseconds(0));

    clock.setTo(farFuture);

    expect(Temporal.Instant.compare(clock.now, farFuture)).toBe(0);
  });

  it("advance_concurrentInJSEventLoop_NoCorruption", async () => {
    // JS is single-threaded so OS-level concurrency does not apply; this
    // test runs many microtask-scheduled advance calls and asserts the
    // final value is exactly N * delta. Documents the single-threaded
    // contract — TestClock is safe under cooperative concurrency because
    // every advance() runs atomically between microtask boundaries.
    const clock = new TestClock(Temporal.Instant.fromEpochMilliseconds(0));
    const tasks: Promise<void>[] = [];
    for (let i = 0; i < 100; i++) {
      tasks.push(
        Promise.resolve().then(() => {
          clock.advance(Temporal.Duration.from({ seconds: 1 }));
        }),
      );
    }
    await Promise.all(tasks);

    expect(
      Temporal.Instant.compare(
        clock.now,
        Temporal.Instant.fromEpochMilliseconds(100_000),
      ),
    ).toBe(0);
  });
});
