<!--
Copyright (c) DCSV. All rights reserved.
-->

# `contracts/keycustodian-error-codes/`

KeyCustodian error-code catalog — the domain-specific failure codes for the KeyCustodian module (key not found, rotation conflict, CA seeding failure, capability-authority denial, etc.) with their HTTP status, error category, and user-message key.

The capability-authority denials are `403 policy_denied`: `KEYCUSTODIAN_REQUEST_ORIGIN_UNESTABLISHED` (the request origin was not positively established by any trust boundary — the fail-closed default), `KEYCUSTODIAN_MINTER_CAPABILITY_REQUIRED` (the cluster-signing root `jwks-signing` is structurally unreachable on the general signing surface — reachable only through the dedicated minter capability), `KEYCUSTODIAN_SIGNING_DOMAIN_NOT_AUTHORIZED` (the requested signing domain is not in the caller's allowed-signing-domains policy set), and the retained `KEYCUSTODIAN_CROSS_PROCESS_DOMAIN_REJECTED`. Their `userMessageKey`s live under the `TK.Keycustodian.Authorization.*` namespace.

The `sign` operation adds two more failures: `KEYCUSTODIAN_SIGNING_KEY_UNAVAILABLE` (`503 infrastructure_unavailable` — no active signing key exists for the requested domain, a retryable not-ready-yet condition) and `KEYCUSTODIAN_EMPTY_SIGNING_INPUT` (`400 validation_failure` — a zero-length signing payload).

## Consumed by

- **.NET** — [`server/services/edge/key-custodian/error-codes-source-gen/`](../../server/services/edge/key-custodian/error-codes-source-gen/README.md) (service-local Roslyn source-gen shell → `KeyCustodianErrorCodes` constants + `KeyCustodianFailures` typed `D2Result` factories in `D2.Edge.KeyCustodian.Domain`)

No `tools/ts-codegen` emitter consumes this catalog directly, but it is picked up by the merged cross-service error-code registry — see [`contracts/error-codes/`](../error-codes/README.md).

## See also

- Codegen pattern + diagnostics: [docs/SRC_GEN.md](../../docs/SRC_GEN.md)
- All contracts: [contracts catalog](../README.md)
- [ADR-0025](../../docs/adrs/0025-request-context-establishment.md) — the `RequestOrigin` establishment model behind the `REQUEST_ORIGIN_UNESTABLISHED` / `MINTER_CAPABILITY_REQUIRED` denials.
