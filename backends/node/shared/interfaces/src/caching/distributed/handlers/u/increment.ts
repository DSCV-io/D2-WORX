import type { IHandler } from "@d2/handler";

/**
 * Input for atomically incrementing a counter in the distributed cache.
 *
 * `expirationMs` (when provided) is applied **only on the call that creates
 * the key** — subsequent increments leave the existing TTL alone. This is
 * the sliding-window-counter contract that rate-limit, throttle, and quota
 * use-cases need: a 5-minute window starts ticking on the first event and
 * keeps ticking regardless of how many more events land inside it.
 *
 * Refresh-on-every-call semantics are NOT supported — they let an attacker
 * trivially bypass any rate limit by spamming requests faster than the
 * window length (each request would extend the window indefinitely, so the
 * attempt counter never expires and the cap is never enforced over a finite
 * period). If you need TTL refresh on a non-counter key, use the `Set`
 * handler instead.
 */
export interface IncrementInput {
  key: string;
  amount?: number;
  expirationMs?: number;
}

/** Output for atomically incrementing a counter in the distributed cache. */
export interface IncrementOutput {
  newValue: number;
}

/** Handler for atomically incrementing a counter in the distributed cache. */
export type IIncrementHandler = IHandler<IncrementInput, IncrementOutput>;
