<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Shared.Auth.Outbound

> Parent: [`server/shared/dotnet/`](../../README.md)

> ## ⚠ Status — this lib predates the auth pivot; read before relying on any flow below
>
> The service-to-service auth model changed. Two of this lib's surfaces no longer describe the intended runtime:
>
> - **`client_credentials` service identity (`IServiceIdentityClient` / `ServiceIdentityCallCredentials` / `AddD2ServiceIdentity()`) is superseded by mTLS workload identity** ([ADR-0023](../../../../../docs/adrs/0023-mtls-workload-identity.md)). Which workload is calling is established by a mutually-authenticated TLS channel — a verified client certificate — not by a service-identity bearer threaded onto every hop. Threading a second JWT through each hop would reintroduce a per-hop mint and an audience-targeting problem at a strict receiver; mTLS supplies workload identity without either.
> - **RFC 8693 token exchange (`ITokenExchangeClient`) is repurposed off the per-hop path** ([ADR-0022](../../../../../docs/adrs/0022-service-auth-mint-once-forward.md)). Edge mints exactly one internal transaction-token at the trust boundary; every downstream cross-process hop **forwards that token unchanged and re-validates it** rather than exchanging for a narrowed one. Exchange is retained for the single boundary mint and the deliberate exceptions — cross-trust-domain calls, justified narrowing, asynchronous scope reduction, and establishing/extending an impersonation `act` chain — not as the per-hop business default.
>
> **This lib is built but wired into no request flow today** — its clients have test-only callers and the Edge issuer endpoint they target is not built. The code disposition (remove the service-identity surface, keep the repurposed token-exchange surface) is a later deliverable; nothing here is removed yet. The sections below describe the lib **as it currently exists**, not the intended steady-state runtime.

Outbound auth runtime — an RFC 8693 `token-exchange` client (`ITokenExchangeClient`) plus the now-superseded `client_credentials` service-identity client (`IServiceIdentityClient`) and a per-channel gRPC opt-in that attaches a service-identity bearer to outbound D² calls. Pure consumer of Edge's OAuth `token_endpoint`; this lib does NOT issue tokens. See the status note above for which surfaces are superseded (service identity → mTLS) versus repurposed (token exchange → boundary mint + exceptions).

OIDC discovery is canonical: a single `D2_AUTH_ISSUER` env var drives `<issuer>/.well-known/openid-configuration`, and `token_endpoint` is read from the discovery doc — no separate URL knobs.

---

## Public API surface

### Outbound clients

> Per the status note above: `IServiceIdentityClient` is superseded by mTLS workload identity, and `ITokenExchangeClient` is repurposed to the boundary mint + exception cases (not the per-hop default). The shapes below document the lib as it currently exists.

| Type                                                                                    | Role                                                                                                                                     |
| --------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------- |
| `IServiceIdentityClient.GetCurrentTokenAsync(ct)`                                       | Returns the current service-identity JWT for outbound calls (cached in-process; refreshed proactively by the background hosted service). |
| `ITokenExchangeClient.ExchangeAsync(subjectToken, targetAudience, narrowedScopes?, ct)` | Exchanges a subject JWT for a token addressed to `targetAudience` (RFC 8693). Cached per `(sessionId, audience, scope-set)` in `ILocalCache`.  |

### Composition root

```csharp
services.AddD2AuthOutbound(opts =>
{
    opts.Issuer = configuration["D2_AUTH_ISSUER"]!;
    opts.ClientId = configuration["D2_AUTH_CLIENT_ID"]!;
    opts.ClientSecret = configuration["D2_AUTH_CLIENT_SECRET"]!;
});
```

Registers:

- `AuthOutboundOptions` (configured)
- `IConfigurationManager<OpenIdConnectConfiguration>` (single per-process, auto-refreshes discovery doc; uses our `IHttpClientFactory`-managed client so the configured timeout / TLS / connection-pool settings apply)
- `ServiceIdentityCache` + `IServiceIdentityClient` + `HttpServiceIdentityClient`
- `TokenExchangeCache` + `ITokenExchangeClient` + `HttpTokenExchangeClient`
- Three named `HttpClient`s (`d2-auth-oidc-discovery`, `d2-auth-service-identity`, `d2-auth-token-exchange`)
- `ServiceIdentityRefreshHostedService` (proactive refresh ~60 s before token expiry)

### Per-channel gRPC opt-in (service-identity attachment — superseded)

> The service-identity bearer this section attaches is superseded by mTLS workload identity ([ADR-0023](../../../../../docs/adrs/0023-mtls-workload-identity.md)) — under the steady-state model the calling workload is identified by the mutually-authenticated TLS channel, and the bearer a downstream gRPC call carries is the single Edge-minted transaction-token forwarded unchanged ([ADR-0022](../../../../../docs/adrs/0022-service-auth-mint-once-forward.md)), not a separate service-identity token. The opt-in below describes the lib as it currently stands; its disposition is a later deliverable.

```csharp
services
    .AddGrpcClient<FilesGrpc.FilesGrpcClient>(o =>
        o.Address = new Uri(D2.Shared.Auth.Abstractions.Audiences.Files))
    .AddD2ServiceIdentity();   // ← attaches Bearer <service-identity-token>

// Non-D² gRPC channels (SeaweedFS, third-party gRPC) omit .AddD2ServiceIdentity().
```

Per-channel opt-in is the safe default — auto-applying to every gRPC channel would leak our internal Edge JWT to non-D² services.

### Forwarded transaction-token — per-request `CallCredentials` (the forward-unchanged rail of ADR-0022)

The forward-unchanged service-to-service model ([ADR-0022 §Realization](../../../../../docs/adrs/0022-service-auth-mint-once-forward.md)): Edge mints exactly one internal transaction-token at the trust boundary, and every downstream cross-process gRPC hop **re-attaches that same token unchanged**. The outbound half of that rail lives here.

```csharp
// Composition root (one-time host setup; pair with AddD2WorkloadCertificateOutbound):
services.AddD2ForwardedJwtOutbound();

// Per-channel attach — the GENERATED gRPC-client DI extension AUTO-CHAINS this
// (alongside .AddD2WorkloadCertificate()); a host never calls it directly:
services
    .AddGrpcClient<FilesGrpc.FilesGrpcClient>(o => o.Address = new Uri("https://files.internal"))
    .AddD2ForwardedJwt();          // ← attaches Bearer <the current request's forwarded JWT>
```

`ForwardedJwtCallCredentials.FromAmbientRequestScope(...)` produces a `CallCredentials` that, **per outbound RPC**, resolves the current inbound request's request-scoped `IForwardedJwtAccessor` (the [redacting `ForwardedJwt` holder](../abstractions/README.md)), reveals the held bearer bytes, and attaches `Authorization: Bearer <bytes>`. It does **not** capture a token at channel construction:

- **Per-channel singleton, per-request token.** A `CallCredentials` is one object bound to the channel and reused for every RPC, but the forwarded token is the _current_ request's token — different on every concurrent request sharing a long-lived channel. So the credential closes over the **ambient-scope port** (a stateless singleton), never a resolved holder or token; per call it re-derives the current request's scope and reads that scope's holder. Because the port is backed by an `AsyncLocal`-flowed accessor, two concurrent requests each observe their own scope, holder, and token — no cross-request bleed. A capture-at-construction credential would forward the first request's token to every later request; resolving per call is what prevents that.
- **Hard-fail on absent token.** No ambient request scope, no registered holder, or an empty/absent `Current` all raise `RpcException(StatusCode.Unauthenticated)` with a fixed, token-free message — never a silent no-header send. A genuinely system-initiated call (a future scheduled job with no inbound request) hard-fails here (the correct fail-loud behavior); such callers carry their own identity when they exist.
- **The sole reveal seam.** This credential is the single production caller of `ForwardedJwt.RevealForForwarding()` — a source-text scan pins that no other production type reveals the raw bearer. The revealed bytes flow ONLY into the metadata write; the credential holds no logger, declares no `[LoggerMessage]`, and logs nothing — the reveal-and-attach is structurally leak-free at the transmission point.
- **Composes alongside mTLS.** `.AddD2ForwardedJwt().AddD2WorkloadCertificate()` is compose-don't-clobber: the forwarded JWT is set on `options.Credentials`, the mTLS leaf chain on the channel handler's `SslOptions` — orthogonal axes, neither clobbers the other. An existing `options.Credentials` is composed with (`ChannelCredentials.Create(existing, ours)`), not replaced; `SecureSsl` is the default when none is set yet.

#### The ambient-scope port + adapter (how the credential reaches the request holder)

`ForwardedJwtCallCredentials` depends on the framework-free **`IAmbientRequestScopeAccessor`** port (declared in `D2.Shared.Auth.Abstractions`), which abstracts "get the current ambient request scope's `IServiceProvider`." The port living in abstractions — referenced by BOTH `D2.Shared.Auth.Outbound` and `D2.Shared.Auth.Http` — is what keeps this lib free of any AspNetCore framework reference (no `auth/http → auth/outbound` edge is needed). The concrete `IHttpContextAccessor`-backed adapter (`HttpContextAmbientRequestScopeAccessor`) lives in the **`D2.Shared.Auth.Http`** transport binding (which already references the framework) and is registered by **`AddD2AuthHttp()`** alongside the request-scoped holder — symmetric: the inbound surface writes the validated bearer into `HttpContext.RequestServices`; the credential reads it back through the same door. A forwarding host is HTTP-inbound in the current architecture (Edge: HTTP from the BFF in, gRPC to backends out), so registering the adapter on the inbound HTTP transport covers it; a future gRPC-inbound-only forwarding host wires its own adapter when it exists.

`AddD2ForwardedJwtOutbound()` registers **neither** the holder nor the ambient adapter (the inbound transport owns both); it is the documented one-time host hook that pairs with `AddD2WorkloadCertificateOutbound()`. The credential reads no configuration (the token is ambient), so it adds no `AuthOutboundOptions` fields.

### Workload certificate — mTLS leaf presentation (the caller half of ADR-0023)

This is the **caller (client) half** of the internal-mTLS workload-identity layer; the callee (server) half — Kestrel require-and-validate — lives in `D2.Shared.AspNetCore` (`AddD2MutualTls`). A workload holds its current leaf certificate in memory and proactively reissues it before expiry, on the same refresh-ahead pattern the service-identity client uses, then presents it on outbound gRPC channels that opt in.

```csharp
// Composition root (opt-in, independent of AddD2AuthOutbound):
services.AddD2WorkloadCertificateOutbound();

// The host supplies the reissue adapter (the dev / harness in-process seam) —
// the shared lib defines only the port, never referencing a service domain:
services.AddSingleton<IWorkloadCertificateIssuer, MyInProcessWorkloadCertificateIssuer>();

// Per-channel opt-in — composes ALONGSIDE .AddD2ServiceIdentity():
services
    .AddGrpcClient<FilesGrpc.FilesGrpcClient>(o => o.Address = new Uri("https://files.internal"))
    .AddD2WorkloadCertificate();   // ← presents the current leaf on the mTLS handshake
```

`AddD2WorkloadCertificateOutbound()` registers:

- `WorkloadLeafCache` — single per-process slot holding the live leaf `X509Certificate2`, its issuing intermediate, and the pre-built `SslStreamCertificateContext` chain (atomic-ref swap; disposes the superseded leaf + intermediate on swap, the current pair on cache disposal — the leaf carries the secret key, the intermediate is public).
- `WorkloadLeafClient` + `IWorkloadLeafSource` — the refresh-ahead leaf source. Reissues through the host-supplied `IWorkloadCertificateIssuer`, builds a live private-key-bearing leaf from the returned DER + PKCS#8 (zeroing the PKCS#8 once the cert owns the key), decodes the issuing intermediate, builds the presentable chain context, caches it, and serves-stale-on-transient (singleflight + circuit-breaker, same shape as the service-identity client).
- `WorkloadLeafRefreshHostedService` — polls every 30 s and reissues when `NotAfter - now <= WorkloadLeafRefreshLeadTime` (default 5 min; leaf TTLs are hours).

`AddD2WorkloadCertificate()` on the gRPC builder sets the channel handler's `SslClientAuthenticationOptions.ClientCertificateContext` to the cache's current chain context (the full `leaf → intermediate` chain) at channel build — presenting the chain lets a strict peer rebuild a root-anchored chain without a machine-store-resident intermediate or a network (AIA) fetch. It composes alongside `AddD2ServiceIdentity()` (the leaf chain is set on the channel handler's `SslOptions`; the token is set on `options.Credentials` — orthogonal, compose-don't-clobber on `options.HttpHandler`). Safe-by-default: a channel that does not call it presents no client certificate.

The chain context is resolved **once, at channel construction** (a `ClientCertificateContext` is not a per-connection selection callback). The refresh-ahead loop keeps the cache holding a current chain, but a consumer holding a long-lived channel does not automatically adopt a rotated leaf — it must rebuild the channel to present the freshly-rotated leaf. Rebuilding a long-lived channel on rotation is the consumer's responsibility; the channel's lifecycle is the natural home for it.

> **Platform note.** On Linux/OpenSSL (the deployment target) the chain context is always built and the full chain is presented. On Windows, Schannel builds the chain outside the process and refuses to construct a chain context for a leaf whose internal-CA root is not installed in the OS trust store (and cannot transmit an application-supplied intermediate without store residency — a documented Schannel limitation). On that path the leaf source caches no chain context and the per-channel opt-in falls back to presenting the bare leaf; a Windows host that needs the chain transmitted installs the CA into the OS store (operator action), which is outside this in-process presentation path. This mirrors the platform split already used for the leaf's private-key handling.

The cross-process "a separate service obtains its first leaf from KeyCustodian over the wire" bootstrap (the gRPC issuance contract + bootstrap-identity provisioning) is documented future work — not the `IWorkloadCertificateIssuer` port, which is the in-process / harness seam that proves the full refresh-ahead + presentation path locally.

---

## File layout

```
auth/outbound/
├── AuthOutboundOptions.cs                            # config
├── AuthOutboundServiceCollectionExtensions.cs        # AddD2AuthOutbound + AddD2WorkloadCertificateOutbound + AddD2ForwardedJwtOutbound composition roots
├── Grpc/
│   ├── ForwardedJwtCallCredentials.cs                # per-request CallCredentials — reveals the request-scoped ForwardedJwt + attaches Bearer (the sole reveal caller)
│   ├── ServiceIdentityCallCredentials.cs             # gRPC CallCredentials sourcing the bearer from IServiceIdentityClient
│   └── GrpcClientBuilderExtensions.cs                # .AddD2ForwardedJwt() (forwarded token) + .AddD2ServiceIdentity() (bearer) + .AddD2WorkloadCertificate() (leaf-chain context) per-channel opt-ins
├── ServiceIdentity/
│   ├── IServiceIdentityClient.cs                     # interface
│   ├── HttpServiceIdentityClient.cs                  # POST /oauth/token grant_type=client_credentials
│   ├── ServiceIdentityCache.cs                       # atomic-ref single-value cache
│   ├── ServiceIdentitySnapshot.cs                    # (Token, ExpiresAt) record
│   ├── ServiceIdentityException.cs                   # internal parse-failure exception
│   └── ServiceIdentityRefreshHostedService.cs        # background proactive refresh
├── WorkloadCertificate/
│   ├── IWorkloadLeafSource.cs                        # interface — current live leaf accessor
│   ├── IWorkloadCertificateIssuer.cs                 # host-supplied reissue port (BCL DER+PKCS#8 boundary)
│   ├── WorkloadLeafMaterial.cs                       # (CertificateDer, PrivateKeyPkcs8, IssuerCertificateDer, NotAfter) record
│   ├── WorkloadLeafClient.cs                         # refresh-ahead reissue + live-chain build (singleflight + breaker)
│   ├── WorkloadLeafCache.cs                          # atomic-ref single-value live-chain cache (disposes superseded leaf + intermediate)
│   ├── WorkloadLeafSnapshot.cs                       # (Leaf, Intermediate, ChainContext, NotAfter) record
│   └── WorkloadLeafRefreshHostedService.cs           # background proactive reissue
├── TokenExchange/
│   ├── ITokenExchangeClient.cs                       # interface
│   ├── HttpTokenExchangeClient.cs                    # POST /oauth/token grant_type=token-exchange
│   ├── TokenExchangeCache.cs                         # ILocalCache facade + sessionId reverse-index + backplane subscriber
│   └── TokenExchangeException.cs                     # internal parse-failure exception
└── Telemetry/
    ├── OutboundTelemetry.cs                          # ActivitySource + Meter + counters
    └── OutboundLog.cs                                # LoggerMessage delegates
```

---

## Caching model

### ServiceIdentity cache

Single per-process slot. Atomic reference swap of an immutable `(Token, ExpiresAt)` snapshot — readers never observe a torn state, and there is no lock on the read path. No `ILocalCache` involvement (single value, no key namespace).

The `ServiceIdentityRefreshHostedService` polls every 5 s and proactively refreshes when `ExpiresAt - now <= ServiceIdentityRefreshLeadTime` (default 60 s). On refresh failure with a still-valid cached token, the warning logs but the existing token continues to be served until it actually expires; only when no still-valid token exists AND the fetch fails does `GetCurrentTokenAsync` hard-fail with `D2Result.ServiceUnavailable`.

Concurrent first-callers (on-demand + the hosted service) dedup to a single HTTP fetch via `Singleflight` from `D2.Shared.Resilience`. Each fetch also passes through a `CircuitBreaker` (5 consecutive transient failures → 30 s open) — after the threshold, callers receive `ServiceUnavailable` immediately without waiting for an HTTP timeout, stopping the hammering of a down Edge.

### TokenExchange cache

Backed by the shared `ILocalCache` singleton (with a `tokenexchange:` key prefix), so the lib-wide `LocalCacheOptions.MaxEntries` ceiling applies (default 100k). Key shape: `tokenexchange:{sessionId}:{audience}:{scopeSetHash}`, where `scopeSetHash` is the first 16 hex chars of SHA-256 over the sorted comma-joined narrowed-scope names (or `_default` when no narrowing is requested).

`sessionId` comes from the inbound JWT's `d2_session_id` claim, parsed without re-validating the signature (the inbound auth middleware already validated upstream). This sessionId is the invalidation key — the cache subscribes to `ICacheInvalidationBackplane` for `session-revoked:{guid}` events and purges every cached exchange token bound to that session via a per-process `ConcurrentDictionary<sessionId, HashSet<cacheKey>>` reverse-index.

Concurrent first-callers for the same `(sessionId, audience, scope-set)` tuple dedup to a single HTTP fetch via `Singleflight`.

Edge unreachable on cache miss → `D2Result.ServiceUnavailable` (no graceful-degradation fallback — Edge being down means auth is down, and downstream services would reject anything we hand them anyway; pretending we have a working token by serving stale entries creates harder-to-debug failure modes than a fast fail). The `CircuitBreaker` (5 consecutive failures → 30 s open) stops the hammering: once the threshold is hit, callers receive `ServiceUnavailable` immediately without waiting for an HTTP timeout.

The backplane subscription is OPTIONAL. If `ICacheInvalidationBackplane` isn't registered, the cache logs a startup warning and falls back to TTL-only invalidation (acceptable for single-instance deployments; not for clusters that need cross-instance session-revoke propagation).

---

## Configuration

| Option                           | Env var                 | Default                  | Purpose                                                                           |
| -------------------------------- | ----------------------- | ------------------------ | --------------------------------------------------------------------------------- |
| `Issuer`                         | `D2_AUTH_ISSUER`        | (required)               | OIDC issuer URL — drives discovery doc fetch.                                     |
| `ClientId`                       | `D2_AUTH_CLIENT_ID`     | (required)               | This service's OAuth client id.                                                   |
| `ClientSecret`                   | `D2_AUTH_CLIENT_SECRET` | (required, NEVER logged) | This service's OAuth client secret.                                               |
| `ServiceIdentityRefreshLeadTime` | —                       | 60 s                     | How early before expiry to proactively refresh the cached service-identity token. |
| `HttpRequestTimeout`             | —                       | 5 s                      | Per-request timeout on outbound HTTP calls to Edge.                               |
| `TokenExchangeCacheKeyPrefix`    | —                       | `tokenexchange:`         | Prefix for token-exchange cache entries in the shared `ILocalCache`.              |
| `TokenExchangeCacheFallbackTtl`  | —                       | 5 min                    | Fallback TTL when the OAuth response's `expires_in` is missing or unparseable.    |

---

## Telemetry

Tag-key + tag-value constants are emitted by [`D2.Shared.Telemetry.Tags.SourceGen`](../../telemetry/tags-source-gen/README.md) into `OutboundTelemetryTags.g.cs` from [`contracts/telemetry/telemetry.spec.json`](../../../../../contracts/telemetry/telemetry.spec.json). Counter call sites reference `OutboundTelemetryTags.ServiceIdentityFetches.Outcome.CACHE_HIT` / etc. instead of bare string literals. The emitted file lands in the tracked `Generated/` directory (committed for inspection, IDE navigation, and PR diff review; re-emitted on every `dotnet build`; do not hand-edit).

| Counter                                          | Tags                                                                                                                                                                                          | Purpose                                                                                                                                             |
| ------------------------------------------------ | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------- |
| `d2.auth.outbound.service_identity.fetches`      | `outcome` (`OutboundTelemetryTags.ServiceIdentityFetches.Outcome.*`: `cache_hit` / `cache_hit_after_singleflight` / `fetch_success` / `fetch_failure` / `http_failure` / `discovery_failure`) | Service-identity token resolutions. One increment per `GetCurrentTokenAsync` / `ForceRefreshAsync` call.                                            |
| `d2.auth.outbound.token_exchange.requests`       | `outcome` (`OutboundTelemetryTags.TokenExchangeRequests.Outcome.*`; same six values)                                                                                                          | Token-exchange requests. One increment per `ExchangeAsync` call (input-validation failures aren't counted — caller bug, not auth-runtime traffic).  |
| `d2.auth.outbound.token_exchange.revoked_purges` | —                                                                                                                                                                                             | Cache entries purged by session-revoked backplane events; one increment per purged key. Useful for verifying cluster-wide invalidation propagation. |

`ActivitySource` and `Meter` both named `D2.Shared.Auth.Outbound`. Hosts wire via `.AddSource(OutboundTelemetry.ACTIVITY_SOURCE_NAME)` / `.AddMeter(OutboundTelemetry.METER_NAME)`.

---

## Bootstrap order

> The bootstrap chain below reflects the superseded `client_credentials` service-identity model, where a service-identity bearer was acquired first and then used to authenticate a host's own outbound calls to Edge. Under the current model that chain dissolves: cross-process calls authenticate their workload by mTLS ([ADR-0023](../../../../../docs/adrs/0023-mtls-workload-identity.md)) — a host's keyring / JWKS calls to Edge present a client certificate, not a service-identity JWT — so the "acquire a service-identity token first" ordering requirement falls away. The bootstrap ordering for the mTLS path (certificate material available before the first outbound call) is part of the subsystem the implementing deliverable builds. This section is retained as a description of the lib as it currently stands.

This lib's pieces depend on standard .NET hosting infrastructure but produce a strict bootstrap-order requirement for downstream auth components:

1. `IServiceIdentityClient` initializes (the refresh hosted service acquires the first token at startup).
2. `IKeyringClient` + `IJwksProvider` (in the inbound `D2.Shared.Auth` lib) use the JWT from #1 to authenticate their own gRPC / HTTP calls to Edge.
3. `JwtAuthMiddleware` / `JwtAuthInterceptor` (also in the inbound lib) start accepting requests.

Hosts that deploy the inbound `D2.Shared.Auth` lib alongside this one MUST register this lib first.

---

## References

- [`D2.Shared.Auth`](../core/README.md) — inbound auth runtime (JWT validator + session liveness + `AddD2Auth` composition root); transport bindings in `D2.Shared.Auth.Http` + `D2.Shared.Auth.Grpc` siblings
- [`D2.Shared.Auth.Abstractions`](../abstractions/README.md) — `Audiences.*` / `JwtClaimTypes.*` constants
- [`D2.Shared.Caching.Abstractions`](../../caching/abstractions/README.md) — `ILocalCache` + `ICacheInvalidationBackplane` interfaces
- [`D2.Shared.Resilience`](../../resilience/README.md) — `Singleflight` for fetch-path deduplication + `CircuitBreaker` to fast-fail during sustained Edge outage
- [ADR-0022](../../../../../docs/adrs/0022-service-auth-mint-once-forward.md) — mint-once-at-the-Edge, forward-unchanged service-to-service model; token exchange repurposed to the boundary mint + exceptions
- [ADR-0023](../../../../../docs/adrs/0023-mtls-workload-identity.md) — mTLS workload identity that supersedes the `client_credentials` service-identity layer
- [RFC 6749 §4.4](https://datatracker.ietf.org/doc/html/rfc6749#section-4.4) — `client_credentials` grant (the basis of the superseded service-identity client)
- [RFC 8693](https://datatracker.ietf.org/doc/html/rfc8693) — token-exchange grant (the boundary mint + the exception cases)
