<!--
Copyright (c) DCSV. All rights reserved.
-->

# KeyCustodian

> Parent: [`server/services/edge/`](../README.md)

The KeyCustodian is Edge's key-lifecycle authority. It owns the lifecycle of every long-lived secret the platform uses — JWKS signing keys (RS256), RabbitMQ payload-encryption keys (AES-256-GCM), session-cookie signing secrets, and service-identity client secrets. It is the single point that generates, activates, rotates, retires, and compromises managed keys, ensuring that no other module holds or controls key material lifecycle.

Key operations are persisted to a dedicated `keycustodian_db` (independent of `auth_db`) using an EF-as-DDD flat-record + pure-mapper pattern (no Repository TLC). Rotation coordination uses PostgreSQL advisory locks — leaderless, no Redis dependency for a rare, non-latency-sensitive operation. The root key that protects all managed key material at rest is file-backed (`secrets/keycustodian/root.key`), loaded at startup via `FileRootKeyProvider` in the Infra layer.

## Module layout

| Sub-project                                                              | Description                                                                                                                 |
| ------------------------------------------------------------------------ | --------------------------------------------------------------------------------------------------------------------------- |
| [`domain/`](domain/README.md)                                            | Pure C# sum-type domain — the five-state `EncryptionKey` hierarchy, value objects, enums, and audit record. Zero EF/DI.     |
| [`app/`](app/README.md)                                                  | CQRS handlers (generate / activate / rotate / retire / compromise / JWKS / rotation-plan), the flat `KeyRecord` + pure mapper, crypto ports, options, and the `AddD2KeyCustodianApp()` DI registration. |
| [`error-codes-source-gen/`](error-codes-source-gen/README.md)           | Roslyn generator shell that emits `KeyCustodianErrorCodes` constants + `KeyCustodianFailures` semantic factories into the domain from `contracts/keycustodian-error-codes/keycustodian-error-codes.spec.json`. Diagnostic prefix: `D2KEC`. |
| `infra/`                                                                 | EF Core `DbContext` implementation, `IEntityTypeConfiguration<KeyRecord>` + `IEntityTypeConfiguration<KeyAuditRecord>`, `FileRootKeyProvider` (reads `secrets/keycustodian/root.key`), RabbitMQ-backed `IKeyRotationAnnouncer`, options binding + `ValidateOnStart` guards, and EF Core migrations. |

## Key design decisions

- **Sum-type state machine**: the domain models key lifecycle as an `abstract record EncryptionKey` base + five sealed per-state records (`PendingKey` / `ActiveKey` / `RetiringKey` / `RetiredKey` / `CompromisedKey`). Illegal transitions are uncompilable. See [ADR-0016](../../../../docs/adrs/0016-keycustodian-lifecycle-store.md).
- **Flat record + pure mapper (EF-as-DDD Shape B)**: the immutable sum type is persisted as a single non-polymorphic `KeyRecord`; a pure static mapper bridges domain ↔ record. See [ADR-0017](../../../../docs/adrs/0017-ef-as-ddd-persistence.md).
- **File-backed root key**: the 32-byte AES root key is stored at `secrets/keycustodian/root.key`, loaded at startup, and never persisted to the database.
- **PG advisory lock rotation coordination**: leaderless; no Redis dependency for key rotation.

## Database

`keycustodian_db` — owned by this module. Tables: `key_record`, `key_audit_record`.

## References

- [ADR-0016](../../../../docs/adrs/0016-keycustodian-lifecycle-store.md) — KeyCustodian lifecycle state machine + dedicated leaderless store
- [ADR-0017](../../../../docs/adrs/0017-ef-as-ddd-persistence.md) — EF-as-DDD persistence (flat non-polymorphic Record + pure mapper)
- KeyCustodian operational context: key rotation, secret handling, and compromise runbook — see the KeyCustodian section of the auth architecture documentation.
