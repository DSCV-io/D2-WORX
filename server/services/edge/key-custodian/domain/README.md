<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Edge.KeyCustodian.Domain

> Parent: [`server/services/edge/key-custodian/`](../README.md)

The pure domain layer of the KeyCustodian. Models the lifecycle of a managed encryption key as an immutable sum-type state machine, plus the pure no-IO rules over it (key generation, smoke testing, kid minting, JWK projection) — no EF Core, no DI, no I/O. BCL crypto primitives (`System.Security.Cryptography`) are used inside the `Rules/` generators and verifiers; key material is root-wrapped before it ever leaves the App layer.

---

## State machine

Key lifecycle is modeled as `abstract record EncryptionKey` + five sealed per-state records:

```
Pending → Active → Retiring → Retired    (terminal)
               ↘
         Compromised (terminal, reachable from Pending / Active / Retiring)
```

Each sealed type overrides `KeyStatus Status { get; }` with a compile-time constant. Transition methods exist ONLY on the states from which the transition is legal — a `RetiredKey` exposes no transition methods; calling one does not compile. This is the make-illegal-states-unrepresentable principle applied at the type level.

| Type              | Transition methods                                                              | Notes                                                      |
| ----------------- | ------------------------------------------------------------------------------- | ---------------------------------------------------------- |
| `PendingKey`      | `Activate(SmokeProof, RotationPolicy, IClock) → D2Result<ActiveKey>`           | Guarded — soak window + proof type; `Compromise(...)`.     |
| `ActiveKey`       | `Rotate(PendingKey successor, IClock) → (RetiringKey, PendingKey)`             | Unguarded — any active key may be rotated; `Compromise(...)`. |
| `RetiringKey`     | `Retire(RotationPolicy, IClock) → D2Result<RetiredKey>`                        | Guarded — grace window elapsed; `Compromise(...)`.          |
| `RetiredKey`      | _(none)_                                                                        | Terminal.                                                  |
| `CompromisedKey`  | _(none)_                                                                        | Terminal.                                                  |

Guarded transitions return `D2Result<TNext>` with a `KeyCustodianFailures<T>.*` factory on rejection. Unguarded transitions return the next state directly.

---

## Folder layout

```
domain/
  Entities/
    EncryptionKey.cs        abstract record base + shared guards
    PendingKey.cs           sealed; Activate + Compromise transitions
    ActiveKey.cs            sealed; Rotate + Compromise transitions
    RetiringKey.cs          sealed; Retire + Compromise transitions
    RetiredKey.cs           sealed; terminal — no transitions
    CompromisedKey.cs       sealed; terminal — no transitions
    EncryptionKeyAudit.cs   append-only lifecycle event record (no key material)
  ValueObjects/
    Kid.cs                  JWKS-safe key identifier VO (smart constructor → D2Result<Kid>)
    KeyDomain.cs            validated domain string VO against a static catalog
    KeyMaterialEncrypted.cs root-wrapped ciphertext VO; ToString redacted
    PublicKeyMaterial.cs    unencrypted public-key bytes VO (RSA only)
    RotationPolicy.cs       validated cadence/grace/smoke-soak durations VO
    SmokeProof.cs           opaque evidence VO — existence IS the proof
    GeneratedKeyMaterial.cs short-lived plaintext-material carrier (zeroed after wrapping)
    Jwk.cs                  RFC 7517 JSON Web Key (the JWKS-projection result)
  Enums/
    KeyStatus.cs            derived state-machine discriminator enum
    KeyType.cs              RsaSigning / AesPayload / Secret
    KeyAuditAction.cs       enum of audit actions (Generated / Activated / Rotated / ...)
  Rules/
    KeyGeneration.cs        pure key-material generator (size as method parameter)
    SmokeTesting.cs         pure per-type smoke verifier → D2Result (never throws)
    KidMinting.cs           pure JWKS-safe kid minter (16 random bytes → base64url)
    JwkProjection.cs        pure SPKI → RFC 7517 JWK projection
    KeySummary.cs           pure projection over EncryptionKey (shared command output)
  Generated/
    (codegen output — see below; do not hand-edit)
```

---

## Value objects

| VO                     | Smart constructor                                      | Notes                                                                                                  |
| ---------------------- | ------------------------------------------------------ | ------------------------------------------------------------------------------------------------------ |
| `Kid`                  | `Create(string?) → D2Result<Kid>`                      | JWKS-safe charset `[A-Za-z0-9_-]`, max 64 chars. `FromTrusted(string)` for rehydration.               |
| `KeyDomain`            | `Create(string?) → D2Result<KeyDomain>`                | Must be a member of the static catalog (EncryptionDomains minus `plaintext` + 3 KC-only domains).     |
| `KeyMaterialEncrypted` | `FromTrusted(ReadOnlyMemory<byte>)`                    | Non-empty root-wrapped ciphertext. `ToString` / `PrintMembers` emit a redaction sentinel.             |
| `PublicKeyMaterial`    | `FromTrusted(ReadOnlyMemory<byte>)`                    | Non-empty unencrypted public-key bytes (RSA SPKI). Not secret — may appear in logs.                   |
| `RotationPolicy`       | `Create(Duration, Duration, Duration) → D2Result<RotationPolicy>` | Validates cadence > grace + smoke-soak; all durations positive.                         |
| `SmokeProof`           | `ForPassedSmokeTest(KeyType, IClock) → SmokeProof`     | Construction gated: existence IS the evidence the smoke test passed. Carries `VerifiedType`.           |
| `GeneratedKeyMaterial` | `new(byte[] plaintext, byte[]? publicSpki)`            | Short-lived carrier for freshly-generated material; `Zero()` wipes the plaintext after root-wrapping.  |
| `Jwk`                  | `new(string kid, string n, string e)`                  | RFC 7517 public JWK (`kty`/`use`/`alg` fixed to RSA/sig/RS256). The return shape of `JwkProjection`.   |

---

## Rules (pure, no-IO behavior over the model)

| Rule              | Signature                                                              | Notes                                                                                          |
| ----------------- | --------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------- |
| `KeyGeneration`   | `Generate(KeyType, int rsaModulusBits, int secretLengthBytes) → GeneratedKeyMaterial` | One static dispatcher over `KeyType`; sizing tunables are method parameters (no `IOptions`). The unreachable `default` arm throws to preserve the unknown-type precondition. |
| `SmokeTesting`    | `Verify(KeyType, ReadOnlyMemory<byte>, ReadOnlyMemory<byte>?) → D2Result` | Per-type round-trip probe (RSA sign/verify, AES-GCM, HMAC). Never throws — corrupt material maps to `KEYCUSTODIAN_SMOKE_TEST_FAILED`. |
| `KidMinting`      | `Mint() → string`                                                     | 16 random bytes → unpadded base64url; guaranteed to pass `Kid.Create`.                          |
| `JwkProjection`   | `ToJwk(string kid, ReadOnlySpan<byte> publicSpki) → Jwk`              | Imports the SPKI to recover modulus/exponent and base64url-encodes them per RFC 7518.          |
| `KeySummary`      | `From(EncryptionKey) → KeySummary`                                    | Non-sensitive projection (kid/domain/type/status/createdAt). The shared output of the generate / activate / retire commands. |

Rules hold no DI, no `IOptions`, no logger, and no clock-as-dependency (`IClock` is a permitted method parameter). A tunable is a parameter the App handler passes in, not configuration the rule reads.

---

## Codegen output (Generated/)

The `error-codes-source-gen/` sibling project emits three files into `Generated/` at build time:

| File                              | Content                                                                     |
| --------------------------------- | --------------------------------------------------------------------------- |
| `KeyCustodianErrorCodes.g.cs`     | 7 `const string` error-code constants + `AllCodes` + `GetHttpStatus`        |
| `KeyCustodianFailures.g.cs`       | 7 `static D2Result FactoryName(...)` semantic factory methods                |
| `KeyCustodianFailures.Generic.g.cs` | The `KeyCustodianFailures<T>` typed twin                                  |

All transition methods and VO smart constructors use the generated `KeyCustodianFailures<T>.*` factories — never raw `D2Result.ValidationFailed(...)` with hand-written codes. See [`error-codes-source-gen/README.md`](../error-codes-source-gen/README.md).

---

## Dependencies

`D2.Shared.Result`, `D2.Shared.Utilities`, `D2.Shared.Time` (NodaTime `IClock` + `Instant`), `D2.Shared.Encryption` (the `EncryptionDomains` catalog consumed by `KeyDomain`'s static catalog builder), `D2.Shared.I18n` (the generated `TK.*` keys), and the BCL `System.Security.Cryptography` (used by the `Rules/` generators + verifiers).

Zero EF Core, zero DI, zero I/O.
