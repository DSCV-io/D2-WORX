// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

/**
 * Three-state circuit breaker state. Mirrors .NET `CircuitState`:
 * `Closed` → `Open` → (after cooldown) `HalfOpen` → `Closed`/`Open`.
 */
export const CircuitState = {
  Closed: "closed",
  Open: "open",
  HalfOpen: "half-open",
} as const;

export type CircuitState = (typeof CircuitState)[keyof typeof CircuitState];
