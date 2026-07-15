// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import {
  SupportedLocales,
  loadSupportedLocalesConfig,
} from "../src/supported-locales.js";

describe("SupportedLocales.canonicalize", () => {
  it.each([
    ["en", "en"],
    ["EN", "en"],
    ["en-us", "en-US"],
    ["EN-US", "en-US"],
    ["en-Latn-US", "en-Latn-US"],
    ["EN-LATN-us", "en-Latn-US"],
    ["en-VARIANT1", "en-variant1"],
    ["en-x-private", "en-x-private"],
    ["fr-FR", "fr-FR"],
    ["fr-fR", "fr-FR"],
    ["zh-Hans-CN", "zh-Hans-CN"],
    ["  en-us  ", "en-US"],
  ])("%s → %s", (input, expected) => {
    expect(SupportedLocales.canonicalize(input)).toBe(expected);
  });
});

describe("SupportedLocales constructor", () => {
  it("throws on empty enabled list", () => {
    expect(() => new SupportedLocales({ enabled: [] })).toThrow(RangeError);
  });

  it("throws when enabled has only falsey entries", () => {
    expect(
      () => new SupportedLocales({ enabled: ["", "  ", null as never] }),
    ).toThrow(RangeError);
  });

  it("dedupes enabled list canonical-case-insensitively", () => {
    const sl = new SupportedLocales({ enabled: ["en-US", "EN-us", "fr-FR"] });
    expect(sl.enabled).toEqual(["en-US", "fr-FR"]);
  });

  it("language fallback resolves to FIRST dialect of the language (en-US wins over en-GB)", () => {
    const sl = new SupportedLocales({ enabled: ["en-US", "en-GB", "fr-FR"] });
    expect(sl.resolve("en")).toBe("en-US");
    expect(sl.resolve("en-AU")).toBe("en-US");
  });

  it("default is first canonical-cased entry when no explicit default", () => {
    const sl = new SupportedLocales({ enabled: ["EN-us", "fr-FR"] });
    expect(sl.default).toBe("en-US");
  });

  it("explicit default normalized to canonical case", () => {
    const sl = new SupportedLocales({
      enabled: ["en-US", "fr-FR"],
      default: "FR-fr",
    });
    expect(sl.default).toBe("fr-FR");
  });

  it("throws when default not in enabled list", () => {
    expect(
      () =>
        new SupportedLocales({
          enabled: ["en-US"],
          default: "fr-FR",
        }),
    ).toThrow(RangeError);
  });
});

describe("SupportedLocales.resolve", () => {
  const sl = new SupportedLocales({
    enabled: ["en-US", "fr-FR", "es-ES"],
  });

  it.each([
    ["exact", "en-US", "en-US"],
    ["case-insensitive", "EN-us", "en-US"],
    ["language-only fallback", "fr", "fr-FR"],
    ["region variant fallback to language", "fr-CH", "fr-FR"],
    ["unknown → default", "ja-JP", "en-US"],
    ["null → default", null, "en-US"],
    ["undefined → default", undefined, "en-US"],
    ["empty → default", "", "en-US"],
    ["whitespace → default", "   ", "en-US"],
  ])("%s '%s' → %s", (_label, input, expected) => {
    expect(sl.resolve(input)).toBe(expected);
  });
});

describe("loadSupportedLocalesConfig", () => {
  it("reads indexed env var", () => {
    const cfg = loadSupportedLocalesConfig("LOC", {
      LOC__0: "en-US",
      LOC__1: "fr-FR",
    });
    expect(cfg.enabled).toEqual(["en-US", "fr-FR"]);
  });

  it("returns empty enabled list when no env entries", () => {
    expect(loadSupportedLocalesConfig("MISSING", {}).enabled).toEqual([]);
  });
});
