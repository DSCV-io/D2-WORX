// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";

import {
  AuthenticationTagMismatchError,
  FrameMalformedError,
  FrameVersionMismatchError,
  KidNotInKeyringError,
} from "../src/errors.js";
import { encodeFrame } from "../src/frame.js";
import { PayloadOpener } from "../src/payload-opener.js";
import { PayloadSealer } from "../src/payload-sealer.js";
import { RecipientPrivateKeyring } from "../src/recipient-private-keyring.js";
import { RecipientPublicKeyring } from "../src/recipient-public-keyring.js";
import {
  DEK_SIZE_BYTES,
  INFO_LABEL,
  buildInfo,
  serviceIdBytes,
} from "../src/sealed-key-derivation.js";
import { decodeSealedFrame, encodeSealedFrame } from "../src/sealed-frame.js";
import {
  fixturePrivateKeyring,
  fixturePublicKeyring,
  generateFixtureP256Keypair,
  generateFixtureP384Keypair,
} from "./sealed-fixture-keys.js";

const utf8 = new TextEncoder();
const nonce12 = new Uint8Array(12);
const ctWithTag20 = new Uint8Array(20);

describe("RecipientPublicKeyring.create", () => {
  it("rejects invalid service ids and kids", async () => {
    const kp = await generateFixtureP256Keypair();
    const keys = new Map([["seal-1", kp.publicSpki]]);

    await expect(
      RecipientPublicKeyring.create("", "seal-1", keys),
    ).rejects.toThrow(/non-empty/);
    await expect(
      RecipientPublicKeyring.create("a".repeat(65), "seal-1", keys),
    ).rejects.toThrow(/at most 64/);
    await expect(
      RecipientPublicKeyring.create("Audit", "seal-1", keys),
    ).rejects.toThrow(/grammar/);
    await expect(
      RecipientPublicKeyring.create("audit", "", keys),
    ).rejects.toThrow(/UTF-8 byte length/);
    await expect(
      RecipientPublicKeyring.create(
        "audit",
        "k".repeat(65),
        new Map([["k".repeat(65), kp.publicSpki]]),
      ),
    ).rejects.toThrow(/UTF-8 byte length/);
    await expect(
      RecipientPublicKeyring.create("audit", "missing", keys),
    ).rejects.toThrow(/not present/);
  });

  it("rejects out-of-range and invalid key material", async () => {
    const kp = await generateFixtureP256Keypair();
    const p384 = await generateFixtureP384Keypair();

    await expect(
      RecipientPublicKeyring.create(
        "audit",
        "seal-1",
        new Map([
          ["seal-1", kp.publicSpki],
          ["seal-2", new Uint8Array(0)],
        ]),
      ),
    ).rejects.toThrow(/must be in \[1,/);

    await expect(
      RecipientPublicKeyring.create(
        "audit",
        "seal-1",
        new Map([
          ["seal-1", kp.publicSpki],
          ["seal-2", new Uint8Array(257).fill(1)],
        ]),
      ),
    ).rejects.toThrow(/must be in \[1,/);

    // Wrong curve (P-384 SPKI) fails the P-256 import.
    await expect(
      RecipientPublicKeyring.create(
        "audit",
        "seal-1",
        new Map([["seal-1", p384.publicSpki]]),
      ),
    ).rejects.toThrow(/not a valid P-256/);

    // Structural garbage of a plausible length also fails.
    await expect(
      RecipientPublicKeyring.create(
        "audit",
        "seal-1",
        new Map([["seal-1", new Uint8Array(91).fill(0x04)]]),
      ),
    ).rejects.toThrow(/not a valid P-256/);
  });

  it("builds a validated keyring and resolves keys", async () => {
    const kp = await generateFixtureP256Keypair();
    const ring = await fixturePublicKeyring("audit", "seal-1", kp);

    expect(ring.recipientServiceId).toBe("audit");
    expect(ring.activeKid).toBe("seal-1");
    expect(ring.tryGetPublicKey("seal-1")).toBeDefined();
    expect(ring.tryGetPublicKey("nope")).toBeUndefined();
    expect(ring.tryGetPublicKey(undefined)).toBeUndefined();
    expect(ring.toString()).toContain("recipientServiceId=audit");
  });
});

describe("RecipientPrivateKeyring.create", () => {
  it("rejects invalid ids, empty maps, invalid kids, and non-private material", async () => {
    const kp = await generateFixtureP256Keypair();

    await expect(
      RecipientPrivateKeyring.create(
        "Audit",
        new Map([["seal-1", kp.privatePkcs8]]),
      ),
    ).rejects.toThrow(/grammar/);

    await expect(
      RecipientPrivateKeyring.create("audit", new Map()),
    ).rejects.toThrow(/at least one key/);

    await expect(
      RecipientPrivateKeyring.create("audit", new Map([["", kp.privatePkcs8]])),
    ).rejects.toThrow(/UTF-8 byte length/);

    // A public-only SPKI is rejected by the PKCS#8 import.
    await expect(
      RecipientPrivateKeyring.create(
        "audit",
        new Map([["seal-1", kp.publicSpki]]),
      ),
    ).rejects.toThrow(/not a valid P-256/);
  });

  it("builds a keyring, resolves keys, and disposes fail-loud + idempotent", async () => {
    const kp = await generateFixtureP256Keypair();
    const ring = await fixturePrivateKeyring("audit", "seal-1", kp);

    expect(ring.recipientServiceId).toBe("audit");
    expect(ring.tryGetPrivateKey("seal-1")).toBeDefined();
    expect(ring.tryGetPrivateKey("nope")).toBeUndefined();
    expect(ring.tryGetPrivateKey(undefined)).toBeUndefined();
    expect(ring.toString()).toContain("recipientServiceId=audit");

    ring.dispose();
    ring.dispose();
    expect(ring.toString()).toBe("RecipientPrivateKeyring(disposed)");
    expect(() => ring.tryGetPrivateKey("seal-1")).toThrow(/disposed/);
  });
});

describe("PayloadSealer + PayloadOpener", () => {
  it("round-trips a plaintext seal → open", async () => {
    const kp = await generateFixtureP256Keypair();
    const sealer = new PayloadSealer(
      await fixturePublicKeyring("audit", "seal-1", kp),
    );
    const opener = new PayloadOpener(
      await fixturePrivateKeyring("audit", "seal-1", kp),
    );
    const plaintext = utf8.encode("sealed round-trip payload");

    const framed = await sealer.seal(plaintext);
    expect(framed[0]).toBe(2);
    expect(decodeSealedFrame(framed).recipientKid).toBe("seal-1");
    expect(new Uint8Array(await opener.open(framed))).toEqual(plaintext);

    expect(sealer.toString()).toBe("PayloadSealer");
    expect(opener.toString()).toBe("PayloadOpener");
  });

  it("uses a fresh ephemeral per seal (frames differ)", async () => {
    const kp = await generateFixtureP256Keypair();
    const sealer = new PayloadSealer(
      await fixturePublicKeyring("audit", "seal-1", kp),
    );
    const a = await sealer.seal(utf8.encode("same"));
    const b = await sealer.seal(utf8.encode("same"));

    expect([...a]).not.toEqual([...b]);
  });

  it("opens active AND retiring kids (rotation overlap)", async () => {
    const active = await generateFixtureP256Keypair();
    const retiring = await generateFixtureP256Keypair();
    const privateRing = await RecipientPrivateKeyring.create(
      "audit",
      new Map([
        ["seal-active", active.privatePkcs8],
        ["seal-retiring", retiring.privatePkcs8],
      ]),
    );
    const opener = new PayloadOpener(privateRing);

    const sealActive = new PayloadSealer(
      await fixturePublicKeyring("audit", "seal-active", active),
    );
    const sealRetiring = new PayloadSealer(
      await fixturePublicKeyring("audit", "seal-retiring", retiring),
    );

    expect(
      new Uint8Array(
        await opener.open(await sealActive.seal(utf8.encode("A"))),
      ),
    ).toEqual(utf8.encode("A"));
    expect(
      new Uint8Array(
        await opener.open(await sealRetiring.seal(utf8.encode("R"))),
      ),
    ).toEqual(utf8.encode("R"));
  });

  it("fails a tampered ciphertext, nonce, or eph_pub with a tag mismatch", async () => {
    const kp = await generateFixtureP256Keypair();
    const sealer = new PayloadSealer(
      await fixturePublicKeyring("audit", "seal-1", kp),
    );
    const opener = new PayloadOpener(
      await fixturePrivateKeyring("audit", "seal-1", kp),
    );

    const framed = await sealer.seal(utf8.encode("tamper"));
    const tampered = Uint8Array.from(framed);
    const last = tampered.length - 1;
    tampered[last] = tampered[last]! ^ 0xff;

    await expect(opener.open(tampered)).rejects.toBeInstanceOf(
      AuthenticationTagMismatchError,
    );
  });

  it("fails a wrong-recipient frame (AAD mismatch)", async () => {
    const kp = await generateFixtureP256Keypair();
    const sealer = new PayloadSealer(
      await fixturePublicKeyring("audit", "seal-1", kp),
    );
    // Same key material, but the opener claims a different service id — the
    // service-bound salt/info/AAD no longer authenticate.
    const opener = new PayloadOpener(
      await fixturePrivateKeyring("courier", "seal-1", kp),
    );

    await expect(
      opener.open(await sealer.seal(utf8.encode("x"))),
    ).rejects.toBeInstanceOf(AuthenticationTagMismatchError);
  });

  it("rejects a frame whose recipient kid is unknown", async () => {
    const kp = await generateFixtureP256Keypair();
    const opener = new PayloadOpener(
      await fixturePrivateKeyring("audit", "seal-1", kp),
    );
    const frame = encodeSealedFrame(
      "unknown-kid",
      kp.publicSpki,
      nonce12,
      ctWithTag20,
    );

    await expect(opener.open(frame)).rejects.toBeInstanceOf(
      KidNotInKeyringError,
    );
  });

  it("rejects a frame carrying a non-P-256 ephemeral public key", async () => {
    const kp = await generateFixtureP256Keypair();
    const opener = new PayloadOpener(
      await fixturePrivateKeyring("audit", "seal-1", kp),
    );
    const frame = encodeSealedFrame(
      "seal-1",
      new Uint8Array(91).fill(0x04),
      nonce12,
      ctWithTag20,
    );

    await expect(opener.open(frame)).rejects.toBeInstanceOf(
      FrameMalformedError,
    );
  });

  it("rejects a version-1 frame passed to the opener", async () => {
    const kp = await generateFixtureP256Keypair();
    const opener = new PayloadOpener(
      await fixturePrivateKeyring("audit", "seal-1", kp),
    );
    const v1 = encodeFrame("k1", nonce12, new Uint8Array(24));

    await expect(opener.open(v1)).rejects.toBeInstanceOf(
      FrameVersionMismatchError,
    );
  });
});

describe("sealed key derivation", () => {
  it("exposes the frozen label and DEK size", () => {
    expect(INFO_LABEL).toBe("d2-seal-v1");
    expect(DEK_SIZE_BYTES).toBe(32);
  });

  it("encodes serviceId bytes as UTF-8", () => {
    expect([...serviceIdBytes("audit")]).toEqual([...utf8.encode("audit")]);
  });

  it("builds the length-delimited info exactly", async () => {
    const kp = await generateFixtureP256Keypair();
    const sid = utf8.encode("audit");
    const info = buildInfo(sid, kp.publicSpki);

    const expected = [
      ...utf8.encode("d2-seal-v1"),
      (sid.length >> 8) & 0xff,
      sid.length & 0xff,
      ...sid,
      (kp.publicSpki.length >> 8) & 0xff,
      kp.publicSpki.length & 0xff,
      ...kp.publicSpki,
    ];

    expect([...info]).toEqual(expected);
  });
});
