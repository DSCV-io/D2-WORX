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
| [`clients/`](clients/README.md)                                          | Transport boundary for external callers — generated transport DTOs for exposed operations (`GetJwksInput`/`GetJwksOutput`/`Jwk`, `GetOidcConfigurationInput`/`GetOidcConfigurationOutput`) and the module façade interface (`IKeyCustodianApi`). References `D2.Shared.Result` + `D2.Shared.Utilities` only; no Domain / App / Infra dep. |
| [`error-codes-source-gen/`](error-codes-source-gen/README.md)            | Roslyn generator shell that emits `KeyCustodianErrorCodes` constants + `KeyCustodianFailures` semantic factories into the domain from `contracts/keycustodian-error-codes/keycustodian-error-codes.spec.json`. Diagnostic prefix: `D2KEC`. |
| [`infra/`](infra/README.md)                                              | Concrete adapters for the App-owned ports: `KeyCustodianDbContext` (EF Core) + persistence configuration, the multi-key `FileRootKeyProvider`, the message-bus `IKeyRotationAnnouncer`, the in-process `KeyRotationService`, the readiness health check, options binding + `ValidateOnStart`, and the `AddD2KeyCustodian()` composition seam. The startup migrator + advisory lock come from the shared `D2.Shared.EntityFrameworkCore.Postgres` library. |

## Key design decisions

- **Sum-type state machine**: the domain models key lifecycle as an `abstract record EncryptionKey` base + five sealed per-state records (`PendingKey` / `ActiveKey` / `RetiringKey` / `RetiredKey` / `CompromisedKey`). Illegal transitions are uncompilable. See [ADR-0016](../../../../docs/adrs/0016-keycustodian-lifecycle-store.md).
- **Flat record + pure mapper (EF-as-DDD Shape B)**: the immutable sum type is persisted as a single non-polymorphic `KeyRecord`; a pure static mapper bridges domain ↔ record. See [ADR-0017](../../../../docs/adrs/0017-ef-as-ddd-persistence.md).
- **File-backed root key (multi-key)**: the 32-byte root key lives at `secrets/keycustodian/root.key`, loaded once at startup and never persisted to the database. An optional `root-next.key` in the same directory loads as a decrypt-only kid for zero-downtime root rotation.
- **PG advisory lock rotation coordination**: leaderless; no Redis dependency for key rotation.
- **Capability-general workload authority**: one pure domain rule (`WorkloadCapabilityAuthority`) answers "may workload W use capability C (sign / seal-encrypt / seal-decrypt) on target D?", keyed on the validated mutual-TLS peer workload identity. The peer identity is surfaced by one capability-general accessor (`GetD2PeerWorkloadIdentity()`) that reads the already-validated client certificate from `HttpContext.Connection.ClientCertificate` (REST) or `ServerCallContext.GetHttpContext()` (gRPC) and re-runs the SPIFFE SAN extraction — fail-closed (no certificate ⇒ no identity ⇒ deny). Signing is policy-driven (`KEYCUSTODIAN_SIGNING_AUTHORITY`) with a structural in-process-only backstop: a cross-process caller can NEVER sign with `jwks-signing` — denied structurally by the rule (independent of policy, `KEYCUSTODIAN_CROSS_PROCESS_DOMAIN_REJECTED`) AND at boot by the config validator that refuses to grant an in-process-only domain to any workload. A domain not in the caller's allowed set is a distinct policy-scope denial (`KEYCUSTODIAN_SIGNING_DOMAIN_NOT_AUTHORIZED`). Seal-encrypt is broad (any scoped producer fetches any public key); seal-decrypt is self-only (the op carries no target — the key is selected by the authenticated identity). Two security counters (`d2.keycustodian.cross_process_signing_rejections` — pages on any non-zero value — and `d2.keycustodian.authority_rejections`) and the `AuthorityRejected` log delegate record denials.

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
| `AllowedSigningDomainsByWorkload` | `KEYCUSTODIAN_SIGNING_AUTHORITY__ALLOWEDSIGNINGDOMAINSBYWORKLOAD__<WORKLOAD>__<i>` | No (empty = deny-all) | Per-workload allowed cross-process signing domains. Key = lowercase SPIFFE workload id (e.g. `edge`); value = the signing key domains that workload may sign with. **The in-process-only domain `jwks-signing` must NEVER appear under any workload — the host refuses to boot if it is granted (fail-loud).** Empty-string keys and non-catalog domain values also fail at startup. An empty policy is valid (every lookup returns the empty set ⇒ deny-all ⇒ fail-closed). |

### Run locally

KeyCustodian runs as part of Edge via Docker Compose. Start the full stack with `docker compose up edge` from `infra/compose/`.

### Health check / debugging

The Infra layer registers a readiness health check (`keycustodian`) reporting whether each configured domain has an active key — Healthy when all do, Degraded during the first-boot soak window, Unhealthy when the database is unreachable or the root key cannot load. The `GetRotationPlan` query handler inspects the lifecycle actions due across all domains. See [`infra/README.md`](infra/README.md) for the composition + configuration details and [`app/README.md`](app/README.md) for handler details.

---

## References

- [ADR-0016](../../../../docs/adrs/0016-keycustodian-lifecycle-store.md) — KeyCustodian lifecycle state machine + dedicated leaderless store
- [ADR-0017](../../../../docs/adrs/0017-ef-as-ddd-persistence.md) — EF-as-DDD persistence (flat non-polymorphic Record + pure mapper)
- KeyCustodian operational context: key rotation, secret handling, and compromise runbook — see the KeyCustodian section of the auth architecture documentation.
