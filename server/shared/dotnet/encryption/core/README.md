<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Shared.Encryption

> Parent: [`server/shared/dotnet/`](../README.md)

AES-256-GCM payload encryption with a JWKS-style multi-kid keyring. Pure crypto primitive — knows nothing about message buses, domains, or where keys come from. Messaging-bus integration and KeyCustodian integration are out of scope for this lib; consumers construct keyrings out of bytes they obtained elsewhere and hand them in.

## Public surface

| Type                                             | Role                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                            |
| ------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `PayloadCryptoKeyring`                           | Immutable container for the active kid + zero-or-more retiring kids, plus an opaque AAD context. `IDisposable` zeroes every key buffer on dispose. Never log or serialize an instance — its `ToString()` is deliberately redacted.                                                                                                                                                                                                                                                                                                                                              |
| `IPayloadCrypto`                                 | `Encrypt(ReadOnlySpan<byte>) → byte[]` (uses the active kid) and `Decrypt(ReadOnlySpan<byte>) → byte[]` (reads the kid from the frame).                                                                                                                                                                                                                                                                                                                                                                                                                                         |
| `PayloadCrypto`                                  | Default implementation. Per-call `AesGcm` instantiation — safe to share across threads.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                         |
| `EncryptionException` (abstract)                 | Base type for `KidNotInKeyringException`, `FrameVersionMismatchException`, `FrameMalformedException`. AEAD authentication failures surface as the BCL `AuthenticationTagMismatchException` — not wrapped, since the operational response differs.                                                                                                                                                                                                                                                                                                                               |
| `AddD2EncryptionFor(serviceKey, keyringFactory)` | Registers a keyed `PayloadCryptoKeyring` and matching keyed `IPayloadCrypto` so a service holding multiple keyrings can pick with `[FromKeyedServices(...)]`.                                                                                                                                                                                                                                                                                                                                                                                                                   |
| `AddD2EncryptionStartupCheck()`                  | Opt-in `IHostedService` that runs an encrypt → decrypt round-trip against every registered keyed crypto at boot and crashes the host on any failure.                                                                                                                                                                                                                                                                                                                                                                                                                            |
| `EncryptionDomains`                              | Spec-driven closed catalog of keyring-domain identifiers (`AUDIT` / `NOTIFICATIONS` / `COURIER` + the `PLAINTEXT` sentinel). Codegen-emitted from `contracts/encryption-domains/encryption-domains.spec.json` by `D2.Shared.EncryptionDomains.SourceGen`. Consumers reference the constants (e.g. `EncryptionDomains.AUDIT`) when registering / resolving keyrings; the `MqMessages.SourceGen` cross-validates `mq-messages.spec.json:encryption` values against this catalog at codegen time so a typo cannot silently route to a non-existent keyring.                        |
| `EncryptionFrameLayout`                          | Spec-driven closed catalog of binary frame-layout constants — `CURRENT_VERSION`, per-field `*_OFFSET` / `*_LENGTH`, and `CONSTRAINT_*` GCM-spec values (`CONSTRAINT_MIN_KID_LENGTH`, `CONSTRAINT_MAX_KID_LENGTH`, `CONSTRAINT_NONCE_LENGTH`, `CONSTRAINT_TAG_LENGTH`, `CONSTRAINT_MIN_FRAME_SIZE`). Codegen-emitted from `contracts/encryption-frame/encryption-frame.spec.json` by `D2.Shared.EncryptionFrame.SourceGen`. Mirrored on the TS side via `@d2/encryption-abstractions`'s `EncryptionFrame` so the .NET encoder and any TS decoder consume identical byte offsets. |

## Spec-driven catalogs

Two closed catalogs in this library are emitted from contracts, not hand-written:

- **`EncryptionDomains`** — from `contracts/encryption-domains/encryption-domains.spec.json`. Single source of truth for keyring-domain identifiers used at registration (`AddD2EncryptionFor(EncryptionDomains.AUDIT, ...)`) and on the messaging side (`mq-messages.spec.json:encryption` values must come from this catalog). Cross-language mirror: `@d2/encryption-abstractions` `EncryptionDomains`.
- **`EncryptionFrameLayout`** — from `contracts/encryption-frame/encryption-frame.spec.json`. Binary frame-layout offsets + lengths + GCM-spec constraint values. Frame format (`[1 byte: version][1 byte: kid_length][N bytes: kid (UTF-8)][12 bytes: nonce][M bytes: ciphertext + 16-byte tag]`) is described in `Frame format` below. Cross-language mirror: `@d2/encryption-abstractions` `EncryptionFrame`.

Both catalogs ship as `.g.cs` files under `Generated/` (committed to git so PR reviewers see diffs without a local build).

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
- Compromise of the keys themselves — handled by KeyCustodian (see the [KeyCustodian README](../../../../services/edge/key-custodian/README.md) for the lifecycle authority; compromise-response runbook authoring is a tracked future deliverable).

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
// store encryptedKeyMaterial in auth_db.encryption_key.key_material_encrypted
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

- `Microsoft.Extensions.DependencyInjection.Abstractions` (DI registration helpers)
- `Microsoft.Extensions.Hosting.Abstractions` (the startup self-test is an `IHostedService`)
- `Microsoft.Extensions.Logging.Abstractions` (self-test logs pass/fail per domain)
- `JetBrains.Annotations` (annotations only, never ships)
- BCL `System.Security.Cryptography.AesGcm` — backed by hardware-accelerated AES-NI on every modern CPU

No deps on `D2.Shared.Result`, `D2.Shared.Utilities`, etc. The lib is intentionally narrow — it's a primitive, not a domain helper.

## Telemetry

None by design. This lib is a low-level crypto primitive — instrumentation lives in the consumer (`D2.Shared.Messaging.RabbitMq` emits the encrypted-publish / encrypted-consume spans + counters). Adding spans here would obscure who actually paid the encrypt/decrypt cost in distributed traces.

## References

- [KeyCustodian README](../../../../services/edge/key-custodian/README.md) — KeyCustodian lifecycle authority (this lib is the encrypt/decrypt half; KeyCustodian owns key generation, rotation, and compromise). Compromise-response runbook authoring is a tracked future deliverable.
