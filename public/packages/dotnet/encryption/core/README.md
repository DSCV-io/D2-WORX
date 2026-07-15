<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.Encryption

> Parent: [`public/packages/dotnet/`](../README.md)

AES-256-GCM payload encryption with a JWKS-style multi-kid keyring, in two modes: **symmetric** (the version-1 frame — one keyring encrypts and decrypts) and **sealed** (the version-2 frame — a P-256 ECDH-ES → HKDF-SHA256 → AES-256-GCM hybrid where producers holding only a recipient's PUBLIC key seal payloads that only the recipient's PRIVATE key opens). Pure crypto primitive — knows nothing about message buses, domains, or where keys come from. Messaging-bus integration and KeyCustodian integration are out of scope for this lib; consumers construct keyrings out of bytes they obtained elsewhere and hand them in.

## Public surface

| Type                                             | Role                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                            |
| ------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `PayloadCryptoKeyring`                           | Immutable container for the active kid + zero-or-more retiring kids, plus an opaque AAD context. `IDisposable` zeroes every key buffer on dispose. Never log or serialize an instance — its `ToString()` is deliberately redacted.                                                                                                                                                                                                                                                                                                                                              |
| `IPayloadCrypto`                                 | `Encrypt(ReadOnlySpan<byte>) → byte[]` (uses the active kid) and `Decrypt(ReadOnlySpan<byte>) → byte[]` (reads the kid from the frame).                                                                                                                                                                                                                                                                                                                                                                                                                                         |
| `PayloadCrypto`                                  | Default implementation. Per-call `AesGcm` instantiation — safe to share across threads.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                         |
| `EncryptionException` (abstract)                 | Base type for `KidNotInKeyringException`, `FrameVersionMismatchException`, `FrameMalformedException`. AEAD authentication failures surface as the BCL `AuthenticationTagMismatchException` — not wrapped, since the operational response differs.                                                                                                                                                                                                                                                                                                                               |
| `AddD2EncryptionFor(serviceKey, keyringFactory)` | Registers a keyed `PayloadCryptoKeyring` and matching keyed `IPayloadCrypto` so a service holding multiple keyrings can pick with `[FromKeyedServices(...)]`.                                                                                                                                                                                                                                                                                                                                                                                                                   |
| `AddD2EncryptionStartupCheck()`                  | Opt-in `IHostedService` that runs an encrypt → decrypt round-trip against every registered keyed crypto at boot and crashes the host on any failure.                                                                                                                                                                                                                                                                                                                                                                                                                            |
| `EncryptionKeyringSource`                        | Provenance enum for a keyed registration — `StaticFactory` (type-zero, fail-closed DENY default) / `KeyCustodian`. Drives the source-provenance guard below.                                                                                                                                                                                                                                                                                                                                                                                                                    |
| `MarkD2EncryptionSource(serviceKey, source)`     | Records the provenance of the keyed registration under `serviceKey` (as a parallel keyed marker — the `EncryptionRegistration` record is unchanged). A KeyCustodian-sourced keyring source calls this; the raw `AddD2EncryptionFor` seam does not, so it stays deny-by-default.                                                                                                                                                                                                                                                                                                   |
| `AddD2EncryptionSourceCheck()`                   | Registers the deny-by-default `IHostedService` source-provenance guard (auto-hooked by `AddD2EncryptionFor`). A static / unmarked keyring crashes a non-Development host; a KeyCustodian-sourced one passes. Idempotent. See "Source-provenance guard" below.                                                                                                                                                                                                                                                                                                                     |
| `EncryptionDomains`                              | Spec-driven closed catalog of **public** keyring-domain identifiers (`PLAINTEXT` sentinel + framework fixtures such as `FIXTURE_SEALED`). Codegen-emitted from `public/contracts/encryption-domains/encryption-domains.spec.json` by `DcsvIo.D2.EncryptionDomains.SourceGen`. Product sealed domains (`audit` / `notifications` / `courier`) live on the private dual-values half (`ProductEncryptionDomains` in `DcsvIo.D2.Private.Encryption.Extensions`) and overlay the public mode catalog via `EncryptionDomainModeCatalog` + `ProductEncryptionDomainBootstrap`. Consumers reference constants when registering / resolving keyrings; messaging `MqMessageDescriptor.IsSealed` reads `EncryptionDomainModeCatalog` (overlay first, then generated baseline).                        |
| `EncryptionDomainModeCatalog`                    | Composition overlay for domain mode + sealed-consumer lookups. Public generated `EncryptionDomainModes` is the baseline; product hosts call `RegisterSealedDomain` (via private `ProductEncryptionDomainBootstrap.EnsureRegistered`) so messaging resolves product sealed wire values without public→private package references. Thread-safe; identical re-register is idempotent; conflicting re-register throws.                        |
| `EncryptionFrameLayout`                          | Spec-driven closed catalog of binary frame-layout constants — `CURRENT_VERSION`, per-field `*_OFFSET` / `*_LENGTH`, and `CONSTRAINT_*` GCM-spec values (`CONSTRAINT_MIN_KID_LENGTH`, `CONSTRAINT_MAX_KID_LENGTH`, `CONSTRAINT_NONCE_LENGTH`, `CONSTRAINT_TAG_LENGTH`, `CONSTRAINT_MIN_FRAME_SIZE`). Codegen-emitted from `contracts/encryption-frame/encryption-frame.spec.json` by `DcsvIo.D2.EncryptionFrame.SourceGen`. Mirrored on the TS side via `@dcsv-io/d2-encryption-abstractions`'s `EncryptionFrame` so the .NET encoder and any TS decoder consume identical byte offsets. |
| `IPayloadSealer`                                 | Encrypt-only capability of the sealed mode: `Seal(ReadOnlySpan<byte>) → byte[]` (a version-2 sealed frame). Structurally CANNOT open — there is no `Open` member; the capability split is compile-time. No AAD parameter — the recipient keyring carries the service id and the AEAD binding is derived from it structurally.                                                                                                                                                                                                                                                    |
| `IPayloadOpener`                                 | Decrypt-only capability of the sealed mode: `Open(ReadOnlySpan<byte>) → byte[]`. Resolves the recipient kid from the frame against this service's private keyring.                                                                                                                                                                                                                                                                                                                                                                                                              |
| `RecipientPublicKeyring`                         | Immutable container for a recipient service's PUBLIC sealing keys (active + retiring) + the recipient service id that anchors the AEAD binding. Constructor validates every entry imports as a P-256 public key AND completes a real agreement (fail-loud at the boundary, not at first `Seal`). Public-by-design material — not disposable; `ToString()` still never renders key bytes.                                                                                                                                                                                        |
| `RecipientPrivateKeyring`                        | Immutable container for THIS service's PRIVATE sealing keys (active + retiring) + the service id. `IDisposable` zeroes every key buffer on dispose; constructor rejects public-only / wrong-curve / garbage input. Never log or serialize an instance — `ToString()` is redacted.                                                                                                                                                                                                                                                                                               |
| `PayloadSealer`                                  | Default `IPayloadSealer`: fresh ephemeral P-256 keypair per message → ECDH against the recipient's active public key → HKDF-SHA256 (frozen conventions below) → per-message AES-256-GCM key → the version-2 frame. Shared secret + derived key zeroized in `finally`. Thread-safe (no shared mutable state).                                                                                                                                                                                                                                                                    |
| `PayloadOpener`                                  | Default `IPayloadOpener`: parse → resolve kid → import the frame's ephemeral public key (non-P-256 material rejected as frame malformation) → the same derivation → decrypt. Tampering / wrong recipient / mismatched keypair surface as `AuthenticationTagMismatchException`.                                                                                                                                                                                                                                                                                                  |
| `SealedFrameLayout`                              | Spec-driven closed catalog of SEALED (version-2) frame-layout constants, including the sealed-only `CONSTRAINT_EPH_PUB_LENGTH_PREFIX_SIZE` (2 — big-endian uint16) and `CONSTRAINT_MAX_EPH_PUB_LENGTH` (allocation cap). Codegen-emitted from the SIBLING spec `contracts/encryption-frame-sealed/encryption-frame-sealed.spec.json` by the same `DcsvIo.D2.EncryptionFrame.SourceGen` (its `SealedFrameGenerator` arm). TS mirror: `@dcsv-io/d2-encryption-abstractions`'s `SealedFrame`.                                                                                             |

## Spec-driven catalogs

Three closed catalogs in this library are emitted from contracts, not hand-written:

- **`EncryptionDomains`** — from `public/contracts/encryption-domains/encryption-domains.spec.json` (public half after dual-values split). Product sealed domains are declared under `private/contracts/encryption-domains/` and emitted as `ProductEncryptionDomains` / `ProductEncryptionDomainModes` in the private Extensions package; runtime sealed resolution for messaging goes through `EncryptionDomainModeCatalog` after product bootstrap. Cross-language mirror: `@dcsv-io/d2-encryption-abstractions` public catalog.
- **`EncryptionFrameLayout`** — from `contracts/encryption-frame/encryption-frame.spec.json`. Binary frame-layout offsets + lengths + GCM-spec constraint values. Frame format (`[1 byte: version][1 byte: kid_length][N bytes: kid (UTF-8)][12 bytes: nonce][M bytes: ciphertext + 16-byte tag]`) is described in `Frame format` below. Cross-language mirror: `@dcsv-io/d2-encryption-abstractions` `EncryptionFrame`.
- **`SealedFrameLayout`** — from the SIBLING spec `contracts/encryption-frame-sealed/encryption-frame-sealed.spec.json`. The version-2 SEALED frame's offsets + lengths + constraints. Deliberately a separate spec file so the version-1 catalog and its generated artifacts stay byte-identical while the sealed layout evolves. Cross-language mirror: `@dcsv-io/d2-encryption-abstractions` `SealedFrame`.

All catalogs ship as `.g.cs` files under `Generated/` (committed to git so PR reviewers see diffs without a local build).

## Frame format

Self-describing on-wire layout — receivers do not need out-of-band metadata to decrypt:

```
[1 byte: version=1]
[1 byte: kid_length (UTF-8 byte count, 1..255)]
[N bytes: kid (UTF-8)]
[12 bytes: GCM nonce (random per encryption)]
[M bytes: ciphertext + 16-byte GCM auth tag]
```

Overhead is ~14 bytes plus the kid length (typical kid `audit-2026q3` → 26 byte total overhead). The version byte is rejected if not `1`; future format revisions cannot be silently downgraded.

## Sealed mode (version-2 frame)

The sealed mode flips the encrypt/decrypt asymmetry into the cryptography for fan-out-sensitive traffic: many producers may SEAL to a recipient service, but only that service's private key OPENS. A producer holding an `IPayloadSealer` structurally cannot decrypt any sealed frame — including its own output.

### Sealed frame format

```
[1 byte:  version=2]
[1 byte:  recipient_kid_length (UTF-8 byte count, 1..64)]
[N bytes: recipient_kid (UTF-8)]
[2 bytes: eph_pub_length (BIG-ENDIAN uint16, capped at 256)]
[M bytes: eph_pub — the per-message ephemeral P-256 public key, SubjectPublicKeyInfo DER]
[12 bytes: GCM nonce (random per seal)]
[K bytes: ciphertext + 16-byte GCM auth tag]
```

The leading version byte is the mode discriminator: the symmetric codec hard-rejects `2`, the sealed codec hard-rejects `1` — the two formats can never cross-parse. `EncryptedBodyComposer.ReadKidFromFrame` (messaging) is version-aware and surfaces the recipient kid for `x-d2-encryption-kid` DLQ triage.

### The hybrid construction + the FROZEN wire values

Per seal: fresh ephemeral P-256 keypair → `Z = ECDH(ephemeral, recipientActivePublic)` → `DEK = HKDF-SHA256(ikm: Z, salt, info, 32)` → fresh 12-byte nonce → AES-256-GCM. Forward secrecy comes from the per-message ephemeral; the DEK is fresh per message so nonce reuse across messages is structurally impossible.

The derivation + AEAD-binding conventions are **WIRE-PERMANENT** (changing any of them breaks decryption of everything sealed before the change; each is pinned by known-answer freeze tests). All are anchored on the RECIPIENT SERVICE id (never the message domain), so one opener per service covers every sealed domain routed to it:

- HKDF `info` = `"d2-seal-v1"` ‖ len16BE(serviceId) ‖ UTF-8(serviceId) ‖ len16BE(ephSPKI) ‖ ephSPKI — each variable component prefixed by its 2-byte big-endian length, so component boundaries are unambiguous by construction.
- HKDF `salt` = UTF-8(serviceId).
- AES-GCM `aad` = UTF-8(serviceId) — a frame sealed for one service can never authenticate as sealed for another, even under the same keypair.

Both sealer and opener derive through the single internal `SealedKeyDerivation` implementation, so producer and consumer cannot disagree byte-for-byte, and neither API takes an AAD parameter — the recipient keyrings carry the service id and agreement is structural.

### Sealed startup self-check

A hosted self-check (the sealed sibling of `AddD2EncryptionStartupCheck`) verifies every registered sealed recipient at boot: a (sealer, opener) pair round-trips a sentinel (a mismatched keypair crashes the host); a producer-only host seal-checks its public material; an opener-only registration is logged (its private material was already validated at keyring construction). Zero registrations is a logged no-op. The registration surface is now **public** — `AddD2SealedEncryptionRecipient(recipientServiceId)` (records a recipient for the self-check) is the building-block seam a sealing SOURCE composes (the KeyCustodian-backed `AddD2SealedEncryptionViaKeyCustodian` in the KC client package), not the surface consumers remember.

A separate deny-by-default **sealed source-provenance guard** (`AddD2SealedEncryptionSourceCheck` → `SealedEncryptionSourceStartupCheck`, the sealed sibling of `AddD2EncryptionSourceCheck`) rejects a static / unmarked sealed recipient outside a Development host — the KeyCustodian-backed source marks each registration `EncryptionKeyringSource.KeyCustodian` (via `MarkD2EncryptionSource`) so it passes. A host that hand-wires a keyed sealer/opener directly, bypassing the library extension, is caught here.

## Threat model

**Defends against:**

- DB row exfiltration of stored keys (assumes keys are wrapped with a separate root key before storage — see "Key wrapping" below).
- Wire interception of ciphertext on RabbitMQ / DLQ / archive blob storage.
- Bit-flipping of ciphertext or AAD (GCM auth tag detects → `AuthenticationTagMismatchException`).
- Cross-domain replay (different AAD contexts cause tag mismatch).
- Cross-version replay (version byte must match).
- Buffer-overrun via crafted frame (every length prefix is bounds-checked).

**Does not defend against (higher-layer concerns):**

- Replay of valid ciphertext — handle with idempotency keys / sequence numbers at the message bus or handler layer.
- Plaintext-length leakage — pad the plaintext if length-hiding matters.
- Memory-dump of the running process — OS-level mitigations (disable swap, etc.).
- Side-channel timing attacks against AES-NI — the BCL / OS handles this.
- Compromise of the keys themselves — handled by KeyCustodian (see the [KeyCustodian README](../../../../services/edge/key-custodian/README.md) for the lifecycle authority; the compromise-response runbook is out of scope for this library).
- A host that hand-registers a keyed `IPayloadCrypto` directly (`AddKeyedSingleton<IPayloadCrypto>(...)`, bypassing every library extension) — such a registration records no `EncryptionRegistration` and is invisible to the source-provenance guard below. That is deliberate circumvention, not the guarded footgun.

## Source-provenance guard (deny-by-default)

Every `AddD2EncryptionFor` registration is checked at host startup for its keyring **provenance**. The intent is to stop a hand-wired static key (the `AddD2EncryptionFor(serviceKey, staticFactory)` footgun) from silently backing production traffic where a rotation-aware, KeyCustodian-sourced keyring belongs.

- **Deny-by-default.** A registration with no `EncryptionSourceMarker` — the raw `AddD2EncryptionFor` seam registers none — is treated as `StaticFactory`. A KeyCustodian-sourced keyring source opts in by calling `MarkD2EncryptionSource(serviceKey, EncryptionKeyringSource.KeyCustodian)`.
- **Environment gate.** Outside a Development host, a static / unmarked domain throws at startup and crashes the host (fail-loud). In a Development host the same case logs a loud warning and proceeds, so local development without a running KeyCustodian still works. A missing `IHostEnvironment` is treated as non-Development (fail-closed).
- **Auto-hooked.** `AddD2EncryptionFor` registers the guard (`AddD2EncryptionSourceCheck()`) itself, so no encryption host can skip it. The registration is idempotent — one `EncryptionSourceStartupCheck` regardless of how many domains register.

**Known limitation (documented residual).** The guard only sees registrations made through the library extensions (`AddD2EncryptionFor` and the KeyCustodian keyring sources), because those record an `EncryptionRegistration`. A host that hand-registers a keyed `IPayloadCrypto` directly, bypassing every library extension, records no registration and is structurally invisible to the guard. That path is deliberate circumvention, not the footgun the guard defends against — it is accepted as out of scope.

## Usage

### Single-keyring service

```csharp
services.AddD2EncryptionFor("audit", sp =>
{
    var bytes = LoadActiveAuditKey();           // your code, fetches from KeyCustodian / KeyringClient / etc.
    return new PayloadCryptoKeyring(
        activeKid: "audit-2026q2",
        keys: new Dictionary<string, byte[]> { ["audit-2026q2"] = bytes },
        aadContext: "audit"u8.ToArray());
});

services.AddD2EncryptionStartupCheck();
```

```csharp
public sealed class AuditPublisher(
    [FromKeyedServices("audit")] IPayloadCrypto crypto)
{
    public byte[] Pack(byte[] plaintext) => crypto.Encrypt(plaintext);
}
```

### Multi-kid keyring during rotation

```csharp
new PayloadCryptoKeyring(
    activeKid: "audit-2026q2",
    keys: new Dictionary<string, byte[]>
    {
        ["audit-2026q2"] = newKey,   // active — encrypts new traffic
        ["audit-2026q1"] = oldKey,   // retiring — decrypts in-flight messages encrypted before rotation
    },
    aadContext: "audit"u8.ToArray());
```

Once the grace window expires, the orchestrator (KeyCustodian) drops the retiring kid and constructs a new keyring; KeyringClient swaps the keyed singleton. Messages arriving for a dropped kid will throw `KidNotInKeyringException`, which the messaging bus routes to a DLQ for forensic decrypt by the ops CLI (which loads archived keys on demand).

### Key wrapping (for storing per-domain keys at rest)

Same primitive. Construct a keyring whose only kid is the root key, with whatever AAD bytes the wrapping layer wants to bind:

```csharp
using var rootKeyring = new PayloadCryptoKeyring(
    activeKid: "root-2026",
    keys: new Dictionary<string, byte[]> { ["root-2026"] = rootKeyBytes },
    aadContext: "wrap"u8.ToArray());

var wrapper = new PayloadCrypto(rootKeyring);
var encryptedKeyMaterial = wrapper.Encrypt(perDomainKeyBytes);
// store encryptedKeyMaterial in d2-keycustodian.key_record (root-wrapped material)
```

## Operational rules (DO and DON'T)

- **DO** dispose keyrings on rotation — disposes zero the key buffers.
- **DO** make AAD non-empty and meaningful (the constructor rejects empty AAD). The string itself is not secret; it just has to match encrypt-side and decrypt-side.
- **DO** rotate keys on the configured cadence — rotation is the primary tool that bounds the exposure window if a service holding a keyring is ever compromised.
- **DO** think about _what's inside_ the encrypted payload, not just who holds the key. In pub/sub, every publisher and consumer of a domain ends up with that domain's keyring (there is no smaller set that still works). The remaining lever is shaping payloads so a compromised key reveals as little as possible — e.g. publish trigger references that the consumer joins to its own DB, instead of fully-denormalized PII.
- **DON'T** log keyring instances. `ToString()` is redacted but field-level reflection or serializers can still leak bytes.
- **DON'T** log frame bytes or exception messages that include them. Our exceptions explicitly do not embed bytes; if you wrap them, keep that property.
- **DON'T** roll your own nonce strategy. `Encrypt` uses 12 random bytes from `RandomNumberGenerator` per call. There is no override path on purpose — GCM nonce reuse is catastrophic.
- **DON'T** strip or shorten the auth tag. The 16-byte tag is part of the ciphertext span and the BCL verifies it; there is no "decrypt-without-auth" mode in this lib's API.
- **DON'T** share keys across purposes. One keyring per purpose (per domain, per wrapping layer, etc.).

## Dependencies

- `Microsoft.Extensions.DependencyInjection.Abstractions` (DI registration helpers + keyed markers)
- `Microsoft.Extensions.Hosting.Abstractions` (the startup checks are `IHostedService`; the source guard reads `IHostEnvironment`)
- `Microsoft.Extensions.Logging.Abstractions` (the startup checks log pass/fail per domain)
- `DcsvIo.D2.Utilities` (the `ThrowIfFalsey` argument guard on registration helpers)
- `JetBrains.Annotations` (annotations only, never ships)
- BCL `System.Security.Cryptography.AesGcm` — backed by hardware-accelerated AES-NI on every modern CPU

No deps on `DcsvIo.D2.Result` or any domain helper. The lib is intentionally narrow — it's a primitive plus its DI registration surface.

## Telemetry

None by design. This lib is a low-level crypto primitive — instrumentation lives in the consumer (`DcsvIo.D2.Messaging.RabbitMq` emits the encrypted-publish / encrypted-consume spans + counters). Adding spans here would obscure who actually paid the encrypt/decrypt cost in distributed traces.

## References

- [KeyCustodian README](../../../../services/edge/key-custodian/README.md) — KeyCustodian lifecycle authority (this lib is the encrypt/decrypt half; KeyCustodian owns key generation, rotation, and compromise). The compromise-response runbook is out of scope for this library.
