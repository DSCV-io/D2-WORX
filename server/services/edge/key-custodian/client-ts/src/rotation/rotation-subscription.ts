// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

/**
 * The rotation-event subscription port — the host adapts `@d2/messaging-rabbitmq`
 * `subscribe` (domain-filtered) to this shape. Modeled as an injected port so the
 * KC-backed crypto wiring stays broker-decoupled and unit-testable (the TS
 * "composition instead of registration" divergence). The behavioral twin of the
 * .NET `IRotationEventChannel` that BOTH the symmetric keyring holder
 * (`KeyringBackedPayloadCrypto`) and the sealed holders subscribe through.
 *
 * Shared by BOTH KC-backed encryption wirings: the symmetric one-call
 * `createEncryptionViaKeyring` subscribes on the bare key domain (e.g. `audit`),
 * and the sealed `createSealedCryptoViaKeyCustodian` on `seal:<serviceId>`.
 */
export interface RotationSubscription {
  /**
   * Registers a handler for rotation events on a domain; returns an unsubscribe.
   *
   * @param domain The rotation domain — the bare key domain for a symmetric keyring
   *   (e.g. `audit`), or `seal:<serviceId>` for a sealed domain.
   * @param handler Invoked on each rotation event for that domain.
   */
  onRotation(domain: string, handler: () => void): () => void;
}
