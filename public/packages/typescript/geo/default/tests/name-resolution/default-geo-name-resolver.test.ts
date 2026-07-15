// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { CountryCode } from "@d2/geo-abstractions";
import { TK } from "@d2/i18n-keys";
import { beforeEach, describe, expect, it } from "vitest";

import { CountryLookup } from "../../src/countries.js";
import {
  _internalResetCache,
  DefaultGeoNameResolver,
  tryResolveCountryByName,
  tryResolveSubdivisionByName,
} from "../../src/name-resolution/default-geo-name-resolver.js";

describe("DefaultGeoNameResolver", () => {
  beforeEach(() => {
    _internalResetCache();
  });

  // §1.2 category: Input validation
  describe("country: input validation", () => {
    it("returns validationFailed for null input", () => {
      const result = tryResolveCountryByName(null as unknown as string);
      expect(result.success).toBe(false);
      expect(result.statusCode).toBe(400);
    });

    it("returns validationFailed for empty string", () => {
      const result = tryResolveCountryByName("");
      expect(result.success).toBe(false);
      expect(result.statusCode).toBe(400);
    });

    it("returns validationFailed for whitespace-only input", () => {
      const result = tryResolveCountryByName("   ");
      expect(result.success).toBe(false);
      expect(result.statusCode).toBe(400);
    });
  });

  // §1.2 category: Resource exhaustion / Security-adversarial
  describe("country: DoS guard (Predicate 0)", () => {
    it("rejects input longer than MAX_NAME_LENGTH (256)", () => {
      const oversized = "a".repeat(257);
      const result = tryResolveCountryByName(oversized);
      expect(result.success).toBe(false);
      expect(result.statusCode).toBe(400);
    });

    it("processes input at MAX_NAME_LENGTH (inclusive cap)", () => {
      const atCap = "a".repeat(256);
      const result = tryResolveCountryByName(atCap);
      // Cascade exhausts — outcome is NotFound (404), not validation failed (400).
      expect(result.success).toBe(false);
      expect(result.statusCode).toBe(404);
    });

    it("rejects 1MB oversized input in under 50ms", () => {
      const oversized = "x".repeat(1_048_576);
      const start = performance.now();
      const result = tryResolveCountryByName(oversized);
      const elapsed = performance.now() - start;
      expect(result.success).toBe(false);
      expect(elapsed).toBeLessThan(50);
    });
  });

  // §1.2 category: Domain-specific — exact match
  describe("country: Pass-1 exact match", () => {
    it("resolves display name 'United States' to US", () => {
      const result = tryResolveCountryByName("United States");
      expect(result.success).toBe(true);
      expect(result.data?.iso31661Alpha2Code).toBe(CountryCode.US);
    });

    it("resolves alpha-3 'USA' to US", () => {
      const result = tryResolveCountryByName("USA");
      expect(result.success).toBe(true);
      expect(result.data?.iso31661Alpha2Code).toBe(CountryCode.US);
    });

    it("resolves case-insensitive 'UNITED STATES' to US", () => {
      const result = tryResolveCountryByName("UNITED STATES");
      expect(result.success).toBe(true);
      expect(result.data?.iso31661Alpha2Code).toBe(CountryCode.US);
    });

    it("resolves whitespace-collapse 'United  States' to US", () => {
      const result = tryResolveCountryByName("United  States");
      expect(result.success).toBe(true);
      expect(result.data?.iso31661Alpha2Code).toBe(CountryCode.US);
    });
  });

  // §1.2 category: Domain-specific — confusable pairs
  describe("country: confusable pairs", () => {
    it.each([
      ["Niger", "NE"],
      ["Nigeria", "NG"],
      ["Iran", "IR"],
      ["Iraq", "IQ"],
      ["Slovakia", "SK"],
      ["Slovenia", "SI"],
      ["Austria", "AT"],
      ["Australia", "AU"],
      ["Chad", "TD"],
      ["Chile", "CL"],
    ])("'%s' resolves to %s (distinct)", (input, expected) => {
      const result = tryResolveCountryByName(input);
      expect(result.success).toBe(true);
      expect(result.data?.iso31661Alpha2Code).toBe(expected);
    });
  });

  // §1.2 category: Domain-specific — cascade exhausted
  describe("country: no-match cases", () => {
    it("returns notFound for an unknown random string", () => {
      const result = tryResolveCountryByName(
        "SomeRandomCountryThatDoesNotExist",
      );
      expect(result.success).toBe(false);
      expect(result.statusCode).toBe(404);
    });

    it("returns notFound for a one-char input (too short for any pass)", () => {
      const result = tryResolveCountryByName("X");
      expect(result.success).toBe(false);
      expect(result.statusCode).toBe(404);
    });
  });

  // §1.2 category: Cross-field — subdivision parent-context
  describe("subdivision: parent-context discipline", () => {
    it("'Georgia' in US resolves to US-GA", () => {
      const us = CountryLookup.byCode[CountryCode.US];
      const result = tryResolveSubdivisionByName("Georgia", us!);
      expect(result.success).toBe(true);
      expect(result.data?.iso31662Code).toBe("US-GA");
    });

    it("'Georgia' in CA returns notFound", () => {
      const ca = CountryLookup.byCode[CountryCode.CA];
      const result = tryResolveSubdivisionByName("Georgia", ca!);
      expect(result.success).toBe(false);
      expect(result.statusCode).toBe(404);
    });

    it("'California' in US resolves to US-CA", () => {
      const us = CountryLookup.byCode[CountryCode.US];
      const result = tryResolveSubdivisionByName("California", us!);
      expect(result.success).toBe(true);
      expect(result.data?.iso31662Code).toBe("US-CA");
    });
  });

  // §1.2 category: Input validation — subdivision boundary
  describe("subdivision: input validation", () => {
    it("returns validationFailed for null parent", () => {
      const result = tryResolveSubdivisionByName(
        "California",
        null as unknown as Parameters<typeof tryResolveSubdivisionByName>[1],
      );
      expect(result.success).toBe(false);
      expect(result.statusCode).toBe(400);
    });

    it("returns validationFailed for empty name", () => {
      const us = CountryLookup.byCode[CountryCode.US];
      const result = tryResolveSubdivisionByName("", us!);
      expect(result.success).toBe(false);
      expect(result.statusCode).toBe(400);
    });

    it("returns notFound for country with no subdivisions (AQ)", () => {
      const aq = CountryLookup.byCode[CountryCode.AQ];
      const result = tryResolveSubdivisionByName("AnyName", aq!);
      expect(result.success).toBe(false);
      expect(result.statusCode).toBe(404);
    });
  });

  // §1.2 category: Error propagation — traceId discipline
  it("D2Result carries traceId = undefined (resolver is request-context-free)", () => {
    const result = tryResolveCountryByName("United States");
    expect(result.traceId).toBeUndefined();
  });

  // §1.2 category: State-lifecycle — class wrapper parity
  it("class DefaultGeoNameResolver implements IGeoNameResolver", () => {
    const resolver = new DefaultGeoNameResolver();
    const result = resolver.tryResolveCountryByName("United States");
    expect(result.success).toBe(true);
    expect(result.data?.iso31661Alpha2Code).toBe(CountryCode.US);
  });

  // §1.2 category: Domain-specific — Pass-1 exact via additional name fields
  describe("country: Pass-1 exact via additional name fields", () => {
    it("resolves numeric code '840' to US", () => {
      const result = tryResolveCountryByName("840");
      expect(result.success).toBe(true);
      expect(result.data?.iso31661Alpha2Code).toBe(CountryCode.US);
    });

    it("resolves official-name alias 'Ivory Coast' to CI", () => {
      const result = tryResolveCountryByName("Ivory Coast");
      expect(result.success).toBe(true);
      expect(result.data?.iso31661Alpha2Code).toBe(CountryCode.CI);
    });

    it("resolves ampersand-token 'Trinidad & Tobago' to TT", () => {
      const result = tryResolveCountryByName("Trinidad & Tobago");
      expect(result.success).toBe(true);
      expect(result.data?.iso31661Alpha2Code).toBe(CountryCode.TT);
    });

    it("resolves 'and'-form 'Trinidad and Tobago' to TT", () => {
      const result = tryResolveCountryByName("Trinidad and Tobago");
      expect(result.success).toBe(true);
      expect(result.data?.iso31661Alpha2Code).toBe(CountryCode.TT);
    });

    it("resolves official-name 'Holy See' to VA", () => {
      const result = tryResolveCountryByName("Holy See");
      expect(result.success).toBe(true);
      expect(result.data?.iso31661Alpha2Code).toBe(CountryCode.VA);
    });

    it("resolves CJK endonym '日本' to JP", () => {
      const result = tryResolveCountryByName("日本");
      expect(result.success).toBe(true);
      expect(result.data?.iso31661Alpha2Code).toBe(CountryCode.JP);
    });

    it("resolves CJK endonym '中华人民共和国' to CN", () => {
      const result = tryResolveCountryByName("中华人民共和国");
      expect(result.success).toBe(true);
      expect(result.data?.iso31661Alpha2Code).toBe(CountryCode.CN);
    });

    it("resolves RTL Arabic endonym 'السعودية' to SA", () => {
      const result = tryResolveCountryByName("السعودية");
      expect(result.success).toBe(true);
      expect(result.data?.iso31661Alpha2Code).toBe(CountryCode.SA);
    });

    it("resolves German endonym 'Deutschland' to DE", () => {
      const result = tryResolveCountryByName("Deutschland");
      expect(result.success).toBe(true);
      expect(result.data?.iso31661Alpha2Code).toBe(CountryCode.DE);
    });
  });

  // §1.2 category: Domain-specific — NFD normalization
  describe("country: NFD normalization", () => {
    it("resolves 'Turkiye' (no diacritic) to TR via NFD-strip on 'Türkiye'", () => {
      const result = tryResolveCountryByName("Turkiye");
      expect(result.success).toBe(true);
      expect(result.data?.iso31661Alpha2Code).toBe(CountryCode.TR);
    });

    it("resolves 'TÜRKİYE' to TR (Turkish dotted İ → invariant 'i', not 'ı')", () => {
      const result = tryResolveCountryByName("TÜRKİYE");
      expect(result.success).toBe(true);
      expect(result.data?.iso31661Alpha2Code).toBe(CountryCode.TR);
    });
  });

  // §1.2 category: Domain-specific — Pass-2 startsWith ambiguity
  describe("country: Pass-2 startsWith ambiguity", () => {
    it("'Unit' (4 chars) ambiguity returns notFound", () => {
      const result = tryResolveCountryByName("Unit");
      expect(result.success).toBe(false);
      expect(result.statusCode).toBe(404);
    });

    it("'United' (6 chars) ambiguity returns notFound", () => {
      const result = tryResolveCountryByName("United");
      expect(result.success).toBe(false);
      expect(result.statusCode).toBe(404);
    });
  });

  // §1.2 category: Domain-specific — Pass-3 contains
  describe("country: Pass-3 contains (unique substring vs ambiguity)", () => {
    it("'Burma' resolves uniquely to MM via DisplayName 'Myanmar (Burma)'", () => {
      const result = tryResolveCountryByName("Burma");
      expect(result.success).toBe(true);
      expect(result.data?.iso31661Alpha2Code).toBe(CountryCode.MM);
    });

    it("'Vatican' resolves uniquely to VA (multiple name-field hits on same record)", () => {
      const result = tryResolveCountryByName("Vatican");
      expect(result.success).toBe(true);
      expect(result.data?.iso31661Alpha2Code).toBe(CountryCode.VA);
    });

    it("'Macedonia' resolves uniquely to MK via 'North Macedonia'", () => {
      const result = tryResolveCountryByName("Macedonia");
      expect(result.success).toBe(true);
      expect(result.data?.iso31661Alpha2Code).toBe(CountryCode.MK);
    });

    it("'Korea' ambiguity (KP + KR) returns notFound", () => {
      const result = tryResolveCountryByName("Korea");
      expect(result.success).toBe(false);
      expect(result.statusCode).toBe(404);
    });

    it("'Republic' ambiguity (many countries) returns notFound", () => {
      const result = tryResolveCountryByName("Republic");
      expect(result.success).toBe(false);
      expect(result.statusCode).toBe(404);
    });
  });

  // §1.2 category: Domain-specific — Pass-1 exact short-circuits Pass-3
  it("country: 'Congo' Pass-1 exact short-circuits Pass-3 contains", () => {
    // 'Congo' is OfficialName of CG; Pass-1 wins before Pass-3 surfaces
    // ambiguity with CD (Congo - Kinshasa) + CG.
    const result = tryResolveCountryByName("Congo");
    expect(result.success).toBe(true);
    expect(result.data?.iso31661Alpha2Code).toBe(CountryCode.CG);
  });

  // §1.2 category: Domain-specific — Pass-4 Levenshtein fuzzy
  describe("country: Pass-4 Levenshtein fuzzy", () => {
    it("'Astralia' (Lev=1) resolves to AU", () => {
      const result = tryResolveCountryByName("Astralia");
      expect(result.success).toBe(true);
      expect(result.data?.iso31661Alpha2Code).toBe(CountryCode.AU);
    });

    it("'Iram' (4 chars) is NOT rescued by Pass-4 (length-gate) — returns notFound", () => {
      const result = tryResolveCountryByName("Iram");
      expect(result.success).toBe(false);
      expect(result.statusCode).toBe(404);
    });
  });

  // §1.2 category: Resource exhaustion — Pass-4 banded bounding under load
  it("country: 256-char input completes Pass-4 within 500ms (banded bound)", () => {
    const atCap = "q".repeat(256);
    const start = performance.now();
    const result = tryResolveCountryByName(atCap);
    const elapsed = performance.now() - start;
    expect(result.success).toBe(false);
    expect(elapsed).toBeLessThan(500);
  });

  // §1.2 category: Domain-specific — subdivision Pass-3 ambiguity
  describe("subdivision: Pass-3 contains", () => {
    it("'Carolina' in US ambiguity (NC + SC) returns notFound", () => {
      const us = CountryLookup.byCode[CountryCode.US];
      const result = tryResolveSubdivisionByName("Carolina", us!);
      expect(result.success).toBe(false);
      expect(result.statusCode).toBe(404);
    });

    it("'North Carolina' exact-match short-circuits Pass-3 → US-NC", () => {
      const us = CountryLookup.byCode[CountryCode.US];
      const result = tryResolveSubdivisionByName("North Carolina", us!);
      expect(result.success).toBe(true);
      expect(result.data?.iso31662Code).toBe("US-NC");
    });

    it("'South Carolina' exact-match short-circuits Pass-3 → US-SC", () => {
      const us = CountryLookup.byCode[CountryCode.US];
      const result = tryResolveSubdivisionByName("South Carolina", us!);
      expect(result.success).toBe(true);
      expect(result.data?.iso31662Code).toBe("US-SC");
    });
  });

  // §1.2 category: Domain-specific — subdivision parent-context discipline
  describe("subdivision: parent-context discipline", () => {
    it("'Washington' in US resolves uniquely to US-WA (DC uses 'District of Columbia')", () => {
      const us = CountryLookup.byCode[CountryCode.US];
      const result = tryResolveSubdivisionByName("Washington", us!);
      expect(result.success).toBe(true);
      expect(result.data?.iso31662Code).toBe("US-WA");
    });

    it("'Ottawa' in US returns notFound (Ottawa is Canadian, not US subdivision)", () => {
      const us = CountryLookup.byCode[CountryCode.US];
      const result = tryResolveSubdivisionByName("Ottawa", us!);
      expect(result.success).toBe(false);
      expect(result.statusCode).toBe(404);
    });
  });

  // §1.2 category: Domain-specific — subdivision Pass-4 fuzzy + skip
  describe("subdivision: Pass-4 Levenshtein fuzzy", () => {
    it("'Califrnia' (Lev=1) in US resolves to US-CA", () => {
      const us = CountryLookup.byCode[CountryCode.US];
      const result = tryResolveSubdivisionByName("Califrnia", us!);
      expect(result.success).toBe(true);
      expect(result.data?.iso31662Code).toBe("US-CA");
    });

    it("'Texxas' (Lev=1) in US resolves to US-TX", () => {
      const us = CountryLookup.byCode[CountryCode.US];
      const result = tryResolveSubdivisionByName("Texxas", us!);
      expect(result.success).toBe(true);
      expect(result.data?.iso31662Code).toBe("US-TX");
    });

    it("'Texs' (4 chars) in US is NOT rescued by Pass-4 — returns notFound", () => {
      const us = CountryLookup.byCode[CountryCode.US];
      const result = tryResolveSubdivisionByName("Texs", us!);
      expect(result.success).toBe(false);
      expect(result.statusCode).toBe(404);
    });
  });

  // §1.2 category: Domain-specific — subdivision NFD normalization
  describe("subdivision: NFD normalization", () => {
    it("'Sao Paulo' (no diacritic) in BR resolves to BR-SP", () => {
      const br = CountryLookup.byCode[CountryCode.BR];
      const result = tryResolveSubdivisionByName("Sao Paulo", br!);
      expect(result.success).toBe(true);
      expect(result.data?.iso31662Code).toBe("BR-SP");
    });

    it("'Ile-de-France' (no diacritic) in FR resolves to FR-IDF", () => {
      const fr = CountryLookup.byCode[CountryCode.FR];
      const result = tryResolveSubdivisionByName("Ile-de-France", fr!);
      expect(result.success).toBe(true);
      expect(result.data?.iso31662Code).toBe("FR-IDF");
    });
  });

  // §1.2 category: Resource exhaustion — subdivision oversized input
  it("subdivision: oversized input returns validationFailed (TOO_LONG)", () => {
    const us = CountryLookup.byCode[CountryCode.US];
    const oversized = "a".repeat(257);
    const result = tryResolveSubdivisionByName(oversized, us!);
    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(400);
  });

  // §1.2 category: Domain-specific — TK key wiring on every NotFound branch
  // Regression guard for §G render-bug class: keys must be snake_case catalog
  // keys, NOT dot-path strings (dot-paths render verbatim instead of resolving).
  describe("country: TK message keys", () => {
    it("Pass-3 ambiguity carries NAME_RESOLUTION_AMBIGUOUS (snake key, not dot-path)", () => {
      const result = tryResolveCountryByName("Korea");
      expect(result.messages).toHaveLength(1);
      expect(result.messages![0]!.key).toBe(
        "geo_errors_name_resolution_ambiguous",
      );
    });

    it("cascade-exhausted carries NAME_RESOLUTION_NOT_FOUND (snake key, not dot-path)", () => {
      const result = tryResolveCountryByName(
        "ZzzzNoSuchCountryNameAnywhereZzzz",
      );
      expect(result.messages).toHaveLength(1);
      expect(result.messages![0]!.key).toBe(
        "geo_errors_name_resolution_not_found",
      );
    });

    // Render guard — keys match the TK catalog constants, confirming no dot-path regression.
    // A full Paraglide-rendered string check would require a translator fixture not
    // available in this test context; asserting key === TK.*.key is the structural guard:
    // if the source is reverted to a raw "TK.Geo.Errors.…" string the TK constant
    // comparison fails because TK.geo.errors.NAME_RESOLUTION_AMBIGUOUS.key ===
    // "geo_errors_name_resolution_ambiguous", not the dot-path.
    it("TK constants match expected catalog keys (render-guard: key ≠ dot-path)", () => {
      expect(TK.common.errors.NOT_NULL_VIOLATION.key).toBe(
        "common_errors_NOT_NULL_VIOLATION",
      );
      expect(TK.common.errors.TOO_LONG.key).toBe("common_errors_TOO_LONG");
      expect(TK.geo.errors.NAME_RESOLUTION_NOT_FOUND.key).toBe(
        "geo_errors_name_resolution_not_found",
      );
      expect(TK.geo.errors.NAME_RESOLUTION_AMBIGUOUS.key).toBe(
        "geo_errors_name_resolution_ambiguous",
      );
    });
  });
});
