<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# `contracts/typespec/`

TypeSpec operation contracts — the hand-authored `.tsp` files that define service operations, models, and emitter test fixtures. The TypeSpec compiler + the D² emitter fleet transform these contracts into TypeScript client stubs, OpenAPI documents, gRPC service definitions, and protocol-buffer schemas.

## Layout (per-module packages)

```
contracts/typespec/
├── README.md                              — this file
├── tspconfig.yaml                         — RETIRED pointer (do not compile as primary)
├── common/
│   └── temporal.tsp                       — shared temporal scalar types
├── fixtures/                              — emitter test fixtures (NOT live product ops)
│   ├── enum-shaped.tsp
│   ├── temporal-shaped.tsp
│   ├── sign-shaped.tsp
│   ├── server-push-shaped.tsp
│   ├── resilience-predicate-shaped.tsp
│   └── openapi-shaped.tsp
├── key-custodian/
│   ├── key-custodian.tsp                  — KeyCustodian live operations
│   └── tspconfig.yaml                     — KC emit options (edge-module) + co-fixtures
└── audit/
    ├── audit.tsp                          — Audit standalone multiproc stub (PingAudit)
    └── tspconfig.yaml                     — Audit emit options (standalone)
```

**Law:** one live module folder = one compile unit. Each module owns `tspconfig.yaml` beside its `.tsp`. Root `tspconfig.yaml` is **not** the KeyCustodian config and must not be used as the primary emit entry.

## Regen

```text
pnpm --filter @dcsv-io/d2-typespec-emitters regen
# or
node tools/scripts/regen-typespec-emitters.mjs
```

The runner executes **N× (tsp compile packageᵢ + COPY_MANIFEST subset for packageᵢ)** in order (KeyCustodian package, then Audit). It never batch-compiles into the shared `dist/generated` then runs one COPY (second compile would clobber the first). Nested `emitter-output-dir` values resolve to the **same** shared `public/packages/typescript/typespec-emitters/dist/generated` (extra `../` from module folders).

## Consumed by

- **TypeScript / TypeSpec** — [`public/packages/typescript/typespec-emitters/`](../../public/packages/typescript/typespec-emitters/README.md) (the emitter fleet); the [`@d2Resilience`](../../public/packages/typescript/typespec-decorators/README.md) decorator library supplies custom decorators

## See also

- Codegen pattern + diagnostics: [docs/SRC_GEN.md](../../docs/SRC_GEN.md)
- All contracts: [contracts catalog](../README.md)
