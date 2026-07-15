// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import type { TKMessage } from "@dcsv-io/d2-i18n-abstractions";

/**
 * Locale-aware translator. Mirrors the .NET `DcsvIo.D2.I18n.ITranslator`
 * shape — `t(locale, message)` resolves a `TKMessage` to its rendered
 * string for the given BCP 47 locale, falling back to the configured
 * default locale on miss + finally returning the key verbatim if no
 * catalog entry exists. NEVER throws.
 */
export interface ITranslator {
  /**
   * Translate a TK message into the named locale. Falls back to the
   * configured default on miss; returns the message key verbatim if
   * neither locale has the key.
   */
  t(locale: string, message: TKMessage): string;
}
