<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Shared.Auth.Grpc

> Parent: [`server/shared/dotnet/`](../../README.md)

gRPC-transport binding for [`D2.Shared.Auth`](../core/README.md) — server-side `Grpc.Core.Interceptors.Interceptor` subclass that runs the JWT validation pipeline + session liveness check on inbound gRPC calls, emits `RpcException(Status, Trailers)` on failure with the `d2_error_code` / `d2_messages` / `traceid` trailer triple, and enforces per-method scope requirements via attribute declarations OR fluent endpoint metadata.

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

// ... alongside the host's own gRPC config:
services.AddGrpc(opts =>
{
    opts.MaxReceiveMessageSize = 16 * 1024 * 1024;
});
```

`AddD2AuthGrpc()` registers the `JwtAuthInterceptor` as a singleton, attaches it to the host's `GrpcServiceOptions.Interceptors` collection, wires a scoped `IRequestContext` resolver that reads from `HttpContext.Items` (the gRPC interceptor writes the validated context to the same shared slot the HTTP middleware uses, so the resolver lambda is identical across both transports), registers a scoped `IForwardedJwtAccessor` holder (the same registration `AddD2AuthHttp()` makes), and registers a singleton `IAmbientRequestScopeAccessor` adapter (see below). The interceptor ALSO writes `ServerCallContext.UserState` for the gRPC-specific hot-path accessor `ServerCallContext.GetD2RequestContext()` — useful for service code that already has a `ServerCallContext` in hand and wants to skip the `IHttpContextAccessor` allocation cost. `AddD2Auth(...)` MUST be called first — fail-fast `InvalidOperationException` otherwise. Idempotent: multiple `AddD2AuthGrpc()` calls do not double-register the interceptor.

Alongside its `IRequestContext` dual-write, the interceptor captures the validated raw bearer into the request-scoped `IForwardedJwtAccessor` (the [redacting forwarded-JWT holder](../abstractions/README.md#forwarded-jwt-credential--forwardedjwt--iforwardedjwtaccessor)) after validation success — so an outbound hop can replay it byte-for-byte ([ADR-0022 §Realization](../../../../../docs/adrs/0022-service-auth-mint-once-forward.md)). Capture is best-effort: a host that does not register the holder no-ops; only a validator-accepted token is ever captured (the harmless / bearer-missing / validation-failure paths leave the holder unset). The raw bearer is never logged.

### Ambient-scope adapter — `GrpcHttpContextAmbientRequestScopeAccessor`

The read-back door for the forwarded-JWT holder on a gRPC-inbound forwarding host — the gRPC-inbound sibling of `D2.Shared.Auth.Http`'s `HttpContextAmbientRequestScopeAccessor`. The outbound forwarding credential ([`ForwardedJwtCallCredentials`](../outbound/README.md#forwarded-transaction-token--per-request-callcredentials-the-forward-unchanged-rail-of-adr-0022) in `D2.Shared.Auth.Outbound`) must reach the _current_ call's request-scoped holder on each outbound RPC, but a gRPC `CallCredentials` runs outside the DI request-scope ambient flow. It depends on the framework-free `IAmbientRequestScopeAccessor` port (declared in `D2.Shared.Auth.Abstractions`, referenced by both transport libs and the outbound lib) rather than on `IHttpContextAccessor` directly — so the outbound lib stays free of any AspNetCore framework reference. gRPC services are hosted inside Kestrel (`Grpc.AspNetCore.Server`), so each call has a per-call `HttpContext` set on the same `AsyncLocal<>` seam the HTTP pipeline uses; `GrpcHttpContextAmbientRequestScopeAccessor` reads `IHttpContextAccessor.HttpContext?.RequestServices` exactly as the HTTP sibling does, and the interceptor's capture writes the holder into that same scope. `AddD2AuthGrpc()` registers it as a singleton (stateless — per-call state flows through the AsyncLocal accessor), symmetric to where it registers the holder write-side, so a gRPC-inbound forwarding host self-wires the read-back door without `AddD2AuthHttp()`.

This is a deliberate tiny duplicate of the HTTP adapter, not a shared type: the two transport-binding libs are siblings with no inter-csproj dependency, so a single shared adapter would force either a forbidden inter-lib edge or a new shared lib for one trivial property. Both adapters implement the same `D2.Shared.Auth.Abstractions` port and read the same `IHttpContextAccessor` seam, so a dual-transport host (HTTP + gRPC on one Kestrel) sees identical behavior regardless of which transport's `TryAddSingleton` wins (first-wins is harmless).

`AddD2AuthGrpc()` does NOT call `AddGrpc()` itself — the host owns that registration so per-host gRPC settings (`MaxReceiveMessageSize`, `EnableDetailedErrors`, etc.) stay under host control.

### Composing with siblings (dual-transport host)

> See [`../core/README.md` § Composing with siblings](../core/README.md#composing-with-siblings) for the canonical dual-transport composition pattern (fluent chain, identical `IRequestContext` resolver across both transports, HTTP-only / gRPC-only carve-outs).

### Per-method scope declaration

Two surfaces declare scope requirements on a gRPC method: the **attribute path** (on the service class or method) and the **fluent path** (on the `IEndpointConventionBuilder` returned by `MapGrpcService<T>()`). Both project onto endpoint metadata and are enforced at runtime by the `JwtAuthInterceptor`.

#### Attribute path (recommended for gRPC)

The recommended surface because gRPC service implementations are concrete classes overriding generated `*ServiceBase` types — declaring scope requirements at the class or method declaration is the most ergonomic.

```csharp
[D2RequireAnyScope("files.read")]
public sealed class FilesService : Files.FilesBase
{
    // Inherits class-level any-scope requirement.
    public override Task<GetFileReply> GetFile(GetFileRequest req, ServerCallContext ctx) { ... }

    // Method-level all-scopes overrides class-level any-scope.
    [D2RequireAllScopes("files.read", "files.write")]
    public override Task<DeleteFileReply> DeleteFile(DeleteFileRequest req, ServerCallContext ctx) { ... }

    // Method-level harmless overrides class-level any-scope.
    [D2HarmlessEndpoint]
    public override Task<HealthReply> Health(Empty req, ServerCallContext ctx) { ... }
}
```

ASP.NET routing auto-pulls class-level and method-level attribute declarations onto endpoint metadata during `MapGrpcService<T>()` — no extra wiring required.

| Attribute | Semantics |
| --- | --- |
| `[D2RequireAnyScope("a", "b")]` | Caller must hold **at least one** of the listed scopes (`ScopeMatch.Any`). |
| `[D2RequireAllScopes("a", "b")]` | Caller must hold **every** listed scope (`ScopeMatch.All`). |
| `[D2HarmlessEndpoint]` | Method bypasses auth entirely (probes / OIDC discovery / harmless intra-cluster info only). SECURITY-CRITICAL — see [Harmless endpoints](#harmless-endpoints). |

#### Fluent path

```csharp
app.MapGrpcService<FilesService>().RequireAnyScope("files.read");
app.MapGrpcService<AdminService>().RequireAllScopes("admin.read", "admin.write");
app.MapGrpcService<HealthProbeService>().MarkAsD2HarmlessEndpoint();
```

The fluent path covers cases where attribute attachment is unwanted: tests that need to inject metadata without modifying production code, conditional registration based on feature flags, endpoint-builder composition pipelines. It attaches `MethodScopeMetadata` directly onto the endpoint builder.

| Extension | Semantics |
| --- | --- |
| `.RequireAnyScope("scope")` | Any-of — caller holds at least one of the listed scopes. |
| `.RequireAllScopes("scope", "other")` | All-of — caller holds every listed scope. |
| `.MarkAsD2HarmlessEndpoint()` | Bypasses auth entirely. SECURITY-CRITICAL — see [Harmless endpoints](#harmless-endpoints). |

#### Precedence

The interceptor resolves the effective scope declaration from the endpoint's metadata collection using this priority order (highest first):

1. **Fluent `MethodScopeMetadata`** — wins over all attribute paths. Added via `RequireAnyScope` / `RequireAllScopes` / `MarkAsD2HarmlessEndpoint` on the builder.
2. **Attribute path (last-declared wins)** — ASP.NET routing appends class-level attributes before method-level ones, so a method-level attribute is always last in collection order. Among the three attribute types (`[D2RequireAnyScope]`, `[D2RequireAllScopes]`, `[D2HarmlessEndpoint]`), the one with the highest metadata-collection index is the effective declaration. This means:
   - Class-level `[D2RequireAnyScope]` + method-level `[D2HarmlessEndpoint]` → harmless wins (method-level is last).
   - Class-level `[D2HarmlessEndpoint]` + method-level `[D2RequireAllScopes]` → all-scopes wins (method-level is last).
   - Class-level `[D2RequireAnyScope]` + method-level `[D2RequireAllScopes]` → all-scopes wins (method-level is last).
3. **No declaration** — deny-by-default: the interceptor runs the full pipeline (validator + liveness); the scope check passes for any authenticated caller (empty required set). See [Deny-by-default boot guard](#deny-by-default-boot-guard).

#### Deny-by-default boot guard

The `AuthEndpointGuardStartupFilter` (wired by `D2.Shared.Auth.Startup` — see [`../startup/README.md`](../startup/README.md)) requires **every** mapped `RouteEndpoint` to carry a declared auth intent (fluent or attribute). If any gRPC method lacks a declaration, the host fails to start with an `InvalidOperationException` listing the undeclared routes — before serving any traffic. Declare intent on every method or the host won't start.

#### Harmless endpoints

`[D2HarmlessEndpoint]` / `MarkAsD2HarmlessEndpoint()` cause the interceptor to skip the entire JWT validation pipeline (signature + claims + session liveness + scope check). Legitimate use cases only — exhaustive enumeration:

- Kubernetes / Docker liveness and readiness probes returning a fixed-shape healthy/unhealthy response with no request-derived data.
- Intra-cluster health or info endpoints returning only closed-enumeration constants (status strings, version identifiers, build metadata) — never user data or any field an operator would consider sensitive.
- OIDC discovery endpoints (Edge service only) — `/.well-known/openid-configuration` and `/.well-known/jwks.json`.

**Any other data exposure via this surface is a security bug.** For endpoints reachable without an existing user session (sign-in / password-reset / public lookups), declare an anon-scope-required endpoint instead — that path still flows through the full validator + scope check.

The deliberately unusual name forces code reviewers to pause and ask "is this endpoint actually harmless?" — the friction is intentional.

### `RpcException` shape — `D2RpcStatusExtensions`

Single emit point for every interceptor-produced auth failure. Usage:

```csharp
throw failure.ToRpcException();
```

Builds an `RpcException(Status, Trailers)`:

| Field | Source |
| --- | --- |
| `Status.StatusCode` | `D2Result.StatusCode` mapped: 401 → `Unauthenticated` (16); 503 → `Unavailable` (14); other → `Internal` (13). |
| `Status.Detail` | DELIBERATELY EMPTY. Telling an attacker which validation step failed (signature vs expired vs claim missing) is an info leak; the granular `d2_error_code` trailer carries the machine-readable taxonomy for legitimate operators. |
| `Trailers[D2GrpcTrailers.ERROR_CODE]` (`"d2_error_code"`) | `D2Result.ErrorCode` (one of the `AUTH_*` constants from `D2.Shared.Auth.Errors.AuthErrorCodes`). |
| `Trailers[D2GrpcTrailers.MESSAGES]` (`"d2_messages"`) | `D2Result.Messages` array serialized as JSON text (TK keys + bounded params). Same wire shape as the HTTP middleware's ProblemDetails `d2_messages` extension. |
| `Trailers[D2GrpcTrailers.TRACE_ID]` (`"traceId"`) | `Activity.Current?.TraceId` (W3C lower-hex format, 32 chars). camelCase matches the HTTP ProblemDetails extension key `traceId`. Omitted when no Activity is on the execution context — never surfaced as null. |

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

`MapGrpcService<T>().RequireAnyScope("scope")` and `.MarkAsD2HarmlessEndpoint()` take precedence over BOTH method-level and class-level attribute declarations — by design (lets tests inject metadata + lets feature flags conditionally adjust scope without modifying production code). If a method appears auth-failing under unexpected scopes, audit the fluent-metadata wiring at the `MapGrpcService` site BEFORE assuming the attribute is correct.

### Streaming methods can't bypass auth — but they CAN bypass scope checks via wrong endpoint metadata

Every streaming method dispatches through the same auth pipeline as unary, but per-method metadata is matched on the gRPC method shape. A new streaming method added without explicit scope declaration falls back to the class-level attribute (or to deny-by-default with no scope check). Spot-check with the gRPC reflection service in development to confirm method-level metadata is wired correctly.

## Failure surface

> See [`../core/README.md` § Failure helpers — `AuthFailures`](../core/README.md#failure-helpers--authfailures) for the canonical 14-row failure-code table (single source: [`contracts/auth-error-codes/auth-error-codes.spec.json`](../../../../../contracts/auth-error-codes/auth-error-codes.spec.json)). The gRPC interceptor maps `D2Result.StatusCode` to `Status.StatusCode` per the transport rule documented at [`../core/README.md` § Failure surface — transport status mapping](../core/README.md#failure-surface--transport-status-mapping): 401 → `Unauthenticated`, 503 → `Unavailable`, other → `Internal`. NEVER `PermissionDenied` (7) — `AUTH_SCOPE_INSUFFICIENT` also maps to `Unauthenticated` so the wire never leaks which check failed.

## Bearer extraction edge cases (RFC 6750 §2.1)

> See [`../core/README.md` § Bearer extraction edge cases](../core/README.md#bearer-extraction-edge-cases-rfc-6750-21) for the canonical edge-case table. The gRPC interceptor reads from the `authorization` request metadata — identical semantics to the HTTP `Authorization` header, only header-name casing differs.

## Streaming-method coverage invariant

All four server-side handler methods — `UnaryServerHandler`, `ClientStreamingServerHandler`, `ServerStreamingServerHandler`, `DuplexStreamingServerHandler` — dispatch to a single shared validation pipeline. A streaming method added later cannot silently bypass auth.

## PII discipline

> See [`../core/README.md` § PII discipline — `SanitizedExceptionRender`](../core/README.md#pii-discipline--sanitizedexceptionrender). PII rules apply uniformly across HTTP and gRPC bindings; both reuse the auth-core lib's `AuthLog` delegates and emit closed-enumeration outcome categories only. `Status.Detail` is DELIBERATELY EMPTY on every auth failure (gRPC parity with the HTTP middleware's empty `ProblemDetails.Detail`); the granular `d2_error_code` trailer carries the machine-readable taxonomy. `d2_messages` carries TK keys + bounded params; `traceId` is a non-PII W3C trace identifier.

## Dependencies

| Package | Why |
| --- | --- |
| `D2.Shared.Auth` | `JwtValidator` (consumed via `InternalsVisibleTo`), `AuthFailures`, `AuthErrorCodes`, `AuthLog`, `AuthTelemetry`. |
| `D2.Shared.Auth.Abstractions` | `ISessionLivenessTracker` contract + `JwtClaimTypes` + `IForwardedJwtAccessor` holder + `IAmbientRequestScopeAccessor` port — this lib hosts the `IHttpContextAccessor`-backed gRPC-side adapter for it so the outbound lib stays framework-free. |
| `D2.Shared.Context.Abstractions` | `IRequestContext` shape. |
| `D2.Shared.Result` | `D2Result` typed factories. |
| `D2.Shared.I18n.Abstractions` | `TKMessage` shape. |
| `D2.Shared.Utilities` | `Falsey()` / `Truthy()` extensions. |
| `Grpc.AspNetCore.Server` | Server-side gRPC binding (`Interceptor`, `ServerCallContext`, `Metadata`, `Status`, `RpcException`, `IServerCallContextFeature`). |
| `Microsoft.AspNetCore.App` (framework ref via `Sdk.Web`) | Hosts gRPC services; provides `HttpContext`, `IEndpointConventionBuilder`, `IApplicationBuilder`. |
| `Microsoft.Extensions.{DependencyInjection,Logging,Options}.Abstractions` | DI / logging / options. |
| `JetBrains.Annotations` | Standard annotations. |

## Tests

`server/shared/dotnet/tests/Unit/Auth/Inbound/Grpc/`:

- `Interceptors/JwtAuthInterceptorTests.cs` — every `RunAuthAsync` branch across all four RPC kinds; bearer-extraction edge cases; validator failure surfaces; liveness outcomes; scope pass / fail; cancellation propagation; `UserState` contract; double-write avoidance; constructor null-guards.
- `Interceptors/D2GrpcUserStateKeysTests.cs` — slot-key constant value pinned (`"D2.RequestContext"`).
- `Interceptors/ServerCallContextRequestContextExtensionsTests.cs` — typed accessor returns null pre-interceptor / non-`IRequestContext` slot; populated value post-interceptor.
- `Endpoints/MethodScopeMetadataTests.cs` — `HarmlessEndpoint` singleton invariants; `ForScopes` deduping + frozen; record equality; zero-scope throws; **`HarmlessEndpoint` factory + `IsHarmlessEndpoint` property names pinned via reflection (literal-string lookup) to guard against type / property renames that downstream analyzers depend on.**
- `Endpoints/D2RequireAnyScopeAttributeTests.cs` — construction (single + multiple scopes); null/whitespace argument throws; class-level vs method-level precedence; any-of semantics.
- `Endpoints/D2RequireAllScopesAttributeTests.cs` — construction (single + multiple scopes); null/whitespace argument throws; all-of semantics; class-level vs method-level precedence.
- `Endpoints/D2HarmlessEndpointAttributeTests.cs` — construction; class-level vs method-level precedence; overrides sibling scope attributes; **type name `"D2HarmlessEndpointAttribute"` + full-namespace pinned via literal-string assertion to guard against renames that downstream analyzers depend on.**
- `Endpoints/RequireD2GrpcScopeExtensionsTests.cs` — fluent extensions correctly attach `MethodScopeMetadata`; any-of vs all-of mode; null/whitespace throws.
- `Status/D2RpcStatusExtensionsTests.cs` — every `AuthFailures` surface → expected `Status.StatusCode` + trailer set; `Status.Detail` empty; `traceid` presence/absence per `Activity.Current`; counter increment.
- `AuthGrpcServiceCollectionExtensionsTests.cs` — DI registration; `AddD2Auth` precondition fail-fast; idempotent re-call; interceptor type registered as singleton; appears in `GrpcServiceOptions.Interceptors` exactly once; scoped `IRequestContext` adapter resolution; `IAmbientRequestScopeAccessor` port resolves (not descriptor-presence) to `GrpcHttpContextAmbientRequestScopeAccessor` as a singleton that reads the call's request scope; **dual-transport (`AddD2AuthHttp()` + `AddD2AuthGrpc()`) first-wins in both registration orders — a single ambient-port registration that resolves and reads the scope under either order.**
- `Ambient/GrpcHttpContextAmbientRequestScopeAccessorTests.cs` — the gRPC-inbound adapter directly: present-context (`Current` is the request scope), absent-context (null), ambient-swap on a shared singleton, and the null-ctor-arg parity decision (constructs but NREs on first read, mirroring the HTTP sibling's no-explicit-guard posture).
- `GrpcInboundForwardingIntegrationTests.cs` — real-`TestServer` gRPC host wired with `AddD2AuthGrpc()` ONLY: inbound bearer → interceptor capture → adapter read-back → verbatim outbound forward (the outbound credential's interceptor invoked directly inside the call, asserting the attached `Authorization` equals `Bearer <inbound token>`); harmless endpoint short-circuits capture so the outbound credential hard-fails `Unauthenticated`; a root-provider (no inbound call) resolution hard-fails `Unauthenticated`.

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

For full per-code reference + remediation, see [`../core/README.md` § Debugging](../core/README.md#debugging) — the same `AUTH_*` taxonomy applies across HTTP and gRPC transports (single sink at `AuthTelemetry.SR_ProblemEmitted`). Two codes specific to gRPC bearer extraction: `AUTH_BEARER_MISSING` (no `authorization` metadata, wrong scheme, or empty after `Bearer ` — check `Metadata.Add("authorization", "Bearer " + token)` or `Grpc.Net.ClientFactory.ConfigureChannel` + `CallCredentials`); `AUTH_SCOPE_INSUFFICIENT` (bearer valid but `Scopes` set didn't satisfy method's scope requirement; `traceid` finds the matching span whose enriched logs show required-vs-presented).

### `IRequestContext` resolution failures

The cross-transport scoped `IRequestContext` resolver registered by `AddD2AuthGrpc()` (lambda byte-equivalent to `AddD2AuthHttp()`'s — both read `HttpContext.Items[D2HttpContextItems.REQUEST_CONTEXT]`) surfaces two distinct `InvalidOperationException` messages:

- **"resolved without an active HttpContext. Ensure the resolution site runs inside an AspNetCore request..."** — `IHttpContextAccessor` returned `null`. Background-service / hosted-service code that takes a constructor `IRequestContext` would surface this; resolution sites must be inside an inbound request.
- **"resolved before the auth pipeline ran. Ensure UseD2Auth() (for HTTP) or AddD2AuthGrpc()'s interceptor (for gRPC) has run..."** — `HttpContext` is present but the slot is empty. Two common causes: (a) `[D2HarmlessEndpoint]` short-circuited the interceptor — see [Footguns → Harmless-endpoint methods + ctor-injected `IRequestContext`](#harmless-endpoint-methods--ctor-injected-irequestcontext) for the three workarounds; (b) resolution site sits upstream of the interceptor (middleware on the AspNetCore pipeline above gRPC trying to constructor-inject `IRequestContext`).

`ServerCallContext.UserState` IS still written by the interceptor on successful auth (the typed accessor `ServerCallContext.GetD2RequestContext()` reads it for the gRPC-specific hot-path use case), but the cross-transport DI resolver does NOT read from `UserState` — it reads from `HttpContext.Items` so a single resolver lambda covers both transports.

## References

- [`../core/README.md`](../core/README.md) — JWT validator + JWKS + liveness internals
- [`../http/README.md`](../http/README.md) — HTTP-transport sibling
- [`../startup/README.md`](../startup/README.md) — deny-by-default boot guard
- [`../abstractions/README.md`](../abstractions/README.md) — `ISessionLivenessTracker`, `JwtClaimTypes`, `Audiences`, `Scopes`
- [`../outbound/README.md`](../outbound/README.md) — the outbound auth lib. Cross-process gRPC workload identity is mTLS ([ADR-0023](../../../../../docs/adrs/0023-mtls-workload-identity.md)) — a verified client certificate on a mutually-authenticated channel — and the bearer a gRPC client carries downstream is the single Edge-minted token forwarded unchanged ([ADR-0022](../../../../../docs/adrs/0022-service-auth-mint-once-forward.md)); the service-identity bearer attachment that lib documents is superseded.
- [`../../result/grpc/README.md`](../../result/grpc/README.md) — business-result `D2ResultProto` envelope codec (`ToProto` / `HandleAsync`). This lib (`D2.Shared.Auth.Grpc`) handles the auth/transport-reject path (`RpcException` + `D2GrpcTrailers`); `D2.Shared.Result.Grpc` handles the business-result path. The two are structurally separate — a `401` from the JWT interceptor never becomes a `D2ResultProto` envelope, and a `404` from a handler never becomes an `RpcException`.
- [RFC 6750](https://datatracker.ietf.org/doc/html/rfc6750) — Bearer Token Usage
- [gRPC status codes](https://grpc.io/docs/guides/status-codes/) — canonical status code semantics
