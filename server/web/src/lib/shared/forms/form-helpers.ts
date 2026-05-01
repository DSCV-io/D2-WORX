/**
 * D2Result ↔ Superforms error mapping bridge.
 *
 * Converts D2Result `InputError[]` (from gateway responses) into the
 * `Record<string, string[]>` shape that Superforms expects for per-field errors.
 */
import type { InputError } from "@d2/result";
import type { SuperValidated } from "sveltekit-superforms";

/**
 * Convert D2Result InputError tuples to a field → errors map.
 *
 * InputError format: `[field, ...errors]`
 * Output format: `{ field: [error1, error2, ...] }`
 *
 * Handles dot-notation field names (e.g. `address.city`) and merges
 * duplicate field entries.
 */
export function mapD2Errors(inputErrors: readonly InputError[]): Record<string, string[]> {
  const result: Record<string, string[]> = {};
  for (const [field, ...errors] of inputErrors) {
    if (!field || errors.length === 0) continue;
    const existing = result[field];
    if (existing) {
      existing.push(...errors);
    } else {
      result[field] = [...errors];
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
