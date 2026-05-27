// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, it, expect } from "vitest";
import { derivePrimaryLocaleTag } from "../../src/transformers/primary-locale-tag.js";

// -----------------------------------------------------------------------
// Fixture catalogs — mimic real CLDR likelySubtags + availableLocales subsets
// covering every code path the algorithm walks. Each catalog scoped to what
// the test needs (keeps fixtures small + obvious).
// -----------------------------------------------------------------------

const realCldrLikelySubtags = new Map<string, string>([
  ["zh-HK", "zh-Hant-HK"],
  ["zh-MO", "zh-Hant-MO"],
  ["zh-TW", "zh-Hant-TW"],
  ["zh-Hant", "zh-Hant-TW"],
  ["sr-ME", "sr-Latn-ME"],
  ["sr", "sr-Cyrl-RS"],
  ["no", "no-Latn-NO"],
  ["nb", "nb-Latn-NO"],
  ["ms-CC", "ms-Arab-CC"],
  ["ms", "ms-Latn-MY"],
  ["fil", "fil-Latn-PH"],
  ["tl", "tl-Latn-PH"],
  ["sq", "sq-Latn-AL"],
  ["cmn", "cmn-Hans-CN"],
]);

const realCldrAvailableLocales = new Set<string>([
  // Bare languages (illustrative subset)
  "en",
  "fr",
  "zh",
  "sr",
  "ms",
  "fil",
  "nb",
  "pt",
  // Lang-Region forms ACTUALLY shipped
  "en-US",
  "en-CC",
  "en-MH",
  "en-MP",
  "en-NR",
  "en-NU",
  "en-PW",
  "en-TK",
  "en-TV",
  "en-VU",
  "en-WS",
  "fr-FR",
  "fr-WF",
  "fr-PM",
  "pt-TL",
  "pt-BR",
  "nb-SJ",
  "nb-NO",
  // Lang-Script-Region forms ACTUALLY shipped (script canonical)
  "zh-Hant-HK",
  "zh-Hant-MO",
  "zh-Hant-TW",
  "zh-Hans-SG",
  "zh-Hans-CN",
  "sr-Latn-ME",
  "sr-Cyrl-RS",
  // Filipino derived form
  "fil-PH",
]);

const baseCatalog = {
  cldrLikelySubtags: realCldrLikelySubtags,
  cldrAvailableLocaleTags: realCldrAvailableLocales,
};

// -----------------------------------------------------------------------
// Pass 2 — CLDR script-subtag canonical expansion
// -----------------------------------------------------------------------

describe("derivePrimaryLocaleTag — CLDR likelySubtags script expansion", () => {
  it("HK: zh + HK -> zh-Hant-HK (CLDR canonical script)", () => {
    const tag = derivePrimaryLocaleTag(
      {
        regionAlpha2: "HK",
        primaryLanguageCode: "zh",
        candidateLocaleTags: ["en-HK", "yue-HK", "zh-Hans-HK", "zh-Hant-HK"],
      },
      baseCatalog,
    );
    expect(tag).toBe("zh-Hant-HK");
  });

  it("MO: zh + MO -> zh-Hant-MO", () => {
    const tag = derivePrimaryLocaleTag(
      {
        regionAlpha2: "MO",
        primaryLanguageCode: "zh",
        candidateLocaleTags: ["en-MO", "pt-MO", "zh-Hans-MO", "zh-Hant-MO"],
      },
      baseCatalog,
    );
    expect(tag).toBe("zh-Hant-MO");
  });

  it("TW: zh + TW -> zh-Hant-TW", () => {
    const tag = derivePrimaryLocaleTag(
      {
        regionAlpha2: "TW",
        primaryLanguageCode: "zh",
        candidateLocaleTags: ["trv-TW", "zh-Hant-TW"],
      },
      baseCatalog,
    );
    expect(tag).toBe("zh-Hant-TW");
  });

  it("ME: sr + ME -> sr-Latn-ME (Latin since 2009 constitutionally)", () => {
    const tag = derivePrimaryLocaleTag(
      {
        regionAlpha2: "ME",
        primaryLanguageCode: "sr",
        candidateLocaleTags: ["sr-Cyrl-ME", "sr-Latn-ME"],
      },
      baseCatalog,
    );
    expect(tag).toBe("sr-Latn-ME");
  });
});

// -----------------------------------------------------------------------
// Pass 3 / 4 — Language alias mapping (tl -> fil, no -> nb, cmn -> zh)
// -----------------------------------------------------------------------

describe("derivePrimaryLocaleTag — language alias resolution", () => {
  it("PH: tl + PH -> fil-PH (Tagalog alias to Filipino; fil-PH in catalog)", () => {
    const tag = derivePrimaryLocaleTag(
      {
        regionAlpha2: "PH",
        primaryLanguageCode: "tl",
        candidateLocaleTags: ["ceb-PH", "en-PH", "es-PH", "fil-PH"],
      },
      baseCatalog,
    );
    expect(tag).toBe("fil-PH");
  });

  it("SJ: no + SJ -> nb-SJ (Norwegian macro-language alias to Bokmaal)", () => {
    const tag = derivePrimaryLocaleTag(
      {
        regionAlpha2: "SJ",
        primaryLanguageCode: "no",
        candidateLocaleTags: ["nb-SJ"],
      },
      baseCatalog,
    );
    expect(tag).toBe("nb-SJ");
  });

  it("SG: cmn + SG -> zh-Hans-SG (Mandarin alias to zh, per-region script scan)", () => {
    // SG specific: CLDR has no `zh-SG` likelySubtags entry (handled implicitly
    // via `zh-Hans` script branch). The algorithm's per-region script-expanded
    // candidate scan picks `zh-Hans-SG` from the country's locale list.
    const tag = derivePrimaryLocaleTag(
      {
        regionAlpha2: "SG",
        primaryLanguageCode: "cmn",
        candidateLocaleTags: ["en-SG", "ms-SG", "ta-SG", "zh-Hans-SG"],
      },
      baseCatalog,
    );
    expect(tag).toBe("zh-Hans-SG");
  });
});

// -----------------------------------------------------------------------
// Pass 6 — Fallback to country's candidate locale list (639-3 primary lang)
// -----------------------------------------------------------------------

describe("derivePrimaryLocaleTag — 639-3 fallback to en-{region}", () => {
  const cases: Array<[string, string, string]> = [
    ["CC", "ms", "en-CC"], // ms-CC + ms-Arab-CC both NOT in catalog -> en-CC
    ["MH", "mh", "en-MH"],
    ["MP", "fil", "en-MP"], // fil-MP NOT in catalog (only en-MP), fil-PH lives at PH
    ["NR", "na", "en-NR"],
    ["NU", "niu", "en-NU"],
    ["PW", "pau", "en-PW"],
    ["TK", "tkl", "en-TK"],
    ["TV", "tvl", "en-TV"],
    ["VU", "bi", "en-VU"],
    ["WS", "sm", "en-WS"],
  ];

  for (const [region, lang, expected] of cases) {
    it(`${region}: ${lang} + ${region} -> ${expected}`, () => {
      const tag = derivePrimaryLocaleTag(
        {
          regionAlpha2: region,
          primaryLanguageCode: lang,
          candidateLocaleTags:
            region === "VU" ? [`en-${region}`, "fr-VU"] : [`en-${region}`],
        },
        baseCatalog,
      );
      expect(tag).toBe(expected);
    });
  }
});

describe("derivePrimaryLocaleTag — Pass 6 fallback to first locale when en-{region} absent", () => {
  it("TL: tet + TL -> pt-TL (no en-TL in candidates; pt-TL is first)", () => {
    const tag = derivePrimaryLocaleTag(
      {
        regionAlpha2: "TL",
        primaryLanguageCode: "tet",
        candidateLocaleTags: ["pt-TL"],
      },
      baseCatalog,
    );
    expect(tag).toBe("pt-TL");
  });

  it("WF: wls + WF -> fr-WF (no en-WF; fr-WF only candidate)", () => {
    const tag = derivePrimaryLocaleTag(
      {
        regionAlpha2: "WF",
        primaryLanguageCode: "wls",
        candidateLocaleTags: ["fr-WF"],
      },
      baseCatalog,
    );
    expect(tag).toBe("fr-WF");
  });
});

// -----------------------------------------------------------------------
// TF special case — CLDR has no fr-TF entry; overlay supplies it -> resolves
// -----------------------------------------------------------------------

describe("derivePrimaryLocaleTag — TF requires locale overlay to resolve", () => {
  it("TF: fr + TF -> null when fr-TF NOT in catalog (overlay not yet applied)", () => {
    // baseCatalog deliberately omits fr-TF. TF has no candidateLocaleTags from
    // CLDR availableLocales (no entries with regionSubtag=TF).
    const tag = derivePrimaryLocaleTag(
      {
        regionAlpha2: "TF",
        primaryLanguageCode: "fr",
        candidateLocaleTags: [],
      },
      baseCatalog,
    );
    expect(tag).toBeNull();
  });

  it("TF: fr + TF -> fr-TF when overlay adds fr-TF to availableLocales", () => {
    const catalogWithOverlay = {
      cldrLikelySubtags: realCldrLikelySubtags,
      cldrAvailableLocaleTags: new Set([...realCldrAvailableLocales, "fr-TF"]),
    };
    const tag = derivePrimaryLocaleTag(
      {
        regionAlpha2: "TF",
        primaryLanguageCode: "fr",
        candidateLocaleTags: [],
      },
      catalogWithOverlay,
    );
    expect(tag).toBe("fr-TF");
  });
});

// -----------------------------------------------------------------------
// Clean cases — no script-expansion needed; lang-region already in catalog
// -----------------------------------------------------------------------

describe("derivePrimaryLocaleTag — clean lang-region cases (no expansion)", () => {
  it("US: en + US -> en-US (direct match)", () => {
    const tag = derivePrimaryLocaleTag(
      {
        regionAlpha2: "US",
        primaryLanguageCode: "en",
        candidateLocaleTags: ["en-US"],
      },
      baseCatalog,
    );
    expect(tag).toBe("en-US");
  });

  it("FR: fr + FR -> fr-FR (direct match)", () => {
    const tag = derivePrimaryLocaleTag(
      {
        regionAlpha2: "FR",
        primaryLanguageCode: "fr",
        candidateLocaleTags: ["fr-FR"],
      },
      baseCatalog,
    );
    expect(tag).toBe("fr-FR");
  });

  it("BR: pt + BR -> pt-BR (direct match)", () => {
    const tag = derivePrimaryLocaleTag(
      {
        regionAlpha2: "BR",
        primaryLanguageCode: "pt",
        candidateLocaleTags: ["pt-BR"],
      },
      baseCatalog,
    );
    expect(tag).toBe("pt-BR");
  });
});

// -----------------------------------------------------------------------
// Edge cases — null primary lang, empty catalog, no candidate fallback
// -----------------------------------------------------------------------

describe("derivePrimaryLocaleTag — edge cases", () => {
  it("returns null when primaryLanguageCode is null AND no candidate locales", () => {
    const tag = derivePrimaryLocaleTag(
      {
        regionAlpha2: "ZZ",
        primaryLanguageCode: null,
        candidateLocaleTags: [],
      },
      baseCatalog,
    );
    expect(tag).toBeNull();
  });

  it("falls back to en-{region} when primaryLanguageCode is null but candidates exist", () => {
    const tag = derivePrimaryLocaleTag(
      {
        regionAlpha2: "CC",
        primaryLanguageCode: null,
        candidateLocaleTags: ["en-CC"],
      },
      baseCatalog,
    );
    expect(tag).toBe("en-CC");
  });

  it("falls back to first sorted candidate when en-{region} absent and primary lang fails", () => {
    const tag = derivePrimaryLocaleTag(
      {
        regionAlpha2: "ZZ",
        primaryLanguageCode: "xx", // unknown lang, no expansion
        candidateLocaleTags: ["qa-ZZ", "ab-ZZ"], // 'ab-ZZ' first when sorted
      },
      {
        cldrLikelySubtags: new Map(),
        cldrAvailableLocaleTags: new Set(),
      },
    );
    expect(tag).toBe("ab-ZZ");
  });

  it("returns the plain lang-region tag when it exists in candidateLocaleTags (catalog-miss path)", () => {
    // Simulates catalog where the EXPANDED form isn't shipped but the PLAIN
    // tag is in the country's locales list — still resolvable.
    const tag = derivePrimaryLocaleTag(
      {
        regionAlpha2: "XX",
        primaryLanguageCode: "yy",
        candidateLocaleTags: ["yy-XX"],
      },
      {
        cldrLikelySubtags: new Map(),
        cldrAvailableLocaleTags: new Set(),
      },
    );
    expect(tag).toBe("yy-XX");
  });

  it("returns null when nothing matches at all", () => {
    const tag = derivePrimaryLocaleTag(
      {
        regionAlpha2: "ZZ",
        primaryLanguageCode: "xx",
        candidateLocaleTags: [],
      },
      {
        cldrLikelySubtags: new Map(),
        cldrAvailableLocaleTags: new Set(),
      },
    );
    expect(tag).toBeNull();
  });

  it("handles malformed likelySubtags expansion (returns plain fallback)", () => {
    const tag = derivePrimaryLocaleTag(
      {
        regionAlpha2: "ZZ",
        primaryLanguageCode: "xx",
        candidateLocaleTags: ["xx-ZZ"],
      },
      {
        // Expansion is only 2 parts (not lang-Script-Region) — algorithm skips
        // expanded form and falls back to plain `xx-ZZ`.
        cldrLikelySubtags: new Map([["xx-ZZ", "xx-ZZ"]]),
        cldrAvailableLocaleTags: new Set(),
      },
    );
    expect(tag).toBe("xx-ZZ");
  });
});
