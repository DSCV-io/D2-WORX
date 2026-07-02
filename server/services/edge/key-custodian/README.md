<!--
Copyright (c) DCSV. All rights reserved.
-->

# KeyCustodian

> Parent: [`server/services/edge/`](../README.md)

For engineers working on the KeyCustodian module or integrating with the key lifecycle from other Edge modules. The KeyCustodian is Edge's key-lifecycle authority. It owns the lifecycle of every long-lived secret the platform uses — JWKS signing keys (RS256), RabbitMQ payload-encryption keys (AES-256-GCM), session-cookie signing secrets, service-identity client secrets, and the internal certificate-authority key (`X509CaCertificate`) that issues per-workload mTLS leaf certificates. KeyCustodian is the internal CA: it seeds the root + issuing intermediate certificate authority, issues short-lived workload leaf certificates on demand, and rotates the CA key through the same overlap lifecycle all managed keys use. It is the single point that generates, activates, rotates, retires, and compromises managed keys, ensuring that no other module holds or controls key material lifecycle.

Key operations are persisted to a dedicated `keycustodian_db` (independent of `auth_db`) using an EF-as-DDD flat-record + pure-mapper pattern (no per-op Repository handlers — direct DbContext + aggregate access per [ADR-0017](../../../../docs/adrs/0017-ef-as-ddd-persistence.md)). Rotation coordination uses PostgreSQL advisory locks — leaderless, no Redis dependency for a rare, non-latency-sensitive operation. The root key that protects all managed key material at rest is file-backed (`secrets/keycustodian/root.key`), loaded at startup via `FileRootKeyProvider` in the Infra layer.

## Module layout

| Sub-project                                                              | Description                                                                                                                 |
| ------------------------------------------------------------------------ | --------------------------------------------------------------------------------------------------------------------------- |
| [`domain/`](domain/README.md)                                            | Pure C# sum-type domain — the five-state `EncryptionKey` hierarchy, value objects, enums, and audit record. Zero EF/DI.     |
| [`app/`](app/README.md)                                                  | CQRS handlers (generate / activate / rotate / retire / compromise / JWKS / rotation-plan), the flat `KeyRecord` + pure mapper, crypto ports, options, and the `AddD2KeyCustodianApp()` DI registration. |
| [`clients/`](clients/README.md)                                          | Transport boundary for external callers — generated transport DTOs for exposed operations (`GetJwksInput`/`GetJwksOutput`/`Jwk`, `GetOidcConfigurationInput`/`GetOidcConfigurationOutput`, `SignInput`/`SignOutput`) and the module façade interface (`IKeyCustodianApi`), plus the hand-authored minter-capability seam `IJwtSigningCapability` (the only path to the cluster-signing root, registered solely in the auth-module composition). References `D2.Shared.Result` + `D2.Shared.Utilities` only; no Domain / App / Infra dep. |
| [`error-codes-source-gen/`](error-codes-source-gen/README.md)            | Roslyn generator shell that emits `KeyCustodianErrorCodes` constants + `KeyCustodianFailures` semantic factories into the domain from `contracts/keycustodian-error-codes/keycustodian-error-codes.spec.json`. Diagnostic prefix: `D2KEC`. |
| [`infra/`](infra/README.md)                                              | Concrete adapters for the App-owned ports: `KeyCustodianDbContext` (EF Core) + persistence configuration, the multi-key `FileRootKeyProvider`, the message-bus `IKeyRotationAnnouncer`, the in-process `KeyRotationService`, the readiness health check, options binding + `ValidateOnStart`, and the `AddD2KeyCustodian()` composition seam. The startup migrator + advisory lock come from the shared `D2.Shared.EntityFrameworkCore.Postgres` library. |

## Key design decisions

- **Sum-type state machine**: the domain models key lifecycle as an `abstract record EncryptionKey` base + five sealed per-state records (`PendingKey` / `ActiveKey` / `RetiringKey` / `RetiredKey` / `CompromisedKey`). Illegal transitions are uncompilable. See [ADR-0016](../../../../docs/adrs/0016-keycustodian-lifecycle-store.md).
- **Flat record + pure mapper (EF-as-DDD Shape B)**: the immutable sum type is persisted as a single non-polymorphic `KeyRecord`; a pure static mapper bridges domain ↔ record. See [ADR-0017](../../../../docs/adrs/0017-ef-as-ddd-persistence.md).
- **File-backed root key (multi-key)**: the 32-byte root key lives at `secrets/keycustodian/root.key`, loaded once at startup and never persisted to the database. An optional `root-next.key` in the same directory loads as a decrypt-only kid for zero-downtime root rotation.
- **PG advisory lock rotation coordination**: leaderless; no Redis dependency for key rotation.
- **Capability-general workload authority**: one pure domain rule (`WorkloadCapabilityAuthority`) answers "may workload W use capability C (sign / seal-encrypt / seal-decrypt) on target D?", keyed on the locally-established request `RequestOrigin` (recomputed by the receiving boundary from its own unforgeable transport facts — never a propagated wire value) plus the validated mutual-TLS peer workload identity (`ImmediateCaller`). Signing is layered + fail-closed: an unestablished origin denies (`KEYCUSTODIAN_REQUEST_ORIGIN_UNESTABLISHED`); the cluster-signing root `jwks-signing` is STRUCTURALLY unreachable on the general signing surface (`IKeyCustodianApi.SignAsync`) for EVERY established origin (`KEYCUSTODIAN_MINTER_CAPABILITY_REQUIRED`) — it is signable only through the dedicated minter capability `IJwtSigningCapability`, registered solely in the JWT minter's (auth-module's) composition via `AddD2JwtSigningCapability()` (possession of the resolved seam plus the in-process-module plane check is the authority). Every other domain signs cross-process only, per the caller's allowed-signing-domains policy (`KEYCUSTODIAN_SIGNING_AUTHORITY`): a domain outside the set is a policy-scope denial (`KEYCUSTODIAN_SIGNING_DOMAIN_NOT_AUTHORIZED`); a cross-process hop with no authenticated peer is denied (`Forbidden`); and the boot-time config validator still refuses to grant `jwks-signing` to any cross-process workload. The general `sign` op (the `internal.kc.sign` scope) is the first production consumer — the KC app stays gRPC-free, reading the established `IRequestContext.Origin` / `.ImmediateCaller`. Seal-encrypt is broad (any scoped producer fetches any public key); seal-decrypt is self-only (the op carries no target — the key is selected by the authenticated identity). Two security counters (`d2.keycustodian.cross_process_signing_rejections` — pages on any non-zero value, fired when a caller tries to reach the cluster root on the general surface — and `d2.keycustodian.authority_rejections`, tagged `capability` + `reason`: `origin-unestablished` / `minter-required` / `not-in-allowed-set` / `identity-absent` / `not-in-process`) and the `AuthorityRejected` log delegate record denials. The dedicated in-process minter capability (`IJwtSigningCapability`) emits the same `authority_rejections` counter (`capability = sign`) + `AuthorityRejected` log on its own deny arms — an unestablished origin (`origin-unestablished`) or an established-but-wrong-plane invocation (`not-in-process`) — so a minter deny is never silent.

## Database

`keycustodian_db` — owned by this module. Tables: `key_record`, `key_audit_record`, `leaf_issuance_audit_record`.

## Operations

KeyCustodian is a module within Edge — it is composed into the Edge host via `AddD2KeyCustodian(...)` and has no standalone process. It ships the key-lifecycle engine (persistence, rotation, vault, health) plus the generated well-known discovery surface below; the Edge host wires the routes into its composition root.

### Well-known endpoints (generated)

The contract (`contracts/typespec/key-custodian/key-custodian.tsp`) declares two anonymous (`@d2Harmless`) `GET` operations served at the OIDC-canonical paths; both are GENERATED end-to-end (route registration + DTOs + in-process façade) by the TypeSpec emitter fleet:

| Path                                | Operation              | Body                                                                                                                                          |
| ----------------------------------- | ---------------------- | ------------------------------------------------------------------------------------------------------------------------------------------- |
| `/.well-known/jwks.json`            | `GetJwks`              | The RFC 7517 JWKS document (active signing key(s) first). Empty signing-key store → `503` (fail-secure).                                     |
| `/.well-known/openid-configuration` | `GetOidcConfiguration` | Minimal OIDC discovery document — `issuer`, `jwks_uri`, `id_token_signing_alg_values_supported: ["RS256"]`, and the spec-required placeholders. |

Any HTTP JWKS / OIDC client (including .NET's `ConfigurationManager<OpenIdConnectConfiguration>`, which the shared `HttpJwksProvider` wraps) auto-discovers `jwks_uri` from the discovery document and fetches the JWKS.

### Internal signing (generated gRPC + in-process)

The contract also declares the `sign` operation — a read-only Query (`@d2Query`) exposed in-process (`@d2InProcess`) and over gRPC (`@d2GrpcMethod("KeyCustodianSigner", "Sign")`), gated by the `internal.kc.sign` scope on the `d2.internal` audience. NO REST surface (a raw HTTP sign endpoint would be an attack surface). It loads a key domain's active `RsaSigning` key, decrypts the private key via root crypto, signs the input (RS256 / PKCS#1 v1.5 over SHA-256) in the pure `RsaSigning` domain rule (zeroing the unwrapped key in a `finally`), and returns `{ signature, kid }`. The signing input carries `[RedactData]` so it never appears in logs. The general surface signs every domain EXCEPT the cluster-signing root `jwks-signing` (reachable only through the dedicated minter capability `IJwtSigningCapability`); a cross-process caller signs per its allowed-signing-domains policy, gated through `WorkloadCapabilityAuthority.AuthorizeSigning` on the established `RequestOrigin`. Two sign-specific failures join the catalog: `KEYCUSTODIAN_SIGNING_KEY_UNAVAILABLE` (503 — no active signing key for the domain, retryable) and `KEYCUSTODIAN_EMPTY_SIGNING_INPUT` (400). The generated `KeyCustodianSignerService` is a thin façade-delegate to `IKeyCustodianApi.SignAsync` with no auth/authority logic; the live host wiring (the CrossProcessHop + scope-enforcing interceptors + `MapGrpcService<KeyCustodianSignerService>().RequireAnyScope("internal.kc.sign")`) is wired by the Edge composition root.

### Configuration

App options bind from the `KEYCUSTODIAN_APP` section (prefix `KEYCUSTODIAN_APP__`), validated at host startup (`ValidateDataAnnotations` + `ValidateOnStart`):

| Option            | Env var                          | Required | Notes                                                                                                                                                       |
| ----------------- | -------------------------------- | -------- | --------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `IssuerBaseUrl`   | `KEYCUSTODIAN_APP__ISSUERBASEURL` | Yes      | The Edge external base URL — the OIDC `issuer` and the prefix of the published `jwks_uri`. Empty/unset crashes the host at startup (fail-loud).             |
| `RsaKeySizeBits`  | `KEYCUSTODIAN_APP__RSAKEYSIZEBITS` | No (2048) | RSA modulus size for generated signing keys (minimum 2048).                                                                                                |
| `SecretLengthBytes` | `KEYCUSTODIAN_APP__SECRETLENGTHBYTES` | No (64) | Opaque-secret length (minimum 16).                                                                                                                         |
| `Default` / `Policies` | `KEYCUSTODIAN_APP__DEFAULT__*` / `…__POLICIES__<DOMAIN>__*` | No | Rotation policy (cadence / grace / smoke-soak) — default + per-domain overrides. `RootCaValidity` / `IntermediateCaValidity` / `LeafValidity` govern mTLS CA windows (leaf < intermediate < root, startup-enforced). |

The signing-domain authority policy binds from its own `KEYCUSTODIAN_SIGNING_AUTHORITY` section (prefix `KEYCUSTODIAN_SIGNING_AUTHORITY__`), validated fail-loud at startup (`ValidateOnStart`):

| Option | Env var | Required | Notes |
| --- | --- | --- | --- |
| `AllowedSigningDomainsByWorkload` | `KEYCUSTODIAN_SIGNING_AUTHORITY__ALLOWEDSIGNINGDOMAINSBYWORKLOAD__<WORKLOAD>__<i>` | No (empty = deny-all) | Per-workload allowed cross-process signing domains. Key = lowercase SPIFFE workload id (e.g. `edge`); value = the signing key domains that workload may sign with. **No never-cross-process-signable domain (`jwks-signing`, `mtls-ca-root`, `mtls-ca-intermediate`) may EVER appear under any workload — the host refuses to boot if one is granted (fail-loud).** Empty-string keys and non-catalog domain values also fail at startup. An empty policy is valid (every lookup returns the empty set ⇒ deny-all ⇒ fail-closed). |

**Host prerequisite — workload identity.** The host must also supply its own workload identity: bind `D2WorkloadIdentityOptions.ServiceId` (the module's establishment-boundary registration owns the bind). `AddD2KeyCustodian` does not re-bind it — it registers a fail-loud presence gate, so an unset `ServiceId` fails `ValidateOnStart` at host start rather than booting anonymously. The gate exists because the CA-seeding and key-rotation System workers establish their `RequestOrigin.System` request context from that self-id; seeding + rotating under an empty self-id would fail late and message-stripped. Bind it before / alongside `AddD2KeyCustodian`.

### Run locally

KeyCustodian runs as part of Edge via Docker Compose. Start the full stack with `docker compose up edge` from `infra/compose/`.

### Health check / debugging

The Infra layer registers a readiness health check (`keycustodian`) reporting whether each configured domain has an active key — Healthy when all do, Degraded during the first-boot soak window, Unhealthy when the database is unreachable or the root key cannot load. The `GetRotationPlan` query handler inspects the lifecycle actions due across all domains. See [`infra/README.md`](infra/README.md) for the composition + configuration details and [`app/README.md`](app/README.md) for handler details.

---

## References

- [ADR-0016](../../../../docs/adrs/0016-keycustodian-lifecycle-store.md) — KeyCustodian lifecycle state machine + dedicated leaderless store
- [ADR-0017](../../../../docs/adrs/0017-ef-as-ddd-persistence.md) — EF-as-DDD persistence (flat non-polymorphic Record + pure mapper)
- [ADR-0025](../../../../docs/adrs/0025-request-context-establishment.md) — the `RequestOrigin` / `ImmediateCaller` establishment model `WorkloadCapabilityAuthority` is keyed on, and the possession-gated minter-capability pattern (`IJwtSigningCapability`) that closes the cluster-signing-root confused-deputy.
- KeyCustodian operational context: key rotation, secret handling, and compromise runbook — see the KeyCustodian section of the auth architecture documentation.
