// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";

import { createStreetAddress, normalizeForHash } from "../src/index.js";

describe("StreetAddress", () => {
  describe("createStreetAddress — happy path", () => {
    it("all 5 lines → Ok, case preserved", () => {
      const r = createStreetAddress(
        "123 Main St",
        "Apt 4B",
        "Brooklyn",
        "NY 11201",
        "USA",
      );
      expect(r.success).toBe(true);
      const a = r.data!;
      expect(a.line1).toBe("123 Main St");
      expect(a.line2).toBe("Apt 4B");
      expect(a.line3).toBe("Brooklyn");
      expect(a.line4).toBe("NY 11201");
      expect(a.line5).toBe("USA");
      expect(a.hashId).toMatch(/^v1\.[0-9a-f]{64}$/);
    });

    it("only line1 → Ok with rest undefined", () => {
      const r = createStreetAddress("Only Line");
      expect(r.success).toBe(true);
      expect(r.data!.line2).toBeUndefined();
      expect(r.data!.line5).toBeUndefined();
    });

    it("line1 + line5 (gap) → Ok — no gap rule", () => {
      const r = createStreetAddress(
        "L1",
        undefined,
        undefined,
        undefined,
        "L5",
      );
      expect(r.success).toBe(true);
      expect(r.data!.line1).toBe("L1");
      expect(r.data!.line5).toBe("L5");
    });
  });

  describe("createStreetAddress — line1 required", () => {
    it.each([undefined, "", "   ", "\t\n\r"])(
      "line1=%j → ValidationFailed",
      (l) => {
        const r = createStreetAddress(l);
        expect(r.success).toBe(false);
        expect(r.messages[0]!.key).toBe(
          "geo_validation_address_line1_required",
        );
      },
    );

    it("line1 only punctuation → ValidationFailed after strip", () => {
      expect(createStreetAddress("....").success).toBe(false);
    });
  });

  describe("Stored form — normalization", () => {
    it("decorative punctuation stripped", () => {
      const r = createStreetAddress("123 Main St., Apt. 4!");
      expect(r.data!.line1).toBe("123 Main St Apt 4");
    });

    it("semantic punctuation preserved (hyphens / apostrophes / # / & / parens)", () => {
      const r = createStreetAddress("O'Connor's #4 & Co (Suite-B)");
      expect(r.data!.line1).toBe("O'Connor's #4 & Co (Suite-B)");
    });

    it("internal whitespace collapsed to single space", () => {
      expect(createStreetAddress("123    Main    St").data!.line1).toBe(
        "123 Main St",
      );
    });

    it("outer whitespace trimmed", () => {
      expect(createStreetAddress("   123 Main St   ").data!.line1).toBe(
        "123 Main St",
      );
    });
  });

  describe("HashId — case dedup", () => {
    it("different case → same hashId", () => {
      const a = createStreetAddress("123 Main St").data!.hashId;
      const b = createStreetAddress("123 main st").data!.hashId;
      const c = createStreetAddress("123 MAIN ST").data!.hashId;
      expect(a).toBe(b);
      expect(a).toBe(c);
    });
  });

  describe("HashId — Latin diacritic dedup via NFD", () => {
    it("Café vs Cafe → same hashId", () => {
      expect(createStreetAddress("Café").data!.hashId).toBe(
        createStreetAddress("Cafe").data!.hashId,
      );
    });

    it("Zürich vs Zurich → same hashId", () => {
      expect(createStreetAddress("Zürich").data!.hashId).toBe(
        createStreetAddress("Zurich").data!.hashId,
      );
    });
  });

  describe("HashId — invariants + determinism", () => {
    it("starts with v1.", () => {
      expect(createStreetAddress("123 Main St").data!.hashId).toMatch(/^v1\./);
    });

    it("length 67", () => {
      expect(createStreetAddress("123 Main St").data!.hashId).toHaveLength(67);
    });

    it("deterministic across calls", () => {
      const r1 = createStreetAddress("123 Main St", "Apt 4");
      const r2 = createStreetAddress("123 Main St", "Apt 4");
      expect(r1.data!.hashId).toBe(r2.data!.hashId);
    });

    it("populating different slots produces distinct hashes", () => {
      const r1 = createStreetAddress("Line 1");
      const r2 = createStreetAddress("Line 1", "Line 2");
      expect(r1.data!.hashId).not.toBe(r2.data!.hashId);
    });
  });

  describe("normalizeForHash — non-Latin scripts preserved (Amendment 56)", () => {
    it("Cyrillic Москва → МОСКВА (uppercase preserved)", () => {
      expect(normalizeForHash("Москва")).toBe("МОСКВА");
    });

    it("CJK 东京 → preserved (caseless)", () => {
      expect(normalizeForHash("东京")).toBe("东京");
    });

    it("Greek Αθήνα → ΑΘΗΝΑ (combining marks stripped, base preserved)", () => {
      expect(normalizeForHash("Αθήνα")).toBe("ΑΘΗΝΑ");
    });

    it("Arabic الرياض → preserved (caseless)", () => {
      expect(normalizeForHash("الرياض")).toBe("الرياض");
    });
  });

  describe("normalizeForHash — adversarial inputs (CRLF / NUL / TAB / emoji / BiDi)", () => {
    it("emoji stripped (surrogate-pair correct via /u flag)", () => {
      expect(normalizeForHash("💩 Address")).toBe(" ADDRESS");
    });

    it("emoji-only input → empty", () => {
      expect(normalizeForHash("💩🌍🚀")).toBe("");
    });

    it("all punctuation → empty", () => {
      expect(normalizeForHash("....,,,;;;:::!!!???")).toBe("");
    });

    it("CRLF stripped", () => {
      const out = normalizeForHash("123 Main\r\nINJECTED");
      expect(out).not.toContain("\r");
      expect(out).not.toContain("\n");
    });

    it("NUL byte stripped", () => {
      const out = normalizeForHash("123 Main\0INJECTED");
      expect(out).not.toContain("\0");
    });

    it("TAB stripped", () => {
      const out = normalizeForHash("123 Main\t\tApt");
      expect(out).not.toContain("\t");
    });

    it("zero-width joiner stripped", () => {
      // U+200D ZWJ — Format category, dropped by category filter.
      expect(normalizeForHash("Main‍St")).toBe("MAINST");
    });

    it("BiDi override stripped", () => {
      // U+202E RIGHT-TO-LEFT OVERRIDE — Format category.
      expect(normalizeForHash("Hello‮World")).toBe("HELLOWORLD");
    });

    it("mixed-script with hyphen + digits — hyphen stripped", () => {
      expect(normalizeForHash("123 Москва-Centre")).toBe("123 МОСКВАCENTRE");
    });
  });

  describe("Stored form — CRLF / NUL / TAB collapse", () => {
    it("CRLF in line1 collapsed/stripped from stored form", () => {
      const r = createStreetAddress("123 Main\r\nINJECTED LOG LINE");
      expect(r.success).toBe(true);
      expect(r.data!.line1).not.toContain("\r");
      expect(r.data!.line1).not.toContain("\n");
      expect(r.data!.line1).toBe("123 Main INJECTED LOG LINE");
    });

    it("NUL byte in line1 stripped from stored", () => {
      const r = createStreetAddress("123 Main\0INJECTED");
      expect(r.success).toBe(true);
      expect(r.data!.line1).not.toContain("\0");
    });

    it("TAB in line1 collapsed to space in stored", () => {
      const r = createStreetAddress("123 Main\t\tApt");
      expect(r.success).toBe(true);
      expect(r.data!.line1).toBe("123 Main Apt");
    });
  });

  describe("Boundary inputs", () => {
    it("single-char line1 → Ok", () => {
      const r = createStreetAddress("A");
      expect(r.success).toBe(true);
      expect(r.data!.line1).toBe("A");
    });

    it("1000-char input does not hang", () => {
      const huge = "A".repeat(1000);
      const r = createStreetAddress(huge);
      expect(r.success).toBe(true);
      expect(r.data!.line1).toHaveLength(1000);
    });
  });

  describe("normalizeForHash — single char + oversized", () => {
    it("single letter preserved", () => {
      expect(normalizeForHash("a")).toBe("A");
    });

    it("1000-char input does not hang", () => {
      const out = normalizeForHash("a".repeat(1000));
      expect(out).toBe("A".repeat(1000));
    });

    it("typical US address", () => {
      expect(normalizeForHash("123 Main St")).toBe("123 MAIN ST");
    });
  });
});
