<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Edge.KeyCustodian.Domain

> Parent: [`server/services/edge/key-custodian/`](../README.md)

The pure domain layer of the KeyCustodian. Models the lifecycle of a managed encryption key as an immutable sum-type state machine — no EF Core, no DI, no I/O, no crypto primitives. Key material arrives as already-encrypted bytes; the domain never touches plaintext.

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
  Keys/
    EncryptionKey.cs        abstract record base + shared guards
    PendingKey.cs           sealed; Activate + Compromise transitions
    ActiveKey.cs            sealed; Rotate + Compromise transitions
    RetiringKey.cs          sealed; Retire + Compromise transitions
    RetiredKey.cs           sealed; terminal — no transitions
    CompromisedKey.cs       sealed; terminal — no transitions
  ValueObjects/
    Kid.cs                  JWKS-safe key identifier VO (smart constructor → D2Result<Kid>)
    KeyDomain.cs            validated domain string VO against a static catalog
    KeyMaterialEncrypted.cs root-wrapped ciphertext VO; ToString redacted
    PublicKeyMaterial.cs    unencrypted public-key bytes VO (RSA only)
    RotationPolicy.cs       validated cadence/grace/smoke-soak durations VO
    SmokeProof.cs           opaque evidence VO — existence IS the proof
  Enums/
    KeyStatus.cs            derived state-machine discriminator enum
    KeyType.cs              RsaSigning / AesPayload / Secret
  Audit/
    EncryptionKeyAudit.cs   append-only lifecycle event record (no key material)
    KeyAuditAction.cs       enum of audit actions (Generated / Activated / Rotated / ...)
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

`D2.Shared.Result`, `D2.Shared.Utilities`, `D2.Shared.Time` (NodaTime `IClock` + `Instant`), `D2.Shared.Encryption` (the `EncryptionDomains` catalog consumed by `KeyDomain`'s static catalog builder).

Zero EF Core, zero DI, zero I/O.
