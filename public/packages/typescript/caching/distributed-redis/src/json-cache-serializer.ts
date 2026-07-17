// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import type { ICacheSerializer } from "@dcsv-io/d2-caching-abstractions";
import { TK } from "@dcsv-io/d2-i18n-keys";
import {
  ErrorCodes,
  fail,
  HttpStatusCode,
  ok,
  type D2Result,
} from "@dcsv-io/d2-result";

const TEXT_ENCODER = new TextEncoder();
const TEXT_DECODER = new TextDecoder();

/**
 * Default {@link ICacheSerializer} using `JSON.stringify` / `JSON.parse`.
 * Twin of .NET `JsonCacheSerializer` (STJ Web defaults): camelCase property
 * names, cycle graphs omit cyclic refs, no indent. Dev-friendly (Redis CLI
 * can inspect UTF-8 JSON values).
 */
export class JsonCacheSerializer implements ICacheSerializer {
  /** Stable format identifier for mixed-serializer archives. */
  readonly contentType = "application/json";

  /**
   * Serializes `value` to UTF-8 JSON bytes with camelCase property names
   * and cycle-safe omission of cyclic references.
   *
   * @returns `ok(bytes)` on success; `fail` with
   *   `ErrorCodes.COULD_NOT_BE_SERIALIZED` (HTTP 500) on encode failure.
   */
  serialize<T>(value: T): D2Result<Uint8Array> {
    try {
      const seen = new WeakSet<object>();
      const json = JSON.stringify(value, (_key, v: unknown) => {
        if (v !== null && typeof v === "object") {
          if (seen.has(v as object)) {
            return undefined;
          }

          seen.add(v as object);

          if (!Array.isArray(v)) {
            const out: Record<string, unknown> = {};

            for (const [k, prop] of Object.entries(
              v as Record<string, unknown>,
            )) {
              out[toCamelCase(k)] = prop;
            }

            return out;
          }
        }

        return v;
      });

      return ok(TEXT_ENCODER.encode(json));
    } catch {
      return fail<Uint8Array>({
        statusCode: HttpStatusCode.InternalServerError,
        errorCode: ErrorCodes.COULD_NOT_BE_SERIALIZED,
        messages: [TK.common.errors.COULD_NOT_BE_SERIALIZED],
      });
    }
  }

  /**
   * Deserializes UTF-8 JSON bytes. JSON `null` is a legitimate value.
   *
   * @returns `ok(value)` on success; `fail` with
   *   `ErrorCodes.COULD_NOT_BE_DESERIALIZED` (HTTP 500) on parse failure.
   */
  deserialize<T>(bytes: Uint8Array): D2Result<T> {
    try {
      if (bytes.byteLength === 0) {
        return fail<T>({
          statusCode: HttpStatusCode.InternalServerError,
          errorCode: ErrorCodes.COULD_NOT_BE_DESERIALIZED,
          messages: [TK.common.errors.COULD_NOT_BE_DESERIALIZED],
        });
      }

      const text = TEXT_DECODER.decode(bytes);
      const value = JSON.parse(text) as T;

      return ok(value);
    } catch {
      return fail<T>({
        statusCode: HttpStatusCode.InternalServerError,
        errorCode: ErrorCodes.COULD_NOT_BE_DESERIALIZED,
        messages: [TK.common.errors.COULD_NOT_BE_DESERIALIZED],
      });
    }
  }
}

function toCamelCase(name: string): string {
  const first = name[0];

  if (first === undefined) {
    return name;
  }

  if (first === first.toLowerCase()) {
    return name;
  }

  return first.toLowerCase() + name.slice(1);
}
