// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Tests for name-transform utilities — direct-unit, adversarial (§1.2).
//
// Covers:
//   - toSnake: lowerCamel→snake, already-snake, single char, empty string,
//     embedded digit, leading cap, all-caps. Pins both regex branches.
//   - toPascal: snake→Pascal, lowerCamel→Pascal, empty, single char,
//     leading underscore, all-caps. Pins both branches.
//   - toKebab: lowerCamel→kebab, PascalCase (concern-folder) →kebab, already-kebab,
//     single char, empty, embedded digit, all-lowercase. Pins both regex branches.

import { describe, it, expect } from "vitest";
import { toSnake, toPascal, toKebab } from "../src/lib/name-transforms.js";

describe("toSnake", () => {
  it("converts lowerCamelCase to lower_snake_case", () => {
    expect(toSnake("myFieldName")).toBe("my_field_name");
  });

  it("converts PascalCase to lower_snake_case", () => {
    expect(toSnake("MyFieldName")).toBe("my_field_name");
  });

  it("is idempotent on already-snake strings", () => {
    expect(toSnake("my_field_name")).toBe("my_field_name");
  });

  it("handles a single lowercase character", () => {
    expect(toSnake("x")).toBe("x");
  });

  it("handles a single uppercase character", () => {
    expect(toSnake("X")).toBe("x");
  });

  it("handles empty string", () => {
    expect(toSnake("")).toBe("");
  });

  it("handles embedded digit before uppercase (id2Code → id2_code)", () => {
    expect(toSnake("id2Code")).toBe("id2_code");
  });

  it("handles leading digit", () => {
    expect(toSnake("2Fields")).toBe("2_fields");
  });

  it("does not insert extra underscores for all-lowercase", () => {
    expect(toSnake("lowercase")).toBe("lowercase");
  });

  it("handles consecutive uppercase letters (acronyms)", () => {
    // The regex /([a-z0-9])([A-Z])/ fires only on a lower-to-upper transition.
    // "XMLParser": X→M→L are upper-to-upper (no firing); L→P is upper-to-upper
    // (no firing); the whole prefix lowercases to "xmlparser".
    // This is the documented spike behavior — we pin the actual output here.
    expect(toSnake("XMLParser")).toBe("xmlparser");
  });
});

describe("toPascal", () => {
  it("converts lower_snake_case to PascalCase", () => {
    expect(toPascal("my_field_name")).toBe("MyFieldName");
  });

  it("converts lowerCamelCase to PascalCase", () => {
    expect(toPascal("myFieldName")).toBe("MyFieldName");
  });

  it("is idempotent on already-PascalCase strings", () => {
    expect(toPascal("MyFieldName")).toBe("MyFieldName");
  });

  it("handles a single lowercase character", () => {
    expect(toPascal("x")).toBe("X");
  });

  it("handles a single uppercase character", () => {
    expect(toPascal("X")).toBe("X");
  });

  it("handles empty string", () => {
    expect(toPascal("")).toBe("");
  });

  it("handles leading underscore (e.g. _field → Field)", () => {
    expect(toPascal("_field")).toBe("Field");
  });

  it("handles all-lowercase single word", () => {
    expect(toPascal("field")).toBe("Field");
  });

  it("handles digit as first char of a snake segment", () => {
    // "id_2code" → toPascal → "Id2code" (digit matches [a-z0-9] in the regex)
    expect(toPascal("id_2code")).toBe("Id2code");
  });

  it("round-trips with toSnake for a normal camel identifier", () => {
    // toPascal(toSnake("myFieldName")) === "MyFieldName"
    const original = "myFieldName";
    expect(toPascal(toSnake(original))).toBe("MyFieldName");
  });
});

describe("toKebab", () => {
  it("converts lowerCamelCase op name to kebab (getKeyring → get-keyring)", () => {
    expect(toKebab("getKeyring")).toBe("get-keyring");
  });

  it("converts a PascalCase concern to kebab (CaCertificate → ca-certificate)", () => {
    expect(toKebab("CaCertificate")).toBe("ca-certificate");
  });

  it("lowercases a single-word PascalCase concern (Signing → signing)", () => {
    expect(toKebab("Signing")).toBe("signing");
  });

  it("is idempotent on already-kebab strings", () => {
    expect(toKebab("get-ca-certificate")).toBe("get-ca-certificate");
  });

  it("handles a single lowercase character", () => {
    expect(toKebab("x")).toBe("x");
  });

  it("handles a single uppercase character", () => {
    expect(toKebab("X")).toBe("x");
  });

  it("handles empty string", () => {
    expect(toKebab("")).toBe("");
  });

  it("handles embedded digit before uppercase (id2Code → id2-code)", () => {
    expect(toKebab("id2Code")).toBe("id2-code");
  });

  it("does not insert extra hyphens for all-lowercase", () => {
    expect(toKebab("sealing")).toBe("sealing");
  });
});
