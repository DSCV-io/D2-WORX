<!--
Copyright (c) DCSV. All rights reserved.
-->

# DcsvIo.D2.Auth.Outbound

> Parent: [`public/packages/dotnet/`](../../README.md)

The caller-side companion to the inbound auth runtime. A host that makes internal cross-process calls reaches for four outbound factors here, each opt-in and independent:

- **Forwarded transaction-token** (`AddD2ForwardedJwt` / `ForwardedJwtCallCredentials`) — the per-request gRPC `CallCredentials` that re-attaches the current inbound request's validated transaction-token unchanged on each outbound hop ([ADR-0022](../../../../../public/docs/adrs/0022-service-auth-mint-once-forward.md)). This is the service-to-service business default: the downstream receiver re-validates the same token and reads the user's identity *and* scopes straight from it.
- **Workload certificate** (`AddD2WorkloadCertificate` / `AddD2WorkloadCertificateOutbound`) — the mTLS leaf the calling workload presents on outbound gRPC channels. The mutually-authenticated TLS channel is what establishes *which workload is calling* ([ADR-0023](../../../../../public/docs/adrs/0023-mtls-workload-identity.md)).
- **RFC 8693 token exchange** (`ITokenExchangeClient`) — the boundary mint plus the deliberate exception cases. Edge mints exactly one internal transaction-token at the trust boundary; exchange is the explicit tool for the single boundary mint and the enumerated exceptions — cross-trust-domain calls, justified narrowing, asynchronous scope reduction, and establishing/extending an impersonation `act` chain — never the per-hop business default.
- **Propagated context** (`AddD2PropagatedContext` / `PropagatedContextClientInterceptor`) — the gRPC client interceptor that writes the `x-d2-context` header (the operational propagation subset PLUS the accumulated call-path) on every outbound RPC ([ADR-0025](../../../../../public/docs/adrs/0025-request-context-establishment.md)). Closes the sync-hop gap ADR-0022 left open: the propagated subset now rides gRPC, not only AMQP.

Pure consumer of Edge's OAuth `token_endpoint`; this lib does NOT issue tokens. OIDC discovery is canonical: a single `D2_AUTH_ISSUER` env var drives `<issuer>/.well-known/openid-configuration`, and `token_endpoint` is read from the discovery doc — no separate URL knobs.

---

## Public API surface

### Token-exchange client

| Type                                                                                    | Role                                                                                                                                     |
| --------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------- |
| `ITokenExchangeClient.ExchangeAsync(subjectToken, targetAudience, narrowedScopes?, ct)` | Exchanges a subject JWT for a token addressed to `targetAudience` (RFC 8693) — the boundary mint + the exception cases. Cached per `(sessionId, audience, scope-set)` in `ILocalCache`. |

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
- `TokenExchangeCache` + `ITokenExchangeClient` + `HttpTokenExchangeClient`
- Two named `HttpClient`s (`d2-auth-oidc-discovery`, `d2-auth-token-exchange`)

The forwarded-JWT and workload-certificate factors have their own composition roots (`AddD2ForwardedJwtOutbound()` and `AddD2WorkloadCertificateOutbound()`), each independent of `AddD2AuthOutbound()` — a host wires only the factors it needs.

### Forwarded transaction-token — per-request `CallCredentials` (the forward-unchanged rail of ADR-0022)

The forward-unchanged service-to-service model ([ADR-0022 §Realization](../../../../../public/docs/adrs/0022-service-auth-mint-once-forward.md)): Edge mints exactly one internal transaction-token at the trust boundary, and every downstream cross-process gRPC hop **re-attaches that same token unchanged**. The outbound half of that rail lives here.

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

`ForwardedJwtCallCredentials` depends on the framework-free **`IAmbientRequestScopeAccessor`** port (declared in `DcsvIo.D2.Auth.Abstractions`), which abstracts "get the current ambient request scope's `IServiceProvider`." The port living in abstractions — referenced by BOTH `DcsvIo.D2.Auth.Outbound` and `DcsvIo.D2.Auth.Http` — is what keeps this lib free of any AspNetCore framework reference (no `auth/http → auth/outbound` edge is needed). The concrete `IHttpContextAccessor`-backed adapter lives in whichever inbound transport binding the host uses — `HttpContextAmbientRequestScopeAccessor` in **`DcsvIo.D2.Auth.Http`** (registered by **`AddD2AuthHttp()`**) and the sibling `GrpcHttpContextAmbientRequestScopeAccessor` in **`DcsvIo.D2.Auth.Grpc`** (registered by **`AddD2AuthGrpc()`**), both alongside the request-scoped holder — symmetric: the inbound surface writes the validated bearer into `HttpContext.RequestServices`; the credential reads it back through the same door. Both transports self-wire the read-back door, so a host is covered whether it is HTTP-inbound (Edge: HTTP from the BFF in, gRPC to backends out) or gRPC-inbound (backend→backend), with no host-supplied adapter required. The two adapters are a deliberate tiny duplicate rather than a shared type (the two transport libs have no inter-csproj dep); on a dual-transport host they read the same door, so first-wins `TryAddSingleton` is harmless.

`AddD2ForwardedJwtOutbound()` registers **neither** the holder nor the ambient adapter (the inbound transport owns both); it is the documented one-time host hook that pairs with `AddD2WorkloadCertificateOutbound()`. The credential reads no configuration (the token is ambient), so it adds no `AuthOutboundOptions` fields.

### Workload certificate — mTLS leaf presentation (the caller half of ADR-0023)

This is the **caller (client) half** of the internal-mTLS workload-identity layer; the callee (server) half — Kestrel require-and-validate — lives in `DcsvIo.D2.AspNetCore` (`AddD2MutualTls`). A workload holds its current leaf certificate in memory and proactively reissues it before expiry on a refresh-ahead loop, then presents it on outbound gRPC channels that opt in.

```csharp
// Composition root (opt-in, independent of AddD2AuthOutbound):
services.AddD2WorkloadCertificateOutbound();

// The host supplies the reissue adapter (the dev / harness in-process seam) —
// the shared lib defines only the port, never referencing a service domain:
services.AddSingleton<IWorkloadCertificateIssuer, MyInProcessWorkloadCertificateIssuer>();

// Per-channel opt-in — composes alongside .AddD2ForwardedJwt():
services
    .AddGrpcClient<FilesGrpc.FilesGrpcClient>(o => o.Address = new Uri("https://files.internal"))
    .AddD2WorkloadCertificate();   // ← presents the current leaf on the mTLS handshake
```

`AddD2WorkloadCertificateOutbound()` registers:

- `WorkloadLeafCache` — single per-process slot holding the live leaf `X509Certificate2`, its issuing intermediate, and the pre-built `SslStreamCertificateContext` chain (atomic-ref swap; disposes the superseded leaf + intermediate on swap, the current pair on cache disposal — the leaf carries the secret key, the intermediate is public).
- `WorkloadLeafClient` + `IWorkloadLeafSource` — the refresh-ahead leaf source, **CSR flow**: it generates a FRESH ECDSA P-256 keypair per reissue (per-rotation key freshness — the workload owns its key lifecycle), builds a PKCS#10 certificate-signing request (fixed placeholder subject `CN=d2-workload` — the issuer structurally ignores it; the leaf's SAN authority is the issuer's authenticated peer view, so the client carries NO identity configuration), obtains a signed leaf through the host-supplied `IWorkloadCertificateIssuer`, REJECTS a returned leaf whose public key does not match the local keypair (no cache write; the mismatch warning + reissue-failure counter fire; a still-valid cached leaf keeps serving), pairs the certificate with the LOCAL key, decodes the issuing intermediate, builds the presentable chain context, caches it, and serves-stale-on-transient (singleflight + circuit-breaker). **The leaf private key never crosses the issuer seam** — the port carries only the CSR (public by construction) and the returned `WorkloadLeafMaterial` carries only certificates.
- `WorkloadLeafRefreshHostedService` — polls every 30 s and reissues when `NotAfter - now <= WorkloadLeafRefreshLeadTime` (default 5 min; leaf TTLs are hours).

`AddD2WorkloadCertificate()` on the gRPC builder sets the channel handler's `SslClientAuthenticationOptions.ClientCertificateContext` to the cache's current chain context (the full `leaf → intermediate` chain) at channel build — presenting the chain lets a strict peer rebuild a root-anchored chain without a machine-store-resident intermediate or a network (AIA) fetch. It composes alongside `AddD2ForwardedJwt()` (the leaf chain is set on the channel handler's `SslOptions`; the forwarded token is set on `options.Credentials` — orthogonal, compose-don't-clobber on `options.HttpHandler`). Safe-by-default: a channel that does not call it presents no client certificate.

The chain context is resolved **once, at channel construction** (a `ClientCertificateContext` is not a per-connection selection callback). The refresh-ahead loop keeps the cache holding a current chain, but a consumer holding a long-lived channel does not automatically adopt a rotated leaf — it must rebuild the channel to present the freshly-rotated leaf. Rebuilding a long-lived channel on rotation is the consumer's responsibility; the channel's lifecycle is the natural home for it.

> **Platform note.** On Linux/OpenSSL (the deployment target) the chain context is always built and the full chain is presented. On Windows, Schannel builds the chain outside the process and refuses to construct a chain context for a leaf whose internal-CA root is not installed in the OS trust store (and cannot transmit an application-supplied intermediate without store residency — a documented Schannel limitation). On that path the leaf source caches no chain context and the per-channel opt-in falls back to presenting the bare leaf; a Windows host that needs the chain transmitted installs the CA into the OS store (operator action), which is outside this in-process presentation path. This mirrors the platform split already used for the leaf's private-key handling.

The `IWorkloadCertificateIssuer` port (`IssueAsync(byte[] csrDer, ct)` → cert-only `WorkloadLeafMaterial(CertificateDer, IssuerCertificateDer, NotAfter)`) is the host-supplied seam. KeyCustodian's server side of the contract is built and TestServer-proven (the `IssueWorkloadCertificate` gRPC method — CSR in, leaf + issuing intermediate out, SAN always the authenticated mTLS peer); the LIVE Edge-host adapter that dials it — plus the first-leaf bootstrap-identity provisioning — is tracked in [ADR-0023](../../../../../public/docs/adrs/0023-mtls-workload-identity.md) § Cross-process issuance. Until then, in-process / harness adapters prove the full refresh-ahead + presentation path locally.

### Propagated context — `PropagatedContextClientInterceptor` (the outbound half of ADR-0025)

```csharp
// Composition root (registers the interceptor singleton; idempotent):
services.AddD2PropagatedContextOutbound();

// Per-channel opt-in — the GENERATED gRPC-client DI extension AUTO-CHAINS this
// alongside .AddD2ForwardedJwt().AddD2WorkloadCertificate(); a host never calls
// it directly:
services
    .AddGrpcClient<FilesGrpc.FilesGrpcClient>(o => o.Address = new Uri("https://files.internal"))
    .AddD2PropagatedContext();
```

Per outbound RPC (all five client call shapes — blocking + async unary, client-streaming, server-streaming, duplex — route through one shared method, so a call shape cannot silently skip propagation), `PropagatedContextClientInterceptor` resolves the CURRENT inbound request's scope through the same framework-free `IAmbientRequestScopeAccessor` port the forwarded-JWT credential uses, reads that scope's `IRequestContext`, projects it via `ToPropagatedContext()` (which includes the accumulated `CallPath` the inbound establishment boundaries appended), and attaches the encoded `x-d2-context` header. Opportunistic, never required: no inbound scope, no request-context, or an empty projection means no header and no throw — a genuinely system-initiated call with no inbound request simply propagates nothing. A plain client interceptor (not `CallCredentials`) is used deliberately so it works on plaintext / loopback channels too — the call-path is non-secret operational telemetry, not a credential. `AddD2PropagatedContextOutbound()` registers ONLY the interceptor type; the `IAmbientRequestScopeAccessor` it depends on is supplied by whichever inbound transport the host uses (`AddD2AuthHttp()` / `AddD2AuthGrpc()`), mirroring `AddD2ForwardedJwtOutbound()`.

---

## File layout

```
auth/outbound/
├── AuthOutboundOptions.cs                            # config
├── AuthOutboundServiceCollectionExtensions.cs        # AddD2AuthOutbound + AddD2WorkloadCertificateOutbound + AddD2ForwardedJwtOutbound composition roots
├── Grpc/
│   ├── ForwardedJwtCallCredentials.cs                # per-request CallCredentials — reveals the request-scoped ForwardedJwt + attaches Bearer (the sole reveal caller)
│   ├── GrpcClientBuilderExtensions.cs                # .AddD2ForwardedJwt() (forwarded token) + .AddD2WorkloadCertificate() (leaf-chain context) per-channel opt-ins
│   ├── PropagatedContextClientInterceptor.cs         # per-call Interceptor — writes the x-d2-context header (propagated subset + call-path) on every outbound RPC
│   └── PropagatedContextOutboundExtensions.cs        # AddD2PropagatedContextOutbound() (host) + .AddD2PropagatedContext() (per-channel opt-in)
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

### Workload-leaf cache

Single per-process slot. Atomic reference swap of an immutable snapshot holding the live leaf `X509Certificate2`, its issuing intermediate, and the pre-built chain context — readers never observe a torn state, and there is no lock on the read path. No `ILocalCache` involvement (single value, no key namespace). The cache owns disposal of the certificate handles: the superseded leaf + intermediate are disposed on swap, the current pair on cache disposal.

The `WorkloadLeafRefreshHostedService` polls every 30 s and proactively reissues when `NotAfter - now <= WorkloadLeafRefreshLeadTime`. On reissue failure with a still-valid cached leaf, the warning logs but the existing leaf continues to be presented until it actually expires.

Concurrent first-callers (on-demand + the refresh hosted service) dedup to a single reissue via `Singleflight` from `DcsvIo.D2.Resilience`. Each reissue also passes through a `CircuitBreaker` (5 consecutive transient failures → 30 s open) — after the threshold, callers fast-fail without waiting for an issuer timeout, stopping the hammering of a down issuer.

### TokenExchange cache

Backed by the shared `ILocalCache` singleton (with a `tokenexchange:` key prefix), so the lib-wide `LocalCacheOptions.MaxEntries` ceiling applies (default 100k). Key shape: `tokenexchange:{sessionId}:{audience}:{scopeSetHash}`, where `scopeSetHash` is the first 16 hex chars of SHA-256 over the sorted comma-joined narrowed-scope names (or `_default` when no narrowing is requested).

`sessionId` comes from the inbound JWT's `d2_session_id` claim, parsed without re-validating the signature (the inbound auth middleware already validated upstream). This sessionId is the invalidation key — the cache subscribes to `ICacheInvalidationBackplane` for `session-revoked:{guid}` events and purges every cached exchange token bound to that session via a per-process `ConcurrentDictionary<sessionId, HashSet<cacheKey>>` reverse-index.

Concurrent first-callers for the same `(sessionId, audience, scope-set)` tuple dedup to a single HTTP fetch via `Singleflight`.

Edge unreachable on cache miss → `D2Result.ServiceUnavailable` (no graceful-degradation fallback — Edge being down means auth is down, and downstream services would reject anything we hand them anyway; pretending we have a working token by serving stale entries creates harder-to-debug failure modes than a fast fail). The `CircuitBreaker` (5 consecutive failures → 30 s open) stops the hammering: once the threshold is hit, callers receive `ServiceUnavailable` immediately without waiting for an HTTP timeout.

The backplane subscription is OPTIONAL. If `ICacheInvalidationBackplane` isn't registered, the cache logs a startup warning and falls back to TTL-only invalidation (acceptable for single-instance deployments; not for clusters that need cross-instance session-revoke propagation).

---

## Configuration

| Option                          | Env var                 | Default                  | Purpose                                                                           |
| ------------------------------- | ----------------------- | ------------------------ | --------------------------------------------------------------------------------- |
| `Issuer`                        | `D2_AUTH_ISSUER`        | (required)               | OIDC issuer URL — drives discovery doc fetch.                                     |
| `ClientId`                      | `D2_AUTH_CLIENT_ID`     | (required)               | This service's OAuth client id.                                                   |
| `ClientSecret`                  | `D2_AUTH_CLIENT_SECRET` | (required, NEVER logged) | This service's OAuth client secret.                                               |
| `WorkloadLeafRefreshLeadTime`   | —                       | 5 min                    | How early before expiry to proactively reissue the cached workload leaf.          |
| `HttpRequestTimeout`            | —                       | 5 s                      | Per-request timeout on outbound HTTP calls to Edge.                               |
| `TokenExchangeCacheKeyPrefix`   | —                       | `tokenexchange:`         | Prefix for token-exchange cache entries in the shared `ILocalCache`.              |
| `TokenExchangeCacheFallbackTtl` | —                       | 5 min                    | Fallback TTL when the OAuth response's `expires_in` is missing or unparseable.    |

---

## Telemetry

Tag-key + tag-value constants are emitted by [`DcsvIo.D2.Telemetry.Tags.SourceGen`](../../telemetry/tags-source-gen/README.md) into `OutboundTelemetryTags.g.cs` from [`contracts/telemetry/telemetry.spec.json`](../../../../../contracts/telemetry/telemetry.spec.json). Counter call sites reference `OutboundTelemetryTags.TokenExchangeRequests.Outcome.CACHE_HIT` / etc. instead of bare string literals. The emitted file lands in the tracked `Generated/` directory (committed for inspection, IDE navigation, and PR diff review; re-emitted on every `dotnet build`; do not hand-edit).

| Counter                                                  | Tags                                                                                                                                                                                          | Purpose                                                                                                                                             |
| -------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------- |
| `d2.auth.outbound.token_exchange.requests`               | `outcome` (`OutboundTelemetryTags.TokenExchangeRequests.Outcome.*`: `cache_hit` / `cache_hit_after_singleflight` / `fetch_success` / `fetch_failure` / `http_failure` / `discovery_failure`) | Token-exchange requests. One increment per `ExchangeAsync` call (input-validation failures aren't counted — caller bug, not auth-runtime traffic).  |
| `d2.auth.outbound.token_exchange.revoked_purges`         | —                                                                                                                                                                                             | Cache entries purged by session-revoked backplane events; one increment per purged key. Useful for verifying cluster-wide invalidation propagation. |
| `d2.auth.outbound.workload_leaf.reissue_failures`        | `leaf_expires_at` (ISO-8601 UTC not-after of the stale cached leaf, or `none` when no cached leaf exists)                                                                                    | Workload leaf reissue failures. One increment per failed `WorkloadLeafClient.ReissueAsync` call.                                                    |

`ActivitySource` and `Meter` both named `DcsvIo.D2.Auth.Outbound`. Hosts wire via `.AddSource(OutboundTelemetry.ACTIVITY_SOURCE_NAME)` / `.AddMeter(OutboundTelemetry.METER_NAME)`.

---

## Bootstrap order

The four outbound factors are independent composition roots — a host wires whichever it needs, in any order relative to each other. Cross-process workload identity is supplied by the mTLS channel (`AddD2WorkloadCertificateOutbound` + the refresh-ahead leaf), so a host's outbound calls to Edge (keyring / JWKS fetches) present a client certificate; the forwarded transaction-token is ambient on each request (no acquire step); propagated context is opportunistic and reads the same ambient scope. None of the four factors imposes a startup-ordering requirement on the inbound `DcsvIo.D2.Auth` lib.

---

## References

- [`DcsvIo.D2.Auth`](../core/README.md) — inbound auth runtime (JWT validator + session liveness + `AddD2Auth` composition root); transport bindings in `DcsvIo.D2.Auth.Http` + `DcsvIo.D2.Auth.Grpc` siblings
- [`DcsvIo.D2.Auth.Abstractions`](../abstractions/README.md) — `Audiences.*` / `JwtClaimTypes.*` constants + the `IForwardedJwtAccessor` holder + the `IAmbientRequestScopeAccessor` port
- [`DcsvIo.D2.Context.Abstractions`](../../context/abstractions/README.md) — `IRequestContext.ToPropagatedContext()`, the projection `PropagatedContextClientInterceptor` encodes onto the outbound header
- [`DcsvIo.D2.Caching.Abstractions`](../../caching/abstractions/README.md) — `ILocalCache` + `ICacheInvalidationBackplane` interfaces
- [`DcsvIo.D2.Resilience`](../../resilience/README.md) — `Singleflight` for fetch-path deduplication + `CircuitBreaker` to fast-fail during sustained outage
- [ADR-0022](../../../../../public/docs/adrs/0022-service-auth-mint-once-forward.md) — mint-once-at-the-Edge, forward-unchanged service-to-service model; token exchange repurposed to the boundary mint + exceptions
- [ADR-0023](../../../../../public/docs/adrs/0023-mtls-workload-identity.md) — mTLS workload identity for cross-process hops
- [ADR-0025](../../../../../public/docs/adrs/0025-request-context-establishment.md) — `Origin` / `ImmediateCaller` / `CallPath` establishment model; `PropagatedContextClientInterceptor` is the outbound half that carries `CallPath` across gRPC hops
- [RFC 8693](https://datatracker.ietf.org/doc/html/rfc8693) — token-exchange grant (the boundary mint + the exception cases)
