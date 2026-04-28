# @d2/handler

BaseHandler pattern with automatic OTel tracing, metrics, and structured logging. Mirrors `D2.Shared.Handler` in .NET. Layer 1.

## Files

| File Name                                              | Description                                                                                                                                                                           |
| ------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| [base-handler.ts](src/base-handler.ts)                 | `BaseHandler` abstract class with OTel spans, 4 metrics, input/output redaction, `validateInput`, traceId auto-injection.                                                             |
| [i-handler.ts](src/i-handler.ts)                       | `IHandler<TInput, TOutput>` interface with optional `redaction` property.                                                                                                             |
| [i-handler-context.ts](src/i-handler-context.ts)       | `IHandlerContext` interface bundling `IRequestContext` + `ILogger`.                                                                                                                   |
| [handler-context.ts](src/handler-context.ts)           | `HandlerContext` implementation of `IHandlerContext`.                                                                                                                                 |
| [i-request-context.ts](src/i-request-context.ts)       | `IRequestContext` interface (tracing, identity, org, network/enrichment, trust, helpers).                                                                                             |
| [handler-options.ts](src/handler-options.ts)           | `HandlerOptions` type + `DEFAULT_HANDLER_OPTIONS` constant.                                                                                                                           |
| [redaction-spec.ts](src/redaction-spec.ts)             | `RedactionSpec` type for declaring handler input/output redaction posture.                                                                                                            |
| [validators.ts](src/validators.ts)                     | Shared Zod refinements (`zodGuid`, `zodHashId`, `zodIpAddress`, `zodEmail`, `zodPhoneE164`, `zodNonEmptyString`, `zodNonEmptyArray`, `zodAllowedContextKey`) + standalone validators. |
| [org-type.ts](src/org-type.ts)                         | `OrgType` enum (Admin, Support, Affiliate, Customer, ThirdParty).                                                                                                                     |
| [service-keys.ts](src/service-keys.ts)                 | DI keys for `IRequestContext` (`IRequestContextKey`) and `IHandlerContext` (`IHandlerContextKey`).                                                                                    |
| [ambient-context.ts](src/ambient-context.ts)           | `requestContextStorage` + `requestLoggerStorage` — AsyncLocalStorage for per-request ambient context.                                                                                 |
| [create-service-scope.ts](src/create-service-scope.ts) | `createServiceScope()` — shared per-request DI scope factory (used by Auth, Comms, E2E). Sets ambient storage.                                                                        |
| [index.ts](src/index.ts)                               | Barrel re-export of all types, interfaces, and classes.                                                                                                                               |

---

## Ambient Context (AsyncLocalStorage)

`HandlerContext` checks two `AsyncLocalStorage` instances before falling back to constructor-provided values:

- **`requestContextStorage`** — per-request `IRequestContext`
- **`requestLoggerStorage`** — per-request `ILogger` (with auth/network bindings)

This means ALL handlers — including pre-auth singletons (rate limit, FindWhoIs, throttle) — automatically see per-request context when ambient storage is active. Mirrors .NET's DI scoping where `HttpContext.Features` provides per-request `IRequestContext` to all handlers regardless of registration lifetime.

**Middleware sets storage via `.run()`** to establish the async context boundary. Downstream middleware (e.g., auth scope) upgrades the stored value via **`.enterWith()`** within the same async context — for example, replacing an enrichment-only context with a fully auth-enriched one after session extraction.

**Concurrent requests are isolated** — each `AsyncLocalStorage.run()` creates an independent async context, so two simultaneous requests with different users see their own `IRequestContext`.

**Fallback**: When no ambient storage is active (e.g., unit tests, BetterAuth callbacks), `HandlerContext` returns the constructor-provided values.

---

## HandlerOptions

Controls handler logging and timing behavior. Can be provided per-call or as a handler default.

```typescript
interface HandlerOptions {
  logInput: boolean; // Log input at debug level (default: true)
  logOutput: boolean; // Log output at debug level (default: true)
  suppressTimeWarnings: boolean; // Skip elapsed-time warning/critical levels (default: false)
  warningThresholdMs: number; // Elapsed time for warning level (default: 100)
  criticalThresholdMs: number; // Elapsed time for critical level (default: 500)
}
```

---

## RedactionSpec

Declares a handler's intrinsic data redaction posture. Defined alongside handler interfaces, wired by implementations.

```typescript
interface RedactionSpec {
  readonly inputFields?: readonly string[]; // Top-level input fields to mask with "[REDACTED]"
  readonly outputFields?: readonly string[]; // Top-level output fields to mask with "[REDACTED]"
  readonly suppressInput?: boolean; // Skip input logging entirely
  readonly suppressOutput?: boolean; // Skip output logging entirely
}
```

**Interaction rules:**

- `HandlerOptions.logInput=false` OR `RedactionSpec.suppressInput=true` → input not logged
- Field masking only applies when logging is not suppressed
- Suppression wins over field masking when both are set

**Every handler touching PII MUST declare a `RedactionSpec`.** This applies to BOTH app AND repo handlers — each `BaseHandler` independently logs its I/O, so a repo handler that receives the same PII as its parent app handler needs its own redaction declaration.

**When to use suppression vs. field masking:**

- **`suppressInput: true`** — use when the input contains raw buffers (e.g., file upload content) or deeply nested structures that cannot be meaningfully field-masked
- **`suppressOutput: true`** — use when the output contains complex PII objects (e.g., `File` entities with `displayName`, contact records) where listing every field would be fragile

---

## Input Validation (`validateInput`)

`BaseHandler` provides `this.validateInput(schema, input)` for Zod-based input validation inside `executeAsync`. Returns `D2Result.validationFailed()` with properly formatted `inputErrors` on failure.

```typescript
protected async executeAsync(input: Input): Promise<D2Result<Output | undefined>> {
  const validation = this.validateInput(inputSchema, input);
  if (validation.failed) {
    return D2Result.bubbleFail(validation);
  }
  // ... handler logic
}
```

**All handlers MUST validate input.** All string fields need max length. Use the shared Zod refinements from `@d2/handler`:

| Refinement             | Description                                             |
| ---------------------- | ------------------------------------------------------- |
| `zodGuid`              | Valid UUID (any version)                                |
| `zodHashId`            | 64-char hex SHA-256 hash ID                             |
| `zodIpAddress`         | Valid IPv4 or IPv6 address                              |
| `zodEmail`             | Basic email format validation                           |
| `zodPhoneE164`         | 7-15 digits (E.164)                                     |
| `zodNonEmptyString(n)` | Non-empty string with max length `n`                    |
| `zodNonEmptyArray(s)`  | Non-empty array of items matching schema `s`            |
| `zodAllowedContextKey` | String in allowed list (empty list disables validation) |

---

## TraceId Auto-Injection

`BaseHandler` automatically injects the ambient `traceId` into every `D2Result` returned from `executeAsync`. **Handlers do NOT pass `traceId` manually.** This eliminates the need for `traceId: this.traceId` on every factory call.

Only pass `traceId` explicitly when constructing `D2Result` **outside** of a handler (e.g., middleware error responses).

---

## OTel Metrics

All handlers automatically emit four metrics:

| Metric                   | Type      | Description                                     |
| ------------------------ | --------- | ----------------------------------------------- |
| `d2.handler.duration`    | Histogram | Execution duration in milliseconds              |
| `d2.handler.invocations` | Counter   | Total handler invocations                       |
| `d2.handler.failures`    | Counter   | Non-success results (D2Result.success == false) |
| `d2.handler.exceptions`  | Counter   | Unhandled exceptions caught by BaseHandler      |

All metrics are tagged with `handler.name` for per-handler breakdowns.

---

## IRequestContext

Unified request context interface carrying tracing, identity, organization, network/enrichment, and trust information. Mirrors `D2.Shared.Handler.IRequestContext` in .NET.

### Field Groups

**Tracing:**

| Property      | Type      | Description       |
| ------------- | --------- | ----------------- |
| `traceId`     | `string?` | OTel trace ID     |
| `requestId`   | `string?` | HTTP request ID   |
| `requestPath` | `string?` | HTTP request path |

**User / Identity:**

| Property          | Type            | Description                                      |
| ----------------- | --------------- | ------------------------------------------------ |
| `isAuthenticated` | `boolean\|null` | `null` = auth hasn't run yet (pre-auth handlers) |
| `userId`          | `string?`       | User ID (impersonated user during impersonation) |
| `email`           | `string?`       | User email                                       |
| `username`        | `string?`       | User login handle                                |

**Agent Organization** (user's actual org membership):

| Property       | Type       | Description                  |
| -------------- | ---------- | ---------------------------- |
| `agentOrgId`   | `string?`  | Agent org ID                 |
| `agentOrgName` | `string?`  | Agent org name               |
| `agentOrgType` | `OrgType?` | Agent org type enum          |
| `agentOrgRole` | `string?`  | User's role in the agent org |

**Target Organization** (org operations execute against — emulated org if emulating, else agent org):

| Property        | Type       | Description                                       |
| --------------- | ---------- | ------------------------------------------------- |
| `targetOrgId`   | `string?`  | Target org ID                                     |
| `targetOrgName` | `string?`  | Target org name                                   |
| `targetOrgType` | `OrgType?` | Target org type enum                              |
| `targetOrgRole` | `string?`  | Role in target org (`"auditor"` during emulation) |

**Org Emulation / User Impersonation:**

| Property                | Type            | Description                                                      |
| ----------------------- | --------------- | ---------------------------------------------------------------- |
| `isOrgEmulating`        | `boolean\|null` | `null` = auth hasn't run. Staff viewing another org as read-only |
| `impersonatedBy`        | `string?`       | Admin who initiated impersonation                                |
| `impersonatingEmail`    | `string?`       | Impersonator's email                                             |
| `impersonatingUsername` | `string?`       | Impersonator's username                                          |
| `isUserImpersonating`   | `boolean\|null` | `null` = auth hasn't run                                         |

**Network / Enrichment** (gateway only — `undefined` in non-gateway services):

| Property            | Type       | Description                                    |
| ------------------- | ---------- | ---------------------------------------------- |
| `clientIp`          | `string?`  | Resolved client IP address                     |
| `serverFingerprint` | `string?`  | SHA-256 of UA + Accept headers                 |
| `clientFingerprint` | `string?`  | Client-provided fingerprint from cookie/header |
| `deviceFingerprint` | `string?`  | SHA-256(clientFP + serverFP + clientIp)        |
| `whoIsHashId`       | `string?`  | Content-addressable hash for WhoIs lookups     |
| `city`              | `string?`  | City from WhoIs data                           |
| `countryCode`       | `string?`  | ISO 3166-1 alpha-2 country code                |
| `subdivisionCode`   | `string?`  | ISO 3166-2 subdivision code                    |
| `isVpn`             | `boolean?` | Whether IP is from a VPN                       |
| `isProxy`           | `boolean?` | Whether IP is from a proxy                     |
| `isTor`             | `boolean?` | Whether IP is from a Tor exit node             |
| `isHosting`         | `boolean?` | Whether IP is from a hosting provider          |

**Trust:**

| Property           | Type            | Description                                    |
| ------------------ | --------------- | ---------------------------------------------- |
| `isTrustedService` | `boolean\|null` | `null` = service-key middleware hasn't run yet |

**Helpers** (computed from org fields):

| Property           | Type       | Description                    |
| ------------------ | ---------- | ------------------------------ |
| `isAgentStaff`     | `boolean?` | Agent org is Support or Admin  |
| `isAgentAdmin`     | `boolean?` | Agent org is Admin             |
| `isTargetingStaff` | `boolean?` | Target org is Support or Admin |
| `isTargetingAdmin` | `boolean?` | Target org is Admin            |

### Span Enrichment Guard

`BaseHandler` enriches OTel spans with request context attributes. The `isOrgEmulating` attribute is only set when `isAuthenticated` is `true` — this prevents unauthenticated requests (pre-auth handlers, health checks) from reporting a misleading `isOrgEmulating: false` on spans. This guard is enforced on both .NET and Node.js `BaseHandler` implementations.

---

## createServiceScope

Shared factory that creates a disposable DI scope with a fresh `traceId` and no auth context. Used for per-RPC, per-message, callback, and startup operations across Auth, Comms, and E2E tests.

```typescript
import { createServiceScope } from "@d2/handler";

// Per-RPC scope in gRPC service
const scope = createServiceScope(provider);
const handler = scope.resolve(IMyHandlerKey);
const result = await handler.handleAsync(input);

// Per-message scope in RabbitMQ consumer
const scope = createServiceScope(provider, customLogger);
```

Sets up:

- Fresh `IRequestContext` with `crypto.randomUUID()` traceId
- `IHandlerContext` (context + logger) registered in scope
- Ambient `AsyncLocalStorage` bound via `enterWith()` so all handlers see per-request context

---

## Implementation Examples

### Interface File

Interfaces define `Input`, `Output`, `RedactionSpec` constant, and `IXxxHandler`. They live in `interfaces/{c,q,u,x}/` subdirectories with barrel re-exports.

```typescript
// interfaces/q/get-contacts-by-ids.ts
import type { IHandler, RedactionSpec } from "@d2/handler";
import type { ContactDTO } from "@d2/protos";

export interface GetContactsByIdsInput {
  ids: string[];
}

export interface GetContactsByIdsOutput {
  data: Map<string, ContactDTO>;
}

/** Recommended redaction for GetContactsByIds handlers. */
export const GET_CONTACTS_BY_IDS_REDACTION: RedactionSpec = {
  suppressOutput: true, // Output contains contact PII
};

/** Handler interface. Requires redaction (output contains PII). */
export interface IGetContactsByIdsHandler extends IHandler<
  GetContactsByIdsInput,
  GetContactsByIdsOutput
> {
  readonly redaction: RedactionSpec;
}
```

### Handler Implementation

Implementations live in `handlers/{c,q,u,x}/` subdirectories and `implements` the interface.

```typescript
// handlers/q/get-contacts-by-ids.ts
import { BaseHandler, type IHandlerContext } from "@d2/handler";
import { D2Result } from "@d2/result";
import { Queries } from "../../interfaces/index.js";

type Input = Queries.GetContactsByIdsInput;
type Output = Queries.GetContactsByIdsOutput;

export class GetContactsByIds
  extends BaseHandler<Input, Output>
  implements Queries.IGetContactsByIdsHandler
{
  // Redaction constant defined in interface file, referenced here
  override get redaction() {
    return Queries.GET_CONTACTS_BY_IDS_REDACTION;
  }

  private readonly store: MemoryCacheStore;
  private readonly geoClient: GeoServiceClient;

  constructor(store: MemoryCacheStore, geoClient: GeoServiceClient, context: IHandlerContext) {
    super(context);
    this.store = store;
    this.geoClient = geoClient;
  }

  protected async executeAsync(input: Input): Promise<D2Result<Output | undefined>> {
    // Validate input (Zod schema)
    const validation = this.validateInput(inputSchema, input);
    if (validation.failed) return D2Result.bubbleFail(validation);

    // Implementation — BaseHandler auto-injects traceId, no manual passing needed
    const cached = this.store.get(input.ids);
    if (cached) return D2Result.ok({ data: cached });

    const result = await this.geoClient.getContactsByIds(input.ids);
    if (result.failed) return D2Result.bubbleFail(result);

    return D2Result.ok({ data: result.data });
  }
}

// Re-export I/O types from interface (consumers import from handler barrel)
export type {
  GetContactsByIdsInput,
  GetContactsByIdsOutput,
} from "../../interfaces/q/get-contacts-by-ids.js";
```

### DI Registration

```typescript
// di.ts (layer registration)
import type { ServiceCollection } from "@d2/di";
import { IGetContactsByIdsKey } from "../interfaces/index.js";
import { GetContactsByIds } from "../handlers/q/get-contacts-by-ids.js";

export function addGeoClientApp(services: ServiceCollection): void {
  services.addTransient(IGetContactsByIdsKey, (sp) => {
    const store = sp.resolve(IMemoryCacheStoreKey);
    const geoClient = sp.resolve(IGeoServiceClientKey);
    const context = sp.resolve(IHandlerContextKey);
    return new GetContactsByIds(store, geoClient, context);
  });
}
```

### Key Points

- **Type aliases** (`type Input = ...`) — keeps handler file clean.
- **`extends BaseHandler<Input, Output>`** — two type params (no TSelf in Node.js).
- **`implements Queries.IXxxHandler`** — required for DI: `services.addTransient(IXxxKey, (sp) => new Xxx(...))`.
- **`override get redaction()`** — returns the constant from the interface file. Only needed for PII handlers.
- **Constructor takes `IHandlerContext`** — always pass to `super(context)`.
- **`executeAsync`** — override this (not `handleAsync`). Return `D2Result<Output | undefined>`.
- **`validateInput`** — call at the top of `executeAsync` with a Zod schema. Returns `D2Result.validationFailed()` on failure.
- **TraceId auto-injected** — do NOT pass `traceId: this.traceId` manually. BaseHandler handles it.
- **Re-export types** — at bottom of handler file so consumers can import I/O types from the handler barrel.
- **Barrel files** — `interfaces/index.ts` re-exports as `import * as Commands` / `import * as Queries` / etc.

---

## .NET Equivalent

`D2.Shared.Handler` — `BaseHandler<THandler, TInput, TOutput>` with `DefaultOptions` virtual property, `HandlerOptions` record, OTel `ActivitySource` + metrics via `BHASW`. See [.NET HANDLER.md](../../dotnet/shared/Handler/HANDLER.md).
