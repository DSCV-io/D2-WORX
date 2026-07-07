// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";

import {
  AuthenticationTagMismatchError,
  FrameVersionMismatchError,
  KidNotInKeyringError,
} from "../src/errors.js";
import { PayloadCrypto } from "../src/payload-crypto.js";
import { PayloadCryptoKeyring } from "../src/payload-crypto-keyring.js";
import { encodeSealedFrame } from "../src/sealed-frame.js";

const utf8 = new TextEncoder();
const key32 = (fill: number): Uint8Array => new Uint8Array(32).fill(fill);
const aad = utf8.encode("d2/audit");

describe("PayloadCryptoKeyring", () => {
  it("rejects invalid construction arguments", () => {
    expect(
      () => new PayloadCryptoKeyring("", new Map([["", key32(1)]]), aad),
    ).toThrow(/activeKid UTF-8 byte length/);

    expect(
      () =>
        new PayloadCryptoKeyring(
          "k".repeat(65),
          new Map([["k".repeat(65), key32(1)]]),
          aad,
        ),
    ).toThrow(/activeKid UTF-8 byte length/);

    expect(
      () =>
        new PayloadCryptoKeyring(
          "k1",
          new Map([["k1", key32(1)]]),
          new Uint8Array(0),
        ),
    ).toThrow(/aadContext must be non-empty/);

    expect(
      () => new PayloadCryptoKeyring("k1", new Map([["k2", key32(1)]]), aad),
    ).toThrow(/not present in keys/);

    expect(
      () =>
        new PayloadCryptoKeyring(
          "k1",
          new Map([
            ["k1", key32(1)],
            ["", key32(2)],
          ]),
          aad,
        ),
    ).toThrow(/UTF-8 byte length/);

    expect(
      () =>
        new PayloadCryptoKeyring(
          "k1",
          new Map([["k1", new Uint8Array(31)]]),
          aad,
        ),
    ).toThrow(/must be exactly 32 bytes/);
  });

  it("exposes activeKid, allKids, aadContext and resolves keys defensively", () => {
    const ring = new PayloadCryptoKeyring(
      "k1",
      new Map([
        ["k1", key32(1)],
        ["k0", key32(2)],
      ]),
      aad,
    );

    expect(ring.activeKid).toBe("k1");
    expect([...ring.allKids].sort()).toEqual(["k0", "k1"]);
    expect([...ring.aadContext]).toEqual([...aad]);
    expect(ring.tryGetKey("k0")).toBeDefined();
    expect(ring.tryGetKey("missing")).toBeUndefined();
    expect(ring.tryGetKey(undefined)).toBeUndefined();
    expect(ring.toString()).toContain("activeKid=k1");
  });

  it("copies key bytes defensively (mutating the input does not affect it)", () => {
    const original = key32(7);
    const ring = new PayloadCryptoKeyring(
      "k1",
      new Map([["k1", original]]),
      aad,
    );
    original.fill(0);

    expect(ring.tryGetKey("k1")?.every((b) => b === 7)).toBe(true);
  });

  it("disposes idempotently and fails-loud on post-dispose access", () => {
    const ring = new PayloadCryptoKeyring(
      "k1",
      new Map([["k1", key32(1)]]),
      aad,
    );
    ring.dispose();
    ring.dispose();

    expect(ring.toString()).toBe("PayloadCryptoKeyring(disposed)");
    expect(() => ring.aadContext).toThrow(/disposed/);
    expect(() => ring.allKids).toThrow(/disposed/);
    expect(() => ring.tryGetKey("k1")).toThrow(/disposed/);
  });
});

describe("PayloadCrypto", () => {
  const ring = (): PayloadCryptoKeyring =>
    new PayloadCryptoKeyring("k1", new Map([["k1", key32(3)]]), aad);

  it("round-trips a plaintext through encrypt + decrypt", async () => {
    const crypto = new PayloadCrypto(ring());
    const plaintext = utf8.encode("symmetric round-trip");
    const frame = await crypto.encrypt(plaintext);

    expect(frame[0]).toBe(1);
    expect(new Uint8Array(await crypto.decrypt(frame))).toEqual(plaintext);
    expect(crypto.toString()).toBe("PayloadCrypto");
  });

  it("uses a fresh nonce per encrypt (ciphertexts differ)", async () => {
    const crypto = new PayloadCrypto(ring());
    const pt = utf8.encode("same input");
    const a = await crypto.encrypt(pt);
    const b = await crypto.encrypt(pt);

    expect([...a]).not.toEqual([...b]);
  });

  it("fails a tampered frame with an authentication-tag mismatch", async () => {
    const crypto = new PayloadCrypto(ring());
    const frame = await crypto.encrypt(utf8.encode("tamper me"));
    const last = frame.length - 1;
    frame[last] = frame[last]! ^ 0xff;

    await expect(crypto.decrypt(frame)).rejects.toBeInstanceOf(
      AuthenticationTagMismatchError,
    );
  });

  it("rejects a frame whose kid is not in the keyring", async () => {
    const cryptoA = new PayloadCrypto(
      new PayloadCryptoKeyring("ka", new Map([["ka", key32(3)]]), aad),
    );
    const cryptoB = new PayloadCrypto(
      new PayloadCryptoKeyring("kb", new Map([["kb", key32(4)]]), aad),
    );
    const frame = await cryptoA.encrypt(utf8.encode("hi"));

    await expect(cryptoB.decrypt(frame)).rejects.toBeInstanceOf(
      KidNotInKeyringError,
    );
  });

  it("rejects a version-2 sealed frame passed to the symmetric decoder", async () => {
    const crypto = new PayloadCrypto(ring());
    const sealedFrame = encodeSealedFrame(
      "rk",
      new Uint8Array(91).fill(0x04),
      new Uint8Array(12),
      new Uint8Array(20),
    );

    await expect(crypto.decrypt(sealedFrame)).rejects.toBeInstanceOf(
      FrameVersionMismatchError,
    );
  });

  it("binds the AAD context — a wrong AAD fails authentication", async () => {
    const shared = key32(9);
    const encryptor = new PayloadCrypto(
      new PayloadCryptoKeyring(
        "k1",
        new Map([["k1", shared]]),
        utf8.encode("aad-1"),
      ),
    );
    const decryptor = new PayloadCrypto(
      new PayloadCryptoKeyring(
        "k1",
        new Map([["k1", shared]]),
        utf8.encode("aad-2"),
      ),
    );
    const frame = await encryptor.encrypt(utf8.encode("bound"));

    await expect(decryptor.decrypt(frame)).rejects.toBeInstanceOf(
      AuthenticationTagMismatchError,
    );
  });
});
