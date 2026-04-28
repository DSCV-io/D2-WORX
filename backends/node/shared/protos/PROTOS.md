# @d2/protos

Generated TypeScript types and gRPC client stubs from Protocol Buffers. Layer 0 — no project dependencies.

## Generation

All TypeScript code is generated from `.proto` files in `contracts/protos/` using:

- **`@bufbuild/buf`** — Build and generate tool
- **`ts-proto`** — TypeScript code generator

Run `pnpm --filter @d2/protos generate` to regenerate.

## Key Exports

| Export                                    | Proto Source     | Description                             |
| ----------------------------------------- | ---------------- | --------------------------------------- |
| `D2ResultProto`                           | `common/v1/`     | Protobuf representation of D2Result     |
| `InputErrorProto`                         | `common/v1/`     | Protobuf representation of input errors |
| `PingServiceClient`                       | `common/v1/`     | Health check gRPC client                |
| `GeoServiceClient`                        | `geo/v1/`        | Geo service gRPC client                 |
| `FindWhoIsRequest/Response`               | `geo/v1/`        | WhoIs lookup messages                   |
| `GetReferenceDataResponse`                | `geo/v1/`        | Geo reference data response             |
| `ContactDTO`, `ContactToCreateDTO`        | `geo/v1/`        | Contact data transfer objects           |
| `CreateContactsRequest/Response`          | `geo/v1/`        | Batch contact creation                  |
| `GetContactsByExtKeysRequest/Response`    | `geo/v1/`        | Contact lookup by ext keys              |
| `DeleteContactsByExtKeysRequest/Response` | `geo/v1/`        | Delete contacts by ext keys             |
| `UpdateContactsByExtKeysRequest/Response` | `geo/v1/`        | Replace contacts at ext keys            |
| `CountryDTO`, `LocationDTO`, etc.         | `geo/v1/`        | Geo domain DTOs                         |
| `GeoRefDataUpdatedEvent`                  | `events/v1/`     | Geo reference data refresh event        |
| `CommsServiceClient`                      | `comms/v1/`      | Comms service gRPC client               |
| `GetChannelPreferenceRequest/Response`    | `comms/v1/`      | Channel preference query                |
| `SetChannelPreferenceRequest/Response`    | `comms/v1/`      | Channel preference update               |
| `Timestamp`                               | Well-known types | Google protobuf timestamp               |

## Dependencies

- `@bufbuild/protobuf` — Runtime protobuf support (BinaryReader/BinaryWriter)
- `@grpc/grpc-js` — gRPC client runtime

## Notes

- `verbatimModuleSyntax: false` in `tsconfig.json` — generated code doesn't comply with strict module syntax
- Proto source at `contracts/protos/` is the single source of truth — both .NET and Node.js read from there
- **`useOptionals=all`** is set in `buf.gen.yaml` — all proto fields generate as `field?: T | undefined` in TypeScript (both proto3 default-value fields and `optional`-keyword fields)
- **Proto3 `optional` keyword** is used on all nullable domain fields (65 fields across geo, comms, common protos). The domain model is the source of truth for nullability — if a field is `?: T` in the domain entity, it MUST be `optional` in the proto
- **C# proto behavior**: Optional string fields still return `""` when unset (protobuf default). Use the generated `HasXxx` property to distinguish "not set" from "empty string." C# mappers use conditional assignment + `.ToNullIfEmpty()` to convert proto empty strings to `null` at the boundary

## .NET Equivalent

`Protos.DotNet` — Generated C# protobuf types via `Grpc.Tools`.
