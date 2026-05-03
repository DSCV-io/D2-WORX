<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Shared.Caching.Local.Abstractions

> **Status**: placeholder — not yet implemented.

## Purpose

Domain-safe slice of the local-cache stack. Owns the `ID2LocalCache` interface + `LocalCacheOptions` value type. Zero external deps (no NuGet packages, no other shared-lib references) so domain layers and any handler / repo / service can declare a dependency on local caching without dragging in the implementation runtime.

The implementation lives in the sibling [`caching-local-default/`](../caching-local-default/) project (memory-backed, lazy TTL + always-on LRU).

## Public API surface (planned)

- `ID2LocalCache` — `Get<T>` / `Set<T>` / `Remove` / `Exists` / `Clear` semantic surface for per-instance caching
- `LocalCacheOptions` — small Options record (max entries, default TTL, eviction policy hooks)

## Dependencies

(planned) None — only the .NET runtime.

## V2.md reference

§4 Phase 0 (Stage 4 — Wave 5).
