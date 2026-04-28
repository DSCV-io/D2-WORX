import { readFileSync, readdirSync } from "node:fs";
import { join } from "node:path";
import { BASE_LOCALE, toBcp47 } from "./supported-locales.js";

export interface Translator {
  /** Translate a message key to the given locale, with optional interpolation. */
  t(locale: string, key: string, params?: Record<string, string>): string;
  /** All supported locales loaded at startup (canonical BCP 47 casing). */
  readonly locales: readonly string[];
  /** The base/fallback locale. */
  readonly baseLocale: string;
}

type MessageCatalog = Record<string, Record<string, string>>;

/**
 * Creates a lightweight translator that reads JSON message catalogs
 * from a directory at startup.
 *
 * - Loads all `{locale}.json` files synchronously (they're small)
 * - Supports `{paramName}` interpolation
 * - Falls back to base locale for unknown keys/locales
 * - All catalog keys and locale references use canonical BCP 47 casing
 */
export function createTranslator(options: {
  /** Absolute path to the directory containing `{locale}.json` files. */
  messagesDir: string;
  /** Base/fallback locale. Defaults to "en-US". */
  baseLocale?: string;
}): Translator {
  const baseLocale = options.baseLocale ?? BASE_LOCALE;
  const catalogs: MessageCatalog = {};
  const loadedLocales: string[] = [];

  // Load all locale files at startup
  const files = readdirSync(options.messagesDir)
    .filter((f: string) => f.endsWith(".json"))
    .sort();
  for (const file of files) {
    const locale = file.replace(".json", "");
    const filePath = join(options.messagesDir, file);
    const content = readFileSync(filePath, "utf-8");
    let messages: Record<string, string>;
    try {
      messages = JSON.parse(content) as Record<string, string>;
    } catch (err) {
      throw new Error(
        `Failed to parse locale file ${filePath}: ${err instanceof Error ? err.message : String(err)}`,
        { cause: err },
      );
    }

    // Strip $schema key if present
    const { $schema: _, ...rest } = messages;

    // Canonical BCP 47 casing for both catalog key and locale list.
    const canonical = toBcp47(locale);
    catalogs[canonical] = rest;
    loadedLocales.push(canonical);
  }

  const baseKey = toBcp47(baseLocale);
  if (!catalogs[baseKey]) {
    throw new Error(`Base locale "${baseLocale}" not found in ${options.messagesDir}`);
  }

  function t(locale: string, key: string, params?: Record<string, string>): string {
    // Normalise input to canonical BCP 47 for lookup
    const canonical = toBcp47(locale);
    const message = catalogs[canonical]?.[key] ?? catalogs[baseKey]?.[key] ?? key;

    if (!params) return message;

    // Replace {paramName} placeholders
    return message.replace(
      /\{(\w+)\}/g,
      (_, paramName: string) => params[paramName] ?? `{${paramName}}`,
    );
  }

  return {
    t,
    locales: Object.freeze(loadedLocales),
    baseLocale,
  };
}
