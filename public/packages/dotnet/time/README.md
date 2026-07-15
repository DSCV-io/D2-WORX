<!--
Copyright (c) DCSV. All rights reserved.
-->

# DcsvIo.D2.Time

> Parent: [`public/packages/dotnet/`](../README.md)
>
> **Audience**: backend .NET service engineers who need deterministic timestamp handling, a dependency-injected clock seam for tests, and NodaTime ↔ PostgreSQL EF Core wiring.

NodaTime wrapper + `IClock` injection seam + Category 1/3 temporal storage
records + Npgsql NodaTime EF Core value-converter wiring.

## Overview

Production code MUST use NodaTime types (`Instant`, `LocalDateTime`,
`ZonedDateTime`, `Duration`, `DateTimeZone`, ...) for all timestamp work —
the BCL `DateTime` / `DateTimeOffset` types are forbidden in domain code per
the temporal-correctness predicates. Every timestamp in the codebase is
classified into one of three categories at design time (see the three-category
model below); the records in this lib (`ZonedInstant`, `LocalAnchoredEvent`)
encode the Category 1 / Category 3 storage shapes.

`IClock` is the single injection seam for "what time is it right now?".
Production binds `IClock → SystemClock`. Tests construct `TestClock` directly
and drive it deterministically.

This lib has NO `AddD2Time()` DI extension — consumers register
`IClock → SystemClock` themselves in their service composition root.

## Public surface

| Type                                             | Purpose                                                                                                                                                                                                                                                                  |
| ------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `IClock`                                         | Interface — single method `Instant GetCurrentInstant()`. The injection seam.                                                                                                                                                                                             |
| `SystemClock`                                    | Production `IClock` implementation. Delegates to `NodaTime.SystemClock.Instance`. Sealed.                                                                                                                                                                                |
| `TestClock`                                      | Test-only `IClock` implementation. Mutable `Now`; `Advance(Duration)` / `SetTo(Instant)`. Thread-safe. Sealed.                                                                                                                                                           |
| `ZonedInstant`                                   | Category 1 record — `Instant` + IANA tz identifier. Smart-constructor (`Create(instant, iana)` → `D2Result<ZonedInstant>`), validates + canonicalizes IANA. Sealed.                                                                                                      |
| `LocalAnchoredEvent`                             | Category 3 record — `LocalDateTime` + IANA tz identifier + denormalized `Instant? NextFireUtc`. Smart-constructor (`Create(scheduledLocal, iana, nextFireUtc?)` → `D2Result<LocalAnchoredEvent>`) + `ComputeNextFire()` encapsulating the DST resolver strategy. Sealed. |
| `IsoDuration`                                    | Lossless ISO-8601 ↔ NodaTime `Duration` bridge. `Parse(string?) → D2Result<Duration>` + `Format(Duration) → string`. Handles whole-unit, sub-second decimal-fraction seconds to nanosecond precision (`"PT0.123456789S"`), days, negative, and large durations — all via `Int128`/int64 nanosecond integer math (NO float). Fills the gap that NodaTime's built-in patterns leave (neither `DurationPattern` nor `PeriodPattern` parses ISO-8601 decimal-fraction seconds). Malformed / out-of-range input → `ValidationFailed(INVALID_DURATION)` (never throws). Static. |
| `NodaTimeValueConverterExtensions.AddD2NodaTime` | Npgsql EF Core extension calling `UseNodaTime()` on the NpgsqlDbContextOptionsBuilder.                                                                                                                                                                                   |

## Three timestamp categories

Every timestamp MUST be assigned a category in its xmldoc summary at design
time. The categories drive storage shape, sort/compare correctness, and the
DST-handling strategy.

| #   | Category                                                        | Storage                                                                                | Custom type                                                       |
| --- | --------------------------------------------------------------- | -------------------------------------------------------------------------------------- | ----------------------------------------------------------------- |
| 1   | Past instant (optionally with original context)                 | `event_at TIMESTAMPTZ` + optional `event_at_zone TEXT NULL`                            | `ZonedInstant` (when context is needed); bare `Instant` otherwise |
| 2   | Future fixed instant (JWT exp, session expiry, idempotency TTL) | `expires_at TIMESTAMPTZ`                                                               | bare `Instant` (no custom type)                                   |
| 3   | Future local-anchored event (cron-like schedules)               | `scheduled_local TIMESTAMP` + `scheduled_zone TEXT` + `next_fire_utc TIMESTAMPTZ NULL` | `LocalAnchoredEvent`                                              |

Sort by absolute time: always use `Instant` / `next_fire_utc` — zone-agnostic
and unambiguous.

DST ambiguity (Category 3 only): call `LocalAnchoredEvent.ComputeNextFire()`,
which applies NodaTime's `Resolvers.LenientResolver` — deterministic, never
throws (spring-forward skipped → maps forward; fall-back ambiguous → picks
earlier instant). The TS-side `@dcsv-io/d2-time` mirrors this via Temporal's
`disambiguation: "compatible"`. Cross-language wire parity is enforced by
the fixtures at `contracts/temporal/temporal-adversarial.fixture.json`,
which both .NET and TS test packages load and assert against.

## Construction (smart-constructor pattern)

`ZonedInstant` and `LocalAnchoredEvent` use the smart-constructor pattern
from `docs/PATTERNS.md`: private positional constructor + `static
D2Result<T> Create(...)` factory that validates the IANA identifier and
canonicalizes deprecated aliases (e.g. `"US/Pacific"` →
`"America/Los_Angeles"`, `"Asia/Saigon"` → `"Asia/Ho_Chi_Minh"`).

```csharp
var zoned = ZonedInstant.Create(Instant.FromUnixTimeSeconds(1_700_000_000), "US/Pacific");
// zoned.Success == true; zoned.Data!.IANAIdentifier == "America/Los_Angeles"

var bad = ZonedInstant.Create(now, "Invalid/Zone");
// bad.Success == false; bad.InputErrors[0].Errors[0] == TK.Common.Time.INVALID_IANA_IDENTIFIER

var evt = LocalAnchoredEvent.Create(new LocalDateTime(2026, 11, 1, 1, 30), "America/New_York");
var fire = evt.Data!.ComputeNextFire();
// On US DST fall-back, fire.Data == 2026-11-01T05:30:00Z (LenientResolver picks earlier).
```

`LocalDateTime` itself is not re-validated by `Create` — invalid calendar
dates (Feb 30, Feb 29 in a non-leap year, hour 24, …) throw
`ArgumentOutOfRangeException` from the `new LocalDateTime(...)` call site
BEFORE `Create` is reached. The lib's tests document the throw behavior
exhaustively. JSON serialization is not supported on the smart-constructor
records out-of-the-box (private ctor); v2 persistence is via
Npgsql.NodaTime EF Core (column-based, no JSON round-trip).

## EF Core wiring

Call `AddD2NodaTime()` inside the Npgsql configuration lambda passed to
`UseNpgsql()`:

```csharp
using DcsvIo.D2.Time.EfCore;

services.AddDbContext<MyDbContext>(opts =>
    opts.UseNpgsql(
        connectionString,
        npgsql => npgsql.AddD2NodaTime()));
```

This enables the full NodaTime ↔ PostgreSQL value-converter suite (Instant ↔
`timestamptz`, LocalDateTime ↔ `timestamp`, LocalDate ↔ `date`, ...). The
extension is idempotent — calling it twice on the same builder is safe.

## TestClock usage

```csharp
using DcsvIo.D2.Time;
using NodaTime;

var clock = new TestClock(Instant.FromUtc(2026, 1, 15, 12, 0));
var sut = new SomethingThatUsesClock(clock);

await sut.DoThingAsync();
clock.Advance(Duration.FromMinutes(5));
await sut.DoOtherThingAsync();

clock.SetTo(Instant.FromUtc(2030, 1, 1, 0, 0));
await sut.DoLateThingAsync();
```

`TestClock` is thread-safe (a `lock` guards `Now` reads and writes). Never
register `TestClock` in production DI.

## NodaTime types in transit

The following NodaTime types appear on this lib's public surface or are
recommended for production use: `Instant`, `LocalDateTime`, `LocalDate`,
`LocalTime`, `ZonedDateTime`, `DateTimeZone`, `Duration`, `Period`,
`OffsetDateTime`, `Resolvers`. Consumers `using NodaTime;` to access them
after taking a `<ProjectReference>` to `DcsvIo.D2.Time`.

**Naming collision note**: consumers using both `DcsvIo.D2.Time` and
`NodaTime` simultaneously (e.g., for `Duration` / `Instant` types) will hit
CS0104 ambiguous reference on `IClock` and `SystemClock` (NodaTime ships its
own `NodaTime.IClock` interface and `NodaTime.SystemClock` class). Add
per-file aliases at the top of the consuming file:

```csharp
using IClock = DcsvIo.D2.Time.IClock;
using SystemClock = DcsvIo.D2.Time.SystemClock;
```

## Telemetry

No telemetry surface — foundation lib emits no spans or metrics directly; consumers instrument via their own OTel setup.

## Configuration

No configuration — zero-config; consumers register `IClock → SystemClock` themselves in their service composition root.

## Dependencies

- `NodaTime` (NuGet)
- `Npgsql.EntityFrameworkCore.PostgreSQL.NodaTime` (NuGet)

D2 project references (smart-constructor pattern surfaces them transitively):

- `DcsvIo.D2.Result` — `D2Result<T>` + `InputError` shapes returned by `Create` factories.
- `DcsvIo.D2.I18n.Abstractions` — `TK.Common.Time.*` translation key constants referenced by the `Create` validation failures.
- `DcsvIo.D2.Utilities` — `Falsey()` extension for the IANA null/empty/whitespace guard.
