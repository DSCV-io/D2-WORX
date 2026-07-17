<!--
Copyright (c) DCSV. All rights reserved.
-->

# DcsvIo.D2.Private.Edge.KeyCustodian.Infra

> Parent: [`private/services/edge/key-custodian/`](../README.md)

The infrastructure layer for the KeyCustodian module — the concrete adapters behind every App-owned port, plus the composition seam that wires the whole module. It mirrors the concern folders of `app/Infrastructure/` with mandatory vendor subfolders and is the only KeyCustodian layer permitted to reference `app/` plus vendor SDKs (EF Core, Npgsql, the message bus).

## What this project owns

| Concern | Adapter | Notes |
| --- | --- | --- |
| `Persistence/Postgres/` | `KeyCustodianDbContext` + the two `IEntityTypeConfiguration<>`s + the design-time factory | Maps the flat `KeyRecord` / `KeyAuditRecord` rows to `d2-keycustodian`. `Status` is a settable value column (not a discriminator); `xmin` is the optimistic-concurrency token; the audit FK is delete-restricted. Two per-domain lifecycle invariants are DB-enforced — at most one Pending and at most one Active key per domain (see [Per-domain key invariants](#per-domain-key-invariants)). |
| `Vault/File/` | `FileRootKeyProvider` | Builds the root keyring from a root-key directory (see below). |
| `Messaging/RabbitMq/` | `RabbitMqKeyRotationAnnouncer` + `KeyRotatedEventMapper` | Publishes the `KeyRotatedEvent` after a committed transition. Fire-and-log — a failed publish never bubbles. |
| `Scheduling/Hosted/` | `KeyRotationService` | In-process timer that drives `RunDueRotations` under a try-advisory-lock (skip-if-held). |
| `Observability/` | `KeyCustodianInfraLog` + `KeyCustodianHealthCheck` | Log delegates (no `Exception` params) + the readiness probe. |
| `Configuration/` | `KeyCustodianInfraOptions` + `AddD2KeyCustodian(...)` | The infra options shape + the composition seam. |

The startup migrator (`AdvisoryLockMigrator<KeyCustodianDbContext>`), the advisory-lock helper (`PgAdvisoryLock`), the design-time factory base, and the Npgsql defaults applier come from the shared `DcsvIo.D2.EntityFrameworkCore.Postgres` library — this project consumes the **mechanism**, it implements none of it. **This assembly owns the generated domain lock-key catalog** (`AdvisoryLocks.D2Keycustodian.{MIGRATOR,ROTATION,CA_SEED}`), emitted by `DcsvIo.D2.AdvisoryLocks.SourceGen` from the central fleet catalog `contracts/advisory-locks/advisory-locks.spec.json`. There are no hand-written lock-key constants.

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

In local dev, `private/tools/scripts/gen-dev-keys.sh` generates `root.key` by default; stage a successor on demand with `gen-dev-keys.sh --rotate-root`. The root-key directory lives under the deny-ruled `secrets/` tree.

## Database lifecycle

`d2-keycustodian` is created on first boot by the migrator's ensure-database step (it connects to the `postgres` maintenance database and issues a `CREATE DATABASE` if absent), then the Initial migration is applied under the blocking migration advisory lock. The step is idempotent and multi-replica-safe: concurrent instances all attempt the lock, the first migrates, the rest block then find nothing pending. The connecting role needs `CREATEDB`. Migrations are generated (`dotnet ef migrations add`) and committed — never hand-edited.

One migration (`OnePendingIndexAndActiveExclusion`) installs the `btree_gist` extension (see below), so the connecting role also needs `CREATE` on `d2-keycustodian`. `btree_gist` is a trusted extension (PostgreSQL 13+), so a non-superuser role with `CREATE` on the database can install it; the migration creates it idempotently (`CREATE EXTENSION IF NOT EXISTS`).

## Per-domain key invariants

Two lifecycle invariants are enforced by the schema, not by application discipline alone (the rotation advisory lock is the coordination mechanism; these constraints are the structural backstop for its race window):

- **At most one Pending key per domain** — a partial UNIQUE index `ux_key_record_one_pending_per_domain` filtered to `status = 'Pending'`. It is declared in the EF model (`KeyRecordConfiguration`), so EF's command-batch preparer uses the unique-index value dependency to emit the releasing `UPDATE` before the acquiring `INSERT` within one `SaveChangesAsync` — this is why the CompromiseKey "mark the old Pending compromised + insert a fresh Pending" swap succeeds. A duplicate raises SQLSTATE `23505` (unique_violation), which `PostgresDbExceptionClassifier` maps to a typed 409 conflict.
- **At most one Active key per domain** — a partial, **DEFERRABLE** EXCLUSION constraint `ux_key_record_one_active_per_domain` (`EXCLUDE USING gist (key_domain WITH =) WHERE (status = 'Active') DEFERRABLE INITIALLY DEFERRED`). EF's fluent API cannot model an EXCLUDE constraint, so it is added in raw SQL inside the scaffolded `OnePendingIndexAndActiveExclusion` migration (a partial UNIQUE index cannot be `DEFERRABLE`, and the RotateKey `Active → Retiring` + `Pending → Active` swap needs the check deferred to COMMIT so the transient two-Active state inside one transaction is tolerated). The constraint is intentionally invisible to the EF model and model snapshot. A duplicate raises SQLSTATE `23P01` (exclusion_violation) — distinct from the Pending index's `23505` — at transaction commit.

The `'Pending'` / `'Active'` SQL filter literals are the persisted `KeyStatus` string names (the enum is stored via `HasConversion<string>()`); `KeyCustodianPersistedEnumStabilityTests` pins those names so a rename cannot silently break the filters. Both invariants are per-domain — keys in different domains never collide.
