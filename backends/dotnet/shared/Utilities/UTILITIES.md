# Utilities

Shared utility extensions and helpers used across all contracts and services. Provides environment variable loading, string cleaning, collection operations, GUID validation, circuit breaker, singleflight, retry, and content-addressable hashing.

## Modules

| Directory / File                                                     | Description                                                                                                                                         |
| -------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------- |
| [StringExtensions.cs](Extensions/StringExtensions.cs)                | `Truthy()`, `Falsey()`, `CleanStr()`, `ToNullIfEmpty()`, `CleanAndValidateEmail()`, `CleanAndValidatePhoneNumber()`, `GetNormalizedStrForHashing()` |
| [EnumerableExtensions.cs](Extensions/EnumerableExtensions.cs)        | Collection `Truthy()`/`Falsey()`, `Clean()` with configurable null handling                                                                         |
| [GuidExtensions.cs](Extensions/GuidExtensions.cs)                    | GUID `Truthy()`/`Falsey()` — checks for null and `Guid.Empty`                                                                                       |
| [RedactDataAttribute.cs](Attributes/RedactDataAttribute.cs)          | Attribute marking properties for redaction in logs/telemetry                                                                                        |
| [RedactReason.cs](Enums/RedactReason.cs)                             | Enum: PersonalInformation, FinancialInformation, SecretInformation, VerboseContent, Other                                                           |
| [D2Env.cs](Configuration/D2Env.cs)                                   | Convention-based `.env` file loader (`.env.local` / `.env`)                                                                                         |
| [ConnectionStringHelper.cs](Configuration/ConnectionStringHelper.cs) | PostgreSQL URI → connection string parser                                                                                                           |
| [RetryHelper.cs](Configuration/RetryHelper.cs)                       | `D2RetryHelper.RetryAsync()` — exponential backoff with jitter                                                                                      |
| [RetryOptions.cs](Configuration/RetryOptions.cs)                     | Configuration for retry behavior (max attempts, base delay, transient detection)                                                                    |
| [CircuitBreaker.cs](CircuitBreaker/CircuitBreaker.cs)                | Generic circuit breaker with thread-safe state via `Interlocked`                                                                                    |
| [CircuitBreakerOptions.cs](CircuitBreaker/CircuitBreakerOptions.cs)  | Configuration: FailureThreshold, CooldownDuration, NowFunc (test seam)                                                                              |
| [CircuitState.cs](CircuitBreaker/CircuitState.cs)                    | Enum: Closed, Open, HalfOpen                                                                                                                        |
| [CircuitOpenException.cs](CircuitBreaker/CircuitOpenException.cs)    | Exception thrown when circuit is open                                                                                                               |
| [Singleflight.cs](Singleflight/Singleflight.cs)                      | Deduplicates concurrent in-flight operations by key                                                                                                 |
| [Constants.cs](Constants/Constants.cs)                               | `EMPTY_UUID`, cache key constants                                                                                                                   |
| [SerializerOptions.cs](Serialization/SerializerOptions.cs)           | Reusable `System.Text.Json` configs with reference cycle handling                                                                                   |

## String Extensions

### `Truthy()` / `Falsey()`

Checks if a string is "truthy" (not null, not empty, not whitespace) or "falsey" (null, empty, or whitespace). `Falsey()` uses `string.IsNullOrWhiteSpace()` internally.

**Important:** `Falsey()` handles null — never write `if (value is null || value.Falsey())`. Just `if (value.Falsey())`. After an early return, use `value!` — the value is guaranteed non-null.

### `ToNullIfEmpty()`

```csharp
extension(string? s)
{
    public string? ToNullIfEmpty()
}
```

Returns `null` if the string is null, empty, or whitespace-only; otherwise returns the trimmed string. Uses `Falsey()` internally. Use at data boundaries (DB rows, proto mapping, user input) to convert empty strings to `null` before they propagate as "valid" data.

```csharp
"  hello  ".ToNullIfEmpty()      // "hello"
"  hello world ".ToNullIfEmpty() // "hello world" (internal space preserved)
"   ".ToNullIfEmpty()            // null
"".ToNullIfEmpty()               // null
((string?)null).ToNullIfEmpty()  // null
```

**Node.js counterpart:** `truthyOrUndefined()` in `@d2/utilities` (returns `undefined` instead of `null`).

### `CleanStr()`

Trims whitespace and collapses duplicate internal whitespace into a single space. Returns `null` if the input is null, empty, or whitespace-only.

### `CleanAndValidateEmail()` / `CleanAndValidatePhoneNumber()`

Validates format + cleans input. Throws `GeoValidationException` on invalid input.

### `GetNormalizedStrForHashing(parts)`

Cleans and lowercases each part, joins with `|` for deterministic content-addressable hashing (SHA-256).

## Circuit Breaker

`CircuitBreaker<T>` protects async operations against sustained downstream failures. Three states: **Closed** (normal) → **Open** (fast-fail) → **HalfOpen** (one probe allowed).

| File                                                                | Description                                                                                                                               |
| ------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------- |
| [CircuitBreaker.cs](CircuitBreaker/CircuitBreaker.cs)               | Generic circuit breaker with thread-safe state via `Interlocked`. Tracks consecutive failures, opens at threshold, probes after cooldown. |
| [CircuitState.cs](CircuitBreaker/CircuitState.cs)                   | Enum: Closed (0), Open (1), HalfOpen (2).                                                                                                 |
| [CircuitBreakerOptions.cs](CircuitBreaker/CircuitBreakerOptions.cs) | Configuration record: FailureThreshold (default 5), CooldownDuration (default 30s), NowFunc (test seam).                                  |
| [CircuitOpenException.cs](CircuitBreaker/CircuitOpenException.cs)   | Exception thrown when the circuit is open and no fallback is provided.                                                                    |

```csharp
var cb = new CircuitBreaker<MyResponse>(
    isFailure: r => !r.Success,
    options: new CircuitBreakerOptions
    {
        FailureThreshold = 5,
        CooldownDuration = TimeSpan.FromSeconds(30),
    },
    onStateChange: (from, to) => logger.LogWarning("Circuit: {From} → {To}", from, to));

var result = await cb.ExecuteAsync(
    ct => myClient.CallAsync(request, ct),
    ct: cancellationToken);
```

**Fail-open pattern** (used by Geo.Client `FindWhoIs`): Catch both `RpcException` and `CircuitOpenException` — when the circuit is open, `ExecuteAsync` throws `CircuitOpenException` instantly (no timeout wait).

## Singleflight

`Singleflight<T>` deduplicates concurrent in-flight async operations by key. The first caller executes the operation; subsequent callers for the same key share the same `Task`. Once complete, the key is removed — it is NOT a cache.

```csharp
var sf = new Singleflight<MyData>();

// Both calls share the same task — only one DB query is made
var tasks = new[]
{
    sf.ExecuteAsync("user:123", ct => db.FindUserAsync("123", ct)),
    sf.ExecuteAsync("user:123", ct => db.FindUserAsync("123", ct)),
};
var results = await Task.WhenAll(tasks);
```

Used by `Geo.Client` to deduplicate concurrent gRPC calls for the same entity.

## Retry

`D2RetryHelper` — exponential backoff with jitter for transient failures.

- `RetryResultAsync<T>()` — retries D2Result-returning operations (checks `result.Success`)
- `RetryExternalAsync<T>()` — retries operations that throw on failure
- Uses `Random.Shared` for thread-safe jitter
- Filters `OperationCanceledException` (not retried, re-thrown)

## Configuration

### `D2Env`

Convention-based `.env` file loader. Reads `.env.local` (takes priority) and `.env`, sets environment variables with three key transforms:

1. Original: `MY_VAR=value` → `Environment["MY_VAR"] = "value"`
2. Infrastructure: `MY_VAR=value` → `Parameters__my-var` (kebab-case for Aspire/container config)
3. Options: `SECTION__PROPERTY=value` → `Section__Property` (IConfiguration binding)

### `ConnectionStringHelper`

`ParsePostgresUri(uri)` — converts `postgresql://user:pass@host:port/db` URIs to ADO.NET connection strings (`Host=...;Port=...;Database=...;Username=...;Password=...`). Handles missing credentials gracefully.

## Node.js Equivalent

`@d2/utilities` — mirrors this package with `truthyOrUndefined()`, `cleanStr`, `CircuitBreaker`, `Singleflight`, `retryAsync`, `escapeHtml`, `generateUuidV7()`, and array/UUID helpers.
