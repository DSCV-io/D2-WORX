<!--
Copyright (c) DCSV. All rights reserved.
-->


> **Visibility: PUBLIC** — ships with the open surface (`public/`).  
> Do not add product IP, private paths, or non-exportable runbooks.
# ADR-0005: Universal handler pipeline — `BaseHandler` + provider-pluggable repo handlers

- **Status**: Accepted
- **Date**: 2026-05-30
- **Deliverable**: D2 shared libraries (backfilled)

## Context

Every operation unit in D²-WORX — CQRS handlers, repository handlers, RabbitMQ consumers, scheduled jobs — must satisfy the same cross-cutting requirements: structured telemetry correlated to the originating request, a uniform typed-failure envelope so callers never deal with raw exceptions (ADR-0003), and PII-safe logging. The options were to satisfy those requirements by hand in each handler (duplicating a try/catch + stopwatch + OTel calls dozens of times) or to introduce a framework intermediary (e.g. MediatR pipeline behaviors).

At the same time, repository handlers require a second concern: translating provider-specific database exceptions (`PostgresException`, `DbUpdateConcurrencyException`, `SocketException`) into the same typed `D2Result` failure vocabulary the rest of the platform speaks. That translation is inherently provider-specific — SQLSTATE `23505` is a PostgreSQL encoding — but the handler calling `SaveChangesAsync` should carry no reference to Npgsql.

These two concerns — universal pipeline and provider-specific exception mapping — pull toward different layers and must be separated cleanly.

## Decision

### Layer 1 — Abstractions (`D2.Shared.Handler.Abstractions`)

`IHandler<TInput, TOutput>` and `IHandlerContext` are published in a zero-heavy-dependency package. Domain-layer code and application interfaces reference these contracts without pulling DI, OTel, or AspNetCore transitively. `HandlerOptions` (a `sealed record`) carries per-call toggles (`LogInput`/`LogOutput`, slow/critical thresholds defaulting to 100 ms/500 ms, `ScopeRequirement`) and lives here so callers can adjust behavior without referencing the core package. The abstractions/core split follows the general pattern of ADR-0006.

`ScopeRequirement` is a positional `sealed record(HandlerScopeMatch Match, IReadOnlySet<string> Scopes)` that combines the scope set with an explicit match-mode enum so the any-of vs all-of semantic is always stated at declaration time:

```csharp
protected override HandlerOptions DefaultOptions => new()
{
    ScopeRequirement = new ScopeRequirement(
        HandlerScopeMatch.All,
        new HashSet<string> { Scopes.Files.Read, Scopes.Files.Write })
};
```

`HandlerScopeMatch` mirrors the transport-layer `ScopeMatch` enum but lives in the handler abstractions package so handler assemblies carry no compile-time dependency on the auth layer (layer-hygiene invariant: `D2.Shared.Handler.Abstractions` references neither `D2.Shared.Auth.Abstractions` nor any ASP.NET Core package).

A `null` `HandlerOptions.ScopeRequirement` (the default) disables the per-handler check entirely — any authenticated caller that passed the transport-layer auth middleware or interceptor may invoke the handler. An empty `Scopes` set is rejected at construction time: the `ScopeRequirement` constructor throws `ArgumentException` if `Scopes` is empty. Pass a `null` `ScopeRequirement` to disable the per-handler check. The pipeline guard (`is { Scopes.Count: > 0 }`) remains as defense-in-depth for a now-unreachable branch.

### Layer 2 — Core pipeline (`D2.Shared.Handler`)

`BaseHandler<TSelf, TInput, TOutput>` is the abstract base every handler inherits. The CRTP `TSelf` parameter drives the typed logger category (`HandlerContext<TSelf>` resolves `ILogger<TSelf>` via DI) and the OTel tag value `d2.handler.name`.

`RunCorePipelineAsync` is non-virtual (non-overridable). Per invocation it:

1. Evaluates `ScopeRequirement` (any-of or all-of, per `HandlerScopeMatch`) and returns `D2Result.Forbidden` at entry if the caller's `IRequestContext.Scopes` does not satisfy it — before any work runs. A `null` `ScopeRequirement` skips the check entirely (the `is { Scopes.Count: > 0 }` guard remains as defense-in-depth, but an empty set is now unconstructible).
2. Starts an `ActivitySource` span (source name `D2.Shared.Handler`) tagged with `d2.handler.name`, user/org/role, and full impersonation context when present.
3. Opens a structured log scope carrying the same correlation fields so every log line emitted inside `ExecuteAsync` automatically carries `d2.trace_id`, `d2.user_id`, `d2.org_id`, and handler name.
4. Increments `d2.handler.invoked` and starts a `Stopwatch`.
5. Calls `ExecuteAsync` inside a universal try/catch:
   - **No exception thrown**: records `d2.handler.duration` (histogram, ms); increments `d2.handler.succeeded` **if the result is successful, else `d2.handler.failed`** (a handler that returns a typed failure result without throwing counts as failed); checks slow/critical thresholds; auto-injects `traceId` on results that don't already carry one; returns `(result, null)`.
   - **`OperationCanceledException`, caller token canceled**: returns `(D2Result.Canceled, oce)` — intentional cancellation.
   - **`OperationCanceledException`, caller token NOT canceled**: returns `(D2Result.ServiceUnavailable, oce)` — a downstream timeout (HttpClient/SQL command timeout, internal watchdog).
   - **Any other exception**: logs only `ex.GetType().Name` (never `ex.Message`, which can carry bearer tokens, connection strings, or raw user input per `rules.md §3.1`), sets span status to Error, returns `(D2Result.UnhandledException, ex)`.
   - In all failure branches: records duration and increments `d2.handler.failed`.

The four OTel instruments (`invoked` / `succeeded` / `failed` / `duration`) live in `HandlerTelemetry` as static fields on a shared `Meter`.

`RunCorePipelineAsync` returns `(D2Result<TOutput?>, Exception?)` rather than only the result. This is intentional: `BaseHandler.HandleAsync` discards the exception and returns only the result, but repo-handler subclasses need the captured exception to perform provider-specific remapping without re-running the pipeline.

`AddD2Handler` registers `HandlerContext<>` as an open-generic Transient. `IRequestContext` is deliberately NOT registered here — each transport stack (HTTP, gRPC, RabbitMQ) builds and scopes its own per-request `IRequestContext` via its own middleware before any handler resolves.

### Layer 3 — Repo handler (`D2.Shared.Handler.Repo`)

`BaseRepoHandler<TSelf, TInput, TOutput>` extends `BaseHandler` and overrides `HandleAsync`. It calls `RunCorePipelineAsync`, receives `(result, exception)`, and if an exception was captured:

- `DbUpdateConcurrencyException` (EF BCL type — provider-agnostic) maps immediately to a concurrency-conflict `D2Result`, with an optional `MapDbException` hook for domain-specific messages.
- Everything else routes to an injected `IDbExceptionClassifier.Classify(exception)` returning a `DbFailureKind?`. A null return means unrecognized — `BaseHandler`'s `UnhandledException` result is kept. A non-null kind dispatches through an exhaustive switch to the matching typed factory (`UniqueViolation`, `ForeignKeyViolation`, `NotNullViolation`, `CheckViolation`, `DbTimeout`, `DbDeadlock`, `DbConnectionFailure`). An unhandled future enum value throws `ArgumentOutOfRangeException` loudly at first exercise rather than silently degrading.

`MapDbException` is a protected virtual hook returning `null` by default. Handlers override it to attach domain-specific `TKMessage` keys and `InputError`s to a known constraint failure (e.g. a unique violation on the user-email index → `auth_errors_EMAIL_ALREADY_TAKEN` + `new InputError("email", ...)`).

`IDbExceptionClassifier` (`D2.Shared.Handler.Repo.Abstractions`) is the provider boundary: a single `DbFailureKind? Classify(Exception)` method, registered singleton, required thread-safe.

### Layer 4 — PostgreSQL classifier (`D2.Shared.Handler.Repo.Postgres`)

`PostgresDbExceptionClassifier` implements `IDbExceptionClassifier` with a two-pass strategy: pass 1 walks the inner-exception chain (handling `AggregateException` branches) for a `PostgresException` with a SQLSTATE and maps `23505` → `UniqueViolation`, `23503` → `ForeignKeyViolation`, `23502` → `NotNullViolation`, `23514` → `CheckViolation`, `40001`/`40P01` → `Deadlock`, `57014` → `Timeout`, `57P03`/`53300`/`08*` → `ConnectionFailure`; pass 2 scans for `SocketException`/`IOException` → `ConnectionFailure`; anything else returns `null` (programmer/config error → surfaces as `UnhandledException` so ops are paged). Client-side `OperationCanceledException` from Npgsql `CommandTimeout` never reaches this classifier — `BaseHandler` handles it first. `AddD2Postgres` registers the classifier via `TryAddSingleton` (a custom classifier registered earlier wins).

### What deliberately does NOT use `BaseHandler`

`DefaultLocalCache` (`D2.Shared.Caching.Local.Default`) bypasses `BaseHandler` entirely: its xmldoc states the work itself is tens of nanoseconds and a handler pipeline would be ~100× overhead. The per-call cost (Activity creation, log-scope dictionary allocation, Stopwatch, counter increments) would dwarf the tens-of-nanoseconds cache work. This is the recognized carve-out: the pipeline serves long-lived, I/O-bound operations; latency-critical in-process primitives opt out by design.

### CQRS and naming conventions

Handler structure follows [ADR-0020](0020-service-project-structure.md): operations are organized under two full-word categories — `Application/Handlers/Commands/` and `Application/Handlers/Queries/` — with one per-operation folder per op (`I<Op>Handler` / `<Op>Handler` / `<Op>Input` / `<Op>Output`, all co-located). The primary-constructor handler idiom is the codebase-wide structural encoding of these decisions (`docs/PATTERNS.md` Handler/service structure; `docs/dev/rules.md §9`).

## Consequences

**Positive.**

- Every handler receives a per-call OTel span and four metrics tagged with `d2.handler.name` for free — no per-handler instrumentation. Dashboards/alerts work uniformly across CQRS, repo, messaging, and job handlers.
- The universal try/catch guarantees no exception escapes a handler boundary as anything other than a typed `D2Result`.
- The structured log scope ensures every log line inside `ExecuteAsync` carries `d2.trace_id` / `d2.user_id` / `d2.org_id` / handler name — no missing-correlation lines.
- Slow/critical threshold logging surfaces performance regressions automatically; handlers needing higher limits override `DefaultOptions` rather than suppressing the warning.
- The `IDbExceptionClassifier` seam isolates all PostgreSQL-specific knowledge in one package; a future SQL Server / SQLite provider registers its own classifier and leaves `BaseRepoHandler` and every concrete handler untouched.
- `MapDbException` attaches domain-specific failure messages to constraint violations without leaving the `BaseRepoHandler` contract.
- `IHandler` / `IHandlerContext` in the abstractions package let domain types declare handler dependencies without pulling DI or OTel transitively.

**Negative / risks.**

- **Base-class coupling.** Every handler is coupled to `BaseHandler`'s pipeline shape; pipeline changes affect every handler at once. Mitigated by the non-virtual `RunCorePipelineAsync` (subclasses cannot tamper) — changes are at one site.
- **CRTP ceremony.** `TSelf` forces every handler to repeat its own type name (`class Foo(...) : BaseHandler<Foo, ...>`) — a visible ergonomic cost with no runtime behavior, existing solely for the typed logger category and OTel tag.
- **Per-call pipeline overhead.** An Activity start/stop, a log-scope dictionary allocation, a Stopwatch, and four counter increments run on every invocation, including fast ones — explicitly acceptable for I/O-bound handlers but the stated reason `DefaultLocalCache` opts out.
- **`DispatchDefault` exhaustiveness gap.** The `DbFailureKind` switch uses a wildcard throw arm, not a compile-time exhaustive switch; adding an enum value without updating the dispatch produces a runtime `ArgumentOutOfRangeException` rather than a compile error (acknowledged in the code).

## Alternatives considered

**Hand-rolled per-handler try/catch and manual telemetry.** The zero-infrastructure baseline. Rejected because: the cross-cutting logic is non-trivial and every author would reimplement it inconsistently; omission is silent (a missing metric increment yields wrong dashboards with no compile/test signal); correlation fields on the log scope would drift across handlers.

**MediatR-style pipeline behaviors.** A mediator with pre/post behaviors provides the same cross-cutting without base-class inheritance. Rejected because: it introduces a third-party mediator dependency on every invocation; the "handler" is no longer the resolvable unit (the mediator is), complicating per-handler DI registration, per-handler `DefaultOptions` overrides, and direct handler-to-handler calls; the `D2Result` contract and `IHandler` already define a clean invocation surface, making the mediator unnecessary indirection.

**Middleware-only cross-cutting (AspNetCore / gRPC interceptor).** Transport middleware can inject spans and catch exceptions at the HTTP/gRPC boundary — but that covers only wire-invoked CQRS handlers, not repository handlers, messaging consumers, or scheduled jobs, which are not on a transport path. Rejected as incomplete; transport middleware still handles JWT validation + request-context construction, but the per-handler pipeline belongs at the handler layer.

## References

- `public/packages/dotnet/handler/abstractions/` — `IHandler.cs`, `IHandlerContext.cs`, `HandlerOptions.cs`, `ScopeRequirement.cs`, `HandlerScopeMatch.cs`.
- `public/packages/dotnet/handler/core/` — `BaseHandler.cs` (sealed `RunCorePipelineAsync`), `BaseHandler.Logging.cs` (exception-type-name-only contract), `HandlerTelemetry.cs` (static `ActivitySource` + `Meter`), `HandlerContext.cs`, `HandlerServiceCollectionExtensions.cs` (`AddD2Handler`).
- `public/packages/dotnet/handler/repo/BaseRepoHandler.cs` — EF/DB exception remapping; `MapDbException` hook; exhaustive dispatch.
- `public/packages/dotnet/handler/repo-abstractions/` — `IDbExceptionClassifier.cs`, `DbFailureKind.cs`, `D2ResultDbFactories.cs`.
- `public/packages/dotnet/handler/repo-postgres/` — `PostgresDbExceptionClassifier.cs`, `PgErrorCodes.cs`, `PostgresServiceCollectionExtensions.cs` (`AddD2Postgres`).
- `public/packages/dotnet/caching/local-default/DefaultLocalCache.cs` — documented `BaseHandler` carve-out.
- `docs/PATTERNS.md` (Handler / service structure); `docs/dev/rules.md §9` (handler predicates) + §3.1 (PII-safe exception logging).
- [ADR-0003](0003-d2result-errors-as-values.md) — the typed failure envelope every handler returns. [ADR-0006](0006-abstractions-implementation-split.md) — the abstractions/implementation split applied here (and the provider-pluggable `IDbExceptionClassifier` triple).
