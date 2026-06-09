// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { readdirSync, readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

// ---------------------------------------------------------------------------
// Cross-locale completeness guard — source-side. For every error-code spec
// discovered under contracts/ (any *.spec.json whose parsed JSON carries an
// `errorCodes` array with entries that have a `userMessageKey` field), assert
// the snake-form userMessageKey is PRESENT and NON-PLACEHOLDER in EVERY locale
// file under contracts/messages/*.json.
//
// The existing TK-validity test (error-codes-tk-validity.parity.test.ts)
// checks that each key RENDERS in en-US. That guard misses the remaining nine
// locales: a key could be present in en-US but missing or stubbed in de-DE,
// ja-JP, etc. This test closes that gap.
//
// SPEC DISCOVERY: rather than a hardcoded list of spec paths, this test walks
// contracts/ recursively and includes a spec iff its parsed JSON has an
// `errorCodes` array whose entries carry a `userMessageKey` field. This means
// a future per-domain spec (geo-error-codes, keycustodian-error-codes, …)
// is automatically covered the moment it lands in contracts/ — no manual list
// update required. The EXPECTED_SPEC_COUNT pin below enforces acknowledgment
// when a new catalog is added (discovery is automatic; count-bump is deliberate).
//
// PLACEHOLDER HEURISTIC (what counts as a non-real translation value):
//   (a) Empty string or whitespace-only — the key has no translation at all.
//   (b) The value equals the snake key itself — un-translated stub
//       (e.g. key "common_errors_CANCELED", value "common_errors_CANCELED").
//   (c) The value matches a known dev-stub pattern: starts with "TODO" (case-
//       insensitive) or contains the literal phrase "Coming soon" (title-case
//       or lower, case-insensitive). Error messages must never be dev stubs
//       because they are shown directly to end users.
//   NOT counted as placeholder: legitimately brief values (single words,
//   punctuated phrases). Length is NOT a proxy for real-ness — some locales
//   produce terse phrasing. Only the above three classes are rejected.
//
// Locale list is DERIVED from contracts/messages/*.json (no hardcoded list)
// so a locale added to the source tree is automatically covered. The test
// additionally asserts the count equals EXPECTED_LOCALE_COUNT (10) so a
// missing or spurious locale file surfaces as a count failure, not silent
// invisibility.
// ---------------------------------------------------------------------------

// ---------------------------------------------------------------------------
// EXPECTED_LOCALE_COUNT: update this constant when a new locale is officially
// added to the project. The count assertion exists so that adding a locale
// without tests for completeness is caught immediately.
// ---------------------------------------------------------------------------
const EXPECTED_LOCALE_COUNT = 10;

// ---------------------------------------------------------------------------
// EXPECTED_SPEC_COUNT: update this constant when a new error-code catalog
// (*.spec.json with an `errorCodes[].userMessageKey` shape) is added to
// contracts/. The discovery is automatic — new specs are covered without any
// code change here — but bumping this count is the deliberate acknowledgment
// that the new catalog's keys are being exercised. If you added a new spec and
// this assertion fires, just increment the number.
// ---------------------------------------------------------------------------
const EXPECTED_SPEC_COUNT = 2;

function findRepoRoot(startDir: string): string {
  let dir = startDir;
  for (;;) {
    try {
      readFileSync(join(dir, "pnpm-workspace.yaml"), "utf8");
      return dir;
    } catch {
      const parent = dirname(dir);
      if (parent === dir)
        throw new Error("could not locate repo root (no pnpm-workspace.yaml)");
      dir = parent;
    }
  }
}

const repoRoot = findRepoRoot(dirname(fileURLToPath(import.meta.url)));
const messagesDir = join(repoRoot, "contracts", "messages");
const contractsDir = join(repoRoot, "contracts");

// Derive locale list from the actual directory contents so additions are
// automatically covered.
const localeFiles = readdirSync(messagesDir).filter((f) => f.endsWith(".json"));
const locales = localeFiles.map((f) => f.replace(/\.json$/, ""));

type LocaleCatalog = Record<string, string>;

function loadLocale(locale: string): LocaleCatalog {
  const raw = JSON.parse(
    readFileSync(join(messagesDir, `${locale}.json`), "utf8"),
  ) as Record<string, string>;
  const { $schema: _schema, ...catalog } = raw;
  return catalog;
}

const catalogs = new Map<string, LocaleCatalog>(
  locales.map((locale) => [locale, loadLocale(locale)]),
);

// ---------------------------------------------------------------------------
// Placeholder heuristic — see module-level comment for rationale.
// ---------------------------------------------------------------------------
const STUB_PATTERNS: readonly RegExp[] = [
  /^\s*$/, // empty or whitespace-only
  /^TODO\b/i, // dev TODO marker
  /coming soon/i, // unbuilt-page stub
];

function isPlaceholder(snakeKey: string, value: string): boolean {
  if (value === snakeKey) return true; // value IS the key (untranslated stub)
  return STUB_PATTERNS.some((re) => re.test(value));
}

// ---------------------------------------------------------------------------
// Spec discovery — walk contracts/ recursively; include any *.spec.json whose
// top-level `errorCodes` array entries carry a `userMessageKey` field. This
// matches both the generic error-codes.spec.json and any future per-domain
// *-error-codes.spec.json without requiring a hardcoded path list.
// ---------------------------------------------------------------------------

interface SpecEntry {
  readonly code: string;
  readonly userMessageKey: string;
}

interface ErrorCodeSpec {
  readonly errorCodes: readonly SpecEntry[];
}

function isErrorCodeSpec(data: unknown): data is ErrorCodeSpec {
  if (typeof data !== "object" || data === null) return false;
  const obj = data as Record<string, unknown>;
  if (!Array.isArray(obj["errorCodes"]) || obj["errorCodes"].length === 0)
    return false;
  // Classify as an error-code spec iff the first entry has a userMessageKey.
  const first = obj["errorCodes"][0] as Record<string, unknown>;
  return typeof first["userMessageKey"] === "string";
}

function walkSpecs(dir: string): string[] {
  const results: string[] = [];
  for (const entry of readdirSync(dir, { withFileTypes: true })) {
    const full = join(dir, entry.name);
    if (entry.isDirectory()) results.push(...walkSpecs(full));
    else if (entry.name.endsWith(".spec.json")) results.push(full);
  }
  return results;
}

const discoveredSpecs: ErrorCodeSpec[] = [];
for (const specPath of walkSpecs(contractsDir).sort()) {
  const data = JSON.parse(readFileSync(specPath, "utf8")) as unknown;
  if (isErrorCodeSpec(data)) discoveredSpecs.push(data);
}

// ---------------------------------------------------------------------------
// Collect all unique snake keys across all discovered specs.
// ---------------------------------------------------------------------------

function snakeFromSymbolPath(symbolPath: string): string {
  // TK.Common.Errors.NOT_FOUND → common_errors_NOT_FOUND
  // TK.Auth.Errors.UNAUTHORIZED → auth_errors_UNAUTHORIZED
  const segments = symbolPath.split(".");
  const domain = segments[1]![0]!.toLowerCase() + segments[1]!.slice(1);
  const category = segments[2]![0]!.toLowerCase() + segments[2]!.slice(1);
  return `${domain}_${category}_${segments[3]}`;
}

function uniqueSnakeKeys(entries: readonly SpecEntry[]): readonly string[] {
  const seen = new Set<string>();
  const result: string[] = [];
  for (const entry of entries) {
    const snake = snakeFromSymbolPath(entry.userMessageKey);
    if (!seen.has(snake)) {
      seen.add(snake);
      result.push(snake);
    }
  }
  return result;
}

// Deduplicate across ALL discovered specs so each unique key is asserted once
// per locale regardless of how many catalogs reference it.
const allSnakeKeysSet = new Set<string>();
for (const spec of discoveredSpecs) {
  for (const snake of uniqueSnakeKeys(spec.errorCodes)) {
    allSnakeKeysSet.add(snake);
  }
}
const allSnakeKeys: readonly string[] = [...allSnakeKeysSet].sort();

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

describe("locale catalog: derived locale list is expected size", () => {
  it(`contracts/messages/ contains exactly ${EXPECTED_LOCALE_COUNT} locale files`, () => {
    expect(locales.length).toBe(EXPECTED_LOCALE_COUNT);
  });
});

// ---------------------------------------------------------------------------
// EXPECTED_SPEC_COUNT guard — forces acknowledgment when a new error-code
// catalog lands in contracts/. Discovery is automatic (keys covered without
// any code change); this count-pin is the deliberate "I know a new catalog
// was added and its keys are now being exercised" acknowledgment. Bump it.
// ---------------------------------------------------------------------------
describe("error-code spec catalog: discovered spec count is expected size", () => {
  it(`contracts/ contains exactly ${EXPECTED_SPEC_COUNT} error-code spec(s) with userMessageKey entries`, () => {
    expect(discoveredSpecs.length).toBe(EXPECTED_SPEC_COUNT);
  });
});

describe("error-codes locale completeness (all discovered catalogs — all locales)", () => {
  for (const snakeKey of allSnakeKeys) {
    describe(`key: ${snakeKey}`, () => {
      for (const locale of locales) {
        it(`${locale}: present and non-placeholder`, () => {
          const catalog = catalogs.get(locale)!;

          // (a) Key must exist in the locale file.
          expect(
            snakeKey in catalog,
            `locale ${locale}: key "${snakeKey}" is MISSING from contracts/messages/${locale}.json`,
          ).toBe(true);

          const value = catalog[snakeKey]!;

          // (b) + (c) Value must not be a placeholder per the heuristic.
          expect(
            isPlaceholder(snakeKey, value),
            `locale ${locale}: key "${snakeKey}" has a placeholder value: ${JSON.stringify(value)}`,
          ).toBe(false);
        });
      }
    });
  }
});
