// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

/**
 * A normalized incoming delivery — the transport-neutral shape the delivery
 * pipeline processes. Built from the rabbitmq-client `AsyncMessage` at the
 * consume boundary so the core pipeline is fully unit-testable without a live
 * broker.
 */
export interface ConsumedMessage {
  /** Raw body bytes (opaque `application/octet-stream`). */
  readonly body: Buffer;
  /** Validated message id, or undefined when absent / rejected. */
  readonly messageId?: string;
  /** All AMQP headers on the delivery. */
  readonly headers: Readonly<Record<string, unknown>>;
  /** Broker-assigned delivery tag. */
  readonly deliveryTag: number;
  /** True if the message was previously delivered. */
  readonly redelivered: boolean;
  /** Exchange the message was published to. */
  readonly exchange: string;
  /** Routing key the message was published with. */
  readonly routingKey: string;
  /** AMQP content-type property (carried forward on DLQ republish). */
  readonly contentType?: string;
  /** AMQP correlation-id property (carried forward on DLQ republish). */
  readonly correlationId?: string;
}

/**
 * Reads an AMQP header as a UTF-8 string. Header values arrive decoded from
 * rabbitmq-client (longstr → string) but a byte-typed header surfaces as a
 * `Uint8Array` / `Buffer`; both normalize to a string. Any other type (or an
 * absent header) yields `undefined`.
 *
 * @param headers The delivery's AMQP headers.
 * @param name The header name.
 */
export function readHeaderString(
  headers: Readonly<Record<string, unknown>>,
  name: string,
): string | undefined {
  const raw = headers[name];
  if (typeof raw === "string") return raw;
  if (raw instanceof Uint8Array) return Buffer.from(raw).toString("utf8");

  return undefined;
}
