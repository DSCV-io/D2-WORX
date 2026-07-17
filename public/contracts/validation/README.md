<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# `contracts/validation/`

Validation rule catalog and parity fixtures — `field-constraints.spec.json` defines field-length limits, named taxonomy enums (`NamePrefix`, `NameSuffix`, `BiologicalSex`), and other per-field constraints; the `fixtures/` subdirectory holds cross-language parity cases for email, phone, and postal-code validators.

## Consumed by

- **.NET** — [`public/packages/dotnet/validation/source-gen/`](../../public/packages/dotnet/validation/source-gen/README.md) (Roslyn source-gen → `FieldConstraints` const-object + taxonomy enum types in `DcsvIo.D2.Validation.Abstractions`)
- **TypeScript** — [`tools/ts-codegen` › `field-constraints-emit.ts`](../../tools/ts-codegen/README.md) (→ `FieldConstraints` const-object + Zod-validated taxonomy types in `@dcsv-io/d2-validation-abstractions`)

## See also

- Codegen pattern + diagnostics: [docs/SRC_GEN.md](../../docs/SRC_GEN.md)
- All contracts: [contracts catalog](../README.md)
