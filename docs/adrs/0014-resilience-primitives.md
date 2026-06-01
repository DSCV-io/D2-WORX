<!--
Copyright (c) DCSV. All rights reserved.
-->

# ADR-0014: Resilience — bespoke `D2Result`-aware primitives (`RetryHelper` / `CircuitBreaker` / `Singleflight` / `ResilientPipeline`)

- **Status**: Accepted
- **Date**: 2026-05-30
- **Deliverable**: Phase 0 — shared libraries (backfilled)

## Context

Service handlers execute outbound calls across heterogeneous boundaries: gRPC stubs (on `HttpClient`), RabbitMQ publishes, EF Core repository chains, Redis via StackExchange, SeaweedFS via SDK, and internal handler chains. Each can fail transiently, fail under load, or produce a failed `D2Result` **without throwing**.

The errors-as-values decision (ADR-0003) established that handler chains return `D2Result<T>` with an `IsTransientRetryable` classifier. This breaks standard retry tooling: Polly and `Microsoft.Extensions.Http.Resilience` operate on exceptions and HTTP response codes — they cannot inspect a returned `D2Result` carrying `ServiceUnavailable` and decide to retry; they only see a successful return value. Additionally, cache-miss stampedes (concurrent callers triggering the same expensive upstream fetch — JWKS, reference data, IPinfo) are a recurring hot-key concern not represented in Polly's primitive set.

A separation note: `ServiceDefaults` (ADR-0013) already wires the BCL `AddStandardResilienceHandler()` on every outbound `HttpClient`, covering transport-level retry/circuit-breaking for HTTP/gRPC. The gap this ADR fills is **in-process** resilience: `D2Result`-returning chains, non-HTTP SDKs, and the stampede-prevention layer before any network call.

## Decision

A bespoke library, `D2.Shared.Resilience`, was built as a thin set of in-process primitives with deep `D2Result` awareness — intentionally **not** a Polly wrapper, written from scratch as pure-logic code. Three primitives + one composition surface:

**1. `RetryHelper` — dual-path retry with `D2Result` awareness.** `RetryAsync<T>` retries on exceptions (via `IsTransient`) and on returned values (via `ShouldRetry`), evaluated independently per attempt. `RetryD2ResultAsync<TData>` is the result-aware overload: absent a caller override (detected by reference-equality against a default sentinel), it retries on `r is { Failed: true, IsTransientRetryable: true }` — i.e. `ServiceUnavailable`/`RateLimited`, never `UnhandledException` (unknown side-effect state must not be auto-retried). Backoff is exponential with full jitter; defaults live in a non-generic peer class so they allocate once, not per closed generic.

**2. `CircuitBreaker<T>` — three-state lock-free breaker.** Closed/Open/Half-Open with all transitions via `Interlocked` (no lock/`SemaphoreSlim` on hot paths). Both thrown exceptions and returned values satisfying `isFailure(result)` increment the failure counter. Half-Open uses `CompareExchange` on a probe-in-flight flag to allow exactly one probe while routing concurrent callers to fallback or `CircuitOpenException`. The `onStateChange` callback is the observability seam; the library emits no spans/metrics/logs of its own. `NowFunc` is injectable for deterministic tests.

**3. `Singleflight<TKey, TValue>` — in-flight de-duplication.** The first caller for a key starts a `Lazy<Task<TValue>>` (`ExecutionAndPublication`) via `ConcurrentDictionary.GetOrAdd`; concurrent callers join the same `Task`. The key is removed in `finally` once settled — explicitly **not** a cache. A per-caller `CancellationToken` cancels only that caller's wait (`Task.WaitAsync(ct)`), never the shared operation.

**4. `ResilientPipeline<TKey, TValue>` — composition surface.** Composes `IResilientLayer` instances outer-first; `ExecuteAsync(key, operation, ct)` returns `D2Result<TValue>` and never throws (every terminating exception maps to a result code: `CircuitOpenException` → `ServiceUnavailable`, caller-canceled `OperationCanceledException` → `Canceled`, other transient → `ServiceUnavailable`, else `UnhandledException`). Layer order is the protection semantic: `[Singleflight, CircuitBreaker, Retry]` is upstream-protecting (retry inside CB); `[Singleflight, Retry, CircuitBreaker]` is restart-recovery (retry outside CB; the caller must size the retry budget to span cooldown). Registration is fluent and **keyed-singleton-only** — there is no unkeyed path, because two unkeyed registrations of the same `(TKey, TValue)` would silently last-wins-overwrite.

**Cross-language parity (`@d2/resilience`).** The BFF tier calls the same external APIs and ships `retryAsync`/`retryD2ResultAsync`, `CircuitBreaker`, `Singleflight`, and `ResilientPipeline` with structurally identical semantics (same layer-order contract, same result-aware retry path via a marker sentinel, same per-caller-cancellation-only guarantee). Jitter is multiplicative (±fraction of the computed delay) to suit browser/Node timing; the TS pipeline does not return `D2Result` at the boundary (JS lacks the BCL exception-to-result mapping) but the retry overload is otherwise equivalent. The library depends only on `D2.Shared.Result` + `Microsoft.Extensions.DependencyInjection.Abstractions` — no Polly, no `Microsoft.Extensions.Resilience`.

## Consequences

**Positive.**

- Retry fires correctly on returned `D2Result` transient failures, not only thrown exceptions — the common case for internal handler chains where infrastructure failures surface as typed results.
- `Singleflight` prevents thundering-herd stampedes at hot keyspaces without external coordination.
- `ResilientPipeline` gives handlers a single call site returning `D2Result` with no try/catch; exception-to-result mapping is centralized and consistent.
- No transitive Polly/`Microsoft.Extensions.Resilience` dependency; lock-free primitives; injectable clock/delay seams for deterministic tests.

**Negative / risks.**

- Two full implementations (C# + TypeScript) maintained in lockstep; semantic drift (jitter formula, classifier coverage) must be caught by review, not a shared implementation.
- The retry-budget/cooldown sizing contract for restart-recovery (`[Retry, CircuitBreaker]`) is documented but not enforced — a misconfigured budget silently exhausts before the breaker recovers.
- `onStateChange` footgun: a throwing callback replaces the upstream exception that triggered the transition (documented, but no runtime guard).
- Callers needing gRPC `StatusCode.Unavailable` awareness must supply a custom `IsTransient` predicate — the default classifier omits a gRPC dependency by design.
- Zero built-in telemetry: observability is consumer-owned via `onStateChange` + call-site spans; no automatic per-attempt/per-transition trace or metric.

## Alternatives considered

**Adopt Polly / `Microsoft.Extensions.Http.Resilience` wholesale.** Mature primitives, but the principal integration payoff is the `HttpClientFactory` path — applicable only to `HttpClient` calls. Most D² outbound boundaries are not HTTP (RabbitMQ, EF Core, Redis, SeaweedFS SDK, internal chains); for those, Polly's retry predicate is exception-only, and making it result-aware requires a custom `ResiliencePipeline<D2Result<T>>` with a `ShouldHandle` inspecting `IsTransientRetryable` — functionally equivalent to `RetryD2ResultAsync` but with Polly's allocation overhead and a transitive dependency across the entire service graph. The BCL standard handler remains wired in ServiceDefaults for outbound `HttpClient`; this ADR covers the non-HTTP / in-process gap, not a replacement.

**Exception-only retry (no `D2Result`-aware path).** Require all transient conditions to throw. Rejected: the errors-as-values decision (ADR-0003) is premised on handlers returning `D2Result`; forcing exceptions through internal chains at resilience boundaries would invert that contract selectively and restore exception-as-control-flow.

**No `Singleflight` primitive.** Rely on external caching to absorb stampedes. Rejected: a cache only helps *after* the first call completes; `Singleflight` deduplicates the in-flight execution at the moment of a cold miss — exactly the stampede window (JWKS, reference data on first access) a cache cannot close.

## References

- `server/shared/dotnet/resilience/` — `Retry/RetryHelper.cs` + `RetryOptions.cs` + `RetryDefaults.cs`; `CircuitBreaker/CircuitBreaker.cs` + `CircuitBreakerOptions.cs`; `Singleflight/Singleflight.cs`; `Pipeline/ResilientPipeline.cs` + `IResilientLayer.cs` + `ResilientPipelineBuilder.cs` + `ResilientPipelineServiceCollectionExtensions.cs`; the csproj (sole external dep: DI abstractions) + README (Polly rejection rationale, layer-order semantics, telemetry-is-consumer-owned).
- `server/shared/typescript/resilience/src/` — `retry/retry-helper.ts`, `singleflight/singleflight.ts`, `pipeline/resilient-pipeline.ts`.
- `server/shared/dotnet/service-defaults/ServiceDefaultsServiceCollectionExtensions.cs` — the `AddStandardResilienceHandler()` wiring for `HttpClient` (the non-overlapping BCL handler).
- `docs/PATTERNS.md` (Resilience section).
- [ADR-0003](0003-d2result-errors-as-values.md) (establishes `IsTransientRetryable`), [ADR-0006](0006-abstractions-implementation-split.md) (handler chains return `D2Result`, not throw), [ADR-0013](0013-service-defaults-composition-root.md) (wires the BCL HttpClient handler separately).
