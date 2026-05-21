<!--
Copyright (c) DCSV. All rights reserved.
-->

# server/shared/ — Shared Libraries

> Parent: [`server/`](../README.md)

Cross-service libraries grouped by language — `.NET` for backend services and `TypeScript` for the SvelteKit BFF (and any other Node-side workspace package). Most catalogs that span both languages are spec-driven (single JSON spec under `contracts/`, codegen on each side) so cross-language drift is structurally impossible.

## Layout

| Path | What |
|---|---|
| [`dotnet/`](dotnet/README.md) | Shared .NET libraries (result, utilities, resilience, i18n, auth, request-context, handler stack, repo handler, caching, messaging, encryption, telemetry, headers catalogs, codegen analyzers) |
| [`typescript/`](typescript/README.md) | Shared TypeScript packages mirroring the .NET surface where parity matters (result, utilities, resilience, i18n, logging, telemetry, service-defaults, protos, auth-context-abstractions, request-context-abstractions, auth-abstractions, the four `headers-*` catalogs, plus the SvelteKit-side `headers` glue + `grpc-client`). Includes the `contract-tests/` private workspace package that asserts cross-language parity on spec-emitted artifacts. |
