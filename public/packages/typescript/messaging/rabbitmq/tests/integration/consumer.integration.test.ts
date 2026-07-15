// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { existsSync, readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

import {
  RabbitMQContainer,
  type StartedRabbitMQContainer,
} from "@testcontainers/rabbitmq";
import { Connection } from "rabbitmq-client";
import type { ILogger } from "@dcsv-io/d2-logging";
import { fail, ok } from "@dcsv-io/d2-result";
import { AmqpHeaders } from "@dcsv-io/d2-headers-amqp";
import {
  DlqFailureCauses,
  DlqFailureMetadataFields,
} from "@dcsv-io/d2-messaging-abstractions";
import { afterAll, beforeAll, describe, expect, it } from "vitest";

import {
  type ConsumeContext,
  DlqNaming,
  InMemoryMessageIdempotencyStore,
  QueuePattern,
  type SubscriptionDescriptor,
  type Subscription,
  subscribe,
} from "../../src/index.js";

// -----------------------------------------------------------------------
// .NET-emitted golden loading (D11). The goldens are emitted by
// public/packages/dotnet/tests/Integration/ContractFixtures/MqGoldenMessageFixtureEmitter
// into contract-tests/fixtures/mq-messages-golden/.
// -----------------------------------------------------------------------

interface GoldenData {
  readonly bodyBase64: string;
  readonly headers: Readonly<Record<string, string>>;
  readonly exchange?: string;
  readonly routingKey?: string;
  readonly expectedDecoded?: Readonly<Record<string, unknown>>;
  readonly expectedRequestId?: string;
  readonly producerTraceId?: string;
}

function findRepoRoot(startDir: string): string {
  let dir = startDir;
  for (;;) {
    if (existsSync(join(dir, "pnpm-workspace.yaml"))) return dir;
    const parent = dirname(dir);
    if (parent === dir) throw new Error("repo root not found");
    dir = parent;
  }
}

function loadGolden(scenario: string): GoldenData {
  const root = findRepoRoot(dirname(fileURLToPath(import.meta.url)));
  // Dual-tree: goldens live under public/packages/typescript/contract-tests.
  const path = join(
    root,
    "public",
    "packages",
    "typescript",
    "contract-tests",
    "fixtures",
    "mq-messages-golden",
    `${scenario}.json`,
  );
  const raw = JSON.parse(readFileSync(path, "utf8")) as { data: GoldenData };
  return raw.data;
}

function deferred<T>(): { promise: Promise<T>; resolve: (v: T) => void } {
  let resolve!: (v: T) => void;
  const promise = new Promise<T>((r) => {
    resolve = r;
  });
  return { promise, resolve };
}

async function delay(ms: number): Promise<void> {
  await new Promise((r) => setTimeout(r, ms));
}

let uniqueCounter = 0;
function unique(prefix: string): string {
  uniqueCounter += 1;
  return `${prefix}.${Date.now().toString(36)}.${uniqueCounter}`;
}

describe("@dcsv-io/d2-messaging-rabbitmq consumer — Testcontainer wire-contract proof", () => {
  let container: StartedRabbitMQContainer;
  let connection: Connection;

  beforeAll(async () => {
    container = await new RabbitMQContainer("rabbitmq:3.13-management").start();
    // getAmqpUrl() omits credentials; the container's default user is guest/guest.
    const url = container
      .getAmqpUrl()
      .replace("amqp://", "amqp://guest:guest@");
    connection = new Connection(url);
    await connection.onConnect();
  }, 180_000);

  afterAll(async () => {
    await connection?.close();
    await container?.stop();
  });

  function makeDescriptor(
    overrides: Partial<SubscriptionDescriptor> = {},
  ): SubscriptionDescriptor {
    return {
      queueName: unique("q.test"),
      exchange: unique("d2.test"),
      exchangeType: "fanout",
      pattern: QueuePattern.DurableShared,
      routingKeyBinding: "",
      prefetch: 4,
      idempotency: false,
      nackedBy: "test-consumer",
      ...overrides,
    };
  }

  async function publishTo(
    exchange: string,
    golden: GoldenData,
    overrideMessageId?: string,
  ): Promise<void> {
    const publisher = connection.createPublisher({ confirm: true });
    try {
      const body = Buffer.from(golden.bodyBase64, "base64");
      const headers: Record<string, string> = {};
      for (const [k, v] of Object.entries(golden.headers)) {
        if (k === AmqpHeaders.CONTENT_TYPE || k === AmqpHeaders.MESSAGE_ID)
          continue;
        headers[k] = v;
      }
      await publisher.send(
        {
          exchange,
          routingKey: "",
          durable: true,
          contentType:
            golden.headers[AmqpHeaders.CONTENT_TYPE] ??
            "application/octet-stream",
          messageId:
            overrideMessageId ?? golden.headers[AmqpHeaders.MESSAGE_ID],
          headers,
        },
        body,
      );
    } finally {
      await publisher.close();
    }
  }

  async function readDlq(
    dlqName: string,
    timeoutMs = 15_000,
  ): Promise<Record<string, unknown>> {
    const deadline = Date.now() + timeoutMs;
    for (;;) {
      const msg = await connection.basicGet({ queue: dlqName, noAck: true });
      if (msg !== undefined) {
        const reason = msg.headers?.[AmqpHeaders.FAILURE_REASON];
        const reasonStr =
          typeof reason === "string"
            ? reason
            : Buffer.from(reason as Uint8Array).toString("utf8");
        return {
          metadata: JSON.parse(reasonStr) as Record<string, unknown>,
          body: msg.body as Buffer,
          headers: msg.headers ?? {},
        };
      }
      if (Date.now() >= deadline)
        throw new Error(`no DLQ message on ${dlqName}`);
      await delay(200);
    }
  }

  it("replays a real .NET KeyRotatedEvent golden: decodes the body + establishes context", async () => {
    const golden = loadGolden("auth-key-rotated-plaintext");
    const descriptor = makeDescriptor();
    const received = deferred<{ message: unknown; ctx: ConsumeContext }>();

    const sub = subscribe({
      connection,
      descriptor,
      logger: silentLogger(),
      handler: (message, ctx) => {
        received.resolve({ message, ctx });
        return ok();
      },
    });
    await sub.ready;

    await publishTo(descriptor.exchange, golden);
    const { message, ctx } = await withTimeout(received.promise, 15_000);

    // The camelCase .NET body decodes to the expected object.
    expect(message).toEqual(golden.expectedDecoded);
    // The base64url x-d2-context establishes the per-message context.
    expect(ctx.propagated.requestId).toBe(golden.expectedRequestId);
    expect(ctx.propagated.requestPath).toBe("/v2/keys/rotate");

    await sub.close();
  });

  it("routes a real encrypted-domain frame to the DLQ with DECRYPT_FAILURE (fail-loud)", async () => {
    const golden = loadGolden("encrypted-frame");
    const descriptor = makeDescriptor();

    const sub = subscribe({
      connection,
      descriptor,
      logger: silentLogger(),
      handler: () => ok(),
    });
    await sub.ready;

    await publishTo(descriptor.exchange, golden);
    const dlq = await readDlq(DlqNaming.dlqFor(sub.queueName));
    const meta = dlq.metadata as Record<string, unknown>;
    expect(meta[DlqFailureMetadataFields.CAUSE]).toBe(
      DlqFailureCauses.DECRYPT_FAILURE,
    );
    // Original ciphertext body is byte-identical in the DLQ.
    expect(
      (dlq.body as Buffer).equals(Buffer.from(golden.bodyBase64, "base64")),
    ).toBe(true);

    await sub.close();
  });

  it("dead-letters a handler failure with metadata + carries producer headers forward", async () => {
    const golden = loadGolden("auth-key-rotated-plaintext");
    const descriptor = makeDescriptor();

    const sub = subscribe({
      connection,
      descriptor,
      logger: silentLogger(),
      handler: () => fail({ errorCode: "KEYCUSTODIAN_SEAL_KEY_UNAVAILABLE" }),
    });
    await sub.ready;

    await publishTo(descriptor.exchange, golden);
    const dlq = await readDlq(DlqNaming.dlqFor(sub.queueName));
    const meta = dlq.metadata as Record<string, unknown>;
    expect(meta[DlqFailureMetadataFields.CAUSE]).toBe(
      DlqFailureCauses.HANDLER_RESULT_FAILURE,
    );
    expect(meta[DlqFailureMetadataFields.ERROR_CODE]).toBe(
      "KEYCUSTODIAN_SEAL_KEY_UNAVAILABLE",
    );
    expect(meta[DlqFailureMetadataFields.TRACE_ID]).toBe(
      golden.producerTraceId,
    );

    // Producer headers ride forward on the DLQ copy.
    const dlqHeaders = dlq.headers as Record<string, unknown>;
    expect(headerString(dlqHeaders, AmqpHeaders.TRACEPARENT)).toBe(
      golden.headers[AmqpHeaders.TRACEPARENT],
    );
    expect(headerString(dlqHeaders, AmqpHeaders.PROPAGATED_CONTEXT)).toBe(
      golden.headers[AmqpHeaders.PROPAGATED_CONTEXT],
    );

    await sub.close();
  });

  it("deduplicates a repeated message-id (ack-and-skip, handler invoked once)", async () => {
    const golden = loadGolden("auth-key-rotated-plaintext");
    const descriptor = makeDescriptor({ idempotency: true });
    let calls = 0;

    const sub = subscribe({
      connection,
      descriptor,
      logger: silentLogger(),
      store: new InMemoryMessageIdempotencyStore(),
      handler: () => {
        calls += 1;
        return ok();
      },
    });
    await sub.ready;

    const id = "dedup-0192f8c1-1111-7000-8000-0000000000cc";
    await publishTo(descriptor.exchange, golden, id);
    await waitFor(() => calls === 1, 15_000);
    await publishTo(descriptor.exchange, golden, id);
    await delay(1500); // give the duplicate time to be ack-skipped

    expect(calls).toBe(1);
    await sub.close();
  });

  it("splits deliveries across competing consumers (each message processed once)", async () => {
    const descriptor = makeDescriptor();
    const golden = loadGolden("auth-key-rotated-plaintext");
    let total = 0;

    const subs: Subscription[] = [];
    for (let i = 0; i < 2; i++) {
      subs.push(
        subscribe({
          connection,
          descriptor, // SAME queueName → competing consumers on one queue
          logger: silentLogger(),
          handler: () => {
            total += 1;
            return ok();
          },
        }),
      );
    }
    await Promise.all(subs.map((s) => s.ready));

    const count = 6;
    for (let i = 0; i < count; i++)
      await publishTo(descriptor.exchange, golden, `${unique("m")}`);

    await waitFor(() => total === count, 20_000);
    expect(total).toBe(count);

    await Promise.all(subs.map((s) => s.close()));
  });
});

function headerString(
  headers: Record<string, unknown>,
  name: string,
): string | undefined {
  const raw = headers[name];
  if (typeof raw === "string") return raw;
  if (raw instanceof Uint8Array) return Buffer.from(raw).toString("utf8");
  return undefined;
}

async function waitFor(cond: () => boolean, timeoutMs: number): Promise<void> {
  const deadline = Date.now() + timeoutMs;
  while (!cond()) {
    if (Date.now() >= deadline) throw new Error("waitFor timed out");
    await delay(100);
  }
}

async function withTimeout<T>(p: Promise<T>, ms: number): Promise<T> {
  return Promise.race([
    p,
    new Promise<T>((_r, reject) =>
      setTimeout(() => reject(new Error("timeout")), ms),
    ),
  ]);
}

function silentLogger(): ILogger {
  const noop = (): void => undefined;
  const logger: ILogger = {
    trace: noop,
    debug: noop,
    info: noop,
    warn: noop,
    error: noop,
    fatal: noop,
    child: () => logger,
  };
  return logger;
}
