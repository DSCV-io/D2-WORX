<!--
Copyright (c) DCSV. All rights reserved.
-->

## 25. Temporal Types (date / time / clock)
<a name="top"></a>
_[← rules index](../rules.md) · §25 of the D2-WORX rules catalog._

**Predicate index:** §25.1–§25.12 · 12 predicates.

Temporal bugs are silent until they fire — DST twice a year, leap days every four years, tzdb updates every few months; failure modes are scheduled jobs that don't fire, fire twice, fire at non-existent local times, or crash on tzdb update. This category enforces the temporal-design discipline codified at `D2.Shared.Time` (.NET) + `@d2/time` (TS) so the same disciplines carry to every consumer.

**The catalog**: `IClock` is the single injection seam. `NodaTime` (.NET) / `Temporal` (TS) types are mandatory in production — never BCL `DateTime` / JS `Date`. Every temporal field is assigned one of three categories at design time (Cat 1 `ZonedInstant` / Cat 2 bare `Instant` / Cat 3 `LocalAnchoredEvent`). Cat 1 + Cat 3 records are constructed only via `Create(...)` smart-constructor factories returning `D2Result<T>`. DST resolution is encapsulated in `LocalAnchoredEvent.ComputeNextFire()`. Wire format is ISO 8601. Adversarial coverage at lib-introduction time is mandatory per `feedback_temporal_adversarial_test_required`.

**Canonical references**: [`server/shared/dotnet/time/README.md`](../../server/shared/dotnet/time/README.md), [`server/shared/typescript/time/README.md`](../../server/shared/typescript/time/README.md), `feedback_temporal_adversarial_test_required` (codified adversarial scenarios memory).

### Predicates — §25 temporal types

- **25.1** Does production code inject `IClock` and call `clock.GetCurrentInstant()` (.NET) / `clock.getInstant()` (TS) for "what time is it now?", rather than calling any system clock API directly?
  - **Forbidden tokens** (.NET, in non-test / non-composition-root paths): `SystemClock.Instance`, `NodaTime.SystemClock.Instance`, `DateTime.UtcNow`, `DateTime.Now`, `DateTimeOffset.UtcNow`, `DateTimeOffset.Now`.
  - **Forbidden tokens** (TS, in non-test / non-composition-root paths): `Temporal.Now.instant()`, `Temporal.Now.zonedDateTimeISO()`, `Temporal.Now.plainDateTimeISO()`, `Date.now()`, `new Date()`.
  - **Exceptions**: composition roots binding `IClock → SystemClock` in production / `IClock → TestClock` in tests; `D2.Shared.Time.SystemClock` / `@d2/time` `SystemClock` internal delegation to the underlying platform API; `TestClock` infrastructure itself.
  - Evidence: grep the forbidden token set against production code with composition-root + test-infra exclusions → expect zero hits, OR per-hit "checked, justified at <file:line> because <reason>".
  - **Why**: determinism. Tests inject `TestClock` to make time programmable; direct system-clock calls hardcode "now", so wall-clock drift, tzdb policy changes, and DST transitions become unobservable to tests.
  - **How**: anything needing "now" takes `IClock` via constructor injection; when porting code that calls a system clock, wrap at the boundary.

- **25.2** Does production code use `NodaTime` (.NET) / `Temporal` (TS) types exclusively for temporal values — never BCL `DateTime` / `DateTimeOffset` / `TimeSpan` (.NET) or JS `Date` (TS)?
  - **Forbidden types** (.NET, in production code): `System.DateTime`, `System.DateTimeOffset`, `System.TimeSpan`. Use `NodaTime.Instant` / `NodaTime.LocalDateTime` / `NodaTime.LocalDate` / `NodaTime.LocalTime` / `NodaTime.ZonedDateTime` / `NodaTime.DateTimeZone` / `NodaTime.Duration` / `NodaTime.Period` / `NodaTime.OffsetDateTime`.
  - **Forbidden types** (TS, in production code): `Date`, `Date.parse(...)`, `Date.now()`, `new Date(...)`. Use `Temporal.Instant` / `Temporal.PlainDateTime` / `Temporal.PlainDate` / `Temporal.PlainTime` / `Temporal.ZonedDateTime` / `Temporal.Duration`.
  - **Interop carve-out**: third-party SDKs / BCL APIs that return BCL temporal types MUST convert at the boundary (e.g., `Instant.FromDateTimeUtc(...)` ONCE at receive, then NodaTime everywhere downstream). The conversion site is the only place a BCL type may legally appear.
  - **§25.2.a — netstandard2.0 source-gen internals + provenance constants carve-out**: `DateTimeOffset` / BCL temporal types are permitted in (i) `netstandard2.0` Roslyn source-gen internals (the generator dll is a build-time analyzer, never a runtime consumer; `NodaTime` cannot be referenced in a `netstandard2.0` generator without boxing the project into a separate TFM), AND (ii) provenance constants emitted into a deliberately NodaTime-free leaf assembly (`geo/abstractions/Generated/`) where the `DateTimeOffset CatalogPublishedAt` constant is spec-metadata that consumers must NOT treat as a live clock value.
  - **§25.2.b — spec-gen-driven wire-boundary types carve-out**: `DateTimeOffset?` — or the NON-nullable `DateTimeOffset` when the field is always populated at construction / append-time — is permitted on spec-gen-driven destination-assembly wire-boundary types (`IRequestContext`, `IAuthContext`, the generated `MutableRequestContext`, and `CallPathEntry.Timestamp` — a spec-declared, `propagate=true`, JSON-wire-serialized hop-trace field whose establishment / append boundaries already convert `Instant → DateTimeOffset` at append-time, so it is always populated and takes the non-nullable form) where (i) the `.spec.json` declares the field as `DateTimeOffset` / `DateTimeOffset?` for JSON interop with the inbound JWT / HTTP header / hop-trace pipeline, AND (ii) the generated xmldoc on the property mandates conversion to `NodaTime.Instant` at the consumer boundary before any temporal computation. This remains a NARROW carve-out strictly scoped to spec-declared wire-propagated types — hand-written code that introduces new `DateTimeOffset` fields does NOT inherit it.
  - Evidence: grep `\bDateTime\b`, `\bDateTimeOffset\b`, `\bTimeSpan\b` in `.cs` production files; grep `new Date\s*\(`, `Date\.now\s*\(`, `Date\.parse\s*\(` in `.ts` production files. Exclude `*Tests.cs` for interop test scope; exclude composition roots + documented interop boundaries.
  - **Why**: BCL `DateTime.Kind` is ambiguous (`Unspecified` / `Local` / `Utc`) — the same value carries different semantics by construction path, producing silent off-by-N-hour bugs; JS `Date` has no zone awareness (comparisons / formatting silently use the runtime's local zone). NodaTime + Temporal force explicit zone handling at the type level.
  - **How**: convert at the interop boundary immediately — `Instant.FromDateTimeUtc(bclDateTime)` / `Temporal.Instant.fromEpochMilliseconds(jsDate.getTime())` — then flow the NodaTime / Temporal type downstream.

- **25.3** Does every new temporal field have an explicit category assignment (Cat 1 / Cat 2 / Cat 3) at design time, with the storage shape + custom type matching the category?
  - **Cat 1 — past instant with optional original context** (audit logs, sign-in events, message-received events where the original wall-clock context must be preserved for display): use `ZonedInstant` (`Instant + ianaIdentifier`). Storage: `<name>_instant TIMESTAMPTZ NOT NULL` + `<name>_iana TEXT NOT NULL` (or `NULL` if context is optional and bare `Instant` is the type).
  - **Cat 2 — future fixed instant OR generic UTC timestamp** (JWT `exp`, session expiry, idempotency TTL, `created_at`, `updated_at`): use bare `Instant` (.NET) / bare `Temporal.Instant` (TS). Storage: `<name> TIMESTAMPTZ NOT NULL`.
  - **Cat 3 — future local-anchored event** (cron-like schedules, recurring appointments, scheduled jobs where the user's wall-clock intent must survive tzdb policy changes): use `LocalAnchoredEvent` (`scheduledLocal + ianaIdentifier + nextFireUtc?`). Storage: `<name>_scheduled_local TIMESTAMP NOT NULL` + `<name>_iana TEXT NOT NULL` + `<name>_next_fire_utc TIMESTAMPTZ NULL` (NULL only during the brief window between create and first `ComputeNextFire()` cache).
  - Evidence: PLAN-phase predicate — every new temporal field in the Plan / migration / entity definition has a category assignment in its xmldoc summary or docstring. Code review confirms storage shape matches the assigned category.
  - **Why**: each category encodes a different invariant — Cat 1 preserves the original wall-clock context for replay / forensics, Cat 2 is the simple UTC case, Cat 3 keeps local-time intent stable across tzdb updates (a recurring 9:00 AM meeting stays 9:00 AM even if the country changes DST policy). Wrong category = wrong storage = production bug under tzdb update or display.
  - **How**: Plan reviewer matches each field to a category; new migrations cite the category in the migration comment. Canonical table: `server/shared/dotnet/time/README.md` ("Three timestamp categories").

- **25.4** Are `ZonedInstant` and `LocalAnchoredEvent` instances constructed EXCLUSIVELY via the static `Create(...)` factory returning `D2Result<T>`?
  - Evidence: grep `new ZonedInstant\s*\(`, `new LocalAnchoredEvent\s*\(` (.NET) and `new ZonedInstant\s*\(`, `new LocalAnchoredEvent\s*\(` (TS) across the entire codebase outside the lib's own internals → expect zero hits. The type system already enforces this in .NET (private positional constructor) and TS (private constructor), but the predicate documents the discipline + catches a regression if anyone makes the constructor public.
  - **Why**: `Create` validates the IANA identifier (null / empty / whitespace / `"Invalid/Zone"` / fixed-offset rejection), canonicalizes deprecated aliases (`"US/Pacific"` → `"America/Los_Angeles"`), and returns `D2Result.ValidationFailed` with `TK.Common.Time.INVALID_IANA_IDENTIFIER` on bad input; direct construction would accept garbage IANA strings into domain state.
  - **How**: code review; if the `Create` overload set doesn't satisfy a need, add an overload — do not relax constructor visibility.

- **25.5** When comparing or sorting temporal values, does the comparison use the UTC `Instant` (.NET) / `Temporal.Instant` (TS) representation — never `LocalDateTime` / `PlainDateTime` directly across zones?
  - **Forbidden patterns**: `ORDER BY <col>_local`, `ORDER BY <col>_scheduled_local`, `WHERE <col>_local [<>=] ...`, `Temporal.PlainDateTime.compare(a, b)` when `a` and `b` are in different zones, `localA.CompareTo(localB)` (.NET) across zones.
  - **Required patterns**: `ORDER BY <col>_instant` / `ORDER BY <col>_next_fire_utc`, `instant.CompareTo(other)` (.NET), `Temporal.Instant.compare(a, b)` (TS).
  - Evidence: grep `ORDER BY .*_local`, `ORDER BY .*_scheduled_local`, `\.ScheduledLocal\s*[<>=]`, `Temporal\.PlainDateTime\.compare` in handlers / queries / repos outside the Time lib → expect zero hits, or per-hit "checked, comparing within same zone documented at <file:line>".
  - **Why**: `LocalDateTime` / `PlainDateTime` across zones cannot be linearly ordered (`2:30 PT < 2:30 ET`? depends on date, DST policy, historical tzdb); UTC `Instant` values are points on the absolute timeline and ARE ordered. Cat 3's denormalized `next_fire_utc` (§25.11) makes Instant-based queries fast.
  - **How**: every `WHERE` / `ORDER BY` / inequality over temporal fields uses the UTC column; Cat 3 range queries hit `<col>_next_fire_utc BETWEEN ...`.

- **25.6** Is DST gap/overlap resolution (LenientResolver / `"compatible"` disambiguation) encapsulated EXCLUSIVELY inside `LocalAnchoredEvent.ComputeNextFire()` (.NET) / `localAnchoredEvent.computeNextFire()` (TS), never called directly from handler code?
  - **Forbidden patterns** (in code outside the Time lib): `Resolvers\.`, `\.AtLeniently\(`, `\.AtStrictly\(`, `Resolvers\.LenientResolver`, `disambiguation:\s*['""](?:compatible|earlier|later|reject)['""]`, `\.toZonedDateTimeISO\([^)]*disambiguation`.
  - Evidence: grep the forbidden token set across handlers / services / domain code outside the Time lib → expect zero hits.
  - **Why**: single source of truth for DST strategy. The lib applies `LenientResolver` (.NET) / `disambiguation: "compatible"` (TS) — both deterministic, verified equivalent via the `contracts/temporal/temporal-adversarial.fixture.json` cross-language fixture. Changing strategy = ONE file change instead of N scattered call sites; hand-rolled resolver calls also bypass the parity guarantee.
  - **How**: call `evt.ComputeNextFire()` and consume the `D2Result<Instant>`. A genuinely different DST strategy = new Time-lib factory/method + cross-language parity test + ADR, never an inlined resolver call at the consumer.

- **25.7** Do DB columns for temporal fields follow the per-category convention?
  - **Cat 1** (`ZonedInstant`): `<name>_instant TIMESTAMPTZ NOT NULL` + `<name>_iana TEXT NOT NULL` (per [`time/README.md` "Three timestamp categories" table](../../server/shared/dotnet/time/README.md#three-timestamp-categories)).
  - **Cat 2** (bare `Instant`): `<name> TIMESTAMPTZ NOT NULL` — single column, no IANA suffix.
  - **Cat 3** (`LocalAnchoredEvent`): `<name>_scheduled_local TIMESTAMP NOT NULL` + `<name>_iana TEXT NOT NULL` + `<name>_next_fire_utc TIMESTAMPTZ NULL`.
  - Evidence: migration review against the field's assigned category (per §25.3). EF entity property → column-name convention check. Per migration → each temporal column matches the expected shape for its category.
  - **Why**: consistent storage means query patterns match across services; Cat 3's `next_fire_utc` denormalization enables fast Instant queries (§25.5) and is what the tzdb-update job rewrites (§25.11). Mixing column shapes within a category breaks the tooling that depends on the convention.
  - **How**: check the category before writing columns; declare the exact suffix convention so the Npgsql NodaTime value converters (`AddD2NodaTime()`) wire automatically.

- **25.8** Are temporal values serialized to the wire using ISO 8601 according to the per-type convention?
  - **`Instant`** → ISO 8601 with `Z` suffix (e.g. `"2026-03-10T21:30:15.123Z"`).
  - **`LocalDateTime`** / **`PlainDateTime`** → ISO 8601 without zone suffix (e.g. `"2026-03-10T14:30:00"`).
  - **`ZonedInstant`** → JSON object: `{"instant": "...Z", "ianaIdentifier": "..."}`.
  - **`LocalAnchoredEvent`** → JSON object: `{"scheduledLocal": "...", "ianaIdentifier": "...", "nextFireUtc": "...Z"}` — `nextFireUtc` MAY be absent on inbound payloads (the server recomputes via `ComputeNextFire()` before persistence); MUST be present on outbound payloads (clients depend on it for display + sort).
  - Evidence: JSON converter contract tests (`CrossLanguageTemporalParityTests` in .NET, `cross-language.test.ts` in TS — both consume `contracts/temporal/temporal-adversarial.fixture.json`); API integration tests assert serialized shape; codegen emitter spec defines the shape per type.
  - **Why**: ISO 8601 is the single wire format both languages agree on (native NodaTime / Temporal binary formats don't interop across .NET ↔ TS). Object-wrapping Cat 1 / Cat 3 keeps the IANA identifier addressable for receiver-side validation without string parsing.
  - **How**: JSON serializer config registered once per service in the composition root; codegen emitter shape defined once per type and applied everywhere it appears on the wire.

- **25.9** When displaying a historical timestamp in a user's local zone, does the display code convert via `instant.InZone(iana).LocalDateTime` (.NET) / `instant.toZonedDateTimeISO(iana)` (TS) using the ORIGINAL stored IANA identifier — never via manual offset arithmetic on cached or derived values?
  - **Forbidden patterns**: hand-rolled offset addition (`addMinutes(60 * offsetHours)`, `+5:30`, `instant + TimeSpan.FromHours(offset)`), looking up "current offset for zone" and applying it to a historical instant, caching an offset value and reusing it across dates.
  - Evidence: grep for manual offset arithmetic patterns in display / formatter code; per display site → uses `InZone(iana)` / `toZonedDateTimeISO(iana)` then formats via `IFormatProvider` (.NET) / `Intl.DateTimeFormat` (TS).
  - **Why**: the tzdb encodes historical zone-rule timelines — DST dates have changed, countries have abolished / introduced DST mid-year (Egypt 2011) or moved zones (Samoa Dec 30 2011). The current offset is NOT the historical offset for arbitrary past instants, so manual arithmetic gets boundary and policy-change dates wrong.
  - **How**: call `Instant.InZone(iana)` / `instant.toZonedDateTimeISO(iana)` (consults the live tzdb at the resolved instant for the correct historical wall-clock), then format via `IFormatProvider` / `Intl.DateTimeFormat`.

- **25.10** Are stored IANA identifiers treated as already-canonical on read (no re-normalization at consumers)?
  - **The contract**: `ZonedInstant.Create` and `LocalAnchoredEvent.Create` smart-constructors canonicalize at the WRITE path (`"US/Pacific"` → `"America/Los_Angeles"`, `"Asia/Saigon"` → `"Asia/Ho_Chi_Minh"`, etc., per the per-lib README "Construction (smart-constructor pattern)" section). What lands in the DB / domain entity IS canonical.
  - **Forbidden at READ**: re-running `TimeZoneIdNormalizer.Canonicalize(entity.IanaIdentifier)` or equivalent re-validation on values already pulled from persistence; falling back to "if the lookup fails, try canonicalizing first" patterns.
  - Evidence: smart-constructor invariant documented in xmldoc on `ZonedInstant.Create` / `LocalAnchoredEvent.Create`; consumer-side code reads `entity.IanaIdentifier` / `entity.ianaIdentifier` directly without re-normalization; tests assert canonical form post-construction.
  - **Why**: single source of truth + tzdb-update safety. Values normalized at write time are unaffected when a future tzdb release drops an alias link; re-normalizing on read would start failing on data that was valid at write — a silent corruption regression triggered by an infra update (double-normalization is also pure overhead).
  - **How**: trust `entity.IanaIdentifier` as canonical; `DateTimeZoneProviders.Tzdb[...]` / `Temporal.TimeZone.from(...)` expect canonical input and the stored form satisfies that contract.

- **25.11** For Cat 3 `LocalAnchoredEvent` entities, is `next_fire_utc` persisted via `evt.ComputeNextFire()` at write time, AND is there a background job that recomputes `next_fire_utc` for FUTURE events on every tzdb-update deploy?
  - **Write-path requirement**: every Cat 3 entity insert / update calls `ComputeNextFire()` and persists the resulting `Instant` to `<col>_next_fire_utc` BEFORE the transaction commits.
  - **Tzdb-update job requirement**: the deploy pipeline / scheduled job runs after every `tzdata` version bump (NodaTime tzdb-only NuGet OR `temporal-polyfill` upgrade) and re-runs `ComputeNextFire()` against every Cat 3 entity whose `next_fire_utc > now` — past events are immutable history and MUST NOT be touched.
  - Evidence: EF entity has the `NextFireUtc` column; write-path handler calls `ComputeNextFire()` before insert / update (citation: handler file:line); the tzdb-update job exists for any service that persists Cat 3 entities (citation: job file:line + scheduled trigger). For services that don't yet persist Cat 3 entities, the predicate is `N/A` with explicit reason "no Cat 3 entities in this service".
  - **Why**: fast querying — `WHERE next_fire_utc BETWEEN ...` is index-scannable (recomputing per read is CPU-bound + re-does DST resolution). Tzdb-update correctness — when a country changes DST policy, future wall-clock-anchored events must have their UTC firing instants recomputed; past events are immutable history and must NOT be rewritten.
  - **How**: write-path computes `nextFireUtc` before persistence; the tzdb-update job iterates Cat 3 entities with `next_fire_utc > now`, re-runs `ComputeNextFire()`, and persists in a batched transaction. The scheduling lib ships this for the entities it owns; service-specific Cat 3 entities ship their own equivalent.

- **25.12** Does any Plan introducing a temporal / date / time / clock / scheduling type or operation enumerate adversarial test scenarios from `feedback_temporal_adversarial_test_required`, AND does the Implementer write tests for each enumerated scenario, AND does the Auditor flag absence as FINDING (not "polish" / "borderline" / "follow-up")?
  - **Mandatory adversarial categories** (per `feedback_temporal_adversarial_test_required`):
    1. **IANA validation**: null / empty / whitespace / `"Invalid/Zone"` / fixed-offset notation (`"UTC+5"`) / deprecated alias normalization / renamed zone / already-canonical pass-through.
    2. **DST transitions**: spring-forward skipped time (e.g. 2:30 AM on US DST day) / fall-back ambiguous time (e.g. 1:30 AM on US DST day) / explicit resolver strategy documentation / mid-year DST policy change (Egypt 2011) / country offset change (Samoa Dec 30 2011).
    3. **Calendar edges**: leap day in leap year (Feb 29 2024) / leap day in non-leap year (Feb 29 2025 rejection) / Gregorian leap rule (1900 not leap, 2000 leap) / invalid date (Feb 30, Apr 31) / year boundaries (year 1, year 9999, NodaTime min/max) / leap-second documentation.
    4. **Cross-language parity**: when both .NET + TS pair exists — identical observable behavior OR explicitly documented divergence (e.g. Temporal CLAMPS `seconds = 60` to 59 vs NodaTime throws).
    5. **Idempotency / determinism**: store-then-recompute produces identical result; recomputation after tzdb update is deterministic OR explicitly handled; construction is pure.
    6. **TestClock**: concurrent reads under writer mutation (thread-safety only meaningful for .NET); negative-duration `Advance` crossing epoch boundary; Min / Max `Instant` boundary (no overflow).
  - Evidence: Plan section enumerates categories explicitly; tests exist for each enumerated scenario (one test per scenario, behavior-descriptive name per §1.8); audit walks the matrix against the test file → every category has ≥1 test, OR per-missing-category FINDING.
  - **Why**: temporal bugs are silent until they fire, so adversarial coverage at lib-introduction time is far cheaper than the downstream data-corruption incident. Empirical catalyst: deliverable 0009 Step 1 — 4 audit rounds walked §1.2 adversarial coverage but missed the absence of DST / leap / IANA tests (surfaced at user review, ~3 hours rework); the gap was Plan-phase enumeration.
  - **How**: the Plan template carries an adversarial-categories checklist for any temporal scope (via the §1.22 matrix). Implementer writes one behavior-descriptively-named test per scenario (§1.8); Auditor cross-walks the test file against `feedback_temporal_adversarial_test_required` (§1.23) — any missing category is a FINDING (MEDIUM minimum; HIGH if the behavior is production-reachable, e.g. a Cat 3 entity creatable at a DST boundary). Cross-ref §1.2, §1.22, §1.23, §1.6.

<sup>[↑ jump to top](#top)</sup>

---

