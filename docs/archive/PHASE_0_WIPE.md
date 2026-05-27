<!--
Copyright (c) DCSV. All rights reserved.
-->

# PHASE_0.md — Wipe + v2 Foundation

**Purpose**: tracking doc for the v1 → v2 wipe + Phase 0 (shared libraries) execution. This doc lives only until Phase 0 ships, then gets archived.

**Architectural source of truth**: [V2.md](V2.md). This doc is execution detail.

---

## Status snapshot

Phase 0 has four execution stages. The **Granular checklist** column links to the section that breaks each stage into individual line items — flip those and update the stage status here when each stage's checklist completes.

| Stage                                                            | Status                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                      | Granular checklist                                                                |
| ---------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------- |
| 1. Pre-wipe checkpoint (tag `pre-v2-wipe`)                       | ✅ Complete                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                 | (single git tag — no detail checklist)                                            |
| 2. Wipe commit (single commit on `nova` branch)                  | ✅ Complete                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                 | [Definition of done (wipe commit)](#definition-of-done-wipe-commit)               |
| 3. Documentation pass (placeholder READMEs + extracted patterns) | ✅ Complete                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                 | [Definition of done (documentation pass)](#definition-of-done-documentation-pass) |
| 4. Shared library implementation (per V2.md §4 Phase 0)          | ✅ **Complete — Waves 1-3 done (Result, Utilities, Resilience, I18n trio); Wave 2 done (full handler stack + repo-handler trio + auth/context vocabulary + spec-driven codegen); Wave 4 done (`D2.Shared.Encryption`); Wave 5 done (caching stack — Abstractions / Local.Default / Distributed.Redis / Tiered); Wave 6 done (`D2.Shared.Messaging` — `[MqPub]` / `[MqSub]` spec-driven model + RabbitMQ impl + DLQ republish + W3C trace propagation); Wave 7 done (`D2.Shared.Logging` + `D2.Shared.Telemetry` + `D2.Shared.AspNetCore` + `D2.Shared.ServiceDefaults` — squash `b3a05f1c` on `nova`, deliverable 0004 SHIPPED 2026-05-12 per [`docs/dev/deliverables/0004-service-defaults.md`](../dev/deliverables/0004-service-defaults.md)); **2914 tests passing across all built libs** incl. Testcontainers Redis + Testcontainers RabbitMQ + synthetic-host integration coverage. Phase 0 is closed. Two pre-Phase-1 deliverables are next (codegen cleanup + TS bridge — see "Pre-Phase-1 Plan" section below) before Phase 1 (Geo libs) begins.** | [Per-library checklist (Stage 4)](#per-library-checklist-stage-4)                 |

**Status legend**: ✅ Complete · 🔄 In progress · ☐ Not started · ⏸ Blocked

**LLM CTA**: when starting work in this phase, scan the snapshot above to identify the active 🔄 stage, then jump to its granular checklist via the link. Don't start work that doesn't match the active stage without explicit user approval.

All four stages are ✅ Complete. The Pre-Phase-1 Plan deliverables (0005 + 0006 + 0007) are all ✅ SHIPPED. This doc remains live through the SvelteKit BFF rewire deliverable (deferred from 0006; sequenced after Edge ships + Paraglide-translation pattern decided), then archives per the lifecycle rule in V2.md §10 when Phase 1 (Geo libs) begins.

---

## Per-library checklist (Stage 4)

Build order respects the dependency graph. Each lib lands as one squash-merged commit on `nova` (from a `nova/{lib}` branch). Flip ✅ when the lib ships with: full code + adversarial tests + README expanded from placeholder to real doc + zero `dotnet build` / `jb inspectcode` warnings.

| Wave | Lib                                                                                                                                                                                                                                                                                                                                                      | Status                                                               | Branch                                                                                 | Depends on                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                      |
| ---- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------- | -------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1    | `D2.Shared.Result`                                                                                                                                                                                                                                                                                                                                       | ✅ Complete                                                          | `n/result` (merged)                                                                    | I18n.Abstractions (TKMessage typing on `Messages` / `InputErrors`) — split was retroactively introduced during the I18n branch and merged back into Result via the same squash                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                  |
| 1    | `D2.Shared.Utilities`                                                                                                                                                                                                                                                                                                                                    | ✅ Complete                                                          | `n/utilities` (merged)                                                                 | Result + I18n.Abstractions (`TryParseEmail` / `TryParsePhoneNumber` return `D2Result<string>` with `TK.*` keys)                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                 |
| 1    | `D2.Shared.Resilience`                                                                                                                                                                                                                                                                                                                                   | ✅ Complete                                                          | `n/utilities` (merged)                                                                 | Result (for the `RetryD2ResultAsync` predicate) — split out from Utilities so retry / circuit-breaker / singleflight can be consumed independently of the boundary helpers                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                      |
| 2    | `D2.Shared.Auth.Abstractions`                                                                                                                                                                                                                                                                                                                            | ✅ Complete (on `n/handler`, awaiting squash)                        | `n/handler`                                                                            | (none — zero external deps; identity vocabulary + codegen-emitted `Scopes`)                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                     |
| 2    | `D2.Shared.Auth.Scopes.SourceGen`                                                                                                                                                                                                                                                                                                                        | ✅ Complete (on `n/handler`)                                         | `n/handler`                                                                            | (none — netstandard2.0 analyzer; consumed by Auth.Abstractions as `OutputItemType="Analyzer"`)                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                  |
| 2    | `D2.Shared.AuthContext.Abstractions`                                                                                                                                                                                                                                                                                                                     | ✅ Complete (on `n/handler`)                                         | `n/handler`                                                                            | Auth.Abstractions; codegen-emitted `IAuthContext` from `contracts/auth-context/IAuthContext.spec.json`                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                          |
| 2    | `D2.Shared.Context.Abstractions`                                                                                                                                                                                                                                                                                                                         | ✅ Complete (on `n/handler`)                                         | `n/handler`                                                                            | AuthContext.Abstractions, Auth.Abstractions, Utilities; **single-lib home for every spec-driven context primitive** — codegen-emitted `IRequestContext` (extends IAuthContext), `MutableRequestContext` concrete, `PropagatedContext` record (`propagate: true` field subset), `PropagatedContextExtensions` (`ToPropagatedContext` / `ApplyPropagatedContext` projections), `PropagatedContextSerializer` (base64url + JSON with per-field `maxLength` validation from the spec) — plus hand-written `ActorChainParser` (RFC 8693 §2.1 strict-mode) + `ScopeClaimParser` (RFC 6749 §3.3 SP-only) + `MalformedActorChainException`. Identity (UserId / OrgId / Scopes / ActorChain) rebuilds from JWT each hop; only the small operational subset propagates via `x-d2-context`                                                                                                                                                                                                                                                                                                                                                                                                                                                 |
| 2    | `D2.Shared.Context.SourceGen`                                                                                                                                                                                                                                                                                                                            | ✅ Complete (on `n/handler`)                                         | `n/handler`                                                                            | (netstandard2.0 analyzer; multi-target — emits `IAuthContext.g.cs` to AuthContext.Abstractions; emits `IRequestContext.g.cs` + `MutableRequestContext.g.cs` + `PropagatedContext.g.cs` + `PropagatedContextExtensions.g.cs` + `PropagatedContextSerializer.g.cs` to Context.Abstractions)                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       |
| 2    | `D2.Shared.Handler.Abstractions`                                                                                                                                                                                                                                                                                                                         | ✅ Complete (on `n/handler`)                                         | `n/handler`                                                                            | Context.Abstractions, Result; `IHandler` / `IHandlerContext` / `HandlerOptions`                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                 |
| 2    | `D2.Shared.Handler`                                                                                                                                                                                                                                                                                                                                      | ✅ Complete (on `n/handler`)                                         | `n/handler`                                                                            | Handler.Abstractions, Context.Abstractions, Result; `BaseHandler<TSelf, TInput, TOutput>` with sealed observability pipeline + 4 OTel metrics + scope pre-check + universal try/catch + TraceId auto-injection                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                  |
| 2    | `D2.Shared.Handler.Repo.Abstractions`                                                                                                                                                                                                                                                                                                                    | ✅ Complete (on `n/handler`)                                         | `n/handler`                                                                            | Result, I18n; `DbFailureKind` + `IDbExceptionClassifier` + 8 typed `D2Result.X()` factories + `IsXxx` discriminators                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                            |
| 2    | `D2.Shared.Handler.Repo`                                                                                                                                                                                                                                                                                                                                 | ✅ Complete (on `n/handler`)                                         | `n/handler`                                                                            | Handler, Handler.Abstractions, Handler.Repo.Abstractions, Result, EF Core; `BaseRepoHandler` consumes injected classifier + `MapDbException` per-handler override                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                               |
| 2    | `D2.Shared.Handler.Repo.Postgres`                                                                                                                                                                                                                                                                                                                        | ✅ Complete (on `n/handler`)                                         | `n/handler`                                                                            | Handler.Repo.Abstractions, Npgsql, EF Core; `PostgresDbExceptionClassifier` + SQLSTATE matrix + `services.AddD2Postgres()`. Sibling provider packages (SqlServer / SQLite / MySQL) would land in the same shape.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                |
| 3    | `D2.Shared.Tests`                                                                                                                                                                                                                                                                                                                                        | 🔄 In progress (1459 tests across all built libs; grows per new lib) | `n/result` (born here); each subsequent lib PR adds its own `Unit/{Lib}/` subdirectory | every other built lib (test infra)                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                              |
| 3    | `D2.Shared.I18n.Abstractions`                                                                                                                                                                                                                                                                                                                            | ✅ Complete                                                          | `n/i18n` (merged)                                                                      | (none — zero external deps; ships TKMessage + ITranslator + SrcGen-emitted TK constants)                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                        |
| 3    | `D2.Shared.I18n.SourceGen`                                                                                                                                                                                                                                                                                                                               | ✅ Complete                                                          | `n/i18n` (merged)                                                                      | (none — netstandard2.0 Roslyn analyzer; consumed by I18n.Abstractions as `OutputItemType="Analyzer"`. Lives at its own top-level slot, `server/shared/dotnet/i18n-source-gen/`.)                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                |
| 3    | `D2.Shared.I18n`                                                                                                                                                                                                                                                                                                                                         | ✅ Complete                                                          | `n/i18n` (merged)                                                                      | I18n.Abstractions, Utilities, IConfiguration, DI Abstractions                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                   |
| 4    | `D2.Shared.Encryption`                                                                                                                                                                                                                                                                                                                                   | ✅ Complete                                                          | `n/encryption` (merged)                                                                | (none — pure crypto primitive; depends only on DI / Hosting / Logging abstractions + BCL `AesGcm`)                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                              |
| 4    | `D2.Shared.Auth` + `D2.Shared.Auth.Http` + `D2.Shared.Auth.Grpc` (inbound auth runtime — JWT validator + JWKS provider + session-liveness tracker + `AddD2Auth` DI in core; HTTP middleware + RFC 7807 ProblemDetails in `.Http`; gRPC interceptor + RpcException trailers in `.Grpc`. Three csprojs so each transport's framework reference is opt-in.) | ✅ Complete (on `n/auth`, awaiting squash to nova)                   | `n/auth`                                                                               | Auth.Abstractions, AuthContext.Abstractions, Caching.Abstractions, Caching.Tiered, Resilience, Result, Utilities                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                |
| 5    | `D2.Shared.Caching.Abstractions`                                                                                                                                                                                                                                                                                                                         | ✅ Complete                                                          | `n/caching-distributed` (merged)                                                       | Result, I18n.Abstractions. Four building-block interfaces (`ICacheBasic`, `ICacheAtomic`, `ICacheBroadcast`, `ICacheSet`) composed by three marker interfaces (`ILocalCache`, `IDistributedCache`, `ITieredCache`). Distributed and tiered share most surface; marker name carries behavioral intent at dep site. All ops return `D2Result<T>` / `D2Result`; null/empty inputs return `ValidationFailed` (impls never throw — `InputFailures.Required(...)` helper). Plus `LocalCacheOptions`, `ICacheSerializer`, `ICacheInvalidationBackplane`. Provider-specific options live on each impl.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                  |
| 5    | `D2.Shared.Caching.Local.Default`                                                                                                                                                                                                                                                                                                                        | ✅ Complete                                                          | `n/caching-distributed` (merged)                                                       | Caching.Abstractions, Microsoft.Extensions.Caching.Memory + Options. `DefaultLocalCache : ILocalCache` wraps `IMemoryCache` for value storage + `ConcurrentDictionary` for atomic state. Direct dispatch — no BaseHandler (per-call handler overhead would be 100× the cache work). Static `Meter` for hit/miss/eviction counters.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                              |
| 5    | `D2.Shared.Caching.Distributed.Redis`                                                                                                                                                                                                                                                                                                                    | ✅ Complete                                                          | `n/caching-distributed` (merged)                                                       | Caching.Abstractions, StackExchange.Redis. `RedisDistributedCache` implements all 4 building blocks (Basic + Atomic + Broadcast + Set). `RedisCacheInvalidationBackplane` via Redis pub/sub with universal "everyone acts" rule. JsonCacheSerializer default. Internal Lua scripts for atomic compound ops. WRONGTYPE / "not an integer" both → Conflict.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       |
| 5    | `D2.Shared.Caching.Tiered`                                                                                                                                                                                                                                                                                                                               | ✅ Complete                                                          | `n/caching-distributed` (merged)                                                       | Caching.Abstractions. `DefaultTieredCache : ITieredCache` composes L1 + L2. L2-first writes (no partial-write states), L1-then-L2 reads, atomic ops route through L2 with L1 invalidation. Subscribes to optional backplane in ctor for cluster-wide L1 coherency.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                              |
| 6    | `D2.Shared.Messaging.Abstractions` + `D2.Shared.Messaging.RabbitMq` + `D2.Shared.Messaging.SourceGen`                                                                                                                                                                                                                                                    | ✅ Complete (on `n/handler`, awaiting squash)                        | `n/handler`                                                                            | Handler, Encryption, RequestContext, Caching.Abstractions; spec-driven `[MqPub]` / `[MqSub]` model with codegen-emitted `MqMessages.*` / `MqSubscriptions.*` constants + immutable `MqMessagesRegistry` / `MqSubscriptionsRegistry`; `MessageWireResolver` + `SubscriberRegistrar` enforce attribute-vs-spec FQN match at first publish / startup; full RabbitMQ.Client 7.x impl with bounded channel pool (idle-TTL eviction), publisher-confirms with transient-classifier retry, W3C `traceparent` propagation, `x-d2-context` propagated header (per-field length capped), DLQ republish-with-failure-header on a dedicated republish channel (SemaphoreSlim-guarded), `x-death` reason-filtered retries-exhausted enforcement, in-flight callback drain on disposal, narrow-catch around `BasicAck` (idempotency-mark-write failure routes to DLQ), composition-time `WaitForConfirm` ↔ `PublisherConfirmsEnabled` validation, `IMessageBus.WaitForReadyAsync` for startup-time publishers, PII-safe log delegates (`SanitizedExceptionRender`). 8-phase audit closeout (design pivot → HIGH → MEDIUM → LOW → 1st doc pass → re-sweep → fix sweep → 2nd doc pass + closeout) documented in [audit_temp.md](audit_temp.md). |
| 7    | `D2.Shared.Logging`                                                                                                                                                                                                                                                                                                                                      | ✅ Complete (on `n/service-defaults`, awaiting squash)               | `n/service-defaults`                                                                   | Utilities, Context.Abstractions, AspNetCore. Serilog config + `RedactDataDestructuringPolicy` enforcement of `[RedactData]` + `UseD2RequestLogging` middleware. Always-on `D2RequestContextEnricher` projects 42 LOG-OK fields off the spec-driven `IRequestContext` onto the request-completion log line; 8 NOT-LOGGED fields (raw IP + sub-country geo + lat/long/geohash) are explicitly suppressed and pinned by integration test. `AddD2Logging` validates options via `ValidateOnStart` (fail-fast on invalid config).                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                    |
| 7    | `D2.Shared.Telemetry`                                                                                                                                                                                                                                                                                                                                    | ✅ Complete (on `n/service-defaults`, awaiting squash)               | `n/service-defaults`                                                                   | Utilities, AspNetCore, Handler, Auth, Auth.Outbound, Messaging.RabbitMq, Caching.Distributed.Redis, Caching.Local.Default. OpenTelemetry SDK setup (traces + metrics + logs) + per-signal OTLP exporters (env-var-gated truthy) + `MapD2PrometheusEndpoint` (IP-restricted to loopback + RFC 1918) + AspNetCore / HttpClient / GrpcNetClient / Process / Runtime auto-instrumentations. Aggregates 4 cross-lib `ActivitySource`s + 6 cross-lib `Meter`s into a single `AddD2Telemetry()` call via `public const string` symbol references (rename safety) plus spec-pin tests (literal-value drift safety). Honors `OTEL_SDK_DISABLED` symmetrically across `AddD2Telemetry` + `MapD2PrometheusEndpoint`.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       |
| 7    | `D2.Shared.AspNetCore`                                                                                                                                                                                                                                                                                                                                   | ✅ Complete (on `n/service-defaults`, awaiting squash)               | `n/service-defaults`                                                                   | Utilities. Cross-cutting AspNetCore middleware + endpoint primitives — `UseD2SecurityHeaders` (OWASP defaults; HSTS only on HTTPS, no preload by default), `AddD2Cors` + `UseD2Cors` (`D2_DEFAULT` policy reading indexed `D2_CORS_ORIGINS__*` env vars, fail-closed via `ValidateOnStart`), `UseD2InfrastructureBypass` (default short-circuit mode invokes the matched endpoint's `RequestDelegate` directly), `AddD2ProblemDetails` (RFC 7807 + `traceId`/`correlationId`/`instance` enrichment, 128-char correlation cap), `AddD2HealthChecks` + `MapD2HealthEndpoints` (`/health` + `/alive` live-tag split), `RunD2ServiceAsync` (PII-safe `Log.Fatal` rendering — type FullName + first stack frame only). Owns the canonical `InfrastructurePathMatcher` consumed by Logging + Telemetry.                                                                                                                                                                                                                                                                                                                                                                                                                               |
| 7    | `D2.Shared.ServiceDefaults`                                                                                                                                                                                                                                                                                                                              | ✅ Complete (on `n/service-defaults`, awaiting squash)               | `n/service-defaults`                                                                   | Logging, Telemetry, AspNetCore, I18n, Handler, Auth, Auth.Http, Auth.Grpc, Caching.Local.Default, Utilities (10 ProjectRefs). Pure thin aggregator (ZERO logic) — `AddD2ServiceDefaults` + `UseD2DefaultPipeline` (LOCKED middleware order; no insertion points) + `MapD2DefaultEndpoints` + `RunD2ServiceAsync`. Auth wiring is fail-fast — `AuthConfigure` MUST be non-null when `SkipAuthAutoWiring = false` (the default); explicit `SkipAuthAutoWiring = true` is the opt-out path for test hosts / anonymous-only admin tools. Aggregator owns ZERO field-level configuration knowledge — every option flows through pass-through `Action<TFromOwningLib>?` delegates so new options on owning libs surface at the call site automatically with no aggregator-side maintenance.                                                                                                                                                                                                                                                                                                                                                                                                                                           |

**Notes:**

- Geo, Location, Contacts placeholder READMEs in `server/shared/dotnet/` belong to **Phase 1** (Geo libs) and **Phase 2** (Contacts) per V2.md §4 — not Stage 4.
- Within a wave, libs can ship in either order or together if small enough to bundle.
- Each lib's commit message: `feat(shared/{lib}): {one-line summary of public API}` plus a body listing key types / OTel metrics / tests added.
- Branches use the `n/` prefix (not `nova/`) so they coexist with the `nova` leaf branch in `refs/heads/n/...`.
- `D2.Shared.Tests` was scaffolded alongside `D2.Shared.Result` (rather than waiting for Wave 3) so the Result lib lands with full test coverage at point-of-merge. Each subsequent shared-lib PR adds its own `Unit/{Lib}/` subdirectory.

---

## Philosophy

V2.md §12 originally specified "wipe-and-rebuild" for everything outside a small KEEP list. After deep tree research (5 areas), that's too aggressive in 4 of 5 areas.

**Revised principle**: KEEP if edit cost is meaningfully less than rebuild cost. YEET if rebuild ≈ edit.

- **Observability dashboards**: 30 lines of edits vs full rebuild → **KEEP + edit**
- **Compose service blocks**: ~13 services translate verbatim → **KEEP + edit**
- **Translation files (10 locales × 620 keys)**: re-translation cost is huge → **KEEP + edit per phase**
- **Service protos**: cheap to rewrite + v1 encodes v1 architecture → **YEET (except common/v1)**
- **Pattern docs (HANDLER.md, RESULT.md, etc.)**: tribal knowledge → **KEEP + adapt**

`/old/v1/` snapshot remains as the safety net — anything yeeted is recoverable via `git show <pre-wipe-sha>:path`.

---

## KEEP / MOVE — content largely unchanged

### Stays at root (with no path changes)

| Item                                                                                              | Notes                                                        |
| ------------------------------------------------------------------------------------------------- | ------------------------------------------------------------ |
| `.editorconfig`                                                                                   | Generic + C# StyleCop carve-outs all transfer                |
| `.gitattributes`                                                                                  | Generic LF normalization                                     |
| `.dockerignore`                                                                                   | Generic                                                      |
| `.git/`, `.github/{labels.json, CODEOWNERS, pull_request_template.md, instructions/, templates/}` | Mostly generic                                               |
| `.husky/commit-msg`                                                                               | AI Co-Authored-By rejection — mandatory keep                 |
| `Makefile`                                                                                        | Compose helper targets — content updates per new service set |
| `LICENSE.md`                                                                                      | Conventional                                                 |
| `CLAUDE.md`, `README.md`, `CHANGELOG.md`, `V2.md`                                                 | Project docs                                                 |
| `CONTRIBUTING.md`                                                                                 | TBD — to scan during the .md sweep                           |

### Moves to `infra/observability/` (zero-touch)

11 of 14 files:

- `loki/config/loki.yaml`
- `mimir/config/mimir.yaml`
- `tempo/config/tempo.yaml`
- `grafana/provisioning/datasources/datasources.yaml`
- `grafana/provisioning/dashboards/dashboards.yaml`
- 5 community dashboards (cAdvisor 19792, MinIO 13502, PG 9628, RabbitMQ 10991, Redis 11835)
- `grafana/provisioning/dashboards/d2-worx/web-vitals-rum.json`

### Moves to `infra/compose/`

`docker-compose.yml` and `docker-compose.prod.yml` move with 13 service blocks intact (path-swap `./observability/` → `./infra/observability/`):

- `d2-postgres` + `d2-pgadmin` + `d2-pg-exporter`
- `d2-redis` + `d2-redisinsight` + `d2-redis-exporter`
- `d2-rabbitmq`, `d2-clamav`, `d2-portainer`
- `d2-loki`, `d2-tempo`, `d2-mimir`, `d2-cadvisor`, `d2-grafana`
- All Swarm `deploy:` blocks (resource limits + restart policies) carry forward — V2.md §5.9 explicitly targets Swarm
- All dev-tools `profiles:` overrides carry forward

### Moves to `infra/docker/`

**Template-forward**: ONE of the .NET Dockerfiles (gateway / geo / signalr) is the copy-modify template for the 5 new .NET Dockerfiles. Same `mcr.microsoft.com/dotnet/sdk:10.0` build → `aspnet:10.0` prod, `dotnet watch` dev pattern — exactly what V2.md §9 prescribes. Keep one open for reference during the wipe, then delete v1 file.

### Moves to `server/`

| File                    | Action                                             |
| ----------------------- | -------------------------------------------------- |
| `Directory.Build.props` | Move + add version-anchor inheritance per V2.md §7 |
| `NuGet.config`          | Move (generic NuGet source)                        |
| `global.json`           | Move (.NET 10.0.100 SDK pin)                       |
| `stylecop.json`         | Move (DCSV company name + copyright text)          |

### Moves to `server/web/`

| File               | Action                                                                     |
| ------------------ | -------------------------------------------------------------------------- |
| `.npmrc`           | Move (engine-strict, save-exact, frozen-lockfile — all apply to SvelteKit) |
| `.prettierrc`      | Move (Svelte-specific config)                                              |
| `.prettierignore`  | Move (has Paraglide-generated path)                                        |
| `package.json`     | Move + strip `pnpm -r` workspace scripts (SvelteKit standalone)            |
| `pnpm-lock.yaml`   | Move + regenerate after `package.json` restructure                         |
| `eslint.config.js` | Move + drop `backends/node` blocks (only Svelte rules survive)             |
| `vitest.config.ts` | Move + drop backend test project pointers (only SvelteKit's)               |

### Moves to `docs/`

| File                        | Action                                                                          |
| --------------------------- | ------------------------------------------------------------------------------- |
| `AUDIT_CHECKLIST.md`        | Move + trim ~3-4 v1-specific items (Drizzle reference, specific SAGA file path) |
| `OPERATIONAL-GUARANTEES.md` | Move + edit (9-job table, JWKS endpoint path, file paths)                       |

### Stays in `contracts/`

- `contracts/protos/common/v1/*` — 4 foundational protos (`d2_result`, `health`, `jobs`, `ping`)
- `contracts/messages/*` — all 10 locale files (UPDATE per phase as features change)

---

## UPDATE IN PLACE — surgical edits

| File                                                               | Edit summary                                                                                                                                                                                                                                                                                                                                                                                                                      |
| ------------------------------------------------------------------ | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ---------- | ---------------------- | -------- | ---------- | ---------------- | --------------------------------------------------------- |
| `observability/alloy/config/config.alloy`                          | ~30 lines — rename scrape jobs `gateway-rest` → `edge`; swap env vars `GATEWAY_*` / `GEO_*` → `EDGE_*`; update Docker drop-list regex `(d2-geo                                                                                                                                                                                                                                                                                    | d2-gateway | d2-signalr)`→`(d2-edge | d2-files | d2-courier | d2-notifications | d2-audit)`; collapse Pino-services regex to `d2-web` only |
| `observability/grafana/.../d2-worx/rest-gateway-performance.json`  | Rename file → `edge-performance.json`; swap `gateway-rest` → `edge`, `REST` → `Edge` in panel queries                                                                                                                                                                                                                                                                                                                             |
| `observability/grafana/.../d2-worx/sveltekit-bff-performance.json` | Rename file → `web-bff-performance.json`; swap `d2-sveltekit` → `d2-web`                                                                                                                                                                                                                                                                                                                                                          |
| `docker-compose.yml` and `.prod.yml`                               | (1) Path swap `./observability/` → `./infra/observability/`; (2) delete 8 v1 service blocks; (3) update `d2-alloy` env vars; (4) decide MinIO+SeaweedFS coexistence per V2.md §5.6; (5) keep `d2-dkron` daemon, drop `d2-dkron-mgr` (Phase 8); (6) switch `d2-sveltekit` to `develop.watch` per V2.md §9; (7) ADD blocks for `d2-edge`, `d2-files` (.NET this time), `d2-courier`, `d2-notifications`, `d2-audit`, `d2-seaweedfs` |
| `docker/Dockerfile.sveltekit`                                      | Drop pnpm workspace bits; drop `backends/node` + `contracts` copies; retarget `clients/web/` → `server/web/`                                                                                                                                                                                                                                                                                                                      |
| `.gitignore`                                                       | Add `.env.secrets`, `secrets/`, `.aspire/settings.json`; drop `clients/web/src/routes/debug/` carve-out (paths change); update Paraglide path `clients/` → `server/web/`                                                                                                                                                                                                                                                          |
| `.github/workflows/test.yml`                                       | **YEET-and-rewrite** per V2.md §8 single-lane shape (proto-checks, build, lint-and-format, unit-tests-{dotnet,web}, integration-{edge,files,courier,notifications,audit,key-rotation}, web-component-tests, web-mock-playwright-tests)                                                                                                                                                                                            |
| `.github/copilot-instructions.md`                                  | Trim ~10-15 lines: drop Drizzle section, drop "Cross-platform enum changes in one commit," drop other Node-service-specific bullets                                                                                                                                                                                                                                                                                               |
| `Makefile`                                                         | Update service names in `make infra` and `make otel`; consider new `make dev` per V2.md §9 (Compose Watch)                                                                                                                                                                                                                                                                                                                        |
| `Directory.Build.props` (after move to `server/`)                  | Add version-anchor inheritance from `d2-version/D2.Version.csproj`                                                                                                                                                                                                                                                                                                                                                                |
| `package.json` (after move to `server/web/`)                       | Strip `pnpm -r` workspace scripts; standalone SvelteKit                                                                                                                                                                                                                                                                                                                                                                           |

---

## YEET — replaced or obsolete

### Files at repo root

- `D2.sln`, `D2.sln.DotSettings`, `D2.sln.DotSettings.user` (replaced by `server/D2.slnx`)
- `pnpm-workspace.yaml` (no workspace; SvelteKit standalone)
- `inspectcode.log`, `inspectcode2.log`, `inspectcode_apphost.log` (build artifacts)
- `nul` (Windows accident)
- `PROFILE_PROGRESS.md` (v1 progress tracker)

### Already gone (handled in pre-wipe checkpoint commit)

- `PLANNING.md`, `RESEARCH_REPORT.md`, `VERSIONING.md`, `TO-REVIEW.md`

### Whole trees

- `/backends/` — entire .NET + Node service tree (rebuild fresh per phase using `/old/v1/` as reference)
- `/clients/` — SvelteKit moves to `server/web/`; mobile placeholder dropped

### `.github/`

- `.github/workflows/test.yml` (replaced — see UPDATE table)
- `tools/proto-gen/.gitkeep` (empty placeholder)

### Docker

- 8 v1 Dockerfiles: `Dockerfile.{auth, comms, dkron-mgr, files, gateway, geo, signalr}` (template-forward via gateway → edge before delete)
- `Dockerfile.sveltekit` deleted AFTER its surgical edit migrates to a new path

### Compose

- 8 v1 service blocks in compose files: `d2-{geo, auth, comms, files, gateway, signalr, dkron-mgr, node-init}` and matching prod overrides

### Contracts (only foundational protos survive)

- `contracts/protos/auth/v1/*` (auth.proto + auth_jobs.proto)
- `contracts/protos/comms/v1/*` (comms.proto + comms_jobs.proto)
- `contracts/protos/files/v1/*` (files.proto + files_jobs.proto + files_service.proto)
- `contracts/protos/geo/v1/*` (geo.proto + geo_jobs.proto)
- `contracts/protos/realtime/v1/*` (realtime_gateway.proto)
- `contracts/protos/events/v1/*` (geo_events.proto)
- `contracts/fixtures/*` (recreated per phase as test data)

---

## Wipe sequence

Single commit at the end. Tag `pre-v2-wipe` first as the safety net.

```
1. git tag pre-v2-wipe
2. Delete root files: D2.sln, D2.sln.DotSettings(.user), pnpm-workspace.yaml, inspectcode*.log, nul, PROFILE_PROGRESS.md
3. Delete /backends/ entire tree
4. Delete /clients/ entire tree (after copying clients/web/ → server/web/)
5. Delete YEET protos: contracts/protos/{auth,comms,files,geo,realtime,events}/, contracts/fixtures/
6. Create server/ tree per V2.md §2:
   - server/services/{edge,files,courier,notifications,audit}/{api,app,domain,infra,tests}/ (empty placeholders)
   - server/services/{files,courier,notifications,audit}/clients/dotnet/ (empty placeholders)
   - server/shared/dotnet/{handler,result,i18n,utilities,service-defaults,caching-local-abstractions,caching-local-default,caching-distributed-abstractions,caching-distributed-redis,messaging,encryption,geo-reference,location,contacts,auth,tests}/ (empty placeholders)
   - server/shared/typescript/README.md (deferred placeholder)
   - server/d2-version/D2.Version.csproj (per V2.md §7)
   - server/web/ (recipient of /clients/web/)
   - server/Directory.Build.props (moved + updated)
   - server/Directory.Packages.props (new — per V2.md §7)
   - server/D2.slnx (empty XML solution; projects added in phase work)
   - server/NuGet.config, server/global.json, server/stylecop.json (moved)
7. Create infra/ tree:
   - infra/docker/ (will receive surgically-edited Dockerfiles per phase)
   - infra/compose/compose.yml, compose.prod.yml (moved + edited per UPDATE table)
   - infra/observability/ (moved + 3 surgical edits)
8. Create docs/:
   - docs/AUDIT_CHECKLIST.md (moved + trimmed)
   - docs/OPERATIONAL-GUARANTEES.md (moved + edited)
   - docs/SECURITY-RUNBOOKS.md (placeholder — populated in Phase 3 per V2.md §5.4)
9. Create tools/scripts/gen-dev-keys.sh (generates dev root key + dev encryption keys; populates secrets/)
10. Create .config/dotnet-tools.json (versionize as local tool)
11. Create .versionize at root (per V2.md §7)
12. Create env split:
    - .env.local.example (committed; non-secret defaults)
    - .env.secrets.example (committed; placeholder values like TWILIO_AUTH_TOKEN=replace_with_real_value)
    - secrets/.gitkeep (gitignored directory for key material)
13. Create .claude/settings.json with deny rules per V2.md §12
14. Update .gitignore: add .env.secrets, secrets/, drop v1 paths, update Paraglide path
15. Surgical edits per UPDATE table:
    - infra/observability/alloy/config/config.alloy
    - 2 grafana dashboards (rename + label swaps)
    - infra/compose/compose.yml + compose.prod.yml
    - infra/docker/Dockerfile.sveltekit
    - .github/workflows/test.yml (full rewrite per V2.md §8)
    - .github/copilot-instructions.md (trim)
    - Makefile (service names + new dev target)
    - server/Directory.Build.props (version anchor inheritance)
    - server/web/package.json (strip workspace scripts)
    - server/web/eslint.config.js (Svelte-only)
    - server/web/vitest.config.ts (SvelteKit-only)
    - server/web/pnpm-lock.yaml (regenerate)
16. Update CLAUDE.md per V2.md §12 "What CLAUDE.md needs updated" list
17. Update README.md per V2.md §12 (replace v1 paths with v2 paths; document env-file split + dev-key generation)
18. Single commit: chore(v2): wipe v1 implementation, restructure for v2 architecture (per V2.md)
```

---

## Definition of done (wipe commit)

- [ ] `git tag pre-v2-wipe` exists
- [ ] Single wipe commit on `nova` branch
- [ ] `git status` clean post-commit
- [ ] Tree matches V2.md §2 layout
- [ ] `.env.local.example` + `.env.secrets.example` committed; `.env.local` + `.env.secrets` + `secrets/` gitignored
- [ ] `.claude/settings.json` committed with deny rules
- [ ] `tools/scripts/gen-dev-keys.sh` exists and is executable
- [ ] CLAUDE.md updated per V2.md §12 list
- [ ] README.md updated per V2.md §12 list
- [ ] `docs/AUDIT_CHECKLIST.md` and `docs/OPERATIONAL-GUARANTEES.md` exist (moved + edited)
- [ ] `infra/observability/` 14 files present; alloy + 2 dashboards updated
- [ ] `infra/compose/compose.yml` + `compose.prod.yml` present, infra services intact, v1 service blocks deleted, new v2 service blocks added (placeholders or noted as TODO per phase)
- [ ] `server/` tree skeleton present with empty per-service folders + foundational config files
- [ ] `contracts/protos/common/v1/*` intact (4 foundational protos)
- [ ] `contracts/messages/*` 10 locale files intact
- [ ] `/old/v1/` snapshot intact

---

## Documentation pass (post-wipe, pre-Phase 0 code)

After the wipe commit lands but BEFORE any shared library code is written, complete a documentation pass so every directory in the new tree has a clear description of what it WILL be. This anchors the structure, gives Phase 0 implementation a contract to land against, and surfaces tribal knowledge extracted from the v1 .md sweep into permanent homes.

### Tribal knowledge extraction (from the .md sweep)

**Intent**: ~3000 lines of v1 docs contain hard-won correctness invariants that aren't obvious from clean code in isolation. The extraction distills these into ~600 lines of evergreen rules in `docs/`. Without it, v2 risks regressing on:

- Adversarial test discipline (drift to happy-path-only)
- Rate-limit dimension hierarchy + sliding-window approximation algorithm
- Idempotency SET NX sentinel pattern
- Constant-time service-key comparison
- Partial-success ladder (NOT_FOUND → SOME_FOUND → OK)
- RedactDataDestructuringPolicy mechanics

The 5x compression is the point: surviving content is exactly the load-bearing tribal knowledge.

**Source**: extraction reads from `/old/v1/` post-wipe (the snapshot is preserved). v1 working-tree files have been deleted by the wipe commit; that's fine — the snapshot is the authoritative reference.

**Extraction order** (dependencies — start with foundational, layer up):

1. `docs/PATTERNS.md` first — biggest doc, most cross-references. Establishes shared vocabulary (TLC/2LC/3LC, D2Result factory list) that other docs reference.
2. `docs/TESTS.md` — references PATTERNS.md for handler categories.
3. `docs/MESSAGING.md` — independent, but references PATTERNS.md handler pattern.
4. `docs/PARITY.md` — short template-style doc; can land any time.
5. `docs/SECURITY-RUNBOOKS.md` placeholder — single TOC stub; expanded in Phase 3.
6. Phase-scoped reference docs (`PHASE_5/6/8_REFERENCE.md`) — independent, can land in any order or be deferred to just-before-each-phase.
7. `server/web/STRATEGY.md` and `server/web/README.md` — moves with edits; happens during the wipe (file relocation), trim happens in doc pass.
8. V2.md §5 inline edits — small surgical edits; verify each is missing before adding.

#### Evergreen docs (create in `docs/`)

| New file                        | Sources                                                                                                                                                                                                                                                                                           | Content                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       |
| ------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **`docs/PATTERNS.md`**          | BACKENDS.md + .NET HANDLER.md + .NET RESULT.md + .NET ServiceDefaults SERVICE_DEFAULT.md + .NET Utilities UTILITIES.md + .NET Repo BATCH_PG.md + ERRORS_PG.md + .NET Cache+Node Cache + .NET Middleware (5 files) + Node i18n I18N.md + Node service-defaults SERVICE_DEFAULTS.md (parseEnvArray) | Single distillation. Sections: TLC/2LC/3LC convention + canonical TLCs table; Handler (DefaultOptions/RedactionSpec/4 OTel metrics, both app AND repo declare); D2Result (12 factory list + partial-success ladder NOT_FOUND→SOME_FOUND→OK + auto-injected traceId); Utilities (Truthy/Falsey + ToNullIfEmpty + CleanStr + CircuitBreaker + Singleflight + retry options); Repo (Batch chunking + PG ~32K param limit + PG error codes 23505/23503/23502/23514 + "catch and return Conflict not 500"); Cache (lazy TTL + LRU + pluggable serializer); Middleware (Idempotency SET NX + sentinel + 30s in-flight TTL; RateLimit 4-dim hierarchy + sliding window approximation; RequestEnrichment IP precedence CF→XR→XF→Remote + fingerprint formulas; JwtAuth fingerprint formula `SHA256(UA\|Accept)`; ServiceKey constant-time `CryptographicOperations.FixedTimeEquals` + "compare against EVERY valid key, no short-circuit"; AuthPolicy route-gate registry); Configuration (parseEnvArray indexed convention `PREFIX__0`); RedactDataDestructuringPolicy mechanics (type-level + property-level + reflection caching + auto via `{@obj}`); i18n (10-locale BCP 47 list + env-driven SUPPORTED_LOCALES + TK constants). |
| **`docs/TESTS.md`**             | .NET TESTS.md + Node testing TESTING.md                                                                                                                                                                                                                                                           | 8-category adversarial Case Coverage Checklist (happy / garbage / boundary / format / cross-field / error-prop / idempotency / concurrency); test naming convention; form + endpoint testing patterns; "if it accepts user input, try to break it" principle; 7 Vitest custom matchers (`toBeSuccess`/`toBeFailure`/`toHaveData`/`toHaveErrorCode`/`toHaveStatusCode`/`toHaveMessages`/`toHaveInputErrors`) — pattern transfers to xUnit assertion helpers. **Single highest-value extraction.**                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                              |
| **`docs/MESSAGING.md`**         | backends/MESSAGING.md (drop v1 exchange/event tables, keep rules)                                                                                                                                                                                                                                 | Proto-canonical-JSON wire format; exchange naming `events.{service}` / `commands.{service}`; queue patterns (exclusive auto-delete vs durable shared); AMQP headers contract (content-type / x-proto-type / message-id / timestamp); at-least-once + idempotent-consumer requirement.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                         |
| **`docs/PARITY.md`**            | backends/PARITY.md (reset row inventory for v2)                                                                                                                                                                                                                                                   | Parity-tracking template + the "Why exclusive?" justification framework for any future cross-language additions.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                              |
| **`docs/SECURITY-RUNBOOKS.md`** | placeholder during wipe                                                                                                                                                                                                                                                                           | Expanded in Phase 3 (Edge build) with compromise runbooks per V2.md §5.4 KeyCustodian: root key rotation, JWT signing key compromise, message-key compromise.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                 |

#### Phase-scoped reference docs (deleted as each phase ships)

These preserve specific design decisions for upcoming rebuilds. They live only until the corresponding phase ships, then get archived.

| New file                        | Sources                                                                                                                                                                                                                                                                    | Used in                                 |
| ------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | --------------------------------------- |
| **`docs/PHASE_5_REFERENCE.md`** | COMMS.md + COMMS_CLIENT.md (Universal Message Shape: 8-field contract `title`/`content`/`plaintext`/`channels`/`urgency`/`correlationId`/`senderService`/`metadata`) + COMMS.md 6 design principles                                                                        | Phase 5 (Courier + Notifications build) |
| **`docs/PHASE_6_REFERENCE.md`** | FILES.md (6 design principles + status state machine `pending → processing → ready\|rejected`) + FILES_DOMAIN.md (smartphone-aware MIME list HEIC/HEIF/3GPP/AAC/M4A + design decisions table) + GEO_CLIENT.md (.NET) DefaultOptions LogInput/LogOutput suppression pattern | Phase 6 (Files .NET rebuild)            |
| **`docs/PHASE_8_REFERENCE.md`** | DKRON_MGR.md (reconciler pattern: every-5min fetch → filter `metadata.managed_by` → diff → upsert/delete; change-detection field list)                                                                                                                                     | Phase 8 (dkron-mgr port to .NET)        |

#### Doc moves (no extraction, just relocate)

- **`server/web/STRATEGY.md`** — moved from `clients/web/SVELTEKIT_STRATEGY.md`, trimmed. Contains library recommendations (Superforms+Formsnap+Zod 4, shadcn-svelte+Bits UI, Sonner toasts, LayerChart 2.0, etc.), testing strategy (Vitest browser-mode + Playwright with mocks + a11y), Faro telemetry. Most carries forward.
- **`server/web/README.md`** — moved from `clients/web/README.md`, trimmed. Hybrid Pattern C diagram + middleware pipeline order + route groups + i18n list. Update v1→v2 paths (browser → Edge direct per V2.md §5.8).

#### Folded into V2.md §5 (small inline edits)

- **§5.5 SignalR** — channel naming convention from SignalR.md (`user:{userId}`, `org:{orgId}`, `thread:{threadId}`) + push-only hub + auto-subscribe-on-connect (verify if already there)
- **§5.4 Auth & Security** — "two role concepts" note (user-level vs org-level) from AUTH.md
- **§5.6 Storage** — content-addressable + immutability rationale from GEO_SERVICE.md (verify if already there)
- **§5.7 Messaging & Notifications** — verify the 6 design principles from COMMS.md are reflected

### Index docs (new)

Every "container" directory gets a README.md acting as a table of contents:

- `docs/README.md` — TOC for all `docs/*.md` files
- `server/shared/dotnet/README.md` — index of shared libs with one-line description of each
- `server/services/README.md` — index of services with one-line description + phase number
- `infra/README.md` — overview of infra layout (compose, docker, observability) + ops commands
- `tools/README.md` — overview of dev tooling (scripts, generators)

### Per-lib placeholder READMEs

Every shared lib in `server/shared/dotnet/{lib}/` gets a `README.md` describing:

- **Purpose** — one paragraph
- **Public API surface** — high-level (no implementation detail)
- **Dependencies** — which other libs it pulls in
- **V2.md reference** — which architectural section governs this lib

Files to create (14 total):

- `server/shared/dotnet/handler/README.md`
- `server/shared/dotnet/result/README.md`
- `server/shared/dotnet/i18n/README.md`
- `server/shared/dotnet/utilities/README.md`
- `server/shared/dotnet/service-defaults/README.md`
- `server/shared/dotnet/caching-local-abstractions/README.md`
- `server/shared/dotnet/caching-local-default/README.md`
- `server/shared/dotnet/caching-distributed-abstractions/README.md`
- `server/shared/dotnet/caching-distributed-redis/README.md`
- `server/shared/dotnet/messaging/README.md`
- `server/shared/dotnet/encryption/README.md`
- `server/shared/dotnet/geo-reference/README.md`
- `server/shared/dotnet/location/README.md`
- `server/shared/dotnet/contacts/README.md`
- `server/shared/dotnet/auth/README.md`
- `server/shared/dotnet/tests/README.md`

### Per-service placeholder READMEs

Every service in `server/services/{service}/` gets a `README.md` describing:

- **Purpose** — one paragraph
- **Public API surface** — high-level (HTTP/gRPC endpoints by category)
- **Dependencies** — other services it consumes + shared libs it uses
- **V2.md reference** — §5.x section
- **Phase number** — when built per V2.md §4

Files to create (5 total):

- `server/services/edge/README.md`
- `server/services/files/README.md`
- `server/services/courier/README.md`
- `server/services/notifications/README.md`
- `server/services/audit/README.md`

### Commit

This documentation pass lands in a SEPARATE commit AFTER the wipe commit:

```
docs(v2): post-wipe documentation pass — placeholder READMEs + extracted patterns
```

**Why separate from the wipe commit**: keeps the wipe commit clean (file restructuring only) and lets the doc pass be reviewed independently. Both commits are required before Phase 0 (shared library code) begins. The wipe commit can stand alone in git history; the doc pass builds on top.

### Definition of done (documentation pass)

**Tribal knowledge extraction (evergreen)**:

- [ ] `docs/PATTERNS.md` — TLC/2LC/3LC + handler + D2Result + utilities + repo + cache + middleware + RedactionSpec + i18n sections present
- [ ] `docs/TESTS.md` — 8-category Case Coverage Checklist + Vitest matchers reference present
- [ ] `docs/MESSAGING.md` — proto-canonical-JSON + exchange naming + queue patterns + AMQP headers + at-least-once present
- [ ] `docs/PARITY.md` — template + "Why exclusive?" framework present (rows reset for v2)
- [ ] `docs/SECURITY-RUNBOOKS.md` — placeholder with TOC stub (expanded Phase 3)

**Tribal knowledge extraction (phase-scoped)**:

- [ ] `docs/PHASE_5_REFERENCE.md` — Universal Message Shape + COMMS 6 principles
- [ ] `docs/PHASE_6_REFERENCE.md` — FILES 6 principles + state machine + smartphone MIME list + GEO_CLIENT log-suppression
- [ ] `docs/PHASE_8_REFERENCE.md` — DKRON_MGR reconciler pattern + change-detection fields

**Doc moves**:

- [ ] `server/web/STRATEGY.md` — moved + trimmed from `clients/web/SVELTEKIT_STRATEGY.md`
- [ ] `server/web/README.md` — moved + v1→v2 path updates from `clients/web/README.md`

**V2.md inline edits** (verify-before-adding to avoid duplication):

- [ ] §5.4 — "two role concepts" note (user-level vs org-level) added if missing
- [ ] §5.5 — SignalR channel naming convention added if missing
- [ ] §5.6 — content-addressable + immutability rationale added if missing
- [ ] §5.7 — COMMS 6 design principles reflected if missing

**Index docs**:

- [ ] `docs/README.md` — TOC for all `docs/*.md`
- [ ] `server/shared/dotnet/README.md` — index of 14 libs
- [ ] `server/services/README.md` — index of 5 services + phase numbers
- [ ] `infra/README.md` — overview + ops commands
- [ ] `tools/README.md` — overview of tooling

**Per-lib placeholder READMEs (14)**:

- [ ] All 14 shared libs have `README.md` in `server/shared/dotnet/{lib}/` per the format above

**Per-service placeholder READMEs (5)**:

- [ ] All 5 services have `README.md` in `server/services/{service}/` per the format above

**Cross-references**:

- [ ] CLAUDE.md §3 reference table updated to include all new `docs/*.md` files
- [ ] PHASE_0.md (this doc) marked for archive (move to `docs/archive/` once Phase 0 ships)

**Commit**:

- [ ] Single docs commit on `nova` branch immediately following the wipe commit
- [ ] Commit message: `docs(v2): post-wipe documentation pass — placeholder READMEs + extracted patterns`

---

## Phase 0 design notes

Design decisions captured during planning that govern Phase 0 implementation. Each note describes the _intent_; implementation lands in the per-library code under `server/shared/dotnet/{lib}/` when `D2.Shared.Handler` (and its consumers) are built. Summarized in `docs/PATTERNS.md` once landed.

### `BaseHandler` refactor + `BaseRepoHandler` for EF exception mapping

**Problem.** v1 `BaseHandler.HandleAsync` (`old/v1/D2-WORX/backends/dotnet/shared/Handler/BaseHandler.cs`) has a universal try/catch that swallows every exception and converts it to `D2Result.UnhandledException`. Repo handlers that need to map EF exceptions (e.g., PG unique-violation → `Conflict`) must add their own try/catch at the top of `ExecuteAsync` — survey of v1 Geo.Infra showed only `CreateContacts.cs` does this; the other ~15 repo handlers have zero exception handling, so constraint violations surface as generic 500s.

**Goal.** Centralize EF→`D2Result` mapping into a dedicated `BaseRepoHandler` so repo handlers stop having to think about it. Eliminate boilerplate while keeping the original `Exception` object out of every wire-format type (per the long-standing rule that `D2Result` is pure data — no exception coupling).

**Shape.**

1. **Extract today's `HandleAsync` body into a sealed-by-default protected method.** Name: `RunCorePipelineAsync`. Returns a value tuple `(D2Result<TOutput?> Result, Exception? CapturedException)`. The existing universal catch sets `CapturedException` to the thrown exception (and returns `UnhandledException` as the Result). On success, `CapturedException` is null. The method is `protected` (not `virtual`) — subclasses cannot tamper with the observability/metrics pipeline; they consume its outcome only.

2. **Make `HandleAsync` `virtual`.** Default implementation is a one-line pass-through:

   ```csharp
   public virtual async ValueTask<D2Result<TOutput?>> HandleAsync(
       TInput input, CancellationToken ct = default, HandlerOptions? options = null)
       => (await RunCorePipelineAsync(input, ct, options)).Result;
   ```

   Existing concrete handlers need zero changes.

3. **Add `BaseRepoHandler<TSelf, TInput, TOutput> : BaseHandler<...>`.** Overrides `HandleAsync`, calls `RunCorePipelineAsync`, switches on `CapturedException` type to remap known EF exceptions to specific `D2Result` codes via existing factories. Unknown exceptions fall through (the original `UnhandledException` Result is returned unchanged).

4. **The `Exception` object lives only on the stack frame inside the BaseHandler hierarchy.** The protected tuple is destructured locally. Only `D2Result` ever escapes. **`D2Result` itself is unchanged** — no new field, no `[JsonIgnore]`, no proto exclusion, no TS parity work. The "no exception details on D2Result" rule (intentional removal during the DeCAF→D2 transition) is preserved by _structure_, not by attribute discipline.

**Why structure-not-attribute matters.** Any `D2Result` field guarded by `[JsonIgnore]` is one new serializer (or one Newtonsoft consumer, or one YAML log destructurer, or one .ts JSON.stringify) away from leaking. A field that doesn't exist can't leak. The exception travels through OTel (`activity?.AddException(ex)` in `RunCorePipelineAsync`) and Loki (log scope) — both already secured against client exposure — and those carriers are the join keys (via `traceId` on `D2Result`) that ops uses for triage.

**Mapping table.** Match on EF exception _type_ first (the type is the reliable signal). EF doesn't guarantee that inner driver exceptions populate primitives like `SqlState` or `ConstraintName`, so use those only as opportunistic refinement. v1 already provides `D2.Shared.Repository.Errors.Pg.PgErrorCodes` static predicates (`IsUniqueViolation`, `IsForeignKeyViolation`, `IsNotNullViolation`, `IsCheckViolation`) that handle both direct `PostgresException` and EF-wrapped `DbUpdateException.InnerException` — port these forward and reuse.

| EF exception type (Microsoft.EntityFrameworkCore + Npgsql)     | Default mapping                       | Notes                                                                                                                                           |
| -------------------------------------------------------------- | ------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------- |
| `DbUpdateConcurrencyException`                                 | `D2Result.Conflict`                   | Row-version mismatch / optimistic concurrency. EF-determined, no driver involvement.                                                            |
| `DbUpdateException` w/ `PgErrorCodes.IsUniqueViolation`        | `D2Result.Conflict`                   | PG `23505`.                                                                                                                                     |
| `DbUpdateException` w/ `PgErrorCodes.IsForeignKeyViolation`    | `D2Result.ValidationFailed`           | PG `23503`. Caller passed an invalid FK.                                                                                                        |
| `DbUpdateException` w/ `PgErrorCodes.IsNotNullViolation`       | `D2Result.ValidationFailed`           | PG `23502`. Required field missing.                                                                                                             |
| `DbUpdateException` w/ `PgErrorCodes.IsCheckViolation`         | `D2Result.ValidationFailed`           | PG `23514`. Check constraint failed.                                                                                                            |
| `DbUpdateException` (no recognized inner)                      | Fall through                          | Forces handlers that hit unrecognized DB errors to add a recognizer rather than papering over with broad `Conflict`.                            |
| `RetryLimitExceededException`                                  | Fall through (= `UnhandledException`) | EF execution-strategy gave up; root cause is in the inner. Logs already capture it.                                                             |
| `OperationCanceledException` when `ct.IsCancellationRequested` | `D2Result.Canceled`                   | User-initiated cancellation only. Other `OperationCanceledException` flavors (framework-internal) fall through — they're a different bug class. |
| Anything else                                                  | Fall through                          | Original `UnhandledException` Result unchanged.                                                                                                 |

**Per-handler refinement.** Subclasses needing constraint-specific mapping override `HandleAsync` themselves. Pattern:

```csharp
public override async ValueTask<D2Result<TOutput?>> HandleAsync(
    TInput input, CancellationToken ct = default, HandlerOptions? options = null)
{
    var ctx = await RunCorePipelineAsync(input, ct, options);
    if (ctx.CapturedException is DbUpdateException dbEx
        && dbEx.InnerException is Npgsql.PostgresException { ConstraintName: "users_email_unique" })
    {
        return D2Result<TOutput?>.Conflict(messages: [TK.account_errors_emailTaken], traceId: TraceId);
    }
    return await base.HandleAsync(input, ct, options);
}
```

**Observability is unchanged.** `RunCorePipelineAsync` still calls `activity?.AddException(ex)`, records the exception metric, emits the unhandled-exception log. Tempo + Loki get the full exception regardless of whether a subclass remaps the Result. **Add one enhancement** at implementation time: push `exceptionType` + `innermostExceptionType` onto the log scope (via `BeginScope`) so Loki queries can filter by type without parsing the message.

**Open questions to resolve at implementation time.**

1. Naming: `RunCorePipelineAsync` vs `RunPipelineAsync` vs `ExecuteWithObservabilityAsync`. Default proposal: `RunCorePipelineAsync`.
2. `DbUpdateException` with no recognized inner — fall through (conservative) vs default `Conflict` (broad). Default proposal: **fall through**, force explicit recognition.
3. Tuple element naming — `(Result, CapturedException)` vs `(Result, Exception)`. Default proposal: `CapturedException` (reads better at the call site, avoids shadowing the type name).
4. Does this also get a parallel `BaseRepoHandler` for the SvelteKit BFF or any future Node.js backend? Per current scope (.NET-only backend per V2.md §5.1), **no**. Revisit if cross-language services land later.

**Out of scope (rejected during design).**

- Adding any exception-metadata field to `D2Result` itself — including sanitised "type-name only" variants. Leakage surface, cross-platform coupling, OTel span already carries this.
- Generalising the wrapping pattern into a `Pipeline` delegate property or middleware-style stack. The simple virtual-`HandleAsync` + protected `RunCorePipelineAsync` covers every realistic use case. Future bases (`BaseAuditedHandler`, etc.) override `HandleAsync` the same way without changing `BaseHandler`.

**Implementation update (post-design).** The handler-repo split landed as 3 packages instead of one — `D2.Shared.Handler.Repo.Abstractions` (typed `D2Result.UniqueViolation` / `.ConcurrencyConflict` / `.DbDeadlock` / etc. extension factories + `IsXxx` discriminators + `IDbExceptionClassifier` interface + `DbFailureKind` enum + `DbErrorCodes` constants), `D2.Shared.Handler.Repo` (`BaseRepoHandler` consumes the injected classifier; provider-agnostic), `D2.Shared.Handler.Repo.Postgres` (`PostgresDbExceptionClassifier` impl + SQLSTATE matrix + `services.AddD2Postgres()`). The mapping table above expanded — instead of collapsing FK / NOT-NULL / CHECK into generic `ValidationFailed`, every classifiable failure gets its own typed factory + error code so callers can branch on what specifically went wrong (e.g. `if (result.IsDbDeadlock) await retry...`, `if (result.IsConcurrencyConflict) await ReloadAndMerge...`). Per-handler refinement is now a `protected virtual MapDbException(ex, kind)` override that returns null to fall through to the typed default.

> **⚠ Integration test requirement (deferred but tracked).** The classifier matrix lives in `D2.Shared.Handler.Repo.Postgres` and dispatches on real `PostgresException.SqlState` values + raw `NpgsqlException` shapes (`08***` connection class, `40P01` deadlock, `57014` query_canceled, `53300` too_many_connections, etc.). Unit tests can only verify the switch logic against synthesized exceptions; **the actual Npgsql wire behavior for each SQLSTATE must be validated by integration tests against a real Postgres instance** (Testcontainers fixture, throwaway DB, force each constraint violation / deadlock / timeout, assert the resulting `D2Result.IsXxx` discriminator fires). Without this, a future Npgsql version that changes how it surfaces a given SQLSTATE silently breaks `BaseRepoHandler`'s mapping with zero unit-test signal. Add to the integration-test backlog when Phase 0 wraps and the first repo-handler-using service stands up.

---

## Then: Phase 0 (shared libraries)

After BOTH the wipe commit AND the documentation pass land, Phase 0 code begins per V2.md §4 — implementing the 14 foundational shared libraries against the contracts established in their placeholder READMEs.

Each library, as it's implemented, expands its placeholder README into a full doc (full public API, examples, gotchas, OTel metrics if applicable). Per V2.md §6: "Every project/module has a corresponding `.md` file."

**Phase 0 is now ✅ Complete** — all libraries shipped per the per-library checklist above; the final Wave 7 squash (`b3a05f1c`) is on `nova` and snapshotted at [`docs/dev/deliverables/0004-service-defaults.md`](../dev/deliverables/0004-service-defaults.md).

---

## Pre-Phase-1 Plan

Two deliverables ship between Phase 0 closing and Phase 1 (Geo libs) starting. Both are **bridge work** — neither produces new runtime services; both prepare the codebase to absorb Phase 1+ at higher quality and lower friction.

| #        | Deliverable                                                | Status                                                                                                                      | Estimated effort | Branch                                                     |
| -------- | ---------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------- | ---------------- | ---------------------------------------------------------- |
| 0005     | `0005-codegen-cleanup-and-dotnet-improvements` (.NET only) | ✅ Complete                                                                                                                 | ~3-5 days        | `n/codegen-cleanup` (off `nova` post-0004)                 |
| 0006     | `0006-ts-bridge`                                           | ✅ Complete — SHIPPED 2026-05-15 per [`docs/dev/deliverables/0006-ts-bridge.md`](../dev/deliverables/0006-ts-bridge.md)     | ~2-3 weeks       | `n/ts-bridge` (off `nova` post-0005, squashed to `nova`)   |
| 0007     | `0007-wire-parity`                                         | ✅ Complete — SHIPPED 2026-05-16 per [`docs/dev/deliverables/0007-wire-parity.md`](../dev/deliverables/0007-wire-parity.md) | ~1 week          | `n/wire-parity` (off `nova` post-0006, squashed to `nova`) |
| (future) | SvelteKit BFF rewire (deferred from 0006)                  | ⏸ Pending — sequenced after Edge exists + Paraglide-translation pattern decided; lands BEFORE Phase 7                       | TBD              | TBD                                                        |

**Order**: 0005 first (no TS dependency); 0006 second (consumes the spec migrations 0005 produces — particularly AuthErrorCodes — so the TS-side codegen runners have a single canonical spec to read instead of a moving target); 0007 third (cross-language wire-format parity sweep — extends the spec-driven contract pattern to ProblemDetails / ErrorCodes / TKMessage / D2Result envelope / gRPC trailers + AMQP wire surface). Phase 1 (Geo libs) starts AFTER 0007 ships.

### Deliverable 0005 — codegen cleanup + .NET improvements ✅ Complete

**Snapshot**: `docs/dev/deliverables/0005-codegen-cleanup-and-dotnet-improvements.md` (final report + lessons + applied `rules.md` augmentation).

**Scope (LOCKED)** — four sub-concerns:

#### Sub-concern A — AuthErrorCodes spec migration

Move the .NET hand-authored AuthErrorCodes catalog (`server/shared/dotnet/auth/Errors/AuthErrorCodes.cs` — 14 constants) + the `AuthFailures.cs` factory methods (16 factories) into a single spec at `contracts/auth-error-codes/auth-error-codes.spec.json`.

Each spec entry pairs:

- The error code string (e.g., `auth.token.expired`)
- HTTP status code mapping
- gRPC StatusCode mapping
- ProblemDetails URI (RFC 7807)
- i18n message key (TK constant)
- Telemetry tag whitelist (which `AuthTelemetry.ProblemEmitted` `error_code` tag values are valid)

Codegen emits:

- `AuthErrorCodes.g.cs` — the constants (replaces today's hand-authored `AuthErrorCodes.cs`)
- `AuthFailures.g.cs` — the factory methods returning `D2Result.Unauthorized` / `Forbidden` etc. with the right code + message + status tuple
- A test fixture that validates the `AuthTelemetry.ProblemEmitted` counter's `error_code` tag whitelist matches the spec at startup (tag-discipline enforcement)

**Single source of truth** across (1) the constant catalog, (2) the factory catalog, (3) the telemetry tag whitelist, (4) the i18n key references. **Ready for TS-side emission** in deliverable 0006 (where `@d2/auth-abstractions` reads the same spec).

#### Sub-concern B — Telemetry tag enumerations spec

Move the per-counter tag whitelists currently expressed as xmldoc enumeration comments on each counter into a single spec at `contracts/telemetry/telemetry.spec.json`.

Coverage:

- `AuthTelemetry` — 4 counters with closed tag sets (validated via Auth integration tests today via xmldoc-only)
- `OutboundTelemetry` — 2 counters (Auth.Outbound)
- `HandlerTelemetry` (4 counters)
- `LoggingTelemetry`
- `TelemetryTelemetry`
- `MessagingTelemetry`
- `RedisCacheTelemetry`
- `LocalCacheTelemetry`

Codegen emits per-meter `*TelemetryTags.g.cs` files with **typed tag constants** (e.g., `AuthTelemetryTags.ErrorCode.TokenExpired`) so counter `Add()` call sites take typed constants instead of raw strings. Optionally emits Prometheus dashboard JSON snippets per counter (defer if scope balloons).

**Runtime enforcement**: counter `Add()` call sites accept `TagList` populated only via the typed constants — caught at compile time, not at telemetry-export time.

#### Sub-concern C — V2.md §5.8 BFF Trust & Privilege Boundary subsection

Self-referential — **the V2.md edits that this very doc-update produces are the canonical statement**. Deliverable 0005 references the new V2.md §5.8 subsection as the existing locked decision; no further V2.md changes needed in 0005 itself. This sub-concern is listed for completeness so 0005's PLAN doesn't accidentally try to re-litigate the BFF boundary.

#### Sub-concern D — Deferred cleanups

Two small carry-overs from prior deliverables surfaced during Phase 0 close-out:

- **Cross-deliverable §14.1 leak** at `server/shared/dotnet/tests/Unit/Auth/Inbound/Validation/JwtValidatorTests.cs:411` — a `pre-fix` literal carried in from commit `4dc6be74` of deliverable 0002 (auth inbound). Strip per §14.1 forbidden-token regex.
- **`server/shared/dotnet/utilities/README.md`** — 368 lines, exceeds §11.21 ≤300-line heuristic. Split per §11.21's multi-doc structure (the authoritative split rules live in rules.md §11.21).

#### Explicitly NOT in 0005 scope (with revisit triggers)

- **JwtClaimTypes spec collapse** — DEFERRED. The existing parity test (`server/shared/dotnet/tests/Unit/Auth/JwtClaimTypesParityTests.cs:28`) already prevents drift between the .NET-side constants and any cross-language consumers of the JWT claim namespace. Spec-ifying the catalog would fragment the hand-authored xmldoc that documents the JWT claim semantics and would introduce an architectural wart for the 5 non-spec constants (`IAT`, `EXP`, `AZP`, `FINGERPRINT`, `ACT_KIND`) that aren't part of any spec-driven shape. **Revisit trigger**: cross-language drift surfaces a real correctness issue.
- **`DbErrorCodes` / `DbFailureKind` / `PgErrorCodes` triple** — DEFERRED. These three files have 2 commits ever between them; the structural drift catches via the wildcard-throw at `BaseRepoHandler.cs:139-163` are excellent (any unrecognized SQLSTATE blows up loudly with the unmapped code in the exception message); only one provider exists today (Postgres). YAGNI applies for a hypothetical 2nd provider. **Revisit trigger**: when a 2nd DB provider's csproj (`D2.Shared.Handler.Repo.SqlServer` / `.Sqlite` / `.MySql` / etc.) is being built — at that point the spec collapse is justified because 2 providers means real cross-provider parity testing.

### Deliverable 0006 — TS bridge

**Scope (LOCKED)** — four sub-concerns shipped in 0006. The architectural shape (drops list, parity strategy, i18n approach, BFF trust boundary) is locked in V2.md §5.8 "TS shared lib forecast" — 0006 implements that forecast for the shared-package layer. The `server/web/` BFF rewire was originally enumerated as a fifth sub-concern but is **deferred to a future SvelteKit-focused deliverable** — see "Deferred from 0006" below.

#### Sub-concern A — Workspace bootstrap

- Add `pnpm-workspace.yaml` at repo root pointing at `server/shared/typescript/*` + `tools/ts-codegen/` + the `contract-tests/` private workspace package. **Excludes** `server/web/` for now — pnpm 10 validates the FULL workspace dep graph regardless of `--filter` flags, and `server/web/package.json` carries broken `workspace:*` deps until the future BFF rewire deliverable lands.
- Root `package.json` for shared `typescript` / `vitest` / `prettier` versions (single source of truth for tool versions across all TS workspaces).
- **Carefully sequenced** per the operator's known pain point: `pnpm install` rotates symlinks across the workspace, which can break Node containers if other Node services are running mid-install. The bootstrap step includes a "stop all Node containers → workspace bootstrap → restart all Node containers" sequence to avoid mid-deliverable container thrash.

#### Sub-concern B — Tier 1 TS packages (13 packages per V2.md §5.8 forecast)

Per the locked Tier 1 list in V2.md §5.8: `@d2/result`, `@d2/utilities`, `@d2/resilience`, `@d2/protos`, `@d2/auth-context-abstractions`, `@d2/request-context-abstractions`, `@d2/auth-abstractions`, `@d2/i18n`, `@d2/logging`, `@d2/telemetry`, `@d2/service-defaults`, `@d2/headers`, `@d2/grpc-client`. Per-package READMEs follow the .NET shared-lib README convention (Purpose / Public API / Dependencies / V2.md reference). The four `@d2/headers-{common,http,amqp,grpc}` per-transport catalogs ship alongside as codegen-emitted siblings to the .NET `D2.Shared.Headers.*` libs (one spec, parallel emission).

#### Sub-concern C — TS-side codegen runners

Consume the specs migrated in 0005 (`auth-error-codes`) plus existing specs (`auth-scopes`, `auth-audiences`, `auth-context`, `request-context`, `headers`, `jwt-claims`, `in-process-keys`, `mq-messages`).

Implementation: sibling Node scripts at `tools/ts-codegen/` reading the same JSON specs the .NET Roslyn SourceGens consume. The TS toolchain stays in TS land, the .NET SourceGen analyzers stay narrowly scoped. Per-script emitters: `auth-context-emit.ts`, `request-context-emit.ts`, `auth-scopes-emit.ts`, `auth-error-codes-emit.ts`, `auth-failures-emit.ts`, `headers-emit.ts` (per-transport target flag), `jwt-claims-emit.ts` (one runner emits `JwtClaimTypes` constants AND the `JwtPayload` typed shape).

#### Sub-concern D — Cross-language contract test infrastructure

Per V2.md §5.8 "Cross-language parity testing":

- `server/shared/typescript/contract-tests/` Vitest workspace package (`private: true`)
- Fixture generation via `dotnet test --filter Category=ContractFixtures` emitting deterministic JSON files under `server/shared/typescript/contract-tests/fixtures/<catalog>/<scenario>.json`
- **Forward-only direction**: `.NET emits fixture → TS reads + asserts`. Bidirectional (TS-emit → .NET-read) is intentionally out of scope; any future need lands as a separate test surface. The TS side does NOT spawn a .NET subprocess at test time — fixtures are committed to git so PR diffs surface drift directly.
- CI gate: scaffold present (commented-out TODO blocks in `.github/workflows/test.yml`) for the `contract-fixtures-emit` (regenerate + assert no `git diff` drift) and `contract-tests-parity` (run Vitest assertions) jobs; activates alongside the .NET `build` job.
- Initial catalog set: `propagated-context/` (round-trip), `auth-context/` (typed-shape), `request-context/` (typed-shape, transitive), `jwt-payload/` (typed-shape vs constants), `redact-paths/` (`[RedactData]` vs spec-emitted arrays), `headers/` (per-transport `as const` membership + wire values).

#### Deferred from 0006 — SvelteKit BFF rewire (future deliverable)

The `server/web/` BFF rewire was originally enumerated as a fifth sub-concern but **dropped mid-deliverable and deferred to a future SvelteKit-focused deliverable**. Reason: the BFF rewire cannot be validated end-to-end without Edge existing (Edge builds in the main Phase plan after Phase 0), and decisions like the Paraglide-translation pattern (Paraglide can't take runtime keys; the v1 BFF used a server-side translation middleware to map `userMessageKey` → Paraglide functions) need to be made in the context of the actual SvelteKit-focused deliverable, not speculatively here.

**Carry-forward items** for the future BFF rewire deliverable:

- `server/web/` stays broken-by-design (16 `workspace:*` deps in `package.json` unmatched + missing 4 new headers catalogs).
- `pnpm-workspace.yaml` stays WITHOUT `server/web/` in the globs (re-add is a one-line edit; was deferred because pnpm 10.15 validates the full workspace dep graph regardless of `--filter` flags — keeping `server/web/` in the workspace would have failed `pnpm install` for 0006).
- 5 server-side guards from `@d2/headers` not yet wired into `hooks.server.ts`.
- Browser-side `authClient` not yet built (`server/web/src/lib/client/auth/`).
- **Paraglide-translation-pattern decision** — open: replicate v1's server-side translation middleware, pass-through-key + browser translates, or codegen-emit-switch-table from spec. Pending the SvelteKit-focused deliverable.
- Faro init verification at `server/web/src/lib/client/telemetry/faro.ts` post-cleanup.
- `@d2/grpc-client` wiring into `hooks.server.ts` for SSR loaders calling Edge.
- gRPC channel teardown signal (`process.on('SIGTERM', closeChannel)` or SvelteKit hook).

**Sequence trigger**: the future BFF rewire deliverable should be sequenced AFTER (a) Edge exists, (b) the Paraglide-translation-pattern decision is made, (c) we're focused on SvelteKit DX. It should land BEFORE Phase 7 (Rebuild SvelteKit BFF — the mainline phase that consumes Edge + Files + Notifications + Courier) so Phase 7 starts with a working `pnpm install` on `server/web/`. It does NOT block Phase 1 (Geo libs).

#### Mid-deliverable cleanup that DID land in 0006

- Deleted stale `server/web/src/paraglide/` outdir (canonical outdir is `src/lib/paraglide/`).
- Ripped out v1-leftover code in `server/web/src/lib/server/` (`auth.server.ts`, `hooks/*`, `middleware.server.ts`, REST gateway clients) that referenced dead `@d2/*` packages from v1.
- Removed v1 `server/web/src/hooks.server.ts` + the `src/routes/api/auth/[...path]/` + `src/routes/api/account/[...path]/` proxy routes (V2.md §5.8 explicitly removes "All `+server.ts` endpoints, all form actions, all proxying").

---

This PHASE_0.md doc gets archived (move to `docs/archive/PHASE_0_WIPE.md` or delete) once Phase 1 (Geo libs) begins.
