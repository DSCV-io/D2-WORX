<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Edge.KeyCustodian.App

> Parent: [`server/services/edge/key-custodian/`](../README.md)

For engineers implementing or extending KeyCustodian operations, ports, or observability. The application layer of the KeyCustodian — the EF-as-DDD command/query handlers that orchestrate the key lifecycle (generate / activate / rotate / retire / compromise), the JWKS + rotation-plan queries, the rotation-policy provider, and the App-owned ports + shapes (`IKeyCustodianDbContext` + flat records + pure mapper, the announcer + root-key-provider ports, the options). The pure crypto (generation, smoke testing, kid minting, JWK projection) lives in the domain `Rules/`; the immutable sum-type domain stays EF-free; the concrete `DbContext`, the keyed root crypto, the file-backed root-key provider, the rotation publisher, and the DI host wiring live in the Infra layer.

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
- **`KeyAuditRecord`** — flat, append-only audit record (identity PK). Carries the kid, action, resulting status, timestamp, and an OPTIONAL non-PII `Detail` breadcrumb — never key material, never the raw compromise reason.
- **`KeyRecordMapper`** — pure static extension members: `ToDomain()` (exhaustive `switch` on `Status` → the right sealed state via the domain's `FromTrusted` factories; a structurally corrupt row throws the trusted-store-corruption `InvalidOperationException`), `ProjectOnto(record)` (nulls EVERY per-state column first, then sets only the current state's — the anti-stale-column discipline), `ToNewRecord()` (INSERT path), and the audit `ToRecord()`.
- **`KeyRecordQueryExtensions`** — composable server-side filters on value columns (`Pending` / `Active` / `Retiring` / `Live` / `ForDomain` / `Signing`).
- **`IKeyCustodianDbContext`** — the only seam Infra implements for App: `DbSet<KeyRecord> Keys`, `DbSet<KeyAuditRecord> Audit`, `SaveChangesAsync`.

A command handler loads a tracked `KeyRecord`, rehydrates the aggregate through `ToDomain()`, invokes the domain transition, projects the result back with `ProjectOnto`, appends an audit record, and calls `SaveChangesAsync` once — one ordinary UPDATE plus the audit INSERT in a single transaction.

---

## Handlers

Each operation lives in its own folder under `Application/Handlers/{Commands,Queries}/<Operation>/`, co-locating the interface (`I<Op>Handler`), the implementation (`<Op>Handler`), the input, and (where the output is operation-specific) the output. A handler's category is determined solely by whether it mutates persistent/shared state.

| Handler                  | Category   | Base              | Input → Output                                     | Intent |
| ----------------------   | ---------- | ----------------- | -------------------------------------------------- | ------ |
| `GenerateKeyHandler`     | `Commands` | `BaseRepoHandler` | `GenerateKeyInput` → `KeySummary`                  | Generate + root-wrap + persist a new pending key (rejects a second live pending key). |
| `ActivateKeyHandler`     | `Commands` | `BaseRepoHandler` | `ActivateKeyInput` → `KeySummary`                  | Smoke-test + activate a soaked pending key (bootstrap / post-compromise). |
| `RotateKeyHandler`       | `Commands` | `BaseRepoHandler` | `RotateKeyInput` → `RotateKeyOutput`               | Atomic swap: incumbent → retiring AND successor → active in one transaction, then announce. |
| `RetireKeyHandler`       | `Commands` | `BaseRepoHandler` | `RetireKeyInput` → `KeySummary`                    | Retire a retiring key whose grace window elapsed. |
| `CompromiseKeyHandler`   | `Commands` | `BaseRepoHandler` | `CompromiseKeyInput` → `CompromiseKeyOutput`       | Mark compromised + auto-generate a replacement pending + urgent announce. |
| `GetJwksHandler`         | `Queries`  | `BaseHandler`     | `GetJwksInput` → `GetJwksOutput`                   | Assemble the RFC 7517 JWKS from active + retiring signing keys (active first). |
| `GetRotationPlanHandler` | `Queries`  | `BaseHandler`     | `GetRotationPlanInput` → `GetRotationPlanOutput`   | Report the lifecycle actions due across all domains (pure read). |

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

- Key material lives only in `KeyMaterialEncrypted` (root-wrapped) — never in inputs, outputs, audit records, or logs. Freshly-generated plaintext is zeroed immediately after wrapping; unwrapped material is zeroed after smoke-testing.
- `CompromiseKeyInput.Reason` is `[RedactData(PersonalInformation)]` and its `ToString` / `PrintMembers` are overridden so the reason never appears in logs or handler I/O traces. The compromise audit record carries a non-sensitive breadcrumb (`"operator-initiated"`), never the raw reason.
- `KeyCustodianLog` (`Application/Observability/`, `[LoggerMessage]`, EventIds 9500–9529) accepts no `Exception` parameter; kid + domain are loggable by design.

---

## Telemetry

`KeyCustodianMetrics` (`Application/Observability/`) declares the domain-level OTel counters on meter **`D2.Edge.KeyCustodian`** (`METER_NAME = "D2.Edge.KeyCustodian"`). Hosts register the meter via `.WithMetrics(m => m.AddMeter(KeyCustodianMetrics.METER_NAME))`.

| Counter | Unit | Tags | Intent |
| ------- | ---- | ---- | ------ |
| `d2.keycustodian.compromises` | `{compromise}` | — | Incremented after each successful durable key-compromise commit. |
| `d2.keycustodian.announce_failures` | `{failure}` | `urgent` (`"true"` = compromise announce, `"false"` = routine rotation) | Post-commit announce failures. Non-zero `urgent="true"` triggers session-invalidation SLO alerting. |
| `d2.keycustodian.key_generations` | `{generation}` | — | Incremented after each successful `GenerateKey` commit. |
| `d2.keycustodian.smoke_test_failures` | `{failure}` | — | Smoke-test failures on activation/rotation attempts. A sustained non-zero rate indicates crypto-subsystem degradation. |
| `d2.keycustodian.empty_jwks_served` | `{response}` | — | `GetJwks` responses that found zero signing keys and returned 503. Any non-zero value is critical — JWT verification is broken cluster-wide. |

These counters complement the cross-cutting per-handler invocation/failure counters that `BaseHandler` already increments; they surface domain-semantic lifecycle events that dashboards alert on independently.

---

## DI

`services.AddD2KeyCustodianApp()` registers the 7 handlers (transient) and the policy provider. Key generation + smoke testing are pure domain rules with no DI, so there are no generator / smoke-tester registrations. The seams App depends on but does not own — the concrete `IKeyCustodianDbContext`, the keyed root `IPayloadCrypto`, `IRootKeyProvider`, and `IKeyRotationAnnouncer` — are registered by the Infra layer, along with the options binding + startup validation.

---

## Dependencies

Project references:

- `D2.Edge.KeyCustodian.Domain` — the aggregates, sealed state types, value objects, enums, and generated error-code factories that App orchestrates.
- `D2.Shared.Handler` — `BaseHandler<TSelf, TInput, TOutput>` for the query handlers (`GetJwks`, `GetRotationPlan`); cross-cutting telemetry, metrics, D2Result, and cancellation.
- `D2.Shared.Handler.Repo` — `BaseRepoHandler` for the command handlers; DB-exception → D2Result mapping via the injected `IDbExceptionClassifier`; brings `Microsoft.EntityFrameworkCore` + `D2.Shared.Result` transitively, but each is listed explicitly.
- `D2.Shared.Result` — `D2Result<T>` semantic factories (`Ok`, `NotFound`, `ValidationFailed`, `Conflict`, etc.) used directly by command/query handlers.
- `D2.Shared.Encryption` — `IPayloadCrypto` for root-wrapping `KeyMaterialEncrypted` in command handlers; `PayloadCryptoKeyring` resolution via keyed DI.
- `D2.Shared.Time` — `IClock` for rotation-planner due-key math and transition timestamps inside command handlers.
- `D2.Shared.Utilities` — `[RedactData]` on `CompromiseKeyInput.Reason` + `Falsey()` input guards. Carried transitively via Domain but listed explicitly ("declare what you use").
- `D2.Shared.I18n.Keys` — the generated `TK.*` translation-key constants passed to `D2Result` factories (e.g. precondition arg-naming messages).

Package references:

- `Microsoft.EntityFrameworkCore` (10.0.7) — `IKeyCustodianDbContext` exposes `DbSet<…>` + `SaveChangesAsync`; App owns the DbContext interface; Infra provides the concrete implementation.
- `Microsoft.Extensions.Options` (10.0.7) — `IOptions<KeyCustodianOptions>` consumed by the generate/compromise handlers and the rotation-policy provider.
- `Microsoft.Extensions.DependencyInjection.Abstractions` (10.0.7) — `AddD2KeyCustodianApp()` DI extension + `[FromKeyedServices]` root-crypto injection attribute.

---

## Configuration

`KeyCustodianOptions` binds from the `KEYCUSTODIAN_APP` configuration section (`KeyCustodianOptions.SECTION = "KEYCUSTODIAN_APP"`). Environment variables use the `KEYCUSTODIAN_APP__` prefix with `__` as the IConfiguration hierarchy separator.

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
| `Cadence`   | `TimeSpan` | How often a key is rotated (activation-to-rotation window). Must be ≥ 1 second and ≥ Grace + SmokeSoak (validated by `RotationPolicy.Create`). |
| `Grace`     | `TimeSpan` | How long a retiring key remains in service after a new key activates. Must be ≥ 1 second. |
| `SmokeSoak` | `TimeSpan` | How long a generated key must soak before it may be activated. Must be ≥ 1 second. |

#### `Policies` — per-domain overrides

A `Dictionary<string, RotationPolicyOptions>` keyed by the normalized domain string (e.g. `"jwks-signing"`, `"cookie"`, `"client-secret"`). A domain absent from this map uses `Default`.

Example environment variables (`__` maps to IConfiguration hierarchy):

```bash
KEYCUSTODIAN_APP__RSAKEYSIZEBITS=2048
KEYCUSTODIAN_APP__SECRETLENGTHBYTES=64
KEYCUSTODIAN_APP__DEFAULT__CADENCE=30.00:00:00
KEYCUSTODIAN_APP__DEFAULT__GRACE=2.00:00:00
KEYCUSTODIAN_APP__DEFAULT__SMOKESOAK=1.00:00:00
KEYCUSTODIAN_APP__POLICIES__JWKS-SIGNING__CADENCE=7.00:00:00
KEYCUSTODIAN_APP__POLICIES__JWKS-SIGNING__GRACE=4.00:00:00
KEYCUSTODIAN_APP__POLICIES__JWKS-SIGNING__SMOKESOAK=0.02:00:00
```

---

## Operations / debugging

**Rotation plan**: call `GetRotationPlan` (query handler) to see the lifecycle actions due across all domains — keys approaching their cadence window, retiring keys whose grace window has elapsed, and any pending keys that have soaked long enough to activate.

**Bootstrap sequence**: a new domain needs at least one key in the `Active` state before rotation can proceed. The typical bootstrap flow is: `GenerateKey` → wait for smoke-soak → `ActivateKey`. A startup health check reporting whether each configured domain has an active key is an Infra-layer concern — NOT IMPLEMENTED (see [`infra/README.md`](../infra/README.md)).

**Compromise recovery**: `CompromiseKey` marks the incumbent compromised, optionally auto-generates a replacement pending key for the same domain, and announces urgently (the announce signal triggers session invalidation for tokens signed by the compromised key). The replacement soaks normally — there is no emergency no-soak path. After the soak window elapses, `ActivateKey` the replacement.

**Key material never appears in logs**: `KeyCustodianLog` (`[LoggerMessage]`, EventIds 9500–9529) accepts no `Exception` parameter. `kid` and `domain` are loggable. All other material-carrying fields (`KeyMaterialEncrypted`, `CompromiseKeyInput.Reason`) are `[RedactData]`-protected and their types override `ToString`/`PrintMembers` to emit redaction sentinels.

**Announce failures are non-fatal**: if the RabbitMQ announce fails after a durable commit, the handler logs (sanitized, no raw exception) and returns success. Consumers self-heal via keyring TTL refresh. Persistent announce failures indicate a messaging infrastructure problem, not a KC domain problem.
