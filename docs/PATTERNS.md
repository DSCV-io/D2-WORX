<!--
Copyright (c) DCSV. All rights reserved.
-->

# PATTERNS.md — D²-WORX Code Patterns

Directory of the core patterns + cross-cutting conventions every D²-WORX shared library and service embodies. Engineers implementing new handlers, libs, or services find here the high-level shape + jump-links to the canonical per-lib READMEs that own the full reference. Three sections stay at depth here because they are codebase-wide conventions with no per-lib canonical home (service project structure, spec-driven codegen philosophy, smart-constructor domain validation); every other entry is a thin description + small example + link.

> This doc INTENTIONALLY summarizes content canonical at the cited per-lib READMEs. Updates to behavior land in the per-lib README FIRST, then propagate to the PATTERNS.md summary only if the at-a-glance description itself changes. Per `docs/dev/rules.md` §11.32 condensed-view discipline.

## Table of contents

1. [.NET project layout (`.csproj`)](#net-project-layout-csproj)
2. [Service project structure](#service-project-structure)
3. [Handler](#handler)
4. [D2Result](#d2result)
5. [Spec-driven error codes](#spec-driven-error-codes)
6. [Utilities](#utilities)
7. [Time / Temporal](#time--temporal)
8. [Resilience](#resilience)
9. [Repository](#repository)
10. [Cache](#cache)
11. [Composition root](#composition-root)
12. [Logging](#logging)
13. [Telemetry](#telemetry)
14. [AspNetCore](#aspnetcore)
15. [JWT inbound auth](#jwt-inbound-auth)
16. [Service-to-service auth (outbound)](#service-to-service-auth-outbound)
17. [Deny-by-default endpoint boot guard](#deny-by-default-endpoint-boot-guard)
18. [Translation — none on HTTP path (intentionally)](#translation--none-on-http-path-intentionally)
19. [Configuration](#configuration)
20. [i18n](#i18n)
21. [Messaging](#messaging)
22. [SAGA — cross-service synchronous compensation](#saga--cross-service-synchronous-compensation)
23. [Multi-instance scaling](#multi-instance-scaling)
24. [Mappers](#mappers)
25. [Batch operations](#batch-operations)
26. [Content-addressable entities](#content-addressable-entities)
27. [Health checks](#health-checks)
28. [PII redaction — `[RedactData]`](#pii-redaction--redactdata)
29. [Anonymization — `[Anonymizable]` decoration + tiered reflection engine](#anonymization--anonymizable-decoration--tiered-reflection-engine)
30. [Contact value objects](#contact-value-objects)
31. [EF VO mapping — complex types + value converters](#ef-vo-mapping--complex-types--value-converters)
32. [EF Core 10 complex-member-index limitation + `CreateD2Index`](#ef-core-10-complex-member-index-limitation--created2index)
33. [Field-constraints codegen catalog](#field-constraints-codegen-catalog)
34. [Spec-driven codegen — the cross-cutting pattern](#spec-driven-codegen--the-cross-cutting-pattern)
35. [Domain validation — smart-constructor pattern](#domain-validation--smart-constructor-pattern)
36. [Input validation](#input-validation)
37. [Module-scoped cache-aside (build-once immutable static data)](#module-scoped-cache-aside-build-once-immutable-static-data)
38. [Namespace-disambiguated extensions over a shared receiver](#namespace-disambiguated-extensions-over-a-shared-receiver)
39. [Reference data](#reference-data)
    - [Endonym discipline](#endonym-discipline)
    - [Typed geo catalogs](#typed-geo-catalogs)
    - [Typed access on IRequestContext](#typed-access-on-irequestcontext)
    - [Geo name resolution at the integration boundary](#geo-name-resolution-at-the-integration-boundary)
    - [Reference data — user-preference cascades](#reference-data--user-preference-cascades)
40. [Hash composition](#hash-composition)
41. [Anti-patterns to actively avoid](#anti-patterns-to-actively-avoid)

---

## .NET project layout (`.csproj`)

Universal build properties (`TargetFramework`, `LangVersion`, `Nullable`, `ImplicitUsings`, `TreatWarningsAsErrors`, `GenerateDocumentationFile`, `StyleCop.Analyzers`, `stylecop.json` link) live in `server/Directory.Build.props` and apply to every csproj automatically. Per-project files only declare what's project-specific. Tier-1 global usings (`D2.Shared.Result`, `D2.Shared.Utilities.Extensions`/`.Attributes`/`.Enums`, `D2.Shared.I18n`) are scoped to **service projects** via `server/services/Directory.Build.targets` — shared libs keep explicit usings. Beyond Tier-1, each project carries a `GlobalUsings.cs` with any namespace repeated across roughly ≥3 files in that project — including `Microsoft.EntityFrameworkCore`, `Microsoft.Extensions.DependencyInjection`, `Microsoft.Extensions.Options`, `System.Security.Cryptography`, or any vendor SDK; the dependency law is enforced by `<ProjectReference>` edges, not using-directive visibility. The established `global using IClock = D2.Shared.Time.IClock;` alias appears project-wide wherever both NodaTime and `D2.Shared.Time` are used, resolving the `NodaTime.IClock` vs `D2.Shared.Time.IClock` ambiguity. Never duplicate SDK ImplicitUsings or Tier-1 entries. Reference implementation and full rationale: [ADR-0020](adrs/0020-service-project-structure.md) + rules.md §5.26.

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

**`RootNamespace`** is always declared explicitly and follows the **namespace structure**, not the directory path (e.g., `D2.Shared.Caching.Distributed.Redis.csproj` under `caching/distributed-redis/` has `RootNamespace=D2.Shared.Caching.Distributed.Redis` — path noise dropped). **Central Package Management** pins every version in `server/Directory.Packages.props`; csproj `<PackageReference>` items reference by ID only (CPM rejects `Version="..."`). **`dotnet build` enforces zero warnings** via `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` — fix or `.editorconfig`-override with rationale; never suppress.

Lib inventory + per-csproj READMEs → [`server/shared/dotnet/README.md`](../server/shared/dotnet/README.md).

---

## Service project structure

Every service project under `server/services/` takes one fixed layered shape. The canonical decision record — the full rationale, the module-within-host carve-out, the consequences — is [ADR-0020](adrs/0020-service-project-structure.md); this section is the daily-driver operational form.

### The layer set + the dependency law

A standalone service is **five runtime projects** + consumer-facing client(s) + (when it owns spec-driven error codes) a `netstandard2.0` source-gen shell. The layers depend in exactly one direction:

```
Domain  ←  App  ←  Infra  ←  Api      (Tests reference what they test; Clients reference contracts + shared libs only)
```

| Project | csproj pattern | SDK | Role |
| ------- | -------------- | --- | ---- |
| `domain/` | `D2.<Area>.<Service>.Domain` | `Microsoft.NET.Sdk` | Pure model — `Entities`/`ValueObjects`/`Enums`/`Rules` (+ generated `Errors`). References shared primitives only — **no** EF, Options, DI, or logging. |
| `app/` | `D2.<Area>.<Service>.App` | `Microsoft.NET.Sdk` | Orchestration — per-op handlers + observability + **ports + shapes** (the two-section split below). Transport-agnostic. |
| `infra/` | `D2.<Area>.<Service>.Infra` | `Microsoft.NET.Sdk` | Adapters implementing the app's ports — the **only** vendor-SDK-touching layer. |
| `api/` | `D2.<Area>.<Service>.Api` | `Microsoft.NET.Sdk.Web` | Composition root — `Program.cs` + host wiring + transport adapters (gRPC/REST/SSE) + transport mappers. The **only** project allowed to reference `infra/`. |
| `tests/` | `D2.<Area>.<Service>.Tests` | `Microsoft.NET.Sdk` | One project per service — `Unit/` + `Integration/` mirroring source. |
| `clients/dotnet/` | `D2.<Area>.<Service>.Client` | `Microsoft.NET.Sdk` | Consumer-facing — references **contracts + shared libs only**, never the service's internals. |

`<Area>` = `Edge` (or the service's own name when standalone). **WHY one direction:** vendor churn touches infra only; the domain stays testable with zero infrastructure; the api is the one place every concrete adapter + transport binding is named. The tie-breaker for an ambiguous placement: *"which layer still compiles if I delete the layer below the candidate?"*

A **module-within-host** (KeyCustodian + the auth module, both inside Edge) takes the standard `domain`/`app`/`infra` but **omits `api/` and its own `tests/`**: the host's `api/` is its composition root (the module exposes `AddD2<Module>()`), the host's api does its transport mapping, and its tests live in the host's test project under a `<Module>/` subtree. The five-project shape is the default; the carve-out is explicit and named.

### Domain — `Entities` / `ValueObjects` / `Enums` / `Rules`

- `Entities/` — aggregate roots, state-machine sum-types (abstract base + sealed per-state — see [EF-as-DDD](#repository)), audit entities.
- `ValueObjects/` — immutable smart-constructor VOs (`Create(...) → D2Result<T>`).
- `Enums/` — closed discriminators + taxonomies.
- `Rules/` — **pure, stateless, no-IO domain logic over entities/VOs** (decision rules, generators, projections, factories). A rule has no injected port, no IO, no `IOptions<>`, no logger (`IClock` is a permitted method *parameter*); a tunable like RSA modulus size is passed in by the handler, not read inside the rule. **WHY:** anything with no port and no IO is domain logic; hosting it in app behind an interface leaves the domain anemic and the logic un-unit-testable without the handler machinery. A rule that "wants" a logger or options is a handler, not a rule.

### App — the two-section split

```
app/
├── Application/
│   ├── Handlers/
│   │   ├── Commands/<Operation>/       # one folder per command operation
│   │   └── Queries/<Operation>/        # one folder per query operation
│   ├── Observability/                  # <Service>Log + <Service>Metrics
│   └── <Service>AppServiceCollectionExtensions.cs   # AddD2<Service>App()
└── Infrastructure/
    ├── <Concern>/                      # ports + shapes, grouped by capability concern
    └── Configuration/<Service>Options.cs            # options POCO with SECTION const
```

`Application/` = "what this service *does*" (the operations); `Infrastructure/` = "what it *needs from the outside*" (the ports + the shapes those ports speak — no adapter, that lives in `infra/`). **WHY:** the split makes `infra/` a structural mirror — every concern folder in `app/Infrastructure/` has a same-named folder in `infra/` holding the adapter, so a port and its impl compare side-by-side.

**Per-operation handler folders.** One folder per operation under its category; it co-locates the interface, impl, input, and output — all suffixed so the folder is namespace-safe:

```
Application/Handlers/Commands/RotateKey/
├── IRotateKeyHandler.cs     # IRotateKeyHandler : IHandler<RotateKeyInput, RotateKeyOutput>
├── RotateKeyHandler.cs      # sealed RotateKeyHandler : ..., IRotateKeyHandler
├── RotateKeyInput.cs
└── RotateKeyOutput.cs
```

Naming law: handler interface `I<Operation>Handler`, impl `<Operation>Handler` (file = type; the bare `<Operation>` type name is not used), input `<Operation>Input` (never `Request`/`Command`), output `<Operation>Output` (never document-style `Outcome`/`Plan`). An operation-private record is `<Operation><Role>` (suffixed) or a `private` nested type. One interface per file; consumers `using` the folder namespace directly — no `partial` aggregation, no grouping aliases. **WHY co-locate:** an interface and its impl are read together nearly always; two unrelated operations' interfaces are read together nearly never. A DTO bucket (`Models/`) is not used — a DTO either co-locates with its operation or, when a shape is shared by 2+ operations, is promoted to a domain VO.

### Command vs Query — the binary side-effect rule

**Exactly two categories: `Commands/` and `Queries/`. A handler's category is determined SOLELY by whether the operation mutates persistent/shared state — a DB write, a distributed-cache write, an external write, or a message publish. The verb in the name is irrelevant.** Side effect → `Commands/`; none → `Queries/`.

| Category | Distributed cache | DB write | External API | Message publish | Test |
| -------- | ----------------- | -------- | ------------ | --------------- | ---- |
| **Commands** | yes | yes | yes | yes | "If the process dies right after, did state change persist?" → **yes**. |
| **Queries** | no | no | no | no | Same death test → **no**. |

There is no third category. An operation that *looks* like a query by name (`Find…`, `Get…`) but mutates as a side effect (find-or-create, cache-warm-on-read that broadcasts) is a `Command` wearing a query-ish verb — the side effect makes it a `Command`. `Find` (resolve, may fetch) vs `Get` (direct read) stays *naming* guidance but no longer maps to a folder. **Local/in-memory caching does NOT disqualify a Query** — instance-scoped ephemeral state (a per-instance `ILocalCache`, a module-scoped `Lazy<FrozenDictionary>`) is not shared-state mutation; only distributed-cache writes (`*AndBroadcast*`, anything other instances observe) push an operation to `Commands/`.

### Concern folders + mandatory vendor/tech/protocol subfolders

`app/Infrastructure/` groups ports + shapes by **capability concern** — a PascalCase singular capability noun (`Persistence`, `Messaging`, `Email`, `Sms`, `Realtime`, `Storage`, `Outbound`, `Vault`, `Scheduling`, `Configuration`, …). `Vault/` not `Secrets/` — a `Secrets/` folder collides with the universal `secrets/` key-material convention on case-insensitive filesystems. The `infra/` project mirrors those concern folders with adapters, and **every concern folder in `infra/` carries a tech/vendor/protocol subfolder — even when only one impl exists today**:

```
infra/Persistence/Postgres/...      not  infra/Persistence/...
infra/Messaging/RabbitMq/...        not  infra/Messaging/...
infra/Email/Resend/...   +  infra/Email/Ses/...      (vendor axis)
infra/Outbound/Grpc/...  +  infra/Outbound/Rest/...   (protocol axis)
infra/Observability/                                  (infra-side log delegates — same folder name as App's Application/Observability/)
```

**WHY mandatory even for a sole impl:** the subfolder is the seam a second vendor lands on without a reshuffle — the day Resend gains an SES fallback, the new adapter drops into a sibling folder and nothing else moves. The generic `Providers/` wrapper is dead; concern + vendor replaces it. The concern-noun set is open-but-deliberate — adding a noun is a standard amendment (this doc + [ADR-0020](adrs/0020-service-project-structure.md)), not an ad-hoc per-service invention.

### Multi-provider — the keyed-resolver recipe

A service may run two vendors of one capability at once (Stripe AND Square), resolving one-of-many by key. App stays vendor-blind (ONE capability port per concern in `app/Infrastructure/<Concern>/`); infra registers keyed adapters under .NET keyed DI (one per vendor subfolder); when the handler picks the vendor at runtime, a resolver port `I<Capability>Resolver.Get(key) → D2Result<T>` wraps `IServiceProvider.GetKeyedService<T>(key)` and maps an unknown key to a typed `D2Result` failure (not a thrown `InvalidOperationException`). A handler that statically knows its vendor injects `[FromKeyedServices("vendor")] I<Capability>` directly. For messaging the resolver layers *on top of* `[MqPub]` — each concrete publisher keeps its compile-time descriptor; the resolver only selects which already-described publisher to use. **WHY a resolver over raw keyed injection when the key is dynamic:** `[FromKeyedServices]` needs a compile-time key; a runtime key (an org's configured vendor) needs a lookup, and a bad key is operator data → `D2Result`, not an exception. Full vertical → [ADR-0020](adrs/0020-service-project-structure.md).

### Options home + namespaces

Options flow: env `SECTION__PROP` (arrays `SECTION__N`) → `D2Env.Load()` → `IConfiguration` → **binding + `ValidateOnStart` in `infra/Configuration/`** → the POCO declared in `app/Infrastructure/Configuration/` (carrying the `SECTION` const) → handlers consume `IOptions<T>` → the domain receives only adapted VOs/primitives (never `Microsoft.Extensions.Options`). `[Required]` on a non-nullable struct is a no-op — use `[Range(typeof(TimeSpan), …)]` or a custom `.Validate(…)`, plus the domain VO's smart constructor as the second floor. Namespaces are the folder path verbatim, **including** the `.App`/`.Infra` layer segment (`D2.Edge.KeyCustodian.App.Infrastructure.Persistence` vs `D2.Edge.KeyCustodian.Infra.Persistence.Postgres`) — not collapsed via `RootNamespace` tricks; in a service the layer IS semantics.

### Verb semantics

- **Find** = "Resolve this for me" — may fetch from external source, may cache / persist. Example: `FindWhoIs`.
- **Get** = "Give me this by ID" — direct lookup, read-only. Example: `GetWhoIsByIds`.

---

## Handler

`.NET`: `BaseHandler<TSelf, TInput, TOutput>` with using aliases (`H`, `I`, `O`), `IHandlerContext`, `DefaultOptions` override. `RunCorePipelineAsync` (sealed) hosts the observability pipeline (activity, span tags, log scope, stopwatch, 4 OTel metrics, universal try/catch); `HandleAsync` is virtual and one-line-pass-through by default. Repo handlers inherit `BaseRepoHandler<TSelf, TInput, TOutput>` which adds typed DB-failure mapping via an injected `IDbExceptionClassifier`. EF-using command and query handlers inherit `BaseRepoHandler` for this DB-failure mapping; the per-operation Repository handler pattern is retired — handlers use the module `DbContext` contract + aggregates + LINQ directly (see [Repository](#repository)).

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

> Duplicated from [`server/shared/dotnet/handler/core/README.md`](../server/shared/dotnet/handler/core/README.md) for at-a-glance directory access. Canonical full reference (pipeline shape, `MapDbException` override, typed booleans, observability surface) lives in the lib READMEs — update both in lockstep. See also: [`handler/repo/README.md`](../server/shared/dotnet/handler/repo/README.md), [`handler/repo-postgres/README.md`](../server/shared/dotnet/handler/repo-postgres/README.md).

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

`IsTransientRetryable` covers `IsServiceUnavailable || IsRateLimited`. **`IsUnhandledException` is intentionally excluded** — an unknown exception means unknown system state; retrying could mask bugs or double-execute a non-idempotent operation.

> Duplicated from [`server/shared/dotnet/result/core/README.md`](../server/shared/dotnet/result/core/README.md) for at-a-glance directory access. Full factory catalog, `BubbleFail` / `Bubble`, per-code booleans (`IsTransientRetryable` / `IsTransientDbFailure`), monadic `Bind` / `Map` / `ThenAsync` / `Match`, and auto-injected `traceId` semantics live in the lib README — update both in lockstep.

---

## Spec-driven error codes

Every error code is declared in a `*-error-codes.spec.json` catalog — one entry per code carrying its `httpStatus`, semantic `category`, and `userMessageKey`. The .NET + TS code constants, the typed `D2Result` failure factories, and the merged cross-service registry are all CODEGEN-emitted from that spec; nothing about a code is hand-written. There are two catalog kinds:

- **Generic catalog** — `contracts/error-codes/` owns the reserved unprefixed namespace (`NOT_FOUND`, `CONFLICT`, `VALIDATION_FAILED`, `SERVICE_UNAVAILABLE`, …). These drive the framework-level `D2Result` factories.
- **Per-domain catalog** — `contracts/<domain>-error-codes/` (e.g. `contracts/auth-error-codes/`) owns codes carrying the enforced `<DOMAIN>_` prefix. These drive a generated `<Domain>Failures` factory class.

The call site NAMES the scope it's drawing from — the factory's receiver tells the reader which catalog owns the code:

```csharp
// framework / generic catalog — the semantic D2Result factories
return D2Result<UserDto>.NotFound();
return D2Result<TokenDto>.ValidationFailed(inputErrors: errors);

// per-domain catalog — the generated <Domain>Failures factory
return AuthFailures<TokenDto>.InvalidGrant();
```

Each factory stamps the spec-declared `(code, httpStatus, category, userMessageKey)` tuple, so the wire payload carries the code + its `category` + a `TKMessage` whose key resolves to localized text on the client. A new code is added by editing the spec + re-running the generator — never by hand-mapping a status + message into a raw `Fail(statusCode, message)` call (per [`docs/dev/rules.md §26.6`](dev/rules.md#26-codegen-discipline-spec--proto--schema-derived-types) + [§5.3](dev/rules.md#5-c-code-conventions)).

**Merged-registry resolution boundary** — the codegen also emits a merged cross-service registry (`ErrorCodeRegistry` in .NET, `errorCodeRegistry` in TS) that aggregates EVERY `*-error-codes.spec.json` catalog into one `code → ErrorCodeInfo` lookup. This is what lets a consuming service branch on a wire code it didn't produce: given a code string from any producer, `TryResolve(code, out info)` returns the `httpStatus`, `category`, `userMessageKey`, and originating `domain` WITHOUT the consumer importing the producer's catalog. The registry is the resolution surface; the per-catalog factories are the production surface — produce with `D2Result.X()` / `<Domain>Failures.X()`, resolve a foreign code with the registry.

> Duplicated from [`server/shared/dotnet/error-codes/registry/README.md`](../server/shared/dotnet/error-codes/registry/README.md) for at-a-glance directory access — the full per-catalog spec format, the `ErrorCodeInfo` field set, and the build-time cross-catalog collision diagnostics live in the lib README — update both in lockstep.

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

`D2.Shared.Resilience` ships `CircuitBreaker<T>` (three-state lock-free), `Singleflight<TKey, TValue>` (concurrent-call deduplication; first caller runs the operation, siblings share its `Task<TValue>`), `RetryHelper.RetryAsync<T>` + the `D2Result`-aware `RetryD2ResultAsync<TData>` overload (exponential backoff + jitter + transient classifier), `TimeoutLayer<TKey, TValue>` (wall-clock deadline that cancels the inner op; place at two pipeline positions for independent total-request and per-attempt deadlines), `RateLimiterLayer<TKey, TValue>` (hand-rolled `SemaphoreSlim` concurrency limiter; fail-loud `MaxConcurrency >= 1`; client-side admission control, not the server-side distributed rate-limit middleware), and the `ResilientPipeline<TKey, TValue>` composition surface. Compose via the fluent DSL at the composition root; handlers inject `ResilientPipeline<TKey, TValue>` and call `pipeline.ExecuteAsync(key, op, ct)`. The pipeline returns `D2Result<TValue>` (never throws) and converts CircuitOpen / cancellation / transient / unknown exceptions to the appropriate result code.

```csharp
// All registrations are keyed — no unkeyed path exists.
// UseSingleflight / UseCircuitBreaker require either a serviceKey or a concrete instance.
services.AddKeyedSingleton<Singleflight<string, MyDto>>("my-key");
services.AddKeyedSingleton<CircuitBreaker<MyDto>>("my-key", (_, _) => new(_ => false));

services.AddResilientPipeline<string, MyDto>("my-key", p => p
    .UseSingleflight("my-key")
    .UseCircuitBreaker("my-key")
    .UseRetries(new() { MaxAttempts = 5 }));
```

**Layer order = protection semantic.** `CircuitBreaker → Retries` means retry-inside-CB (upstream-protecting; backoff gives a fragile upstream air). `Retries → CircuitBreaker` means retry-outside-CB (restart-recovery; retry layer treats `CircuitOpenException` as transient and backs off through it). Full state-machine semantics + composition trade-offs → [`server/shared/dotnet/resilience/README.md`](../server/shared/dotnet/resilience/README.md).

---

## Repository

EF Core for all relational data, accessed **directly through the module `DbContext` contract + aggregates + LINQ** — the per-op Repository handler is retired ([ADR-0017](adrs/0017-ef-as-ddd-persistence.md)). Command/query handlers inherit `BaseRepoHandler` / `BaseHandler` (see [Handler](#handler)) for the cross-cutting pipeline; the registered `IDbExceptionClassifier` translates DB exceptions into typed `D2Result` failures (`UniqueViolation`, `ConcurrencyConflict`, `DbDeadlock`, `DbTimeout`, etc.). Callers branch on `result.IsUniqueViolation` / `IsTransientDbFailure` — never on raw SQLSTATE catches.

**Physical homes** ([ADR-0020](adrs/0020-service-project-structure.md)): the `I<Service>DbContext` port, the flat `<Entity>Record`, the pure `<Entity>RecordMapper`, and the `<Entity>RecordQueryExtensions` all live in `app/Infrastructure/Persistence/`; the concrete `<Service>DbContext`, the `IEntityTypeConfiguration<Record>`, and the `Migrations/` folder live in `infra/Persistence/Postgres/`. App speaks the query *language* (DbContext/DbSet/LINQ); infra owns the *database* (Npgsql, connection, migrations, `xmin`).

- **Batch chunking** — PG has a ~32K parameter cap per query (signed-int param index); default chunk size **500**. Use `input.HashIds.Chunk(_BATCH_SIZE)` with `_BATCH_SIZE` via the Options pattern. See [Batch operations](#batch-operations).
- **Partial-success → D2Result mapping** — all resolved → `Ok`; some → `SomeFound`; none → `NotFound`. Never return `Ok` with empty data.
- **UPDATE / DELETE row-count** — `SaveChangesAsync()` returns affected rows. Zero where you expected ≥1 → `NotFound`. Per `rules.md §9.32`.
- **Migrations — generator only.** `dotnet ef migrations add <Name>` is the only path. NEVER hand-edit `*.cs` migration files, `*ModelSnapshot.cs`, or `__EFMigrationsHistory` rows. Multi-replica safety: startup migrator acquires a PG advisory lock. Per `rules.md §9.10`.
- **EF migration `.editorconfig` exclusion** — add `[**/Migrations/*.cs] generated_code = true` to the service's `.editorconfig` when the first migration lands, so EF-emitted files are excluded from StyleCop (SA1200/SA1413/SA1633). Never suppress per-rule or hand-edit the generated output. Per `rules.md §26.9`.

### Rich sum-type state-machine aggregates + flat-record persistence

Stateful domain aggregates whose states differ in the operations they support are modeled as an **abstract base + sealed per-state subtype hierarchy** so illegal transitions are uncompilable. The `Status` enum is a derived persistence discriminator computed from the type at persistence time — never the authority on which transitions exist.

```csharp
// Domain — EF-free, sealed per state
public abstract record EncryptionKey(Guid Id, ...);
public sealed record PendingKey(Guid Id, ...) : EncryptionKey(Id, ...);
public sealed record ActiveKey(Guid Id, ...) : EncryptionKey(Id, ...);
public sealed record RetiredKey(Guid Id, ...) : EncryptionKey(Id, ...);

// Enum — derived discriminator only (for DB lookups / LINQ filters)
public enum KeyStatus { Pending, Active, Retired }
```

**Persistence shape — flat `<Entity>Record`, never TPH.** EF Core 10 confirmed that TPH with an abstract base produces `DELETE` + re-`INSERT` for state transitions (gap between delete and insert, loss of `xmin` token, foreign-key risks). The flat record keeps the transition as an atomic `UPDATE`:

| Artifact | Home | Role |
| -------- | ---- | ---- |
| `KeyRecord` (flat EF record with all columns + `Status` + `xmin`) | `app/Infrastructure/Persistence/` | EF entity; `xmin` = concurrency token |
| `KeyRecordMapper` (pure C# 14 extension members) | `app/Infrastructure/Persistence/` | `ToDomain()`: switch Status → sealed subtype; `ProjectOnto(key)`: overwrite mutable columns |
| `KeyRecordQueryExtensions` | `app/Infrastructure/Persistence/` | `IQueryable<KeyRecord>` convenience filters |
| `KeyCustodianDbContext` : `IKeyCustodianDbContext` | `infra/Persistence/Postgres/` | Concrete EF context |
| `KeyRecordConfiguration` : `IEntityTypeConfiguration<KeyRecord>` | `infra/Persistence/Postgres/` | `HasKey`, `UseXminAsConcurrencyToken`, value-converters |
| `Migrations/` | `infra/Persistence/Postgres/` | EF-generated only; excluded from StyleCop via `.editorconfig` |

```csharp
extension(KeyRecord record)
{
    public EncryptionKey ToDomain() => record.Status switch
    {
        KeyStatus.Pending  => new PendingKey(record.Id, ...),
        KeyStatus.Active   => new ActiveKey(record.Id, ...),
        KeyStatus.Retired  => new RetiredKey(record.Id, ...),
        _ => throw new InvalidOperationException($"Unknown KeyStatus {record.Status}")
    };

    public KeyRecord ProjectOnto(EncryptionKey key) => record with
    {
        Status     = key.ToStatus(),   // derived from type
        UpdatedAt  = key.UpdatedAt,
        // ... other mutable columns
    };
}
```

**State transition pattern in handlers** (single `SaveChangesAsync` = atomic UPDATE + audit):

```csharp
// Load the flat record
var record = await ctx.Keys.SingleOrDefaultAsync(r => r.Id == id, ct);
if (record is null) return D2Result.NotFound();

// Reconstruct the domain sum-type
var key = record.ToDomain();

// Apply the domain transition (compile-time safe — method only exists on PendingKey)
if (key is not PendingKey pending) return D2Result.Conflict();
var activated = pending.Activate(clock.GetCurrentInstant());

// Write audit entry + updated record in the SAME transaction
ctx.KeyAuditLog.Add(new KeyAuditEntry(...));
record.ProjectOnto(activated);
var rows = await ctx.SaveChangesAsync(ct);
if (rows == 0) return D2Result.Conflict();   // xmin mismatch
```

Canonical: [ADR-0016](adrs/0016-keycustodian-lifecycle-store.md) + [ADR-0017](adrs/0017-ef-as-ddd-persistence.md). Predicate enforcement: `rules.md §9.31` (sum-type shape) + `§9.38` (flat-record persistence) + `§9.37` (EF-as-DDD handler shape).

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

Canonical: [`server/shared/dotnet/caching/abstractions/README.md`](../server/shared/dotnet/caching/abstractions/README.md). Default impls: [`caching/local-default/`](../server/shared/dotnet/caching/local-default/README.md), [`caching/distributed-redis/`](../server/shared/dotnet/caching/distributed-redis/README.md), [`caching/tiered/`](../server/shared/dotnet/caching/tiered/README.md).

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

`AddD2ServiceDefaults` chains `D2Env.Load` → `AddD2Logging` → `AddD2Telemetry` → `AddD2I18n` → `AddD2Handler` → `AddD2Auth` (+ `.Http` + `.Grpc`) → `AddD2LocalCache` → `AddD2HealthChecks` → `AddD2ProblemDetails` → `AddD2Cors`. The aggregator owns ZERO logic; new options on owning libs flow through via pass-through `Action<TFromOwningLib>?` delegates. `UseD2DefaultPipeline` middleware order is **LOCKED** (no insertion points): security headers → request logging → CORS → routing → infrastructure bypass → authentication → `UseD2Auth` → authorization. Auth wiring is fail-fast (`AuthConfigure` MUST be non-null when `SkipAuthAutoWiring = false`). Resilience is NOT wired by this aggregator — it is caller-side + opt-in via `D2.Shared.Resilience`.

> Duplicated from [`server/shared/dotnet/service-defaults/README.md`](../server/shared/dotnet/service-defaults/README.md) for at-a-glance directory access. Full call-order rationale + middleware-order rationale + opt-out matrix (`SkipAuthAutoWiring` / `SkipLocalCacheAutoWiring`) + thin-aggregator convention test live in the lib README — update both in lockstep.

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

Canonical: [`server/shared/dotnet/telemetry/core/README.md`](../server/shared/dotnet/telemetry/core/README.md).

---

## AspNetCore

`D2.Shared.AspNetCore` ships cross-cutting middleware + endpoint primitives: `UseD2SecurityHeaders` (OWASP defaults; HSTS only on HTTPS, preload opt-in), `AddD2Cors` / `UseD2Cors` (canonical `D2_CORS_ORIGINS__*` indexed env-var; fail-closed via `ValidateOnStart`), `UseD2InfrastructureBypass` (default short-circuit invokes the matched endpoint's `RequestDelegate` directly — heavy middleware does NOT run on `/health` / `/alive` / `/metrics` / `/.well-known`), `AddD2ProblemDetails` (RFC 7807 customizer; `traceId` / `correlationId` / `instance` enrichment), `AddD2HealthChecks` + `MapD2HealthEndpoints` (`/health` full + `/alive` live-tag split), `RunD2ServiceAsync` (PII-safe `Log.Fatal` rendering — type FullName + first stack frame, NEVER `ex.Message`).

The public static `InfrastructurePathMatcher` is the **single source of truth** for the path set across Logging, Telemetry, and `UseD2InfrastructureBypass`. Canonical: [`server/shared/dotnet/aspnetcore/README.md`](../server/shared/dotnet/aspnetcore/README.md).

---

## JWT inbound auth

RS256 + JWKS-based inbound auth, transport-binding split across three sibling csprojs: **`D2.Shared.Auth`** (runtime — `JwtValidator`, `HttpJwksProvider`, `TieredCacheSessionLivenessTracker`, `AuthFailures`), **`D2.Shared.Auth.Http`** (HTTP middleware — `JwtAuthMiddleware`, RFC 7807 ProblemDetails, `RequireAnyScope` / `RequireAllScopes` fluent metadata + `MarkAsD2HarmlessEndpoint`), **`D2.Shared.Auth.Grpc`** (gRPC interceptor — `JwtAuthInterceptor`, `RpcException(Status, Trailers)` shape with `d2_error_code` / `d2_messages` / `traceid` trailers + `[D2RequireAnyScope]` / `[D2RequireAllScopes]` / `[D2HarmlessEndpoint]` attributes).

```csharp
services.AddD2Auth(opts => { opts.Issuer = ...; opts.Audience = ...; });
services.AddD2AuthHttp();      // and/or AddD2AuthGrpc();
app.UseD2Auth();
app.MapGet("/files/{id}", H).RequireAnyScope(Scopes.Files.Read);
app.MapGet("/files/{id}/lock", H).RequireAllScopes(Scopes.Files.Read, Scopes.Files.Write);
app.MapGet("/healthz", () => "ok").MarkAsD2HarmlessEndpoint();
```

Per-validation pipeline: bearer extraction (transport layer) → signature + standard-claim validation (RS256 pinned, issuer / audience / lifetime with 30s clock skew, reactive-refresh-on-unknown-`kid`) → session liveness (`TieredCacheSessionLivenessTracker`; fail-closed on liveness store outage) → per-endpoint scope check with explicit match mode (any-of via `RequireAnyScope` / `[D2RequireAnyScope]`; all-of via `RequireAllScopes` / `[D2RequireAllScopes]`). JWKS at the OIDC-canonical `/.well-known/jwks.json`; cluster-wide JWKS rotation via `ICacheInvalidationBackplane` (Redis pub/sub on `d2.security.key-rotated:jwks`). Uniform **401** at the auth boundary regardless of "JWT bad" vs "scope insufficient" — granularity surfaces only on `d2_error_code`. `MarkAsD2HarmlessEndpoint()` / `[D2HarmlessEndpoint]` opts out of the full pipeline (reserved for k8s probes + OIDC discovery); `[AllowAnonymous]` is deliberately NOT recognized. Both transports register a scoped `IRequestContext` reading from a shared `HttpContext.Items` slot (`D2HttpContextItems.REQUEST_CONTEXT`); gRPC interceptor dual-writes to `ServerCallContext.UserState` for hot-path access. Every mapped endpoint must carry a declared auth intent or the host fails to start — see [Deny-by-default endpoint boot guard](#deny-by-default-endpoint-boot-guard).

Canonical: [`server/shared/dotnet/auth/core/README.md`](../server/shared/dotnet/auth/core/README.md), [`auth/http/README.md`](../server/shared/dotnet/auth/http/README.md), [`auth/grpc/README.md`](../server/shared/dotnet/auth/grpc/README.md).

---

## Service-to-service auth (outbound)

Internal cross-process calls use three independent outbound factors from `D2.Shared.Auth.Outbound`, each opt-in:

1. **Forwarded transaction-token (the business default).** Edge mints exactly one internal transaction-token at the trust boundary via RFC 8693 token exchange. Every downstream gRPC hop re-attaches that same token unchanged via `ForwardedJwtCallCredentials`. The downstream receiver re-validates the token and reads the user identity and scopes directly from it — no per-hop re-exchange. ([ADR-0022](adrs/0022-service-auth-mint-once-forward.md))

2. **Workload certificate (mTLS identity).** The calling workload presents a short-lived leaf certificate on the outbound gRPC channel. The mutually-authenticated TLS channel establishes *which workload is calling*. `AddD2WorkloadCertificate` + `AddD2WorkloadCertificateOutbound` wire both sides. ([ADR-0023](adrs/0023-mtls-workload-identity.md))

3. **RFC 8693 token exchange (explicit exception cases only).** The boundary mint that produces the forwarded token, plus the narrow set of legitimate exceptions: cross-trust-domain calls, justified scope narrowing, asynchronous scope reduction, and `act` chain establishment/extension. Never the per-hop business default — the forward-unchanged rail covers the common case.

```csharp
// Forwarded-JWT factor — wire in the composition root of each calling service.
services.AddD2ForwardedJwtOutbound();

// Workload-certificate factor.
services.AddD2WorkloadCertificate(opts => { ... });
services.AddD2WorkloadCertificateOutbound();

// Token-exchange client (boundary mint + exception cases).
services.AddD2AuthOutbound(opts => { opts.Issuer = ...; opts.ClientId = ...; opts.ClientSecret = ...; });
```

Canonical: [`server/shared/dotnet/auth/outbound/README.md`](../server/shared/dotnet/auth/outbound/README.md). Workload-identity background: [`server/shared/dotnet/workload-identity/README.md`](../server/shared/dotnet/workload-identity/README.md).

---

## Request-context establishment (`Origin` / `ImmediateCaller` / `CallPath`)

Every trust boundary a request can pass through recomputes two local, non-propagated facts on `IRequestContext` — `Origin` (`RequestOrigin`: which kind of boundary produced this hop's context) and `ImmediateCaller` (`string?`: who called this hop) — and appends one entry to a propagated, telemetry-only `CallPath`. `Origin`/`ImmediateCaller` are never carried in from the wire; each boundary derives them fresh from its own transport evidence, so a capability authority can trust them the same way it trusts a validated JWT claim. `CallPath` is the opposite shape on purpose — it accumulates across hops and rides `x-d2-context`, so no authority rule ever takes it as a parameter.

Five establishment boundaries populate these fields, one per way a request-scoped context can originate plus the outbound leg that carries the propagated subset forward:

```csharp
// Inbound HTTP (D2.Shared.Auth.Http) — sets Origin = EdgeInbound, starts the call-path.
app.UseD2RequestOriginEdge();

// Inbound gRPC (D2.Shared.Auth.Grpc) — sets Origin = CrossProcessHop, ImmediateCaller
// from the validated mTLS client certificate, appends a WorkloadHop entry.
services.AddD2RequestOriginGrpc();   // registers RequestOriginCrossProcessInterceptor

// In-process module call (D2.Shared.Context.Abstractions) — the generated I<Module>Api
// leaf calls this before dispatching; sets Origin = InProcessModule.
requestContext.EstablishInProcessModule(callingModuleId, targetModuleId, clock);

// System worker (D2.Shared.Context.Abstractions) — a background service's per-iteration
// scope calls this before resolving a handler; sets Origin = System.
scopedServices.EstablishSystemContext(hostServiceId, clock);

// Outbound gRPC (D2.Shared.Auth.Outbound) — writes x-d2-context (operational subset +
// accumulated call-path) on every outbound call; auto-chained by the generated client.
builder.AddD2PropagatedContext();
```

**Fail-closed by construction.** A freshly-constructed context's `Origin` is `RequestOrigin.Unestablished` — the enum's zero member — so any authority rule consulting `Origin` denies unless a boundary has positively established it. There is no "assume a plane" fallback; a missing establishment call is a loud, rejected request, not a silent over-grant.

Canonical: [ADR-0025](adrs/0025-request-context-establishment.md).

### Minter capability — possession-gated authority over a cluster-root secret

A capability over the single highest-value secret in a service (KeyCustodian's cluster JWT signing key, `jwks-signing`) is never a branch inside the general-purpose authority rule every request eventually reaches. It is a **separate interface**, registered by a **dedicated DI extension** called only from the one composition root allowed to hold it:

```csharp
public interface IJwtSigningCapability
{
    ValueTask<D2Result<SignOutput>> SignJwtAsync(SignInput input, CancellationToken ct = default);
}

// Called ONLY from the owning composition (the JWT minter / auth module) —
// never from the general client registration (AddD2KeyCustodianClients()).
services.AddD2JwtSigningCapability();
```

**Possession of the resolved interface is the authority.** A provider built without the dedicated registration cannot resolve `IJwtSigningCapability` at all — there is no runtime flag to flip and no caller identity to spoof, because the capability either was wired into this composition root or it was not, and that is a build-time, review-visible fact. The implementation adds a second, independent guard — an origin check (`Origin == InProcessModule`) — so even a reference that somehow escaped its owning composition cannot be used from the wrong plane. The general-purpose authority rule structurally excludes the guarded target for every caller and every origin, so the capability is the *only* path to it, not merely the *recommended* one.

Canonical: [ADR-0025](adrs/0025-request-context-establishment.md) §"The authority model."

---

## Deny-by-default endpoint boot guard

Every mapped `RouteEndpoint` must declare its auth intent before the service may serve traffic. An endpoint with no declaration silently admits any authenticated caller at runtime — a class of misconfiguration that is impossible to observe from a passing test run. The `AuthEndpointGuardStartupFilter` (in `D2.Shared.Auth.Startup`, wired automatically by `AddD2ServiceDefaults`) converts this silent failure into a fast, deterministic startup failure.

### What the guard checks

At host startup — after middleware pipeline construction (including `UseRouting`, which merges all endpoint data sources into the DI composite) and before Kestrel accepts connections — the guard walks every endpoint in `EndpointDataSource.Endpoints`. For each `RouteEndpoint` it verifies that one of the following is present in the endpoint's metadata collection:

| Intent | How to attach |
| ------ | ------------- |
| HTTP any-of scope | `.RequireAnyScope("scope1", "scope2")` on `IEndpointConventionBuilder` |
| HTTP all-of scope | `.RequireAllScopes("scope1", "scope2")` on `IEndpointConventionBuilder` |
| gRPC fluent any-of scope | `.RequireAnyScope("scope1")` on the gRPC service builder |
| gRPC fluent all-of scope | `.RequireAllScopes("scope1", "scope2")` on the gRPC service builder |
| gRPC attribute any-of scope | `[D2RequireAnyScope("scope1")]` on the service class or method |
| gRPC attribute all-of scope | `[D2RequireAllScopes("scope1", "scope2")]` on the service class or method |
| Harmless bypass | `.MarkAsD2HarmlessEndpoint()` (fluent) or `[D2HarmlessEndpoint]` (attribute) |

An endpoint that satisfies none of these causes the host to throw `InvalidOperationException` naming the offending route patterns and abort before serving.

### Exempt endpoints

Three categories are exempt from the check:

1. **Infrastructure paths** — endpoints whose route pattern matches `/health`, `/alive`, `/metrics`, or `/.well-known` (via `D2AspNetCoreConstants.DEFAULT_INFRASTRUCTURE_PATHS` + `InfrastructurePathMatcher`). These are typically registered by `MapD2HealthEndpoints` and `MapD2PrometheusEndpoint`, which attach `MarkAsD2HarmlessEndpoint` automatically; they are also exempt by path so that third-party health-probe registrations without the D² fluent extension don't trip the guard.
2. **Non-`RouteEndpoint` entries** — base `Endpoint` instances with no route pattern carry no route identity and cannot be guarded by convention.
3. **gRPC infrastructure catch-all endpoints** — `MapGrpcService<T>()` registers catch-all slots (e.g. `{pkg}.{Svc}/{unimplementedMethod:grpcunimplemented}`) to return `UNIMPLEMENTED` for unknown routes. The guard identifies these by the `grpcunimplemented` route constraint, which gRPC AspNetCore adds exclusively to its catch-all parameters and no other endpoint type carries.

### Opt-out

Set `D2ServiceDefaultsOptions.SkipAuthEndpointGuard = true` when wiring via `AddD2ServiceDefaults`. Appropriate for test hosts that register synthetic endpoints without auth declarations and for anonymous-only admin tools. When opting out, per-endpoint auth intent is the developer's responsibility.

### Implementation mechanism

The guard is an `IStartupFilter` rather than an `IHostedService`. In the `WebApplication` model, `app.MapXxx()` calls write into `WebApplication.DataSources` before `StartAsync`; these sources are merged into the DI-resolved `EndpointDataSource` composite during pipeline construction (the `next(app)` call inside `IStartupFilter.Configure`). The `IStartupFilter` post-`next` window is therefore the earliest point at which the full endpoint set is visible. An `IHostedService` would capture the `EndpointDataSource` singleton at DI-resolve time — before the `WebApplication` data sources are merged — and would see an empty collection. The `IStartupFilter` path guarantees the guard walks the complete, production-faithful endpoint set before any request byte arrives.

```csharp
// Standard service setup — guard is ON by default.
builder.Services.AddD2ServiceDefaults(builder.Configuration, opts =>
{
    opts.AuthConfigure = auth => { auth.Issuer = ...; auth.Audience = ...; };
});

// Every endpoint must declare intent:
app.MapGet("/files/{id}", H).RequireAnyScope(Scopes.Files.Read);
app.MapGet("/files/{id}/lock", H).RequireAllScopes(Scopes.Files.Read, Scopes.Files.Write);
app.MapGet("/healthz", () => "ok").MarkAsD2HarmlessEndpoint();

// Opt out for test hosts:
opts.SkipAuthEndpointGuard = true;
```

> Detailed implementation: `server/shared/dotnet/auth/startup/AuthEndpointGuardStartupFilter.cs` + `AuthEndpointGuardServiceCollectionExtensions.cs`. Opt-out surface: `server/shared/dotnet/service-defaults/D2ServiceDefaultsOptions.cs`. Architectural rationale: [ADR-0012](adrs/0012-self-rolled-dotnet-auth.md) §5.

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

Canonical: [`i18n/abstractions/README.md`](../server/shared/dotnet/i18n/abstractions/README.md), [`i18n/core/README.md`](../server/shared/dotnet/i18n/core/README.md), [`i18n/source-gen/README.md`](../server/shared/dotnet/i18n/source-gen/README.md).

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

Canonical (full wire format + topology + publisher path + consumer path + DLX / DLQ + tiered retry + encryption posture + operational anti-patterns): [`server/shared/dotnet/messaging/rabbitmq/README.md`](../server/shared/dotnet/messaging/rabbitmq/README.md). Spec authoring + codegen diagnostics → [`messaging/source-gen/README.md`](../server/shared/dotnet/messaging/source-gen/README.md). Transport-agnostic abstractions → [`messaging/abstractions/README.md`](../server/shared/dotnet/messaging/abstractions/README.md).

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

Irreversible flows (e.g., user-deletion anonymize) are NOT SAGAs — they're fire-and-forget fanouts; downstream services own their idempotent consumers rather than coordinating rollback. Full cross-service SAGA design is tracked in the Edge gateway planning docs (not yet built).

---

## Multi-instance scaling

Every D² service is designed to run with N replicas behind a load balancer. Correctness must not depend on instance affinity — sessions live in Redis (3-tier with PG dual-write), JWT validation reads from shared JWKS, rate-limit counters in Redis (cluster scope, never per-process), HTTP idempotency in Redis (`SET NX` + 24h TTL). Local in-memory caches are per-instance with cluster-wide L1 coherence via `ICacheInvalidationBackplane` (Redis pub/sub) for `*AndBroadcast*` write variants. Background jobs use Redis distributed locks (`SET NX`) — return early if held. Cache-invalidation events use fanout exchanges with exclusive auto-delete queues (not competing consumers) for cluster-wide propagation.

Full service-onboarding checklist (rate limiting / HTTP idempotency / session+auth / local caches / background jobs / cache invalidation / connection strings / DB constraints / migrations / cross-service mutations / encryption) is tracked in the Edge gateway planning docs (not yet built).

---

## Mappers

**The uppermost-node rule: a mapping lives in the highest layer that actually touches the foreign representation; every layer beneath it speaks domain (or `<Op>Input`/`<Op>Output` at the handler boundary).** A service has five mapping surfaces, each with exactly one home:

| # | Surface (foreign ↔ ours) | Home |
| - | ------------------------ | ---- |
| 1 | Transport — proto / REST JSON ↔ `<Op>Input`/`<Op>Output` | `api/Mappers/` (the host's api for a module-within-host) |
| 2 | Persistence — EF record ↔ domain aggregate | `app/Infrastructure/Persistence/` (beside the record) |
| 3 | Provider SDK — vendor type (Stripe / Resend / IpInfo) ↔ domain | `infra/<Concern>/<Vendor>/` (inside the adapter) |
| 4 | Messaging wire — spec-generated event ↔ domain values | `infra/Messaging/<Broker>/` (inside the publisher) |
| 5 | Primitives — `string` / `int` / bytes → domain VO | domain (`Create` factories + `Rules/` projections) |

Every mapper is a pure static C# 14 extension-member class — no DI, no IO, no validation — named `<ForeignType>Mapper`. Use the `extension(T target) { ... }` block form per `rules.md §5`, never the old `this T target` parameter style. Validation lives in the smart-constructor on the domain type (surface 5).

```csharp
extension(KeyRecord record)
{
    public EncryptionKey ToDomain() => /* flat row → sum-type aggregate, switch on Status */;
}
```

**WHY transport mapping lives in `api/`, not `app/`:** proto/REST is a transport concern and the api is the uppermost node of the transport path; placing it in app would force app to reference the generated proto types, coupling the orchestration layer to one wire format and breaking "App is reusable across transports." **WHY the persistence mapper stays in `app/`, not `infra/`:** under EF-as-DDD the handlers compose the queries and materialize the records, so app is the uppermost node of the persistence path; the record mapper is a pure mapper over the app-owned record shape with no EF dependency (and placing it in infra is impossible anyway — app may not reference infra). Full rationale → [ADR-0020](adrs/0020-service-project-structure.md) + [ADR-0017](adrs/0017-ef-as-ddd-persistence.md).

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

`WhoIs` follows the same content-addressable pattern with a single aggregate factory. See the per-lib READMEs for the full surface ([D2.Shared.Location](../server/shared/dotnet/location/core/README.md)).

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

## Anonymization — `[Anonymizable]` decoration + tiered reflection engine

At-rest subject-keyed PII overwrite (GDPR right-to-erasure): on user/org deletion the engine overwrites that subject's PII **in place** with faux or tombstone values — never NULL by default, never hard-delete. Faux values are non-i18n developer-supplied literals (tombstone strings, template-computed addresses). This concern is **strictly separate from `[RedactData]`** (Serilog log-masking): the two systems are independently decorated, independently enforced, and do not cross-check each other. Lives in `D2.Shared.DataGovernance` — a pure Abstractions seam library plus an EF Core engine implementation.

### Decoration — one annotation, two front-ends

**Markers** — three interfaces every decorated entity type interacts with:

| Interface | Role |
|---|---|
| `IUserOwned` / `IOrgOwned` | Engine `WHERE` clause subject key. `Guid?` — rows with a `null` id are skipped. |
| `IExemptFromAnonymization` | Opt-out: engine skips the entity type entirely, even if it carries decorated fields. |
| `IAnonymizationTrackable` | Mandatory on every ownership-marked entity that has decorated fields. `bool IsAnonymized` — set by the engine; excludes already-anonymized rows on re-run for idempotency. Guard-enforced at startup. |

**`[Anonymizable]` attribute** — for consumer-owned types (entity scalars + owned/complex sub-properties of VOs the consuming service authors). Activated by calling `builder.ApplyAnonymizationConventions()` in `ConfigureConventions`. Four call-site forms:

| Form | Strategy |
|---|---|
| `[Anonymizable(AnonymizeKind.SetNull)]` | `SetNull` — column must be nullable |
| `[Anonymizable(AnonymizeKind.SetEmpty)]` | `SetEmpty` — column must be a string |
| `[Anonymizable("tombstone")]` | `Constant("tombstone")` — fixed developer string |
| `[Anonymizable(template: "deletedUser{UserId}@deleted.user.dcsv.io")]` | `Template` — `{FieldName}` sibling interpolation; `Guid` values rendered without dashes |

**Fluent path** — `Anonymize*` block-form extensions on `PropertyBuilder<T>`, `OwnedNavigationBuilder<TOwner, TDependent>`, `ComplexPropertyBuilder<T>`, and `ComplexTypePropertyBuilder<T>` (the type returned by `cp.Property(lambda)` inside a `ComplexProperty` callback — the "receiver-is-the-property" form used when you already hold the member builder). This is the **universal** path and the **only** path for foreign VOs — types the consuming service does not own and cannot annotate (e.g., `D2.Shared.Location` types, Contacts VOs).

```csharp
model.Entity<User>()
     .OwnsOne(u => u.Address, nav =>
     {
         nav.AnonymizeNull<string?>(a => a.Street);
         nav.AnonymizeEmpty<string?>(a => a.PostalCode);
     });
```

Both paths converge on the **same `D2:Anonymize` EF model annotation** (`AnonymizationAnnotations.ANONYMIZE`). The engine reads only the annotation at runtime — origin-agnostic. Precedence: fluent > attribute (EF Explicit > DataAnnotation). Decoration is **purely opt-in per field**; no `[RedactData]` completeness cross-check exists or is intended — a missed PII field is the consumer's responsibility, not a boot failure.

A `[NotMapped]` property is invisible to the attribute convention and is rejected outright by the fluent sub-selectors (`InvalidOperationException` at model-build time) — anonymization decorates already-persisted columns, not unmapped members.

### Tiered reflection-driven engine

Per-entity-type classification is cached at startup (analogous to `RedactDataDestructuringPolicy`'s per-type `ConcurrentDictionary`). Three tiers:

| Tier | Applies when | Strategy |
|---|---|---|
| A | Scalar / table-split owned / complex (incl. JSON column) | `ExecuteUpdateAsync` — no rows materialized; one filtered round-trip per entity type |
| B | Any entity with a `Template` rule | Materialize → mutate in CLR → `SaveChangesAsync` (chunked by `BatchSize`, concurrency-aware reload-retry up to `MaxConcurrencyRetries`) |
| C | Owned-JSON or `OwnsMany` child table | Fail-fast at startup — blocked by the startup guard (never silent) |

The engine is provider-agnostic across any relational EF Core provider. Column names are resolved via `IProperty.GetColumnName()` — never guessed from CLR names.

`IAnonymizationEngine` exposes `AnonymizeUserAsync(Guid userId, ct) → Task<D2Result<AnonymizationOutcome>>` and `AnonymizeOrgAsync`. Semantics: `Guid.Empty → ValidationFailed`; idempotent (filters `IsAnonymized == false`); **fail-closed** (any single entity-type failure returns a non-Ok result — never silent partial success); re-run safe. PII-safe logging omits the subject id entirely — each sweep gets a fresh `sweepId` plus counts and model-metadata names only.

**Deny-by-default startup guard** — `AnonymizationModelValidator` (`IHostedService`) validates the host `DbContext` model before traffic flows, collecting all findings before throwing a PII-safe `InvalidOperationException`. The seven guard rules enforce declared-rule integrity:

- Every decorated entity implements an ownership marker or `IExemptFromAnonymization` (V1)
- Every decorated non-exempt entity implements `IAnonymizationTrackable` (V2)
- No decorated entity is Tier-C (V3)
- Every `Template` token names an existing scalar sibling (V4)
- Every `[Anonymizable]`-decorated property has a `D2:Anonymize` annotation — detects missing `ApplyAnonymizationConventions()` (V5)
- No divergent attribute + fluent double-declaration on the same property (V6)
- No `SetNull` rule targets a non-nullable column (V7)

Opt out for test hosts only: `DATA_GOVERNANCE__SKIPMODELVALIDATION=true`.

**DI**: `services.AddD2DataGovernance(configuration)` — one call registers `IAnonymizationEngine`, `AnonymizationEngineOptions` (section `DATA_GOVERNANCE`), and the validator hosted service.

Canonical references: [`data-governance/abstractions/README.md`](../server/shared/dotnet/data-governance/abstractions/README.md) (markers, attribute, rule, engine seam) · [`data-governance/entity-framework-core/README.md`](../server/shared/dotnet/data-governance/entity-framework-core/README.md) (full fluent API, options, DI, guard rules) · [ADR-0015](adrs/0015-anonymization-data-governance.md).

---

## Contact value objects

`D2.Shared.Contacts` ships six composable, self-redacting PII value objects. Each is constructed through a `Create(...) → D2Result<T>` smart constructor that applies a dumb structural floor (length caps from the `FieldConstraints` catalog, shape / coherence rules) before constructing the record. Email and phone additionally accept an optional caller-injected smart validator (`IEmailValidator` / `IPhoneValidator`); when omitted, the structural floor applies.

| VO | Key fields | Notes |
|---|---|---|
| `Personal` | `FirstName` (req), `MiddleName?`, `LastName?`, `PreferredName?`, `HashId` | `HashId` = `"v1." + SHA-256` over First/Middle/Last — correlation, NOT dedup; PreferredName excluded for digest stability |
| `NameAffixes` | `Prefix?` (`NamePrefix`), `PrefixCustom?`, `Suffix?` (`NameSuffix`), `SuffixCustom?` | Custom required iff enum is `Other`; all-null rejected |
| `Demographics` | `DateOfBirth?` (`LocalDate`), `BiologicalSex?` | All-null rejected; DOB not in future, not > 150 years past |
| `Professional` | `CompanyName` (req), `JobTitle?`, `Department?`, `CompanyWebsite?` (`Uri`) | No `HashId` |
| `EmailAddress` | `Value` | Floor: trim + lowercase + shape check + length cap; validator: bubbled verbatim |
| `PhoneNumber` | `Value` | Floor: digits only, 7–15 digits, raw-length cap; validator: bubbled (typically E.164) |

All PII properties carry `[RedactData(Reason = RedactReason.PersonalInformation)]` — self-redacting in Serilog logs. `Personal.HashId` is visible (one-way digest, correlation-safe). Address fields reuse the `D2.Shared.Location` VOs directly — no new address VO.

Canonical reference: [`contacts/core/README.md`](../server/shared/dotnet/contacts/core/README.md).

---

## EF VO mapping — complex types + value converters

The folded owned-component model (ADR-0001) maps contact and location VOs into their host entities with zero EF references on the domain types. The mechanism splits by VO shape:

- **Multi-field VOs → EF complex types** (`ComplexProperty`). Members become first-class queryable columns with clean JOIN-free SQL. The host calls a per-VO helper inside a `b.ComplexProperty(p => p.Name, cp => …)` callback:

  ```csharp
  // Host infra IEntityTypeConfiguration<Person> — illustrative.
  b.ComplexProperty(p => p.Name, cp => cp.MapPersonal());
  b.ComplexProperty(p => p.Location, cp => cp.MapAdminLocation());
  ```

  Each helper wires member value converters, applies `HasMaxLength` from `FieldConstraints`, and writes per-field anonymize defaults via the `D2.Shared.DataGovernance.EntityFrameworkCore` fluent API in one call.

- **Single-value VOs → EF value converters**. One column, native unique index, root-scoped anonymize templates. The helper returns a coupling object:

  ```csharp
  b.MapEmailAddress(p => p.Email)
   .Unique("deletedUser{UserId}@deleted.user.dcsv.io");  // unique index + template
  ```

  **"Unique-without-a-template" is unrepresentable.** There is no parameterless `.Unique()` — the type system removes the footgun. A static tombstone would collide on a unique column; requiring a `{Token}` ensures per-row-distinct values on erasure.

- **Same-VO-type-twice** (e.g., legal name + maiden name `Personal`, billing + shipping `AdminLocation`) works natively: call the helper twice via two distinct host-property selectors. EF Core 10 prefixes columns by the owning-property path (`LegalName_FirstName` vs `MaidenName_FirstName`). The helpers never call `HasColumnName`, which preserves this automatic uniquification.

Available helpers — **Contacts** (`D2.Shared.Contacts.EntityFrameworkCore`): `MapPersonal`, `MapNameAffixes`, `MapDemographics`, `MapProfessional` (complex), `MapEmailAddress`, `MapPhoneNumber` (value converter). **Location** (`D2.Shared.Location.EntityFrameworkCore`): `MapStreetAddress`, `MapAdminLocation`, `MapCoordinates` (complex; `SubdivisionCode`/`CountryCode` converters encapsulated in `MapAdminLocation`; Coordinates tombstones on erasure).

Canonical references: [`contacts/entity-framework-core/README.md`](../server/shared/dotnet/contacts/entity-framework-core/README.md) · [`location/entity-framework-core/README.md`](../server/shared/dotnet/location/entity-framework-core/README.md).

---

## EF Core 10 complex-member-index limitation + `CreateD2Index`

> **User-flagged footgun.** This is not a gap in the toolkit — it is a documented EF Core 10 limitation. Consumers who need an index on a `ComplexProperty` member column must use the recipe below.

**The limitation.** In EF Core 10, model-aware indexes on `ComplexProperty` member columns are not expressible via the fluent API:

- `HasIndex(u => u.Vo.Member)` — throws "not a valid member-access expression" at model-build time.
- `HasIndex("Vo_Member")` — throws (shadow property needs a declared type; EF won't accept it for a complex-type member column).
- Metadata `AddIndex([complexMemberProp])` + a finalizing convention — **silently discarded** at finalization (`"index properties … not declared on the entity type"`); `IMigrationsModelDiffer` emits **zero** `CreateIndexOperation`.

The only EF-10 path is a raw `migrationBuilder.CreateIndex(name, table, "Vo_Member")` line in the host migration — **model-unaware** (the host owns the index lifecycle; EF 10 does not know about it).

**The toolkit workaround — `CreateD2Index`.** `D2.Shared.EntityFrameworkCore` ships a typed `MigrationBuilder` extension:

```csharp
// In the host's Up() migration method.
migrationBuilder.CreateD2Index<Person>(
    table: "persons",
    member: u => u.Name.FirstName,
    unique: false);
// Derives column name "Name_FirstName" from the expression — no magic string.
```

`CreateD2Index<TEntity>(table, member, name?, unique?)` derives the `{ComplexProp}_{Member}` column name from the typed selector expression, emits a `CreateIndexOperation`, and keeps the migration line type-safe.

**Current limitation**: EF Core 10 cannot declare model-aware indexes on `ComplexProperty` member columns — fluent `HasIndex(u => u.Vo.Member)` throws and metadata-path indexes are silently discarded at finalization. `CreateD2Index` is the current workaround. EF Core 11 (issue [#31246](https://github.com/dotnet/efcore/issues/31246), merged 2026-05-19) makes `HasIndex(u => u.Vo.Member)` native; migrating existing `CreateD2Index` calls to fluent `HasIndex` when EF 11 is adopted is a tracked follow-up.

Value-converter indexes and complex-member _queries_ have **no such limitation**.

Canonical reference: [`entity-framework-core/README.md`](../server/shared/dotnet/entity-framework-core/README.md).

---

## Field-constraints codegen catalog

A spec-driven catalog (`contracts/validation/field-constraints.spec.json`) emits shared field-length constants and three taxonomy enums to both .NET and TypeScript — one source of truth for field-size caps and closed-list enumerations.

**What it emits:**

- `FieldConstraints` — `public const int` caps consumed by VO `Create` gates, Location VOs, and frontend Zod schemas (e.g., `FIRST_NAME_MAX`, `EMAIL_MAX`, `POSTAL_CODE_MAX`, `PHONE_MIN_DIGITS`).
- `NamePrefix` / `NameSuffix` / `BiologicalSex` — `byte`-backed, string-wire taxonomy enums with `[JsonConverter(typeof(JsonStringEnumConverter))]`.

**.NET target** — emitted into `D2.Shared.Validation.Abstractions` by `validation/source-gen/` (Roslyn `IIncrementalGenerator`, `D2FC` diagnostic prefix, single-target dispatch gated on `AssemblyName == "D2.Shared.Validation.Abstractions"`). Files land in `validation/abstractions/Generated/`.

**TypeScript target** — emitted into `@d2/validation-abstractions` by `tools/ts-codegen/src/field-constraints-emit.ts`. Files land in `server/shared/typescript/validation/abstractions/src/generated/`.

**Consumers:** `D2.Shared.Contacts` VO `Create` factories; `D2.Shared.Location` VO `Create` factories; `@d2/validation-abstractions` Zod schemas; frontend form validators.

Canonical references: [SRC_GEN.md](SRC_GEN.md) · [`validation/abstractions/README.md`](../server/shared/dotnet/validation/abstractions/README.md).

---

## Spec-driven codegen — the cross-cutting pattern

Shared vocabularies that ship across language boundaries (.NET handlers, TS clients, ops dashboards) live in JSON spec files under `contracts/`. A Roslyn `IIncrementalGenerator` reads the spec at every build and emits typed constants directly into the consuming assembly. Hand-mirrored constants are forbidden — drift between spec and code is structurally impossible because the constants don't exist unless the spec entry does.

The pattern in one paragraph: spec at `contracts/{topic}/{topic}.spec.json` (paired with `schema.json`); SourceGen csproj at `server/shared/dotnet/{topic}-source-gen/` (`netstandard2.0`, `IsRoslynComponent=true`, gated by `Compilation.AssemblyName`); consumer csproj wires `<ProjectReference … OutputItemType="Analyzer" ReferenceOutputAssembly="false" />` + `<AdditionalFiles Include="…/{topic}.spec.json" />`; codegen output committed to git under `Generated/` with `linguist-generated=true`; source-gen tests use per-VALUE substring pins (`.Contain("public const string FOO = \"foo\";")`) — never framework snapshots.

When a hand-written constants catalog gets a spec backing, the migration is **outright deletion of the hand-written file plus net-new spec authoring** — not a parallel-emit-then-deprecate dance. A parallel-emit phase would let stale hand-written constants drift undetected.

Canonical: [`docs/SRC_GEN.md`](SRC_GEN.md) — full how-to-author guide for both .NET (Roslyn `IIncrementalGenerator`) and TypeScript (`tools/ts-codegen`). Sourcegen registry (19 spec catalogs, grouped by purpose) → [`server/shared/dotnet/README.md` § Source generators](../server/shared/dotnet/README.md#source-generators-registry).

This pattern is the structural enforcement behind the [5-layer rename safety net](#composition-root) — every renamed spec field cascades through generated code, consumer compile sites, `nameof()`-bound emission sites, behavioral tests, and spec-pin literal tests, in that order.

---

## Per-package versioning & releases

Each consumable shared library (`D2.Shared.*` for .NET, `@d2/*` for TypeScript) carries its own `MAJOR.MINOR.PATCH` version and `CHANGELOG.md`. The `tools/release-runner` derives per-package bumps from a **build-free artifact diff** — a source fingerprint (SHA-256 over committed source + API report + resolved dep versions + declared toolchain pin) plus a public-API-surface diff. Commit footers (`WIRE-BREAKING:` / `BREAKING CHANGE:`) are **escalation overrides only**: they can raise the diff-derived bump level but never lower it. The footer is also the ONE conscious force-valve act for breaks not detectable by the diff. Wire/contract breaks are auto-detected by `tools/contract-gate`; library public-API breaks are author-declared via footer. Registry publishing (npm / NuGet) is never automatic. Deployable services are excluded — they version on the product cadence.

Canonical discipline: `rules.md §26.19`. Operational how-to: `CONTRIBUTING.md` (Per-package versioning) + `docs/COMMANDS.md` (Per-package version + Cutting a library release).

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

## Input validation

Three cross-language validators cover the three most common user-input fields. Both .NET and TypeScript ship the same two-package split: an abstractions package (contracts only, zero impl dependencies) and a default package (concrete implementations + DI registration).

| Validator | .NET | TypeScript | Backing |
| --------- | ---- | ---------- | ------- |
| Email | `IEmailValidator` / `DefaultEmailValidator` | `IEmailValidator` / `DefaultEmailValidator` | Shared ASCII structural regex, trim + lowercase |
| Phone | `IPhoneValidator` / `DefaultPhoneValidator` | `IPhoneValidator` / `DefaultPhoneValidator` | libphonenumber port — parse + E.164 normalize |
| Postal code | `IPostalCodeValidator` / `DefaultPostalCodeValidator` | `IPostalCodeValidator` / `DefaultPostalCodeValidator` | Per-country regex ported from postcode-validator |

Every validator exposes a single `Validate(...)` / `validate(...)` method returning `D2Result<string>`:

- **Success** — the normalized value (trimmed + lowercased email; E.164 phone; trimmed + uppercased postal code).
- **Failure** — `ValidationFailed` with a single per-field `InputError` keyed with `TK.Common.Validation.*_INVALID`. Field keys are `"email"`, `"phone"`, `"postalCode"`. Empty, whitespace-only, and structurally invalid input all collapse to the same `*_INVALID` failure.

Cross-language behavior is pinned against `contracts/validation/fixtures/{email,phone,postcode}.json` parity fixtures — a value accepted on one runtime is accepted on the other. Postal-code validation fails closed: an unknown or absent country code always returns `ValidationFailed`; there is no permissive global-range fallback on either runtime.

Canonical per-lib READMEs:
[D2.Shared.Validation.Abstractions](../server/shared/dotnet/validation/abstractions/README.md) ·
[D2.Shared.Validation](../server/shared/dotnet/validation/default/README.md) ·
[@d2/validation-abstractions](../server/shared/typescript/validation/abstractions/README.md) ·
[@d2/validation](../server/shared/typescript/validation/default/README.md).

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

Canonical implementation: `DefaultGeoNameResolver` in `server/shared/dotnet/geo/default/NameResolution/`. TS mirror: `server/shared/typescript/geo/default/src/name-resolution/default-geo-name-resolver.ts` (TS uses a module-scoped `Map | undefined` plus a build-count interlocked counter for thundering-herd safety under JS's single-thread execution).

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

## Reference data

Reference data is static, spec-driven, and versioned. Unlike user-generated entities it is read-only at runtime, ships with the binary, and changes only through a codegen rebuild cycle. D²-WORX manages reference data through three layers:

1. **JSON specs** under `contracts/geo/` — the single source of truth for every country, subdivision, currency, language, locale, and timezone.
2. **Codegen output** in `D2.Shared.Geo.Abstractions` (record shapes, typed enums, wrapper structs, lookup contracts) and `D2.Shared.Geo.Default` (static catalog instances, FrozenDictionary lookup tables, name-resolver warm-up).
3. **Deprecation metadata** — the `DeprecationInfo` record on each entity communicates withdrawn or merged entries without deletion (ISO 3166 country changes, superseded subdivision codes, renamed currencies). Consumers check `entry.Deprecated` before use.

### Endonym discipline

Display names follow the endonym-first convention: the entity carries its name in the locale of origin (`"Deutschland"` for Germany, `"日本"` for Japan), plus an `EnglishName` for cross-locale rendering. Sort by `EnglishName` for user-facing lists; use `DisplayName` only when the locale context is known.

### Typed geo catalogs

Closed-set catalogs (country, currency, language, geopolitical entity) are real C# enums with the `Code` suffix: `CountryCode`, `CurrencyCode`, `LanguageCode`, `GeopoliticalEntityCode`. Open-set catalogs (subdivision, locale, timezone) are branded wrapper structs: `SubdivisionCode`, `LocaleCode`, `TimezoneCode`. The distinction determines the .NET shape — closed-set gets a real enum, open-set gets a struct:

| Category | .NET shape | Rationale |
|---|---|---|
| Closed-set (Countries, Currencies, Languages, GeopoliticalEntities) | Real enum (`ushort` backing for Country; `ushort` for Currency; `ushort` for Language) | IDE autocompletion is exhaustive; switch expressions are exhaustive; `FrozenDictionary` keyed on enum has zero string-allocation overhead per lookup |
| Open-set (Subdivisions, Locales, Timezones) | `readonly struct` wrapping a `string` | Codes contain hyphens and slashes (`US-NY`, `en-US`, `America/New_York`) that C# identifiers cannot encode; the struct gives a strong type at compile time while the backing string carries the full code |

**Never use a raw `string` for a typed code.** Pass `CountryCode.US`, `SubdivisionCode.US_NY`, `LocaleCode.EnUS`, `TimezoneCode.AmericaNewYork` — not `"US"`, `"US-NY"`, `"en-US"`, `"America/New_York"`. The type system enforces no `NotFound` case is possible when the enum IS the catalog.

```csharp
// Typed static access — IDE auto-completes the enum member.
Country us = Countries.US;
Subdivision ny = SubdivisionLookup[SubdivisionCode.US_NY];
Currency usd = CurrencyLookup[CurrencyCode.USD];
Language en = LanguageLookup[LanguageCode.En];

// String-input boundary code (deserialization, name-resolver fallback).
// Preferred only at ingestion boundaries, not in domain logic.
CountryCode? code = "US".TryParseTruthyNull<CountryCode>(ignoreCase: true, out var c)
    ? c : (CountryCode?)null;
```

Canonical lib references: [D2.Shared.Geo.Abstractions](../server/shared/dotnet/geo/abstractions/README.md) · [@d2/geo-abstractions](../server/shared/typescript/geo/abstractions/README.md) · [D2.Shared.Geo.Default](../server/shared/dotnet/geo/default/README.md) · [@d2/geo-default](../server/shared/typescript/geo/default/README.md).

### Typed access on IRequestContext

The raw string fields on `IRequestContext` (`CountryIso31661Alpha2Code`, `SubdivisionIso31662Code`) carry ISO codes as they arrived over the wire. Extension methods in `D2.Shared.Geo.Abstractions` parse these to typed codes; extensions in `D2.Shared.Geo.Default` return the full record:

```csharp
// Abstractions-layer — parse only (no catalog dep).
using D2.Shared.Geo.Abstractions.Extensions;
CountryCode? code = request.Country();            // null if absent / unknown
SubdivisionCode? sub = request.Subdivision();    // null if absent / unknown

// Default-layer — catalog lookup (returns full record with all nav properties).
using D2.Shared.Geo.Default.Extensions;
Country? country = request.Country();            // null if absent / unknown
Subdivision? subdivision = request.Subdivision();
string? lang = request.Country()?.PrimaryLanguage?.DisplayName; // no second lookup needed
```

The two extension classes share the method names `Country()` and `Subdivision()` but live in different namespaces — the compiler resolves by which `using` is in scope (CS0121 when both are imported, so consumers pick one). TypeScript uses distinct free-function names (`countryFor(context)` / `subdivisionFor(context)`) in `@d2/geo-default/extensions/` for the same reason the TS language lacks extension-method namespace shadowing.

### Geo name resolution at the integration boundary

When third-party text data arrives (geolocation APIs, shipping providers, form submissions), country / subdivision names are free strings that may be in any language or spelling variant. The typed catalog layer cannot accept them directly. Use `IGeoNameResolver` at the ingestion boundary:

```csharp
// Inject IGeoNameResolver (registered by D2.Shared.Geo.Default DI extension).
public sealed class IngestWhoIsHandler(IGeoNameResolver geoNames, ...) : BaseHandler<...>
{
    protected override async Task<D2Result<WhoIs>> ExecuteAsync(RawWhoIsInput input, ...)
    {
        // Resolve free-text country name → typed record (cache-aside: O(n) first,
        // O(1) thereafter). Partial / fuzzy match via Levenshtein comparer.
        D2Result<Country> country = await geoNames.FindCountryAsync(input.CountryName);
        if (country.BubbleOnFailure<Country, WhoIs>(out var failed, out var c))
            return failed;

        // From here, use the typed Country record — no further resolver calls.
        CountryCode code = c.Iso31661Alpha2Code;
        ...
    }
}
```

`IGeoNameResolver` is NOT for typed-code inputs. A handler that already holds `CountryCode.US` from a JWT claim or `IRequestContext` uses the typed accessors (`Countries.US`, `CountryLookup[code]`) — no resolver needed. Resolver calls are reserved for the ingestion boundary where third-party text arrives. Misusing the resolver for typed-code inputs is a correctness regression (Levenshtein distance on `"US"` against all 249 countries may not return `CountryCode.US` if a display name is also `"US"`).

### Reference data — user-preference cascades

The `LocaleIetfBcp47Tag`, `TimezoneIanaName`, and `CurrencyIso4217Code` fields on `IRequestContext` carry the resolved user preference for each category. The resolution algorithm is owned by the Edge auth middleware (outside this lib's scope); the propagated field carries the resolved value to every downstream consumer.

**Locale resolution** (priority high → low):

1. User profile preference (DB; loaded at session start, cached in Redis).
2. Org default preference (DB; loaded at session start, cached).
3. `X-D2-Locale` header (explicit user UI override).
4. `Accept-Language` header (browser / OS preference).
5. Fallback: `"en-US"`.

**Timezone resolution** (priority high → low):

1. User profile.
2. Org default.
3. `X-D2-Timezone` header (BFF-set from `Intl.DateTimeFormat().resolvedOptions().timeZone`).
4. WhoIs-derived: `CountryIso31661Alpha2Code` + `SubdivisionIso31662Code` → IANA name via `D2.Shared.Geo.Default` lookup.
5. Fallback: `"UTC"`.

**Currency resolution** (priority high → low):

1. User profile.
2. Org default.
3. `X-D2-Currency` header (explicit UI selection).
4. `CountryIso31661Alpha2Code`-derived: ISO 3166 → ISO 4217 primary legal-tender mapping via `D2.Shared.Geo.Default`.
5. Fallback: `NULL` — no sensible universal default. Consumers surface a validation error when `CurrencyIso4217Code` is required and absent.

The `X-D2-Locale`, `X-D2-Timezone`, and `X-D2-Currency` headers are `http`-only constants in `HttpHeaders` (generated from `contracts/headers/headers.spec.json`). The Edge auth middleware that reads them is responsible for adding them to `Access-Control-Allow-Headers` in CORS configuration before any cross-origin client starts sending them.

---

## Hash composition

Content-addressable entities compute their hash IDs via a versioned prefix + multi-component slot rule. Two guarantees:

1. **Versioned prefix** — `"v1." + hex` means a future hashing-algorithm change (`v2.*`) never collides with existing IDs. Old and new hashes can coexist in persistence until a migration window closes.
2. **Multi-component slot rule** — when an entity is composed from independent sub-components (e.g. `Location = f(Coordinates, StreetAddress, AdminLocation)`), each sub-component hashes independently, then the composed hash is SHA-256 over the concatenated sub-component hashes in canonical slot order. Absent sub-components contribute their own canonical empty-slot hash. This prevents "missing field → null → same hash as different-missing-field" collisions.

```csharp
// Each sub-component is independently content-addressable.
var coords = Coordinates.Create(lat, lon).Data;     // "v1.<sha256(lat||lon)>"
var admin  = AdminLocation.Create(...).Data;         // "v1.<sha256(country||sub||city||postal)>"

// Composition: SHA-256 over the two HashIds in slot order (coords first, admin second).
// Absent sub-component uses a canonical empty-slot sentinel defined per VO.
string? locationHash = ComposeLocationHash.Compose(coords, admin);
```

Normalization: string inputs are lower-cased and whitespace-stripped before hashing. The factory documents the canonical slot order and normalization contract. No caller may replicate the hashing logic — only the factory can produce a valid hash for that entity type, so refactoring the hash scheme has exactly one edit site.

---

## Anti-patterns to actively avoid

- **Thin handlers that just call another handler** — if an app-layer handler's body is `return otherHandler.HandleAsync(input)`, delete it; depend on the inner handler directly.
- **Hand-written DB migrations** — generator-driven only. Per `rules.md §9.10`.
- **String error codes outside `D2Result` factories** — use `TK.*` constants from `D2.Shared.I18n`.
- **Wrapping framework primitives without an opinionated semantic** — use `IDistributedCache` directly only if Microsoft's `Get` / `Set` / `Refresh` / `Remove` is enough. If you need `SetNx` / `Increment` / `AcquireLock` — use D²'s richer abstraction.
- **Returning `Ok()` after a fallible operation** — a `try/catch` that swallows failure and returns success is almost always a bug. Either `BubbleFail` or explicitly handle. Per `rules.md §9.20`.
- **Hardcoding what should be in Options** — batch sizes, cache expirations, retry attempts, lock TTLs all go through `IOptions<T>`.
- **Identity (UserId / OrgId / Scopes) in AMQP headers** — identity rebuilds from the JWT at every sync hop. Headers stay plaintext at-rest.
- **Hand-mirrored constants for spec-driven catalogs** — if a catalog has a SourceGen, the hand-written file gets deleted outright. See [Spec-driven codegen](#spec-driven-codegen--the-cross-cutting-pattern).
- **Pure logic hosted in the app layer** — a generator/verifier/projection with no port and no IO belongs in domain `Rules/`, not behind an app-layer handler or strategy interface. App handlers orchestrate; they do not host domain logic. See [Service project structure](#service-project-structure).
- **A flat `Models/` DTO bucket** — a DTO either co-locates with its operation (`<Op>Input`/`<Op>Output`) or, when shared by 2+ operations, is promoted to a domain VO. A flat folder of records has no per-shape owner.
- **A generic `Providers/` wrapper for vendor adapters** — use a capability concern folder + a mandatory vendor/tech/protocol subfolder (`infra/Email/Resend/`, not `infra/Providers/Email/`). See [Service project structure](#service-project-structure).
