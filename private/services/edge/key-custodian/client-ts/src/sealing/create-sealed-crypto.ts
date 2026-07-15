// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { ConsumerServiceByDomain } from "@dcsv-io/d2-encryption-abstractions";
import type { ILogger } from "@dcsv-io/d2-logging";

import type { RotationSubscription } from "../rotation/rotation-subscription.js";
import { KeyringBackedPayloadOpener } from "./keyring-backed-payload-opener.js";
import { KeyringBackedPayloadSealer } from "./keyring-backed-payload-sealer.js";
import { PRODUCT_SEALED_CONSUMER_SERVICES } from "./product-sealed-consumers.js";
import type { SealingClient } from "./sealing-client.js";

const _SERVICE_ID_GRAMMAR = /^[a-z0-9-]{1,64}$/;

/** The wired sealed-crypto instances the host passes into publisher/consumer composition. */
export interface SealedCryptoWiring {
  /** A hot-swap sealer per distinct generated consumer service. */
  readonly sealersByConsumerService: ReadonlyMap<
    string,
    KeyringBackedPayloadSealer
  >;
  /**
   * This service's hot-swap opener — present ONLY when this service is named as
   * some sealed domain's consumer (a non-consumer host gets none: least-privilege,
   * structurally matching the .NET opener-arm rule).
   */
  readonly ownOpener?: KeyringBackedPayloadOpener;
  /** Unsubscribes rotation handlers and disposes every wired instance. */
  dispose(): void;
}

/** Options for {@link createSealedCryptoViaKeyCustodian}. */
export interface CreateSealedCryptoOptions {
  /** This service's id (lowercase `[a-z0-9-]`, at most 64 chars). */
  readonly ownServiceId: string;
  /** The KeyCustodian sealing client (dialed over the mTLS channel by the host). */
  readonly sealingClient: SealingClient;
  /** Structured logger (never receives an `Error` directly — PII safety). */
  readonly logger: ILogger;
  /**
   * The rotation subscription — REQUIRED. Wires `seal:<svc>` refresh for every
   * sealer and the opener. Never optional: a sealed consumer that skips rotation
   * silently serves STALE keys after KeyCustodian rotates until the process
   * restarts. The .NET twin (`AddD2SealedEncryptionViaKeyCustodian`) always wires
   * the rotation subscriber.
   */
  readonly rotationSubscription: RotationSubscription;
  /** Bounded refresh attempts before serving the current keyring. */
  readonly refreshAttempts?: number;
  /** Grace before a displaced private keyring is zeroized (ms). */
  readonly graceMs?: number;
}

/**
 * The ONE spec-driven call that wires a service's sealed encryption support — the
 * TS twin of the .NET `AddD2SealedEncryptionViaKeyCustodian(ownServiceId)`. All
 * spec-driven off the generated `ConsumerServiceByDomain` lookup:
 *
 * - builds a hot-swap sealer for EVERY distinct generated consumer service (lazy
 *   public-key fetch — a producer never fails boot on a not-yet-provisioned
 *   recipient);
 * - builds this service's private-keyring opener ONLY when `ConsumerServiceByDomain`
 *   names this service as some sealed domain's consumer (boot fetch awaited = the
 *   fail-loud twin; a non-consumer host gets no opener — least-privilege);
 * - always wires rotation refresh on `seal:<svc>` (the subscription is required —
 *   omitting it would silently serve stale keys after a rotation).
 *
 * The returned instances are passed explicitly into `createPublisher({ crypto })`
 * and the consumer `CryptoBodyOpener` composition — the one-call ergonomics as
 * composition rather than ambient DI (there is no DI container in Node).
 *
 * @param options The wiring options.
 * @returns The wired sealers + optional opener + a dispose.
 * @throws When `ownServiceId` violates the service-id grammar.
 */
export async function createSealedCryptoViaKeyCustodian(
  options: CreateSealedCryptoOptions,
): Promise<SealedCryptoWiring> {
  if (!_SERVICE_ID_GRAMMAR.test(options.ownServiceId)) {
    throw new Error(
      `ownServiceId '${options.ownServiceId}' must match the workload service-id ` +
        "grammar (lowercase [a-z0-9-], 1-64 chars).",
    );
  }

  // Public catalog (framework fixture) ∪ product sealed consumers (private contracts).
  const consumerServices = [
    ...new Set([
      ...Object.values(ConsumerServiceByDomain),
      ...PRODUCT_SEALED_CONSUMER_SERVICES,
    ]),
  ];
  const sealers = new Map<string, KeyringBackedPayloadSealer>();
  const unsubscribes: (() => void)[] = [];

  for (const svc of consumerServices) {
    const sealer = KeyringBackedPayloadSealer.create(
      options.sealingClient,
      svc,
      {
        logger: options.logger,
        refreshAttempts: options.refreshAttempts,
      },
    );
    sealers.set(svc, sealer);

    unsubscribes.push(
      options.rotationSubscription.onRotation(`seal:${svc}`, () => {
        // Fire-and-forget is intentional (§4.18): the sealer rotation refresh is
        // best-effort — its bounded retry logs on exhaustion and keeps serving the
        // current public keyring, so a failed refresh never rejects the handler.
        void sealer.refresh();
      }),
    );
  }

  let ownOpener: KeyringBackedPayloadOpener | undefined;

  if ((consumerServices as string[]).includes(options.ownServiceId)) {
    ownOpener = await KeyringBackedPayloadOpener.create(options.sealingClient, {
      logger: options.logger,
      refreshAttempts: options.refreshAttempts,
      graceMs: options.graceMs,
    });
    const opener = ownOpener;

    unsubscribes.push(
      options.rotationSubscription.onRotation(
        `seal:${options.ownServiceId}`,
        () => {
          // Fire-and-forget is intentional (§4.18): the opener rotation refresh is
          // best-effort — its bounded retry logs on exhaustion and keeps serving the
          // current private keyring, so a failed refresh never rejects the handler.
          void opener.refresh();
        },
      ),
    );
  }

  let disposed = false;

  return {
    sealersByConsumerService: sealers,
    ownOpener,
    dispose: () => {
      if (disposed) {
        return;
      }

      disposed = true;

      for (const unsubscribe of unsubscribes) {
        unsubscribe();
      }

      for (const sealer of sealers.values()) {
        sealer.dispose();
      }

      ownOpener?.dispose();
    },
  };
}
