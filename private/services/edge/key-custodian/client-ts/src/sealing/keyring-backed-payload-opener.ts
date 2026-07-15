// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import {
  type IPayloadOpener,
  PayloadOpener,
  type RecipientPrivateKeyring,
} from "@dcsv-io/d2-encryption";
import type { ILogger } from "@dcsv-io/d2-logging";
import { RetryHelper } from "@dcsv-io/d2-resilience";
import { type D2Result, HttpStatusCode } from "@dcsv-io/d2-result";

import type { SealingClient } from "./sealing-client.js";

/** Default bounded refresh attempts before serving the current keyring. */
const _DEFAULT_REFRESH_ATTEMPTS = 3;

/** Default base backoff between bounded rotation-refresh attempts (ms). */
const _DEFAULT_REFRESH_BASE_DELAY_MS = 2_000;

/** Default grace before a displaced private keyring is zeroized (ms). */
const _DEFAULT_GRACE_MS = 30_000;

/**
 * Transient-result classifier for the rotation-refresh retry — the TS twin of the
 * .NET `RetryHelper.RetryD2ResultAsync` default `ShouldRetry` (retries only
 * `ServiceUnavailable` / rate-limited, never a permanent auth/validation failure).
 */
const isTransientResult = (result: unknown): boolean => {
  const { statusCode } = result as D2Result<unknown>;

  return (
    statusCode === HttpStatusCode.ServiceUnavailable ||
    statusCode === HttpStatusCode.TooManyRequests
  );
};

/** Tunables for the hot-swap opener. */
export interface KeyringBackedOpenerOptions {
  readonly logger: ILogger;
  readonly refreshAttempts?: number;
  readonly refreshBaseDelayMs?: number;
  readonly graceMs?: number;
}

/**
 * A KeyCustodian-backed {@link IPayloadOpener} with rotation hot-swap — the TS
 * twin of the .NET `KeyringBackedPayloadOpener`. Holds this service's private
 * sealing keyring; a rotation re-fetches and swaps the reference. The swap is a
 * plain reference replacement: the single-threaded event loop makes it atomic,
 * so no `Volatile`/`Interlocked` twin is needed (unlike the .NET runtime). The
 * displaced keyring is zeroized after a grace window so in-flight opens finish
 * on a coherent keyring.
 */
export class KeyringBackedPayloadOpener implements IPayloadOpener {
  #keyring: RecipientPrivateKeyring;
  #opener: PayloadOpener;
  #disposed = false;
  readonly #client: SealingClient;
  readonly #logger: ILogger;
  readonly #refreshAttempts: number;
  readonly #refreshBaseDelayMs: number;
  readonly #graceMs: number;
  // Each displaced private keyring is tracked with its grace timer so dispose
  // zeroizes it (clearing the timer alone would skip the closure's zeroize).
  readonly #pending = new Map<
    ReturnType<typeof setTimeout>,
    RecipientPrivateKeyring
  >();

  private constructor(
    client: SealingClient,
    keyring: RecipientPrivateKeyring,
    options: KeyringBackedOpenerOptions,
  ) {
    this.#client = client;
    this.#keyring = keyring;
    this.#opener = new PayloadOpener(keyring);
    this.#logger = options.logger;
    this.#refreshAttempts =
      options.refreshAttempts ?? _DEFAULT_REFRESH_ATTEMPTS;
    this.#refreshBaseDelayMs =
      options.refreshBaseDelayMs ?? _DEFAULT_REFRESH_BASE_DELAY_MS;
    this.#graceMs = options.graceMs ?? _DEFAULT_GRACE_MS;
  }

  /**
   * Boot-fetches the private keyring fail-loud (a host with KC unreachable does
   * not start — the deliberate fail-loud boot posture) and returns a ready opener.
   *
   * @param client The sealing client.
   * @param options Logger + tunables.
   */
  static async create(
    client: SealingClient,
    options: KeyringBackedOpenerOptions,
  ): Promise<KeyringBackedPayloadOpener> {
    const result = await client.getOwnPrivateKeyring();

    if (result.failed || result.data === undefined) {
      throw new Error(
        "KeyCustodian sealed private keyring fetch failed at boot — the opener " +
          "cannot start (fail-closed).",
      );
    }

    return new KeyringBackedPayloadOpener(client, result.data, options);
  }

  /** @inheritdoc */
  open(framed: Uint8Array): Promise<Uint8Array> {
    this.#throwIfDisposed();

    return this.#opener.open(framed);
  }

  /**
   * Re-fetches the private keyring and hot-swaps it. Bounded retry; on
   * exhaustion the current keyring is served (never a gap) with a loud log.
   */
  async refresh(): Promise<void> {
    this.#throwIfDisposed();

    // Bounded, transient-classified retry via the shared RetryHelper (the .NET
    // twin drives the identical refresh through `RetryHelper.RetryD2ResultAsync`):
    // a permanent auth/validation failure short-circuits immediately; a transient
    // failure backs off exponentially up to the cap, then serves the current keyring.
    const result =
      await RetryHelper.retryD2ResultAsync<RecipientPrivateKeyring>(
        () => this.#client.getOwnPrivateKeyring(),
        {
          maxAttempts: this.#refreshAttempts,
          baseDelayMs: this.#refreshBaseDelayMs,
          shouldRetry: isTransientResult,
        },
      );

    if (!result.failed && result.data !== undefined) {
      this.#swap(result.data);

      return;
    }

    this.#logger.error(
      "sealed private keyring refresh exhausted — serving current keyring",
      { attempts: this.#refreshAttempts, errorCode: result.errorCode },
    );
  }

  #swap(next: RecipientPrivateKeyring): void {
    const displaced = this.#keyring;
    this.#keyring = next;
    this.#opener = new PayloadOpener(next);

    const timer = setTimeout(() => {
      displaced.dispose();
      this.#pending.delete(timer);
    }, this.#graceMs);

    // Do not keep the event loop alive for a grace timer (Node timer handle).
    timer.unref();
    this.#pending.set(timer, displaced);
  }

  /** Disposes the opener — cancels pending grace timers and zeroizes the keyring. */
  dispose(): void {
    if (this.#disposed) {
      return;
    }

    this.#disposed = true;

    // Force-zeroize every still-pending displaced private keyring (mirrors the .NET
    // twin's drain-and-force-zeroize): clearing the timer alone would skip the
    // zeroize its closure performs, leaking private key material for any rotation
    // within the grace window before dispose.
    for (const [timer, displaced] of this.#pending) {
      clearTimeout(timer);
      displaced.dispose();
    }

    this.#pending.clear();
    this.#keyring.dispose();
  }

  #throwIfDisposed(): void {
    if (this.#disposed) {
      throw new Error("KeyringBackedPayloadOpener has been disposed.");
    }
  }
}
