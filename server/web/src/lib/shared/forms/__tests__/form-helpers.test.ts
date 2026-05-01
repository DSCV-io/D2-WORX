import { describe, it, expect } from "vitest";
import { mapD2Errors } from "../form-helpers.js";
import type { InputError } from "@d2/result";

describe("mapD2Errors", () => {
  it("converts single field single error", () => {
    const errors: InputError[] = [["email", "Invalid email"]];
    expect(mapD2Errors(errors)).toEqual({ email: ["Invalid email"] });
  });

  it("converts single field multiple errors", () => {
    const errors: InputError[] = [["email", "Required", "Must be valid"]];
    expect(mapD2Errors(errors)).toEqual({
      email: ["Required", "Must be valid"],
    });
  });

  it("converts multiple fields", () => {
    const errors: InputError[] = [
      ["email", "Required"],
      ["name", "Too short"],
    ];
    expect(mapD2Errors(errors)).toEqual({
      email: ["Required"],
      name: ["Too short"],
    });
  });

  it("merges duplicate field entries", () => {
    const errors: InputError[] = [
      ["email", "Required"],
      ["email", "Must be valid"],
    ];
    expect(mapD2Errors(errors)).toEqual({
      email: ["Required", "Must be valid"],
    });
  });

  it("handles dot-notation field names", () => {
    const errors: InputError[] = [["address.city", "Required"]];
    expect(mapD2Errors(errors)).toEqual({
      "address.city": ["Required"],
    });
  });

  it("returns empty object for empty input", () => {
    expect(mapD2Errors([])).toEqual({});
  });

  it("skips entries with empty field name", () => {
    const errors: InputError[] = [["", "Some error"]];
    expect(mapD2Errors(errors)).toEqual({});
  });

  it("skips entries with no error messages", () => {
    const errors: InputError[] = [["email"]];
    expect(mapD2Errors(errors)).toEqual({});
  });
});
