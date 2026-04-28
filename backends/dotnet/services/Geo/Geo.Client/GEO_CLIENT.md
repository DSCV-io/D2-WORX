# Geo.Client

Service-owned client library for the Geo microservice. Contains messages, handler interfaces, and default implementations that consumer services depend on for geographic reference data operations (multi-tier caching, disk persistence, gRPC requests, and messaging).

## Files

| File Name                                  | Description                                                                                                                                                                                                                               |
| ------------------------------------------ | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| [Extensions.cs](Extensions.cs)             | DI extension methods: `AddGeoRefDataConsumer` (auto-registers `UpdatedConsumerService` + `ContactEvictionConsumerService` hosted services), `AddGeoRefDataProvider`, `AddWhoIsCache`, `AddContactHandlers`.                               |
| [GeoClientOptions.cs](GeoClientOptions.cs) | Configuration options for WhoIs cache, contact cache, `AllowedContextKeys`, `ApiKey`, and circuit breaker settings.                                                                                                                       |
| [Geo.Client.csproj](Geo.Client.csproj)     | Project file with dependencies on Handler, I18n, InMemoryCache.Default, Interfaces, Messaging.RabbitMQ, Protos.DotNet, Result.Extensions, Utilities, Grpc.Net.ClientFactory, and Microsoft.Extensions.Configuration/Hosting.Abstractions. |

---

## Data Redaction

Geo.Client handlers deal with sensitive data (IP addresses, geographic coordinates) and proto-generated DTOs that cannot be annotated with `[RedactData]`. Two complementary mechanisms protect against PII leaks in logs:

### DefaultOptions Overrides

Most handlers suppress I/O logging entirely via `DefaultOptions` because their input/output contains proto-generated `GetReferenceDataResponse` (large, un-annotatable):

| Handler                 | LogInput    | LogOutput   | Rationale                                                  |
| ----------------------- | ----------- | ----------- | ---------------------------------------------------------- |
| SetInMem                | `false`     | `false`     | Input is `GetReferenceDataResponse` (proto-generated)      |
| SetInDist               | `false`     | `false`     | Input is `GetReferenceDataResponse` (proto-generated)      |
| SetOnDisk               | `false`     | `false`     | Input is `GetReferenceDataResponse` (proto-generated)      |
| GetFromMem              | `false`     | `false`     | Output is `GetReferenceDataResponse` (proto-generated)     |
| GetFromDist             | `false`     | `false`     | Output is `GetReferenceDataResponse` (proto-generated)     |
| GetFromDisk             | `false`     | `false`     | Output is `GetReferenceDataResponse` (proto-generated)     |
| Get                     | `false`     | `false`     | Orchestrates all above, same proto data flows through      |
| FindWhoIs               | `true`      | `false`     | Input annotated with `[RedactData]`; output is proto WhoIs |
| ReqUpdate               | _(default)_ | _(default)_ | I/O is empty / non-sensitive                               |
| CreateContacts          | `false`     | `false`     | Input/output contains contact PII (proto-generated)        |
| DeleteContactsByExtKeys | _(default)_ | _(default)_ | I/O is ext keys / counts (non-sensitive)                   |
| GetContactsByExtKeys    | _(default)_ | `false`     | Output contains contact PII (proto-generated)              |
| UpdateContactsByExtKeys | `false`     | `false`     | Input/output contains contact PII (proto-generated)        |

### [RedactData] Annotations

`FindWhoIsInput` has property-level `[RedactData]` on its sensitive fields, processed by `RedactDataDestructuringPolicy` during Serilog destructuring:

```csharp
public record FindWhoIsInput(
    [property: RedactData(Reason = RedactReason.PersonalInformation)] string IpAddress);
```

This allows input logging to remain enabled (useful for debugging) while ensuring PII is masked in log output.

---

## Interfaces

> ### CQRS
>
> #### Handlers
>
> ##### C (Commands)
>
> | File Name                                                                                               | Description                                                                                                             |
> | ------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------- |
> | [ICommands.cs](Interfaces/CQRS/Handlers/C/ICommands.cs)                                                 | Partial interface defining command operations for geographic reference data state-changing operations.                  |
> | [ICommands.ReqUpdate.cs](Interfaces/CQRS/Handlers/C/ICommands.ReqUpdate.cs)                             | Extends ICommands with IReqUpdateHandler for requesting reference data updates via gRPC.                                |
> | [ICommands.SetInDist.cs](Interfaces/CQRS/Handlers/C/ICommands.SetInDist.cs)                             | Extends ICommands with ISetInDistHandler for storing reference data in Redis distributed cache.                         |
> | [ICommands.SetInMem.cs](Interfaces/CQRS/Handlers/C/ICommands.SetInMem.cs)                               | Extends ICommands with ISetInMemHandler for storing reference data in memory cache.                                     |
> | [ICommands.SetOnDisk.cs](Interfaces/CQRS/Handlers/C/ICommands.SetOnDisk.cs)                             | Extends ICommands with ISetOnDiskHandler for persisting reference data to disk.                                         |
> | [ICommands.CreateContacts.cs](Interfaces/CQRS/Handlers/C/ICommands.CreateContacts.cs)                   | Extends ICommands with ICreateContactsHandler for creating Geo contacts via gRPC. Validates `AllowedContextKeys`.       |
> | [ICommands.DeleteContactsByExtKeys.cs](Interfaces/CQRS/Handlers/C/ICommands.DeleteContactsByExtKeys.cs) | Extends ICommands with IDeleteContactsByExtKeysHandler for deleting Geo contacts by ext keys via gRPC + cache eviction. |
>
> ##### Q (Queries)
>
> | File Name                                                                                       | Description                                                                                                  |
> | ----------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------ |
> | [IQueries.cs](Interfaces/CQRS/Handlers/Q/IQueries.cs)                                           | Partial interface defining query operations for geographic reference data read-only operations.              |
> | [IQueries.GetFromDisk.cs](Interfaces/CQRS/Handlers/Q/IQueries.GetFromDisk.cs)                   | Extends IQueries with IGetFromDiskHandler for retrieving reference data from disk storage.                   |
> | [IQueries.GetFromDist.cs](Interfaces/CQRS/Handlers/Q/IQueries.GetFromDist.cs)                   | Extends IQueries with IGetFromDistHandler for retrieving reference data from Redis.                          |
> | [IQueries.GetFromMem.cs](Interfaces/CQRS/Handlers/Q/IQueries.GetFromMem.cs)                     | Extends IQueries with IGetFromMemHandler for retrieving reference data from memory cache.                    |
> | [IQueries.GetContactsByExtKeys.cs](Interfaces/CQRS/Handlers/Q/IQueries.GetContactsByExtKeys.cs) | Extends IQueries with IGetContactsByExtKeysHandler for fetching contacts by ext keys with local cache-aside. |
>
> ##### X (Complex)
>
> | File Name                                                                                             | Description                                                                                               |
> | ----------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------- |
> | [IComplex.cs](Interfaces/CQRS/Handlers/X/IComplex.cs)                                                 | Partial interface defining complex operations for geographic reference data operations with side effects. |
> | [IComplex.Get.cs](Interfaces/CQRS/Handlers/X/IComplex.Get.cs)                                         | Extends IComplex with IGetHandler for orchestrating multi-tier cache retrieval with fallback chain.       |
> | [IComplex.FindWhoIs.cs](Interfaces/CQRS/Handlers/X/IComplex.FindWhoIs.cs)                             | Extends IComplex with IFindWhoIsHandler for WhoIs lookup with local caching and gRPC fallback.            |
> | [IComplex.UpdateContactsByExtKeys.cs](Interfaces/CQRS/Handlers/X/IComplex.UpdateContactsByExtKeys.cs) | Extends IComplex with IUpdateContactsByExtKeysHandler for replacing contacts at ext keys via gRPC.        |

> ### Messaging
>
> #### Handlers
>
> ##### Sub (Subscribers)
>
> | File Name                                                                              | Description                                                                                    |
> | -------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------- |
> | [ISubs.cs](Interfaces/Messaging/Handlers/Sub/ISubs.cs)                                 | Partial interface defining subscription operations for geographic reference data messaging.    |
> | [ISubs.Updated.cs](Interfaces/Messaging/Handlers/Sub/ISubs.Updated.cs)                 | Extends ISubs with IUpdatedHandler for processing GeoRefDataUpdatedEvent events.               |
> | [ISubs.ContactsEvicted.cs](Interfaces/Messaging/Handlers/Sub/ISubs.ContactsEvicted.cs) | Extends ISubs with IContactsEvictedHandler for processing ContactsEvictedEvent cache eviction. |

---

## CQRS

> ### Handlers
>
> #### C (Commands)
>
> | File Name                                                                | Description                                                                                                  |
> | ------------------------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------ |
> | [ReqUpdate.cs](CQRS/Handlers/C/ReqUpdate.cs)                             | Handler requesting reference data update from Geo service via gRPC, returning the response.                  |
> | [SetInDist.cs](CQRS/Handlers/C/SetInDist.cs)                             | Handler storing serialized GetReferenceDataResponse in Redis distributed cache with configurable expiration. |
> | [SetInMem.cs](CQRS/Handlers/C/SetInMem.cs)                               | Handler storing GetReferenceDataResponse in memory cache with configurable expiration.                       |
> | [SetOnDisk.cs](CQRS/Handlers/C/SetOnDisk.cs)                             | Handler persisting serialized GetReferenceDataResponse to a local file for disk-tier fallback.               |
> | [CreateContacts.cs](CQRS/Handlers/C/CreateContacts.cs)                   | Handler creating Geo contacts via gRPC. Validates `AllowedContextKeys`. PII redacted.                        |
> | [DeleteContactsByExtKeys.cs](CQRS/Handlers/C/DeleteContactsByExtKeys.cs) | Handler deleting Geo contacts by ext keys via gRPC + evicting from local IMemoryCache.                       |
>
> #### Q (Queries)
>
> | File Name                                                          | Description                                                                                                      |
> | ------------------------------------------------------------------ | ---------------------------------------------------------------------------------------------------------------- |
> | [GetFromDisk.cs](CQRS/Handlers/Q/GetFromDisk.cs)                   | Handler retrieving GetReferenceDataResponse from disk by deserializing the persisted protobuf file.              |
> | [GetFromDist.cs](CQRS/Handlers/Q/GetFromDist.cs)                   | Handler retrieving GetReferenceDataResponse from Redis distributed cache.                                        |
> | [GetFromMem.cs](CQRS/Handlers/Q/GetFromMem.cs)                     | Handler retrieving GetReferenceDataResponse from memory cache.                                                   |
> | [GetContactsByExtKeys.cs](CQRS/Handlers/Q/GetContactsByExtKeys.cs) | Handler fetching Geo contacts by ext keys with local cache-aside (immutable, no TTL). Fail-open on gRPC failure. |
>
> #### X (Complex)
>
> | File Name                                                                | Description                                                                                                                                                                   |
> | ------------------------------------------------------------------------ | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
> | [Get.cs](CQRS/Handlers/X/Get.cs)                                         | Orchestrator handler implementing multi-tier cache fallback: Memory → Redis → Disk → gRPC, populating higher tiers on miss.                                                   |
> | [FindWhoIs.cs](CQRS/Handlers/X/FindWhoIs.cs)                             | Handler for WhoIs lookups with local IMemoryCache caching, singleflight deduplication, circuit breaker, and Geo gRPC service fallback. Used by request enrichment middleware. |
> | [UpdateContactsByExtKeys.cs](CQRS/Handlers/X/UpdateContactsByExtKeys.cs) | Handler replacing contacts at ext keys via gRPC (atomic delete + create) + ext-key cache eviction. PII redacted.                                                              |

---

## Messaging

> ### Handlers
>
> #### Sub (Subscribers)
>
> | File Name                                                       | Description                                                                                                           |
> | --------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------- |
> | [Updated.cs](Messaging/Handlers/Sub/Updated.cs)                 | Handler processing GeoRefDataUpdatedEvent events by requesting fresh data from Geo service and updating all caches.   |
> | [ContactsEvicted.cs](Messaging/Handlers/Sub/ContactsEvicted.cs) | Handler processing ContactsEvictedEvent by evicting matching contact IDs and ext-keys from the local in-memory cache. |
>
> ### Consumers
>
> | File Name                                                                                  | Description                                                                                                                                                         |
> | ------------------------------------------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
> | [UpdatedConsumerService.cs](Messaging/Consumers/UpdatedConsumerService.cs)                 | BackgroundService hosting a ProtoConsumer<GeoRefDataUpdatedEvent> that delegates to the Updated sub handler.                                                        |
> | [ContactEvictionConsumerService.cs](Messaging/Consumers/ContactEvictionConsumerService.cs) | BackgroundService hosting a broadcast ProtoConsumer<ContactsEvictedEvent> (exclusive auto-delete queue per instance) that delegates to the ContactsEvicted handler. |

---

## Cache-Invalidation Consumer Auto-Wiring

The Geo service emits two fanout events that consumers must subscribe to in order to keep their local memory caches coherent across processes:

| Event                    | Exchange                      | Purpose                                                                            |
| ------------------------ | ----------------------------- | ---------------------------------------------------------------------------------- |
| `GeoRefDataUpdatedEvent` | `events.geo.refdata.updated`  | Geo published new reference data — refetch + repopulate all ref-data cache tiers.  |
| `ContactsEvictedEvent`   | `events.geo.contacts.evicted` | Geo deleted/replaced contacts — evict matching IDs and ext-keys from local memory. |

Without these subscriptions, the local memory cache silently drifts whenever Geo mutates data (a contact deleted on instance A is still served stale by instance B). Both are fanout exchanges, so each consumer process binds its own auto-deleted queue (`{exchange}.{instanceId}`) and receives every event.

### .NET — Auto-Registration via `AddGeoRefDataConsumer`

Calling `services.AddGeoRefDataConsumer(configuration)` now automatically registers both consumer hosted services:

```csharp
services.AddHostedService<UpdatedConsumerService>();        // ref-data updates
services.AddHostedService<ContactEvictionConsumerService>();// contact evictions
```

Composition roots that register the Geo client get cache invalidation for free — no extra wiring required. Each consumer creates its own auto-deleted queue per process so every instance receives every event.

### Node.js — Opt-in via `wireGeoClientConsumers(options)`

`@d2/geo-client` exports a `wireGeoClientConsumers(options)` helper. Composition roots that have a connected `MessageBus` call it once after the bus is connected:

```typescript
import { wireGeoClientConsumers } from "@d2/geo-client";

// After the MessageBus is connected and the cache store is registered:
await wireGeoClientConsumers({
  bus,
  cacheStore,
  logger,
  // Optional — only services that cache ref-data wire the Updated handler:
  updatedHandlerFactory: () => new RefDataUpdatedHandler(...),
});
```

- `ContactsEvicted` is auto-constructed (only needs the local cache store) and always wired.
- `Updated` (ref-data) is opt-in via the caller-supplied `updatedHandlerFactory` — services that don't cache ref-data omit it.

The Auth and Comms Node services already call this helper in their composition roots.

---

## Security — API Key + Context Key Validation

Contacts are only accessible externally via ext keys (`contextKey` + `relatedEntityId`). ID-based get/delete are removed from client libraries (proto RPCs remain for Geo's internal use).

### Defense-in-Depth Layers

| Layer                 | Mechanism                                        | Enforced By                                           |
| --------------------- | ------------------------------------------------ | ----------------------------------------------------- |
| **Transport**         | gRPC metadata `x-api-key` header                 | Client `CallCredentials` + Geo.API server interceptor |
| **Ownership**         | API key → allowed context keys mapping           | Geo.API `ApiKeyInterceptor` (server-side)             |
| **Client validation** | `AllowedContextKeys` in `GeoClientOptions`       | All contact handlers (defense-in-depth)               |
| **Access pattern**    | Ext-key-only (no PK-based get/delete externally) | Client library (ID handlers removed)                  |

### Cache Key Conventions

| Cache Key Pattern                                       | Value          | Populated By         | Evicted By                                       |
| ------------------------------------------------------- | -------------- | -------------------- | ------------------------------------------------ |
| `geo:contacts-by-extkey:{contextKey}:{relatedEntityId}` | `ContactDTO[]` | GetContactsByExtKeys | DeleteContactsByExtKeys, UpdateContactsByExtKeys |

Single `IMemoryCache` instance. No TTL — contacts are immutable.

---

## Validators

Reusable FluentValidation validators for proto-generated DTOs, exported as single source of truth. Any service creating contacts via Geo should compose these via `.SetValidator()` instead of duplicating rules.

| File Name                                                             | Description                                                                                                                                                                                                                                            |
| --------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| [ContactToCreateValidator.cs](Validators/ContactToCreateValidator.cs) | Aggregate validator for `ContactToCreateDTO`. Mirrors Geo domain factory constraints (names 255, company 255, website 2048, `ietf_bcp47_tag` max 35, optional `iana_identifier`, emails, phones). Supports indexed property names for bulk validation. |

---

## .NET-Specific Notes

- `AddContactHandlers` in `Extensions.cs` registers ext-key handlers and configures `CallCredentials` for the gRPC channel to inject `x-api-key` from `GeoClientOptions.ApiKey`
- `GeoAppOptions.ApiKeyMappings` (server-side, in Geo.API) maps each API key to its allowed context keys
- `ApiKeyInterceptor` in Geo.API validates API key + context keys on all contact RPCs

---

## Usage Examples

### DI Registration (.NET)

Consumer services wire up Geo.Client via extension methods in `Extensions.cs`. Each method registers a focused set of handlers.

**Gateway or any service needing WhoIs + request enrichment:**

```csharp
// Program.cs — Register gRPC client first, then layer-specific handlers.

// 1. Register the gRPC channel for GeoService.
builder.Services.AddGrpcClient<GeoService.GeoServiceClient>(o =>
{
    o.Address = new Uri($"http://{geoAddress}");
});

// 2. Register WhoIs cache (FindWhoIs + circuit breaker + singleflight + memory cache).
//    Reads GEO_CLIENT config section; "GATEWAY" overlays GATEWAY_GEO_CLIENT on top.
builder.Services.AddWhoIsCache(builder.Configuration, servicePrefix: "GATEWAY");

// 3. Register contact handlers (CreateContacts, DeleteContactsByExtKeys, etc.).
//    Same layered config pattern.
builder.Services.AddContactHandlers(builder.Configuration, servicePrefix: "AUTH");

// 4. Register reference data consumer (multi-tier cache: mem → Redis → disk → gRPC).
//    Also auto-registers UpdatedConsumerService + ContactEvictionConsumerService
//    hosted services so the local memory cache stays coherent across instances
//    when Geo publishes ref-data updates or contact evictions.
builder.Services.AddGeoRefDataConsumer(builder.Configuration);
```

**Configuration (appsettings / env vars):**

```jsonc
{
  "GEO_CLIENT": {
    "WhoIsCacheExpiration": "08:00:00",
    "WhoIsCacheMaxEntries": 10000,
    "CircuitBreakerFailureThreshold": 5,
    "CircuitBreakerCooldownDuration": "00:00:30",
  },
  "AUTH_GEO_CLIENT": {
    "ApiKey": "your-api-key-here",
    "AllowedContextKeys": ["auth_user", "auth_org_contact", "auth_org_invitation"],
  },
}
```

### DI Registration (Node.js)

Node.js uses `@d2/di` ServiceKeys and manual wiring. Contact handlers are registered in a setup function; FindWhoIs is constructed directly.

```typescript
import {
  createGeoServiceClient,
  createGeoCircuitBreaker,
  FindWhoIs,
  CreateContacts,
  ICreateContactsKey,
  DEFAULT_GEO_CLIENT_OPTIONS,
  type GeoClientOptions,
} from "@d2/geo-client";
import { MemoryCacheStore } from "@d2/cache-memory";
import { Singleflight } from "@d2/utilities";

// 1. Create gRPC client (singleton).
const geoClient = createGeoServiceClient(geoAddress, geoApiKey);

// 2. FindWhoIs — requires MemoryCacheStore, circuit breaker, singleflight.
const geoOptions: GeoClientOptions = {
  ...DEFAULT_GEO_CLIENT_OPTIONS,
  allowedContextKeys: [],
  apiKey: geoApiKey,
};
const findWhoIs = new FindWhoIs(
  new MemoryCacheStore(),
  geoClient,
  geoOptions,
  createGeoCircuitBreaker(geoOptions, logger),
  new Singleflight(),
  serviceContext,
);

// 3. Contact handlers — register with DI container.
const createContacts = new CreateContacts(geoClient, geoOptions, serviceContext);
services.addInstance(ICreateContactsKey, createContacts);
```

### FindWhoIs — Consuming from Request Enrichment

FindWhoIs is the most common consumer-facing handler. It is called by request enrichment middleware to resolve IP → geo location data.

**.NET (injected per-request via ASP.NET Core DI):**

```csharp
public async Task InvokeAsync(
    HttpContext context,
    IComplex.IFindWhoIsHandler whoIsHandler)
{
    var clientIp = IpResolver.Resolve(context, ...);

    var result = await whoIsHandler.HandleAsync(
        new IComplex.FindWhoIsInput(clientIp),
        context.RequestAborted);

    if (result.CheckSuccess(out var output) && output?.WhoIs is { } whoIs)
    {
        // Use whoIs.Location?.City, whoIs.Location?.CountryIso31661Alpha2Code,
        // whoIs.IsVpn, whoIs.IsProxy, etc. to enrich IRequestContext.
    }
}
```

**Node.js (handler instance called directly):**

```typescript
import { enrichRequest } from "@d2/request-enrichment";

// enrichRequest calls findWhoIs.handleAsync internally:
const requestContext = await enrichRequest(headers, findWhoIs, options, logger);

// Or call FindWhoIs directly:
const result = await findWhoIs.handleAsync({
  ipAddress: clientIp,
});
const output = result.checkSuccess();
if (output?.whoIs) {
  // output.whoIs.location?.city, output.whoIs.isVpn, etc.
}
```

### Contact Operations — CreateContacts

Contact creation goes through the Geo.Client `CreateContacts` handler, which calls Geo service via gRPC. The handler validates context keys against `AllowedContextKeys` before making the call.

**.NET:**

```csharp
// Build the gRPC request with one or more contacts to create.
var request = new CreateContactsRequest
{
    ContactsToCreate =
    {
        new ContactToCreateDTO
        {
            ContextKey = "auth_org_contact",
            RelatedEntityId = orgId,
            PersonalDetails = new PersonalDTO
            {
                FirstName = "Jane",
                LastName = "Doe",
            },
            ContactMethods = new ContactMethodsDTO
            {
                Emails = { new EmailAddressDTO { Value = "jane@example.com", Labels = { "Work" } } },
            },
        },
    },
};

var result = await createContactsHandler.HandleAsync(
    new ICommands.CreateContactsInput(request), ct);

if (result.CheckSuccess(out var output))
{
    // output.Data is List<ContactDTO> — the created contacts with IDs.
}
```

**Node.js:**

```typescript
const result = await createContacts.handleAsync({
  contacts: [
    {
      contextKey: "auth_org_contact",
      relatedEntityId: orgId,
      personalDetails: {
        firstName: "Jane",
        lastName: "Doe",
      },
      contactMethods: {
        emails: [{ value: "jane@example.com", labels: ["Work"] }],
        phoneNumbers: [],
      },
      ietfBcp47Tag: "en-US",
      ianaIdentifier: "America/New_York",
    },
  ],
});

const output = result.checkSuccess();
if (output) {
  // output.data — ContactDTO[] with assigned IDs
}
```

### Cache Key Conventions

Contact caches use the ext-key pattern. WhoIs uses content-addressable hash IDs.

| Entity  | Cache Key Pattern                                       | TTL     | Eviction                                         |
| ------- | ------------------------------------------------------- | ------- | ------------------------------------------------ |
| WhoIs   | `geo:whois:{ip}`                                        | 8 hours | LRU (10,000 entries)                             |
| Contact | `geo:contacts-by-extkey:{contextKey}:{relatedEntityId}` | No TTL  | DeleteContactsByExtKeys, UpdateContactsByExtKeys |

WhoIs cache keys use the format `geo:whois:{ip}` — the content-addressable SHA-256 hash is computed from `SHA256(ip|year|month)` on the server side. Contact cache keys are computed from the ext-key pair (contextKey + relatedEntityId) — contacts are immutable, so no TTL is needed.

---

## Proto Optional Field Handling

Proto responses use `optional` fields (65 fields across all proto definitions). Geo.Client handlers account for this:

- **Array fields**: Use `?? []` or null-coalescing when accessing repeated fields from proto responses to handle cases where the field is absent.
- **String fields**: Use `?.` optional chaining when reading string properties from proto DTOs (e.g., `whoIs.Location?.City`, `contact.PersonalDetails?.FirstName`).
- **Cache key construction**: Guards against null/undefined key components — skips the cache entry if required key fields are missing rather than constructing an invalid key.
- **`.ToNullIfEmpty()`**: Applied at proto→domain boundaries to convert empty strings (proto3 default) to `null` where the domain model expects `string?`.

## TS Equivalent

`@d2/geo-client` — same interface + handler structure, `RedactionSpec` for logging suppression, field-level masking on `FindWhoIs` inputs. Zod schemas (`contactInputSchema` etc.) exported for the same reuse pattern.
