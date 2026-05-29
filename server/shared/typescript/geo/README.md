<!--
Copyright (c) DCSV. All rights reserved.
-->

# geo/

> Parent: [`server/shared/typescript/`](../README.md)

Spec-driven geographic reference data and lookup contracts for TS consumers — the minimal hand-written API surface and the codegen-emitted in-memory catalogs. Both are emitted from the same seven `contracts/geo/*.spec.json` files that drive the .NET side via `tools/ts-codegen/src/geo-emitter/`, so cross-language drift is structurally impossible. Domain code references the abstractions without dragging in catalog data.

## Packages

| Package                                   | Description                                                                                                                          |
| ----------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------- |
| [`abstractions/`](abstractions/README.md) | The minimal hand-written geo API surface — `IGeoReference`, `IGeoNameResolver` + normalization helpers, `DeprecationInfo`. Spec-derived types are codegen-emitted here. Mirrors `D2.Shared.Geo.Abstractions`. |
| [`default/`](default/README.md)           | The codegen-emitted in-memory geo catalogs — per-entity records, nested lookup objects, flat lookup maps, and a module-init coordinator. Mirrors `D2.Shared.Geo.Default`. |
