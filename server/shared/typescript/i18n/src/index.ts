// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

export type { ITranslator } from "./i-translator.js";
export {
  SupportedLocales,
  type SupportedLocalesConfig,
  loadSupportedLocalesConfig,
} from "./supported-locales.js";
export { Translator, type LocaleCatalogs } from "./translator.js";
// Re-export TKMessage from @d2/result so consumers don't need both packages.
export { type TKMessage, tk } from "@d2/result";
