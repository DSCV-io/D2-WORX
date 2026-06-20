<!--
Copyright (c) DCSV. All rights reserved.
-->

# POST_PIVOT_ROADMAP.md — Remaining work after the auth pivot + to finish the contract-IDL emitters

**Status**: living roadmap — consolidates the work that the auth pivot (ADR-0022 / ADR-0023) and the
unfinished contract-IDL emitter fleet (ADR-0021 / C0) leave open. The work is real and scattered across
~6 docs; this is the single place to see *what is left, in what order, and what each item is blocked on*.

**This doc LINKS, it does not DUPLICATE.** Every row points at the canonical source that owns the detail
(an ADR, a phase tracking doc, a deliverable record, or the active deliverable workspace). When a source
says something different from a row here, the source wins and this row is stale — fix the row. The value
here is the cross-cutting *sequencing + blocked-on* view that no single source carries, not a re-statement
of any of them.

**How to read the status column** (one vocabulary across all four areas):

| Status | Meaning |
| ------ | ------- |
| ✅ done | Built + tested + shipped (or merged on its branch). The row is here for completeness / sequencing context. |
| 🔄 active | Active right now in a named deliverable. |
| 📐 specified-deferred | The design is locked (an ADR / deliverable decided it) but the code is deliberately deferred, with a tracked to-be-done note. |
| ✍ not-yet-specified | Needs a design decision before it can be built — no locked spec yet. Flagged in [§E](#e--items-that-still-need-a-design-decision). |

**Scope boundary.** This roadmap covers the auth-pivot reconciliation, the mTLS cross-process remainder,
and the contract-IDL (C0) emitter completion. It does NOT re-plan the rest of Phase 3 (the auth track
A1–A6, the Edge-pipeline track E1–E5) — that DAG lives in [PHASE_3.md](PHASE_3.md). Where an item here is
*blocked on* a Phase-3 deliverable, the row names it.

---

## Table of contents

- [A — mTLS remaining (Phase-3, host-gated)](#a--mtls-remaining-phase-3-host-gated)
- [B — Auth-pivot existing-code reconciliation](#b--auth-pivot-existing-code-reconciliation)
- [C — Contract-IDL emitter / C0 completion](#c--contract-idl-emitter--c0-completion)
- [D — Sequencing, dependencies, and the coupling points](#d--sequencing-dependencies-and-the-coupling-points)
- [E — Items that still need a design decision](#e--items-that-still-need-a-design-decision)
- [See also (adjacent loose ends — pointers only)](#see-also-adjacent-loose-ends--pointers-only)

---

## A — mTLS remaining (Phase-3, host-gated)

0022 shipped the reusable mTLS plumbing (KeyCustodian-as-CA + server require-and-validate + client
leaf-present + refresh-ahead + SPIFFE-SAN) and proved it end-to-end on a loopback harness. What remains is
the cross-process wiring that genuinely needs a running Edge gRPC host — all four items are the explicit,
tracked 0022 D7 deferral.

**Canonical detail**: [ADR-0023 "Negative / new work"](../adrs/0023-mtls-workload-identity.md) +
[PHASE_3_EDGE.md §3 "Deferred mTLS machinery to build here"](PHASE_3_EDGE.md#3-scheduled-jobs--edge-as-cron-trigger-receiver)
+ [deliverable 0022 §Honest caveats](../dev/deliverables/0022-mtls-workload-identity.md).

| # | Item | Status | Canonical source | Blocked on |
| - | ---- | ------ | ---------------- | ---------- |
| A1 | Reusable mTLS plumbing (CA issuance, server validate, client present + refresh, SPIFFE-SAN, loopback proof) | ✅ done | [0022 record](../dev/deliverables/0022-mtls-workload-identity.md) | — |
| A2 | Cross-process gRPC `IssueWorkloadCertificate` endpoint (today in-process only; `IKeyCustodianApi` exposes only `GetJwks`) | 📐 specified-deferred | [PHASE_3_EDGE.md §3](PHASE_3_EDGE.md) item (1) | a running Edge gRPC host (PHASE_3 A1) + the C0 gRPC contract for the issuance op |
| A3 | First-leaf bootstrap identity (chicken-and-egg: a workload needs a leaf to mTLS-call KeyCustodian for a leaf) — provisioned by the deployment orchestrator | ✍ not-yet-specified | [ADR-0023 "Negative / new work"](../adrs/0023-mtls-workload-identity.md) + [PHASE_3_EDGE.md §3](PHASE_3_EDGE.md) item (2) | a design decision (see [§E](#e--items-that-still-need-a-design-decision)) |
| A4 | Wire the mTLS server + leaf-refresh client into the running Edge host (shipped client uses an in-process issuer delegate as the dev/harness seam) | 📐 specified-deferred | [PHASE_3_EDGE.md §3](PHASE_3_EDGE.md) item (3) | a running Edge host (PHASE_3 A1) |
| A5 | Channel-rebuild-on-rotation for long-lived gRPC channels (`AddD2WorkloadCertificate` captures the leaf at channel construction; long-lived channels must rebuild to adopt a rotated leaf) | 📐 specified-deferred | [ADR-0023 "Negative / new work"](../adrs/0023-mtls-workload-identity.md) + [PHASE_3_EDGE.md §3](PHASE_3_EDGE.md) item (4) | Edge host wiring (A4) — the host build is where channel-lifetime policy lands |

---

## B — Auth-pivot existing-code reconciliation

The pivot (mint-once-at-Edge + forward-unchanged) made the per-hop `client_credentials` service-identity
surface dead and added new plumbing requirements. The reconciliation splits into what the **active 0023**
deliverable knocks out (dev-first, no host) and what stays **Edge-gated** beyond it.

**Canonical detail**: [deliverable 0023 workspace](../wip/0023-forwarded-token-auth/README.md) (active —
the §2 re-track findings, §4 locked decisions L1–L13, §5 step breakdown) + the
[0021 "Deferred to code" ledger](../dev/deliverables/0021-auth-pivot.md) + the
[PHASE_0_AUTH per-hop validator sequence](PHASE_0_AUTH.md) (checks 0–9, layer-annotated) and minted-claim
set.

### B.1 — The active deliverable: 0023 (forwarded-JWT plumbing + service-identity retirement)

🔄 active on `n/forwarded-token-auth` (PLAN drafted; awaiting sign-off). **The single source of truth for these
items is the [0023 README](../wip/0023-forwarded-token-auth/README.md) — do not duplicate its step detail
here.** Rows below are the headline scope so the roadmap stays scannable.

| # | Item (0023 scope) | Status | Canonical source |
| - | ----------------- | ------ | ---------------- |
| B1 | `D2_INTERNAL_AUDIENCE` constant in `D2.Shared.Auth.Abstractions` (decided in ADR-0022/0022-D8; NOT yet in code — zero code hits today; built in 0023 Step 1) | 🔄 active | [0023 §2b G1 + Step 1](../wip/0023-forwarded-token-auth/README.md) |
| B2 | Request-scoped raw-JWT holder (`IForwardedJwtAccessor`) + the never-logged `ForwardedJwt` wrapper + inbound capture at both transports | 🔄 active | [0023 Step 2](../wip/0023-forwarded-token-auth/README.md) |
| B3 | Per-request `CallCredentials` forwarding-attach (the per-channel-singleton-vs-per-request crux), resolved via the framework-free `IAmbientRequestScopeAccessor` port + its `IHttpContextAccessor`-backed HTTP adapter. **The call-path client interceptor that was originally part of this step is DEFERRED to [B9](#b2--beyond-0023-edge-gated--c0-gated)** (the wire-format needs a real .NET→.NET hop) | 🔄 active | [0023 Step 3](../wip/0023-forwarded-token-auth/README.md) |
| B4 | Emitter auto-wire of `.AddD2ForwardedJwt().AddD2WorkloadCertificate()` into the generated DI registration + KeyCustodian client regen (**couples with [C7](#c--contract-idl-emitter--c0-completion)**) | 🔄 active | [0023 Step 4](../wip/0023-forwarded-token-auth/README.md) |
| B5 | Retire the `client_credentials` service-identity surface (client / call-creds / hosted-service / cache / snapshot / exception / `AddD2ServiceIdentity` / its options + telemetry); PRESERVE token-exchange + workload-certificate | 🔄 active | [0023 Step 5](../wip/0023-forwarded-token-auth/README.md) |
| B6 | Doc / comment reconciliation off the "predates the pivot" framing to steady-state forwarded-token | 🔄 active | [0023 Step 6](../wip/0023-forwarded-token-auth/README.md) |

### B.2 — Beyond 0023 (Edge-gated / C0-gated)

These were named in the [0021 "Deferred to code" ledger](../dev/deliverables/0021-auth-pivot.md) and the
[PHASE_0_AUTH build-state notes](PHASE_0_AUTH.md). They need a running Edge host, the boundary minter, or
the C0 contract emission — so they sit after 0023.

| # | Item | Status | Canonical source | Blocked on |
| - | ---- | ------ | ---------------- | ---------- |
| B7 | Edge `/oauth/token` boundary minter — the ONE mint per request (`aud=d2.internal`, scope = fan-out union, `act` when impersonating; RFC 8693 retained for the boundary mint) | 📐 specified-deferred | [ADR-0022 "Edge mints exactly one…"](../adrs/0022-service-auth-mint-once-forward.md) + [PHASE_3.md A2](PHASE_3.md) | PHASE_3 A2 (token issuance + JWKS) |
| B8 | Anon-JWT minting (Pattern A) — short-lived anon-session token for every unauthenticated visitor | 📐 specified-deferred | [PHASE_3.md "Anon-visitor Pattern A"](PHASE_3.md) + [PHASE_0_AUTH §3.8](PHASE_0_AUTH.md) | PHASE_3 A3 (sets `d2_whois_id`; needs E1 enrichment) |
| B9 | Operational-subset (`PropagatedContext`) reader/writer on .NET → .NET sync gRPC/HTTP hops — incl. the new call-path field (identity half already correct; operational subset not yet wired on sync .NET hops). **Absorbs the call-path / telemetry client `Interceptor` deferred out of 0023 Step 3** (`§13.4`-approved deferral) — 0023 shipped only the JWT-attach rail; the call-path rail rides operational `x-d2-context`, never the JWT, so it lands here with its wire-format + a real hop to traverse | 📐 specified-deferred | [ADR-0022 "Operational-subset propagation…not yet wired"](../adrs/0022-service-auth-mint-once-forward.md) + [PHASE_0_AUTH build-state](PHASE_0_AUTH.md) + [0023 Step 3 C2 deferral](../wip/0023-forwarded-token-auth/03-outbound-callcredentials/journal.md) | a service-to-service .NET call path (first appears with a second .NET service / Edge→backend) |
| B10 | Build-time caller-scopes ⊇ callee-scopes check (`@d2Calls`-style edge annotation) | 📐 specified-deferred | [ADR-0022 "The build statically verifies scope consistency…"](../adrs/0022-service-auth-mint-once-forward.md) | C0 (additive emitter output — see [C6](#c--contract-idl-emitter--c0-completion)) |
| B11 | TS BFF `InternalToken*` → boundary-token rename (e.g. `EdgeBoundaryToken*`) + the BFF forwarding path. The BFF `client_credentials` token is a LEGITIMATE external-client-of-Edge boundary token (survivor — do NOT retire); only the *name* collides with the `d2.internal` forwarding | 📐 specified-deferred | [0023 §2c finding (d) + §7](../wip/0023-forwarded-token-auth/README.md) | PHASE_3 BFF (Phase 7) — rename blast radius is the `@d2/grpc-client` consumers |
| B12 | `contracts/*.spec.json` docstring fixes ("Updates on every token exchange") + the `ts-codegen` emitters + regenerate `.g.*` | 📐 specified-deferred | [0021 "Deferred to code"](../dev/deliverables/0021-auth-pivot.md) | none structural — entangled with the `act`-chain-stops-at-internal-hop semantics; can go with B9 or standalone |
| B13 | Over-the-wire mint↔validate parity test (a forwarded `aud=d2.internal` token mints at Edge and re-validates at a receiver) | 📐 specified-deferred | [0021 C2 follow-up](../dev/deliverables/0021-auth-pivot.md) + [0023 L13/R7](../wip/0023-forwarded-token-auth/README.md) | a running minter + validator (PHASE_3 A2) — 0023 proves byte-unchanged forwarding in isolation only |
| B14 | Emit `D2_INTERNAL_AUDIENCE` to the TS runtime (the `.NET` constant lands in B1; the TS side mirrors it) | ✍ not-yet-specified | [0021 "Deferred to code"](../dev/deliverables/0021-auth-pivot.md) (declare + emit in both runtimes) | a decision on the TS emission mechanism (see [§E](#e--items-that-still-need-a-design-decision)) |
| B15 | Wire the forwarded-JWT outbound plumbing into the running Edge gRPC host (the credential + `.AddD2ForwardedJwt()` chain + `AddD2ForwardedJwtOutbound()` are dev-first / loopback-proven; attaching them to a live Edge host is host-gated — the forwarded-token **sibling of the mTLS host-wiring item [A4](#a--mtls-remaining-phase-3-host-gated)**) | 📐 specified-deferred | [ADR-0022 §Realization "the reusable plumbing is built…ahead of a running Edge gRPC host; wiring it into an Edge host follows when that host exists"](../adrs/0022-service-auth-mint-once-forward.md) + [0023 §3 / §5 Deferred](../wip/0023-forwarded-token-auth/README.md) | a running Edge host (PHASE_3 A1) — `api/` is `.gitkeep` today |
| B16 | gRPC-inbound-only forwarding-host adapter for the ambient-scope port. The shipped `IHttpContextAccessor`-backed `IAmbientRequestScopeAccessor` adapter lives in `auth/http` and covers the ONLY in-scope forwarding host (Edge: HTTP-inbound). A future **backend→backend gRPC-inbound-only** forwarding host has no adapter registered — the credential's `GetRequiredService<IAmbientRequestScopeAccessor>()` would throw at channel build — so it must ship its own gRPC-side adapter when it exists (the `auth/http`↔`auth/grpc` no-inter-dep rule prevents a single shared adapter type) | 📐 specified-deferred | [0023 Step 3 §0 locked decision + Deviation 2](../wip/0023-forwarded-token-auth/03-outbound-callcredentials/journal.md) + `AddD2ForwardedJwtOutbound()` XML doc (`auth/outbound/AuthOutboundServiceCollectionExtensions.cs`) | the gRPC-inbound forwarding host itself (rides B9 / backend→backend — first appears with a second .NET service) |
| B17 | Identity for genuinely system-initiated calls (a scheduled job / background worker with no inbound user request). The forwarding credential hard-fails `Unauthenticated` when no ambient request scope exists (the correct fail-loud behavior today); such callers must carry their OWN minted identity, designed when they exist | ✍ not-yet-specified | [ADR-0022 §Realization "genuinely system-initiated calls…carry their own identity and are handled when they exist"](../adrs/0022-service-auth-mint-once-forward.md) + `ForwardedJwtCallCredentials.cs` XML-doc caveat | a scheduled-jobs / background-worker execution path to exist (PHASE_3 Edge scheduled-jobs receiver) + a design decision (see [§E](#e--items-that-still-need-a-design-decision)) |

---

## C — Contract-IDL emitter / C0 completion

C0 (the unified operation-contract IDL — [ADR-0021](../adrs/0021-unified-operation-contract-idl.md)) is
[PHASE_3.md](PHASE_3.md)'s **☐ Next** foundational deliverable, but a substantial emitter fleet already
accumulated on `n/typespec-emitters` without a formal PLAN→SHIP deliverable record. ADR-0021 specifies a
**seven-emitter** fleet; the package today ships the DTO (C#/TS), proto, gRPC-service, handler-interface,
façade, route-policy, and idempotency-store-seam emitters — but **not** the OpenAPI-extension emitter or the
parity-test emitter, and several scalar/type and binding gaps are deliberately deferred (loud `D2TSP*`
diagnostics, not silent gaps).

**Canonical detail**: [ADR-0021 "The D² emitter fleet — seven emitters"](../adrs/0021-unified-operation-contract-idl.md)
+ the emitter package itself (`server/shared/typescript/typespec-emitters/` — `src/emitter.ts` `$onEmit`,
`src/lib.ts` D2TSP* catalog, `src/lib/scalar-registry.ts`, `VALIDATION.md` deferral ledger) +
[PHASE_3.md C0 row](PHASE_3.md).

| # | Item | Status | Canonical source | Blocked on |
| - | ---- | ------ | ---------------- | ---------- |
| C1 | Built emitters: C# DTO, TS DTO, proto, gRPC service + transport mappers, handler interface, façade, route+policy, idempotency-gate seam (+ the `@d2*` decorator vocabulary + the proven dual REST+gRPC binding) | ✅ done (on branch) | emitter package `src/emitter.ts` + `src/lib/` | — |
| C2 | OpenAPI (D² extension layer) emitter — the `x-d2-*` policy extensions the stock `@typespec/openapi3` emitter cannot surface (ADR-0021 names it as one of the seven; not present in `$onEmit`) | ✍ not-yet-specified→build | [ADR-0021 emitter table](../adrs/0021-unified-operation-contract-idl.md) | nothing — buildable now (dev-first, no host) |
| C3 | Parity-test emitter — generates the cross-language + registry-existence validation tests (scope-exists, error-code-exists, C#↔TS field/optionality/casing parity, REST↔gRPC same-type, route uniqueness, handler-resolves, known audience/tier). ADR-0021 calls this "a primary justification for the whole system"; not present in `$onEmit` | ✍ not-yet-specified→build | [ADR-0021 "Parity / validation tests are a first-class output"](../adrs/0021-unified-operation-contract-idl.md) | nothing — buildable now |
| C4 | SSE / `@d2ServerPush` binding emission — today `@d2ServerPush` is read only as an exposure marker (routes DTOs); there is no `text/event-stream` (`data:`/`event:`) binding emitter. ADR-0021 keeps the SSE *binding* in the named hand-written fringe, so decide emit-vs-fringe | ✍ not-yet-specified | [ADR-0021 "named, non-growing hand-written fringe"](../adrs/0021-unified-operation-contract-idl.md) + `src/emitter.ts:526` | a design decision (see [§E](#e--items-that-still-need-a-design-decision)) |
| C5 | Temporal scalars (`utcDateTime` / `plainDate` / `plainTime` / `offsetDateTime` / `duration`) — currently loud `D2TSP001`; the registry defers them pending NodaTime ↔ `DateTimeOffset` mapping decisions | 📐 specified-deferred | `src/lib/scalar-registry.ts:5-15` (the deferral is documented in code) | a NodaTime/`DateTimeOffset`/TS mapping decision (see [§E](#e--items-that-still-need-a-design-decision)) |
| C6 | Enum / union property types — currently loud `D2TSP002` ("not yet supported by the DTO emitter") | 📐 specified-deferred | `src/lib.ts` D2TSP002 + `src/lib/model-walk.ts:220,246` | nothing structural — buildable now (the deferral is a capability gap, not a blocked one) |
| C7 | Emitter auto-wire of the outbound forwarded-JWT + workload-cert DI chain (replaces the dead "host MUST chain `.AddD2ServiceIdentity()`" docstring at `grpc-client-emitter.ts:631-639`) — **this is 0023 [B4](#b--auth-pivot-existing-code-reconciliation), landed in the emitter; it is a C0-correctness fix AND a 0023 deliverable** | 🔄 active | [0023 Step 4](../wip/0023-forwarded-token-auth/README.md) + `src/lib/grpc-client-emitter.ts:631-639` | — (in flight under 0023) |
| C8 | Real Edge HTTP-idempotency-store impl behind the generated seam (the emitter generates `D2GeneratedIdempotencyStore.g.cs`; "the real Edge HTTP-idempotency middleware will implement this seam") | 📐 specified-deferred | `src/lib/idempotency-gate-emitter.ts:5-10` + [PHASE_3_EDGE.md §1](PHASE_3_EDGE.md) | a running Edge host (PHASE_3 E2 — cross-cutting middleware) |
| C9 | Formal C0 deliverable CLOSEOUT — the emitter work accumulated on `n/typespec-emitters` without a PLAN→SHIP deliverable record; PHASE_3.md still lists C0 as ☐ Next. Reconcile: a deliverable record + a SHIP, OR re-scope C0 to "remaining emitter gaps (C2/C3/C4/C5/C6) + closeout" | ✍ not-yet-specified | [PHASE_3.md C0 row](PHASE_3.md) (status reconciliation) | an orchestrator/user decision on how to record the already-built work |

---

## D — Sequencing, dependencies, and the coupling points

### What can go NOW (dev-first, no running Edge host)

- **0023** in full ([B1–B6](#b1--the-active-deliverable-0023-forwarded-jwt-plumbing--service-identity-retirement)) — it is *defined* as the dev-first half (build + unit-test + loopback in isolation; Edge wiring explicitly out of scope).
- **The host-independent C0 emitter gaps** — [C2](#c--contract-idl-emitter--c0-completion) (OpenAPI extension), [C3](#c--contract-idl-emitter--c0-completion) (parity-test), [C6](#c--contract-idl-emitter--c0-completion) (enum/union). [C5](#c--contract-idl-emitter--c0-completion) (temporal scalars) is buildable once its mapping decision is made. These are pure codegen — no host.
- **[B12](#b2--beyond-0023-edge-gated--c0-gated)** (spec docstring + `ts-codegen` fixes) is host-independent codegen, though it pairs naturally with B9.

### What is BLOCKED on a running Edge host (PHASE_3 A1 stands the host up)

- **All of [A2–A5](#a--mtls-remaining-phase-3-host-gated)** (the four mTLS cross-process items).
- **[B7](#b2--beyond-0023-edge-gated--c0-gated)** (boundary minter — PHASE_3 A2), **[B8](#b2--beyond-0023-edge-gated--c0-gated)** (anon-mint — A3), **[B13](#b2--beyond-0023-edge-gated--c0-gated)** (over-the-wire parity test — needs a live minter+validator), **[C8](#c--contract-idl-emitter--c0-completion)** (real idempotency store — E2).
- **[B15](#b2--beyond-0023-edge-gated--c0-gated)** (wire the forwarded-JWT outbound plumbing into the running Edge host — the forwarded-token sibling of the mTLS [A4](#a--mtls-remaining-phase-3-host-gated) host-wiring; both land when the Edge host exists).
- **[B9](#b2--beyond-0023-edge-gated--c0-gated)** (sync .NET operational-subset, now also absorbing the call-path interceptor deferred out of 0023) + **[B16](#b2--beyond-0023-edge-gated--c0-gated)** (gRPC-inbound-only forwarding-host adapter) need an actual service-to-service .NET call path to exist.
- **[B11](#b2--beyond-0023-edge-gated--c0-gated)** (BFF token rename + forwarding) is Phase-7 (BFF rebuild).

### Coupling points (where one item is load-bearing for another)

1. **The emitter auto-wire ([B4](#b--auth-pivot-existing-code-reconciliation) = [C7](#c--contract-idl-emitter--c0-completion)) is the same change in two ledgers.** It is both a 0023 deliverable step (retire the dead `.AddD2ServiceIdentity()` guidance) and a C0-emitter-correctness fix (the generated client must auto-wire mandatory outbound auth). It ships once, under 0023. C0 inherits it.
2. **[B10](#b2--beyond-0023-edge-gated--c0-gated) (build-time scope-superset check) is an additive C0 emitter output**, not standalone auth work — it rides the contract IDL's `@d2Calls`-edge emission.
3. **[A2](#a--mtls-remaining-phase-3-host-gated) (gRPC issuance endpoint) needs the C0 gRPC contract** for the `IssueWorkloadCertificate` op — it is an endpoint, so per ADR-0021's foundational ordering C0 should precede it.
4. **[B1](#b1--the-active-deliverable-0023-forwarded-jwt-plumbing--service-identity-retirement) (`D2_INTERNAL_AUDIENCE` constant) is a prerequisite** for B7 (the minter sets `aud` to it) and B13 (the validator checks it) and B14 (the TS mirror).

### Recommended next-deliverable order

1. **0023** — finish the active forwarded-JWT plumbing + service-identity retirement (in flight). Unblocks the auth-reconciliation debt and lands the emitter auto-wire (B4/C7).
2. **C0 closeout + remaining host-independent emitter gaps** — decide [C9](#c--contract-idl-emitter--c0-completion) (how to record the already-built fleet), then build [C2](#c--contract-idl-emitter--c0-completion) (OpenAPI extension), [C3](#c--contract-idl-emitter--c0-completion) (parity-test), [C6](#c--contract-idl-emitter--c0-completion) (enum/union), and [C5](#c--contract-idl-emitter--c0-completion) (temporal scalars, once mapped). This is the "complete the emitters entirely" path and it needs no host. Resolve [C4](#c--contract-idl-emitter--c0-completion) (SSE emit-vs-fringe) as part of the closeout.
3. **PHASE_3 A1** (Edge host shell) — the gate that unblocks everything host-dependent. Tracked in [PHASE_3.md](PHASE_3.md), not here.
4. **Then, host-gated, in PHASE_3 order**: A2 (→ B7 boundary minter; A2 mTLS issuance endpoint), A3 (→ B8 anon-mint), E1/E2 (→ C8 idempotency store), and the mTLS host wiring [A4/A5](#a--mtls-remaining-phase-3-host-gated). [B9](#b2--beyond-0023-edge-gated--c0-gated)/[B13](#b2--beyond-0023-edge-gated--c0-gated) fall out once a real call path + minter exist. [B10](#b2--beyond-0023-edge-gated--c0-gated) rides C0's call-edge emission whenever a declared A-calls-B edge first appears.
5. **Phase 7**: [B11](#b2--beyond-0023-edge-gated--c0-gated) (BFF token rename + forwarding) with the BFF rebuild.

---

## E — Items that still need a design decision

These are the rows above marked ✍ not-yet-specified that carry a genuine open question (as opposed to "spec
locked, code deferred"). Each needs a decision before it can be planned.

- **[A3](#a--mtls-remaining-phase-3-host-gated) — first-leaf bootstrap identity.** How a workload obtains its *first* leaf before it can mTLS-call KeyCustodian (chicken-and-egg). ADR-0023 says "provisioned by the deployment orchestrator from a secret" but the mechanism is not designed. Needs an ADR or a PHASE_3 design note.
- **[B14](#b2--beyond-0023-edge-gated--c0-gated) — `D2_INTERNAL_AUDIENCE` to the TS runtime.** The `.NET` constant is hand-declared (NOT spec-mirrored — it is the universal *receive* audience, never an exchange target, so it is deliberately out of `audiences.spec.json`). The TS side needs the same value, but the emission mechanism (hand-declared TS constant vs a dedicated single-entry spec vs piggybacking an existing emitter) is undecided — and must avoid recreating a spec-mirror DTO.
- **[C4](#c--contract-idl-emitter--c0-completion) — SSE / `@d2ServerPush` binding: emit vs fringe.** ADR-0021 lists the SSE *binding* in the named hand-written fringe (the ndjson-vs-`text/event-stream` gap is universal), yet `@d2ServerPush` is a first-class decorator. Decide whether the emitter generates the `text/event-stream` binding scaffold or it stays hand-written per the fringe policy.
- **[C5](#c--contract-idl-emitter--c0-completion) — temporal scalar mapping.** The scalar registry defers `utcDateTime`/`plainDate`/`plainTime`/`offsetDateTime`/`duration` pending the NodaTime ↔ `DateTimeOffset` ↔ TS decisions. The mapping must align with the existing `D2.Shared.Time` / `@d2/time` conventions and the wire `DateTimeOffset?` rule ([TIMESTAMPS.md](../TIMESTAMPS.md)) before the registry entries land.
- **[C9](#c--contract-idl-emitter--c0-completion) — C0 record reconciliation.** Whether to write a retroactive C0 deliverable record for the already-built fleet and SHIP it, or re-scope C0 in PHASE_3.md to the remaining gaps + a closeout. A bookkeeping decision, but it gates flipping PHASE_3.md's C0 row off ☐ Next.
- **[B17](#b2--beyond-0023-edge-gated--c0-gated) — identity for system-initiated calls.** A scheduled job / background worker has no inbound user request, so no forwarded JWT — the forwarding credential correctly hard-fails `Unauthenticated`. How such a caller obtains its OWN identity (a self-minted service token, a dedicated boundary mint, or mTLS-only with a synthetic context) is undesigned; it surfaces when the first system-initiated execution path (the Edge scheduled-jobs receiver) is built.

---

## See also (adjacent loose ends — pointers only, NOT absorbed here)

These are tracked elsewhere and are *not* part of the auth-pivot / emitter scope; listed so the roadmap is
not mistaken for the whole open-work picture:

- **Pre-existing `D2.Shared.Tests` OTel/CORS flake** — a MeterProvider registration race under the full
  parallel suite (passes isolated). Tracked in the [0022 record §Honest caveats](../dev/deliverables/0022-mtls-workload-identity.md);
  not a pivot/emitter item.
- **`server/web` `D2Result.<factory>` static-call gap** — latent BFF type errors (v2 factories are module
  functions, not statics); unsurfaced because `server/web` isn't host-typechecked yet. Tracked as its own
  follow-up (sweep when the web container is stood up); not a pivot/emitter item.
