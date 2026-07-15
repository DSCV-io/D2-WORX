// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { AmqpHeaders } from "@d2/headers-amqp";

// Hard cap on accepted message-id length. Our publisher emits a 36-char
// UUIDv7; an attacker controlling a queue we subscribe to could send a 64KB id
// and bloat the idempotency store key. Anything longer falls through to "no id"
// behavior (skips idempotency, still acks). Mirrors the .NET
// `SubscriberChannel._MAX_MESSAGE_ID_LENGTH`.
const _MAX_MESSAGE_ID_LENGTH = 128;
const _CONTROL_CHAR_MAX = 0x1f;
const _DEL_CHAR = 0x7f;
const _COLON = 0x3a;
const _SPACE = 0x20;

/**
 * Reads and validates the delivery's message id — the mirror of the .NET
 * `SubscriberChannel.ReadMessageId`. Prefers the AMQP `message-id` property,
 * falling back to the `message-id` header. Rejects pathological lengths
 * (key-injection / store-memory DoS) and any control character / `:` / space
 * that could split the store's namespace — a rejected id returns `undefined`
 * (the delivery still acks, just without idempotency).
 *
 * @param amqpMessageId The AMQP `message-id` property (or undefined).
 * @param headers The delivery's AMQP headers (or undefined).
 */
export function readValidatedMessageId(
  amqpMessageId: string | undefined,
  headers: Readonly<Record<string, unknown>> | undefined,
): string | undefined {
  const raw = readRaw(amqpMessageId, headers);
  if (raw === undefined) return undefined;

  if (raw.length === 0 || raw.length > _MAX_MESSAGE_ID_LENGTH) return undefined;

  for (let i = 0; i < raw.length; i++) {
    const code = raw.charCodeAt(i);
    if (
      code <= _CONTROL_CHAR_MAX ||
      code === _DEL_CHAR ||
      code === _COLON ||
      code === _SPACE
    ) {
      return undefined;
    }
  }

  return raw;
}

function readRaw(
  amqpMessageId: string | undefined,
  headers: Readonly<Record<string, unknown>> | undefined,
): string | undefined {
  if (typeof amqpMessageId === "string" && amqpMessageId.length > 0)
    return amqpMessageId;

  const fromHeader = headers?.[AmqpHeaders.MESSAGE_ID];
  if (typeof fromHeader === "string") return fromHeader;
  if (fromHeader instanceof Uint8Array)
    return Buffer.from(fromHeader).toString("utf8");

  return undefined;
}
