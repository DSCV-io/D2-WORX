import { describe, it, expect, vi, beforeAll, afterAll, afterEach } from "vitest";
import { D2Result } from "@d2/result";
import { createNotificationConsumer, declareRetryTopology } from "@d2/comms-infra";
import { COMMS_EVENTS } from "@d2/comms-client";
import { COMMS_RETRY } from "@d2/comms-domain";
import type { ServiceProvider, ServiceScope } from "@d2/di";
import {
  startRabbitMQ,
  stopRabbitMQ,
  getBus,
  withTimeout,
} from "./helpers/rabbitmq-test-helpers.js";

/**
 * Integration test for createNotificationConsumer against a real RabbitMQ instance.
 *
 * Verifies the full publish -> consume -> dispatch flow:
 *   Publisher -> comms.notifications exchange -> comms.notifications queue -> consumer -> Deliver handler
 */
describe("NotificationConsumer (integration)", () => {
  // Track resources for cleanup — prevents leaked consumers from stealing messages
  const cleanupFns: (() => Promise<void>)[] = [];

  beforeAll(async () => {
    await startRabbitMQ();
    // Declare retry topology before tests (mirrors composition root)
    await declareRetryTopology(getBus());
  }, 60_000);

  afterEach(async () => {
    // Close all consumers/publishers created in the test, even if assertions failed
    for (const fn of cleanupFns) {
      try {
        await fn();
      } catch {
        // Ignore close errors during cleanup
      }
    }
    cleanupFns.length = 0;
  });

  afterAll(stopRabbitMQ);

  function createMockHandler() {
    return {
      handleAsync: vi.fn().mockResolvedValue(D2Result.ok({ data: {} })),
    };
  }

  function createMockLogger() {
    return {
      debug: vi.fn(),
      info: vi.fn(),
      warn: vi.fn(),
      error: vi.fn(),
      fatal: vi.fn(),
      child: vi.fn(),
    };
  }

  /**
   * Creates a mock ServiceProvider + createScope that resolves the given handler
   * for any ServiceKey. Mirrors the real composition root's createServiceScope.
   */
  function createMockProviderAndScope(mockHandler?: { handleAsync: ReturnType<typeof vi.fn> }) {
    const handler = mockHandler ?? createMockHandler();
    const createScope = vi.fn().mockImplementation(() => {
      return {
        resolve: vi.fn().mockReturnValue(handler),
        dispose: vi.fn(),
      } as unknown as ServiceScope;
    });
    const provider = {} as ServiceProvider;
    return { provider, createScope, handler };
  }

  /** Creates a publisher and registers it for automatic cleanup. */
  function createTrackedPublisher(
    options?: Parameters<ReturnType<typeof getBus>["createPublisher"]>[0],
  ) {
    const pub = getBus().createPublisher(options);
    cleanupFns.push(() => pub.close());
    return pub;
  }

  /** Creates a standard notification message matching the NotifyInput shape. */
  function createNotificationMessage(overrides?: Record<string, unknown>) {
    return {
      recipientContactId: "00000000-0000-0000-0000-000000000001",
      title: "Test Notification",
      content: "# Hello\nThis is a test.",
      plaintext: "Hello, this is a test.",
      channels: ["email"],
      correlationId: "corr-123",
      senderService: "auth",
      ...overrides,
    };
  }

  it("should consume a notification message from RabbitMQ", async () => {
    const bus = getBus();
    const { provider, createScope, handler } = createMockProviderAndScope();
    const retryPublisher = createTrackedPublisher();
    const logger = createMockLogger();

    let resolveDispatched: () => void;
    const dispatched = new Promise<void>((resolve) => {
      resolveDispatched = resolve;
    });
    handler.handleAsync.mockImplementation(async () => {
      resolveDispatched();
      return D2Result.ok({ data: {} });
    });

    const consumer = createNotificationConsumer({
      messageBus: bus,
      provider,
      createScope,
      retryPublisher,
      logger: logger as never,
    });
    cleanupFns.push(() => consumer.close());
    await consumer.ready;

    const publisher = createTrackedPublisher({
      exchanges: [{ exchange: COMMS_EVENTS.NOTIFICATIONS_EXCHANGE, type: "fanout" }],
    });
    await publisher.send(
      { exchange: COMMS_EVENTS.NOTIFICATIONS_EXCHANGE, routingKey: "" },
      createNotificationMessage(),
    );

    await withTimeout(dispatched);

    expect(handler.handleAsync).toHaveBeenCalledOnce();

    const input = handler.handleAsync.mock.calls[0][0];
    expect(input.recipientContactId).toBe("00000000-0000-0000-0000-000000000001");
    expect(input.title).toBe("Test Notification");
    expect(input.content).toBe("# Hello\nThis is a test.");
    expect(input.plainTextContent).toBe("Hello, this is a test.");
    expect(input.channels).toEqual(["email"]);
    expect(input.correlationId).toBe("corr-123");
    expect(input.senderService).toBe("auth");
  });

  it("should handle multiple messages in sequence", async () => {
    const bus = getBus();
    const { provider, createScope, handler } = createMockProviderAndScope();
    const retryPublisher = createTrackedPublisher();
    const logger = createMockLogger();

    let callCount = 0;
    let resolveAll: () => void;
    const allDispatched = new Promise<void>((resolve) => {
      resolveAll = resolve;
    });

    handler.handleAsync.mockImplementation(async () => {
      callCount++;
      if (callCount >= 3) resolveAll();
      return D2Result.ok({ data: {} });
    });

    const consumer = createNotificationConsumer({
      messageBus: bus,
      provider,
      createScope,
      retryPublisher,
      logger: logger as never,
    });
    cleanupFns.push(() => consumer.close());
    await consumer.ready;

    const publisher = createTrackedPublisher({
      exchanges: [{ exchange: COMMS_EVENTS.NOTIFICATIONS_EXCHANGE, type: "fanout" }],
    });

    await publisher.send(
      { exchange: COMMS_EVENTS.NOTIFICATIONS_EXCHANGE, routingKey: "" },
      createNotificationMessage({
        recipientContactId: "00000000-0000-0000-0000-000000000011",
        correlationId: "corr-1",
        title: "Notification 1",
      }),
    );
    await publisher.send(
      { exchange: COMMS_EVENTS.NOTIFICATIONS_EXCHANGE, routingKey: "" },
      createNotificationMessage({
        recipientContactId: "00000000-0000-0000-0000-000000000012",
        correlationId: "corr-2",
        title: "Notification 2",
      }),
    );
    await publisher.send(
      { exchange: COMMS_EVENTS.NOTIFICATIONS_EXCHANGE, routingKey: "" },
      createNotificationMessage({
        recipientContactId: "00000000-0000-0000-0000-000000000013",
        correlationId: "corr-3",
        title: "Notification 3",
      }),
    );

    await withTimeout(allDispatched);

    expect(callCount).toBe(3);
  });

  it("should drop message with invalid UUID for recipientContactId", async () => {
    const bus = getBus();
    const { provider, createScope, handler } = createMockProviderAndScope();
    const retryPublisher = createTrackedPublisher();
    const logger = createMockLogger();

    const consumer = createNotificationConsumer({
      messageBus: bus,
      provider,
      createScope,
      retryPublisher,
      logger: logger as never,
    });
    cleanupFns.push(() => consumer.close());
    await consumer.ready;

    const publisher = createTrackedPublisher({
      exchanges: [{ exchange: COMMS_EVENTS.NOTIFICATIONS_EXCHANGE, type: "fanout" }],
    });

    // Send a message with an invalid UUID for recipientContactId
    await publisher.send(
      { exchange: COMMS_EVENTS.NOTIFICATIONS_EXCHANGE, routingKey: "" },
      createNotificationMessage({ recipientContactId: "not-a-uuid" }),
    );

    // Wait enough time for the consumer to process the message
    await new Promise((resolve) => setTimeout(resolve, 2_000));

    // Handler should NOT have been called — consumer drops invalid messages before dispatch
    expect(handler.handleAsync).not.toHaveBeenCalled();
    // Consumer should have logged a warning about validation failure
    expect(logger.warn).toHaveBeenCalledWith(
      expect.stringContaining("Invalid notification message"),
      expect.objectContaining({ errors: expect.any(Array) }),
    );
  });

  it("should drop message with missing required fields", async () => {
    const bus = getBus();
    const { provider, createScope, handler } = createMockProviderAndScope();
    const retryPublisher = createTrackedPublisher();
    const logger = createMockLogger();

    const consumer = createNotificationConsumer({
      messageBus: bus,
      provider,
      createScope,
      retryPublisher,
      logger: logger as never,
    });
    cleanupFns.push(() => consumer.close());
    await consumer.ready;

    const publisher = createTrackedPublisher({
      exchanges: [{ exchange: COMMS_EVENTS.NOTIFICATIONS_EXCHANGE, type: "fanout" }],
    });

    // Send a message missing the required "title" field
    const { title: _, ...noTitle } = createNotificationMessage();
    await publisher.send(
      { exchange: COMMS_EVENTS.NOTIFICATIONS_EXCHANGE, routingKey: "" },
      noTitle,
    );

    await new Promise((resolve) => setTimeout(resolve, 2_000));

    expect(handler.handleAsync).not.toHaveBeenCalled();
    expect(logger.warn).toHaveBeenCalledWith(
      expect.stringContaining("Invalid notification message"),
      expect.objectContaining({ errors: expect.any(Array) }),
    );
  });

  it("should drop a completely empty message object", async () => {
    const bus = getBus();
    const { provider, createScope, handler } = createMockProviderAndScope();
    const retryPublisher = createTrackedPublisher();
    const logger = createMockLogger();

    const consumer = createNotificationConsumer({
      messageBus: bus,
      provider,
      createScope,
      retryPublisher,
      logger: logger as never,
    });
    cleanupFns.push(() => consumer.close());
    await consumer.ready;

    const publisher = createTrackedPublisher({
      exchanges: [{ exchange: COMMS_EVENTS.NOTIFICATIONS_EXCHANGE, type: "fanout" }],
    });

    // Send a completely empty object
    await publisher.send({ exchange: COMMS_EVENTS.NOTIFICATIONS_EXCHANGE, routingKey: "" }, {});

    await new Promise((resolve) => setTimeout(resolve, 2_000));

    expect(handler.handleAsync).not.toHaveBeenCalled();
    expect(logger.warn).toHaveBeenCalledWith(
      expect.stringContaining("Invalid notification message"),
      expect.objectContaining({ errors: expect.any(Array) }),
    );
  });

  it("should retry a failed message via tier queue and redeliver to main queue", async () => {
    const bus = getBus();
    const retryPublisher = createTrackedPublisher();
    const logger = createMockLogger();

    let callCount = 0;
    let resolveSecondCall: () => void;
    const secondCall = new Promise<void>((resolve) => {
      resolveSecondCall = resolve;
    });

    const handler = createMockHandler();
    handler.handleAsync.mockImplementation(async () => {
      callCount++;
      if (callCount === 1) {
        // First call: return DELIVERY_FAILED to trigger retry
        return D2Result.fail({
          messages: ["comms_errors_DELIVERY_RETRY_SCHEDULED"],
          statusCode: 503,
          errorCode: COMMS_RETRY.DELIVERY_FAILED,
        });
      }
      resolveSecondCall();
      return D2Result.ok({ data: {} });
    });

    const { provider, createScope } = createMockProviderAndScope(handler);

    const consumer = createNotificationConsumer({
      messageBus: bus,
      provider,
      createScope,
      retryPublisher,
      logger: logger as never,
    });
    cleanupFns.push(() => consumer.close());
    await consumer.ready;

    const publisher = createTrackedPublisher({
      exchanges: [{ exchange: COMMS_EVENTS.NOTIFICATIONS_EXCHANGE, type: "fanout" }],
    });
    await publisher.send(
      { exchange: COMMS_EVENTS.NOTIFICATIONS_EXCHANGE, routingKey: "" },
      createNotificationMessage({
        recipientContactId: "00000000-0000-0000-0000-000000000021",
        correlationId: "corr-retry-1",
        title: "Retry Notification",
      }),
    );

    // tier-1 has 5s TTL, so allow up to 15s for the full retry cycle
    await withTimeout(secondCall, 15_000);

    expect(callCount).toBe(2);
    expect(logger.warn).toHaveBeenCalledWith(
      "Handler failed, scheduling retry",
      expect.objectContaining({ retryCount: 1 }),
    );
  }, 20_000);
});
