<!--
Copyright (c) DCSV. All rights reserved.
-->

# PATTERNS.md — D²-WORX Code Patterns

Directory of the load-bearing patterns + cross-cutting conventions every D²-WORX shared library and service embodies. Engineers implementing new handlers, libs, or services find here the high-level shape + jump-links to the canonical per-lib READMEs that own the full reference. Three sections stay at depth here because they are codebase-wide conventions with no per-lib canonical home (TLC folder convention, spec-driven codegen philosophy, smart-constructor domain validation); every other entry is a thin description + small example + link.

> This doc INTENTIONALLY summarizes content canonical at the cited per-lib READMEs. Updates to behavior land in the per-lib README FIRST, then propagate to the PATTERNS.md summary only if the at-a-glance description itself changes. Per `docs/dev/rules.md` §11.32 condensed-view discipline.

## Table of contents

1. [.NET project layout (`.csproj`)](#net-project-layout-csproj)
2. [TLC / 2LC / 3LC folder convention](#tlc--2lc--3lc-folder-convention)
3. [Handler](#handler)
4. [D2Result](#d2result)
5. [Utilities](#utilities)
6. [Time / Temporal](#time--temporal)
7. [Resilience](#resilience)
8. [Repository](#repository)
9. [Cache](#cache)
10. [Composition root](#composition-root)
11. [Logging](#logging)
12. [Telemetry](#telemetry)
13. [AspNetCore](#aspnetcore)
14. [JWT inbound auth](#jwt-inbound-auth)
15. [Translation — none on HTTP path (intentionally)](#translation--none-on-http-path-intentionally)
16. [Configuration](#configuration)
17. [i18n](#i18n)
18. [Messaging](#messaging)
19. [SAGA — cross-service synchronous compensation](#saga--cross-service-synchronous-compensation)
20. [Multi-instance scaling](#multi-instance-scaling)
21. [Mappers](#mappers)
22. [Batch operations](#batch-operations)
23. [Content-addressable entities](#content-addressable-entities)
24. [Health checks](#health-checks)
25. [PII redaction — `[RedactData]`](#pii-redaction--redactdata)
26. [Spec-driven codegen — the cross-cutting pattern](#spec-driven-codegen--the-cross-cutting-pattern)
27. [Domain validation — smart-constructor pattern](#domain-validation--smart-constructor-pattern)
28. [Anti-patterns to actively avoid](#anti-patterns-to-actively-avoid)

---

## .NET project layout (`.csproj`)

Universal build properties (`TargetFramework`, `LangVersion`, `Nullable`, `ImplicitUsings`, `TreatWarningsAsErrors`, `GenerateDocumentationFile`, `StyleCop.Analyzers`, `stylecop.json` link, four global usings) live in `server/Directory.Build.props` and apply to every csproj automatically. Per-project files only declare what's project-specific.

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <RootNamespace>D2.Shared.{LibName}</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="SomePackage" />   <!-- no Version="..." — CPM handles it -->
  </ItemGroup>
</Project>
```

**SDKs**: shared libs + services use `Microsoft.NET.Sdk`; HTTP/gRPC service entry projects use `Microsoft.NET.Sdk.Web`; test projects add `<IsPackable>false</IsPackable>` + `<IsTestProject>true</IsTestProject>` + `<UseMicrosoftTestingPlatformRunner>true</UseMicrosoftTestingPlatformRunner>` + `<TestingPlatformDotnetTestSupport>true</TestingPlatformDotnetTestSupport>` (xUnit v3 + MTP).

**`RootNamespace`** is always declared explicitly and follows the **namespace structure**, not the directory path (e.g., `DistributedCache.Redis.csproj` under `Implementations/Caching/Distributed/` has `RootNamespace=D2.Shared.DistributedCache.Redis` — path noise dropped). **Central Package Management** pins every version in `server/Directory.Packages.props`; csproj `<PackageReference>` items reference by ID only (CPM rejects `Version="..."`). **`dotnet build` enforces zero warnings** via `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` — fix or `.editorconfig`-override with rationale; never suppress.

Lib inventory + per-csproj READMEs → [`server/shared/dotnet/README.md`](../server/shared/dotnet/README.md).

---

## TLC / 2LC / 3LC folder convention

Three-tier folder hierarchy for all backend code. **TLC** = architectural concern, **2LC** = implementation type, **3LC** = operation type.

### Canonical TLCs (with their 3LC alphabets)

| TLC            | 3LC verbiage                                              | Meaning                   |
| -------------- | --------------------------------------------------------- | ------------------------- |
| **CQRS**       | `C/` Commands, `Q/` Queries, `U/` Utilities, `X/` Complex | Business operation intent |
| **Messaging**  | `Pub/` Publishers, `Sub/` Subscribers                     | Message direction         |
| **Repository** | `C/` Create, `R/` Read, `U/` Update, `D/` Delete          | CRUD operation            |
| **Caching**    | `C/` Create, `R/` Read, `U/` Update, `D/` Delete          | CRUD operation            |
| **Outbound**   | (per-protocol — `Grpc/`, `Http/`, `S3/`, etc.)            | Outbound integrations     |
| **Realtime**   | `Push/`                                                   | Real-time push (SignalR)  |
| **Storage**    | `C/` Create, `R/` Read, `U/` Update, `D/` Delete          | Object / file storage     |

- **Interfaces** live in `Interfaces/{TLC}/Handlers/{3LC}/`
- **Implementations** live in `Implementations/{TLC}/Handlers/{3LC}/` (app layer) or `{TLC}/Handlers/{3LC}/` (infra layer)
- **One handler per file** under each 3LC subdirectory; one handler interface per file under `Interfaces/{TLC}/Handlers/{3LC}/`. Consumers `using` the namespaces directly — no `partial` interface aggregation, no grouping aliases.

### Capability vs Dependency

A handler's TLC reflects **what it does** (capability), not **what it uses** (dependency). A query handler that internally consults a cache is still a `Q/` handler — it doesn't become `Caching/Q/` because it reads cache. Reserve TLC for the primary capability of the handler.

### CQRS Q vs C distinction

| Type        | Distributed cache | DB write | External API | Message publish | Test                                                       |
| ----------- | ----------------- | -------- | ------------ | --------------- | ---------------------------------------------------------- |
| **Query**   | No                | No       | No           | No              | "If the process dies after, would state persist?" → **No** |
| **Command** | Yes               | Yes      | Yes          | Yes             | Primary intent = mutation of persistent / shared state     |
| **Complex** | Yes               | Yes      | Yes          | Yes             | Primary intent = retrieval, but may mutate as side effect  |
| **Utility** | Varies            | Varies   | Varies       | Varies          | Shared logic invoked by other handlers as a building block |

Local / in-memory caching is permitted as an invisible optimization — instance-scoped, ephemeral, doesn't affect other instances. A query that warms a local memory cache is still a query.

### Why Utility lives in app layer, not domain

A Utility handler exists when the same logic is needed by multiple Q/C/X handlers AND requires something domain shouldn't carry: third-party libs (HTTP / JSON / regex / crypto), DI-injected services (logger, telemetry, options, other handlers), or handler-pattern infrastructure (auto-emitted OTel metrics, `[RedactData]` integration, `DefaultOptions`, structured I/O logging). Domain stays pure — only entities, value objects, business rules, and helper methods with ZERO external deps. Conversely, if logic is genuinely pure, put it on the domain entity / value object as a method; don't manufacture a Utility handler "just in case."

### Verb semantics

- **Find** = "Resolve this for me" — may fetch from external source, may cache / persist. Example: `FindWhoIs`.
- **Get** = "Give me this by ID" — direct lookup, read-only. Example: `GetWhoIsByIds`.

---

## Handler

`.NET`: `BaseHandler<TSelf, TInput, TOutput>` with using aliases (`H`, `I`, `O`), `IHandlerContext`, `DefaultOptions` override. `RunCorePipelineAsync` (sealed) hosts the observability pipeline (activity, span tags, log scope, stopwatch, 4 OTel metrics, universal try/catch); `HandleAsync` is virtual and one-line-pass-through by default. Repo handlers inherit `BaseRepoHandler<TSelf, TInput, TOutput>` which adds typed DB-failure mapping via an injected `IDbExceptionClassifier`.

```csharp
public sealed class CreateUser(HandlerContext<CreateUser> ctx, IDbExceptionClassifier cls, IAppDbContext db)
    : BaseRepoHandler<CreateUser, CreateUserInput, UserDto>(ctx, cls), ICreateUser
{
    protected override async ValueTask<D2Result<UserDto?>> ExecuteAsync(
        CreateUserInput input, CancellationToken ct)
    {
        var user = User.Create(input);
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        return D2Result<UserDto?>.Created(user.ToDto());
    }
}
```

> Duplicated from [`server/shared/dotnet/handler/README.md`](../server/shared/dotnet/handler/README.md) for at-a-glance directory access. Canonical full reference (pipeline shape, `MapDbException` override, typed booleans, observability surface) lives in the lib READMEs — update both in lockstep. See also: [`handler-repo/README.md`](../server/shared/dotnet/handler-repo/README.md), [`handler-repo-postgres/README.md`](../server/shared/dotnet/handler-repo-postgres/README.md).

---

## D2Result

Result objects replace exceptions for control flow. Use **semantic factories** — `Ok` / `Created` / `NotFound` / `Unauthorized` / `Forbidden` / `ValidationFailed` / `Conflict` / `ServiceUnavailable` / `UnhandledException` / `PayloadTooLarge` / `TooManyRequests` / `Canceled` / `SomeFound`. Raw `Fail()` is reserved for re-mapping arbitrary upstream codes. `Messages` is `IReadOnlyList<TKMessage>` and `InputErrors` is `IReadOnlyList<InputError>` — every user-visible message is a translation key by construction (the only way to construct a `TKMessage` is via the SrcGen-emitted `TK.*` constants).

```csharp
var orderR = await getOrder.HandleAsync(input);
if (orderR.BubbleOnFailure<Order, OutputDto>(out var bubbled, out var order))
    return bubbled;

// continue with `order` as a strongly-typed local
return D2Result<OutputDto>.Ok(order.ToDto());
```

Partial-success ladder: `NotFound` (none resolved, Success=false) → `SomeFound` (partial, data attached, Success=false) → `Ok` (all resolved, Success=true). Callers use `IsPartialOrMissing` (`IsNotFound || IsSomeFound`) for cache-fallback. `Forbidden` is returned when an authenticated caller lacks the required scope.

> Duplicated from [`server/shared/dotnet/result/README.md`](../server/shared/dotnet/result/README.md) for at-a-glance directory access. Full factory catalog, `BubbleFail` / `Bubble`, per-code booleans (`IsTransientRetryable` / `IsTransientDbFailure`), monadic `Bind` / `Map` / `ThenAsync` / `Match`, and auto-injected `traceId` semantics live in the lib README — update both in lockstep.

---

## Utilities

`D2.Shared.Utilities` ships null-safe extensions (`Truthy()` / `Falsey()` over `string?` / `IEnumerable<T>?` / `Guid` / `Guid?`), boundary normalizers (`ToNullIfEmpty()`, `CleanStr()`, `CleanDisplayStr()`), `D2Result`-returning validators (`TryParseEmail()`, `TryParsePhoneNumber()`), the `[RedactData]` attribute, frozen `JsonSerializerOptions` presets (`SR_IgnoreCycles`, `SR_Web`, `SR_WebIgnoreNull`), `ConnectionStringHelper` for `redis://` / `postgresql://` / `amqp://` URI parsing, and `D2Env.Load(...)` for `.env*` loading in host scenarios. Reach for these at every boundary BEFORE hand-rolling — they prevent a whole class of subtle null-handling and string-shape bugs.

```csharp
if (rawEmail.Falsey()) return D2Result<Contact>.ValidationFailed(...);
var emailR = rawEmail.TryParseEmail();          // returns D2Result<string>
if (emailR.BubbleOnFailure<string, Contact>(out var bubbled, out var email))
    return bubbled;
// `email` is non-null here; chain into the next primitive.
```

Canonical: [`server/shared/dotnet/utilities/README.md`](../server/shared/dotnet/utilities/README.md).

---

## Time / Temporal

`D2.Shared.Time` (.NET) and `@d2/time` (TypeScript) wrap NodaTime (.NET) and the TC39 `Temporal` API (TS, polyfilled via `temporal-polyfill`) to give every service the same temporal vocabulary. BCL `DateTime` / `DateTimeOffset` are forbidden in new code: they silently apply current DST rules to historical wall-clock values (wrong for invoicing / audit), have no DI-friendly clock abstraction, and the Windows-vs-Linux `TimeZoneInfo` name fragility surprises ops migrations. NodaTime + IANA tzdb fix all three; the `Temporal` API mirrors NodaTime's vocabulary on the JS side.

The `IClock` injection seam is the universal mockability primitive — a single-method interface (`Instant GetCurrentInstant()` in .NET, `Temporal.Instant getInstant()` in TS). `SystemClock` is bound to `IClock` in each service's composition root; `TestClock` (mutable, thread-safe, `Advance(Duration)` + `SetTo(Instant)`) is constructed directly in tests for deterministic time. Production code NEVER reads `DateTime.UtcNow` / `Temporal.Now.instant()` directly — it always goes through `IClock`.

Every timestamp in the codebase is categorized at design time into one of three buckets, and the xmldoc / TSdoc summary on the field declares which:

- **Category 1 — Past instant with original wall-clock context** (`ZonedInstant`): UTC `Instant` + IANA tz id, for events whose displayed local time must remain reconstructable across server tz / DST drift (audit log entries, login events, signed-at timestamps). Storage: `event_at TIMESTAMPTZ` + `event_at_zone TEXT NULL`. Sort / compare on the `Instant` — zone-agnostic.
- **Category 2 — Future fixed instant** (bare `Instant`): UTC moment with no zone context (JWT exp, idempotency-key TTL, session expiry). Storage: `expires_at TIMESTAMPTZ`. No custom type — `Instant` is correct.
- **Category 3 — Future local-anchored event** (`LocalAnchoredEvent`): `LocalDateTime` + IANA tz id + denormalized `Instant? NextFireUtc` cache, for cron-like wall-clock-locked schedules ("every Tuesday at 9 AM Edmonton"). DST ambiguity resolves through `Resolvers.LenientResolver` (deterministic, never throws). Recompute `NextFireUtc` when the tzdb updates or scheduling changes. Sort on `NextFireUtc`.

EF Core wiring is one line inside the Npgsql config lambda — `UseNpgsql(conn, npg => npg.AddD2NodaTime())` — which delegates to the `Npgsql.EntityFrameworkCore.PostgreSQL.NodaTime` plugin's `UseNodaTime()` for the full Instant ↔ `timestamptz` / LocalDateTime ↔ `timestamp` / LocalDate ↔ `date` mapping suite. Idempotent — safe to call from multiple composition roots.

```csharp
public sealed class SignInHandler(IClock clock, IUserRepo users) : ...
{
    protected override async ValueTask<D2Result<SessionDto>> ExecuteAsync(...)
    {
        var now = clock.GetCurrentInstant();          // never DateTime.UtcNow
        var session = new Session { CreatedAt = now, ... };
        // ...
    }
}
```

Canonical: [`server/shared/dotnet/time/README.md`](../server/shared/dotnet/time/README.md) (.NET) and [`server/shared/typescript/time/README.md`](../server/shared/typescript/time/README.md) (TS). Cross-language parity tracked in [PARITY.md](PARITY.md).

---

## Resilience

`D2.Shared.Resilience` ships `CircuitBreaker<T>` (three-state lock-free), `Singleflight<TKey, TValue>` (concurrent-call deduplication; first caller runs the operation, siblings share its `Task<TValue>`), `RetryHelper.RetryAsync<T>` + the `D2Result`-aware `RetryD2ResultAsync<TData>` overload (exponential backoff + jitter + transient classifier), and the `ResilientPipeline<TKey, TValue>` composition surface. Compose via the fluent DSL at the composition root; handlers inject `ResilientPipeline<TKey, TValue>` and call `pipeline.ExecuteAsync(key, op, ct)`. The pipeline returns `D2Result<TValue>` (never throws) and converts CircuitOpen / cancellation / transient / unknown exceptions to the appropriate result code.

```csharp
services.AddResilientPipeline<string, MyDto>(p => p
    .UseSingleflight()
    .UseCircuitBreaker()
    .UseRetries(new(maxAttempts: 5)));
```

**Layer order = protection semantic.** `CircuitBreaker → Retries` means retry-inside-CB (upstream-protecting; backoff gives a fragile upstream air). `Retries → CircuitBreaker` means retry-outside-CB (restart-recovery; retry layer treats `CircuitOpenException` as transient and backs off through it). Full state-machine semantics + composition trade-offs → [`server/shared/dotnet/resilience/README.md`](../server/shared/dotnet/resilience/README.md).

---

## Repository

EF Core for all relational data. Repo handlers inherit `BaseRepoHandler` (see [Handler](#handler)); the registered `IDbExceptionClassifier` translates DB exceptions into typed `D2Result` failures (`UniqueViolation`, `ConcurrencyConflict`, `DbDeadlock`, `DbTimeout`, etc.). Callers branch on `result.IsUniqueViolation` / `IsTransientDbFailure` — never on raw SQLSTATE catches.

- **Batch chunking** — PG has a ~32K parameter cap per query (signed-int param index); default chunk size **500**. Use `input.HashIds.Chunk(_BATCH_SIZE)` with `_BATCH_SIZE` via the Options pattern. See [Batch operations](#batch-operations).
- **Partial-success → D2Result mapping** — all resolved → `Ok`; some → `SomeFound`; none → `NotFound`. Never return `Ok` with empty data.
- **UPDATE / DELETE row-count** — `SaveChangesAsync()` returns affected rows. Zero where you expected ≥1 → `NotFound`. Per `rules.md §9.32`.
- **Migrations — generator only.** `dotnet ef migrations add <Name>` is the only path. NEVER hand-edit `*.cs` migration files, `*ModelSnapshot.cs`, or `__EFMigrationsHistory` rows. Multi-replica safety: startup migrator acquires a PG advisory lock. Per `rules.md §9.10`.

---

## Cache

Three marker interfaces composed from four building blocks (`ICacheBasic` + `ICacheAtomic` + `ICacheBroadcast` + `ICacheSet`):

| Marker              | Composes                         | Scope                             | Use for                                                                                         |
| ------------------- | -------------------------------- | --------------------------------- | ----------------------------------------------------------------------------------------------- |
| `ILocalCache`       | Basic + Atomic                   | Per-process (in-process atomics)  | Instance-scoped: per-instance fingerprint cache, hot in-process lookups, single-writer counters |
| `IDistributedCache` | Basic + Atomic + Broadcast + Set | Cluster (every read hits remote)  | Rate-limit counters, distributed locks, FP-too-common detection (the one `ICacheSet` consumer)  |
| `ITieredCache`      | Basic + Atomic + Broadcast       | L1 + L2 composed (with backplane) | Read-heavy entity data where freshness within a few seconds is acceptable                       |

All ops return `D2Result<T>` / `D2Result`. Null / empty inputs return `D2Result.ValidationFailed` (with an `InputError` naming the offending param) — implementations never throw `ArgumentException` for caller mistakes. Cluster-wide L1 coherency uses `ICacheInvalidationBackplane` (Redis pub/sub) — the `*AndBroadcast*` write variants publish on every send.

```csharp
public sealed class GetEntityByIdHandler(ITieredCache cache, IEntityRepo repo) : ...
{
    protected override async ValueTask<D2Result<EntityDto?>> ExecuteAsync(...)
    {
        var cached = await cache.GetAsync<EntityDto>($"Entity:{id}", ct);
        if (cached.IsOk) return cached;
        // ... fall through to repo, then `cache.SetAsync`.
    }
}
```

Canonical: [`server/shared/dotnet/caching-abstractions/README.md`](../server/shared/dotnet/caching-abstractions/README.md). Default impls: [`caching-local-default/`](../server/shared/dotnet/caching-local-default/README.md), [`caching-distributed-redis/`](../server/shared/dotnet/caching-distributed-redis/README.md), [`caching-tiered/`](../server/shared/dotnet/caching-tiered/README.md).

---

## Composition root

Every D² service composes through one ordered call to `D2.Shared.ServiceDefaults`:

```csharp
builder.Services.AddD2ServiceDefaults(builder.Configuration, opts =>
{
    opts.AuthConfigure = auth =>
    {
        auth.Issuer = new Uri("https://edge.example.com");
        auth.Audience = "files-service";
    };
});

var app = builder.Build();
app.UseD2DefaultPipeline();
app.MapD2DefaultEndpoints();
await app.RunD2ServiceAsync("files");
```

`AddD2ServiceDefaults` chains `D2Env.Load` → `AddD2Logging` → `AddD2Telemetry` → `AddD2I18n` → `AddD2Handler` → `AddD2Auth` (+ `.Http` + `.Grpc`) → `AddD2LocalCache` → `AddD2HealthChecks` → `AddD2ProblemDetails` → `AddD2Cors` → standard `HttpClient` resilience handler. The aggregator owns ZERO logic; new options on owning libs flow through via pass-through `Action<TFromOwningLib>?` delegates. `UseD2DefaultPipeline` middleware order is **LOCKED** (no insertion points): security headers → request logging → CORS → routing → infrastructure bypass → authentication → `UseD2Auth` → authorization. Auth wiring is fail-fast (`AuthConfigure` MUST be non-null when `SkipAuthAutoWiring = false`).

> Duplicated from [`server/shared/dotnet/service-defaults/README.md`](../server/shared/dotnet/service-defaults/README.md) for at-a-glance directory access. Full call-order rationale + middleware-order rationale + opt-out matrix (`SkipAuthAutoWiring` / `SkipLocalCacheAutoWiring` / `SkipHttpClientResilienceDefaults`) + thin-aggregator convention test live in the lib README — update both in lockstep.

### 5-layer rename safety net (spec-driven codegen + nameof discipline)

The combination of spec-driven codegen + the `nameof()`-over-literals discipline (forbid raw string literals when emitting codegen'd member names — Serilog property keys, OTel span tag keys, JSON field names mirroring an interface property, telemetry counter tag keys) gives a 5-layer safety net against silent drift on spec rename:

1. **Spec change** regenerates `.g.cs` — new name appears, old disappears.
2. **Consumer compile-fail** — call sites referencing the old member name break.
3. **Production emission compile-fail** — `nameof(IRequestContext.OldName)` bindings break.
4. **Behavioral test compile-fail** — `nameof`-bound test assertions break.
5. **Spec-pin literal test-fail** — separate spec-pin tests assert literal values of emitted symbols, catching intended renames that weren't propagated to operator dashboards / saved queries / alert rules.

Every codegen'd member that ships across an operator-visible boundary (logs, traces, metrics, wire format) earns this 5-layer treatment.

---

## Logging

`D2.Shared.Logging` ships Serilog configuration + `[RedactData]` enforcement via `RedactDataDestructuringPolicy` + the `UseD2RequestLogging` middleware. The destructuring policy reflects over `BindingFlags.Public | Instance` PROPERTIES (not fields) on every `@`-captured object. **Capture mode matters**: Serilog's `@`-prefix invokes destructuring (the policy fires); `{prop}` (no `@`) calls `.ToString()` and bypasses destructuring entirely.

```csharp
logger.LogInformation("Processed {@User}", user);   // PII redacted via [RedactData]
logger.LogInformation("Processed {User}", user);    // bypasses destructuring — leaks
```

The middleware does NOT log the connection-remote IP — at internal services it's the upstream Edge IP, not the client's; at Edge it's PII. 8 NOT-LOGGED fields (`ClientIp`, `City`, `Region`, `SubdivisionCode`, `PostalCode`, `Latitude`, `Longitude`, `Geohash`) are pinned by integration test.

Canonical (full 42 LOG-OK / 8 NOT-LOGGED enumeration + per-source overrides + precedence notes): [`server/shared/dotnet/logging/README.md`](../server/shared/dotnet/logging/README.md).

---

## Telemetry

`D2.Shared.Telemetry` ships OpenTelemetry SDK setup (traces + metrics + logs) + per-signal OTLP exporters (env-var-gated truthy) + an IP-restricted Prometheus scraping endpoint (`MapD2PrometheusEndpoint` — 403 for non-loopback / non-RFC-1918) + cross-lib `ActivitySource` / `Meter` aggregation (4 ActivitySources + 6 Meters via `public const string` symbol references). `OTEL_SDK_DISABLED=true` symmetrically short-circuits BOTH `AddD2Telemetry` AND `MapD2PrometheusEndpoint`. Auto-instrumentations: AspNetCore inbound, HttpClient outbound, GrpcNetClient outbound, Process + Runtime metrics. AspNetCore `Filter` excludes infrastructure paths via the canonical `InfrastructurePathMatcher` from `D2.Shared.AspNetCore`.

Canonical: [`server/shared/dotnet/telemetry/README.md`](../server/shared/dotnet/telemetry/README.md).

---

## AspNetCore

`D2.Shared.AspNetCore` ships cross-cutting middleware + endpoint primitives: `UseD2SecurityHeaders` (OWASP defaults; HSTS only on HTTPS, preload opt-in), `AddD2Cors` / `UseD2Cors` (canonical `D2_CORS_ORIGINS__*` indexed env-var; fail-closed via `ValidateOnStart`), `UseD2InfrastructureBypass` (default short-circuit invokes the matched endpoint's `RequestDelegate` directly — heavy middleware does NOT run on `/health` / `/alive` / `/metrics` / `/.well-known`), `AddD2ProblemDetails` (RFC 7807 customizer; `traceId` / `correlationId` / `instance` enrichment), `AddD2HealthChecks` + `MapD2HealthEndpoints` (`/health` full + `/alive` live-tag split), `RunD2ServiceAsync` (PII-safe `Log.Fatal` rendering — type FullName + first stack frame, NEVER `ex.Message`).

The public static `InfrastructurePathMatcher` is the **single source of truth** for the path set across Logging, Telemetry, and `UseD2InfrastructureBypass`. Canonical: [`server/shared/dotnet/aspnetcore/README.md`](../server/shared/dotnet/aspnetcore/README.md).

---

## JWT inbound auth

RS256 + JWKS-based inbound auth, transport-binding split across three sibling csprojs: **`D2.Shared.Auth`** (runtime — `JwtValidator`, `HttpJwksProvider`, `TieredCacheSessionLivenessTracker`, `AuthFailures`), **`D2.Shared.Auth.Http`** (HTTP middleware — `JwtAuthMiddleware`, RFC 7807 ProblemDetails, `RequireD2Scope("…")` fluent metadata + `[D2RequireScope("…")]` attribute), **`D2.Shared.Auth.Grpc`** (gRPC interceptor — `JwtAuthInterceptor`, `RpcException(Status, Trailers)` shape with `d2_error_code` / `d2_messages` / `traceid` trailers).

```csharp
services.AddD2Auth(opts => { opts.Issuer = ...; opts.Audience = ...; });
services.AddD2AuthHttp();      // and/or AddD2AuthGrpc();
app.UseD2Auth();
app.MapGet("/files/{id}", H).RequireD2Scope("files.read");
```

Per-validation pipeline: bearer extraction (transport layer) → signature + standard-claim validation (RS256 pinned, issuer / audience / lifetime with 30s clock skew, reactive-refresh-on-unknown-`kid`) → session liveness (`TieredCacheSessionLivenessTracker`; fail-closed on liveness store outage) → per-endpoint scope check (transport layer; metadata enumerates required scopes, middleware verifies superset). JWKS at the OIDC-canonical `/.well-known/jwks.json`; cluster-wide JWKS rotation via `ICacheInvalidationBackplane` (Redis pub/sub on `d2.security.key-rotated:jwks`). Uniform **401** at the auth boundary regardless of "JWT bad" vs "scope insufficient" — granularity surfaces only on `d2_error_code`. `MarkAsD2HarmlessEndpoint()` / `[D2HarmlessEndpoint]` opts out of the full pipeline (reserved for k8s probes + OIDC discovery). Both transports register a scoped `IRequestContext` reading from a shared `HttpContext.Items` slot (`D2HttpContextItems.REQUEST_CONTEXT`); gRPC interceptor dual-writes to `ServerCallContext.UserState` for hot-path access.

Canonical: [`server/shared/dotnet/auth/README.md`](../server/shared/dotnet/auth/README.md), [`auth-http/README.md`](../server/shared/dotnet/auth-http/README.md), [`auth-grpc/README.md`](../server/shared/dotnet/auth-grpc/README.md).

---

## Translation — none on HTTP path (intentionally)

There is **no** server-side HTTP translation middleware. `D2Result` ships `TKMessage` objects (`{ "key": "...", "params": { ... }? }`) verbatim over the wire; the SvelteKit client translates on receipt via Paraglide. CDN caching benefits, no `Vary: Accept-Language` fragmentation. The runtime `D2.Shared.I18n.Translator` does exist but is consumed only by **outbound notifications** (Courier emails / SMS / push) where the recipient locale comes from the user profile. See [i18n](#i18n).

---

## Configuration

D² env vars use the **indexed convention** for arrays: `PREFIX__0=value0`, `PREFIX__1=value1`, etc. (NOT comma-separated — that breaks for values containing commas). Matches .NET `IConfiguration` array binding (`__N` index → array element). TS-side `parseEnvArray("PREFIX")` returns the equivalent array.

Connection-string parsers for `postgres://`, `redis://`, `amqp://` are centralized in `D2.Shared.Utilities.ConnectionStringHelper` — never `new Uri(connStr)` ad-hoc. The shared parser handles passwords containing `@`, special characters, and multi-host fallback.

Options pattern (`IOptions<T>` with defaults; configuration section convention) is used everywhere a behavior is tunable per service — batch sizes, cache expirations, retry attempts, lock TTLs, host-specific URIs. Hardcoding what should be in Options is an anti-pattern.

---

## i18n

The i18n stack splits across three csprojs: **`D2.Shared.I18n.Abstractions`** (zero external deps — `TKMessage` primitive, `ITranslator` interface, SrcGen-emitted `TK.*` constants), **`D2.Shared.I18n.SourceGen`** (Roslyn `IIncrementalGenerator` referenced as Analyzer — emits `TK.*` from `contracts/messages/en-US.json`), **`D2.Shared.I18n`** (runtime `Translator` + `SupportedLocales` + `AddD2I18n` DI; consumed only by composition roots + outbound-notification handlers).

`TKMessage` ships verbatim over the wire — same JSON shape in code and on the wire:

```csharp
D2Result<T>.ValidationFailed(messages: [
    TK.Auth.Errors.PASSWORD_WEAK.With("minLength", "12")]);

D2Result<T>.ValidationFailed(inputErrors: [
    new InputError("email", [TK.Common.Validation.EMAIL_INVALID])]);
```

`TKMessage`'s constructor is **internal** — producers can ONLY construct via the SrcGen-emitted `TK.*` constants. "Untranslated literal in `D2Result.Messages`" is structurally unrepresentable. Translation key conventions: `auth_{feature}_{purpose}`, `webclient_{section}_{purpose}`, `common_ui_*` / `common_errors_*`. When adding new keys, add to ALL locale files in `contracts/messages/` simultaneously — the SrcGen surfaces gaps via `D2I18N002` at build time. The 10-locale list is driven by env vars `PUBLIC_ENABLED_LOCALES__*` + `PUBLIC_DEFAULT_LOCALE`.

Canonical: [`i18n-abstractions/README.md`](../server/shared/dotnet/i18n-abstractions/README.md), [`i18n/README.md`](../server/shared/dotnet/i18n/README.md), [`i18n-source-gen/README.md`](../server/shared/dotnet/i18n-source-gen/README.md).

---

## Messaging

Async cross-service messaging uses RabbitMQ. Producers mark message classes with `[MqPub(MqMessages.X)]`; consumers mark handler classes with `[MqSub(MqSubscriptions.Y)]`. Spec files at `contracts/mq-messages/` and `contracts/mq-subscriptions/` are the source of truth — a Roslyn `IIncrementalGenerator` emits typed constants + a runtime descriptor registry into `D2.Shared.Messaging.Abstractions`. The resolver hard-fails on FQN mismatch / unknown constant; the registrar hard-fails on handler-message-type mismatch.

Wire bodies are either plaintext UTF-8 JSON or AES-256-GCM-encrypted (the descriptor's `encryption` field is the keyring domain or the literal `plaintext`). Every published message carries the canonical AMQP header set (`message-id` UUIDv7, `traceparent`, `tracestate`, `x-d2-context`, optional `x-d2-encryption-kid`). **Headers stay plaintext at-rest — identity (UserId / OrgId / Scopes) is NEVER in headers; it rebuilds from the JWT at every sync hop.**

```csharp
services
    .AddD2EncryptionFor(EncryptionDomains.AUDIT, factory: ...)
    .AddD2MessagingRabbitMq(o => o.ConnectionUri = "amqps://...")
    .AddD2Handler()
    .AddD2SubscribersFromAssembly(typeof(MyConsumerAssembly).Assembly);
```

Publishers inject `IMessageBus` and call `PublishAsync(message)`. Consumers inherit `BaseHandler<TSelf, TMessage, Unit>` and override `ExecuteAsync`. Failures route to `{queue}.dlx` with a JSON-encoded `DlqFailureMetadata` header; optional tiered-retry exchanges declared per-subscription when `tieredRetry` is set.

Canonical (full wire format + topology + publisher path + consumer path + DLX / DLQ + tiered retry + encryption posture + operational anti-patterns): [`server/shared/dotnet/messaging-rabbitmq/README.md`](../server/shared/dotnet/messaging-rabbitmq/README.md). Spec authoring + codegen diagnostics → [`messaging-source-gen/README.md`](../server/shared/dotnet/messaging-source-gen/README.md). Transport-agnostic abstractions → [`messaging-abstractions/README.md`](../server/shared/dotnet/messaging-abstractions/README.md).

---

## SAGA — cross-service synchronous compensation

For mutations that must touch state in multiple services, the orchestrating service (typically Edge) coordinates a **synchronous SAGA** when the user expects an immediate, visible result. Ordering: compensable step first (safest to roll back), then the anchor step. If the anchor fails, **compensate** the earlier step (delete / revert). Compensation failure escalates via `logger.fatal` for manual reconciliation; the handler returns the original failure — the user's request is never silently "succeeded" when state is inconsistent.

```csharp
// 1) Compensable step
var geoR = await createContactGeo.HandleAsync(input, ct);
if (geoR.BubbleOnFailure<GeoRecord, ContactDto>(out var bubbled, out var geo)) return bubbled;

// 2) Anchor step
var authR = await linkContactAuth.HandleAsync(new(input.UserId, geo.Id), ct);
if (!authR.IsOk)
{
    var rollback = await deleteContactGeo.HandleAsync(new(geo.Id), ct);
    if (!rollback.IsOk) logger.LogCritical("SAGA compensation failed: {GeoId}", geo.Id);
    return D2Result<ContactDto>.BubbleFail<ContactDto>(authR);
}
return D2Result<ContactDto>.Ok(authR.Data!);
```

Irreversible flows (e.g., user-deletion anonymize) are NOT SAGAs — they're fire-and-forget fanouts; downstream services own their idempotent consumers rather than coordinating rollback. Canonical: not yet shipped; design at [`docs/v2/PHASE_3_EDGE.md §6`](v2/PHASE_3_EDGE.md#6-cross-service-saga-pattern). Will migrate to the Edge lib README when the service ships.

---

## Multi-instance scaling

Every D² service is designed to run with N replicas behind a load balancer. Correctness must not depend on instance affinity — sessions live in Redis (3-tier with PG dual-write), JWT validation reads from shared JWKS, rate-limit counters in Redis (cluster scope, never per-process), HTTP idempotency in Redis (`SET NX` + 24h TTL). Local in-memory caches are per-instance with cluster-wide L1 coherence via `ICacheInvalidationBackplane` (Redis pub/sub) for `*AndBroadcast*` write variants. Background jobs use Redis distributed locks (`SET NX`) — return early if held. Cache-invalidation events use fanout exchanges with exclusive auto-delete queues (not competing consumers) for cluster-wide propagation.

Full service-onboarding checklist (rate limiting / HTTP idempotency / session+auth / local caches / background jobs / cache invalidation / connection strings / DB constraints / migrations / cross-service mutations / encryption): Canonical not yet shipped; design at [`docs/v2/PHASE_3_EDGE.md §5`](v2/PHASE_3_EDGE.md#5-multi-instance-scaling--service-onboarding-checklist). Will migrate to the Edge lib README when the service ships.

---

## Mappers

`{Entity} ↔ {Dto}` projections live in `{Service}.App/Mappers/` as C# 14 extension members. Pure projections — no DI, no IO, no validation. Validation lives in the smart-constructor on the domain type. Use the C# 14 `extension(T target) { ... }` block form per `rules.md §5`, never the old `this T target` parameter style.

```csharp
extension(Order order)
{
    public OrderDto ToDto() => new(order.Id, order.Status, order.Total);
}
```

---

## Batch operations

Batch lookups / updates chunk inputs via `input.HashIds.Chunk(_BATCH_SIZE)` with `_BATCH_SIZE` injected through the Options pattern. Default chunk size **500** per the PG ~32K-parameter limit (see [Repository](#repository)). Partial-success ladder per [D2Result](#d2result): all resolved → `Ok`, some resolved → `SomeFound` (data attached), none → `NotFound`. Hardcoding chunk size in the handler is an anti-pattern — every batch handler exposes the size via its `Options` record so operators can tune per-environment.

---

## Content-addressable entities

Entities whose identity is structurally derivable from their contents use SHA-256 hash IDs (`"v1." + 64-char lowercase hex` — versioned so the hashing scheme can evolve without breaking existing IDs). The factory method computes the hash from canonically-normalized inputs; persistence is dedup-by-key. Enables idempotent re-ingestion + cross-service cache sharing without coordination — two services that independently observe the same payload arrive at the same hash ID and cross-reference via cache without a central registry.

Location is the canonical case. Rather than a single aggregate `Location` record, the shipped design uses three independent value objects (each content-addressable in its own right) plus a free-function composer:

```csharp
// 1. Construct any subset of the three value objects.
var coords = Coordinates.Create(40.7128, -74.0060).Data;
var street = StreetAddress.Create(line1: "350 Fifth Ave", line2: "Floor 86").Data;
var admin  = AdminLocation.Create(
    countryIso31661Alpha2Code: CountryCode.US,
    subdivisionIso31662Code: SubdivisionCode.US_NY,
    city: "New York",
    postalCode: "10118").Data;

// 2. Each VO carries its own `"v1." + SHA-256(...)` HashId.
//    coords.HashId   == "v1.<hex>"  — content-addressable for the cell.
//    street.HashId   == "v1.<hex>"  — content-addressable for the address.
//    admin.HashId    == "v1.<hex>"  — content-addressable for the admin tuple.

// 3. Compose the per-component HashIds into a single location identity.
string? locationHash = ComposeLocationHash.Compose(coords, street, admin);
// `locationHash` is null only when all three inputs are null (location is absent).
```

`WhoIs` follows the same content-addressable pattern with a single aggregate factory. See the per-lib READMEs for the full surface ([D2.Shared.Location](../server/shared/dotnet/location/README.md), [@d2/location](../server/shared/typescript/location/README.md), [WhoIs lib README](../server/shared/dotnet/whois-abstractions/README.md) when introduced).

---

## Health checks

Per-service infrastructure health checks must use the same code path as production — DB checks through EF Core (`AddDbContextCheck<TContext>()`), Redis through `StackExchange.Redis`, RabbitMQ through `RabbitMQ.Client`. A check that bypasses the production library won't detect library-layer issues (serializer config, connection-string parsing, auth). `AddD2HealthChecks` registers a baseline `"self"` check tagged `"live"`; per-service infrastructure layers chain their own. `MapD2HealthEndpoints` maps `/health` (full) + `/alive` (only `"live"`-tagged — k8s liveness split). See [AspNetCore](#aspnetcore).

---

## PII redaction — `[RedactData]`

Every data type carrying PII (emails, phones, IPs, addresses, names, message content, filenames, presigned URLs, AMQP URIs) MUST have the `[RedactData]` attribute. Lives on the type or property (not handlers), applies to ALL Serilog logging recursively, reflection-cached per type, works for `{@obj}` structured logging — `{prop}` (no `@`) bypasses destructuring.

```csharp
[RedactData(Reason = RedactReason.PersonalInformation)]
public sealed record Contact(string Email, string PhoneE164);
```

When `[RedactData]` can't be applied (proto-generated DTOs that ts-proto / protoc-gen-csharp emit without our attribute), use `DefaultOptions.LogInput=false` / `LogOutput=false` on the handler and document the suppressing proto type in the handler's class comment. The capture-mode footgun is documented in [Logging](#logging).

---

## Spec-driven codegen — the cross-cutting pattern

Shared vocabularies that ship across language boundaries (.NET handlers, TS clients, ops dashboards) live in JSON spec files under `contracts/`. A Roslyn `IIncrementalGenerator` reads the spec at every build and emits typed constants directly into the consuming assembly. Hand-mirrored constants are forbidden — drift between spec and code is structurally impossible because the constants don't exist unless the spec entry does.

The pattern in one paragraph: spec at `contracts/{topic}/{topic}.spec.json` (paired with `schema.json`); SourceGen csproj at `server/shared/dotnet/{topic}-source-gen/` (`netstandard2.0`, `IsRoslynComponent=true`, gated by `Compilation.AssemblyName`); consumer csproj wires `<ProjectReference … OutputItemType="Analyzer" ReferenceOutputAssembly="false" />` + `<AdditionalFiles Include="…/{topic}.spec.json" />`; codegen output committed to git under `Generated/` with `linguist-generated=true`; source-gen tests use per-VALUE substring pins (`.Contain("public const string FOO = \"foo\";")`) — never framework snapshots.

When a hand-written constants catalog gets a spec backing, the migration is **outright deletion of the hand-written file plus net-new spec authoring** — not a parallel-emit-then-deprecate dance. A parallel-emit phase would let stale hand-written constants drift undetected.

Canonical: [`docs/SRC_GEN.md`](SRC_GEN.md) — full how-to-author guide for both .NET (Roslyn `IIncrementalGenerator`) and TypeScript (`tools/ts-codegen`). Sourcegen registry (19 spec catalogs, grouped by purpose) → [`server/shared/dotnet/README.md` § Source generators](../server/shared/dotnet/README.md#source-generators-registry).

This pattern is the structural enforcement behind the [5-layer rename safety net](#composition-root) — every renamed spec field cascades through generated code, consumer compile sites, `nameof()`-bound emission sites, behavioral tests, and spec-pin literal tests, in that order.

---

## Domain validation — smart-constructor pattern

Domain types use **smart-constructor factories returning `D2Result<T>`** for all input-validating construction. Throwing constructors are reserved for programmer-bug invariants (null where non-null is required, internal state corruption that can't be triggered by user input).

```csharp
public sealed record Contact
{
    public string Email { get; init; }

    private Contact(string email) => Email = email;

    public static D2Result<Contact> Create(string? rawEmail)
    {
        var emailR = rawEmail.TryParseEmail();

        if (emailR.BubbleOnFailure<string, Contact>(out var bubbled, out var email))
            return bubbled;

        return D2Result<Contact>.Ok(new Contact(email!));
    }
}
```

The pattern:

1. **Private constructor** — domain instances cannot be created bypassing validation.
2. **Static `Create` returning `D2Result<TSelf>`** — primitive-level rules go through the `string?.TryParse*` extensions in `D2.Shared.Utilities`; cross-field rules belong to the `Create` method itself.
3. **`BubbleFail` chains** — each primitive validation result bubbles up; the composite never reports half-validated state.
4. **`TKMessage` keys** — failure messages are `TK.*` constants; the wire-format response slots them straight into `Messages` / `InputErrors`.

Validation is single-layered: smart-constructor factories on domain types are the one place input gets checked. Primitive rules (email shape, phone shape, URL shape) → `string?.TryParse*` extensions in [Utilities](#utilities). Cross-field rules (start-date < end-date, password matches confirm) → composite `Create` method on the domain type; aggregate per-field failures with `D2Result.Combine` so a single submit surfaces every problem at once.

### When to throw vs return

| Case                                                                            | Mechanism                                                                                                 |
| ------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------- |
| User input fails validation                                                     | `D2Result<T>.ValidationFailed` with `TK.*` keys                                                           |
| External lookup misses                                                          | `D2Result<T>.NotFound`                                                                                    |
| Downstream service errors                                                       | `BubbleFail` from the result                                                                              |
| Programmer-bug invariant (null param marked non-null, internal corrupted state) | Throw `ArgumentNullException` / `InvalidOperationException`                                               |
| Cancellation                                                                    | Re-throw `OperationCanceledException` (or let it propagate); `BaseHandler` maps it to `D2Result.Canceled` |

The rule: **anything caused by data the caller controls is a result, not an exception. Anything caused by code that should be impossible is an exception.**

---

## Module-scoped cache-aside (build-once immutable static data)

For services that consume a build-once, never-invalidated, in-process static catalog (e.g. the geo name resolver over the codegen-emitted Country / Subdivision catalogs), the cache-aside pattern lives in a module-scoped `Lazy<FrozenDictionary>` (or `ConcurrentDictionary<TKey, Lazy<...>>` for per-key caches), NOT in `ILocalCache` / `IDistributedCache` / `ITieredCache`.

Rationale (per call-site choice):

- **Build-once-then-read-many** — the underlying data is immutable post-module-init, so TTL / eviction / cross-instance coherency are unnecessary work.
- **Avoiding `D2Result` envelope cost** — `ILocalCache.GetAsync` returns `D2Result<T>` per call; for an O(1) dictionary read the envelope allocation dominates the lookup.
- **Idiomatic .NET** — `static readonly Lazy<FrozenDictionary>` with `LazyThreadSafetyMode.ExecutionAndPublication` is the language-canonical shape for this category.

**Atomic-publish mandate**: each cache target gets ONE `Lazy<T>` whose dictionary value type encodes BOTH the record reference AND any sentinel flags (e.g. ambiguity). Two separate `Lazy<>` fields (record map + companion `FrozenSet<string>` of sentinels) would not publish atomically under a narrow race window. The single-struct value type guarantees that both fields publish in one assignment.

```csharp
internal readonly record struct CountryCacheEntry(Country? Record, bool IsAmbiguous);

internal static readonly Lazy<FrozenDictionary<string, CountryCacheEntry>>
    SR_CountryByName = new(
        BuildCountryByNameMap,
        LazyThreadSafetyMode.ExecutionAndPublication);
```

**Per-key cache-aside**: when each key (e.g. parent country) needs its own build factory and shouldn't trigger work for other keys, wrap each per-key map in its own `Lazy<>` inside a `ConcurrentDictionary<TKey, Lazy<...>>`. Construct the `Lazy<>` in the `GetOrAdd` factory but trigger `.Value` AFTER `GetOrAdd` returns — calling `.Value` inside the factory defeats the per-key build-once guarantee.

**Deterministic iteration order during build**: when sentinel decisions depend on the iteration order of multiple records that normalize to the same key, sort the input by a stable key (e.g. `OrderBy(c => c.Iso31661Alpha2Code, StringComparer.Ordinal)`) so two processes building the cache independently agree byte-for-byte.

Canonical implementation: `DefaultGeoNameResolver` in `server/shared/dotnet/geo-default/NameResolution/`. TS mirror: `server/shared/typescript/geo-default/src/name-resolution/default-geo-name-resolver.ts` (TS uses a module-scoped `Map | undefined` plus a build-count interlocked counter for thundering-herd safety under JS's single-thread execution).

## Namespace-disambiguated extensions over a shared receiver

When an Abstractions-layer extension and a Default-layer extension target the same receiver type (e.g. `IRequestContext`) and want to share a method name (e.g. `Country()`) but return different types (boundary parser → typed code vs catalog lookup → full record), place each extension in its OWN namespace and require consumers to pick exactly one via `using`. The C# compiler resolves the call site by the namespace import; importing both produces CS0121 (ambiguous reference) at compile time — a clear footgun the IDE surfaces immediately.

```csharp
// Boundary parser — no catalog dependency.
namespace D2.Shared.Geo.Abstractions.Extensions;
public static class IRequestContextGeoExtensions
{
    extension(IRequestContext context)
    {
        public CountryCode? Country() { /* parse raw → typed code */ }
    }
}

// Record-returning wrapper — catalog dependency lives in the same assembly
// as the implementation.
namespace D2.Shared.Geo.Default.Extensions;
public static class IRequestContextGeoExtensions
{
    extension(IRequestContext context)
    {
        public Country? Country() { /* code → catalog lookup */ }
    }
}
```

This idiom works only when the two extensions return DIFFERENT types (so the disambiguation has semantic meaning). If both extensions returned the same type, the choice would be invisible at the call site and consumers couldn't reason about it.

TypeScript has no equivalent of extension-method namespace shadowing, so the TS mirror in `@d2/geo-default/extensions/` uses distinct free-function names (`countryFor(context)` / `subdivisionFor(context)`) for clarity. Document the parity asymmetry in the affected per-lib READMEs.

## Anti-patterns to actively avoid

- **Thin handlers that just call another handler** — if an app-layer handler's body is `return otherHandler.HandleAsync(input)`, delete it; depend on the inner handler directly.
- **Hand-written DB migrations** — generator-driven only. Per `rules.md §9.10`.
- **String error codes outside `D2Result` factories** — use `TK.*` constants from `D2.Shared.I18n`.
- **Wrapping framework primitives without an opinionated semantic** — use `IDistributedCache` directly only if Microsoft's `Get` / `Set` / `Refresh` / `Remove` is enough. If you need `SetNx` / `Increment` / `AcquireLock` — use D²'s richer abstraction.
- **Returning `Ok()` after a fallible operation** — a `try/catch` that swallows failure and returns success is almost always a bug. Either `BubbleFail` or explicitly handle. Per `rules.md §9.20`.
- **Hardcoding what should be in Options** — batch sizes, cache expirations, retry attempts, lock TTLs all go through `IOptions<T>`.
- **Identity (UserId / OrgId / Scopes) in AMQP headers** — identity rebuilds from the JWT at every sync hop. Headers stay plaintext at-rest.
- **Hand-mirrored constants for spec-driven catalogs** — if a catalog has a SourceGen, the hand-written file gets deleted outright. See [Spec-driven codegen](#spec-driven-codegen--the-cross-cutting-pattern).
