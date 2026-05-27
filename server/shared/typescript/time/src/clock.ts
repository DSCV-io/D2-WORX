// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { Temporal } from "temporal-polyfill";

/**
 * Seam for retrieving the current instant in time. Inject this interface
 * rather than reading `Temporal.Now.instant()` directly so unit tests can
 * supply a deterministic {@link TestClock}.
 */
export interface IClock {
  /** Returns the current UTC {@link Temporal.Instant}. */
  getInstant(): Temporal.Instant;
}

/**
 * Production implementation of {@link IClock}. Delegates to
 * `Temporal.Now.instant()`. Bind this as the singleton `IClock` in each
 * service's composition root.
 */
export class SystemClock implements IClock {
  getInstant(): Temporal.Instant {
    return Temporal.Now.instant();
  }
}

/**
 * Test-only {@link IClock} implementation with a controllable current
 * instant. Never register in production. Construct directly in test setup
 * and inject as `IClock`.
 */
export class TestClock implements IClock {
  private _instant: Temporal.Instant;

  constructor(initial: Temporal.Instant) {
    this._instant = initial;
  }

  /**
   * The current simulated instant. Equivalent to calling
   * {@link TestClock.getInstant}.
   */
  get now(): Temporal.Instant {
    return this._instant;
  }

  getInstant(): Temporal.Instant {
    return this._instant;
  }

  /**
   * Advances the current simulated instant forward (or backward for a
   * negative duration) by the given {@link Temporal.Duration}.
   * A zero-duration advance is a no-op.
   */
  advance(duration: Temporal.Duration): void {
    this._instant = this._instant.add(duration);
  }

  /**
   * Sets the current simulated instant to an explicit value, replacing any
   * previously-advanced instant.
   */
  setTo(instant: Temporal.Instant): void {
    this._instant = instant;
  }
}
