// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

/**
 * Sentinel error thrown by an `onSave` callback when the user dismissed an
 * intermediate confirmation step rather than committing the change. Inline
 * field components recognize this name and treat it as "no save happened" —
 * the field stays dirty (save/revert affordance reappears) instead of
 * landing in the "Failed to save" error state.
 *
 * Use it when an `onSave` flow involves a confirmation modal or any other
 * gate that the user can back out of without it being a real failure.
 */
export class SaveCancelledError extends Error {
  constructor(message = "Save cancelled by user.") {
    super(message);
    this.name = "SaveCancelledError";
  }
}

/**
 * Type guard for the sentinel — handles both direct `instanceof` checks and
 * cross-realm cases (e.g., errors thrown from a different module bundle)
 * where `instanceof` can return false even for the same class.
 */
export function isSaveCancelledError(err: unknown): boolean {
  return (
    err instanceof SaveCancelledError || (err instanceof Error && err.name === "SaveCancelledError")
  );
}
