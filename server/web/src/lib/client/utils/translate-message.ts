// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

/**
 * Resolve a backend D2Result message into a translated string.
 *
 * Backend handlers return TK constant strings (e.g. "common_errors_UNKNOWN")
 * in `D2Result.messages`. Paraglide compiles each key into a callable on the
 * `messages` module. This helper looks up the key at runtime and invokes it,
 * falling back to the original string if it isn't a known translation key
 * (some upstream errors return free-form English).
 */
import * as m from "$lib/paraglide/messages.js";

const messageRegistry = m as unknown as Record<
  string,
  ((args?: Record<string, unknown>) => string) | undefined
>;

export function translateMessage(
  key: string | undefined,
  args?: Record<string, unknown>,
  fallback?: string,
): string {
  if (!key) return fallback ?? "";
  const fn = messageRegistry[key];
  if (typeof fn === "function") {
    try {
      return fn(args);
    } catch {
      return fallback ?? key;
    }
  }
  return key;
}
