<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.Resilience — Pipeline Scenarios

> Parent: [`README.md`](README.md)

Different consumers want different things from the resilient pipeline. Pick the recipe whose tradeoffs match the use case; the rationale + real-world example below each helps you confirm fit.

---

## Layer-order quick reference

The canonical order and the key decisions that flow from it:

```
[Singleflight →] RateLimiter → TotalTimeout → Retry → CircuitBreaker → PerAttemptTimeout
```

The two orderings for Retry↔CircuitBreaker have different semantics. Choose by use case:

| Shape | Order | CB sees | Opens when | Best for |
|---|---|---|---|---|
| **Restart-recovery** | `R → CB` | Each retry is a separate CB execution | After N individual attempt failures | Rolling-deploy read calls, gRPC S2S |
| **Upstream-protecting** | `CB → R` | One full retry budget = one CB execution | After N complete retry sequences fail | Fragile third-party writes (Resend, Twilio) |

The `CircuitOpenException` is treated as **transient** by the default retry classifier, so `R → CB` naturally backs off through the CB cooldown. Callers MUST size `MaxAttempts + backoff` to span `CooldownDuration`; if retries exhaust before the breaker recovers, the result is `ServiceUnavailable`.

**Anti-patterns to avoid:**
- `RL` innermost — burns retry/CB/timeout resources before rejecting.
- Per-attempt-timeout **outside** Retry — acts as a second total-timeout, not a per-attempt bound.
- `Singleflight` on mutating ops — merges distinct side-effects (silent data loss).

Full rationale for each position: [`README.md — Order matters`](README.md#order-matters--the-canonical-layer-order-and-the-why).

---

## 1. Fail-fast hot-path read (graceful degradation)

**Goal:** caller is on the request hot path, has a working fallback (e.g. "no WhoIs context for this request — proceed without country/ASN data"), and absolutely cannot afford retry latency.

```csharp
services.AddResilientPipeline<string, T>("ipinfo", p => p
    .UseSingleflight("ipinfo")
    .UseCircuitBreaker("ipinfo"));
// no retry — fail-open is the contract
```

The CB rides through sustained outages by fast-failing; SF stops a request burst from triggering N identical expensive calls. No retry means worst-case latency stays bounded by the operation's own timeout.

**Real-world:** Edge's WhoIs-enrichment middleware calling ipinfo on every HTTP request. Failure returns `null`, downstream rate-limiter and risk-scorer handle the absence.

## 2. Restart-tolerant cross-service call (the most common case for cross-service traffic)

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

## 3. Backend-friendly retried write (fragile upstream, must succeed)

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

## 4. Burst-dedupe only

**Goal:** the operation is expensive and concurrent identical requests are common, but you don't need either retry or breaker — failures are surfaced to the caller as-is.

```csharp
services.AddResilientPipeline<string, T>("jwks-fetch", p => p.UseSingleflight("jwks-fetch"));
```

100 concurrent identical requests collapse to 1 upstream call; everyone shares the result (or the failure, in which case all get the same exception → same D2Result mapping).

**Real-world:** JWKS fetch on cold cache. Token validation under burst traffic shouldn't trigger 100 simultaneous JWKS fetches.

## 5. Outbox-style eventual delivery (essentially unbounded retry)

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

## 6. Bounded request-deadline retry (sync request with TTL)

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

## 7. D2Result-only pass-through

**Goal:** you want the exception-to-D2Result mapping but nothing else. Useful for purely-internal calls where there's no upstream to be resilient against, but you want consistent error handling.

```csharp
services.AddResilientPipeline<string, T>("internal-passthrough", p => { });
```

Zero layers. Every exception still gets mapped per the documented rules; success goes to `D2Result.Ok`. Same call surface as everywhere else — handlers don't need a second code path for "internal call" vs "resilient call."

**Real-world:** Wrapping a CPU-bound in-process operation that throws on bad input, when you want the result shape to match handler conventions without inventing per-handler try/catch.

## 8. Multi-criticality dispatch (multiple pipelines per consumer)

**Goal:** the same operation (e.g. publishing to RabbitMQ) needs different resilience policies depending on the importance of THIS particular call. Critical events MUST land; routine events should land; diagnostic events can drop.

The keyed-services discipline makes this trivial — register one pipeline per criticality tier, share a CB across tiers via key reference, and inject all three pipelines into the consumer via `[FromKeyedServices(...)]`. No router type needed.

```csharp
// app layer — single source of truth for the audit module's keys.
namespace DcsvIo.D2.Private.Audit.App;

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

**Real-world:** DcsvIo.D2.Private.Audit client chooses by event severity. Critical security events (sign-in, key rotation, admin actions) get the unbounded-retry treatment so they MUST land. Routine business events get the backend-friendly composition. Diagnostic / verbose events fire-and-forget with no retry to protect the audit pipeline from being overwhelmed during an incident — exactly when audit volume spikes.

The pattern generalizes to any domain client where one operation has multiple criticality tiers: D2.Notifications by user-facing-importance, D2.Courier by transactional-vs-marketing, file uploads by user-tier, etc.

## 9. Canonical full-stack composition (all five layers)

**Goal:** maximum protection for a critical cross-process call — the canonical composition (rate-limiter → total-timeout → retry → circuit-breaker → per-attempt-timeout).

```csharp
// Canonical ordering (outer → inner):
// RateLimiter → TotalTimeout → Retry → CircuitBreaker → PerAttemptTimeout
services.AddKeyedSingleton<CircuitBreaker<T>>("svc", (_, _) => new(_ => false));
services.AddResilientPipeline<string, T>("svc", p => p
    .UseRateLimiter(new RateLimiterOptions(maxConcurrency: 20))       // outermost: admission control
    .UseTimeout(new TimeoutOptions(TimeSpan.FromSeconds(30)))          // total: bounds all retries
    .UseRetries(new()
    {
        MaxAttempts = 3,
        BaseDelayMs = 500,
        MaxDelayMs = 10_000,
    })
    .UseCircuitBreaker("svc")
    .UseTimeout(new TimeoutOptions(TimeSpan.FromSeconds(8))));         // per-attempt deadline
```

Layer semantics in this composition:
- **RateLimiter** (outermost): prevents stampede from overwhelming the upstream when it is slow; a rejected caller gets `TooManyRequests` immediately.
- **TotalTimeout**: if the full retry sequence takes > 30 s, abort — caller sees `ServiceUnavailable`.
- **Retry**: up to 3 attempts with exponential backoff; backs off through a transient `TimeoutException` (per-attempt expiry) or `CircuitOpenException`.
- **CircuitBreaker**: protects the upstream from sustained pressure; opens after consecutive failures so retries back off through cooldown.
- **PerAttemptTimeout**: each single attempt is bounded to 8 s; a slow upstream doesn't burn the whole retry budget on a single attempt.

When Singleflight is also used (read-by-key, hot-path), place it outermost so all callers for the same key share one concurrency permit and one retry sequence:

```csharp
services.AddResilientPipeline<string, T>("svc", p => p
    .UseSingleflight("svc")
    .UseRateLimiter(...)
    .UseTimeout(total)
    .UseRetries(...)
    .UseCircuitBreaker("svc")
    .UseTimeout(perAttempt));
```

**Real-world:** any critical gRPC service-to-service call where you want: dedup of hot keys, capped concurrent pressure, a total budget, retries across rolling deploys, a breaker for sustained outages, and a per-attempt clock.

## 10. Bypass with D2Result mapping (PassThrough)

**Goal:** caller explicitly wants no resilience overhead (latency-critical hot path, internal operation that never fails transiently) but still needs the `D2Result<T>` return shape for consistent error handling.

```csharp
// At the call site — no DI registration needed.
var result = await ResilientPipeline<string, T>.PassThrough.ExecuteAsync(
    key,
    ct => SomeFastInternalOp(ct),
    ct);
```

Or inject it as the per-call override when the caller wants to override a default pipeline:

```csharp
// Generated client method signature:
// ValueTask<D2Result<T>> GetAsync(string key, CancellationToken ct,
//     ResilientPipeline<string, T>? pipeline = null)
var result = await client.GetAsync(key, ct, pipeline: ResilientPipeline<string, T>.PassThrough);
```

Zero layers: no retry, no CB, no timeout, no rate-limiter. Every exception is still mapped to `D2Result<T>` per the documented rules (so callers don't need their own try/catch). The `PassThrough` singleton is the named equivalent of `new ResilientPipeline<string, T>()`.

**Real-world:** a handler that calls a pure in-process helper (no network) but wants to keep the same `if (result.Failed) return result;` call-site convention. Or a generated client where the caller explicitly knows this particular invocation is on an already-healthy, local connection.

## 11. Mutating command — conservative (no SF, no transport-retry, or explicit retry-safety caveat)

**Goal:** a `Create*` / `Update*` / `Delete*` call to an upstream over gRPC or HTTP where each call is a distinct business intent. Duplicating the side-effect (by retrying) would be incorrect.

```csharp
// No Singleflight — two callers wanting to CreateUser should produce two users.
// No Retry layer — unless the upstream guarantees idempotency (natural-key upsert,
// idempotency-key header, or the operation is provably safe to re-issue).
// CB + RL still protect against a down upstream and admission control.
services.AddKeyedSingleton<CircuitBreaker<T>>(key, (_, _) => new(isFailure: _ => false));
services.AddResilientPipeline<string, T>(key, p => p
    .UseRateLimiter(new RateLimiterOptions(maxConcurrency: 10))
    .UseTimeout(new TimeoutOptions(TimeSpan.FromSeconds(15)))
    .UseCircuitBreaker(key));
// No UseRetries — mutations must not be duplicated.
```

If the upstream DOES guarantee idempotency (e.g. idempotency-key header), it is safe to add a conservative `UseRetries` with a small budget and no jitter:

```csharp
.UseRetries(new()
{
    MaxAttempts = 2,         // initial attempt + one retry only
    BaseDelayMs = 500,
    MaxDelayMs = 500,
    Jitter = false,          // deterministic timing for easier reasoning
})
```

**Real-world:** `CreateOrg`, `RotateKey`, `PublishEvent`. SF would silently merge two org-creation attempts for the same caller into one (one org produced, not two). CB + RL still guard against a down upstream. Retry only if the team has explicitly validated idempotency at the upstream.

## 12. Total-timeout vs per-attempt-timeout — illustrating the difference

**Goal:** demonstrate the behavioral difference between placing `UseTimeout` outside Retry (total-budget) vs inside Retry (per-attempt).

### Total-timeout outside retry (bounds the entire user-facing budget)

```csharp
// A 30 s total budget with 3 retry attempts.
// If attempts 1 + 2 each take 13 s, the 3rd attempt never starts — total budget fires.
services.AddResilientPipeline<string, T>(key, p => p
    .UseTimeout(new TimeoutOptions(TimeSpan.FromSeconds(30)))   // TOTAL: wraps all retries
    .UseRetries(new() { MaxAttempts = 3, BaseDelayMs = 200 })
    .UseCircuitBreaker(key));
```

### Per-attempt-timeout inside retry (each individual attempt bounded independently)

```csharp
// Each attempt bounded to 5 s. If attempt 1 times out → TimeoutException (transient) →
// Retry fires a second attempt. Total elapsed can still be MaxAttempts × 5 s.
services.AddResilientPipeline<string, T>(key, p => p
    .UseRetries(new() { MaxAttempts = 3, BaseDelayMs = 200 })   // retry is OUTER
    .UseCircuitBreaker(key)
    .UseTimeout(new TimeoutOptions(TimeSpan.FromSeconds(5))));   // PER-ATTEMPT: inside retry
```

### Both together (the canonical full-stack)

```csharp
// Total-timeout + per-attempt-timeout. The total clock bounds the SLA; the per-attempt
// clock prevents a single slow attempt from burning the entire retry budget.
services.AddResilientPipeline<string, T>(key, p => p
    .UseRateLimiter()
    .UseTimeout(new TimeoutOptions(TimeSpan.FromSeconds(30)))   // total
    .UseRetries(new() { MaxAttempts = 3, BaseDelayMs = 500 })
    .UseCircuitBreaker(key)
    .UseTimeout(new TimeoutOptions(TimeSpan.FromSeconds(8))));  // per-attempt
// Worst case: 3 × 8 s = 24 s per-attempt budget fits inside the 30 s total (with margin
// for backoff). If a single attempt takes 10 s (>8 s), it is cut off and retried.
// If total elapsed exceeds 30 s, the retry loop is terminated regardless of attempt count.
```

**Real-world:** any call with a hard user-facing SLA. The total-timeout enforces the SLA; the per-attempt-timeout prevents a single slow upstream response from burning the whole budget.
