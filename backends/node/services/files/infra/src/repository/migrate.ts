import { drizzle } from "drizzle-orm/node-postgres";
import { migrate } from "drizzle-orm/node-postgres/migrator";
import type { Pool } from "pg";
import { resolve } from "node:path";

/**
 * Runs Drizzle migrations against the given pg.Pool.
 *
 * Used by both app startup (composition root) and integration tests
 * (Testcontainers) to apply custom table schemas. Idempotent — safe
 * to call multiple times.
 */
export async function runMigrations(pool: Pool): Promise<void> {
  const db = drizzle(pool);
  await migrate(db, {
    migrationsFolder: resolve(import.meta.dirname, "..", "..", "src", "repository", "migrations"),
  });
}
