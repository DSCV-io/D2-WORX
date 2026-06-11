<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Edge.KeyCustodian.App

> Parent: [`server/services/edge/key-custodian/`](../README.md)

The application layer of the KeyCustodian — the EF-as-DDD command/query handlers that orchestrate the key lifecycle (generate / activate / rotate / retire / compromise), the JWKS + rotation-plan queries, the rotation-policy provider, and the App-owned ports + shapes (`IKeyCustodianDbContext` + flat records + pure mapper, the announcer + root-key-provider ports, the options). The pure crypto (generation, smoke testing, kid minting, JWK projection) lives in the domain `Rules/`; the immutable sum-type domain stays EF-free; the concrete `DbContext`, the keyed root crypto, the file-backed root-key provider, the rotation publisher, and the DI host wiring live in the Infra layer.

---

## Folder layout — two sections

```
app/
  Application/
    Handlers/
      Commands/<Operation>/   per-op folder: I<Op>Handler.cs, <Op>Handler.cs, <Op>Input.cs[, <Op>Output.cs]
      Queries/<Operation>/     same shape for the read operations
    Observability/             KeyCustodianLog (+ metrics)
    KeyCustodianAppServiceCollectionExtensions.cs   AddD2KeyCustodianApp()
  Infrastructure/
    Persistence/               IKeyCustodianDbContext + flat records + mapper + query extensions
    Messaging/                 IKeyRotationAnnouncer (port)
    Vault/                     IRootKeyProvider (port) + KeyCustodianRootKey (keyed-DI discriminator)
    Configuration/             KeyCustodianOptions + RotationPolicyOptions + IRotationPolicyProvider (+ options impl)
```

`Application/` is what the service *does*; `Infrastructure/` is what it *needs from the outside* (ports + the shapes those ports speak). The `infra/` project mirrors the `Infrastructure/` concern folders with the concrete adapters.

---

## Persistence — flat record + pure mapper (EF-as-DDD, Shape B)

The domain models a key as an immutable sum type (`EncryptionKey` + 5 sealed states). EF Core cannot morph a tracked entity's CLR type on a transition, so persistence uses a flat, non-polymorphic record whose type never changes:

- **`KeyRecord`** — one row per key. Primitive / closed-enum columns only (no value converters): `Kid` (PK), `KeyDomain`, `KeyType`, `KeyMaterialEncrypted` (root-wrapped, never logged), `PublicKeyMaterial` (SPKI, loggable), `CreatedAt`, a settable `Status` value column (NOT a TPH discriminator), the nullable per-state timestamps (`ActivatedAt` / `RetiringAt` / `RetiredAt` / `CompromisedAt`), `CompromiseReason`, and `Xmin` (the PostgreSQL concurrency token, mapped in Infra).
- **`KeyAuditRecord`** — flat, append-only audit row (identity PK). Carries the kid, action, resulting status, timestamp, and an OPTIONAL non-PII `Detail` breadcrumb — never key material, never the raw compromise reason.
- **`KeyRecordMapper`** — pure static extension members: `ToDomain()` (exhaustive `switch` on `Status` → the right sealed state via the domain's `FromTrusted` factories; a structurally corrupt row throws the trusted-store-corruption `InvalidOperationException`), `ProjectOnto(record)` (nulls EVERY per-state column first, then sets only the current state's — the anti-stale-column discipline), `ToNewRecord()` (INSERT path), and the audit `ToRecord()`.
- **`KeyRecordQueryExtensions`** — composable server-side filters on value columns (`Pending` / `Active` / `Retiring` / `Live` / `ForDomain` / `Signing`).
- **`IKeyCustodianDbContext`** — the only seam Infra implements for App: `DbSet<KeyRecord> Keys`, `DbSet<KeyAuditRecord> Audit`, `SaveChangesAsync`.

A command handler loads a tracked `KeyRecord`, rehydrates the aggregate through `ToDomain()`, invokes the domain transition, projects the result back with `ProjectOnto`, appends an audit row, and calls `SaveChangesAsync` once — one ordinary UPDATE plus the audit INSERT in a single transaction.

---

## Handlers

Each operation lives in its own folder under `Application/Handlers/{Commands,Queries}/<Operation>/`, co-locating the interface (`I<Op>Handler`), the implementation (`<Op>Handler`), the input, and (where the output is operation-specific) the output. A handler's category is determined solely by whether it mutates persistent/shared state.

| Handler                | Category   | Base              | Input → Output                          | Intent |
| ---------------------- | ---------- | ----------------- | --------------------------------------- | ------ |
| `GenerateKeyHandler`   | `Commands` | `BaseRepoHandler` | `GenerateKeyInput` → `KeySummary`       | Generate + root-wrap + persist a new pending key (rejects a second live pending key). |
| `ActivateKeyHandler`   | `Commands` | `BaseRepoHandler` | `ActivateKeyInput` → `KeySummary`       | Smoke-test + activate a soaked pending key (bootstrap / post-compromise). |
| `RotateKeyHandler`     | `Commands` | `BaseRepoHandler` | `RotateKeyInput` → `RotateKeyOutput`    | Atomic swap: incumbent → retiring AND successor → active in one transaction, then announce. |
| `RetireKeyHandler`     | `Commands` | `BaseRepoHandler` | `RetireKeyInput` → `KeySummary`         | Retire a retiring key whose grace window elapsed. |
| `CompromiseKeyHandler` | `Commands` | `BaseRepoHandler` | `CompromiseKeyInput` → `CompromiseKeyOutput` | Mark compromised + auto-generate a replacement pending + urgent announce. |
| `GetJwksHandler`       | `Queries`  | `BaseHandler`     | `GetJwksInput` → `GetJwksOutput`        | Assemble the RFC 7517 JWKS from active + retiring signing keys (active first). |
| `GetRotationPlanHandler`| `Queries` | `BaseHandler`     | `GetRotationPlanInput` → `GetRotationPlanOutput` | Report the lifecycle actions due across all domains (pure read). |

`KeySummary` is a shared domain projection (`domain/Rules/KeySummary.cs`) returned by the generate / activate / retire commands; the other operations declare an operation-specific `<Op>Output`. Every command handler validates input at the top via the domain `Create` / transition smart constructors (`Kid.Create`, `KeyDomain.Create`), surfaces failures only via the generated `KeyCustodianFailures.*` factories + the domain's results, and checks every nested result. Outputs carry NO key material.

---

## Lifecycle behaviors

- **Atomic rotation (`RotateKey`).** The incumbent → retiring and successor → active transitions land in ONE `SaveChangesAsync`, so there is never a window with no active signing key. The standalone `ActivateKey` remains for bootstrap and post-compromise activation.
- **Compromise (`CompromiseKey`).** Marks the key compromised, auto-generates a replacement pending key for the same domain (toggle via the input flag), and announces urgently (the announce carries the session-invalidation signal). The replacement soaks normally — there is no emergency no-soak activation here.
- **Announce-after-commit.** The rotation / compromise announcement runs AFTER the durable commit. A failed announce is logged (sanitized, no raw exception) and the handler still returns success — the transition is durable and consumers self-heal via keyring TTL refresh.

---

## Domain rules the handlers call

The pure crypto-over-domain logic lives in `domain/Rules/` and is called directly by the handlers (no port, no DI):

- **`KeyGeneration.Generate(KeyType, rsaModulusBits, secretLengthBytes)`** — one static dispatcher per `KeyType`; returns `D2Result<GeneratedKeyMaterial>` (unknown `KeyType` → `KEYCUSTODIAN_PRECONDITION_VIOLATED`, never a throw). The handler reads RSA size + secret length from `IOptions<KeyCustodianOptions>` and passes them in. `GeneratedKeyMaterial.Zero()` wipes the plaintext after wrapping.
- **`SmokeTesting.Verify(...)`** — RSA sign/verify, AES-GCM round-trip, HMAC usability; returns a `D2Result` (never throws on bad material).
- **`KidMinting.Mint()`** — 16 random bytes → unpadded base64url, JWKS-safe.
- **`JwkProjection.ToJwk(...)`** — SPKI bytes → RFC 7517 `Jwk`.

## App-owned ports + shapes (`Infrastructure/`)

- **`IRootKeyProvider`** (`Infrastructure/Vault/`) — port for the root keyring; the file-backed implementation is Infra's. **`KeyCustodianRootKey.ROOT_SERVICE_KEY`** is the keyed-services discriminator handlers inject the root `IPayloadCrypto` under.
- **`KeyCustodianOptions` / `RotationPolicyOptions`** (`Infrastructure/Configuration/`) — the default + per-domain rotation policies (`TimeSpan` for config binding) + generator sizing.
- **`IRotationPolicyProvider`** (`OptionsRotationPolicyProvider`, `Infrastructure/Configuration/`) — converts `TimeSpan` → `Duration` and validates through `RotationPolicy.Create`; an invalid configured policy surfaces `KEYCUSTODIAN_INVALID_ROTATION_POLICY`. The one defensible "impl lives in App" case — it reads `IOptions`, touches no vendor SDK or IO.
- **`IKeyRotationAnnouncer`** (`Infrastructure/Messaging/`) — the domain-shaped publisher port; App references no messaging library. Infra implements it over the message bus.

---

## PII / key-material safety

- Key material lives only in `KeyMaterialEncrypted` (root-wrapped) — never in inputs, outputs, audit rows, or logs. Freshly-generated plaintext is zeroed immediately after wrapping; unwrapped material is zeroed after smoke-testing.
- `CompromiseKeyInput.Reason` is `[RedactData(PersonalInformation)]` and its `ToString` / `PrintMembers` are overridden so the reason never appears in logs or handler I/O traces. The compromise audit row carries a non-sensitive breadcrumb (`"operator-initiated"`), never the raw reason.
- `KeyCustodianLog` (`Application/Observability/`, `[LoggerMessage]`, EventIds 9500–9529) accepts no `Exception` parameter; kid + domain are loggable by design.

---

## DI

`services.AddD2KeyCustodianApp()` registers the 7 handlers (transient) and the policy provider. Key generation + smoke testing are pure domain rules with no DI, so there are no generator / smoke-tester registrations. The seams App depends on but does not own — the concrete `IKeyCustodianDbContext`, the keyed root `IPayloadCrypto`, `IRootKeyProvider`, and `IKeyRotationAnnouncer` — are registered by the Infra layer, along with the options binding + startup validation.

---

## Configuration

`KeyCustodianOptions` binds from the `KeyCustodian` configuration section (`KeyCustodianOptions.SECTION = "KeyCustodian"`).

### Key-generator sizing

| Property           | Type  | Default | Notes                                                                      |
| ------------------ | ----- | ------- | -------------------------------------------------------------------------- |
| `RsaKeySizeBits`   | `int` | `2048`  | RSA modulus size for signing keys. Valid range: 2048+ (enforced by Infra `ValidateOnStart`). |
| `SecretLengthBytes`| `int` | `64`    | Length of generated opaque secret keys in bytes. Valid range: 16+ (enforced by Infra `ValidateOnStart`). |

### Rotation policies

Rotation policies use `TimeSpan` fields so they bind cleanly from `IConfiguration` (e.g. `"30.00:00:00"` = 30 days). The `OptionsRotationPolicyProvider` converts each `TimeSpan` to a NodaTime `Duration` and validates through `RotationPolicy.Create`.

#### `Default` — applies to any domain without an explicit override

| Property    | Type       | Notes                                                        |
| ----------- | ---------- | ------------------------------------------------------------ |
| `Cadence`   | `TimeSpan` | How often a key is rotated (activation-to-rotation window). Must be > Grace + SmokeSoak. |
| `Grace`     | `TimeSpan` | How long a retiring key remains in service after a new key activates. |
| `SmokeSoak` | `TimeSpan` | How long a generated key must soak before it may be activated. |

#### `Policies` — per-domain overrides

A `Dictionary<string, RotationPolicyOptions>` keyed by the normalized domain string (e.g. `"jwks-signing"`, `"cookie"`, `"client-secret"`). A domain absent from this map uses `Default`.

Example `appsettings.json` section:

```json
"KeyCustodian": {
  "RsaKeySizeBits": 2048,
  "SecretLengthBytes": 64,
  "Default": {
    "Cadence":   "30.00:00:00",
    "Grace":     "2.00:00:00",
    "SmokeSoak": "1.00:00:00"
  },
  "Policies": {
    "jwks-signing": {
      "Cadence":   "7.00:00:00",
      "Grace":     "4.00:00:00",
      "SmokeSoak": "0.02:00:00"
    }
  }
}
```

---

## Operations / debugging

**Rotation plan**: call `GetRotationPlan` (query handler) to see the lifecycle actions due across all domains — keys approaching their cadence window, retiring keys whose grace window has elapsed, and any pending keys that have soaked long enough to activate.

**Bootstrap sequence**: a new domain needs at least one key in the `Active` state before rotation can proceed. The typical bootstrap flow is: `GenerateKey` → wait for smoke-soak → `ActivateKey`. The Infra layer provides a startup health check that reports whether each configured domain has an active key.

**Compromise recovery**: `CompromiseKey` marks the incumbent compromised, optionally auto-generates a replacement pending key for the same domain, and announces urgently (the announce signal triggers session invalidation for tokens signed by the compromised key). The replacement soaks normally — there is no emergency no-soak path. After the soak window elapses, `ActivateKey` the replacement.

**Key material never appears in logs**: `KeyCustodianLog` (`[LoggerMessage]`, EventIds 9500–9529) accepts no `Exception` parameter. `kid` and `domain` are loggable. All other material-carrying fields (`KeyMaterialEncrypted`, `CompromiseKeyInput.Reason`) are `[RedactData]`-protected and their types override `ToString`/`PrintMembers` to emit redaction sentinels.

**Announce failures are non-fatal**: if the RabbitMQ announce fails after a durable commit, the handler logs (sanitized, no raw exception) and returns success. Consumers self-heal via keyring TTL refresh. Persistent announce failures indicate a messaging infrastructure problem, not a KC domain problem.
