import { createPostgresTestHelper, type PostgresTestHelper } from "@d2/testing";
import { runMigrations } from "@d2/files-infra";

const helper: PostgresTestHelper = createPostgresTestHelper(runMigrations);

export async function startPostgres(): Promise<void> {
  await helper.start();
}

export async function stopPostgres(): Promise<void> {
  await helper.stop();
}

export function getPool() {
  return helper.getPool();
}

export function getDb() {
  return helper.getDb();
}

export async function cleanAllTables(): Promise<void> {
  await helper.clean("TRUNCATE file CASCADE");
}
