// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import type { D2Result } from "@dcsv-io/d2-result";

/**
 * Pluggable serialization seam for distributed caches. Local caches
 * store objects directly and do not need this. This package owns the
 * port only; a default JSON implementation is part of the
 * `@dcsv-io/d2-caching-distributed-redis` package surface. Provider-specific
 * impls may swap in MessagePack, Protobuf, etc., for size or perf wins.
 *
 * Impl failure codes (not defined here — reuse existing catalogs when
 * implementing): `COULD_NOT_BE_SERIALIZED` /
 * `COULD_NOT_BE_DESERIALIZED`.
 */
export interface ICacheSerializer {
  /**
   * Stable identifier for the serialization format (free string, e.g.
   * `"application/json"`). Allows mixed-serializer DLQs / archive
   * blobs to round-trip safely.
   */
  readonly contentType: string;

  /**
   * Serializes `value` to bytes.
   *
   * @returns `ok(bytes)`; failure with a serialize error code on
   *   serializer error.
   */
  serialize<T>(value: T): D2Result<Uint8Array>;

  /**
   * Deserializes `bytes` back to a value.
   *
   * @returns `ok(value)`; failure with a deserialize error code on
   *   deserializer error.
   */
  deserialize<T>(bytes: Uint8Array): D2Result<T>;
}
