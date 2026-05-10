<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Shared.Resilience

> Parent: [`server/shared/dotnet/`](../README.md)

Resilience primitives for protecting outbound calls — `RetryHelper` (with optional `D2Result` awareness), `CircuitBreaker<T>`, and `Singleflight<TKey, TValue>`. Lock-free where possible (`Interlocked` operations, `ConcurrentDictionary`); test seams baked in (clock + delay overrides).

Depends only on `D2.Shared.Result` (for the `D2Result`-aware retry overload).

> **Why not Polly?** Most of our outbound boundaries are NOT HTTP (RabbitMQ publishes, EF Core, Redis via StackExchange, internal handler chains, SeaweedFS via SDK). Polly's main "free win" — its HttpClientFactory integration via `AddStandardResilienceHandler()` — applies cleanly to gRPC (since `Grpc.Net.Client` rides on `HttpClient`) and external HTTP APIs, but the HTTP-level integration only sees HTTP 200 + trailing gRPC status codes; retry-on-`StatusCode.Unavailable` requires custom predicates anyway. With <500 LOC of working v1 logic + first-class `D2Result.IsTransientRetryable` integration, owning the primitives is cheaper than wrapping Polly.

---

## File layout

| Path | Contents |
|---|---|
| `Retry/RetryHelper.cs` | Static `RetryAsync<T>` (generic) and `RetryD2ResultAsync<TData>` (D2Result-aware overload). Internal `IsTransientException` classifier and `CalculateDelay` math. |
| `Retry/RetryOptions.cs` + `Retry/RetryDefaults.cs` | `RetryOptions<T>` record — `MaxAttempts`, `BaseDelayMs`, `BackoffMultiplier`, `MaxDelayMs`, `Jitter`, `ShouldRetry`, `IsTransient`, `DelayFunc`. Defaults centralized in the non-generic `RetryDefaults` peer (single SoT, no per-T duplication). |
| `CircuitBreaker/CircuitBreaker.cs` | `CircuitBreaker<T>` — three-state Closed / Open / Half-Open with lock-free state transitions. |
| `CircuitBreaker/CircuitBreakerOptions.cs` | `CircuitBreakerOptions` — `FailureThreshold`, `CooldownDuration`, `NowFunc` (test clock). Owns the single source of truth for breaker defaults; the breaker reads from a parameterless Options instance when nothing is supplied. |
| `CircuitBreaker/CircuitState.cs` | `CircuitState` enum — `Closed`, `Open`, `HalfOpen`. |
| `CircuitBreaker/CircuitOpenException.cs` | Thrown by `ExecuteAsync` when the circuit is open and no fallback is supplied. |
| `Singleflight/Singleflight.cs` | `Singleflight<TKey, TValue>` — deduplicates concurrent in-flight async operations by key. NOT a cache: keys are removed once the operation completes. |
| `Pipeline/IResilientLayer.cs` | The decorator interface — one `WrapAsync(key, next, ct)` method. |
| `Pipeline/{Singleflight,CircuitBreaker,Retry}Layer.cs` | The three concrete layer wrappers around the primitives above. |
| `Pipeline/ResilientPipeline.cs` | Composes layers in outer-first order; one `ExecuteAsync(key, op, ct)` returning `D2Result<TValue>` (never throws — every exception is mapped to a result). |
| `Pipeline/IResilientPipelineBuilder.cs` + `ResilientPipelineBuilder.cs` | Fluent registration DSL — `.UseSingleflight().UseCircuitBreaker().UseRetries(opts)` etc. |
| `Pipeline/ResilientPipelineServiceCollectionExtensions.cs` | `AddResilientPipeline<TKey, TValue>(p => ...)` extension on `IServiceCollection`. |

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

> **Telemetry is consumer-owned.** This lib emits no spans / metrics / logs of its own — by design, to stay free of `System.Diagnostics.DiagnosticSource` and `Microsoft.Extensions.Logging` transitive deps. The `onStateChange` callback is the canonical observability seam for circuit-breaker transitions; the `RetryHelper.RetryAsync` `logger` parameter is the seam for retry attempts. Pipeline-level observability (per-layer counters, per-attempt spans) is future work.

Test seams:

- `CircuitBreakerOptions.NowFunc` — override for the monotonic-millisecond clock (default `Environment.TickCount64`). Tests use a `FakeClock` to advance time deterministically without `Task.Delay`.

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

**This is NOT a cache.** Once the operation completes, the key is removed and the next call re-runs the operation. Use Singleflight to prevent thundering-herd duplication of in-progress work, then layer a real cache (`D2.Shared.Caching.Memory`, `D2.Shared.Caching.Redis`, etc.) on top of the singleflight call site if you want persistent reuse of the result.

#### When to use Singleflight — and when NOT

| Use SF when… | Don't use SF when… |
|---|---|
| Multiple concurrent callers ask for the **same logical thing** by key | Each call is a **distinct intent** that should produce a distinct effect |
| Operation is **idempotent** — running it once and sharing the result is correct for everyone waiting | Operation has a **per-call side effect** (publish a message, send an email, write a row, charge a card) |
| You're preventing a **thundering-herd / cache-miss stampede** on a hot key | Two callers happen to share a transport but represent different business events |

Concrete:

- ✅ **WhoIs / IPinfo lookup by IP** — 50 requests for `1.2.3.4` collapse to 1 upstream call.
- ✅ **JWKS fetch** — every concurrent token validation wants the same document.
- ✅ **Reference-data lookups** (currencies, countries, feature-flag manifests).
- ✅ **Cache-miss read for a hot key** (config-by-name, user-by-id during a request burst).
- ❌ **Audit / event publishes** — each event is unique data; SF would silently drop events whose key collided.
- ❌ **Email / SMS / push delivery** — each `Notify` is a discrete intent. Two callers asking to email Alice = two emails.
- ❌ **gRPC / HTTP writes** (`Create*`, `Update*`, `Delete*`) — SF in front deletes work.
- ❌ **Outbox / queue drain publishers** — dedup belongs upstream (in the outbox itself), not in the publisher.

Heuristic: if the answer to *"if two callers ask, is one shared answer correct for both?"* is **yes**, SF fits. If the answer is *"two callers means two side effects we want"*, SF is a bug.

Behavioral guarantees:

- **`TKey` constraint**: `notnull`. Strings, GUIDs, value-types, custom hashable records all work.
- **Per-caller cancellation does NOT affect siblings.** When you pass a `CancellationToken`, only YOUR wait is cancellable. The shared operation runs with `CancellationToken.None`, so one caller bailing out cannot poison the result for everyone else sharing it.
- **Exception propagation**: an operation throw propagates to ALL waiting callers (they share the same `Task`). The key is still removed in the `finally` block, so the next call after the throw starts a fresh operation.
- **Lazy initialization** via `Lazy<Task<TValue>>` with `LazyThreadSafetyMode.ExecutionAndPublication` — the operation is started exactly once per key even under aggressive concurrency.
- **`Size`** property exposes the current in-flight count (instantaneous; useful for metrics).

---

## When to reach for which

| Need | Tool |
|---|---|
| Backoff-and-retry around a flaky external call | `RetryHelper.RetryAsync` |
| Backoff-and-retry around a `D2Result`-returning handler chain | `RetryHelper.RetryD2ResultAsync` |
| Avoid hammering a confirmed-down upstream while it recovers | `CircuitBreaker<T>` |
| Avoid the "five concurrent first requests trigger five identical expensive lookups" stampede | `Singleflight<TKey, TValue>` |
| Compose two or three of the above behind a single call site that returns a `D2Result` | `ResilientPipeline<TKey, TValue>` (see Pipeline section below) |

The three primitives compose naturally. The `Pipeline` namespace is the canonical way to do this composition; reach for the raw primitives only when you need direct control or have an unusual layering requirement.

---

## Pipeline — the high-level composition surface

`ResilientPipeline<TKey, TValue>` is a configured pipeline of `IResilientLayer<TKey, TValue>` decorators that:

- composes layers in **outer-first order** (first layer wraps everything else)
- exposes ONE call: `ExecuteAsync(key, operation, ct)` returning `D2Result<TValue>`
- **never throws** — every terminating exception is converted to a `D2Result` per the documented mapping (CircuitOpen → ServiceUnavailable, caller-canceled → Canceled, transient that slipped past layers → ServiceUnavailable, anything else → UnhandledException)

### Two-tier API: fluent at registration, dead-simple at call site

The intent is that **client-lib authors** configure the pipeline once at the lib's composition root using the fluent DSL; **handlers inside the lib** see only the one-line call surface; **callers of the lib** see nothing — resilience is invisible.

#### Tier 1 — registration (lib composition root, fluent)

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

#### Tier 2 — call site (handler, brain-dead simple)

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

### Layer order IS the protection semantic — pick what you mean

The fluent chain order = layer order in the resulting pipeline (outer-first). The two canonical full-stack compositions:

```csharp
// Upstream-protecting (default for fragile upstreams like Resend / Twilio).
// CB sees ONE execution per full retry budget; opens after N failed retry
// sequences. Backoff between attempts gives the upstream air to recover.
// No SF — each email is a distinct delivery intent (see "When to use
// Singleflight" above).
const string courier = "courier-resend";
services.AddResilientPipeline<string, EmailSendResult>(courier, p => p
    .UseCircuitBreaker(courier)
    .UseRetries(new() { MaxAttempts = 4 }));

// Restart-recovery (the most common cross-service-call case for read-by-key
// gRPC). Each retry is a SEPARATE CB execution. When CB opens,
// CircuitOpenException is treated as transient by the default classifier —
// retry backs off through it; if the breaker's cooldown elapses during the
// backoff, the next attempt finds the breaker probing / closed and succeeds.
// SF is appropriate here: many concurrent uploads may resolve the SAME
// context-key, and one shared answer is correct for all of them.
//
// CALLERS MUST size MaxAttempts + backoff to span the breaker's
// CooldownDuration; otherwise retries exhaust on perpetual CO and the
// pipeline returns ServiceUnavailable.
const string edgeContext = "edge-context-resolve";
services.AddResilientPipeline<string, ContextDocument>(edgeContext, p => p
    .UseSingleflight(edgeContext)
    .UseRetries(new() { MaxAttempts = 4 })
    .UseCircuitBreaker(edgeContext));
```

Both are valid. The lib author chooses based on whether the priority is "don't kill the upstream" (retry-inside) or "ride out an upstream restart" (retry-outside). The retry-outside composition pays additional caller-side latency in exchange for resilience to brief upstream outages.

### Skipping layers

Each `Use*` is independent — call zero, one, two, or three. A pipeline with no layers still does the exception → result mapping (handy for "I want D2Result but no actual resilience"):

```csharp
services.AddResilientPipeline<string, T>("internal-op", p => { });                                    // pure exception-to-D2Result mapper
services.AddResilientPipeline<string, T>("internal-op", p => p.UseRetries());                         // retry-only
services.AddResilientPipeline<string, T>("internal-op", p => p.UseCircuitBreaker("internal-op").UseRetries()); // no SF
```

### Cross-pipeline shared primitives

Sometimes two pipelines should SHARE a primitive (e.g. multi-criticality audit pipelines all sharing one broker-level CB so any tier's failures count toward the same breaker state). Just register the shared primitive under its own key and reference that key from each pipeline:

```csharp
services.AddKeyedSingleton<CircuitBreaker<PublishAck>>("audit-broker", (_, _) => /* configured */);

services.AddResilientPipeline<string, PublishAck>("audit-critical", p => p
    .UseRetries(new() { MaxAttempts = 50 })
    .UseCircuitBreaker("audit-broker"));   // shared CB

services.AddResilientPipeline<string, PublishAck>("audit-routine", p => p
    .UseRetries(new() { MaxAttempts = 4 })
    .UseCircuitBreaker("audit-broker"));   // same shared CB
```

Now both pipelines feed failures into the same breaker — when the broker is sick, ALL audit publishes fast-fail uniformly. The shared topology is grep-able: `"audit-broker"` shows every consumer.

### Scenarios — picking the right composition

Different consumers want different things. Pick the recipe whose tradeoffs match the use case; the rationale + real-world example below each helps you confirm fit.

#### 1. Fail-fast hot-path read (graceful degradation)

**Goal:** caller is on the request hot path, has a working fallback (e.g. "no WhoIs context for this request — proceed without country/ASN data"), and absolutely cannot afford retry latency.

```csharp
services.AddResilientPipeline<string, T>("ipinfo", p => p
    .UseSingleflight("ipinfo")
    .UseCircuitBreaker("ipinfo"));
// no retry — fail-open is the contract
```

The CB rides through sustained outages by fast-failing; SF stops a request burst from triggering N identical expensive calls. No retry means worst-case latency stays bounded by the operation's own timeout.

**Real-world:** Edge's WhoIs-enrichment middleware calling ipinfo on every HTTP request. Failure returns `null`, downstream rate-limiter and risk-scorer handle the absence.

#### 2. Restart-tolerant cross-service call (the most common case for cross-service traffic)

**Goal:** caller can wait a few seconds during an upstream rolling restart but ultimately needs the call to succeed.

For a **read-by-key gRPC** call where the same key is hot (e.g. context resolution for many concurrent uploads), SF is appropriate — one shared resolve answers everyone:

```csharp
services.AddResilientPipeline<string, ContextDocument>("edge-context-resolve", p => p
    .UseSingleflight("edge-context-resolve")
    .UseRetries(new() { MaxAttempts = 5, BaseDelayMs = 500, MaxDelayMs = 10_000 })
    .UseCircuitBreaker("edge-context-resolve"));
// retry OUTSIDE CB — CO is treated as transient, retry backs off through it
```

For a **mutation** (`Create*` / `Update*`) call drop the SF layer — each call is a distinct intent.

The CB opens fast on upstream failure; the retry layer treats `CircuitOpenException` as transient and waits through the cooldown; a later attempt finds the breaker probing/closed and succeeds. Backoff between 5 attempts gives 4 intervals (≈ 0.5 + 1 + 2 + 4 = 7.5 s with full jitter, halved on average ≈ 3.75 s) which MUST exceed the CB cooldown, otherwise retries exhaust on perpetual CO and you fall back to ServiceUnavailable.

**Real-world:** D2.Files handling an upload, calling Edge's gRPC for context-key resolution during an Edge rolling deploy. Many concurrent uploads referencing the same context dedup to one upstream resolve (SF); the retry layer rides through the brief restart window. The user's upload pays a few seconds of added latency in exchange for not error-pageing.

#### 3. Backend-friendly retried write (fragile upstream, must succeed)

**Goal:** the upstream is fragile (third-party API with rate limits, intermittent 5xxs) and the operation MUST eventually succeed. Don't pile retries on top of a struggling upstream.

```csharp
services.AddResilientPipeline<string, EmailSendResult>("courier-resend", p => p
    .UseCircuitBreaker("courier-resend")
    .UseRetries(new() { MaxAttempts = 4, BaseDelayMs = 1000, BackoffMultiplier = 3.0 }));
// retry INSIDE CB — CB sees one execution per full retry budget
// no SF — each email is a discrete delivery intent (two callers asking to
// email Alice = two emails). SF here would silently merge unrelated sends.
```

Each "call" from the CB's POV is one full retry sequence. Failure threshold is reached after N call sequences fail (not N raw pings), so the CB doesn't open prematurely on a single-call retry burst. When the CB does open, the upstream gets real recovery time.

**Real-world:** D2.Courier delivery to Resend / Twilio. Provider transient errors retry; sustained provider outages trip the breaker so we stop hammering a clearly-broken provider.

#### 4. Burst-dedupe only

**Goal:** the operation is expensive and concurrent identical requests are common, but you don't need either retry or breaker — failures are surfaced to the caller as-is.

```csharp
services.AddResilientPipeline<string, T>("jwks-fetch", p => p.UseSingleflight("jwks-fetch"));
```

100 concurrent identical requests collapse to 1 upstream call; everyone shares the result (or the failure, in which case all get the same exception → same D2Result mapping).

**Real-world:** JWKS fetch on cold cache. Token validation under burst traffic shouldn't trigger 100 simultaneous JWKS fetches.

#### 5. Outbox-style eventual delivery (essentially unbounded retry)

**Goal:** background relay (outbox table → message broker) where the operation MUST land eventually and there's no caller waiting on latency.

```csharp
services.AddResilientPipeline<string, T>("outbox-relay", p => p
    .UseRetries(new()
    {
        MaxAttempts = 50,
        BaseDelayMs = 1000,
        BackoffMultiplier = 1.5,
        MaxDelayMs = 60_000,
    })
    .UseCircuitBreaker("outbox-relay"));
// no SF — outbox dedupes upstream of this; very long retry budget; CB-around-retry as backstop
```

Total budget is generous (minutes), so the CB cooldown comfortably fits within retry attempts. If the upstream is down for 10+ minutes, the relay surfaces ServiceUnavailable to the outbox driver, which can flag the row for operator attention without losing the message.

**Real-world:** Outbox-to-RabbitMQ publisher. The driver runs every N seconds; each tick processes outbox rows through this pipeline. Rows that exhaust the retry budget stay in the outbox for the next tick.

#### 6. Bounded request-deadline retry (sync request with TTL)

**Goal:** caller has a hard deadline (request timeout, end-user-facing operation with budget). Retry within that budget but never exceed it.

```csharp
services.AddResilientPipeline<string, T>("sync-deadline", p => p
    .UseRetries(new()
    {
        MaxAttempts = 3,
        BaseDelayMs = 200,
        MaxDelayMs = 1000,
        Jitter = false,                 // deterministic timing for budget reasoning
    }));
// no CB — failures are surfaced; caller controls deadline via the CT
```

Total worst-case ≈ 200 + 400 + 800 = 1400 ms (no jitter). Caller passes a CancellationToken with their deadline; OCE flows through as `Canceled`. Use when you want retry but not the breaker semantics — typical for handlers that already live behind their own gateway-level CB.

**Real-world:** Synchronous gRPC call from one service handler to another, where the outer request has a 2s deadline. Retry to absorb a single transient blip without busting the deadline.

#### 7. D2Result-only pass-through

**Goal:** you want the exception-to-D2Result mapping but nothing else. Useful for purely-internal calls where there's no upstream to be resilient against, but you want consistent error handling.

```csharp
services.AddResilientPipeline<string, T>("internal-passthrough", p => { });
```

Zero layers. Every exception still gets mapped per the documented rules; success goes to `D2Result.Ok`. Same call surface as everywhere else — handlers don't need a second code path for "internal call" vs "resilient call."

**Real-world:** Wrapping a CPU-bound in-process operation that throws on bad input, when you want the result shape to match handler conventions without inventing per-handler try/catch.

#### 8. Multi-criticality dispatch (multiple pipelines per consumer)

**Goal:** the same operation (e.g. publishing to RabbitMQ) needs different resilience policies depending on the importance of THIS particular call. Critical events MUST land; routine events should land; diagnostic events can drop.

The keyed-services discipline makes this trivial — register one pipeline per criticality tier, share a CB across tiers via key reference, and inject all three pipelines into the consumer via `[FromKeyedServices(...)]`. No router type needed.

```csharp
// app layer — single source of truth for the audit module's keys.
namespace D2.Audit.App;

public static class AuditServiceKeys
{
    public const string CRITICAL      = "audit-critical";
    public const string ROUTINE       = "audit-routine";
    public const string DIAGNOSTIC    = "audit-diagnostic";
    public const string SHARED_BROKER = "audit-broker";   // CB shared across tiers
}
```

```csharp
// Composition root. No Singleflight on any tier — each audit event is unique
// data, deduping by key would silently drop business records.
services.AddKeyedSingleton<CircuitBreaker<PublishAck>>(
    AuditServiceKeys.SHARED_BROKER, (_, _) => /* configured */);

services.AddResilientPipeline<string, PublishAck>(AuditServiceKeys.CRITICAL, p => p
    .UseRetries(new() { MaxAttempts = 50, BaseDelayMs = 1000, MaxDelayMs = 60_000 })
    .UseCircuitBreaker(AuditServiceKeys.SHARED_BROKER));

services.AddResilientPipeline<string, PublishAck>(AuditServiceKeys.ROUTINE, p => p
    .UseCircuitBreaker(AuditServiceKeys.SHARED_BROKER)        // shared
    .UseRetries(new() { MaxAttempts = 4 }));

services.AddResilientPipeline<string, PublishAck>(AuditServiceKeys.DIAGNOSTIC, p => p
    .UseCircuitBreaker(AuditServiceKeys.SHARED_BROKER));      // shared, no retry — drop on failure
```

```csharp
// Consumer — three keyed injections, dispatch by severity.
public sealed class AuditClient(
    [FromKeyedServices(AuditServiceKeys.CRITICAL)]   ResilientPipeline<string, PublishAck> critical,
    [FromKeyedServices(AuditServiceKeys.ROUTINE)]    ResilientPipeline<string, PublishAck> routine,
    [FromKeyedServices(AuditServiceKeys.DIAGNOSTIC)] ResilientPipeline<string, PublishAck> diagnostic,
    IRabbitPublisher rabbit)
{
    public ValueTask<D2Result<PublishAck>> PublishAsync(AuditEvent ev, CancellationToken ct)
    {
        var pipeline = ev.Severity switch
        {
            AuditSeverity.Critical    => critical,
            AuditSeverity.Routine     => routine,
            AuditSeverity.Diagnostic  => diagnostic,
            _                         => routine,
        };

        return pipeline.ExecuteAsync(ev.Id, c => rabbit.PublishAsync(ev, c), ct);
    }
}
```

Three keyed pipelines share one broker-level CB (`SHARED_BROKER`) but differ in retry budget — critical retries forever, routine has a small budget, diagnostic drops on first failure. Grep `AuditServiceKeys.SHARED_BROKER` to find every consumer of the shared CB; grep `AuditServiceKeys.CRITICAL` to find each tier's wiring. No router/wrapper type — DI carries the dispatch.

**Real-world:** D2.Audit client chooses by event severity. Critical security events (sign-in, key rotation, admin actions) get the unbounded-retry treatment so they MUST land. Routine business events get the backend-friendly composition. Diagnostic / verbose events fire-and-forget with no retry to protect the audit pipeline from being overwhelmed during an incident — exactly when audit volume spikes.

The pattern generalizes to any domain client where one operation has multiple criticality tiers: D2.Notifications by user-facing-importance, D2.Courier by transactional-vs-marketing, file uploads by user-tier, etc.

### Adding new layers later

`IResilientLayer<TKey, TValue>` is a single-method interface. Future layers (timeout, bulkhead, rate-limit, telemetry, etc.) plug in as additional `Use*` methods on the builder + new `XxxLayer` implementations — no breaking changes to existing pipelines or call sites.

---

## Tests

`server/shared/dotnet/tests/Unit/Resilience/` — adversarial coverage at 100% lines + 100% branches. Categories:

- **`RetryHelper`**: full transient-classifier matrix (HTTP 5xx / 429 / 408 / non-transient codes / null status / TaskCanceled / Timeout / Socket / arbitrary). Happy path, throws-then-succeeds, throws-every-attempt-exhaustion, ShouldRetry-true-then-false, ShouldRetry-always-true (last-value wins on exhaustion), alternating throw+return (last terminator wins), pre-canceled token, OCE-from-ct (NOT classified transient), DelayFunc-invoked-between-retries.
- **`RetryD2ResultAsync`**: default predicate retries `ServiceUnavailable`, default predicate does NOT retry `NotFound`, caller-`ShouldRetry`-override wins, null-options behavior.
- **`CalculateDelay`** (internal): zero-index returns base, multiplier applied, max-delay clamp, exponent overflow clamp, jitter range property (200 samples in `[0, calculated)`).
- **`CircuitBreaker`**: initial state, single success/failure, threshold transition Closed→Open, mixed exception+value-failure threshold, success resets counter, Open without/with fallback, Open→HalfOpen on cooldown, HalfOpen probe success closes, HalfOpen probe failure (exception OR value-failure) reopens, HalfOpen probe-lock (concurrent caller gets fallback / throws), `Reset` from Open fires callback, `Reset` from Closed is no-op, callback-null branches on every transition.
- **`Singleflight`**: single-call sanity, concurrent-callers-same-key dedup (1 invocation, 3 returns), concurrent-callers-different-keys both run, sequential-calls-same-key re-run (proves no caching), exception propagation to all waiters + key removed for retry, per-caller cancellation does NOT affect siblings, no-cancellable-token fast path, non-string key (Guid).
- **`CircuitBreakerOptions` / `RetryOptions` / `CircuitOpenException`**: defaults, init-only overrides, exception constructor variants.

Run: `dotnet test server/shared/dotnet/tests`

CLI coverage one-liner:

```bash
cd server/shared/dotnet/tests
coverlet bin/Debug/net10.0/D2.Shared.Tests.dll \
  --target dotnet --targetargs "test --no-build" \
  --include "[D2.Shared.Resilience]*" \
  --exclude-by-attribute "GeneratedCode" \
  --format cobertura --output ./coverage/resilience.cobertura.xml
```
