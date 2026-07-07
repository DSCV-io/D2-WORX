// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { ConsumerServiceByDomain } from "@d2/encryption-abstractions";
import type { ILogger } from "@d2/logging";

import { KeyringBackedPayloadOpener } from "./keyring-backed-payload-opener.js";
import { KeyringBackedPayloadSealer } from "./keyring-backed-payload-sealer.js";
import type { SealingClient } from "./sealing-client.js";

const _SERVICE_ID_GRAMMAR = /^[a-z0-9-]{1,64}$/;

/**
 * The rotation-event subscription port — the host adapts `@d2/messaging-rabbitmq`
 * `subscribe` (domain-filtered on `seal:<serviceId>`) to this shape. Modeled as
 * an injected port so the sealed-crypto wiring stays broker-decoupled and
 * unit-testable (the TS "composition instead of registration" divergence).
 */
export interface RotationSubscription {
  /**
   * Registers a handler for rotation events on a domain; returns an unsubscribe.
   *
   * @param domain The rotation domain (e.g. `seal:audit`).
   * @param handler Invoked on each rotation event for that domain.
   */
  onRotation(domain: string, handler: () => void): () => void;
}

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
  /** Optional rotation subscription — wires `seal:<svc>` refresh when supplied. */
  readonly rotationSubscription?: RotationSubscription;
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
 * - wires rotation refresh on `seal:<svc>` when a rotation subscription is given.
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

  const consumerServices = [...new Set(Object.values(ConsumerServiceByDomain))];
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

    if (options.rotationSubscription !== undefined) {
      unsubscribes.push(
        options.rotationSubscription.onRotation(`seal:${svc}`, () => {
          void sealer.refresh();
        }),
      );
    }
  }

  let ownOpener: KeyringBackedPayloadOpener | undefined;

  if ((consumerServices as string[]).includes(options.ownServiceId)) {
    ownOpener = await KeyringBackedPayloadOpener.create(options.sealingClient, {
      logger: options.logger,
      refreshAttempts: options.refreshAttempts,
      graceMs: options.graceMs,
    });
    const opener = ownOpener;

    if (options.rotationSubscription !== undefined) {
      unsubscribes.push(
        options.rotationSubscription.onRotation(
          `seal:${options.ownServiceId}`,
          () => {
            void opener.refresh();
          },
        ),
      );
    }
  }

  return {
    sealersByConsumerService: sealers,
    ownOpener,
    dispose: () => {
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
