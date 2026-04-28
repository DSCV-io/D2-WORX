import { PostgreSqlContainer, type StartedPostgreSqlContainer } from "@testcontainers/postgresql";
import { RabbitMQContainer, type StartedRabbitMQContainer } from "@testcontainers/rabbitmq";
import { RedisContainer, type StartedRedisContainer } from "@testcontainers/redis";
import pg from "pg";

let pgContainer: StartedPostgreSqlContainer;
let rabbitContainer: StartedRabbitMQContainer;
let redisContainer: StartedRedisContainer;

// Pools for each database
let adminPool: pg.Pool;
let authPool: pg.Pool;
let commsPool: pg.Pool;
let filesPool: pg.Pool;

/**
 * Starts shared infrastructure containers for E2E tests:
 * - PostgreSQL (single container with 3 databases: e2e_auth, e2e_comms, e2e_files)
 * - RabbitMQ
 * - Redis (used by Geo service for distributed cache)
 */
export async function startContainers(): Promise<void> {
  // Start containers in parallel
  const [pgC, rabbitC, redisC] = await Promise.all([
    new PostgreSqlContainer("postgres:18").start(),
    new RabbitMQContainer("rabbitmq:4.1-management").start(),
    new RedisContainer("redis:8.2").start(),
  ]);

  pgContainer = pgC;
  rabbitContainer = rabbitC;
  redisContainer = redisC;

  // Create additional databases in the same PG container
  adminPool = new pg.Pool({ connectionString: pgContainer.getConnectionUri() });
  await adminPool.query("CREATE DATABASE e2e_auth");
  await adminPool.query("CREATE DATABASE e2e_comms");
  await adminPool.query("CREATE DATABASE e2e_files");

  // Create per-database pools
  const baseUri = pgContainer.getConnectionUri();
  const authUri = baseUri.replace(/\/[^/]+$/, "/e2e_auth");
  const commsUri = baseUri.replace(/\/[^/]+$/, "/e2e_comms");
  const filesUri = baseUri.replace(/\/[^/]+$/, "/e2e_files");

  authPool = new pg.Pool({ connectionString: authUri });
  commsPool = new pg.Pool({ connectionString: commsUri });
  filesPool = new pg.Pool({ connectionString: filesUri });
}

/** Race a promise against a timeout (resolves even if inner hangs). */
function withTimeout(promise: Promise<unknown>, ms: number, label: string): Promise<void> {
  return Promise.race([
    promise.then(() => {}),
    new Promise<void>((resolve) =>
      setTimeout(() => {
        console.warn(`[E2E] ${label} timed out after ${ms}ms — forcing continue`);
        resolve();
      }, ms),
    ),
  ]);
}

/**
 * Stops all containers and closes pools.
 * Each step has a timeout to prevent the afterAll hook from hanging.
 */
export async function stopContainers(): Promise<void> {
  await withTimeout(authPool?.end() ?? Promise.resolve(), 3_000, "authPool.end");
  await withTimeout(commsPool?.end() ?? Promise.resolve(), 3_000, "commsPool.end");
  await withTimeout(filesPool?.end() ?? Promise.resolve(), 3_000, "filesPool.end");
  await withTimeout(adminPool?.end() ?? Promise.resolve(), 3_000, "adminPool.end");
  // Container stops are usually fast but can hang if the Docker daemon is busy
  await withTimeout(pgContainer?.stop() ?? Promise.resolve(), 10_000, "pgContainer.stop");
  await withTimeout(rabbitContainer?.stop() ?? Promise.resolve(), 10_000, "rabbitContainer.stop");
  await withTimeout(redisContainer?.stop() ?? Promise.resolve(), 10_000, "redisContainer.stop");
}

/** Connection string for auth database. */
export function getAuthPgUrl(): string {
  return pgContainer.getConnectionUri().replace(/\/[^/]+$/, "/e2e_auth");
}

/** Connection string for comms database. */
export function getCommsPgUrl(): string {
  return pgContainer.getConnectionUri().replace(/\/[^/]+$/, "/e2e_comms");
}

/** Connection string for files database. */
export function getFilesPgUrl(): string {
  return pgContainer.getConnectionUri().replace(/\/[^/]+$/, "/e2e_files");
}

/** Default PG connection string (for Geo — uses default db). */
export function getGeoPgUrl(): string {
  return pgContainer.getConnectionUri();
}

/**
 * Redis connection URL (redis://host:port).
 * Uses the testcontainer's mapped port.
 */
export function getRedisUrl(): string {
  return redisContainer.getConnectionUrl();
}

/**
 * RabbitMQ connection URL (amqp://guest:guest@host:port).
 * Manually constructed with default credentials (matches testcontainer defaults).
 */
export function getRabbitUrl(): string {
  return `amqp://guest:guest@${rabbitContainer.getHost()}:${rabbitContainer.getMappedPort(5672)}`;
}

/** Auth database pool (for assertions). */
export function getAuthPool(): pg.Pool {
  return authPool;
}

/** Comms database pool (for assertions). */
export function getCommsPool(): pg.Pool {
  return commsPool;
}

/** Files database pool (for assertions). */
export function getFilesPool(): pg.Pool {
  return filesPool;
}
