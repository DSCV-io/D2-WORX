<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Shared.Auth.Grpc

> Parent: [`server/shared/dotnet/`](../README.md)

gRPC-transport binding for [`D2.Shared.Auth`](../auth/README.md) — server-side `Grpc.Core.Interceptors.Interceptor` subclass that runs the JWT validation pipeline + session liveness check on inbound gRPC calls, emits `RpcException(Status, Trailers)` on failure with the `d2_error_code` / `d2_messages` / `traceid` trailer triple, and supports per-method scope requirements via attribute-on-method declarations OR fluent endpoint metadata.

Lives in its own csproj (separate from `D2.Shared.Auth`) so the `Grpc.AspNetCore.Server` framework reference is opt-in: HTTP-only services / worker processes / console hosts that consume `D2.Shared.Auth` for JWT validation in non-gRPC paths don't need to drag in the gRPC server framework. Sibling [`D2.Shared.Auth.Http`](../auth-http/README.md) holds the HTTP-transport binding under the same logic. The two transport-binding csprojs are siblings (no inter-csproj dep): each registers an identical scoped `IRequestContext` resolver lambda that reads from a shared `HttpContext.Items` slot, so a single dual-transport host wires both extensions and resolves `IRequestContext` correctly under either transport.

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

A host that serves both HTTP endpoints and gRPC services on the same AspNetCore Kestrel host wires all three extensions in a fluent chain:

```csharp
builder.Services
    .AddD2Auth(opts =>
    {
        opts.Issuer = new Uri("https://edge.internal");
        opts.Audience = Audiences.MyService;
    })
    .AddD2AuthHttp()
    .AddD2AuthGrpc();

builder.Services.AddGrpc(o => o.MaxReceiveMessageSize = 16 * 1024 * 1024);

builder.Services.AddOpenTelemetry()
    .WithTracing(t => t.AddSource(AuthTelemetry.ACTIVITY_SOURCE_NAME))
    .WithMetrics(m => m.AddMeter(AuthTelemetry.METER_NAME));

var app = builder.Build();
app.UseRouting();
app.UseD2Auth();              // HTTP middleware
app.MapGet("/files/{id}", H).RequireD2Scope("files.read");
app.MapGrpcService<MyGrpcService>();   // interceptor handles gRPC auth
app.Run();
```

Both transport extensions register an IDENTICAL scoped `IRequestContext` resolver lambda reading from the same `HttpContext.Items` slot. Constructor-injecting `IRequestContext` works correctly under either transport. Registration order does not matter: `TryAddScoped` first-wins is harmless because the lambdas behave identically given the same `HttpContext` state.

For HTTP-only or gRPC-only hosts, omit the unused `AddD2AuthXxx()` call — each transport extension is opt-in via the host's csproj `<PackageReference>` chain.

### Per-method scope metadata

#### Attribute path (recommended)

```csharp
[D2RequireScope("files.read")]
public sealed class FilesService : Files.FilesBase
{
    public override Task<GetFileReply> GetFile(GetFileRequest req, ServerCallContext ctx) { ... }

    [D2AllowAnonymous]
    public override Task<HealthReply> Health(Empty req, ServerCallContext ctx) { ... }
}
```

`D2RequireScopeAttribute` and `D2AllowAnonymousAttribute` apply at method level OR class level. ASP.NET routing auto-pulls them onto endpoint metadata during `MapGrpcService<T>()` — no extra wiring required.

**Precedence** (mirrors BCL `[AllowAnonymous]` over `[Authorize]`):
- Method-level `[D2AllowAnonymous]` overrides any class-level `[D2RequireScope]`.
- Method-level `[D2RequireScope]` overrides any class-level `[D2RequireScope]`.
- Fluent metadata (see below) takes precedence over both attribute paths.

#### Fluent path

```csharp
app.MapGrpcService<FilesService>().RequireD2Scope("files.read");
app.MapGrpcService<PublicLookupService>().AllowD2Anonymous();
```

Useful for tests that need to inject metadata without modifying production code, conditional registration based on feature flags, and endpoint-builder composition pipelines.

#### Deny-by-default

A gRPC method with NO `MethodScopeMetadata` / `[D2RequireScope]` / `[D2AllowAnonymous]` gets the FULL pipeline (validator + liveness; scope check passes against the empty required set). Methods that need anonymous access MUST opt in explicitly — the codebase deliberately does NOT recognize the BCL `[AllowAnonymous]` attribute (its semantic is tied to the BCL `AuthorizationMiddleware` chain we bypass).

### `RpcException` shape — `D2RpcStatusExtensions`

Single emit point for every interceptor-produced auth failure. Usage:

```csharp
throw failure.ToRpcException();
```

Builds an `RpcException(Status, Trailers)`:

| Field | Source |
|---|---|
| `Status.StatusCode` | `D2Result.StatusCode` mapped: 401 → `Unauthenticated` (16); 503 → `Unavailable` (14); other → `Internal` (13). |
| `Status.Detail` | DELIBERATELY EMPTY. Telling an attacker which validation step failed (signature vs expired vs claim missing) is an info leak; the granular `d2_error_code` trailer carries the machine-readable taxonomy for legitimate operators. |
| `Trailers["d2_error_code"]` | `D2Result.ErrorCode` (one of the `AUTH_*` constants from `D2.Shared.Auth.Errors.AuthErrorCodes`). |
| `Trailers["d2_messages"]` | `D2Result.Messages` array serialized as JSON text (TK keys + bounded params). Same wire shape as the HTTP middleware's ProblemDetails `d2_messages` extension. |
| `Trailers["traceid"]` | `Activity.Current?.TraceId` (W3C lower-hex format). Omitted when no Activity is on the execution context — never surfaced as null. |

Side-effect: increments `AuthTelemetry.ProblemEmitted` tagged with `d2_error_code` (single sink across HTTP + gRPC, intentionally so dashboards aggregate cleanly).

**Why no `PermissionDenied` (gRPC code 7)**: the interceptor maps every auth failure (including scope-insufficient) to `Unauthenticated` — never `PermissionDenied` — to avoid leaking which check failed. Same uniform-shape policy as the HTTP middleware's no-403 rule.

### `ServerCallContext.GetD2RequestContext()`

Typed accessor for the populated `IRequestContext` the interceptor writes to `ServerCallContext.UserState`. Preferred over raw key lookups; the raw key constant lives on the internal `D2GrpcUserStateKeys` class precisely so callers reach for this extension.

```csharp
public override Task<GetFileReply> GetFile(GetFileRequest req, ServerCallContext ctx)
{
    var requestContext = ctx.GetD2RequestContext();
    // requestContext is the validated IRequestContext when the interceptor ran;
    // null on anonymous methods / pre-interceptor pipeline stages.
}
```

Or better, constructor-inject `IRequestContext` directly — the scoped adapter registered by `AddD2AuthGrpc()` resolves from the same slot.

## Footguns / common pitfalls

### Anonymous methods + ctor-injected `IRequestContext`

The scoped `IRequestContext` adapter registered by `AddD2AuthGrpc()` resolves from `HttpContext.Items[D2HttpContextItems.REQUEST_CONTEXT]` — populated by the interceptor AFTER successful auth (the same write also lands on `ServerCallContext.UserState` for the gRPC-specific hot-path accessor `ServerCallContext.GetD2RequestContext()`, but the cross-transport DI resolver reads from the `HttpContext.Items` slot, NOT `UserState` — that's how a single resolver lambda works identically under HTTP and gRPC). On methods marked `[D2AllowAnonymous]` (or class-level anon services), the interceptor SHORT-CIRCUITS the auth pipeline and never writes to either slot. A gRPC service that constructor-injects `IRequestContext` will then fail at resolve time with:

```text
InvalidOperationException: IRequestContext was resolved before the auth
pipeline ran. Ensure UseD2Auth() (for HTTP) or AddD2AuthGrpc()'s
interceptor (for gRPC) has run before resolving IRequestContext.
```

(If the resolution site somehow runs without ANY active `HttpContext`, the resolver throws a different message: `"IRequestContext was resolved without an active HttpContext. Ensure the resolution site runs inside an AspNetCore request (UseD2Auth() for HTTP; AddD2AuthGrpc() for gRPC) and that an HttpContext is on the execution context."` This is the "no `HttpContext` on the execution context" surface — distinct from the "`HttpContext` exists but the auth pipeline didn't run yet" surface above.)

Why fail-fast: returning a sentinel "anonymous" context would let downstream code silently degrade (e.g. log "user_id=null" to ALL records, leak rows by querying with a missing tenant filter); a noisy throw at registration time forces the deployer to make an explicit choice.

**Workarounds**:

1. **Resolve lazily inside the method body** (preferred for mixed services). Inject `IServiceProvider` / `IServiceScopeFactory` instead of `IRequestContext`; resolve `IRequestContext` only inside non-anonymous method bodies:

   ```csharp
   public sealed class FilesService(IServiceProvider sp) : Files.FilesBase
   {
       [D2AllowAnonymous]
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
       // requestContext is non-null on authenticated calls; null on anonymous.
   }
   ```

3. **Split the service** — keep anonymous methods on a separate gRPC service class that doesn't inject `IRequestContext` at all. Cleanest for fully-anonymous services like health/info endpoints.

### `[D2AllowAnonymous]` does NOT honor the BCL `[AllowAnonymous]`

The interceptor deliberately ignores `Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute` — its semantic is wired to the BCL `AuthorizationMiddleware` chain that the D2 auth pipeline bypasses. Use `[D2AllowAnonymous]` exclusively. A method decorated only with the BCL attribute will be treated as deny-by-default and the interceptor will require a valid bearer.

### Fluent metadata silently overrides attributes

`MapGrpcService<T>().RequireD2Scope("scope")` and `.AllowD2Anonymous()` take precedence over BOTH method-level and class-level attribute declarations — by design (lets tests inject metadata + lets feature flags conditionally adjust scope without modifying production code). If a method appears auth-failing under unexpected scopes, audit the fluent-metadata wiring at the `MapGrpcService` site BEFORE assuming the attribute is correct.

### Streaming methods can't bypass auth — but they CAN bypass scope checks via wrong endpoint metadata

Every streaming method dispatches through the same auth pipeline as unary, but per-method metadata is matched on the gRPC method shape. A new streaming method added without explicit `[D2RequireScope]` falls back to the class-level attribute (or to deny-by-default with no scope check). Spot-check with the gRPC reflection service in development to confirm method-level metadata is wired correctly.

## Failure surface

Every failure in this lib's interceptor terminates the call with an `RpcException` carrying `Status.Unauthenticated` or `Status.Unavailable` (NEVER `PermissionDenied` — see [`AuthErrorCodes.AUTH_SCOPE_INSUFFICIENT`](../auth/Errors/AuthErrorCodes.cs) remarks for the rationale).

| `d2_error_code` | gRPC `Status` | Trigger |
|---|---|---|
| `AUTH_BEARER_MISSING` | `Unauthenticated` | No `authorization` metadata / non-`Bearer ` prefix / empty token after prefix. |
| `AUTH_BEARER_MALFORMED` | `Unauthenticated` | Metadata present but token is not a parseable JWT. |
| `AUTH_JWT_SIGNATURE_INVALID` | `Unauthenticated` | Signature verification failed. |
| `AUTH_JWT_EXPIRED` | `Unauthenticated` | `exp` in the past, beyond clock skew. |
| `AUTH_JWT_NOT_YET_VALID` | `Unauthenticated` | `nbf` in the future, beyond clock skew. |
| `AUTH_JWT_ISSUER_MISMATCH` | `Unauthenticated` | `iss` mismatch. |
| `AUTH_JWT_AUDIENCE_MISMATCH` | `Unauthenticated` | `aud` mismatch. |
| `AUTH_JWT_CLAIM_MISSING` | `Unauthenticated` | Required claim absent (e.g. `d2_session_id` when `RequireSessionIdClaim`). |
| `AUTH_JWT_KID_NOT_FOUND` | `Unauthenticated` | Unknown `kid` after one reactive JWKS refresh. |
| `AUTH_SESSION_REVOKED` | `Unauthenticated` | Session liveness check returned revoked. |
| `AUTH_SCOPE_INSUFFICIENT` | `Unauthenticated` | Caller has no scopes overlapping the method's required set. |
| `AUTH_JWKS_UNAVAILABLE` | `Unavailable` | Upstream OIDC issuer unreachable; no cached snapshot. |
| `AUTH_SESSION_LIVENESS_UNAVAILABLE` | `Unavailable` | Liveness store unreachable; fail-closed. |

## Bearer extraction edge cases (RFC 6750 §2.1)

| Input | Result |
|---|---|
| `authorization` metadata absent | `BearerMissing` |
| `authorization: Basic foo` | `BearerMissing` (wrong scheme) |
| `authorization: bearer eyJ...` | OK (case-insensitive prefix match) |
| `authorization: BEARER eyJ...` | OK |
| `authorization: Bearer ` (empty after prefix) | `BearerMissing` (semantically nothing to validate) |
| `authorization: Bearer not.a.jwt.too.many.parts` | Validator returns `BearerMalformed` |
| Multiple `authorization` entries | First wins (parity with HTTP middleware) |
| Whitespace inside token | NOT trimmed — passed through verbatim; validator rejects. Trimming would mask client bugs. |

## Streaming-method coverage invariant

All four server-side handler methods — `UnaryServerHandler`, `ClientStreamingServerHandler`, `ServerStreamingServerHandler`, `DuplexStreamingServerHandler` — dispatch to a single shared validation pipeline. A streaming method added later cannot silently bypass auth.

## PII discipline

Bearer bytes, claim values, and scope strings NEVER reach logs / span tags / metric tags / trailer fields / exception interpolations. The interceptor reuses the auth-core lib's `AuthLog` delegates:

- `BearerHeaderMissing` — boolean fact, no header value.
- `ScopeRequirementUnmet(string requiredScopesSummary)` — closed-enumeration summary (`"N scopes required, first=files.read"`), NOT the full scope set verbatim.
- `LivenessRevoked` — no parameters; sessionId is PII, never logged.

`Status.Detail` is empty (info-leak avoidance); `d2_error_code` carries closed-enum constants only; `d2_messages` carries TK keys + bounded params (auth surface uses `UNAUTHORIZED` / `TEMPORARILY_UNAVAILABLE` keys with no params today); `traceid` is a non-PII W3C trace identifier.

## Dependencies

| Package | Why |
|---|---|
| `D2.Shared.Auth` | `JwtValidator` (consumed via `InternalsVisibleTo`), `AuthFailures`, `AuthErrorCodes`, `AuthLog`, `AuthTelemetry`. |
| `D2.Shared.Auth.Abstractions` | `ISessionLivenessTracker` contract + `JwtClaimTypes`. |
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
- `Endpoints/MethodScopeMetadataTests.cs` — `Anonymous` singleton invariants; `ForScopes` deduping + frozen; record equality; zero-scope throws.
- `Endpoints/D2RequireScopeAttributeTests.cs` — construction (single + multiple scopes); null/whitespace argument throws; class-level vs method-level precedence.
- `Endpoints/D2AllowAnonymousAttributeTests.cs` — construction; class-level vs method-level precedence; overrides sibling `[D2RequireScope]`.
- `Endpoints/RequireD2GrpcScopeExtensionsTests.cs` — fluent extensions correctly attach metadata; null/whitespace throws.
- `Status/D2RpcStatusExtensionsTests.cs` — every `AuthFailures` surface → expected `Status.StatusCode` + trailer set; `Status.Detail` empty; `traceid` presence/absence per `Activity.Current`; counter increment.
- `AuthGrpcServiceCollectionExtensionsTests.cs` — DI registration; `AddD2Auth` precondition fail-fast; idempotent re-call; interceptor type registered as singleton; appears in `GrpcServiceOptions.Interceptors` exactly once; scoped `IRequestContext` adapter resolution.

Run: `dotnet test server/shared/dotnet/tests`.

## Debugging

When a gRPC caller starts seeing `RpcException` with `Status.Unauthenticated` / `Status.Unavailable` from a service guarded by this lib:

### Inspecting trailers from the client

Auth failures carry the diagnostic in trailers, NOT in `Status.Detail` (which is intentionally empty — info-leak avoidance). Read them via `RpcException.Trailers`:

```csharp
try
{
    var reply = await client.GetFileAsync(request);
}
catch (RpcException ex)
{
    var errorCode = ex.Trailers.GetValue("d2_error_code"); // e.g. "AUTH_JWT_EXPIRED"
    var messages = ex.Trailers.GetValue("d2_messages");    // JSON array of TKMessage
    var traceId = ex.Trailers.GetValue("traceid");         // W3C trace id (lower-hex)
    logger.LogWarning(
        "gRPC auth rejected: code={Code} traceid={TraceId}",
        errorCode,
        traceId);
}
```

### Inspecting trailers from `grpcurl`

Use `-v` (verbose) to surface trailer metadata:

```text
grpcurl -v -H "authorization: Bearer eyJ..." \
    localhost:5000 my.package.Files/GetFile
```

`Trailers received:` appears at the bottom of verbose output with `d2_error_code`, `d2_messages`, `traceid`. The Status line shows the gRPC status code (e.g. `Code: Unauthenticated`).

### `d2_messages` JSON shape

Same wire shape as the HTTP middleware's ProblemDetails `d2_messages` extension — a JSON array of TKMessage records:

```json
[{"key":"UNAUTHORIZED","params":{}}]
```

`key` is a TK constant (translated client-side via the i18n catalog); `params` carries bounded scalar substitutions only (no PII). Auth-surface messages today use `UNAUTHORIZED` / `TEMPORARILY_UNAVAILABLE` keys with no params.

### Correlating `traceid` to OTel spans

The `traceid` trailer is the W3C trace-id of the active `Activity.Current` at the time of failure (lower-hex format, 32 chars). Correlate with the server-side span in your OTel backend (Tempo / Jaeger / etc.) — the `JwtAuthInterceptor` runs inside the gRPC server-call activity, so the span will carry the auth-failure logs (via `AuthLog` delegates) and the `AuthTelemetry.ProblemEmitted` counter increment.

### `IRequestContext` resolution failures (anonymous methods + DI ctor injection)

Two distinct `InvalidOperationException` surfaces from the cross-transport scoped `IRequestContext` resolver registered by `AddD2AuthGrpc()` (lambda body byte-equivalent to the one `AddD2AuthHttp()` registers — both read `HttpContext.Items[D2HttpContextItems.REQUEST_CONTEXT]`):

- **`"IRequestContext was resolved without an active HttpContext. Ensure the resolution site runs inside an AspNetCore request (UseD2Auth() for HTTP; AddD2AuthGrpc() for gRPC) and that an HttpContext is on the execution context."`** — the resolver fetched `IHttpContextAccessor` and got `null`. Background-service / hosted-service code that takes a constructor `IRequestContext` would surface this. Resolution sites must be inside an inbound request.
- **`"IRequestContext was resolved before the auth pipeline ran. Ensure UseD2Auth() (for HTTP) or AddD2AuthGrpc()'s interceptor (for gRPC) has run before resolving IRequestContext."`** — `HttpContext` is present but the slot is empty. Two common causes: (a) the gRPC method is decorated with `[D2AllowAnonymous]` (interceptor short-circuited; nothing was written to the slot) — see [Footguns / common pitfalls → Anonymous methods + ctor-injected `IRequestContext`](#anonymous-methods--ctor-injected-irequestcontext) for the three workarounds; (b) the resolution site runs upstream of the interceptor — rarer for gRPC since the interceptor is the first hop in the gRPC service-call pipeline, but possible when middleware on the AspNetCore pipeline (above gRPC) tries to constructor-inject `IRequestContext`.

The error messages are produced by the resolver lambda in `AuthGrpcServiceCollectionExtensions.AddD2AuthGrpc()`. The matching `AddD2AuthHttp()` lambda emits identical text — both transports surface the same diagnostics.

Note: `ServerCallContext.UserState` IS still written by the interceptor on successful auth (and the typed accessor `ServerCallContext.GetD2RequestContext()` reads it for the gRPC-specific hot-path use case). But the cross-transport DI resolver does NOT read from `UserState` — it reads from `HttpContext.Items` so a single resolver lambda covers both transports. This split keeps the gRPC hot-path accessor available for service code that already has a `ServerCallContext` in hand without forcing the DI resolver into transport-specific branching.

### Common AUTH_* error codes

For full code reference + remediation per code, see [`../auth/README.md` § Debugging](../auth/README.md#debugging) — the same `AUTH_*` taxonomy applies across HTTP and gRPC transports (single sink at `AuthTelemetry.ProblemEmitted`). Two codes specific to the failure surface above:

- **`AUTH_BEARER_MISSING`** — no `authorization` metadata, wrong scheme, or empty after `Bearer `. Check the caller's `Metadata.Add("authorization", "Bearer " + token)` site (or `Grpc.Net.ClientFactory`'s `ConfigureChannel` + `CallCredentials`).
- **`AUTH_SCOPE_INSUFFICIENT`** — bearer is valid, but the validated `Scopes` set didn't overlap with the method's required set. `traceid` lets you find the matching server-side span — the span's enriched logs will show which scope set was required vs which was presented (without leaking the values into the trailer or `Status.Detail`).

## References

- [`../auth/README.md`](../auth/README.md) — JWT validator + JWKS + liveness internals
- [`../auth-http/README.md`](../auth-http/README.md) — HTTP-transport sibling
- [`../auth-abstractions/README.md`](../auth-abstractions/README.md) — `ISessionLivenessTracker`, `JwtClaimTypes`, `Audiences`, `Scopes`
- [`../auth-outbound/README.md`](../auth-outbound/README.md) — outbound service-identity bearer attachment (the gRPC counterpart on the client side)
- [RFC 6750](https://datatracker.ietf.org/doc/html/rfc6750) — Bearer Token Usage
- [gRPC status codes](https://grpc.io/docs/guides/status-codes/) — canonical status code semantics
