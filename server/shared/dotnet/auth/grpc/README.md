<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Shared.Auth.Grpc

> Parent: [`server/shared/dotnet/`](../../README.md)

gRPC-transport binding for [`D2.Shared.Auth`](../core/README.md) — server-side `Grpc.Core.Interceptors.Interceptor` subclass that runs the JWT validation pipeline + session liveness check on inbound gRPC calls, emits `RpcException(Status, Trailers)` on failure with the `d2_error_code` / `d2_messages` / `traceid` trailer triple, and supports per-method scope requirements via attribute-on-method declarations OR fluent endpoint metadata.

Lives in its own csproj (separate from `D2.Shared.Auth`) so the `Grpc.AspNetCore.Server` framework reference is opt-in: HTTP-only services / worker processes / console hosts that consume `D2.Shared.Auth` for JWT validation in non-gRPC paths don't need to drag in the gRPC server framework. Sibling [`D2.Shared.Auth.Http`](../http/README.md) holds the HTTP-transport binding under the same logic. The two transport-binding csprojs are siblings (no inter-csproj dep): each registers an identical scoped `IRequestContext` resolver lambda that reads from a shared `HttpContext.Items` slot, so a single dual-transport host wires both extensions and resolves `IRequestContext` correctly under either transport.

The codegen-emitted `D2GrpcUserStateKeys.g.cs` (sourced from `contracts/in-process-keys/keys.spec.json` via `D2.Shared.InProcessKeys.SourceGen`) lands in the tracked `Generated/D2.Shared.InProcessKeys.SourceGen/...` directory — committed for inspection, IDE navigation, and PR diff review; re-emitted on every `dotnet build`; do not hand-edit.

## Public API surface

### Composition

```csharp
services
    .AddD2Auth(opts =>
    {
        opts.Issuer = new Uri("https://edge.internal");
        opts.Audience = Audiences.MyService;
    })
    .AddD2AuthGrpc();

// ... later, alongside the host's own gRPC config:
services.AddGrpc(opts =>
{
    opts.MaxReceiveMessageSize = 16 * 1024 * 1024;
});
```

`AddD2AuthGrpc()` registers the `JwtAuthInterceptor` as a singleton, attaches it to the host's `GrpcServiceOptions.Interceptors` collection, and wires a scoped `IRequestContext` resolver that reads from `HttpContext.Items` (the gRPC interceptor writes the validated context to the same shared slot the HTTP middleware uses, so the resolver lambda is identical across both transports). The interceptor ALSO writes `ServerCallContext.UserState` for the gRPC-specific hot-path accessor `ServerCallContext.GetD2RequestContext()` — useful for service code that already has a `ServerCallContext` in hand and wants to skip the `IHttpContextAccessor` allocation cost. `AddD2Auth(...)` MUST be called first — fail-fast `InvalidOperationException` otherwise. Idempotent: multiple `AddD2AuthGrpc()` calls do not double-register the interceptor.

`AddD2AuthGrpc()` does NOT call `AddGrpc()` itself — the host owns that registration so per-host gRPC settings (`MaxReceiveMessageSize`, `EnableDetailedErrors`, etc.) stay under host control.

### Composing with siblings (dual-transport host)

> See [`../core/README.md` § Composing with siblings](../core/README.md#composing-with-siblings) for the canonical dual-transport composition pattern (fluent chain, identical `IRequestContext` resolver across both transports, HTTP-only / gRPC-only carve-outs).

### Per-method scope metadata

#### Attribute path (recommended)

```csharp
[D2RequireScope("files.read")]
public sealed class FilesService : Files.FilesBase
{
    public override Task<GetFileReply> GetFile(GetFileRequest req, ServerCallContext ctx) { ... }

    [D2HarmlessEndpoint]
    public override Task<HealthReply> Health(Empty req, ServerCallContext ctx) { ... }
}
```

`D2RequireScopeAttribute` and `D2HarmlessEndpointAttribute` apply at method level OR class level. ASP.NET routing auto-pulls them onto endpoint metadata during `MapGrpcService<T>()` — no extra wiring required.

**Precedence** (mirrors BCL `[AllowAnonymous]` over `[Authorize]`):

- Method-level `[D2HarmlessEndpoint]` overrides any class-level `[D2RequireScope]`.
- Method-level `[D2RequireScope]` overrides any class-level `[D2RequireScope]`.
- Fluent metadata takes precedence over both attribute paths.

#### Fluent path

```csharp
app.MapGrpcService<FilesService>().RequireD2Scope("files.read");
app.MapGrpcService<HealthProbeService>().MarkAsD2HarmlessEndpoint();
```

Useful for tests that need to inject metadata without modifying production code, conditional registration based on feature flags, and endpoint-builder composition pipelines.

#### Deny-by-default

A gRPC method with NO `MethodScopeMetadata` / `[D2RequireScope]` / `[D2HarmlessEndpoint]` gets the FULL pipeline (validator + liveness; scope check passes against the empty required set). Methods that need to bypass auth entirely MUST opt in explicitly via `[D2HarmlessEndpoint]` — the codebase deliberately does NOT recognize the BCL `[AllowAnonymous]` attribute (its semantic is tied to the BCL `AuthorizationMiddleware` chain we bypass).

### `RpcException` shape — `D2RpcStatusExtensions`

Single emit point for every interceptor-produced auth failure. Usage:

```csharp
throw failure.ToRpcException();
```

Builds an `RpcException(Status, Trailers)`:

| Field                                                     | Source                                                                                                                                                                                                                             |
| --------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `Status.StatusCode`                                       | `D2Result.StatusCode` mapped: 401 → `Unauthenticated` (16); 503 → `Unavailable` (14); other → `Internal` (13).                                                                                                                     |
| `Status.Detail`                                           | DELIBERATELY EMPTY. Telling an attacker which validation step failed (signature vs expired vs claim missing) is an info leak; the granular `d2_error_code` trailer carries the machine-readable taxonomy for legitimate operators. |
| `Trailers[D2GrpcTrailers.ERROR_CODE]` (`"d2_error_code"`) | `D2Result.ErrorCode` (one of the `AUTH_*` constants from `D2.Shared.Auth.Errors.AuthErrorCodes`).                                                                                                                                  |
| `Trailers[D2GrpcTrailers.MESSAGES]` (`"d2_messages"`)     | `D2Result.Messages` array serialized as JSON text (TK keys + bounded params). Same wire shape as the HTTP middleware's ProblemDetails `d2_messages` extension.                                                                     |
| `Trailers[D2GrpcTrailers.TRACE_ID]` (`"traceId"`)         | `Activity.Current?.TraceId` (W3C lower-hex format, 32 chars). camelCase matches the HTTP ProblemDetails extension key `traceId`. Omitted when no Activity is on the execution context — never surfaced as null.                    |

The trailer keys are spec-driven via `contracts/grpc-trailers/grpc-trailers.spec.json` — the `D2.Shared.Grpc.Trailers.SourceGen` Roslyn generator emits `D2GrpcTrailers` into this csproj from the spec, and `tools/ts-codegen` emits the cross-language sibling `D2GrpcTrailers` into `@d2/grpc-client`. Both sides reference identical wire values byte-for-byte.

Side-effect: increments `AuthTelemetry.SR_ProblemEmitted` tagged with `d2_error_code` (single sink across HTTP + gRPC, intentionally so dashboards aggregate cleanly).

**Design rationale: status code mapping is uniform**: the interceptor maps every auth failure (including scope-insufficient) to `Unauthenticated` — never `PermissionDenied` — to avoid leaking which check failed. Same uniform-shape policy as the HTTP middleware's no-403 rule.

### `ServerCallContext.GetD2RequestContext()`

Typed accessor for the populated `IRequestContext` the interceptor writes to `ServerCallContext.UserState`. Preferred over raw key lookups; the raw key constant lives on the internal `D2GrpcUserStateKeys` class precisely so callers reach for this extension.

```csharp
public override Task<GetFileReply> GetFile(GetFileRequest req, ServerCallContext ctx)
{
    var requestContext = ctx.GetD2RequestContext();
    // requestContext is the validated IRequestContext when the interceptor ran;
    // null on harmless endpoints / pre-interceptor pipeline stages.
}
```

Or better, constructor-inject `IRequestContext` directly — the scoped adapter registered by `AddD2AuthGrpc()` resolves from the same slot.

## Footguns / common pitfalls

### Harmless-endpoint methods + ctor-injected `IRequestContext`

The scoped `IRequestContext` adapter registered by `AddD2AuthGrpc()` resolves from `HttpContext.Items[D2HttpContextItems.REQUEST_CONTEXT]` — populated by the interceptor AFTER successful auth. On methods marked `[D2HarmlessEndpoint]` (or class-level harmless-endpoint services), the interceptor SHORT-CIRCUITS the auth pipeline and never writes the slot. A gRPC service that constructor-injects `IRequestContext` then fails at resolve time with `InvalidOperationException` — see [Debugging → `IRequestContext` resolution failures](#irequestcontext-resolution-failures) for the exact message variants and root causes.

Fail-fast rationale: a sentinel "anonymous" context would let downstream code silently degrade (e.g. log `user_id=null` to ALL records, leak rows by querying with a missing tenant filter); the noisy throw forces the deployer to make an explicit choice.

**Workarounds**:

1. **Resolve lazily inside the method body** (preferred for mixed services). Inject `IServiceProvider` / `IServiceScopeFactory` instead of `IRequestContext`; resolve `IRequestContext` only inside non-harmless method bodies:

   ```csharp
   public sealed class FilesService(IServiceProvider sp) : Files.FilesBase
   {
       [D2HarmlessEndpoint]
       public override Task<HealthReply> Health(Empty req, ServerCallContext ctx)
           => Task.FromResult(new HealthReply());

       public override Task<GetFileReply> GetFile(GetFileRequest req, ServerCallContext ctx)
       {
           var requestContext = sp.GetRequiredService<IRequestContext>();
           // ... use requestContext
       }
   }
   ```

2. **Use `ServerCallContext.GetD2RequestContext()` directly** (lighter-weight). Skips the DI hop:

   ```csharp
   public override Task<GetFileReply> GetFile(GetFileRequest req, ServerCallContext ctx)
   {
       var requestContext = ctx.GetD2RequestContext();
       // requestContext is non-null on authenticated calls; null on harmless endpoints.
   }
   ```

3. **Split the service** — keep harmless-endpoint methods on a separate gRPC service class that doesn't inject `IRequestContext` at all. Cleanest for fully-harmless services like health/info endpoints.

### `[D2HarmlessEndpoint]` does NOT honor the BCL `[AllowAnonymous]`

The interceptor deliberately ignores `Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute` — its semantic is wired to the BCL `AuthorizationMiddleware` chain that the D2 auth pipeline bypasses. Use `[D2HarmlessEndpoint]` exclusively. A method decorated only with the BCL attribute will be treated as deny-by-default and the interceptor will require a valid bearer.

### Fluent metadata silently overrides attributes

`MapGrpcService<T>().RequireD2Scope("scope")` and `.MarkAsD2HarmlessEndpoint()` take precedence over BOTH method-level and class-level attribute declarations — by design (lets tests inject metadata + lets feature flags conditionally adjust scope without modifying production code). If a method appears auth-failing under unexpected scopes, audit the fluent-metadata wiring at the `MapGrpcService` site BEFORE assuming the attribute is correct.

### Streaming methods can't bypass auth — but they CAN bypass scope checks via wrong endpoint metadata

Every streaming method dispatches through the same auth pipeline as unary, but per-method metadata is matched on the gRPC method shape. A new streaming method added without explicit `[D2RequireScope]` falls back to the class-level attribute (or to deny-by-default with no scope check). Spot-check with the gRPC reflection service in development to confirm method-level metadata is wired correctly.

## Failure surface

> See [`../core/README.md` § Failure helpers — `AuthFailures`](../core/README.md#failure-helpers--authfailures) for the canonical 14-row failure-code table (single source: [`contracts/auth-error-codes/auth-error-codes.spec.json`](../../../../../contracts/auth-error-codes/auth-error-codes.spec.json)). The gRPC interceptor maps `D2Result.StatusCode` to `Status.StatusCode` per the transport rule documented at [`../core/README.md` § Failure surface — transport status mapping](../core/README.md#failure-surface--transport-status-mapping): 401 → `Unauthenticated`, 503 → `Unavailable`, other → `Internal`. NEVER `PermissionDenied` (7) — `AUTH_SCOPE_INSUFFICIENT` also maps to `Unauthenticated` so the wire never leaks which check failed.

## Bearer extraction edge cases (RFC 6750 §2.1)

> See [`../core/README.md` § Bearer extraction edge cases](../core/README.md#bearer-extraction-edge-cases-rfc-6750-21) for the canonical edge-case table. The gRPC interceptor reads from the `authorization` request metadata — identical semantics to the HTTP `Authorization` header, only header-name casing differs.

## Streaming-method coverage invariant

All four server-side handler methods — `UnaryServerHandler`, `ClientStreamingServerHandler`, `ServerStreamingServerHandler`, `DuplexStreamingServerHandler` — dispatch to a single shared validation pipeline. A streaming method added later cannot silently bypass auth.

## PII discipline

> See [`../core/README.md` § PII discipline — `SanitizedExceptionRender`](../core/README.md#pii-discipline--sanitizedexceptionrender). PII rules apply uniformly across HTTP and gRPC bindings; both reuse the auth-core lib's `AuthLog` delegates and emit closed-enumeration outcome categories only. `Status.Detail` is DELIBERATELY EMPTY on every auth failure (gRPC parity with the HTTP middleware's empty `ProblemDetails.Detail`); the granular `d2_error_code` trailer carries the machine-readable taxonomy. `d2_messages` carries TK keys + bounded params; `traceId` is a non-PII W3C trace identifier.

## Dependencies

| Package                                                                   | Why                                                                                                                               |
| ------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------- |
| `D2.Shared.Auth`                                                          | `JwtValidator` (consumed via `InternalsVisibleTo`), `AuthFailures`, `AuthErrorCodes`, `AuthLog`, `AuthTelemetry`.                 |
| `D2.Shared.Auth.Abstractions`                                             | `ISessionLivenessTracker` contract + `JwtClaimTypes`.                                                                             |
| `D2.Shared.Context.Abstractions`                                          | `IRequestContext` shape.                                                                                                          |
| `D2.Shared.Result`                                                        | `D2Result` typed factories.                                                                                                       |
| `D2.Shared.I18n.Abstractions`                                             | `TKMessage` shape.                                                                                                                |
| `D2.Shared.Utilities`                                                     | `Falsey()` / `Truthy()` extensions.                                                                                               |
| `Grpc.AspNetCore.Server`                                                  | Server-side gRPC binding (`Interceptor`, `ServerCallContext`, `Metadata`, `Status`, `RpcException`, `IServerCallContextFeature`). |
| `Microsoft.AspNetCore.App` (framework ref via `Sdk.Web`)                  | Hosts gRPC services; provides `HttpContext`, `IEndpointConventionBuilder`, `IApplicationBuilder`.                                 |
| `Microsoft.Extensions.{DependencyInjection,Logging,Options}.Abstractions` | DI / logging / options.                                                                                                           |
| `JetBrains.Annotations`                                                   | Standard annotations.                                                                                                             |

## Tests

`server/shared/dotnet/tests/Unit/Auth/Inbound/Grpc/`:

- `Interceptors/JwtAuthInterceptorTests.cs` — every `RunAuthAsync` branch across all four RPC kinds; bearer-extraction edge cases; validator failure surfaces; liveness outcomes; scope pass / fail; cancellation propagation; `UserState` contract; double-write avoidance; constructor null-guards.
- `Interceptors/D2GrpcUserStateKeysTests.cs` — slot-key constant value pinned (`"D2.RequestContext"`).
- `Interceptors/ServerCallContextRequestContextExtensionsTests.cs` — typed accessor returns null pre-interceptor / non-`IRequestContext` slot; populated value post-interceptor.
- `Endpoints/MethodScopeMetadataTests.cs` — `HarmlessEndpoint` singleton invariants; `ForScopes` deduping + frozen; record equality; zero-scope throws; **`HarmlessEndpoint` factory + `IsHarmlessEndpoint` property names pinned via reflection (literal-string lookup) to guard against type / property renames that downstream analyzers depend on.**
- `Endpoints/D2RequireScopeAttributeTests.cs` — construction (single + multiple scopes); null/whitespace argument throws; class-level vs method-level precedence.
- `Endpoints/D2HarmlessEndpointAttributeTests.cs` — construction; class-level vs method-level precedence; overrides sibling `[D2RequireScope]`; **type name `"D2HarmlessEndpointAttribute"` + full-namespace pinned via literal-string assertion to guard against renames that downstream analyzers depend on.**
- `Endpoints/RequireD2GrpcScopeExtensionsTests.cs` — fluent extensions correctly attach metadata; null/whitespace throws.
- `Status/D2RpcStatusExtensionsTests.cs` — every `AuthFailures` surface → expected `Status.StatusCode` + trailer set; `Status.Detail` empty; `traceid` presence/absence per `Activity.Current`; counter increment.
- `AuthGrpcServiceCollectionExtensionsTests.cs` — DI registration; `AddD2Auth` precondition fail-fast; idempotent re-call; interceptor type registered as singleton; appears in `GrpcServiceOptions.Interceptors` exactly once; scoped `IRequestContext` adapter resolution.

Run: `dotnet test server/shared/dotnet/tests`.

## Debugging

Auth failures terminate the gRPC call with `RpcException(Status.Unauthenticated | Status.Unavailable)` and carry the diagnostic in trailers (`Status.Detail` is intentionally empty — info-leak avoidance). Inspect via `RpcException.Trailers`:

```csharp
catch (RpcException ex)
{
    var errorCode = ex.Trailers.GetValue("d2_error_code"); // e.g. "AUTH_JWT_EXPIRED"
    var messages = ex.Trailers.GetValue("d2_messages");    // JSON array of TKMessage
    var traceId = ex.Trailers.GetValue("traceId");         // W3C trace id (lower-hex, 32 chars). camelCase — same key as the HTTP ProblemDetails extension.
}
```

From `grpcurl`, `-v` surfaces trailer metadata at the bottom of verbose output (`Code: Unauthenticated`, `Trailers received: ...`). `d2_messages` is the same JSON-array-of-TKMessage shape as the HTTP middleware's ProblemDetails extension (`[{"key":"UNAUTHORIZED","params":{}}]`); `key` is a TK constant translated client-side; `params` carries bounded scalar substitutions only (no PII). The `traceid` trailer is the lower-hex 32-char W3C trace-id of `Activity.Current` at failure time — correlate with the server-side span in your OTel backend; the `JwtAuthInterceptor` runs inside the gRPC server-call activity so its `AuthLog` delegates and `AuthTelemetry.SR_ProblemEmitted` counter sit on the same span.

For full per-code reference + remediation, see [`../core/README.md` § Debugging](../core/README.md#debugging) — the same `AUTH_*` taxonomy applies across HTTP and gRPC transports (single sink at `AuthTelemetry.SR_ProblemEmitted`). Two codes specific to gRPC bearer extraction: `AUTH_BEARER_MISSING` (no `authorization` metadata, wrong scheme, or empty after `Bearer ` — check `Metadata.Add("authorization", "Bearer " + token)` or `Grpc.Net.ClientFactory.ConfigureChannel` + `CallCredentials`); `AUTH_SCOPE_INSUFFICIENT` (bearer valid but `Scopes` set didn't overlap method's required set; `traceid` finds the matching span whose enriched logs show required-vs-presented).

### `IRequestContext` resolution failures

The cross-transport scoped `IRequestContext` resolver registered by `AddD2AuthGrpc()` (lambda byte-equivalent to `AddD2AuthHttp()`'s — both read `HttpContext.Items[D2HttpContextItems.REQUEST_CONTEXT]`) surfaces two distinct `InvalidOperationException` messages:

- **"resolved without an active HttpContext. Ensure the resolution site runs inside an AspNetCore request..."** — `IHttpContextAccessor` returned `null`. Background-service / hosted-service code that takes a constructor `IRequestContext` would surface this; resolution sites must be inside an inbound request.
- **"resolved before the auth pipeline ran. Ensure UseD2Auth() (for HTTP) or AddD2AuthGrpc()'s interceptor (for gRPC) has run..."** — `HttpContext` is present but the slot is empty. Two common causes: (a) `[D2HarmlessEndpoint]` short-circuited the interceptor — see [Footguns → Harmless-endpoint methods + ctor-injected `IRequestContext`](#harmless-endpoint-methods--ctor-injected-irequestcontext) for the three workarounds; (b) resolution site sits upstream of the interceptor (middleware on the AspNetCore pipeline above gRPC trying to constructor-inject `IRequestContext`).

`ServerCallContext.UserState` IS still written by the interceptor on successful auth (the typed accessor `ServerCallContext.GetD2RequestContext()` reads it for the gRPC-specific hot-path use case), but the cross-transport DI resolver does NOT read from `UserState` — it reads from `HttpContext.Items` so a single resolver lambda covers both transports.

## References

- [`../core/README.md`](../core/README.md) — JWT validator + JWKS + liveness internals
- [`../http/README.md`](../http/README.md) — HTTP-transport sibling
- [`../abstractions/README.md`](../abstractions/README.md) — `ISessionLivenessTracker`, `JwtClaimTypes`, `Audiences`, `Scopes`
- [`../outbound/README.md`](../outbound/README.md) — outbound service-identity bearer attachment (the gRPC counterpart on the client side)
- [RFC 6750](https://datatracker.ietf.org/doc/html/rfc6750) — Bearer Token Usage
- [gRPC status codes](https://grpc.io/docs/guides/status-codes/) — canonical status code semantics
