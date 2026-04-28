import { describe, it, expect, vi, beforeAll, afterAll, afterEach, beforeEach } from "vitest";
import { generateUuidV7 } from "@d2/utilities";
import { D2Result } from "@d2/result";
import { ServiceCollection } from "@d2/di";
import {
  HandlerContext,
  IHandlerContextKey,
  IRequestContextKey,
  createServiceScope,
} from "@d2/handler";
import { ILoggerKey, createLogger } from "@d2/logging";
import type { ILogger } from "@d2/logging";
import { ConsumerResult } from "@d2/messaging";
import type { Complex } from "@d2/geo-client";
import type { WhoIsDTO } from "@d2/protos";
import { AUTH_MESSAGING } from "@d2/auth-domain";
import { IUpdateSignInEventWhoIsIdKey, type SignInEventRepoHandlers } from "@d2/auth-app";
import {
  createSignInEventRepoHandlers,
  addAuthInfra,
  createWhoIsResolutionConsumer,
} from "@d2/auth-infra";
import { startPostgres, stopPostgres, getDb, cleanCustomTables } from "./postgres-test-helpers.js";
import { startRabbitMQ, stopRabbitMQ, getBus, withTimeout } from "./rabbitmq-test-helpers.js";
import type { SignInEvent } from "@d2/auth-domain";

function createTestContext() {
  return new HandlerContext(
    {
      traceId: "trace-whois-consumer",
      isAuthenticated: false,
      isTrustedService: null,
      isAgentStaff: false,
      isAgentAdmin: false,
      isTargetingStaff: false,
      isTargetingAdmin: false,
      isOrgEmulating: false,
      isUserImpersonating: false,
    },
    createLogger({ level: "silent" as never }),
  );
}

function makeEvent(overrides?: Partial<SignInEvent>): SignInEvent {
  return {
    id: generateUuidV7(),
    userId: "user-1",
    successful: true,
    ipAddress: "203.0.113.42",
    userAgent: "Mozilla/5.0 Integration Test",
    whoIsId: undefined,
    createdAt: new Date(),
    ...overrides,
  };
}

describe("WhoIsResolutionConsumer (integration)", () => {
  let repo: SignInEventRepoHandlers;
  let logger: ILogger;
  const cleanupFns: (() => Promise<void>)[] = [];

  beforeAll(async () => {
    await startPostgres();
    await startRabbitMQ();
    const ctx = createTestContext();
    repo = createSignInEventRepoHandlers(getDb(), ctx);
    logger = createLogger({ level: "silent" as never });
  }, 120_000);

  afterAll(async () => {
    await stopRabbitMQ();
    await stopPostgres();
  });

  afterEach(async () => {
    // Close all consumers/publishers created in the test — prevents
    // competing consumers on shared queues across tests
    for (const fn of cleanupFns) {
      try {
        await fn();
      } catch {
        // Ignore close errors during cleanup
      }
    }
    cleanupFns.length = 0;
  });

  beforeEach(async () => {
    await cleanCustomTables();
  });

  function createMockFindWhoIs(hashId: string | undefined): Complex.IFindWhoIsHandler {
    const whoIs: WhoIsDTO | undefined = hashId
      ? {
          hashId,
          ipAddress: "203.0.113.42",
          fingerprint: "test-fp",
          city: "Test City",
          country: "US",
          countryName: "United States",
          regionName: "Test",
          isp: "Test ISP",
          isVpn: false,
          isProxy: false,
          isTor: false,
          latitude: 0,
          longitude: 0,
          timezone: "UTC",
          createdAt: new Date().toISOString(),
          locationHashId: "loc-hash",
        }
      : undefined;

    return {
      handleAsync: vi.fn().mockResolvedValue(D2Result.ok({ data: { whoIs } })),
      redaction: { inputFields: ["ipAddress", "fingerprint"], suppressOutput: true },
    } as unknown as Complex.IFindWhoIsHandler;
  }

  function buildProvider() {
    const services = new ServiceCollection();
    services.addInstance(ILoggerKey, logger);
    services.addScoped(
      IHandlerContextKey,
      (sp) => new HandlerContext(sp.resolve(IRequestContextKey), sp.resolve(ILoggerKey)),
    );
    addAuthInfra(services, getDb());
    return services.build();
  }

  it("should consume messages from a direct exchange", async () => {
    const bus = getBus();
    let received = false;

    const consumer = bus.subscribeEnriched<unknown>(
      {
        queue: "test-sanity-queue",
        exchanges: [{ exchange: "test-sanity-exchange", type: "direct" as const }],
        queueBindings: [
          {
            exchange: "test-sanity-exchange",
            routingKey: "test-sanity-queue",
          },
        ],
      },
      async () => {
        received = true;
        return ConsumerResult.ACK;
      },
    );
    cleanupFns.push(() => consumer.close());
    await consumer.ready;

    const publisher = bus.createPublisher({
      exchanges: [{ exchange: "test-sanity-exchange", type: "direct" as const }],
    });
    cleanupFns.push(() => publisher.close());
    await publisher.send(
      { exchange: "test-sanity-exchange", routingKey: "test-sanity-queue" },
      { msg: "hello" },
    );

    await new Promise((r) => setTimeout(r, 2000));
    expect(received).toBe(true);
  }, 15_000);

  it("should update whoIsId on sign-in event after consuming message", async () => {
    // 1. Create a sign-in event with undefined whoIsId
    const event = makeEvent();
    await repo.create.handleAsync({ event });

    // Verify whoIsId is undefined initially
    const before = await repo.findByUserId.handleAsync({
      userId: event.userId,
      limit: 1,
      offset: 0,
    });
    expect(before.data!.events[0].whoIsId).toBeUndefined();

    // 2. Set up the consumer with a mock FindWhoIs that returns a known hash
    const expectedHashId = "a".repeat(64);
    const findWhoIs = createMockFindWhoIs(expectedHashId);
    const provider = buildProvider();

    let resolveProcessed: () => void;
    const processed = new Promise<void>((resolve) => {
      resolveProcessed = resolve;
    });

    // Wrap findWhoIs to detect when processing completes
    const originalHandleAsync = findWhoIs.handleAsync.bind(findWhoIs);
    (findWhoIs as any).handleAsync = vi.fn().mockImplementation(async (input: any) => {
      const result = await originalHandleAsync(input);
      // Give the consumer a tick to complete the DB update before resolving
      setTimeout(() => resolveProcessed(), 50);
      return result;
    });

    const consumer = createWhoIsResolutionConsumer({
      messageBus: getBus(),
      provider,
      createScope: (p) => createServiceScope(p, logger),
      findWhoIs,
      logger,
    });
    cleanupFns.push(() => consumer.close());
    await consumer.ready;

    // 3. Publish a WhoIs resolution message
    const publisher = getBus().createPublisher({
      exchanges: [
        {
          exchange: AUTH_MESSAGING.WHOIS_RESOLUTION_EXCHANGE,
          type: AUTH_MESSAGING.WHOIS_RESOLUTION_EXCHANGE_TYPE,
        },
      ],
    });
    cleanupFns.push(() => publisher.close());

    await publisher.send(
      {
        exchange: AUTH_MESSAGING.WHOIS_RESOLUTION_EXCHANGE,
        routingKey: AUTH_MESSAGING.WHOIS_RESOLUTION_QUEUE,
      },
      {
        signInEventId: event.id,
        ipAddress: event.ipAddress,
        userAgent: event.userAgent,
      },
    );

    // 4. Wait for consumer to process
    await withTimeout(processed);
    // Allow DB update to settle
    await new Promise((r) => setTimeout(r, 200));

    // 5. Verify the DB record now has the whoIsId
    const after = await repo.findByUserId.handleAsync({
      userId: event.userId,
      limit: 1,
      offset: 0,
    });
    expect(after.data!.events[0].whoIsId).toBe(expectedHashId);
  }, 30_000);

  it("should ACK and skip when FindWhoIs returns no data (fail-open)", async () => {
    const event = makeEvent();
    await repo.create.handleAsync({ event });

    // FindWhoIs returns undefined — Geo unavailable or no match
    const findWhoIs = createMockFindWhoIs(undefined);
    const provider = buildProvider();

    let resolveProcessed: () => void;
    const processed = new Promise<void>((resolve) => {
      resolveProcessed = resolve;
    });

    const originalHandleAsync = findWhoIs.handleAsync.bind(findWhoIs);
    (findWhoIs as any).handleAsync = vi.fn().mockImplementation(async (input: any) => {
      const result = await originalHandleAsync(input);
      setTimeout(() => resolveProcessed(), 50);
      return result;
    });

    const consumer = createWhoIsResolutionConsumer({
      messageBus: getBus(),
      provider,
      createScope: (p) => createServiceScope(p, logger),
      findWhoIs,
      logger,
    });
    cleanupFns.push(() => consumer.close());
    await consumer.ready;

    const publisher = getBus().createPublisher({
      exchanges: [
        {
          exchange: AUTH_MESSAGING.WHOIS_RESOLUTION_EXCHANGE,
          type: AUTH_MESSAGING.WHOIS_RESOLUTION_EXCHANGE_TYPE,
        },
      ],
    });
    cleanupFns.push(() => publisher.close());

    await publisher.send(
      {
        exchange: AUTH_MESSAGING.WHOIS_RESOLUTION_EXCHANGE,
        routingKey: AUTH_MESSAGING.WHOIS_RESOLUTION_QUEUE,
      },
      {
        signInEventId: event.id,
        ipAddress: event.ipAddress,
        userAgent: event.userAgent,
      },
    );

    await withTimeout(processed);
    await new Promise((r) => setTimeout(r, 200));

    // whoIsId should still be undefined — fail-open behavior
    const after = await repo.findByUserId.handleAsync({
      userId: event.userId,
      limit: 1,
      offset: 0,
    });
    expect(after.data!.events[0].whoIsId).toBeUndefined();
  }, 30_000);

  it("should drop invalid messages without crashing", async () => {
    const findWhoIs = createMockFindWhoIs("x".repeat(64));
    const provider = buildProvider();
    const warnFn = vi.fn();

    const consumer = createWhoIsResolutionConsumer({
      messageBus: getBus(),
      provider,
      createScope: (p) => createServiceScope(p, logger),
      findWhoIs,
      logger: { ...logger, warn: warnFn } as unknown as ILogger,
    });
    cleanupFns.push(() => consumer.close());
    await consumer.ready;

    const publisher = getBus().createPublisher({
      exchanges: [
        {
          exchange: AUTH_MESSAGING.WHOIS_RESOLUTION_EXCHANGE,
          type: AUTH_MESSAGING.WHOIS_RESOLUTION_EXCHANGE_TYPE,
        },
      ],
    });
    cleanupFns.push(() => publisher.close());

    // Send an invalid message (missing required fields)
    await publisher.send(
      {
        exchange: AUTH_MESSAGING.WHOIS_RESOLUTION_EXCHANGE,
        routingKey: AUTH_MESSAGING.WHOIS_RESOLUTION_QUEUE,
      },
      { garbage: true },
    );

    // Give it time to process
    await new Promise((r) => setTimeout(r, 1000));

    // FindWhoIs should NOT have been called (message was invalid)
    expect(findWhoIs.handleAsync).not.toHaveBeenCalled();
    // Logger should have warned
    expect(warnFn).toHaveBeenCalledWith(
      "Invalid WhoIs resolution message — dropping",
      expect.any(Object),
    );
  }, 15_000);
});
