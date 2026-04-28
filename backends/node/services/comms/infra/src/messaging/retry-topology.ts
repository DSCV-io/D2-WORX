import type { MessageBus } from "@d2/messaging";
import { COMMS_MESSAGING, COMMS_RETRY, RETRY_POLICY } from "@d2/comms-domain";

/**
 * Declares the DLX-based retry topology on the broker.
 *
 * Creates:
 * - 1 requeue exchange (direct, durable) that routes expired messages back to the main queue
 * - 5 tier queues with escalating TTLs (5s → 10s → 30s → 60s → 300s), each dead-lettering
 *   to the requeue exchange with the main queue as the routing key
 * - 1 binding from the requeue exchange to the main consumer queue
 *
 * This must be called once at startup, before the consumer begins processing.
 */
export async function declareRetryTopology(messageBus: MessageBus): Promise<void> {
  const tierQueues = COMMS_RETRY.TIER_TTLS.map((ttl, index) => ({
    queue: `${COMMS_RETRY.TIER_QUEUE_PREFIX}${index + 1}`,
    durable: true,
    arguments: {
      "x-message-ttl": ttl,
      "x-dead-letter-exchange": COMMS_RETRY.REQUEUE_EXCHANGE,
      "x-dead-letter-routing-key": COMMS_MESSAGING.NOTIFICATIONS_QUEUE,
    } as Record<string, unknown>,
  }));

  await messageBus.declareTopology({
    exchanges: [{ exchange: COMMS_RETRY.REQUEUE_EXCHANGE, type: "direct", durable: true }],
    queues: [
      // Main queue must exist before we can bind to it
      { queue: COMMS_MESSAGING.NOTIFICATIONS_QUEUE, durable: true },
      ...tierQueues,
    ],
    bindings: [
      {
        exchange: COMMS_RETRY.REQUEUE_EXCHANGE,
        queue: COMMS_MESSAGING.NOTIFICATIONS_QUEUE,
        routingKey: COMMS_MESSAGING.NOTIFICATIONS_QUEUE,
      },
    ],
  });
}

/**
 * Maps a retry count to the appropriate tier queue name.
 *
 * Retry counts 1–4 map to tiers 1–4 respectively. Retry counts 5+ cap at tier 5.
 * Returns `null` when max attempts (10) have been reached — the message should be dropped.
 *
 * @param retryCount - The current retry count (0 = first attempt, 1 = first retry, etc.)
 * @returns The tier queue name, or `undefined` if max attempts reached.
 */
export function getRetryTierQueue(retryCount: number): string | undefined {
  if (retryCount >= RETRY_POLICY.MAX_ATTEMPTS) {
    return undefined;
  }

  // retryCount 0 → tier 1, retryCount 1 → tier 2, ..., retryCount 4+ → tier 5
  const tier = Math.min(retryCount + 1, COMMS_RETRY.TIER_TTLS.length);
  return `${COMMS_RETRY.TIER_QUEUE_PREFIX}${tier}`;
}
