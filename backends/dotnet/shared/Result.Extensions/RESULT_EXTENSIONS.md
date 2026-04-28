# Result.Extensions

Extension methods for converting between D2Result and Protocol Buffer representations, plus gRPC call handling utilities. Bridges the gap between internal result types and wire-format messages.

## Files

| File Name                                | Description                                                                                                                   |
| ---------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------- |
| [ProtoExtensions.cs](ProtoExtensions.cs) | Extension methods for D2Result to/from D2ResultProto conversion and AsyncUnaryCall handling with automatic error translation. |

## Proto Optional Fields

`ErrorCode` and `TraceId` on `D2ResultProto` are declared with the `optional` keyword in proto3:

```protobuf
optional string error_code = 3;
optional string trace_id = 6;
```

Grpc.Tools generates `HasErrorCode` / `HasTraceId` properties, and setters still call `CheckNotNull` (assigning `null` throws). The conversion methods handle this:

- **`ToProto()`** uses conditional assignment — only sets a field when the value is non-null:
  ```csharp
  if (result.ErrorCode is not null) proto.ErrorCode = result.ErrorCode;
  if (result.TraceId is not null) proto.TraceId = result.TraceId;
  ```
- **`ToD2Result()`** handles optional fields with `HasField` guards:
  ```csharp
  ErrorCode = proto.HasErrorCode ? proto.ErrorCode : null
  ```
- **Node.js `d2ResultFromProto()`** uses null-coalescing: `errorCode: proto.errorCode ?? undefined`

The domain model is the source of truth for nullability — proto `optional` merely mirrors it on the wire. Collections use `?? []` defaults; booleans use `?? false`.

## Extension Methods

### D2Result → D2ResultProto

Converts internal result to wire format:

```csharp
var proto = result.ToProto();
```

### D2ResultProto → D2Result

Converts wire format back to internal result with typed data:

```csharp
var result = proto.ToD2Result(data);
```

### AsyncUnaryCall Handling

Wraps gRPC calls with automatic error handling and result conversion:

```csharp
var result = await client.SomeMethodAsync(request, ct)
    .HandleAsync(
        r => r.Result,    // Result selector
        r => r.Data,      // Data selector
        logger,           // Optional logger
        traceId);         // Optional trace ID
```

Automatically handles:

- `RpcException` → `D2Result.Fail` with `SERVICE_UNAVAILABLE`
- General exceptions → `D2Result.UnhandledException`
- Successful responses → `D2Result` with data from proto

## Usage Pattern

Typical handler implementation calling a gRPC service:

```csharp
protected override async ValueTask<D2Result<O?>> ExecuteAsync(
    I input,
    CancellationToken ct = default)
{
    var r = await r_client.SomeMethodAsync(new(), cancellationToken: ct)
        .HandleAsync(
            r => r.Result,
            r => r.Data,
            Context.Logger,
            TraceId);

    return D2Result<O?>.Bubble(r, new O(r.Data?.SomeField));
}
```
