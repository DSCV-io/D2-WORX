// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import {
  ALL_ERROR_CATEGORIES,
  ErrorCategoryWire,
  type ErrorCategory,
} from "../src/index.js";

// The nine closed wire strings — the cross-runtime source of truth. Mirrors
// the .NET D2.Shared.ErrorCodes.Category.ErrorCategory enum's wire set.
const EXPECTED_WIRES: readonly ErrorCategory[] = [
  "conflict",
  "infrastructure_unavailable",
  "internal_error",
  "not_found",
  "partial_success",
  "payload_too_large",
  "policy_denied",
  "rate_limited",
  "validation_failure",
];

describe("@d2/error-category", () => {
  it("ALL_ERROR_CATEGORIES has exactly the nine closed wire strings", () => {
    expect([...ALL_ERROR_CATEGORIES].sort()).toEqual(
      [...EXPECTED_WIRES].sort(),
    );
  });

  it("ALL_ERROR_CATEGORIES has no duplicates", () => {
    expect(new Set(ALL_ERROR_CATEGORIES).size).toBe(
      ALL_ERROR_CATEGORIES.length,
    );
  });

  it("ErrorCategoryWire maps every PascalCase member to a known wire string", () => {
    const wireSet = new Set<string>(ALL_ERROR_CATEGORIES);
    for (const wire of Object.values(ErrorCategoryWire)) {
      expect(wireSet.has(wire)).toBe(true);
    }
  });

  it("ErrorCategoryWire has one member per category", () => {
    expect(Object.keys(ErrorCategoryWire)).toHaveLength(
      ALL_ERROR_CATEGORIES.length,
    );
  });

  it("ErrorCategoryWire member names are PascalCase of their wire string", () => {
    const toMember = (wire: string): string =>
      wire
        .split("_")
        .map((s) => s.charAt(0).toUpperCase() + s.slice(1).toLowerCase())
        .join("");
    for (const [member, wire] of Object.entries(ErrorCategoryWire)) {
      expect(member).toBe(toMember(wire));
    }
  });

  it("specific members carry the expected wire strings", () => {
    expect(ErrorCategoryWire.NotFound).toBe("not_found");
    expect(ErrorCategoryWire.ValidationFailure).toBe("validation_failure");
    expect(ErrorCategoryWire.InfrastructureUnavailable).toBe(
      "infrastructure_unavailable",
    );
    expect(ErrorCategoryWire.PartialSuccess).toBe("partial_success");
  });
});
