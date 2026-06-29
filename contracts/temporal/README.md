<!--
Copyright (c) DCSV. All rights reserved.
-->

# `contracts/temporal/`

Temporal adversarial parity fixture — cross-language test cases covering DST transitions, ambiguous times, and leap-year boundaries that both .NET (`NodaTime`) and TypeScript (`@js-temporal/polyfill`) must resolve identically.

## Consumed by

- **TypeScript / TypeSpec** — [`server/shared/typescript/typespec-emitters/`](../../server/shared/typescript/typespec-emitters/README.md) byte-gate tests; `temporal-adversarial.fixture.json` drives cross-language DST + temporal-arithmetic parity assertions
- **.NET** — `server/shared/dotnet/tests/` temporal parity tests read this fixture to assert `NodaTime` produces the expected UTC instants for every adversarial case

This is a test-only parity fixture — no runtime library is generated from it.

## See also

- Codegen pattern + diagnostics: [docs/SRC_GEN.md](../../docs/SRC_GEN.md)
- All contracts: [contracts catalog](../README.md)
