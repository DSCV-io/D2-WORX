<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# @dcsv-io/d2-messaging-abstractions

> Parent: [`public/packages/typescript/`](../README.md)

D2 messaging-protocol wire identifiers. Today: the DLQ failure-metadata wire shape (`DlqFailureMetadataFields` for JSON property names + `DlqFailureCauses` for the closed-enum cause-string catalog). Mirrors .NET `DcsvIo.D2.Messaging.DlqFailureMetadataFields` (in `DcsvIo.D2.Messaging.Abstractions`) and `DcsvIo.D2.Messaging.RabbitMq.Subscribing.DlqFailureCauses` (in `DcsvIo.D2.Messaging.RabbitMq`).

## Public API

| Export                            | Source                      | Mirror                                                      |
| --------------------------------- | --------------------------- | ----------------------------------------------------------- |
| `DlqFailureMetadataFields`        | `dlq-failure-metadata.g.ts` | `DcsvIo.D2.Messaging.DlqFailureMetadataFields`              |
| `DlqFailureMetadataField`         | `dlq-failure-metadata.g.ts` | n/a (TS-only union type)                                    |
| `ALL_DLQ_FAILURE_METADATA_FIELDS` | `dlq-failure-metadata.g.ts` | `DlqFailureMetadataFields.AllFields`                        |
| `DlqFailureCauses`                | `dlq-failure-metadata.g.ts` | `DcsvIo.D2.Messaging.RabbitMq.Subscribing.DlqFailureCauses` |
| `DlqFailureCause`                 | `dlq-failure-metadata.g.ts` | n/a (TS-only union type)                                    |
| `ALL_DLQ_FAILURE_CAUSES`          | `dlq-failure-metadata.g.ts` | `DlqFailureCauses.AllCauses`                                |

## Codegen workflow

`prebuild` invokes `tools/ts-codegen/src/dlq-failure-metadata-emit.ts` before `tsc -b`. Generated files (`*.g.ts`) are committed to git.

## When to reach for this catalog

- **DLQ ops tooling**: `d2 msg inspect-dlq` reads the `x-d2-failure-reason` header from DLQ messages — use the field-name constants to deserialize the JSON shape, use the cause-string constants to dispatch on failure category; importing this catalog keeps the tool in lockstep with the production publisher.
- **TS-side RabbitMQ subscribers**: any handler that may end up generating a DLQ entry (via header forwarding) references the same cause constants.

## Spec contract

`contracts/dlq-failure-metadata/dlq-failure-metadata.spec.json` is the single source of truth. Two sub-catalogs: `fields[]` (property names) + `causes[]` (closed-enum cause strings). The .NET side dispatches the fields-half to `DcsvIo.D2.Messaging.Abstractions` and the causes-half to `DcsvIo.D2.Messaging.RabbitMq`.

## Dependencies

None at runtime — pure constants. DevDeps: `vitest` + `@vitest/coverage-v8` + `typescript`.
