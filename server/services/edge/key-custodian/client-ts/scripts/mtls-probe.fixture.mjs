// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// mTLS loopback probe — the Node half of the live-handshake harness
// (the .NET NodeLeafClientMutualTlsHarnessTests spawns this). Runs against the
// BUILT @d2/key-custodian-client dist (`pnpm --filter @d2/key-custodian-client
// build` first), so the exact production runtime is what dials the wire.
//
// Modes:
//   client-flow  — the FULL production client path: fresh P-256 keypair → PKCS#10
//                  CSR → REAL gRPC issuance (the emitted TS client over a
//                  proto-loader stub) at the KC endpoint → mismatch defense →
//                  CA-chain fetch → mutual-TLS credentials → dial the
//                  mTLS-REQUIRED endpoint presenting the issued leaf.
//   present-pem  — present a harness-supplied leaf/key/chain PEM set at the
//                  mTLS endpoint (drives the adversarial reject matrix).
//   no-cert      — dial the mTLS endpoint with NO client certificate.
//   get-keyring  — present a harness-supplied leaf/key/chain PEM set at the
//                  mTLS-REQUIRED KeyCustodian keyring endpoint, run the shipped
//                  GrpcKeyringClient (over the emitted gRPC facade) to fetch the
//                  domain's keyring, then decrypt a .NET-produced frame with the
//                  shipped @d2/encryption PayloadCrypto (cross-runtime pin).
//
// The probe TRUSTS the harness's self-signed loopback server certificate via the
// explicitly-passed PEM (never rejectUnauthorized:false). It writes a JSON result
// file the .NET test asserts on and exits 0 whenever a result was produced (the
// VERDICT lives in the result); a crash before the result is written exits 1.
//
// Args (positional):
//   <mode> <resultPath> <serverCertPemPath> <mtlsTarget host:port>
//   [kcTarget host:port]                      (client-flow)
//   [leafPemPath] [keyPemPath] [extraChainPemPath]  (present-pem)
//   [leafPemPath] [keyPemPath] [chainPemPath] [keyDomain] [frameBase64]  (get-keyring)

import "reflect-metadata";
import { readFileSync, writeFileSync } from "node:fs";
import { createHash } from "node:crypto";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import grpc from "@grpc/grpc-js";
import protoLoader from "@grpc/proto-loader";
import { PayloadCrypto } from "@d2/encryption";
import {
  WorkloadLeafClient,
  GrpcWorkloadCertificateIssuer,
  createKeyCustodianGrpcClient,
  buildMutualTlsCredentials,
  GrpcKeyringClient,
} from "../dist/index.js";

const SCRIPT_DIR = dirname(fileURLToPath(import.meta.url));
const REPO_ROOT = resolve(SCRIPT_DIR, "../../../../../..");
/** Production KC protos (Edge.Api home) — sign/issue/cacert/keyring/seal. */
const KC_PROTOS_DIR = join(
  REPO_ROOT,
  "server/services/edge/api/Protos/KeyCustodian",
);
/** Fixture protos (SignFixture*) stay under the tests tree. */
const FIXTURE_PROTOS_DIR = join(
  REPO_ROOT,
  "server/services/edge/tests/Unit/KeyCustodian/TypeSpecGrpc/Protos",
);
const COMMON_PROTOS_DIR = join(REPO_ROOT, "contracts/protos");

const LOADER_OPTIONS = {
  keepCase: false,
  defaults: true,
  longs: String,
  includeDirs: [COMMON_PROTOS_DIR, KC_PROTOS_DIR, FIXTURE_PROTOS_DIR],
};

const [mode, resultPath, serverCertPemPath, mtlsTarget, ...rest] =
  process.argv.slice(2);

function loadService(
  protoFile,
  packagePath,
  serviceName,
  protosDir = KC_PROTOS_DIR,
) {
  const def = protoLoader.loadSync(join(protosDir, protoFile), LOADER_OPTIONS);
  const pkg = grpc.loadPackageDefinition(def);
  let node = pkg;
  for (const segment of packagePath.split(".")) node = node[segment];
  return node[serviceName];
}

function sha256Hex(bytes) {
  return createHash("sha256").update(bytes).digest("hex");
}

/** One SignFixture business call at the mTLS endpoint with the given credentials. */
function callSignFixture(credentials) {
  const SignFixtureSigner = loadService(
    "sign_fixture_signer_sign_fixture.g.proto",
    "d2.signfixtures.v2alpha",
    "SignFixtureSigner",
    FIXTURE_PROTOS_DIR,
  );
  const client = new SignFixtureSigner(mtlsTarget, credentials);

  return new Promise((resolveCall) => {
    client.signFixture(
      { kid: "probe", payload: Buffer.from([0x01]) },
      { deadline: Date.now() + 20_000 },
      (error, response) => {
        client.close();
        if (error) {
          resolveCall({
            callSucceeded: false,
            grpcCode: error.code,
          });
          return;
        }
        resolveCall({
          callSucceeded: true,
          resultSuccess: response.result?.success === true,
          signature: response.data?.signature,
        });
      },
    );
  });
}

async function runClientFlow(kcTarget, serverCertPem) {
  // The issuance channel is server-TLS only (trusting the harness server cert);
  // the workload has no identity yet — that is the point of issuance.
  const issuanceCreds = grpc.credentials.createSsl(Buffer.from(serverCertPem));

  const CertificateAuthority = loadService(
    "key_custodian_certificate_authority_issue_workload_certificate.g.proto",
    "d2.keycustodian.v2alpha",
    "KeyCustodianCertificateAuthority",
  );
  const CaCertificate = loadService(
    "key_custodian_ca_certificate_get_ca_certificate.g.proto",
    "d2.keycustodian.v2alpha",
    "KeyCustodianCaCertificate",
  );

  const certAuthClient = new CertificateAuthority(kcTarget, issuanceCreds);
  const caCertClient = new CaCertificate(kcTarget, issuanceCreds);

  // The composed stub exposes exactly the methods the EMITTED TS gRPC client
  // binds (stub.issueWorkloadCertificate / stub.getCaCertificate).
  const stub = {
    issueWorkloadCertificate:
      certAuthClient.issueWorkloadCertificate.bind(certAuthClient),
    getCaCertificate: caCertClient.getCaCertificate.bind(caCertClient),
  };

  const issuer = new GrpcWorkloadCertificateIssuer(
    createKeyCustodianGrpcClient(stub),
  );
  const leafClient = new WorkloadLeafClient(issuer);

  // The full production path: keypair → CSR → wire issuance → mismatch defense.
  const leaf = await leafClient.getCurrentLeaf();

  if (leaf.failed) {
    return {
      stage: "issuance",
      issuanceSucceeded: false,
      statusCode: leaf.statusCode,
      errorCode: leaf.errorCode,
    };
  }

  // CA-chain fetch + trust assembly (the CA trust-fetch behavior) — reported as DER hashes so
  // the .NET side pins the served chain equals the real CA's, plus the raw chain
  // for the assembled-bundle length assertion.
  const chain = await issuer.getCaCertificate();
  const trust = await leafClient.getCaTrustBundle();

  // Present the ISSUED leaf at the mTLS endpoint. The loopback SERVER cert is
  // self-signed harness plumbing, so the channel's server-trust root is the
  // explicitly-passed server cert; the CLIENT-side material (chain + key) is the
  // production client's own.
  const credentials = buildMutualTlsCredentials({
    caBundlePem: serverCertPem,
    certChainPem: leaf.data.certChainPem,
    privateKeyPem: leaf.data.privateKeyPem,
  });

  const call = await callSignFixture(credentials);

  return {
    stage: "business-call",
    issuanceSucceeded: true,
    caRootDerSha256: chain.data
      ? sha256Hex(chain.data.rootCertificateDer)
      : undefined,
    caIntermediateDerSha256: chain.data
      ? sha256Hex(chain.data.intermediateCertificateDer)
      : undefined,
    trustBundleAssembled:
      trust.success === true && trust.data.caBundlePem.length > 0,
    ...call,
  };
}

async function runPresentPem(
  serverCertPem,
  leafPemPath,
  keyPemPath,
  extraChainPemPath,
) {
  const leafPem = readFileSync(leafPemPath, "utf8");
  const keyPem = readFileSync(keyPemPath, "utf8");
  const extraChainPem =
    extraChainPemPath !== undefined
      ? readFileSync(extraChainPemPath, "utf8")
      : "";

  const credentials = buildMutualTlsCredentials({
    caBundlePem: serverCertPem,
    certChainPem: leafPem + extraChainPem,
    privateKeyPem: keyPem,
  });

  return { stage: "business-call", ...(await callSignFixture(credentials)) };
}

async function runNoCert(serverCertPem) {
  const credentials = grpc.credentials.createSsl(Buffer.from(serverCertPem));

  return { stage: "business-call", ...(await callSignFixture(credentials)) };
}

async function runGetKeyring(
  serverCertPem,
  leafPemPath,
  keyPemPath,
  chainPemPath,
  keyDomain,
  frameBase64,
) {
  const leafPem = readFileSync(leafPemPath, "utf8");
  const keyPem = readFileSync(keyPemPath, "utf8");
  const chainPem = readFileSync(chainPemPath, "utf8");

  const credentials = buildMutualTlsCredentials({
    caBundlePem: serverCertPem,
    certChainPem: leafPem + chainPem,
    privateKeyPem: keyPem,
  });

  // The emitted KeyCustodian keyring service over a proto-loader grpc-js stub, dialed
  // over the mutual-TLS channel — the exact production wire the SSR host uses.
  const KeyCustodianKeyring = loadService(
    "key_custodian_keyring_get_keyring.g.proto",
    "d2.keycustodian.v2alpha",
    "KeyCustodianKeyring",
  );
  const keyringSvcClient = new KeyCustodianKeyring(mtlsTarget, credentials);
  const stub = {
    getKeyring: keyringSvcClient.getKeyring.bind(keyringSvcClient),
  };

  // The SHIPPED consumer runtime: the emitted facade + GrpcKeyringClient.
  const keyringClient = new GrpcKeyringClient(
    createKeyCustodianGrpcClient(stub),
  );
  const result = await keyringClient.getKeyring(keyDomain);
  keyringSvcClient.close();

  if (result.failed || result.data === undefined) {
    return {
      stage: "get-keyring",
      keyringFetched: false,
      statusCode: result.statusCode,
      errorCode: result.errorCode,
    };
  }

  const keyring = result.data;
  const activeKid = keyring.activeKid;
  const crypto = new PayloadCrypto(keyring);

  // Cross-runtime: decrypt the .NET-produced frame with the fetched keyring.
  const frame = new Uint8Array(Buffer.from(frameBase64, "base64"));
  const decrypted = Buffer.from(await crypto.decrypt(frame));

  // Same-runtime round-trip: the fetched keyring also encrypts + decrypts.
  const selfPlain = Buffer.from("ts-self-roundtrip", "utf8");
  const selfFrame = await crypto.encrypt(new Uint8Array(selfPlain));
  const selfDecrypted = Buffer.from(await crypto.decrypt(selfFrame));

  keyring.dispose();

  return {
    stage: "get-keyring",
    keyringFetched: true,
    activeKid,
    decryptedBase64: decrypted.toString("base64"),
    selfRoundTripOk: selfDecrypted.equals(selfPlain),
  };
}

async function main() {
  const serverCertPem = readFileSync(serverCertPemPath, "utf8");

  let outcome;

  if (mode === "client-flow") {
    outcome = await runClientFlow(rest[0], serverCertPem);
  } else if (mode === "present-pem") {
    outcome = await runPresentPem(serverCertPem, rest[0], rest[1], rest[2]);
  } else if (mode === "no-cert") {
    outcome = await runNoCert(serverCertPem);
  } else if (mode === "get-keyring") {
    outcome = await runGetKeyring(
      serverCertPem,
      rest[0],
      rest[1],
      rest[2],
      rest[3],
      rest[4],
    );
  } else {
    throw new Error(`Unknown probe mode: ${mode}`);
  }

  writeFileSync(
    resultPath,
    JSON.stringify({ ok: true, mode, ...outcome }, null, 2),
  );
}

try {
  await main();
  process.exit(0);
} catch (error) {
  // Best-effort result so the .NET side sees the crash shape (never key material).
  try {
    writeFileSync(
      resultPath,
      JSON.stringify(
        { ok: false, mode, crash: String(error?.name ?? "Error") },
        null,
        2,
      ),
    );
  } catch {
    // The result path itself is unusable — the exit code carries the failure.
  }
  console.error(String(error?.stack ?? error));
  process.exit(1);
}
