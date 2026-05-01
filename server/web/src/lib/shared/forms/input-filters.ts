/**
 * Cursor-preserving input filters for form fields.
 *
 * Each filter is an `oninput` event handler that strips disallowed characters
 * while preserving the cursor position. Attach via `oninput={digitsOnly}`.
 */

type InputEvent = Event & { currentTarget: HTMLInputElement };

/** Strip to only allow digits (0-9). */
export function digitsOnly(e: InputEvent): void {
  applyFilter(e, (v) => v.replace(/[^0-9]/g, ""));
}

/** Strip to only allow alphabetic characters (a-z, A-Z). */
export function alphaOnly(e: InputEvent): void {
  applyFilter(e, (v) => v.replace(/[^a-zA-Z]/g, ""));
}

/** Remove all whitespace. */
export function noSpaces(e: InputEvent): void {
  applyFilter(e, (v) => v.replace(/\s/g, ""));
}

/** Force uppercase. */
export function uppercase(e: InputEvent): void {
  applyFilter(e, (v) => v.toUpperCase());
}

/** Create a max-length filter. Returns an event handler. */
export function maxLength(max: number): (e: InputEvent) => void {
  return (e: InputEvent) => {
    applyFilter(e, (v) => v.slice(0, max));
  };
}

/**
 * Compose multiple filters. Applies left-to-right.
 *
 * Usage: `oninput={compose(digitsOnly, maxLength(5))}`
 */
export function compose(...filters: Array<(e: InputEvent) => void>): (e: InputEvent) => void {
  return (e: InputEvent) => {
    for (const filter of filters) {
      filter(e);
    }
  };
}

/**
 * Core filter logic — applies a transform to the input value while
 * preserving cursor position.
 */
function applyFilter(e: InputEvent, transform: (value: string) => string): void {
  const input = e.currentTarget;
  const before = input.value;
  const cursorPos = input.selectionStart ?? before.length;
  const after = transform(before);

  if (before !== after) {
    // Calculate how many characters were removed before the cursor
    const removedBefore =
      before.slice(0, cursorPos).length - transform(before.slice(0, cursorPos)).length;
    input.value = after;
    const newPos = Math.max(0, cursorPos - removedBefore);
    input.setSelectionRange(newPos, newPos);
  }
}
