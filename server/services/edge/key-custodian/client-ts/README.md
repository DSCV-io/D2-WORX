<!--
Copyright (c) DCSV. All rights reserved.
-->

# @d2/key-custodian-client

> Parent: [`server/services/edge/key-custodian/`](../README.md)

The Node **workload-leaf certificate client** — the behavioral twin of the .NET
`D2.Shared.Auth.Outbound.WorkloadCertificate.WorkloadLeafClient`. A workload that
needs to present a mutual-TLS client identity uses this package to obtain and keep
current a short-lived leaf certificate from KeyCustodian, and to present it on an
outbound gRPC channel.

The private key **never leaves the process** and never crosses any wire: the
client generates a fresh ECDSA P-256 keypair locally, sends only a PKCS#10
certificate-signing request (CSR — public material), and pairs the returned
certificate with its local key.

## What it does

- **Fresh keypair per (re)issue** — a new ECDSA P-256 key is minted every reissue
  cycle (rotation freshness); the private key is exported only as a PKCS#8 PEM
  handed to the channel credentials, never logged or serialized to any wire.
- **CSR-only issuance** — builds a PKCS#10 CSR (`@peculiar/x509`) with a fixed
  placeholder subject (`CN=d2-workload`). KeyCustodian structurally ignores the
  CSR subject: the leaf's subject-alternative-name is always its authenticated
  view of the caller, so impersonation-by-subject is unrepresentable.
- **Mismatch defense** — a returned leaf whose public key does not equal the local
  key is rejected before any cache write (there is no private key for it); the
  still-valid cached leaf keeps serving.
- **CA-chain fetch + trust assembly** — fetches `getCaCertificate` (root + issuing
  intermediate) and assembles the trust bundle the mutual-TLS channel pins for the
  server side. (This is a TS-side behavior; the .NET client receives its
  intermediate inline.)
- **Refresh-ahead + serve-stale** — a single-value cache serves any unexpired leaf;
  within a refresh margin the client proactively reissues under a single-flight +
  circuit-breaker, and a still-valid leaf keeps being served if a reissue fails.
- **Mutual-TLS presentation** — assembles `ChannelCredentials.createSsl(...)`
  presenting the leaf chain + private key and pinning the fetched CA bundle
  (net-new TS-side; the shared `@d2/grpc-client` channel is server-TLS only).

## Public surface

- `WorkloadLeafClient` — the transport-agnostic core (`getCurrentLeaf`,
  `forceReissue`, `getCaTrustBundle`, `currentChannelCredentials`, `dispose`).
- `WorkloadCertificateIssuer` — the transport port (`issueLeaf`,
  `getCaCertificate`) the client depends on.
- `GrpcWorkloadCertificateIssuer` — the adapter binding the port to the emitted
  KeyCustodian TS gRPC client.
- `createKeyCustodianGrpcClient`, `KeyCustodianGrpcClient`, `IssueLeafInput/Output`,
  `GetCaCertificateInput/Output` — re-exported from the generated wire surface for
  host composition.
- `buildMutualTlsCredentials`, `assembleTrustStore`, `generateLeafKeypair`,
  `buildCsr`, `leafMatchesLocalKey`, `derToPem` — the composable building blocks.

## Configuration

`WorkloadLeafClient` takes an optional `WorkloadLeafClientOptions` — every field is
optional and the defaults mirror the .NET `AuthOutboundResilienceDefaults`.

| Option                   | Type              | Default    | Purpose / when to override                                                                          |
| ------------------------ | ----------------- | ---------- | -------------------------------------------------------------------------------------------------- |
| `now`                    | `() => number`    | `Date.now` | Epoch-millisecond clock. Override for deterministic timing in tests.                                |
| `refreshMarginMs`        | `number`          | `300000`   | Refresh-ahead margin (5 min): a leaf within this margin of its not-after triggers a proactive reissue while still being served. Lower it for a tighter reissue window, raise it to reissue earlier. |
| `circuitFailureThreshold`| `number`          | `5`        | Consecutive transient reissue failures before the circuit opens (fast-fail).                        |
| `circuitCooldownMs`      | `number`          | `30000`    | Circuit open→half-open cooldown (30 s) before the next reissue probe is allowed.                    |
| `logger`                 | `ILogger \| undefined` | `undefined` | Optional structured logger. Receives only sanitized failure fields — never key material.       |

## Dependencies

Workspace refs:

- `@d2/grpc-client` — shared gRPC channel + client primitives (the base server-TLS channel).
- `@d2/logging` — `ILogger` + `sanitizedErrorRender` for safe, key-material-free failure logging.
- `@d2/resilience` — `CircuitBreaker` + `Singleflight` backing the reissue resilience + dedup.
- `@d2/result` — `D2Result` typed outcomes.
- `@d2/utilities` — shared TS utility helpers.

External packages:

- `@grpc/grpc-js` (`1.14.3`) — gRPC transport + `ChannelCredentials`.
- `@peculiar/x509` (`2.0.0`) — PKCS#10 CSR construction + X.509 parsing (the leaf↔local-key mismatch defense).
- `reflect-metadata` (`0.2.2`) — decorator-metadata runtime the emitted ts-proto client relies on.
- `temporal-polyfill` (`0.3.2`) — supplies `Temporal.Instant` for the leaf not-after (the TS twin of the .NET NodaTime `Instant`).

## Composition (host wiring)

The host builds the emitted gRPC client over a ts-proto grpc-js stub bound to a
mutual-TLS channel, then hands it to `GrpcWorkloadCertificateIssuer`:

```ts
import {
  WorkloadLeafClient,
  GrpcWorkloadCertificateIssuer,
  createKeyCustodianGrpcClient,
} from "@d2/key-custodian-client";

// `stub` is the KeyCustodian ts-proto grpc-js client bound to the KC endpoint.
const grpcClient = createKeyCustodianGrpcClient(stub);
const issuer = new GrpcWorkloadCertificateIssuer(grpcClient);
const leafClient = new WorkloadLeafClient(issuer);

const credentials = await leafClient.currentChannelCredentials();
// present `credentials.data` on the outbound mutual-TLS channel.
```

The live cross-process gRPC issuer stub construction is host-gated (the same
host-gating the .NET real gRPC issuer carries — see ADR-0023).

## The generated wire surface

The TypeSpec-emitted KeyCustodian gRPC client + its wire DTOs (`@ts-nocheck`,
byte-gate-governed) are co-located **by concern**, mirroring the .NET client: the
gRPC client lives in `src/facade/`, and each op's DTO in its concern folder
(`src/signing/`, `src/keyring/`, `src/sealing/`, `src/issuance/`,
`src/ca-certificate/`). The cohesive hand-written leaf-issuance / mTLS runtime is
kept together under `src/issuance/` (co-located with the `issue-leaf` DTO it
serves). Placement is driven by each op's `@d2Concern` — the emitter routes the
DTO into `<concern-kebab>/` and rewrites the client's DTO imports to match. They
are produced by the `@d2/typespec-emitters` pipeline (the `ts-client-output-dirs`
emission target) and are **not** hand-edited — regenerate via the emitter's regen
script.

## Telemetry

N/A — this client emits no counters, spans, or metrics of its own. Observability is
via the optional injected `ILogger` (sanitized failure fields only — never key
material). Cross-process issuance telemetry surfaces in the consuming host and the
KeyCustodian service.

## Edge cases / gotchas

- **Serve-stale on transient failure** — a still-valid cached leaf keeps serving when
  a reissue fails; once no valid leaf can be produced the call returns a typed
  `serviceUnavailable`, and an aborted signal returns `canceled`.
- **Mismatch defense** — a returned leaf certifying a different public key is rejected
  before any cache write (there is no private key for it), so a compromised or wrong
  leaf can never be presented.
- **CSR size cap** — a CSR exceeding `MAX_CSR_DER_BYTES` is rejected client-side
  (defensive; KeyCustodian enforces the same cap server-side).
- **Windows/Schannel chain transmission** — on Linux/OpenSSL (the deployment target)
  the full `leaf → intermediate` chain is presented; on Windows, Schannel cannot
  transmit an application-supplied intermediate for a leaf whose internal-CA root is
  not in the OS trust store (a documented Schannel limitation) — install the CA into
  the OS store on a Windows host that needs the chain transmitted.

## Testing

- **Unit** (`tests/`, Vitest, 100% coverage): adversarial CSR / keypair / mismatch
  / trust-assembly / cache / rotation / serve-stale / circuit / cancellation /
  disposal, plus structural secret pins (no key material on the seam or in logs).
- **Cross-runtime (primary, unconditional)** — the TS CSR fixture emitter
  (`scripts/emit-csr-fixtures.fixture.ts`) writes committed CSR DER fixtures (valid
  + adversarial matrix) into the .NET harness folder; the `.NET`
  `NodeLeafClientCsrFixtureTests` drive the REAL KeyCustodian `CsrVerification` +
  issuance over them.
- **Live loopback mutual-TLS** — the `.NET` `NodeLeafClientMutualTlsHarnessTests`
  spawn the Node probe (`scripts/mtls-probe.fixture.mjs`), which runs the full
  production path over real Kestrel hosts (issuance over the wire → mismatch
  defense → CA fetch → mutual-TLS presentation → business call) and the reject
  matrix, at the handshake level. Cert-presenting cases run on Node/OpenSSL, which
  presents a private-CA leaf over the loopback socket where .NET/Schannel cannot.

### Commands

```sh
pnpm --filter @d2/key-custodian-client build
pnpm --filter @d2/key-custodian-client test
pnpm --filter @d2/key-custodian-client test:coverage
pnpm --filter @d2/key-custodian-client emit-csr-fixtures   # regenerate CSR fixtures
```

---

## Sealed + symmetric encryption runtime

Beyond the certificate client, this package carries the KC-backed payload
encryption runtime over the emitted `getKeyring` / `getOrLazyProvision*Seal*`
wire surface (dialed over the mTLS channel by the host) — the TS twin of
the .NET KC client sealer/opener/crypto sources.

- `SealingClient` / `GrpcSealingClient` + `KeyringClient` / `GrpcKeyringClient` —
  least-privilege ports mapping the emitted DTOs to validated `@d2/encryption`
  keyrings.
- `KeyringBackedPayloadSealer` (lazy public-key fetch), `KeyringBackedPayloadOpener`
  (fail-loud boot fetch), and `KeyringBackedPayloadCrypto` (symmetric) — each with
  rotation hot-swap (a plain reference swap — the single-threaded event loop makes
  it atomic, so no `Volatile`/`Interlocked` twin is needed), bounded refresh +
  serve-current, and grace-window zeroize of displaced private keyrings.
- `createEncryptionViaKeyring({ keyDomain, keyringClient, rotationSubscription, logger })`
  — the ONE call for SYMMETRIC (shared-keyring) encryption (the TS twin of the .NET
  `AddD2EncryptionForViaKeyring("<domain>")`): boot-fetches the keyring fail-loud,
  wires rotation refresh on the bare key domain (the .NET twin subscribes the same
  bare domain — a symmetric keyring has no `seal:` prefix), and returns the ready
  `IPayloadCrypto` + a `dispose`. `rotationSubscription` is REQUIRED — a symmetric
  consumer can never silently skip rotation and serve stale keys after KeyCustodian
  rotates.
- `createSealedCryptoViaKeyCustodian({ ownServiceId, sealingClient, rotationSubscription, ... })`
  — the ONE spec-driven call for SEALED (per-consumer ECDH) encryption (the TS twin
  of `AddD2SealedEncryptionViaKeyCustodian`): builds a sealer for every distinct
  generated `ConsumerServiceByDomain` entry and this service's opener ONLY when it is
  named a consumer (least-privilege). `rotationSubscription` is REQUIRED here too (the
  .NET twin always wires the rotation subscriber). The returned instances are passed
  explicitly into `@d2/messaging-rabbitmq`'s `createPublisher({ crypto })` /
  `CryptoBodyOpener` composition (composition instead of ambient DI). Key bytes are
  never logged.
- Both one-call helpers take a `RotationSubscription` port — the host adapts
  `@d2/messaging-rabbitmq` `subscribe` (domain-filtered) to it; it is the behavioral
  twin of the .NET `IRotationEventChannel`.
