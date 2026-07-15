// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

// Test-only key material generators. Every symbol carries the `Fixture` marker
// (§7.23) — these produce throwaway P-256 / P-384 keypairs for round-trip and
// adversarial tests; NEVER production key material.

import { webcrypto } from "node:crypto";

import { PayloadCryptoKeyring } from "../src/payload-crypto-keyring.js";
import { RecipientPrivateKeyring } from "../src/recipient-private-keyring.js";
import { RecipientPublicKeyring } from "../src/recipient-public-keyring.js";

/** A throwaway EC keypair (public SPKI + private PKCS#8), test-only. */
export interface FixtureKeypair {
  readonly publicSpki: Uint8Array;
  readonly privatePkcs8: Uint8Array;
}

async function exportKeypair(
  pair: webcrypto.CryptoKeyPair,
): Promise<FixtureKeypair> {
  const publicSpki = new Uint8Array(
    await webcrypto.subtle.exportKey("spki", pair.publicKey),
  );
  const privatePkcs8 = new Uint8Array(
    await webcrypto.subtle.exportKey("pkcs8", pair.privateKey),
  );

  return { publicSpki, privatePkcs8 };
}

/** Generates a throwaway P-256 ECDH keypair for sealed-mode tests. */
export async function generateFixtureP256Keypair(): Promise<FixtureKeypair> {
  const pair = await webcrypto.subtle.generateKey(
    { name: "ECDH", namedCurve: "P-256" },
    true,
    ["deriveBits"],
  );

  return exportKeypair(pair);
}

/** Generates a throwaway P-384 keypair — the wrong-curve adversarial input. */
export async function generateFixtureP384Keypair(): Promise<FixtureKeypair> {
  const pair = await webcrypto.subtle.generateKey(
    { name: "ECDH", namedCurve: "P-384" },
    true,
    ["deriveBits"],
  );

  return exportKeypair(pair);
}

/** Builds a single-kid recipient public keyring from a fixture keypair. */
export function fixturePublicKeyring(
  serviceId: string,
  kid: string,
  keypair: FixtureKeypair,
): Promise<RecipientPublicKeyring> {
  return RecipientPublicKeyring.create(
    serviceId,
    kid,
    new Map([[kid, keypair.publicSpki]]),
  );
}

/** Builds a single-kid recipient private keyring from a fixture keypair. */
export function fixturePrivateKeyring(
  serviceId: string,
  kid: string,
  keypair: FixtureKeypair,
): Promise<RecipientPrivateKeyring> {
  return RecipientPrivateKeyring.create(
    serviceId,
    new Map([[kid, keypair.privatePkcs8]]),
  );
}

/** Builds a symmetric keyring with a single random 32-byte key. */
export function fixtureSymmetricKeyring(
  kid: string,
  aadContext: Uint8Array,
): PayloadCryptoKeyring {
  const key = webcrypto.getRandomValues(new Uint8Array(32));

  return new PayloadCryptoKeyring(kid, new Map([[kid, key]]), aadContext);
}
