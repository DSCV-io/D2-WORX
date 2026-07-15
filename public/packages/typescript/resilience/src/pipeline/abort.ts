// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

/**
 * Internal abort-signal plumbing shared across the pipeline layers. Mirrors
 * the .NET resilience lib's `CancellationToken` / `CancellationTokenSource`
 * usage:
 *
 * - {@link linkAbort} ≈ `CancellationTokenSource.CreateLinkedTokenSource` — a
 *   derived controller aborted when EITHER the parent signal OR an explicit
 *   `abort()` fires; `dispose()` removes the parent listener (no leak).
 * - {@link raceAbort} ≈ `Task.WaitAsync(ct)` — settle as soon as the wrapped
 *   promise settles OR the signal aborts, whichever comes first, propagating
 *   an {@link AbortError} on abort and cleaning up the listener either way.
 * - {@link throwIfAborted} is the pre-flight guard the layers use to reject an
 *   already-aborted caller before entering a gate.
 *
 * Layers distinguish a caller-initiated abort from a timeout by inspecting the
 * caller signal directly (`signal?.aborted`) rather than sniffing error names.
 *
 * Not exported from the package barrel — this is layer-internal wiring.
 */

/**
 * The canonical caller-cancellation error. `name === "AbortError"` matches the
 * DOM `AbortController` convention and the existing
 * `RetryHelper.isCancellation` check (which already treats `name === "AbortError"`
 * and `message === "aborted"` as cancellation, never retried). Distinct from
 * `TimeoutError` so a timeout stays transient-retryable while a caller abort
 * does not.
 */
export class AbortError extends Error {
  constructor(message = "aborted") {
    super(message);
    this.name = "AbortError";
  }
}

/** Throws an {@link AbortError} immediately when `signal` is already aborted. */
export function throwIfAborted(signal?: AbortSignal): void {
  if (signal?.aborted === true) throw new AbortError();
}

/** A derived controller plus a disposer that detaches its parent listener. */
export interface LinkedAbort {
  /** The derived controller — its signal is what an inner op should observe. */
  readonly controller: AbortController;
  /** Detaches the parent-signal listener. Idempotent; ALWAYS call it. */
  readonly dispose: () => void;
}

/**
 * Creates an {@link AbortController} that aborts when the supplied parent
 * `signal` aborts (or is already aborted), in addition to any explicit
 * `controller.abort(...)` the caller issues. Mirrors
 * `CancellationTokenSource.CreateLinkedTokenSource(parent)` — the returned
 * controller's signal is the "linked token" inner work should observe.
 *
 * `dispose()` removes the parent listener so a long-lived parent signal does
 * not retain the (short-lived) derived controller. Always call it once the
 * linked scope ends.
 */
export function linkAbort(signal?: AbortSignal): LinkedAbort {
  const controller = new AbortController();

  if (signal === undefined) {
    return { controller, dispose: () => {} };
  }

  if (signal.aborted) {
    controller.abort(signal.reason);
    return { controller, dispose: () => {} };
  }

  const onAbort = (): void => controller.abort(signal.reason);
  signal.addEventListener("abort", onAbort, { once: true });

  return {
    controller,
    dispose: () => signal.removeEventListener("abort", onAbort),
  };
}

/**
 * Settles as soon as `promise` settles OR `signal` aborts, whichever happens
 * first. On abort, rejects with an {@link AbortError} (the caller's wait is
 * canceled) WITHOUT disturbing `promise` itself — the wrapped work keeps
 * running for any other awaiters (mirrors `Task.WaitAsync(ct)`, which cancels
 * only this caller's wait, not the shared `Task`). The abort listener is
 * always removed once either side settles, so no listener leaks.
 *
 * When `signal` is undefined the wrapped promise is returned unchanged.
 */
export function raceAbort<T>(
  promise: Promise<T>,
  signal?: AbortSignal,
): Promise<T> {
  if (signal === undefined) return promise;
  if (signal.aborted) return Promise.reject(new AbortError());

  return new Promise<T>((resolve, reject) => {
    const onAbort = (): void => reject(new AbortError());
    signal.addEventListener("abort", onAbort, { once: true });

    const cleanup = (): void => signal.removeEventListener("abort", onAbort);
    promise.then(
      (value) => {
        cleanup();
        resolve(value);
      },
      (err: unknown) => {
        cleanup();
        reject(err);
      },
    );
  });
}
