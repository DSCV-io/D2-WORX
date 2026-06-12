<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Edge.KeyCustodian.Infra

> Parent: [`server/services/edge/key-custodian/`](../README.md)

> **Status: NOT IMPLEMENTED — tracked at [docs/v2/PHASE_0_AUTH.md](../../../../docs/v2/PHASE_0_AUTH.md)**

For engineers implementing the KeyCustodian infrastructure layer. This project owns the concrete adapters for App-owned ports: the EF Core `DbContext` (PostgreSQL `xmin` concurrency token, advisory-lock migration setup), the `FileRootKeyProvider` (file-backed root key), the RabbitMQ-backed `IKeyRotationAnnouncer` (encrypted payload via `D2.Shared.Encryption`), options binding + `ValidateOnStart` guards, and EF Core migrations. It mirrors the concern folders of `app/Infrastructure/` (`Persistence/`, `Vault/`, `Messaging/`, `Configuration/`) with vendor-subfoldered adapters, and is the only layer in KeyCustodian permitted to reference `app/` plus vendor SDKs (EF Core, RabbitMQ client).
