// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import {
  type IPropagatedContext,
  PropagatedContextSerializer,
} from "@dcsv-io/d2-request-context-abstractions";

/**
 * Mutable per-message context the consumer builds fresh for every delivery.
 * Its shape is EXACTLY {@link IPropagatedContext} — the propagated OPERATIONAL
 * subset (request id / path / fingerprints / WhoIs hash / locale-tier fields /
 * `callPath`). It STRUCTURALLY has no identity slots (UserId / OrgId / Scopes)
 * and no `RequestOrigin` slot: those are not fields of `IPropagatedContext`, so
 * the wire can never populate them here (§9.41 — origin/identity are never
 * reconstructed from a propagated wire value; identity rebuilds from a JWT on a
 * sync hop, and an async consumer must not claim caller identity).
 */
export type MutablePropagatedContext = {
  -readonly [K in keyof IPropagatedContext]?: IPropagatedContext[K];
};

/**
 * The established per-message consume context handed to a message handler. It
 * carries ONLY the propagated operational subset (including the telemetry-only
 * `callPath` hop-trace, which is never an authority input).
 */
export interface ConsumeContext {
  readonly propagated: IPropagatedContext;
}

/**
 * Applies the decoded propagated subset onto a fresh per-message context — the
 * TS mirror of the .NET `MutableRequestContext.ApplyPropagatedContext` /
 * generated `PropagatedContextExtensions` projection. Copies the full
 * operational subset (all `IPropagatedContext` fields, INCLUDING `callPath`);
 * because the source type is `IPropagatedContext` it structurally cannot carry
 * identity or origin, so neither can be applied.
 *
 * @param target The fresh per-message context to populate.
 * @param source The decoded propagated subset.
 */
export function applyPropagatedContext(
  target: MutablePropagatedContext,
  source: IPropagatedContext,
): void {
  Object.assign(target, source);
}

/**
 * Establishes a fresh per-message {@link ConsumeContext} from the raw
 * `x-d2-context` header value. Decodes via the shared, cap-enforcing
 * `PropagatedContextSerializer.tryDecode`; an absent / undecodable / cap-busting
 * header yields an EMPTY context (never a reject — propagation is opportunistic,
 * matching the .NET TryDecode posture). A crafted header smuggling extra keys
 * (a forged `userId` / `origin`) is ignored because `tryDecode` reads only its
 * closed field set — those keys can never reach the established context.
 *
 * The wire value is BASE64URL-of-JSON (the header spec + the .NET
 * `PropagatedContextSerializer.Encode`); the base64url wrapping is decoded here
 * before delegating to `tryDecode` (which parses the inner JSON + enforces the
 * per-field caps) — the exact pattern the BFF's `parseRequestContextFromHeaders`
 * and the gRPC context interceptor use. `Buffer.from(x, "base64url")` never
 * throws on garbage input; `tryDecode` then rejects any non-JSON payload.
 *
 * @param rawContextHeader The raw `x-d2-context` header value (or undefined).
 */
export function establishConsumeContext(
  rawContextHeader: string | undefined,
): ConsumeContext {
  const decoded = decodePropagated(rawContextHeader);
  const fresh: MutablePropagatedContext = {};
  if (decoded !== undefined) applyPropagatedContext(fresh, decoded);

  return { propagated: fresh as IPropagatedContext };
}

function decodePropagated(
  rawContextHeader: string | undefined,
): IPropagatedContext | undefined {
  if (
    rawContextHeader === undefined ||
    rawContextHeader.length === 0 ||
    /[\r\n]/.test(rawContextHeader)
  ) {
    return undefined;
  }

  const json = Buffer.from(rawContextHeader, "base64url").toString("utf8");
  return PropagatedContextSerializer.tryDecode(json);
}
