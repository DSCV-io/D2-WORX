// -----------------------------------------------------------------------
// <copyright file="main.ts" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

// NOTE: OpenTelemetry is bootstrapped via the `--import @d2/service-defaults/register`
// Node.js loader flag in package.json scripts (dev + start). The register module
// calls `setupTelemetry({ serviceName })` using OTEL_SERVICE_NAME from env vars
// (set by Docker Compose env vars). OTel is fully active before this file executes.

import { createLogger } from "@d2/logging";
import { parseConfig, logConfig } from "./config.js";
import { checkHealth } from "./dkron-client.js";
import { reconcile } from "./reconciler.js";
import type { ReconcileResult } from "./types.js";

const logger = createLogger({ serviceName: "dkron-mgr" });

// ── Constants ────────────────────────────────────────────────────────────
const INITIAL_BACKOFF_MS = 1_000;
const MAX_BACKOFF_MS = 30_000;

// ── Shutdown support ─────────────────────────────────────────────────────
// Declared before waitForHealth() so sleep() can reference interruptSleep
// during health-check retries without hitting temporal dead zone.
let shutdownRequested = false;
let interruptSleep: (() => void) | undefined;

for (const signal of ["SIGINT", "SIGTERM"] as const) {
  process.on(signal, () => {
    logger.info(`Received ${signal}, shutting down`);
    shutdownRequested = true;
    interruptSleep?.();
  });
}

// ── Config ───────────────────────────────────────────────────────────────
const config = parseConfig(logger);
logConfig(config, logger);

// ── Wait for Dkron ───────────────────────────────────────────────────────
await waitForHealth();

// ── Initial reconciliation ───────────────────────────────────────────────
logger.info("Dkron healthy — running initial reconciliation");
await runReconciliation();

// ── Reconciliation loop ──────────────────────────────────────────────────
// Use a sequential loop (not setInterval) to prevent overlapping runs
// if a reconciliation cycle takes longer than the interval.
logger.info(`Starting reconciliation loop (interval: ${config.reconcileIntervalMs}ms)`);

while (!shutdownRequested) {
  await sleep(config.reconcileIntervalMs);
  if (!shutdownRequested) await runReconciliation();
}

process.exit(0);

// ─────────────────────────────────────────────────────────────────────────

/** Poll Dkron health with exponential backoff until it responds. */
async function waitForHealth(): Promise<void> {
  let delay = INITIAL_BACKOFF_MS;

  while (!shutdownRequested) {
    const healthy = await checkHealth(config.dkronUrl, logger);
    if (healthy) return;

    logger.warn(
      `Dkron not ready at ${config.dkronUrl} (waiting for Raft leader), retrying in ${delay}ms`,
    );
    await sleep(delay);
    if (shutdownRequested) return;
    delay = Math.min(delay * 2, MAX_BACKOFF_MS);
  }
}

/** Run one reconciliation cycle. Never throws -- errors are logged. */
async function runReconciliation(): Promise<void> {
  try {
    const result = await reconcile(config, logger);
    logResult(result);
  } catch (err) {
    logger.error("Reconciliation cycle failed", err);
  }
}

function logResult(result: ReconcileResult): void {
  const summary = [
    `${result.created.length} created`,
    `${result.updated.length} updated`,
    `${result.deleted.length} deleted`,
    `${result.unchanged.length} unchanged`,
  ].join(", ");

  if (result.errors.length > 0) {
    logger.warn(`Reconciliation complete (${summary}) with ${result.errors.length} error(s)`);
    for (const e of result.errors) {
      logger.error(`  [${e.operation}] ${e.job}: ${e.error}`);
    }
  } else {
    logger.info(`Reconciliation complete: ${summary}`);
  }

  const details: [string, string[]][] = [
    ["Created", result.created],
    ["Updated", result.updated],
    ["Deleted", result.deleted],
  ];

  for (const [label, items] of details) {
    if (items.length > 0) {
      logger.info(`  ${label}: ${items.join(", ")}`);
    }
  }
}

function sleep(ms: number): Promise<void> {
  return new Promise((resolve) => {
    const timer = setTimeout(resolve, ms);
    interruptSleep = () => {
      clearTimeout(timer);
      resolve();
    };
  });
}
