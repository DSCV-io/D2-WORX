<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# @dcsv-io/d2-time

> Parent: [`public/packages/typescript/`](../README.md)
>
> **Audience**: backend Node/TypeScript service and BFF engineers who need the same deterministic clock seam and temporal storage types as `DcsvIo.D2.Time` (.NET).

Temporal-API wrapper providing the `IClock` injection seam + Category 1/3
temporal storage types. Mirrors `DcsvIo.D2.Time` (.NET).

## Overview

All production code reads "what time is it right now?" through `IClock`,
never via `Temporal.Now.instant()` directly. Production binds `IClock` →
`SystemClock`. Tests construct `TestClock` directly and drive it
deterministically.

The Temporal API (TC39 Stage 3) is the JavaScript equivalent of NodaTime —
unambiguous `Instant` (UTC), `PlainDateTime` (wall-clock, no zone),
`Duration`, etc. Node 22+ ships partial native support; we use
`temporal-polyfill` for stable cross-runtime behavior.

## Public surface

| Type / function      | Purpose                                                                                                                                                                                                                                                                           |
| -------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `IClock`             | Interface — single method `getInstant(): Temporal.Instant`. The injection seam.                                                                                                                                                                                                   |
| `SystemClock`        | Production `IClock`. Delegates to `Temporal.Now.instant()`.                                                                                                                                                                                                                       |
| `TestClock`          | Test-only `IClock` with `now` getter + `advance(Duration)` / `setTo(Instant)`. JS is single-threaded so no explicit locking is needed.                                                                                                                                            |
| `ZonedInstant`       | Category 1 class — `{ instant, ianaIdentifier }`. Smart-constructor (`ZonedInstant.create(instant, iana)` → `D2Result<ZonedInstant>`) validates + canonicalizes IANA.                                                                                                             |
| `LocalAnchoredEvent` | Category 3 class — `{ scheduledLocal, ianaIdentifier, nextFireUtc? }`. Smart-constructor (`LocalAnchoredEvent.create(scheduledLocal, iana, nextFireUtc?)` → `D2Result<LocalAnchoredEvent>`) + `computeNextFire()` encapsulating the Temporal `disambiguation: "compatible"` rule. |

## Three timestamp categories

Every timestamp MUST be assigned a category at design time. The categories
drive storage shape, sort/compare correctness, and DST handling.

| #   | Category                                          | Storage                                                                                | Custom type                                                                |
| --- | ------------------------------------------------- | -------------------------------------------------------------------------------------- | -------------------------------------------------------------------------- |
| 1   | Past instant (optionally with original context)   | `event_at TIMESTAMPTZ` + optional `event_at_zone TEXT NULL`                            | `ZonedInstant` (when context is needed); bare `Temporal.Instant` otherwise |
| 2   | Future fixed instant (JWT exp, session expiry)    | `expires_at TIMESTAMPTZ`                                                               | bare `Temporal.Instant`                                                    |
| 3   | Future local-anchored event (cron-like schedules) | `scheduled_local TIMESTAMP` + `scheduled_zone TEXT` + `next_fire_utc TIMESTAMPTZ NULL` | `LocalAnchoredEvent`                                                       |

Sort by absolute time: always use the UTC `Instant` field — zone-agnostic
and unambiguous.

DST ambiguity (Category 3 only): call `LocalAnchoredEvent.computeNextFire()`,
which applies Temporal's `disambiguation: "compatible"` (matches .NET
NodaTime's `Resolvers.LenientResolver`). Cross-language wire parity is
enforced by `contracts/temporal/temporal-adversarial.fixture.json` —
loaded by both .NET (`CrossLanguageTemporalParityTests`) and TS
(`cross-language.test.ts`) test suites.

## Construction (smart-constructor pattern)

```ts
import { ZonedInstant, LocalAnchoredEvent } from "@dcsv-io/d2-time";
import { Temporal } from "temporal-polyfill";

const zoned = ZonedInstant.create(
  Temporal.Instant.fromEpochMilliseconds(1_700_000_000_000),
  "US/Pacific",
);
// zoned.success === true; zoned.data!.ianaIdentifier === "America/Los_Angeles"

const bad = ZonedInstant.create(Temporal.Now.instant(), "Invalid/Zone");
// bad.success === false; bad.inputErrors[0].errors[0].key === "common_time_INVALID_IANA_IDENTIFIER"

const evt = LocalAnchoredEvent.create(
  Temporal.PlainDateTime.from("2026-11-01T01:30:00"),
  "America/New_York",
);
const fire = evt.data!.computeNextFire();
// On US DST fall-back, fire.data === Instant 2026-11-01T05:30:00Z (earlier of the ambiguous pair).
```

`PlainDateTime` itself is not re-validated by `create` — Temporal's parser
throws `RangeError` for invalid calendar dates (Feb 30, Feb 29 in a
non-leap year, hour 24, …) at the `Temporal.PlainDateTime.from(...)` call
site BEFORE `create` is reached. The lib's tests document the throw
behavior exhaustively (note: Temporal silently CLAMPS seconds = 60 to 59,
unlike .NET NodaTime which throws — both reject leap seconds, but with
different error modes; documented as a contract divergence).

## TestClock usage

```ts
import { TestClock } from "@dcsv-io/d2-time";
import { Temporal } from "temporal-polyfill";

const clock = new TestClock(Temporal.Instant.from("2026-01-15T12:00:00Z"));
const sut = new SomethingThatUsesClock(clock);

await sut.doThing();
clock.advance(Temporal.Duration.from({ minutes: 5 }));
await sut.doOtherThing();

clock.setTo(Temporal.Instant.from("2030-01-01T00:00:00Z"));
await sut.doLateThing();
```

Never register `TestClock` in production composition roots.

## Temporal-API note

Node 22+ has partial native Temporal support (behind a flag in some
releases). `temporal-polyfill@0.3.2` provides stable, complete coverage
across runtimes.

## Telemetry

No telemetry surface — foundation lib emits no spans or metrics directly; consumers instrument via their own OTel setup.

## Configuration

No configuration — zero-config; consumers register `IClock → SystemClock` themselves in their service composition root.

## Dependencies

- `temporal-polyfill` (npm)
- `@dcsv-io/d2-i18n-keys` (workspace) — generated TK constant catalog; `TK.common.errors.*` and `TK.common.time.*` keys used by the `create` factories.
- `@dcsv-io/d2-result` (workspace) — `D2Result<T>`, `InputError`, and TK helpers returned by the `create` factories.
