// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import {
  type IPayloadSealer,
  PayloadSealer,
  type RecipientPublicKeyring,
} from "@dcsv-io/d2-encryption";
import type { ILogger } from "@dcsv-io/d2-logging";
import { RetryHelper } from "@dcsv-io/d2-resilience";
import { type D2Result, HttpStatusCode } from "@dcsv-io/d2-result";

import type { SealingClient } from "./sealing-client.js";

/** Default bounded refresh attempts before serving the current keyring. */
const _DEFAULT_REFRESH_ATTEMPTS = 3;

/** Default base backoff between bounded rotation-refresh attempts (ms). */
const _DEFAULT_REFRESH_BASE_DELAY_MS = 2_000;

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

/** Tunables for the hot-swap sealer. */
export interface KeyringBackedSealerOptions {
  readonly logger: ILogger;
  readonly refreshAttempts?: number;
  readonly refreshBaseDelayMs?: number;
}

/**
 * A KeyCustodian-backed {@link IPayloadSealer} with LAZY public-key fetch — the
 * TS twin of the .NET `KeyringBackedPayloadSealer`. The first `seal` triggers the
 * fetch (a producer host must not fail BOOT because a recipient has not lazily
 * provisioned yet); a failed fetch surfaces as a typed error the publisher maps
 * to a retryable failure, NEVER a plaintext fallback. Rotation re-fetches the
 * recipient's public keyring and swaps the reference (public material — no
 * zeroize; the swap still lets in-flight seals finish on a coherent keyring).
 */
export class KeyringBackedPayloadSealer implements IPayloadSealer {
  #sealer: PayloadSealer | undefined;
  // The in-flight first-fetch, shared by concurrent first-sealers so a cold-start
  // burst issues ONE fetch (the .NET twin dedupes under `r_initLock`). Cleared on
  // settle so a failed fetch re-attempts on the next seal.
  #initPromise: Promise<PayloadSealer> | undefined;
  #disposed = false;
  readonly #client: SealingClient;
  readonly #recipientServiceId: string;
  readonly #logger: ILogger;
  readonly #refreshAttempts: number;
  readonly #refreshBaseDelayMs: number;

  private constructor(
    client: SealingClient,
    recipientServiceId: string,
    options: KeyringBackedSealerOptions,
  ) {
    this.#client = client;
    this.#recipientServiceId = recipientServiceId;
    this.#logger = options.logger;
    this.#refreshAttempts =
      options.refreshAttempts ?? _DEFAULT_REFRESH_ATTEMPTS;
    this.#refreshBaseDelayMs =
      options.refreshBaseDelayMs ?? _DEFAULT_REFRESH_BASE_DELAY_MS;
  }

  /**
   * Builds a lazy sealer — NOT async (no boot fetch; the first `seal` fetches).
   *
   * @param client The sealing client.
   * @param recipientServiceId The recipient service to seal to.
   * @param options Logger + tunables.
   */
  static create(
    client: SealingClient,
    recipientServiceId: string,
    options: KeyringBackedSealerOptions,
  ): KeyringBackedPayloadSealer {
    return new KeyringBackedPayloadSealer(client, recipientServiceId, options);
  }

  /** @inheritdoc */
  async seal(plaintext: Uint8Array): Promise<Uint8Array> {
    this.#throwIfDisposed();

    const sealer = this.#sealer ?? (await this.#ensureSealer());

    return sealer.seal(plaintext);
  }

  /**
   * Re-fetches the recipient public keyring and hot-swaps it. Bounded retry; on
   * exhaustion the current keyring is served (never a gap) with a loud log.
   */
  async refresh(): Promise<void> {
    this.#throwIfDisposed();

    // Bounded, transient-classified retry via the shared RetryHelper (the .NET
    // twin drives the identical refresh through `RetryHelper.RetryD2ResultAsync`):
    // a permanent auth/validation failure short-circuits immediately; a transient
    // failure backs off exponentially up to the cap, then serves the current keyring.
    const result = await RetryHelper.retryD2ResultAsync<RecipientPublicKeyring>(
      () => this.#client.getPublicKeyring(this.#recipientServiceId),
      {
        maxAttempts: this.#refreshAttempts,
        baseDelayMs: this.#refreshBaseDelayMs,
        shouldRetry: isTransientResult,
      },
    );

    if (!result.failed && result.data !== undefined) {
      this.#sealer = new PayloadSealer(result.data);

      return;
    }

    this.#logger.error(
      "sealed public keyring refresh exhausted — serving current keyring",
      {
        attempts: this.#refreshAttempts,
        recipientServiceId: this.#recipientServiceId,
        errorCode: result.errorCode,
      },
    );
  }

  /** Disposes the sealer. Public material carries no key bytes to zeroize. */
  dispose(): void {
    this.#disposed = true;
    this.#sealer = undefined;
    this.#initPromise = undefined;
  }

  // Dedupes concurrent first-seals onto one in-flight fetch, then installs the
  // sealer — unless a rotation refresh already installed a newer keyring while the
  // fetch was in flight (never overwrite a fresher keyring with a staler one).
  #ensureSealer(): Promise<PayloadSealer> {
    this.#initPromise ??= this.#initializeSealer();

    return this.#initPromise;
  }

  async #initializeSealer(): Promise<PayloadSealer> {
    try {
      const keyring = await this.#fetchKeyring();

      this.#sealer ??= new PayloadSealer(keyring);

      return this.#sealer;
    } finally {
      this.#initPromise = undefined;
    }
  }

  async #fetchKeyring(): Promise<RecipientPublicKeyring> {
    const result = await this.#client.getPublicKeyring(
      this.#recipientServiceId,
    );

    if (result.failed || result.data === undefined) {
      throw new Error(
        `KeyCustodian sealed public keyring fetch failed for recipient ` +
          `'${this.#recipientServiceId}' — cannot seal (retryable; never a ` +
          "plaintext fallback).",
      );
    }

    return result.data;
  }

  #throwIfDisposed(): void {
    if (this.#disposed) {
      throw new Error("KeyringBackedPayloadSealer has been disposed.");
    }
  }
}
