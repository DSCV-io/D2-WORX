<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Shared.Caching.Local.Default

> **Status**: placeholder — not yet implemented.

## Purpose

Default in-memory implementation of `ID2LocalCache` (from [`caching-local-abstractions/`](../caching-local-abstractions/)). Per-instance, lazy TTL (eviction on next access — no timer thread), always-on LRU eviction with a default ceiling of 10K entries (configurable via `LocalCacheOptions`).

This is one possible implementation of the local-cache abstraction; future implementations (e.g., `caching-local-faster` if FASTER ever pulls its weight) would live as sibling projects under their own `caching-local-{impl}/` slot.

## Public API surface (planned)

- `DefaultLocalCache` — implements `ID2LocalCache` over `ConcurrentDictionary` + LRU bookkeeping
- 5 CRUD handlers (`Caching/C/`, `Caching/R/`, `Caching/U/`, `Caching/D/`, `Caching/U/Exists.cs`) per [PATTERNS.md](../../../../docs/PATTERNS.md) TLC convention
- `AddD2LocalCacheDefault(IServiceCollection, Action<LocalCacheOptions>?)` DI extension

## Dependencies

(planned) `D2.Shared.Caching.Local.Abstractions`, `D2.Shared.Handler` (since cache ops are handlers and inherit observability), `D2.Shared.Result`.

## V2.md reference

§4 Phase 0 (Stage 4 — Wave 5).
