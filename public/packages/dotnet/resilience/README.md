<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.Resilience

> Parent: [`public/packages/dotnet/`](../README.md)

The sole, feature-complete resilience mechanism for the platform. Covers retry, circuit-breaker, singleflight, timeout, and concurrency rate-limiting as composable pipeline layers. Lock-free where possible (`Interlocked` operations, `ConcurrentDictionary`); test seams baked in (clock + delay overrides). Resilience is **caller-side, opt-in** (off by default — it costs latency) and **per-call overridable**.

Depends only on `DcsvIo.D2.Result` (for the `D2Result`-aware retry overload) and `Microsoft.Extensions.DependencyInjection.Abstractions` (for keyed DI).

> **Design rationale: custom primitives over Polly.** Most of our outbound boundaries are NOT HTTP (RabbitMQ publishes, EF Core, Redis via StackExchange, internal handler chains, SeaweedFS via SDK). Polly's main "free win" — its HttpClientFactory integration via `AddStandardResilienceHandler()` — applies cleanly to gRPC (since `Grpc.Net.Client` rides on `HttpClient`) and external HTTP APIs, but the HTTP-level integration only sees HTTP 200 + trailing gRPC status codes; retry-on-`StatusCode.Unavailable` requires custom predicates anyway. With <500 LOC of pure-logic primitives + first-class `D2Result.IsTransientRetryable` integration, owning the primitives is cheaper than wrapping Polly.

---

## File layout

| Path                                                                    | Contents                                                                                                                                                                                                                                                          |
| ----------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `Retry/RetryHelper.cs`                                                  | Static `RetryAsync<T>` (generic) and `RetryD2ResultAsync<TData>` (D2Result-aware overload). Internal `IsTransientException` classifier and `CalculateDelay` math.                                                                                                 |
| `Retry/RetryOptions.cs` + `Retry/RetryDefaults.cs`                      | `RetryOptions<T>` record — `MaxAttempts`, `BaseDelayMs`, `BackoffMultiplier`, `MaxDelayMs`, `Jitter`, `ShouldRetry`, `IsTransient`, `DelayFunc`. Defaults centralized in the non-generic `RetryDefaults` peer (single SoT, no per-T duplication).                 |
| `CircuitBreaker/CircuitBreaker.cs`                                      | `CircuitBreaker<T>` — three-state Closed / Open / Half-Open with lock-free state transitions.                                                                                                                                                                     |
| `CircuitBreaker/CircuitBreakerOptions.cs`                               | `CircuitBreakerOptions` — `FailureThreshold`, `CooldownDuration`, `NowFunc` (test clock). Owns the single source of truth for breaker defaults; the breaker reads from a parameterless Options instance when nothing is supplied.                                 |
| `CircuitBreaker/CircuitState.cs`                                        | `CircuitState` enum — `Closed`, `Open`, `HalfOpen`.                                                                                                                                                                                                               |
| `CircuitBreaker/CircuitOpenException.cs`                                | Thrown by `ExecuteAsync` when the circuit is open and no fallback is supplied.                                                                                                                                                                                    |
| `Singleflight/Singleflight.cs`                                          | `Singleflight<TKey, TValue>` — deduplicates concurrent in-flight async operations by key. NOT a cache: keys are removed once the operation completes.                                                                                                             |
| `Timeout/TimeoutOptions.cs`                                             | `TimeoutOptions` — `Duration` (default 10 s). Zero or negative = pass-through (no CTS created). Canonical small-Options-record shape.                                                                                                                             |
| `RateLimiting/RateLimiter.cs`                                           | Hand-rolled `SemaphoreSlim`-based concurrency limiter (no `System.Threading.RateLimiting` package). `ExecuteAsync<T>` acquires a permit within `AcquisitionTimeout`, runs the op, releases in `finally`; throws `RateLimitRejectedException` on rejection.       |
| `RateLimiting/RateLimiterOptions.cs`                                    | `RateLimiterOptions` — `MaxConcurrency` (default 100), `AcquisitionTimeout` (default `TimeSpan.Zero` = reject-fast). Canonical small-Options-record shape.                                                                                                       |
| `RateLimiting/RateLimitRejectedException.cs`                            | Thrown when a permit cannot be acquired within `AcquisitionTimeout`. Maps to `D2Result.TooManyRequests()` (429 / `RATE_LIMITED`) at the pipeline boundary.                                                                                                       |
| `Pipeline/IResilientLayer.cs`                                           | The decorator interface — one `WrapAsync(key, next, ct)` method.                                                                                                                                                                                                  |
| `Pipeline/{Singleflight,CircuitBreaker,Retry,Timeout,RateLimiter}Layer.cs` | The five concrete layer wrappers around the primitives above.                                                                                                                                                                                                  |
| `Pipeline/ResilientPipeline.cs`                                         | Composes layers in outer-first order; one `ExecuteAsync(key, op, ct)` returning `D2Result<TValue>` (never throws — every exception is mapped to a result). Exposes `PassThrough` static sentinel for the bypass-but-keep-D2Result-shape use case.                |
| `Pipeline/IResilientPipelineBuilder.cs` + `ResilientPipelineBuilder.cs` | Fluent registration DSL — `.UseTimeout().UseRateLimiter().UseSingleflight().UseCircuitBreaker().UseRetries(opts)` etc.                                                                                                                                            |
| `Pipeline/ResilientPipelineServiceCollectionExtensions.cs`              | `AddResilientPipeline<TKey, TValue>(p => ...)` extension on `IServiceCollection`.                                                                                                                                                                                |

---

## Public API

### `RetryHelper.RetryAsync<T>` — exponential backoff with jitter

```csharp
var result = await RetryHelper.RetryAsync(
    operation: async (attempt, ct) =>
    {
        // 'attempt' is 1-based.
        return await SomeFlakyApiCall(ct);
    },
    options: new RetryOptions<MyResponse>
    {
        MaxAttempts = 5,
        BaseDelayMs = 200,
        BackoffMultiplier = 2.0,
        MaxDelayMs = 30_000,
        Jitter = true,
    },
    ct);
```

Defaults (when `options` is null): 5 attempts, 1s base delay, ×2 multiplier, 30s ceiling, full jitter (uniform `[0, calculated)`), retry transient exceptions only (`HttpRequestException` ≥500 / 429 / 408, `TaskCanceledException`, `TimeoutException`, `SocketException`), accept all returned values.

Behavioral guarantees:

- **Two retry triggers, evaluated independently per attempt:**
  - `IsTransient(ex)` — thrown exceptions. Default: the helper's built-in classifier. Override for gRPC `StatusCode.Unavailable`, custom transient codes, etc.
  - `ShouldRetry(value)` — returned values. Default: never retry returns. Useful for "retry on 5xx body without a thrown exception" patterns.
- **Final attempt always terminates the loop** by returning the value or throwing the exception — no defensive epilogue.
- **`OperationCanceledException` from `ct`** is re-raised as cancellation, NEVER classified as transient (would otherwise mask user-initiated cancellation as a retryable network blip).
- **Backoff math**: `min(BaseDelayMs × BackoffMultiplier^retryIndex, MaxDelayMs)`, then jittered to `random(0, calculated)` when `Jitter=true`. Exponent clamped to 63 to avoid `Math.Pow` overflow on degenerate retry counts.

#### `RetryD2ResultAsync<TData>` — `D2Result`-aware overload

When the operation returns a `D2Result<TData>` instead of throwing, the default `ShouldRetry` predicate becomes `r => r.Failed && r.IsTransientRetryable`:

```csharp
var result = await RetryHelper.RetryD2ResultAsync(
    operation: (_, ct) => SomeHandlerThatReturnsD2Result(ct),
    options: new RetryOptions<D2Result<MyDto>> { MaxAttempts = 4 },
    ct);
```

Retries on `ServiceUnavailable` and `RateLimited` results (the two error codes that `IsTransientRetryable` covers). Crucially does NOT retry on `UnhandledException` results — unknown system state must never be auto-retried (a side effect may have committed). Caller-supplied `ShouldRetry` always wins over the default.

### `CircuitBreaker<T>` — three-state lock-free breaker

```csharp
var cb = new CircuitBreaker<MyResponse>(
    isFailure: r => !r.Success,                         // value-failure predicate
    options: new(failureThreshold: 5, cooldownDuration: TimeSpan.FromSeconds(30)),
    onStateChange: (from, to) =>
        logger.LogInformation("circuit: {From} → {To}", from, to));

var result = await cb.ExecuteAsync(
    operation: ct => SomeUpstreamCall(ct),
    fallback: () => ValueTask.FromResult(MyResponse.Cached),
    ct);
```

`CircuitBreakerOptions` follows the project's small-Options-record convention: every parameter is nullable + falls back to its documented default in the ctor body, so call sites stay terse:

```csharp
new()                                                 // all defaults (5, 30s, null)
new(3)                                                // FailureThreshold=3, rest defaulted
new(3, TimeSpan.FromMilliseconds(100), clock.Now)     // all three positionally
new(failureThreshold: 3, nowFunc: clock.Now)          // skip middle, named args
new(0)                                                // explicit 0 is preserved (not coerced to default)
```

`with`-expressions still work for record-style overrides on an existing instance.

State machine:

- **Closed** — calls pass through. Failures (thrown exceptions OR `isFailure(value) == true`) increment a counter. Success resets the counter. When counter hits `FailureThreshold` → **Open**.
- **Open** — fast-fails. With a `fallback`, returns the fallback's value; without one, throws `CircuitOpenException`. Stays open until `CooldownDuration` has elapsed.
- **Half-Open** (after cooldown) — exactly ONE caller wins the probe slot (lock-free `Interlocked.CompareExchange` on a probe-in-flight flag); concurrent callers during the probe receive the fallback (or `CircuitOpenException`). On probe success → **Closed** (counter reset). On probe failure → straight back to **Open** (cooldown timer reset).

Thread-safety:

- All state via `Interlocked` operations + `Volatile.Read`. No locks.
- `onStateChange` callback fires synchronously on the thread that triggered the transition. Idempotent transitions (Closed → Closed) do NOT fire it. Keep callbacks fast and non-blocking.
- **Footgun — `onStateChange` MUST NOT throw.** A throwing callback REPLACES the upstream exception that triggered the transition: a buggy logger inside the callback can swap a meaningful "TimeoutException from upstream X" with its own "InvalidOperationException from logger", making outage diagnosis painful. Wrap the callback body in your own try/catch (or stick to plain log/metric calls that won't throw) to preserve the upstream exception for callers.
- `Reset()` manually returns the breaker to Closed (clears counter + probe flag); only fires `onStateChange` if state actually changed.

> **Telemetry is consumer-owned.** This lib emits no spans / metrics / logs of its own — by design, to stay free of `System.Diagnostics.DiagnosticSource` and `Microsoft.Extensions.Logging` transitive deps. The `onStateChange` callback is the canonical observability seam for circuit-breaker transitions; the `RetryHelper.RetryAsync` `logger` parameter is the seam for retry attempts. Pipeline-level observability (per-layer counters, per-attempt spans) is out of scope for this lib — the consumer composes it at their own observability boundary.

Test seams:

- `CircuitBreakerOptions.NowFunc` — override for the monotonic-millisecond clock (default `Environment.TickCount64`). Tests use a `FakeClock` to advance time deterministically without `Task.Delay`.

### `TimeoutLayer<TKey, TValue>` — wall-clock deadline

Bounds the wrapped operation with a linked `CancellationTokenSource`. On expiry throws `TimeoutException` (distinct from caller cancellation) so:

- an outer `RetryLayer` retries it (`TimeoutException` is already classified transient by `RetryHelper.IsTransientException` — zero classifier change needed)
- a leaked timeout at the pipeline boundary maps to `D2Result.ServiceUnavailable()` (503, `IsTransientRetryable = true`)

Place at **two positions** in the same pipeline to express separate total-request and per-attempt deadlines (the builder's flat-list accumulator supports duplicate layer types):

```csharp
services.AddResilientPipeline<string, T>("key", p => p
    .UseRateLimiter()
    .UseTimeout(new(TimeSpan.FromSeconds(30)))  // total: bounds all retries combined
    .UseRetries(new() { MaxAttempts = 3 })
    .UseCircuitBreaker("key")
    .UseTimeout(new(TimeSpan.FromSeconds(5))));  // per-attempt: inside retry loop
```

`TimeoutOptions` follows the small-Options-record convention: parameterless ctor = 10-second default; `Duration <= Zero` = pass-through (no CTS allocated).

### `RateLimiter` + `RateLimiterLayer<TKey, TValue>` — concurrency limiter

Hand-rolled `SemaphoreSlim`-based concurrency limiter (no `System.Threading.RateLimiting` package — matches the lib's <500-LOC / sole-external-dep ethos). Bounds the number of concurrent in-flight calls to `MaxConcurrency`. Callers that cannot acquire a permit within `AcquisitionTimeout` are rejected via `RateLimitRejectedException` (→ `D2Result.TooManyRequests()` / 429 at the pipeline boundary).

> **Client-side, in-process only.** This is admission control for outbound calls — it limits concurrent pressure from THIS process on an upstream. It is NOT the server-side, distributed per-tier rate-limit middleware (which uses `IDistributedCache` counters).

```csharp
services.AddResilientPipeline<string, T>("key", p => p
    .UseRateLimiter(new RateLimiterOptions(maxConcurrency: 10))
    .UseRetries());
```

To share one `RateLimiter` across multiple pipelines (e.g. a shared broker-level concurrency cap), register it as a keyed singleton and use the service-key overload — mirroring the shared-CB pattern:

```csharp
services.AddKeyedSingleton<RateLimiter>("broker", (_, _) => new(new(maxConcurrency: 10)));
services.AddResilientPipeline<string, T>("critical", p => p.UseRateLimiter("broker"));
services.AddResilientPipeline<string, T>("routine",  p => p.UseRateLimiter("broker"));
```

`RateLimiterOptions` follows the small-Options-record convention. `RateLimiterLayer` is `IDisposable`. In the inline-options case, the layer owns the `RateLimiter` and its `SemaphoreSlim`; disposal propagates through the pipeline: `ResilientPipeline<TKey, TValue>` implements `IDisposable` and iterates its layers on dispose, so the container disposes the pipeline singleton at teardown and the owned semaphore is released. In the keyed-DI case, the `RateLimiter` singleton is registered directly with the container, which disposes it at teardown; the layer holds a reference only and its `Dispose` is a no-op.

### `ResilientPipeline<TKey, TValue>.PassThrough` — bypass sentinel

```csharp
// Caller explicitly wants no resilience but still wants the D2Result shape.
var result = await ResilientPipeline<string, T>.PassThrough.ExecuteAsync(key, op, ct);
```

A zero-layer pipeline singleton that performs ONLY the exception → `D2Result<TValue>` mapping — no retry, no CB, no timeout, no rate-limiter. Equivalent to `new ResilientPipeline<TKey, TValue>()` but named so the bypass intent is explicit in generated clients and per-call override sites.

### `Singleflight<TKey, TValue>` — concurrent-call deduplication

The first caller for a given key runs the operation; concurrent callers for the same key share the same `Task<TValue>`. Once the operation completes (success or failure), the key is removed from the in-flight map.

```csharp
private static readonly Singleflight<string, WhoIsRecord> sr_whoIsLookups = new();

public Task<WhoIsRecord> ResolveAsync(string ip, CancellationToken ct) =>
    sr_whoIsLookups.ExecuteAsync(
        key: ip,
        operation: token => FetchExpensiveWhoIsAsync(ip, token),
        ct: ct).AsTask();
```

**This is NOT a cache.** Once the operation completes, the key is removed and the next call re-runs the operation. Use Singleflight to prevent thundering-herd duplication of in-progress work, then layer a real cache (`DcsvIo.D2.Caching.Memory`, `DcsvIo.D2.Caching.Redis`, etc.) on top of the singleflight call site if you want persistent reuse of the result.

#### When to use Singleflight — and when NOT

| Use SF when…                                                                                         | Don't use SF when…                                                                                      |
| ---------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------- |
| Multiple concurrent callers ask for the **same logical thing** by key                                | Each call is a **distinct intent** that should produce a distinct effect                                |
| Operation is **idempotent** — running it once and sharing the result is correct for everyone waiting | Operation has a **per-call side effect** (publish a message, send an email, write a row, charge a card) |
| You're preventing a **thundering-herd / cache-miss stampede** on a hot key                           | Two callers happen to share a transport but represent different business events                         |

Concrete:

- ✅ **WhoIs / IPinfo lookup by IP** — 50 requests for `1.2.3.4` collapse to 1 upstream call.
- ✅ **JWKS fetch** — every concurrent token validation wants the same document.
- ✅ **Reference-data lookups** (currencies, countries, feature-flag manifests).
- ✅ **Cache-miss read for a hot key** (config-by-name, user-by-id during a request burst).
- ❌ **Audit / event publishes** — each event is unique data; SF would silently drop events whose key collided.
- ❌ **Email / SMS / push delivery** — each `Notify` is a discrete intent. Two callers asking to email Alice = two emails.
- ❌ **gRPC / HTTP writes** (`Create*`, `Update*`, `Delete*`) — SF in front deletes work.
- ❌ **Outbox / queue drain publishers** — dedup belongs upstream (in the outbox itself), not in the publisher.

Heuristic: if the answer to _"if two callers ask, is one shared answer correct for both?"_ is **yes**, SF fits. If the answer is _"two callers means two side effects we want"_, SF is a bug.

Behavioral guarantees:

- **`TKey` constraint**: `notnull`. Strings, GUIDs, value-types, custom hashable records all work.
- **Per-caller cancellation does NOT affect siblings.** When you pass a `CancellationToken`, only YOUR wait is cancellable. The shared operation runs with `CancellationToken.None`, so one caller bailing out cannot poison the result for everyone else sharing it.
- **Exception propagation**: an operation throw propagates to ALL waiting callers (they share the same `Task`). The key is still removed in the `finally` block, so the next call after the throw starts a fresh operation.
- **Lazy initialization** via `Lazy<Task<TValue>>` with `LazyThreadSafetyMode.ExecutionAndPublication` — the operation is started exactly once per key even under aggressive concurrency.
- **`Size`** property exposes the current in-flight count (instantaneous; useful for metrics).

---

## When to reach for which

| Need                                                                                         | Tool                                                                      |
| -------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------- |
| Backoff-and-retry around a flaky external call                                               | `RetryHelper.RetryAsync`                                                  |
| Backoff-and-retry around a `D2Result`-returning handler chain                                | `RetryHelper.RetryD2ResultAsync`                                          |
| Avoid hammering a confirmed-down upstream while it recovers                                  | `CircuitBreaker<T>`                                                       |
| Avoid the "five concurrent first requests trigger five identical expensive lookups" stampede | `Singleflight<TKey, TValue>`                                              |
| Bound an operation to a wall-clock deadline; surface expiry as a retryable transient         | `TimeoutLayer<TKey, TValue>` (via `UseTimeout` on the pipeline builder)   |
| Limit concurrent in-flight outbound calls (client-side admission control)                    | `RateLimiter` + `RateLimiterLayer<TKey, TValue>` (via `UseRateLimiter`)  |
| Bypass-but-keep-D2Result-shape (per-call "no resilience" override)                           | `ResilientPipeline<TKey, TValue>.PassThrough`                             |
| Compose any of the above behind a single call site that returns a `D2Result`                 | `ResilientPipeline<TKey, TValue>` (see Pipeline section below)            |

The three primitives compose naturally. The `Pipeline` namespace is the canonical way to do this composition; reach for the raw primitives only when you need direct control or have an unusual layering requirement.

---

## Pipeline — the high-level composition surface

`ResilientPipeline<TKey, TValue>` is a configured pipeline of `IResilientLayer<TKey, TValue>` decorators that:

- composes layers in **outer-first order** (first layer wraps everything else)
- exposes ONE call: `ExecuteAsync(key, operation, ct)` returning `D2Result<TValue>`
- **never throws** — every terminating exception is converted to a `D2Result` per the documented mapping:

  | Exception | Mapping |
  |---|---|
  | `CircuitOpenException` | `ServiceUnavailable` (503) |
  | `RateLimitRejectedException` | `TooManyRequests` (429 / `RATE_LIMITED`, `IsTransientRetryable = true`) |
  | `OperationCanceledException` when caller `ct` canceled | `Canceled` |
  | `TimeoutException` + any other transient (`RetryHelper.IsTransientException`) | `ServiceUnavailable` (503, `IsTransientRetryable = true`) |
  | Anything else | `UnhandledException` |

### Two-layer API: fluent at registration, dead-simple at call site

The intent is that **client-lib authors** configure the pipeline once at the lib's composition root using the fluent DSL; **handlers inside the lib** see only the one-line call surface; **callers of the lib** see nothing — resilience is invisible.

#### Registration layer (lib composition root, fluent)

**All registrations are keyed.** The lib provides no unkeyed registration or resolution path because two unkeyed registrations of the same `(TKey, TValue)` shape would silently overwrite each other (last-wins) — the keyed-mandatory rule eliminates that footgun by construction. Every layer call says EXACTLY which keyed primitive it pulls.

In practice, service keys live in a per-domain constants class in the consumer's app layer (NOT inline strings in registration code), so refactor-renames stay safe and `[FromKeyedServices(...)]` attributes on consumers stay in sync. Public consts are `UPPER_CASE`:

```csharp
// app layer constants — single source of truth for every key.
namespace Edge.IpEnrichment;

public static class IpinfoServiceKeys
{
    public const string LOOKUP = "ipinfo";
}
```

```csharp
// Composition root for the same module — wraps the external ipinfo HTTP API.
public static IServiceCollection AddIpinfoLookup(this IServiceCollection services)
{
    services.AddKeyedSingleton<Singleflight<string, IpinfoLookupResponse>>(IpinfoServiceKeys.LOOKUP);
    services.AddKeyedSingleton<CircuitBreaker<IpinfoLookupResponse>>(
        IpinfoServiceKeys.LOOKUP, (_, _) => /* configured */);

    services.AddResilientPipeline<string, IpinfoLookupResponse>(IpinfoServiceKeys.LOOKUP, p => p
        .UseSingleflight(IpinfoServiceKeys.LOOKUP)
        .UseCircuitBreaker(IpinfoServiceKeys.LOOKUP));

    services.AddTransient<IFindWhoIsHandler, FindWhoIs>();
    return services;
}
```

#### Call-site layer (handler, brain-dead simple)

The handler's constructor pulls the keyed pipeline via `[FromKeyedServices]`:

```csharp
public sealed partial class FindWhoIs(
    [FromKeyedServices(IpinfoServiceKeys.LOOKUP)] ResilientPipeline<string, IpinfoLookupResponse> pipeline,
    IIpinfoClient ipinfo) : BaseHandler<...>
{
    public override async ValueTask<D2Result<WhoIsDTO?>> ExecuteAsync(I input, CancellationToken ct)
    {
        var responseR = await pipeline.ExecuteAsync(
            $"whois:{input.IpAddress}",
            c => ipinfo.LookupAsync(input.IpAddress, c),
            ct);

        if (responseR.BubbleOnFailure<IpinfoLookupResponse, WhoIsDTO?>(out var bubbled, out var resp))
            return bubbled;

        return D2Result<WhoIsDTO?>.Ok(resp.ToDto());
    }
}
```

### Layer order IS the protection semantic

The fluent chain order = layer order in the resulting pipeline (outer-first). Lib authors choose between **upstream-protecting** (`UseCircuitBreaker → UseRetries`: CB sees ONE execution per retry budget; opens after N failed sequences; backoff gives the upstream air to recover — fits fragile upstreams like Resend / Twilio) or **restart-recovery** (`UseSingleflight → UseRetries → UseCircuitBreaker`: each retry is a separate CB execution; `CircuitOpenException` is treated as transient by the default classifier; retries back off through cooldown — fits read-by-key gRPC across rolling deploys). Caller MUST size `MaxAttempts + backoff` to span the breaker's `CooldownDuration` for the restart-recovery composition; otherwise retries exhaust on perpetual CO. Both compositions plus six other recipes live in [SCENARIOS.md](SCENARIOS.md).

### Skipping layers + cross-pipeline shared primitives

Each `Use*` is independent — call zero, one, two, or three. A zero-layer pipeline still does the exception → `D2Result` mapping. To share a primitive across pipelines (e.g. multi-criticality audit pipelines sharing one broker-level CB so any tier's failures count toward the same breaker state), register the shared primitive under its own key and reference that key from each pipeline. The shared topology stays grep-able via the key constant.

### Order matters — the canonical layer order and the WHY

**The recommended outer-to-inner order:**

```
[Singleflight →] RateLimiter → TotalTimeout → Retry → CircuitBreaker → PerAttemptTimeout
```

Singleflight is in brackets because it only applies to idempotent-by-key operations (reads, lookups, JWKS fetches). Never use it on mutating ops.

**Why each position:**

| Layer | Position | Why |
|---|---|---|
| **Singleflight** | Outermost (when used) | All deduplicated callers share ONE concurrency permit, ONE total-budget clock, ONE retry sequence. Placing it anywhere inside means N callers compete for permits and run N independent retry sequences against a breaker that may trip on one of them. |
| **RateLimiter** | 2nd outermost (1st when no SF) | Reject before spending any resources — no retry, no CB state, no timeout clock consumed. A rejected caller gets `TooManyRequests` in microseconds. Placing RL inside Retry means the system burns retry attempts on calls that shouldn't have been made at all. |
| **TotalTimeout** | Outside Retry | Bounds the ENTIRE user-facing latency across all retry attempts. If total budget is 30 s and per-attempt budget is 5 s with 3 retries, the total clock ensures the caller's SLA is respected even if all 3 attempts take 10 s each. Placing total-timeout inside Retry bounds only a single attempt — the "total" is then meaningless. |
| **Retry** | Outside CircuitBreaker — restart-recovery shape | Each retry is a separate CB execution. `CircuitOpenException` is treated as transient by the default classifier; retries back off through the CB cooldown and a later attempt may find the breaker probing/closed. Use this shape when callers can wait for an upstream restart (e.g. rolling deploy). |
| **CircuitBreaker** | Outside Retry — upstream-protecting shape | One full retry sequence = one CB execution. The CB opens only after N complete retry sequences fail, giving the upstream more air. Use this shape for fragile third-party APIs (Resend, Twilio) where you want backoff between whole retry budgets, not just individual attempts. |
| **PerAttemptTimeout** | Innermost | Bounds each individual attempt. A timed-out attempt surfaces as `TimeoutException` (transient), which the outer Retry layer retries. Placing per-attempt-timeout outside Retry is useless — it would bound only one attempt and then surface `ServiceUnavailable` to the caller with no retry. |

**The two canonical Retry↔CircuitBreaker orderings:**

```
Retry → CircuitBreaker  (restart-recovery)
CB opens fast; retries back off through cooldown; recovers from upstream restarts.
Use for: read-by-key gRPC, rolling-deploy resilience.
MaxAttempts + backoff MUST span CooldownDuration.

CircuitBreaker → Retry  (upstream-protecting)
One full retry budget = one CB execution; CB opens only after N sequences fail.
Use for: fragile third-party writes (Resend / Twilio), outbox publishers.
```

**Anti-patterns:**

- **RL innermost** — rejects AFTER burning retry attempts, timeout budget, and CB state.
- **Per-attempt-timeout outside Retry** — times out the entire retry loop; each individual attempt has no separate deadline. Equivalent to a second total-timeout, not a per-attempt one.
- **Singleflight on a mutating op** — two callers wanting to `CreateUser` share ONE execution; only one user is created. Merging distinct side-effects is a silent data-loss bug. See the "When NOT to use SF" table below.

### Preferred configurations by use case

#### (a) Idempotent read-by-key over gRPC/HTTP — restart-recovery shape with SF

```csharp
// Use case: D2.Files → Edge context resolution, JWKS fetch, reference-data lookups.
// Key is the entity ID / IP / etc. — many concurrent callers for the same key are common.
services.AddKeyedSingleton<Singleflight<string, T>>(key);
services.AddKeyedSingleton<CircuitBreaker<T>>(key, (_, _) => new(_ => false));
services.AddResilientPipeline<string, T>(key, p => p
    .UseSingleflight(key)
    .UseRateLimiter(new RateLimiterOptions(maxConcurrency: 20))
    .UseTimeout(new TimeoutOptions(TimeSpan.FromSeconds(30)))           // total budget
    .UseRetries(new()
    {
        MaxAttempts = 5,
        BaseDelayMs = 500,
        MaxDelayMs = 10_000,
    })
    .UseCircuitBreaker(key)
    .UseTimeout(new TimeoutOptions(TimeSpan.FromSeconds(5))));           // per-attempt
// Retry outside CB (restart-recovery): MaxAttempts × backoff MUST exceed CooldownDuration.
// With 5 attempts and 500ms base/×2: ≈ 0.5 + 1 + 2 + 4 = 7.5s avg → fits 30s default cooldown.
```

#### (b) Fragile external API — upstream-protecting CB outside Retry

```csharp
// Use case: D2.Courier → Resend/Twilio. Third-party transient errors retry;
// sustained outages trip the breaker so the provider gets recovery time.
services.AddKeyedSingleton<CircuitBreaker<T>>(key, (_, _) => new(
    isFailure: _ => false,
    options: new(failureThreshold: 3, cooldownDuration: TimeSpan.FromMinutes(1))));
services.AddResilientPipeline<string, T>(key, p => p
    .UseRateLimiter(new RateLimiterOptions(maxConcurrency: 5))          // don't pile on
    .UseTimeout(new TimeoutOptions(TimeSpan.FromSeconds(20)))            // total budget
    .UseCircuitBreaker(key)                                              // upstream-protecting
    .UseRetries(new()
    {
        MaxAttempts = 4,
        BaseDelayMs = 1000,
        BackoffMultiplier = 3.0,
        MaxDelayMs = 30_000,
    })
    .UseTimeout(new TimeoutOptions(TimeSpan.FromSeconds(8))));           // per-attempt
// CB outside R: CB sees one execution per full retry budget. Opens slowly.
// No Singleflight — each email/SMS is a discrete delivery intent.
```

#### (c) Mutating command — conservative; NO Singleflight

```csharp
// Use case: Create/Update/Delete handlers calling an upstream over gRPC.
// No Singleflight — two callers wanting to CreateUser should produce two users.
// Transport-level retry is dangerous unless the upstream is idempotent (idempotency key /
// the operation is provably safe to re-issue). If uncertain: NO retry layer.
// Add retry only when the upstream guarantees idempotency or the mutation is truly safe
// to re-issue (e.g. "upsert by natural key", "mark delivered" with idempotency guard).
services.AddKeyedSingleton<CircuitBreaker<T>>(key, (_, _) => new(_ => false));
services.AddResilientPipeline<string, T>(key, p => p
    .UseRateLimiter(new RateLimiterOptions(maxConcurrency: 10))
    .UseTimeout(new TimeoutOptions(TimeSpan.FromSeconds(15)))
    .UseCircuitBreaker(key));
// No transport-level Retry — side effects must not be duplicated.
// CB still protects against a confirmed-down upstream; RL prevents stampede.
```

#### (d) Internal S2S gRPC call — the canonical default full stack

```csharp
// Use case: any critical inter-service gRPC call in D2 (not idempotent-by-key,
// or SF not needed). Mirrors the strategy set of .NET's standard HTTP resilience handler (retry + circuit-breaker + rate-limiter + per-attempt timeout).
services.AddKeyedSingleton<CircuitBreaker<T>>(key, (_, _) => new(_ => false));
services.AddResilientPipeline<string, T>(key, p => p
    .UseRateLimiter(new RateLimiterOptions(maxConcurrency: 20))
    .UseTimeout(new TimeoutOptions(TimeSpan.FromSeconds(30)))
    .UseRetries(new()
    {
        MaxAttempts = 3,
        BaseDelayMs = 500,
        MaxDelayMs = 10_000,
    })
    .UseCircuitBreaker(key)
    .UseTimeout(new TimeoutOptions(TimeSpan.FromSeconds(8))));
// Restart-recovery without SF: each caller runs its own retry sequence.
// Add UseSingleflight(key) outermost if the call is a read-by-key hot path.
```

### Canonical layer ordering (the standard full-stack composition)

The standard outer-first composition covers the same strategy set as .NET's standard HTTP resilience handler and is fully expressible with this lib's native layers:

```
RateLimiter → TotalTimeout → Retry → CircuitBreaker → PerAttemptTimeout
```

Builder call:

```csharp
services.AddResilientPipeline<string, T>("key", p => p
    .UseRateLimiter()                                          // outermost: admission control
    .UseTimeout(new(TimeSpan.FromSeconds(30)))                 // total-request budget
    .UseRetries(new() { MaxAttempts = 3, BaseDelayMs = 500 })
    .UseCircuitBreaker("key")
    .UseTimeout(new(TimeSpan.FromSeconds(5))));                // per-attempt deadline
```

When Singleflight is used, place it outermost so deduped callers share one concurrency permit:

```
Singleflight → RateLimiter → TotalTimeout → Retry → CircuitBreaker → PerAttemptTimeout
```

See SCENARIOS.md for named recipes + tradeoff rationale.

### Resilience as a caller-side, opt-in choice (cross-process clients)

Beyond the lib-internal-invisible pattern (configure once at the composition root; handlers call one line; callers see nothing), the pipeline also supports a **caller-facing, opt-in, per-call-overridable** usage for generated cross-process clients. Three caller modes:

1. **(a) Use the endpoint's declared default** — the endpoint registers a keyed `ResilientPipeline`; the caller resolves it by key via `[FromKeyedServices(...)]`. No new API.
2. **(b) Supply your own** — the caller passes its own `ResilientPipeline` (or resolves a different keyed pipeline) overriding the endpoint default.
3. **(c) Bypass** — the caller explicitly wants no resilience but still needs the `D2Result<T>` return shape: use `ResilientPipeline<TKey, TValue>.PassThrough`.

Use the lib-internal-invisible pattern for a service's own outbound calls (Courier → Resend, Edge → ipinfo) where the lib author controls the composition. Use the caller-opt-in pattern for shared/generated cross-process clients (gRPC-client emitter output) where the CALLER owns the latency/criticality tradeoff.

### Extensibility

`IResilientLayer<TKey, TValue>` is a single-method interface. Adding a new layer is mechanical: one new `XxxLayer` impl + one new `Use*` builder method on the pipeline — no breaking changes to existing pipelines or call sites.

---

## Tests

`public/packages/dotnet/tests/Unit/Resilience/` — comprehensive adversarial coverage across every public surface. Categories:

- **`RetryHelper`**: full transient-classifier matrix (HTTP 5xx / 429 / 408 / non-transient codes / null status / TaskCanceled / Timeout / Socket / arbitrary). Happy path, throws-then-succeeds, throws-every-attempt-exhaustion, ShouldRetry-true-then-false, ShouldRetry-always-true (last-value wins on exhaustion), alternating throw+return (last terminator wins), pre-canceled token, OCE-from-ct (NOT classified transient), DelayFunc-invoked-between-retries.
- **`RetryD2ResultAsync`**: default predicate retries `ServiceUnavailable`, default predicate does NOT retry `NotFound`, caller-`ShouldRetry`-override wins, null-options behavior.
- **`CalculateDelay`** (internal): zero-index returns base, multiplier applied, max-delay clamp, exponent overflow clamp, jitter range property (200 samples in `[0, calculated)`).
- **`CircuitBreaker`**: initial state, single success/failure, threshold transition Closed→Open, mixed exception+value-failure threshold, success resets counter, Open without/with fallback, Open→HalfOpen on cooldown, HalfOpen probe success closes, HalfOpen probe failure (exception OR value-failure) reopens, HalfOpen probe-lock (concurrent caller gets fallback / throws), `Reset` from Open fires callback, `Reset` from Closed is no-op, callback-null branches on every transition.
- **`Singleflight`**: single-call sanity, concurrent-callers-same-key dedup (1 invocation, 3 returns), concurrent-callers-different-keys both run, sequential-calls-same-key re-run (proves no caching), exception propagation to all waiters + key removed for retry, per-caller cancellation does NOT affect siblings, no-cancellable-token fast path, non-string key (Guid).
- **`CircuitBreakerOptions` / `RetryOptions` / `CircuitOpenException`**: defaults, init-only overrides, exception constructor variants.
- **`TimeoutLayer`**: op-completes-before-timeout (no throw), timeout-fires-throws-TimeoutException (NOT OCE), message-contains-duration, Duration-zero/negative pass-through, default-10s-no-fire, caller-ct-cancel-NOT-masked-as-timeout, both-tokens-cancel-caller-wins, per-attempt-timeout-inside-retry-retried-then-succeeds, per-attempt-timeout-exhausted-maps-to-ServiceUnavailable, total-timeout-maps-to-ServiceUnavailable.
- **`TimeoutOptions`**: default ctor 10s, parameterized null→default, explicit duration, zero preserved, negative preserved, with-expression.
- **`RateLimiter`**: under-limit runs, N+1-rejected-at-zero-acquisition-timeout, positive-acquisition-admits-on-release, positive-acquisition-rejects-after-timeout, op-throws-permit-released, caller-ct-canceled-before-acquire-no-permit-leaked, max-concurrency-stress (50 callers / K=5 gate), Dispose-no-throw.
- **`RateLimiterLayer`**: under-limit runs, gate-full-zero-timeout-throws, release-on-throw, pipeline-maps-to-TooManyRequests, keyed-DI resolves, inline-options builds, explicit-instance does-not-own-limiter, Dispose-no-throw.
- **`RateLimiterOptions`**: default ctor all-defaults, null-args→defaults, explicit values, zero/negative MaxConcurrency throws ArgumentOutOfRangeException (F-2 regression pin), one is minimum valid, zero AcquisitionTimeout preserved, with-expression.
- **`RateLimitRejectedException`**: all three ctor variants, IsException subtype.
- **`ResilientPipelineTests` (extended)**: TimeoutException-maps-ServiceUnavailable, RateLimitRejectedException-maps-TooManyRequests, PassThrough-maps-exceptions, PassThrough-success-ok, PassThrough-is-singleton, PassThrough-no-retry, canonical-full-stack-order-tracing.
- **`ResilientPipelineBuilderTests` (extended)**: UseTimeout-null/explicit, UseTimeout-twice-two-positions, UseRateLimiter-null/explicit, UseRateLimiter-keyed-DI.
- **`PipelineCompositionTests`**: 6-layer nesting adversarial tests covering all canonical configurations and the order-of-operations proofs: canonical-full-stack-RL-Tt-R-CB-Ta (flaky succeeds, permanently-down exhausts), CB↔R order-sensitivity proof (R→CB real-op-called-once-vs CB→R real-op-called-N-times), total-timeout-outside-retry-terminates-loop, per-attempt-timeout-inside-retry-retried-then-succeeds, RL-outermost-short-circuits-inner-layers, RL-rejection-not-retried-when-inside-retry, SF-outermost-dedup-across-full-stack, SF-distinct-keys-distinct-executions, caller-cancellation-through-deep-stack-maps-to-Canceled, PassThrough-zero-layers-no-retry.
- **`ResilientPipelineServiceCollectionExtensionsTests` (extended)**: `Dispose_InlineOptionsPipeline_DisposesOwnedRateLimiter` — F-1 regression (proves `ServiceProvider.Dispose` propagates through the keyed singleton pipeline to the inline-owned `RateLimiter`/`SemaphoreSlim`).

Run: `dotnet test public/packages/dotnet/tests`

CLI coverage one-liner:

```bash
cd public/packages/dotnet/tests
coverlet bin/Debug/net10.0/DcsvIo.D2.Tests.dll \
  --target dotnet --targetargs "test --no-build" \
  --include "[DcsvIo.D2.Resilience]*" \
  --exclude-by-attribute "GeneratedCode" \
  --format cobertura --output ./coverage/resilience.cobertura.xml
```
