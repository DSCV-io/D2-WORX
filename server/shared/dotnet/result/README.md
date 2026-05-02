<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Shared.Result

> **Status**: placeholder — not yet implemented.

## Purpose

`D2Result<T>` — errors-as-values pattern. Replaces exception-based control flow throughout D²-WORX. Every handler returns a `D2Result<T>`; callers branch on `result.Success` and propagate failures via `BubbleFail`.

## Public API surface

- `D2Result<T>` — discriminated result (success + data) or (failure + statusCode + errorCode + messages + inputErrors)
- Semantic factories: `Ok`, `Created`, `NotFound`, `Unauthorized`, `Forbidden`, `ValidationFailed`, `Conflict`, `ServiceUnavailable`, `UnhandledException`, `PayloadTooLarge`, `Cancelled`, `SomeFound`
- `BubbleFail<TOuter, TInner>(inner)` — propagate downstream failure preserving status + errorCode + messages
- `Bubble<TOuter, TInner>(inner)` — propagate downstream success untouched (use sparingly— prefer to depend on inner directly)
- Pattern-matching helpers (CheckSuccess / CheckFailure)
- Auto-injected `traceId` from `IRequestContext.TraceId`

## Dependencies

- (none — foundational; consumed by every other lib)

## References

- D2Result monadic operations + guard helper
- Per-code booleans + concept-named combined helpers
- [docs/PATTERNS.md](../../../../docs/PATTERNS.md) "D2Result" section — full factory list + partial-success ladder
