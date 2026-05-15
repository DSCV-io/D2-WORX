// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import type { TKMessage } from "@d2/result";
import { falsey } from "@d2/utilities";

import type { ITranslator } from "./i-translator.js";
import type { SupportedLocales } from "./supported-locales.js";

/**
 * In-memory message catalog map keyed by canonical-cased locale tag.
 */
export type LocaleCatalogs = Readonly<
  Record<string, Readonly<Record<string, string>>>
>;

/**
 * Default `ITranslator` implementation. Resolves the requested locale via
 * {@link SupportedLocales}, looks the key up in the per-locale catalog,
 * falls back to the default locale's catalog, and finally returns the
 * key verbatim if neither has the entry. NEVER throws — matches the .NET
 * `Translator.T` semantic.
 *
 * Parameter substitution uses `{name}` placeholders. Unmatched placeholders
 * are left literal so the operator notices them in the output.
 */
export class Translator implements ITranslator {
  constructor(
    private readonly locales: SupportedLocales,
    private readonly catalogs: LocaleCatalogs,
  ) {}

  t(locale: string, message: TKMessage): string {
    const tag = this.locales.resolve(locale);
    const template =
      this.catalogs[tag]?.[message.key] ??
      this.catalogs[this.locales.default]?.[message.key] ??
      message.key;
    return Translator.render(template, message.params);
  }

  static render(
    template: string,
    params: Readonly<Record<string, unknown>> | undefined,
  ): string {
    if (params === undefined || falsey(template)) return template;
    return template.replace(/\{(\w+)\}/g, (match, name: string) => {
      if (Object.prototype.hasOwnProperty.call(params, name))
        return String(params[name]);
      return match;
    });
  }
}
