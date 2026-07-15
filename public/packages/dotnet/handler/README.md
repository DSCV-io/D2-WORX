<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# handler/

> Parent: [`public/packages/dotnet/`](../README.md)

The handler stack every operation in every service inherits — the domain-safe contracts, the runtime base handler, and the repo-flavored extension with its provider-pluggable exception classification. CQRS handlers, repo handlers, messaging consumers, and scheduled jobs all build on `BaseHandler`, which emits per-call OTel metrics and a span and converts uncaught exceptions into typed `D2Result` failures. Domain code references the abstractions to declare handler contracts without pulling DI / OpenTelemetry / AspNetCore.

## Packages

| Package                                                | Description                                                                                                                            |
| ------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------- |
| [`abstractions/`](abstractions/README.md)              | Domain-safe slice — `IHandler` / `IHandlerContext` interfaces plus the `HandlerOptions` record.                                       |
| [`core/`](core/README.md)                              | `BaseHandler<TSelf, TInput, TOutput>` + `HandlerContext` + `HandlerTelemetry` + `AddD2Handler` — the runtime every handler inherits.  |
| [`repo-abstractions/`](repo-abstractions/README.md)    | Repo-handler vocabulary — `DbFailureKind` enum, `IDbExceptionClassifier`, and parallel `D2Result` extension factories.                |
| [`repo/`](repo/README.md)                              | EF-flavored `BaseRepoHandler` converting captured exceptions into typed `D2Result` failures via an injected classifier.               |
| [`repo-postgres/`](repo-postgres/README.md)            | PostgreSQL `IDbExceptionClassifier` implementation — owns the SQLSTATE matrix and exception-wrapping rules.                            |
