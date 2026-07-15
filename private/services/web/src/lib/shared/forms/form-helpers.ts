// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

/**
 * D2Result ↔ Superforms error mapping bridge.
 *
 * Converts D2Result `InputError[]` (from gateway responses) into the
 * `Record<string, string[]>` shape that Superforms expects for per-field
 * errors. `InputError` is an OBJECT (`{field, errors: TKMessage[]}`) per
 * the spec-derived `InputErrorWireShape` catalog — NOT a tuple. The
 * helper renders each `TKMessage` to a localized string via Paraglide's
 * `m[key]()` keyed translator lookup; unknown keys fall back to the raw
 * key string so the form still displays something operators can
 * recognize.
 */
import type { InputError, TKMessage } from "@dcsv-io/d2-result";
import type { SuperValidated } from "sveltekit-superforms";
import * as m from "$lib/paraglide/messages.js";

/**
 * Render one `TKMessage` to a localized string via Paraglide. Unknown
 * keys fall back to the raw key — never throws, always returns a
 * presentable string.
 */
function renderTk(message: TKMessage): string {
  const fn = (m as Record<string, (params?: Record<string, unknown>) => string>)[message.key];
  return fn === undefined ? message.key : fn(message.params);
}

/**
 * Convert D2Result `InputError[]` into a field → localized-strings map.
 *
 * Wire format: `[{ field, errors: TKMessage[] }, ...]` — self-describing
 * objects per the spec-derived `InputErrorWireShape` catalog.
 * Output format: `{ field: [renderedString1, renderedString2, ...] }`.
 *
 * Handles dot-notation field names (e.g. `address.city`) and merges
 * duplicate field entries. Empty field names and zero-error entries are
 * skipped.
 */
export function mapD2Errors(inputErrors: readonly InputError[]): Record<string, string[]> {
  const result: Record<string, string[]> = {};
  for (const { field, errors } of inputErrors) {
    if (!field || errors.length === 0) continue;
    const rendered = errors.map(renderTk);
    const existing = result[field];
    if (existing) {
      existing.push(...rendered);
    } else {
      result[field] = rendered;
    }
  }
  return result;
}

/**
 * Apply D2Result input errors to a Superforms form object.
 *
 * Usage in form actions:
 * ```ts
 * const result = await parseGatewayResponse(response);
 * if (result.inputErrors?.length) {
 *   applyD2Errors(form, result.inputErrors);
 *   return fail(400, { form });
 * }
 * ```
 */
export function applyD2Errors(
  form: SuperValidated<Record<string, unknown>>,
  inputErrors: readonly InputError[],
): void {
  const mapped = mapD2Errors(inputErrors);
  for (const [field, errors] of Object.entries(mapped)) {
    const existing = (form.errors as Record<string, string[]>)[field];
    if (existing) {
      existing.push(...errors);
    } else {
      (form.errors as Record<string, string[]>)[field] = errors;
    }
  }
  form.valid = false;
}
