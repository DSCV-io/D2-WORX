<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->


> **Visibility: PUBLIC** — ships with the open surface (`public/`).  
> Do not add product IP, private paths, or non-exportable runbooks.
# ADR-0022: Service-to-service auth — mint once at the Edge, forward the token unchanged

- **Status**: Accepted
- **Date**: 2026-06-17 (call-path closure: 2026-06-30; CSR port / mesh-member amendment: 2026-07-02)
- **Deliverable**: 0021-auth-pivot; call-path closure: 0026-kc-crypto-surface; CSR port / mesh-member amendment: 0026-kc-crypto-surface

## Context

A request that enters at the Edge fans out into a chain of internal calls: Edge calls a service, that service calls another, an operation depends on a downstream operation. Every hop in that chain needs the same two answers the entry point had — *who is acting* (the user identity, the impersonation chain) and *what is this caller allowed to do* (the authorized scopes). The question this decision settles is how that identity-plus-authorization travels from the trust boundary down to the deepest hop.

The model the codebase had leaned toward — without ever wiring it — was **per-hop token exchange**. Under that model every backend hop performs an RFC 8693 token exchange: it takes the bearer token it received, calls back to the issuer, and gets a *re-minted* token narrowed to the next hop's audience and the next hop's reduced scope set, then forwards that. The appeal is textbook least privilege: each hop holds a token scoped to exactly what it needs and addressed to exactly the service it calls, so a leaked downstream token grants the minimum.

That model is the wrong default here, for three reasons:

- **It puts a synchronous mint on the request path.** Every hop blocks on a round-trip to the issuer to acquire its narrowed token before it can make its outbound call. On a chain of depth N that is N issuer round-trips serialized into the user-facing latency of a single request — a cost the request path pays on every call, forever, to buy an attenuation the deployment does not currently need.
- **It couples every hop to issuer availability.** A hop cannot call downstream until the issuer mints its token, so the issuer becomes a hard, in-line dependency of every internal call rather than a once-per-request boundary dependency. An issuer hiccup degrades the whole internal call graph, not just the front door.
- **It was never actually wired.** The outbound token-exchange and service-identity clients exist as library types, but their only callers are unit tests — no production request flow invokes them. The Edge token-issuer endpoint that would mint and re-mint these tokens is not built; the Edge service's host, application, domain, and infrastructure projects are placeholder-only. End-to-end cross-service auth runs nowhere in the system today. There is no working per-hop-exchange path to preserve.

This decision is therefore about an **unfinished design**, not a rewrite of working code. Choosing against per-hop exchange drops a path that was sketched and partially stubbed but never carried a single real request — the migration risk is the cost of deleting unreached stubs, not the cost of re-architecting a live data flow.

The inbound half of the picture is already settled and already correct. [ADR-0007](0007-request-context-propagation.md) established that **every sync hop rebuilds full identity from the bearer JWT and propagates only a small operational subset** (request id, idempotency key, fingerprints, risk score, locale — the data a downstream service genuinely cannot recompute, none of it bearer identity) over the `x-d2-context` header. Identity is never carried as a plaintext header; it is reconstructed at each hop from a signed token the hop independently re-validates. That context model is the foundation this decision builds on — the only open question it leaves is *which* token each hop validates, and whether that token is freshly minted per hop or minted once and forwarded.

## Decision

Identity and authorization travel downstream as **a single transaction-token, minted once at the Edge trust boundary and forwarded unchanged across every cross-process hop**, with each hop independently re-validating it and **mTLS authenticating the calling workload** as an additive second factor. RFC 8693 token exchange is retained for the boundary mint and a handful of deliberate exceptions, but it is not the per-hop business-call mechanism.

### Edge mints exactly one internal transaction-token

The Edge service is the token issuer — it holds the RS256 signing key (managed through KeyCustodian) and is the sole place identity crosses from the untrusted outside into the internal trust domain. At that boundary Edge validates the incoming cookie or edge-facing token and **mints exactly one internal transaction-token** for the request:

- **`aud=d2.internal`** — a single, broad internal audience that every internal service accepts. This value is a hand-declared well-known constant, `WellKnownAudiences.D2_INTERNAL_AUDIENCE` (value `"d2.internal"`) in `DcsvIo.D2.Auth.Abstractions` — never a raw literal scattered through the code — so the mint side and every hop's audience check read it from the same source. It is deliberately **not** an entry in the audiences contract that backs the per-service target audiences: those entries are token-exchange *targets* (a call exchanges *to* one of them), whereas `d2.internal` is the universal *receive* audience every hop validates and is never an exchange target. It is a special-case protocol constant of the same kind as the existing `"d2-edge"` special-case (the TypeScript-side audience that the `@d2Audience` validator exempts from spec lookup — `typespec-decorators/src/validators.ts:151-152`), not a spec-derived per-service audience. `D2_INTERNAL_AUDIENCE` is the first such hand-declared audience constant on the C# side.
- **`scope`** — the union of scopes the request needs across its entire downstream fan-out.
- **`act`** — an impersonation actor chain, present only when the request is acting on behalf of another subject.

This is the only mint on the request path. Every subsequent hop receives this token; no hop mints another for a normal business call.

### Each hop forwards the token unchanged and re-validates it

The transaction-token is **forwarded byte-for-byte** across every cross-process hop (Edge → service A → service B → …). Each receiving hop independently **re-validates** the token it receives before any handler runs:

- RS256 signature against the cached JWKS public key (with reactive refresh on an unknown signing-key id),
- `iss` (the Edge issuer),
- `aud == d2.internal`,
- `exp` / `nbf` lifetime, with the configured clock skew,
- the RS256 algorithm pin (defending `alg=none` and HMAC-with-public-key confusion),
- session liveness (fail-closed), and
- the receiving operation's own required scopes.

**Fine-grained authorization is by scope, evaluated per operation at every hop — never by audience.** The audience answers only "is this token meant for the internal trust domain"; it is deliberately not the authorization axis. Each operation declares the scopes it requires and enforces them locally against the forwarded token's scope set, so the same broad-audience token is authorized differently at every hop according to what that hop's operation demands.

**WHY forward-unchanged rather than re-mint:** the token is already a signed, short-lived, independently-verifiable assertion of identity and authorization; re-minting it at each hop buys nothing the receiving hop cannot establish for itself by validating signature, audience, lifetime, liveness, and its own scopes. The thing re-minting would add — a per-hop *narrowed* audience and scope set — is replaced by per-operation scope enforcement (each hop already rejects a token lacking its scopes) plus mTLS workload authentication (each hop already knows which workload called it). Forwarding keeps the trust anchor on the issuer's signature at every boundary while removing the per-hop mint round-trip and the per-hop issuer dependency.

### mTLS authenticates the calling workload — additive, never a validation skip

Every cross-process hop runs over mutually-authenticated TLS: the caller presents a client certificate, the callee presents a server certificate, and each verifies the other against the internal certificate authority. This establishes **workload identity** (which service made this call) and an authenticated, encrypted channel, as defense-in-depth layered on top of the JWT.

mTLS is **strictly additive — it is not a reason to skip JWT re-validation**. Every hop still performs the full token validation above; the mTLS client identity is an *additional* fact the hop learns, not a substitute for verifying the bearer assertion. JWT-plus-mTLS is strictly stronger than JWT alone: an attacker now needs both a valid signed token *and* a trusted workload certificate to make an accepted internal call, and the workload identity is established by the channel rather than asserted in a forgeable header. The mTLS certificate-authority and issuance subsystem is specified in [ADR-0023](0023-mtls-workload-identity.md); this decision depends on it for the workload-authentication factor.

### In-process module calls pass the validated context directly

Modules hosted inside the Edge process (KeyCustodian, the auth module) are reached through an in-process façade, not a network hop. A call from the host into one of these modules **passes the already-validated request context directly** — there is no wire token to mint or forward and no mTLS handshake, because there is no process boundary and no untrusted channel between caller and callee. The token-forward-and-re-validate discipline applies to cross-process hops; an in-process call has already cleared the boundary the discipline guards. [ADR-0025](0025-request-context-establishment.md) names this boundary explicitly (`Origin = InProcessModule`) so a capability authority can distinguish a genuine in-process call from a request that merely became in-process downstream of an external call — the distinction the original in-process-only signing rule lacked.

### Asynchronous hops are unchanged

Asynchronous (message-broker) hops do not carry a JWT. The operational context rides **encrypted inside the message frame**, and the encryption boundary is the trust boundary — a consumer that can decrypt the frame is, by construction, inside the trust domain. This is the model [ADR-0007](0007-request-context-propagation.md) already establishes for AMQP (consumers receive no bearer token and reconstruct no identity; they decode only the operational subset), and this decision leaves it untouched. The mint-once-and-forward rule governs synchronous request/response hops; the asynchronous path has its own, already-settled trust model.

### Every cross-process hop appends to and logs a service call-path

Each cross-process hop **appends its own service identity and a timestamp** to a propagated **call-path** field, and **logs that field on receipt**. The result is that the chain of services a request traversed is recoverable from the logs of any hop in the chain — even when a trace span is dropped, broken, or never sampled, the call-path reconstructs "this request entered at Edge, went through A, then B, then reached me."

The call-path lives in the **propagated operational context, never on the signed token.** The token is immutable in flight (mutating it would invalidate its signature, and the forward-unchanged rule depends on it staying byte-identical), so a field that every hop appends to cannot live there; it belongs with the other operational data that propagates alongside the token. The call-path is **depth-bounded** (a request cannot grow it without limit) and is distinct from the impersonation `act` chain — the `act` chain records *on whose behalf* a subject acts and is part of identity, whereas the call-path records *which workloads handled the request* and is operational telemetry.

**Closed by [ADR-0025](0025-request-context-establishment.md).** The wire field, its encoding, and the depth bound — left open here — are now concrete: `CallPath` (`IReadOnlyList<CallPathEntry>`, one `{id, kind, timestamp}` entry per hop) rides the same `x-d2-context` header as the rest of the propagated subset, and every hop that can originate or receive a request — the Edge HTTP boundary, a cross-process gRPC hop, an in-process module call, and an in-host background worker — appends its own entry through a dedicated establishment boundary before dispatching. ADR-0025 also introduces `Origin` and `ImmediateCaller`, two **non-propagated** companion facts a capability authority can use where the call-path itself structurally cannot (the call-path is telemetry; authority never reads it).

### The build statically verifies scope consistency across declared call edges

Where an operation declares the downstream operations it calls, the build **statically verifies that the caller's required scopes are a superset of each callee's required scopes.** For every declared A-calls-B edge, A's required scope set must contain B's required scope set; a violation fails the build.

**WHY a build-time check:** under forward-unchanged, the single token A holds is the same token B will validate. If B requires a scope that A's token does not carry, the call reaches B and is rejected at B's scope check — a runtime failure on a path that a narrower per-hop re-mint would have surfaced differently. Verifying caller-scopes ⊇ callee-scopes at build time makes the forward model **provably safe by construction**: a forwarded token can never arrive at a hop whose scope it lacks, because such a call graph does not compile. The annotation that declares call edges and the codegen that enforces the superset rule are left to the deliverable that implements them.

### RFC 8693 token exchange is retained, repurposed

Token exchange is not removed — it is moved off the per-hop business-call path and reserved for the cases that genuinely need a fresh or transformed token:

- the **single boundary mint** at Edge (the one mint per request described above),
- **cross-trust-domain calls** — a call leaving the `d2.internal` domain for an external or differently-trusted audience needs a token minted for that audience,
- **deliberate narrowing exceptions** — a specific, justified case that wants to hand downstream a token narrower than the request's union,
- **asynchronous scope reduction** — reducing the authority that rides an async message below the request's full scope set, and
- **impersonation** — establishing or extending the `act` chain.

What changes is only the *default*: an ordinary internal hop does **not** exchange; it forwards. Exchange becomes the explicit, exceptional tool rather than the implicit per-hop tax.

### Realization: forwarded-JWT credential mechanism

This is the in-process realization of the forward-unchanged rule above — how a hop *holds* the forwarded transaction-token and *re-attaches* it outbound. A hop holds the token in a **dedicated request-scoped holder**, wrapped in a **redacting value type that is unloggable by construction**, and re-attaches it on outbound gRPC via a **per-request `CallCredentials` interceptor that resolves the holder at call time**. The forwarded user-token and mTLS workload identity are orthogonal rails that compose on a channel; neither rescues the other.

Three forces shape the mechanism. **The JWT must be replayed verbatim, not reconstructed** — a hop that rebuilt a token from the claims it parsed would produce a different, unsigned-by-Edge token that every downstream hop would reject, so a hop that will forward MUST retain the original bearer bytes. **A retained raw credential is a logging-leak hazard** — a live bearer is exactly the kind of value that leaks if it rides on a broadly-projected object (the `IRequestContext` is projected into a wide Serilog enrichment and an OTel tag set; any field added there is emitted on every request), so the credential must be structurally excluded from that projection. **gRPC `CallCredentials` are per-channel singletons, but the token is per-request** — under forward-unchanged the token is the current request's user token, different on every concurrent request sharing a long-lived channel, so a credential that captured a token at channel-build time would forward the wrong user's token to every subsequent request.

#### Inbound: capture the raw bearer in a request-scoped holder, isolated from the enriched context

After the auth surface validates the inbound bearer (HTTP middleware and gRPC interceptor alike), it stores the **raw bearer string** in a dedicated request-scoped holder (`IForwardedJwtAccessor`) — separately from the `IRequestContext` it already builds. The holder is the only place the raw JWT survives validation. It is deliberately **not** a property of `IRequestContext`: the enriched context is a broadly-projected log/telemetry surface, and the forwarded JWT must be structurally excluded from that projection so its non-logging cannot regress through an unrelated enricher change. The two are different types with different DI registrations; a structural test asserts the enrichment field-set contains no forwarded-JWT field.

#### The credential is a redacting wrapper, unloggable by construction

The JWT is wrapped in a value type (`ForwardedJwt`) whose non-logging is structural, reinforced by four layers: it is **self-redacting** (`ToString()` and any default serialization yield a constant placeholder, never the bytes; the type carries `[RedactData]`; it exposes no public raw accessor); it has a **single reveal seam** (the raw bytes are reachable only via one explicit method, `RevealForForwarding()`, whose sole production caller is the outbound forwarding credential); it is **enrichment-isolated** (held off the projected context, above); and it is **never a log parameter** (no `[LoggerMessage]` delegate takes the type). The guarantee is proven, not asserted — a `ToString()`-redacts test, a field-set-exclusion structural test, a log-capture test across a capture→forward cycle asserting the bytes never surface, and the two scans (sole-reveal-caller, no-log-delegate-parameter) — the same proof shape as the mTLS log-delegate contract guard. An existing sensitive-value wrapper is reused if one satisfies these properties; the type is built net-new only if none does.

#### Outbound: a per-request `CallCredentials` that resolves the holder at call time

Outbound gRPC re-attaches the forwarded JWT via `CallCredentials.FromInterceptor`, where the interceptor **resolves the request-scoped holder on each call** and attaches `Authorization: Bearer <revealed bytes>` — it does not capture a JWT at channel construction. This is the load-bearing distinction: because `CallCredentials` is a per-channel singleton but the holder is request-scoped, one long-lived channel correctly forwards each concurrent request's own JWT. A small client interceptor carries the operational call-path / telemetry rail, which rides the operational context, never the immutable JWT. An absent JWT on an internal hop hard-fails (`RpcException(StatusCode.Unauthenticated)`) rather than silently sending no header; genuinely system-initiated calls (e.g. future scheduled jobs with no inbound user request) carry their own identity and are handled when they exist. The per-channel chain `.AddD2ForwardedJwt().AddD2WorkloadCertificate()` composes the forwarding `CallCredentials` with channel-level mTLS on a compose-don't-clobber basis: the leaf chain sits on the channel handler's `SslOptions`, the forwarded JWT on `options.Credentials`, orthogonal.

#### Composition: the gRPC-client emitter auto-wires the outbound auth; the host supplies only un-inventable config

The chain is **not** a per-client host opt-in. The gRPC-client emitter **auto-wires** it: the generated `<Module>GrpcClientsGenerated.g.cs` DI extension emits `.AddD2ForwardedJwt().AddD2WorkloadCertificate()` on every generated internal gRPC client, so a host can never forget to attach the outbound auth on a generated client. The host's only residual setup is a one-time config registration — `AddD2ForwardedJwtOutbound()` + `AddD2WorkloadCertificateOutbound()` — supplying what the emitter cannot invent: CA trust anchors, the issuer endpoint, and the JWT-holder / `CallCredentials` services the auto-wired chain resolves. Auto-wiring a security-critical cross-cutting that every generated client needs makes the safe path the only path, in place of a forgettable docstring instruction to chain it manually.

#### The internal audience is a hand-declared constant; the `client_credentials` service-identity surface is retired

The forwarded token's `aud` is `WellKnownAudiences.D2_INTERNAL_AUDIENCE` (value `"d2.internal"`) — a hand-declared constant in `DcsvIo.D2.Auth.Abstractions`, the universal *receive* audience every hop validates. It is deliberately **not** an entry in `audiences.spec.json` (whose entries are token-exchange *targets*), so it is a protocol constant, not a spec-mirror DTO.

The per-service `client_credentials` service-identity surface (`IServiceIdentityClient`, `HttpServiceIdentityClient`, `ServiceIdentityCallCredentials`, `AddD2ServiceIdentity`, the refresh hosted service, cache, snapshot, exception) is removed: its workload-identity role is mTLS's ([ADR-0023](0023-mtls-workload-identity.md)) and its user-identity role is the forwarded transaction-token's. The RFC 8693 token-exchange surface (`ITokenExchangeClient`) is retained, repurposed to the boundary mint and the deliberate exceptions above; the shared OIDC-discovery + `ClientId`/`ClientSecret` options it depends on are kept. The BFF's TypeScript `client_credentials` boundary token (audience `d2.edge`, the `X-D2-Internal-Token` BFF↔Edge rail) is a legitimate *external* client of Edge and is unaffected.

Realizing the credential mechanism carries net-new consequences that fold into the operational picture below: the request-scoped holder makes a live bearer reachable in-process for the request's lifetime, so the structural guarantees and their tests are the reason this is safe, not optional polish — any new log/serialization surface must respect them. Reaching the request-scoped holder from inside a `CallCredentials` interceptor (which runs outside the normal DI request-scope ambient flow) is the highest-uncertainty mechanic and may need an ambient accessor or scope capture at call initiation. As with the mTLS factor, the reusable plumbing is built and unit/loopback-proven ahead of a running Edge gRPC host; wiring it into an Edge host follows when that host exists.

## Consequences

**Positive.**

- **Near-zero migration risk.** The path being dropped — per-hop exchange — carried no real traffic; its clients had only test callers and its issuer endpoint was never built. Choosing forward-unchanged deletes unreached stubs rather than re-architecting a live flow.
- **JWT-plus-mTLS is strictly stronger than JWT alone.** An accepted internal call now requires both a valid signed transaction-token and a trusted workload certificate; workload identity is established by the authenticated channel instead of asserted in a forgeable header.
- **No per-hop mint latency and no per-hop issuer coupling on the request path.** Identity crosses the issuer exactly once per request, at the boundary; downstream hops validate locally against cached JWKS and never block on the issuer to acquire a token.
- **One short token TTL bounds the entire downstream chain's revocation lag.** Because the same token is forwarded the whole way down, its expiry caps how long any hop in the chain will keep honoring it after a session is revoked — there is no fan-out of independently-lived re-minted tokens to reason about.

**Negative / new work.**

- **mTLS certificate issuance is a genuinely new subsystem.** No X.509 / certificate-authority capability exists in the system today — KeyCustodian manages only asymmetric signing keys, symmetric payload-encryption keys, and secrets, and the dev key-generation tooling produces only a symmetric root key, with no CSR, CA, or certificate surface anywhere. The workload-identity factor therefore requires building a new PKI capability into KeyCustodian ([ADR-0023](0023-mtls-workload-identity.md)); it is not a configuration of something already present.
- **Least privilege is enforced locally, not by per-hop narrowing.** The forwarded token carries the union of the request's scopes the whole way down, so a deep hop's token is broader than that hop strictly needs. The guarantee that this is safe comes from per-operation scope enforcement at every hop plus the build-time superset check, not from each hop holding a minimally-scoped token. This is an accepted trade: the request path is cheaper and simpler, at the cost of a forwarded token that is wider than a per-hop-narrowed one would be.
- **The forwarded-token audience must be the broad internal audience.** A per-service audience is incompatible with forwarding unchanged — a token addressed to one service would fail the audience check at the next — so the audience is `d2.internal` for the whole internal domain, and fine-grained authorization is carried entirely by scope. A per-service audience would require exactly the per-hop re-mint this decision removes.
- **Operational-subset propagation on synchronous .NET-to-.NET hops — closed by [ADR-0025](0025-request-context-establishment.md).** This was originally a gap: the synchronous .NET transport middleware built context from JWT claims only and did not read or forward the propagated operational subset on those hops. It is now wired both directions — an outbound gRPC client interceptor writes `x-d2-context` (operational subset plus the accumulated call-path) on every outbound call, and the inbound gRPC server interceptor applies it before establishing the receiving hop's own `Origin`/`ImmediateCaller`/`CallPath` entry — proven end-to-end over an in-memory two-process `TestServer` harness. What remains is not the machinery but the **live Edge host** registering these interceptors on a running pipeline, tracked as its own build dependency (a host, not a code follow-up).

## Alternatives considered

**Per-hop RFC 8693 token exchange (the model leaned toward, now rejected as the default).** Re-mint a narrowed-audience, reduced-scope token at every backend hop. Rejected as the per-hop default: it places a synchronous issuer round-trip on the request path at every hop (a latency anti-pattern that scales with call-graph depth), makes the issuer an in-line dependency of every internal call rather than a once-per-request boundary dependency, was never actually wired (test-only callers, unbuilt issuer endpoint), and adds standing complexity to buy an attenuation the deployment does not currently need. It is retained only for the boundary mint and the deliberate exceptions enumerated in the Decision.

**Per-service audience via per-hop re-mint.** Give each service its own audience so a leaked token is addressed only to one service. Rejected: it is fundamentally incompatible with forwarding a token unchanged — a token minted with one service's audience fails the audience check at the next hop — so it can only be achieved by re-minting at every hop, which is the per-hop-exchange cost above under a different name. The broad internal audience plus per-operation scope enforcement plus mTLS workload identity gives equivalent containment without the per-hop mint.

**Transport-trust fast path — mTLS plus a trusted plaintext identity header, skipping per-hop JWT validation.** Pass identity in a header that internal services accept *because* the channel is mTLS-authenticated, skipping per-hop token re-validation entirely. This is the variant [ADR-0007](0007-request-context-propagation.md) already weighs and rejects, and that rejection still stands here: it moves the trust anchor from the token (cryptographically bound to the issuer's signature at every boundary) to the network, so one compromised or misconfigured internal workload could forge any identity, and it puts identity material in plaintext headers at rest. This decision's use of mTLS is the opposite of that fast path: **mTLS is additive and every hop still re-validates the JWT.** As ADR-0007 puts it, identity-via-JWT and the operational subset are both still *propagated* — what is declined is *trusting* a propagated identity *without re-verifying its token*. Forwarding the token unchanged and re-validating it at every hop preserves exactly that zero-trust property; mTLS only adds workload authentication on top.

**Offline attenuation with Biscuit / macaroon-style tokens.** Tokens that support cryptographic *attenuation* — a holder can derive a strictly-narrower token offline, with no issuer round-trip — would allow per-hop scope narrowing without reintroducing a per-hop mint. Not adopted now: the forward-unchanged model with per-operation scope enforcement meets the current requirement, and Biscuit would introduce a second token format and verification stack. It is noted as the **upgrade path** if per-hop scope *attenuation* ever becomes a hard requirement — it buys the narrowing of per-hop exchange without the latency of per-hop exchange.

## Amendment — 2026-07-02: the certificate-issuer port carries a CSR, and the BFF is a mesh-member workload

Two items catch this decision up to what shipped in the mTLS certificate-issuance work. Neither touches the mint-once-forward mechanism itself (the boundary mint, the byte-for-byte forward, the per-hop re-validation, and the request-scoped forwarded-JWT holder are all unchanged).

### 1. The consumer certificate-issuer port carries a CSR, not key-bearing material

The consumer-side port a workload uses to obtain its leaf — `IWorkloadCertificateIssuer` (`public/packages/dotnet/auth/outbound/WorkloadCertificate/IWorkloadCertificateIssuer.cs`) — takes a **DER-encoded PKCS#10 certificate-signing request** and returns the **leaf plus the issuing intermediate** as public DER (`WorkloadLeafMaterial`), rather than a parameterless call returning key-bearing material. The workload (`WorkloadLeafClient`) generates a **fresh keypair per rotation**, builds the CSR, obtains the signed leaf, and **verifies the returned certificate pairs with its local key** — a leaf certifying a different key can never be presented and is rejected before any cache write. The leaf private key never crosses the seam. This is the mint-once-forward model's consumer realization of the CSR-custody shape [ADR-0023](0023-mtls-workload-identity.md)'s 2026-07-02 amendment establishes; the certificate-authority and issuance mechanics are specified there.

### 2. The SvelteKit BFF is a mesh-member workload

The subsection above ("The internal audience is a hand-declared constant; the `client_credentials` service-identity surface is retired") closes with:

> The BFF's TypeScript `client_credentials` boundary token (audience `d2.edge`, the `X-D2-Internal-Token` BFF↔Edge rail) is a legitimate *external* client of Edge and is unaffected.

That clause is **superseded** by the 2026-07-03 mesh-member ruling. The SvelteKit BFF is a **privileged backend workload — a mesh member**, not an external client of Edge: it holds a KC-issued mTLS leaf (its issuance path is the Node `WorkloadLeafClient` twin), makes **direct calls to internal services**, and **forwards the Edge-minted transaction token unchanged** per this decision as the **first internal hop**. The boundary `client_credentials` / external-client-of-Edge model for the BFF is retired; the `oauth_client` boundary-token model survives only for genuinely-external clients. The full workload-identity establishment and the BFF's least-privilege posture (its leaf is all it holds — zero KeyCustodian grants) are in [ADR-0023](0023-mtls-workload-identity.md)'s amendment, amended in lockstep with this one.

## References

- [ADR-0007](0007-request-context-propagation.md) — the request/auth context model this decision builds on: rebuild identity from the JWT at every hop, propagate only the operational subset over `x-d2-context`, and the prior rejection of the transport-trust fast path that this decision's additive-mTLS use deliberately does not reactivate.
- [ADR-0023](0023-mtls-workload-identity.md) — the mTLS workload-identity decision and the certificate-authority / issuance subsystem the workload-authentication factor here depends on.
- [ADR-0012](0012-self-rolled-dotnet-auth.md) — the self-rolled .NET auth surface: the inbound validation stack every hop re-validates with, the RS256 + JWKS choice, the `d2_`-prefixed claim and scope vocabulary, and the outbound token-exchange client this decision repurposes off the per-hop path.
- [ADR-0021](0021-unified-operation-contract-idl.md) — the unified operation-contract IDL whose codegen generates the forward-unchanged client this model relies on: `@d2ServedBy` emits the forwarding edge route plus its cross-process gRPC client or in-process leaf, and the build-time caller-scopes-superset-of-callee-scopes check and the propagated call-path field this decision describes are additive emitter outputs of that contract.
- [ADR-0025](0025-request-context-establishment.md) — the call-path's concrete wire shape and its five establishment boundaries (closing the "not yet wired" consequence above), plus the non-propagated `Origin`/`ImmediateCaller` facts that give a capability authority what the call-path itself cannot safely provide.
- IETF OAuth Transaction Tokens — `draft-ietf-oauth-transaction-tokens` (informative): the industry pattern of minting one short-lived transaction token at the trust boundary and presenting it across the internal call chain that this decision mirrors.
