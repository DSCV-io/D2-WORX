<!--
Copyright (c) DCSV. All rights reserved.
-->

# `@d2/contract-tests` — Cross-language parity test infrastructure

> Parent: [`server/shared/typescript/`](../README.md)

Fixture-driven one-way parity tests. The .NET test suite emits canonical fixture JSON files; this package's Vitest tests read those fixtures and assert that the TS-side spec-emitted decoders / encoders / catalogs agree byte-for-byte (after canonicalization).

A drift in either emitter — .NET source-generator output OR `tools/ts-codegen` output — surfaces in the next CI run as a parity-test failure.

## Public API

This package is an internal test workspace. Two helpers are exported for the parity tests themselves and are reusable in any test package needing JSON canonicalization for cross-language comparison:

| Export                              | Purpose                                                                                                                |
| ----------------------------------- | ---------------------------------------------------------------------------------------------------------------------- |
| `canonicalize(value)`               | Recursive deep-canonicalize — sort object keys lexicographically, preserve array order, return primitives as-is.       |
| `canonicalJson(value)`              | Same as `canonicalize` but returns a JSON string for byte-equal comparisons.                                           |
| `loadFixture<T>(catalog, scenario)` | Read and parse a fixture file at `fixtures/<catalog>/<scenario>.json`. Returns the envelope `{ scenario, data, ... }`. |
| `fixtureUrl(catalog, scenario)`     | Resolve a fixture file URL relative to the package root.                                                               |

## Configuration

None. Coverage thresholds explicitly do NOT apply: this is a TEST package whose source IS test infrastructure, not production code. The per-package 100/100/100/100 threshold convention applies to packages with shippable production code; parity tests assert against external fixtures and don't carry their own internal logic to cover.

## Dependencies

| Package                                                                             | Why                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                  |
| ----------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `@d2/auth-context-abstractions`                                                     | Exports `IAuthContext` shape + `IAuthContextRedactPaths` + enum types for parity assertion.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                          |
| `@d2/request-context-abstractions`                                                  | Exports `IRequestContext` shape + `IRequestContextRedactPaths` + `IPropagatedContext` + `PropagatedContextSerializer` for round-trip parity.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                         |
| `@d2/auth-abstractions`                                                             | Exports `JwtPayload` typed shape + `JwtClaimTypes` + `Scopes` + `AuthErrorCodes` + `AuthFailures` for parity assertion.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                              |
| `@d2/headers-common` / `@d2/headers-http` / `@d2/headers-amqp` / `@d2/headers-grpc` | Wire-protocol header catalogs whose membership and wire values must match the .NET-emitted catalogs entry-for-entry.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                 |
| `@d2/headers`                                                                       | RFC 7807 ProblemDetails wire-format catalog (`PROBLEM_TYPE_URI_PREFIX` + `ProblemDetailsExtensionKeys` + `ProblemDetailsTitles`) whose membership and wire values must match the .NET-emitted `D2ProblemDetailsExtensions` partial-class constants.                                                                                                                                                                                                                                                                                                                                                                                  |
| `@d2/result`                                                                        | Generic D2Result error-codes catalog (`ErrorCodes` + `ALL_ERROR_CODES` + `getErrorHttpStatus`) whose membership and wire values must match the .NET-emitted `D2.Shared.Result.ErrorCodes` codegen output, plus the per-code HTTP status mapping. Also exports `TkMessageWireShape` / `InputErrorWireShape` / `D2ResultEnvelopeFieldNames` whose property-name constants must match the .NET-emitted `D2.Shared.I18n.TkMessageWireShape` / `D2.Shared.Result.InputErrorWireShape` / `D2.Shared.Result.D2ResultEnvelopeFieldNames` constants, plus the `TKMessage` / `InputError` interfaces used by the round-trip parity assertions. |
| `@d2/caching-abstractions` / `@d2/caching-local-default` / `@d2/caching-distributed-redis` / `@d2/caching-tiered` | Twin-pin constants for `caching-twin` parity (defaults, meters, Lua bodies, tiered closed-set op names / EventId semantics). |

## Architectural shape

```
.NET test suite                              TS Vitest test suite
[Trait("Category","ContractFixtures")]       (in @d2/contract-tests)
       |                                              ^
       v                                              |
  Emit fixture JSON                              Read fixture JSON
  to disk                                        from disk
       |                                              |
       └────────────► same path ◄────────────────────┘
            server/shared/typescript/
            contract-tests/fixtures/
            <catalog>/<scenario>.json
```

The fixture file is the meeting point. Both sides reference the SAME file path. There is NO subprocess, NO JSON-RPC bridge, NO live spawn-and-roundtrip — the comparison is entirely deterministic across two independent runs (one per language).

Forward-only: `.NET → fixture → TS read+assert`. Bidirectional (TS-emit → `.NET-read+assert`) is out of scope for this package — any TS-side encoder asserting against a .NET decoder belongs in a separate test surface (e.g. its own `*-contract-tests` package).

## Fixture file format

Each fixture is a pretty-printed JSON envelope:

```json
{
  "$comment": "Synthetic test data; no real PII. Generated by D2.Shared.Tests.",
  "scenario": "full",
  "data": { "...payload that the parity test compares..." }
}
```

The `data` field is what the parity test asserts on (after canonicalization). The metadata fields annotate the file for human readers in PR diffs.

Synthetic test data only — no real PII. Per RFC 5737 the fixtures use IPs from `192.0.2.0/24`; per RFC 2606 emails use `*.invalid`; user/org IDs are synthetic UUIDs (`00000000-0000-0000-0000-000000000001` etc.).

## Catalogs covered

| Catalog                 | Fixture path                                    | What's compared                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       |
| ----------------------- | ----------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `propagated-context/`   | `fixtures/propagated-context/<scenario>.json`   | TS `PropagatedContextSerializer.serialize`/`tryDecode` round-trips against the JSON the .NET serializer emits.                                                                                                                                                                                                                                                                                                                                                                                                                                                        |
| `auth-context/`         | `fixtures/auth-context/<scenario>.json`         | The serialized IAuthContext property surface (camelCase, omit-null) matches the TS-side IAuthContext shape.                                                                                                                                                                                                                                                                                                                                                                                                                                                           |
| `request-context/`      | `fixtures/request-context/<scenario>.json`      | The serialized IRequestContext property surface (transitive IAuthContext + own properties) matches the TS-side IRequestContext shape.                                                                                                                                                                                                                                                                                                                                                                                                                                 |
| `jwt-payload/`          | `fixtures/jwt-payload/<scenario>.json`          | A claims dictionary on the .NET side serializes to the same wire shape as the TS-side JwtPayload type.                                                                                                                                                                                                                                                                                                                                                                                                                                                                |
| `redact-paths/`         | `fixtures/redact-paths/<scenario>.json`         | The TS-side `IAuthContextRedactPaths` / `IRequestContextRedactPaths` arrays exactly match the .NET-emitted paths (deep-equal, sorted).                                                                                                                                                                                                                                                                                                                                                                                                                                |
| `headers/`              | `fixtures/headers/<catalog>.json`               | TS-side `CommonHeaders` / `HttpHeaders` / `AmqpHeaders` / `GrpcHeaders` `as const` membership matches the .NET-emitted `{ constName: wireValue }` map.                                                                                                                                                                                                                                                                                                                                                                                                                |
| `problem-details/`      | `fixtures/problem-details/<scenario>.json`      | TS-side `PROBLEM_TYPE_URI_PREFIX` + `ProblemDetailsExtensionKeys` + `ProblemDetailsTitles` byte-equal to the .NET-emitted `D2ProblemDetailsExtensions` partial-class constants. Three scenarios: `uri-prefix`, `extension-keys`, `titles`.                                                                                                                                                                                                                                                                                                                            |
| `error-codes/`          | `fixtures/error-codes/<scenario>.json`          | TS-side `ErrorCodes` + `getErrorHttpStatus` byte-equal to the .NET-emitted `D2.Shared.Result.ErrorCodes` codegen output. Two scenarios: `codes` (constName → wire value map) and `http-statuses` (code → HTTP status map).                                                                                                                                                                                                                                                                                                                                            |
| `tk-message/`           | `fixtures/tk-message/<scenario>.json`           | TS-side `TkMessageWireShape` byte-equal to the .NET-emitted `D2.Shared.I18n.TkMessageWireShape` constants. Four scenarios: `shape` (constName → wire value map), `round-trip-no-params` / `round-trip-with-params` / `round-trip-with-multiple-params` (canonical JSON shapes produced by `TKMessageJsonConverter` — proves wire serializer ↔ TS parser composition).                                                                                                                                                                                                 |
| `input-error/`          | `fixtures/input-error/<scenario>.json`          | TS-side `InputErrorWireShape` byte-equal to the .NET-emitted `D2.Shared.Result.InputErrorWireShape` constants. Four scenarios: `shape` (constName → wire value map), `round-trip-single-error` / `round-trip-multiple-errors` / `round-trip-dot-notation-field` (canonical JSON shapes including nested TKMessage envelopes — proves wire-shape composition across catalog boundaries).                                                                                                                                                                               |
| `d2result-envelope/`    | `fixtures/d2result-envelope/<scenario>.json`    | TS-side `D2ResultEnvelopeFieldNames` byte-equal to the .NET-emitted `D2.Shared.Result.D2ResultEnvelopeFieldNames` constants. Six scenarios: `field-names` (constName → wire value map), `round-trip-ok` / `round-trip-ok-with-data` / `round-trip-not-found` / `round-trip-validation-failed` / `round-trip-with-trace-id` (canonical JSON shapes produced by `JsonSerializer.Serialize(D2Result)` — proves the BFF-gateway envelope's 7-field wire shape end-to-end, including the catalog-pin guard that no `D2Result.Booleans` discriminator leaks onto the wire). |
| `grpc-trailers/`        | `fixtures/grpc-trailers/<scenario>.json`        | TS-side `D2GrpcTrailers` `as const` membership + wire values byte-equal to the .NET-emitted `D2.Shared.Auth.Grpc.Status.D2GrpcTrailers` constants. Pins the gRPC trailer Metadata-key catalog (e.g. the canonical camelCase `traceId` trailer key matching the HTTP ProblemDetails extension key).                                                                                                                                                                                                                                                                    |
| `otel-messaging-tags/`  | `fixtures/otel-messaging-tags/<scenario>.json`  | TS-side `MessagingActivityTags` `as const` membership + wire values byte-equal to the .NET-emitted `D2.Shared.Messaging.RabbitMq.MessagingActivityTags` constants. Closes the publisher / consumer semconv drift (`messaging.operation` vs `messaging.operation.type`) structurally.                                                                                                                                                                                                                                                                                  |
| `encryption-domains/`   | `fixtures/encryption-domains/<scenario>.json`   | TS-side `EncryptionDomains` closed-enum membership + wire values byte-equal to the .NET-emitted `D2.Shared.Encryption.EncryptionDomains` constants. Cross-language critical — TS-side `@d2/encryption-abstractions` consumes the same closed-enum identifiers when decoding the on-wire encryption frame.                                                                                                                                                                                                                                                             |
| `dlq-failure-metadata/` | `fixtures/dlq-failure-metadata/<scenario>.json` | TS-side `DlqFailureMetadataFields` (JSON property-name constants) + `DlqFailureCauses` (closed-enum cause-string constants) byte-equal to the .NET-emitted `D2.Shared.Messaging.DlqFailureMetadataFields` + `D2.Shared.Messaging.RabbitMq.Subscribing.DlqFailureCauses` catalogs. Consumed by the TS-side `@d2/messaging-abstractions` package.                                                                                                                                                                                                                       |
| `encryption-frame/`     | `fixtures/encryption-frame/<scenario>.json`     | TS-side `EncryptionFrame` binary-layout field-offset + byte-length constants + `CONSTRAINT_*` cap constants byte-equal to the .NET-emitted `D2.Shared.Encryption.EncryptionFrameLayout` constants. Source of truth for the TS-side `@d2/encryption-abstractions` encryption-frame catalog; cross-language byte-for-byte parity on the binary frame is structurally guaranteed — both sides read the same spec.                                                                                                                                                        |
| `caching-twin/`         | `fixtures/caching-twin/constants.json`          | Dual-runtime caching **constants/semantics** (KOM-01..08): local + Redis defaults, OTel meter names/instruments, Lua script bodies (normalized), invalidation channel, tiered EventId/binding semantics. Emitted by `CachingTwinFixtureEmitter`; asserted by `caching-twin.parity.test.ts`. **Not** a full algorithm/behavior interop harness — package-local unit + Testcontainers ITs cover runtime behavior.                                                                                                                                                   |

## Usage

```bash
# 1. Regenerate fixtures (.NET side writes them to disk via TestPaths.RepoRoot()):
dotnet test server/D2.slnx -- --filter-trait "Category=ContractFixtures"

# 2. Run the parity tests (TS side reads them + asserts):
pnpm test:contracts        # from repo root
# or
pnpm --filter @d2/contract-tests test
```

Fixtures are emitted and drift-checked by the active Contract fixture emission lane in `.github/workflows/test.yml`; TS parity assertions run inside the shared-subtree unit lane via this package. The composite local gate remains `pnpm test:contracts` at the repo root (emit + assert).

## Edge cases

- **Single-source fixture path**: the .NET emitter writes to the SAME path the TS test reads from. Path is resolved on the .NET side via `TestPaths.RepoRoot()` (walks up to find `server/D2.slnx`); on the TS side via `fixtureUrl(catalog, scenario)` (URL relative to `import.meta.url`). The directory is `Directory.CreateDirectory`'d on the .NET side so emit-on-fresh-checkout works.
- **Canonicalization rule**: object keys are sorted lexicographically before comparison; arrays preserve order. Whitespace and key insertion order are not parity signals.
- **Idempotency**: every fixture-emitter is deterministic — running the suite twice produces zero `git diff` on the fixtures directory.
- **Drift detection**: any per-VALUE divergence (a header wire-value typo, a missing property, a redact-path swap, a serializer omission rule change) produces a fixture-comparison failure with the specific entry / field named in the diagnostic.
