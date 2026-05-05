<!--
Copyright (c) DCSV. All rights reserved.
-->

# server/shared/ — Shared Libraries

> Parent: [`server/`](../README.md)

Cross-service libraries grouped by language. Currently .NET only — the SvelteKit BFF in [`server/web/`](../web/README.md) consumes its own `@d2/*` workspace deps inside the web project itself, not from a sibling `typescript/` tree (yet).

## Layout

| Path | What |
|---|---|
| [`dotnet/`](dotnet/README.md) | Shared .NET libraries (result, utilities, resilience, i18n, auth, request-context, handler stack, repo handler, codegen analyzers) |

A `typescript/` tree may join later if a Node.js service ships and needs cross-package shared code; today the BFF is the only TS consumer and keeps its modules in-tree.
