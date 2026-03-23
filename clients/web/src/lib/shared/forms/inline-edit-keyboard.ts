/**
 * Shared keyboard handler for inline edit components.
 * Enter = save (when dirty), Escape = revert (when dirty).
 */
export function createInlineEditKeyHandler(opts: {
  isDirty: () => boolean;
  onSave: () => void;
  onRevert: () => void;
}): (e: KeyboardEvent) => void {
  return (e: KeyboardEvent) => {
    if (e.key === "Enter" && opts.isDirty()) {
      e.preventDefault();
      opts.onSave();
    } else if (e.key === "Escape" && opts.isDirty()) {
      e.preventDefault();
      opts.onRevert();
    }
  };
}
