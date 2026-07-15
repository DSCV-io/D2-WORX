// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

/**
 * Partial Locale spec entry, produced from:
 *   - CLDR `availableLocales.json` `full` set (Unicode-3.0) — authoritative tag catalog
 *   - Node ICU `Intl.Locale` — structural decomposition into language/script/region
 *   - Node ICU `Intl.DisplayNames` — composed display name (English) + endonym
 *     (own language)
 *   - CLDR `cldr-numbers-full/{locale}/numbers.json` (Unicode-3.0)
 *     — decimal + thousands separators
 *   - CLDR `cldr-dates-full/{locale}/ca-gregorian.json` (Unicode-3.0)
 *     — date format pattern (DMY/MDY/YMD)
 *
 * Mirrors the Locale entity shape. `IsSelectable` is a Tier 2 field (derived from
 * public/contracts/messages/{tag}.json file presence) and not part of this pipeline-raw shape.
 * Region-derived fields (firstDayOfWeek / weekendStart / weekendEnd / measurementSystem)
 * live on Country in src-data and get DENORMALIZED onto Locale in the Tier 2 clean-pass —
 * they're not duplicated in src-data to avoid drift during per-catalog refreshes.
 */
export interface LocalePartial {
  /** IETF BCP 47 tag, canonical-cased (e.g., "en-US", "zh-Hans-CN"). Primary key. */
  ietfBcp47Tag: string;
  /** ISO 639-1 (or 639-3 fallback) language subtag, lowercase (e.g., "en", "zh", "yue"). */
  languageSubtag: string;
  /**
   * ISO 15924 script subtag, TitleCase (e.g., "Hans", "Cyrl").
   * Null when locale carries no script subtag.
   */
  scriptSubtag: string | null;
  /**
   * ISO 3166-1 alpha-2 region subtag, UPPERCASE (e.g., "US", "JP"). Null when locale
   * carries no region subtag. UN M49 numeric regions (e.g., "001" = World) also surface
   * here.
   */
  regionSubtag: string | null;
  /** Locale name composed in English via Node ICU Intl.DisplayNames(['en']). */
  displayName: string;
  /**
   * Locale name composed in its own primary language via
   * Node ICU Intl.DisplayNames([languageSubtag]). Null when ICU can't compose
   * (very rare).
   */
  endonymDisplayName: string | null;
  /**
   * Localized display names composed across the 11 supported locales
   * (parallel to Country.LocalizedDisplayNames).
   */
  localizedDisplayNames: Record<string, string>;
  /** Decimal separator from CLDR numbers.json `symbols-numberSystem-{default}`.`decimal`. */
  decimalSeparator: string;
  /**
   * Group / thousands separator from CLDR numbers.json
   * `symbols-numberSystem-{default}`.`group`. May be NBSP (U+00A0) for fr/pl/etc.
   */
  thousandsSeparator: string;
  /** Derived enum (DMY / MDY / YMD) from CLDR ca-gregorian.json `dateFormats.short`. */
  dateFormatPattern: "DMY" | "MDY" | "YMD";
  /**
   * Diagnostic — which CLDR locale supplied the numbers/dates data. Equals
   * `ietfBcp47Tag` when the locale has a direct file; differs when CLDR
   * locale-inheritance fallback walked to a parent.
   */
  cldrDataSourceLocale: string;
  _provenance: {
    sources: string[];
    extractedAt: string;
  };
}

/**
 * Numeric + date-format data resolved from CLDR locale-inheritance fallback chain.
 * Returned by the caller-provided resolver so the transformer stays pure.
 */
export interface LocaleCldrFormattingData {
  decimalSeparator: string;
  thousandsSeparator: string;
  dateFormatPattern: "DMY" | "MDY" | "YMD";
  /** Which CLDR locale tag actually supplied the data (after fallback walk). */
  cldrDataSourceLocale: string;
}

/**
 * Build a LocalePartial for a single BCP 47 tag.
 * Returns null when `Intl.Locale` rejects the tag (malformed) or display composition fails.
 */
export function transformLocaleTag(
  tag: string,
  ctx: {
    supportedLocaleCodes: readonly string[];
    formatting: LocaleCldrFormattingData;
  },
): LocalePartial | null {
  let parsed: Intl.Locale;
  try {
    parsed = new Intl.Locale(tag);
  } catch {
    return null;
  }
  // Some CLDR tags (e.g. private-use only or non-conforming variants) yield an
  // Intl.Locale with no `language` subtag. Skip those — they're not useful as catalog entries.
  if (!parsed.language) return null;

  const englishDisplay = composeDisplayName(tag, "en");
  if (!englishDisplay) return null;

  const localizedDisplayNames: Record<string, string> = {};
  for (const lang of ctx.supportedLocaleCodes) {
    const name = composeDisplayName(tag, lang);
    if (name) localizedDisplayNames[lang] = name;
  }
  const endonymDisplayName = composeDisplayName(tag, parsed.language);

  return {
    ietfBcp47Tag: parsed.baseName, // canonical-cased form (e.g., "en-US", "zh-Hans-CN")
    languageSubtag: parsed.language.toLowerCase(),
    scriptSubtag: parsed.script ?? null,
    regionSubtag: parsed.region ?? null,
    displayName: englishDisplay,
    endonymDisplayName,
    localizedDisplayNames,
    decimalSeparator: ctx.formatting.decimalSeparator,
    thousandsSeparator: ctx.formatting.thousandsSeparator,
    dateFormatPattern: ctx.formatting.dateFormatPattern,
    cldrDataSourceLocale: ctx.formatting.cldrDataSourceLocale,
    _provenance: {
      sources: [
        "cldr-core/availableLocales",
        "intl-display-names-icu",
        "cldr-numbers-full",
        "cldr-dates-full",
      ],
      extractedAt: new Date().toISOString(),
    },
  };
}

/**
 * Composes a locale's display name in a given target locale via Node ICU.
 * Returns null when composition fails or yields a useless fallback (ICU returns the
 * input tag verbatim when it can't compose — that indicates no usable name).
 */
function composeDisplayName(tag: string, targetLocale: string): string | null {
  try {
    const dn = new Intl.DisplayNames([targetLocale], { type: "language" });
    const result = dn.of(tag);
    if (!result) return null;
    if (result === tag) return null; // ICU couldn't compose; returned input verbatim
    return result;
  } catch {
    return null;
  }
}
