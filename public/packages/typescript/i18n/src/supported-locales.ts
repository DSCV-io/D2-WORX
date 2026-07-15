// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { falsey, parseEnvArray } from "@dcsv-io/d2-utilities";

/**
 * Configuration for {@link SupportedLocales}. Pass either an explicit list
 * or an env-var prefix (`I18N_LOCALES__0=en-US, I18N_LOCALES__1=fr-FR`)
 * matching .NET `IConfiguration` array binding semantics.
 */
export interface SupportedLocalesConfig {
  /** BCP 47 locales (e.g. `en-US`, `fr-FR`). The first becomes the default. */
  readonly enabled: readonly string[];
  /** Optional explicit default; defaults to `enabled[0]`. */
  readonly default?: string;
}

/**
 * Reads enabled-locale config from an env-var record using the indexed
 * convention `${prefix}__0`, `${prefix}__1`. Convenience for the common
 * case where the operator wires the locale list via env vars.
 */
export function loadSupportedLocalesConfig(
  prefix: string,
  env: Readonly<Record<string, string | undefined>>,
): SupportedLocalesConfig {
  const enabled = parseEnvArray(prefix, env);
  return { enabled };
}

/**
 * BCP 47 supported-locale registry with canonical-casing and
 * language-fallback resolution. Mirrors .NET `DcsvIo.D2.I18n.SupportedLocales`.
 */
export class SupportedLocales {
  /** Canonical-cased default locale. */
  readonly default: string;
  /** Canonical-cased enabled locales (de-duplicated, in input order). */
  readonly enabled: readonly string[];

  private readonly enabledLower: readonly string[];
  private readonly languageOnly: ReadonlyMap<string, string>;

  constructor(config: SupportedLocalesConfig) {
    if (falsey(config.enabled))
      throw new RangeError("SupportedLocales: enabled list is empty");

    const seen = new Set<string>();
    const canonical: string[] = [];
    for (const raw of config.enabled) {
      if (falsey(raw)) continue;
      const c = SupportedLocales.canonicalize(raw);
      const lower = c.toLowerCase();
      if (seen.has(lower)) continue;
      seen.add(lower);
      canonical.push(c);
    }
    if (canonical.length === 0)
      throw new RangeError(
        "SupportedLocales: enabled list contained no truthy values",
      );

    this.enabled = canonical;
    this.enabledLower = canonical.map((l) => l.toLowerCase());

    const languageOnly = new Map<string, string>();
    for (const c of canonical) {
      const lang = c.split("-")[0]!.toLowerCase();
      if (!languageOnly.has(lang)) languageOnly.set(lang, c);
    }
    this.languageOnly = languageOnly;

    const defaultRaw = config.default ?? canonical[0]!;
    const resolvedDefault = this.resolveOrUndef(defaultRaw);
    if (resolvedDefault === undefined)
      throw new RangeError(
        `SupportedLocales: default '${defaultRaw}' not in enabled list`,
      );
    this.default = resolvedDefault;
  }

  /**
   * Resolve a requested locale tag against the enabled list. Returns the
   * canonical-cased exact match if present, then the language-only
   * fallback, then the default locale. NEVER returns a non-enabled tag.
   *
   * Wire-boundary carve-out per rules.md §6.15: accepts `string | null`
   * because the primary caller passes a cookie / header value from
   * `Headers.get(...)` directly (Web `Headers` API returns `string | null`).
   * The boundary normalizes `null` to "absent" internally.
   */
  resolve(requested: string | null | undefined): string {
    return this.resolveOrUndef(requested) ?? this.default;
  }

  private resolveOrUndef(
    requested: string | null | undefined,
  ): string | undefined {
    if (falsey(requested)) return undefined;
    const c = SupportedLocales.canonicalize(requested as string);
    const lower = c.toLowerCase();
    const idx = this.enabledLower.indexOf(lower);
    if (idx >= 0) return this.enabled[idx]!;
    const lang = lower.split("-")[0]!;
    return this.languageOnly.get(lang);
  }

  /**
   * Canonical BCP 47 casing — language lowercase, region uppercase,
   * script title-case. Rest of the subtags lowercase.
   */
  static canonicalize(tag: string): string {
    const parts = tag.trim().split("-");
    return parts
      .map((p, i) => {
        if (i === 0) return p.toLowerCase();
        if (p.length === 2) return p.toUpperCase();
        if (p.length === 4)
          return p[0]!.toUpperCase() + p.slice(1).toLowerCase();
        return p.toLowerCase();
      })
      .join("-");
  }
}
