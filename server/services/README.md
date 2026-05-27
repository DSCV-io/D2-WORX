<!--
Copyright (c) DCSV. All rights reserved.
-->

# server/services/ — D²-WORX Services

> Parent: [`server/`](../README.md)

.NET service implementations. Each service is a self-contained DDD-layered project (api / app / domain / infra / tests + clients/dotnet for client libraries) built in bottom-up dependency order.

## Services

| Service                                     | Purpose                                                                                                                                                                                                      |
| ------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| [`edge/`](edge/README.md)                   | The unified gateway. YARP routing + self-rolled Auth module (RFC 8693 + client_credentials + KeyCustodian) + SignalR hubs + in-process WhoIs (IPinfo) + all cross-cutting middleware. Single public ingress. |
| [`audit/`](audit/README.md)                 | Append-only audit store. Consumes `d2.audit.events` from RabbitMQ (encrypted); writes to `audit_db` with INSERT-only role. Different retention + access control from operational data.                       |
| [`courier/`](courier/README.md)             | Pure outbound delivery — email + SMS. Markdown content rendered to HTML via Markdig; brand chrome via Razor.                                                                                                 |
| [`notifications/`](notifications/README.md) | In-app activity feed. Persistent feed entries with read/unread, pagination, aggregation. Consumes `d2.notifications.requests` events; calls Edge's SignalR push API for live delivery.                       |
| [`files/`](files/README.md)                 | File management + processing + variants. SeaweedFS for storage. ClamAV virus scanning (fail-closed). Per-context-key config.                                                                                 |

## Standard service shape

Per project convention:

```
server/services/{service}/
  api/             # HTTP + gRPC entry point — ASP.NET Core minimal API + grpc-dotnet
  app/             # Application layer — CQRS handlers, mappers
  domain/          # Domain layer — entities, value objects, enums, business rules
  infra/           # Infrastructure layer — DbContext, repositories, integrations
  tests/           # Per-service tests (unit + integration via Testcontainers)
  clients/dotnet/  # .NET client library (Edge has none — it's the entry point)
```

## Conventions

- **Folder naming**: lowercase outer (`edge/`, `api/`, `app/`)
- **Project naming**: PascalCase dot-separated (`D2.Edge.API.csproj` lives in `edge/api/`)
- **One handler per file** under `Implementations/{TLC}/Handlers/{3LC}/` per [PATTERNS.md](../../docs/PATTERNS.md) TLC convention
- **Every service + project has a `README.md`**

## Build

```bash
dotnet build server/D2.slnx                                                        # full solution
dotnet build server/services/edge/api/D2.Edge.API.csproj                           # single project
dotnet test server/services/edge/tests --no-build --configuration Release          # service tests
```

## Compose integration

Each service has a block in [`infra/compose/compose.yml`](../../infra/compose/compose.yml) mirroring its build status.
