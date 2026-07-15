// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import { tk } from "@dcsv-io/d2-i18n-abstractions";
import { SupportedLocales } from "../src/supported-locales.js";
import { Translator } from "../src/translator.js";

const locales = new SupportedLocales({ enabled: ["en-US", "fr-FR"] });
const catalogs = {
  "en-US": {
    "TK.greeting": "Hello, {name}!",
    "TK.shared": "shared en",
  },
  "fr-FR": {
    "TK.greeting": "Bonjour, {name} !",
  },
};
const translator = new Translator(locales, catalogs);

describe("Translator.t", () => {
  it("renders the requested locale + substitutes params", () => {
    expect(translator.t("fr-FR", tk("TK.greeting", { name: "Alice" }))).toBe(
      "Bonjour, Alice !",
    );
  });

  it("falls back to default locale on missing key in requested locale", () => {
    expect(translator.t("fr-FR", tk("TK.shared"))).toBe("shared en");
  });

  it("returns key verbatim when neither locale has the entry", () => {
    expect(translator.t("en-US", tk("TK.unknown"))).toBe("TK.unknown");
  });

  it("does NOT throw on null locale (treats as default)", () => {
    expect(translator.t(null as unknown as string, tk("TK.shared"))).toBe(
      "shared en",
    );
  });

  it("leaves unmatched placeholders literal when params present but key missing", () => {
    expect(Translator.render("Hello, {name}!", { other: "X" })).toBe(
      "Hello, {name}!",
    );
  });

  it("leaves unmatched placeholders literal when params undefined", () => {
    expect(translator.t("en-US", tk("TK.greeting"))).toBe("Hello, {name}!");
  });

  it("ignores extra params not in template", () => {
    expect(
      translator.t("en-US", tk("TK.greeting", { name: "X", extra: "Y" })),
    ).toBe("Hello, X!");
  });

  it("renders empty/whitespace templates as-is", () => {
    expect(Translator.render("", { name: "Alice" })).toBe("");
  });

  it("renders without params", () => {
    expect(Translator.render("hello", undefined)).toBe("hello");
  });

  it("supports number/boolean params", () => {
    expect(Translator.render("Count: {n}", { n: 42 })).toBe("Count: 42");
    expect(Translator.render("On: {b}", { b: true })).toBe("On: true");
  });
});
