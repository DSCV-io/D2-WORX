// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import type { IPayloadCrypto } from "@dcsv-io/d2-encryption";
import type { ILogger } from "@dcsv-io/d2-logging";
import { falsey } from "@dcsv-io/d2-utilities";

import type { RotationSubscription } from "../rotation/rotation-subscription.js";
import { KeyringBackedPayloadCrypto } from "./keyring-backed-payload-crypto.js";
import type { KeyringClient } from "./keyring-client.js";

/** Options for {@link createEncryptionViaKeyring}. */
export interface CreateEncryptionViaKeyringOptions {
  /** The symmetric payload key domain to serve (non-empty; e.g. `audit`). */
  readonly keyDomain: string;
  /** The KeyCustodian keyring client (dialed over the mTLS channel by the host). */
  readonly keyringClient: KeyringClient;
  /**
   * The rotation subscription — REQUIRED. Wiring rotation is never optional: a
   * symmetric consumer that skips it silently serves STALE keys after KeyCustodian
   * rotates until the process restarts. The .NET twin (`AddD2EncryptionForViaKeyring`)
   * cannot omit the rotation subscriber either.
   */
  readonly rotationSubscription: RotationSubscription;
  /** Structured logger (never receives an `Error` directly — PII safety). */
  readonly logger: ILogger;
  /** Bounded refresh attempts before serving the current keyring. */
  readonly refreshAttempts?: number;
  /** Base backoff between bounded rotation-refresh attempts (ms). */
  readonly refreshBaseDelayMs?: number;
  /** Grace before a displaced keyring is zeroized (ms). */
  readonly graceMs?: number;
}

/** The wired symmetric crypto + a dispose the host owns. */
export interface KeyringCryptoWiring {
  /** The hot-swap {@link IPayloadCrypto} the host passes into publisher/consumer composition. */
  readonly crypto: IPayloadCrypto;
  /** Unsubscribes the rotation handler and zeroizes the keyring. */
  dispose(): void;
}

/**
 * The ONE call that wires a service's symmetric (shared-keyring) payload encryption —
 * the TS twin of the .NET `AddD2EncryptionForViaKeyring(domain)`. It:
 *
 * - boot-fetches the keyring fail-loud via {@link KeyringBackedPayloadCrypto.create}
 *   (a host with KeyCustodian unreachable does not start — never a silent/plaintext
 *   fallback);
 * - wires rotation refresh on the bare key domain (the .NET twin subscribes
 *   `channel.Subscribe(domain, ...)` on the same bare domain — a symmetric keyring
 *   has no `seal:` prefix);
 * - returns the ready {@link IPayloadCrypto} plus a dispose that stops rotations
 *   first, then zeroizes.
 *
 * The returned `crypto` is passed explicitly into `createPublisher({ crypto })` and
 * the consumer `CryptoBodyOpener` composition — the one-call ergonomics as
 * composition rather than ambient DI (there is no DI container in Node). Rotation
 * wiring is required, so a caller can never silently ship a stale-key footgun.
 *
 * @param options The wiring options.
 * @returns The wired crypto + a dispose.
 * @throws When `keyDomain` is empty/whitespace, or the boot keyring fetch fails.
 */
export async function createEncryptionViaKeyring(
  options: CreateEncryptionViaKeyringOptions,
): Promise<KeyringCryptoWiring> {
  if (falsey(options.keyDomain)) {
    throw new Error("keyDomain must be a non-empty key domain.");
  }

  const crypto = await KeyringBackedPayloadCrypto.create(
    options.keyringClient,
    options.keyDomain,
    {
      logger: options.logger,
      refreshAttempts: options.refreshAttempts,
      refreshBaseDelayMs: options.refreshBaseDelayMs,
      graceMs: options.graceMs,
    },
  );

  const unsubscribe = options.rotationSubscription.onRotation(
    options.keyDomain,
    () => {
      // Fire-and-forget is intentional (§4.18): the rotation refresh is best-effort —
      // its own bounded retry logs on exhaustion and keeps serving the current keyring,
      // so a failed refresh never rejects the event handler or degrades correctness.
      void crypto.refresh();
    },
  );

  let disposed = false;

  return {
    crypto,
    dispose: () => {
      if (disposed) {
        return;
      }

      disposed = true;
      unsubscribe();
      crypto.dispose();
    },
  };
}
