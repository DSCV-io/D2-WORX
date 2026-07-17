// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { Connection } from "rabbitmq-client";

/**
 * Connection configuration for the RabbitMQ consumer runtime. Mirrors the
 * .NET `RabbitMqConnectionOptions` surface: a single AMQP URI (host / port /
 * vhost / credentials / TLS embedded) plus a client-provided name and
 * reconnect backoff. The URI embeds credentials and is treated as a SECRET —
 * never log it whole; route any diagnostic through {@link redactAmqpUri}.
 */
export interface RabbitMqConnectionOptions {
  /**
   * AMQP connection URI — `amqp://...` or `amqps://...`. SECRET (embeds the
   * broker password). TLS is auto-enabled by the `amqps:` scheme.
   */
  readonly connectionUri: string;
  /** Client name surfaced in the RabbitMQ management UI (e.g. the service id). */
  readonly clientProvidedName?: string;
  /** Reconnect backoff floor in milliseconds (rabbitmq-client `retryLow`). */
  readonly retryLowMs?: number;
  /** Reconnect backoff ceiling in milliseconds (rabbitmq-client `retryHigh`). */
  readonly retryHighMs?: number;
  /** Heartbeat interval in seconds (0 disables). */
  readonly heartbeatSeconds?: number;
}

/**
 * Strips userinfo (credentials) from an AMQP URI so it can appear in logs
 * without leaking the broker password. Returns a `<scheme>://<host-and-path>`
 * form; a URI that cannot be parsed collapses to its scheme only so a
 * malformed value still can't leak an embedded secret.
 *
 * @param uri The AMQP URI (never logged whole).
 */
export function redactAmqpUri(uri: string): string {
  try {
    const parsed = new URL(uri);
    parsed.username = "";
    parsed.password = "";
    return parsed.toString();
  } catch {
    const schemeEnd = uri.indexOf("://");
    return schemeEnd > 0
      ? `${uri.slice(0, schemeEnd)}://[redacted]`
      : "[redacted]";
  }
}

/**
 * Constructs a long-lived, auto-reconnecting {@link Connection} from the D2
 * options. The connection immediately begins connecting and recovers from
 * dropped connections on its own (exponential backoff between
 * `retryLowMs`..`retryHighMs`); consumers registered via `subscribe` re-declare
 * their topology on every reconnect.
 *
 * @param options The connection configuration (URI is a secret).
 */
export function createConnection(
  options: RabbitMqConnectionOptions,
): Connection {
  return new Connection({
    url: options.connectionUri,
    connectionName: options.clientProvidedName,
    retryLow: options.retryLowMs,
    retryHigh: options.retryHighMs,
    heartbeat: options.heartbeatSeconds,
  });
}
