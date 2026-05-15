<!--
Copyright (c) DCSV. All rights reserved.
-->

# @d2/resilience

Retry / circuit breaker / singleflight / composable pipeline. Mirrors
`D2.Shared.Resilience` (.NET).

## Public API

| Export                                                        | Purpose                                                                                      |
| ------------------------------------------------------------- | -------------------------------------------------------------------------------------------- |
| `RetryHelper.retryAsync(op, opts?, signal?, rng?)`            | Generic retry with backoff + jitter; cancellation never retried.                             |
| `RetryHelper.retryD2ResultAsync(op, opts?, signal?, rng?)`    | `D2Result`-aware retry — retries failure shapes matching `shouldRetry`/`isTransient`.        |
| `RetryOptions<T>` / `RETRY_DEFAULTS`                          | Policy options + sensible defaults (3 attempts / 100ms base / 2x mul / 5s cap / 20% jitter). |
| `CircuitBreaker<T>`                                           | Three-state (Closed / Open / HalfOpen) breaker.                                              |
| `CircuitBreakerOptions` / `CircuitState` / `CircuitOpenError` | Config + state enum + error type.                                                            |
| `Singleflight<K, V>`                                          | In-flight dedup by key.                                                                      |
| `ResilientPipeline` / `ResilientPipelineBuilder`              | Composable layered pipeline (singleflight + breaker + retry).                                |
| `IResilientLayer`                                             | Layer contract.                                                                              |

## Dependencies

- `@d2/utilities` (boundary helpers)
- `@d2/result` (D2Result-aware retry overload)
- `@d2/logging` (reserved — transient-classification log lines added later)

## Usage example

```ts
import {
  RetryHelper,
  CircuitBreaker,
  ResilientPipelineBuilder,
} from "@d2/resilience";

// Plain retry.
const r = await RetryHelper.retryAsync(() => fetchUser(id));

// Pipeline composition.
const pipe = new ResilientPipelineBuilder()
  .useSingleflight()
  .useCircuitBreaker({ failureThreshold: 5, cooldownMs: 30_000 })
  .useRetries({ maxAttempts: 3 })
  .build();

const data = await pipe.execute(`users:${id}`, () => fetchUser(id));
```

## Parity with .NET

Mirrors `D2.Shared.Resilience`:

- `RetryHelper.retryAsync` ↔ `RetryHelper.RetryAsync<T>`.
- `RetryHelper.retryD2ResultAsync` ↔ `RetryHelper.RetryD2ResultAsync<T>` —
  same "only retry transient fail-results" carve-out.
- `CircuitBreaker` ↔ `CircuitBreaker<T>` — same three-state lifecycle.
- `Singleflight` ↔ `Singleflight<TKey, TValue>` — same key-coalescing.
- `ResilientPipelineBuilder` ↔ `ResilientPipelineBuilder` — same outer-first
  ordering.
- Cancellation never classified as transient (matches .NET behavior).

## Edge cases

- Aborted `AbortSignal` short-circuits before the first attempt.
- `maxAttempts < 1` → `RangeError`.
- `Singleflight` clears entries after settle — back-pressure does not
  accumulate indefinitely.
- HalfOpen failure re-arms cooldown (single-trip semantics).
- `CircuitBreaker` rejects `failureThreshold < 1` and `cooldownMs < 0`.
