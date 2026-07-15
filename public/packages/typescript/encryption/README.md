<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# @dcsv-io/d2-encryption

Runtime crypto twin of .NET `DcsvIo.D2.Encryption`, for the KC-backed crypto
runtimes that compose it — `@dcsv-io/d2-private-key-custodian-client` (the sealer / opener /
symmetric-crypto sources) and `@dcsv-io/d2-messaging-rabbitmq` (the auto-encrypting
publisher composer + `CryptoBodyOpener`), plus any Node service wiring KC-backed
payload crypto. Provides both payload encryption modes, byte-identical to the
.NET encoder (KAT-pinned, both directions), built on WebCrypto (`node:crypto`
`webcrypto.subtle`). Consumes the wire-layout constants from
[`@dcsv-io/d2-encryption-abstractions`](../encryption-abstractions/README.md); this
package adds the behavioral codecs, keyrings, and AEAD primitives.

.NET mirror: `DcsvIo.D2.Encryption` (the runtime crypto core). The
abstractions/runtime split mirrors the .NET layout — abstractions carry the
spec-emitted layout constants; this package carries the hand-written runtime.

## Two modes

| Mode | Frame | Primitive | Keying |
| --- | --- | --- | --- |
| **Symmetric** (v1) | `EncryptionFrame` | AES-256-GCM | one shared multi-kid keyring per domain; AAD = keyring's `aadContext` |
| **Sealed** (v2) | `SealedFrame` | P-256 ECDH-ES → HKDF-SHA256 → AES-256-GCM | producer seals to a recipient service's public keyring; only the recipient's private keyring opens |

The sealed derivation is **frozen** and wire-permanent (see
`sealed-key-derivation.ts`): HKDF `info` = `"d2-seal-v1"` ‖ len16BE(serviceId) ‖
serviceId ‖ len16BE(ephSPKI) ‖ ephSPKI; HKDF `salt` = AES-GCM `aad` =
UTF-8(serviceId) — anchored on the recipient SERVICE, never the message domain,
so one opener per service covers every sealed domain routed to it.

## Public surface

- **Ports**: `IPayloadCrypto` (`encrypt`/`decrypt`), `IPayloadSealer` (`seal`),
  `IPayloadOpener` (`open`) — all async (WebCrypto is async; the .NET twins are
  synchronous).
- **Symmetric**: `PayloadCryptoKeyring`, `PayloadCrypto`.
- **Sealed**: `RecipientPublicKeyring` (producer), `RecipientPrivateKeyring`
  (consumer), `PayloadSealer`, `PayloadOpener`. The recipient keyrings are built
  through async `create(...)` factories — WebCrypto's `importKey` is the
  construction-time P-256 validation (structural import + on-curve check).
- **Typed failure taxonomy** (twins of the .NET exception hierarchy):
  `EncryptionError` (base), `FrameMalformedError`, `FrameVersionMismatchError`,
  `KidNotInKeyringError`, `AuthenticationTagMismatchError`.

## Usage

The KC-backed runtimes build the keyrings from fetched key material and compose
these ports; a direct call site is just construct-then-await:

```ts
import { PayloadCrypto, PayloadCryptoKeyring } from "@dcsv-io/d2-encryption";

// symmetric (v1): active kid, a kid → 32-byte-key map, and the domain AAD context
const keyring = new PayloadCryptoKeyring(activeKid, keyBytesByKid, aadContext);
const crypto = new PayloadCrypto(keyring);
const framed = await crypto.encrypt(plaintextBytes); // v1 frame
const plaintext = await crypto.decrypt(framed);
```

The sealed (v2) path is the same shape:
`new PayloadSealer(recipientPublicKeyring).seal(bytes)` on the producer,
`new PayloadOpener(recipientPrivateKeyring).open(frame)` on the recipient — the
recipient keyrings are built through their async `create(...)` factories.

## Configuration & telemetry — N/A

- **Configuration**: none — key material and service ids are constructor /
  `create(...)` arguments; there are no Options records or environment knobs.
- **Telemetry**: none — these are compile-neutral crypto primitives that emit no
  counters / spans / metrics. Fetch / rotation / publish telemetry belongs to the
  KC-backed runtimes that compose them (`@dcsv-io/d2-private-key-custodian-client`,
  `@dcsv-io/d2-messaging-rabbitmq`).

## Invariants

- **Key material never logged**: every keyring's `toString` is redacted; error
  messages carry no key/frame/plaintext bytes.
- **Zeroization** (`PayloadCryptoKeyring`, `RecipientPrivateKeyring`): `dispose`
  fills the internal buffers with zeroes. Best-effort against JS GC reality — it
  clears the specific backing buffers this package holds, not any copy the
  runtime may have made during import.
- **Fail-loud**: an undecodable frame, an unknown kid, a wrong-recipient frame,
  or tampering each surface as a distinct typed error — never a silent
  mis-decode.

## Cross-runtime proof

Byte-compatibility with .NET is gated by file-based known-answer vectors in both
directions (see [`@dcsv-io/d2-contract-tests`](../contract-tests/README.md) and the
`scripts/emit-*.fixture.ts` frame emitters), plus the .NET `TsCryptoInterop`
suite that opens TS-produced frames. The frozen sealed derivation is pinned by
the .NET `SealedKeyDerivationFreezeTests` and reproduced here.
