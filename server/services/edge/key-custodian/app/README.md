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
    Facade/                    KeyCustodianApi.g.cs + KeyCustodianClientGenerated.g.cs (generated façade impl + AddD2KeyCustodianClient())
    Signing/                   KeyDomainSigner + JwtSigningCapability + JwtSigningCapabilityServiceCollectionExtensions
    Issuance/                  ICaLeafSigningCapability + CaLeafSigningCapability + CaSignedLeaf + its DI extension (the isolated CA-leaf signer)
    CertificateAuthority/      CaSuccessorFactory (successor-key orchestrator serving GenerateKey / CompromiseKey / RunDueRotations) + ICaRootSigningCapability + CaRootSigningCapability + its DI extension (the isolated CA-root signer — the sole holder of EVERY stored mtls-ca-root plaintext use, §9.44)
    Keyring/                   InProcessKeyringClient + KeyringConsumerServiceCollectionExtensions (the in-process keyring consumer source)
    Sealing/                   SealKeyProvisioning + SealKeyServingSet (the shared load-or-lazily-provision path both seal ops call)
    Observability/             KeyCustodianLog (+ metrics)
    KeyCustodianAppServiceCollectionExtensions.cs   AddD2KeyCustodianApp()
  Infrastructure/
    Persistence/               IKeyCustodianDbContext + flat records + mapper + query extensions
    Messaging/                 IKeyRotationAnnouncer (port)
    Vault/                     IRootKeyProvider (port) + KeyCustodianRootKey (keyed-DI discriminator)
    Configuration/             KeyCustodianOptions + RotationPolicyOptions + IRotationPolicyProvider (+ options impl)
```

`Handlers/` + `Observability/` follow ADR-0020 exactly; the op-noun concern folders (`Signing/`, `Issuance/`, `CertificateAuthority/`, `Keyring/`, `Sealing/`) are siblings of `Handlers/` holding the app-side support types that serve one-or-more handlers or the consumer side (never nested inside `Handlers/<Op>/`), and `Facade/` holds the generated module-façade impl + registration. The layer root keeps only the composition-root extension.

`Application/` is what the service *does*; `Infrastructure/` is what it *needs from the outside* (ports + the shapes those ports speak). The `infra/` project mirrors the `Infrastructure/` concern folders with the concrete adapters.

---

## Persistence — flat record + pure mapper (EF-as-DDD, Shape B)

The domain models a key as an immutable sum type (`EncryptionKey` + 5 sealed states). EF Core cannot morph a tracked entity's CLR type on a transition, so persistence uses a flat, non-polymorphic record whose type never changes:

- **`KeyRecord`** — one row per key. Primitive / closed-enum columns only (no value converters): `Kid` (PK), `KeyDomain`, `KeyType`, `KeyMaterialEncrypted` (root-wrapped, never logged), `PublicKeyMaterial` (SPKI / certificate PEM, loggable), `CreatedAt`, a settable `Status` value column (NOT a TPH discriminator), the nullable per-state timestamps (`ActivatedAt` / `RetiringAt` / `RetiredAt` / `CompromisedAt`), `CompromiseReason`, and `Xmin` (the PostgreSQL concurrency token, mapped in Infra).
- **`KeyAuditRecord`** — flat, append-only audit record (identity PK). Carries the kid, action, resulting status, timestamp, and an OPTIONAL non-PII `Detail` breadcrumb — never key material, never the raw compromise reason.
- **`LeafIssuanceAuditRecord`** — flat, append-only record for each workload leaf-certificate issuance (identity PK). Carries `WorkloadServiceId`, `IssuingCaKid` (FK → `KeyRecord.Kid`), `IssuedAt`, and `LeafNotAfter`. A leaf is issued on demand and is NOT a managed-key aggregate — only the issuance audit entry is persisted. Never key material, never the leaf private key.
- **`KeyRecordMapper`** — pure static extension members: `ToDomain()` (exhaustive `switch` on `Status` → the right sealed state via the domain's `FromTrusted` factories; a structurally corrupt row throws the trusted-store-corruption `InvalidOperationException`), `ProjectOnto(record)` (nulls EVERY per-state column first, then sets only the current state's — the anti-stale-column discipline), `ToNewRecord()` (INSERT path), the key-audit `ToRecord()`, and the leaf-issuance-audit `ToRecord()`.
- **`KeyRecordQueryExtensions`** — composable server-side filters on value columns (`Pending` / `Active` / `Retiring` / `Live` / `ForDomain` / `Signing` / `Payload` / `Sealing`).
- **`IKeyCustodianDbContext`** — the only seam Infra implements for App: `DbSet<KeyRecord> Keys`, `DbSet<KeyAuditRecord> Audit`, `DbSet<LeafIssuanceAuditRecord> LeafIssuanceAudit`, `SaveChangesAsync`.

A command handler loads a tracked `KeyRecord`, rehydrates the aggregate through `ToDomain()`, invokes the domain transition, projects the result back with `ProjectOnto`, appends an audit record, and calls `SaveChangesAsync` once — one ordinary UPDATE plus the audit INSERT in a single transaction.

---

## Handlers

Each operation lives in its own folder under `Application/Handlers/{Commands,Queries}/<Operation>/`, co-locating the interface (`I<Op>Handler`), the implementation (`<Op>Handler`), the input, and (where the output is operation-specific) the output. A handler's category is determined solely by whether it mutates persistent/shared state.

| Handler                            | Category   | Base              | Input → Output                                              | Intent |
| ---------------------------------- | ---------- | ----------------- | ----------------------------------------------------------- | ------ |
| `GenerateKeyHandler`               | `Commands` | `BaseRepoHandler` | `GenerateKeyInput` → `KeySummary`                           | Generate + root-wrap + persist a new pending key (rejects a second live pending key, and rejects a `(domain, type)` pair that disagrees with the domain's bound `KeyType` — `KEYCUSTODIAN_KEY_TYPE_DOMAIN_MISMATCH`). |
| `ActivateKeyHandler`               | `Commands` | `BaseRepoHandler` | `ActivateKeyInput` → `KeySummary`                           | Smoke-test + activate a soaked pending key (bootstrap / post-compromise). |
| `RotateKeyHandler`                 | `Commands` | `BaseRepoHandler` | `RotateKeyInput` → `RotateKeyOutput`                        | Atomic swap: incumbent → retiring AND successor → active in one transaction, then announce. |
| `RetireKeyHandler`                 | `Commands` | `BaseRepoHandler` | `RetireKeyInput` → `KeySummary`                             | Retire a retiring key whose grace window elapsed. |
| `CompromiseKeyHandler`             | `Commands` | `BaseRepoHandler` | `CompromiseKeyInput` → `CompromiseKeyOutput`                | Mark compromised (the durable kill commits first, in its own save) + urgent announce, then a best-effort replacement pending (skipped mid-rotation; a build/save failure is logged, never a rollback). |
| `IssueWorkloadCertificateHandler`  | `Commands` | `BaseRepoHandler` | `IssueWorkloadCertificateInput` → `IssueWorkloadCertificateOutput` | The CSR-flow issuance chokepoint BOTH planes flow through. Gates in order: the per-handler `internal.kc.issue` `ScopeRequirement`; the fail-closed `WorkloadCertificateAuthority.AuthorizeIssuance` (cross-process-only, authenticated-peer-required — denies fire the `capability = issuance` telemetry); the pure `CsrVerification` (size cap / PKCS#10 parse / proof-of-possession / P-256 curve OID → the uniform 400 `KEYCUSTODIAN_INVALID_CSR`). Then it derives the leaf SAN from the AUTHENTICATED peer (`ImmediateCaller` — the CSR's subject is structurally ignored), signs via the isolated `ICaLeafSigningCapability` (503 when no active intermediate), writes the `LeafIssuanceAuditRecord`, increments the issued counter, and logs `LeafCertificateIssued` (9515). Output = leaf + issuing-intermediate DER + validity — the leaf private key never exists inside KeyCustodian. |
| `IssueLeafHandler`                 | `Commands` | `BaseHandler`     | `IssueLeafInput` → `IssueLeafOutput` (generated wire DTOs)  | Thin shell for the generated `issueLeaf` op: maps `csrDer` → the inner input, delegates through `IIssueWorkloadCertificateHandler`'s FULL pipeline, maps the inner output → the wire DTO (`Instant` → `DateTimeOffset`). No second gate, no second telemetry site — inner denials bubble unchanged. |
| `SeedCertificateAuthorityHandler`  | `Commands` | `BaseRepoHandler` | `SeedCertificateAuthorityInput` → `SeedCertificateAuthorityOutput` | Idempotent bootstrap: loads the root + intermediate from `ICaProvider`, persists both as active `X509CaCertificate` managed keys (genuine pending → smoke-test → activate path), and writes the generated + activated audit entries in one `SaveChangesAsync`. No-op when both CA domains already hold an active key. |
| `GetJwksHandler`                   | `Queries`  | `BaseHandler`     | `GetJwksInput` → `GetJwksOutput`                            | Assemble the RFC 7517 JWKS from active + retiring signing keys (active first). |
| `GetOidcConfigurationHandler`      | `Queries`  | `BaseHandler`     | `GetOidcConfigurationInput` → `GetOidcConfigurationOutput`  | Serve the minimal OIDC discovery document (pure config read — no DB, no crypto) so OIDC/JWKS clients auto-discover the JWKS endpoint. |
| `GetRotationPlanHandler`           | `Queries`  | `BaseHandler`     | `GetRotationPlanInput` → `GetRotationPlanOutput`            | Report the lifecycle actions due across all domains (pure read). |
| `RunDueRotationsHandler`           | `Commands` | `BaseHandler`     | `RunDueRotationsInput` → `RunDueRotationsOutput`            | Orchestrate all due lifecycle actions across domains (bootstrap → activate → rotate → generate-successor → retire) by composing `GetRotationPlan` with the per-action command handlers; per-domain failures are isolated and counted in `Errors`. |
| `SignHandler`                      | `Queries`  | `BaseHandler`     | `SignInput` → `SignOutput`                                  | Load a key domain's active `RsaSigning` key, decrypt the private key via root crypto, sign the input (RS256) via the pure `RsaSigning` domain rule (zeroing the unwrapped key in a `finally`), and return the signature + kid. Authority-gated through `WorkloadCapabilityAuthority.AuthorizeSigning` on the established `IRequestContext.Origin` / `.ImmediateCaller` ([ADR-0025](../../../../../docs/adrs/0025-request-context-establishment.md)) — categorically rejects the `jwks-signing` root (reachable only via the dedicated `IJwtSigningCapability` minter seam) and the never-signable CA domains, then sharply rejects any domain whose bound `KeyType` is not `RsaSigning` with the permanent 400 `KEYCUSTODIAN_KEY_TYPE_DOMAIN_MISMATCH` (never the retryable 503). |
| `GetKeyringHandler`                | `Queries`  | `BaseHandler`     | `GetKeyringInput` → `GetKeyringOutput`                      | Load a payload domain's Active + Retiring `AesPayload` keys (via `.Payload()`), root-unwrap each, and return the active kid + every entry (`{ kid, keyBytes }`, active-first then retiring newest-activated-first) + the domain's AAD context (`KeyringAadProjection.For`). Authority-gated through `WorkloadCapabilityAuthority.AuthorizeKeyringFetch` on the established `IRequestContext.Origin` / `.ImmediateCaller` ([ADR-0025](../../../../../docs/adrs/0025-request-context-establishment.md)) — serves only the cross-process-hop + in-process-module planes, per the caller's allowed-keyring-domains policy; no Active key → the retryable 503 `KEYCUSTODIAN_KEYRING_KEY_UNAVAILABLE` (even when Retiring rows exist). Authority runs BEFORE the sharp 400 `KEYCUSTODIAN_KEY_TYPE_DOMAIN_MISMATCH` key-type fork, which is defense-in-depth only (unreachable in production — no caller can hold a validator-forbidden non-payload grant). Custody of the raw AES bytes transfers to the caller; the `[RedactData(SecretInformation)]` on `KeyringEntry.KeyBytes` keeps them out of logs. |
| `GetCaCertificateHandler`          | `Queries`  | `BaseHandler`     | `GetCaCertificateInput` → `GetCaCertificateOutput` (generated wire DTOs) | Serve the CA chain — the active root (the trust anchor a workload pins) + the active issuing intermediate — as DER public certificate material. Gated by the per-handler `internal.kc.cacert` `ScopeRequirement` + `WorkloadCertificateAuthority.AuthorizeCaCertificateFetch` (cross-process + in-process planes only; broad within them — public material, no policy map; denies fire the `capability = ca-cert` telemetry). Both tiers REQUIRED: a missing / inactive / malformed tier is the retryable 503 `KEYCUSTODIAN_NO_ACTIVE_ISSUING_CA` + `CaCertificateUnavailable` (9514). No decrypt (certs are plaintext public columns), no DB write. |
| `GetOrLazyProvisionSealPublicKeyHandler`          | `Commands` | `BaseRepoHandler` | `GetOrLazyProvisionSealPublicKeyInput` → `GetOrLazyProvisionSealPublicKeyOutput` (generated wire DTOs) | Serve a target service's Active + Retiring PUBLIC sealing keys (SPKI, straight from the plaintext-at-rest column — no root-decrypt; active first then retiring newest-activated-first). Gates in order: the per-handler `internal.kc.seal.encrypt` `ScopeRequirement`; the fail-closed `WorkloadCapabilityAuthority.AuthorizeSealEncrypt` (cross-process + in-process planes, broad within them — denies fire `capability = seal-encrypt`, target `none`, BEFORE the serviceId validation so no validation oracle); then `WorkloadIdentity.Create` on the target serviceId (400 `KEYCUSTODIAN_INVALID_WORKLOAD_IDENTITY`). A `Command` — the first request LAZILY PROVISIONS the service's `EcdhSealing` keypair via the shared `SealKeyProvisioning` path; no Active key → the retryable 503 `KEYCUSTODIAN_SEAL_KEY_UNAVAILABLE` + `SealKeyUnavailable` (9517). |
| `GetOrLazyProvisionOwnSealPrivateKeyHandler`      | `Commands` | `BaseRepoHandler` | `GetOrLazyProvisionOwnSealPrivateKeyInput` (empty) → `GetOrLazyProvisionOwnSealPrivateKeyOutput` (generated wire DTOs) | Serve the CALLER'S OWN Active + Retiring PRIVATE sealing keys, root-unwrapped (`[RedactData(SecretInformation)]` on `PrivatePkcs8`). Gates in order: the per-handler `internal.kc.seal.open` `ScopeRequirement`; the fail-closed `WorkloadCapabilityAuthority.AuthorizeSealDecrypt` — CROSS-PROCESS ONLY (the hard gate: on the only served plane `ImmediateCaller` IS the unforgeable validated mTLS peer id; a forged in-process caller is denied at the plane arm and never reaches key selection; denies fire `capability = seal-decrypt`, target `none`). The op carries NO target — the key domain is `KeyDomain.ForSeal(ImmediateCaller)`, so impersonation is structurally unrepresentable. Same lazy provisioning + 503 semantics as `GetOrLazyProvisionSealPublicKeyHandler`. |

`KeySummary` is a shared domain projection (`domain/Rules/KeySummary.cs`) returned by the generate / activate / retire commands; the other operations declare an operation-specific `<Op>Output`. Every command handler validates input at the top via the domain `Create` / transition smart constructors (`Kid.Create`, `KeyDomain.Create`), surfaces failures only via the generated `KeyCustodianFailures.*` factories + the domain's results, and checks every nested result. Outputs carry NO key material.

**Lifecycle authority (System-plane-only, fail-closed).** Every lifecycle command handler (`GenerateKey`, `ActivateKey`, `RotateKey`, `RetireKey`, `CompromiseKey`, `RunDueRotations`, `SeedCertificateAuthority`) calls the pure `KeyLifecycleAuthority.AuthorizeLifecycleMutation(Context.Request.Origin)` rule at the TOP of `ExecuteAsync` — before any input validation. Only the in-host System plane (established by the scheduler workers via `EstablishSystemContext`) is admitted; `Unestablished` denies first with the specific origin-unestablished failure; every other established plane is `Forbidden`. Denies route through the shared `LifecycleAuthorityTelemetry.Deny` seam (`Application/Observability/`), which fires the `capability = lifecycle` authority-rejection counter + the `AuthorityRejected` log and bubbles the typed failure. The System plane carries no scopes by design, so these handlers deliberately declare no `ScopeRequirement` — the origin gate is the control; a future admin transport must consciously extend the rule and add its own per-op scope. This System-plane gate is KEPT as defense-in-depth BENEATH the §9.44 structural isolation: the four lifecycle-MUTATION handlers additionally require the dedicated `ICaRootSigningCapability` (registered only by `AddD2CaRootSigningCapability()`), so a host that does not opt in cannot construct them at all — a build-time DI fact, not a runtime branch.

---

## Lifecycle behaviors

- **Atomic rotation (`RotateKey`).** The incumbent → retiring and successor → active transitions land in ONE `SaveChangesAsync`, so there is never a window with no active signing key. The standalone `ActivateKey` remains for bootstrap and post-compromise activation.
- **Compromise (`CompromiseKey`).** The compromise transition (plus its audit) commits in its OWN `SaveChangesAsync` FIRST — the durable kill — so a compromised key can never stay live because a replacement could not be built or inserted. It then announces urgently (the announce carries the session-invalidation signal). Only then, when requested (toggle via the input flag), a replacement pending key for the same domain is generated as a best-effort follow-up in a separate save: it is skipped (and the pre-existing successor reported instead) when the domain is already mid-rotation, and a build failure or a second-save conflict is logged and yields a null replacement rather than rolling the compromise back. The replacement soaks normally — there is no emergency no-soak activation here.
- **Announce-after-commit.** The rotation / compromise announcement runs AFTER the durable commit. A failed announce is logged (sanitized, no raw exception) and the handler still returns success — the transition is durable and consumers self-heal via keyring TTL refresh.
- **Lazy seal provisioning (`SealKeyProvisioning`, `Application/Sealing/`).** The first seal-op request for a service's `seal:<serviceId>` domain generates an `EcdhSealing` keypair, smoke-tests it inline (a real self-seal→self-open round-trip), and activates it through the genuine pending→activate machinery with a back-dated `CreatedAt` (the soak's purpose — never serve an unproven key — is satisfied by the inline smoke test; the same sanctioned pattern the CA seeder uses), committing the Active row + its `Generated` + `Activated` audit entries in ONE transaction. Concurrent first-requests converge: a `SaveChanges` failure classified `UniqueViolation` (the one-Active EXCLUDE / one-Pending unique index) triggers ONE re-read that serves the winner's key — no conflict escapes to any caller. A re-read that finds Pending-only (or a live Pending blocking provisioning up front) is the retryable 503 `KEYCUSTODIAN_SEAL_KEY_UNAVAILABLE`; an orphaned Pending is operator-recoverable via the existing System-plane lifecycle ops. The path is structurally seal-only — it hard-codes `KeyType.EcdhSealing` under a `ForSeal`-resolved domain.

---

## Domain rules the handlers call

The pure crypto-over-domain logic lives in `domain/Rules/` and is called directly by the handlers (no port, no DI):

- **`KeyGeneration.Generate(KeyType, rsaModulusBits, secretLengthBytes)`** — one static dispatcher per `KeyType`; returns `D2Result<GeneratedKeyMaterial>` (unknown `KeyType` → `KEYCUSTODIAN_PRECONDITION_VIOLATED`, never a throw). The handler reads RSA size + secret length from `IOptions<KeyCustodianOptions>` and passes them in. `GeneratedKeyMaterial.Zero()` wipes the plaintext after wrapping.
- **`SmokeTesting.Verify(...)`** — RSA sign/verify, AES-GCM round-trip, HMAC usability, CA ECDSA sign/verify, and the `EcdhSealing` full self-seal→self-open round-trip through the sealed-encryption core; returns a `D2Result` (never throws on bad material).
- **`KidMinting.Mint()`** — 16 random bytes → unpadded base64url, JWKS-safe.
- **`JwkProjection.ToJwk(...)`** — SPKI bytes → RFC 7517 `Jwk`.
- **`RsaSigning.Sign(privatePkcs8, signingInput)`** — RS256 (RSASSA-PKCS1-v1_5 over SHA-256) sign over an already-unwrapped private key; returns `D2Result<string>` (base64url signature). BCL crypto only, never throws — a crypto import/sign failure surfaces as `KEYCUSTODIAN_PRECONDITION_VIOLATED`. Called from the App-internal `KeyDomainSigner.SignActiveKeyAsync` helper shared by `SignHandler` and the `JwtSigningCapability` minter.
- **`KeyringAadProjection.For(domain)`** — the single KC-side computation of a payload domain's AEAD additional-authenticated-data: the UTF-8 bytes of `"d2/<domain>"`, returned by `GetKeyringHandler` on the wire so a consumer binds its `PayloadCryptoKeyring` to the exact same AAD KeyCustodian computed. Authenticated-not-secret and stable-per-domain-for-life — the byte layout is frozen (a freeze test pins the literal bytes per payload domain), because changing the prefix / separator / encoding would make every value already encrypted under the old AAD fail to decrypt.
- **`WorkloadCapabilityAuthority.AuthorizeKeyringFetch(immediateCaller, origin, target, allowedKeyringDomainsForCaller)`** — the pure fail-closed keyring authority arm `GetKeyringHandler` calls at the top of `ExecuteAsync` (before the key-type fork). Layered: unestablished origin → 403 first-checked deny; plane deny (serves only `CrossProcessHop` + `InProcessModule`) → uniform 403; no caller identity on a served plane → `Forbidden`; target not in the caller's allowed set → the same uniform 403 `KEYCUSTODIAN_KEYRING_DOMAIN_NOT_AUTHORIZED`. The policy set is a method parameter (resolved by the handler from `IKeyringDomainAuthorityPolicy`), never an injected option.
- **`WorkloadCapabilityAuthority.AuthorizeSealEncrypt(immediateCaller, origin)` / `AuthorizeSealDecrypt(immediateCaller, origin)`** — the pure fail-closed seal arms the two seal handlers call FIRST. Both: unestablished origin → 403 first-checked deny; no caller identity on a served plane → `Forbidden`. Seal-encrypt serves `CrossProcessHop` + `InProcessModule` and is broad within them (no per-target policy — public material; the scope gates whether the caller may seal at all). Seal-decrypt serves `CrossProcessHop` ONLY (no unforgeable in-process identity exists, so an in-process private-key fetch is refused at the plane arm); any other plane → the uniform 403 `KEYCUSTODIAN_SEAL_NOT_AUTHORIZED`.
- **`KeyDomain.ForSeal(serviceId)`** — validates a raw service id against the shared workload-identity grammar and constructs the `seal:<serviceId>` domain (bound `KeyType.EcdhSealing`). The pattern-based seal family also resolves through `KeyDomain.Create` / `FromTrusted` (one resolution seam), so the lifecycle ops (rotate / compromise / retire) work on seal domains under the `Default` rotation policy.

## App-owned ports + shapes (`Infrastructure/`)

- **`IRootKeyProvider`** (`Infrastructure/Vault/`) — port for the root keyring; the file-backed implementation is Infra's. **`KeyCustodianRootKey.ROOT_SERVICE_KEY`** is the keyed-services discriminator handlers inject the root `IPayloadCrypto` under.
- **`KeyCustodianOptions` / `RotationPolicyOptions`** (`Infrastructure/Configuration/`) — the default + per-domain rotation policies (`TimeSpan` for config binding) + generator sizing.
- **`IRotationPolicyProvider`** (`OptionsRotationPolicyProvider`, `Infrastructure/Configuration/`) — converts `TimeSpan` → `Duration` and validates through `RotationPolicy.Create`; an invalid configured policy surfaces `KEYCUSTODIAN_INVALID_ROTATION_POLICY`. The one defensible "impl lives in App" case — it reads `IOptions`, touches no vendor SDK or IO.
- **`ISigningDomainAuthorityPolicy`** (`OptionsSigningDomainAuthorityPolicy`, `Infrastructure/Configuration/`) — resolves the set of signing key domains a cross-process workload may sign with (`AllowedSigningDomainsFor(workloadId)`); an unknown workload resolves to the EMPTY set (default-deny). `SignHandler` resolves this policy and passes the result into the pure `WorkloadCapabilityAuthority.AuthorizeSigning` rule. Binds from `SigningDomainAuthorityOptions` (`KEYCUSTODIAN_SIGNING_AUTHORITY` section); its `Validate()` fail-loud-refuses to boot if any `NeverCrossProcessSignableDomains` member (`jwks-signing`, `mtls-ca-root`, `mtls-ca-intermediate`) is ever granted to a workload.
- **`IKeyringDomainAuthorityPolicy`** (`OptionsKeyringDomainAuthorityPolicy`, `Infrastructure/Configuration/`) — resolves the set of payload key domains a caller may fetch a keyring for (`AllowedKeyringDomainsFor(workloadId)`); an unknown caller resolves to the EMPTY set (default-deny). `GetKeyringHandler` resolves this policy and passes the result into the pure `WorkloadCapabilityAuthority.AuthorizeKeyringFetch` rule. Unlike the signing policy (cross-process only), the caller key may be EITHER a cross-process SPIFFE workload id OR an in-process module id (both share the bare `[a-z0-9-]` grammar and both flow through `ImmediateCaller`). Binds from `KeyringDomainAuthorityOptions` (`KEYCUSTODIAN_KEYRING_AUTHORITY` section); its `Validate()` fail-loud-refuses to boot if ANY non-payload (non-`AesPayload`) domain — `jwks-signing` / `cookie` / `client-secret` / the `mtls-ca-*` trust anchors — is granted to any caller, because a keyring is a full encrypt+decrypt capability and only payload domains are keyring-grantable. An empty policy is valid (deny-all).
- **`IKeyRotationAnnouncer`** (`Infrastructure/Messaging/`) — the domain-shaped publisher port; App references no messaging library. Infra implements it over the message bus.

---

## PII / key-material safety

- Key material lives only in `KeyMaterialEncrypted` (root-wrapped) — never in inputs, audit records, or logs. Freshly-generated plaintext is zeroed immediately after wrapping; unwrapped material is zeroed after smoke-testing.
- **Two deliberate raw-key outputs.** `GetKeyringOutput.Entries[].KeyBytes` carries the root-UNWRAPPED AES-256 key on the wire (a full encrypt+decrypt capability handed to an authorized payload-domain consumer), and `GetOrLazyProvisionOwnSealPrivateKeyOutput.Entries[].PrivatePkcs8` carries the caller's OWN root-unwrapped ECDH private sealing key (self-only by op shape — no target parameter exists). Both are `[RedactData(SecretInformation)]`-masked so they never render in logs, but custody of the raw bytes transfers to the caller (which copies them into a defensive-copying + zeroing keyring — `PayloadCryptoKeyring` / `RecipientPrivateKeyring`); KeyCustodian does NOT zero them — the caller owns their lifetime. The keyring's companion `AadContext` and the seal ops' `PublicSpki` are authenticated-or-public-not-secret and deliberately NOT redacted, so operators can debug cross-service disagreements.
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
| `d2.keycustodian.leaf_certificates_issued` | `{certificate}` | — | Incremented after each successful `IssueWorkloadCertificate` commit (the durable commit of the leaf-issuance audit row). |
| `d2.keycustodian.no_active_issuing_ca` | `{response}` | — | Requests that found a certificate-authority tier missing and returned 503: an issuance with no active issuing intermediate, or a CA-certificate fetch with no active root / intermediate. A sustained non-zero rate means the CA has not been seeded or is between rotations — no workload can obtain a leaf or the trust anchor, so the mTLS mesh cannot form. |
| `d2.keycustodian.cross_process_signing_rejections` | `{rejection}` | — | Total general-surface `Sign` requests rejected for attempting to reach a crown-jewel key: the cluster-signing root (`jwks-signing`, the `MinterCapabilityRequired` arm) or a certificate-authority domain (`mtls-ca-root` / `mtls-ca-intermediate`, the never-signable `CrossProcessDomainRejected` arm). The highest-severity authority signal: any non-zero value means a caller tried to sign with a key that is structurally unreachable on the general surface (from ANY established origin). Pages on any non-zero value. |
| `d2.keycustodian.authority_rejections` | `{rejection}` | `capability` (`sign` / `lifecycle` / `keyring` / `issuance` / `ca-cert` / `seal-encrypt` / `seal-decrypt`), `reason` (`origin-unestablished` / `minter-required` / `never-signable` / `not-in-allowed-set` / `unauthorized-plane` / `identity-absent` / `not-in-process` / `not-system`) | Total capability-authority rejections across every capability — the broad dashboard counter complementing the specific `cross_process_signing_rejections` counter above. Both tag values are closed-enum named constants (`KeyCustodianMetrics.AuthorityRejections`) referenced at every emit site, never free text. `not-in-process` is minter-only; `not-system` is lifecycle-only (a lifecycle mutation attempted from a plane other than the in-host System worker plane); `unauthorized-plane` fires when a keyring / issuance / ca-cert request arrived on a plane that surface does not serve (the keyring policy miss uses `not-in-allowed-set`). Issuance and ca-cert are TARGETLESS capabilities — their `AuthorityRejected` log target is the closed-set `none` marker. |
| `d2.keycustodian.signing_key_unavailable` | `{response}` | — | `Sign` requests that found no active signing key for the requested domain and returned 503. A sustained non-zero rate means a signing domain has not been seeded or is mid-rotation with no active key — JWT minting for that domain is blocked until a key is active. |
| `d2.keycustodian.empty_keyring_served` | `{response}` | — | `GetKeyring` requests that found no active payload key for the requested domain and returned 503 — incremented only on the no-active path (even when Retiring rows exist). A sustained non-zero rate means a payload domain's keyring is unprovisioned or mid-rotation with no active key — payload encryption for that domain is blocked until a key is active. Mirrors `empty_jwks_served` for the keyring-distribution surface. |
| `d2.keycustodian.seal_keypairs_provisioned` | `{keypair}` | — | Per-service ECDH sealing keypairs provisioned lazily on first use by the seal ops (incremented after the provisioning transaction commits). A spike is expected when a new service first participates in sealed encryption; a sustained non-zero rate for an established service would indicate provisioning is not converging. The 503 no-active-key path logs `SealKeyUnavailable` (9517) and increments `seal_key_unavailable`; a provision logs `SealKeypairProvisioned` (9516 — service id, kid, triggering caller; never key bytes). |
| `d2.keycustodian.seal_key_unavailable` | `{response}` | — | Seal-key fetches (public or own-private) that found no active sealing key for the requested service and returned 503 `KEYCUSTODIAN_SEAL_KEY_UNAVAILABLE`. A sustained non-zero rate means a service's seal domain is unprovisioned or mid-rotation with no active key — sealing to or opening for that service is blocked until a key is active. Mirrors `empty_keyring_served` for the seal-distribution surfaces; the 503 path also logs `SealKeyUnavailable` (9517). |

These counters complement the cross-cutting per-handler invocation/failure counters that `BaseHandler` already increments; they surface domain-semantic lifecycle events that dashboards alert on independently.

---

## DI

`services.AddD2KeyCustodianApp()` registers the 17 handlers (transient) and the policy providers (rotation-policy + signing-domain-authority + keyring-domain-authority). Key generation + smoke testing are pure domain rules with no DI, so there are no generator / smoke-tester registrations. The seams App depends on but does not own — the concrete `IKeyCustodianDbContext`, the keyed root `IPayloadCrypto`, `IRootKeyProvider`, `IKeyRotationAnnouncer`, and `ICaProvider` — are registered by the Infra layer, along with the options binding + startup validation. The dedicated minter capability `IJwtSigningCapability` is registered SEPARATELY, via `AddD2JwtSigningCapability()`, called ONLY from the JWT minter's (auth module's) composition — never from `AddD2KeyCustodianApp()` / `AddD2KeyCustodianClient()`. The issuance leaf-signing capability `ICaLeafSigningCapability` (`Application/Issuance/`) follows the same discipline: registered SEPARATELY via `AddD2CaLeafSigningCapability()`, called ONLY from the composition root that serves the issuance surface — a provider built from the general registration alone cannot resolve it, so the issuance path is structurally absent from a host that does not opt in. The CA-root-signing capability `ICaRootSigningCapability` (`Application/CertificateAuthority/`) applies the same §9.44 discipline to the mesh trust anchor: registered SEPARATELY via `AddD2CaRootSigningCapability()` (the System-worker host's composition), it is the SOLE holder of every stored `mtls-ca-root` private-key plaintext use — both the intermediate-minting SIGN path (`CaSuccessorFactory` delegates the root→intermediate unwrap+sign to it) AND the root-domain SMOKE-VERIFY path (`ActivateKey` / `RotateKey` route a pending/successor `mtls-ca-root` smoke through it; every other domain keeps the inline generic smoke). Because all FOUR lifecycle-mutation handlers (`GenerateKey`, `ActivateKey`, `RotateKey`, `CompromiseKey`) take the capability, the general registration alone cannot even construct them (nor `RunDueRotations`, which composes them). No stored-root plaintext materializes anywhere outside this seam; every use fires the single chokepoint (`SR_CaRootKeyUsesTotal{operation}` + `CaRootKeySigningUsed` 9518 / `CaRootKeySmokeTested` 9519 — kids + operation only, never key material).

App also hosts the module's IN-PROCESS keyring consumer source: `services.AddD2EncryptionFromKeyCustodian(domain, callingModuleId)` (`Application/Keyring/KeyringConsumerServiceCollectionExtensions.cs`) backs a keyed `IPayloadCrypto` with the co-hosted leaf via the `internal` `Application/Keyring/InProcessKeyringClient.cs` — each fetch establishes the in-process-module plane (`RequestOrigin.InProcessModule`, explicit `callingModuleId`, the `internal.kc.keyring` scope) on a fresh scope and flows through the real fail-closed `AuthorizeKeyringFetch`. It composes the client package's shared hot-swap machinery (rotation channel + refresh subscriber + `KeyringBackedPayloadCrypto` + the KeyCustodian provenance marker) through the same-module internals grant; the cross-process sibling `AddD2EncryptionForViaKeyring` lives in the client package. This source lives in App (not the client package) because it references the leaf `IKeyCustodianApi`, which the client package cannot reach under the dependency law.

---

## Dependencies

Project references:

- `D2.Edge.KeyCustodian.Domain` — the aggregates, sealed state types, value objects, enums, and generated error-code factories that App orchestrates.
- `D2.Shared.Handler` — `BaseHandler<TSelf, TInput, TOutput>` for the query handlers (`GetJwks`, `GetRotationPlan`); cross-cutting telemetry, metrics, D2Result, and cancellation.
- `D2.Shared.Handler.Repo` — `BaseRepoHandler` for the command handlers; DB-exception → D2Result mapping via the injected `IDbExceptionClassifier`; brings `Microsoft.EntityFrameworkCore` + `D2.Shared.Result` transitively, but each is listed explicitly.
- `D2.Shared.Result` — `D2Result<T>` semantic factories (`Ok`, `NotFound`, `ValidationFailed`, `Conflict`, etc.) used directly by command/query handlers.
- `D2.Shared.Encryption` — `IPayloadCrypto` for root-wrapping `KeyMaterialEncrypted` in command handlers; `PayloadCryptoKeyring` resolution via keyed DI.
- `D2.Shared.Time` — `IClock` for rotation-planner due-key math and transition timestamps inside command handlers.
- `D2.Shared.Context.Abstractions` — `IRequestContext` (the established `Origin` / `ImmediateCaller`, [ADR-0025](../../../../../docs/adrs/0025-request-context-establishment.md)) `SignHandler` reads and `JwtSigningCapability` injects to assert its in-process plane. Read side only — App stays gRPC-free and ASP.NET-Core-free.
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

### Signing-domain authority policy

`SigningDomainAuthorityOptions` binds from its own `KEYCUSTODIAN_SIGNING_AUTHORITY` section (prefix `KEYCUSTODIAN_SIGNING_AUTHORITY__`), validated fail-loud at startup (`ValidateOnStart`, Infra layer). `AllowedSigningDomainsByWorkload` is a `Dictionary<string, List<string>>` keyed by lowercase SPIFFE workload id (e.g. `"edge"`); a workload absent from the map resolves to the empty set (default-deny). No never-cross-process-signable domain (`jwks-signing`, `mtls-ca-root`, `mtls-ca-intermediate`) may ever appear under any workload — `Validate()` refuses to boot if one does. An empty policy is valid (every lookup denies).

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

### Keyring-domain authority policy

`KeyringDomainAuthorityOptions` binds from its own `KEYCUSTODIAN_KEYRING_AUTHORITY` section (prefix `KEYCUSTODIAN_KEYRING_AUTHORITY__`), validated fail-loud at startup (`ValidateOnStart`, Infra layer). `AllowedKeyringDomainsByWorkload` is a `Dictionary<string, List<string>>` (comparer `OrdinalIgnoreCase`, so a Windows env-var provider that uppercases keys still matches the lowercase caller ids) keyed by a bare lowercase caller id — a cross-process SPIFFE workload id (e.g. `"audit"`) OR an in-process module id (e.g. `"edge"`), both sharing the bare `[a-z0-9-]` grammar; a caller absent from the map resolves to the empty set (default-deny). Only payload (`AesPayload`) domains are grantable — `Validate()` refuses to boot if any non-payload domain (`jwks-signing` / `cookie` / `client-secret` / the `mtls-ca-*` trust anchors) is granted to any caller, because a keyring is a full encrypt+decrypt capability. Empty-string keys and non-catalog domain values also fail at startup; an empty policy is valid (every lookup denies). This boot gate is the production guard behind `GetKeyringHandler`'s defense-in-depth key-type fork: because no caller can ever hold a non-payload grant, the authority arm denies a non-payload domain with a uniform 403 before the fork is reachable.

---

## Operations / debugging

**Rotation plan**: call `GetRotationPlan` (query handler) to see the lifecycle actions due across all domains — keys approaching their cadence window, retiring keys whose grace window has elapsed, and any pending keys that have soaked long enough to activate.

**Bootstrap sequence**: a new domain needs at least one key in the `Active` state before rotation can proceed. The typical bootstrap flow is: `GenerateKey` → wait for smoke-soak → `ActivateKey`. Readiness is reported by `KeyCustodianHealthCheck` (Infra): **Unhealthy** when the root keyring cannot be loaded or the database is unreachable; **Degraded** when every configured domain (`KeyDomain.All`) is reachable but at least one lacks an `Active` key (e.g. first-boot soak — readiness still returns 200); **Healthy** when every configured domain has an `Active` key. See [`infra/README.md`](../infra/README.md).

**Compromise recovery**: `CompromiseKey` marks the incumbent compromised and commits that durable kill first (in its own save), then announces urgently (the announce signal triggers session invalidation for tokens signed by the compromised key). When requested, a replacement pending key for the same domain is generated as a best-effort follow-up after the commit — skipped when the domain already holds a live pending successor (reported as the replacement), and logged-not-fatal on failure so the compromise is never rolled back. The replacement soaks normally — there is no emergency no-soak path. After the soak window elapses, `ActivateKey` the replacement.

**Key material never appears in logs**: `KeyCustodianLog` (`[LoggerMessage]`, EventIds 9500–9529) accepts no `Exception` parameter. `kid` and `domain` are loggable. All other material-carrying fields (`KeyMaterialEncrypted`, `CompromiseKeyInput.Reason`) are `[RedactData]`-protected and their types override `ToString`/`PrintMembers` to emit redaction sentinels.

**Announce failures are non-fatal**: if the RabbitMQ announce fails after a durable commit, the handler logs (sanitized, no raw exception) and returns success. Consumers self-heal via keyring TTL refresh. Persistent announce failures indicate a messaging infrastructure problem, not a KC domain problem.
