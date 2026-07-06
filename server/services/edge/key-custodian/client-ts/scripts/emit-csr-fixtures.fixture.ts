// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// CSR fixture emitter — the file-based cross-runtime gate's TS half. Emits
// COMMITTED PKCS#10 CSR fixtures (one valid + the adversarial matrix) plus a
// manifest naming each fixture's expected verdict, into the .NET harness folder
// (server/services/edge/tests/Integration/KeyCustodian/NodeLeafClient/fixtures/).
// The .NET tests there load these fixtures and drive the REAL KeyCustodian
// CsrVerification + issuance rules against them — TS-generated requests proven
// by the production .NET validator.
//
// Regeneration: `pnpm --filter @d2/key-custodian-client emit-csr-fixtures`.
// ECDSA signatures are randomized, so re-running produces new bytes with the
// SAME verdicts — commit the refreshed set together with this script when the
// CSR construction changes.
//
// Everything emitted is PUBLIC material (public keys + self-signatures); the
// throwaway private keys never leave this process and are discarded on exit.

import "reflect-metadata";
import { mkdirSync, writeFileSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import {
  Pkcs10CertificateRequestGenerator,
  SubjectAlternativeNameExtension,
  cryptoProvider,
} from "@peculiar/x509";
import { buildCsr, CSR_SUBJECT } from "../src/csr-builder.js";
import { generateLeafKeypair } from "../src/leaf-keypair.js";

cryptoProvider.set(globalThis.crypto);

const SCRIPT_DIR = dirname(fileURLToPath(import.meta.url));
// client-ts/scripts → repo root is six levels up.
const REPO_ROOT = resolve(SCRIPT_DIR, "../../../../../..");
const FIXTURES_DIR = join(
  REPO_ROOT,
  "server/services/edge/tests/Integration/KeyCustodian/NodeLeafClient/fixtures",
);

const SIGN_ALG: EcdsaParams = { name: "ECDSA", hash: "SHA-256" };

interface FixtureEntry {
  readonly file: string;
  readonly expectedVerdict:
    | "issues"
    | "issues-subject-ignored"
    | "rejected-invalid-csr";
  readonly description: string;
}

async function generateKeys(namedCurve: string): Promise<CryptoKeyPair> {
  return globalThis.crypto.subtle.generateKey(
    { name: "ECDSA", namedCurve },
    true,
    ["sign", "verify"],
  );
}

/** The valid CSR — the exact production construction (buildCsr over a fresh P-256 key). */
async function validCsr(): Promise<Uint8Array> {
  const kp = await generateLeafKeypair();

  return buildCsr(kp.cryptoKeyPair);
}

/** A well-formed P-256 CSR whose subject claims ANOTHER service — KC must ignore it. */
async function foreignCnCsr(): Promise<Uint8Array> {
  const keys = await generateKeys("P-256");
  const csr = await Pkcs10CertificateRequestGenerator.create({
    name: "CN=files,O=D2 Imposter",
    keys,
    signingAlgorithm: SIGN_ALG,
  });

  return new Uint8Array(csr.rawData);
}

/** A well-formed CSR over a P-384 key — rejected by the P-256 curve policy. */
async function wrongCurveCsr(): Promise<Uint8Array> {
  const keys = await generateKeys("P-384");
  const csr = await Pkcs10CertificateRequestGenerator.create({
    name: CSR_SUBJECT,
    keys,
    signingAlgorithm: { name: "ECDSA", hash: "SHA-384" },
  });

  return new Uint8Array(csr.rawData);
}

/**
 * A REAL, well-formed P-256 CSR inflated past the 4096-byte DER cap via a large
 * requested SAN extension — rejected by the size bound BEFORE any parse.
 */
async function oversizedCsr(): Promise<Uint8Array> {
  const keys = await generateKeys("P-256");
  const names = Array.from({ length: 160 }, (_, i) => ({
    type: "dns" as const,
    value: `oversized-padding-entry-${String(i).padStart(4, "0")}.fixture.d2.internal`,
  }));
  const csr = await Pkcs10CertificateRequestGenerator.create({
    name: CSR_SUBJECT,
    keys,
    signingAlgorithm: SIGN_ALG,
    extensions: [new SubjectAlternativeNameExtension(names)],
  });

  return new Uint8Array(csr.rawData);
}

/**
 * A valid CSR with ONE flipped byte in its trailing signature bytes — the DER
 * structure still parses, but the proof-of-possession self-signature fails.
 */
async function tamperedPopCsr(): Promise<Uint8Array> {
  const der = await validCsr();
  const tampered = new Uint8Array(der);
  // The signature BIT STRING is the LAST element of a CertificationRequest —
  // flipping the final byte corrupts the signature value, not the structure.
  tampered[tampered.length - 1] = tampered[tampered.length - 1]! ^ 0xff;

  return tampered;
}

async function main(): Promise<void> {
  mkdirSync(FIXTURES_DIR, { recursive: true });

  const entries: FixtureEntry[] = [];

  async function emit(
    file: string,
    expectedVerdict: FixtureEntry["expectedVerdict"],
    description: string,
    produce: () => Promise<Uint8Array>,
  ): Promise<void> {
    const bytes = await produce();
    writeFileSync(join(FIXTURES_DIR, file), bytes);
    entries.push({ file, expectedVerdict, description });
    console.log(`  ${file} (${bytes.byteLength} bytes) → ${expectedVerdict}`);
  }

  await emit(
    "valid-p256.csr.fixture.der",
    "issues",
    "The production CSR construction: fresh P-256 key, CN=d2-workload subject, ECDSA-SHA256 proof-of-possession. Verifies and issues; the leaf SAN is the harness-supplied authenticated identity.",
    validCsr,
  );

  await emit(
    "foreign-cn.csr.fixture.der",
    "issues-subject-ignored",
    "Well-formed P-256 CSR whose subject claims another service (CN=files). KeyCustodian structurally ignores the subject: it verifies, issues, and the leaf SAN is STILL the authenticated identity — impersonation via CSR subject is unrepresentable.",
    foreignCnCsr,
  );

  await emit(
    "wrong-curve-p384.csr.fixture.der",
    "rejected-invalid-csr",
    "Well-formed CSR over a P-384 key. The leaf key policy accepts ONLY the P-256 named curve (by curve OID), so verification rejects it.",
    wrongCurveCsr,
  );

  await emit(
    "oversized.csr.fixture.der",
    "rejected-invalid-csr",
    "Real P-256 CSR inflated past the 4096-byte DER cap by a large requested SAN extension. The size bound rejects it BEFORE any ASN.1 parse.",
    oversizedCsr,
  );

  await emit(
    "tampered-pop.csr.fixture.der",
    "rejected-invalid-csr",
    "Valid CSR with one flipped byte in the trailing signature — the proof-of-possession self-signature fails verification.",
    tamperedPopCsr,
  );

  const manifest = {
    emittedBy:
      "server/services/edge/key-custodian/client-ts/scripts/emit-csr-fixtures.fixture.ts",
    regenerate: "pnpm --filter @d2/key-custodian-client emit-csr-fixtures",
    note: "ECDSA signatures are randomized: regeneration produces new bytes with the same verdicts. The .NET NodeLeafClient tests drive the REAL CsrVerification + issuance rules over these files.",
    fixtures: entries,
  };

  writeFileSync(
    join(FIXTURES_DIR, "csr-fixtures.manifest.fixture.json"),
    JSON.stringify(manifest, null, 2) + "\n",
  );

  console.log(`Emitted ${entries.length} CSR fixtures → ${FIXTURES_DIR}`);
}

await main();
