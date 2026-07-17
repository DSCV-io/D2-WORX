<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# `contracts/problem-details/`

RFC 7807 problem-details extension-key catalog — the `type` URI prefix and the `d2_`-prefixed extension property names written into ASP.NET Core problem-details responses.

## Consumed by

- **.NET** — [`public/packages/dotnet/problem-details/source-gen/`](../../public/packages/dotnet/problem-details/source-gen/README.md) (Roslyn source-gen → `D2ProblemDetailsKeys` constants in `DcsvIo.D2.ProblemDetails.Abstractions`)
- **TypeScript** — [`tools/ts-codegen` › `problem-details-emit.ts`](../../tools/ts-codegen/README.md) (→ matching `D2ProblemDetailsKeys` constants in `@dcsv-io/d2-problem-details-abstractions`)

## See also

- Codegen pattern + diagnostics: [docs/SRC_GEN.md](../../docs/SRC_GEN.md)
- All contracts: [contracts catalog](../README.md)
