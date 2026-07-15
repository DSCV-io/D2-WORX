// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

export type { ITranslator } from "./i-translator.js";
export {
  SupportedLocales,
  type SupportedLocalesConfig,
  loadSupportedLocalesConfig,
} from "./supported-locales.js";
export { Translator, type LocaleCatalogs } from "./translator.js";
// Re-export the TKMessage primitives from @d2/i18n-abstractions so consumers
// of @d2/i18n get the message shape + factory without a second import.
export { type TKMessage, tk } from "@d2/i18n-abstractions";
