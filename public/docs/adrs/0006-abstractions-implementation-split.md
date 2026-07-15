<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->


> **Visibility: PUBLIC** — ships with the open surface (`public/`).  
> Do not add product IP, private paths, or non-exportable runbooks.
# ADR-0006: Domain-safe abstractions slices + provider-pluggable implementation packages

- **Status**: Accepted
- **Date**: 2026-05-30
- **Deliverable**: D2 shared libraries (backfilled)

## Context

`public/packages/dotnet/` contains roughly 40 built library projects organized into 13 named clusters. Within every multi-package cluster, the dependency graph exhibits the same recurring structural split: a zero-infrastructure-dependency `*/abstractions` (or `*/context-abstractions`) package that domain code can freely reference, and one or more sibling `*/{core,default,impl,repo,repo-postgres,rabbitmq,distributed-redis,tiered,local-default}` packages that carry the runtime weight — DI extensions, ORM drivers, broker clients, OpenTelemetry, AspNetCore, heavy NuGet packages.

The split appears across the clusters (evidence: the `public/packages/dotnet/README.md` library table + dependency graph):

| Cluster | Abstractions slice | Runtime / provider sibling(s) |
|---|---|---|
| i18n | `i18n/abstractions` — `TKMessage`, `ITranslator`, codegen `TK` constants; "Zero external deps" | `i18n/core` — `Translator`, `AddD2I18n` |
| auth | `auth/abstractions` — enums, SrcGen `Scopes`/`Audiences`/`JwtClaimTypes`, `IJwksProvider` | `auth/core`, `auth/http`, `auth/grpc`, `auth/outbound` |
| context | `auth/context-abstractions` (`IAuthContext`), `context/abstractions` (`IRequestContext`, `PropagatedContext` + serializer) | consumed/populated by `auth/core` |
| handler | `handler/abstractions` — `IHandler`, `IHandlerContext`, `HandlerOptions` | `handler/core` — `BaseHandler`, OTel, `AddD2Handler` |
| handler/repo | `handler/repo-abstractions` — `DbFailureKind`, `IDbExceptionClassifier`; "no EF Core, no Npgsql, no provider deps" | `handler/repo` (EF, zero provider deps); `handler/repo-postgres` (SQLSTATE matrix, Npgsql) |
| caching | `caching/abstractions` — `ILocalCache`/`IDistributedCache`/`ITieredCache` markers | `caching/local-default`, `caching/distributed-redis`, `caching/tiered` |
| geo | `geo/abstractions` — `IGeoReference`, `IGeoNameResolver`, codegen geo types | `geo/default` — catalogs, `DefaultGeoNameResolver`, `AddD2GeoDefault` |
| messaging | `messaging/abstractions` — `IMessageBus`, `[MqPub]`/`[MqSub]`, codegen registries | `messaging/rabbitmq` — RabbitMQ.Client 7.x impl |
| problem-details | `problem-details/abstractions` — codegen `D2ProblemDetailsKeys`; "Zero runtime deps" | (consumed by `auth/http` + `aspnetcore/`) |
| validation _(a multi-package concern area that follows the same split; not in the README's formal 13-cluster index)_ | `validation/abstractions` — `IEmailValidator`/`IPhoneValidator`/`IPostalCodeValidator`; codegen-emitted `FieldConstraints` field-length constants + `NamePrefix`/`NameSuffix`/`BiologicalSex` taxonomy enums (consumed by domain VOs + frontend Zod schemas) | `validation/default` — defaults + libphonenumber-csharp |

The i18n split's stated purpose — "exactly mirrors `Microsoft.Extensions.Logging.Abstractions` vs `Microsoft.Extensions.Logging`" — is the canonical articulation (`i18n/abstractions/README.md`). The handler/repo triple is the most explicit provider-pluggability statement: `repo-abstractions` defines the `IDbExceptionClassifier` seam; `repo` consumes it with EF but zero provider deps; `repo-postgres` provides the PostgreSQL SQLSTATE matrix via DI (`AddD2Postgres()`), making database-engine selection a composition-root decision (ADR-0005).

The pattern also intersects codegen (ADR-0002): source generators emit vocabulary (constants, interfaces, wire-shape records) directly into abstractions packages, because those packages have no infrastructure prerequisites at compile time and can therefore be referenced by other abstractions or codegen targets without pulling runtime weight — `auth/context-abstractions` (`IAuthContext`), `messaging/abstractions` (`MqMessages`/`MqSubscriptions`), `problem-details/abstractions` (`D2ProblemDetailsKeys`), `geo/abstractions` (geo types) all follow this shape.

The dependency graph in `public/packages/dotnet/README.md` shows the acyclic result: impl nodes point inward to abstraction nodes, never the reverse. `docs/dev/rules.md §9` enforces this at the audit-round level — §9.8 mandates that every `<ProjectReference>` edit also update the dep graph in the same change, keeping the chart a living specification.

## Decision

Every multi-package cluster in `public/packages/dotnet/` is split into at minimum two csprojs: a `*/abstractions` slice and one or more runtime/provider siblings. The split is structural, not stylistic, with these invariants:

**The abstractions slice contains:**

- Interfaces domain code programs against (`IHandler`, `IMessageBus`, `ILocalCache`, `IDbExceptionClassifier`, `IAuthContext`, `IRequestContext`, `IGeoReference`, `IEmailValidator`, `ITranslator`, …).
- Pure value records and enums carrying no infrastructure identity (`DbFailureKind`, `HandlerOptions`, `TKMessage`, `ActorEntry`, geo record shapes).
- Codegen-emitted constant catalogs and spec-derived vocabulary other abstractions or generators must reference (`TK.*`, `Scopes.*`, `MqMessages.*`, `D2ProblemDetailsKeys`, geo types).
- Zero external NuGet beyond the lowest-layer foundation libs (`DcsvIo.D2.Result`, `DcsvIo.D2.I18n.Abstractions`) and, where the contract surface technically requires it, one carefully scoped NuGet (e.g. `Microsoft.IdentityModel.Tokens` on `auth/abstractions` for the `SecurityKey` field of `JwksKeySetSnapshot`).

**The runtime/provider siblings contain:**

- Concrete implementations (`BaseHandler`, `Translator`, `DefaultLocalCache`, `RedisDistributedCache`, `PostgresDbExceptionClassifier`, the RabbitMQ bus, `DefaultGeoNameResolver`).
- DI registration extensions (`AddD2Handler`, `AddD2I18n`, `AddD2GeoDefault`, `AddD2MessagingRabbitMq`, `AddD2Postgres`).
- Framework/provider NuGets (OpenTelemetry, AspNetCore, EF Core, Npgsql, StackExchange.Redis, RabbitMQ.Client, libphonenumber-csharp, NodaTime).
- Telemetry emission, middleware bindings, connection/retry logic.

Domain and application code takes `ProjectReference` only on `*/abstractions` packages. The composition root (a service's `Program.cs` or `service-defaults/`) is the only place that references impl packages and wires them to abstractions via DI. Rules §9.8 enforces that every `<ProjectReference>` addition updates the published dependency graph, making violations visible in PR diffs.

## Consequences

**Positive.**

- **Domain code stays infrastructure-free.** A handler needing a cache, a bus, or geo lookups declares a constructor dependency on `ILocalCache` / `IMessageBus` / `IGeoReference` — never on `StackExchange.Redis`, `RabbitMQ.Client`, or `DefaultGeoNameResolver`. Domain assemblies stay small, fast to compile, and free of transitive package bloat.
- **The dependency graph is structurally acyclic.** Abstractions sit at the bottom of their subgraph; impls sit above. The published Mermaid chart is the observable proof; §9.8 prevents drift.
- **Provider pluggability via DI.** Swapping `repo-postgres` for a future `repo-mysql` requires only a different composition-root registration — `BaseRepoHandler` does not change. The three cache implementations are interchangeable at the `ILocalCache`/`IDistributedCache`/`ITieredCache` injection sites; the cache stack already has three live implementations.
- **Abstractions are safe codegen targets**, because they have no infrastructure prerequisites at compile time.
- **Parallel build feasibility.** Abstraction csprojs have minimal inputs and compile quickly; impls depend on already-compiled abstractions.
- **Testability.** Any collaborator expressible as an abstractions interface can be faked without instantiating real infrastructure (`IDbExceptionClassifier`, `ILocalCache`, `IMessageBus`, `IGeoReference`, `ITranslator`).

**Negative / risks.**

- **Package proliferation.** Each concern warranting the split produces ≥2 csprojs. With ~40 built shared-lib projects already, this grows linearly with new concerns — solution load time, the `.slnx`, and `Directory.Packages.props` all expand.
- **Discipline cost of the split decision.** Each new cluster requires a judgment call about what belongs in the abstractions slice vs. the impl. A `SecurityKey` field on a contract interface forced one NuGet into `auth/abstractions` — friction that required a noted exception. Getting the boundary wrong early is expensive to undo.
- **Thin packages.** The extreme case is `problem-details/abstractions`: it exists solely to hold a codegen-emitted `D2ProblemDetailsKeys` class so `auth/http` and `aspnetcore/` can share it without depending on each other — correct by the pattern's logic but essentially one file.
- **Discoverability.** Understanding "how caching works" means navigating `caching/abstractions` + three impls rather than one library. The cluster-level README index + per-lib cross-links mitigate this, but the navigation cost is real.

## Alternatives considered

**Monolithic per-concern libraries (interface + implementation together).** One csproj per cluster carrying both the interface and the implementation. Simple, low package count, but forces every consumer of the interface to transitively pick up all provider dependencies. `i18n/abstractions`'s README explicitly cites this as the rejected path: lumping the runtime translator into the same project as `TKMessage` would force every domain project to transitively pick up DI + Configuration just to spell out an error key. Any handler in any service would carry RabbitMQ.Client, StackExchange.Redis, and NodaTime in its compilation unit regardless of use.

**A single mega-abstractions package.** One `DcsvIo.D2.Abstractions` collecting all interfaces across all clusters. Removes proliferation but would need to reference every foundation type used by any interface (`D2Result`, `TKMessage`, `SecurityKey`, `CountryCode`), making it neither zero-dep nor lightweight, and it cannot serve as a codegen target for multiple generators without coupling them.

**No formal split — domain code references impl packages directly.** Lowest friction: one project per concern, domain handlers import `DcsvIo.D2.Messaging.RabbitMq` directly. Rejected because it inverts the dependency direction — domain code would depend on infrastructure, binding it to specific providers and making it impossible to test without a live broker. This is the "infra dep leaking into domain" failure mode §9 layer-hygiene rules exist to catch.

## References

- `public/packages/dotnet/README.md` — library table (Purpose column) + dependency graph; the structural evidence base for this ADR.
- `public/packages/dotnet/i18n/abstractions/README.md` — "Why split" section; the canonical articulation ("matches `Microsoft.Extensions.Logging.Abstractions` vs `Microsoft.Extensions.Logging` exactly").
- `public/packages/dotnet/handler/repo-abstractions/README.md`, `handler/repo/README.md` — "Pure abstractions: no EF Core, no Npgsql, no provider deps"; "Provider-specific knowledge lives in sibling packages" — the most explicit provider-pluggability example.
- `public/packages/dotnet/caching/abstractions/README.md`, `messaging/abstractions/README.md`, `geo/abstractions/README.md` — the same "domain-safe, no runtime/transport deps" rationale across clusters.
- `docs/dev/rules.md §9` (layer hygiene) + §9.8 (dep-graph update requirement for every `<ProjectReference>` edit).
- [ADR-0005](0005-handler-pipeline.md) — the handler/repo abstractions+core+provider triple is the most developed instance of this pattern. [ADR-0002](0002-spec-driven-codegen.md) — codegen emits into the abstractions slices. [ADR-0003](0003-d2result-errors-as-values.md), [ADR-0004](0004-i18n-tkmessage.md) — `DcsvIo.D2.Result` + `DcsvIo.D2.I18n.Abstractions` are the two foundation deps the abstractions slices are allowed to reference.
