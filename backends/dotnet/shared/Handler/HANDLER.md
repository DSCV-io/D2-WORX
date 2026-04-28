# Handler

Handler infrastructure implementing CQRS pattern with automatic logging, tracing, and error handling. Base classes for commands/queries with D2Result integration and OpenTelemetry instrumentation.

## Files

| File Name                                        | Description                                                                                                                                                                                               |
| ------------------------------------------------ | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| [IHandler.cs](IHandler.cs)                       | Generic handler interface defining async operation contracts with input/output types, cancellation tokens, and optional handler configuration.                                                            |
| [BaseHandler.cs](BaseHandler.cs)                 | Abstract base handler with built-in OpenTelemetry tracing, structured logging, execution timing, and exception handling wrapper around ExecuteAsync.                                                      |
| [IHandlerContext.cs](IHandlerContext.cs)         | Context interface bundling IRequestContext and ILogger to reduce constructor boilerplate across handlers.                                                                                                 |
| [HandlerContext.cs](HandlerContext.cs)           | Default implementation of IHandlerContext providing request context and logging access.                                                                                                                   |
| [IRequestContext.cs](IRequestContext.cs)         | Unified request context: tracing, identity, agent/target org, network/enrichment fields, trust flag, and org relationship helpers.                                                                        |
| [HandlerOptions.cs](HandlerOptions.cs)           | Configuration record for handler execution behavior including input/output logging, time warning thresholds, and suppression flags.                                                                       |
| [BHASW.cs](BHASW.cs)                             | Internal static class providing ActivitySource for OpenTelemetry distributed tracing of handler operations. Acronym: Base Handler Activity Source Wrapper.                                                |
| [OrgType.cs](OrgType.cs)                         | Enum defining organization types: Admin (full system access), Support (customer support capabilities), Affiliate (partners/resellers), Customer (standard users), ThirdParty (external partners/clients). |
| [Auth/JwtClaimTypes.cs](Auth/JwtClaimTypes.cs)   | JWT claim type string constants (sub, email, name, orgId, orgName, orgType, role, emulatedOrgId, emulatedOrgName, emulatedOrgType, isEmulating, impersonatedBy, isImpersonating, fp).                     |
| [Auth/OrgTypeValues.cs](Auth/OrgTypeValues.cs)   | Organization type string constants + STAFF/ALL arrays for policy evaluation.                                                                                                                              |
| [Auth/RoleValues.cs](Auth/RoleValues.cs)         | Role string constants (auditor, agent, officer, owner) + HIERARCHY dictionary + `AtOrAbove(minRole)` helper.                                                                                              |
| [Auth/AuthPolicies.cs](Auth/AuthPolicies.cs)     | Named authorization policy constants (Authenticated, HasActiveOrg, StaffOnly, AdminOnly).                                                                                                                 |
| [Auth/RequestHeaders.cs](Auth/RequestHeaders.cs) | Custom HTTP header constants (Idempotency-Key, X-Client-Fingerprint).                                                                                                                                     |
| [Validators.cs](Validators.cs)                   | FluentValidation extensions: `IsValidIpAddress`, `IsValidHashId`, `IsValidGuid`, `IsValidEmail`, `IsValidPhoneE164`, `IsAllowedContextKey`.                                                               |

---

## HandlerOptions

Configuration record controlling handler logging and timing behavior. Used both as per-call options and as handler-level defaults via `DefaultOptions`.

```csharp
public record HandlerOptions(
    bool SuppressTimeWarnings = false,  // Skip elapsed-time warning/critical log levels
    bool LogInput = true,               // Log input at Debug level via Serilog {@Input} destructuring
    bool LogOutput = true,              // Log output at Debug level via Serilog {@Output} destructuring
    long WarningThresholdMs = 100,      // Elapsed time threshold for Warning log level
    long CriticalThresholdMs = 500);    // Elapsed time threshold for Critical log level
```

## DefaultOptions

`BaseHandler` exposes a `virtual HandlerOptions DefaultOptions` property that subclasses override to set handler-level logging defaults. When `HandleAsync` receives `null` for the `options` parameter, it falls through to `DefaultOptions`.

**Resolution order:** Per-call options (if provided) > `DefaultOptions` override (if handler defines one) > base defaults (`new HandlerOptions()`).

```csharp
// Handler that suppresses I/O logging by default (e.g., handles proto-generated PII data)
protected override HandlerOptions DefaultOptions => new(LogInput: false, LogOutput: false);

// Caller can still override per-call:
await handler.HandleAsync(input, ct, options: new HandlerOptions(LogInput: true));
```

**BOTH app AND repo handlers** must declare a `DefaultOptions` override when processing PII. This is not app-layer-only — each `BaseHandler` independently logs its I/O, so a repo handler that receives PII (e.g., contact details, IP addresses, filenames) needs its own `DefaultOptions => new(LogInput: false, LogOutput: false)` to prevent PII leaks in logs.

**Interaction with `[RedactData]`:** `DefaultOptions` controls whether `BaseHandler` logs input/output at all. The `RedactDataDestructuringPolicy` (in ServiceDefaults) handles _how_ logged objects are serialized — masking `[RedactData]`-annotated fields. Both mechanisms work together: `DefaultOptions` is the coarse switch, `[RedactData]` is the fine-grained field-level mask.

## OTel Metrics

All handlers automatically emit four metrics via the `BHASW` static instrumentation:

| Metric                   | Type      | Description                                     |
| ------------------------ | --------- | ----------------------------------------------- |
| `d2.handler.duration`    | Histogram | Execution duration in milliseconds              |
| `d2.handler.invocations` | Counter   | Total handler invocations                       |
| `d2.handler.failures`    | Counter   | Non-success results (D2Result.Success == false) |
| `d2.handler.exceptions`  | Counter   | Unhandled exceptions caught by BaseHandler      |

All metrics are tagged with `handler.name` for per-handler breakdowns.

## IRequestContext

Unified request context interface carrying tracing, identity, organization, network/enrichment, and trust information. Replaces the former separate `IRequestInfo` (network fields) and `IRequestContext` (identity fields) — all fields now live on a single interface.

Two implementations exist:

- **`MutableRequestContext`** (in `RequestEnrichment.Default`) — used by the gateway, progressively populated by middleware pipeline
- **`RequestContext`** (in `Handler.Extensions`) — used by non-gateway gRPC services, populated from JWT claims only (network/enrichment and trust fields return `null`/`false`)

### Field Groups

**Tracing:**

| Property      | Type      | Description               |
| ------------- | --------- | ------------------------- |
| `TraceId`     | `string?` | OTel trace ID             |
| `RequestId`   | `string?` | ASP.NET `TraceIdentifier` |
| `RequestPath` | `string?` | HTTP request path         |

**User / Identity:**

| Property          | Type      | Description                                      |
| ----------------- | --------- | ------------------------------------------------ |
| `IsAuthenticated` | `bool?`   | Whether the user is authenticated                |
| `UserId`          | `Guid?`   | User ID (impersonated user during impersonation) |
| `Email`           | `string?` | User email                                       |
| `Username`        | `string?` | User login handle                                |

**Agent Organization** (user's actual org membership):

| Property       | Type       | Description                  |
| -------------- | ---------- | ---------------------------- |
| `AgentOrgId`   | `Guid?`    | Agent org ID                 |
| `AgentOrgName` | `string?`  | Agent org name               |
| `AgentOrgType` | `OrgType?` | Agent org type enum          |
| `AgentOrgRole` | `string?`  | User's role in the agent org |

**Target Organization** (org operations execute against — emulated org if emulating, else agent org):

| Property        | Type       | Description                                       |
| --------------- | ---------- | ------------------------------------------------- |
| `TargetOrgId`   | `Guid?`    | Target org ID                                     |
| `TargetOrgName` | `string?`  | Target org name                                   |
| `TargetOrgType` | `OrgType?` | Target org type enum                              |
| `TargetOrgRole` | `string?`  | Role in target org (`"auditor"` during emulation) |

**Org Emulation / User Impersonation:**

| Property                | Type      | Description                            |
| ----------------------- | --------- | -------------------------------------- |
| `IsOrgEmulating`        | `bool?`   | Staff viewing another org as read-only |
| `ImpersonatedBy`        | `Guid?`   | Admin who initiated impersonation      |
| `ImpersonatingEmail`    | `string?` | Impersonator's email                   |
| `ImpersonatingUsername` | `string?` | Impersonator's username                |
| `IsUserImpersonating`   | `bool?`   | Whether user impersonation is active   |

**Network / Enrichment** (gateway only — `null` in non-gateway services):

| Property            | Type      | Description                                    |
| ------------------- | --------- | ---------------------------------------------- |
| `ClientIp`          | `string?` | Resolved client IP address                     |
| `ServerFingerprint` | `string?` | SHA-256 of UA + Accept headers                 |
| `ClientFingerprint` | `string?` | Client-provided fingerprint from cookie/header |
| `DeviceFingerprint` | `string?` | SHA-256(clientFP + serverFP + clientIp)        |
| `WhoIsHashId`       | `string?` | Content-addressable hash for WhoIs lookups     |
| `City`              | `string?` | City from WhoIs data                           |
| `CountryCode`       | `string?` | ISO 3166-1 alpha-2 country code                |
| `SubdivisionCode`   | `string?` | ISO 3166-2 subdivision code                    |
| `IsVpn`             | `bool?`   | Whether IP is from a VPN                       |
| `IsProxy`           | `bool?`   | Whether IP is from a proxy                     |
| `IsTor`             | `bool?`   | Whether IP is from a Tor exit node             |
| `IsHosting`         | `bool?`   | Whether IP is from a hosting provider          |

**Trust:**

| Property           | Type    | Description                                  |
| ------------------ | ------- | -------------------------------------------- |
| `IsTrustedService` | `bool?` | Request from a trusted service (`X-Api-Key`) |

**`bool?` auth flags:** `IsAuthenticated`, `IsTrustedService`, `IsOrgEmulating`, and `IsUserImpersonating` use `bool?` (nullable). `null` = "not yet determined" (pre-auth middleware). `false` = "confirmed not." Never treat `null` as `false` — e.g., `if (!ctx.IsAuthenticated)` is wrong when the value is `null` because `!null` is `null` (falsey in C# conditional), which would incorrectly deny access before auth middleware has run. Always check explicitly: `if (ctx.IsAuthenticated != true)` or `if (ctx.IsAuthenticated is null or false)`.

**Helpers** (computed from org fields):

| Property           | Type   | Description                    |
| ------------------ | ------ | ------------------------------ |
| `IsAgentStaff`     | `bool` | Agent org is Support or Admin  |
| `IsAgentAdmin`     | `bool` | Agent org is Admin             |
| `IsTargetingStaff` | `bool` | Target org is Support or Admin |
| `IsTargetingAdmin` | `bool` | Target org is Admin            |

### Span Enrichment Guard

`BaseHandler` enriches OTel spans with request context attributes. The `IsOrgEmulating` attribute is only set when `IsAuthenticated` is `true` — this prevents unauthenticated requests (pre-auth handlers, health checks) from reporting a misleading `isOrgEmulating: false` on spans. This guard is enforced on both .NET and Node.js `BaseHandler` implementations.

---

## Implementation Example

Complete handler showing the established pattern. Implementations live in `ServiceName.App/Implementations/CQRS/Handlers/{C,Q,U,X}/`. Interfaces live in `ServiceName.App/Interfaces/CQRS/Handlers/{C,Q,U,X}/`.

```csharp
// -----------------------------------------------------------------------
// <copyright file="SetInMem.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Geo.Client.CQRS.Handlers.C;

using D2.Shared.Handler;
using D2.Shared.Result;

// Using aliases for clean implementations — one per handler
using H = D2.Geo.Client.Interfaces.CQRS.Handlers.C.ICommands.ISetInMemHandler;
using I = D2.Geo.Client.Interfaces.CQRS.Handlers.C.ICommands.SetInMemInput;
using O = D2.Geo.Client.Interfaces.CQRS.Handlers.C.ICommands.SetInMemOutput;

/// <summary>
/// Handler for setting georeference data in the in-memory cache.
/// </summary>
public class SetInMem : BaseHandler<SetInMem, I, O>, H
{
    private readonly IUpdate.ISetHandler<GeoRefData> r_memoryCacheSet;

    /// <summary>
    /// Initializes a new instance of the <see cref="SetInMem"/> class.
    /// </summary>
    public SetInMem(
        IUpdate.ISetHandler<GeoRefData> memoryCacheSet,
        IHandlerContext context)
        : base(context)
    {
        r_memoryCacheSet = memoryCacheSet;
    }

    /// <inheritdoc />
    protected override HandlerOptions DefaultOptions => new(LogInput: false, LogOutput: false);

    /// <summary>
    /// Executes the handler.
    /// </summary>
    protected override async ValueTask<D2Result<O?>> ExecuteAsync(
        I input,
        CancellationToken ct = default)
    {
        var setR = await r_memoryCacheSet.HandleAsync(
            new(CacheKeys.REFDATA, input.Data), ct);

        return setR.Success
            ? D2Result<O?>.Ok()
            : D2Result<O?>.BubbleFail(setR);
    }
}
```

### Key Points

- **Using aliases** (`H`, `I`, `O`) — keeps handler files readable. One set per handler.
- **`BaseHandler<TSelf, TInput, TOutput>`** — first type param is the handler itself (for OTel span naming).
- **Implement the interface** (`H`) — required for DI registration: `services.AddTransient<H, SetInMem>()`.
- **Constructor takes `IHandlerContext`** — bundles `IRequestContext` + `ILogger`. Always pass to `base(context)`.
- **`DefaultOptions`** — optional override. Suppresses I/O logging for PII-heavy handlers.
- **Return `D2Result<O?>`** — use semantic factories (`Ok`, `NotFound`, `BubbleFail`, etc.). See `RESULT.md`.
- **traceId auto-injection** — `BaseHandler` automatically injects `traceId` into every `D2Result` it returns. Handlers should NOT pass `traceId: TraceId` manually when constructing results.
