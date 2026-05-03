<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Shared.Utilities

Foundational helpers used at every boundary across D²-WORX. The "no value too small to centralize" library — preventing whole classes of bugs (empty-string-as-data, env-var collisions, JSON cycles) from ever entering domain code.

Zero runtime dependencies beyond `dotenv.net` (loaded only when `D2Env.Load()` is called) and `JetBrains.Annotations` (compile-time markers). Consumed by every other shared lib + service.

---

## File layout

| Path | Contents |
|---|---|
| `Attributes/RedactDataAttribute.cs` | `[RedactData(Reason = ..., CustomReason = "...")]` — marker attribute consumed reflectively by the observability layer (Serilog destructuring policy). Apply to types, properties, fields, parameters — anything `AttributeTargets.All`. |
| `Configuration/ConnectionStringHelper.cs` | URI ↔ wire-format converters for Redis / PostgreSQL / RabbitMQ env vars. |
| `Configuration/D2Env.cs` | `.env*` file loader for host-side scenarios (tests, IDE debug, ad-hoc `dotnet run`). No-op inside Docker Compose (Compose handles env injection natively). |
| `Enums/RedactReason.cs` | Standard reasons for redaction (`PersonalInformation`, `FinancialInformation`, `SecretInformation`, etc.) used by `[RedactData]`. |
| `Enums/IsolationLevel.cs` | DB isolation level enum with phenomena-matrix doc, mirroring the standard SQL isolation taxonomy. |
| `Extensions/StringExtensions.cs` | `Truthy()` / `Falsey()` / `ToNullIfEmpty()` / `CleanStr()` / `CleanDisplayStr()` / `CleanAndValidateEmail()` / `CleanAndValidatePhoneNumber()` / `GetNormalizedStrForHashing()`. |
| `Extensions/EnumerableExtensions.cs` | `Truthy()` / `Falsey()` for `IEnumerable<T>?` + the `Clean()` helper with configurable empty/null behavior. |
| `Extensions/CleanEnumEmptyBehavior.cs`, `CleanValueNullBehavior.cs` | Behavior enums for `EnumerableExtensions.Clean()`. |
| `Extensions/GuidExtensions.cs` | `Truthy()` / `Falsey()` for `Guid` and `Guid?` (treats `Guid.Empty` as falsey). |
| `Serialization/SerializerOptions.cs` | Frozen `JsonSerializerOptions` presets — `SR_IgnoreCycles`, `SR_Web`, `SR_WebIgnoreNull`. |

---

## Public API

### Boundary checks — `Truthy()` / `Falsey()` / `ToNullIfEmpty()`

The single most-used pair in the codebase. Null-safe extensions defined for `string?`, `IEnumerable<T>?`, `Guid`, and `Guid?`.

```csharp
string? userInput = ...;

if (userInput.Falsey())              // true for null / "" / "   " / "\t\n"
    return D2Result.ValidationFailed();

// After Falsey() returns false, the compiler considers value null-suspect, but
// you know it's set. The `!` is one of the FEW legitimate uses of null-forgiving.
var trimmed = userInput!.Trim();

// Or the canonical idiom — collapse to null at boundaries:
var stored = rawValue.ToNullIfEmpty();   // null | trimmed-non-empty
```

`ToNullIfEmpty()` is the workhorse for "convert empty/whitespace to null at every system boundary" — DB rows, proto mappings, user input. It's mandatory per [CLAUDE.md §5](../../../../CLAUDE.md) for keeping empty strings out of domain models.

```csharp
IEnumerable<T>? items = ...;
if (items.Falsey()) ...                   // true for null OR zero elements

Guid? id = ...;
if (id.Truthy()) ...                      // true for non-null AND non-empty
```

### Display-friendly cleaners — `CleanStr()` / `CleanDisplayStr()`

`CleanStr()` trims and collapses internal whitespace runs into a single space; returns `null` when empty afterward.

```csharp
"  hello   world  ".CleanStr()           // "hello world"
"a\t\nb".CleanStr()                       // "a b"
"   ".CleanStr()                          // null
```

`CleanDisplayStr()` strips characters not allowed in display names (HTML tags, markdown syntax, brackets, quotes, backticks, `<>(){}[]"\`+=|\\` etc.) and then runs `CleanStr()`. Allowed: any Unicode-letter script, digits, spaces, hyphens, apostrophes, periods, commas.

```csharp
"<script>alert('x')</script>John Doe".CleanDisplayStr()
// "scriptalert'x'scriptJohn Doe"

"Mary-Jane O'Neil, Jr.".CleanDisplayStr()       // unchanged — all allowed
"Иван Петров".CleanDisplayStr()                  // unchanged — Cyrillic letters
"日本語名前".CleanDisplayStr()                    // unchanged — CJK letters
"@@@***".CleanDisplayStr()                       // null — nothing left after stripping
```

### Throwing validators — `CleanAndValidateEmail()` / `CleanAndValidatePhoneNumber()`

Domain-layer constructors. **Intentionally throw** `ArgumentException` on invalid input — domain types should fail fast on invariant violations.

> Application/handler layers prefer errors-as-values: use `FluentValidation` + `D2Result.ValidationFailed()` instead. The throwing helpers exist for places where a violated invariant SHOULD halt execution (typically inside record constructors).

```csharp
"USER@EXAMPLE.COM".CleanAndValidateEmail()       // "user@example.com"
"  user@example.com  ".CleanAndValidateEmail()   // "user@example.com"
"noatsign".CleanAndValidateEmail()               // throws ArgumentException

"+44 20 7946 0958".CleanAndValidatePhoneNumber() // "442079460958" (digits only, 7–15 length)
"555-123-4567".CleanAndValidatePhoneNumber()     // "5551234567"
"123456".CleanAndValidatePhoneNumber()           // throws — 6 digits, below floor
```

Length envelope: 7–15 digits (E.164's effective range after the `+`).

### Hash key composition — `GetNormalizedStrForHashing()`

Joins a `string?[]` with `|` separators after lowercasing + cleaning each part. Empty parts are preserved as empty segments so positional alignment is retained — important when callers build composite hash keys like `"city|region|country"` where any field may be missing.

```csharp
new string?[] { " Test One ", "   ", "TEST3" }.GetNormalizedStrForHashing()
// "test one||test3"

new string?[] { null, "", "  " }.GetNormalizedStrForHashing()
// "||"
```

### Enumerable cleaning — `Clean()`

Materializes the enumerable, applies a per-element cleaner, and reshapes the result via two behavior knobs.

```csharp
var input = new[] { "keep1", "drop", "keep2" };
var cleaned = input.Clean(s => s == "drop" ? null : s);
// ["keep1", "keep2"]

// All cleaned to null + ReturnEmpty → []
input.Clean(_ => null, CleanEnumEmptyBehavior.ReturnEmpty);

// Cleaner returns null + ThrowOnNull → throws InvalidOperationException
input.Clean(_ => null, valueNullBehavior: CleanValueNullBehavior.ThrowOnNull);

// Empty input + Throw → throws ArgumentException
Array.Empty<string>().Clean(s => s, CleanEnumEmptyBehavior.Throw);
```

Two enums control behavior:

- `CleanEnumEmptyBehavior` (input or post-clean is empty): `ReturnNull` (default), `ReturnEmpty`, `Throw`.
- `CleanValueNullBehavior` (cleaner returns null for an element): `RemoveNulls` (default), `ThrowOnNull`.

The implementation calls `.ToList()` once upfront — generator-backed enumerables with side effects are enumerated exactly once.

### `[RedactData]` attribute

Marker attribute consumed by the Serilog destructuring policy in `D2.Shared.ServiceDefaults` (later phase). Apply to types, properties, fields, parameters — anywhere PII or secrets might leak into logs/spans/metrics.

```csharp
public sealed record User
{
    public required string Id { get; init; }

    [RedactData(Reason = RedactReason.PersonalInformation)]
    public required string Email { get; init; }

    [RedactData(Reason = RedactReason.SecretInformation, CustomReason = "OAuth bearer token")]
    public required string AccessToken { get; init; }
}
```

`RedactReason` values: `Unspecified`, `PersonalInformation`, `FinancialInformation`, `SecretInformation`, `VerboseContent`, `Other`.

### `SerializerOptions` presets

Three frozen `JsonSerializerOptions` instances — share them across the process; they're thread-safe and per-call allocation-free.

| Preset | Property naming | Enums | Nulls | Cycles |
|---|---|---|---|---|
| `SR_IgnoreCycles` | as-declared | as integers | included | tolerated (deduped during serialize) |
| `SR_Web` | camelCase | as strings | included | not tolerated |
| `SR_WebIgnoreNull` | camelCase | as strings | omitted | not tolerated |

```csharp
JsonSerializer.Serialize(dto, SerializerOptions.SR_Web);
// {"firstName":"Ada","lastName":"Lovelace","status":"Active"}

JsonSerializer.Serialize(dto, SerializerOptions.SR_WebIgnoreNull);
// {"firstName":"Ada"}    — null fields omitted

JsonSerializer.Serialize(graph, SerializerOptions.SR_IgnoreCycles);
// safe even if graph contains self-references
```

### `ConnectionStringHelper` — URI ↔ wire-format conversion

Bridges standard URI-format env vars (`REDIS_URL=redis://:p@host:6379`, `*_DATABASE_URL=postgresql://u:p@host/db`, `RABBITMQ_URL=amqp://...`) into the wire formats expected by .NET clients (`StackExchange.Redis`, `Npgsql`). RabbitMQ accepts AMQP URIs natively — the RMQ helper just reads the env var.

```csharp
// Inside Geo.API/Program.cs:
var redis    = ConnectionStringHelper.GetRedis();
var postgres = ConnectionStringHelper.GetPostgres("GEO_DATABASE_URL");
var rabbit   = ConnectionStringHelper.GetRabbitMq();

// Or parse a known string directly (no env var read):
ConnectionStringHelper.ParseRedisUri("redis://:secret@host:6380");
// "host:6380,password=secret"

ConnectionStringHelper.ParsePostgresUri("postgresql://u:p@h:5433/db");
// "Host=h;Port=5433;Username=u;Password=p;Database=db"
```

Pass-through for already-converted values: `ParseRedisUri("h:6380,password=x")` → unchanged, `ParsePostgresUri("Host=h;Port=5432;Database=db")` → unchanged. Defaults: Redis port `6379`, PostgreSQL port `5432`. URL-encoded credentials (`%40` → `@`, `%3A` → `:`) are unescaped automatically.

`Get*` overloads throw `InvalidOperationException` with a "Check your `.env.local` file." hint when the env var is missing or empty — never silently fall back to localhost.

### `D2Env` — `.env*` file loader

For **host-side** scenarios (running tests, IDE debug, ad-hoc `dotnet run`) where Docker Compose's native `env_file:` injection is not in play. Inside Compose containers, env vars are already set by Compose before `Load()` runs — D2Env's "process-env wins" rule means this is a no-op.

```csharp
// Default — load .env, .env.local, .env.secrets in that order from the
// nearest discovery directory:
D2Env.Load();

// Explicit override — load only what you want, in the order you want:
D2Env.Load(".env.test", ".env.local");
```

#### Discovery (option B — "first directory wins")

Walks up from `AppContext.BaseDirectory` (max 12 levels) looking for the FIRST directory that contains AT LEAST ONE of the named files, then loads every matching file from THAT directory only. Files from different ancestor directories are NEVER mixed — prevents weird hybrid situations where the "wrong" `.env.secrets` from a higher directory gets paired with the "right" `.env.local` from a lower one.

```
C:\repo\.env.secrets         ← FOUND first
C:\repo\subproj\.env.local   ← FOUND first (different walk start)

If walk starts in C:\repo\subproj\bin\Debug:
  → discovery dir = C:\repo\subproj
  → loads C:\repo\subproj\.env.local only
  → does NOT pick up C:\repo\.env.secrets
```

#### Precedence rules

1. **Process env wins over every file.** Any environment variable set when `Load()` was invoked (containers, IDE-injected vars, parent shell) is preserved unchanged. Files cannot overwrite container/parent values.
2. **Within file loading, later files in the list override earlier ones.** With the default `[".env", ".env.local", ".env.secrets"]`, `.env.secrets` overrides `.env.local`, which overrides `.env`. This matches Docker Compose's `--env-file` ordering, so host-side and container-side behavior match for the same .env files.

#### Idempotency

`Load()` is safe (and cheap) to call multiple times. Subsequent calls are no-ops — the file system is not re-walked. Tests can re-trigger via the internal `ResetForTests()` seam.

#### Caveat — case sensitivity

Env-var key collision detection uses the platform comparer: case-INsensitive on Windows (`PATH` and `path` are the same key), case-SENSITIVE everywhere else. D²-WORX's convention is uppercase env-var names; this matters only at the rare cross-OS edge.

### `IsolationLevel`

Standard SQL isolation level enum with phenomena matrix. Values: `ReadUncommitted`, `ReadCommitted` (default), `RepeatableRead`, `Serializable`. Use to parametrize EF Core / Npgsql transaction scopes.

| Level | Dirty reads | Non-repeatable reads | Phantom reads | Serialization anomaly |
|---|---|---|---|---|
| `ReadUncommitted` | yes | yes | yes | yes |
| `ReadCommitted` | no | yes | yes | yes |
| `RepeatableRead` | no | no | yes (not in PG) | yes |
| `Serializable` | no | no | no | no |

> PostgreSQL note: `ReadUncommitted` behaves identically to `ReadCommitted`; `RepeatableRead` does not allow phantom reads.

---

## Tests

`server/shared/dotnet/tests/Unit/Utilities/` — adversarial coverage at 100% lines + 100% branches. Categories:

- All `Truthy`/`Falsey` overloads — null, empty, whitespace-only, multi-element, boundary values.
- `ToNullIfEmpty` — null/empty/whitespace/trim/identity paths.
- `CleanStr` / `CleanDisplayStr` — Unicode multi-script preservation, allowed-char retention, all-stripped → null.
- `CleanAndValidateEmail` / `CleanAndValidatePhoneNumber` — happy paths AND every documented throw condition (null, empty, no `@`, no dot, double `@`, embedded space, length out of bounds, non-digit input, etc.).
- `GetNormalizedStrForHashing` — empty array, all-falsey, mixed, position-preservation property.
- `EnumerableExtensions.Clean` — every (3 × 2) combination of empty-behavior and value-null-behavior, plus generator-backed single-enumeration property.
- `RedactDataAttribute` — defaults, init-only setters, `AttributeUsage` target, reflective attribute discovery.
- `SerializerOptions` — camelCase, string-enums, null preservation/omission, cycle tolerance.
- `ConnectionStringHelper` — pass-through cases, URI parsing for both Redis and Postgres, default ports, URL-encoded credentials, env-var resolution including missing-var throws (with collection-isolated env-mutating tests).
- `D2Env` — `ApplyVars` precedence (process-env wins, later files override earlier), file discovery ("first dir with any match wins"), depth-limit exhaustion, platform comparer test seam, `Load()` idempotency.

Run: `dotnet test server/shared/dotnet/tests`

CLI coverage one-liner (writes a Cobertura XML; coverlet.console's stdout summary shows totals):

```bash
cd server/shared/dotnet/tests
coverlet bin/Debug/net10.0/D2.Shared.Tests.dll \
  --target dotnet --targetargs "test --no-build" \
  --include "[D2.Shared.Utilities]*" \
  --exclude-by-attribute "GeneratedCode" \
  --format cobertura --output ./coverage/utilities.cobertura.xml
```
