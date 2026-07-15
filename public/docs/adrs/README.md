<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# public/docs/adrs

> **Visibility: PUBLIC** — framework ADRs on the open surface. Product/host ADRs live under the private monorepo product-docs tree (not required for a public clone).

Framework architectural decision records that ship with the open surface.

## Law

- Every ADR file under this folder carries a **Visibility: PUBLIC** banner.
- Content is framework law only — no product IP, non-export operator runbooks, or private paths as clone requirements.
- Numbering may be discontinuous (open set is pick-and-choose).
- Layout dual-tree law: [ADR-0026](0026-public-private-monorepo-layout.md).

## Index (all PUBLIC ADRs)

| ADR | Title |
| --- | --- |
| [0001](0001-contacts-folded-owned-component.md) | Contacts as a folded owned component |
| [0002](0002-spec-driven-codegen.md) | Spec-driven codegen |
| [0003](0003-d2result-errors-as-values.md) | D2Result — errors as values |
| [0004](0004-i18n-tkmessage.md) | i18n TKMessage |
| [0005](0005-handler-pipeline.md) | Handler pipeline |
| [0006](0006-abstractions-implementation-split.md) | Abstractions / implementation split |
| [0007](0007-request-context-propagation.md) | Request-context propagation |
| [0008](0008-caching-marker-interfaces.md) | Caching marker interfaces |
| [0009](0009-async-messaging-encrypted-payloads.md) | Async messaging with encrypted payloads |
| [0010](0010-observability-dual-enrichment.md) | Observability dual enrichment |
| [0011](0011-pii-redaction-logging-safety.md) | PII redaction / logging safety |
| [0012](0012-self-rolled-dotnet-auth.md) | Self-rolled .NET auth |
| [0013](0013-service-defaults-composition-root.md) | Service-defaults composition root |
| [0014](0014-resilience-primitives.md) | Resilience primitives |
| [0015](0015-anonymization-data-governance.md) | Anonymization / data governance |
| [0017](0017-ef-as-ddd-persistence.md) | EF-as-DDD persistence |
| [0018](0018-spec-driven-error-codes.md) | Spec-driven error codes |
| [0019](0019-wrapped-result-wire-model.md) | Wrapped-result wire model |
| [0020](0020-service-project-structure.md) | Service project structure |
| [0021](0021-unified-operation-contract-idl.md) | Unified operation contract IDL |
| [0022](0022-service-auth-mint-once-forward.md) | Service auth — mint once, forward |
| [0024](0024-contract-api-versioning-strategy.md) | Contract / API versioning strategy |
| [0025](0025-request-context-establishment.md) | Request-context establishment |
| [0026](0026-public-private-monorepo-layout.md) | Public/private monorepo layout + dual-repo cutover |
