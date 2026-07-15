# ts-codegen — Emitter Validation Ledger

This ledger names every emitter module in `src/`, what each is validated against, and the replace-trigger for any test doubles. All entries must remain current; update this file in the same change as any emitter addition, rename, or validation-strategy change.

---

## Emitter modules

### Core emitters (`src/*.ts`)

| Emitter | Test file(s) | Validated against | Test-double replace-trigger |
|---------|-------------|-------------------|-----------------------------|
| `error-codes-emit.ts` | `error-codes-emit.test.ts`, `error-codes-deprecation-emit.test.ts`, `error-codes-emit-propagation.test.ts`, `error-codes-byte-parity.test.ts`, `auth-error-codes-emit.test.ts`, `auth-failures-emit.test.ts` | Synthetic `ErrorCodesSpec` fixtures (unit); real `contracts/error-codes/error-codes.spec.json` + `contracts/auth-error-codes/auth-error-codes.spec.json` via byte-parity golden test; no test doubles | N/A — no doubles |
| `error-codes-registry-emit.ts` | `error-codes-registry-emit.test.ts` | Synthetic spec fixtures; real committed `.g.ts` files via byte-parity assertions | N/A — no doubles |
| `error-category-emit.ts` | `error-category-emit.test.ts` | Synthetic `ErrorCategorySpec` fixtures; real `contracts/error-codes/error-category.spec.json` via in-memory regeneration assertion | N/A — no doubles |
| `auth-scopes-emit.ts` | `auth-scopes-emit.test.ts` | Synthetic `ScopesSpec` fixtures | N/A — no doubles |
| `auth-context-emit.ts` | `auth-context-emit.test.ts` | Synthetic fixtures | N/A — no doubles |
| `jwt-claims-emit.ts` | `jwt-claims-emit.test.ts` | Synthetic `JwtClaimsSpec` fixtures | N/A — no doubles |
| `headers-emit.ts` | `headers-emit.test.ts` | Synthetic `HeadersSpec` fixtures | N/A — no doubles |
| `request-context-emit.ts` | `request-context-emit.test.ts` | Synthetic `ContextSpec` fixtures | N/A — no doubles |
| `d2result-envelope-emit.ts` | `d2result-envelope-emit.test.ts` | Synthetic `EnvelopeSpec` fixtures | N/A — no doubles |
| `wire-shape-emit.ts` | `wire-shape-emit.test.ts` | Synthetic `WireShapeSpec` fixtures | N/A — no doubles |
| `field-constraints-emit.ts` | `field-constraints-emit.test.ts`, `field-constraints-emit-propagation.test.ts`, `field-constraints-spec-parity.test.ts` | Synthetic `FieldConstraintsSpec` fixtures; real constraints spec via parity test | N/A — no doubles |
| `problem-details-emit.ts` | `problem-details-emit.test.ts` | Synthetic `ProblemDetailsSpec` fixtures | N/A — no doubles |
| `grpc-trailers-emit.ts` | _(no dedicated test yet — tracked below)_ | — | Replace double when grpc-trailers consumer is wired |
| `otel-messaging-tags-emit.ts` | _(no dedicated test yet — tracked below)_ | — | Replace double when otel-messaging consumer is wired |
| `dlq-failure-metadata-emit.ts` | _(no dedicated test yet — tracked below)_ | — | Replace double when DLQ consumer is wired |
| `encryption-frame-emit.ts` | _(no dedicated test yet — tracked below)_ | — | Replace double when encryption-frame consumer is wired |
| `encryption-domains-emit.ts` | _(no dedicated test yet — tracked below)_ | — | Replace double when encryption-domains consumer is wired |
| `tk-keys-emit.ts` | `tk-keys-emit.test.ts` | Synthetic fixtures | N/A — no doubles |
| `mq-messages-emit.ts` | `mq-messages-emit.test.ts`; `mq-messages.parity.test.ts` (`@dcsv-io/d2-contract-tests`) | Synthetic `MqMessagesSpec` fixtures + the `D2MQ001–006` validation diagnostics (unit — `mq-messages-emit.test.ts`, mirroring the `.NET` `DcsvIo.D2.Messaging.SourceGen.MqGenerator` predicate surface); real `contracts/mq-messages/mq-messages.spec.json` via the two-sided `.NET`↔TS parity test (`.NET` `MqMessagesFixtureEmitter` → `fixtures/mq-messages/registry.json` ↔ TS `MqMessagesRegistry`: membership + per-descriptor field-by-field + whole-registry canonical byte-equality); no test doubles | N/A — no doubles |
| `orchestrator.ts` | _(integration — driven indirectly by byte-parity tests)_ | Real spec files via the byte-parity golden suite | N/A |

### Geo sub-emitters (`src/geo-emitter/`)

| Emitter | Test file(s) | Validated against | Test-double replace-trigger |
|---------|-------------|-------------------|-----------------------------|
| `enum-emit.ts` | _(byte-parity via geo pipeline test suite in .NET)_ | Real `.spec.json` data via the .NET `GeoEnumsFixtureEmitter` byte-parity assertions | N/A |
| `record-shape-emit.ts` | _(byte-parity via geo pipeline)_ | Same as above | N/A |
| `wrapper-code-emit.ts` | _(byte-parity via geo pipeline)_ | Same as above | N/A |
| `geo-catalog-emit.ts` | _(byte-parity via geo pipeline)_ | Real `GeoCatalog` via `.NET` parity | N/A |
| `records-meta-emit.ts` | _(byte-parity via geo pipeline)_ | Same as above | N/A |
| `catalog-uniqueness.ts` | _(utility; exercised indirectly by geo pipeline)_ | — | N/A |
| `emit-helpers.ts` | _(utility; exercised indirectly)_ | — | N/A |
| `field-classification.ts` | _(utility; exercised indirectly)_ | — | N/A |
| `spec-loader.ts` | _(exercised indirectly via geo pipeline)_ | — | N/A |
| `vocabulary-guard.ts` | _(exercised indirectly)_ | — | N/A |
| `index.ts` | _(entry point; exercised indirectly)_ | — | N/A |
| `cli.ts` | _(CLI entry; not unit-tested directly)_ | — | N/A |

### Geo default data emitters (`src/geo-emitter/default/`)

All default data emitters are validated via the .NET byte-parity golden suite (`GeoEnumsFixtureEmitter`, `GeoCatalogFixtureEmitter`, `GeoWrapperStructsFixtureEmitter`) which regenerates from the committed spec and asserts byte-level identity against the committed `.g.ts` files.

| Emitter | Primary validation mechanism |
|---------|------------------------------|
| `country-data-emit.ts` | .NET byte-parity + real geo spec data |
| `currency-data-emit.ts` | .NET byte-parity + real geo spec data |
| `language-data-emit.ts` | .NET byte-parity + real geo spec data |
| `locale-data-emit.ts` | .NET byte-parity + real geo spec data |
| `subdivision-data-emit.ts` | .NET byte-parity + real geo spec data |
| `timezone-data-emit.ts` | .NET byte-parity + real geo spec data |
| `geopolitical-entity-data-emit.ts` | .NET byte-parity + real geo spec data |
| `geo-data-initializer-emit.ts` | .NET byte-parity + real geo spec data |
| `paths.ts` | Utility; exercised indirectly |

### Shared library (`src/lib/`)

| Module | Test file(s) | Notes |
|--------|-------------|-------|
| `diagnostics.ts` | `diagnostics.test.ts` | Unit-tested directly |
| `file-emit.ts` | `file-emit.test.ts` | Unit-tested directly |
| `paths.ts` | `paths.test.ts` | Unit-tested directly |
| `spec-loader.ts` | `spec-loader.test.ts` | Unit-tested directly |
| `string-builder.ts` | `string-builder.test.ts` | Unit-tested directly |
| `tk-key-transform.ts` | `tk-key-transform.test.ts` | Unit-tested directly |

---

## Emitters with no dedicated test yet

The following emitters are committed without a standalone test file. Each is exercised indirectly by the byte-parity or orchestrator test suites, but a dedicated test must be added when a consuming host is first wired.

| Emitter | Gap | Replace-trigger |
|---------|-----|-----------------|
| `grpc-trailers-emit.ts` | No `grpc-trailers-emit.test.ts` | Add test when DcsvIo.D2.Grpc.Trailers source-gen consumer is wired in Edge |
| `otel-messaging-tags-emit.ts` | No `otel-messaging-tags-emit.test.ts` | Add test when OTel messaging consumer is wired |
| `dlq-failure-metadata-emit.ts` | No `dlq-failure-metadata-emit.test.ts` | Add test when DLQ metadata consumer is wired |
| `encryption-frame-emit.ts` | No `encryption-frame-emit.test.ts` | Add test when encryption-frame consumer is wired |
| `encryption-domains-emit.ts` | No `encryption-domains-emit.test.ts` | Add test when encryption-domains consumer is wired |

---

## Inert spec fields

The following fields are declared on emitter input types but intentionally not consumed by any emitter today. Each has an inertness-asserting test confirming the field does not appear in emitted output.

| Field | Declared on | Inertness test | Notes |
|-------|-------------|----------------|-------|
| `sunset` | `ErrorCodeEntry` (`error-codes-emit.ts`) | `error-codes-deprecation-emit.test.ts` — "sunset field — inert; never appears in emitted output" | Forward-registration slot for RFC 8594 `Sunset` response header; consumed by Edge response middleware when that middleware exists |
