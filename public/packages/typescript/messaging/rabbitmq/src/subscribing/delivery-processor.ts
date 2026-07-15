// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { context, trace } from "@opentelemetry/api";
import { AmqpHeaders } from "@d2/headers-amqp";
import type { ILogger } from "@d2/logging";
import { sanitizedErrorRender } from "@d2/logging";
import type { D2Result } from "@d2/result";

import {
  type ConsumeContext,
  establishConsumeContext,
} from "../context/consume-context.js";
import { DlqNaming } from "../topology/dlq-naming.js";
import { type SubscriptionDescriptor } from "../topology/subscription-descriptor.js";
import {
  ackFailuresCounter,
  dlqRepublishFailuresCounter,
} from "../telemetry.js";
import { type BodyOpener } from "./body-opener.js";
import { type ConsumedMessage, readHeaderString } from "./consumed-message.js";
import { DlqFailureHeaderBuilder } from "./dlq-failure-metadata.js";
import { type IMessageIdempotencyStore } from "../idempotency/message-idempotency-store.js";
import { MessageBodyDecodeError } from "./message-body-decode-error.js";
import { readAttemptCount } from "./attempt-count.js";
import { startConsumerSpan, validTraceId } from "./consumer-span.js";

/** What the pipeline decides to do with a delivery. */
export enum DeliveryAction {
  /** BasicAck — the delivery is settled (processed, skipped, or republished+acked). */
  Ack,
  /** BasicNack(requeue=false) — the broker dead-letters via `x-dead-letter-exchange`. */
  Drop,
}

/**
 * A message handler. Returns a {@link D2Result} — a failed result routes the
 * message to the DLQ (`HANDLER_RESULT_FAILURE`); a thrown error routes it as
 * `HANDLER_EXCEPTION`. Receives the established per-message {@link ConsumeContext}.
 */
export type MessageHandler = (
  message: unknown,
  ctx: ConsumeContext,
) => Promise<D2Result> | D2Result;

/**
 * Republishes the original body to a DLX with the failure header attached.
 * Resolves on success; REJECTS on failure (the pipeline falls back to a plain
 * NACK-no-requeue).
 */
export type RepublishFn = (
  dlxName: string,
  body: Buffer,
  headers: Record<string, unknown>,
) => Promise<void>;

/** Collaborators the delivery pipeline needs (all injectable for testing). */
export interface DeliveryProcessorDeps {
  readonly descriptor: SubscriptionDescriptor;
  readonly resolvedQueueName: string;
  readonly handler: MessageHandler;
  readonly opener: BodyOpener;
  readonly republish: RepublishFn;
  readonly logger: ILogger;
  readonly store?: IMessageIdempotencyStore;
}

/**
 * The per-delivery pipeline — the behavioral twin of the .NET
 * `SubscriberChannel.OnReceivedCoreAsync`: establish a consume span + per-message
 * context, run the idempotency pre-check (ack-and-skip a duplicate; fail-open on
 * a read outage), enforce the tiered-retry attempt cap, decode + dispatch, and
 * settle — ack on success (marking the idempotency window FIRST), or route to the
 * DLQ via republish-with-failure-header-then-ack (falling back to NACK-no-requeue
 * if the republish itself fails). Pure with respect to the broker: every effect
 * is an injected seam, so the whole matrix is unit-testable.
 */
export class DeliveryProcessor {
  private readonly deps: DeliveryProcessorDeps;

  constructor(deps: DeliveryProcessorDeps) {
    this.deps = deps;
  }

  /** Processes one delivery, returning the settle action to signal to the broker. */
  async process(msg: ConsumedMessage): Promise<DeliveryAction> {
    const span = startConsumerSpan(this.deps.resolvedQueueName, msg);
    try {
      return await context.with(trace.setSpan(context.active(), span), () =>
        this.core(msg, () => validTraceId(span)),
      );
    } finally {
      span.end();
    }
  }

  private async core(
    msg: ConsumedMessage,
    traceId: () => string | undefined,
  ): Promise<DeliveryAction> {
    const { descriptor, logger, store, resolvedQueueName: queue } = this.deps;
    const consumeCtx = establishConsumeContext(
      readHeaderString(msg.headers, AmqpHeaders.PROPAGATED_CONTEXT),
    );

    logger.debug("delivery received", {
      queue,
      deliveryTag: msg.deliveryTag,
      redelivered: msg.redelivered,
      messageId: msg.messageId,
    });

    // Idempotency pre-check — a hit ACK-and-SKIPs (NEVER DLQ); a read-path
    // store outage fails OPEN (process anyway).
    if (
      descriptor.idempotency &&
      msg.messageId !== undefined &&
      store !== undefined
    ) {
      const seen = await store.hasSeen(msg.messageId);
      if (!seen.failed && seen.data === true) {
        logger.debug("delivery skipped (duplicate — ack-and-skip)", {
          queue,
          deliveryTag: msg.deliveryTag,
          messageId: msg.messageId,
        });

        return DeliveryAction.Ack;
      }
    }

    // Tiered-retry attempt-count enforcement — burn the budget → DLQ.
    if (descriptor.tieredRetry !== undefined) {
      const attemptCount = readAttemptCount(msg.headers);
      if (attemptCount >= descriptor.tieredRetry.maxAttempts) {
        logger.warn("retries exhausted — routing to DLQ", {
          queue,
          messageId: msg.messageId,
          attemptCount,
          maxAttempts: descriptor.tieredRetry.maxAttempts,
        });

        return this.nackToDlq(
          msg,
          DlqFailureHeaderBuilder.fromRetriesExhausted(attemptCount, {
            traceId: traceId(),
            nackedBy: descriptor.nackedBy,
          }),
        );
      }
    }

    // Decode + dispatch. A decode / handler throw settles inside `dispatch`
    // (returning a DeliveryAction); a returned D2Result is inspected here.
    const outcome = await this.dispatch(msg, consumeCtx, traceId);
    if (typeof outcome === "number") return outcome;

    if (outcome.failed) {
      logger.warn("handler result failure — routing to DLQ", {
        queue,
        errorCode: outcome.errorCode,
      });

      return this.nackToDlq(
        msg,
        DlqFailureHeaderBuilder.fromResult(outcome, {
          traceId: traceId(),
          nackedBy: descriptor.nackedBy,
        }),
      );
    }

    // Success — write the idempotency mark BEFORE the ack so a crash between
    // mark and ack redelivers safely rather than duplicating work. A mark WRITE
    // failure fails CLOSED (NACK to DLQ) — acking an unmarked message would
    // leave the dedup window unguarded.
    if (
      descriptor.idempotency &&
      msg.messageId !== undefined &&
      store !== undefined
    ) {
      const mark = await store.markSeen(msg.messageId);
      if (mark.failed) {
        ackFailuresCounter.add(1);
        logger.error("idempotency mark failed — NACK to DLQ", {
          queue,
          deliveryTag: msg.deliveryTag,
          messageId: msg.messageId,
          errorCode: mark.errorCode,
        });

        return DeliveryAction.Drop;
      }
    }

    logger.debug("delivery acked", { queue, deliveryTag: msg.deliveryTag });
    return DeliveryAction.Ack;
  }

  private async dispatch(
    msg: ConsumedMessage,
    consumeCtx: ConsumeContext,
    traceId: () => string | undefined,
  ): Promise<DeliveryAction | D2Result> {
    const { descriptor, logger, resolvedQueueName: queue } = this.deps;
    try {
      const decoded = await this.deps.opener.open(msg.body);
      return await this.deps.handler(decoded, consumeCtx);
    } catch (err) {
      if (err instanceof MessageBodyDecodeError) {
        const rootError = err.cause instanceof Error ? err.cause : err;
        logger.error("boundary failure — routing to DLQ", {
          queue,
          cause: err.decodeCause,
          messageId: msg.messageId,
          error: sanitizedErrorRender(err),
        });

        return this.nackToDlq(
          msg,
          DlqFailureHeaderBuilder.fromBoundary(err.decodeCause, rootError, {
            traceId: traceId(),
            nackedBy: descriptor.nackedBy,
          }),
        );
      }

      logger.error("handler threw — routing to DLQ", {
        queue,
        messageId: msg.messageId,
        error: sanitizedErrorRender(err),
      });

      return this.nackToDlq(
        msg,
        DlqFailureHeaderBuilder.fromException(err, {
          traceId: traceId(),
          nackedBy: descriptor.nackedBy,
        }),
      );
    }
  }

  private async nackToDlq(
    msg: ConsumedMessage,
    failureHeader: string,
  ): Promise<DeliveryAction> {
    const { logger, resolvedQueueName: queue } = this.deps;
    const dlxName = DlqNaming.dlxFor(queue);
    const headers = buildRepublishHeaders(msg.headers, failureHeader);

    try {
      await this.deps.republish(dlxName, msg.body, headers);

      // Republished with the failure header + ack the original — the broker's
      // own x-dead-letter-exchange routing is NOT also triggered (which would
      // land a header-less duplicate in the DLQ alongside).
      return DeliveryAction.Ack;
    } catch (err) {
      dlqRepublishFailuresCounter.add(1);
      logger.error("DLQ republish failed — NACK-no-requeue fallback", {
        queue,
        dlxName,
        deliveryTag: msg.deliveryTag,
        messageId: msg.messageId,
        error: sanitizedErrorRender(err),
      });

      // Fall back to NACK-no-requeue: the broker routes a header-less copy to
      // the DLQ via x-dead-letter-exchange — better than losing the message.
      return DeliveryAction.Drop;
    }
  }
}

function buildRepublishHeaders(
  source: Readonly<Record<string, unknown>>,
  failureHeader: string,
): Record<string, unknown> {
  // Carry forward producer-set headers (traceparent, x-d2-context, x-proto-type,
  // message-id, ...) so DLQ inspection sees the same context as a normal
  // delivery, then set the failure-reason header.
  const headers: Record<string, unknown> = { ...source };
  headers[AmqpHeaders.FAILURE_REASON] = failureHeader;
  return headers;
}
