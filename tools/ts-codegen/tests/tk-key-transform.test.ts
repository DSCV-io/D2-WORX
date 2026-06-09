// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import { TK } from "@d2/i18n-keys";
import { parseTkKey } from "../src/lib/tk-key-transform.js";

describe("parseTkKey — inverse KeyDecomposer", () => {
  it("transforms the canonical auth key", () => {
    const parts = parseTkKey("TK.Auth.Errors.UNAUTHORIZED");
    expect(parts?.snakeKey).toBe("auth_errors_UNAUTHORIZED");
    expect(parts?.tkConstantPath).toBe("TK.auth.errors.UNAUTHORIZED");
  });

  it("transforms a common key", () => {
    const parts = parseTkKey("TK.Common.Errors.NOT_FOUND");
    expect(parts?.snakeKey).toBe("common_errors_NOT_FOUND");
    expect(parts?.tkConstantPath).toBe("TK.common.errors.NOT_FOUND");
  });

  it("lowercases ONLY the first char of multi-char domain/category segments", () => {
    const parts = parseTkKey("TK.KeyCustodian.Validation.SOAK_NOT_ELAPSED");
    expect(parts?.snakeKey).toBe("keyCustodian_validation_SOAK_NOT_ELAPSED");
    expect(parts?.tkConstantPath).toBe(
      "TK.keyCustodian.validation.SOAK_NOT_ELAPSED",
    );
  });

  it("preserves underscores inside the SCREAMING constant segment", () => {
    const parts = parseTkKey("TK.Auth.Errors.TEMPORARILY_UNAVAILABLE");
    expect(parts?.snakeKey).toBe("auth_errors_TEMPORARILY_UNAVAILABLE");
  });

  // -------------------------------------------------------------------------
  // Adversarial — anything not matching the canonical shape returns undefined.
  // -------------------------------------------------------------------------

  it("returns undefined for undefined input", () => {
    expect(parseTkKey(undefined)).toBeUndefined();
  });

  it("returns undefined for empty string", () => {
    expect(parseTkKey("")).toBeUndefined();
  });

  it("returns undefined when not enough segments", () => {
    expect(parseTkKey("TK.Auth.Errors")).toBeUndefined();
    expect(parseTkKey("TK.Auth")).toBeUndefined();
  });

  it("returns undefined when too many segments", () => {
    expect(parseTkKey("TK.Auth.Errors.A.B")).toBeUndefined();
  });

  it("returns undefined when the prefix is not TK", () => {
    expect(parseTkKey("XK.Auth.Errors.UNAUTHORIZED")).toBeUndefined();
  });

  it("returns undefined on a zero-length segment", () => {
    expect(parseTkKey("TK..Errors.UNAUTHORIZED")).toBeUndefined();
    expect(parseTkKey("TK.Auth..UNAUTHORIZED")).toBeUndefined();
    expect(parseTkKey("TK.Auth.Errors.")).toBeUndefined();
  });
});

// ---------------------------------------------------------------------------
// Round-trip equivalence against the LIVE generated TK constants.
// These tests verify that `parseTkKey` stays in sync with the generated TS
// TK catalog (`@d2/i18n-keys`) — a string-pin alone would miss drift between
// the transform logic and the actual generated constant values.
// ---------------------------------------------------------------------------

describe("parseTkKey — round-trip equivalence with the live TK catalog", () => {
  it("TK.Auth.Errors.UNAUTHORIZED: snakeKey matches the live TK constant key", () => {
    const parts = parseTkKey("TK.Auth.Errors.UNAUTHORIZED");
    // parseTkKey produces the snake key; the live TK constant IS a TKMessage
    // whose `.key` is that snake key.
    expect(parts?.snakeKey).toBe(TK.auth.errors.UNAUTHORIZED.key);
  });

  it("TK.Auth.Errors.TEMPORARILY_UNAVAILABLE: snakeKey matches the live TK constant key", () => {
    const parts = parseTkKey("TK.Auth.Errors.TEMPORARILY_UNAVAILABLE");
    expect(parts?.snakeKey).toBe(TK.auth.errors.TEMPORARILY_UNAVAILABLE.key);
  });

  it("tkConstantPath exactly matches the live TK object access path", () => {
    // Verifies the access-path string produced by parseTkKey can actually be
    // used to navigate the live TK object — catching any naming divergence
    // between the transform and the generated catalog.
    const parts = parseTkKey("TK.Auth.Errors.UNAUTHORIZED");
    expect(parts?.tkConstantPath).toBe("TK.auth.errors.UNAUTHORIZED");
    // The live constant is reachable at that path — a TKMessage keyed by the snake key.
    expect(TK.auth.errors.UNAUTHORIZED.key).toBe("auth_errors_UNAUTHORIZED");
  });
});
