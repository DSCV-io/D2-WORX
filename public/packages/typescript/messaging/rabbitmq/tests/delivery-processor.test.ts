// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { AmqpHeaders } from "@d2/headers-amqp";
import {
  DlqFailureCauses,
  DlqFailureMetadataFields,
} from "@d2/messaging-abstractions";
import { fail, ok } from "@d2/result";
import { describe, expect, it, vi } from "vitest";

import { type BodyOpener } from "../src/subscribing/body-opener.js";
import { type ConsumeContext } from "../src/context/consume-context.js";
import {
  DeliveryAction,
  DeliveryProcessor,
  type DeliveryProcessorDeps,
  type MessageHandler,
  type RepublishFn,
} from "../src/subscribing/delivery-processor.js";
import { type IMessageIdempotencyStore } from "../src/idempotency/message-idempotency-store.js";
import { PlaintextBodyOpener } from "../src/subscribing/body-opener.js";
import { QueuePattern } from "../src/topology/queue-pattern.js";
import { type SubscriptionDescriptor } from "../src/topology/subscription-descriptor.js";
import {
  encodePropagatedHeader,
  makeMessage,
  RecordingLogger,
  SAMPLE_PRODUCER_TRACE_ID,
  SAMPLE_TRACEPARENT,
} from "./helpers.js";

const baseDescriptor: SubscriptionDescriptor = {
  queueName: "audit.key-rotated",
  exchange: "d2.security.key-rotated",
  exchangeType: "fanout",
  pattern: QueuePattern.DurableShared,
  routingKeyBinding: "",
  prefetch: 8,
  idempotency: false,
  nackedBy: "audit-svc",
};

interface Harness {
  readonly processor: DeliveryProcessor;
  readonly logger: RecordingLogger;
  readonly republished: {
    dlxName: string;
    body: Buffer;
    headers: Record<string, unknown>;
  }[];
  readonly handler: ReturnType<typeof vi.fn>;
  readonly store: {
    hasSeen: ReturnType<typeof vi.fn>;
    markSeen: ReturnType<typeof vi.fn>;
  };
}

function build(overrides: Partial<DeliveryProcessorDeps> = {}): Harness {
  const logger = new RecordingLogger();
  const republished: Harness["republished"] = [];
  const handler = vi.fn<MessageHandler>(() => ok());
  const store = {
    hasSeen: vi.fn(() => Promise.resolve(ok(false))),
    markSeen: vi.fn(() => Promise.resolve(ok())),
  };
  const republish: RepublishFn = async (dlxName, body, headers) => {
    republished.push({ dlxName, body, headers });
  };
  const opener: BodyOpener = {
    open: (body) => JSON.parse(body.toString("utf8")),
  };

  const processor = new DeliveryProcessor({
    descriptor: baseDescriptor,
    resolvedQueueName: "audit.key-rotated",
    handler,
    opener,
    republish,
    logger,
    store,
    ...overrides,
  });

  return { processor, logger, republished, handler, store };
}

function decodeFailure(
  headers: Record<string, unknown>,
): Record<string, unknown> {
  return JSON.parse(headers[AmqpHeaders.FAILURE_REASON] as string) as Record<
    string,
    unknown
  >;
}

describe("DeliveryProcessor — success + DLQ routing", () => {
  it("acks on handler success", async () => {
    const h = build();
    const action = await h.processor.process(makeMessage());
    expect(action).toBe(DeliveryAction.Ack);
    expect(h.handler).toHaveBeenCalledOnce();
    expect(h.republished).toHaveLength(0);
  });

  it("routes a failed handler result to the DLQ then acks (HANDLER_RESULT_FAILURE)", async () => {
    const h = build({
      handler: vi.fn(() => fail({ errorCode: "KEYCUSTODIAN_X" })),
    });
    const action = await h.processor.process(makeMessage());
    expect(action).toBe(DeliveryAction.Ack);
    expect(h.republished).toHaveLength(1);
    const meta = decodeFailure(h.republished[0]!.headers);
    expect(meta[DlqFailureMetadataFields.CAUSE]).toBe(
      DlqFailureCauses.HANDLER_RESULT_FAILURE,
    );
    expect(meta[DlqFailureMetadataFields.ERROR_CODE]).toBe("KEYCUSTODIAN_X");
  });

  it("routes a thrown handler to the DLQ (HANDLER_EXCEPTION)", async () => {
    const h = build({
      handler: vi.fn(() => {
        throw new TypeError("boom");
      }),
    });
    await h.processor.process(makeMessage());
    const meta = decodeFailure(h.republished[0]!.headers);
    expect(meta[DlqFailureMetadataFields.CAUSE]).toBe(
      DlqFailureCauses.HANDLER_EXCEPTION,
    );
    expect(meta[DlqFailureMetadataFields.ERROR_CODE]).toBe("TypeError");
  });

  it("routes a JSON-parse failure to the DLQ (DESERIALIZE_FAILURE)", async () => {
    const h = build({ opener: new PlaintextBodyOpener() });
    await h.processor.process(
      makeMessage({ body: Buffer.from("{not json", "utf8") }),
    );
    const meta = decodeFailure(h.republished[0]!.headers);
    expect(meta[DlqFailureMetadataFields.CAUSE]).toBe(
      DlqFailureCauses.DESERIALIZE_FAILURE,
    );
  });

  it("routes an encrypted body with no opener to the DLQ (DECRYPT_FAILURE, fail-loud)", async () => {
    const h = build({ opener: new PlaintextBodyOpener() });
    await h.processor.process(
      makeMessage({ body: Buffer.from([0x01, 0x02, 0x6b]) }),
    );
    const meta = decodeFailure(h.republished[0]!.headers);
    expect(meta[DlqFailureMetadataFields.CAUSE]).toBe(
      DlqFailureCauses.DECRYPT_FAILURE,
    );
  });

  it("falls back to DROP (NACK-no-requeue) when the DLQ republish itself fails", async () => {
    const h = build({
      handler: vi.fn(() => fail({ errorCode: "X" })),
      republish: () => Promise.reject(new Error("broker down")),
    });
    const action = await h.processor.process(makeMessage());
    expect(action).toBe(DeliveryAction.Drop);
    expect(h.logger.logged("DLQ republish failed")).toBe(true);
  });

  it("carries producer headers forward and stamps the failure reason on republish (§5.2)", async () => {
    const h = build({ handler: vi.fn(() => fail({ errorCode: "X" })) });
    await h.processor.process(
      makeMessage({
        headers: { traceparent: SAMPLE_TRACEPARENT, "x-proto-type": "Foo" },
      }),
    );
    const headers = h.republished[0]!.headers;
    expect(headers["traceparent"]).toBe(SAMPLE_TRACEPARENT);
    expect(headers["x-proto-type"]).toBe("Foo");
    expect(headers[AmqpHeaders.FAILURE_REASON]).toBeDefined();
  });
});

describe("DeliveryProcessor — idempotency 5-point contract (§5.2)", () => {
  const idempotent: SubscriptionDescriptor = {
    ...baseDescriptor,
    idempotency: true,
  };

  it("#1 already-seen id → ACK-and-SKIP, handler NOT invoked, NEVER DLQ", async () => {
    const h = build({
      descriptor: idempotent,
      store: {
        hasSeen: vi.fn(() => Promise.resolve(ok(true))),
        markSeen: vi.fn(() => Promise.resolve(ok())),
      } as unknown as IMessageIdempotencyStore,
    });
    const action = await h.processor.process(makeMessage());
    expect(action).toBe(DeliveryAction.Ack);
    expect(h.handler).not.toHaveBeenCalled();
    expect(h.republished).toHaveLength(0);
    expect(h.logger.logged("ack-and-skip")).toBe(true);
  });

  it("#2 read-path store outage → FAIL-OPEN (process the message)", async () => {
    const h = build({
      descriptor: idempotent,
      store: {
        hasSeen: vi.fn(() =>
          Promise.resolve(fail({ errorCode: "SERVICE_UNAVAILABLE" })),
        ),
        markSeen: vi.fn(() => Promise.resolve(ok())),
      } as unknown as IMessageIdempotencyStore,
    });
    const action = await h.processor.process(makeMessage());
    expect(action).toBe(DeliveryAction.Ack);
    expect(h.handler).toHaveBeenCalledOnce();
  });

  it("#3 the seen-mark is written on the SUCCESS path, before ack", async () => {
    const h = build({ descriptor: idempotent });
    await h.processor.process(makeMessage({ messageId: "id-3" }));
    expect(h.store.markSeen).toHaveBeenCalledWith("id-3");
  });

  it("#4 MarkSeen WRITE failure → NACK-no-requeue → DLQ + operator signal", async () => {
    const h = build({
      descriptor: idempotent,
      store: {
        hasSeen: vi.fn(() => Promise.resolve(ok(false))),
        markSeen: vi.fn(() =>
          Promise.resolve(fail({ errorCode: "SERVICE_UNAVAILABLE" })),
        ),
      } as unknown as IMessageIdempotencyStore,
    });
    const action = await h.processor.process(makeMessage());
    expect(action).toBe(DeliveryAction.Drop);
    expect(h.logger.logged("idempotency mark failed")).toBe(true);
  });

  it("#5 failure paths NEVER mark (a DLQ'd message stays reprocessable)", async () => {
    const h = build({
      descriptor: idempotent,
      handler: vi.fn(() => fail({ errorCode: "X" })),
    });
    await h.processor.process(makeMessage());
    expect(h.store.markSeen).not.toHaveBeenCalled();
  });

  it("skips idempotency entirely when the descriptor opts out", async () => {
    const h = build({ descriptor: { ...baseDescriptor, idempotency: false } });
    await h.processor.process(makeMessage());
    expect(h.store.hasSeen).not.toHaveBeenCalled();
    expect(h.store.markSeen).not.toHaveBeenCalled();
  });

  it("skips idempotency when the message has no valid id", async () => {
    const h = build({ descriptor: idempotent });
    await h.processor.process(makeMessage({ messageId: undefined }));
    expect(h.store.hasSeen).not.toHaveBeenCalled();
  });
});

describe("DeliveryProcessor — tiered-retry attempt cap", () => {
  const tiered: SubscriptionDescriptor = {
    ...baseDescriptor,
    tieredRetry: { maxAttempts: 3, tiersMs: [1000] },
  };

  it("routes to the DLQ with RETRIES_EXHAUSTED once the budget is burned (handler NOT invoked)", async () => {
    const h = build({ descriptor: tiered });
    const action = await h.processor.process(
      makeMessage({
        headers: { "x-death": [{ reason: "expired", count: 3 }] },
      }),
    );
    expect(action).toBe(DeliveryAction.Ack); // republished + ack
    expect(h.handler).not.toHaveBeenCalled();
    const meta = decodeFailure(h.republished[0]!.headers);
    expect(meta[DlqFailureMetadataFields.CAUSE]).toBe(
      DlqFailureCauses.RETRIES_EXHAUSTED,
    );
    expect(meta[DlqFailureMetadataFields.ATTEMPT_COUNT]).toBe(3);
  });

  it("dispatches normally while under the attempt budget", async () => {
    const h = build({ descriptor: tiered });
    await h.processor.process(
      makeMessage({
        headers: { "x-death": [{ reason: "expired", count: 1 }] },
      }),
    );
    expect(h.handler).toHaveBeenCalledOnce();
    expect(h.republished).toHaveLength(0);
  });
});

describe("DeliveryProcessor — consume-side context establishment (§5.2a)", () => {
  it("3a: the handler-visible context carries the producer's propagated fields", async () => {
    let captured: ConsumeContext | undefined;
    const encoded = encodePropagatedHeader({
      requestId: "req-abc",
      requestPath: "/rotate",
      callPath: [
        { id: "edge", kind: "Edge", timestamp: "2026-07-03T00:00:00Z" },
      ],
    });
    const h = build({
      handler: vi.fn((_msg, ctx: ConsumeContext) => {
        captured = ctx;
        return ok();
      }),
    });
    await h.processor.process(
      makeMessage({ headers: { [AmqpHeaders.PROPAGATED_CONTEXT]: encoded } }),
    );
    expect(captured?.propagated.requestId).toBe("req-abc");
    expect(captured?.propagated.callPath).toHaveLength(1);
  });

  it("3a: the DLQ metadata traceId equals the producer traceId (cross-runtime linkage)", async () => {
    const h = build({ handler: vi.fn(() => fail({ errorCode: "X" })) });
    await h.processor.process(
      makeMessage({ headers: { traceparent: SAMPLE_TRACEPARENT } }),
    );
    const meta = decodeFailure(h.republished[0]!.headers);
    expect(meta[DlqFailureMetadataFields.TRACE_ID]).toBe(
      SAMPLE_PRODUCER_TRACE_ID,
    );
  });

  it("3a: a malformed x-d2-context is fail-safe — the message still processes", async () => {
    let captured: ConsumeContext | undefined;
    const h = build({
      handler: vi.fn((_msg, ctx: ConsumeContext) => {
        captured = ctx;
        return ok();
      }),
    });
    const action = await h.processor.process(
      makeMessage({
        headers: { [AmqpHeaders.PROPAGATED_CONTEXT]: "}{garbage" },
      }),
    );
    expect(action).toBe(DeliveryAction.Ack);
    expect(
      Object.keys(captured!.propagated as Record<string, unknown>),
    ).toHaveLength(0);
  });

  it("3b: the decoded message value is handed to the handler alongside the context", async () => {
    let decoded: unknown;
    const h = build({
      handler: vi.fn((msg: unknown) => {
        decoded = msg;
        return ok();
      }),
    });
    await h.processor.process(
      makeMessage({
        body: Buffer.from(JSON.stringify({ domain: "audit" }), "utf8"),
      }),
    );
    expect(decoded).toEqual({ domain: "audit" });
  });
});
