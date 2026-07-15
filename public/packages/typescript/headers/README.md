<!--
Copyright (c) DCSV. All rights reserved.
-->

# headers/

> Parent: [`public/packages/typescript/`](../README.md)

The D² wire-protocol header catalogs for TS consumers, split per transport, plus the SvelteKit BFF-side glue that consumes them. The per-transport catalogs are codegen-emitted from the same `contracts/headers/headers.spec.json` spec that drives the .NET `DcsvIo.D2.Headers.*` catalogs, so a header that appears on multiple transports carries an identical wire value across both languages. The `core/` package is the BFF-side glue (JWT decode, `x-d2-context` decode, ProblemDetails builder, route guards) — it has no .NET counterpart.

## Packages

| Package                       | Description                                                                                                          |
| ----------------------------- | ------------------------------------------------------------------------------------------------------------------ |
| [`core/`](core/README.md)     | SvelteKit BFF-side glue — JWT claim decode, `x-d2-context` decode, RFC 7807 ProblemDetails builder, server-side route guards. |
| [`common/`](common/README.md) | Cross-transport headers (`PROPAGATED_CONTEXT`, `TRACEPARENT`, `TRACESTATE`, `AUTHORIZATION`). Mirrors `DcsvIo.D2.Headers.Common`. |
| [`http/`](http/README.md)     | HTTP-applicable headers (HTTP-only entries plus cross-transport entries inline). Mirrors `DcsvIo.D2.Headers.Http`. |
| [`amqp/`](amqp/README.md)     | AMQP-applicable headers (AMQP-only entries plus cross-transport entries inline). Mirrors `DcsvIo.D2.Headers.Amqp`. |
| [`grpc/`](grpc/README.md)     | gRPC-applicable headers. Mirrors `DcsvIo.D2.Headers.Grpc`.                                                          |
