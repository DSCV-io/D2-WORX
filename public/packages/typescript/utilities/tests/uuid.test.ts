// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import { uuidv7 } from "../src/uuid.js";
import { UUID_RE } from "../src/regex.js";

// Canonical UUIDv7 shape: version nibble `7`, RFC 4122 variant `[89ab]`.
const UUIDV7_RE =
  /^[0-9a-f]{8}-[0-9a-f]{4}-7[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/;

describe("uuidv7", () => {
  it("mints a well-formed, version-7 UUID (default clock)", () => {
    const id = uuidv7();
    expect(id).toMatch(UUIDV7_RE);
    // Also passes the general canonical-UUID gate the parse helpers use.
    expect(UUID_RE.test(id)).toBe(true);
  });

  it("sets the version nibble to 7 and the variant to [89ab]", () => {
    const id = uuidv7();
    expect(id[14]).toBe("7");
    expect(["8", "9", "a", "b"]).toContain(id[19]);
  });

  it("is unique across successive calls (default clock)", () => {
    const a = uuidv7();
    expect(uuidv7()).not.toBe(a);
  });

  it("encodes the injected clock as the 48-bit big-endian timestamp prefix", () => {
    // A fixed 48-bit epoch-ms value → deterministic first 12 hex digits.
    const fixedMs = 0x0123456789ab;
    const expectedPrefix = fixedMs.toString(16).padStart(12, "0"); // "0123456789ab"

    const id = uuidv7(() => fixedMs);

    // First 8 hex = bytes[0..3]; next 4 hex (after the first dash) = bytes[4..5].
    expect(id.slice(0, 8)).toBe(expectedPrefix.slice(0, 8)); // "01234567"
    expect(id.slice(9, 13)).toBe(expectedPrefix.slice(8, 12)); // "89ab"
    // The rest still carries a valid version/variant.
    expect(id).toMatch(UUIDV7_RE);
  });

  it("keeps the random tail unique even under a frozen clock", () => {
    const frozen = () => 1_700_000_000_000;
    const a = uuidv7(frozen);
    const b = uuidv7(frozen);
    // Same timestamp prefix, different random suffix.
    expect(a.slice(0, 13)).toBe(b.slice(0, 13));
    expect(a).not.toBe(b);
  });

  it("encodes the minimum (zero) timestamp as an all-zero prefix", () => {
    const id = uuidv7(() => 0);
    expect(id.slice(0, 8)).toBe("00000000");
    expect(id.slice(9, 13)).toBe("0000");
    expect(id).toMatch(UUIDV7_RE);
  });
});
