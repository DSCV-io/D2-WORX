<!--
Copyright (c) DCSV. All rights reserved.
-->

# PATTERNS.md — D²-WORX Code Patterns

> The load-bearing patterns + invariants that every D²-WORX shared library and service embodies.

---

## .NET project layout (`.csproj`)

Universal build properties (`TargetFramework`, `LangVersion`, `Nullable`, `ImplicitUsings`, `TreatWarningsAsErrors`, `GenerateDocumentationFile`, the `StyleCop.Analyzers` package, the `stylecop.json` link, and the four global usings — `System` / `System.Collections.Generic` / `System.Threading` / `System.Threading.Tasks`) live in `server/Directory.Build.props` and apply to every `.csproj` automatically. Per-csproj files only declare what's project-specific.

**Canonical lib `.csproj` (minimal):**

```xml
<!--
Copyright (c) DCSV. All rights reserved.
-->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <RootNamespace>D2.Shared.{LibName}</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\Other\Other.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="SomePackage" />   <!-- no Version="..." — Central Package Mgmt handles it -->
  </ItemGroup>
</Project>
```

**Per-project flags by project type:**

| Project type | SDK | Add to PropertyGroup |
|---|---|---|
| Shared lib | `Microsoft.NET.Sdk` | `<RootNamespace>` |
| Service (api/app/domain/infra) | `Microsoft.NET.Sdk` | `<RootNamespace>` |
| Service API (HTTP/gRPC entry) | `Microsoft.NET.Sdk.Web` | `<RootNamespace>` |
| Test project | `Microsoft.NET.Sdk` | `<RootNamespace>`, `<IsPackable>false</IsPackable>`, `<IsTestProject>true</IsTestProject>`, `<UseMicrosoftTestingPlatformRunner>true</UseMicrosoftTestingPlatformRunner>`, `<TestingPlatformDotnetTestSupport>true</TestingPlatformDotnetTestSupport>` (xUnit v3 + MTP) |

**`RootNamespace` rule:** always declared explicitly; follows the **namespace structure**, not the directory path. Example: `DistributedCache.Redis.csproj` lives at `server/shared/dotnet/Implementations/Caching/Distributed/DistributedCache.Redis/` but its `RootNamespace` is `D2.Shared.DistributedCache.Redis` — the `Implementations/Caching/Distributed/` path noise is dropped. Folder layout serves discoverability; namespace serves consumers.

**Central Package Management:** every package version is pinned in `server/Directory.Packages.props`. `<PackageReference>` items in csproj files reference by ID only — **never** include `Version="..."` (CPM rejects it).

**`dotnet build` enforces zero warnings.** `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` makes every StyleCop / CA**** / null-ref / CS1591 (missing XML doc on public API) warning a hard build failure. There is no "warning we can ignore" — fix it or document why with an `.editorconfig` rule override.

---

## TLC / 2LC / 3LC Folder Convention

Three-tier folder hierarchy for all backend code. **TLC** = architectural concern, **2LC** = implementation type, **3LC** = operation type.

### Canonical TLCs (with their 3LC alphabets)

| TLC | 3LC verbiage | Meaning |
|---|---|---|
| **CQRS** | `C/` Commands, `Q/` Queries, `U/` Utilities, `X/` Complex | Business operation intent |
| **Messaging** | `Pub/` Publishers, `Sub/` Subscribers | Message direction |
| **Repository** | `C/` Create, `R/` Read, `U/` Update, `D/` Delete | CRUD operation |
| **Caching** | `C/` Create, `R/` Read, `U/` Update, `D/` Delete | CRUD operation |
| **Outbound** | (per-protocol — `Grpc/`, `Http/`, `S3/`, etc.) | Outbound integrations |
| **Realtime** | `Push/` (and future `Stream/`) | Real-time push (SignalR) |
| **Storage** | `C/` Create, `R/` Read, `U/` Update, `D/` Delete | Object/file storage |

### Layout

- **Interfaces** live in `Interfaces/{TLC}/Handlers/{3LC}/`
- **Implementations** live in `Implementations/{TLC}/Handlers/{3LC}/` (app layer) or `{TLC}/Handlers/{3LC}/` (infra layer)
- **One handler per file** under each 3LC subdirectory

### Capability vs Dependency

A handler's TLC reflects **what it does** (capability), not **what it uses** (dependency). A query handler that internally consults a cache is still a `Q/` handler — it doesn't become `Caching/Q/` because it reads cache. Reserve TLC for the primary capability of the handler.

### CQRS Q vs C distinction

| Type | Distributed cache | DB write | External API | Message publish | Test |
|---|---|---|---|---|---|
| **Query** | No | No | No | No | "If the process dies after, would state persist?" → **No** |
| **Command** | Yes | Yes | Yes | Yes | Primary intent = mutation of persistent/shared state |
| **Complex** | Yes | Yes | Yes | Yes | Primary intent = retrieval, but may mutate as side effect |
| **Utility** | Varies (per caller) | Varies | Varies | Varies | Shared logic invoked by other handlers as a building block — see "Why Utility lives in app, not domain" below |

**Local / in-memory caching is permitted as an invisible optimization** — instance-scoped, ephemeral, doesn't affect other instances. A query that warms a local memory cache is still a query.

#### Why Utility lives in app layer, not domain

A Utility handler exists when the same piece of logic is needed by multiple Q/C/X handlers AND that logic requires something the domain layer shouldn't carry. The default instinct — "it's pure logic, put it in domain" — is wrong when:

- The logic depends on **third-party libraries** (HTTP client, JSON / XML parser, regex engine, date / locale library, image processor, crypto provider, etc.) that would pollute domain's dep graph and force every domain consumer to transitively pull them in
- The logic depends on **DI-injected services** (logger, telemetry, options, other handlers) that the domain layer doesn't have access to
- The logic benefits from **handler-pattern infrastructure** (auto-emitted OTel metrics, `[RedactData]` integration, `DefaultOptions`, structured input/output logging) that pure domain methods can't access

Domain stays pure: only entities, value objects, business rules, and helper methods that need ZERO external deps. Anything heavier becomes a Utility handler in the app layer.

The reverse is also true — if the logic is genuinely pure (no third-party deps, no DI services, no need for handler infra), put it on the domain entity / value object as a method. Don't manufacture a Utility handler just because "it might be called from multiple places."

### Interface organization

One handler interface per file under `Interfaces/{TLC}/Handlers/{3LC}/`. Consumers `using` the namespaces directly — no `partial` interface aggregation, no grouping aliases. The folder structure IS the discoverability mechanism. (We tried per-operation partial-interface aggregation; it added more friction than it removed.)

### Verb Semantics

- **Find** = "Resolve this for me" — may fetch from external source, may cache/persist. Example: `FindWhoIs`
- **Get** = "Give me this by ID" — direct lookup, read-only. Example: `GetWhoIsByIds`

---

## Handler

`.NET`: `BaseHandler<TSelf, TInput, TOutput>` with using aliases (`H`, `I`, `O`), `IHandlerContext`, `DefaultOptions` override.

### Pipeline shape — `HandleAsync` is virtual; `RunCorePipelineAsync` is sealed

`HandleAsync` is `virtual` and acts as a thin policy layer. The actual observability pipeline (activity, span tags, log scope, stopwatch, metrics, the universal try/catch) lives in a `protected` (non-virtual) `RunCorePipelineAsync` that returns a tuple:

```csharp
(D2Result<TOutput?> Result, Exception? CapturedException)
```

`CapturedException` is non-null only when the universal catch fired (= the Result will be `UnhandledException`). The tuple is `protected` to the BaseHandler hierarchy; **the `Exception` object never escapes BaseHandler — it is not a field on D2Result, never serialized, never crosses a wire boundary**. Subclasses that want to inspect it must do so by overriding `HandleAsync` and consuming the tuple locally.

Default `HandleAsync` is a one-line pass-through: `return (await RunCorePipelineAsync(...)).Result`. Existing handlers need zero changes.

### `BaseRepoHandler` — typed DB-failure mapping (provider-agnostic)

Repo handlers — anything that calls EF / a real database — inherit from `BaseRepoHandler<TSelf, TInput, TOutput>` (in `D2.Shared.Handler.Repo`) instead of `BaseHandler` directly. The base converts any database exception captured by `RunCorePipelineAsync` into a typed `D2Result` failure (concurrency conflict, unique violation, deadlock, connection failure, etc.) so callers can branch on **what actually went wrong** instead of getting a generic 500.

**Provider-agnostic by design.** `BaseRepoHandler` itself depends only on EF Core (`DbUpdateConcurrencyException` is BCL-typed and handled directly). All other DB exceptions are routed through an injected `IDbExceptionClassifier` (in `D2.Shared.Handler.Repo.Abstractions`). Provider-specific knowledge lives in sibling packages — PostgreSQL ships in `D2.Shared.Handler.Repo.Postgres`; future SQL Server / SQLite / MySQL providers would be sibling packages with the same shape.

#### Mapping

| Captured exception | Classified as | Default `D2Result` factory |
|---|---|---|
| `DbUpdateConcurrencyException` | `ConcurrencyConflict` (handled directly — BCL-typed) | `D2Result.ConcurrencyConflict()` |
| Anything else → `IDbExceptionClassifier.Classify(ex)` returns `UniqueViolation` | `UniqueViolation` | `D2Result.UniqueViolation()` |
| Returns `ForeignKeyViolation` | `ForeignKeyViolation` | `D2Result.ForeignKeyViolation()` |
| Returns `NotNullViolation` | `NotNullViolation` | `D2Result.NotNullViolation()` |
| Returns `CheckViolation` | `CheckViolation` | `D2Result.CheckViolation()` |
| Returns `Timeout` | `Timeout` | `D2Result.DbTimeout()` |
| Returns `Deadlock` | `Deadlock` | `D2Result.DbDeadlock()` |
| Returns `ConnectionFailure` | `ConnectionFailure` | `D2Result.DbConnectionFailure()` |
| Classifier returns `null` | unknown | Falls through — `BaseHandler`'s `UnhandledException` preserved |

`OperationCanceledException` is intentionally NOT remapped here — `BaseHandler.RunCorePipelineAsync` already handles it (`D2Result.Canceled` for caller-initiated cancellation, `D2Result.ServiceUnavailable` for downstream timeouts not tied to the request token).

The 8 typed factories all live as `D2Result` extensions in `D2.Shared.Handler.Repo.Abstractions` (`D2ResultDbFactories` / `D2ResultDbGenericFactories`). HTTP status mapping: `ConcurrencyConflict` / `UniqueViolation` / `ForeignKeyViolation` / `DbDeadlock` → 409; `NotNullViolation` / `CheckViolation` → 400; `DbTimeout` / `DbConnectionFailure` → 503.

#### Per-handler refinement — `MapDbException` override

The default factory dispatch produces a generic message ("This value is already in use") with no field-level information — useful for diagnostics but weak UX for form-driven flows. Handlers that know their constraint identity override `MapDbException` to attach a domain-specific `TKMessage` + `InputError`:

```csharp
public sealed class CreateUser(
    HandlerContext<CreateUser> context,
    IDbExceptionClassifier classifier,
    IAppDbContext db)
    : BaseRepoHandler<CreateUser, CreateUserInput, UserDto>(context, classifier), ICreateUser
{
    protected override async ValueTask<D2Result<UserDto?>> ExecuteAsync(
        CreateUserInput input, CancellationToken ct)
    {
        var user = User.Create(input);
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        return D2Result<UserDto?>.Created(user.ToDto());
    }

    protected override D2Result<UserDto?>? MapDbException(Exception ex, DbFailureKind kind)
    {
        // The DB-side unique index `users_email_key` covers the email column.
        if (kind == DbFailureKind.UniqueViolation && IsEmailIndex(ex))
        {
            return D2Result<UserDto?>.UniqueViolation(
                messages: [TK.Auth.Errors.EMAIL_ALREADY_TAKEN],
                inputErrors: [new InputError("email", "EMAIL_ALREADY_TAKEN")]);
        }

        return null; // fall back to the generic factory
    }
}
```

Returning `null` falls through to the default factory — handlers only customize the cases they care about.

#### Caller-side discrimination — typed booleans

Callers branch on the typed `IsXxx` extension properties (in `D2ResultDbBooleans`) instead of catching SQLSTATE strings or comparing raw `ErrorCode` values:

```csharp
var result = await createUser.HandleAsync(input);

if (result.IsUniqueViolation)        return Conflict(result);                  // 409, surface to user
if (result.IsConcurrencyConflict)    return await ReloadAndMergeAsync(input);  // optimistic-concurrency retry
if (result.IsTransientDbFailure)     return await retry.RetryAsync(...);       // deadlock / timeout / connection
if (result.IsForeignKeyViolation)    return BadRequest(result);                // referenced item missing
```

Roll-up: `IsTransientDbFailure = IsDbDeadlock || IsDbTimeout || IsDbConnectionFailure`. **Concurrency conflicts are intentionally excluded** — they need reload-then-merge logic, not a blind retry.

This sits on a different axis from the HTTP-flavored `IsTransientRetryable` (`IsServiceUnavailable || IsRateLimited`) on `D2Result` itself. A generic retry policy that wants to catch BOTH HTTP-flavored and DB-flavored transient failures should check the union: `result.IsTransientRetryable || result.IsTransientDbFailure`.

#### DI registration

`BaseRepoHandler` requires an `IDbExceptionClassifier` from DI. The composition root registers a provider-specific implementation:

```csharp
services.AddD2Handler();
services.AddD2Postgres();   // registers PostgresDbExceptionClassifier as IDbExceptionClassifier
services.AddDbContext<AppDbContext>(o => o.UseNpgsql(...));
services.AddTransient<ICreateUser, CreateUser>();
```

Without a registered classifier, resolving any `BaseRepoHandler` subclass fails fast at the container.

**Observability is unchanged.** `RunCorePipelineAsync` still calls `activity?.AddException(ex)`, records the exception metric, and emits the unhandled-exception log regardless of whether `BaseRepoHandler` remaps the result. Tempo + Loki get the full picture; type names (`exceptionType`, `innermostExceptionType`) are pushed to the log scope so Loki can filter by type without parsing message text.

### HandlerOptions resolution order

Per call → `DefaultOptions` (handler-level override) → BaseHandler defaults. Higher-precedence options shadow lower. `DefaultOptions` lets a handler set policies that apply to every invocation without forcing every caller to specify them.

### PII redaction — `[RedactData]` is canonical

Every data type carrying PII (emails, phones, IPs, addresses, names, message content, filenames, presigned URLs) MUST have the `[RedactData]` attribute. This:
- Lives on the type / property — not on handlers
- Applies to ALL Serilog logging recursively (not just handler I/O)
- Reflection-cached per type
- Works for `{@obj}` structured logging

When `[RedactData]` can't be applied (proto-generated DTOs that ts-proto / protoc-gen-csharp emit without our attribute), use `DefaultOptions.LogInput=false` / `LogOutput=false` on the handler. Document in the handler's class comment which proto type triggered the suppression.

### 4 OTel metrics every handler emits

Auto-emitted by `BaseHandler`:
1. **Invocation count** — incremented per `HandleAsync` call
2. **Success count** — incremented when result is `success: true`
3. **Failure count** — incremented per failure, labeled by `errorCode`
4. **Duration histogram** — ms from invoke to result

These don't need to be wired by the handler author — `BaseHandler` does it. Adding new handlers gets observability for free.

### Both app AND repo handlers declare PII redaction

A repo handler's input/output is logged independently of its app-layer caller. If the repo handler returns rows containing PII, the repo handler's redaction must cover them. Don't assume the app handler's redaction "trickles down" — each `BaseHandler` is independent.

---

## D2Result

Result objects replace exceptions for control flow. Class hierarchy: `D2Result<T> : D2Result` (NOT a record / discriminated union — preserved for polymorphism in `BubbleFail(D2Result)`).

`Messages` is `IReadOnlyList<TKMessage>` and `InputErrors` is `IReadOnlyList<InputError>` (where `InputError = (string Field, IReadOnlyList<TKMessage> Errors)`). Typing both slots as `TKMessage` (rather than raw strings) makes "every user-visible message is a translation key" structurally enforced — the only way to construct a `TKMessage` is via the SrcGen-emitted `TK.*` constants in `D2.Shared.I18n.Abstractions`. See [i18n](#i18n) below.

`TKMessage` ships over the wire as `{ "key": "...", "params": { ... }? }`; the SvelteKit client translates client-side via Paraglide. Server-side rendering only happens for outbound notifications (Courier emails / SMS) where the recipient locale comes from their profile.

### Factories (use semantic factories — never raw `Fail()`)

Every factory that takes `messages:` accepts an `IReadOnlyList<TKMessage>`. `ValidationFailed` additionally takes `inputErrors:` as `IReadOnlyList<InputError>`. The compiler refuses to build a literal-string array — the caller MUST use the SrcGen-emitted `TK.*` constants. See [i18n](#i18n) below.

Every factory with a `messages?` parameter ships a sensible default — pass nothing for the canonical message, pass your own `TK.*` constants for context-specific wording.

| Factory | Status | ErrorCode | Use case |
|---|---|---|---|
| `Ok<T>(data)` | 200 | (none) | Success with data |
| `Created<T>(data)` | 201 | (none) | Resource created |
| `NotFound(messages?)` | 404 | `NOT_FOUND` | Lookup miss (none of the keys resolved) |
| `Unauthorized(messages?)` | 401 | `UNAUTHORIZED` | Missing / invalid auth |
| `Forbidden(messages?)` | 403 | `FORBIDDEN` | Authenticated but lacks permission |
| `ValidationFailed(messages?, inputErrors?, errorCode?)` | 400 | `VALIDATION_FAILED` (overridable) | Input failed schema / business validation. `inputErrors` is `IReadOnlyList<InputError>`. Override `errorCode` for domain-specific signals (e.g. `"FILES_INVALID_CONTENT_TYPE"`). |
| `Conflict(messages?)` | 409 | `CONFLICT` | DB constraint violation, version conflict |
| `ServiceUnavailable(messages?, errorCode?)` | 503 | `SERVICE_UNAVAILABLE` (overridable) | Downstream dep down. Override `errorCode` for retry-vs-DLQ signals. |
| `UnhandledException(messages?)` | 500 | `UNHANDLED_EXCEPTION` | Top-level safety net only. **Excluded from `IsTransientRetryable`** — unknown system state is never auto-retried. Exception details live in OTel span + log scope, never on the result. |
| `PayloadTooLarge(messages?)` | 413 | `PAYLOAD_TOO_LARGE` | Upload exceeded limit |
| `TooManyRequests(messages?, errorCode?)` | 429 | `RATE_LIMITED` (overridable) | Rate-limit middleware tripped. Override `errorCode` for client-side discrimination (e.g. `"OTP_RATE_LIMITED"`). |
| `Canceled(messages?)` | 400 | `CANCELED` | CancellationToken triggered |
| `SomeFound<T>(data, messages?)` | 206 | `SOME_FOUND` | Partial success on batch lookup. `Success` is **false** — see partial-success ladder below. |

`Fail<T>(statusCode, errorCode, messages?)` exists for re-mapping arbitrary upstream codes (HTTP proxy passthrough). **If a factory matches your case, USE the factory.**

### `InputError` — per-field errors

```csharp
public sealed record InputError(string Field, IReadOnlyList<TKMessage> Errors);
```

`Field` is the form-field name (matches the wire-format key the client uses to attach errors to inputs). `Errors` is one or more `TKMessage`s for that field. Wire format:

```json
{
  "inputErrors": [
    { "field": "email", "errors": [{ "key": "common_validation_EMAIL_INVALID" }] }
  ]
}
```

Self-describing — clients render under each input directly without needing to know positional layouts.

### Partial-Success Ladder

Batch lookups return one of three results:
- **`NotFound`** — none of the keys resolved (Success = false)
- **`SomeFound`** — partial; data carries what was found (Success = false)
- **`Ok`** — all keys resolved (Success = true)

Only `Ok` sets `Success = true`. Callers use `IsPartialOrMissing` (`IsNotFound || IsSomeFound`) for cache-fallback flows — both warrant a downstream lookup, while other failures (Forbidden, etc.) do not.

### Bubble / BubbleFail

Propagate downstream failures without re-wrapping:
- `BubbleFail<T>(downstream)` — current handler failed because downstream failed; preserves status + errorCode + messages + inputErrors + traceId. Sets `Data` to default.
- `Bubble<T>(downstream, data?)` — passes upstream success OR failure through with attached data. Used for `SomeFound` partial-result propagation.

### Per-code booleans + combined helpers

Prefer these over manual `result.ErrorCode == ErrorCodes.X` comparisons:

```csharp
result.IsOk / IsCreated / IsNotFound / IsSomeFound / IsConflict / IsForbidden /
   IsUnauthorized / IsValidationFailed / IsServiceUnavailable / IsRateLimited /
   IsUnhandledException / IsPayloadTooLarge / IsCanceled / IsIdempotencyInFlight

result.IsPartialOrMissing      // IsNotFound || IsSomeFound
result.IsTransientRetryable    // IsServiceUnavailable || IsRateLimited (UnhandledException EXCLUDED)
```

When a factory's `errorCode` is overridden, the corresponding boolean returns false (the comparison is on `ErrorCode`). Domain-overridden codes bypass auto-classification — that's intentional.

### `BubbleOnFailure` — the workhorse guard helper

For multi-value-threading in command + complex handlers — bail early on upstream failure, continue with locals on success:

```csharp
if (orderR.BubbleOnFailure<Order, OutputDto>(out var bubbled, out var order)) return bubbled;
// continue with `order` as a local — strongly typed, no .Data! noise
```

Returns `true` on failure (caller returns `bubbled`); `false` on success (caller continues with `data`). This is the dominant pattern across the codebase.

### Monadic ops — `Bind` / `Map` / `Match`

For genuine linear pipelines where state flows step-to-step (sign-in, file processing, risk scoring):

```csharp
var result = await GetUserAsync(id)
    .BindAsync(user => ValidateConsentAsync(user))
    .BindAsync(user => UpgradeRoleAsync(user))
    .MapAsync(user => user.ToDto());
```

| Operator | Sync / Async | Purpose |
|---|---|---|
| `Bind<TNext>(Func<T, D2Result<TNext>>)` | sync | Chain to next handler that can fail |
| `Map<TNext>(Func<T, TNext>)` | sync | Pure projection that can't fail |
| `Match<R>(Func<T, R>, Func<D2Result<T>, R>)` | sync, terminal | Reduce both branches to one value |
| `BindAsync` (Task + ValueTask) | async | Async equivalent of Bind |
| `MapAsync` (Task + ValueTask) | async | Async equivalent of Map (sync projection) |
| `ThenAsync` (Task + ValueTask) | async, same shape | `BindAsync` sugar when `T == TNext` |

All chaining operators **short-circuit on failure** — the next/projection step is NOT invoked, the upstream failure propagates.

**When to use which tool:**
- **`BubbleOnFailure` + locals**: multi-value threading. Most command + complex handlers, multi-tier query handlers.
- **`Bind` / `Map` / `ThenAsync` / `Match`**: linear pipelines where state accumulates step-by-step.

### Auto-injected `traceId`

`BaseHandler` injects the current `IRequestContext.traceId` into the result object automatically. Handlers don't manually pass `traceId: this.traceId`. The trace ID flows through cross-service responses for end-to-end correlation.

---

## Utilities

`D2.Shared.Utilities` ships these. Use them at every boundary — they prevent a class of subtle bugs. See the lib's [README](../server/shared/dotnet/utilities/README.md) for the full API surface.

### `Truthy()` / `Falsey()` — null-safe predicates

Defined for `string?`, `IEnumerable<T>?`, `Guid`, and `Guid?`. All handle `null` cleanly. **Never** `if (value is null || value.Falsey())` — just `if (value.Falsey())`. After early return on `Falsey`, use `value!` — value is guaranteed non-null. This is one of the few legitimate uses of the null-forgiving operator.

### `ToNullIfEmpty()` — boundary normalizer

`string?` extension that returns `null` if input is null, empty, or whitespace-only (trims first). Use at every boundary where external strings enter the domain (proto, DB rows, request bodies). Prevents empty strings from polluting domain models.

### `CleanStr()` / `CleanDisplayStr()` — normalize-or-null

`CleanStr()` trims and collapses internal whitespace runs into a single space; returns `null` when empty afterward. `CleanDisplayStr()` additionally strips characters not allowed in display names (HTML tags, brackets, quotes, backticks, etc.) — preserves any Unicode-letter script + digits + spaces + `-` `'` `.` `,`.

### `TryParseEmail()` / `TryParsePhoneNumber()` — `D2Result<string>`-returning validators

`string?` extensions that return `D2Result<string>` with TK-keyed messages on failure. Compose naturally with the smart-constructor pattern in domain layers — chain via `Bind` / `BubbleFail` instead of try/catch. Worked example in [Domain Validation](#domain-validation--smart-constructor-pattern) below.

Failure carries `TK.Common.Validation.EMAIL_INVALID` / `TK.Common.Validation.PHONE_INVALID` keys (translated client-side). Phone validation strips non-digits and enforces the 7-15 length envelope (E.164-compatible).

### `[RedactData]` attribute

Marker attribute on PII / secret types/properties. Reflectively consumed by the Serilog destructuring policy in `D2.Shared.ServiceDefaults` to redact from logs / spans / metrics. Reasons: `PersonalInformation`, `FinancialInformation`, `SecretInformation`, `VerboseContent`, `Other`, `Unspecified`.

### `SerializerOptions` — frozen `JsonSerializerOptions` presets

`SR_IgnoreCycles`, `SR_Web` (camelCase + string enums), `SR_WebIgnoreNull` (same + omit nulls). Thread-safe and reused per call — share them across the process, never construct ad-hoc.

### `ConnectionStringHelper` — URI ↔ wire-format

Standard `REDIS_URL=redis://...`, `*_DATABASE_URL=postgresql://...`, `RABBITMQ_URL=amqp://...` env vars get parsed into the wire format expected by .NET clients (`StackExchange.Redis`, `Npgsql`). RabbitMQ accepts AMQP URIs natively. Pass-through for already-converted values.

### `D2Env.Load(params string[] fileNames)` — `.env*` loader for host scenarios

Default file list: `[".env", ".env.local", ".env.secrets"]`. Walks up from `AppContext.BaseDirectory` (max 12 levels) and finds the FIRST directory containing AT LEAST ONE of the named files; loads ALL matching files from THAT directory only (never mixes files across ancestor directories). Process env wins over every file; later files in the list override earlier files (matches Docker Compose's `--env-file` ordering). No-op inside Compose containers (env already set by the time `Load()` runs).

---

## Resilience

`D2.Shared.Resilience` ships these. See the lib's [README](../server/shared/dotnet/resilience/README.md) for the full state-machine documentation.

### `CircuitBreaker<T>` — three-state lock-free breaker

`Closed` (normal — failures tracked) → `Open` (fast-fail past `FailureThreshold`) → `HalfOpen` (one probe allowed past `CooldownDuration`). State transitions via `Interlocked.CompareExchange` — no locks. The `isFailure` predicate counts value-failures alongside thrown exceptions (e.g., `r => !r.Success`). Open state with a fallback returns the fallback; without one, throws `CircuitOpenException`. Probe-in-flight flag ensures only ONE caller probes at a time during HalfOpen; concurrent callers receive the fallback.

`CircuitBreakerOptions` is the canonical **small-Options-record**: nullable-param ctor + parameterless ctor that chains to defaults. Call sites stay terse — `new()`, `new(3)`, `new(3, TimeSpan.FromMilliseconds(100), clock.Now)`, `new(failureThreshold: 3, nowFunc: clock.Now)` all work. Explicit `0` / `TimeSpan.Zero` are preserved (no sentinel coercion).

### `Singleflight<TKey, TValue>` — concurrent-call deduplication

Type-safe per-key + per-value-shape concurrent-call deduplication. The first caller for a given key runs the operation; concurrent callers share the same `Task<TValue>`. Once the operation completes (success OR failure), the key is removed — **NOT a cache.** Per-caller cancellation only cancels that caller's wait; the shared operation runs with `CancellationToken.None` so siblings are isolated. Used heavily in cache-fill paths to prevent thundering-herd backend hits.

### `RetryHelper.RetryAsync<T>` — exponential backoff with jitter

Generic retrier — wraps any throwing async call with exponential backoff + optional jitter + transient-error predicate + configurable `DelayFunc` (test seam). The default classifier flags `HttpRequestException` (5xx / 429 / 408), `TaskCanceledException`, `TimeoutException`, `SocketException`. Defaults: 5 attempts, 1s base, ×2 backoff, 30s ceiling, full jitter. `OperationCanceledException` from the supplied `ct` is re-raised as cancellation, NEVER classified transient.

### `RetryHelper.RetryD2ResultAsync<TData>` — `D2Result`-aware overload

When the operation returns a `D2Result<TData>`, the default `ShouldRetry` becomes `r => r.Failed && r.IsTransientRetryable` (retries `ServiceUnavailable` and `RateLimited`; deliberately does NOT retry `UnhandledException` — unknown system state must never be auto-retried because side effects may have committed). Caller-supplied `ShouldRetry` always wins.

### Composing — `ResilientPipeline<TKey, TValue>` (the canonical surface)

For combining two or three of the primitives behind ONE call site, use `D2.Shared.Resilience.Pipeline.ResilientPipeline<TKey, TValue>` rather than nesting the raw primitives. It returns `D2Result<TValue>` (never throws) and converts CircuitOpen / cancellation / transient / unknown exceptions to the appropriate result code.

Two-tier API:

- **Lib composition root** uses the fluent DSL — `services.AddResilientPipeline<TKey, TValue>(p => p.UseSingleflight().UseCircuitBreaker().UseRetries(opts));`
- **Handler** injects `ResilientPipeline<TKey, TValue>` and calls `pipeline.ExecuteAsync(key, op, ct)` — one line, returns `D2Result`.

**Layer order = protection semantic.** `UseCircuitBreaker()` BEFORE `UseRetries()` means retry-INSIDE-CB (upstream-protecting; backoff between attempts gives a fragile upstream air). `UseRetries()` BEFORE `UseCircuitBreaker()` means retry-OUTSIDE-CB (restart-recovery; the retry layer treats `CircuitOpenException` as transient and backs off through it, so a breaker that cools down mid-retry lets the next attempt succeed). The retry-outside composition trades caller-side latency for resilience to upstream restarts; size `MaxAttempts + backoff` to span `CooldownDuration` or retries exhaust on perpetual CO. Full discussion → [resilience/README.md § Pipeline](../server/shared/dotnet/resilience/README.md).

Reach for the raw primitives directly only when you need behavior the pipeline doesn't offer (custom fallback delegates, observation hooks, etc.).

---

## Repository

EF Core for all relational data.

### Batch chunking — PG ~32K parameter limit

PG has a hard limit of ~32K parameters per query (signed-int parameter index). At ~5 columns per row, that's ~6500 rows max per batch. Default chunk size is **500** — comfortable margin, keeps statement plans cacheable.

`input.HashIds.Chunk(_BATCH_SIZE)` for any batch lookup / update. Configure `_BATCH_SIZE` via the Options pattern, never hardcode.

### Partial success → D2Result mapping

Batch ops return:
- All keys resolved → `Ok`
- Some keys resolved → `SomeFound` (data + missing keys)
- No keys resolved → `NotFound`

Don't return `Ok` with empty data when nothing matched — that's a `NotFound`.

### DB-failure mapping — `BaseRepoHandler` does this for you

Repo handlers do **not** catch SQLSTATE strings or `PostgresException` directly. Inherit from `BaseRepoHandler` (see [BaseRepoHandler](#baserepohandler--typed-db-failure-mapping-provider-agnostic) above) and the registered `IDbExceptionClassifier` translates DB exceptions into typed `D2Result` failures (`UniqueViolation`, `ForeignKeyViolation`, `NotNullViolation`, `CheckViolation`, `ConcurrencyConflict`, `DbDeadlock`, `DbTimeout`, `DbConnectionFailure`).

Callers branch on the typed booleans (`result.IsUniqueViolation`, `result.IsConcurrencyConflict`, `result.IsTransientDbFailure`, etc.) — never on raw SQLSTATE catches. **Never let constraint violations bubble as `500 UnhandledException`** — that's a missing `BaseRepoHandler` inheritance or a missing `services.AddD2Postgres()` registration.

### EF Core UPDATE/DELETE check affected rows

`SaveChangesAsync()` returns the number of rows affected. If zero where you expected ≥1, return `NotFound` — the row didn't exist. Don't return `Ok` on a no-op update.

### Migrations — generator only

`dotnet ef migrations add <Name>` is the only path. **Never** hand-edit `*.cs` migration files, `*ModelSnapshot.cs`, or `__EFMigrationsHistory` rows. EF Core's internal model snapshot will desync silently and break all subsequent migrations.

If the generator fails, **STOP and ask** — don't patch by hand. Multi-replica safety: startup migrator acquires PG advisory lock so only one replica migrates at a time; others wait.

---

## Cache

The cache stack is fronted by `D2.Shared.Caching.Abstractions`. **Four building-block interfaces** — `ICacheBasic` (Get/Set/Remove + bulk variants + Exists/GetTtl), `ICacheAtomic` (SetNx, Increment, Acquire/ReleaseLock), `ICacheBroadcast` (`*AndBroadcast*` write variants that publish to a backplane), `ICacheSet` (SADD/SCARD/SREM/SISMEMBER, cluster-only) — are composed by **three marker interfaces** that callers actually inject:

| Marker | Composes | Scope | Use for |
|---|---|---|---|
| `ILocalCache` | Basic + Atomic | Per-process. Atomic ops at process scope. | Instance-scoped caches: per-instance fingerprint cache, hot in-process lookups, single-writer counters. |
| `IDistributedCache` | Basic + Atomic + Broadcast + Set | Cluster. Every read hits the remote store. | Rate-limit counters, distributed locks, ephemeral session lookups, FP-too-common detection (the one Set primitive consumer). |
| `ITieredCache` | Basic + Atomic + Broadcast | Composed L1 + L2. | Read-heavy entity data where freshness within a few seconds is acceptable. |

`IDistributedCache` and `ITieredCache` are method-for-method identical (the only surface difference is `ICacheSet`, which tiered deliberately omits — set cardinality is inherently cluster-only and tiered composition would silently hide that). The marker name carries behavioral intent at the dependency site so the reader knows the scope without checking registration.

**Every op returns `D2Result<T>` / `D2Result`**. Null/empty inputs return `D2Result.ValidationFailed` with an `InputError` naming the offending parameter — implementations never throw `ArgumentException` for caller mistakes.

### Default implementations

- **`DefaultLocalCache : ILocalCache`** (`caching-local-default`) — wraps `Microsoft.Extensions.Caching.Memory.IMemoryCache` (ConcurrentDictionary-backed, lock-free reads). Direct method dispatch — no `BaseHandler` wrapping (per-call handler overhead would be 100× the ~60ns cache work). Always sets `entry.Size = 1` so `MaxEntries` enforces a real entry-count cap (mitigates the IMemoryCache SizeLimit footgun where unset Size means unbounded growth). Static `Meter` for hits/misses/sets/removes/evictions.
- **`RedisDistributedCache : IDistributedCache`** (`caching-distributed-redis`) — over `StackExchange.Redis`. Compound atomic ops (Increment+TTL, ReleaseLock compare-and-delete, SADD+TTL on first-add) use Lua scripts so each op is a single round-trip. WRONGTYPE / "value is not an integer" both map to `D2Result.Conflict`. Pluggable `ICacheSerializer` (default `JsonCacheSerializer` — `System.Text.Json`, dev-friendly because Redis CLI can inspect values directly).
- **`RedisCacheInvalidationBackplane : ICacheInvalidationBackplane`** — Redis pub/sub. Universal "everyone acts" rule: every subscriber receives every message, including the publisher's own. No sender-ID filter; the cost of self-receive is bounded (re-fetch from L2 on next read). `Subscribe(Func<string, CancellationToken, ValueTask>) → IAsyncDisposable` — explicit lifetime tracking, not `event +=`.
- **`DefaultTieredCache : ITieredCache, IAsyncDisposable`** (`caching-tiered`) — composes one `ILocalCache` (L1) + one `IDistributedCache` (L2) via DI. **Reads**: try L1 → on miss fall through to L2 → populate L1 from L2 hit. **Writes**: L2 first, then L1 only if L2 succeeded (no partial-write states; nothing to roll back). **Atomic ops**: route through L2 (the cluster source of truth) with L1 invalidation as side effect. **`*AndBroadcast*` writes**: publish to the registered backplane after the underlying op succeeds. Subscribes to the backplane in its constructor for cluster-wide L1 coherency.

### Picking a marker

- **Need to share state across instances?** → `IDistributedCache` (every read fresh from cluster) or `ITieredCache` (L1-cached but kept coherent via backplane).
- **Need atomic at cluster scope?** → `IDistributedCache` (or `ITieredCache`, which delegates atomics to L2).
- **Need set primitives (SADD/SCARD)?** → `IDistributedCache` only. Tiered does not compose `ICacheSet` because cardinality is meaningful only at cluster scope.
- **Per-instance ephemeral data?** → `ILocalCache`. No backplane involvement, no remote round-trip.

---

## Composition root

Every D² service composes its host through one ordered call to `D2.Shared.ServiceDefaults` rather than re-implementing the full Serilog + OTel + middleware + DI plumbing in every `Program.cs`.

### `AddD2ServiceDefaults` + `UseD2DefaultPipeline` + `MapD2DefaultEndpoints` + `RunD2ServiceAsync`

```csharp
using D2.Shared.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddD2ServiceDefaults(
    builder.Configuration,
    opts =>
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

// service-specific endpoints + Map* calls here

await app.RunD2ServiceAsync("files");
```

`AddD2ServiceDefaults` chains `D2Env.Load` → `AddD2Logging` → `AddD2Telemetry` → `AddD2I18n` → `AddD2Handler` → `AddD2Auth` (+ `.Http` + `.Grpc`) → `AddD2LocalCache` → `AddD2HealthChecks` → `AddD2ProblemDetails` → `AddD2Cors` → `ConfigureHttpClientDefaults(http => http.AddStandardResilienceHandler())` in that order. Each step delegates entirely to its owning lib — see [Logging](#logging), [Telemetry](#telemetry), [AspNetCore](#aspnetcore), [i18n](#i18n), [Handler](#handler), [Middleware](#middleware) (Auth subsection), and [Cache](#cache) for what each step wires.

### Pure thin aggregator — ZERO logic

The aggregator csproj owns no `try / catch`, no string literals beyond the per-lib option-name constants it forwards, no `[LoggerMessage]`, no `Activity.SetTag`, no handlers — purely composition + delegation. A convention test pins this: the aggregator assembly contains zero non-static public classes other than the options record. Adding logic to the aggregator instead of to the owning lib is the failure mode this constraint exists to prevent.

The architectural payoff: new options on any owning lib show up at the aggregator's call site automatically through pass-through `Action<TFromOwningLib>?` delegates (`LoggingConfigure`, `TelemetryConfigure`, `AuthConfigure`, etc.) with no aggregator-side maintenance — the aggregator owns ZERO field-level configuration knowledge.

### `UseD2DefaultPipeline` middleware order is LOCKED

```csharp
app.UseD2SecurityHeaders();
app.UseD2RequestLogging();
app.UseD2Cors();
app.UseRouting();
app.UseD2InfrastructureBypass();
app.UseAuthentication();
app.UseD2Auth();
app.UseAuthorization();
```

No insertion points exposed. Services that need bespoke ordering call the underlying lib extensions themselves and skip the aggregator's `UseD2DefaultPipeline` entirely. The order rationale (per [RATE-LIMITING.md](RATE-LIMITING.md) §4):

- **Security headers first** — OWASP headers apply to EVERY response, including responses produced by middleware that short-circuits the pipeline (CORS preflight, infrastructure bypass).
- **Request logging early (before routing)** — even early-pipeline failures emit a structured request-completion line.
- **CORS after RequestLogging, before Routing** — CORS preflight (`OPTIONS`) must short-circuit before routing tries to match a verb-specific endpoint.
- **Routing then InfrastructureBypass** — bypass needs the routing-resolved endpoint on the context to invoke the matched `RequestDelegate` directly when short-circuiting.
- **Authentication → Auth → Authorization** — JWT auth middleware (`UseD2Auth`) requires the AspNetCore authentication feature on the context (`UseAuthentication`) and runs BEFORE `UseAuthorization` so the authorization stage fires scope / policy gates against the populated `IRequestContext`.

The rate-limit middleware (an Edge-only concern, owned outside the aggregator) slots BETWEEN `UseD2InfrastructureBypass` and `UseAuthentication`. Edge composes the same canonical D² stack but inserts its own `UseD2RateLimit` at that exact slot.

### Auth wiring is fail-fast

`AuthConfigure` MUST be non-null when `SkipAuthAutoWiring = false` (the default) — the aggregator throws `InvalidOperationException` at host build with a clear remediation message otherwise. Setting `SkipAuthAutoWiring = true` opts out of auth wiring entirely (test hosts, anonymous-only admin endpoints). The fail-fast prevents services from accidentally shipping without auth wiring; the explicit opt-out is the only way to bypass.

### Opt-out matrix

| `D2ServiceDefaultsOptions` flag | When `true`, the aggregator does NOT call... |
|---|---|
| `SkipAuthAutoWiring` | `AddD2Auth` / `AddD2AuthHttp` / `AddD2AuthGrpc` |
| `SkipLocalCacheAutoWiring` | `AddD2LocalCache` |
| `SkipHttpClientResilienceDefaults` | `ConfigureHttpClientDefaults(http => http.AddStandardResilienceHandler())` |

Defaults are all `false`. The opt-out flag set is intentionally narrow — only components where the >95% case wants auto-wire AND a small set of services (test hosts, dry-run admin tools) need to opt out. Components without an opt-out flag (Logging, Telemetry, I18n, Handler, HealthChecks, ProblemDetails, Cors) are wired unconditionally because every D² service needs them.

### 5-layer rename safety net (spec-driven codegen + nameof discipline)

The combination of spec-driven codegen + the `nameof()`-over-literals discipline (forbid raw string literals when emitting codegen'd member names — Serilog property keys, OTel span tag keys, JSON field names mirroring an interface property, telemetry counter tag keys; use `nameof(SourceOfTruthType.Member)` instead) gives a 5-layer safety net against silent drift when a property is renamed in its source-of-truth spec:

1. **Spec change** triggers auto-regeneration of `.g.cs` files — the new property name appears in the generated interface, the old name disappears.
2. **Consumer compile-fail** — any consumer that referenced the old member name directly (`ctx.OldName`) breaks at the call site.
3. **Production emission compile-fail** — any production code emitting the property as a structured-log property / span tag / wire-format key via `nameof(IRequestContext.OldName)` breaks at the `nameof` binding.
4. **Behavioral test compile-fail** — any test that asserted against the property name via `nameof(IRequestContext.OldName)` breaks the same way.
5. **Spec-pin literal test-fail** — for the cases where ops queries the wire shape by literal string (`service="D2.Shared.Auth"` in Loki, `IRequestContext.RequestId` in a Tempo span tag), a separate spec-pin test asserts the literal value of every emitted symbol. A rename without a corresponding spec-pin update fails the test, forcing explicit acknowledgment of the wire-format change.

Layers 1-4 catch unintended drift at compile time. Layer 5 catches intended renames that weren't propagated to operator dashboards / saved queries / alert rules. Every codegen'd member that ships across an operator-visible boundary (logs, traces, metrics, wire format) earns this 5-layer treatment.

---

## Logging

`D2.Shared.Logging` ships Serilog configuration + `[RedactData]` enforcement + an ASP.NET Core request-logging middleware. See the lib's [README](../server/shared/dotnet/logging/README.md) for the full surface (full LOG-OK / NOT-LOGGED enumeration, per-source overrides, precedence notes).

### `AddD2Logging` — Serilog config + `RedactDataDestructuringPolicy` enforcement

Builds the Serilog `LoggerConfiguration`, sets `Log.Logger`, wires the MEL bridge with `AddSerilog(preserveStaticLogger: true, writeToProviders: true)` so OTel log-exporter providers (registered separately by `D2.Shared.Telemetry`) flow alongside the in-process Console sink. Validates options at host build via `ValidateOnStart` (fail-fast).

Per-source minimum-level overrides applied on top of the configured `MinimumLevel`: `Microsoft.AspNetCore` / `Microsoft.Extensions.Http` / `System.Net.Http` → `Warning` (suppresses framework / HTTP-client noise that Information-level produces by default); `D2` → `Debug` (keeps D² code's diagnostic logs visible).

### `[RedactData]` is the canonical PII redaction mechanism

The `RedactDataDestructuringPolicy` reflects over `BindingFlags.Public | Instance` PROPERTIES (not fields) on every `@`-captured object across every `ILogger.Log*` call across every service that wires `AddD2Logging`. Three rules to know:

1. **Capture mode matters.** Serilog's `@`-prefix invokes destructuring (the policy fires). The default `{prop}` capture (without `@`) calls `.ToString()` and bypasses destructuring entirely. `[RedactData]` on a record + `logger.Information("user {User}", user)` (no `@`) leaks the PII the attribute was supposed to redact.
2. **Field-level `[RedactData]` is silently ignored.** Use property syntax for redaction.
3. **Computed properties leak through their inputs.** Redact the computed property too if its inputs are PII.

| Decoration | Effect |
|---|---|
| `[RedactData]` on the type | Entire value rendered as `[REDACTED: {Reason}]`. |
| `[RedactData]` on a property | Only that property is masked; siblings destructure normally. |
| `[RedactData(CustomReason = "...")]` | The custom string replaces the enum-name reason in the placeholder. |

Reason rendering uses the enum name (`PersonalInformation`, `FinancialInformation`, `SecretInformation`, `VerboseContent`, `Other`, `Unspecified`) unless a `CustomReason` overrides it.

### `UseD2RequestLogging` — request-completion middleware

Wraps Serilog's `UseSerilogRequestLogging` with D² defaults: infrastructure-path suppression (request-completion events for `/health`, `/alive`, `/metrics`, `/.well-known` emit at `Verbose` so the default minimum-level gate filters them out — driven by `InfrastructurePathMatcher` from `D2.Shared.AspNetCore` so the same path set is used by Logging + Telemetry + the bypass middleware), conservative diagnostic-context enrichment, and projection of the spec-driven `IRequestContext` LOG-OK fields onto the request-completion event via `D2RequestContextEnricher`.

### Network/IP enrichment — what is NOT logged

The middleware does NOT log the request's connection-remote IP address (`HttpContext.Connection.RemoteIpAddress`). Two reasons:

- **At internal services**, that address is the upstream Edge instance's IP, not the original client's. Operators reading logs would interpret it as the user's IP, which it isn't — a data-shape footgun.
- **At Edge**, the resolved client IP is PII. Logging it as a structured-context field would bypass the `[RedactData]` destructuring policy entirely (diagnostic-context `Set` writes a `ScalarValue` directly).

The 8 explicitly-suppressed NOT-LOGGED fields (`ClientIp`, `City`, `Region`, `SubdivisionCode`, `PostalCode`, `Latitude`, `Longitude`, `Geohash`) are pinned by integration test that enumerates each one and asserts NONE appear in the rendered JSON output. Adding a new PII field to the spec without a coverage update is a contract failure that surfaces at test time.

### Caller-added structured fields BYPASS `[RedactData]`

Callers that chain into `RequestLoggingOptions.EnrichDiagnosticContext` to add their own structured fields MUST own PII discipline for anything they add — the diagnostic-context path emits `ScalarValue`s directly, the destructuring policy never sees them. If the data is PII, log a `[RedactData]`-decorated wrapper object using the destructuring capture mode (`{@MyObj}`) inside a `LoggerScope` instead.

### `LOG-OK` / `NOT-LOGGED` field contract

The full 42 LOG-OK / 8 NOT-LOGGED enumeration with per-field PII rationale + precedence notes (where `TraceId` overrides the middleware's local value but `RequestId` and `RequestPath` lose to Serilog's pre-binding on the HTTP path) lives in [logging/README.md](../server/shared/dotnet/logging/README.md). When adding a new spec-driven `IRequestContext` field, add the field to the LOG-OK / NOT-LOGGED list AND update the integration-test contract in the same change.

---

## Telemetry

`D2.Shared.Telemetry` ships OpenTelemetry SDK setup (traces + metrics + logs) + per-signal OTLP exporters + an IP-restricted Prometheus scraping endpoint + cross-lib `ActivitySource` / `Meter` aggregation. See the lib's [README](../server/shared/dotnet/telemetry/README.md) for the full options + per-instrumentation enumeration.

### `AddD2Telemetry` — OTel SDK + auto-instrumentations + cross-lib aggregation

Registers the OpenTelemetry SDK builder, the per-signal OTLP exporters whose corresponding endpoint env vars (`OTEL_EXPORTER_OTLP_TRACES_ENDPOINT` / `OTEL_EXPORTER_OTLP_METRICS_ENDPOINT` / `OTEL_EXPORTER_OTLP_LOGS_ENDPOINT`) are truthy, the in-process Prometheus exporter (when `EnablePrometheusExporter` is true, default `true`), and the standard auto-instrumentations:

| Instrumentation | Default | Notes |
|---|---|---|
| AspNetCore inbound | enabled | `Filter` callback excludes infrastructure paths via the canonical `InfrastructurePathMatcher` from `D2.Shared.AspNetCore`. `RecordException = true` captures exception type + stack trace as span events (no PII — diagnostic-class data). |
| HttpClient outbound | enabled | `FilterHttpRequestMessage` callback suppresses spans for outbound calls whose URI starts with any configured OTLP endpoint (prevents infinite-loop instrumentation against the OTel exporter itself). `EnrichWithHttpRequestMessage` re-sets `url.full` to scheme + host + port + path (no query string) as defense-in-depth against future SDK regressions on query-string PII. |
| GrpcNetClient outbound | enabled | Standard span tags. |
| Process metrics | enabled | CPU, memory, fd count. |
| Runtime metrics | enabled | GC, threadpool, JIT. |
| Prometheus exporter | enabled | In-process scraping endpoint mapped via `MapD2PrometheusEndpoint`. |

### `OTEL_SDK_DISABLED` — symmetric short-circuit

When the canonical OpenTelemetry env var `OTEL_SDK_DISABLED` is set to `"true"` (case-insensitive), BOTH `AddD2Telemetry` and `MapD2PrometheusEndpoint` short-circuit:

- `AddD2Telemetry` returns the unmutated `services` collection — no providers / exporters / instrumentations registered.
- `MapD2PrometheusEndpoint` returns the unmutated `endpoints` builder — no `/metrics` route mapped.

Used in E2E tests where the OTel SDK is undesired (the SDK's per-process singleton state otherwise contaminates tests that build multiple hosts in sequence). Consumers MUST tolerate the absence of both surfaces under the kill-switch condition — code that resolves `MeterProvider` / `TracerProvider` from DI MUST tolerate absence.

### `MapD2PrometheusEndpoint` — IP-restricted scrape endpoint

Maps `/metrics` via OpenTelemetry's `MapPrometheusScrapingEndpoint`, with an endpoint filter that returns `403 Forbidden` for requests whose connection-remote IP is neither loopback nor RFC 1918 private. The IP restriction defends against accidental public exposure of the metrics surface (cardinality / hostname / version disclosure) when a service is mistakenly exposed without an upstream reverse proxy filter.

### Cross-lib `ActivitySource` / `Meter` aggregation — single `AddD2Telemetry()` call

Every shared lib that publishes its `ActivitySource` / `Meter` name through a `public const string` symbol gets aggregated into the SDK builder via `AddD2Telemetry` so a single call wires the entire shared-lib telemetry surface into the configured exporters. The aggregation table currently covers 4 cross-lib `ActivitySource`s (`Handler`, `Auth`, `Auth.Outbound`, `Messaging.RabbitMq`) + 6 cross-lib `Meter`s (the same 4 + `Caching.Distributed.Redis` + `Caching.Local.Default`).

Each cross-lib reference is a `public const string` symbol (NOT a literal string) so a rename in any owning lib surfaces as a build break in `D2.Shared.Telemetry` at compile time. Per the [5-layer rename safety net](#5-layer-rename-safety-net-spec-driven-codegen--nameof-discipline) above, spec-pin tests additionally pin the literal wire values so an in-place value change without a symbol rename surfaces as a test failure — operators querying Loki / Tempo / Prometheus by literal source name (`service="D2.Shared.Auth"`) are protected from silent wire-format drift.

### Cooperation with `D2.Shared.Logging` — independent at compile time, MEL bridge at runtime

The two libs are intentionally independent at the csproj level — neither references the other. They cooperate at runtime via the MEL bridge: `AddD2Logging` sets `writeToProviders: true` on the Serilog → MEL bridge, and the OTLP log-exporter `ILoggerProvider` registered by `AddD2Telemetry` receives the same log events that Serilog routes to its console sink. Hosts may wire one without the other.

---

## AspNetCore

`D2.Shared.AspNetCore` ships cross-cutting AspNetCore middleware + endpoint primitives every D² service composition root needs but that don't belong on a single domain lib. See the lib's [README](../server/shared/dotnet/aspnetcore/README.md) for the full surface.

### Six public extensions

| Extension | What it does |
|---|---|
| `UseD2SecurityHeaders` | Writes an OWASP-aligned default header set on every response via `HttpResponse.OnStarting`. HSTS only on HTTPS (the spec forbids HSTS on HTTP). HSTS preload submission is intentionally NOT included by default — preload is a one-way door (once the apex domain is in the browser-built-in preload list, removal is slow and incomplete). Each service that wants preload submission opts in by setting `StrictTransportSecurity` to a value that includes `preload`. |
| `AddD2Cors` + `UseD2Cors` | Registers the `D2_DEFAULT` CORS policy reading the canonical indexed env-var convention `D2_CORS_ORIGINS__0`, `D2_CORS_ORIGINS__1`, ... per .NET `IConfiguration` array binding. Validates fail-closed at host build via `ValidateOnStart()` — empty origins list is a deliberate decision, not an oversight. The `AllowCredentials = true` + `Origins = ["*"]` combination is forbidden per CORS spec; the validator rejects it at host build. |
| `UseD2InfrastructureBypass` | For each request, sets `HttpContext.Items["D2.IsInfrastructure"]` to a boolean indicating whether the request path matches the configured infrastructure-path list (default: `/health`, `/alive`, `/metrics`, `/.well-known`). Default short-circuit mode (`TagOnly = false`) invokes the matched endpoint's `RequestDelegate` directly when the path matches, bypassing every middleware registered AFTER this one — heavy business middleware (rate limiting, idempotency, auth) does NOT execute on cheap probe / metrics / well-known requests. |
| `AddD2ProblemDetails` | RFC 7807 customizer that sets `extensions["traceId"]` from `Activity.Current?.TraceId.ToString()` falling back to `HttpContext.TraceIdentifier`; reads `extensions["correlationId"]` from the configured request header (`X-Correlation-Id` by default; capped at 128 chars; values exceeding the cap are treated as absent and a fresh GUID is generated); sets the RFC 7807 `instance` field to `{Method} {Path}` when `IncludeRequestPath` is true. PII discipline: NEVER reads `HttpContext.Request.QueryString`, `Request.Body`, or any user-input source. |
| `AddD2HealthChecks` + `MapD2HealthEndpoints` | `AddD2HealthChecks` registers a baseline `"self"` check tagged `"live"` that always returns `Healthy`; idempotent. Per-service infrastructure layers add their own checks (DB, Redis, RabbitMQ, KeyCustodian) by chaining `services.AddHealthChecks().AddDbContextCheck<...>()` etc. `MapD2HealthEndpoints` maps `/health` (full) + `/alive` (only `"live"`-tagged checks — the kubernetes-conventional liveness split). |
| `RunD2ServiceAsync` | Async wrapper around `WebApplication.RunAsync()` that logs `Log.Information("Starting {ServiceName} ({EnvironmentName})")` at entry, on exception logs `Log.Fatal` with PII-safe exception rendering — type FullName + first stack frame only, NEVER `ex.Message`, since exception messages at host startup can carry connection strings + configured secrets + host-environment specifics. In `finally`: awaits `Log.CloseAndFlushAsync()` to drain Serilog's buffered batch sink before process exit. |

### `InfrastructurePathMatcher` — single source of truth

The public static `InfrastructurePathMatcher` is the canonical infrastructure-path matcher across the D² shared-lib stack. Consumed by:

- `D2.Shared.Logging.WebApplicationLoggingExtensions.UseD2RequestLogging` to down-rank request-completion log lines for infrastructure endpoints to `Verbose`
- `D2.Shared.Telemetry.TelemetryServiceCollectionExtensions.AddD2Telemetry` in the AspNetCore-instrumentation `Filter` callback to suppress auto-spans
- `UseD2InfrastructureBypass` (this lib)

Uses the AspNetCore-canonical `PathString.StartsWithSegments(PathString)` overload — case-insensitive, segment-boundary-matched (`/healthz` does NOT match prefix `/health`; `/health/db` does). Empty `PathString`, null configured list, and per-entry null / empty / whitespace prefixes are all defensive no-ops (returns `false` rather than throwing).

The path set (`/health`, `/alive`, `/metrics`, `/.well-known`) stays aligned across the three consumers without per-lib literal duplication. Earlier per-lib `internal` duplicates were collapsed into this canonical public matcher in the same change that introduced `D2.Shared.AspNetCore` so all consumers stay aligned on the path set.

---

## Middleware

### Idempotency — SET NX + sentinel pattern

- Storage: Redis `SET NX` with 24h TTL — shared across all Edge instances
- First request with a given `Idempotency-Key` writes a **sentinel** (`{ status: "in-flight" }`) with a short TTL (default 30s), processes the handler, then writes the **cached response** with the full 24h TTL
- Concurrent duplicate request: SET NX fails → reads existing value → if sentinel, polls briefly + retries; if cached response, returns it immediately
- Cached response shape: `{ statusCode, body, contentType }` — only what's needed for replay
- Fail-open: if Redis is unreachable, request passes through (availability > strictness)

### RateLimit — multi-dimensional sliding window

4 dimensions, hierarchically ordered (most specific first):

| Dimension | Default cap | Why |
|---|---|---|
| Client fingerprint | 100/min | Per-device cap |
| IP | 5,000/min | Per-source-IP cap |
| City | 25,000/min | Per-city aggregate (NAT/proxy ranges) |
| Country | 100,000/min | Per-country aggregate (CDN ranges) |

If ANY dimension exceeds, block the request for 5min.

**Algorithm: sliding window approximation** — two fixed-window counters per dimension + weighted average of (current + previous) windows based on elapsed time within the current window. **No Lua scripts needed** — pure Redis `INCR` + `TTL`, atomic, cross-instance safe.

Fail-open + service-identity bypass: same as Idempotency.

### RequestEnrichment

IP resolution priority:
1. `CF-Connecting-IP` (Cloudflare)
2. `X-Real-IP` (proxy)
3. `X-Forwarded-For` (last entry — leftmost is client)
4. `Context.Connection.RemoteIpAddress` (direct connection)

Fingerprints (composite — server-side + client-side components combined):
- Server fingerprint = `SHA256(UA | Accept-Language | Accept-Encoding | Accept)`
- Device fingerprint = `SHA256(client-side hash | server-side hash)`

Populates `MutableRequestContext` progressively as middleware layers run. Infrastructure paths (health, metrics, observability scrape endpoints) bypass enrichment via shared `InfrastructurePaths.IsInfrastructure()`.

### JwtAuth — RS256 + JWKS-based, transport-binding split

Inbound JWT auth ships as three sibling csprojs:

- **`D2.Shared.Auth`** — runtime: `JwtValidator` (signature + standard-claim checks), `HttpJwksProvider` (JWKS fetch + Singleflight + cooldown + circuit-breaker), `TieredCacheSessionLivenessTracker` (sentinel-only liveness), telemetry, failure helpers (`AuthFailures.Jwt*` returning `D2Result`). Pure handler-shaped logic — no transport coupling. One DI extension: `services.AddD2Auth(opts => { opts.Issuer = ...; opts.Audience = ...; })`.
- **`D2.Shared.Auth.Http`** — HTTP transport binding: `JwtAuthMiddleware`, RFC 7807 `ProblemDetails` shape with `d2_error_code` extension, `RequireD2Scope("…")` fluent metadata + `[D2RequireScope("…")]` attribute, harmless-endpoint opt-out via `MarkAsD2HarmlessEndpoint()`. Wired with `services.AddD2AuthHttp()` + `app.UseD2Auth()`.
- **`D2.Shared.Auth.Grpc`** — gRPC transport binding: `JwtAuthInterceptor`, `RpcException(Status, Trailers)` shape with `d2_error_code` / `d2_messages` / `traceid` trailers, `[D2RequireScope("…")]` method attributes, `[D2HarmlessEndpoint]` opt-out. Wired with `services.AddD2AuthGrpc()`.

Per-validation pipeline (order is important for what happens at WHICH layer):

1. **Bearer extraction** — transport-binding layer (HTTP middleware reads `Authorization: Bearer …`; gRPC interceptor reads the `authorization` metadata entry).
2. **Signature + standard-claim validation** — runtime (`JwtValidator`): RS256 (algorithm pinned via `TokenValidationParameters.ValidAlgorithms` — defends against `alg=none` + HMAC confusion); issuer; audience; lifetime (`exp`/`nbf`) with default 30s clock skew; `iss`/`aud`/`exp` MUST be present (`RequireExpirationTime = true`). Reactive-refresh-on-unknown-`kid`: one cooldown-gated JWKS refresh + one retry — persistent miss surfaces as `AUTH_JWT_KID_NOT_FOUND` (401, not 503 — the JWT itself is suspect, not the upstream JWKS). RFC 8693 §2.1 `act` chain malformation surfaces as `AUTH_JWT_ACT_CHAIN_MALFORMED`.
3. **Session liveness** — transport-binding layer, AFTER validator returns success. Sentinel-only `TieredCacheSessionLivenessTracker` checks `session:{sessionId:N}` against the tiered cache. Fail-closed on liveness store outage (`AUTH_SESSION_LIVENESS_UNAVAILABLE` 503, not silent allow).
4. **Per-endpoint scope check** — transport-binding layer. The `RequireD2Scope` / `[D2RequireScope]` metadata enumerates scopes; the middleware/interceptor verifies the JWT's `scope` set is a superset.

JWKS at the OIDC-canonical `/.well-known/jwks.json` (off-the-shelf JWT libraries auto-discover via `/.well-known/openid-configuration` — no custom paths). Cluster-wide JWKS rotation via the `ICacheInvalidationBackplane` (Redis pub/sub on `d2.security.key-rotated:jwks`); the `JwksBackplaneSubscriber` triggers `RefreshAsync` on every consumer instance.

### Cross-transport `IRequestContext` resolver

A host can wire HTTP + gRPC simultaneously on the same Kestrel instance. Both transports MUST land the resolved `IRequestContext` in the SAME DI scope so handler injection works regardless of which transport invoked the call. Both transport extensions register a scoped `IRequestContext` resolver lambda that reads from a shared `HttpContext.Items` slot (`D2HttpContextItems.REQUEST_CONTEXT`, lives in `auth-abstractions`). HTTP middleware writes the slot directly. gRPC interceptor mirrors the write into both `HttpContext.Items` AND `ServerCallContext.UserState` (the gRPC hot-path accessor reads the latter for zero-cost lookups). A parity test (`tests/Unit/Auth/Inbound/RequestContextResolverParityTests.cs`) defends against future drift between the two registration paths.

### Uniform 401 — no 403 from the auth boundary

`AUTH_SCOPE_INSUFFICIENT` maps to **401 Unauthorized** in BOTH transports — never 403 Forbidden. Rationale: the auth boundary keeps a uniform shape regardless of whether the JWT was bad (signature / expiry / issuer) or the scopes were insufficient. An attacker probing endpoints can't distinguish "not authenticated" from "authenticated but lacks scope X" via status code — both look the same. Coarse user-facing message (`auth_errors_UNAUTHORIZED` TK key) carries no granularity either; granularity surfaces ONLY on the machine-readable `d2_error_code` extension (and gRPC trailer).

### ServiceKey — constant-time comparison

`X-D2-Service-Key` header for inter-service auth (legacy / pre-OAuth deployments). (Long-term direction is RFC 6749 §4.4 `client_credentials` via `D2.Shared.Auth.Outbound`.)

`CryptographicOperations.FixedTimeEquals` for the comparison. **Plain `==` is vulnerable to timing attacks**.

**Compare against EVERY valid key, no short-circuit.** Even after a match, iterate the rest of the valid-key list. Otherwise the comparison time leaks which key matched.

`.RequireServiceKey()` endpoint filter for one-line gating.

### Endpoint scope metadata

Per-endpoint required-scope sets are declared in the route surface, NOT in handler code. The two metadata paths feed the same enforcement at the transport-binding layer:

| Transport | Fluent | Attribute |
|---|---|---|
| HTTP | `app.MapGet("/files/{id}", H).RequireD2Scope("files.read");` | n/a (Minimal API surface uses fluent) |
| gRPC | n/a (gRPC services use class/method attributes) | `[D2RequireScope("files.read")] public override Task<...> GetFile(...)` |

Three endpoint states, evaluated by the auth boundary:

- **No scope metadata** — runs the FULL pipeline (JWT validator + session-liveness check) and accepts any authenticated caller. Equivalent to "auth required, no specific scope required."
- **`RequireD2Scope("...")` / `[D2RequireScope("...")]`** — runs the FULL pipeline, then requires the caller's scopes to overlap (any-of) with the declared set. Insufficient scopes → uniform `AUTH_SCOPE_INSUFFICIENT` 401.
- **`MarkAsD2HarmlessEndpoint()` / `[D2HarmlessEndpoint]`** — SKIPS the entire pipeline (no validator, no liveness, no scope check). The deny-by-default pressure is at the SCOPE level, not the endpoint-registration level — harmless-endpoint opt-out is the explicit, security-sensitive escape hatch reserved for k8s probes, intra-cluster health/info endpoints, and OIDC discovery (Edge-only). Any other use is a security bug.

### Other route-level gates

Beyond per-endpoint scopes, broader policy still composes at the route level (handlers stay clean of re-checks — they trust `IRequestContext`):

| Method | Gate |
|---|---|
| `.RequireAuth()` | Authenticated user |
| `.RequireOrg(...types)` | User has active org membership matching org type(s) |
| `.RequireRole(...roles)` | User has role within active org |
| `.RequireStaff()` | Org type = staff/admin (impersonation-aware) |
| `.RequireTrustedService()` | Service-identity JWT (not user JWT) |

**Gate at route level — no handler-level re-checks.** Handlers should trust `IRequestContext`. Route-level gates make security visible at the endpoint declaration.

For full per-csproj reference (JWKS provider internals, telemetry tag enumerations, failure-code debugging table, composition examples for HTTP-only / gRPC-only / both): see [`server/shared/dotnet/auth/README.md`](../server/shared/dotnet/auth/README.md), [`auth-http/README.md`](../server/shared/dotnet/auth-http/README.md), [`auth-grpc/README.md`](../server/shared/dotnet/auth-grpc/README.md).

### Translation — none (intentionally)

There is **no** server-side HTTP translation middleware. `D2Result` ships `TKMessage` objects (`{ "key": "...", "params": { ... }? }`) verbatim over the wire; the SvelteKit client translates on receipt via Paraglide. This decision is permanent — see [i18n](#i18n) for the rationale.

The runtime `D2.Shared.I18n.Translator` does exist, but it is consumed only by **outbound notifications** (Courier emails / SMS / push) where the recipient locale is on their user profile and the rendered text must be inlined into the notification payload before delivery.

---

## Configuration

### `parseEnvArray()` — indexed env-var convention

D2's env vars use **indexed convention** for arrays: `PREFIX__0=value0`, `PREFIX__1=value1`, etc. (NOT comma-separated — that breaks for values containing commas).

`parseEnvArray("PREFIX")` returns `["value0", "value1", ...]`. Used everywhere arrays land in env: locale lists, CORS origins, API key lists, etc.

Also matches .NET `IConfiguration`'s array handling (`__N` index → array element). Cross-platform-compatible env conventions.

### URL parsers

Connection-string parsers for `postgres://`, `redis://`, `amqp://`. Centralize parsing — never `new Uri(connStr)` ad-hoc. The shared parser handles edge cases (passwords with `@`, special characters, multi-host fallback).

---

## i18n

The i18n stack splits across three csprojs (two consumption-facing libs + one Roslyn analyzer):

- **`D2.Shared.I18n.Abstractions`** — zero external deps (no NuGet packages, no other shared-lib references — only what the .NET runtime ships). Owns `TKMessage`, the `ITranslator` interface, and the SrcGen-emitted `TK.*` constants. Domain layers reference this; `D2.Shared.Result` and `D2.Shared.Utilities` depend on it.
- **`D2.Shared.I18n.SourceGen`** — Roslyn `IIncrementalGenerator` (netstandard2.0) referenced by Abstractions as an analyzer (`OutputItemType="Analyzer"`, no runtime dll). Emits the `TK.*` constants from `contracts/messages/en-US.json` at every build. Lives at `server/shared/dotnet/i18n-source-gen/` as its own top-level slot — different TFM, different consumption pattern, conceptually a sibling of Abstractions, not a sub-component.
- **`D2.Shared.I18n`** — runtime. Owns `Translator`, `SupportedLocales`, and the `AddD2I18n` DI extension. Pulls `IConfiguration` + DI Abstractions. **Domain code never references this** — only composition roots and outbound-notification handlers (Courier).

### `TKMessage` — the structural primitive

```csharp
public sealed record TKMessage
{
    public string Key { get; }
    public IReadOnlyDictionary<string, string>? Parameters { get; }
    internal TKMessage(string key, IReadOnlyDictionary<string, string>? parameters = null);
    public TKMessage With(string name, string value);
    public TKMessage With(IReadOnlyDictionary<string, string> parameters);
}
```

- **Internal constructor.** Producers can ONLY construct a `TKMessage` via the SrcGen-emitted `TK.*` constants. There is no public ctor. "Untranslated literal in `D2Result.Messages`" is structurally unrepresentable.
- **Immutable.** `With(...)` returns a new instance; the static-readonly `TK` constants stay pinned.
- **Order-independent param equality.** Two `TKMessage`s with the same key and same param bindings (regardless of `With()` call order) compare equal.

```csharp
// No params:
D2Result<T>.NotFound(messages: [TK.Common.Errors.NOT_FOUND]);

// With params:
D2Result<T>.ValidationFailed(messages: [
    TK.Auth.Errors.PASSWORD_WEAK.With("minLength", "12")]);

// Per-field:
D2Result<T>.ValidationFailed(inputErrors: [
    new InputError("email", [TK.Common.Validation.EMAIL_INVALID])]);
```

### Wire format = code shape

`TKMessage` ships verbatim over the wire — same JSON shape in code and on the wire, no separate "in-memory" vs "wire" representation:

```json
{ "key": "auth_errors_PASSWORD_WEAK", "params": { "minLength": "12" } }
```

Inside a full `D2Result`:

```json
{
  "success": false,
  "statusCode": 422,
  "messages": [{ "key": "common_errors_VALIDATION_FAILED" }],
  "inputErrors": [
    { "field": "email", "errors": [{ "key": "common_validation_EMAIL_INVALID" }] }
  ]
}
```

**Translation happens client-side.** SvelteKit / Paraglide consumes the wire-format `TKMessage` objects and renders them in the active locale. The server is locale-unaware on the HTTP response path. CDN caching benefits, no `Vary: Accept-Language` fragmentation. The runtime `Translator` is invoked only for outbound notifications where recipient locale comes from the user profile.

### TK Source Generator

> **TL;DR.** Edit `contracts/messages/en-US.json`, save, build. The constant `TK.Domain.Category.IDENTIFIER` appears at next IntelliSense hit. No registration step, no manual TK class to maintain. Drift between JSON and code is impossible by construction. Read on for the rules; you can skip the internals on first pass.

`D2.Shared.I18n.SourceGen.TKGenerator` (a Roslyn `IIncrementalGenerator` referenced as Analyzer) reads `contracts/messages/*.json` via `<AdditionalFiles>`, treats `en-US.json` as the source of truth, decomposes each key (`{domain}_{category}_{IDENTIFIER}`) into a TK path (`TK.Domain.Category.IDENTIFIER`), and emits a `TK.g.cs` containing nested `static partial class` chains with one `static readonly TKMessage` per key.

| JSON key | Generated path |
|---|---|
| `common_errors_NOT_FOUND` | `TK.Common.Errors.NOT_FOUND` |
| `auth_email_invitation_subject` | `TK.Auth.Email.INVITATION_SUBJECT` |
| `geo_validation_address_line1_required` | `TK.Geo.Validation.ADDRESS_LINE1_REQUIRED` |

Build-time diagnostics (`D2I18N001`–`D2I18N006`) cover invalid keys, per-locale coverage gaps, key collisions, orphaned keys in non-en-US catalogs, missing `en-US.json`, and malformed JSON. Drift between code constants and JSON catalog keys is structurally impossible — the constant doesn't exist if the JSON key doesn't.

Full surface in [`server/shared/dotnet/i18n-abstractions/README.md`](../server/shared/dotnet/i18n-abstractions/README.md).

### TK constants — never bare literals

Outside the SrcGen-emitted `TK.*` constants, **never write a translation-key string literal**. The `TKMessage` ctor is `internal` precisely to make this impossible:

```csharp
// ✗ Compile error — no public ctor
new TKMessage("common_errors_NOT_FOUND");

// ✓ Use the constant
TK.Common.Errors.NOT_FOUND
```

Backend handler messages (`D2Result.Messages`), input errors (`D2Result.InputErrors`), and notification content (D2.Courier) all consume `TKMessage`. End users see all of these — they all need to be translation keys.

### BCP 47 locale convention

10-locale list (matches `contracts/messages/`):
- `en-US`, `en-CA`, `en-GB`
- `fr-FR`, `fr-CA`
- `es-ES`, `es-MX`
- `de-DE`
- `it-IT`
- `ja-JP`

Driven by env vars (per `parseEnvArray()` above): `PUBLIC_ENABLED_LOCALES__0`, `PUBLIC_ENABLED_LOCALES__1`, etc. + `PUBLIC_DEFAULT_LOCALE`. `SupportedLocales` reads these at construction and exposes canonical-cased `All` / `Base` / `LanguageDefaults` properties.

### Translation key conventions

- Auth pages: `auth_{feature}_{purpose}`
- App pages: `webclient_app_{page}_{purpose}`
- Design / demo / debug: `webclient_{section}_{purpose}`
- Common UI / errors: `common_ui_*` / `common_errors_*`
- Backend handler messages: prefer `common_errors_*` keys
- Reuse existing keys where they match

When adding new keys: add to ALL locale files in `contracts/messages/` simultaneously. They MUST stay in sync. The SrcGen surfaces gaps via D2I18N002 at build time — but adding-to-en-US-only still ships a missing-translation latent bug; catch it at PR review.

---

## Spec-driven codegen — the cross-cutting pattern

Shared vocabularies that ship across language boundaries (.NET handlers, TS clients, ops dashboards) live in JSON spec files under `contracts/`. A Roslyn `IIncrementalGenerator` reads the spec at every build and emits typed constants directly into the consuming assembly. Hand-mirrored constants are forbidden — drift between the spec and the code is structurally impossible because the constants don't exist unless the spec entry does.

The pattern:

1. **Spec file** at `contracts/{topic}/{topic}.spec.json` (paired with `schema.json` for editor + CI validation).
2. **SourceGen csproj** at `server/shared/dotnet/{topic}-source-gen/` — `netstandard2.0`, `IsRoslynComponent=true`, `PrivateAssets="all"` on Roslyn deps + bundled `System.Text.Json`. Diagnostics are split: a Roslyn-decoupled `EmitDiagnostic` record + per-id factories for emitter logic (so pure-logic tests can reference the diagnostic IDs without loading Roslyn), `DiagnosticDescriptors.cs` holds the Roslyn `DiagnosticDescriptor` instances loaded only inside the host.
3. **Single-target dispatch** — the generator gates emission by `Compilation.AssemblyName` (or per-meter `consumingAssembly` field for multi-meter specs). Other consumers get the analyzer DLL but no emission. Test projects and downstream csprojs that just transitively reference the consuming assembly get the constants for free, without re-running the generator.
4. **Consumer wiring** — the consuming csproj adds a `<ProjectReference … OutputItemType="Analyzer" ReferenceOutputAssembly="false" />` to the SrcGen csproj plus an `<AdditionalFiles Include="…/{topic}.spec.json" />` entry. Build-time `D2***` diagnostics gate every spec change.
5. **Cross-spec resolution** — when one spec's emitted vocabulary needs to enumerate values from another spec (e.g. a telemetry tag whose closed-set values are the auth-error-codes catalog), the resolver reads the sibling spec at codegen time via the same `<AdditionalFiles>` mechanism and emits a build error on missing / mismatched references. A test-time spec-consistency assertion (e.g. `tests/Unit/SpecsConsistency/AuthErrorCodesVsTelemetrySpecConsistencyTests.cs`) reads both specs side-by-side and asserts set equality, defending against drift the build-time check might miss.

All instances mirror the same csproj template + diagnostic-split + single-target-dispatch structure:

| SrcGen | Spec source | Emits into | Diagnostic prefix | Notes |
|---|---|---|---|---|
| `D2.Shared.I18n.SourceGen` | `contracts/messages/*.json` | `D2.Shared.I18n.Abstractions` | `D2I18N` | Translation-key constants `TK.*`. See the [TK Source Generator](#tk-source-generator) section above. |
| `D2.Shared.Auth.Scopes.SourceGen` | `contracts/auth-scopes/scopes.spec.json` | `D2.Shared.Auth.Abstractions` | `D2AS` | OAuth scope-string constants + tree structure. Reference template — newer SrcGens mirror its file layout. |
| `D2.Shared.Auth.Audiences.SourceGen` | `contracts/auth-audiences/audiences.spec.json` | `D2.Shared.Auth.Abstractions` | `D2AAU` | JWT `aud`-claim audience constants + `AllUrls` / `ByName` / `IsKnown` / `Resolve` / `ResolveByUrl` helpers. |
| `D2.Shared.Auth.ErrorCodes.SourceGen` | `contracts/auth-error-codes/auth-error-codes.spec.json` | `D2.Shared.Auth` | `D2AEC` | `AuthErrorCodes` constants + `AuthFailures` semantic-factory class. Consumed by transport-binding csprojs that surface `d2_error_code` on the wire. |
| `D2.Shared.Telemetry.Tags.SourceGen` | `contracts/telemetry/telemetry.spec.json` | per-meter (gated by `consumingAssembly`) | `D2TEL` | Per-meter `*TelemetryTags.g.cs` typed-constants classes. The `d2.auth.problem.emitted` instrument's `d2_error_code` tag uses `valuesFromSpec=auth-error-codes` — cross-spec resolver enumerates at codegen time; `AuthErrorCodesVsTelemetrySpecConsistencyTests` asserts set equality. |
| `D2.Shared.Headers.SourceGen` | `contracts/headers/headers.spec.json` | `D2.Shared.Headers.{Common,Http,Amqp,Grpc}` (gated by `Compilation.AssemblyName`; `Common` filters `applicability.Length >= 2`, others filter `applicability.Contains(transport)`) | `D2HDR` | Wire-protocol header constants. One spec, four catalogs, identical wire values for cross-transport entries. |
| `D2.Shared.Auth.JwtClaims.SourceGen` | `contracts/jwt-claims/jwt-claims.spec.json` | `D2.Shared.Auth.Abstractions` | `D2JWT` | JWT claim-name constants. Standard claims keep canonical names (`sub`, `aud`, `act`, `scope`); D² custom claims use `d2_` prefix. Same spec drives the TS-side `@d2/auth-abstractions` `JwtClaimTypes` catalog. |
| `D2.Shared.InProcessKeys.SourceGen` | `contracts/in-process-keys/keys.spec.json` | `D2.Shared.Auth.Abstractions` (`D2HttpContextItems`) + `D2.Shared.Auth.Grpc` (`D2GrpcUserStateKeys`) (per-binding `consumingAssembly`) | `D2IPK` | In-process slot-key catalogs with cross-binding wire-value parity by construction. |
| `D2.Shared.Context.SourceGen` | `contracts/auth-context/IAuthContext.spec.json` + `contracts/request-context/IRequestContext.spec.json` | `D2.Shared.AuthContext.Abstractions` (`IAuthContext`) + `D2.Shared.Context.Abstractions` (`IRequestContext`, `MutableRequestContext`, `PropagatedContext`, `PropagatedContextExtensions`, `PropagatedContextSerializer`) (per-assembly dispatch) | `D2CTX` | Spec-driven interface + concrete + propagation codec. Identity fields (`UserId` / `OrgId` / `Scopes` / `ActorChain`) MUST NOT be marked `propagate: true` — those rebuild from the JWT at every sync hop. |
| `D2.Shared.Messaging.SourceGen` | `contracts/mq-messages/mq-messages.spec.json` + `contracts/mq-subscriptions/mq-subscriptions.spec.json` | `D2.Shared.Messaging.Abstractions` (`MqMessages.g.cs` + `MqSubscriptions.g.cs` constants + immutable `MqMessagesRegistry` / `MqSubscriptionsRegistry` lookup tables) | `D2MQ` | Cross-spec validation against `D2.Shared.Encryption.EncryptionDomains` (D2MQ004 fires on encryption-domain drift). Drives the `[MqPub]` / `[MqSub]` attribute resolution + the wire-routing tables consumed by `D2.Shared.Messaging.RabbitMq`. |

### Codegen output is committed to git

Both languages' codegen output ships **inside the repository**, not as a build artifact:

- **.NET**: each consumer csproj sets `<CompilerGeneratedFilesOutputPath>Generated</CompilerGeneratedFilesOutputPath>` so the SourceGen output lands under `<csproj-dir>/Generated/<GeneratorAssembly>/<GeneratorClass>/*.g.cs`. The directory is tracked in git. A `<Compile Remove="$(CompilerGeneratedFilesOutputPath)\**\*.cs" />` `<ItemGroup>` is required alongside, otherwise the SDK-default `<Compile Include="**/*.cs" />` glob picks up the on-disk copy AND the in-memory pipeline emits the same content, producing `CS0101` / `CS0111` duplicate-definition errors. See the [Microsoft Learn reference](https://learn.microsoft.com/dotnet/core/project-sdk/msbuild-props#compilergeneratedfilesoutputpath).
- **TS**: each `tsx` emitter under `tools/ts-codegen/` writes `*.g.ts` files into the consuming package's `src/` directory. `pnpm codegen` re-emits all; per-package `pnpm generate` re-emits just one.
- **`.gitattributes`**: both `*.g.cs` and `*.g.ts` carry `linguist-generated=true` so GitHub PR UI collapses generated-file diffs by default. Reviewers can expand for spot-checks.

Why commit codegen output: PR reviewers see spec → emit fidelity in the diff (a one-line spec edit shows the full downstream catalog change in the same review), IDE navigation works on first checkout without a local build, and spec drift between hand-edits and codegen output is impossible to introduce silently. Idempotency is structural — re-running the SrcGen against the unchanged spec produces byte-identical output, so the second `dotnet build` / `pnpm codegen` after a clean state is a no-op in the working tree.

### Spec-driven catalog migration — the "delete the hand-written outright" pattern

When a hand-written constants catalog (e.g. `RequestHeaders.cs`, `JwtClaimTypes.cs`, `D2HttpContextItems.cs`) gets a spec backing, the migration is **outright deletion of the hand-written file plus net-new spec authoring**, not a parallel-emit-then-deprecate dance:

1. Author the spec (`contracts/{topic}/{topic}.spec.json` + `schema.json`).
2. Author the SourceGen (`server/shared/dotnet/{topic}-source-gen/`) per the template above.
3. Wire the consumer csproj's `<ProjectReference OutputItemType="Analyzer">` + `<AdditionalFiles>` entries.
4. Delete the hand-written `.cs` file in the same change.
5. Spec-pin tests assert per-VALUE constants for every entry (not just structural reflection) — these tests fail on every spec edit and are intentionally low-cost to update so the test gates the spec change explicitly.
6. Cross-spec consistency tests assert set-equality across any spec that depends on another (e.g. headers spec driving multiple per-transport catalogs all referencing the same wire values).

The "delete outright" discipline keeps the codebase honest — a parallel-emit phase would let stale hand-written constants drift undetected. Same principle as not committing v1-retrospective framing into KEEP docs: describe what IS, not the journey from what WAS.

Adding a new spec-driven codegen lib:

1. Add `contracts/{topic}/{topic}.spec.json` + `schema.json`.
2. Copy `server/shared/dotnet/auth-scopes-source-gen/` as a template — keep the per-source-gen file layout (`{Topic}Spec.cs`, `{Topic}SpecLoader.cs`, `{Topic}Emitter.cs`, `{Topic}Generator.cs`, `EmitDiagnostics.cs` factory shim, `EmitResult.cs`, `DiagnosticIds.cs`, `DiagnosticDescriptors.cs`). The `Polyfills/`, `EmitDiagnostic` record, `LoadResult<TSpec>`, and `SpecFile` types come from the shared `source-gen-shared/` directory via the `<Compile Include>` block (see "Shared source-gen scaffolding" below).
3. Reserve a 4-letter diagnostic-id prefix (existing: `D2I18N`, `D2AS`, `D2AAU`, `D2AEC`, `D2TEL`, `D2HDR`, `D2JWT`, `D2IPK`, `D2CTX`, `D2MQ`) and number contiguously from `001`.
4. Gate emission by assembly name in the `[Generator]` so non-target consumers get analyzer-only.
5. Wire each consumer csproj with `<ProjectReference … OutputItemType="Analyzer" ReferenceOutputAssembly="false" />` + `<AdditionalFiles Include="…/spec.json" />` + `<EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>` + `<CompilerGeneratedFilesOutputPath>Generated</CompilerGeneratedFilesOutputPath>` + the `<Compile Remove="$(CompilerGeneratedFilesOutputPath)\**\*.cs" />` `<ItemGroup>` block.
6. Add a per-lib `README.md` documenting the spec format, field rules, diagnostics table, emitted output, and wiring snippet.
7. If the catalog has a TS-side counterpart, add the matching `tsx` emitter under `tools/ts-codegen/` so both languages stay in lockstep.

Spec-driven codegen is the structural enforcement behind the [5-layer rename safety net](#5-layer-rename-safety-net-spec-driven-codegen--nameof-discipline) above — every renamed spec field cascades through generated code, consumer compile sites, `nameof()`-bound emission sites, behavioral tests, and spec-pin literal tests, in that order.

### Shared source-gen scaffolding

The source-gen scaffolding common to every generator lives under `server/shared/dotnet/source-gen-shared/`:

```
server/shared/dotnet/source-gen-shared/
├── Polyfills/
│   ├── IsExternalInit.cs       — netstandard2.0 init-accessor polyfill (System.Runtime.CompilerServices)
│   └── StringExt.cs            — Falsey() / Truthy() polyfill (D2.Shared.SourceGen.Polyfills)
├── EmitDiagnostic.cs            — record (string DescriptorId, ImmutableArray<object> Args)
├── LoadResult.cs                — generic record LoadResult<TSpec> wrapping spec or diagnostic
└── SpecFile.cs                  — IIncrementalGenerator pipeline boundary (Path, Content)
```

`source-gen-shared/` is **not a project** — there's no csproj. Each source-gen csproj wires the shared files in via:

```xml
<ItemGroup>
  <Compile Include="..\source-gen-shared\**\*.cs">
    <Link>Shared\%(RecursiveDir)%(Filename)%(Extension)</Link>
  </Compile>
</ItemGroup>
```

The `<Link>` element groups the included files under a virtual `Shared\` folder in IDE views.

What stays per-source-gen:

- `{Topic}Generator.cs` — `[Generator]`-attributed entry point; topic-specific dispatch + Roslyn-host wiring.
- `{Topic}Emitter.cs` — pure-logic emitter writing the `.g.cs` source.
- `{Topic}SpecLoader.cs` — JSON-shape parser for the topic's spec.
- `{Topic}Spec.cs` / `{Topic}Entry.cs` / etc. — topic-specific data records.
- `EmitDiagnostics.cs` — topic-specific factory helpers producing the shared `EmitDiagnostic` record (each factory wraps `new(DiagnosticIds.X, [args])`).
- `EmitResult.cs` — kept per-source-gen because the field shape varies (some carry `(GeneratedSource, Diagnostics)`, others additionally carry a `HintName` for multi-file emission). Extracting a shared shape would be a footgun; per-source-gen control is the safer default.
- `DiagnosticIds.cs` + `DiagnosticDescriptors.cs` — per-topic IDs + Roslyn descriptors.

What's structurally guaranteed by the shared scaffolding:

- **Falsey/Truthy dogfood** — every source-gen consumes the same polyfill via `using D2.Shared.SourceGen.Polyfills;`. A new generator cannot accidentally fall back to `string.IsNullOrEmpty` for "missing local polyfill" reasons; the polyfill arrives via the shared `Compile Include` automatically. (See `tests/Unit/Context/SourceGen/ContextEmitterDogfoodTests.cs` for the regression-pin pattern; copy that test into any source-gen whose emitter logic depends on Falsey/Truthy semantics.)
- **`LoadResult<TSpec>` shape** — every loader returns the same generic record; consumers always see `(Spec, Diagnostic)` properties.
- **`EmitDiagnostic` record shape** — every diagnostic uses the same `(DescriptorId, Args)` shape; tests can write a single helper that handles diagnostics from any source-gen.
- **`SpecFile` pipeline boundary** — every generator uses the same value-equatable record at the IIncrementalGenerator pipeline boundary, so Roslyn's incremental cache works uniformly.

When a test project (e.g. `D2.Shared.Tests`) directly references multiple source-gen DLLs and constructs the shared types directly (e.g. `new SpecFile(...)`), CS0433 ambiguity surfaces — the same type+namespace is defined in N source-gen assemblies. Resolve via `extern alias` on the relevant `<ProjectReference>` (`Aliases="global,XxxSourceGen"`) plus `extern alias XxxSourceGen;` + `using SpecFile = XxxSourceGen::D2.Shared.SourceGen.SpecFile;` in the test file. Tests that consume the source-gen only through its public API (e.g. `Generator.Initialize` + the Roslyn driver) don't hit the ambiguity and don't need the alias dance.

---

## Domain Validation — smart-constructor pattern

Domain types use **smart-constructor factories returning `D2Result<T>`** for all input-validating construction. Throwing constructors are reserved for programmer-bug invariants (null where non-null is required, internal state corruption that can't be triggered by user input).

```csharp
public sealed record Contact
{
    public string Email { get; init; }

    private Contact(string email) => Email = email;

    public static D2Result<Contact> Create(string? rawEmail)
    {
        var emailResult = rawEmail.TryParseEmail();
        if (emailResult.BubbleOnFailure<string, Contact>(out var bubbled, out var email))
            return bubbled;

        return D2Result<Contact>.Ok(new Contact(email!));
    }
}
```

The pattern:

1. **Private constructor** — domain instances cannot be created bypassing validation.
2. **Static `Create` returning `D2Result<TSelf>`** — primitive-level rules go through the `string?.TryParse*` extensions in `D2.Shared.Utilities`; cross-field rules belong to the `Create` method itself.
3. **`BubbleFail` chains.** Each primitive validation result bubbles up. The composite never reports half-validated state — either everything passes and you get `Ok`, or the first failure shapes the response.
4. **`TKMessage` keys.** Failure messages are `TK.*` constants; the wire-format response slots them straight into `Messages` / `InputErrors`.

### Validation layers

Validation is single-layered: smart-constructor factories on domain types are the one place input gets checked.

- Primitive rules (email shape, phone shape, URL shape) → `string?.TryParse*` extensions in `D2.Shared.Utilities`. Each returns a `D2Result<T>` so failures slot straight into the composite.
- Cross-field rules (start-date < end-date, password matches confirm, etc.) → composite `Create` method on the domain type. Aggregate per-field failures with `D2Result.Combine` so a single submit surfaces every problem at once instead of bailing on the first.
- DTO-bag pre-validation at HTTP boundaries (when the body has so many disjoint shape concerns that mapping straight to a domain type would be premature) → a thin static method per route that returns `D2Result<TInput>`. Keep these tiny — most boundaries can map directly to the domain type and let `Create` validate.

### When to throw vs return

| Case | Mechanism |
|---|---|
| User input fails validation | `D2Result<T>.ValidationFailed` with `TK.*` keys |
| External lookup misses | `D2Result<T>.NotFound` |
| Downstream service errors | `BubbleFail` from the result |
| Programmer-bug invariant (null param marked non-null, internal corrupted state) | Throw `ArgumentNullException` / `InvalidOperationException` |
| Cancellation | Re-throw `OperationCanceledException` (or let it propagate); `BaseRepoHandler` maps it to `D2Result.Canceled` |

The rule: **anything caused by data the caller controls is a result, not an exception. Anything caused by code that should be impossible is an exception.**

---

## Anti-Patterns to Actively Avoid

- **Thin handlers that just call another handler** — eliminate per-handler cleanup. If an app-layer handler's body is `return otherHandler.HandleAsync(input)`, delete it; depend on the inner handler directly.
- **Hand-written DB migrations** — generator-driven only.
- **String error codes outside `D2Result` factories** — use `TK.*` constants from `D2.Shared.I18n`.
- **Wrapping framework primitives without an opinionated semantic** — use `IDistributedCache` directly only if Microsoft's `Get`/`Set`/`Refresh`/`Remove` is enough. If you need `SetNx` / `Increment` / `AcquireLock` — use D²'s richer abstraction.
- **Returning `Ok()` after a fallible operation** — a `try/catch` that swallows failure and returns success is almost always a bug. Either `BubbleFail` or explicitly handle.
- **Hardcoding what should be in Options** — batch sizes, cache expirations, retry attempts, lock TTLs all go through `IOptions<T>`.
