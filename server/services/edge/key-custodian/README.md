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
| [`clients/`](clients/README.md)                                          | Transport boundary for external callers — generated transport DTOs for exposed operations (`GetJwksInput`, `GetJwksOutput`, `Jwk`) and the module façade interface. References `D2.Shared.Result` + `D2.Shared.Utilities` only; no Domain / App / Infra dep. |
| [`error-codes-source-gen/`](error-codes-source-gen/README.md)            | Roslyn generator shell that emits `KeyCustodianErrorCodes` constants + `KeyCustodianFailures` semantic factories into the domain from `contracts/keycustodian-error-codes/keycustodian-error-codes.spec.json`. Diagnostic prefix: `D2KEC`. |
| [`infra/`](infra/README.md)                                              | Concrete adapters for the App-owned ports: `KeyCustodianDbContext` (EF Core) + persistence configuration, the multi-key `FileRootKeyProvider`, the message-bus `IKeyRotationAnnouncer`, the in-process `KeyRotationService`, the readiness health check, options binding + `ValidateOnStart`, and the `AddD2KeyCustodian()` composition seam. The startup migrator + advisory lock come from the shared `D2.Shared.EntityFrameworkCore.Postgres` library. |

## Key design decisions

- **Sum-type state machine**: the domain models key lifecycle as an `abstract record EncryptionKey` base + five sealed per-state records (`PendingKey` / `ActiveKey` / `RetiringKey` / `RetiredKey` / `CompromisedKey`). Illegal transitions are uncompilable. See [ADR-0016](../../../../docs/adrs/0016-keycustodian-lifecycle-store.md).
- **Flat record + pure mapper (EF-as-DDD Shape B)**: the immutable sum type is persisted as a single non-polymorphic `KeyRecord`; a pure static mapper bridges domain ↔ record. See [ADR-0017](../../../../docs/adrs/0017-ef-as-ddd-persistence.md).
- **File-backed root key (multi-key)**: the 32-byte root key lives at `secrets/keycustodian/root.key`, loaded once at startup and never persisted to the database. An optional `root-next.key` in the same directory loads as a decrypt-only kid for zero-downtime root rotation.
- **PG advisory lock rotation coordination**: leaderless; no Redis dependency for key rotation.

## Database

`keycustodian_db` — owned by this module. Tables: `key_record`, `key_audit_record`, `leaf_issuance_audit_record`.

## Operations

KeyCustodian is a module within Edge — it is composed into the Edge host via `AddD2KeyCustodian(...)` and has no standalone process. The JWKS HTTP / gRPC transport surface is owned by the Edge transport layer; this module ships the key-lifecycle engine (persistence, rotation, vault, health), not the endpoints that expose it.

### Run locally

KeyCustodian runs as part of Edge via Docker Compose. Start the full stack with `docker compose up edge` from `infra/compose/`.

### Health check / debugging

The Infra layer registers a readiness health check (`keycustodian`) reporting whether each configured domain has an active key — Healthy when all do, Degraded during the first-boot soak window, Unhealthy when the database is unreachable or the root key cannot load. The `GetRotationPlan` query handler inspects the lifecycle actions due across all domains. See [`infra/README.md`](infra/README.md) for the composition + configuration details and [`app/README.md`](app/README.md) for handler details.

---

## References

- [ADR-0016](../../../../docs/adrs/0016-keycustodian-lifecycle-store.md) — KeyCustodian lifecycle state machine + dedicated leaderless store
- [ADR-0017](../../../../docs/adrs/0017-ef-as-ddd-persistence.md) — EF-as-DDD persistence (flat non-polymorphic Record + pure mapper)
- KeyCustodian operational context: key rotation, secret handling, and compromise runbook — see the KeyCustodian section of the auth architecture documentation.
