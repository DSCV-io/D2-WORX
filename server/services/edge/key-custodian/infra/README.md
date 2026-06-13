<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Edge.KeyCustodian.Infra

> Parent: [`server/services/edge/key-custodian/`](../README.md)

The infrastructure layer for the KeyCustodian module — the concrete adapters behind every App-owned port, plus the composition seam that wires the whole module. It mirrors the concern folders of `app/Infrastructure/` with mandatory vendor subfolders and is the only KeyCustodian layer permitted to reference `app/` plus vendor SDKs (EF Core, Npgsql, the message bus).

## What this project owns

| Concern | Adapter | Notes |
| --- | --- | --- |
| `Persistence/Postgres/` | `KeyCustodianDbContext` + the two `IEntityTypeConfiguration<>`s + the design-time factory | Maps the flat `KeyRecord` / `KeyAuditRecord` rows to `keycustodian_db`. `Status` is a settable value column (not a discriminator); `xmin` is the optimistic-concurrency token; the audit FK is delete-restricted. |
| `Vault/File/` | `FileRootKeyProvider` | Builds the root keyring from a root-key directory (see below). |
| `Messaging/RabbitMq/` | `RabbitMqKeyRotationAnnouncer` + `KeyRotatedEventMapper` | Publishes the `KeyRotatedEvent` after a committed transition. Fire-and-log — a failed publish never bubbles. |
| `Scheduling/Hosted/` | `KeyRotationService` | In-process timer that drives `RunDueRotations` under a try-advisory-lock (skip-if-held). |
| `Observability/` | `KeyCustodianInfraLog` + `KeyCustodianHealthCheck` | Log delegates (no `Exception` params) + the readiness probe. |
| `Configuration/` | `KeyCustodianInfraOptions` + `AddD2KeyCustodian(...)` | The infra options shape + the composition seam. |

The startup migrator (`AdvisoryLockMigrator<KeyCustodianDbContext>`), the advisory-lock helper (`PgAdvisoryLock`), the design-time factory base, and the Npgsql defaults applier come from the shared `D2.Shared.EntityFrameworkCore.Postgres` library — this project consumes them, it implements none of them. The two advisory-lock keys are spec-generated (`AdvisoryLocks.KeycustodianDb.MIGRATOR` / `.ROTATION`); there are no hand-written lock-key constants here.

## Composition

The host wires the whole module with one call:

```csharp
services.AddD2KeyCustodian(configuration, connectionString);
```

It binds + start-validates both options POCOs, registers the scoped `DbContext` with the canonical Npgsql defaults (NodaTime + command timeout + migrations-assembly; **no retry strategy**, because an execution-strategy reconnect would silently drop a session advisory lock), registers the keyed root crypto over the file-backed keyring with a startup round-trip check, registers the announcer, registers the migrator **before** the rotation service (start order matters), registers the readiness health checks, and chains `AddD2KeyCustodianApp()`. The host supplies the liveness `self` check.

## Configuration

All configuration is environment variables (`SECTION__PROPERTY`). The connection string is supplied to `AddD2KeyCustodian` directly (from `KEYCUSTODIAN_DATABASE_URL`); everything else binds from two sections. Worked values live in `.env.local.example`.

**`KEYCUSTODIAN_INFRA__*`** (this layer):

| Variable | Meaning | Default |
| --- | --- | --- |
| `KEYCUSTODIAN_INFRA__ROOTKEYPATH` | Directory holding the root-key files | (required) |
| `KEYCUSTODIAN_INFRA__ROTATIONCHECKINTERVAL` | How often the scheduler checks for due rotations (`d.hh:mm:ss`) | `00:05:00` |
| `KEYCUSTODIAN_INFRA__DBCOMMANDTIMEOUTSECONDS` | Per-command database timeout | `30` |

The rotation policy (cadence / grace / smoke-soak per domain) and generated-key sizing bind from the App section `KEYCUSTODIAN_APP__*`.

## The root-key directory (multi-key)

`KEYCUSTODIAN_INFRA__ROOTKEYPATH` points at a directory, not a file. The provider derives two fixed filenames from it:

- **`root.key`** — REQUIRED. 64 hex chars (32 bytes), with an optional trailing newline. It is the active kid — all new key material is wrapped with it. A missing, empty, non-hex, or wrong-length primary fails host boot.
- **`root-next.key`** — OPTIONAL. Same format. When present, it loads as a decrypt-only kid alongside the primary, enabling zero-downtime root rotation. It is **absent in steady state** — it exists only during a rotation window. A present-but-corrupt successor also fails host boot (a bad successor is an operator error mid-rotation, not "treat as absent").

In local dev, `tools/scripts/gen-dev-keys.sh` generates `root.key` by default; stage a successor on demand with `gen-dev-keys.sh --rotate-root`. The root-key directory lives under the deny-ruled `secrets/` tree.

## Database lifecycle

`keycustodian_db` is created on first boot by the migrator's ensure-database step (it connects to the `postgres` maintenance database and issues a `CREATE DATABASE` if absent), then the Initial migration is applied under the blocking migration advisory lock. The step is idempotent and multi-replica-safe: concurrent instances all attempt the lock, the first migrates, the rest block then find nothing pending. The connecting role needs `CREATEDB`. Migrations are generated (`dotnet ef migrations add`) and committed — never hand-edited.
