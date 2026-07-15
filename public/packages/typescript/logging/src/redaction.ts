// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { falsey } from "@dcsv-io/d2-utilities";

/**
 * Hand-written types register their PII paths via this helper at module
 * load time; the `setupLogger()` consumer flattens all registered paths
 * (plus codegen-emitted `<TypeName>RedactPaths` constants) into Pino's
 * `redact: { paths }` configuration.
 *
 * Path syntax matches Pino's: dot-separated, with `[*]` for array
 * wildcards. The `bindings.` prefix is added by `setupLogger()` so callers
 * pass field names relative to the type root (e.g. `"email"`, not
 * `"bindings.email"`).
 *
 * Codegen-emitted abstractions emit a `readonly string[]` constant
 * directly rather than calling this helper, so this helper is for
 * hand-written types only.
 */
const sr_registry = new Map<symbol, readonly string[]>();

/**
 * Register the PII paths for a hand-written type. Repeated calls with the
 * same identifier replace the previous registration (idempotent).
 */
export function markRedactedFields(
  typeIdentifier: symbol,
  paths: readonly string[],
): void {
  if (typeof typeIdentifier !== "symbol")
    throw new TypeError("markRedactedFields: typeIdentifier must be a symbol");
  const cleaned: string[] = [];
  for (const p of paths) {
    if (falsey(p)) continue;
    cleaned.push((p as string).trim());
  }
  sr_registry.set(typeIdentifier, cleaned);
}

/**
 * Returns the redacted paths registered for a single type identifier.
 * Returns an empty array if no registration exists (no throwing on
 * "unknown" — operators may legitimately query identifiers before
 * registration in tests).
 */
export function getRedactedFieldsFor(
  typeIdentifier: symbol,
): readonly string[] {
  return sr_registry.get(typeIdentifier) ?? [];
}

/**
 * Returns ALL registered paths flattened across every registered type.
 * Used by `setupLogger()` to feed Pino's `redact: { paths }` config.
 */
export function collectAllRedactedFields(): readonly string[] {
  const out: string[] = [];
  for (const paths of sr_registry.values()) out.push(...paths);
  return out;
}

/**
 * Test-only escape hatch — clears the registry. Production code should
 * NEVER call this; only test fixtures isolating per-test state.
 */
export function clearRedactedFieldsRegistry(): void {
  sr_registry.clear();
}
