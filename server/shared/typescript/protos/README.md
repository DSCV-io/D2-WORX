<!--
Copyright (c) DCSV. All rights reserved.
-->

# @d2/protos

Buf-generated TS modules + gRPC client stubs from `contracts/protos/`.
Mirrors `D2.Shared.Protos` (.NET — generated via `Grpc.Tools` at csproj
build time).

## Public API

The package re-exports every generated module under `src/generated/`. The
generation workflow (Buf + ts-proto) runs via `pnpm generate` and writes
output that is committed to git so consumers don't need to run codegen on
first build. As `contracts/protos/{namespace}/v1/*.proto` files land they
auto-generate into `src/generated/{namespace}/v1/{name}.ts`.

## Dependencies

- `@bufbuild/protobuf` — runtime types for proto-generated code.
- `@grpc/grpc-js` — gRPC client transport.
- `@bufbuild/buf` (devDep) — codegen tooling.
- `ts-proto` (devDep) — protoc plugin emitting TS.

## Generation workflow

```bash
pnpm --filter @d2/protos generate
```

`buf.gen.yaml` configures `ts-proto` with options matching the v1 reference:
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
