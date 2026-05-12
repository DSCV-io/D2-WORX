<!--
Copyright (c) DCSV. All rights reserved.
-->

# server/shared/dotnet/ — Shared .NET Libraries

> Parent: [`server/shared/`](../README.md)

Foundational libraries consumed by every D²-WORX .NET service.

Per project convention, every library has its own `README.md`. The list below points at each lib's local README. **Status** column indicates whether the lib is built (csproj + sources) or still a placeholder shell (folder + README only — implementation lands as the consuming services need it).

## Libraries

| Lib | Status | Purpose | Reference |
|---|---|---|---|
| [`result/`](result/README.md) | **Built** | `D2Result<T>` — errors-as-values, semantic factories, partial-success ladder, `BubbleFail` propagation, auto-injected `traceId`. `Messages` / `InputErrors` are typed as `IReadOnlyList<TKMessage>` (compile-time enforcement: every user-facing message is a translation key). | [PATTERNS.md](../../../docs/PATTERNS.md) D2Result section |
| [`utilities/`](utilities/README.md) | **Built** | `Truthy()` / `Falsey()` / `ToNullIfEmpty()` / `CleanStr()` + `TryParseEmail()` / `TryParsePhoneNumber()` (return `D2Result<string>` for smart-constructor chaining), `[RedactData]` attribute, `D2Env`, `ConnectionStringHelper`, `SerializerOptions`. | [PATTERNS.md](../../../docs/PATTERNS.md) Utilities section |
| [`resilience/`](resilience/README.md) | **Built** | `RetryHelper` (with `D2Result`-aware overload), `CircuitBreaker<T>`, `Singleflight<TKey, TValue>`, and the `ResilientPipeline<TKey, TValue>` composition surface. | [PATTERNS.md](../../../docs/PATTERNS.md) Resilience section |
| [`i18n-abstractions/`](i18n-abstractions/README.md) | **Built** | Domain-safe slice — `TKMessage` primitive, SrcGen-emitted `TK` constants from `contracts/messages/en-US.json`, `ITranslator` interface. Zero external deps. Drift between JSON and TK code constants is structurally impossible (the constant doesn't exist if the JSON key doesn't). | [PATTERNS.md](../../../docs/PATTERNS.md) i18n section |
| [`i18n-source-gen/`](i18n-source-gen/) | **Built** | Roslyn `IIncrementalGenerator` (netstandard2.0) that emits the `TK.*` constants consumed by `i18n-abstractions/`. Referenced as Analyzer; its dll never ships into any consuming assembly. Lives at its own top-level slot because it has a different TFM and a different consumption pattern from a normal lib. | [PATTERNS.md](../../../docs/PATTERNS.md) i18n section |
| [`i18n/`](i18n/README.md) | **Built** | Runtime `Translator` + `SupportedLocales` + `AddD2I18n` DI extension. Used by Courier-style outbound notifications; HTTP responses ship `TKMessage` objects unchanged for client-side translation via SvelteKit/Paraglide. | [PATTERNS.md](../../../docs/PATTERNS.md) i18n section |
| [`auth-abstractions/`](auth-abstractions/README.md) | **Built** | Identity / authorization vocabulary AND consumer-side runtime contracts — `OrgType`, `Role`, `ActorKind`, `ImpersonationKind`, `ActionSensitivity`, `JwtClaimTypes`, `RequestHeaders`, `ActorEntry`, the SrcGen-emitted `Scopes` and `Audiences` static partial classes, plus the read-only `IJwksProvider` / `ISessionLivenessTracker` interfaces and `JwksKeySetSnapshot` record returned by them. Depends on `result/` + `i18n-abstractions/` (for `D2Result<T>` returns) + `Microsoft.IdentityModel.Tokens` (for `SecurityKey` on the snapshot). Consumed by domain code, context-abstractions, handler-abstractions, and the runtime auth lib. | [PATTERNS.md](../../../docs/PATTERNS.md) Scopes / authorization section |
| [`auth-scopes-source-gen/`](auth-scopes-source-gen/) | **Built** | Roslyn `IIncrementalGenerator` (netstandard2.0) that emits the `Scopes.*` constants for `auth-abstractions/` from `contracts/auth-scopes/scopes.spec.json`. Referenced as Analyzer; its dll never ships into any consuming assembly. | [PATTERNS.md](../../../docs/PATTERNS.md) Scopes / authorization section |
| [`auth-audiences-source-gen/`](auth-audiences-source-gen/README.md) | **Built** | Roslyn `IIncrementalGenerator` (netstandard2.0) that emits the `Audiences.*` const-string catalog for `auth-abstractions/` from `contracts/auth-audiences/audiences.spec.json`. Single source of truth for JWT `aud` claim values + `TokenExchangeClient.ExchangeAsync` `targetAudience` arguments. Referenced as Analyzer. | [PATTERNS.md](../../../docs/PATTERNS.md) Scopes / authorization section |
| [`auth-context-abstractions/`](auth-context-abstractions/README.md) | **Built** | Domain-safe slice of the request context — `IAuthContext` (codegen-emitted from `contracts/auth-context/IAuthContext.spec.json`) plus hand-written `IAuthContextExtensions` (`HasScope`, `IsStaff`, etc.). Lets domain code read caller identity / scopes / impersonation without pulling DI / AspNetCore / Configuration. | [PATTERNS.md](../../../docs/PATTERNS.md) Context section |
| [`context-source-gen/`](context-source-gen/) | **Built** | Roslyn `IIncrementalGenerator` (netstandard2.0). Reads `contracts/{auth,request}-context/*.spec.json` and emits, per target assembly: `IAuthContext.g.cs` into `auth-context-abstractions/`; `IRequestContext.g.cs` + `MutableRequestContext.g.cs` + `PropagatedContext.g.cs` + `PropagatedContextExtensions.g.cs` + `PropagatedContextSerializer.g.cs` (with per-field `maxLength` validation baked from the spec) into `context-abstractions/`. | [PATTERNS.md](../../../docs/PATTERNS.md) Context section |
| [`context-abstractions/`](context-abstractions/README.md) | **Built** | Single-lib home for every spec-driven context primitive. Codegen-emitted: `IRequestContext` interface (extends `IAuthContext`), `MutableRequestContext` concrete, `PropagatedContext` record (the `propagate: true` field subset), `PropagatedContextExtensions` (`ToPropagatedContext` / `ApplyPropagatedContext` projections), `PropagatedContextSerializer` (base64url + JSON codec for the cross-hop `x-d2-context` header, with per-field length caps from the spec). Hand-written RFC helpers ship here too: `ActorChainParser` (RFC 8693 §2.1 nested actor chain, depth-limited strict-mode), `ScopeClaimParser` (RFC 6749 §3.3 SP-only scope string or JSON array). Identity (UserId / OrgId / Scopes / ActorChain) rebuilds from JWT each hop; only the small operational subset propagates. | [PATTERNS.md](../../../docs/PATTERNS.md) Context section |
| [`handler-abstractions/`](handler-abstractions/README.md) | **Built** | Domain-safe slice of the handler stack — `IHandler` / `IHandlerContext` interfaces + the `HandlerOptions` record. Lets domain code reference handler contracts without pulling DI / OpenTelemetry / AspNetCore. | [PATTERNS.md](../../../docs/PATTERNS.md) Handler section |
| [`handler/`](handler/README.md) | **Built** | `BaseHandler<TSelf, TInput, TOutput>` + `HandlerContext` + `HandlerTelemetry` + `AddD2Handler` — the runtime piece every handler in every service inherits (CQRS handlers, repo handlers, messaging consumers, scheduled jobs). Auto-emits 4 OTel metrics per call (invoked / succeeded / failed / duration_ms) plus a per-call span via `ActivitySource`. Universal try/catch shape: `ExecuteAsync` exceptions surface as `D2Result.UnhandledException`; `OperationCanceledException` surfaces as `D2Result.Canceled`. `RunCorePipelineAsync` exposes the captured exception so `BaseRepoHandler` can remap EF/PG-specific exceptions to typed `D2Result` failure codes. | [PATTERNS.md](../../../docs/PATTERNS.md) Handler section |
| [`handler-repo-abstractions/`](handler-repo-abstractions/README.md) | **Built** | Vocabulary for repo-flavored handlers — `DbFailureKind` enum + `IDbExceptionClassifier` interface, plus `D2Result` extension factories (`UniqueViolation()`, `IsDeadlock`, etc.) parallel to the built-in semantic factories on `result/`. Pure abstractions: no EF Core, no Npgsql, no provider deps. | [PATTERNS.md](../../../docs/PATTERNS.md) Repository section |
| [`handler-repo/`](handler-repo/README.md) | **Built** | EF-flavored `BaseRepoHandler` — sits on top of `BaseHandler` and converts captured exceptions into typed `D2Result` failures via an injected `IDbExceptionClassifier`. Provider-specific knowledge lives in sibling packages (`handler-repo-postgres/`, future `handler-repo-sqlserver/`, etc.) — this csproj has zero provider deps. | [PATTERNS.md](../../../docs/PATTERNS.md) Repository section |
| [`handler-repo-postgres/`](handler-repo-postgres/README.md) | **Built** | PostgreSQL implementation of `IDbExceptionClassifier`. Plugs into `BaseRepoHandler` via DI (`services.AddD2Postgres()`). Owns the SQLSTATE matrix + the wrapping rules for `DbUpdateException` ↔ `PostgresException` ↔ raw `NpgsqlException`. | [PATTERNS.md](../../../docs/PATTERNS.md) Repository section |
| [`tests/`](tests/README.md) | **Built** | Test infrastructure for ALL shared libs (deliberately one project — overkill to spin up a separate test csproj for every lightweight lib). | [TESTS.md](../../../docs/TESTS.md) |
| [`service-defaults/`](service-defaults/README.md) | Placeholder | Service composition root — OTel SDK bootstrap, Serilog setup, structured request logging, `[RedactData]` destructuring policy registration. | [PATTERNS.md](../../../docs/PATTERNS.md) (RedactDataDestructuringPolicy mechanics) |
| [`caching-abstractions/`](caching-abstractions/README.md) | **Built** | Shared abstractions for the whole cache stack. Three building-block interfaces (`ICacheBasic`, `ICacheAtomic`, `ICacheBroadcast`) are composed by three marker interfaces — `ILocalCache` (basic + atomic, no broadcast — per-process scope), `IDistributedCache` (all three — cluster scope, every read hits remote), `ITieredCache` (all three — L1+L2 composed, reads from L1 first). Distributed and tiered are method-for-method identical; the marker name carries behavioral intent at the dependency site. All ops return `D2Result<T>` / `D2Result`. | [PATTERNS.md](../../../docs/PATTERNS.md) Cache section |
| [`caching-local-default/`](caching-local-default/README.md) | **Built** | `DefaultLocalCache : ILocalCache` wraps `Microsoft.Extensions.Caching.Memory.IMemoryCache` for value storage + a `ConcurrentDictionary` for the in-process lock state. Direct method dispatch — no `BaseHandler` (per-call handler overhead would be 100× the ~60ns cache work). Static `Meter` for hit/miss/eviction counters. Always sets `Size=1` per entry so `MaxEntries` enforces a real entry-count cap (mitigates the IMemoryCache SizeLimit footgun). | [PATTERNS.md](../../../docs/PATTERNS.md) Cache section |
| [`caching-distributed-redis/`](caching-distributed-redis/README.md) | **Built** | `RedisDistributedCache : IDistributedCache` over StackExchange.Redis — implements all four building blocks (Basic + Atomic + Broadcast + Set). `RedisCacheInvalidationBackplane : ICacheInvalidationBackplane` via Redis pub/sub. `JsonCacheSerializer` default. Internal Lua scripts make compound atomic ops single-round-trip (Increment+TTL, ReleaseLock compare-and-delete, SADD+TTL on first-add). Aggregate `Meter` for hits/misses/sets/removes/broadcasts/errors. Future implementations (Valkey, Memcached, Garnet) would land as sibling `caching-distributed-{impl}/` projects with the same surface. | [PATTERNS.md](../../../docs/PATTERNS.md) Cache section |
| [`caching-tiered/`](caching-tiered/README.md) | **Built** | `DefaultTieredCache : ITieredCache` composes one `ILocalCache` (L1) + one `IDistributedCache` (L2). L2-first writes (no partial-write states), L1-then-L2 reads with populate-on-L2-hit, atomic ops route through L2 with L1 invalidation as side effect. Subscribes to optional `ICacheInvalidationBackplane` at construction for cluster-wide L1 coherency. | [PATTERNS.md](../../../docs/PATTERNS.md) Cache section |
| [`messaging-source-gen/`](messaging-source-gen/) | **Built** | Roslyn `IIncrementalGenerator` (netstandard2.0). Reads `contracts/mq-messages/mq-messages.spec.json` + `contracts/mq-subscriptions/mq-subscriptions.spec.json` and emits `MqMessages.g.cs` + `MqSubscriptions.g.cs` (constants + immutable `MqMessagesRegistry` / `MqSubscriptionsRegistry` lookup tables) into `messaging-abstractions/`. Validates encryption-domain whitelist (against `D2.Shared.Encryption.EncryptionDomains`) at codegen time. | [MESSAGING.md](../../../docs/MESSAGING.md) |
| [`messaging-abstractions/`](messaging-abstractions/README.md) | **Built** | Transport-agnostic surface — `IMessageBus` (+ `WaitForReadyAsync`), `IMessageIdempotencyStore`, `[MqPub(MqMessages.X)]` / `[MqSub(MqSubscriptions.X)]` attributes (default-deny), codegen-emitted `MqMessageDescriptor` / `MqSubscriptionDescriptor` records (+ `TieredRetryDescriptor`), `AmqpHeaders` constants (incl. `traceparent` / `tracestate` / `x-d2-context` / `x-d2-failure-reason`), `QueuePattern` enum, `PublisherOptions`, `SubscriberRegistry` + `SubscriberRegistrar`, `MessagingFailures`, `MessagingJsonOptions`, `DlqFailureMetadata`, plus `services.AddD2SubscribersFromAssembly(...)` + programmatic `AddD2Subscriber<TSub, TIn>(MqSubscriptionDescriptor)` escape hatch. Zero transport deps so domain code can attach `[MqPub]` and request `IMessageBus` without dragging in `RabbitMQ.Client`. | [MESSAGING.md](../../../docs/MESSAGING.md) |
| [`messaging-rabbitmq/`](messaging-rabbitmq/README.md) | **Built** | Default `RabbitMQ.Client 7.x` impl of `messaging-abstractions/`. Singleton bus (per-publish transient scope for keyed crypto + `IRequestContext` snapshot), `ID2Connection` with lazy reconnect loop, bounded publisher channel pool with `IdleTtl` eviction, body composition (typed message JSON, optional AES-256-GCM via `D2.Shared.Encryption` — no envelope wrapper), `MessageWireResolver` (`Type → MqMessageDescriptor` via `[MqPub]` + codegen registry), full W3C `traceparent` / `tracestate` cross-hop propagation, `x-d2-context` header for `PropagatedContext`, publisher confirms + transient-classifier retry, idempotent topology (exchanges + DLX + DLQ + optional retry tiers), per-subscriber `IChannel` + dedicated republish channel (DLQ republish-with-failure-header), in-flight callback drain on disposal, narrow-catch around `BasicAck`, `x-death`-driven `RETRIES_EXHAUSTED` enforcement (filtered by reason), composition-time `WaitForConfirm`↔`PublisherConfirmsEnabled` validation, `CacheIdempotencyStore` over `IDistributedCache` (or operator-provided fake), PII-safe log delegates (`SanitizedExceptionRender`). One-call DI: `AddD2MessagingRabbitMq(...)`. | [MESSAGING.md](../../../docs/MESSAGING.md) |
| [`encryption/`](encryption/README.md) | **Built** | `PayloadCryptoKeyring` (immutable, JWKS-style multi-kid, `IDisposable` zeroes key bytes), `IPayloadCrypto` + `PayloadCrypto` (AES-256-GCM, per-call `AesGcm`, AAD bound to the keyring's context bytes), self-describing frame format `[v1][kid_len][kid][nonce:12][cipher+tag]`, typed exception hierarchy, keyed-services DI helper (`AddD2EncryptionFor`), and an opt-in `AddD2EncryptionStartupCheck` that round-trips a sentinel per registered domain at boot. Pure crypto primitive — knows nothing about domains, message buses, or key fetching. | [SECURITY-RUNBOOKS.md](../../../docs/SECURITY-RUNBOOKS.md) |
| [`geo-reference/`](geo-reference/README.md) | Placeholder | Embedded geographic reference data — countries, IANA timezones, currencies, locales, regions. Not a service. | — |
| [`location/`](location/README.md) | Placeholder | Location value objects — `AdminLocation` (country / state / city / postal), `Coordinates`, `StreetAddress`. Content-addressable hash IDs (built-in dedup + cacheability). | [OPERATIONAL-GUARANTEES.md](../../../docs/OPERATIONAL-GUARANTEES.md) (content-addressable entities) |
| [`contacts/`](contacts/README.md) | Placeholder | Contact entity + per-consuming-service DB pattern. Library owns its own `DbContext` + migrations; consuming service provides connection string. | [OPERATIONAL-GUARANTEES.md](../../../docs/OPERATIONAL-GUARANTEES.md) (immutability rationale) |
| [`auth/`](auth/README.md) | **Built** | Inbound auth runtime core — `AddD2Auth` DI composition root, `AuthOptions`, `JwtValidator` (signature + standard-claim validation), `ClaimsToContextMapper` (claims → `IRequestContext` projection), `HttpJwksProvider` + `JwksBackplaneSubscriber`, `TieredCacheSessionLivenessTracker` + `SessionRevokedBackplaneSubscriber`, `AuthFailures` semantic-helper failures (`Bearer*` / `Jwt*` / `Jwks*` / `Session*` 401/503 catalogue), `AuthErrorCodes` granular `d2_error_code` constants, `AuthTelemetry` (4 counters + 3 histograms), `AuthLog` PII-safe `[LoggerMessage]` delegates. Transport-binding csprojs `auth-http/` + `auth-grpc/` register the per-transport pipeline. The vocabulary slice (`Scopes`, enums, claim type strings, contracts) ships in `auth-abstractions/`. | [SECURITY-RUNBOOKS.md](../../../docs/SECURITY-RUNBOOKS.md) |
| [`auth-http/`](auth-http/README.md) | **Built** | HTTP-transport binding for `auth/` — convention-based `JwtAuthMiddleware` that runs the validator + session liveness on inbound HTTP requests, emits RFC 7807 ProblemDetails on failure (single emit point via `D2ProblemDetailsExtensions`), and supports per-endpoint scope requirements via `EndpointScopeMetadata` + `RequireD2Scope` / `AllowD2Anonymous` fluent extensions. `AddD2AuthHttp()` registers `IHttpContextAccessor` + a scoped `IRequestContext` resolver reading from the cross-transport `HttpContext.Items` slot. AspNetCore framework reference is opt-in via this csproj. | [SECURITY-RUNBOOKS.md](../../../docs/SECURITY-RUNBOOKS.md) |
| [`auth-grpc/`](auth-grpc/README.md) | **Built** | gRPC-transport binding for `auth/` — server-side `JwtAuthInterceptor` (covers all four RPC kinds via a shared pipeline) that runs the validator + session liveness on inbound gRPC calls, emits `RpcException(Status, Trailers)` with the `d2_error_code` / `d2_messages` / `traceid` trailer triple via `D2RpcStatusExtensions`, supports per-method scope metadata via attribute (`[D2RequireScope]` / `[D2AllowAnonymous]`) OR fluent (`RequireD2Scope` / `AllowD2Anonymous` on `MapGrpcService<T>()`). `AddD2AuthGrpc()` registers the interceptor + a scoped `IRequestContext` resolver reading from the cross-transport `HttpContext.Items` slot (interceptor dual-writes to `ServerCallContext.UserState` for the gRPC hot-path accessor). `Grpc.AspNetCore.Server` framework reference is opt-in via this csproj. | [SECURITY-RUNBOOKS.md](../../../docs/SECURITY-RUNBOOKS.md) |
| [`auth-outbound/`](auth-outbound/README.md) | **Built** | Outbound auth runtime — `IServiceIdentityClient` (`client_credentials`) + `ITokenExchangeClient` (RFC 8693) + `.AddD2ServiceIdentity()` per-channel gRPC opt-in. OIDC discovery via `D2_AUTH_ISSUER`. ServiceIdentity caches in-process with proactive refresh; TokenExchange caches in `ILocalCache` with sessionId reverse-index for backplane-driven invalidation. | [SECURITY-RUNBOOKS.md](../../../docs/SECURITY-RUNBOOKS.md) |

## Dependency graph (built libs only)

The chart below shows the actual `<ProjectReference>` graph of the libraries currently marked **Built**. Placeholders are not shown — the shape of their deps will evolve as they're implemented. Update this chart as part of adding / modifying any shared lib.

The `tests/` project is omitted because it depends on every shared lib (test infra) and nothing depends on it — including it would clutter the chart without showing any structural information.

The chart is sliced into four subgraphs that mirror the layering rules. Within each subgraph, dependencies flow downward; cross-subgraph arrows go LEFT-to-RIGHT (foundation → context → handler → repo). To minimize crossings, only one direct cross-cluster arrow per pair is shown when a transitive path through a same-cluster intermediate exists; the prose under the chart calls out the load-bearing redirects.

```mermaid
graph LR
    classDef built fill:#d4edda,stroke:#28a745,color:#000
    classDef analyzer fill:#fff3cd,stroke:#856404,color:#000

    subgraph FOUNDATION["Foundation (result + i18n + utilities + resilience)"]
        direction TB
        I18nSG[i18n-source-gen]:::analyzer
        I18nAbs[i18n-abstractions]
        I18n[i18n]
        Result[result]
        Utilities[utilities]
        Resilience[resilience]

        I18nSG -.->|analyzer| I18nAbs
        I18n --> I18nAbs
        I18n --> Utilities
        Result --> I18nAbs
        Utilities --> Result
        Resilience --> Result
    end

    subgraph AUTHCTX["Auth + per-request context (codegen-driven)"]
        direction TB
        AuthScopesSG[auth-scopes-source-gen]:::analyzer
        AuthAudiencesSG[auth-audiences-source-gen]:::analyzer
        AuthAbs[auth-abstractions]
        ContextSG[context-source-gen]:::analyzer
        AuthCtxAbs[auth-context-abstractions]
        CtxAbs[context-abstractions]

        AuthScopesSG -.->|analyzer| AuthAbs
        AuthAudiencesSG -.->|analyzer| AuthAbs
        ContextSG -.->|analyzer| AuthCtxAbs
        ContextSG -.->|analyzer| CtxAbs
        AuthCtxAbs --> AuthAbs
        CtxAbs --> AuthCtxAbs
        CtxAbs --> AuthAbs
    end

    %% auth-abstractions also references the foundation Result + i18n-abstractions
    %% (for D2Result<T> on IJwksProvider / ISessionLivenessTracker contracts) plus
    %% Microsoft.IdentityModel.Tokens (NuGet) for SecurityKey on JwksKeySetSnapshot.
    %% Cross-subgraph edge from AuthAbs to Result is implicit but load-bearing.
    AuthAbs --> Result

    subgraph HANDLER["Handler stack"]
        direction TB
        HandlerAbs[handler-abstractions]
        Handler[handler]

        Handler --> HandlerAbs
    end

    subgraph AUTHRUNTIME["Auth runtime"]
        direction TB
        Auth[auth]
        AuthHttp[auth-http]
        AuthGrpc[auth-grpc]
        AuthOutbound[auth-outbound]

        Auth --> AuthAbs
        Auth --> AuthCtxAbs
        Auth --> CtxAbs
        AuthHttp --> Auth
        AuthHttp --> AuthAbs
        AuthHttp --> CtxAbs
        AuthGrpc --> Auth
        AuthGrpc --> AuthAbs
        AuthGrpc --> CtxAbs
        AuthOutbound --> AuthAbs
        AuthOutbound --> CtxAbs
    end

    subgraph REPO["Repo handler (provider-pluggable)"]
        direction TB
        RepoAbs[handler-repo-abstractions]
        Repo[handler-repo]
        RepoPg[handler-repo-postgres]

        Repo --> RepoAbs
        RepoPg --> RepoAbs
    end

    subgraph CRYPTO["Crypto primitives"]
        direction TB
        Encryption[encryption]
    end

    subgraph CACHING["Cache stack"]
        direction TB
        CacheAbs[caching-abstractions]
        CacheLocal[caching-local-default]
        CacheRedis[caching-distributed-redis]
        CacheTiered[caching-tiered]

        CacheLocal --> CacheAbs
        CacheRedis --> CacheAbs
        CacheTiered --> CacheAbs
    end

    subgraph MESSAGING["Messaging stack"]
        direction TB
        MsgSrcGen[messaging-source-gen]:::analyzer
        MsgAbs[messaging-abstractions]
        MsgRabbit[messaging-rabbitmq]

        MsgSrcGen -.->|analyzer| MsgAbs
        MsgRabbit --> MsgAbs
    end

    %% Cross-subgraph dependencies (only direct refs that aren't transitively
    %% implied by an intra-subgraph path).
    CtxAbs --> Utilities
    HandlerAbs --> CtxAbs
    HandlerAbs --> Result
    Repo --> Handler
    RepoAbs --> I18n
    CacheAbs --> Result
    CacheLocal --> Utilities
    CacheRedis --> Utilities
    AuthOutbound --> CacheAbs
    AuthOutbound --> Resilience
    Auth --> CacheAbs
    Auth --> CacheTiered
    Auth --> Resilience
    Auth --> Result
    Auth --> Utilities
    MsgAbs --> Handler
    MsgAbs --> Encryption
    MsgRabbit --> Encryption
    MsgRabbit --> CacheAbs
    MsgRabbit --> Resilience

    class I18nAbs,I18n,Result,Utilities,Resilience,AuthAbs,AuthCtxAbs,CtxAbs,HandlerAbs,Handler,RepoAbs,Repo,RepoPg,Encryption,CacheAbs,CacheLocal,CacheRedis,CacheTiered,Auth,AuthHttp,AuthGrpc,AuthOutbound,MsgAbs,MsgRabbit,MsgSrcGen built
```

**Reading the chart:**

- **Solid arrows = `<ProjectReference>`** (runtime dep). **Dashed arrows = `OutputItemType="Analyzer"`** (build-time only — the analyzer dll never ships in the consumer's `bin/`).
- **Yellow nodes = analyzers** (i18n / auth-scopes / context source generators). They emit code into the consuming assembly at compile time.
- **Green nodes = runtime libs.**
- **`tests/` is omitted** — it depends on every shared lib (test infra) and nothing depends on it; including it would clutter the chart without showing any structural information.

**Why some arrows are not drawn explicitly:** several csprojs add direct `<ProjectReference>`s that are also reachable transitively through an intra-subgraph hop. To keep the chart legible, those redundant edges are hidden — the live deps still exist in the csprojs:

- `context-abstractions` directly refs `auth-context-abstractions` + `auth-abstractions` (parsers use `ActorEntry` / enums)
- `handler` directly refs `result` (transitive via `handler-abstractions`)
- `handler-repo` directly refs `handler-abstractions`, `result` (transitive via `handler` and `handler-repo-abstractions`)
- `handler-repo-abstractions` directly refs `result` (transitive via `i18n`)
- `caching-abstractions`, `caching-local-default`, `caching-distributed-redis` each directly ref `i18n-abstractions` (transitive via `result → i18n-abstractions`)
- `auth-abstractions` directly refs `i18n-abstractions` (transitive via `result → i18n-abstractions`)
- `caching-tiered` directly refs `result` (transitive via `caching-abstractions → result`)
- `auth-outbound` directly refs `auth-context-abstractions` (transitive via `context-abstractions → auth-context-abstractions`)
- `auth` directly refs `i18n-abstractions`, `auth-context-abstractions` (transitive via `auth-abstractions` / `context-abstractions`)
- `auth-http` and `auth-grpc` directly ref `result`, `i18n-abstractions`, `utilities` (transitive via `auth → result/i18n-abstractions/utilities`)

**Load-bearing direct edges that may LOOK redundant — do NOT prune:**

- `auth-http → auth-abstractions` and `auth-grpc → auth-abstractions` are **NOT** transitively redundant via `auth`. Both transport csprojs reach the shared `D2HttpContextItems` slot key (lives in `auth-abstractions`) directly, NOT via `auth`. The cross-transport `IRequestContext` resolver pattern depends on both transports reading + writing to the same constant — both transports MUST take a direct dep on `auth-abstractions` to see it. Pruning either edge thinking it's redundant via `auth` silently breaks the resolver pattern.
- `messaging-abstractions` directly refs `result`, `i18n-abstractions`, `utilities` (transitive via `handler`)
- `messaging-rabbitmq` directly refs `handler`, `result`, `i18n-abstractions`, `utilities` (transitive via `messaging-abstractions`)

The cross-subgraph arrows that ARE drawn capture every load-bearing inter-cluster dep:

- `context-abstractions → utilities` — uses `Falsey` / `TryParseTruthyNull` extensions in the parsers
- `handler-abstractions → context-abstractions` — `IHandlerContext` exposes `IRequestContext`
- `handler-abstractions → result` — `IHandler.HandleAsync` returns `D2Result<TOutput?>`
- `handler-repo → handler` — `BaseRepoHandler` extends `BaseHandler`
- `handler-repo-abstractions → i18n` — typed `D2Result.X()` factories use `TK.Common.Errors.*` for default messages
- `caching-abstractions → result` — every cache op returns `D2Result<T>` / `D2Result`
- `messaging-abstractions → handler` — `BaseHandler<THandler, TIn, Unit>` is the type constraint on subscribers + `IHandlerContext` flows into messaging through the same context envelope
- `messaging-abstractions → encryption` — `messaging-source-gen` reads the encryption-domain constants from `D2.Shared.Encryption.EncryptionDomains` at codegen time to validate every `mq-messages.spec.json` entry's `encryption` value (D2MQ004 fires on drift)
- `caching-local-default → utilities` — `Falsey()` / `IsNonPositive` for input validation
- `caching-distributed-redis → utilities` — `Falsey()` / extension methods for input validation
- `auth-outbound → caching-abstractions` — `ILocalCache` backs the service-identity cache; `IDistributedCache` backs the token-exchange cache
- `auth-outbound → resilience` — `Singleflight` collapses concurrent token-fetch attempts; `RetryHelper` drives transient-failure retries
- `messaging-rabbitmq → encryption` — `IPayloadCrypto` per encryption domain is keyed-DI-resolved when composing message bodies
- `messaging-rabbitmq → caching-abstractions` — `CacheIdempotencyStore` backs `IMessageIdempotencyStore` onto `IDistributedCache`
- `messaging-rabbitmq → resilience` — `RetryHelper.RetryAsync` drives the publisher's transient-retry loop

## Conventions

- **Folder naming**: lowercase outer (`handler/`, `caching-distributed-redis/`)
- **Project naming**: PascalCase dot-separated (`D2.Shared.Handler.csproj` lives in `handler/`)
- **One handler per file** under `Handlers/{TLC}/{3LC}/` per [PATTERNS.md](../../../docs/PATTERNS.md) TLC convention
- **Every project has a `README.md`**
- **Update the dep graph above** when adding / removing a shared lib or changing its `<ProjectReference>` set
- **Abstractions slices stay zero-external-dep** so domain code can reference them freely. Runtime concretes (DI, OTel, EF, ASP.NET Core) live in sibling non-`-abstractions` packages.

## Build

```bash
dotnet build server/D2.slnx        # full solution (includes all shared libs + services)
```

Each lib registers its DI surface via an `AddXxx(IServiceCollection)` extension method — consuming services compose them at the composition root.
