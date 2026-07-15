<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->


> **Visibility: PUBLIC** — ships with the open surface (`public/`).  
> Do not add product IP, private paths, or non-exportable runbooks.
# ADR-0019: Wrapped-result wire model — a `D2Result` crosses any transport faithfully, with zero caller try-catch

- **Status**: Accepted
- **Date**: 2026-06-09
- **Deliverable**: 0017-error-codes

## Context

`D2Result` is the single in-memory representation of an operation outcome (ADR-0003): success or one of a finite set of failures, carrying `success`, exact HTTP `status`, `errorCode`, `category`, `messages` (`TKMessage[]`), `inputErrors`, `traceId`, and `data`. The framework's value proposition is that a caller of *any* downstream — a gRPC service, a REST endpoint — writes `var r = await <call>(...);`, gets a `D2Result<T>`, and branches on it. No `try-catch` at the call site; transport faults are already `D2Result`s by the time the caller sees them; business failures (404 / 409 / 400) cross the wire losslessly and re-materialize as the exact same `D2Result` the producer returned.

The in-memory `D2Result` plus a strong supporting foundation already exist — `TKMessage` objects (not raw key strings), structured `InputError` records, spec-pinned envelope / ProblemDetails field names, the merged registry, and the typed `ErrorCategory` of ADR-0018 — but two per-transport pieces are not yet seated:

- **The codec** — serialize a `D2Result` onto the wire and re-materialize it on the other side losslessly.
- **The resilience wrapper** — catch a transport / infrastructure fault and turn it into an informative `D2Result`, so the caller never writes `try-catch`.

Without them, the wire is leaky in specific ways. On gRPC there is no general business-result serializer; the only mechanism is an auth-only trailer writer that maps a result into a lossy 3-way gRPC status bucket (404 / 409 / 400 collapsed). On HTTP, the producers emit RFC 7807 ProblemDetails but nothing reads one back: a `application/problem+json` 401 decodes to a featureless `D2Result` with `d2_error_code` / `d2_messages` / `d2_input_errors` silently dropped — indistinguishable from a 401 from a black hole.

A second force is a pair of failure modes the codec must structurally prevent: logging the raw transport exception (which leaks `Status.Detail` / broker-host detail through `ex.Message`), and baking `err.details` / `err.message` straight into the user-facing `messages[]` array (both an info leak and an untranslated string flowing to the client). Both violate the project's PII / logging discipline, so the codec makes them unreachable.

The decision, then, is to seat the codec + resilience wrapper per transport on top of the existing specs and resilience primitives — a small, well-bounded addition rather than a new design.

## Decision

A `D2Result` crosses any transport faithfully and re-materializes cleanly, and the boundary between a **business failure** and a **transport fault** is explicit and load-bearing.

**The business-vs-transport boundary.** A business failure (404, 409, 400, …) is a normal, expected outcome and rides the wire *as data*: `success=false` inside the result that the transport carries. The transport itself succeeded. `RpcException` / gRPC non-OK status / HTTP transport errors are reserved for *genuine* transport or infrastructure faults (the channel broke, the auth middleware rejected the JWT, the host was unreachable). Conflating the two — forcing a business 409 into a non-OK gRPC status — is the fidelity loss this model exists to avoid.

**gRPC carries the result as an envelope field in a normal OK response.** A service response message embeds a `D2ResultProto` (`public/contracts/protos/common/v1/d2_result.proto`) as field 1, with the typed payload riding in sibling fields. A business failure returns a normal gRPC response with `success=false` in the embedded proto — it never throws. The exact HTTP status crosses as `int32 status_code`, so 404 / 409 / 400 are preserved losslessly; `category` rides as field 4 (a snake-case wire string), so a consumer gets the typed class without importing the producer's catalog; `messages` and `input_errors` carry full `TKMessage` fidelity (key + params) via `TKMessageProto`. The auth trailer writer (`D2RpcStatusExtensions`, `RpcException` + `Unauthenticated` / `Unavailable`) survives **only** for the JWT-middleware transport-reject path — a genuine auth fault, distinct from an `Unauthorized()` `D2Result` from a handler.

**HTTP carries the same `D2Result` via RFC 7807 ProblemDetails `d2_*` extensions.** The producers emit `d2_error_code` / `d2_messages` / `d2_input_errors` / `d2_category` / `traceId` as ProblemDetails extensions; the BFF re-materializes them with `parseProblemDetailsResponse` (content-type-discriminated on `application/problem+json`, reading the spec-driven `ProblemDetailsExtensionKeys.*` constants verbatim — never normalized, never hand-typed). This restores parity with the gRPC envelope across the HTTP boundary.

**Codecs do the lossless serialize / re-materialize.** On .NET, `DcsvIo.D2.Result.Grpc` provides `D2Result.ToProto()` and `D2ResultProto.ToD2Result<TData>(data)` (C# 14 extension members); on node, `@dcsv-io/d2-grpc-client` provides `d2ResultToProto` / `d2ResultFromProto`. Optional-string proto fields rehydrate via `Falsey()` so "absent" ≠ "empty string"; an unknown `category` wire string parses to null / undefined via `TryFromWire`, never throwing (deploy-skew tolerance).

**Resilience wrappers map transport faults to an informative `D2Result` — zero caller try-catch.** `AsyncUnaryCall<TProto>.HandleAsync<TData>(resultSelector, dataSelector, logger?, traceId?)` (.NET) and `handleGrpcCall(callFn, resultSelector, dataSelector, traceId?)` (node) await the call, re-materialize on success, and on a transport fault return a pre-built factory result: `RpcException` → `ServiceUnavailable` (with `Cancelled(12)` → `Canceled`), `OperationCanceledException` → `Canceled`, any other exception → `UnhandledException`. The selectors are statically typed against the generated response message, so a proto-field rename is a compile error — no reflection, no marker-interface convention. Transient-fault classification (`IsTransientGrpcException` / `isTransientGrpcError`, gRPC codes 4 / 8 / 10 / 13 / 14) lives in the codec lib, not the resilience lib, so the resilience primitives stay gRPC-free; the predicate is passed to `RetryHelper.RetryD2ResultAsync`. On HTTP, `executeFetch` is the same shape: `AbortError` → `Canceled`, `TimeoutError` → `REQUEST_FAILED`, network error → `UnhandledException`.

**PII discipline is first-pass, not a follow-up.** The two failure modes named above are structurally prevented. On .NET, `[LoggerMessage]` never accepts an `Exception`; transport faults are logged via `SanitizedExceptionRender.TypeName(ex)` + `FirstFrame(ex)` + the safe `ex.StatusCode` enum, never `RpcException.Message`. On node, the user-facing `messages[]` is always a `TKMessage` constant (`TK.common.errors.SERVICE_UNAVAILABLE` / `UNHANDLED_EXCEPTION` / `CANCELED` / `UNAUTHORIZED`), never `err.details` / `err.message`; the safe numeric `err.code` is logged separately if at all. On the HTTP parse-back, the raw `title` / `detail` / `instance` (operator English, not translation keys) are never copied into `messages` — only the structured `d2_messages` `TKMessage[]` render.

**Cross-runtime codec parity is proven by fixtures.** A parity test asserts the `D2ResultProto` field set ≡ the `d2result-envelope` spec ≡ both runtimes' `D2Result` property sets; a fixture-based round-trip feeds a .NET-serialized proto through node's `d2ResultFromProto` (and vice versa) and asserts equal field values, so the two codecs round-trip identically. Protos remain their own source of truth — there is no contracts→proto generator; the compiler and the parity tests catch drift.

## Consequences

**Positive.**

- A caller of any downstream writes no `try-catch`. Transport faults arrive as `ServiceUnavailable` / `Canceled` / `UnhandledException` `D2Result`s; business failures arrive as the exact `D2Result` the producer returned.
- Status fidelity is lossless on gRPC: 404 vs 409 vs 400 survive as `int32 status_code`, not collapsed into a 3-way gRPC bucket.
- The same `D2Result` shape (including `category`) crosses both gRPC and HTTP, so a consumer's branching logic is transport-agnostic — and the HTTP parse-back closes the gap where an auth 401 was indistinguishable from a black-hole 401.
- The business-vs-transport split keeps gRPC status semantics honest (`OK` for a business failure, because the RPC itself succeeded) and keeps `RpcException` / trailers reserved for real transport / auth faults.
- The envelope model has no ~8KB trailer bound, so large `inputErrors` ride intact, and it is per-message — it extends naturally to server-streaming if/when that lands.
- PII safety is structural at the boundary: no `ex.Message` / `err.message` / `Status.Detail` / ProblemDetails `Detail` can reach a user-facing message or a log line.

**Negative / risks.**

- Two result-on-wire mechanisms coexist on gRPC (the business envelope and the auth transport-reject trailer). The boundary is documented, but it must not drift — a 401 from auth middleware is a different thing from an `Unauthorized()` handler result.
- The envelope requires a `D2ResultProto result = N` field on every response message and generated proto stubs, i.e. a real proto-codegen toolchain. This deliverable pulled it forward only for the `D2ResultProto` stub (both runtimes); the broad per-service proto toolchain remains a separate future infra deliverable.
- The typed payload rides in sibling response fields, stitched back in `ToD2Result`. The tricky case — `SomeFound` with `success=false` *and* data present — must be pinned by round-trip tests on both runtimes.
- Several transport codecs/wrappers remain explicitly out of scope and tracked-future: the node `@dcsv-io/d2-messaging` runtime, SignalR / gRPC push, SSE, the SSR gateway client, and the node gRPC *server* boilerplate. The model is settled; only the un-built transports await their owning service.
- RabbitMQ deliberately does **not** ride a `D2Result` in the payload (the payload is the domain DTO); the result lives in the publisher return and the consumer-handler return that drives ACK vs DLQ. This is correct but is a different shape from the sync codec, which engineers must keep distinct.

## Alternatives considered

**Trailer-patching the business result onto an `RpcException`.** Carry the whole `D2Result` across gRPC trailers on a forced non-OK status. Rejected on every axis: it forces a non-OK gRPC status (lossy 3-way bucket → status fidelity loss needing recovery), every field becomes a stringly-typed trailer (large `inputErrors` can blow the ~8KB trailer bound), the typed `data` payload has nowhere to live on a failure-only exception, trailers are once-per-call so they can't carry per-message streaming results. The trailer writer survives only for the genuine auth transport-reject.

**Exceptions as control flow across the wire.** Let business failures throw and be caught at a middleware boundary. Rejected for the same reasons ADR-0003 rejected exceptions in-process, amplified by the wire: the failure surface is invisible in the call signature, partial-success (`SOME_FOUND`) is awkward, and it reintroduces the `try-catch`-at-every-call-site cost the whole model exists to remove.

**Bespoke per-call error handling.** Let each call site hand-write its own `RpcException` / fetch-error mapping. Rejected: it is the absence of a decision — it guarantees inconsistent status mapping, duplicated transient-fault logic, and recurring PII leaks where a developer reaches for `ex.Message` because nothing stopped them. A single codec + wrapper per transport makes the safe path the only path.

## References

- `public/contracts/protos/common/v1/d2_result.proto` — the `D2ResultProto` envelope (with `category` field 4); `public/contracts/d2result-envelope/d2result-envelope.spec.json` — the JSON envelope field-name spec; `public/contracts/problem-details/problem-details.spec.json` — the ProblemDetails extension keys (incl. `d2_category`).
- `public/packages/dotnet/result/grpc/ProtoExtensions.cs` — `ToProto` / `ToD2Result<T>` / `HandleAsync<T>` / `IsTransientGrpcException`, with `SanitizedExceptionRender` PII discipline; `public/packages/dotnet/result/grpc/README.md`.
- `public/packages/typescript/grpc-client/src/` — `d2-result-to-proto.ts` / `d2-result-from-proto.ts` / `handle-grpc-call.ts` (`handleGrpcCall` / `isTransientGrpcError` / `unaryCall`).
- HTTP consumer codec (private monorepo illustration — not required for public clone): BFF `parseProblemDetailsResponse` + `executeFetch`; .NET producers under `public/packages/dotnet/auth/http/ProblemDetails/` + `public/packages/dotnet/aspnetcore/Internal/`.
- `public/packages/dotnet/auth/grpc/Status/D2RpcStatusExtensions.cs` — the auth trailer writer (transport-reject only).
- `public/packages/typescript/contract-tests/` — cross-runtime codec parity fixtures; round-trip + adversarial + PII-discipline regression tests.
- [ADR-0003](0003-d2result-errors-as-values.md) — `D2Result` as the single in-memory outcome type this serializes faithfully. [ADR-0018](0018-spec-driven-error-codes.md) — the spec-driven codes + `ErrorCategory` the envelope carries. [ADR-0009](0009-async-messaging-encrypted-payloads.md) — the async-messaging boundary, where the `D2Result` rides the publisher / consumer return rather than the (encrypted) payload.
