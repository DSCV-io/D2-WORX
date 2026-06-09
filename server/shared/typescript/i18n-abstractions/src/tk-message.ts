// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

/**
 * Translation-key message shape. Matches the .NET `TKMessage` wire format —
 * `{ key: "...", params?: Record<string, unknown> }`. Producers obtain
 * `TKMessage` instances via the SrcGen-emitted `TK.*` constants; the type
 * system makes "untranslated literal in `messages`" structurally
 * unrepresentable.
 *
 * The TS-side TK catalog is provided by Paraglide; this package declares
 * the shared `TKMessage` interface so cross-language wire round-trips stay
 * byte-identical. The JSON property names (`key`, `params`) come from the
 * spec-derived `TkMessageWireShape` catalog
 * (`./generated/tk-message.g.ts`) — `contracts/tk-message/tk-message.spec.json`
 * drives BOTH the .NET serializer AND this interface, so cross-language
 * wire drift on the property names is structurally impossible.
 */
export interface TKMessage {
  /** Translation key (e.g. `TK.Common.Errors.NOT_FOUND`). */
  readonly key: string;

  /** Optional parameter bindings rendered into the message at translate time. */
  readonly params?: Readonly<Record<string, unknown>>;
}

/**
 * Constructs a TKMessage from a key + optional params. Mirrors the .NET
 * `new TKMessage(key, params)` ergonomics.
 */
export function tk(
  key: string,
  params?: Readonly<Record<string, unknown>>,
): TKMessage {
  return params === undefined ? { key } : { key, params };
}
