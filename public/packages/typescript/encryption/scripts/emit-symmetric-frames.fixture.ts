// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

// TS → .NET cross-runtime fixture emitter (test/fixture-only, `.fixture.ts`).
// Produces symmetric (v1) frames with @dcsv-io/d2-encryption's PayloadCrypto so the
// .NET TsCryptoInterop suite can decrypt them with the REAL PayloadCrypto and
// prove the TS encoder is byte-compatible with the .NET decoder. Regenerate via
// `pnpm --filter @dcsv-io/d2-encryption emit-crypto-fixtures`.
//
// The emitted frames are NON-deterministic by design: this script mints a fresh
// random key + GCM nonce per emit, so every emit produces a different frameHex.
// That is expected — the proof is functional (the .NET suite decrypts the frames
// and compares plaintext); the byte-exact gate is delegated to the deterministic
// `symmetric-crypto-kat` known-answer fixture, not this manifest.

import { webcrypto } from "node:crypto";
import { mkdirSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

import { PayloadCrypto } from "../src/payload-crypto.js";
import { PayloadCryptoKeyring } from "../src/payload-crypto-keyring.js";

const SCRIPT_DIR = dirname(fileURLToPath(import.meta.url));
const REPO_ROOT = join(SCRIPT_DIR, "..", "..", "..", "..", "..");
const OUT_DIR = join(
  REPO_ROOT,
  "public/packages/dotnet/tests/Integration/Encryption/TsCryptoInterop/fixtures",
);
const OUT_FILE = "symmetric-frames.manifest.fixture.json";

const KID = "ts-sym-kid";
const PLAINTEXTS = [
  "ts-symmetric-known-answer",
  "a second symmetric payload — longer, with punctuation & digits 0123456789",
];

async function main(): Promise<void> {
  const key = webcrypto.getRandomValues(new Uint8Array(32));
  const aadContext = new TextEncoder().encode("d2/audit-payload");
  const keyring = new PayloadCryptoKeyring(
    KID,
    new Map([[KID, key]]),
    aadContext,
  );
  const crypto = new PayloadCrypto(keyring);

  const frames: { plaintextUtf8: string; frameHex: string }[] = [];
  for (const plaintextUtf8 of PLAINTEXTS) {
    const framed = await crypto.encrypt(
      new TextEncoder().encode(plaintextUtf8),
    );
    frames.push({
      plaintextUtf8,
      frameHex: Buffer.from(framed).toString("hex").toUpperCase(),
    });
  }

  const manifest = {
    $comment:
      "Synthetic fixture-only key material; no real PII. TS-produced symmetric " +
      "frames decrypted by the .NET TsCryptoInterop suite.",
    emittedBy:
      "public/packages/typescript/encryption/scripts/emit-symmetric-frames.fixture.ts",
    regenerate: "pnpm --filter @dcsv-io/d2-encryption emit-crypto-fixtures",
    kid: KID,
    keyBase64: Buffer.from(key).toString("base64"),
    aadContextBase64: Buffer.from(aadContext).toString("base64"),
    frames,
  };

  mkdirSync(OUT_DIR, { recursive: true });
  writeFileSync(
    join(OUT_DIR, OUT_FILE),
    JSON.stringify(manifest, null, 2) + "\n",
  );
  console.log(`  ${OUT_FILE} (${frames.length} symmetric frames)`);
}

await main();
