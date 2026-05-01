import { DISPLAY_NAME_INVALID_RE } from "@d2/utilities";

/**
 * Input event handler that strips characters not allowed in display names.
 * Attach to any `<input>` element: `oninput={maskDisplayName}`
 *
 * Preserves cursor position after stripping.
 */
export function maskDisplayName(e: Event): void {
  const input = e.target as HTMLInputElement;
  const before = input.value;
  const cleaned = before.replace(DISPLAY_NAME_INVALID_RE, "");
  if (cleaned !== before) {
    const cursorOffset = before.length - cleaned.length;
    const pos = (input.selectionStart ?? before.length) - cursorOffset;
    input.value = cleaned;
    input.setSelectionRange(pos, pos);
    input.dispatchEvent(new Event("input", { bubbles: true }));
  }
}
