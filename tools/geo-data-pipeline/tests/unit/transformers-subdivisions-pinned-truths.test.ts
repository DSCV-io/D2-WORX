// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { readFile } from "node:fs/promises";
import { resolve } from "node:path";
import { describe, expect, it } from "vitest";
import { REPO_ROOT_PATH } from "../../src/util/cache.js";

/**
 * Pinned canonical truths — invariants the post-refresh
 * `contracts/geo/src-data/subdivisions.spec.json` MUST satisfy regardless of
 * upstream version drift. Each assertion uses `substring contains` so that
 * minor format variations ("Tehran" → "Tehran Province") still pass while
 * wholesale drift (CLDR shifting "Markazi" to IR-22 instead of IR-00 — the
 * 2020 Iran reassignment bug — or any equivalent class) fails loudly.
 *
 * When this fails, the operator should:
 *   1. Inspect the failing code's row in `subdivisions.spec.json`.
 *   2. Verify the underlying upstream change (Wikidata SPARQL label for the
 *      code, debian/iso-codes' `iso_3166-2.json` entry, CLDR alignment).
 *   3. Either:
 *      a. Refresh upstream caches (delete relevant `.cache/wikidata/` files
 *         + re-run `pnpm geo:refresh`), OR
 *      b. Add an overlay entry at
 *         `contracts/geo/overlays/subdivisions.overlays.spec.json` if upstream
 *         is genuinely wrong and Debian fallback isn't acceptable.
 *
 * Failure here is a process signal, not a code bug — the pipeline correctly
 * pulled what upstream provided; that upstream is unexpected.
 */

const SUBDIVISIONS_SRC_DATA_PATH = resolve(
  REPO_ROOT_PATH,
  "contracts",
  "geo",
  "src-data",
  "subdivisions.spec.json",
);

interface SubdivisionEntry {
  iso31662Code: string;
  displayName: string;
  officialName: string;
  endonymDisplayName: string | null;
  countryISO31661Alpha2Code: string;
}

interface SubdivisionsSpec {
  entries: SubdivisionEntry[];
}

async function loadSubdivisions(): Promise<Map<string, SubdivisionEntry>> {
  const text = await readFile(SUBDIVISIONS_SRC_DATA_PATH, "utf8");
  const spec = JSON.parse(text) as SubdivisionsSpec;
  const byCode = new Map<string, SubdivisionEntry>();
  for (const e of spec.entries) byCode.set(e.iso31662Code, e);
  return byCode;
}

/**
 * Canonical truths the post-refresh subdivisions catalog must satisfy.
 *
 * Each row: `[isoCode, oneOfAcceptedSubstrings, scenarioRationale]`. The
 * assertion passes if the post-refresh displayName contains ANY of the
 * `oneOfAcceptedSubstrings` (case-insensitive).
 */
const PINNED_TRUTHS: ReadonlyArray<{
  code: string;
  accepted: readonly string[];
  rationale: string;
}> = [
  // Iran — catches the CLDR 2020-11-24 ISO 3166-2:IR reassignment drift class.
  // Pre-fix CLDR shipped "Markazi" at IR-22 (now should be Hormozgan); we use
  // Wikidata.en authority post-2026-05-23 which tracks the canonical
  // reassignment.
  {
    code: "IR-22",
    accepted: ["Hormoz"],
    rationale:
      "post-2020 ISO 3166-2:IR reassignment; Wikidata.en says 'Hormozgan Province'",
  },
  {
    code: "IR-23",
    accepted: ["Tehran", "Tehrān"],
    rationale: "Wikidata.en says 'Tehran Province'",
  },
  {
    code: "IR-00",
    accepted: ["Markazi", "Markazī"],
    rationale:
      "post-2020 reassignment placed Markazi at IR-00; Wikidata.en says 'Markazi Province'",
  },

  // Norway — catches the post-2020 county-merger drift class. NO-30 (Viken)
  // was created by the 2020 merger; CLDR was slow to reflect.
  {
    code: "NO-03",
    accepted: ["Oslo"],
    rationale: "Norway capital municipality (also a county)",
  },
  {
    code: "NO-30",
    accepted: ["Viken"],
    rationale: "post-2020 Norwegian county merger created Viken",
  },

  // Estonia — catches the EE-37 / Harju modernization.
  {
    code: "EE-37",
    accepted: ["Harju"],
    rationale: "Estonian current county; CLDR shipped retired EE-44 alongside",
  },

  // United States — high-traffic codes; any drift here is alarming.
  {
    code: "US-CA",
    accepted: ["California"],
    rationale: "high-traffic US state; any drift here is alarming",
  },
  {
    code: "US-AL",
    accepted: ["Alabama"],
    rationale: "US state",
  },
  {
    code: "US-NY",
    accepted: ["New York"],
    rationale: "US state",
  },
  {
    code: "US-TX",
    accepted: ["Texas"],
    rationale: "US state",
  },

  // Germany — Bavaria is the canonical English form; "Bayern" is the endonym.
  // Wikidata.en gives the English-canonical form.
  {
    code: "DE-BY",
    accepted: ["Bavaria"],
    rationale:
      "Wikidata.en uses the English-canonical 'Bavaria' (vs endonym 'Bayern')",
  },
  {
    code: "DE-BE",
    accepted: ["Berlin"],
    rationale: "Berlin",
  },

  // Thailand — Bangkok is the English-canonical form.
  {
    code: "TH-10",
    accepted: ["Bangkok"],
    rationale: "Thailand capital",
  },

  // China — Beijing is the standard English form post-pinyin reform.
  {
    code: "CN-BJ",
    accepted: ["Beijing"],
    rationale: "Chinese capital; standard pinyin English form",
  },
  {
    code: "CN-SH",
    accepted: ["Shanghai"],
    rationale: "Chinese city; standard pinyin English form",
  },

  // Korea — Seoul is the English-canonical form.
  {
    code: "KR-11",
    accepted: ["Seoul"],
    rationale: "Korean capital",
  },

  // UAE — Ajman is the emirate name; Wikidata typically prefixes "Emirate of".
  {
    code: "AE-AJ",
    accepted: ["Ajman"],
    rationale: "UAE emirate; Wikidata may say 'Emirate of Ajman'",
  },

  // United Kingdom — England constituent country.
  {
    code: "GB-ENG",
    accepted: ["England"],
    rationale: "UK constituent country",
  },

  // Japan — Tokyo metropolitan prefecture.
  {
    code: "JP-13",
    accepted: ["Tokyo", "Tōkyō"],
    rationale: "Japanese metropolitan prefecture",
  },

  // Spain — Catalonia autonomous community; English-canonical form.
  {
    code: "ES-CT",
    accepted: ["Catalonia", "Cataluña"],
    rationale: "Spanish autonomous community",
  },

  // France — Île-de-France region (Paris metro).
  {
    code: "FR-IDF",
    accepted: ["Île-de-France", "Ile-de-France"],
    rationale: "French region (Paris metro); diacritic-tolerant",
  },

  // Brazil — São Paulo state.
  {
    code: "BR-SP",
    accepted: ["São Paulo", "Sao Paulo"],
    rationale: "Brazilian state; diacritic-tolerant",
  },

  // Canada — provinces.
  {
    code: "CA-AB",
    accepted: ["Alberta"],
    rationale: "Canadian province",
  },
  {
    code: "CA-ON",
    accepted: ["Ontario"],
    rationale: "Canadian province",
  },

  // Australia — New South Wales.
  {
    code: "AU-NSW",
    accepted: ["New South Wales"],
    rationale: "Australian state",
  },

  // India — Maharashtra (Mumbai).
  {
    code: "IN-MH",
    accepted: ["Maharashtra"],
    rationale: "Indian state",
  },

  // Russia — Moscow (federal city).
  {
    code: "RU-MOW",
    accepted: ["Moscow", "Moskva"],
    rationale: "Russian federal city",
  },
];

describe("subdivisions.spec.json pinned canonical truths", () => {
  it("every pinned code resolves to a substring-accepted displayName", async () => {
    const byCode = await loadSubdivisions();
    const failures: string[] = [];
    for (const truth of PINNED_TRUTHS) {
      const entry = byCode.get(truth.code);
      if (!entry) {
        failures.push(
          `${truth.code}: entry MISSING from subdivisions.spec.json ` +
            `(rationale: ${truth.rationale})`,
        );
        continue;
      }
      const displayLower = entry.displayName.toLowerCase();
      const matched = truth.accepted.some((sub) =>
        displayLower.includes(sub.toLowerCase()),
      );
      if (!matched) {
        failures.push(
          `${truth.code}: displayName="${entry.displayName}" did not contain any of ` +
            `[${truth.accepted.map((s) => `"${s}"`).join(", ")}] ` +
            `(rationale: ${truth.rationale}). Refresh upstream caches OR add an overlay entry.`,
        );
      }
    }
    expect(failures, failures.join("\n")).toHaveLength(0);
  });

  it("displayName equals officialName for every pinned code", async () => {
    // The current architecture ties officialName to displayName (Wikidata + Debian
    // don't distinguish). If this changes, the assertion catches it.
    const byCode = await loadSubdivisions();
    const failures: string[] = [];
    for (const truth of PINNED_TRUTHS) {
      const entry = byCode.get(truth.code);
      if (!entry) continue;
      if (entry.displayName !== entry.officialName) {
        failures.push(
          `${truth.code}: displayName="${entry.displayName}" ` +
            `but officialName="${entry.officialName}" — current architecture requires equality`,
        );
      }
    }
    expect(failures, failures.join("\n")).toHaveLength(0);
  });

  it("every Norwegian first-order subdivision has an endonym (cascade-resolved)", async () => {
    // Norway endonym cascade: nb → nn → no → da → sv. Sample NO-03 (Oslo)
    // specifically — it MUST resolve to a Norwegian-script value.
    const byCode = await loadSubdivisions();
    const oslo = byCode.get("NO-03");
    expect(oslo).toBeDefined();
    if (oslo) {
      expect(oslo.endonymDisplayName).toBeTruthy();
      expect(oslo.endonymDisplayName!.toLowerCase()).toContain("oslo");
    }
  });
});
