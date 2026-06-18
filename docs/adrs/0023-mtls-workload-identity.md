<!--
Copyright (c) DCSV. All rights reserved.
-->

# ADR-0023: mTLS workload identity — KeyCustodian-issued certificates, additive to JWT

- **Status**: Accepted
- **Date**: 2026-06-17
- **Deliverable**: 0021-auth-pivot (decision accepted); 0022-mtls-workload-identity (Decision subsections authored)

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

Production therefore extends the development model rather than replacing it: what changes is how the key material is protected and delivered, not the authentication design.

### Certificate hierarchy and lifetimes

The hierarchy is a **two-tier certificate authority with per-workload leaves**:

- A self-signed **root** certificate authority, long-lived (on the order of ten years), the single trust anchor every workload pins. Its private key signs only the issuing intermediate, never a leaf. In development the root key is a file on the developer machine, under `secrets/`; in production it is held offline under production-grade custody. Moving the root key offline is the production hardening — it is a change in where the key lives, not a change in code or in the trust model.
- An **issuing intermediate** certificate authority, signed by the root, with a shorter validity (on the order of one year). The intermediate is the online signer: it lives inside KeyCustodian and signs every per-workload leaf. Keeping the root out of the online signing path means a compromise of the online signer rolls the intermediate without re-anchoring every workload's trust in a new root.
- Per-workload **leaf** certificates, signed by the intermediate, short-lived (on the order of twenty-four hours in development, configurable downward for production). A leaf carries one workload's identity in its subject-alternative-name and is the certificate a workload presents on a hop.

The validity periods are configurable tunables, not hardcoded constants — they are supplied to certificate generation as parameters, the same way KeyCustodian already treats key sizes and rotation cadences as configuration.

### Key algorithm

The root, the intermediate, and every leaf use **ECDSA on the NIST P-256 curve, with SHA-256 signatures**.

This is a deliberate departure from the RSA convention KeyCustodian uses for its JWKS signing key. The RSA-2048 signing key exists because RS256 JWT signing requires RSA; the JWKS interop constraint that drives RSA there does not apply to internal mTLS certificates, which are consumed only by D²'s own workloads over the TLS handshake and never published to an external relying party that might lack ECDSA support. Three reasons make ECDSA P-256 the better fit for this internal PKI:

- **Equivalent security at a fraction of the size.** NIST SP 800-57 Part 1 places P-256 at the 128-bit security level — the same strength as RSA-3072 — while the keys and signatures are far smaller. Smaller certificates mean smaller TLS handshakes on every cross-process hop.
- **It is the modern default posture for new asymmetric keys.** The OWASP Cryptographic Storage Cheat Sheet recommends elliptic-curve keys first and frames RSA as the fallback "if elliptic-curve cryptography is not available" — exactly the inverse of an interop-driven RSA default, which does not apply here.
- **It matches the workload-identity model being adopted.** SPIRE issues ECDSA P-256 X.509-SVIDs by default for workload mTLS; keeping the same algorithm keeps the certificates aligned with the SPIFFE/SPIRE on-ramp the naming scheme already targets.

The short-lived leaves reinforce the choice: leaves are reissued continuously, so the cheaper key generation and handshake cost of ECDSA compounds across every workload's refresh cycle. No new dependency is introduced — `System.Security.Cryptography.X509Certificates.CertificateRequest` with an `ECDsa` key is base-class-library surface.

### Workload identity naming (SPIFFE-compatible subject-alternative-name)

A leaf carries its workload identity as a **URI subject-alternative-name in the SPIFFE format**: `spiffe://d2.internal/workload/<service>`, where `<service>` is the lowercase service identifier (for example `spiffe://d2.internal/workload/edge`, `spiffe://d2.internal/workload/files`). The **trust domain is `d2.internal`** — the same internal-trust-domain name the forwarded transaction-token validates as its audience.

Peer validation on the receiving side is a default-deny check with three conjuncts, all of which must hold: the presented certificate **chains to the internal certificate authority**, its subject-alternative-name **trust domain equals `d2.internal`**, and the **workload identifier is a member of the receiver's configured allowed-workload set**. An unknown workload, a foreign trust domain, a certificate that chains to a different root, or a certificate with no SPIFFE subject-alternative-name is rejected. Keeping the identity in the SPIFFE format is the on-ramp to a future SPIRE or service-mesh adoption — the identities already in use would not need re-architecting.

### Leaf lifetime, rotation, and revocation

**Leaves are short-lived and refreshed ahead of expiry.** Each workload holds its current leaf in memory and proactively reissues it before it expires, on the same refresh-ahead pattern the internal-token client already uses: a background loop computes a refresh-due condition against the leaf's not-after instant minus a lead time, reissues on startup and on each due tick, and — if reissue is briefly unreachable — keeps serving the still-valid leaf until its not-after rather than failing immediately. The leaf's own short lifetime bounds how long a reissue outage can be tolerated.

**The certificate authority rotates on KeyCustodian's existing overlap machinery.** A certificate-authority roll is a rotation of a KeyCustodian-managed key: a successor is brought into service while the predecessor continues serving through its grace window, so no connection established under the old authority is severed mid-flight ([ADR-0016](0016-keycustodian-lifecycle-store.md)). The certificate-specific rotation honors that overlap rather than introducing a new rotation model.

**Revocation is expiry-first.** The primary revocation mechanism is the short leaf lifetime — a leaf's blast radius is bounded by its roughly twenty-four-hour validity, the "let it expire" posture the SPIFFE model takes. For a compromised certificate authority, marking the authority key compromised terminally stops it from issuing further leaves, and rolling to a fresh intermediate re-anchors trust; once peers move to the new authority, every leaf the compromised intermediate signed is implicitly distrusted. **There is no certificate revocation list and no online certificate status protocol in this version** — this is a documented deferral, not a silent gap; either could be added later if the expiry-first posture proves insufficient.

### KeyCustodian modeling: the certificate authority is a managed key, the leaf is on-demand issuance

The two tiers of the model are persisted differently because their lifetimes and cardinality differ sharply.

The **root and the intermediate are full managed-key aggregates**, persisted with the same lifecycle the existing signing and payload-encryption keys use: the sum-type state machine (pending, active, retiring, retired, compromised), the concurrency-token-guarded flat record, the same-transaction audit write, and the overlap rotation. They are few and long-lived, so the lifecycle machinery is exactly the right weight, and a certificate-authority roll reuses the rotation flow already built. To carry the certificate-authority key, KeyCustodian gains a new key type — **`X509CaCertificate`** — alongside the existing signing, payload-encryption, and secret types. A certificate-authority key carries its root-wrapped private key as encrypted material, exactly like the signing key, and carries the certificate as its public material. The material-shape invariant that today admits a public component only for the asymmetric signing key gains the certificate-authority case, so a certificate-authority key is required to carry its certificate as public material. The certificate-authority certificate is public and is intentionally not redacted, the same treatment the signing key's public material already receives.

**Leaf certificates are not managed-key aggregates.** A leaf is issued on demand by an issuance operation and returned to the caller; it is not persisted as a managed-key record. The reasons are cardinality, material shape, and lifecycle weight: every workload reissues a leaf roughly daily, so persisting each as a five-state aggregate would flood the key store with short-lived rows reconstructible from the intermediate at any time; a leaf's private key is held by the *workload*, not custodied at rest by KeyCustodian, which is the opposite of the at-rest-custody shape a managed-key record is built around; and a leaf has no lifecycle transitions for KeyCustodian to protect — it is minted, handed out, and expires. There is therefore **no leaf key type** — a leaf never becomes a managed-key record. The leaf private key the workload holds is secret and is zeroed after use; the leaf certificate itself, being presented on the wire in the TLS handshake, is public.

Each issuance writes a **lightweight issuance audit entry** — which workload, when, signed by which issuing authority — so the operational question of which workloads hold live certificates is answerable. That audit entry is the only persistence on the leaf path.

### .NET mechanism

**On the callee, the server requires and validates the client certificate.** Kestrel is configured to require a client certificate on the HTTPS endpoint and to run a custom validation callback; the callback chains the presented certificate to the internal certificate authority and runs the SPIFFE subject-alternative-name peer check above. This wiring is supplied through the shared service-defaults host configuration, which is the single place every service's host pipeline is assembled — there is no client-certificate configuration there today, so this is a new shared host extension rather than a per-service hand-roll.

**On the caller, the client presents its leaf on the channel.** The outbound gRPC client builder gains a per-channel, opt-in client-certificate attachment that sets the channel handler's client certificate from the refresh-ahead leaf cache. It composes alongside the existing per-channel credential attachment and keeps the same safe-by-default posture: a channel that does not opt in presents no client certificate. Both halves use only base-class-library X.509 surface (`System.Security.Cryptography.X509Certificates`) plus Kestrel and the existing builders — no new host framework.

**The development bootstrap mirrors the existing root-key bootstrap.** The developer key-generation tooling is extended to emit a dev root and a dev intermediate (certificate plus key) into `secrets/`, and KeyCustodian reads them through a certificate-authority provider that mirrors the existing file-backed root-key provider: it loads the certificate-authority material from `secrets/`, validates the chain, fails loud on a missing, malformed, or expired chain, never logs key bytes, and caches the loaded material. There is no vendored or paid certificate authority and no service mesh — the full mutually-authenticated path runs on a developer machine. The operator runs the generation tooling and the material lands in `secrets/`, which is access-restricted; production differs only in where the certificate-authority key comes from.

### mTLS is additive and never gates the forwarded token

mTLS authenticates the peer workload and the channel; it never gates or replaces the forwarded-token validation. The receiving hop runs the full token re-validation regardless of the mTLS result — the peer-certificate check is an *additional* precondition that adds a fact about which workload is calling, not a short-circuit that lets a valid certificate stand in for a valid token. A hop that presents a valid leaf but a missing, malformed, or expired token is still rejected at token validation; a hop that presents a valid token but a missing, untrusted, or expired leaf is still rejected at the channel. Both factors are required, neither rescues the other. This is the line [ADR-0007](0007-request-context-propagation.md) draws — identity stays cryptographically bound to the issuer's signature on the forwarded token at every hop, and no plaintext identity header is ever trusted because the channel is mTLS-authenticated.

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

The detailed mechanics — the two-tier hierarchy, the ECDSA P-256 key algorithm, the leaf lifetime and refresh-ahead rotation, the expiry-first revocation posture, the SPIFFE subject-alternative-name naming scheme, the certificate-authority-as-managed-key versus leaf-as-on-demand-issuance modeling, and the .NET server-require / client-present mechanism — are settled in the Decision subsections above. What remains for the implementing work is the code: the certificate-authority key custody, the issuance operation, leaf rotation, the dev bootstrap, and the server and client transport wiring.

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
