<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# context/

> Parent: [`public/packages/dotnet/`](../README.md)

The per-request context primitives D2 services carry across hops — the spec-driven `IRequestContext` surface plus the source generator that emits it. Domain code references the abstractions to read caller identity, scopes, impersonation, and propagated operational state without pulling DI / AspNetCore / Configuration. The propagated subset travels the `x-d2-context` header between services; full identity rebuilds from the JWT on each hop.

## Packages

| Package                                   | Description                                                                                                                                                |
| ----------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------- |
| [`abstractions/`](abstractions/README.md) | Single home for every spec-driven context primitive — codegen-emitted `IRequestContext` / `MutableRequestContext` / `PropagatedContext` plus hand-written RFC actor-chain and scope-claim parsers. |
| [`source-gen/`](source-gen/README.md)     | Roslyn generator that reads `contracts/{auth,request}-context/*.spec.json` and emits the context interfaces / records into `abstractions/` and `auth/context-abstractions/`. |
