// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { webcrypto } from "node:crypto";

const _HEX: readonly string[] = Array.from({ length: 256 }, (_v, i) =>
  i.toString(16).padStart(2, "0"),
);

/**
 * Mints a UUIDv7 (RFC 9562) — a 48-bit big-endian Unix-ms timestamp prefix +
 * 74 random bits, version + variant bits set. Time-ordered, so ids used as
 * store keys keep temporal locality (e.g. a messaging `message-id` matching the
 * .NET contract: a 36-char lowercase-hex UUIDv7).
 *
 * @param now Injectable clock (epoch ms) for deterministic tests; the default
 *   (`Date.now`) preserves wall-clock behavior.
 * @returns A canonical `8-4-4-4-12` UUIDv7 string.
 */
export function uuidv7(now: () => number = Date.now): string {
  const bytes = webcrypto.getRandomValues(new Uint8Array(16));
  const ms = now();

  bytes[0] = Math.floor(ms / 2 ** 40) & 0xff;
  bytes[1] = Math.floor(ms / 2 ** 32) & 0xff;
  bytes[2] = Math.floor(ms / 2 ** 24) & 0xff;
  bytes[3] = Math.floor(ms / 2 ** 16) & 0xff;
  bytes[4] = Math.floor(ms / 2 ** 8) & 0xff;
  bytes[5] = ms & 0xff;

  // Version 7 in the high nibble of byte 6; RFC 4122 variant in byte 8.
  bytes[6] = (bytes[6]! & 0x0f) | 0x70;
  bytes[8] = (bytes[8]! & 0x3f) | 0x80;

  const h = (i: number): string => _HEX[bytes[i]!]!;

  return (
    `${h(0)}${h(1)}${h(2)}${h(3)}-${h(4)}${h(5)}-${h(6)}${h(7)}-` +
    `${h(8)}${h(9)}-${h(10)}${h(11)}${h(12)}${h(13)}${h(14)}${h(15)}`
  );
}
