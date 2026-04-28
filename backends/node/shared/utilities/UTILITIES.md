# @d2/utilities

Shared utility functions and constants. Mirrors `D2.Shared.Utilities` in .NET. Layer 0 — no project dependencies.

## Modules

| Module                                           | Description                                                                                                           |
| ------------------------------------------------ | --------------------------------------------------------------------------------------------------------------------- |
| [string-extensions.ts](src/string-extensions.ts) | `cleanStr`, `cleanAndValidateEmail`, `cleanAndValidatePhoneNumber`, `getNormalizedStrForHashing`, `truthyOrUndefined` |
| [array-extensions.ts](src/array-extensions.ts)   | `arrayTruthy`, `arrayFalsey` — array presence checks                                                                  |
| [uuid-extensions.ts](src/uuid-extensions.ts)     | `uuidTruthy`, `uuidFalsey`, `EMPTY_UUID`, `generateUuidV7`                                                            |
| [circuit-breaker.ts](src/circuit-breaker.ts)     | `CircuitBreaker` class, `CircuitState` enum, `CircuitOpenError`                                                       |
| [singleflight.ts](src/singleflight.ts)           | `Singleflight` — deduplicates concurrent in-flight operations by key                                                  |
| [retry.ts](src/retry.ts)                         | `retryAsync` — exponential backoff with transient error detection                                                     |
| [escape-html.ts](src/escape-html.ts)             | `escapeHtml` — XSS-safe HTML entity escaping for untrusted strings                                                    |
| [constants.ts](src/constants.ts)                 | `GEO_REF_DATA_FILE_NAME` — shared constant for geo reference data disk cache                                          |
| [index.ts](src/index.ts)                         | Barrel re-export of all utilities                                                                                     |

## String Utilities

### `cleanStr(str)`

Trims leading/trailing whitespace, collapses duplicate internal whitespace into a single space. Returns `undefined` if the input is null, undefined, empty, or whitespace-only.

### `truthyOrUndefined(value)`

```typescript
function truthyOrUndefined(value: string | null | undefined): string | undefined;
```

Converts empty/whitespace strings to `undefined` at data boundaries (user input, DB rows, proto mapping). Trims the value and returns `undefined` if the result is empty. Returns the trimmed string otherwise.

Unlike `cleanStr`, this does **NOT** collapse internal whitespace — it only trims and checks for emptiness. Use `cleanStr` for full whitespace normalization; use `truthyOrUndefined` to preserve internal spacing but reject empty/blank values.

```typescript
truthyOrUndefined("  hello  "); // "hello"
truthyOrUndefined("  hello world "); // "hello world" (internal space preserved)
truthyOrUndefined("   "); // undefined
truthyOrUndefined(""); // undefined
truthyOrUndefined(null); // undefined
```

**C# counterpart:** `ToNullIfEmpty()` extension method in `D2.Shared.Utilities`.

### `cleanAndValidateEmail(email)`

Cleans, normalizes (lowercase), and validates email format. Throws on invalid input.

### `cleanAndValidatePhoneNumber(phone)`

Strips non-digit characters, validates length (7–15 digits). Throws on invalid input.

### `getNormalizedStrForHashing(parts)`

Cleans and lowercases each part, joins with `|` for deterministic content-addressable hashing.

### `escapeHtml(str)`

Escapes `&`, `<`, `>`, `"` to their HTML entities. Used for untrusted string interpolation in HTML templates.

## Circuit Breaker

`CircuitBreaker<T>` protects async operations against sustained downstream failures. Three states: **Closed** (normal) → **Open** (fast-fail) → **HalfOpen** (one probe allowed).

```typescript
import { CircuitBreaker, CircuitState, CircuitOpenError } from "@d2/utilities";

const cb = new CircuitBreaker<MyResponse>({
  failureThreshold: 5, // consecutive failures to open (default: 5)
  cooldownMs: 30_000, // ms before probing (default: 30s)
  isFailure: (r) => !r.success, // result-based failure detection
  onStateChange: (from, to) => logger.info(`Circuit: ${from} → ${to}`),
});

const result = await cb.execute("my-key", () => client.call(request));
```

- `CircuitOpenError` is thrown when the circuit is open (catch for fail-open patterns)
- `isFailureError` predicate filters which thrown errors count as failures (default: all)
- Used by `@d2/geo-client` `FindWhoIs` handler for fail-open WhoIs lookups

## Singleflight

`Singleflight` deduplicates concurrent in-flight async operations by key. The first caller executes the operation; subsequent callers for the same key share the same Promise. Once the operation completes (success or failure), the key is removed — it is NOT a cache.

```typescript
import { Singleflight } from "@d2/utilities";

const sf = new Singleflight();

// Both calls share the same fetch — only one HTTP request is made
const [a, b] = await Promise.all([
  sf.execute("user:123", () => fetchUser("123")),
  sf.execute("user:123", () => fetchUser("123")),
]);
```

Used by `@d2/geo-client` to deduplicate concurrent gRPC calls for the same entity.

## Retry

`retryAsync<T>()` — general-purpose retry with exponential backoff.

```typescript
import { retryAsync, isTransientError } from "@d2/utilities";

const result = await retryAsync(() => fetchData(), {
  maxAttempts: 4, // default: 4
  baseDelayMs: 1000, // default: 1000 (1s → 2s → 4s → 8s)
  isTransient: isTransientError, // default: 5xx, timeout, 429
  signal: abortController.signal, // optional abort
});
```

`isTransientError(error)` checks for HTTP 5xx, 429, timeouts, and network errors.

## UUID Utilities

- `generateUuidV7()` — time-sortable UUIDv7 for primary keys
- `uuidTruthy(value)` / `uuidFalsey(value)` — checks for null/empty/`EMPTY_UUID`
- `EMPTY_UUID` — `"00000000-0000-0000-0000-000000000000"`

## Array Utilities

- `arrayTruthy(value)` — true if array is non-null and non-empty
- `arrayFalsey(value)` — true if array is null, undefined, or empty

## .NET Equivalent

`D2.Shared.Utilities` — mirrors this package with `Truthy()`/`Falsey()` extension methods, `CleanStr`, `ToNullIfEmpty()`, `CircuitBreaker<T>`, `Singleflight<T>`, `RetryHelper`, `D2Env`, `ConnectionStringHelper`, `EMPTY_UUID`, and `Guid.CreateVersion7()`.
