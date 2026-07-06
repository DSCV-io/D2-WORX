// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";

import { DlqNaming } from "../src/topology/dlq-naming.js";
import { QueuePattern, queueFlagsFor } from "../src/topology/queue-pattern.js";
import { planTopology } from "../src/topology/topology-plan.js";
import {
  resolveQueueName,
  type SubscriptionDescriptor,
} from "../src/topology/subscription-descriptor.js";

const baseDescriptor: SubscriptionDescriptor = {
  queueName: "audit.key-rotated",
  exchange: "d2.security.key-rotated",
  exchangeType: "fanout",
  pattern: QueuePattern.DurableShared,
  routingKeyBinding: "",
  prefetch: 8,
  idempotency: false,
};

describe("DlqNaming — byte-identical to the .NET convention", () => {
  it("derives dlx / dlq / retry names from the queue name", () => {
    expect(DlqNaming.dlxFor("q")).toBe("q.dlx");
    expect(DlqNaming.dlqFor("q")).toBe("q.dlq");
    expect(DlqNaming.retryTierExchangeFor("q", 0)).toBe("q.retry.0");
    expect(DlqNaming.retryTierQueueFor("q", 2)).toBe("q.retry.2");
    expect(DlqNaming.retryReturnExchangeFor("q")).toBe("q.retry.return");
  });
});

describe("queueFlagsFor — mirrors DefaultTopologyDeclarer.QueueFlagsFor", () => {
  it("CompetingConsumer + DurableShared are durable, non-exclusive", () => {
    expect(queueFlagsFor(QueuePattern.CompetingConsumer)).toEqual({
      durable: true,
      exclusive: false,
      autoDelete: false,
    });
    expect(queueFlagsFor(QueuePattern.DurableShared)).toEqual({
      durable: true,
      exclusive: false,
      autoDelete: false,
    });
  });

  it("FanoutExclusiveAutoDelete is non-durable, exclusive, auto-delete", () => {
    expect(queueFlagsFor(QueuePattern.FanoutExclusiveAutoDelete)).toEqual({
      durable: false,
      exclusive: true,
      autoDelete: true,
    });
  });

  it("throws on an unknown pattern", () => {
    expect(() => queueFlagsFor("bogus" as QueuePattern)).toThrow(
      /Unknown queue pattern/,
    );
  });
});

describe("resolveQueueName — per-process suffix for fanout-exclusive only", () => {
  it("leaves non-fanout patterns unchanged", () => {
    expect(resolveQueueName(baseDescriptor)).toBe("audit.key-rotated");
  });

  it("appends an explicit suffix for fanout-exclusive", () => {
    const d: SubscriptionDescriptor = {
      ...baseDescriptor,
      pattern: QueuePattern.FanoutExclusiveAutoDelete,
    };
    expect(resolveQueueName(d, "abcd1234")).toBe("audit.key-rotated.abcd1234");
  });

  it("generates an 8-char hex suffix when none supplied", () => {
    const d: SubscriptionDescriptor = {
      ...baseDescriptor,
      pattern: QueuePattern.FanoutExclusiveAutoDelete,
    };
    const resolved = resolveQueueName(d);
    expect(resolved).toMatch(/^audit\.key-rotated\.[0-9a-f]{8}$/);
  });
});

describe("planTopology — the DLQ/DLX wire-contract vs .NET DefaultTopologyDeclarer", () => {
  it("declares primary exchange + DLX + DLQ + primary queue with matching args", () => {
    const plan = planTopology(baseDescriptor, "audit.key-rotated");

    // Exactly two exchanges: primary + DLX (fanout).
    expect(plan.exchanges).toEqual([
      {
        exchange: "d2.security.key-rotated",
        type: "fanout",
        durable: true,
        autoDelete: false,
      },
      {
        exchange: "audit.key-rotated.dlx",
        type: "fanout",
        durable: true,
        autoDelete: false,
      },
    ]);

    // Exactly two queues: DLQ + primary (primary carries the DLX args).
    expect(plan.queues).toEqual([
      {
        queue: "audit.key-rotated.dlq",
        durable: true,
        exclusive: false,
        autoDelete: false,
      },
      {
        queue: "audit.key-rotated",
        durable: true,
        exclusive: false,
        autoDelete: false,
        arguments: {
          "x-dead-letter-exchange": "audit.key-rotated.dlx",
          "x-dead-letter-routing-key": "",
        },
      },
    ]);

    // Exactly two bindings: DLQ→DLX and primary→exchange.
    expect(plan.bindings).toEqual([
      {
        queue: "audit.key-rotated.dlq",
        exchange: "audit.key-rotated.dlx",
        routingKey: "",
      },
      {
        queue: "audit.key-rotated",
        exchange: "d2.security.key-rotated",
        routingKey: "",
      },
    ]);

    expect(plan.primaryQueue.queue).toBe("audit.key-rotated");
  });

  it("applies the fanout-exclusive durability flags to the primary queue", () => {
    const d: SubscriptionDescriptor = {
      ...baseDescriptor,
      pattern: QueuePattern.FanoutExclusiveAutoDelete,
    };
    const plan = planTopology(d, "audit.key-rotated.abcd1234");

    expect(plan.primaryQueue).toMatchObject({
      queue: "audit.key-rotated.abcd1234",
      durable: false,
      exclusive: true,
      autoDelete: true,
    });
  });

  it("uses the descriptor routing key for a topic binding", () => {
    const d: SubscriptionDescriptor = {
      ...baseDescriptor,
      exchangeType: "topic",
      routingKeyBinding: "audit.*",
    };
    const plan = planTopology(d, "audit.key-rotated");
    expect(plan.exchanges[0]).toMatchObject({ type: "topic" });
    expect(plan.bindings[1]).toEqual({
      queue: "audit.key-rotated",
      exchange: "d2.security.key-rotated",
      routingKey: "audit.*",
    });
  });

  it("stands up the retry-tier topology when tieredRetry is declared", () => {
    const d: SubscriptionDescriptor = {
      ...baseDescriptor,
      tieredRetry: { maxAttempts: 3, tiersMs: [1000, 5000] },
    };
    const plan = planTopology(d, "audit.key-rotated");

    // + return exchange + 2 tier exchanges = 4 total.
    expect(plan.exchanges.map((e) => e.exchange)).toEqual([
      "d2.security.key-rotated",
      "audit.key-rotated.dlx",
      "audit.key-rotated.retry.return",
      "audit.key-rotated.retry.0",
      "audit.key-rotated.retry.1",
    ]);

    // + 2 tier queues = 4 total (dlq, primary, tier.0, tier.1).
    const tier0 = plan.queues.find(
      (q) => q.queue === "audit.key-rotated.retry.0",
    );
    expect(tier0?.arguments).toEqual({
      "x-message-ttl": 1000,
      "x-dead-letter-exchange": "audit.key-rotated.retry.return",
      "x-dead-letter-routing-key": "",
    });
    const tier1 = plan.queues.find(
      (q) => q.queue === "audit.key-rotated.retry.1",
    );
    expect(tier1?.arguments).toMatchObject({ "x-message-ttl": 5000 });

    // primary is bound BACK to the return exchange for the retry cycle.
    expect(plan.bindings).toContainEqual({
      queue: "audit.key-rotated",
      exchange: "audit.key-rotated.retry.return",
      routingKey: "",
    });
    expect(plan.bindings).toContainEqual({
      queue: "audit.key-rotated.retry.0",
      exchange: "audit.key-rotated.retry.0",
      routingKey: "",
    });
  });
});
