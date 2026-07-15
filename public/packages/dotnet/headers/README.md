<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# headers/

> Parent: [`public/packages/dotnet/`](../README.md)

The D² wire-protocol header catalogs, split per transport, for every service that reads or writes D² headers on HTTP, AMQP, or gRPC. All catalogs are codegen-emitted from a single `contracts/headers/headers.spec.json` spec, so a header that appears on multiple transports carries an identical wire value across all of them — drift is structurally impossible. The same spec drives the TS-side `@dcsv-io/d2-headers-*` packages. Each per-transport catalog has zero runtime dependencies.

## Packages

| Package                                   | Description                                                                                                          |
| ----------------------------------------- | ------------------------------------------------------------------------------------------------------------------- |
| [`common/`](common/README.md)             | Cross-transport headers (`PROPAGATED_CONTEXT`, `TRACEPARENT`, `TRACESTATE`, `AUTHORIZATION`).                        |
| [`http/`](http/README.md)                 | HTTP-applicable headers (`IDEMPOTENCY_KEY`, `CLIENT_FINGERPRINT`, `INTERNAL_TOKEN` plus cross-transport entries).    |
| [`amqp/`](amqp/README.md)                 | AMQP-applicable headers (`MESSAGE_ID`, `PROTO_TYPE`, `ENCRYPTION_KID`, `FAILURE_REASON` plus cross-transport entries). |
| [`grpc/`](grpc/README.md)                 | gRPC-applicable headers (`AUTHORIZATION` plus cross-transport entries).                                             |
| [`source-gen/`](source-gen/README.md)     | Roslyn generator emitting the per-transport catalog classes into the four catalog packages from the headers spec.   |
