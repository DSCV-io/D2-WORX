<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# geo/

> Parent: [`public/packages/dotnet/`](../README.md)

Spec-driven geographic reference data and lookup contracts for D2 services that resolve, validate, or store geographic identifiers. The cluster holds the minimal hand-written API surface (lookup contracts, name resolver, typed request-context accessors), the codegen-emitted in-memory catalogs plus the default name-resolver implementation, and the multi-target source generator that emits both the geo TYPES and the geo DATA from the spec files under `contracts/geo/`. Domain code references the abstractions without dragging in catalog data.

## Packages

| Package                                   | Description                                                                                                                                       |
| ----------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------- |
| [`abstractions/`](abstractions/README.md) | The minimal hand-written geo API surface — `IGeoReference`, `IGeoNameResolver` + normalization helpers, typed request-context accessors. Spec-derived types are codegen-emitted here. |
| [`default/`](default/README.md)           | The codegen-emitted in-memory geo catalogs plus the hand-written `DefaultGeoNameResolver` and Default-layer request-context extensions.          |
| [`source-gen/`](source-gen/README.md)     | Roslyn generator with multi-target dispatch — emits geo TYPES into `abstractions/` and geo DATA into `default/` from the spec files under `contracts/geo/`. |
