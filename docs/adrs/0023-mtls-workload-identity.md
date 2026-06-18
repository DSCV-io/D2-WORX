<!--
Copyright (c) DCSV. All rights reserved.
-->

# ADR-0023: mTLS workload identity — KeyCustodian-issued certificates, additive to JWT

- **Status**: Accepted
- **Date**: 2026-06-17
- **Deliverable**: 0021-auth-pivot

## Context

[ADR-0022](0022-service-auth-mint-once-forward.md) mints exactly one internal transaction-token at the Edge trust boundary and forwards it unchanged across every cross-process hop, each hop independently re-validating it. That model answers *who is acting* at every hop — the forwarded token is a signed, re-verified assertion of the user identity, the impersonation chain, and the authorized scopes. It does not answer *which workload is calling*. The forwarded user-token says the request belongs to a given user with a given scope set; it cannot distinguish whether the caller presenting that token is the legitimate service A in the call graph or a compromised workload replaying a valid token it captured. Two callers holding the same valid token are indistinguishable to the receiver from the token alone.

The codebase already sketched a workload-identity mechanism and never wired it: an RFC 6749 §4.4 `client_credentials` service-identity token, minted per service and presented on outbound calls. That mechanism is not built — its client exists as a library type whose only callers are unit tests, and the Edge issuer endpoint that would mint it is not built. It also carries a structural problem under the forward-unchanged model: a service-identity token is addressed to a specific audience, and a receiver that validates audience strictly would reject a service-identity token minted for a different target — the same audience-targeting friction that drove the broad internal audience decision. Worse, threading a separate service-identity token through every hop reintroduces a per-hop service-token mint, the exact per-hop issuer round-trip the forward-unchanged decision exists to remove. Workload identity carried as a second forwarded JWT is the wrong shape.

Separately, the system has no certificate capability to build on. KeyCustodian — the authority that owns the lifecycle of every long-lived secret — today manages exactly three kinds of key: an asymmetric RS256 signing key (whose public half feeds JWKS), a symmetric payload-encryption key, and an opaque symmetric secret (cookie-signing HMAC, client-secret material). It has no certificate, certificate-authority, certificate-signing-request, or X.509 surface anywhere; its key-generation rule emits raw PKCS#8 private material and raw SPKI public material, never a certificate. The developer key-generation tooling produces a single symmetric root key and nothing else — no CA, no leaf certificates, no signing request. A workload-identity-via-certificates approach is therefore a new capability to build, not a configuration of something already present.

A hard constraint shapes the choice: the solution must run on a developer machine with no vendored service mesh and no paid or cloud-hosted certificate authority. Local development has to exercise the full workload-authentication path without standing up external infrastructure or incurring a per-developer cost.

## Decision

Workload identity and channel security are established by **mutually-authenticated TLS on every cross-process hop, with the certificates issued and rotated by KeyCustodian acting as the internal certificate authority**. This is layered on top of — never in place of — the per-hop JWT re-validation that establishes user identity.

### Every cross-process hop runs over mutually-authenticated TLS

Every synchronous call between two separate service processes (gRPC or HTTP from one service process to another) runs over mutually-authenticated TLS. The caller presents a client certificate and the callee presents a server certificate, and each side verifies the other's certificate against the internal certificate authority before the call proceeds. This establishes two things at once: **workload identity** — the callee knows which workload made the call, from the verified client certificate rather than from anything the caller asserts in a header — and an **authenticated, encrypted, integrity-protected channel** carrying the forwarded transaction-token and the operational context.

### KeyCustodian is the internal certificate authority

KeyCustodian becomes the internal certificate authority. It holds the certificate-authority key, it issues a per-workload leaf certificate that carries the workload's identity, and it rotates those leaf certificates on its existing lifecycle machinery: short-lived leaves with staged successors brought into service before the predecessor leaves it, so a rotation never breaks a connection in flight. This extends the key-lifecycle model KeyCustodian already runs ([ADR-0016](0016-keycustodian-lifecycle-store.md)) — the sum-type state machine and the overlap-rotation policy (a successor activates while the predecessor continues serving through its grace window, with the cadence constrained so a key never rotates before its predecessor finishes retiring) — with a new certificate key-type and the issuance, rotation, and revocation operations that go with it. The CA key takes its place alongside the signing and payload-encryption keys as KeyCustodian-managed, root-wrapped material with a governed lifecycle.

### mTLS is strictly additive — never a JWT-validation skip

mTLS adds a factor; it removes none. Every cross-process hop still performs the **full JWT re-validation** described in [ADR-0022](0022-service-auth-mint-once-forward.md) — signature against cached JWKS, issuer, the broad internal audience, lifetime, the algorithm pin, session liveness, and the receiving operation's required scopes. The mTLS peer identity is an *additional* fact the receiver learns about the call, not a substitute for verifying the bearer assertion. An accepted internal call requires **both** a valid forwarded transaction-token **and** a trusted workload certificate.

This is deliberately **not** the transport-trust fast path that [ADR-0007](0007-request-context-propagation.md) weighs and rejects. That rejected path uses mTLS as a reason to *skip* per-hop token validation — passing a plaintext identity header that the receiver trusts *because* the channel is mTLS-authenticated, moving the trust anchor from the token to the network. That rejection still stands. The use of mTLS here is the opposite: the token is still forwarded and still independently re-validated at every hop, and mTLS only adds workload authentication on top. The trust anchor for *identity* remains the issuer's signature on the token, re-verified at every boundary; mTLS answers the separate question of *which workload* is presenting that token. JWT-plus-mTLS is strictly stronger than JWT alone — an attacker now needs both a valid signed token and a trusted workload certificate, and the workload identity is established by the channel rather than asserted in a forgeable header.

### In-process module calls and asynchronous hops are out of scope

Modules hosted inside the Edge process — KeyCustodian itself and the auth module — are reached through an in-process façade, not a network call. Such a call crosses no process boundary and traverses no untrusted channel, so it uses no mTLS; there is no peer to authenticate and no wire to protect. The mTLS discipline governs cross-process hops only.

Asynchronous message-broker hops have their own, already-settled trust model: the operational context rides encrypted inside the message frame and the encryption boundary is the trust boundary ([ADR-0022](0022-service-auth-mint-once-forward.md), [ADR-0007](0007-request-context-propagation.md)). Transport-level TLS for the broker connection is a separate operational concern and is out of scope for this decision, which concerns workload authentication on synchronous service-to-service hops.

### Development and production run the same model — the difference is secret management

For local development the certificate authority is self-signed and generated on the developer machine: a dev CA plus the per-workload leaf certificates are produced locally, extending the existing developer key-generation tooling with a KeyCustodian dev bootstrap. The full mutually-authenticated path therefore runs on a developer machine with no service mesh and no paid certificate authority.

Production is not a separate design. It runs the same model — KeyCustodian issues every leaf from the certificate-authority key, every hop validates its peer's certificate against that authority, and leaves rotate on the overlap lifecycle described above. The issuance, validation, and rotation flow is identical to development; the authentication design does not change between the two.

What hardens between development and production is secret management, in two facets. The first is custody of the certificate-authority key: in development it is a throwaway file on the local machine, while in production it is the highest-value secret in the system, held under production-grade custody — encrypted at rest under the KeyCustodian root key, access-controlled, and audited on every signing operation. The second is secure distribution of certificate material to the workloads: in development every leaf is generated locally on one machine, while in production each workload obtains and renews its leaf across separate hosts, its identity bootstrapped from a secret the deployment orchestrator provisions.

Production therefore extends the development model rather than replacing it: what changes is how the key material is protected and delivered, not the authentication design. The certificate hierarchy and the exact distribution and bootstrap mechanism are part of the subsystem design left to the implementing deliverable, not decided here.

## Consequences

**Positive.**

- **Real workload identity.** The callee learns which workload called it from a verified client certificate — a fact the forwarded user-token cannot supply. That identity is available for service-level authorization, for audit (recording which workloads handled a request, not only which user), and for anomaly detection (a call from an unexpected workload is visible).
- **Defense-in-depth.** An accepted internal call requires both a valid signed transaction-token and a trusted workload certificate. A leaked token alone no longer suffices, and a compromised workload without a trusted certificate cannot make an accepted call; the workload identity is established by the authenticated channel instead of asserted in a forgeable header.
- **The channel protects the forwarded credentials on the wire.** The forwarded transaction-token and the operational context travel inside an encrypted, integrity-protected channel between every pair of service processes, rather than in cleartext on the network.
- **It cleanly replaces the unwired service-identity layer.** Workload identity comes from the channel, not from a second forwarded JWT, so the `client_credentials` service-identity token is no longer needed — which removes both the per-hop service-token mint it would have required and the audience-targeting problem a forwarded service-identity token would have hit at a strict receiver.
- **It runs locally with no payware.** The self-signed dev CA exercises the full path on a developer machine, with no vendored service mesh and no paid or cloud certificate authority.

**Negative / new work.**

- **A genuinely new PKI subsystem in KeyCustodian.** No certificate-authority capability exists today — KeyCustodian manages only signing keys, payload-encryption keys, and secrets, with no X.509, CSR, or CA surface anywhere, and the dev tooling produces only a symmetric root key. This decision requires building certificate-authority-key custody, certificate-signing-request handling, leaf issuance, leaf rotation, and revocation as new capability — not configuring something already present.
- **Certificate distribution and bootstrap are new machinery.** Each workload must obtain its leaf certificate, renew it before expiry, and trust the certificate-authority certificate. The mechanism that bootstraps a workload's identity and distributes and renews its material does not exist yet and has to be built and operated.
- **Rotation must not break in-flight connections.** Leaf rotation has to bring a successor into service before retiring the predecessor so that connections established under the old leaf are not severed mid-flight; KeyCustodian's existing overlap-rotation model applies, but the certificate-specific rotation has to honor it.
- **The operational surface grows.** Certificate expiry has to be monitored across every workload, and the certificate-authority key becomes the highest-value secret in the system — its compromise would let an attacker mint a trusted workload identity, so its custody is the most sensitive in the deployment.

The detailed mechanics are left to the deliverable that implements the subsystem: the leaf-certificate lifetime and rotation cadence, the distribution and bootstrap mechanism, the revocation strategy (short-lived leaves that expire quickly versus a revocation list or an online status protocol), the workload-identity subject-alternative-name naming scheme, and whether the hierarchy is a single root or a root with issuing intermediates.

## Alternatives considered

**Vendored service mesh (Istio, Linkerd, Consul Connect).** A service mesh provides mutually-authenticated TLS and workload identity out of the box — sidecar proxies establish mTLS and the mesh control plane issues and rotates workload certificates. Rejected: a mesh is heavy operational infrastructure — a control plane and per-workload proxies — that does not run trivially on a developer machine and adds a large dependency the deployment does not otherwise need. KeyCustodian already owns key lifecycle and overlap rotation, so issuing workload certificates extends machinery that already exists rather than adopting a whole platform to get it.

**SPIFFE / SPIRE.** SPIFFE is the open standard for workload identity and SPIRE is its reference implementation — the canonical way to give workloads cryptographic identity documents. Not adopted as a dependency now: SPIRE is its own infrastructure to run and operate, which the local-development constraint and the preference to extend KeyCustodian argue against. The workload-identity naming is kept compatible with the SPIFFE identity scheme, so adopting SPIFFE/SPIRE later — if the deployment grows to want a standard, interoperable workload-identity layer — would not require re-architecting the identities already in use.

**JWT only, no mTLS (the status quo plus the unwired service-identity attempt).** Continue authenticating hops by re-validating the forwarded user-token, and rely on the sketched `client_credentials` service-identity token for workload identity. Rejected: re-validating the forwarded user-token is zero-trust on *user* identity but yields no *workload* identity at all — two callers holding the same valid token are indistinguishable — and the service-identity token meant to fill that gap is unbuilt, carries the audience-targeting problem at a strict receiver, and would reintroduce a per-hop service-token mint. Mutually-authenticated TLS delivers workload identity and defense-in-depth directly, without a second forwarded token and without a per-hop mint.

**Cloud or managed private certificate authority.** Use a hosted private-CA service to issue and manage the workload certificates. Rejected: a managed private CA is a paid, cloud-bound dependency that breaks the requirement to run the full path locally on a developer machine. The self-signed certificate authority issued through KeyCustodian runs anywhere — on a laptop for development and under production custody in deployment — on one model.

## References

- [ADR-0022](0022-service-auth-mint-once-forward.md) — the mint-once-at-the-Edge, forward-unchanged service-auth model; this decision supplies the workload-authentication factor that model layers mTLS on top of, and which it depends on for establishing which workload presents the forwarded token.
- [ADR-0007](0007-request-context-propagation.md) — the request/auth context model and its rejection of the transport-trust fast path (mTLS as a reason to skip per-hop token validation); this decision's additive use of mTLS keeps full per-hop JWT re-validation and therefore does not reactivate that rejection.
- [ADR-0016](0016-keycustodian-lifecycle-store.md) — KeyCustodian's key-lifecycle state machine and overlap-rotation model; the certificate-authority capability extends this machinery with a new certificate key-type and its issuance, rotation, and revocation operations.
- [ADR-0012](0012-self-rolled-dotnet-auth.md) — the self-rolled .NET auth surface, including the `client_credentials` service-identity client this decision's workload identity replaces.
- [ADR-0021](0021-unified-operation-contract-idl.md) — the unified operation-contract IDL whose codegen emits the cross-process gRPC client for each service-to-service hop; that generated client is the channel this decision's mutually-authenticated TLS secures.
