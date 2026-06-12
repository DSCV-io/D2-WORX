<!--
Copyright (c) DCSV. All rights reserved.
-->

# KeyCustodian

> Parent: [`server/services/edge/`](../README.md)

For engineers working on the KeyCustodian module or integrating with the key lifecycle from other Edge modules. The KeyCustodian is Edge's key-lifecycle authority. It owns the lifecycle of every long-lived secret the platform uses — JWKS signing keys (RS256), RabbitMQ payload-encryption keys (AES-256-GCM), session-cookie signing secrets, and service-identity client secrets. It is the single point that generates, activates, rotates, retires, and compromises managed keys, ensuring that no other module holds or controls key material lifecycle.

Key operations are persisted to a dedicated `keycustodian_db` (independent of `auth_db`) using an EF-as-DDD flat-record + pure-mapper pattern (no per-op Repository handlers — direct DbContext + aggregate access per [ADR-0017](../../../../docs/adrs/0017-ef-as-ddd-persistence.md)). Rotation coordination uses PostgreSQL advisory locks — leaderless, no Redis dependency for a rare, non-latency-sensitive operation. The root key that protects all managed key material at rest is file-backed (`secrets/keycustodian/root.key`), loaded at startup via `FileRootKeyProvider` in the Infra layer.

## Module layout

| Sub-project                                                              | Description                                                                                                                 |
| ------------------------------------------------------------------------ | --------------------------------------------------------------------------------------------------------------------------- |
| [`domain/`](domain/README.md)                                            | Pure C# sum-type domain — the five-state `EncryptionKey` hierarchy, value objects, enums, and audit record. Zero EF/DI.     |
| [`app/`](app/README.md)                                                  | CQRS handlers (generate / activate / rotate / retire / compromise / JWKS / rotation-plan), the flat `KeyRecord` + pure mapper, crypto ports, options, and the `AddD2KeyCustodianApp()` DI registration. |
| [`error-codes-source-gen/`](error-codes-source-gen/README.md)            | Roslyn generator shell that emits `KeyCustodianErrorCodes` constants + `KeyCustodianFailures` semantic factories into the domain from `contracts/keycustodian-error-codes/keycustodian-error-codes.spec.json`. Diagnostic prefix: `D2KEC`. |
| [`infra/`](infra/README.md)                                              | Concrete adapters for the App-owned ports: `IKeyCustodianDbContext` (EF Core), persistence configuration, `FileRootKeyProvider`, RabbitMQ-backed `IKeyRotationAnnouncer`, options binding + `ValidateOnStart` guards, and EF Core migrations. See [`infra/README.md`](infra/README.md) for current status. |

## Key design decisions

- **Sum-type state machine**: the domain models key lifecycle as an `abstract record EncryptionKey` base + five sealed per-state records (`PendingKey` / `ActiveKey` / `RetiringKey` / `RetiredKey` / `CompromisedKey`). Illegal transitions are uncompilable. See [ADR-0016](../../../../docs/adrs/0016-keycustodian-lifecycle-store.md).
- **Flat record + pure mapper (EF-as-DDD Shape B)**: the immutable sum type is persisted as a single non-polymorphic `KeyRecord`; a pure static mapper bridges domain ↔ record. See [ADR-0017](../../../../docs/adrs/0017-ef-as-ddd-persistence.md).
- **File-backed root key**: the 32-byte AES root key is stored at `secrets/keycustodian/root.key`, loaded at startup, and never persisted to the database.
- **PG advisory lock rotation coordination**: leaderless; no Redis dependency for key rotation.

## Database

`keycustodian_db` — owned by this module. Tables: `key_record`, `key_audit_record`.

## Operations

> **Status: NOT IMPLEMENTED — tracked at [docs/v2/PHASE_0_AUTH.md](../../../../docs/v2/PHASE_0_AUTH.md)**

### Run locally

KeyCustodian runs as part of Edge via Docker Compose. Start the full stack with `docker compose up edge` from `infra/compose/`.

### Health check / debugging

A startup health check reporting whether each configured domain has an active signing key is an Infra-layer concern — NOT IMPLEMENTED (see [`infra/README.md`](infra/README.md)). Use the `GetRotationPlan` query handler to inspect the lifecycle actions due across all domains. See [`app/README.md`](app/README.md) for handler details.

---

## References

- [ADR-0016](../../../../docs/adrs/0016-keycustodian-lifecycle-store.md) — KeyCustodian lifecycle state machine + dedicated leaderless store
- [ADR-0017](../../../../docs/adrs/0017-ef-as-ddd-persistence.md) — EF-as-DDD persistence (flat non-polymorphic Record + pure mapper)
- KeyCustodian operational context: key rotation, secret handling, and compromise runbook — see the KeyCustodian section of the auth architecture documentation.
