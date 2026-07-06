<!--
Copyright (c) DCSV. All rights reserved.
-->

## 17. D2Result Usage & Extensions
<a name="top"></a>
_[← rules index](../rules.md) · §17 of the D2-WORX rules catalog._

<!-- VERBATIM-BEGIN -->

`D2Result` replaces exceptions for control flow. Every operation that can fail returns a `D2Result<T>`. Master the extension methods so call sites stay clean.

### Predicates — §17 D2Result usage & extensions

- **17.1** Is `D2Result.BubbleFail` / `BubbleOnFailure` used to early-return from a handler when a nested operation fails? (Not manual `if (!result.Success) return D2Result<TOut>.Fail(...)`.)
  - Evidence: per nested handler call → bubble pattern confirmed.

- **17.2** Is `D2Result.Combine` used to aggregate multiple parallel `D2Result<T>` values into a single tuple / list result with combined errors?
  - **5 fixed-arity overloads (2-5)** + `IEnumerable<T>` overload.
  - **Eager evaluation**. All-success → tuple/list of unwrapped values + first non-null traceId. Any-failure → aggregated `ValidationFailed` with concatenated messages + inputErrors. Empty `IEnumerable` → `Ok` empty.
  - Evidence: per multi-result aggregation → `Combine` use.

- **17.3** Are partial-success paths handled correctly? `NOT_FOUND` (none found) → `SOME_FOUND` (partial, data returned) → `OK` (all found).
  - Evidence: per multi-fetch handler → tri-state outcome.

- **17.4** Does every `D2Result` carry `traceId` (auto-populated by `BaseHandler`)?
  - Evidence: per result → traceId presence.

- **17.5** Are typed factories preferred? `D2Result<string>.ServiceUnavailable()` instead of `BubbleFail(D2Result.ServiceUnavailable())`.
  - Evidence: per factory call → typed form when available.

- **17.6** Do extension methods on `D2Result` follow the `extension(D2Result<T> r) { ... }` C# 14 syntax (per §5.6)?
  - Evidence: per new D2Result extension → syntax confirmed.

- **17.7** When mapping arbitrary upstream status codes (e.g., from a third-party HTTP API), is raw `D2Result.Fail(statusCode, ...)` justified in journal? Otherwise convert to a semantic factory.
  - Evidence: per raw `Fail` use → justification.

- **17.8** Is `IsUnhandledException` excluded from `IsTransientRetryable` and every retry-eligibility check?
  - **Rule**: `IsTransientRetryable` covers `IsServiceUnavailable || IsRateLimited` only. `IsUnhandledException` MUST NOT appear in any retry-eligibility predicate.
  - **Forbidden**: `if (result.IsTransientRetryable || result.IsUnhandledException) retry(...)` — treating an unknown exception as retryable.
  - **Evidence**: `grep -rEn 'IsUnhandledException' <scope>` → per hit, confirm the hit is NOT inside a retry-eligibility branch. `D2Result.Booleans.cs` + `docs/PATTERNS.md` D2Result section document the intentional exclusion.
  - **Why**: an unhandled exception means unknown system state. Auto-retrying it risks masking bugs and double-executing side effects on non-idempotent operations. Only `IsServiceUnavailable` and `IsRateLimited` are known-safe to retry — their failure modes are infrastructure-transient, not logic-failure.
  - **How**: retry helpers use `result.IsTransientRetryable` (which already excludes `IsUnhandledException`). Any hand-rolled retry predicate must enumerate the retryable codes explicitly and omit `IsUnhandledException`.

<sup>[↑ jump to top](#top)</sup>

---

