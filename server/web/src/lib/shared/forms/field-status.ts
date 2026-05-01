/** Field validation status for inline visual feedback. */
export type FieldStatus = "idle" | "validating" | "valid" | "invalid";

function hasNonEmptyValue(value: unknown): boolean {
  if (value === null || value === undefined) return false;
  if (typeof value === "string") return value.trim().length > 0;
  if (Array.isArray(value)) return value.length > 0;
  return true;
}

/**
 * Derives the visual status of a form field.
 *
 * Callers control visibility timing via a `showStatus` flag — this function
 * only evaluates whether the current state is valid/invalid/idle.
 * The caller gates: `showStatus ? getFieldStatus(...) : "idle"`.
 */
export function getFieldStatus(opts: {
  errors: string[] | undefined;
  value: unknown;
}): FieldStatus {
  if (opts.errors?.length) return "invalid";
  if (hasNonEmptyValue(opts.value)) return "valid";
  return "idle";
}
