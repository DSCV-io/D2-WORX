<!--
Copyright (c) DCSV. All rights reserved.
-->

# TIMESTAMPS.md — D²-WORX Temporal Reference

**Audience**: D² backend engineers building features that handle temporal data — timestamps,
scheduling, wire serialization, and cross-language time comparisons.

**What this doc answers**: how D² represents, stores, serializes, and compares time values across
runtime, persistence, and wire boundaries; which NodaTime type to reach for in each scenario; and
how to correctly consume `DateTimeOffset?` fields from the request / auth context.

> Canonical runtime API reference: [DcsvIo.D2.Time](../public/packages/dotnet/time/README.md) (.NET)
> · [@dcsv-io/d2-time](../public/packages/typescript/time/README.md) (TypeScript / Node 22+).

---

## Table of contents

1. [Three-category model](#three-category-model)
2. [PostgreSQL column mapping](#postgresql-column-mapping)
3. [NodaTime type selection guide](#nodatime-type-selection-guide)
4. [DST ambiguity and `LenientResolver`](#dst-ambiguity-and-lenientresolver)
5. [IClock injection rule](#iclock-injection-rule)
6. [Consuming wire `DateTimeOffset?` fields](#consuming-wire-datetimeoffset-fields)

---

## Three-category model

Every timestamp MUST be assigned a category in its xmldoc summary at design time. Category
classification drives storage shape, sort/compare correctness, and the DST-handling strategy.

| # | Category | Typical examples | Storage | Custom type |
|---|---|---|---|---|
| **1** | **Past instant** (happened; optionally preserved with original timezone context) | `createdAt`, `completedAt`, `requestStartedAt`, `lastLoginAt` | `event_at TIMESTAMPTZ` + optional `event_at_zone TEXT NULL` | `ZonedInstant` when original tz context is needed; bare `Instant` otherwise |
| **2** | **Future fixed instant** (deadline; absolute UTC; no wall-clock ambiguity) | JWT `exp`, session expiry, idempotency TTL, `tokenExpiresAt`, `lastStepUpAt`, `requestStartedAt` on IRequestContext | `expires_at TIMESTAMPTZ` | bare `Instant` (no custom type) |
| **3** | **Future local-anchored event** (scheduled wall-clock time; DST-sensitive) | cron triggers, user-set reminders, recurring billing dates | `scheduled_local TIMESTAMP` + `scheduled_zone TEXT` + `next_fire_utc TIMESTAMPTZ NULL` | `LocalAnchoredEvent` |

Key rules:

- **Category 1 and 2 always sort by UTC instant** — use `Instant` / `next_fire_utc` for ordering, never by local wall-clock form.
- **Category 2 is deadline-relative** — consumers check "is this instant still in the future?" using `clock.GetCurrentInstant() < expiresAt`, not by string comparison.
- **Category 3 is DST-sensitive** — re-evaluate `next_fire_utc` after every change to `scheduled_local` or `scheduled_zone`, and after IANA tzdb updates.

### Classifying wire context fields

The `IRequestContext` and `IAuthContext` spec fields carrying `DateTimeOffset?` are both Category 2:

| Field | Interface | Category | Why |
|---|---|---|---|
| `RequestStartedAt` | `IRequestContext` | 2 — past UTC instant | Originating request's start instant; fixed point in time; used to compute end-to-end SLA latency via `now - requestStartedAt`. |
| `LastStepUpAt` | `IAuthContext` | 2 — past UTC instant | Unix-seconds timestamp of last step-up completion; fixed point in time; used to check "step-up within last N minutes". |
| `TokenIssuedAt` | `IAuthContext` | 2 — past UTC instant | JWT `iat` claim; fixed point in time. |
| `TokenExpiresAt` | `IAuthContext` | 2 — past UTC instant | JWT `exp` claim; fixed point in time (expiry is checked as "is now before exp?"). |

---

## PostgreSQL column mapping

| Category | .NET domain type | PG column type | Notes |
|---|---|---|---|
| 1 (bare past instant) | `Instant` | `TIMESTAMPTZ` | Npgsql `UseNodaTime()` maps `Instant ↔ TIMESTAMPTZ` automatically. |
| 1 (with zone context) | `ZonedInstant` | `event_at TIMESTAMPTZ` + `event_at_zone TEXT NULL` | `ZonedInstant` is a value object; persist as two columns. |
| 2 | `Instant` | `TIMESTAMPTZ` | Same mapping as Category 1 bare. |
| 3 | `LocalAnchoredEvent` | `scheduled_local TIMESTAMP` + `scheduled_zone TEXT` + `next_fire_utc TIMESTAMPTZ NULL` | Three-column shape. `TIMESTAMP` (without TZ) stores wall-clock; `scheduled_zone` is the IANA name; `next_fire_utc` is the precomputed UTC execution time. |
| Date-only | `LocalDate` | `DATE` | No time component, no zone. |

**Never store a `TIMESTAMP` (without TZ) for a Category 1 or 2 value.** `TIMESTAMP` has no timezone information and will silently produce incorrect results when the PG server or application host changes timezone.

---

## NodaTime type selection guide

| Scenario | Correct type | Incorrect type |
|---|---|---|
| "What time is it now?" (injected clock) | `IClock.GetCurrentInstant()` → `Instant` | `DateTime.UtcNow` / `DateTimeOffset.UtcNow` |
| Past event timestamp | `Instant` | `DateTime` / `DateTimeOffset` |
| Past event + preserve original timezone | `ZonedInstant` | `DateTimeOffset` (leaks offset, not IANA name) |
| Future deadline | `Instant` | `DateTime` / `DateTimeOffset` |
| Scheduled wall-clock event (cron-like) | `LocalAnchoredEvent` | `DateTime` (ignores DST) |
| Duration / elapsed time arithmetic | `Duration` | `TimeSpan` (BCL) |
| Date-only (birthday, invoice date) | `LocalDate` | `DateTime` (carries phantom midnight-UTC) |
| Timezone reference | `DateTimeZone` (from `DateTimeZoneProviders.Tzdb`) | `TimeZoneInfo` (Windows registry; tzdb drift) |

---

## DST ambiguity and `LenientResolver`

DST gaps and overlaps occur in Category 3 (local-anchored events) only. `LocalAnchoredEvent.ComputeNextFire()` applies NodaTime's `Resolvers.LenientResolver` strategy:

- **Spring-forward gap** (e.g. `02:30` does not exist when clocks jump 02:00 → 03:00): map forward to the next valid wall-clock time after the gap (the result is `03:00`).
- **Fall-back ambiguity** (e.g. `01:30` exists twice when clocks fall back): pick the earlier (pre-transition) instant. This is deterministic and never throws.

The TypeScript counterpart (`@dcsv-io/d2-time`) mirrors this via Temporal's `disambiguation: "compatible"`. Cross-language parity is verified by adversarial fixture tests at `public/contracts/temporal/temporal-adversarial.fixture.json` loaded by both .NET and TS test suites.

**Category 1 and 2 timestamps are not DST-sensitive** — they are UTC instants and do not traverse timezone transitions.

---

## IClock injection rule

Domain code MUST NOT call `DateTime.UtcNow`, `DateTimeOffset.UtcNow`, or `Instant.FromDateTimeUtc(DateTime.UtcNow)` directly. Inject `IClock` and call `clock.GetCurrentInstant()`.

```csharp
// CORRECT: clock injected via primary constructor.
public sealed class CheckStepUpFreshness(IClock clock) : BaseHandler<...>
{
    private static readonly Duration _STEP_UP_MAX_AGE = Duration.FromMinutes(15);

    protected override Task<D2Result<...>> ExecuteAsync(...)
    {
        Instant now = clock.GetCurrentInstant();
        // ...
    }
}

// WRONG: untestable; clock can't be controlled in tests.
Instant now = SystemClock.Instance.GetCurrentInstant();  // forbidden in domain code
```

Production composition root: `services.AddSingleton<IClock, SystemClock>()`.
Test fixture: `var clock = new TestClock(Instant.FromUtc(2026, 1, 1, 0, 0)); clock.Advance(Duration.FromHours(1));`.

---

## Consuming wire `DateTimeOffset?` fields

`IRequestContext` and `IAuthContext` carry several `DateTimeOffset?` fields (`RequestStartedAt`,
`LastStepUpAt`, `TokenIssuedAt`, `TokenExpiresAt`). These are **Category 2 — past UTC instants**
serialized as `DateTimeOffset?` for JSON-serialization interop with the cross-language source-gen
pipeline. The wire form is NOT the domain form.

**Rule**: before any temporal arithmetic (comparisons, duration calculations, SLA checks, step-up
freshness checks), convert `DateTimeOffset → NodaTime.Instant` at the consumer boundary.

```csharp
// Step-up freshness check — correct pattern.
DateTimeOffset? lastStepUp = request.LastStepUpAt;
if (lastStepUp is null)
    return AuthFailures.StepUpRequired(...);

// Convert at the boundary before any arithmetic.
Instant stepUpInstant = Instant.FromDateTimeOffset(lastStepUp.Value);
Instant now = clock.GetCurrentInstant();
Duration age = now - stepUpInstant;

if (age > Duration.FromMinutes(15))
    return AuthFailures.StepUpRequired(...);
```

**Forbidden pattern** — direct `DateTimeOffset` subtraction in domain code:

```csharp
// WRONG: DateTimeOffset subtraction produces TimeSpan (BCL), not NodaTime Duration.
// Mixes BCL and NodaTime arithmetic; bypasses clock injection; untestable.
TimeSpan age = DateTimeOffset.UtcNow - request.LastStepUpAt!.Value;  // forbidden
```

**Cross-language note**: the TS side receives these fields as `string | undefined` (ISO 8601 format,
e.g. `"2026-05-27T14:30:00.000Z"`). Consumers convert via `Temporal.Instant.from(value)` before
arithmetic. The `Temporal` API is available natively in Node 22+ and via `temporal-polyfill` in
`@dcsv-io/d2-time` for environments that need it.

### Offset preservation behavior (.NET System.Text.Json)

.NET `System.Text.Json` serializes `DateTimeOffset` using the `"O"` (round-trip) format, which
preserves the original offset (e.g. `"2026-05-27T09:00:00-05:00"` is preserved as-is, not
normalized to `"2026-05-27T14:00:00+00:00"`). Two `DateTimeOffset` values with different offsets
are equal when they represent the same UTC instant; `.Value.Offset` is preserved through the wire
roundtrip. Consumers that care only about the UTC instant should call
`Instant.FromDateTimeOffset(dto.Value)` which strips the offset.
