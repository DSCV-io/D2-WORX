import { D2Result } from "./d2-result.js";
import { ErrorCodes } from "./error-codes.js";

// ---------------------------------------------------------------------------
// Shared retry config (duplicated from @d2/utilities to avoid dependency)
// ---------------------------------------------------------------------------

/**
 * Base retry configuration shared by clean and dirty retriers.
 */
export interface RetryConfig {
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
  /** Abort signal for cancellation. */
  signal?: AbortSignal;
  /** @internal Override for testing — replaces the actual delay. */
  _delayFn?: (ms: number, signal?: AbortSignal) => Promise<void>;
}

// ---------------------------------------------------------------------------
// Clean version: operation returns D2Result
// ---------------------------------------------------------------------------

/**
 * Options for `retryResultAsync` (clean retrier).
 */
export interface RetryResultOptions extends RetryConfig {
  /** Override default D2Result transient detection. */
  isTransientResult?: (result: D2Result<unknown>) => boolean;
}

/**
 * Default transient result detector.
 *
 * Considers transient:
 * - Error codes: SERVICE_UNAVAILABLE, UNHANDLED_EXCEPTION, RATE_LIMITED, CONFLICT
 * - Status codes: any >= 500, or 429 (TooManyRequests)
 *
 * NOT transient: NOT_FOUND, UNAUTHORIZED, FORBIDDEN, VALIDATION_FAILED, SOME_FOUND
 */
export function isTransientResult(result: D2Result<unknown>): boolean {
  if (result.success) return false;

  // Check error codes first (more specific)
  if (result.errorCode) {
    switch (result.errorCode) {
      case ErrorCodes.SERVICE_UNAVAILABLE:
      case ErrorCodes.UNHANDLED_EXCEPTION:
      case ErrorCodes.RATE_LIMITED:
      case ErrorCodes.CONFLICT:
        return true;
      case ErrorCodes.NOT_FOUND:
      case ErrorCodes.UNAUTHORIZED:
      case ErrorCodes.FORBIDDEN:
      case ErrorCodes.VALIDATION_FAILED:
      case ErrorCodes.SOME_FOUND:
      case ErrorCodes.COULD_NOT_BE_SERIALIZED:
      case ErrorCodes.COULD_NOT_BE_DESERIALIZED:
      case ErrorCodes.PAYLOAD_TOO_LARGE:
      case ErrorCodes.CANCELLED:
        return false;
      default:
        break;
    }
  }

  // Fall back to status code
  if (result.statusCode >= 500 || result.statusCode === 429) return true;

  return false;
}

/**
 * Retry a D2Result-returning operation with exponential backoff.
 *
 * - Success → return immediately
 * - Transient failure → delay + retry
 * - Permanent failure → return immediately
 * - Operation throws → catch, return `D2Result.unhandledException()`
 * - After all attempts → return last D2Result
 *
 * Never throws.
 */
export async function retryResultAsync<T>(
  operation: (attempt: number) => Promise<D2Result<T>>,
  options?: RetryResultOptions,
): Promise<D2Result<T>> {
  const config = resolveConfig(options);
  const checkTransient = options?.isTransientResult ?? isTransientResult;

  let lastResult: D2Result<T> | undefined;

  for (let attempt = 1; attempt <= config.maxAttempts; attempt++) {
    if (config.signal?.aborted) return lastResult ?? D2Result.cancelled<T>();

    try {
      lastResult = await operation(attempt);
    } catch (err: unknown) {
      lastResult = D2Result.unhandledException<T>({
        messages: [
          `Retry attempt ${attempt} threw: ${err instanceof Error ? err.message : String(err)}`,
        ],
      });
    }

    // Success → return
    if (lastResult.success) return lastResult;

    // Permanent failure → return immediately
    if (!checkTransient(lastResult)) return lastResult;

    // Transient failure → delay + retry (unless last attempt)
    if (attempt < config.maxAttempts) {
      await config.delayFn(
        calculateDelay(
          attempt - 1,
          config.baseDelayMs,
          config.backoffMultiplier,
          config.maxDelayMs,
          config.jitter,
        ),
        config.signal,
      );
    }
  }

  return lastResult ?? D2Result.cancelled<T>();
}

// ---------------------------------------------------------------------------
// Dirty version: operation returns raw TRaw, caller provides mapping
// ---------------------------------------------------------------------------

/**
 * Options for `retryExternalAsync` (dirty retrier).
 */
export interface RetryExternalOptions extends RetryConfig {
  /** Maps thrown exception → D2Result. Default: D2Result.unhandledException(). */
  mapError?: (error: unknown) => D2Result<unknown>;
  /** Override default D2Result transient detection (applied to mapped result). */
  isTransientResult?: (result: D2Result<unknown>) => boolean;
}

/**
 * Retry an external operation with mapping to D2Result.
 *
 * The operation returns a raw `TRaw` value. The caller provides `mapResult`
 * to convert raw responses to D2Result, and optionally `mapError` for exceptions.
 *
 * After mapping, the same transient detection as `retryResultAsync` is used.
 *
 * Never throws.
 */
export async function retryExternalAsync<TRaw, TData>(
  operation: (attempt: number) => Promise<TRaw>,
  mapResult: (response: TRaw) => D2Result<TData>,
  options?: RetryExternalOptions,
): Promise<D2Result<TData>> {
  const config = resolveConfig(options);
  const mapError = options?.mapError ?? (() => D2Result.unhandledException());
  const checkTransient = options?.isTransientResult ?? isTransientResult;

  let lastResult: D2Result<TData> | undefined;

  for (let attempt = 1; attempt <= config.maxAttempts; attempt++) {
    if (config.signal?.aborted) return lastResult ?? D2Result.cancelled<TData>();

    let mapped: D2Result<unknown>;

    try {
      const raw = await operation(attempt);
      const d2 = mapResult(raw);
      mapped = d2;

      // Success → return the properly typed result
      if (d2.success) return d2;
      lastResult = d2;
    } catch (error: unknown) {
      mapped = mapError(error);
      lastResult = D2Result.bubble<TData>(mapped);
    }

    // Permanent failure → return immediately
    if (!checkTransient(mapped)) return lastResult;

    // Transient failure → delay + retry (unless last attempt)
    if (attempt < config.maxAttempts) {
      await config.delayFn(
        calculateDelay(
          attempt - 1,
          config.baseDelayMs,
          config.backoffMultiplier,
          config.maxDelayMs,
          config.jitter,
        ),
        config.signal,
      );
    }
  }

  return lastResult ?? D2Result.cancelled<TData>();
}

// ---------------------------------------------------------------------------
// Internal helpers (duplicated from @d2/utilities to keep zero deps)
// ---------------------------------------------------------------------------

function resolveConfig(options?: RetryConfig) {
  return {
    maxAttempts: options?.maxAttempts ?? 5,
    baseDelayMs: options?.baseDelayMs ?? 1000,
    backoffMultiplier: options?.backoffMultiplier ?? 2,
    maxDelayMs: options?.maxDelayMs ?? 30_000,
    jitter: options?.jitter ?? true,
    signal: options?.signal,
    delayFn: options?._delayFn ?? defaultDelay,
  };
}

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
