# Backend Architecture & Organization

## Overview

The D²-WORX backend follows a **hierarchical, three-tier categorization system** (TLC→2LC→3LC) that provides consistent organization across all projects, from shared contracts to service-specific implementations. This structure prioritizes discoverability, maintainability, and scalability.

---

## The Three-Tier Hierarchy

### TLC (Top-Level Category)

> **What:** Primary architectural concern
>
> **Examples:** `CQRS`, `Messaging`, `Repository`, `Caching`

### 2LC (Second-Level Category)

> **What:** Implementation detail or subdivision
>
> **Examples:** `Handlers`, `Publishers`, `Consumers`, `Entities`, `Migrations`

### 3LC (Third-Level Category)

> **What:** Specific pattern or operation type
>
> **Examples:** `C` (Commands), `Q` (Queries), `Pub` (Publishers), `Sub` (Subscribers)

---

## Canonical TLCs

The following TLCs are first-class architectural concerns. Each has a defined 3LC pattern. See
"New TLC Governance" below for when (and how) to introduce a new one.

| TLC                 | Purpose                                         | 3LC pattern                            | Example handlers                       |
| ------------------- | ----------------------------------------------- | -------------------------------------- | -------------------------------------- |
| `cqrs/`             | Application orchestration handlers              | `c/` `q/` `u/` `x/` (operation intent) | `UploadFile`, `GetFileMetadata`        |
| `messaging/`        | Pub/sub via RabbitMQ                            | `pub/` `sub/`                          | `PublishFileForProcessing`             |
| `repository/`       | DB read/write via ORM                           | `c/` `r/` `u/` `d/` (CRUD)             | `CreateFileRecord`, `GetFileById`      |
| `caching/`          | Distributed/local cache abstractions            | `c/` `r/` `u/` `d/` (CRUD)             | `Set`, `Get`, `Remove`                 |
| `outbound/`         | gRPC client wrappers calling **other** services | flat (one handler per RPC)             | `CallCanAccess`, `CallOnFileProcessed` |
| `realtime/`         | Real-time push handlers (SignalR / WebSocket)   | flat (one handler per event type)      | `PushFileUpdate`, `PushUserUpdated`    |
| `storage/`          | Object/blob storage (S3, MinIO)                 | `c/` `r/` `u/` `d/` (CRUD)             | `PutStorageObject`, `GetStorageObject` |
| `scanning/`         | Content scanning (ClamAV, future moderation)    | flat                                   | `ScanFile`                             |
| `image-processing/` | Image transforms (Sharp variants, EXIF, etc.)   | flat                                   | `ProcessVariants`                      |

### New TLC Governance

Before introducing a new top-level folder, ask: **"Is this a _capability_ the service exposes,
or just a _kind of dependency_?"**

- **Capability** → new TLC. Document it here in BACKENDS.md as part of the same change. Get a
  second opinion before merging — TLCs proliferate quickly when each developer answers this
  differently.
- **Dependency** → fold into an existing TLC. (`providers/` was an anti-pattern: it grouped by
  _kind of dependency_ — "things we call out to" — rather than by capability. It's been
  decomposed into `outbound/`, `storage/`, `scanning/`, `image-processing/`.)

3LC choice within a new TLC: prefer the existing alphabets (`c/r/u/d`, `c/q/u/x`, `pub/sub`)
when they fit the operation taxonomy. Use flat layout when the TLC has only a handful of
handlers and they don't fall on a clear axis.

## Category Definitions

### CQRS (Command Query Responsibility Segregation)

Separates read operations (queries) from write operations (commands) with clear semantic boundaries.

This kind of separation is apparent throughout the application and is a core pattern in D²-WORX
services.

**Rationale:**

- **C (Commands):** Primary intent is mutation of persistent/shared state. Caller expects
  durable changes (database writes, distributed cache updates, file writes, message publishing).

- **Q (Queries):** Read-only from the perspective of persistent/shared state. Local/in-memory
  caching is permitted as an invisible optimization — it's instance-scoped and ephemeral,
  so it doesn't affect other service instances or survive process restarts.
  Read-only external I/O is also permitted (e.g., gRPC fetches, S3 reads, presign-URL
  generation) — it's part of "answering the query," not a side effect.

- **U (Utilities):** Stateless helper operations (validation, transformation, dispatch,
  composition) that don't write to any data store. May call read-only external services
  to compose an answer; must not publish, mutate, or persist.

- **X (Complex):** Primary intent is retrieval, but may mutate persistent/shared state as a
  side effect to ensure future availability (e.g., fetching from external source then persisting
  to database, write-through to distributed cache). Also: SAGA orchestrators that coordinate
  multiple handler calls with rollback semantics — see "SAGA Pattern" below.

**Side Effect Classification:**

| Effect Type                  | Query | Command | Complex | Utility |
| ---------------------------- | ----- | ------- | ------- | ------- |
| Local/in-memory cache        | ✅    | ✅      | ✅      | ✅      |
| Distributed cache (read)     | ✅    | ✅      | ✅      | ✅      |
| Distributed cache (mutate)   | ❌    | ✅      | ✅      | ❌      |
| Database (read)              | ✅    | ✅      | ✅      | ✅      |
| Database (mutate)            | ❌    | ✅      | ✅      | ❌      |
| External read I/O (gRPC, S3) | ✅    | ✅      | ✅      | ✅      |
| External mutation            | ❌    | ✅      | ✅      | ❌      |
| File system (write)          | ❌    | ✅      | ✅      | ❌      |
| Message publishing           | ❌    | ✅      | ✅      | ❌      |

**Key Distinction (the persistence test):** If the process dies immediately after the handler
completes, would any state change persist or be visible to other instances? For **Q** and **U**,
the answer must be "no." For **X** and **C**, mutations are expected.

**Canonical examples (Files service):**

- `CheckFileAccess` (Q/) — calls `callCanAccess` (gRPC out), no mutations → ✅ Q
- `DownloadFileVariant` (Q/) — pure read composition through `getById` / `resolveAccess`
  / `getStorage` → ✅ Q
- `GetFileVariantUrl` (Q/) — pure read + presigned-URL generation → ✅ Q
- `ResolveFileAccess` (U/) — strategy dispatcher that may invoke read-only callbacks → ✅ U

**Structure:**

```
CQRS/
|
|__ Handlers/
    |
    |__ C/ -> Commands (state-changing operations)
    |__ Q/ -> Queries (read-only persistent state, may do read-only external I/O)
    |__ U/ -> Utilities (neither read nor write of persistent state, may do read-only external I/O)
    |__ X/ -> Complex (mixed side effects, SAGA orchestrators)
```

### SAGA Pattern (Multi-Service Orchestrators)

When a single logical operation must mutate state in multiple services with rollback semantics
on failure, the orchestrator does not fit the BaseHandler shape (single input → single output).
**Sanctioned exception:** SAGA helpers may live in `cqrs/handlers/x/` as **free functions**
(not BaseHandler subclasses). They are:

- Pure orchestrators — they accept handler dependencies + payload via params, invoke them in
  order, and compensate on failure.
- Documented with the operations they coordinate, the failure-compensation paths, and the
  expected logger.fatal escalation when compensation also fails.
- Re-exported from the package's `index.ts` like any other public surface.

**Canonical example:** `runCrossServiceUpdate` in
`backends/node/services/auth/app/src/implementations/cqrs/handlers/x/cross-service-update.ts`.
Coordinates Geo (contact data) + Auth (BetterAuth user/session rows) writes:
`Geo first → Auth second → compensate Geo on Auth failure → logger.fatal on rollback failure`.

Adding a new SAGA helper requires the same governance as adding a new TLC: review for whether
this is genuinely a multi-step orchestration with rollback, or whether it can be modeled as a
regular Command (`c/`) handler that publishes events and lets each service own its own consumer.

### Messaging (Async Event-Driven Communication)

Enables loosely-coupled service communication with pub-sub patterns using raw AMQP (RabbitMQ) with Protocol Buffer event contracts.

**Rationale:**

- **Handlers → Pub & Sub:** Business logic remains framework-agnostic.
- **Publishers & Consumers:** AMQP infrastructure adapters isolated from domain logic.
- Separates RabbitMQ transport from business logic
- Enables use of interfaces to be defined by dependencies (App Layer / Contracts).

**Structure:**

```
Messaging/
|
|__ Handlers/
|   |
|   |__ Pub/ -> Publisher handlers (send messages)
|   |__ Sub/ -> Subscriber handlers (receive messages)
|
|__ Publishers/ -> AMQP publisher implementations (ProtoPublisher wrappers)
|__ Consumers/ -> AMQP consumer implementations (ProtoConsumer/BackgroundService)
```

### Repository (Data Access & Persistence)

Encapsulates database operations following CRUD patterns with additional infrastructure concerns.

**Rationale:**

- **CRUD separation:** Clear boundaries for each operation type.
- **Transactions at same level:** Transaction control is orthogonal to CRUD.
- **Entities/Migrations/Seeding:** Infrastructure concerns grouped together.

**Structure:**

```
Repository/
|
|__ Handlers/
|   |
|   |__ C/ -> Create operations
|   |__ R/ -> Read operations
|   |__ U/ -> Update operations
|   |__ D/ -> Delete operations
|
|__ Entities/ -> EF Core configurations
|__ Migrations/ -> Database schema evolution
|__ Seeding/ -> Initial/reference data
|__ Transactions/ -> Transaction control handlers
```

### Caching (Multi-Tier Cache Strategy)

Provides layered caching with abstract, distributed, and in-memory interfaces and implementations.

**Structure (under Interfaces):**

```
Caching/
|
|__ Abstract/ -> Base interfaces for cache operations
|   |
|   |__ Handlers/
|       |
|       |__ C/ -> Create operations
|       |__ R/ -> Read operations
|       |__ U/ -> Update operations
|       |__ D/ -> Delete operations
|
|__ Distributed/ -> Distributed cache interfaces (e.g., Redis)
|   |
|   |__ Handlers/
|       |
|       |__ C/ -> Create operations
|       |__ R/ -> Read operations
|       |__ U/ -> Update operations
|       |__ D/ -> Delete operations
|
|__ InMemory/ -> Process-local cache interfaces
    |
    |__ Handlers/
        |
        |__ C/ -> Create operations
        |__ R/ -> Read operations
        |__ U/ -> Update operations
        |__ D/ -> Delete operations
```

**Rationale:**

- **Abstract provides contracts:** Services code against interfaces.
- **Distributed vs InMemory:** Clear separation of cache tiers.
- **Same CRUD pattern:** Consistency with Repository layer.

---

## Project Types & Their Structure

### Contracts (Abstract)

**Purpose:** Define "what" without "how" - pure abstractions.

**Key Principle:** Little-to-no implementation, maximum contract definition.

**Structure:**

```
Contracts/
|
|__ Handler/ -> Base handler abstractions
|
|__ I18n/ -> Translation infrastructure (Translator + TK constants, loads contracts/messages/*.json — 10 BCP 47 locales)
|
|__ Interfaces/ -> All interface definitions following TLC hierarchy
|
|__ Result/ -> D2Result pattern
|
|__ Result.Extensions/ -> Extension methods for D2Result
|
|__ Utilities/ -> Shared helpers
|
|__ Tests/ -> Unit and integration tests for contracts and their implementations (for now)
```

### Contracts (Implementations)

**Purpose:** Reusable, drop-in implementations of contract interfaces.

**Key Principle:** Services consume these via DI without reinventing common functionality.

**Structure:**

```
Implementations/ -> Common reusable implementations
|
|__ Caching/ -> ...for caching
|   |
|   |__ Distributed/ -> Shared, distributed cache implementations
|   |   |
|   |   |__ DistributedCache.Redis/ -> Redis implementation (Get, Set, Remove, Exists, GetTtl, Increment)
|   |
|   |__ InMemory/ -> Local, in-memory cache implementations
|       |
|       |__ InMemoryCache.Default/ -> Memory implementation
|
|__ Messaging/ -> AMQP messaging implementations
|   |
|   |__ Messaging.RabbitMQ/ -> RabbitMQ publisher/consumer (ProtoPublisher, ProtoConsumer, BackgroundService)
|
|__ Middleware/ -> HTTP middleware implementations
|   |
|   |__ RequestEnrichment.Default/ -> Request context enrichment (IP resolution, fingerprinting, WhoIs)
|   |
|   |__ RateLimit.Default/ -> Multi-dimensional sliding-window rate limiting
|   |
|   |__ Idempotency.Default/ -> Idempotency-Key header middleware (Redis SET NX, response caching)
|   |
|   |__ JwtAuth.Default/ -> JWT Bearer + JWKS + fingerprint binding
|   |
|   |__ ServiceKey.Default/ -> S2S API key
|   |
|   |__ AuthPolicy.Default/ -> AuthPolicies constants + RoutePolicyExtensions (.RequireAuth(), .RequireOrg(), etc.)
|   |
|   |__ Translation.Default/ -> Gateway-edge D2Result message/inputError translation (resolves TK.* keys)
|
|__ Repository/ -> Common repository implementations
|   |
|   |__ Batch/ -> Batch query utilities
|   |   |
|   |   |__ Batch.Pg/ -> PostgreSQL batched queries (chunked IN clauses)
|   |
|   |__ Errors/ -> Database error helpers
|   |   |
|   |   |__ Errors.Pg/ -> PostgreSQL constraint error detection (unique violation, FK, etc.)
|   |
|   |__ Transactions/ -> Transaction management implementations
|       |
|       |__ Transactions.Pg/ -> PostgreSQL transactions
```

### Service Projects (Domain-Specific)

**Purpose:** Each service follows clean architecture with domain, application, infrastructure, and API layers.

**Key Principle:** Each service owns its data and business logic and exposes functionality via gRPC APIs with versioned contracts. Each service also owns a **client library** (`ServiceName.Client`) containing messages, interfaces, and default implementations that consumers depend on.

**Rationale:**

- **Domain Layer:** Completely unaware of D²-WORX or its patterns, pure business logic and data modeling.
- **App Layer:** Implements additional, more complex business logic using domain entities and interfaces representing infrastructure concerns via DI.
- **Infra Layer:** Concrete implementations of infrastructure concerns (DB, messaging, caching).
- **API Layer:** Thin gRPC layer exposing service functionality, delegating to App layer handlers. This is where everything is ultimately wired together, including DI registration.

Current Service Structure:

```
ServiceName/ -> Root folder for the service
|
|
|__ ServiceName.Client/ -> Service-owned client library
|   |
|   |__ Messages/ -> Domain event POCOs consumed by other services
|   |__ Interfaces/ -> Handler interfaces for consumers (CQRS, Messaging)
|   |__ CQRS/Handlers/ -> Default handler implementations (cache, disk, gRPC)
|   |__ Messaging/ -> Default messaging handlers and AMQP consumers
|   |__ Extensions.cs -> DI registration for consumer services
|
|
|__ ServiceName.Domain/ -> Project folder for domain layer
|   |
|   |__ Entities/ -> Domain entities
|   |__ Enums/ -> Domain enums
|   |__ Exceptions/ -> Domain-specific exceptions
|   |__ ValueObjects/ -> Domain value objects
|
|
|__ ServiceName.App/ -> Project folder for application layer
|   |
|   |__ Extensions.cs -> DI registration extensions
|   |
|   |__ Interfaces/ -> Interfaces to be implemented in /Implementations or Infra project
|   |   |
|   |   |__ CQRS/ -> Interfaces for CQRS / app-layer handler implementations
|   |   |   |
|   |   |   |__ Handlers/ -> Handler interfaces
|   |   |       |
|   |   |       |__ C/ -> Command handlers (state-changing)
|   |   |       |   |
|   |   |       |   |__ ICommands.cs -> Base partial interface
|   |   |       |   |__ ICommands.DoSomeCommand.cs -> Specific command handler interface
|   |   |       |
|   |   |       |__ Q/ -> Query handlers (read-only - no side effects)
|   |   |       |__ U/ -> Utility handlers (neither read nor write - no side effects)
|   |   |       |__ X/ -> Complex handlers (mixed side effects)
|   |   |
|   |   |__ Messaging/ -> Interfaces for infra-layer messaging implementations
|   |   |   |
|   |   |   |__ Handlers/ -> Messaging handler interfaces
|   |   |       |
|   |   |       |__ Pub/ -> Publisher interfaces
|   |   |       |__ Sub/ -> Subscriber interfaces
|   |   |           |
|   |   |           |__ISubs.cs -> Base partial interface
|   |   |           |__ISubs.SomeEvent.cs -> Specific subscriber interface
|   |   |
|   |   |__ Repository/ -> Interfaces for infra-layer repository implementations
|   |   |   |
|   |   |   |__ Handlers/ -> Repository handler interfaces
|   |   |       |
|   |   |       |__ C/ -> Create handlers
|   |   |       |   |
|   |   |       |   |__ ICreate.cs -> Base partial interface
|   |   |       |   |__ ICreate.SomeEntity.cs -> Specific create handler interface
|   |   |       |
|   |   |       |__ R/ -> Read handlers
|   |   |       |__ U/ -> Update handlers
|   |   |       |__ D/ -> Delete handlers
|   |   |
|   |   |__ Caching/ -> Interfaces for infra-layer caching implementations
|   |                   (if applicable - there is a default caching impl in Contracts)
|   |
|   |__ Implementations/ -> Concrete implementations
|       |
|       |__ CQRS/ -> Application-layer handler implementations
|           |
|           |__ Handlers/ -> Handler implementations
|               |
|               |__ C/ -> Command handlers
|               |   |
|               |   |__ DoSomeCommand.cs -> Command handler implementation
|               |
|               |__ Q/ -> Query handlers
|               |__ U/ -> Utility handlers
|               |__ X/ -> Complex handlers
|
|
|__ ServiceName.Infra/ -> Project folder for infrastructure layer
|   |
|   |__ Extensions.cs -> DI registration extensions
|   |
|   |__ Messaging/ -> Messaging implementations
|   |   |
|   |   |__ Handlers/ -> Messaging handler implementations
|   |   |   |
|   |   |   |__ Pub/ -> Publishers
|   |   |   |__ Sub/ -> Subscribers
|   |   |       |
|   |   |       |__ SomeEvent.cs -> Subscriber implementation
|   |   |
|   |   |__ Publishers/ -> AMQP publisher implementations (ProtoPublisher wrappers)
|   |   |__ Consumers/ -> AMQP consumer implementations (BackgroundService + ProtoConsumer<T>)
|   |
|   |__ Repository/ -> Repository implementations
|       |
|       |__ Entities/ -> EF Core entity configurations
|       |
|       |__ Handlers/ -> Repository handler implementations
|       |   |
|       |   |__ C/ -> Create handlers
|       |   |   |
|       |   |   |__ CreateSomeEntity.cs -> Create handler implementation
|       |   |
|       |   |__ R/ -> Read handlers
|       |   |__ U/ -> Update handlers
|       |   |__ D/ -> Delete handlers
|       |
|       |__ Migrations/ -> EF Core migrations
|       |
|       |__ Seeding/ -> Database seeding scripts
|
|
|__ ServiceName.API/ -> Project folder for API layer
    |
    |__ Program.cs -> Service bootstrap and DI registration (uses Extensions from App and Infra)
    |
    |__ Services/ -> gRPC service implementations
        |
        |__ ServiceNameService.cs -> gRPC service delegating to App layer handlers
```

---

## Practical Guidelines

### When to Create a New Category

**Add a TLC when:**

- You have a fundamentally new architectural concern (e.g., `Scheduling`)
- It would contain 3+ handler files
- It's orthogonal to existing categories

**Add a 2LC when:**

- You need to separate implementation details (e.g., `Entities`, `Migrations`)
- Infrastructure adapters need isolation (e.g., `Publishers`, `Consumers`)

**Add a 3LC when:**

- You're subdividing operations by type (e.g., CRUD, Pub/Sub)

### Naming Conventions

- **Folders:** PascalCase (`Handlers`, `Publishers`, `Consumers`)
- **TLC folders:** Match architectural concern (`CQRS`, `Messaging`, `Repository`)
- **3LC folders:** Single-letter / abbreviations (`C`, `Q`, `R`) or clear terms (`Pub`, `Sub`)
- **Files:**
  - for implementations: **match interface name** (e.g., `SetInMem.cs` for `ISetInMemHandler` interface defined in `ICommands.SetInMem.cs`)
  - for interfaces: prefix with **operation name** (e.g., `ICommands.SetInMem.cs` for `ICommands.cs` partial interface in `/Handlers/C/` folder)

### Extension Pattern

Interfaces are **partial** and split across files by operation:

```csharp
// ICommands.cs - base partial interface
public partial interface ICommands { }

// ICommands.SetInMem.cs - extends with specific handler
public partial interface ICommands
{
    public interface ISetInMemHandler : IHandler<SetInMemInput, SetInMemOutput>;
    public record SetInMemInput(GetReferenceDataResponse Data);
    public record SetInMemOutput;
}
```

**Benefits:**

- One file per operation (easy to find)
- Grouped by common interface (discoverability)
- Clean using aliases in implementations

---

## Benefits of This Structure

### Consistency

Every project follows the same organizational pattern - once learned, navigating any project is intuitive.

### Scalability

Adding new operations, handlers, or categories follows established patterns without restructuring.

### Discoverability

File location tells you exactly what it does: `CQRS/Handlers/Q/GetSomeData.cs` is obviously a query handler.

### Testability

Clear separation between business logic (Handlers) and infrastructure (AMQP messaging, EF Core) enables isolated testing.

### Maintainability

Changes are localized - updating caching strategy only affects `Caching/` implementations, not consumers.

---

## Common Patterns

### Handler Registration (DI)

**.NET** — Uses `Microsoft.Extensions.DependencyInjection` directly:

```csharp
// In Extensions.cs
services.AddTransient<ICommands.ISetInMemHandler, SetInMem>();
services.AddTransient<IQueries.IGetFromMemHandler, GetFromMem>();
```

**Node.js** — Uses `@d2/di` (`ServiceCollection` / `ServiceProvider` / `ServiceScope`), mirroring the .NET pattern:

```typescript
// In registration.ts (mirrors Extensions.cs)
import { ServiceCollection } from "@d2/di";

export function addMyApp(services: ServiceCollection): void {
  services.addTransient(
    IMyHandlerKey,
    (sp) => new MyHandler(sp.resolve(IMyRepoKey), sp.resolve(IHandlerContextKey)),
  );
}
```

```typescript
// In composition-root.ts (mirrors Program.cs)
const services = new ServiceCollection();
services.addInstance(ILoggerKey, logger);
services.addScoped(
  IHandlerContextKey,
  (sp) => new HandlerContext(sp.resolve(IRequestContextKey), sp.resolve(ILoggerKey)),
);
addMyInfra(services, db);
addMyApp(services);
const provider = services.build();
```

`ServiceKey<T>` branded tokens replace erased TypeScript interfaces as DI keys. They are co-located with their handler interfaces. See ADR-011 in `PLANNING.md` for full details.

### RabbitMQ Messaging Registration

```csharp
// In service Infra Extensions.cs
services.AddRabbitMqMessaging(connectionString);       // Registers IConnection + ProtoPublisher
services.AddTransient<UpdatePublisher>();               // Service-specific publisher
services.AddHostedService<UpdatedConsumerService>();    // BackgroundService consumer
```

### Using Aliases for Clean Implementations

This may be an odd thing to do at first, but makes handler files a lot more readable once you "get it".

```csharp
using H = ICommands.ISetInMemHandler;
using I = ICommands.SetInMemInput;
using O = ICommands.SetInMemOutput;

public class SetInMem : BaseHandler<SetInMem, I, O>, H
{
    protected override ValueTask<D2Result<O?>> ExecuteAsync(I input, CancellationToken ct)
    {
        // Implementation
    }
}
```

---

## Scheduled Jobs — Dkron

### Overview

Background maintenance jobs (data purge, cleanup) are triggered by [Dkron](https://dkron.io/) (v4.0.9), a distributed cron service running as a Docker container via Docker Compose.

**Flow:** `Dkron (cron) → REST Gateway (HTTP POST) → gRPC Service (handler)`

Dkron calls the REST gateway's job endpoints using the `X-Api-Key` header for authentication. The gateway validates the key via `ServiceKeyMiddleware`, then proxies the request as a gRPC call to the target service.

### Job Definitions

All jobs run daily during a 2–4 AM UTC maintenance window, staggered by 15 minutes to avoid thundering herd on shared Redis locks.

| Job Name                          | Schedule (UTC) | Gateway Endpoint                                    |
| --------------------------------- | -------------- | --------------------------------------------------- |
| `geo-purge-stale-whois`           | 02:00          | `POST /api/v1/geo/jobs/purge-stale-whois`           |
| `geo-cleanup-orphaned-locations`  | 02:15          | `POST /api/v1/geo/jobs/cleanup-orphaned-locations`  |
| `auth-purge-sessions`             | 02:30          | `POST /api/v1/auth/jobs/purge-sessions`             |
| `auth-purge-sign-in-events`       | 02:45          | `POST /api/v1/auth/jobs/purge-sign-in-events`       |
| `auth-cleanup-invitations`        | 03:00          | `POST /api/v1/auth/jobs/cleanup-invitations`        |
| `auth-cleanup-emulation-consents` | 03:15          | `POST /api/v1/auth/jobs/cleanup-emulation-consents` |
| `comms-purge-deleted-messages`    | 03:30          | `POST /api/v1/comms/jobs/purge-deleted-messages`    |
| `comms-purge-delivery-history`    | 03:45          | `POST /api/v1/comms/jobs/purge-delivery-history`    |

**Ordering:** Geo WhoIs purge runs _before_ orphaned location cleanup — deleting WhoIs records may orphan locations.

### Provisioning

Jobs are automatically provisioned by the `@d2/dkron-mgr` service — a continuously running Node.js reconciler that manages Dkron job state. It creates missing jobs, updates changed jobs, and deletes orphaned managed jobs every 5 minutes (configurable). See `backends/node/services/dkron-mgr/DKRON_MGR.md` for full details.

All managed jobs are tagged with `metadata.managed_by = "d2-dkron-mgr"`. Untagged jobs (manually created) are left untouched.

### Authentication

The Dkron service key is configured in `.env.local` as `GATEWAY_SERVICEKEY__VALIDKEYS__1`. The gateway's `ServiceKeyMiddleware` validates the `X-Api-Key` header and sets `IsTrustedService = true`, which bypasses rate limiting and JWT validation.

### Dashboard

Dkron dashboard: check Docker Compose port mappings for the exposed port (container port 8080).

---

## Evolution & Flexibility

This structure is **descriptive, not prescriptive**. As D²-WORX evolves:

- New TLCs can emerge (e.g., `Scheduling`, `Notifications`)
- Existing categories can be refined (e.g., splitting `X` if it grows large)
- Services can adopt patterns selectively based on their needs

The hierarchy provides **guardrails without constraints**, enabling consistent growth without rigid enforcement.
