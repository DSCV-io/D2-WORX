<!--
Copyright (c) DCSV. All rights reserved.
-->

# @d2/protos

> Parent: [`server/shared/typescript/`](../README.md)

Buf-generated TS modules + gRPC client stubs from `contracts/protos/`.
Mirrors `D2.Shared.Protos` (.NET — generated via `Grpc.Tools` at csproj
build time).

## Public API

The package re-exports every generated module under `src/generated/`. The
generation workflow (Buf + ts-proto) runs via `pnpm generate` and writes
output that is committed to git so consumers don't need to run codegen on
first build. Each `contracts/protos/{namespace}/v1/*.proto` file
auto-generates into `src/generated/{namespace}/v1/{name}.ts` via the
build's `generate` step.

Current re-exports from `src/index.ts`:

| Export              | Source file                             | Purpose                                                                   |
| ------------------- | --------------------------------------- | ------------------------------------------------------------------------- |
| `D2ResultProto`     | `generated/common/v1/d2_result.ts`      | Full `D2Result` wire envelope (success, status, errorCode, category, …).  |
| `TKMessageProto`    | `generated/common/v1/d2_result.ts`      | Translation-key message with `map<string,string> params`.                 |
| `InputErrorProto`   | `generated/common/v1/d2_result.ts`      | Field-level validation error: field name + `TKMessageProto[]` errors.     |

The codec (`d2ResultToProto` / `d2ResultFromProto` / `handleGrpcCall`) that
converts between `D2Result` and `D2ResultProto` lives in `@d2/grpc-client`,
not here — this package is the generated-stub layer only.

## Dependencies

- `@bufbuild/protobuf` — runtime types for proto-generated code.
- `@grpc/grpc-js` — gRPC client transport.
- `@bufbuild/buf` (devDep) — codegen tooling.
- `ts-proto` (devDep) — protoc plugin emitting TS.

## Generation workflow

```bash
pnpm --filter @d2/protos generate
```

`buf.gen.yaml` configures `ts-proto` with these options:
`esModuleInterop=true`, `outputServices=grpc-js`, `useExactTypes=false`,
`oneof=unions`, `useOptionals=messages`. Output is per-file under
`src/generated/{namespace}/v1/{name}.ts`.

## Parity with .NET

Every `.proto` file generates into BOTH:

- .NET: `D2.Shared.Protos` (via `Grpc.Tools` + csproj `<Protobuf Include>`)
- TS: `@d2/protos/src/generated/...` (via Buf + ts-proto)

The `contracts/protos/` directory is the single source of truth — neither
side hand-writes proto modules.

## Edge cases

- Codegen idempotency: re-running `pnpm generate` produces zero diff if
  no `.proto` changes. Spec drift surfaces in PR review.
- Generated code is committed to git so consumers don't pay a
  generate-on-first-build penalty.
- Generated code is excluded from coverage thresholds and from ESLint
  formatting (lives in `src/generated/`).
