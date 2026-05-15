// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

/**
 * Thrown by `CircuitBreaker.execute()` when the circuit is currently open
 * (cooldown active). Mirrors .NET `CircuitOpenException`.
 */
export class CircuitOpenError extends Error {
  constructor(message = "circuit is open") {
    super(message);
    this.name = "CircuitOpenError";
  }
}
