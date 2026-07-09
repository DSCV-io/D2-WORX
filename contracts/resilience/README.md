<!--
Copyright (c) DCSV. All rights reserved.
-->

# `contracts/resilience/`

Resilience predicate parity fixtures — cross-language test cases that feed identically-shaped `D2Result` values to the emitted C# and TypeScript `@d2Resilience` retry/fail predicates and assert both produce the same boolean outcomes.

## Consumed by

- **TypeSpec** — [`server/shared/typescript/typespec-decorators/`](../../server/shared/typescript/typespec-decorators/README.md) reads the resilience predicate specs to validate `@d2Resilience` decorator arguments at compile time
- **TypeScript** — [`server/shared/typescript/typespec-emitters/`](../../server/shared/typescript/typespec-emitters/README.md) byte-gate tests; `predicate-parity.fixture.json` and `predicate-parity-nested.fixture.json` drive cross-language predicate parity assertions

These are test-only parity fixtures — no runtime library is generated from them.

## See also

- Codegen pattern + diagnostics: [docs/SRC_GEN.md](../../docs/SRC_GEN.md)
- All contracts: [contracts catalog](../README.md)
