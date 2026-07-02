<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Edge.KeyCustodian.Domain

> Parent: [`server/services/edge/key-custodian/`](../README.md)

For engineers working inside the KeyCustodian module or any layer that calls into domain entities and rules. The pure domain layer of the KeyCustodian. Models the lifecycle of a managed encryption key as an immutable sum-type state machine, plus the pure no-IO rules over it (key generation, smoke testing, kid minting, JWK projection) — no EF Core, no DI, no I/O. BCL crypto primitives (`System.Security.Cryptography`) are used inside the `Rules/` generators and verifiers; key material is root-wrapped before it ever leaves the App layer.

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
| `PendingKey`      | `Activate(SmokeProof?, RotationPolicy?, IClock?) → D2Result<ActiveKey>`       | Guarded — soak window + proof type; null args → `KEYCUSTODIAN_PRECONDITION_VIOLATED`; `Compromise(...)`. |
| `ActiveKey`       | `Rotate(PendingKey? successor, IClock?) → D2Result<(RetiringKey, PendingKey)>`| Guarded — null args or domain/type mismatch → `KEYCUSTODIAN_PRECONDITION_VIOLATED`; `Compromise(...)`. |
| `RetiringKey`     | `Retire(RotationPolicy?, IClock?) → D2Result<RetiredKey>`                     | Guarded — grace window elapsed; null args → `KEYCUSTODIAN_PRECONDITION_VIOLATED`; `Compromise(...)`. |
| `RetiredKey`      | _(none)_                                                                        | Terminal.                                                  |
| `CompromisedKey`  | _(none)_                                                                        | Terminal.                                                  |

All transition methods return `D2Result<TNext>`; null-argument guards return `KEYCUSTODIAN_PRECONDITION_VIOLATED`, and lifecycle guards return a specific validation-failure code on rejection.

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
    KeyType.cs              RsaSigning / AesPayload / Secret / X509CaCertificate
    KeyAuditAction.cs       enum of audit actions (Generated / Activated / Rotated / ...)
  Rules/
    KeyGeneration.cs        pure key-material generator (size as method parameter)
    SmokeTesting.cs         pure per-type smoke verifier → D2Result (never throws)
    KidMinting.cs           pure JWKS-safe kid minter (16 random bytes → base64url)
    JwkProjection.cs        pure SPKI → RFC 7517 JWK projection
    KeySummary.cs           pure projection over EncryptionKey (shared command output)
    RsaSigning.cs           pure RS256 sign over an already-unwrapped private key
    CaCertificateGeneration.cs       pure root / intermediate CA certificate generator
    WorkloadCertificateIssuance.cs   pure workload leaf-certificate builder
    WorkloadCapabilityAuthority.cs   pure capability-general workload→target authority rule
    KeyLifecycleAuthority.cs         pure System-plane-only lifecycle-mutation authority rule
    WorkloadCertificateAuthority.cs  interim fail-closed DENY-ALL issuance authority skeleton
  Generated/
    (codegen output — see below; do not hand-edit)
```

---

## Value objects

| VO                     | Smart constructor                                                   | Notes                                                                                                  |
| ---------------------- | ------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------ |
| `Kid`                  | `Create(string?) → D2Result<Kid>`                                   | JWKS-safe charset `[A-Za-z0-9_-]`, max 64 chars. `FromTrusted(string)` for rehydration.               |
| `KeyDomain`            | `Create(string?) → D2Result<KeyDomain>`                             | Must be a member of the static catalog (EncryptionDomains minus `plaintext` + 5 KC-only domains). Every entry carries its bound `KeyType` (`jwks-signing`→RsaSigning, `cookie`/`client-secret`→Secret, `mtls-ca-*`→X509CaCertificate, encryption domains→AesPayload) — the single domain→type source of truth. `FromTrusted(string)` resolves the canonical entry case-insensitively and THROWS on a non-catalog stored value (data corruption). |
| `KeyMaterialEncrypted` | `FromTrusted(ReadOnlyMemory<byte>)`                                 | Non-empty root-wrapped ciphertext. `ToString` / `PrintMembers` emit a redaction sentinel.             |
| `PublicKeyMaterial`    | `FromTrusted(ReadOnlyMemory<byte>)`                                 | Non-empty unencrypted public-key bytes (RSA SPKI). Not secret — may appear in logs.                   |
| `RotationPolicy`       | `Create(Duration, Duration, Duration) → D2Result<RotationPolicy>`   | Validates cadence ≥ grace + smoke-soak; all durations positive.                         |
| `SmokeProof`           | `ForPassedSmokeTest(KeyType, IClock?) → D2Result<SmokeProof>`       | Construction gated: existence IS the evidence the smoke test passed. Carries `VerifiedType`.           |
| `GeneratedKeyMaterial` | `new(byte[] plaintext, byte[]? publicSpki)`                         | Short-lived carrier for freshly-generated material; `Zero()` wipes the plaintext after root-wrapping.  |
| `Jwk`                  | `new(string kid, string n, string e)`                               | RFC 7517 public JWK (`kty`/`use`/`alg` fixed to RSA/sig/RS256). The return shape of `JwkProjection`.   |

---

## Rules (pure, no-IO behavior over the model)

| Rule              | Signature                                                                                         | Notes                                                                                          |
| ----------------- | ------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------- |
| `KeyGeneration`   | `Generate(KeyType, int rsaModulusBits, int secretLengthBytes) → D2Result<GeneratedKeyMaterial>`   | One static dispatcher over `KeyType`; sizing tunables are method parameters (no `IOptions`). An unrecognized `KeyType` returns a flagged `KEYCUSTODIAN_PRECONDITION_VIOLATED` (never throws). |
| `SmokeTesting`    | `Verify(KeyType, ReadOnlyMemory<byte>, ReadOnlyMemory<byte>?) → D2Result`                         | Per-type round-trip probe (RSA sign/verify, AES-GCM, HMAC). Never throws — corrupt material maps to `KEYCUSTODIAN_SMOKE_TEST_FAILED`. |
| `KidMinting`      | `Mint() → string`                                                                                 | 16 random bytes → unpadded base64url; guaranteed to pass `Kid.Create`.                          |
| `JwkProjection`   | `ToJwk(string kid, ReadOnlySpan<byte> publicSpki) → Jwk`                                          | Imports the SPKI to recover modulus/exponent and base64url-encodes them per RFC 7518.          |
| `KeySummary`      | `From(EncryptionKey) → KeySummary`                                                                | Non-sensitive projection (kid/domain/type/status/createdAt). The shared output of the generate / activate / retire commands. |
| `RsaSigning`      | `Sign(ReadOnlySpan<byte> privatePkcs8, ReadOnlySpan<byte> signingInput) → D2Result<string>`       | RS256 (RSASSA-PKCS1-v1_5 over SHA-256) sign over an already-unwrapped private key; base64url-encoded signature. BCL crypto only; a crypto import/sign failure maps to `KEYCUSTODIAN_PRECONDITION_VIOLATED` rather than throwing. |

Rules hold no DI, no `IOptions`, no logger, and no clock-as-dependency (`IClock` is a permitted method parameter). A tunable is a parameter the App handler passes in, not configuration the rule reads.

## Lifecycle authority — `KeyLifecycleAuthority`

The System-plane-only authority over every destructive key-lifecycle mutation (generate / activate / rotate / retire / compromise / run-due-rotations / seed-CA). `AuthorizeLifecycleMutation(RequestOrigin)` is layered and fail-closed: `Unestablished` denies FIRST with the specific origin-unestablished failure (the type-zero explicit deny); `System` (the in-host workers that establish it via `EstablishSystemContext`) is the only allow; every other established plane is `Forbidden`. The System plane deliberately carries no scopes, so the origin gate — not a `ScopeRequirement` — is the control; a future admin transport (the operator compromise-key action is the standing candidate) must consciously extend this rule and add its own per-op scope. Every lifecycle command handler calls it at the TOP of `ExecuteAsync` and emits the `capability = lifecycle` authority-rejection counter + `AuthorityRejected` log on a deny.

## Issuance authority — `WorkloadCertificateAuthority` (interim DENY-ALL)

The authority over workload leaf-certificate issuance. The committed body is a fail-closed DENY-ALL skeleton — `Unestablished` denies first with the specific origin-unestablished failure; EVERY established origin is `Forbidden` — so a premature transport wiring denies 100% instead of minting workload identities for arbitrary callers. The cross-process issuance transport must land WITH the real rule replacing the deny arm: fail-closed on non-cross-process origin / absent peer identity, requested-workload-equals-authenticated-caller binding (or an explicit isolated delegated-issuer capability), plus the per-handler scope.

## Capability authority — `WorkloadCapabilityAuthority`

The capability-general workload→target authority rule — answers "may workload W
use capability C (sign / seal-encrypt / seal-decrypt) on target D?"
([ADR-0025](../../../../../docs/adrs/0025-request-context-establishment.md)).
Pure — no DB, no `IOptions`, no logging, never throws; every arm returns a
`D2Result` (allow = `Ok`, deny a typed failure). The workload→policy map is a
method PARAMETER, never an injected option — the App handler resolves the
policy and owns the counter / log on a deny.

| Method                | Keyed on                                                                 | Notes |
| ---------------------- | ------------------------------------------------------------------------ | ----- |
| `AuthorizeSigning`     | `RequestOrigin` (the locally-established, never-propagated hop fact, from `D2.Shared.Auth.Abstractions`) + `ImmediateCaller` + the target `KeyDomain` + the caller's allowed-signing-domains set | Fail-closed layered decision: `Unestablished` origin denies; the cluster-signing root `jwks-signing` is STRUCTURALLY unreachable here for EVERY established origin (`MinterCapabilityRequired`) — reachable only via `AuthorizeMinterSigning`; every other `NeverCrossProcessSignableDomains` member (the CA trust anchors) is denied for EVERY origin (`CrossProcessDomainRejected`); every other domain requires `CrossProcessHop` + an authenticated peer + membership in the caller's allowed set. |
| `AuthorizeMinterSigning` | `RequestOrigin` only                                                    | The dedicated JWT-minter capability's own gate — requires `Origin == InProcessModule`; possession of the capability (registered only in the auth-module composition) plus this plane check IS the authority. |
| `AuthorizeSealEncrypt` | The caller's authenticated workload id (presence only)                   | Broad — any authenticated caller may fetch any public seal key (public material is harmless to over-share). |
| `AuthorizeSealDecrypt` | The caller's authenticated workload id (presence only)                   | Self-only, enforced by the op SHAPE (`getOwnSealPrivateKey()` carries no target) — no in-handler `caller == target` comparison exists because there is no target. |

`AuthorizeSigning` is the general `sign` op's chokepoint; `AuthorizeMinterSigning` is the dedicated `IJwtSigningCapability` minter's chokepoint. The two never overlap: the general surface categorically rejects `jwks-signing`, and the minter path never routes through `AuthorizeSigning` at all — closing the confused-deputy shape a bare `bool isCrossProcess` check could not express.

---

## Codegen output (Generated/)

The `error-codes-source-gen/` sibling project emits three files into `Generated/` at build time:

| File                              | Content                                                                     |
| --------------------------------- | --------------------------------------------------------------------------- |
| `KeyCustodianErrorCodes.g.cs`     | 23 `const string` error-code constants + `AllCodes` + `GetHttpStatus`        |
| `KeyCustodianFailures.g.cs`       | 23 `static D2Result FactoryName(...)` semantic factory methods                |
| `KeyCustodianFailures.Generic.g.cs` | The `KeyCustodianFailures<T>` typed twin                                  |

All transition methods and VO smart constructors use the generated `KeyCustodianFailures<T>.*` factories — never raw `D2Result.ValidationFailed(...)` with hand-written codes. See [`error-codes-source-gen/README.md`](../error-codes-source-gen/README.md).

---

## Dependencies

`D2.Shared.Result`, `D2.Shared.Utilities`, `D2.Shared.Time` (NodaTime `IClock` + `Instant`), `D2.Shared.Auth.Abstractions` (the domain-safe `RequestOrigin` enum `WorkloadCapabilityAuthority` keys its fail-closed decision on — pure enums/records only, so the domain dependency law holds), `D2.Shared.Encryption` (the `EncryptionDomains` catalog consumed by `KeyDomain`'s static catalog builder), `D2.Shared.I18n` (the generated `TK.*` keys — injected via the Tier-1 global using in `server/services/Directory.Build.targets`; not a direct `<ProjectReference>`), and the BCL `System.Security.Cryptography` (used by the `Rules/` generators + verifiers).

Zero EF Core, zero DI, zero I/O.

---

## Telemetry

N/A — pure-domain library; no OTel instruments, no DI, no meters or tracers.

## Configuration

N/A — pure-domain library; sizing tunables are method parameters passed by the App layer, not bound configuration.

## Usage

N/A — domain entities and rules are consumed directly by the App layer handlers; there is no standalone usage entry point.
