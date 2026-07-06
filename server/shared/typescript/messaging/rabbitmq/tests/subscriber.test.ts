// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import {
  type AsyncMessage,
  type Connection,
  ConsumerStatus,
} from "rabbitmq-client";
import { fail, ok } from "@d2/result";
import { describe, expect, it, vi } from "vitest";

import { subscribe } from "../src/subscribing/subscriber.js";
import { QueuePattern } from "../src/topology/queue-pattern.js";
import { type SubscriptionDescriptor } from "../src/topology/subscription-descriptor.js";
import { RecordingLogger } from "./helpers.js";

const descriptor: SubscriptionDescriptor = {
  queueName: "audit.key-rotated",
  exchange: "d2.security.key-rotated",
  exchangeType: "fanout",
  pattern: QueuePattern.DurableShared,
  routingKeyBinding: "",
  prefetch: 8,
  idempotency: false,
};

type ConsumerCb = (
  msg: AsyncMessage,
) => Promise<ConsumerStatus | void> | ConsumerStatus | void;

class FakeConsumer {
  started = false;
  closed = false;
  private readonly cbs = new Map<string, ((arg?: unknown) => void)[]>();

  constructor(
    readonly props: Record<string, unknown>,
    readonly handlerCb: ConsumerCb,
  ) {}

  start(): void {
    this.started = true;
  }

  close(): Promise<void> {
    this.closed = true;
    return Promise.resolve();
  }

  on(event: string, cb: (arg?: unknown) => void): this {
    const list = this.cbs.get(event) ?? [];
    list.push(cb);
    this.cbs.set(event, list);
    return this;
  }

  emit(event: string, arg?: unknown): void {
    for (const cb of this.cbs.get(event) ?? []) cb(arg);
  }
}

class FakePublisher {
  closed = false;
  readonly sent: unknown[] = [];
  sendImpl: () => Promise<void> = () => Promise.resolve();

  send(envelope: unknown): Promise<void> {
    this.sent.push(envelope);
    return this.sendImpl();
  }

  close(): Promise<void> {
    this.closed = true;
    return Promise.resolve();
  }
}

class FakeConnection {
  readonly exchangeDeclare = vi.fn(() => Promise.resolve());
  queueDeclare = vi.fn(() => Promise.resolve());
  readonly queueBind = vi.fn(() => Promise.resolve());
  readonly connectionCbs: (() => void)[] = [];
  consumer!: FakeConsumer;
  publisher!: FakePublisher;

  on(event: string, cb: () => void): this {
    if (event === "connection") this.connectionCbs.push(cb);
    return this;
  }

  off(event: string, cb: () => void): this {
    if (event === "connection") {
      const i = this.connectionCbs.indexOf(cb);
      if (i !== -1) this.connectionCbs.splice(i, 1);
    }

    return this;
  }

  createPublisher(): FakePublisher {
    this.publisher = new FakePublisher();
    return this.publisher;
  }

  createConsumer(props: Record<string, unknown>, cb: ConsumerCb): FakeConsumer {
    this.consumer = new FakeConsumer(props, cb);
    return this.consumer;
  }

  fireConnection(): void {
    for (const cb of this.connectionCbs) cb();
  }
}

function makeAsyncMessage(overrides: Partial<AsyncMessage> = {}): AsyncMessage {
  return {
    body: Buffer.from(JSON.stringify({ ok: true }), "utf8"),
    messageId: "0192f8c1-1111-7000-8000-000000000001",
    headers: {},
    deliveryTag: 1,
    redelivered: false,
    exchange: "d2.security.key-rotated",
    routingKey: "",
    consumerTag: "ct",
    ...overrides,
  } as AsyncMessage;
}

async function start(
  conn: FakeConnection,
  opts: Partial<Parameters<typeof subscribe>[0]> = {},
): Promise<{ sub: ReturnType<typeof subscribe>; logger: RecordingLogger }> {
  const logger = opts.logger ?? new RecordingLogger();
  const sub = subscribe({
    connection: conn as unknown as Connection,
    descriptor,
    handler: () => ok(),
    logger,
    ...opts,
  });
  await vi.waitFor(() => expect(conn.consumer.started).toBe(true));
  conn.consumer.emit("ready");
  await sub.ready;
  return { sub, logger: logger as RecordingLogger };
}

describe("subscribe — consumer host wiring", () => {
  it("passes the .NET topology into ConsumerProps and starts lazily", async () => {
    const conn = new FakeConnection();
    await start(conn);

    const props = conn.consumer.props;
    expect(props["queue"]).toBe("audit.key-rotated");
    expect(props["lazy"]).toBe(true);
    expect(props["requeue"]).toBe(false);
    expect(props["concurrency"]).toBe(8);
    expect(props["qos"]).toEqual({ prefetchCount: 8 });
    expect((props["queueOptions"] as { queue: string }).queue).toBe(
      "audit.key-rotated",
    );
    expect((props["exchanges"] as unknown[]).length).toBe(2);
    const bindings = props["queueBindings"] as { queue: string }[];
    expect(bindings.every((b) => b.queue === "audit.key-rotated")).toBe(true);
    expect(conn.consumer.started).toBe(true);
  });

  it("declares the aux (DLQ) topology up front and again on reconnect", async () => {
    const conn = new FakeConnection();
    await start(conn);
    const afterStart = conn.queueDeclare.mock.calls.length;
    expect(afterStart).toBeGreaterThan(0); // DLQ declared

    conn.fireConnection();
    await vi.waitFor(() =>
      expect(conn.queueDeclare.mock.calls.length).toBeGreaterThan(afterStart),
    );
  });

  it("logs (never throws) when reconnect topology re-declaration fails", async () => {
    const conn = new FakeConnection();
    const { logger } = await start(conn);
    conn.queueDeclare = vi.fn(() =>
      Promise.reject(new Error("declare failed")),
    );
    conn.fireConnection();
    await vi.waitFor(() =>
      expect(logger.logged("aux topology declaration failed")).toBe(true),
    );
  });

  it("maps a successful delivery to ConsumerStatus.ACK", async () => {
    const conn = new FakeConnection();
    await start(conn);
    const status = await conn.consumer.handlerCb(makeAsyncMessage());
    expect(status).toBe(ConsumerStatus.ACK);
  });

  it("maps a republish-failed delivery to ConsumerStatus.DROP", async () => {
    const conn = new FakeConnection();
    await start(conn, { handler: () => fail({ errorCode: "X" }) });
    conn.publisher.sendImpl = () => Promise.reject(new Error("broker down"));
    const status = await conn.consumer.handlerCb(makeAsyncMessage());
    expect(status).toBe(ConsumerStatus.DROP);
  });

  it("normalizes every AsyncMessage body shape (buffer / string / object / bytes)", async () => {
    const conn = new FakeConnection();
    await start(conn);
    const json = JSON.stringify({ v: 1 });
    for (const body of [
      Buffer.from(json, "utf8"),
      json,
      { v: 1 },
      new Uint8Array(Buffer.from(json, "utf8")),
    ]) {
      const status = await conn.consumer.handlerCb(makeAsyncMessage({ body }));
      expect(status).toBe(ConsumerStatus.ACK);
    }
  });

  it("tolerates an AsyncMessage with no headers", async () => {
    const conn = new FakeConnection();
    await start(conn);
    const status = await conn.consumer.handlerCb(
      makeAsyncMessage({ headers: undefined }),
    );
    expect(status).toBe(ConsumerStatus.ACK);
  });

  it("logs a consumer error event", async () => {
    const conn = new FakeConnection();
    const { logger } = await start(conn);
    conn.consumer.emit("error", new Error("channel gone"));
    expect(logger.logged("consumer error")).toBe(true);
  });

  it("closes the consumer + publisher on close()", async () => {
    const conn = new FakeConnection();
    const { sub } = await start(conn);
    await sub.close();
    expect(conn.consumer.closed).toBe(true);
    expect(conn.publisher.closed).toBe(true);
  });

  it("deregisters the reconnect listener on close (no re-fire after close)", async () => {
    const conn = new FakeConnection();
    const { sub } = await start(conn);
    expect(conn.connectionCbs.length).toBe(1); // one reconnect listener registered

    await sub.close();
    expect(conn.connectionCbs.length).toBe(0); // deregistered by close()

    // A post-close reconnect must NOT redeclare a dead subscriber's aux topology.
    const exchangesBefore = conn.exchangeDeclare.mock.calls.length;
    conn.fireConnection();
    expect(conn.exchangeDeclare.mock.calls.length).toBe(exchangesBefore);
  });

  it("resolves the fanout queue name with a per-process suffix", async () => {
    const conn = new FakeConnection();
    const { sub } = await start(conn, {
      descriptor: {
        ...descriptor,
        pattern: QueuePattern.FanoutExclusiveAutoDelete,
      },
      queueSuffix: "abcd1234",
    });
    expect(sub.queueName).toBe("audit.key-rotated.abcd1234");
  });
});
