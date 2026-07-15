// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import {
  type IPayloadCrypto,
  PayloadCrypto,
  type PayloadCryptoKeyring,
} from "@dcsv-io/d2-encryption";
import type { ILogger } from "@dcsv-io/d2-logging";
import { RetryHelper } from "@dcsv-io/d2-resilience";
import { type D2Result, HttpStatusCode } from "@dcsv-io/d2-result";

import type { KeyringClient } from "./keyring-client.js";

/** Default bounded refresh attempts before serving the current keyring. */
const _DEFAULT_REFRESH_ATTEMPTS = 3;

/** Default base backoff between bounded rotation-refresh attempts (ms). */
const _DEFAULT_REFRESH_BASE_DELAY_MS = 2_000;

/** Default grace before a displaced keyring is zeroized (ms). */
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

/** Tunables for the hot-swap symmetric crypto. */
export interface KeyringBackedCryptoOptions {
  readonly logger: ILogger;
  readonly refreshAttempts?: number;
  readonly refreshBaseDelayMs?: number;
  readonly graceMs?: number;
}

/**
 * A KeyCustodian-backed {@link IPayloadCrypto} with rotation hot-swap — the TS
 * twin of the .NET `KeyringBackedPayloadCrypto` (the symmetric getKeyring
 * consumer runtime). Boot-fetches its keyring fail-loud; a rotation re-fetches
 * and swaps the reference (a plain reference replacement — the single-threaded
 * event loop makes it atomic, so no `Volatile`/`Interlocked` twin is needed).
 * The displaced keyring is zeroized after a grace window so in-flight
 * encrypt/decrypt calls finish on a coherent keyring.
 */
export class KeyringBackedPayloadCrypto implements IPayloadCrypto {
  #keyring: PayloadCryptoKeyring;
  #crypto: PayloadCrypto;
  #disposed = false;
  readonly #client: KeyringClient;
  readonly #keyDomain: string;
  readonly #logger: ILogger;
  readonly #refreshAttempts: number;
  readonly #refreshBaseDelayMs: number;
  readonly #graceMs: number;
  // Each displaced keyring is tracked with its grace timer so dispose zeroizes it
  // (clearing the timer alone would skip the zeroize the timer closure performs).
  readonly #pending = new Map<
    ReturnType<typeof setTimeout>,
    PayloadCryptoKeyring
  >();

  private constructor(
    client: KeyringClient,
    keyDomain: string,
    keyring: PayloadCryptoKeyring,
    options: KeyringBackedCryptoOptions,
  ) {
    this.#client = client;
    this.#keyDomain = keyDomain;
    this.#keyring = keyring;
    this.#crypto = new PayloadCrypto(keyring);
    this.#logger = options.logger;
    this.#refreshAttempts =
      options.refreshAttempts ?? _DEFAULT_REFRESH_ATTEMPTS;
    this.#refreshBaseDelayMs =
      options.refreshBaseDelayMs ?? _DEFAULT_REFRESH_BASE_DELAY_MS;
    this.#graceMs = options.graceMs ?? _DEFAULT_GRACE_MS;
  }

  /**
   * Boot-fetches the keyring fail-loud (a host with KC unreachable does not
   * start — the deliberate fail-loud boot posture) and returns ready crypto.
   *
   * @param client The keyring client.
   * @param keyDomain The key domain to bind to.
   * @param options Logger + tunables.
   */
  static async create(
    client: KeyringClient,
    keyDomain: string,
    options: KeyringBackedCryptoOptions,
  ): Promise<KeyringBackedPayloadCrypto> {
    const result = await client.getKeyring(keyDomain);

    if (result.failed || result.data === undefined) {
      throw new Error(
        `KeyCustodian keyring fetch failed at boot for domain '${keyDomain}' — ` +
          "the crypto cannot start (fail-closed).",
      );
    }

    return new KeyringBackedPayloadCrypto(
      client,
      keyDomain,
      result.data,
      options,
    );
  }

  /** @inheritdoc */
  encrypt(plaintext: Uint8Array): Promise<Uint8Array> {
    this.#throwIfDisposed();

    return this.#crypto.encrypt(plaintext);
  }

  /** @inheritdoc */
  decrypt(framed: Uint8Array): Promise<Uint8Array> {
    this.#throwIfDisposed();

    return this.#crypto.decrypt(framed);
  }

  /**
   * Re-fetches the keyring and hot-swaps it. Bounded retry; on exhaustion the
   * current keyring is served (never a gap) with a loud log.
   */
  async refresh(): Promise<void> {
    this.#throwIfDisposed();

    // Bounded, transient-classified retry via the shared RetryHelper (the .NET
    // twin drives the identical refresh through `RetryHelper.RetryD2ResultAsync`):
    // a permanent auth/validation failure short-circuits immediately; a transient
    // failure backs off exponentially up to the cap, then serves the current keyring.
    const result = await RetryHelper.retryD2ResultAsync<PayloadCryptoKeyring>(
      () => this.#client.getKeyring(this.#keyDomain),
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

    this.#logger.error("keyring refresh exhausted — serving current keyring", {
      attempts: this.#refreshAttempts,
      keyDomain: this.#keyDomain,
      errorCode: result.errorCode,
    });
  }

  #swap(next: PayloadCryptoKeyring): void {
    const displaced = this.#keyring;
    this.#keyring = next;
    this.#crypto = new PayloadCrypto(next);

    const timer = setTimeout(() => {
      displaced.dispose();
      this.#pending.delete(timer);
    }, this.#graceMs);

    timer.unref();
    this.#pending.set(timer, displaced);
  }

  /** Disposes the crypto — cancels pending grace timers and zeroizes the keyring. */
  dispose(): void {
    if (this.#disposed) {
      return;
    }

    this.#disposed = true;

    // Force-zeroize every still-pending displaced keyring (mirrors the .NET twin's
    // drain-and-force-zeroize): clearing the timer alone would skip the zeroize its
    // closure performs, leaking key material for any rotation within the grace window.
    for (const [timer, displaced] of this.#pending) {
      clearTimeout(timer);
      displaced.dispose();
    }

    this.#pending.clear();
    this.#keyring.dispose();
  }

  #throwIfDisposed(): void {
    if (this.#disposed) {
      throw new Error("KeyringBackedPayloadCrypto has been disposed.");
    }
  }
}
