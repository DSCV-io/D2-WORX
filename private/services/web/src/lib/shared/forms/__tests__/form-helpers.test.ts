// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, it, expect, vi } from "vitest";
import type { InputError } from "@dcsv-io/d2-result";

// Paraglide message lookup. The form-helpers module renders TKMessages
// via `m[key]()`; tests stub the module so behavior is deterministic
// without depending on the live Paraglide catalog. Each stubbed key
// returns its own raw key — assertions on rendered output read like
// echo. Unknown keys fall back to the raw key in the helper itself.
vi.mock("$lib/paraglide/messages.js", () => ({
  // Provide a small catalog of stubs used across tests. The helper does
  // a runtime `m[key]` lookup, so anything not stubbed here exercises
  // the unknown-key fallback path (returns the raw key).
  "TK.email.INVALID": () => "TK.email.INVALID",
  "TK.email.REQUIRED": () => "TK.email.REQUIRED",
  "TK.email.TOO_LONG": () => "TK.email.TOO_LONG",
  "TK.name.TOO_SHORT": () => "TK.name.TOO_SHORT",
  "TK.address.city.REQUIRED": () => "TK.address.city.REQUIRED",
  "TK.password.WEAK": (params?: { minLength?: number }) =>
    `TK.password.WEAK(minLength=${params?.minLength ?? "?"})`,
}));

// Import AFTER vi.mock so the stub takes effect.
const { mapD2Errors } = await import("../form-helpers.js");

describe("mapD2Errors (object-shape InputError)", () => {
  it("converts single-field single-error", () => {
    const errors: InputError[] = [{ field: "email", errors: [{ key: "TK.email.INVALID" }] }];
    expect(mapD2Errors(errors)).toEqual({ email: ["TK.email.INVALID"] });
  });

  it("converts single-field multiple-errors", () => {
    const errors: InputError[] = [
      {
        field: "email",
        errors: [{ key: "TK.email.REQUIRED" }, { key: "TK.email.INVALID" }],
      },
    ];
    expect(mapD2Errors(errors)).toEqual({
      email: ["TK.email.REQUIRED", "TK.email.INVALID"],
    });
  });

  it("converts multiple fields", () => {
    const errors: InputError[] = [
      { field: "email", errors: [{ key: "TK.email.REQUIRED" }] },
      { field: "name", errors: [{ key: "TK.name.TOO_SHORT" }] },
    ];
    expect(mapD2Errors(errors)).toEqual({
      email: ["TK.email.REQUIRED"],
      name: ["TK.name.TOO_SHORT"],
    });
  });

  it("merges duplicate field entries", () => {
    const errors: InputError[] = [
      { field: "email", errors: [{ key: "TK.email.REQUIRED" }] },
      { field: "email", errors: [{ key: "TK.email.INVALID" }] },
    ];
    expect(mapD2Errors(errors)).toEqual({
      email: ["TK.email.REQUIRED", "TK.email.INVALID"],
    });
  });

  it("handles dot-notation field names", () => {
    const errors: InputError[] = [
      { field: "address.city", errors: [{ key: "TK.address.city.REQUIRED" }] },
    ];
    expect(mapD2Errors(errors)).toEqual({
      "address.city": ["TK.address.city.REQUIRED"],
    });
  });

  it("returns empty object for empty input", () => {
    expect(mapD2Errors([])).toEqual({});
  });

  it("skips entries with empty field name", () => {
    const errors: InputError[] = [{ field: "", errors: [{ key: "TK.email.INVALID" }] }];
    expect(mapD2Errors(errors)).toEqual({});
  });

  it("skips entries with no error messages", () => {
    const errors: InputError[] = [{ field: "email", errors: [] }];
    expect(mapD2Errors(errors)).toEqual({});
  });

  it("renders TKMessage params via paraglide lookup", () => {
    const errors: InputError[] = [
      {
        field: "password",
        errors: [{ key: "TK.password.WEAK", params: { minLength: 12 } }],
      },
    ];
    expect(mapD2Errors(errors)).toEqual({
      password: ["TK.password.WEAK(minLength=12)"],
    });
  });

  it("falls back to raw key when paraglide does not know the key", () => {
    const errors: InputError[] = [
      { field: "email", errors: [{ key: "TK.email.UNKNOWN_BY_PARAGLIDE" }] },
    ];
    // Unknown key → renderTk returns the raw key, NOT undefined / throw.
    expect(mapD2Errors(errors)).toEqual({
      email: ["TK.email.UNKNOWN_BY_PARAGLIDE"],
    });
  });

  // -------------------------------------------------------------------
  // Regression test pinning object-destructure shape. The mismatched
  // tuple-style destructure `for (const [field, ...errors] of inputErrors)`
  // silently produces empty objects when fed the canonical
  // `{field, errors: TKMessage[]}` wire shape — every form submission
  // carrying validation errors would render no errors. The .NET-emitted
  // InputError is a sealed record serializing as a JSON object, so
  // tuple-destructure pulls `undefined` for both field and errors. This
  // pins the correct object-shape destructure against that failure mode.
  // -------------------------------------------------------------------
  it("regression: object-destructure (not tuple) — wire-shape parity with .NET", () => {
    // Synthesize an InputError exactly as the .NET gateway emits it —
    // a JSON object, NOT a 2-tuple. The previous tuple-destructure would
    // try to pull index 0 (field) and index 1+ (errors) out of an
    // object, returning undefined for both → entire result becomes {}.
    const wireShapedFromDotNet = JSON.parse(
      JSON.stringify([{ field: "email", errors: [{ key: "TK.email.INVALID" }] }]),
    ) as InputError[];

    const result = mapD2Errors(wireShapedFromDotNet);

    // With the fix, the field appears in the output map with its rendered
    // error. With the bug, this assertion fails because the result is {}.
    expect(result).toHaveProperty("email");
    expect(result.email).toEqual(["TK.email.INVALID"]);
  });
});
