<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->


> **Visibility: PUBLIC** — ships with the open surface (`public/`).  
> Do not add product IP, private paths, or non-exportable runbooks.
# ADR-0003: `D2Result` — errors-as-values instead of exceptions for control flow

- **Status**: Accepted
- **Date**: 2026-05-30
- **Deliverable**: D2 shared libraries (backfilled)

## Context

D2 is a multi-service .NET 10 / SvelteKit system. Every handler — CQRS, repository, messaging consumer, scheduled job — can fail in a finite set of well-understood ways: resource not found, caller unauthorized or forbidden, input invalid, downstream unavailable, rate-limited, duplicate conflict. These are **expected outcomes**, not programmer errors.

The mainstream .NET default is to model these expected outcomes as exceptions (`NotFoundException`, `ValidationException`, etc.) and filter/catch them at a middleware boundary. That model has consistent costs in a layered, multi-service codebase: the failure path is invisible in the method signature, intermediate callers can accidentally swallow or re-wrap exceptions, partial-success states (some-but-not-all of a batch resolved) are awkward to express, and cross-language wire parity requires an additional mapping layer between the exception hierarchy and the HTTP envelope the TypeScript client reads.

The codebase also has a hard constraint on user-visible messages: every message must be a translation key — raw strings must be structurally unrepresentable, not merely discouraged by convention (see ADR-0004). Encoding that constraint in an exception-based model requires either a checked-exception analogue (unavailable in C#) or additional tooling.

## Decision

All expected operation outcomes are modeled as `D2Result` / `D2Result<TData>` value objects returned from every handler and service method. Exceptions are reserved exclusively for programmer-bug invariants.

**The core type** (`public/packages/dotnet/result/core/D2Result.cs` and its partials) carries seven wire fields: `Success`, `Data`, `Messages`, `InputErrors`, `StatusCode`, `ErrorCode`, and `TraceId`. Every property name is bound to a codegen-emitted constant from `D2ResultEnvelopeFieldNames.g.cs` (generated from `public/contracts/d2result-envelope/d2result-envelope.spec.json` — see ADR-0002) via `[JsonPropertyName]`, so the camelCase wire shape is correct under any `JsonSerializerOptions` and cross-language drift on the envelope field names is structurally impossible.

**Semantic factories** (`D2Result.Factories.cs`, `D2Result.Generic.Factories.cs`) are the only authorized construction path for failure states: `Ok`, `Created`, `NotFound`, `Unauthorized`, `Forbidden`, `ValidationFailed`, `Conflict`, `ServiceUnavailable`, `UnhandledException`, `PayloadTooLarge`, `TooManyRequests`, `Canceled`, `SomeFound`, `PartialSuccess`. Each factory bundles the canonical HTTP status code, the catalog `ErrorCode`, and a sensible default `TKMessage`. Raw `Fail()` is reserved for re-mapping arbitrary upstream codes where no semantic factory matches (`docs/PATTERNS.md` D2Result; `docs/dev/rules.md §5.3`).

**`ErrorCodes`** is codegen-emitted from `public/contracts/error-codes/error-codes.spec.json` (each code with a declared HTTP status). The same spec drives the TypeScript side via `public/tools/ts-codegen`. Hand-written constants are forbidden; cross-language drift is structurally impossible.

**`Messages` and `InputErrors` are typed as `IReadOnlyList<TKMessage>` and `IReadOnlyList<InputError>`** respectively. `TKMessage` has an `internal` constructor in `DcsvIo.D2.I18n.Abstractions`; the only way to produce one is via the SrcGen-emitted `TK.*` constants (ADR-0004). An untranslated literal in `D2Result.Messages` is structurally unrepresentable — the constraint is enforced by the type system at compile time, not by linter or convention.

**`TraceId` is auto-injected** at every handler boundary by the handler pipeline (ADR-0005) via `result.WithTraceId(traceId)`. Handlers do not thread trace context manually.

**Failure propagation** is explicit and lossless. `BubbleFail<TData>(D2Result)` re-wraps an upstream failure into the outer handler's payload type, preserving all metadata. `BubbleOnFailure<TOuter>(out bubbled, out data)` is the workhorse one-liner guard (`D2ResultGuardExtensions.cs`): returns `true` on failure (caller returns `bubbled`) and `false` on success (caller continues with unwrapped `data`). Monadic `Bind` / `Map` / `Match` and their async equivalents cover genuine linear pipelines (`D2Result.Generic.Monadic.cs`). `D2Result.Combine(...)` aggregates parallel results for fan-out validation (`D2Result.Combine.cs`).

**Partial-success ladder** (`NOT_FOUND` → `SOME_FOUND` → `OK`): batch query handlers return `NotFound` when zero items resolve, `SomeFound` (HTTP 206, `Success=false`, data attached) when some resolve, and `Ok` when all resolve. `PartialSuccess` (HTTP 207, `Success=true`) is the distinct write-side partial outcome — e.g., a tiered cache wrote L1 but not L2. Callers use `IsPartialOrMissing` (`IsNotFound || IsSomeFound`) for cache-fallback flows (`D2Result.Booleans.cs`).

**Per-code boolean discriminators** (`D2Result.Booleans.cs`) carry `[JsonIgnore]` and never appear on the wire. `IsTransientRetryable` explicitly excludes `IsUnhandledException` — unknown system state is never auto-retried.

**TypeScript mirror** (`public/packages/typescript/result/`, package `@dcsv-io/d2-result`): `D2Result<T>` class (`src/d2-result.ts`), factory functions mirroring the .NET factory surface (`src/factories.ts`), `bubbleFail` / `bubble` propagation (`src/bubble.ts`), and the `TKMessage` interface (`src/tk-message.ts`) — same envelope, same `ErrorCodes` catalog (generated from the same spec). Wire round-trips are byte-identical and parity-tested.

## Consequences

**Positive.**

- **Explicit control flow.** Every caller sees the failure surface in the method signature. Silent swallow requires a deliberate act (`if (!r.Failed)`); no exception escapes unnoticed up the call stack.
- **No exception-as-flow overhead.** Stack-unwinding, exception-filter middleware, and the associated allocation pressure are absent from the normal-operation path.
- **Composable propagation.** `BubbleFail` / `BubbleOnFailure` propagate the upstream failure losslessly through type boundaries in one line. Accidental message-drop or status-code re-mapping requires deliberate work.
- **Partial-success is first-class.** The `NOT_FOUND → SOME_FOUND → OK` ladder expresses batch-query outcomes with zero additional types or out-parameters.
- **Cross-language wire parity is structural.** The envelope field names, error codes, and `InputError` shape are spec-generated into both runtimes from the same JSON contracts (ADR-0002). Drift requires changing a spec file, which regenerates both sides.
- **No untranslated strings in responses.** `TKMessage`'s internal constructor guarantees every user-visible message is a translation key; the server stays locale-unaware on the HTTP path (ADR-0004).
- **Uniform `traceId` on every response**, auto-injected at the handler boundary.

**Negative / risks.**

- **Ceremony.** Every method in every handler layer returns a `D2Result`. New team members must learn the `BubbleOnFailure` idiom before writing fluent handler code.
- **Discipline to not `Ok()` after a failed downstream call.** Nothing in the type system prevents `return D2Result<T>.Ok(...)` after an upstream failure was silently ignored. The `BubbleOnFailure` pattern and the rules.md review predicates mitigate this; the language cannot enforce it.
- **`Combine` collapses error codes.** Aggregating heterogeneous failures via `D2Result.Combine` collapses error codes to `VALIDATION_FAILED`. Callers needing to preserve a typed upstream code use `BubbleFail` directly.
- **`IsTransientRetryable` depends on the canonical `errorCode`.** When a factory's `errorCode` is overridden (e.g., a domain-specific retry code), the per-code discriminators return `false` and the result falls outside the auto-retry classifier. Domain-specific retry logic must check the domain code explicitly — intentional, but it requires documentation.

## Alternatives considered

**Exceptions for control flow (mainstream .NET default).** The obvious baseline. Rejected because: (a) the failure surface is invisible in the method signature, creating silent-swallow risk in multi-layer orchestration; (b) partial-success states (`SOME_FOUND`, `PARTIAL_SUCCESS`) are awkward to model cleanly; (c) cross-language wire parity requires a separate exception-to-envelope mapping layer at the HTTP boundary; (d) encoding "messages must be translation keys" requires a checked-exception analogue C# does not have.

**Raw status-code returns (tuples or a `(TData?, ErrorCode?)` union).** Rejected because status-code + message are not enough for the wire envelope (no per-field `InputErrors`, no `TraceId`, no typed `TKMessage`), and ad-hoc tuples would still require a separate serialization mapping to reach the TypeScript client.

**A third-party Result library (FluentResults, OneOf, CSharpFunctionalExtensions).** Each carries genuine value (metadata accumulation, discriminated-union semantics, functional `Bind`/`Map`). Rejected for a bespoke `D2Result` because none of them: (a) ships wire-parity-pinned envelope serialization against a shared spec; (b) types `Messages` as `IReadOnlyList<TKMessage>` for the compile-time translation-key guarantee; (c) has a `TraceId` slot auto-injected at handler boundaries; (d) models the `NOT_FOUND → SOME_FOUND → OK` partial-success ladder and the `SomeFound`/`PartialSuccess` distinction. A zero-external-dependency bespoke type, spec-generated at the wire boundary, is the only path where all four properties hold at once.

## References

- `public/packages/dotnet/result/core/` — `D2Result.cs` + partials (`*.Factories.cs`, `*.Generic.Factories.cs`, `*.Generic.Monadic.cs`, `*.Booleans.cs`, `*.Combine.cs`, `D2ResultGuardExtensions.cs`, `InputError.cs`), and the committed `Generated/` `ErrorCodes.g.cs` + `D2ResultEnvelopeFieldNames.g.cs`.
- `public/packages/dotnet/result/core/README.md` — full factory table, bubble propagation, partial-success ladder, monadic API.
- `public/contracts/error-codes/error-codes.spec.json`, `public/contracts/d2result-envelope/d2result-envelope.spec.json` — the source-of-truth specs.
- `public/packages/typescript/result/src/` — `d2-result.ts`, `factories.ts`, `bubble.ts`, `tk-message.ts` (the TypeScript mirror).
- `docs/PATTERNS.md` (D2Result section); `docs/dev/rules.md §5.3` (semantic factories over raw `Fail`), §17 (D2Result usage), §20.5 (exceptions only for programmer-bug invariants).
- [ADR-0002](0002-spec-driven-codegen.md) — `ErrorCodes` + envelope field names are spec-emitted. [ADR-0004](0004-i18n-tkmessage.md) — `Messages` typed as `TKMessage`. [ADR-0005](0005-handler-pipeline.md) — `traceId` auto-injection + the uniform exception→`D2Result` guarantee.
