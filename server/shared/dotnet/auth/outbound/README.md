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

---

## File layout

```
auth/outbound/
├── AuthOutboundOptions.cs                            # config
├── AuthOutboundServiceCollectionExtensions.cs        # AddD2AuthOutbound composition root
├── Grpc/
│   ├── ServiceIdentityCallCredentials.cs             # gRPC CallCredentials sourcing the bearer from IServiceIdentityClient
│   └── GrpcClientBuilderExtensions.cs                # .AddD2ServiceIdentity() per-channel opt-in
├── ServiceIdentity/
│   ├── IServiceIdentityClient.cs                     # interface
│   ├── HttpServiceIdentityClient.cs                  # POST /oauth/token grant_type=client_credentials
│   ├── ServiceIdentityCache.cs                       # atomic-ref single-value cache
│   ├── ServiceIdentitySnapshot.cs                    # (Token, ExpiresAt) record
│   ├── ServiceIdentityException.cs                   # internal parse-failure exception
│   └── ServiceIdentityRefreshHostedService.cs        # background proactive refresh
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
