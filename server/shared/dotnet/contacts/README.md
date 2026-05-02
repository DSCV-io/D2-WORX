<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Shared.Contacts

> **Status**: placeholder — not yet implemented.

## Purpose

Per-consuming-service contacts library. Each consuming service owns its own contacts DB; this library provides the entity + repository handlers + EF Core migrations. Consuming services call `services.AddD2Contacts(connectionString)` to wire it up.

Contacts use **UUIDv7 IDs + are immutable post-create**. UUIDs (not content-addressable) because two distinct contacts can legitimately share content (two people with the same email at different times). Immutability for caching ergonomics — "updates" are modeled as create-new + repoint-owner-reference + delete-old, so cached references stay valid forever without invalidation logic.

## Public API surface

- `Contact` — entity record (immutable post-create; updates = create-new + repoint + delete-old)
- Repository handlers under `Repository/Handlers/{C,R,U,D}/`:
  - `Create` — insert new contact (returns ID)
  - `GetById` / `GetByIds` (batch)
  - `Delete` (cleanup after repoint)
- `DbContext` + EF Core migrations shipped with the library (apply on consuming service startup via `IHostedService` migrator + PG advisory lock)
- DI registration: `services.AddD2Contacts(connectionString)` — consuming service decides where its contacts DB lives

## Dependencies

- `D2.Shared.Handler` (handlers inherit BaseHandler)
- `D2.Shared.Result`
- `D2.Shared.Utilities`
- `Microsoft.EntityFrameworkCore` + `Npgsql.EntityFrameworkCore.PostgreSQL`

## References

- Storage — per-service contacts DB pattern; library owns its own DbContext + migrations, consuming service provides connection string
- Auth (Contact architecture / immutability rationale)

## Per-service DB convention

| Consuming service | Contacts DB |
|---|---|
| Edge (Auth module) | `auth_contacts_db` |
| D2.Files | `files_contacts_db` |
| D2.Courier | `courier_contacts_db` |
| D2.Notifications | `notifications_contacts_db` |
| D2.Audit | `audit_contacts_db` |

All on the same PG server — one server, many DBs.
