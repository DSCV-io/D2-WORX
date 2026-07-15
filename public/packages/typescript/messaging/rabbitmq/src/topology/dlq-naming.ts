// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

/**
 * Conventional names for the dead-letter exchange / queue that pair with a
 * primary queue, plus retry-tier naming used by the optional broker-level
 * retry topology. Byte-identical to the .NET
 * `D2.Shared.Messaging.RabbitMq.Topology.DlqNaming` — the names are derived
 * from the queue name, full stop (no override), so ops tooling and dashboards
 * rely on a fixed shape across both runtimes.
 */
export const DlqNaming = {
  /** Returns the DLX name for a primary queue. */
  dlxFor(queueName: string): string {
    return `${queueName}.dlx`;
  },

  /** Returns the DLQ name for a primary queue. */
  dlqFor(queueName: string): string {
    return `${queueName}.dlq`;
  },

  /** Returns the retry-tier exchange name (one per tier). */
  retryTierExchangeFor(queueName: string, tierIndex: number): string {
    return `${queueName}.retry.${tierIndex}`;
  },

  /** Returns the retry-tier queue name (one per tier). */
  retryTierQueueFor(queueName: string, tierIndex: number): string {
    return `${queueName}.retry.${tierIndex}`;
  },

  /**
   * Returns the return exchange name — retry queues dead-letter here on TTL
   * expiry; the binding routes back to the primary queue.
   */
  retryReturnExchangeFor(queueName: string): string {
    return `${queueName}.retry.return`;
  },
} as const;
