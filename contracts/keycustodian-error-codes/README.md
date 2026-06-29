<!--
Copyright (c) DCSV. All rights reserved.
-->

# `contracts/keycustodian-error-codes/`

KeyCustodian error-code catalog — the domain-specific failure codes for the KeyCustodian module (key not found, rotation conflict, CA seeding failure, etc.) with their HTTP status, error category, and user-message key.

## Consumed by

- **.NET** — [`server/services/edge/key-custodian/error-codes-source-gen/`](../../server/services/edge/key-custodian/error-codes-source-gen/README.md) (service-local Roslyn source-gen shell → `KeyCustodianErrorCodes` constants + `KeyCustodianFailures` typed `D2Result` factories in `D2.Edge.KeyCustodian.Domain`)

No `tools/ts-codegen` emitter consumes this catalog directly, but it is picked up by the merged cross-service error-code registry — see [`contracts/error-codes/`](../error-codes/README.md).

## See also

- Codegen pattern + diagnostics: [docs/SRC_GEN.md](../../docs/SRC_GEN.md)
- All contracts: [contracts catalog](../README.md)
