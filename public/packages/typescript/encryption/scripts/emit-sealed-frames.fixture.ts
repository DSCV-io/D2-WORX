// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

// TS → .NET cross-runtime fixture emitter (test/fixture-only, `.fixture.ts`).
// Produces sealed (v2) frames with @dcsv-io/d2-encryption's PayloadSealer so the .NET
// TsCryptoInterop suite can open them with the REAL PayloadOpener and prove the
// TS encoder is byte-compatible with the .NET decoder. Regenerate via
// `pnpm --filter @dcsv-io/d2-encryption emit-crypto-fixtures`.
//
// The emitted frames are NON-deterministic by design: this script mints a fresh
// recipient keypair, and the sealer mints a fresh per-message ephemeral keypair
// + nonce (no injection point), so every emit produces a different frameHex.
// That is expected — the proof is functional (the .NET suite opens the frames
// and compares plaintext); the byte-exact gate is delegated to the deterministic
// `sealed-crypto-kat` known-answer fixture, not this manifest.

import { webcrypto } from "node:crypto";
import { mkdirSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

import { PayloadSealer } from "../src/payload-sealer.js";
import { RecipientPublicKeyring } from "../src/recipient-public-keyring.js";

const SCRIPT_DIR = dirname(fileURLToPath(import.meta.url));
const REPO_ROOT = join(SCRIPT_DIR, "..", "..", "..", "..", "..");
const OUT_DIR = join(
  REPO_ROOT,
  "public/packages/dotnet/tests/Integration/Encryption/TsCryptoInterop/fixtures",
);
const OUT_FILE = "sealed-frames.manifest.fixture.json";

const RECIPIENT_SERVICE_ID = "audit";
const RECIPIENT_KID = "ts-seal-kid";
const PLAINTEXTS = [
  "ts-sealed-known-answer",
  "a second sealed payload — longer, with punctuation & digits 0123456789",
];

async function main(): Promise<void> {
  const pair = await webcrypto.subtle.generateKey(
    { name: "ECDH", namedCurve: "P-256" },
    true,
    ["deriveBits"],
  );
  const publicSpki = new Uint8Array(
    await webcrypto.subtle.exportKey("spki", pair.publicKey),
  );
  const privatePkcs8 = new Uint8Array(
    await webcrypto.subtle.exportKey("pkcs8", pair.privateKey),
  );

  const keyring = await RecipientPublicKeyring.create(
    RECIPIENT_SERVICE_ID,
    RECIPIENT_KID,
    new Map([[RECIPIENT_KID, publicSpki]]),
  );
  const sealer = new PayloadSealer(keyring);

  const frames: { plaintextUtf8: string; frameHex: string }[] = [];
  for (const plaintextUtf8 of PLAINTEXTS) {
    const framed = await sealer.seal(new TextEncoder().encode(plaintextUtf8));
    frames.push({
      plaintextUtf8,
      frameHex: Buffer.from(framed).toString("hex").toUpperCase(),
    });
  }

  const manifest = {
    $comment:
      "Synthetic fixture-only key material; no real PII. TS-produced sealed " +
      "frames opened by the .NET TsCryptoInterop suite.",
    emittedBy:
      "public/packages/typescript/encryption/scripts/emit-sealed-frames.fixture.ts",
    regenerate: "pnpm --filter @dcsv-io/d2-encryption emit-crypto-fixtures",
    recipientServiceId: RECIPIENT_SERVICE_ID,
    recipientKid: RECIPIENT_KID,
    recipientPrivatePkcs8Base64: Buffer.from(privatePkcs8).toString("base64"),
    frames,
  };

  mkdirSync(OUT_DIR, { recursive: true });
  writeFileSync(
    join(OUT_DIR, OUT_FILE),
    JSON.stringify(manifest, null, 2) + "\n",
  );
  console.log(`  ${OUT_FILE} (${frames.length} sealed frames)`);
}

await main();
