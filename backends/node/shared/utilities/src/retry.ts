/**
 * Options for the generic retry utility.
 */
export interface RetryOptions<T = unknown> {
  /** Maximum number of attempts (including the initial call). Default: 5. */
  maxAttempts?: number;
  /** Base delay in milliseconds before the first retry. Default: 1000. */
  baseDelayMs?: number;
  /** Multiplier applied to the delay after each retry. Default: 2. */
  backoffMultiplier?: number;
  /** Maximum delay in milliseconds. Default: 30000. */
  maxDelayMs?: number;
  /** Whether to apply full jitter (uniform [0, calculated)). Default: true. */
  jitter?: boolean;
  /** Inspect a returned value to decide whether to retry. Default: never retry returns. */
  shouldRetry?: (result: T) => boolean;
  /** Inspect a thrown error to decide whether to retry. Default: common transient patterns. */
  isTransientError?: (error: unknown) => boolean;
  /** Abort signal for cancellation. */
  signal?: AbortSignal;
  /** @internal Override for testing — replaces the actual delay. */
  _delayFn?: (ms: number, signal?: AbortSignal) => Promise<void>;
}

/**
 * Default transient error detector.
 *
 * Checks for:
 * - Timeout patterns (ETIMEDOUT, deadline, timeout in message)
 * - Connection patterns (ECONNREFUSED, ECONNRESET, ENETUNREACH)
 * - NOT AbortError (user cancellation should propagate immediately)
 *
 * Note: gRPC-specific transient detection lives in `@d2/service-defaults/grpc`
 * (`isTransientGrpcError`). This function is protocol-agnostic.
 */
export function isTransientError(error: unknown): boolean {
  if (error == null) return false;

  // Never retry user cancellation
  if (error instanceof DOMException && error.name === "AbortError") return false;
  if (error instanceof Error && error.name === "AbortError") return false;

  // Check error message / code string for common transient patterns
  const errCode = error instanceof Error ? ((error as { code?: string }).code ?? "") : "";
  const message =
    error instanceof Error
      ? `${error.message} ${errCode}`.toLowerCase()
      : String(error).toLowerCase();

  if (message.includes("etimedout")) return true;
  if (message.includes("deadline")) return true;
  if (message.includes("timeout")) return true;
  if (message.includes("econnrefused")) return true;
  if (message.includes("econnreset")) return true;
  if (message.includes("enetunreach")) return true;

  return false;
}

/**
 * Generic retry utility with exponential backoff and optional jitter.
 *
 * The operation receives a 1-based attempt number. On thrown errors,
 * `isTransientError` controls retry behavior. On returned values,
 * `shouldRetry` controls retry behavior.
 *
 * After all attempts are exhausted: throws the last error (if the last
 * attempt threw) or returns the last result (if the last attempt returned).
 */
export async function retryAsync<T>(
  operation: (attempt: number) => Promise<T>,
  options?: RetryOptions<T>,
): Promise<T> {
  const maxAttempts = options?.maxAttempts ?? 5;
  const baseDelayMs = options?.baseDelayMs ?? 1000;
  const backoffMultiplier = options?.backoffMultiplier ?? 2;
  const maxDelayMs = options?.maxDelayMs ?? 30_000;
  const jitter = options?.jitter ?? true;
  const shouldRetry = options?.shouldRetry ?? (() => false);
  const checkTransient = options?.isTransientError ?? isTransientError;
  const signal = options?.signal;
  const delayFn = options?._delayFn ?? defaultDelay;

  let lastError: unknown;
  let lastResult: T | undefined;
  let lastWasError = false;

  for (let attempt = 1; attempt <= maxAttempts; attempt++) {
    signal?.throwIfAborted();

    try {
      const result = await operation(attempt);

      if (attempt < maxAttempts && shouldRetry(result)) {
        lastResult = result;
        lastWasError = false;
        await delayFn(
          calculateDelay(attempt - 1, baseDelayMs, backoffMultiplier, maxDelayMs, jitter),
          signal,
        );
        continue;
      }

      return result;
    } catch (error: unknown) {
      lastError = error;
      lastWasError = true;

      if (attempt < maxAttempts && checkTransient(error)) {
        await delayFn(
          calculateDelay(attempt - 1, baseDelayMs, backoffMultiplier, maxDelayMs, jitter),
          signal,
        );
        continue;
      }

      if (!checkTransient(error) || attempt >= maxAttempts) {
        throw error;
      }
    }
  }

  // All attempts exhausted
  if (lastWasError) {
    throw lastError;
  }
  return lastResult as T;
}

/**
 * Calculate the delay for a given retry index (0-based).
 *
 * ```
 * calculatedDelay = min(baseDelayMs * (backoffMultiplier ^ retryIndex), maxDelayMs)
 * actualDelay = jitter ? random(0, calculatedDelay) : calculatedDelay
 * ```
 */
function calculateDelay(
  retryIndex: number,
  baseDelayMs: number,
  backoffMultiplier: number,
  maxDelayMs: number,
  jitter: boolean,
): number {
  const calculated = Math.min(baseDelayMs * Math.pow(backoffMultiplier, retryIndex), maxDelayMs);
  return jitter ? Math.random() * calculated : calculated;
}

async function defaultDelay(ms: number, signal?: AbortSignal): Promise<void> {
  if (signal?.aborted) {
    return Promise.reject(signal.reason);
  }

  return new Promise((resolve, reject) => {
    const onAbort = () => {
      clearTimeout(timer);
      reject(signal!.reason);
    };
    const timer = setTimeout(() => {
      signal?.removeEventListener("abort", onAbort);
      resolve();
    }, ms);
    signal?.addEventListener("abort", onAbort, { once: true });
  });
}
