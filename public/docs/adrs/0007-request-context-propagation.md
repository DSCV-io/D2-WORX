<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->


> **Visibility: PUBLIC** — ships with the open surface (`public/`).  
> Do not add product IP, private paths, or non-exportable runbooks.
# ADR-0007: Request/auth context — spec-driven `IRequestContext`/`IAuthContext`, rebuild-from-JWT each hop, `x-d2-context` propagation

- **Status**: Accepted
- **Date**: 2026-05-30 (mTLS-framing reframe: 2026-06-18; establishment-fields amendment: 2026-06-30)
- **Deliverable**: D2 shared libraries (backfilled); establishment fields: 0026-kc-crypto-surface

## Context

Every handler, repository, and middleware needs answers to three questions about the in-flight request: *who is acting* (identity, impersonation, scopes), *what transport delivered it* (tracing, fingerprints, geo, entitlements), and *what did Edge observe that downstream services cannot recompute* (risk score, session fingerprint, idempotency key). Several forces shaped the design:

- D2 spans two runtimes — the .NET services (Edge, the public ingress, is itself .NET) and the SvelteKit (TypeScript) BFF. Context definitions must be authoritative in one place and derivable in both runtimes without hand-maintaining parallel types.
- Services communicate over HTTP, gRPC, and AMQP. Context encoding must be transport-agnostic.
- Propagating full identity across hops via headers would put plaintext `UserId`/`OrgId`/`Scopes` in RabbitMQ header frames at-rest — the broker becomes a blind carrier of credential material. Unacceptable under the threat model.
- Domain-layer code must read caller identity without pulling in ASP.NET Core or DI, so the domain assembly stays testable in isolation.
- The RFC 8693 §2.1 `act` claim is a recursive JSON object; impersonation introduces additional required sub-claims. The parsing rules are specific enough that silent degraded-mode fallbacks would mask upstream mint bugs.

The spec-driven codegen decision (ADR-0002) and the abstractions/implementation split (ADR-0006) both apply directly here.

## Decision

Three interlocking choices, read as a unit.

### 1. Context interfaces are spec-driven and codegen-emitted

`public/contracts/auth-context/IAuthContext.spec.json` and `public/contracts/request-context/IRequestContext.spec.json` are the single source of truth for every property: name, type, JWT claim name, propagation flag (`propagate: true/false`), per-field wire-length cap (`maxLength`), PII redaction annotation (`redact: true`), and doc comment.

The Roslyn generator `DcsvIo.D2.Context.SourceGen` reads both specs via `AdditionalFiles` and dispatches by consuming assembly: into `DcsvIo.D2.AuthContext.Abstractions` it emits `IAuthContext.g.cs`; into `DcsvIo.D2.Context.Abstractions` it emits `IRequestContext.g.cs` (which extends `IAuthContext` per the spec), `MutableRequestContext.g.cs` (the settable concrete, with `FromClaims` / `FromJwtPayloadNoValidation` factories), `PropagatedContext.g.cs`, `PropagatedContextExtensions.g.cs`, and `PropagatedContextSerializer.g.cs`. Build-time diagnostics (`D2CTX*`) surface spec violations at build (most as errors; `D2CTX005` is a warning).

The wire encoding used by `PropagatedContextSerializer` — canonical JSON (camelCase, omit-null, no whitespace) → UTF-8 → base64url — is language-neutral; the SvelteKit (TypeScript) BFF consumes the same field names and `propagate`/`maxLength`/`redact` annotations from the same specs. Two parsing helpers are hand-written (stable RFC text, not spec-variable behavior): `ActorChainParser` (RFC 8693 §2.1, strict mode, depth limit, throws `MalformedActorChainException` on any structural failure) and `ScopeClaimParser` (RFC 6749 §3.3, space-separator grammar, defensive JSON-array path).

### 2. Full identity rebuilds from the JWT at every hop; only a small operational subset propagates

Every sync hop (HTTP, gRPC) carries a bearer JWT. The receiving service's auth middleware validates it (signature, expiry, audience, issuer, session liveness) and calls `MutableRequestContext.FromClaims` to populate all identity fields — `UserId`, `OrgId`, `OrgType`, `OrgRole`, `Scopes`, `ActorChain`, `SessionId`, token timestamps, `AuthMethod`, etc. — fresh from the JWT. **None of these appears in `PropagatedContext`.**

`PropagatedContext` contains only the fields whose `propagate: true` annotation appears in the request-context spec — operational data a downstream service genuinely cannot recompute (`RequestId`, `RequestPath`, `RequestStartedAt`, `IdempotencyKey`, session/current fingerprints, `RiskScore`, `EdgeNodeId`, locale/timezone/currency, `OrgPlanTier`, feature flags, `WhoIsHashId`) — none of which is bearer identity. `PropagatedContextSerializer` enforces a global header cap plus per-field caps baked from the spec's `maxLength`; `TryDecode` returns `null` on any failure path — propagation is opportunistic, never required. The header name is `x-d2-context` on all transports. AMQP consumers receive no JWT and do not reconstruct identity — they decode only the operational subset.

### 3. The domain-safe `IAuthContext` slice

`IAuthContext` (namespace `DcsvIo.D2.AuthContext.Abstractions`) is a read-only identity/org/impersonation/scopes interface with no dependency on ASP.NET Core or `HttpContext`. Domain assemblies reference only `DcsvIo.D2.AuthContext.Abstractions` + `DcsvIo.D2.Auth.Abstractions` (vocabulary: `ActorEntry`, `OrgType`, `Role`, `ImpersonationKind`). `IRequestContext` extends `IAuthContext` with transport fields; only transport middleware and integration code reference `DcsvIo.D2.Context.Abstractions`. Hand-written `IAuthContextExtensions` (`HasScope`, `IsStaff`, `IsForcedImpersonation`, …) provide the domain predicate surface — stable domain logic, not spec-variable, so not generated. `ActorChainParser` strict mode deliberately throws on any malformed `act` claim (auth middleware catches it → 401) rather than returning a degraded result: a signed token with a malformed actor chain is a broken upstream mint condition.

### 4. Establishment fields — `Origin` / `ImmediateCaller` / `CallPath` (added by [ADR-0025](0025-request-context-establishment.md))

`IRequestContext` gains a fourth spec section, `Establishment`, folding in three fields every trust boundary populates fresh on its own request context: `Origin` (`RequestOrigin`, non-nullable — which kind of boundary produced this hop's context: the Edge external ingress, a cross-process mTLS-authenticated hop, an in-process module call, or an in-host system worker), `ImmediateCaller` (`string?` — who called this hop, sourced from the validated mTLS client certificate on a cross-process hop or the calling module's own id in-process), and `CallPath` (`IReadOnlyList<CallPathEntry>`, `propagate: true`, depth-bounded — the accumulated sequence of hops the request has traversed).

`Origin` and `ImmediateCaller` are deliberately **not** `propagate: true` — the generator's `propagate`-gated filter (the same mechanism that already keeps full identity out of `PropagatedContext`, above) structurally excludes both from ever reaching the wire; every boundary recomputes them locally instead of trusting an inbound value. `CallPath` is the opposite shape: it is the first `propagate: true` list-of-records field the spec has emitted (every field propagated before it was a scalar), and it is operational telemetry only — no authority decision anywhere reads it. This is the same rebuild-fresh-per-hop discipline this ADR already applies to identity, extended to a second, narrower class of fact (which kind of boundary, not which user) that a capability authority needs and that must be at least as unforgeable as identity itself.

Full design, the five establishment boundaries (one per transport plus outbound propagation), and the authority model this enables are in [ADR-0025](0025-request-context-establishment.md).

## Consequences

**Positive.**

- A one-line spec change updates the .NET interface, the mutable concrete + JWT factories, the propagated subset, the wire-length validator, and (via the same spec consumed by TS codegen) the TS redact paths. Cross-language drift is a build error, not a review artifact.
- Full identity is never in headers at-rest or in-flight except as a signed JWT blob. Brokers, gRPC proxies, and AMQP storage contain no credential material beyond the `x-d2-context` operational subset.
- Domain code has no transitive ASP.NET Core dependency; handlers take `IAuthContext`/`IRequestContext` and unit-test against a plain `MutableRequestContext`.
- Strict actor-chain parsing surfaces malformed `act` claims as 401 at the validation boundary, not as silent nulls deep in a handler; impersonation audit accuracy is enforced structurally.
- `TryDecode` returns null on failure and `ApplyPropagatedContext` is a no-op on null — the system degrades cleanly when the header is absent or corrupted.

**Negative / risks.**

- Adding a `propagate: true` field is a cross-language coordination event (the .NET serializer and TS decoder must both rebuild). In one monorepo this is a single CI run, but it is not free.
- Every inbound sync hop re-validates the bearer JWT (RS256 signature against the **cached** JWKS key + claims + a session-liveness check) rather than trusting a pre-authenticated identity asserted by the transport. That per-hop CPU + liveness lookup is overhead a **transport-trust shortcut** would avoid — internal services mutually authenticated by mTLS, passing a plaintext identity header that downstream accepts *in place of* re-validating the token. We deliberately don't lean on mTLS that way: using the channel as a license to skip token re-validation moves the trust anchor from the token (bound to the issuer's signature at every hop) to the network, so one compromised or misconfigured internal service could forge any identity. The threat model accepts the (cache-warm) re-validation cost to keep every hop zero-trust. **mTLS itself is adopted** — as an *additive* workload-identity and channel layer ([ADR-0023](0023-mtls-workload-identity.md)) that sits alongside per-hop token re-validation and never replaces it; what is declined is using mTLS to *skip* that validation. (Note: identity-via-JWT and the operational subset are both still *propagated* — what we decline is *trusting* a propagated identity without re-verifying its token.)
- The closed type vocabulary (enforced by codegen diagnostics) means adding a new property type touches the type vocabulary + emitters in lockstep; a propagated `IReadOnlySet<string>` is currently blocked (hence `FeatureFlagsCsv` is a CSV string).
- Strict parsing throws on malformed tokens: if upstream token exchange introduces a new `act` structure without a coordinated `ActorChainParser` update, valid tokens are rejected 401 — intended fail-closed behavior, but a deployment-sequencing constraint.

## Alternatives considered

**Transport-trust shortcut (use mTLS to skip per-hop validation — trust a plaintext identity header *because* the channel is mTLS-authenticated).** Pass `UserId`/`OrgId`/`Scopes` in a header that internal services accept *in place of* re-validating the token, on the strength of the mTLS channel alone. This is the *only* variant that actually avoids the validation cost — but it anchors trust in the network rather than the token, so any compromised or misconfigured internal service can forge identity, and the identity material now sits in plaintext headers at-rest (RabbitMQ frame stores, log shippers, broker management APIs). (Propagating identity in a header that is *still* integrity-protected — HMAC or nested JWT — saves nothing: you re-validate that instead of the bearer.) Rejected: identity stays cryptographically bound to the issuer's signature at every trust boundary. **This rejects the *role*, not the technology** — mTLS *is* adopted, as an additive workload-identity and channel layer ([ADR-0023](0023-mtls-workload-identity.md)) that authenticates the calling workload and protects the wire *alongside* per-hop token re-validation; what is declined is leaning on it to skip that validation. The forward-the-token-unchanged service-auth model that builds on this re-validate-every-hop decision is [ADR-0022](0022-service-auth-mint-once-forward.md).

**Hand-written context types per language.** Define the C# interfaces and a parallel TS interface, kept in sync by review. Rejected: independent PR pressure drives divergence within weeks — the exact problem the spec-driven codegen decision (ADR-0002) exists to solve.

**Thread identity through method parameters.** Pass `UserId`/`OrgId`/`Scopes` explicitly through every call. Rejected: does not compose with cross-cutting concerns (audit, logging, scope checks) without parameter-object proliferation or an ad-hoc ambient-context reinvention. A scoped `IAuthContext` DI service achieves the ambient effect with explicit testability, and ADR-0006 keeps the interface free of DI baggage.

## References

> **Monorepo-private process paths** (`docs/PATTERNS.md`, `docs/dev/rules.md`, and similar) are illustration only in the product monorepo that embeds this open tree — **not required for a public clone** of this ADR (see [ADR-0026](0026-public-private-monorepo-layout.md) Notes).
- `public/contracts/auth-context/IAuthContext.spec.json`, `public/contracts/request-context/IRequestContext.spec.json` — the source-of-truth specs (incl. `propagate`/`maxLength`/`redact`).
- `public/packages/dotnet/context/source-gen/ContextGenerator.cs` — the Roslyn generator (assembly dispatch, spec loading, emitter orchestration).
- `public/packages/dotnet/context/abstractions/Generated/` — emitted `IRequestContext`, `MutableRequestContext` (`FromClaims` / `FromJwtPayloadNoValidation`), `PropagatedContext`, `PropagatedContextExtensions`, `PropagatedContextSerializer` (global + per-field length caps).
- `public/packages/dotnet/auth/context-abstractions/Generated/` (emitted `IAuthContext`) + hand-written `IAuthContextExtensions.cs`.
- `public/packages/dotnet/context/abstractions/ActorChainParser.cs` (RFC 8693 §2.1 strict), `ScopeClaimParser.cs` (RFC 6749 §3.3).
- `docs/PATTERNS.md` (Context section); RFC 8693 §2.1/§4.1, RFC 6749 §3.3, RFC 7519 §4.1.3.
- [ADR-0002](0002-spec-driven-codegen.md) — the codegen pattern this applies. [ADR-0006](0006-abstractions-implementation-split.md) — the domain-safe abstractions slice.
- [ADR-0022](0022-service-auth-mint-once-forward.md) — the service-to-service auth model that builds on this decision's rebuild-from-JWT-each-hop choice: one token minted at the Edge boundary and forwarded unchanged, re-validated at every hop.
- [ADR-0023](0023-mtls-workload-identity.md) — mTLS as an additive workload-identity and channel layer, adopted on top of (never in place of) the per-hop token re-validation this decision establishes; the transport-trust shortcut weighed and rejected above declines only the misuse of mTLS to skip that validation.
- [ADR-0025](0025-request-context-establishment.md) — the `Establishment` fields (`Origin` / `ImmediateCaller` / `CallPath`) added to `IRequestContext` in §Decision-4 above: the local, non-propagated `Origin`/`ImmediateCaller` facts a capability authority can trust, and the propagated, telemetry-only `CallPath`.
