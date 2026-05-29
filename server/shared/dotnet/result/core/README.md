<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Shared.Result

> Parent: [`server/shared/dotnet/`](../../README.md)

`D2Result<T>` — errors-as-values pattern for D²-WORX. Replaces exception-based control flow throughout the backend. Every handler returns a `D2Result<T>`; callers branch on `result.Success` and propagate failures via `BubbleFail`.

Foundational lib. References only `D2.Shared.I18n.Abstractions` (itself zero-runtime-dep) so `Messages` and `InputErrors` can be typed as `TKMessage` — a structural compile-time guarantee that every user-visible message is a translation key. Consumed by every other library and service.

---

## File layout

| File                                            | Contents                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                               |
| ----------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `D2Result.cs`                                   | Non-generic base — constructor + properties (`Success`, `Failed`, `Messages`, `InputErrors`, `StatusCode`, `ErrorCode`, `TraceId`). Every wire-emitted property carries `[JsonPropertyName(D2ResultEnvelopeFieldNames.*)]` referencing the codegen-emitted catalog so the envelope keys ship as camelCase under ANY `JsonSerializerOptions` — callers do not need to configure `JsonNamingPolicy.CamelCase` for the wire shape to render correctly.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                    |
| `D2Result.Factories.cs`                         | Static semantic factories on `D2Result` (Ok, Created, Fail, NotFound, Forbidden, Unauthorized, ValidationFailed, Conflict, ServiceUnavailable, UnhandledException, PayloadTooLarge, TooManyRequests, Canceled, SomeFound)                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                              |
| `D2Result.Booleans.cs`                          | Per-error-code discriminators (`IsOk`, `IsNotFound`, `IsConflict`, etc.) + combined helpers (`IsPartialOrMissing`, `IsTransientRetryable`). All discriminators carry `[JsonIgnore]` — they're in-process call-site helpers, never wire fields. The non-generic `Failed` property is also `[JsonIgnore]`'d.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                             |
| `D2Result.Generic.cs`                           | Generic `D2Result<TData>` — adds `Data` + `CheckSuccess` / `CheckFailure` for inline destructuring. `Data` carries `[JsonPropertyName(D2ResultEnvelopeFieldNames.DATA)]` referencing the catalog.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                      |
| `D2Result.Generic.Factories.cs`                 | Generic factories mirroring the non-generic ones, plus `BubbleFail` / `Bubble` for upstream-result propagation                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                         |
| `D2Result.Generic.Monadic.cs`                   | Instance methods `Bind` / `Map` / `Match` for genuine monadic pipelines                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                |
| `D2ResultAsyncExtensions.cs`                    | Extension methods on `Task<D2Result<T>>` + `ValueTask<D2Result<T>>`: `BindAsync` / `MapAsync` / `ThenAsync`                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                            |
| `D2ResultGuardExtensions.cs`                    | `BubbleOnFailure` — the workhorse guard helper for the multi-value-threading pattern                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                   |
| `InputError.cs`                                 | `sealed record InputError(string Field, IReadOnlyList<TKMessage> Errors)` — per-field error rows used in `D2Result.InputErrors`. Record parameters carry `[JsonPropertyName(InputErrorWireShape.FIELD/ERRORS)]` referencing the codegen-emitted catalog constants so the wire shape stays camelCase (`{field, errors}`) under ANY `JsonSerializerOptions`.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                             |
| `Generated/.../ErrorCodes.g.cs`                 | Standardized error code constants — `NOT_FOUND`, `FORBIDDEN`, `VALIDATION_FAILED`, `PARTIAL_SUCCESS`, `IDEMPOTENCY_IN_FLIGHT`, `COULD_NOT_BE_SERIALIZED`, `COULD_NOT_BE_DESERIALIZED`, etc. (15 total). Codegen-emitted by [`D2.Shared.Result.ErrorCodes.SourceGen`](../../source-gen-shared/error-codes-source-gen/README.md) from [`contracts/error-codes/error-codes.spec.json`](../../../../../contracts/error-codes/error-codes.spec.json). Same spec drives the TS-side `@d2/result` `ErrorCodes` catalog via `tools/ts-codegen/src/error-codes-emit.ts` — cross-language drift is structurally impossible. The class also exposes `IReadOnlyList<string> AllCodes` (spec-order enumeration) and `int GetHttpStatus(string)` (declared HTTP status per code; `500` for unknown).                                                                                                                                                                                                                                                                                         |
| `Generated/.../InputErrorWireShape.g.cs`        | JSON property-name catalog for the `InputError` wire shape — `FIELD = "field"` + `ERRORS = "errors"`. Codegen-emitted by [`D2.Shared.WireShapes.SourceGen`](../../source-gen-shared/wire-shapes-source-gen/README.md) from [`contracts/input-error/input-error.spec.json`](../../../../../contracts/input-error/input-error.spec.json) (multi-target dispatch by assembly name). Referenced by `InputError`'s `[JsonPropertyName]` attributes; same spec drives the TS-side `@d2/result` `InputErrorWireShape` catalog via `tools/ts-codegen/src/wire-shape-emit.ts` — cross-language wire drift on the property names is structurally impossible.                                                                                                                                                                                                                                                                                                                                                                                                                             |
| `Generated/.../D2ResultEnvelopeFieldNames.g.cs` | JSON property-name catalog for the D2Result Shape B wire envelope (the BFF gateway response shape every frontend reads) — 7 constants: `SUCCESS`, `DATA`, `MESSAGES`, `INPUT_ERRORS`, `ERROR_CODE`, `TRACE_ID`, `STATUS_CODE`. Codegen-emitted by [`D2.Shared.Result.Envelope.SourceGen`](../envelope-source-gen/README.md) from [`contracts/d2result-envelope/d2result-envelope.spec.json`](../../../../../contracts/d2result-envelope/d2result-envelope.spec.json) (single-target dispatch on `D2.Shared.Result`). Referenced by `[JsonPropertyName(D2ResultEnvelopeFieldNames.*)]` attributes on every wire-emitted `D2Result` property. Same spec drives the TS-side `@d2/result` `D2ResultEnvelopeFieldNames` catalog via `tools/ts-codegen/src/d2result-envelope-emit.ts`; the BFF gateway parser reads via these constants instead of hand-rolled string literals. Cross-language wire drift on the 7 envelope field names is structurally impossible. The class also exposes `IReadOnlyList<string> AllFields` (spec-order enumeration). |
| `Unit.cs`                                       | `readonly record struct Unit` with canonical `Unit.Value` — payload-less success marker for handlers whose Ok state carries no data (subscribers, fire-and-forget commands). Used as the `TData` in `D2Result<Unit>`.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                  |

---

## Public API surface

### Properties (every result)

| Member                                      | Purpose                                                                                                                                                                                                                                                                                                                                              |
| ------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `Success` (bool)                            | True on the Ok path; false on every failure factory (including `SomeFound` — it's on the partial-success ladder).                                                                                                                                                                                                                                    |
| `Failed` (bool)                             | Convenience inverse of `Success`.                                                                                                                                                                                                                                                                                                                    |
| `Messages` (`IReadOnlyList<TKMessage>`)     | Translation-key messages. Wire format: `[{ "key": "...", "params": { ... }? }, ...]`. The SvelteKit client renders these via Paraglide; the server stays locale-unaware. Producers MUST use the SrcGen-emitted `TK.*` constants from `D2.Shared.I18n.Abstractions` — `TKMessage` has an internal ctor, so untranslated literals are unrepresentable. |
| `InputErrors` (`IReadOnlyList<InputError>`) | Per-field error rows. `InputError = (string Field, IReadOnlyList<TKMessage> Errors)`. Wire format: `[{ "field": "email", "errors": [{ "key": "..." }, ...] }, ...]` — self-describing; clients render under each input directly.                                                                                                                     |
| `StatusCode` (HttpStatusCode)               | HTTP-equivalent status. Defaults to 200 on success, 400 on failure. Each semantic factory sets the canonical code.                                                                                                                                                                                                                                   |
| `ErrorCode` (string?)                       | One of `ErrorCodes.*`, or a domain-specific override (e.g. `"FILES_INVALID_CONTENT_TYPE"`).                                                                                                                                                                                                                                                          |
| `TraceId` (string?)                         | Trace identifier for cross-service correlation. Auto-injected by `BaseHandler`; manual on factory calls outside handlers.                                                                                                                                                                                                                            |
| `Data` (TData?)                             | (Generic only) The payload. Default for the type when not provided.                                                                                                                                                                                                                                                                                  |

### Semantic factories — when to use which

The partial-success ladder for queries:

```
NOT_FOUND  →  SOME_FOUND  →  OK
(none)        (partial)      (all)
```

Only `Ok` sets `Success=true`. `SomeFound` and `NotFound` are both failures; consumers use `IsPartialOrMissing` to discriminate.

| Factory                         | StatusCode        | ErrorCode                           | Use when                                                                                                                                                                                                                 |
| ------------------------------- | ----------------- | ----------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `Ok`                            | 200               | (none)                              | Operation succeeded; payload (if generic) is the result.                                                                                                                                                                 |
| `Created`                       | 201               | (none)                              | Operation succeeded and created a new resource.                                                                                                                                                                          |
| `NotFound`                      | 404               | `NOT_FOUND`                         | Lookup found zero of the requested entities.                                                                                                                                                                             |
| `SomeFound`                     | 206               | `SOME_FOUND`                        | Batch lookup found some-but-not-all of the requested entities.                                                                                                                                                           |
| `Forbidden`                     | 403               | `FORBIDDEN`                         | Caller is authenticated but lacks the required scope.                                                                                                                                                                    |
| `Unauthorized`                  | 401               | `UNAUTHORIZED`                      | Caller is not authenticated.                                                                                                                                                                                             |
| `ValidationFailed`              | 400               | `VALIDATION_FAILED` (overridable)   | Input failed validation; populate `InputErrors`. Override `errorCode` for domain-specific signals (e.g. `"FILES_INVALID_CONTENT_TYPE"`).                                                                                 |
| `Conflict`                      | 409               | `CONFLICT`                          | DB unique-constraint violation, optimistic-concurrency conflict, etc.                                                                                                                                                    |
| `ServiceUnavailable`            | 503               | `SERVICE_UNAVAILABLE` (overridable) | Downstream service unavailable. Override `errorCode` to give consumers retry-vs-DLQ signals.                                                                                                                             |
| `UnhandledException`            | 500               | `UNHANDLED_EXCEPTION`               | Caught exception with no specific mapping. **Excluded from `IsTransientRetryable`** — unknown system state is never auto-retried.                                                                                        |
| `PayloadTooLarge`               | 413               | `PAYLOAD_TOO_LARGE`                 | Request body exceeds limit.                                                                                                                                                                                              |
| `TooManyRequests`               | 429               | `RATE_LIMITED` (overridable)        | Rate-limit middleware tripped. Override `errorCode` for client-side discrimination (e.g. `"OTP_RATE_LIMITED"`).                                                                                                          |
| `Canceled`                      | 400               | `CANCELED`                          | Operation canceled by client or server.                                                                                                                                                                                  |
| `PartialSuccess` (generic only) | 207               | `PARTIAL_SUCCESS`                   | Multi-target write where SOME targets succeeded (e.g. tiered cache wrote L1 but not L2). **Distinct from `SomeFound`**: `Success=true` here — the operation did partially succeed; callers branch on `IsPartialSuccess`. |
| `Fail`                          | 400 (overridable) | (overridable)                       | Last-resort raw factory. Use only when no semantic factory matches.                                                                                                                                                      |

### Per-code booleans

```csharp
result.IsOk                    // == Success
result.IsCreated               // StatusCode == Created
result.IsNotFound              // ErrorCode == NOT_FOUND
result.IsSomeFound             // ErrorCode == SOME_FOUND
result.IsPartialSuccess        // ErrorCode == PARTIAL_SUCCESS (and Success=true)
result.IsConflict              // ErrorCode == CONFLICT
result.IsForbidden             // ErrorCode == FORBIDDEN
result.IsUnauthorized          // ErrorCode == UNAUTHORIZED
result.IsValidationFailed      // ErrorCode == VALIDATION_FAILED
result.IsServiceUnavailable    // ErrorCode == SERVICE_UNAVAILABLE
result.IsRateLimited           // ErrorCode == RATE_LIMITED
result.IsUnhandledException    // ErrorCode == UNHANDLED_EXCEPTION
result.IsPayloadTooLarge       // ErrorCode == PAYLOAD_TOO_LARGE
result.IsCanceled              // ErrorCode == CANCELED
result.IsIdempotencyInFlight   // ErrorCode == IDEMPOTENCY_IN_FLIGHT
```

Combined helpers — name the concept, not the code:

```csharp
result.IsPartialOrMissing     // IsNotFound || IsSomeFound — both warrant downstream lookup in cache flows
result.IsTransientRetryable   // IsServiceUnavailable || IsRateLimited — UnhandledException is INTENTIONALLY excluded
```

> **Caveat:** when a factory's `errorCode` is overridden (e.g. `ServiceUnavailable(errorCode: "DOMAIN_RETRY_LATER")`), the corresponding boolean (`IsServiceUnavailable`) returns false because the comparison is on `ErrorCode`. Domain overrides bypass the auto-retry classification — that's intentional: domain-specific codes need domain-specific retry decisions.

### Bubble propagation

Pass an upstream failure through a generic boundary without losing detail:

```csharp
// Inner returns D2Result<int>; outer returns D2Result<OutputDto>
var inner = await GetCountAsync();
if (inner.Failed) return D2Result<OutputDto>.BubbleFail(inner);
```

`BubbleFail<TData>(D2Result)` copies `Messages`, `InputErrors`, `StatusCode`, `ErrorCode`, `TraceId` and sets `Data` to `default`. Use whenever an upstream failure should propagate up the call chain unchanged.

`Bubble<TData>(D2Result, TData?)` is the same but preserves both success AND failure (carries data even when the upstream failed) — used for `SomeFound` partial-result propagation.

### `CheckSuccess` / `CheckFailure` — inline destructuring

```csharp
if (cacheR.CheckSuccess(out var data))
{
    return D2Result<OutputDto>.Ok(data!.ToDto());
}
```

`CheckFailure` exposes data on failure too — useful for `SomeFound` flows where partial data is present despite `Success=false`.

### Monadic ops — `Bind` / `Map` / `Match`

For **genuine linear pipelines** where state flows step-to-step (sign-in flow, file processing, risk scoring):

```csharp
var result = await GetUserAsync(id)
    .BindAsync(user => ValidateConsentAsync(user))
    .BindAsync(user => UpgradeRoleAsync(user))
    .MapAsync(user => user.ToDto());
```

All chaining operators **short-circuit on failure**: the next/projection step is NOT invoked, and the upstream failure propagates via `BubbleFail`.

| Operator                                             | Signature                                     | When                                                                                      |
| ---------------------------------------------------- | --------------------------------------------- | ----------------------------------------------------------------------------------------- |
| `Bind<TNext>(Func<TData, D2Result<TNext>>)`          | sync, can change shape                        | `D2Result<T> → D2Result<U>` where the next step itself can fail                           |
| `Map<TNext>(Func<TData, TNext>)`                     | sync, can change shape, projection can't fail | Pure transformation of the success payload                                                |
| `Match<R>(Func<TData, R>, Func<D2Result<TData>, R>)` | sync, terminal                                | Reduce both success + failure branches to a single value (e.g. for HTTP response shaping) |
| `BindAsync` (Task + ValueTask)                       | async, can change shape                       | Async equivalent of `Bind`                                                                |
| `MapAsync` (Task + ValueTask)                        | async with sync projection                    | Async equivalent of `Map`                                                                 |
| `ThenAsync` (Task + ValueTask)                       | async, **same shape**                         | Sugar for `BindAsync` when `TData == TNext` (state evolves within one payload type)       |

### `BubbleOnFailure` — the workhorse guard helper

For the **dominant pattern** in command + complex handlers — guard against upstream failure, continue with locals on success:

```csharp
public override async ValueTask<D2Result<OutputDto?>> ExecuteAsync(I input, CancellationToken ct)
{
    var orderR = await getOrderById.HandleAsync(input.OrderId, ct);
    if (orderR.BubbleOnFailure<Order, OutputDto>(out var bubbled, out var order)) return bubbled;
    // continue with `order` as a local — strongly typed, no .Data! noise

    var contactR = await getContact.HandleAsync(order.ContactId, ct);
    if (contactR.BubbleOnFailure<Contact, OutputDto>(out var bubbled2, out var contact)) return bubbled2;

    return D2Result<OutputDto?>.Ok(BuildDto(order, contact));
}
```

Returns `true` when failure (caller returns `bubbled` immediately); `false` when success (caller continues with `data`).

> **`TOuter?` wrapping is intentional.** `BubbleOnFailure<TInner, TOuter>(out D2Result<TOuter?> bubbled, ...)` returns the outer wrapped as nullable. This means handler signatures consistently end in `D2Result<OutputDto?>` rather than `D2Result<OutputDto>` — the trailing `?` is the codebase convention for handler outputs and ensures `default!` on the success path of `BubbleOnFailure` doesn't lie about non-nullness. New handlers should follow the `D2Result<OutputDto?>` shape.

**When to use which tool:**

- **`BubbleOnFailure` + locals** (workhorse): multi-value threading. Most command + complex handlers, multi-tier query handlers, anything orchestrating across services.
- **`Bind` / `Map` / `ThenAsync` / `Match`**: genuine linear pipelines where state accumulates step-by-step (sign-in, file processing, risk scoring).

### `WithTraceId` — auto-injection seam

```csharp
result.WithTraceId(traceId);  // returns a NEW result with TraceId set; original unchanged
```

`BaseHandler.RunCorePipelineAsync` calls this on every result before returning, auto-injecting the request trace id without touching handler code. Handlers don't normally call `WithTraceId` directly; it's documented because tests + custom pipelines occasionally need it.

### `Combine` — N-input aggregation

`D2Result.Combine(...)` takes 2-5 typed `D2Result<T>` inputs (or an `IEnumerable<D2Result<T>>`) and returns either a tuple/list of unwrapped data on all-success or an aggregated `ValidationFailed` with concatenated `Messages` + `InputErrors` on any-failure. The first non-null `TraceId` across inputs wins; `ErrorCode`s collapse to `VALIDATION_FAILED` (errorCode discrimination is lost intentionally — Combine's failure semantic is "input-bag rejected").

**No `CombineAsync` overload.** Callers materialize first: `var (a, b) = (await taskA, await taskB); D2Result.Combine(a, b);` (or `await Task.WhenAll(...)` for arrays). Folding async into Combine ergonomics adds surface without removing call-site complexity.

---

## Default messages

Every failure factory with a `messages?` parameter ships a sensible default `TKMessage`, so the canonical case is `D2Result<T>.NotFound()` with no arguments. Pass your own `TK.*` constants for context-specific wording. The supplied list **replaces** the default — never appended.

The `TKMessage` type has an `internal` ctor in `D2.Shared.I18n.Abstractions`, so callers must reach for the SrcGen-emitted `TK.*` constants — raw-string escape hatches don't compile.

There is **no server-side translation middleware**. `TKMessage` ships verbatim over the wire; the SvelteKit client (Paraglide) renders in the active locale. Server-side translation happens only for outbound notifications (Courier emails / SMS) where the recipient locale comes from the user profile.

---

## Tests

`server/shared/dotnet/tests/Unit/Result/` — adversarial coverage across every public surface. Categories:

- All factory shapes + custom-message override paths (asserting `TKMessage` Key + Params equality, not raw strings)
- Per-code booleans, including the `IsTransientRetryable` exclusion of `UnhandledException`
- Generic factories + `BubbleFail` / `Bubble` cross-type propagation
- `InputError` shape — record equality, `TKMessage[]`-typed errors, JSON wire-format roundtrip
- `CheckSuccess` / `CheckFailure` including partial-success data exposure
- Monadic laws (left identity, right identity, associativity)
- Lazy evaluation (next / projection NOT invoked on failure)
- Sync + async chaining, short-circuiting on mid-chain failure
- `BubbleOnFailure` happy path + handler-shaped call site
- Adversarial: empty/null/whitespace inputs, errorCode override breaking auto-classification, exception propagation through Map/Bind, `SomeFound` treated-as-failure-but-carrying-data

Run: `dotnet test server/shared/dotnet/tests`
