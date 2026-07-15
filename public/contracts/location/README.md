<!--
Copyright (c) DCSV. All rights reserved.
-->

# `contracts/location/`

Location parity-fixture — hand-maintained cross-language hash-determinism test cases for `D2.Shared.Location` value objects (`Coordinates`, `StreetAddress`, `AdminLocation`, and composed location hashes). Each case pins an expected SHA-256 hash ID.

## Consumed by

- **.NET** — [`public/packages/dotnet/tests/Unit/Location/LocationHashDeterminismTests.cs`](../../public/packages/dotnet/tests/Unit/Location/LocationHashDeterminismTests.cs) loads `parity-fixtures.json` at test runtime and asserts byte-identical hash output for every case

This is a .NET-only parity fixture — no library is generated from it. The former TypeScript `@d2/location` package and its fixture emitter were removed. Update this file by hand when adding new hash-determinism cases.

## See also

- Codegen pattern + diagnostics: [docs/SRC_GEN.md](../../docs/SRC_GEN.md)
- All contracts: [contracts catalog](../README.md)
