# Protos.DotNet

C# code generation project that compiles Protocol Buffer definitions into strongly-typed gRPC clients, servers, and DTOs. Uses Grpc.Tools to automatically generate code from .proto files during build.

## Proto Definitions

Protocol Buffer source files define service contracts and message schemas for inter-service communication:

### Common

Shared proto definitions used across all services:

| File Name                                                                       | Description                                                                                                              |
| ------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------ |
| [d2_result.proto](../../../../../../contracts/protos/common/v1/d2_result.proto) | D2ResultProto and InputErrorProto messages for standardized operation results with success status, messages, and errors. |
| [ping.proto](../../../../../../contracts/protos/common/v1/ping.proto)           | Simple PingService for testing proto generation and gRPC connectivity with request/response messages.                    |

### Events

Cross-service async event definitions:

| File Name                                                                         | Description                                                                |
| --------------------------------------------------------------------------------- | -------------------------------------------------------------------------- |
| [geo_events.proto](../../../../../../contracts/protos/events/v1/geo_events.proto) | `GeoRefDataUpdatedEvent` — broadcast when Geo reference data is refreshed. |

### Geo

Geographic reference data service definitions:

| File Name                                                        | Description                                                                                                                                                                                                                          |
| ---------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| [geo.proto](../../../../../../contracts/protos/geo/v1/geo.proto) | GeoService contract with 9 RPCs: reference data (Get, RequestUpdate), WhoIs (FindWhoIs), Contacts (GetByIds†, GetByExtKeys, Create, Delete†, DeleteByExtKeys, UpdateByExtKeys). † = internal only, not exposed via client libraries. |

### Comms

Communication/delivery service definitions:

| File Name                                                              | Description                                                                                                                                           |
| ---------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------- |
| [comms.proto](../../../../../../contracts/protos/comms/v1/comms.proto) | CommsService gRPC contract: Deliver, GetChannelPreference, SetChannelPreference, UpsertTemplate, GetTemplate, GetDeliveryHistory, GetDeliveryRequest. |

## Generated Output

Grpc.Tools produces C# code including:

- Service client stubs (e.g., `GeoService.GeoServiceClient`) for calling remote services
- Service base classes (e.g., `GeoService.GeoServiceBase`) for implementing service handlers
- Message DTOs with protobuf serialization (e.g., `CountryDTO`, `GeoRefData`, `D2ResultProto`)
- Map/repeated field collections for complex data structures

## D2ResultProto Pattern

All gRPC responses follow a consistent pattern wrapping data with `D2ResultProto`:

```protobuf
message SomeOperationResponse {
    d2.common.v1.D2ResultProto result = 1;
    SomeOperationData data = 2;
}
```

This enables:

- Unified success/failure handling across all endpoints
- Consistent error structure with messages, input errors, and status codes
- Trace ID propagation for debugging and observability

## Proto `optional` Keyword

65 fields across all proto definitions now use the `optional` keyword in proto3. This affects code generation:

- **`HasField` property**: Grpc.Tools generates a `Has{FieldName}` boolean property (e.g., `HasErrorCode`, `HasTraceId`) that distinguishes "field was set" from "field has default value."
- **Setters still call `CheckNotNull`**: Assigning `null` to an optional string field throws `ArgumentNullException`. Always guard with a conditional before assignment:
  ```csharp
  if (value is not null) dto.Field = value;
  ```
- **Domain model is source of truth**: Proto `optional` mirrors the domain model's nullability (`string?`, `bool?`). The proto schema does not introduce new nullability — it reflects what the domain already defines.
- **Backward compatible**: Messages with optional fields are wire-compatible with consumers that were compiled before the `optional` keyword was added.

**Note:** Generated code is produced automatically during build. Modify source .proto files to change contracts.
