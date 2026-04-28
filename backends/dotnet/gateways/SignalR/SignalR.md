# SignalR Gateway

Standalone .NET real-time push gateway. Accepts WebSocket connections from authenticated browser clients and receives push requests from internal services via gRPC. Forwards events to connected clients using SignalR Groups (channel-based routing).

**Runtime:** ASP.NET Core (.NET 10)
**Transport:** WebSocket (browser-facing) + gRPC (internal push)
**Backplane:** Redis (StackExchangeRedis)
**Auth:** JWT Bearer (RS256 JWKS via `JwtAuth.Default`)

> **F6 scope: authenticated hub only.** Public hub + subscription tokens (anonymous/third-party support chat) deferred to Comms Stage B. See ADR-028 in [PLANNING.md](../../../../PLANNING.md) for the full two-tier design.

---

## Architecture

```
Browser (authenticated)
  -> WebSocket -> /hub/authenticated (JWT via ?access_token= query param)
  -> AuthenticatedHub auto-subscribes to user:{userId} + org:{orgId} groups
  -> Receives push events via ReceiveEvent(event, payloadJson)

Internal services (Files, Comms, Auth, etc.)
  -> gRPC -> RealtimeGateway.PushToChannel(channel, event, payloadJson)
  -> Hub context resolves channel -> SignalR Group -> WebSocket connections
  -> All connected clients in the group receive the event
```

Channel naming convention: `user:{userId}`, `org:{orgId}`, `thread:{threadId}` (thread channels deferred to Comms Stage B).

---

## Hub

### AuthenticatedHub (`/hub/authenticated`)

Push-only hub -- no client-invokable methods in F6 scope. Requires JWT Bearer authorization.

**On connect:**

- Extracts `sub` (userId) and `activeOrganizationId` (orgId) from JWT claims
- Auto-subscribes the connection to `user:{userId}` and `org:{orgId}` SignalR Groups
- Logs connection with userId + orgId

**On disconnect:**

- Logs disconnection (SignalR handles group cleanup automatically)

**Client event shape:**

```
ReceiveEvent(event: string, payloadJson: string)
```

Examples: `file:ready`, `file:rejected`, `notification:delivered`, `message:new`.

---

## gRPC Service

### RealtimeGateway

Proto: [`contracts/protos/realtime/v1/realtime_gateway.proto`](../../../../contracts/protos/realtime/v1/realtime_gateway.proto)

| RPC               | Auth            | Description                                           |
| ----------------- | --------------- | ----------------------------------------------------- |
| PushToChannel     | Service key     | Push event to all connections subscribed to a channel |
| RemoveFromChannel | Service key     | Evict a specific connection from a channel group      |
| CheckHealth       | None (no attr.) | Health probe -- returns status + timestamp            |

**PushToChannel** sends `ReceiveEvent(event, payloadJson)` to the SignalR Group matching `request.Channel`. Fire-and-forget from the caller's perspective.

**RemoveFromChannel** removes a specific `connectionId` from a channel group. Used when a service revokes access (e.g., user removed from a thread, org membership revoked).

---

## Authentication

### Browser (WebSocket)

JWT is passed as `?access_token=` query parameter during WebSocket upgrade (standard SignalR convention -- WebSocket API does not support `Authorization` headers).

The gateway reuses `JwtAuth.Default` (`AddJwtAuth`) for RS256 JWKS validation against the Auth service. A `PostConfigure<JwtBearerOptions>` hook extracts the token from the query string for `/hub` paths, then delegates to the standard JWT validation pipeline.

Config section: `SIGNALR_AUTH` (same shape as other .NET services).

### gRPC (Internal Push)

Service key validation via `ServiceKeyInterceptor` -- a gRPC server interceptor that reads the `x-api-key` metadata header and validates against configured keys using **constant-time comparison** (`CryptographicOperations.FixedTimeEquals`).

Only RPCs decorated with `[RequiresServiceKey]` are validated. `CheckHealth` is exempt (no attribute).

Config section: `SIGNALR_SERVICEKEY` (indexed array: `SIGNALR_SERVICEKEY__VALIDKEYS__0`, etc.).

---

## Redis Backplane

`Microsoft.AspNetCore.SignalR.StackExchangeRedis` with channel prefix `d2-signalr`. Enables multi-instance connection tracking -- any gateway instance can push to any connected client. Wired from day one so horizontal scaling is transparent.

Uses the shared `REDIS_URL` connection string.

---

## Docker

Container: `d2-signalr`
Dockerfile: `docker/Dockerfile.signalr`
Ports: `${SIGNALR_HTTP_PORT}` (5400) + `${SIGNALR_GRPC_PORT}` (5401)
Depends on: `d2-redis`, `d2-auth`
Health check: `curl -sf http://localhost:${SIGNALR_HTTP_PORT}/health`

---

## Environment Variables

| Variable                           | Purpose                                                  |
| ---------------------------------- | -------------------------------------------------------- |
| `SIGNALR_SERVICE_NAME`             | OTel service name (default: `d2-signalr`)                |
| `SIGNALR_HTTP_PORT`                | HTTP/WebSocket port (default: `5400`)                    |
| `SIGNALR_GRPC_PORT`                | gRPC internal push port (default: `5401`)                |
| `REDIS_URL`                        | Redis connection string (backplane + shared cache)       |
| `SIGNALR_AUTH__AUTHSERVICEBASEURL` | Auth service base URL for JWKS retrieval                 |
| `SIGNALR_AUTH__ISSUER`             | Expected JWT issuer                                      |
| `SIGNALR_AUTH__AUDIENCE`           | Expected JWT audience                                    |
| `SIGNALR_SERVICEKEY__VALIDKEYS__0` | Valid API keys for gRPC service key validation (indexed) |
| `OTEL_EXPORTER_OTLP_*`             | OTel exporter endpoints (traces, logs, metrics)          |

---

## File Tree

```
backends/dotnet/gateways/SignalR/
  SignalR.csproj                    Project file (.NET 10 web SDK)
  SignalR.md                        This document
  Program.cs                        Service bootstrap: SignalR + Redis backplane, JWT auth,
                                     gRPC service key config, security headers, hub + gRPC mapping
  Hubs/
    AuthenticatedHub.cs             JWT-authorized hub. Auto-subscribes user: + org: groups on connect
  Services/
    RealtimeGatewayService.cs       gRPC RealtimeGateway implementation (PushToChannel, RemoveFromChannel,
                                     CheckHealth). Routes to SignalR Groups via IHubContext
  Interceptors/
    ServiceKeyInterceptor.cs        gRPC server interceptor — x-api-key validation, constant-time compare
    RequiresServiceKeyAttribute.cs  Marker attribute for gRPC methods requiring service key auth
    SignalRServiceKeyOptions.cs     Options class (ValidKeys list, bound from SIGNALR_SERVICEKEY section)
```

## Dependencies

| Package                                           | Purpose                                |
| ------------------------------------------------- | -------------------------------------- |
| `D2.Shared.ServiceDefaults`                       | OTel, health, Prometheus, Serilog      |
| `D2.Shared.Handler.Extensions`                    | Handler auth utilities (JwtClaimTypes) |
| `D2.Shared.JwtAuth.Default`                       | JWT Bearer auth (RS256 JWKS)           |
| `D2.Shared.Protos.DotNet`                         | Proto-generated RealtimeGateway types  |
| `Grpc.AspNetCore`                                 | gRPC server hosting                    |
| `Microsoft.AspNetCore.SignalR.StackExchangeRedis` | Redis backplane for SignalR            |
