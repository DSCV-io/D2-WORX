/**
 * Idempotency key generation utility.
 *
 * Generates a unique key for the Idempotency-Key header used by the
 * gateway's idempotency middleware to deduplicate mutation requests.
 *
 * Isomorphic — crypto.randomUUID() is available in both Node.js and browsers.
 */

/**
 * Generate a unique idempotency key (UUIDv4).
 */
export function generateIdempotencyKey(): string {
  return crypto.randomUUID();
}
