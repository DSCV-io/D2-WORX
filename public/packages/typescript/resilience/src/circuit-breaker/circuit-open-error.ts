// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
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
