<!--
Copyright (c) DCSV. All rights reserved.
-->

# ADR-0025: Request-context establishment — Origin, immediate-caller & call-path hop-tracking (anti-spoofing)

- **Status**: Accepted
- **Date**: 2026-06-30
- **Deliverable**: 0026-kc-crypto-surface

## Context

[ADR-0022](0022-service-auth-mint-once-forward.md) mints one internal transaction-token at the Edge boundary and forwards it unchanged across every cross-process hop; [ADR-0023](0023-mtls-workload-identity.md) adds mutually-authenticated TLS so a receiving hop also learns *which workload* is calling. Both decisions answer *who is acting* and *which workload presented the token*. Neither answers a third, structurally distinct question a capability authority needs: *what kind of trust boundary produced the context I am about to act on, this hop* — was it the external Edge ingress, a cross-process mTLS-authenticated hop, an in-process module call inside one host, or an in-host background worker with no inbound request at all?

That gap is not cosmetic. KeyCustodian's capability authority (`WorkloadCapabilityAuthority`, [ADR-0016](0016-keycustodian-lifecycle-store.md)) has to decide whether a caller may sign with a given key domain, and the cluster JWT signing key (`jwks-signing` — the root that every forwarded transaction-token's signature ultimately depends on) is the single highest-value target in the system: whoever can sign with it can mint a token that every internal service will accept. The rule committed ahead of this decision keyed its allow branch on a bare `bool isCrossProcess`: *any* call that is not cross-process was allowed to sign the cluster root. That is a confused-deputy shape — "not cross-process" is not the same fact as "genuinely originated in-process with no external request in its ancestry." A request that entered externally and was later dispatched through code that happens to run in-process would still present as `isCrossProcess == false` to a naive check with no caller identity to fall back on (the in-process plane has no cross-process caller id to inspect), and nothing in the `bool` structurally rules that shape out.

`IRequestContext` ([ADR-0007](0007-request-context-propagation.md)) already carries an operational subset that propagates across hops, plus identity that rebuilds fresh from the JWT at every hop. ADR-0022 sketched a **service call-path** — every hop appends its own identity and a timestamp, so the chain of services a request traversed is recoverable from any hop's logs even when a trace span is dropped — but left "the exact wire field, its encoding, and the bound... to the deliverable that implements propagation" (ADR-0022 §"Every cross-process hop appends to and logs a service call-path") and flagged, as an open consequence, that "operational-subset propagation on synchronous .NET-to-.NET hops is not yet wired" (ADR-0022 §Consequences). This decision closes both: it defines the wire shape of the call-path, and it wires the reader/writer on every synchronous .NET transport.

A second, sharper need drove the design past "just add a call-path field": the call-path is **operational telemetry**, built by accumulating each hop's self-reported identity as the request travels. An authority decision must never key on it — a value that rides across process boundaries and accumulates through multiple hops is exactly the kind of value a security review should treat with suspicion as an authority input, even when the transport carrying it is itself trusted, because nothing about the *field's own shape* stops a future caller from reading it for an authorization decision by mistake. The system needed a **different kind of fact** for authority: something computed fresh, locally, by the boundary that is about to act, from a transport signal that boundary cannot be tricked into misreading — never carried in from elsewhere.

## Decision

Introduce two new **local, unforgeable, non-propagated** facts — `Origin` and `ImmediateCaller` — recomputed by every trust boundary from its own transport evidence, and one **propagated, telemetry-only** fact — `CallPath` — that accumulates hop identities for observability. All three fold into the existing spec-driven `IRequestContext` (ADR-0007). A fourth concept, the RFC 8693 `act` impersonation chain, already existed and is unchanged by this decision — it is included below only to state the boundary between it and the new fields precisely.

### The four concepts

| Concept | Answers | Established | Travels | Authority-grade? |
|---|---|---|---|---|
| `Origin` (`RequestOrigin` enum) | "What kind of boundary produced my context, this hop?" | Recomputed **locally** by the receiving boundary from its own transport facts | Nowhere — never propagated | **Yes** — local, so unforgeable |
| `ImmediateCaller` (`string?`) | "Who called me, this hop?" | mTLS client certificate (cross-process) / calling module id (in-process) | Nowhere — never propagated | **Yes** (cross-process: certificate-derived, unforgeable) / telemetry-grade (in-process: self-asserted inside a trusted process) |
| `CallPath` (`IReadOnlyList<CallPathEntry>`) | "What sequence of workloads/modules has handled this request so far?" | Each hop appends its own identity + a timestamp on receipt | **Propagated** on `x-d2-context`, depth-bounded | **No** — operational telemetry only; authority never reads it |
| `act` chain (pre-existing) | "On whose behalf is the subject acting?" | Minted/extended at auth or token-exchange | Inside the signed JWT | **Yes** — signed |

`Origin` and `ImmediateCaller` are deliberately **not propagated**: a value derived once and carried across a hop stops being a fact about *that hop* and becomes a claim about a hop the current boundary did not itself observe. Every boundary instead recomputes both from scratch, from evidence only that boundary can see — the presence and content of a validated mTLS client certificate, the fact that a call arrived through the in-process module façade rather than over a wire, or the fact that a caller announced itself as a background worker with no request to enrich. A wire-supplied `Origin` or `ImmediateCaller` is structurally impossible to construct: nothing serializes them, so there is no slot for a forged value to occupy in the first place.

`CallPath` is the opposite shape on purpose. It is exactly the kind of accumulating, cross-hop value that must never back an authority decision, so it is built to be useless for that purpose even if a future caller tried: it records identity, not permission, and no comparison an authority rule could plug it into changes the caller's grant. Keeping it structurally separate from `Origin`/`ImmediateCaller` — a different type, a different propagation flag, documented as telemetry-only in its own doc comment — is what makes "authority never reads the call-path" a property a reviewer can verify by looking at which type a rule's parameters have, not a convention that depends on every future author remembering not to reach for it.

### The spec fold-in

Three fields land in a new `Establishment` section of `contracts/request-context/IRequestContext.spec.json`, generating into the existing `IRequestContext.g.cs` / `MutableRequestContext.g.cs` / `PropagatedContext.g.cs` family (ADR-0007 §1):

- `Origin: RequestOrigin` — non-nullable; the enum's zero member (`Unestablished`) is the type-level fail-closed default, so a context no boundary has touched carries a value that denies by construction rather than a null that a careless check might treat as "any plane."
- `ImmediateCaller: string?` — nullable; absent on `EdgeInbound` and `System` (no upstream internal workload to name).
- `CallPath: IReadOnlyList<CallPathEntry>` — `propagate: true`, `maxLength` reinterpreted as the maximum entry count (depth-bounded, so a request cannot grow the field without limit as it threads through the system).

Neither `Origin` nor `ImmediateCaller` carries `propagate: true`, so the generated `PropagatedContext` structurally excludes both — there is no code path by which either could reach the wire. `CallPath` is the first `propagate: true` list-of-records field the context spec has emitted; every field propagated before it was a scalar.

`RequestOrigin`, `CallPathKind`, and `CallPathEntry` are hand-authored (not generated) in `D2.Shared.Auth.Abstractions` — the same domain-safe vocabulary library that already hosts `OrgType`, `Role`, `ActorKind`, and `ActorEntry`. They live there rather than in the transport-facing `D2.Shared.Context.Abstractions` because the KeyCustodian domain rule that reads `RequestOrigin` for authority decisions (`WorkloadCapabilityAuthority`) references only the domain-safe vocabulary layer, never the transport layer — placing the enum anywhere transport-adjacent would pull a transport dependency into domain code that must stay ASP.NET-Core-free.

```csharp
// server/shared/dotnet/auth/abstractions/RequestOrigin.cs
public enum RequestOrigin
{
    Unestablished = 0,   // no boundary has positively established this yet — fail-closed
    EdgeInbound = 1,     // the external HTTP boundary; the start of the call-path
    CrossProcessHop = 2, // a gRPC hop authenticated by a validated mTLS client certificate
    InProcessModule = 3, // an in-host module call through the generated I<Module>Api leaf
    System = 4,          // an in-host background worker with no inbound user request
}
```

### The five establishment boundaries

Every place a request-scoped context can come into existence recomputes `Origin` + `ImmediateCaller` and appends to `CallPath`. Four are inbound establishment points; the fifth carries `CallPath` (and the rest of the propagated subset) back out on every outbound synchronous call so the next hop's establishment has something to read and append to.

1. **EdgeInbound** — `RequestOriginEdgeInboundMiddleware` (`D2.Shared.Auth.Http`). Runs after the JWT auth middleware has populated the context slot; sets `Origin = EdgeInbound`, `ImmediateCaller = null` (the external client is not an internal workload), and starts a fresh `CallPath` with one `CallPathKind.Edge` entry carrying the host's own service id.
2. **CrossProcessHop** — `RequestOriginCrossProcessInterceptor` (`D2.Shared.Auth.Grpc`), a gRPC server interceptor registered after `JwtAuthInterceptor` so the JWT-validated identity is already on the context before this interceptor enriches it. It applies the inbound `x-d2-context` propagated subset (including the inherited call-path), sets `Origin = CrossProcessHop`, sets `ImmediateCaller` from the validated mutual-TLS peer certificate (`GetD2PeerWorkloadIdentity()`, which reads `Connection.ClientCertificate` and nothing else — never a header, claim, or payload field), appends this hop's own identity as a `CallPathKind.WorkloadHop` entry, and logs the received call-path's entry count. All four gRPC server-handler shapes (unary, client-streaming, server-streaming, duplex) route through one shared establishment method, so a streaming method added later cannot silently skip it. No validated certificate means a `null` caller, which the authority rule below treats as fail-closed.
3. **InProcessModule** — `InProcessModuleBoundary.EstablishInProcessModule` (`D2.Shared.Context.Abstractions`), an extension method the generated in-host module façade (the `I<Module>Api` leaf, ADR-0021) calls before dispatching into another module inside the same host. Sets `Origin = InProcessModule`, sets `ImmediateCaller` to the calling module's own id (self-asserted — there is no wire and no untrusted party between caller and callee, so there is nothing to spoof), and appends a `CallPathKind.ModuleHop` entry.
4. **System** — `SystemRequestContextBootstrap.EstablishSystemContext` (`D2.Shared.Context.Abstractions`), called by an in-host background worker's per-iteration DI scope before it resolves any handler. Sets `Origin = System`, `ImmediateCaller` to the host's own service id, and starts a fresh single-entry `CallPath` with a `CallPathKind.System` entry. A system-established context is least-privilege by construction: it carries the host's identity for audit and telemetry, but — per the authority model below — grants no signing authority at all, because signing authority requires either `CrossProcessHop` or `InProcessModule`.
5. **Outbound propagation** — `PropagatedContextClientInterceptor` (`D2.Shared.Auth.Outbound`), a gRPC client interceptor that resolves the *current* inbound request's scope through the framework-free `IAmbientRequestScopeAccessor` port, projects that scope's context (including the accumulated `CallPath`) via `ToPropagatedContext()`, and attaches the encoded `x-d2-context` header on every outbound call shape. This is the piece that closes the gap ADR-0022 left open: the propagated operational subset — including the call-path — now rides every synchronous gRPC hop, not only AMQP. It is opportunistic (no scope, no context, or an empty projection means no header and no throw) and uses a plain client interceptor rather than `CallCredentials`, so it works on plaintext and loopback channels too — the call-path is non-secret operational data, not a credential.

### The anti-spoofing invariants

1. **`Origin` and `ImmediateCaller` are non-propagated by construction.** Neither field carries `propagate: true`, so the code-generated `PropagatedContext` cannot contain either — there is no serialization path for a wire-supplied value to travel through, and every boundary above recomputes both fresh from local evidence regardless of anything already on the context when the boundary runs.
2. **The cross-process caller is the validated mTLS certificate, never anything else.** `ImmediateCaller` on a `CrossProcessHop` traces to exactly one call — `GetD2PeerWorkloadIdentity()` reading `Connection.ClientCertificate` — with no fallback to a header, claim, or request body field. No certificate means a `null` caller, which fails closed.
3. **`CallPath` is telemetry, and authority code never reads it.** No arm of `WorkloadCapabilityAuthority` takes a `CallPath` parameter or inspects one; the type boundary between the authority-grade fields and the telemetry field is the enforcement mechanism, not a comment.
4. **`Origin == Unestablished` denies.** The scoped default for a freshly-constructed context is `RequestOrigin.Unestablished` (the enum's zero value), and the first arm of every authority rule below checks for it and denies before any other logic runs. A context that no boundary has touched — a bug, a missing wire-up, a test double built without going through establishment — cannot reach an allow branch by omission; it has to be affirmatively denied by every consulting rule, and the rule's own first line is the thing verifying that.

### The authority model: possession-gated minter capability closes the confused-deputy

`WorkloadCapabilityAuthority.AuthorizeSigning` is refined to take the established `RequestOrigin` in place of the prior `bool isCrossProcess`:

```csharp
public static D2Result AuthorizeSigning(
    string? immediateCaller, RequestOrigin origin, KeyDomain target,
    IReadOnlySet<string> allowedSigningDomainsForCaller)
{
    if (origin == RequestOrigin.Unestablished)
        return KeyCustodianFailures.RequestOriginUnestablished();

    if (MinterOnlySigningDomains.Contains(target.Value))
        return KeyCustodianFailures.MinterCapabilityRequired();

    if (origin != RequestOrigin.CrossProcessHop)
        return KeyCustodianFailures.SigningDomainNotAuthorized();

    if (immediateCaller.Falsey())
        return D2Result.Forbidden();

    return allowedSigningDomainsForCaller.Contains(target.Value)
        ? D2Result.Ok()
        : KeyCustodianFailures.SigningDomainNotAuthorized();
}
```

The key change is the second arm. `jwks-signing` — the cluster-signing root — is now **structurally unreachable on the general signing surface for every established origin**, not merely for cross-process callers. This is the direct fix for the confused-deputy shape in the Context section: under the old `bool isCrossProcess` rule, "not cross-process" was the allow condition for the root key, and any code path that ended up executing in-process — regardless of how the request that triggered it originated — satisfied that condition. Under the refined rule, being in-process is no longer sufficient for anything; the root key has exactly one path to it, and that path does not run through `AuthorizeSigning` at all.

The one path is a dedicated capability, `IJwtSigningCapability`:

```csharp
public interface IJwtSigningCapability
{
    ValueTask<D2Result<SignOutput>> SignJwtAsync(SignInput input, CancellationToken ct = default);
}
```

Its implementation checks a separate, minimal rule — `AuthorizeMinterSigning(RequestOrigin origin)`, which denies on `Unestablished` and on anything other than `InProcessModule` — then signs the active `jwks-signing` key directly, bypassing `AuthorizeSigning` entirely (whose second arm would reject the root key categorically regardless of caller). The capability is registered by a dedicated DI extension, `AddD2JwtSigningCapability()`, called **only** from the JWT minter's (auth module's) composition — never from `AddD2KeyCustodianClients()`, the registration every ordinary consumer of the KeyCustodian client uses. **Possession of the resolved interface is the authority**: a provider built without the dedicated registration cannot resolve `IJwtSigningCapability` at all, so there is no runtime check to bypass and no caller identity to spoof — the capability either was wired into this composition root or it was not, and that is a build-time, review-visible fact, not a request-time decision. The in-process-plane check inside the implementation is a second, independent guard: even a caller that somehow obtained the interface reference could not use it outside the `InProcessModule` origin.

This is the general shape the decision establishes for any future cluster-root-grade secret: authority over the most sensitive material is not a branch inside the general-purpose rule that everyone's request eventually reaches — it is a capability interface handed to exactly one composition root, gated a second time by an origin the general rule already treats as fail-closed by default.

### The build-vs-run line

Every piece described above — all five establishment boundaries, the refined authority rule, the minter capability and its DI isolation — is built and proven in isolation: generator tests for the spec fold-in, an in-memory gRPC `TestServer` for the interceptors, a two-endpoint `TestServer` proving `Origin`/`CallPath` propagate correctly from one process's outbound interceptor to the next process's inbound interceptor, DI-resolution tests proving the capability is unreachable from the general client registration and reachable only from the dedicated one, and Testcontainer-PostgreSQL tests exercising the real KeyCustodian handler graph including the general `sign` operation's deny path through `AuthorizeSigning`. Nothing in this decision is stubbed or deferred.

The one thing genuinely not built here is the **Edge host process** that composes these pieces into a live, multi-service deployment — registering the interceptors on a running Kestrel/gRPC pipeline, standing up the auth module that would call `AddD2JwtSigningCapability()`, and running an actual second .NET service to be the other end of a cross-process hop outside a test harness. That is a host, not a machinery piece: every seam the host will wire already exists, is unit- and integration-proven, and does not change shape when the host lands.

## Consequences

**Positive.**

- **The confused-deputy path to the cluster-signing root is closed structurally, not by convention.** Nothing on the general signing surface can reach `jwks-signing` regardless of origin; the only path is a capability that a provider cannot even resolve unless the auth module's composition explicitly registered it.
- **A capability authority denies by construction when establishment did not happen.** Because the scoped default is `RequestOrigin.Unestablished` and every rule's first arm checks for it, a missing or forgotten establishment call is fail-closed rather than an accidental allow — a defect that would otherwise open a door instead surfaces as every legitimate call being rejected, which is loud and gets fixed.
- **The service call-path is finally concrete and closes a documented gap.** ADR-0022 explicitly deferred the call-path's wire shape and flagged sync-hop operational-subset propagation as unwired; both are now built, tested end-to-end across a real two-process `TestServer` harness, and available on every synchronous transport, not only AMQP.
- **Authority and telemetry cannot be confused at the type level.** `CallPath` is a different type than `Origin`/`ImmediateCaller`, propagates differently, and no authority rule takes it as a parameter — a reviewer can confirm the separation by reading a rule's signature.
- **The pattern generalizes.** Any future cluster-root-grade secret gets the same shape for free: a possession-gated capability interface, registered in exactly one composition root, guarded a second time by an origin the general rule already denies by default.

**Negative / new work.**

- **A new closed-vocabulary trio to maintain.** `RequestOrigin`, `CallPathKind`, and `CallPathEntry` are hand-authored, not generated, so a future establishment boundary (should one ever be needed) requires a deliberate code change here rather than a spec edit alone.
- **`CallPath` is the first propagated list-of-records field the context spec has ever emitted**, which required extending the serializer beyond the scalar-only propagation it previously supported — a nontrivial addition to the codegen surface that every future propagated collection field will now reuse.
- **Five establishment call sites instead of one.** Every place a request-scoped context originates now has its own small, hand-written establishment call (middleware, interceptor, extension method, bootstrap helper); a sixth transport added later needs its own establishment boundary, not a shared default.
- **The Edge host wiring remains genuinely outstanding.** Every machinery piece is proven in isolation, but nothing here proves the *live, multi-process* system — that exercise still needs the Edge host, tracked separately.

## Alternatives considered

**Keep the `bool? IsCrossProcess` / `string? CallerWorkloadId` shape from the earlier peer-context sketch.** A two-state (or three-state, with `null`) boolean distinguishes only "cross-process or not," which is exactly the shape that produced the confused-deputy risk this decision fixes — it cannot express "genuinely in-process module call" versus "in-process because the code happened to execute there this time" versus "background worker with no request at all." `RequestOrigin`'s four-way enum is not a stylistic preference over the boolean; the extra states are what make the distinction the authority rule needs expressible at all, and `Unestablished` as the enum's zero value gives fail-closed-by-default without the `bool?` three-state gymnastics a nullable boolean would need to express the same thing.

**Let `CallPath` (or a similar hop-identity field) double as an authority input** — e.g., deny signing unless the immediately-preceding call-path entry matches an expected workload. Rejected: this reintroduces exactly the failure mode `Origin`/`ImmediateCaller` were built to avoid — an accumulating, propagated value used for a security decision. A propagated field's integrity depends on every hop in its history having appended honestly; a locally-recomputed field's integrity depends on only the current boundary's own transport evidence. The latter is a strictly smaller trust surface, so authority stays on `Origin`/`ImmediateCaller` and `CallPath` stays telemetry-only, permanently.

**Gate the cluster-signing root with a per-call `isMinter: bool` parameter threaded through `AuthorizeSigning`, instead of a separate capability interface.** Rejected: a boolean parameter is exactly the kind of runtime-checkable flag a caller could get wrong, forget to set, or (in a future refactor) end up passing from user-influenced input. A capability interface that literally cannot be resolved outside one composition root removes the flag from the runtime path entirely — there is nothing to check because there is nothing to call.

## References

- [ADR-0007](0007-request-context-propagation.md) — the spec-driven `IRequestContext` model this decision extends with the `Establishment` section; the propagate/maxLength mechanism the new fields plug into.
- [ADR-0022](0022-service-auth-mint-once-forward.md) — mint-once-at-the-Edge, forward-unchanged; the service call-path this decision concretizes and wires end-to-end, and the sync-hop operational-subset propagation gap this decision closes.
- [ADR-0023](0023-mtls-workload-identity.md) — mTLS workload identity; the validated client certificate this decision's `CrossProcessHop` establishment boundary reads `ImmediateCaller` from.
- [ADR-0016](0016-keycustodian-lifecycle-store.md) — KeyCustodian's key-lifecycle state machine; `WorkloadCapabilityAuthority` is the domain rule this decision refines to take `RequestOrigin`.
- [ADR-0021](0021-unified-operation-contract-idl.md) — the in-process module leaf (`I<Module>Api`) that the `InProcessModule` establishment boundary wraps.
- `contracts/request-context/IRequestContext.spec.json` — the `Establishment` section (`Origin` / `ImmediateCaller` / `CallPath`).
- `server/shared/dotnet/auth/abstractions/RequestOrigin.cs`, `CallPathKind.cs`, `CallPathEntry.cs` — the hand-authored vocabulary trio.
- `server/shared/dotnet/auth/http/Middleware/RequestOriginEdgeInboundMiddleware.cs`, `server/shared/dotnet/auth/grpc/Interceptors/RequestOriginCrossProcessInterceptor.cs`, `server/shared/dotnet/context/abstractions/InProcessModuleBoundary.cs`, `server/shared/dotnet/context/abstractions/SystemRequestContextBootstrap.cs`, `server/shared/dotnet/auth/outbound/Grpc/PropagatedContextClientInterceptor.cs` — the five establishment boundaries.
- `server/services/edge/key-custodian/domain/Rules/WorkloadCapabilityAuthority.cs` — the refined `AuthorizeSigning` / new `AuthorizeMinterSigning` rules.
- `server/services/edge/key-custodian/clients/IJwtSigningCapability.cs`, `server/services/edge/key-custodian/app/Application/JwtSigningCapability.cs`, `server/services/edge/key-custodian/app/Application/JwtSigningCapabilityServiceCollectionExtensions.cs` — the minter capability seam, its implementation, and its isolated DI registration.
