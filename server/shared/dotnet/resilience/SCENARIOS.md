<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Shared.Resilience — Pipeline Scenarios

> Parent: [`README.md`](README.md)

Different consumers want different things from the resilient pipeline. Pick the recipe whose tradeoffs match the use case; the rationale + real-world example below each helps you confirm fit.

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
