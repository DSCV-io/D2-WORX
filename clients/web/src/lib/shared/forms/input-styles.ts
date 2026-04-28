/**
 * Shared Tailwind class constants for input elements.
 *
 * Used by both the shadcn Input primitive and inline edit components
 * to ensure visual consistency across all form contexts.
 *
 * Source of truth: matches the non-file branch of `ui/input/input.svelte`.
 */

/**
 * Base input classes — border, background, sizing, typography, disabled state.
 *
 * Uses `bg-input` (NOT `bg-background`) so all input-shaped components share
 * the same surface token regardless of whether they sit on the page or
 * inside a card. Source of truth must match `ui/input/input.svelte`,
 * `ui/textarea/textarea.svelte`, `ui/select/select-trigger.svelte`, and
 * the form-* combobox triggers — they ALL use `bg-input`.
 */
export const INPUT_CLASSES =
  "border-input bg-input placeholder:text-muted-foreground flex h-9 w-full min-w-0 rounded-md border px-3 py-1 text-base outline-none transition-[color,box-shadow,border-color] disabled:cursor-not-allowed disabled:opacity-50 md:text-sm";

/** Focus ring classes — applied on focus-visible. */
export const INPUT_FOCUS_CLASSES =
  "focus-visible:border-ring focus-visible:ring-ring/50 focus-visible:ring-[3px]";

/** ARIA invalid classes — applied when aria-invalid is set (used by superforms). */
export const INPUT_INVALID_CLASSES =
  "aria-invalid:ring-destructive/20 dark:aria-invalid:ring-destructive/40 aria-invalid:border-2 aria-invalid:border-destructive";

/** Inline edit border state classes. */
export const INLINE_BORDER_DIRTY = "border-2 border-blue-500";
export const INLINE_BORDER_SAVED = "border-2 border-green-500";
export const INLINE_BORDER_INVALID = "border-2 border-destructive";
