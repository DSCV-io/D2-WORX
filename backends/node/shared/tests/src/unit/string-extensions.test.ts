import { describe, it, expect } from "vitest";
import {
  cleanStr,
  cleanAndValidateEmail,
  cleanAndValidatePhoneNumber,
  getNormalizedStrForHashing,
} from "@d2/utilities";

// ---------------------------------------------------------------------------
// cleanStr
// ---------------------------------------------------------------------------

describe("cleanStr", () => {
  it.each([
    ["  hello  ", "hello"],
    ["hello", "hello"],
    ["  HELLO  ", "HELLO"],
    ["hello world", "hello world"],
    ["  hello   world  ", "hello world"],
    ["hello\t\tworld", "hello world"],
    ["hello\n\nworld", "hello world"],
    ["  hello  \n  world  ", "hello world"],
  ])('cleans "%s" to "%s"', (input: string, expected: string) => {
    expect(cleanStr(input)).toBe(expected);
  });

  it.each([null, undefined, "", " ", "   ", "\t", "\n", "  \t  \n  "])(
    "returns undefined for %j",
    (input: string | null | undefined) => {
      expect(cleanStr(input)).toBeUndefined();
    },
  );
});

// ---------------------------------------------------------------------------
// cleanAndValidateEmail
// ---------------------------------------------------------------------------

describe("cleanAndValidateEmail", () => {
  it.each([
    ["test@example.com", "test@example.com"],
    ["TEST@EXAMPLE.COM", "test@example.com"],
    ["  test@example.com  ", "test@example.com"],
    ["user.name@example.com", "user.name@example.com"],
    ["user+tag@example.co.uk", "user+tag@example.co.uk"],
  ])('validates and cleans "%s" to "%s"', (input: string, expected: string) => {
    expect(cleanAndValidateEmail(input)).toBe(expected);
  });

  it.each([
    null,
    undefined,
    "",
    "   ",
    "notavalidemail",
    "@example.com",
    "test@",
    "test@.com",
    "test @example.com",
    "test@ example.com",
  ])("throws for invalid email %j", (input: string | null | undefined) => {
    expect(() => cleanAndValidateEmail(input)).toThrow("Invalid email address format.");
  });
});

// ---------------------------------------------------------------------------
// cleanAndValidatePhoneNumber
// ---------------------------------------------------------------------------

describe("cleanAndValidatePhoneNumber", () => {
  it.each([
    ["1234567", "1234567"],
    ["123456789012345", "123456789012345"],
    ["+1 (555) 123-4567", "15551234567"],
    ["555-123-4567", "5551234567"],
    ["(555) 123 4567", "5551234567"],
    ["+44 20 7946 0958", "442079460958"],
    ["  555.123.4567  ", "5551234567"],
  ])('cleans "%s" to "%s"', (input: string, expected: string) => {
    expect(cleanAndValidatePhoneNumber(input)).toBe(expected);
  });

  it.each([null, undefined, "", "   "])(
    'throws "cannot be null or empty" for %j',
    (input: string | null | undefined) => {
      expect(() => cleanAndValidatePhoneNumber(input)).toThrow(
        "Phone number cannot be null or empty.",
      );
    },
  );

  it.each(["abc", "---", "()"])(
    'throws "Invalid phone number format" for "%s"',
    (input: string) => {
      expect(() => cleanAndValidatePhoneNumber(input)).toThrow("Invalid phone number format.");
    },
  );

  it.each([
    "123456", // 6 digits (too short)
    "1234567890123456", // 16 digits (too long)
  ])('throws "between 7 and 15 digits" for "%s"', (input: string) => {
    expect(() => cleanAndValidatePhoneNumber(input)).toThrow(
      "Phone number must be between 7 and 15 digits in length.",
    );
  });
});

// ---------------------------------------------------------------------------
// getNormalizedStrForHashing
// ---------------------------------------------------------------------------

describe("getNormalizedStrForHashing", () => {
  it("normalizes valid parts", () => {
    expect(getNormalizedStrForHashing(["Hello", "World", "Test"])).toBe("hello|world|test");
  });

  it("replaces null parts with empty strings", () => {
    expect(getNormalizedStrForHashing(["Hello", null, "Test"])).toBe("hello||test");
  });

  it("replaces undefined parts with empty strings", () => {
    expect(getNormalizedStrForHashing(["Hello", undefined, "Test"])).toBe("hello||test");
  });

  it("replaces whitespace-only parts with empty strings", () => {
    expect(getNormalizedStrForHashing(["Hello", "   ", "Test"])).toBe("hello||test");
  });

  it("converts mixed case to lowercase", () => {
    expect(getNormalizedStrForHashing(["HELLO", "WoRlD", "TeSt"])).toBe("hello|world|test");
  });

  it("trims and normalizes whitespace within parts", () => {
    expect(getNormalizedStrForHashing(["  Hello  ", "World   Test", "  "])).toBe(
      "hello|world test|",
    );
  });

  it("returns empty string for empty array", () => {
    expect(getNormalizedStrForHashing([])).toBe("");
  });

  it("produces consistent results for identical content", () => {
    const parts = ["Hello", "World", null, "Test"];
    expect(getNormalizedStrForHashing(parts)).toBe(getNormalizedStrForHashing(parts));
  });

  it("produces different results for different content", () => {
    expect(getNormalizedStrForHashing(["Hello", "World"])).not.toBe(
      getNormalizedStrForHashing(["Hello", "Universe"]),
    );
  });
});
