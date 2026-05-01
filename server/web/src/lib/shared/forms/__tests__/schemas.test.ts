import { describe, it, expect } from "vitest";
import {
  nameField,
  emailField,
  phoneField,
  phoneFieldOptional,
  postcodeField,
  streetField,
  urlField,
  currencyField,
  passwordField,
} from "../schemas.js";

describe("nameField", () => {
  const schema = nameField();

  it("accepts valid names", () => {
    expect(schema.safeParse("Jane").success).toBe(true);
    expect(schema.safeParse("O'Brien").success).toBe(true);
    expect(schema.safeParse("San Francisco").success).toBe(true);
  });

  it("rejects empty string", () => {
    expect(schema.safeParse("").success).toBe(false);
  });

  it("rejects string exceeding default max (255)", () => {
    expect(schema.safeParse("a".repeat(256)).success).toBe(false);
  });

  it("respects custom max length", () => {
    const short = nameField(10);
    expect(short.safeParse("a".repeat(10)).success).toBe(true);
    expect(short.safeParse("a".repeat(11)).success).toBe(false);
  });

  it("rejects whitespace-only strings", () => {
    expect(schema.safeParse("   ").success).toBe(false);
    expect(schema.safeParse(" \t ").success).toBe(false);
  });

  it("trims leading/trailing whitespace before validation", () => {
    const result = schema.safeParse("  Jane  ");
    expect(result.success).toBe(true);
    if (result.success) expect(result.data).toBe("Jane");
  });
});

describe("emailField", () => {
  const schema = emailField();

  it("accepts valid emails", () => {
    expect(schema.safeParse("user@example.com").success).toBe(true);
    expect(schema.safeParse("user+tag@sub.domain.com").success).toBe(true);
  });

  it("rejects invalid emails", () => {
    expect(schema.safeParse("not-an-email").success).toBe(false);
    expect(schema.safeParse("@missing-local.com").success).toBe(false);
  });

  it("rejects empty string", () => {
    expect(schema.safeParse("").success).toBe(false);
  });

  it("rejects overly long emails (>254 chars)", () => {
    expect(schema.safeParse("a".repeat(250) + "@x.com").success).toBe(false);
  });

  it("rejects whitespace-only strings", () => {
    expect(schema.safeParse("   ").success).toBe(false);
  });

  it("trims whitespace and lowercases before validation", () => {
    const result = schema.safeParse("  User@Example.COM  ");
    expect(result.success).toBe(true);
    if (result.success) expect(result.data).toBe("user@example.com");
  });
});

describe("phoneField", () => {
  const schema = phoneField();

  it("accepts valid E.164 numbers", () => {
    expect(schema.safeParse("+12025551234").success).toBe(true);
    expect(schema.safeParse("+442071234567").success).toBe(true);
  });

  it("rejects invalid numbers", () => {
    expect(schema.safeParse("12345").success).toBe(false);
    expect(schema.safeParse("not-a-number").success).toBe(false);
  });

  it("rejects empty string", () => {
    expect(schema.safeParse("").success).toBe(false);
  });

  it("rejects whitespace-only strings", () => {
    expect(schema.safeParse("   ").success).toBe(false);
  });

  it("rejects phone exceeding 20 chars", () => {
    expect(schema.safeParse("+1" + "5".repeat(19)).success).toBe(false);
  });
});

describe("phoneFieldOptional", () => {
  const schema = phoneFieldOptional();

  it("accepts empty string", () => {
    expect(schema.safeParse("").success).toBe(true);
  });

  it("accepts valid number", () => {
    expect(schema.safeParse("+12025551234").success).toBe(true);
  });

  it("rejects invalid non-empty string", () => {
    expect(schema.safeParse("abc").success).toBe(false);
  });

  it("trims whitespace-only to empty (treated as blank)", () => {
    const result = schema.safeParse("   ");
    expect(result.success).toBe(true);
  });

  it("rejects phone exceeding 20 chars", () => {
    expect(schema.safeParse("+1" + "5".repeat(19)).success).toBe(false);
  });
});

describe("postcodeField", () => {
  it("accepts any non-empty string without country", () => {
    const schema = postcodeField();
    expect(schema.safeParse("12345").success).toBe(true);
    expect(schema.safeParse("K1A 0B1").success).toBe(true);
  });

  it("rejects empty string", () => {
    const schema = postcodeField();
    expect(schema.safeParse("").success).toBe(false);
  });

  it("rejects whitespace-only strings", () => {
    const schema = postcodeField();
    expect(schema.safeParse("  ").success).toBe(false);
  });

  it("validates US postal codes", () => {
    const schema = postcodeField("US");
    expect(schema.safeParse("12345").success).toBe(true);
    expect(schema.safeParse("12345-6789").success).toBe(true);
    expect(schema.safeParse("ABCDE").success).toBe(false);
  });

  it("validates CA postal codes", () => {
    const schema = postcodeField("CA");
    expect(schema.safeParse("K1A 0B1").success).toBe(true);
    expect(schema.safeParse("12345").success).toBe(false);
  });
});

describe("streetField", () => {
  const schema = streetField();

  it("accepts valid addresses", () => {
    expect(schema.safeParse("123 Main St").success).toBe(true);
  });

  it("rejects empty string", () => {
    expect(schema.safeParse("").success).toBe(false);
  });

  it("rejects whitespace-only strings", () => {
    expect(schema.safeParse("   ").success).toBe(false);
  });

  it("trims whitespace before validation", () => {
    const result = schema.safeParse("  123 Main St  ");
    expect(result.success).toBe(true);
    if (result.success) expect(result.data).toBe("123 Main St");
  });

  it("respects max length (255)", () => {
    expect(schema.safeParse("a".repeat(256)).success).toBe(false);
  });
});

describe("urlField", () => {
  const schema = urlField();

  it("accepts valid URLs", () => {
    expect(schema.safeParse("https://example.com").success).toBe(true);
  });

  it("accepts empty string (optional)", () => {
    expect(schema.safeParse("").success).toBe(true);
  });

  it("rejects invalid URLs", () => {
    expect(schema.safeParse("not a url").success).toBe(false);
  });
});

describe("passwordField", () => {
  const schema = passwordField();

  it("accepts valid password with mixed characters", () => {
    expect(schema.safeParse("MySecretPass1").success).toBe(true);
  });

  it("accepts exactly 12 characters", () => {
    expect(schema.safeParse("abcdef123456").success).toBe(true);
  });

  it("accepts exactly 128 characters (max)", () => {
    expect(schema.safeParse("a".repeat(128)).success).toBe(true);
  });

  it("rejects empty string", () => {
    expect(schema.safeParse("").success).toBe(false);
  });

  it("rejects fewer than 12 characters", () => {
    expect(schema.safeParse("short").success).toBe(false);
    expect(schema.safeParse("a".repeat(11)).success).toBe(false);
  });

  it("rejects more than 128 characters", () => {
    expect(schema.safeParse("a".repeat(129)).success).toBe(false);
  });

  it("rejects numeric-only strings", () => {
    expect(schema.safeParse("123456789012").success).toBe(false);
    expect(schema.safeParse("000000000000").success).toBe(false);
  });

  it("rejects date-like strings (digits + date separators only)", () => {
    expect(schema.safeParse("2025-01-01-01").success).toBe(false);
    expect(schema.safeParse("25/01/1997/01").success).toBe(false);
    expect(schema.safeParse("01.02.2025.03").success).toBe(false);
    expect(schema.safeParse("2025 01 01 01").success).toBe(false);
  });

  it("accepts digits mixed with letters (not numeric-only)", () => {
    expect(schema.safeParse("abc123def456").success).toBe(true);
  });

  it("rejects whitespace-only strings of 12+ chars", () => {
    // Whitespace-only matches the date-like pattern (digits + separators including spaces)
    // but also fails min-length after trim in practice. The refine catches spaces-only.
    expect(schema.safeParse("            ").success).toBe(false);
  });

  it("respects custom min/max", () => {
    const short = passwordField(8, 20);
    expect(short.safeParse("abcdefgh").success).toBe(true);
    expect(short.safeParse("abcdefg").success).toBe(false);
    expect(short.safeParse("a".repeat(21)).success).toBe(false);
  });

  it("accepts passwords with special characters", () => {
    expect(schema.safeParse("P@ssw0rd!2025").success).toBe(true);
    expect(schema.safeParse("!@#$%^&*()ab").success).toBe(true);
  });
});

describe("currencyField", () => {
  const schema = currencyField();

  it("accepts valid ISO 4217 codes", () => {
    expect(schema.safeParse("USD").success).toBe(true);
    expect(schema.safeParse("EUR").success).toBe(true);
  });

  it("rejects lowercase", () => {
    expect(schema.safeParse("usd").success).toBe(false);
  });

  it("rejects wrong length", () => {
    expect(schema.safeParse("US").success).toBe(false);
    expect(schema.safeParse("USDX").success).toBe(false);
  });
});
