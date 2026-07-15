// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { falsey } from "@dcsv-io/d2-utilities";
import type { InputError } from "./input-error.js";
import type { TKMessage } from "@dcsv-io/d2-i18n-abstractions";

/**
 * Locale-aware translator function. Implementations bind `key + params`
 * to a localized string (typically backed by Paraglide on the
 * BFF/browser, or the `ITranslator` on the .NET server-side notifications
 * path). The function receives the raw `key` plus an OPTIONAL parameter
 * dictionary — implementations MUST tolerate an undefined `params`.
 *
 * @example
 * ```ts
 * import { m } from "@dcsv-io/d2-i18n";
 *
 * const translate: TranslateFn = (key, params) => {
 *   const fn = (m as Record<string, (p?: Record<string, unknown>) => string>)[key];
 *   return fn === undefined ? key : fn(params);
 * };
 * ```
 */
export type TranslateFn = (
  key: string,
  params?: Readonly<Record<string, unknown>>,
) => string;

/**
 * Renders a single `TKMessage` to a localized string using the supplied
 * translator. Boundary helper for consumers (toast notifications, form
 * error display, plain-text logs) that expect rendered strings rather
 * than the i18n-preserving `TKMessage` shape.
 *
 * Used at the BFF/browser boundary where the client locale is known and
 * a presentation surface needs final text. The wire path keeps
 * `TKMessage` shapes end-to-end — pre-rendering at the server destroys
 * client-side locale-switching ability.
 *
 * @param message The translation-key message to render.
 * @param translate Locale-aware translator function.
 * @returns The localized string. If the translator does not recognize
 *  the key, falls back to returning the key verbatim (best-effort: the
 *  raw key is still recognizable to operators and ships information
 *  about what was supposed to render).
 */
export function renderMessage(
  message: TKMessage,
  translate: TranslateFn,
): string {
  return translate(message.key, message.params);
}

/**
 * Renders an array of `TKMessage`s to localized strings. Empty input
 * returns an empty array (does not throw). Null / undefined input is
 * normalized to an empty array — defensive on input from untrusted
 * gateway responses where the field MAY be omitted.
 *
 * Wire-boundary carve-out per rules.md §6.15: the parameter accepts
 * `null` because gateway response envelopes (deserialized from JSON over
 * the wire) may carry `null` for missing message arrays; this boundary
 * absorbs both `null` and `undefined` and emits the empty-array sentinel.
 *
 * @param messages Array of `TKMessage` to render. `undefined` / `null`
 *  treated as empty.
 * @param translate Locale-aware translator function.
 * @returns Array of localized strings, one per input message.
 */
export function renderMessages(
  messages: readonly TKMessage[] | undefined | null,
  translate: TranslateFn,
): string[] {
  if (falsey(messages)) return [];
  return messages!.map((m) => renderMessage(m, translate));
}

/**
 * Renders an array of `InputError`s to a field-name → string[] map.
 * Boundary helper for Superforms-style form error display where each
 * field carries a list of human-readable error strings.
 *
 * Empty / undefined / null input returns an empty object. Duplicate
 * fields are merged (subsequent errors append to the existing array).
 * Entries with empty field name or zero errors are skipped.
 *
 * Wire-boundary carve-out per rules.md §6.15: see {@link renderMessages}.
 *
 * @param inputErrors Array of `InputError` to render. `undefined` /
 *  `null` treated as empty.
 * @param translate Locale-aware translator function.
 * @returns A map keyed by field name whose values are localized error
 *  strings for that field.
 */
export function renderInputErrors(
  inputErrors: readonly InputError[] | undefined | null,
  translate: TranslateFn,
): Record<string, string[]> {
  const result: Record<string, string[]> = {};
  if (falsey(inputErrors)) return result;

  for (const ie of inputErrors!) {
    if (!ie.field || ie.errors.length === 0) continue;
    const rendered = ie.errors.map((m) => renderMessage(m, translate));
    const existing = result[ie.field];
    if (existing) existing.push(...rendered);
    else result[ie.field] = rendered;
  }
  return result;
}
