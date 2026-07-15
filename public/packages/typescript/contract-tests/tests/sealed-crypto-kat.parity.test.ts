// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import { PayloadOpener, RecipientPrivateKeyring } from "@d2/encryption";
// Internal derivation + codec — a KAT parity gate reproduces every stage so a
// cross-runtime drift localizes to the failing stage (info / shared-secret /
// DEK / frame). These are package-internal (not on the public barrel); a KAT
// harness deliberately reaches them via the built dist by relative path.
import {
  buildInfo,
  deriveDek,
  serviceIdBytes,
} from "../../encryption/dist/sealed-key-derivation.js";
import { encodeSealedFrame } from "../../encryption/dist/sealed-frame.js";
import {
  deriveRawSecret,
  importPrivateP256,
  importPublicP256,
} from "../../encryption/dist/ecdh-p256.js";
import { aesGcmEncrypt } from "../../encryption/dist/subtle.js";
import { loadFixture } from "../src/index.js";

interface SealedKat {
  readonly serviceId: string;
  readonly recipientKid: string;
  readonly plaintextUtf8: string;
  readonly recipientPrivatePkcs8Base64: string;
  readonly ephemeralPublicSpkiBase64: string;
  readonly nonceHex: string;
  readonly infoHex: string;
  readonly sharedSecretHex: string;
  readonly dekHex: string;
  readonly frameHex: string;
}

const hex = (u8: Uint8Array): string =>
  Buffer.from(u8).toString("hex").toUpperCase();
const fromB64 = (b64: string): Uint8Array =>
  new Uint8Array(Buffer.from(b64, "base64"));
const fromHex = (h: string): Uint8Array =>
  new Uint8Array(Buffer.from(h, "hex"));

describe("sealed-crypto-kat parity (.NET PayloadSealer ↔ TS @d2/encryption)", () => {
  const kat = loadFixture<SealedKat>("sealed-crypto-kat", "known-answer").data;
  const sid = serviceIdBytes(kat.serviceId);
  const ephSpki = fromB64(kat.ephemeralPublicSpkiBase64);
  const recipientPkcs8 = fromB64(kat.recipientPrivatePkcs8Base64);
  const nonce = fromHex(kat.nonceHex);
  const plaintext = new TextEncoder().encode(kat.plaintextUtf8);

  it("reproduces the frozen HKDF info bytes", () => {
    expect(hex(buildInfo(sid, ephSpki))).toBe(kat.infoHex);
  });

  it("reproduces the raw ECDH shared secret", async () => {
    const priv = await importPrivateP256(recipientPkcs8);
    const pub = await importPublicP256(ephSpki);
    expect(hex(await deriveRawSecret(priv, pub))).toBe(kat.sharedSecretHex);
  });

  it("reproduces the derived content-encryption key (HKDF-SHA256)", async () => {
    expect(
      hex(await deriveDek(fromHex(kat.sharedSecretHex), sid, ephSpki)),
    ).toBe(kat.dekHex);
  });

  it("reproduces the sealed frame byte-for-byte", async () => {
    const ctWithTag = await aesGcmEncrypt(
      fromHex(kat.dekHex),
      nonce,
      plaintext,
      sid,
    );
    expect(
      hex(encodeSealedFrame(kat.recipientKid, ephSpki, nonce, ctWithTag)),
    ).toBe(kat.frameHex);
  });

  it("opens the .NET-produced frame via the production opener path", async () => {
    const ring = await RecipientPrivateKeyring.create(
      kat.serviceId,
      new Map([[kat.recipientKid, recipientPkcs8]]),
    );
    const opened = await new PayloadOpener(ring).open(fromHex(kat.frameHex));
    expect(new TextDecoder().decode(opened)).toBe(kat.plaintextUtf8);
  });
});
