<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Shared.Utilities

> **Status**: placeholder — not yet implemented.

## Purpose

Foundational helpers used at every boundary across D²-WORX. The "no value too small to centralize" library — preventing whole classes of bugs (empty-string-as-data, race conditions on retry, etc.).

## Public API surface

- `Truthy()` / `Falsey()` — null-safe extension methods. After early return on `Falsey`, use `value!` (one of the few legitimate uses of `!`)
- `ToNullIfEmpty()` — `string?` extension; returns `null` if input is null, empty, or whitespace-only (trims first)
- `CleanStr()` — like `ToNullIfEmpty()` but also strips control characters + normalizes Unicode
- `CircuitBreaker<T>` — three-state (Closed / Open / HalfOpen); state transitions via `Interlocked` (lock-free fast path)
- `Singleflight<T, TKey>` — deduplicates concurrent calls for the same key
- Retry helpers — exponential backoff with jitter; transient-error predicates
- Cache constants — standard TTL values, LRU sizes, etc.

## Dependencies

- (none — foundational; consumed by everything else)

## References

- CLAUDE.md §5 cross-platform rules: `Truthy` / `Falsey` handle null, `ToNullIfEmpty()` at boundaries
- [docs/PATTERNS.md](../../../../docs/PATTERNS.md) "Utilities" section — full mechanics
