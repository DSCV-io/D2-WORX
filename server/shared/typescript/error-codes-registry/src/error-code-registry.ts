// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { type ErrorCategory } from "@d2/error-category";
import type { TKMessage } from "@d2/i18n-abstractions";

// Re-export the relocated ErrorCategory so existing consumers that imported it
// from this package (`import { type ErrorCategory } from "@d2/error-codes-registry"`)
// keep working unchanged.
export { type ErrorCategory } from "@d2/error-category";

// ---------------------------------------------------------------------------
// ErrorCodeInfo — the 8-field per-code metadata record. Generated entries
// from `error-code-registry.g.ts` conform to this interface.
//
// Field alignment with the .NET runtime (must match exactly for the
// cross-runtime parity test to pass):
//   code            → string (SCREAMING_SNAKE wire-format code)
//   httpStatus      → number (HTTP status integer)
//   category        → ErrorCategory (9-value string-union from @d2/error-category)
//   userMessageKey  → TKMessage (typed TK constant from @d2/i18n-keys)
//   factoryName     → string (PascalCase factory symbol)
//   factoryShape    → ErrorCodeFactoryShape (2-value string-union: standard / none)
//   doc             → string (developer / JSDoc documentation text)
//   domain          → string (derived from spec filename; "common" for generic)
// ---------------------------------------------------------------------------

// ErrorCategory is the relocated foundational @d2/error-category leaf — the
// nine-value closed union, generated from contracts/error-category/
// error-category.spec.json (the cross-runtime source). Re-exported from this
// package's barrel for backward compatibility.

/**
 * Closed factory-shape classification. Mirrors the canonical schema enum
 * byte-for-byte: `standard` is the one universal error-factory shape
 * (messages?, inputErrors?, errorCode?, category?, traceId? — all optional);
 * `none` emits the constant + boolean only (no factory).
 */
export type ErrorCodeFactoryShape = "standard" | "none";

/**
 * Full metadata record for one error code in the merged registry. Generated
 * from all `*-error-codes.spec.json` catalogs under `contracts/`. Mirrors the
 * .NET `ErrorCodeInfo` readonly record struct — 8 fields, same names, same
 * semantics. Cross-runtime parity is enforced by the contract-tests parity
 * fixture.
 */
export interface ErrorCodeInfo {
  /** Wire-format error code (SCREAMING_SNAKE). */
  readonly code: string;
  /** HTTP status mapping for transport. */
  readonly httpStatus: number;
  /** Closed semantic/telemetry classification. */
  readonly category: ErrorCategory;
  /**
   * Translation-key reference as a typed `TKMessage` constant from
   * `@d2/i18n-keys`. Read `.key` for the raw snake_case wire key.
   */
  readonly userMessageKey: TKMessage;
  /** PascalCase factory symbol (e.g. `BearerMissing`, `NotFound`). */
  readonly factoryName: string;
  /** Factory signature variant. */
  readonly factoryShape: ErrorCodeFactoryShape;
  /** Developer documentation text. */
  readonly doc: string;
  /**
   * Domain token derived from the spec filename. Generic catalog →
   * `"common"`; per-domain catalogs use their prefix token in lowercase
   * (e.g. `"auth"` for `auth-error-codes.spec.json`).
   */
  readonly domain: string;
}

// ---------------------------------------------------------------------------
// ErrorCodeRegistry — the resolution API. Backed by a frozen Map for O(1)
// ordinal case-sensitive lookup. Static (no DI) — the data is compile-time-
// frozen with no runtime config.
//
// Resolution semantics (hard not-found):
//   resolve("UNKNOWN_CODE")  → undefined
//   has("UNKNOWN_CODE")      → false
//   Unknown codes are NOT synthesized — the caller owns the fallback.
// ---------------------------------------------------------------------------

/**
 * Merged error-code registry. Aggregates all `*-error-codes.spec.json` catalogs
 * from `contracts/` into a single frozen `code → ErrorCodeInfo` lookup table.
 *
 * Use `resolve(code)` for single-code lookups. Use `has(code)` when only
 * membership is needed. Use `all` to iterate every registered code.
 */
export interface ErrorCodeRegistry {
  /**
   * Resolve a wire error code to its full metadata.
   * Returns `undefined` for any code not in the registry (hard not-found —
   * the caller owns any fallback logic).
   */
  resolve(code: string): ErrorCodeInfo | undefined;

  /**
   * Returns `true` if `code` is present in the registry, `false` otherwise.
   * Ordinal case-sensitive — `"not_found"` ≠ `"NOT_FOUND"`.
   */
  has(code: string): boolean;

  /** Every registered code in the order they appear in `error-code-registry.g.ts`. */
  readonly all: readonly ErrorCodeInfo[];
}

/**
 * Build a frozen {@link ErrorCodeRegistry} from the generated entries array.
 * Called once at module load time in `error-code-registry.g.ts`.
 *
 * @internal — exported for the generated file only; consumers use
 *   `errorCodeRegistry` from `@d2/error-codes-registry`.
 */
export function buildRegistry(
  entries: readonly ErrorCodeInfo[],
): ErrorCodeRegistry {
  const map = new Map<string, ErrorCodeInfo>();
  for (const entry of entries) map.set(entry.code, entry);
  Object.freeze(map);

  return Object.freeze({
    resolve(code: string): ErrorCodeInfo | undefined {
      return map.get(code);
    },
    has(code: string): boolean {
      return map.has(code);
    },
    all: Object.freeze([...entries]) as readonly ErrorCodeInfo[],
  });
}
