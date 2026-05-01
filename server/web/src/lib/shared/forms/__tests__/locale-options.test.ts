import { describe, it, expect } from "vitest";
import { localesToOptions, type LocaleOption } from "../locale-options.js";

describe("locale-options", () => {
  // ---------------------------------------------------------------------------
  // localesToOptions (Geo ref data)
  // ---------------------------------------------------------------------------
  describe("localesToOptions", () => {
    const mockLocales = {
      "en-US": {
        ietfBcp47Tag: "en-US",
        name: "English (United States)",
        endonym: "English (United States)",
        languageIso6391Code: "en",
        countryIso31661Alpha2Code: "US",
      },
      "fr-CA": {
        ietfBcp47Tag: "fr-CA",
        name: "French (Canada)",
        endonym: "Français (Canada)",
        languageIso6391Code: "fr",
        countryIso31661Alpha2Code: "CA",
      },
      "ja-JP": {
        ietfBcp47Tag: "ja-JP",
        name: "Japanese (Japan)",
        endonym: "日本語 (日本)",
        languageIso6391Code: "ja",
        countryIso31661Alpha2Code: "JP",
      },
    };

    it("transforms Geo locale DTOs into locale options", () => {
      const result = localesToOptions(mockLocales, ["en-US", "fr-CA", "ja-JP"]);

      expect(result).toHaveLength(3);
      expect(result[0]).toEqual({
        code: "en-US",
        endonym: "English (United States)",
        flag: "/flags/4x3/us.svg",
      });
      expect(result[1]).toEqual({
        code: "fr-CA",
        endonym: "Français (Canada)",
        flag: "/flags/4x3/ca.svg",
      });
      expect(result[2]).toEqual({
        code: "ja-JP",
        endonym: "日本語 (日本)",
        flag: "/flags/4x3/jp.svg",
      });
    });

    it("filters to only enabled locales", () => {
      const result = localesToOptions(mockLocales, ["en-US"]);

      expect(result).toHaveLength(1);
      expect(result[0]!.code).toBe("en-US");
    });

    it("emits code-only fallback for enabled locales not in ref data", () => {
      const result = localesToOptions(mockLocales, ["en-US", "de-DE"]);

      expect(result).toHaveLength(2);
      expect(result[0]!.code).toBe("en-US");
      expect(result[0]!.endonym).toBe("English (United States)");
      expect(result[1]).toEqual({ code: "de-DE", endonym: "de-DE", flag: "/flags/4x3/de.svg" });
    });

    it("returns code-only fallbacks when no enabled locales match ref data", () => {
      const result = localesToOptions(mockLocales, ["de-DE", "it-IT"]);

      expect(result).toHaveLength(2);
      expect(result[0]).toEqual({ code: "de-DE", endonym: "de-DE", flag: "/flags/4x3/de.svg" });
      expect(result[1]).toEqual({ code: "it-IT", endonym: "it-IT", flag: "/flags/4x3/it.svg" });
    });

    it("preserves order of enabled locales", () => {
      const result = localesToOptions(mockLocales, ["ja-JP", "en-US", "fr-CA"]);

      expect(result.map((o: LocaleOption) => o.code)).toEqual(["ja-JP", "en-US", "fr-CA"]);
    });
  });
});
