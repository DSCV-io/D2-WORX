// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import type { IPayloadCrypto, IPayloadSealer } from "@dcsv-io/d2-encryption";
import type {
  EncryptionDomain,
  EncryptionDomainModes,
} from "@dcsv-io/d2-encryption-abstractions";
import type {
  MqMessageCatalogKey,
  MqMessagesCatalog,
} from "@dcsv-io/d2-messaging-abstractions";

/**
 * The compile-time type-witness that fuses publishing and encryption (the TS
 * twin of the .NET spec-driven composer + DI). Publishing a message to an
 * encrypted domain that was not wired is a COMPILE error, and there is no
 * raw-bytes publish surface — the composer for a domain is the ONLY path to the
 * socket for that domain. Runtime default-deny (see `publisher.ts`) is the
 * second lock for dynamic / fixture paths that bypass the static catalog.
 */

/**
 * Domains that carry payload encryption — every domain except the `plaintext`
 * sentinel (which ships cleartext and needs no composer).
 */
export type EncryptedDomain = Exclude<EncryptionDomain, "plaintext">;

/**
 * The composer a domain requires, branded by its encryption mode: a `sealed`
 * domain accepts only an {@link IPayloadSealer}; a `symmetric` domain only an
 * {@link IPayloadCrypto}. Driven by the literal `EncryptionDomainModes` types so
 * a mode change reshapes the required composer automatically.
 */
export type ComposerFor<D extends EncryptedDomain> =
  (typeof EncryptionDomainModes)[D] extends "sealed"
    ? IPayloadSealer
    : IPayloadCrypto;

/**
 * The full wiring map — every encrypted domain to its mode-branded composer.
 * A host passes a `Partial` of this (it wires only the domains it publishes to);
 * `createPublisher` captures the wired subset's literal keys.
 */
export type DomainCryptoMap = {
  [D in EncryptedDomain]: ComposerFor<D>;
};

/** The literal `encryption` value a cataloged message publishes under. */
export type CatalogEncryption<K extends MqMessageCatalogKey> =
  (typeof MqMessagesCatalog)[K]["encryption"];

/**
 * The message constants of an arbitrary catalog `TCatalog` publishable given the
 * wired composer map `TWired`: a message is publishable iff its `encryption`
 * literal is `plaintext` (always cleartext) or is a key of `TWired` (a composer
 * is wired for it). Generic over the catalog so the compile-time type-witness is
 * provable against a synthetic catalog (the production catalog carries no sealed
 * messages yet).
 */
export type PublishableKeyOf<TCatalog, TWired> = {
  [K in keyof TCatalog]: TCatalog[K] extends { readonly encryption: infer E }
    ? E extends keyof TWired | "plaintext"
      ? K
      : never
    : never;
}[keyof TCatalog];

/**
 * The publishable message constants of the PRODUCTION catalog given the wired
 * composer map `TWired`. Publishing any other message is a COMPILE error — there
 * is no overload that skips composition.
 */
export type PublishableKey<TWired> = PublishableKeyOf<
  typeof MqMessagesCatalog,
  TWired
>;
