// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import { PayloadCrypto, PayloadCryptoKeyring } from "@d2/encryption";
// Internal codec — see the sealed KAT parity suite for the rationale on
// reaching package internals from a cross-runtime KAT harness.
import { encodeFrame } from "../../encryption/dist/frame.js";
import { aesGcmEncrypt } from "../../encryption/dist/subtle.js";
import { loadFixture } from "../src/index.js";

interface SymmetricKat {
  readonly kid: string;
  readonly plaintextUtf8: string;
  readonly keyBase64: string;
  readonly aadContextBase64: string;
  readonly nonceHex: string;
  readonly frameHex: string;
}

const hex = (u8: Uint8Array): string =>
  Buffer.from(u8).toString("hex").toUpperCase();
const fromB64 = (b64: string): Uint8Array =>
  new Uint8Array(Buffer.from(b64, "base64"));
const fromHex = (h: string): Uint8Array =>
  new Uint8Array(Buffer.from(h, "hex"));

describe("symmetric-crypto-kat parity (.NET PayloadCrypto ↔ TS @d2/encryption)", () => {
  const kat = loadFixture<SymmetricKat>(
    "symmetric-crypto-kat",
    "known-answer",
  ).data;
  const key = fromB64(kat.keyBase64);
  const aad = fromB64(kat.aadContextBase64);
  const nonce = fromHex(kat.nonceHex);
  const plaintext = new TextEncoder().encode(kat.plaintextUtf8);

  it("reproduces the v1 frame byte-for-byte", async () => {
    const ctWithTag = await aesGcmEncrypt(key, nonce, plaintext, aad);
    expect(hex(encodeFrame(kat.kid, nonce, ctWithTag))).toBe(kat.frameHex);
  });

  it("decrypts the .NET-produced frame via the production crypto path", async () => {
    const ring = new PayloadCryptoKeyring(
      kat.kid,
      new Map([[kat.kid, key]]),
      aad,
    );
    const opened = await new PayloadCrypto(ring).decrypt(fromHex(kat.frameHex));
    expect(new TextDecoder().decode(opened)).toBe(kat.plaintextUtf8);
  });
});
