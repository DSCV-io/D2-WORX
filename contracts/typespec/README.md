<!--
Copyright (c) DCSV. All rights reserved.
-->

# `contracts/typespec/`

TypeSpec operation contracts — the hand-authored `.tsp` files that define service operations, models, and emitter test fixtures. The TypeSpec compiler + the D² emitter fleet transform these contracts into TypeScript client stubs, OpenAPI documents, gRPC service definitions, and protocol-buffer schemas.

## Layout

```
contracts/typespec/
├── common/
│   └── temporal.tsp                    — shared temporal scalar types (Instant, LocalDate, etc.)
├── fixtures/                           — emitter test fixtures (NOT live operations)
│   ├── enum-shaped.tsp                 — enum wire-parity fixture
│   ├── temporal-shaped.tsp             — temporal scalar fixture
│   ├── sign-shaped.tsp                 — signing operation fixture (FixtureKeySigner namespace)
│   ├── server-push-shaped.tsp          — SSE/server-push fixture
│   ├── resilience-predicate-shaped.tsp — @d2Resilience decorator fixture
│   └── openapi-shaped.tsp              — OpenAPI emitter fixture
└── key-custodian/
    └── key-custodian.tsp               — KeyCustodian live operation (GetJwks)
```

## Consumed by

- **TypeScript / TypeSpec** — [`server/shared/typescript/typespec-emitters/`](../../server/shared/typescript/typespec-emitters/README.md) (the emitter fleet — processes `.tsp` files and emits TypeScript stubs, OpenAPI specs, proto definitions, and gRPC client code); the [`@d2Resilience`](../../server/shared/typescript/typespec-decorators/README.md) decorator library supplies the custom decorators these contracts use

The regeneration runner is `tools/scripts/regen-typespec-emitters.mjs`, which drives the TypeSpec compiler + emitter fleet over all contracts in this directory.

## See also

- Codegen pattern + diagnostics: [docs/SRC_GEN.md](../../docs/SRC_GEN.md)
- All contracts: [contracts catalog](../README.md)
