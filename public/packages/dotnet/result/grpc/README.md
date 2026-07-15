<!--
Copyright (c) DCSV. All rights reserved.
-->

# DcsvIo.D2.Result.Grpc

Faithful in-memory → wire → in-memory `D2Result` round-trip over a gRPC
`D2ResultProto` response envelope. Every service handler returns a `D2Result`
as-is; the framework wraps it; the consumer re-materializes an equal `D2Result`
on the other side — zero caller try-catch for business failures.

## Core API

```csharp
// Server side: wrap
var response = new FindThingsResponse
{
    Result = result.ToProto(),
};
response.Data.AddRange(things);
return response;

// Consumer side: re-materialize
var res = await client.FindThings(req).HandleAsync(r => r.Result, r => r.Data);
if (res.Category == ErrorCategory.NotFound) { /* typed, no registry lookup */ }
res.StatusCode == HttpStatusCode.Conflict; // exact HTTP status, not a lossy gRPC bucket
```

## Key properties

- **Full fidelity**: every field — `Success`, `StatusCode` (exact integer), `ErrorCode`,
  `Category`, `Messages` (key + params), `InputErrors`, `TraceId`, `Data` — round-trips
  equal.
- **Typed `ErrorCategory`**: populated by factories at generation time. `ErrorCategory.NotFound`
  is available directly on the rebuilt result; no runtime registry lookup on the hot path.
- **`TKMessageProto`**: structured message carrier (`key` + `map<string,string> params`).
  Full parameter fidelity — a `TKMessage` with substitution bindings survives the round-trip
  unchanged.
- **Transport fault fail-open**: `RpcException` → `ServiceUnavailable`; `Cancelled` →
  `Canceled`; any other `Exception` → `UnhandledException`. User-facing messages stay
  the factory TK constants — never raw transport strings.
- **PII-safe logging**: `[LoggerMessage]` signatures do NOT accept `Exception`. Transport
  detail is logged via `SanitizedExceptionRender.TypeName` + `FirstFrame` + `StatusCode` —
  never `ex.Message` (which can embed AMQP URIs, JWT contents, configured secrets).
- **`IsTransientGrpcException`**: classifies `DeadlineExceeded`, `ResourceExhausted`,
  `Aborted`, `Internal`, `Unavailable` as transient for retry callers.

## Wire mechanism boundary

Two mechanisms coexist on every gRPC call — each has a distinct role and must not be conflated:

| Mechanism | What it carries | gRPC status on the wire | Who reads it |
| --- | --- | --- | --- |
| **`D2ResultProto` response envelope** (this lib) | Business results — both success and failure. A handler returning `NotFound()` or `ValidationFailed()` produces a normal gRPC `OK` response; the failure detail rides inside the `D2ResultProto result` envelope field. | gRPC `OK` (status 0) + response body | `HandleAsync` / `ToD2Result<T>` — caller gets a clean `D2Result` with zero try-catch for business failures |
| **`RpcException` + `D2GrpcTrailers`** (`DcsvIo.D2.Auth.Grpc`) | Transport/auth rejections — JWT validation failure, JWKS unavailable, scope insufficient. These are genuine transport-layer faults, not handler outcomes. | Non-`OK` gRPC status (`Unauthenticated` (16) or `Unavailable` (14)) + trailers (`d2_error_code` / `d2_messages` / `traceId`) | Auth middleware on the server side; retry/circuit-breaker on the client side |

**Boundary rule**: a `401` from the JWT interceptor is `RpcException(Unauthenticated)` — never a `D2ResultProto{ success=false, status_code=401 }`. A `404` from a handler is `D2ResultProto{ success=false, status_code=404 }` — never an `RpcException`. The two paths are structurally separate and must stay that way.

## Proto contract

`contracts/protos/common/v1/d2_result.proto` is the single source of truth. The
generated `D2ResultProto` / `TKMessageProto` / `InputErrorProto` types live in namespace
`D2.Services.Protos.Common.V1`.

## Dependencies (acyclic)

```
DcsvIo.D2.Result.Grpc
  ├── DcsvIo.D2.Result          (result-core — D2Result, InputError, TKMessage)
  ├── DcsvIo.D2.ErrorCodes.Category   (ErrorCategory, ErrorCategoryWire)
  ├── DcsvIo.D2.Utilities       (Falsey() null-guard)
  ├── DcsvIo.D2.I18n.Abstractions     (TKMessage ctor — InternalsVisibleTo)
  ├── Google.Protobuf           (proto runtime)
  └── Grpc.Net.Client           (AsyncUnaryCall<T>, RpcException)
```
