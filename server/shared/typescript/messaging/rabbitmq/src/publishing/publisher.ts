// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { SpanStatusCode } from "@opentelemetry/api";
import { AmqpHeaders } from "@d2/headers-amqp";
import type { ILogger } from "@d2/logging";
import { sanitizedErrorRender } from "@d2/logging";
import {
  MqMessagesRegistry,
  type MqMessageDescriptor,
} from "@d2/messaging-abstractions";
import {
  type D2Result,
  ok,
  serviceUnavailable,
  unhandledException,
} from "@d2/result";
import { MessagingActivityTags } from "@d2/telemetry";
import { uuidv7 } from "@d2/utilities";
import type { Connection } from "rabbitmq-client";

import {
  publishFailuresCounter,
  publishesCounter,
  producerTracer,
} from "../telemetry.js";
import { composeBody, type Composer } from "./body-composer.js";
import type { DomainCryptoMap, PublishableKey } from "./domain-crypto-map.js";

const _CONTENT_TYPE = "application/octet-stream";

/**
 * Default broker-confirm wait bound (ms) — mirrors the .NET publisher's
 * `RabbitMqPublisherOptions.ConfirmTimeout` default (5s, `RabbitMqMessageBus.cs`).
 * In confirm mode a wedged broker must never hang the publish call forever.
 */
const _CONFIRM_TIMEOUT_MS = 5_000;

/** The AMQP publish envelope this publisher hands to the confirm-publisher. */
export interface PublishEnvelope {
  readonly exchange: string;
  readonly routingKey: string;
  readonly durable: boolean;
  readonly messageId: string;
  readonly contentType: string;
  readonly headers: Readonly<Record<string, unknown>>;
}

/** The confirm-publisher send seam (injectable for tests; real = broker send). */
export type PublishSender = (
  envelope: PublishEnvelope,
  body: Buffer,
) => Promise<void>;

/**
 * Raised when the broker-confirm wait exceeds the bound. Mapped to a transient
 * publish failure — the .NET twin surfaces the same case as a `TimeoutException`
 * (transient) from the `ConfirmTimeout` linked CTS.
 */
class PublishConfirmTimeoutError extends Error {
  constructor(timeoutMs: number) {
    super(`Publish confirm timed out after ${timeoutMs}ms.`);
    this.name = "PublishConfirmTimeoutError";
  }
}

/**
 * Sends via the confirm-publisher, bounding the broker-confirm wait. In confirm
 * mode `sender` resolves only when the broker acks; a timeout wins the race and
 * rejects with {@link PublishConfirmTimeoutError} so a wedged confirm surfaces as a
 * transient failure instead of hanging the publish call. The underlying send is
 * abandoned on timeout — the confirm-publisher's own reconnect re-drives.
 */
async function sendWithConfirmTimeout(
  sender: PublishSender,
  envelope: PublishEnvelope,
  body: Buffer,
  confirmTimeoutMs: number,
): Promise<void> {
  // The Promise executor runs synchronously, so `timer` is always assigned before
  // the try below (the `!` asserts that to the compiler).
  let timer: ReturnType<typeof setTimeout>;
  const timeout = new Promise<never>((_, reject) => {
    timer = setTimeout(
      () => reject(new PublishConfirmTimeoutError(confirmTimeoutMs)),
      confirmTimeoutMs,
    );

    // Do not keep the event loop alive for the confirm-timeout guard.
    timer.unref();
  });

  try {
    await Promise.race([sender(envelope, body), timeout]);
  } finally {
    clearTimeout(timer!);
  }
}

/**
 * The auto-encrypting publisher — publishing and encryption are structurally
 * fused. `publish` is the ONLY path to the socket, and its `key` is
 * compile-time constrained to {@link PublishableKey}: a message on an encrypted
 * domain that was not wired into `crypto` is a COMPILE error, and there is no
 * raw-bytes publish overload. The runtime default-deny in
 * {@link composeBody} is the second lock for dynamic / fixture paths.
 */
export interface D2Publisher<TWired> {
  /**
   * Publishes a message. `key` must be a message whose encryption domain is
   * `plaintext` or is wired into the publisher's `crypto` map.
   *
   * @param key The publishable message constant.
   * @param message The message payload (JSON-serialized onto the wire).
   * @returns A successful result on broker confirm; a typed failure otherwise.
   */
  publish<K extends PublishableKey<TWired>>(
    key: K,
    message: object,
  ): Promise<D2Result>;
  /** Closes the underlying confirm-publisher. */
  close(): Promise<void>;
}

/** Options for {@link createPublisher}. */
export interface CreatePublisherOptions<
  TWired extends Partial<DomainCryptoMap>,
> {
  /**
   * The wired composers, keyed by encrypted domain. Passed with `const` so the
   * wired domains' literal keys drive the {@link PublishableKey} constraint.
   */
  readonly crypto: TWired;
  /** Structured logger (never receives an `Error` directly — PII safety). */
  readonly logger: ILogger;
  /**
   * Injectable descriptor registry (default = the generated
   * `MqMessagesRegistry`). A fixture registry drives sealed-domain runtime tests
   * while the production catalog carries no sealed messages.
   */
  readonly descriptors?: Readonly<Record<string, MqMessageDescriptor>>;
  /**
   * Broker-confirm wait bound (ms). Defaults to the .NET `ConfirmTimeout` (5s). A
   * wedged confirm surfaces as a transient failure instead of hanging forever.
   */
  readonly confirmTimeoutMs?: number;
}

/**
 * Publish logic, factored out of the broker binding so the compose / default-deny
 * / envelope / telemetry path is unit-testable with a fake {@link PublishSender}.
 * The body is composed ONCE (a resend reuses the exact bytes — no re-encrypt
 * under a fresh nonce).
 */
export async function publishVia(
  sender: PublishSender,
  registry: Readonly<Record<string, MqMessageDescriptor>>,
  composers: Readonly<Record<string, Composer | undefined>>,
  logger: ILogger,
  key: string,
  message: object,
  confirmTimeoutMs: number = _CONFIRM_TIMEOUT_MS,
): Promise<D2Result> {
  // Default-deny descriptor resolution — an unknown constant never publishes.
  const descriptor = registry[key];

  if (descriptor === undefined) {
    publishFailuresCounter.add(1);
    logger.error("publish rejected — unknown message constant", { key });

    return unhandledException();
  }

  const json = new TextEncoder().encode(JSON.stringify(message));
  const span = producerTracer().startSpan("publish");
  span.setAttribute(MessagingActivityTags.MESSAGING_SYSTEM, "rabbitmq");
  span.setAttribute(MessagingActivityTags.MESSAGING_OPERATION_TYPE, "publish");
  span.setAttribute(
    MessagingActivityTags.MESSAGING_DESTINATION_NAME,
    descriptor.exchange,
  );

  try {
    // Compose ONCE (reused verbatim on any resend).
    const composed = await composeBody(descriptor, json, composers);
    const messageId = uuidv7();
    const routingKey = descriptor.defaultRoutingKey ?? "";

    const headers: Record<string, unknown> = {
      [AmqpHeaders.PROTO_TYPE]: descriptor.messageType,
    };

    if (composed.kid !== undefined) {
      headers[AmqpHeaders.ENCRYPTION_KID] = composed.kid;
    }

    span.setAttribute(MessagingActivityTags.MESSAGING_MESSAGE_ID, messageId);
    span.setAttribute(
      MessagingActivityTags.MESSAGING_RABBITMQ_ROUTING_KEY,
      routingKey,
    );

    await sendWithConfirmTimeout(
      sender,
      {
        exchange: descriptor.exchange,
        routingKey,
        durable: true,
        messageId,
        contentType: _CONTENT_TYPE,
        headers,
      },
      Buffer.from(composed.body),
      confirmTimeoutMs,
    );

    publishesCounter.add(1);

    return ok();
  } catch (err) {
    publishFailuresCounter.add(1);
    span.setStatus({ code: SpanStatusCode.ERROR });

    if (err instanceof PublishConfirmTimeoutError) {
      logger.error("publish confirm timed out", {
        key,
        exchange: descriptor.exchange,
        timeoutMs: confirmTimeoutMs,
      });
    } else {
      logger.error("publish failed", {
        key,
        exchange: descriptor.exchange,
        error: sanitizedErrorRender(err),
      });
    }

    return serviceUnavailable();
  } finally {
    span.end();
  }
}

/**
 * Creates an auto-encrypting publisher over a live connection. The wired
 * `crypto` map's literal keys drive the compile-time {@link PublishableKey}
 * constraint on {@link D2Publisher.publish}.
 *
 * @param connection A live (auto-reconnecting) connection.
 * @param options The wired composers + logger (+ optional fixture registry).
 * @returns The publisher.
 */
export function createPublisher<const TWired extends Partial<DomainCryptoMap>>(
  connection: Connection,
  options: CreatePublisherOptions<TWired>,
): D2Publisher<TWired> {
  const registry = options.descriptors ?? MqMessagesRegistry;
  const composers = options.crypto as Readonly<
    Record<string, Composer | undefined>
  >;
  const confirmTimeoutMs = options.confirmTimeoutMs ?? _CONFIRM_TIMEOUT_MS;
  const publisher = connection.createPublisher({ confirm: true });
  const sender: PublishSender = (envelope, body) =>
    publisher.send(envelope, body);

  return {
    publish: ((key: string, message: object) =>
      publishVia(
        sender,
        registry,
        composers,
        options.logger,
        key,
        message,
        confirmTimeoutMs,
      )) as D2Publisher<TWired>["publish"],
    close: () => publisher.close(),
  };
}
