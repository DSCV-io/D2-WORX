// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

/**
 * Resolve a backend D2Result message into a translated string.
 *
 * Backend handlers return TKMessage objects in `D2Result.messages` —
 * `{key, params?}` per the spec-derived wire shape at
 * `contracts/tk-message/tk-message.spec.json`. Paraglide compiles each
 * key into a callable on the `messages` module; this helper looks up the
 * key at runtime, passes the TKMessage params as the placeholder
 * bindings, and falls back to the raw key (or the supplied fallback)
 * when Paraglide does not recognize the key.
 *
 * Also accepts a bare `string | undefined` for call sites that have a
 * raw key in hand (no params binding needed); this overload keeps the
 * call shape unchanged at those sites.
 */
import type { TKMessage } from "@dcsv-io/d2-result";

import * as m from "$lib/paraglide/messages.js";

const messageRegistry = m as unknown as Record<
  string,
  ((args?: Record<string, unknown>) => string) | undefined
>;

/**
 * Resolve a TKMessage / raw key / undefined into a translated string.
 *
 * @param message Either a `TKMessage` (wire shape: `{key, params?}`) — the
 *   common case from `D2Result.messages` — OR a bare key string (when
 *   caller has no params to bind), OR `undefined` (no message present).
 * @param args Additional params merged with the TKMessage params (the
 *   args argument wins on key collision). Useful when the caller has
 *   context that the server-side TKMessage couldn't bind.
 * @param fallback Returned when `message` is undefined/empty OR
 *   Paraglide errors during render. Defaults to the raw key.
 */
export function translateMessage(
  message: TKMessage | string | undefined,
  args?: Record<string, unknown>,
  fallback?: string,
): string {
  if (message === undefined || message === null || message === "") return fallback ?? "";

  const key = typeof message === "string" ? message : message.key;
  const tkParams = typeof message === "string" ? undefined : message.params;
  const mergedArgs =
    tkParams === undefined && args === undefined
      ? undefined
      : { ...(tkParams ?? {}), ...(args ?? {}) };

  const fn = messageRegistry[key];
  if (typeof fn === "function") {
    try {
      return fn(mergedArgs);
    } catch {
      return fallback ?? key;
    }
  }
  // Unknown key — fall back to the raw key so operators see the bare TK
  // identifier instead of an empty rendered string. Callers that truly
  // need their fallback used for unknown keys should pre-check.
  return key;
}
