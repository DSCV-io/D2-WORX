import { describe, it, expect, beforeAll, afterAll } from "vitest";
import pg from "pg";
import Redis from "ioredis";
import { drizzle, type NodePgDatabase } from "drizzle-orm/node-postgres";
import { PostgreSqlContainer, type StartedPostgreSqlContainer } from "@testcontainers/postgresql";
import { RedisContainer, type StartedRedisContainer } from "@testcontainers/redis";
import { ServiceCollection, type ServiceProvider } from "@d2/di";
import { HandlerContext, IHandlerContextKey, IRequestContextKey } from "@d2/handler";
import { createLogger, ILoggerKey, type ILogger } from "@d2/logging";
import { AcquireLock, ReleaseLock } from "@d2/cache-redis";
import { DistributedCache } from "@d2/interfaces";
import { generateUuidV7 } from "@d2/utilities";
// Auth
import {
  addAuthInfra,
  runMigrations as runAuthMigrations,
  user,
  session,
  organization,
  invitation,
  signInEvent,
  emulationConsent,
} from "@d2/auth-infra";
import {
  RunSessionPurge,
  RunSignInEventPurge,
  RunInvitationCleanup,
  RunEmulationConsentCleanup,
  DEFAULT_AUTH_JOB_OPTIONS,
  IRunSessionPurgeKey,
  IRunSignInEventPurgeKey,
  IRunInvitationCleanupKey,
  IRunEmulationConsentCleanupKey,
  IPurgeExpiredSessionsKey,
  IPurgeSignInEventsKey,
  IPurgeExpiredInvitationsKey,
  IPurgeExpiredEmulationConsentsKey,
} from "@d2/auth-app";
// Comms
import {
  addCommsInfra,
  runMigrations as runCommsMigrations,
  message,
  deliveryRequest,
  deliveryAttempt,
} from "@d2/comms-infra";
import {
  RunDeletedMessagePurge,
  RunDeliveryHistoryPurge,
  DEFAULT_COMMS_JOB_OPTIONS,
  IRunDeletedMessagePurgeKey,
  IRunDeliveryHistoryPurgeKey,
  IPurgeDeletedMessagesKey,
  IPurgeDeliveryHistoryKey,
} from "@d2/comms-app";
import { createServiceScope } from "@d2/handler";

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function daysAgo(days: number): Date {
  const d = new Date();
  d.setDate(d.getDate() - days);
  return d;
}

function hoursAgo(hours: number): Date {
  const d = new Date();
  d.setHours(d.getHours() - hours);
  return d;
}

function daysFromNow(days: number): Date {
  const d = new Date();
  d.setDate(d.getDate() + days);
  return d;
}

// ---------------------------------------------------------------------------
// E2E: Scheduled Job Execution
// ---------------------------------------------------------------------------

describe("E2E: Scheduled job execution (full DI → lock → purge → DB)", () => {
  let pgContainer: StartedPostgreSqlContainer;
  let redisContainer: StartedRedisContainer;
  let authPool: pg.Pool;
  let commsPool: pg.Pool;
  let redis: Redis;
  let authDb: NodePgDatabase;
  let commsDb: NodePgDatabase;
  let authProvider: ServiceProvider;
  let commsProvider: ServiceProvider;
  let logger: ILogger;

  beforeAll(async () => {
    // 1. Start containers (PG + Redis only — jobs don't need RabbitMQ or Geo)
    const [pgC, redisC] = await Promise.all([
      new PostgreSqlContainer("postgres:18").start(),
      new RedisContainer("redis:8.2").start(),
    ]);
    pgContainer = pgC;
    redisContainer = redisC;

    // 2. Create per-service databases
    const adminPool = new pg.Pool({ connectionString: pgContainer.getConnectionUri() });
    await adminPool.query("CREATE DATABASE e2e_auth_jobs");
    await adminPool.query("CREATE DATABASE e2e_comms_jobs");
    await adminPool.end();

    const baseUri = pgContainer.getConnectionUri();
    const authPgUrl = baseUri.replace(/\/[^/]+$/, "/e2e_auth_jobs");
    const commsPgUrl = baseUri.replace(/\/[^/]+$/, "/e2e_comms_jobs");

    // 3. Run migrations
    authPool = new pg.Pool({ connectionString: authPgUrl });
    commsPool = new pg.Pool({ connectionString: commsPgUrl });
    await Promise.all([runAuthMigrations(authPool), runCommsMigrations(commsPool)]);
    authDb = drizzle(authPool);
    commsDb = drizzle(commsPool);

    // 4. Connect Redis
    redis = new Redis(redisContainer.getConnectionUrl());

    // 5. Shared logger
    logger = createLogger({ serviceName: "e2e-jobs", level: "silent" as never });
    const serviceContext = new HandlerContext(
      {
        isAuthenticated: false,
        isTrustedService: null,
        isAgentStaff: false,
        isAgentAdmin: false,
        isTargetingStaff: false,
        isTargetingAdmin: false,
        isOrgEmulating: false,
        isUserImpersonating: false,
      },
      logger,
    );

    // 6. Build Auth DI container (minimal — only job-related handlers)
    const authServices = new ServiceCollection();
    authServices.addInstance(ILoggerKey, logger);
    authServices.addScoped(
      IHandlerContextKey,
      (sp) => new HandlerContext(sp.resolve(IRequestContextKey), sp.resolve(ILoggerKey)),
    );
    authServices.addInstance(
      DistributedCache.IDistributedCacheAcquireLockKey,
      new AcquireLock(redis, serviceContext),
    );
    authServices.addInstance(
      DistributedCache.IDistributedCacheReleaseLockKey,
      new ReleaseLock(redis, serviceContext),
    );
    addAuthInfra(authServices, authDb);
    // Register only job CQRS handlers (avoids geo-client deps from addAuthApp)
    authServices.addTransient(
      IRunSessionPurgeKey,
      (sp) =>
        new RunSessionPurge(
          sp.resolve(DistributedCache.IDistributedCacheAcquireLockKey),
          sp.resolve(DistributedCache.IDistributedCacheReleaseLockKey),
          sp.resolve(IPurgeExpiredSessionsKey),
          DEFAULT_AUTH_JOB_OPTIONS,
          sp.resolve(IHandlerContextKey),
        ),
    );
    authServices.addTransient(
      IRunSignInEventPurgeKey,
      (sp) =>
        new RunSignInEventPurge(
          sp.resolve(DistributedCache.IDistributedCacheAcquireLockKey),
          sp.resolve(DistributedCache.IDistributedCacheReleaseLockKey),
          sp.resolve(IPurgeSignInEventsKey),
          DEFAULT_AUTH_JOB_OPTIONS,
          sp.resolve(IHandlerContextKey),
        ),
    );
    authServices.addTransient(
      IRunInvitationCleanupKey,
      (sp) =>
        new RunInvitationCleanup(
          sp.resolve(DistributedCache.IDistributedCacheAcquireLockKey),
          sp.resolve(DistributedCache.IDistributedCacheReleaseLockKey),
          sp.resolve(IPurgeExpiredInvitationsKey),
          DEFAULT_AUTH_JOB_OPTIONS,
          sp.resolve(IHandlerContextKey),
        ),
    );
    authServices.addTransient(
      IRunEmulationConsentCleanupKey,
      (sp) =>
        new RunEmulationConsentCleanup(
          sp.resolve(DistributedCache.IDistributedCacheAcquireLockKey),
          sp.resolve(DistributedCache.IDistributedCacheReleaseLockKey),
          sp.resolve(IPurgeExpiredEmulationConsentsKey),
          DEFAULT_AUTH_JOB_OPTIONS,
          sp.resolve(IHandlerContextKey),
        ),
    );
    authProvider = authServices.build();

    // 7. Build Comms DI container (minimal — only job-related handlers)
    const commsServices = new ServiceCollection();
    commsServices.addInstance(ILoggerKey, logger);
    commsServices.addScoped(
      IHandlerContextKey,
      (sp) => new HandlerContext(sp.resolve(IRequestContextKey), sp.resolve(ILoggerKey)),
    );
    commsServices.addInstance(
      DistributedCache.IDistributedCacheAcquireLockKey,
      new AcquireLock(redis, serviceContext),
    );
    commsServices.addInstance(
      DistributedCache.IDistributedCacheReleaseLockKey,
      new ReleaseLock(redis, serviceContext),
    );
    addCommsInfra(commsServices, commsDb);
    commsServices.addTransient(
      IRunDeletedMessagePurgeKey,
      (sp) =>
        new RunDeletedMessagePurge(
          sp.resolve(DistributedCache.IDistributedCacheAcquireLockKey),
          sp.resolve(DistributedCache.IDistributedCacheReleaseLockKey),
          sp.resolve(IPurgeDeletedMessagesKey),
          DEFAULT_COMMS_JOB_OPTIONS,
          sp.resolve(IHandlerContextKey),
        ),
    );
    commsServices.addTransient(
      IRunDeliveryHistoryPurgeKey,
      (sp) =>
        new RunDeliveryHistoryPurge(
          sp.resolve(DistributedCache.IDistributedCacheAcquireLockKey),
          sp.resolve(DistributedCache.IDistributedCacheReleaseLockKey),
          sp.resolve(IPurgeDeliveryHistoryKey),
          DEFAULT_COMMS_JOB_OPTIONS,
          sp.resolve(IHandlerContextKey),
        ),
    );
    commsProvider = commsServices.build();
  }, 120_000);

  afterAll(async () => {
    authProvider?.dispose();
    commsProvider?.dispose();
    redis?.disconnect();
    await Promise.all([authPool?.end(), commsPool?.end()]);
    await Promise.all([pgContainer?.stop(), redisContainer?.stop()]);
  });

  // -------------------------------------------------------------------------
  // Auth: Session Purge
  // -------------------------------------------------------------------------

  it("auth: session purge deletes expired sessions via full DI stack", async () => {
    // Seed a user (required FK for sessions)
    const userId = generateUuidV7();
    const username = `job-user-${userId.slice(0, 8)}`;
    await authDb.insert(user).values({
      id: userId,
      name: "Job Test User",
      email: `job-session-${userId}@test.com`,
      emailVerified: true,
      username,
      displayUsername: username,
      createdAt: new Date(),
      updatedAt: new Date(),
    });

    // Seed sessions: 2 expired, 1 active
    const expiredSession1 = generateUuidV7();
    const expiredSession2 = generateUuidV7();
    const activeSession = generateUuidV7();
    await authDb.insert(session).values([
      {
        id: expiredSession1,
        expiresAt: hoursAgo(2),
        token: `token-expired-1-${expiredSession1}`,
        createdAt: daysAgo(10),
        updatedAt: daysAgo(10),
        ipAddress: "10.0.0.1",
        userAgent: "test",
        userId,
      },
      {
        id: expiredSession2,
        expiresAt: hoursAgo(1),
        token: `token-expired-2-${expiredSession2}`,
        createdAt: daysAgo(5),
        updatedAt: daysAgo(5),
        ipAddress: "10.0.0.2",
        userAgent: "test",
        userId,
      },
      {
        id: activeSession,
        expiresAt: daysFromNow(7),
        token: `token-active-${activeSession}`,
        createdAt: new Date(),
        updatedAt: new Date(),
        ipAddress: "10.0.0.3",
        userAgent: "test",
        userId,
      },
    ]);

    // Resolve handler from DI scope and execute
    const scope = createServiceScope(authProvider, logger);
    try {
      const handler = scope.resolve(IRunSessionPurgeKey);
      const result = await handler.handleAsync({});

      expect(result.success).toBe(true);
      expect(result.data?.lockAcquired).toBe(true);
      expect(result.data?.rowsAffected).toBeGreaterThanOrEqual(2);
      expect(result.data?.durationMs).toBeGreaterThanOrEqual(0);
    } finally {
      scope.dispose();
    }

    // Verify: expired sessions deleted, active session preserved
    const remaining = await authDb.select().from(session);
    const remainingIds = remaining.map((s) => s.id);
    expect(remainingIds).not.toContain(expiredSession1);
    expect(remainingIds).not.toContain(expiredSession2);
    expect(remainingIds).toContain(activeSession);
  });

  // -------------------------------------------------------------------------
  // Auth: Sign-In Event Purge
  // -------------------------------------------------------------------------

  it("auth: sign-in event purge deletes old events via full DI stack", async () => {
    // Sign-in events have no FK constraints — seed directly
    const oldEvent1 = generateUuidV7();
    const oldEvent2 = generateUuidV7();
    const recentEvent = generateUuidV7();
    await authDb.insert(signInEvent).values([
      {
        id: oldEvent1,
        userId: generateUuidV7(),
        successful: true,
        ipAddress: "10.0.0.1",
        userAgent: "test",
        createdAt: daysAgo(100), // older than 90-day retention
      },
      {
        id: oldEvent2,
        userId: generateUuidV7(),
        successful: false,
        ipAddress: "10.0.0.2",
        userAgent: "test",
        createdAt: daysAgo(95),
      },
      {
        id: recentEvent,
        userId: generateUuidV7(),
        successful: true,
        ipAddress: "10.0.0.3",
        userAgent: "test",
        createdAt: daysAgo(30), // within retention
      },
    ]);

    const scope = createServiceScope(authProvider, logger);
    try {
      const handler = scope.resolve(IRunSignInEventPurgeKey);
      const result = await handler.handleAsync({});

      expect(result.success).toBe(true);
      expect(result.data?.lockAcquired).toBe(true);
      expect(result.data?.rowsAffected).toBeGreaterThanOrEqual(2);
    } finally {
      scope.dispose();
    }

    // Verify: old events deleted, recent event preserved
    const remaining = await authDb.select().from(signInEvent);
    const remainingIds = remaining.map((e) => e.id);
    expect(remainingIds).not.toContain(oldEvent1);
    expect(remainingIds).not.toContain(oldEvent2);
    expect(remainingIds).toContain(recentEvent);
  });

  // -------------------------------------------------------------------------
  // Comms: Deleted Message Purge
  // -------------------------------------------------------------------------

  it("comms: deleted message purge removes soft-deleted messages via full DI stack", async () => {
    const oldDeletedMsg = generateUuidV7();
    const recentDeletedMsg = generateUuidV7();
    const activeMsg = generateUuidV7();
    await commsDb.insert(message).values([
      {
        id: oldDeletedMsg,
        content: "old deleted",
        plainTextContent: "old deleted",
        contentFormat: "markdown",
        sensitive: false,
        urgency: "normal",
        senderService: "test",
        createdAt: daysAgo(200),
        updatedAt: daysAgo(200),
        deletedAt: daysAgo(100), // deleted > 90 days ago (retention default)
      },
      {
        id: recentDeletedMsg,
        content: "recently deleted",
        plainTextContent: "recently deleted",
        contentFormat: "markdown",
        sensitive: false,
        urgency: "normal",
        senderService: "test",
        createdAt: daysAgo(50),
        updatedAt: daysAgo(50),
        deletedAt: daysAgo(30), // deleted < 90 days ago — should be preserved
      },
      {
        id: activeMsg,
        content: "active message",
        plainTextContent: "active message",
        contentFormat: "markdown",
        sensitive: false,
        urgency: "normal",
        senderService: "test",
        createdAt: new Date(),
        updatedAt: new Date(),
        // No deletedAt — active
      },
    ]);

    const scope = createServiceScope(commsProvider, logger);
    try {
      const handler = scope.resolve(IRunDeletedMessagePurgeKey);
      const result = await handler.handleAsync({});

      expect(result.success).toBe(true);
      expect(result.data?.lockAcquired).toBe(true);
      expect(result.data?.rowsAffected).toBeGreaterThanOrEqual(1);
    } finally {
      scope.dispose();
    }

    // Verify: old deleted message purged, recent deleted and active preserved
    const remaining = await commsDb.select().from(message);
    const remainingIds = remaining.map((m) => m.id);
    expect(remainingIds).not.toContain(oldDeletedMsg);
    expect(remainingIds).toContain(recentDeletedMsg);
    expect(remainingIds).toContain(activeMsg);
  });

  // -------------------------------------------------------------------------
  // Comms: Delivery History Purge
  // -------------------------------------------------------------------------

  it("comms: delivery history purge removes old requests and attempts via full DI stack", async () => {
    // Old delivery request + attempts (> 365 days)
    const oldReqId = generateUuidV7();
    const recentReqId = generateUuidV7();
    // delivery_request.message_id has a NOT NULL FK to message.id with
    // ON DELETE CASCADE — must seed messages first.
    const oldMsgId = generateUuidV7();
    const recentMsgId = generateUuidV7();

    await commsDb.insert(message).values([
      {
        id: oldMsgId,
        senderService: "test",
        title: "old",
        content: "old",
        plainTextContent: "old",
        channels: ["email"],
        createdAt: daysAgo(400),
        updatedAt: daysAgo(400),
      },
      {
        id: recentMsgId,
        senderService: "test",
        title: "recent",
        content: "recent",
        plainTextContent: "recent",
        channels: ["email"],
        createdAt: daysAgo(100),
        updatedAt: daysAgo(100),
      },
    ]);

    await commsDb.insert(deliveryRequest).values([
      {
        id: oldReqId,
        messageId: oldMsgId,
        correlationId: generateUuidV7(),
        recipientContactId: generateUuidV7(),
        createdAt: daysAgo(400), // older than 365-day retention
      },
      {
        id: recentReqId,
        messageId: recentMsgId,
        correlationId: generateUuidV7(),
        recipientContactId: generateUuidV7(),
        createdAt: daysAgo(100), // within retention
      },
    ]);

    // Add attempts for both requests
    const oldAttemptId = generateUuidV7();
    const recentAttemptId = generateUuidV7();
    await commsDb.insert(deliveryAttempt).values([
      {
        id: oldAttemptId,
        requestId: oldReqId,
        channel: "email",
        recipientAddress: "old@test.com",
        status: "delivered",
        attemptNumber: 1,
        createdAt: daysAgo(400),
      },
      {
        id: recentAttemptId,
        requestId: recentReqId,
        channel: "email",
        recipientAddress: "recent@test.com",
        status: "delivered",
        attemptNumber: 1,
        createdAt: daysAgo(100),
      },
    ]);

    const scope = createServiceScope(commsProvider, logger);
    try {
      const handler = scope.resolve(IRunDeliveryHistoryPurgeKey);
      const result = await handler.handleAsync({});

      expect(result.success).toBe(true);
      expect(result.data?.lockAcquired).toBe(true);
      expect(result.data?.rowsAffected).toBeGreaterThanOrEqual(1);
    } finally {
      scope.dispose();
    }

    // Verify: old request + attempt deleted, recent ones preserved
    const remainingRequests = await commsDb.select().from(deliveryRequest);
    expect(remainingRequests.map((r) => r.id)).toContain(recentReqId);
    expect(remainingRequests.map((r) => r.id)).not.toContain(oldReqId);

    const remainingAttempts = await commsDb.select().from(deliveryAttempt);
    expect(remainingAttempts.map((a) => a.id)).toContain(recentAttemptId);
    expect(remainingAttempts.map((a) => a.id)).not.toContain(oldAttemptId);
  });
});
