<!--
Copyright (c) DCSV. All rights reserved.
-->

# POST_PIVOT_ROADMAP.md — The single source of truth for all deferred / remaining / Edge-wire-up work

**Status**: living roadmap — **THE canonical, committed tracker for ALL deferred, remaining, and
must-wire-up-later work** across the framework. If a piece of work is deferred (specified-but-not-built,
host-gated, design-pending, or built-as-a-seam awaiting a real consumer), it has a row HERE. The Edge /
middleware build runs off this ONE checklist — nothing deferred is allowed to live only in a per-deliverable
ledger, a phase-doc deferral section, or a code comment, where the eventual builder would never find it.

**The "post-pivot" name is historical.** The file was born to track the auth-pivot (ADR-0022 / ADR-0023) +
the contract-IDL emitter fleet (ADR-0021 / C0) remainder, but its scope is now the full deferred-work
surface. The filename is kept as-is to preserve inbound links; the framing is broadened.

**This doc is the INDEX of record; detail sources are DETAIL, never the sole home.** Per-deliverable
ledgers (e.g. the 0019 `VALIDATION.md` replace-trigger ledger, the 0021 "Deferred to code" list) and
phase-doc deferral sections ([PHASE_3_EDGE.md §3](PHASE_3_EDGE.md), [PHASE_0_AUTH.md](PHASE_0_AUTH.md)
build-state, the [PHASE_3.md](PHASE_3.md) DAG) carry the deep detail and stay the canonical *owner* of
each item's design — but no deferral may be reachable ONLY from one of them. This roadmap consolidates and
points at them. If you defer work anywhere, you add (or confirm) a row here in the same change.

**This doc LINKS, it does not DUPLICATE.** Every row points at the canonical source that owns the detail
(an ADR, a phase tracking doc, a deliverable record, or the active deliverable workspace). When a source
says something different from a row here, the source wins and this row is stale — fix the row. The value
here is the cross-cutting *complete-inventory + sequencing + blocked-on* view that no single source carries,
not a re-statement of any of them.

**How to read the status column** (one vocabulary across all four areas):

| Status | Meaning |
| ------ | ------- |
| ✅ done | Built + tested + shipped (or merged on its branch). The row is here for completeness / sequencing context. |
| 🔄 active | Active right now in a named deliverable. |
| 📐 specified-deferred | The design is locked (an ADR / deliverable decided it) but the code is deliberately deferred, with a tracked to-be-done note. |
| ✍ not-yet-specified | Needs a design decision before it can be built — no locked spec yet. Flagged in [§E](#e--items-that-still-need-a-design-decision). |

**Scope boundary — inventory vs DAG.** This roadmap is the complete *inventory* of deferred / remaining /
seam-binding work: the auth-pivot reconciliation, the mTLS cross-process remainder, the contract-IDL (C0)
emitter completion, the Edge / middleware seam-bindings, and the 0019 emitter-fleet finish-list. It does
NOT *re-plan* the forward Phase-3 build DAG (the auth track, the Edge-pipeline track) — that sequencing
plan lives in [PHASE_3.md](PHASE_3.md). The two are complementary: PHASE_3.md is the *build order*; this is
the *deferred-work checklist* the build must drain. Where an item here is *blocked on* a Phase-3 deliverable,
the row names it. New deliverables that are forward-build (not draining a deferral) belong in PHASE_3.md,
not here.

---

## Table of contents

- [A — mTLS remaining (Phase-3, host-gated)](#a--mtls-remaining-phase-3-host-gated)
- [B — Auth-pivot existing-code reconciliation](#b--auth-pivot-existing-code-reconciliation)
- [C — Contract-IDL emitter / C0 completion](#c--contract-idl-emitter--c0-completion)
- [F — Edge / middleware seam-binding (generated markers awaiting their real consumer)](#f--edge--middleware-seam-binding-generated-markers-awaiting-their-real-consumer)
- [G — Edge / middleware wire-up checklist (the seam→real-consumer master list)](#g--edge--middleware-wire-up-checklist-the-seamreal-consumer-master-list)
- [D — Sequencing, dependencies, and the coupling points](#d--sequencing-dependencies-and-the-coupling-points)
- [E — Items that still need a design decision](#e--items-that-still-need-a-design-decision)
- [H — Cross-cutting deferrals tracked outside this index (pointers)](#h--cross-cutting-deferrals-tracked-outside-this-index-pointers-deep-tracked-elsewhere)

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

### B.1 — The active deliverable: 0023 (forwarded-JWT plumbing + service-identity retirement) <sup>✅ SHIPPED</sup>

✅ SHIPPED 2026-06-20 on `n/forwarded-token-auth` (converged + governed; FINAL-REVIEW converged in 2 rounds). **The
single source of truth for these items is the [0023 deliverable record](../dev/deliverables/0023-forwarded-token-auth.md) —
do not duplicate its step detail here.** Rows below are the headline scope so the roadmap stays scannable.

| # | Item (0023 scope) | Status | Canonical source |
| - | ----------------- | ------ | ---------------- |
| B1 | `D2_INTERNAL_AUDIENCE` constant in `D2.Shared.Auth.Abstractions` (decided in ADR-0022/0022-D8; built in 0023 Step 1 as the hand-declared UPPER_CASE receive-audience constant) | ✅ done in 0023 (`63ace1ef`) | [0023 record S1](../dev/deliverables/0023-forwarded-token-auth.md) |
| B2 | Request-scoped raw-JWT holder (`IForwardedJwtAccessor`) + the never-logged `ForwardedJwt` wrapper + inbound capture at both transports | ✅ done in 0023 (`d0de85ec`) | [0023 record S2](../dev/deliverables/0023-forwarded-token-auth.md) |
| B3 | Per-request `CallCredentials` forwarding-attach (the per-channel-singleton-vs-per-request crux), resolved via the framework-free `IAmbientRequestScopeAccessor` port + its `IHttpContextAccessor`-backed HTTP adapter. **The call-path client interceptor that was originally part of this step is DEFERRED to [B9](#b2--beyond-0023-edge-gated--c0-gated)** (the wire-format needs a real .NET→.NET hop) | ✅ done in 0023 (`198ad482`) | [0023 record S3](../dev/deliverables/0023-forwarded-token-auth.md) |
| B4 | Emitter auto-wire of `.AddD2ForwardedJwt().AddD2WorkloadCertificate()` into the generated DI registration + KeyCustodian client regen (**couples with [C7](#c--contract-idl-emitter--c0-completion)**) | ✅ done in 0023 (`c7264318`) | [0023 record S4](../dev/deliverables/0023-forwarded-token-auth.md) |
| B5 | Retire the `client_credentials` service-identity surface (client / call-creds / hosted-service / cache / snapshot / exception / `AddD2ServiceIdentity` / its options + telemetry); PRESERVE token-exchange + workload-certificate | ✅ done in 0023 (`538b9bc3`) | [0023 record S5](../dev/deliverables/0023-forwarded-token-auth.md) |
| B6 | Doc / comment reconciliation off the "predates the pivot" framing to steady-state forwarded-token | ✅ done in 0023 (`7335ae80`) | [0023 record S6](../dev/deliverables/0023-forwarded-token-auth.md) |

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
| B16 | gRPC-inbound-only forwarding-host adapter for the ambient-scope port. `AddD2AuthGrpc()` now registers `GrpcHttpContextAmbientRequestScopeAccessor` — the gRPC-inbound sibling of the `auth/http` `HttpContextAmbientRequestScopeAccessor` — so a **backend→backend gRPC-inbound** forwarding host self-wires the `IAmbientRequestScopeAccessor` read-back door the outbound credential's `GetRequiredService<IAmbientRequestScopeAccessor>()` needs (a deliberate tiny duplicate, since the `auth/http`↔`auth/grpc` no-inter-dep rule prevents a single shared adapter type). Both inbound transports now self-wire the port; dual-transport hosts are first-wins-safe (both adapters read the same door). | ✅ done in 0023 — gRPC-inbound sibling adapter | [0023 Step 5 (B16)](../wip/0023-forwarded-token-auth/05-grpc-inbound-forwarding-adapter/journal.md) + `auth/grpc/Ambient/GrpcHttpContextAmbientRequestScopeAccessor.cs` | — (closed; the live forwarding into a running gRPC-inbound host still rides B15 host-wiring) |
| B17 | Identity for genuinely system-initiated calls (a scheduled job / background worker with no inbound user request). The forwarding credential hard-fails `Unauthenticated` when no ambient request scope exists (the correct fail-loud behavior today); such callers must carry their OWN minted identity, designed when they exist | ✍ not-yet-specified | [ADR-0022 §Realization "genuinely system-initiated calls…carry their own identity and are handled when they exist"](../adrs/0022-service-auth-mint-once-forward.md) + `ForwardedJwtCallCredentials.cs` XML-doc caveat | a scheduled-jobs / background-worker execution path to exist (PHASE_3 Edge scheduled-jobs receiver) + a design decision (see [§E](#e--items-that-still-need-a-design-decision)) |
| B18 | **`@d2/auth-bff-client` package missing — `server/web` typechecks blocked.** `server/web/package.json` declares `"@d2/auth-bff-client": "workspace:*"` but the package does not exist in `pnpm-workspace.yaml` — so `pnpm install` for `server/web` fails with `ERR_PNPM_WORKSPACE_PKG_NOT_FOUND` and `svelte-check` (+ any TS typechecking of the BFF) cannot run at all. The `D2Result.<factory>` static-call gap noted in [§H](#h--cross-cutting-deferrals-tracked-outside-this-index-pointers-deep-tracked-elsewhere) is itself already fixed against `@d2/result`'s `.d.ts`, but full `server/web` typecheck verification is blocked until this package exists. **What needs doing**: create the `@d2/auth-bff-client` package + add it to `pnpm-workspace.yaml`, then `svelte-check` can run end-to-end. This is BFF rebuild work. | 📐 specified-deferred | `server/web/package.json` (the `workspace:*` dep declaration) + `pnpm-workspace.yaml` (missing entry) | PHASE_3 BFF rebuild (Phase 7 — [B11](#b2--beyond-0023-edge-gated--c0-gated)) — the package is part of the BFF auth forwarding surface that doesn't exist yet |

---

## C — Contract-IDL emitter / C0 completion

C0 (the unified operation-contract IDL — [ADR-0021](../adrs/0021-unified-operation-contract-idl.md)) is
[PHASE_3.md](PHASE_3.md)'s **☐ Next** foundational deliverable, but a substantial emitter fleet already
accumulated on `n/typespec-emitters` without a formal PLAN→SHIP deliverable record. ADR-0021 specifies a
**seven-emitter** fleet; the package today ships the DTO (C#/TS), proto, gRPC-service, handler-interface,
façade, route-policy, and idempotency-store-seam emitters — but **not** the OpenAPI-extension emitter or the
parity-test emitter, and several scalar/type and binding gaps are deliberately deferred (loud `D2TSP*`
diagnostics, not silent gaps).

The emitter work was tracked as deliverable 0019 (`@d2/typespec-emitters`) but **never reached
FINAL-REVIEW or SHIP** — its remaining steps (the .NET gRPC-client validation harness, the TS client
emitter, the `@d2Resilience` custom-predicate emitter, the over-the-wire tests, the harness + parity
consolidation, and the FINAL-REVIEW/SHIP/record) lived only in the gitignored 0019 workspace journal until
rescued into the rows below ([C10–C16](#c--contract-idl-emitter--c0-completion)). They are **host-independent**
and are the active completion front for C0.

**Canonical detail**: [ADR-0021 "The D² emitter fleet — seven emitters"](../adrs/0021-unified-operation-contract-idl.md)
+ the emitter package itself (`server/shared/typescript/typespec-emitters/` — `src/emitter.ts` `$onEmit`,
`src/lib.ts` D2TSP* catalog, `src/lib/scalar-registry.ts`, `VALIDATION.md` deferral ledger) +
[PHASE_3.md C0 row](PHASE_3.md).

| # | Item | Status | Canonical source | Blocked on |
| - | ---- | ------ | ---------------- | ---------- |
| C1 | Built + validated emitters: C# DTO, TS DTO, proto, gRPC service + transport mappers, handler interface, façade, route+policy, idempotency-gate seam (+ the `@d2*` decorator vocabulary + the proven dual REST+gRPC binding) — eight emitters, each with byte-gate fixtures, C# harness validation, and a `VALIDATION.md` ledger row | ✅ done (on branch) | emitter package `src/emitter.ts` + `src/lib/` + `VALIDATION.md` | — |
| C1a | **.NET gRPC-client emitter — source built + wired AND VALIDATED.** The emitter source (`grpc-client-emitter.ts`) is written and dispatched at `emitter.ts:459`, auto-chains `.AddD2ForwardedJwt().AddD2WorkloadCertificate()` (the [C7](#c--contract-idl-emitter--c0-completion) auto-wire), AND its validation harness is on disk: committed fixtures in `…/TypeSpecGrpc/Generated/` (`IKeyCustodianGrpcClient.g.cs` / `KeyCustodianGrpcClient.g.cs` / `SignClientMappers.g.cs` / `KeyCustodianGrpcClientsGenerated.g.cs` / `SignClientKeys.g.cs`), `GrpcClientTests.cs` (16 cases — the captured-envelope matrix vs the REAL `D2.Shared.Resilience` keyed pipeline + REAL `D2.Shared.Result.Grpc` envelope mapper: `ValidationFailed` NOT retried at `CallCount==1`, transient retried at `CallCount>1`, recovery at `CallCount==2`, envelope fidelity, §1.3 DI resolution), the 5-block byte-gate (each `+ *DRIFTED`), and the `VALIDATION.md` gRPC-client behavioral + byte-parity rows. **MUT-1 (production `Clients.csproj` gRPC-client wiring) is N/A, NOT deferred-incomplete**: there is no real `@d2GrpcMethod` op (the only gRPC ops are fixtures; KC's real surface is in-process `getJwks`, already a real emitter consumer). The first real gRPC consumer wires the generated client when a service declares a real gRPC op. The host-gated channel-address + outbound-auth wiring stay Edge-gated (§G rows 227/228, valid). **User decision (2026-06-20): ship 0019 emitter-complete.** | ✅ done | `src/lib/grpc-client-emitter.ts` (source) + `…/TypeSpecGrpc/Generated/` fixtures + `GrpcClientTests.cs` + `VALIDATION.md` (gRPC-client rows) + [PHASE_3.md C0 row](PHASE_3.md) | — (validation complete; the deliverable-level row-flip rides the [C16](#c--contract-idl-emitter--c0-completion) SHIP step) |
| C2 | OpenAPI (D² extension layer) emitter — the `x-d2-*` policy extensions the stock `@typespec/openapi3` emitter cannot surface (ADR-0021 names it as one of the seven). **BUILT + validated.** `src/lib/openapi-emitter.ts` runs the genuine stock `@typespec/openapi3` `getOpenAPI3` for the HTTP shape (NO reimplementation) and layers the four `x-d2-*` extensions (`x-d2-scope` structured `{mode,scopes}` / `x-d2-tier` / `x-d2-audience` / `x-d2-csrf`) from the `@d2*` stateMaps + a doc-level `x-d2-generated-by`, dispatched from `$onEmit` when ≥1 `@service` exists. One document per `@service` × version (versioned fan-out proven non-vacuous via a 2-version `@versioned` fixture). Byte-gated `.g.json` fixtures (+ deliberate-drift negatives), 100% `src/**` coverage, `VALIDATION.md` row. This is the seventh ADR-0021 emitter — the fleet is complete on the emitter-source axis. | ✅ done | `src/lib/openapi-emitter.ts` + `contracts/typespec/fixtures/openapi-shaped.tsp` + `…/TypeSpecOpenApi/Generated/*.openapi.g.json` + `VALIDATION.md` (OpenAPI section) | — (validation complete; the deliverable-level row-flip rides the [C16](#c--contract-idl-emitter--c0-completion) SHIP step) |
| C3 | Parity-test emitter — generates the cross-language + registry-existence validation tests (scope-exists, error-code-exists, C#↔TS field/optionality/casing parity, REST↔gRPC same-type, route uniqueness, handler-resolves, known audience/tier). ADR-0021 calls this "a primary justification for the whole system"; not present in `$onEmit` | ✍ not-yet-specified→build | [ADR-0021 "Parity / validation tests are a first-class output"](../adrs/0021-unified-operation-contract-idl.md) | nothing — buildable now |
| C4 | SSE / `@d2ServerPush` binding emission — today `@d2ServerPush` is read only as an exposure marker (routes DTOs); there is no `text/event-stream` (`data:`/`event:`) binding emitter. ADR-0021 keeps the SSE *binding* in the named hand-written fringe, so decide emit-vs-fringe | ✅ done (dispatch emitter built + validated against `ISseEmitSink` seam; `text/event-stream` wire binding stays named hand-written fringe per ADR-0021; resolved: emit dispatch, fringe binding) | [ADR-0021 "named, non-growing hand-written fringe"](../adrs/0021-unified-operation-contract-idl.md) + `src/lib/sse-dispatch-emitter.ts` + `VALIDATION.md` (SSE-dispatch row) | — |
| C5 | Temporal scalars (`utcDateTime` / `plainDate` / `plainTime` / `offsetDateTime` / `duration`) — currently loud `D2TSP001`; the registry defers them pending NodaTime ↔ `DateTimeOffset` mapping decisions | ✅ done (full lossless temporal table built: all scalar types mapped to NodaTime/`DateTimeOffset`/ISO wire shapes; zone-bearing composite DTOs emitted; C# + TS round-trip fixtures + DST-adversarial + cross-language parity validated; `D2TSP001` lifted for supported scalars) | `src/lib/scalar-registry.ts` + `VALIDATION.md` (temporal rows) + `docs/wip/0019-typespec-emitters/temporal-mapping.md` | — |
| C6 | Enum / union property types — currently loud `D2TSP002` ("not yet supported by the DTO emitter") | ✅ done (enum + union handling added to `walkModel` across all three emit targets: C# `enum` + `[JsonConverter]`, proto named enum, TS string-union; `D2TSP002` lifted for supported shapes; round-trip + cross-language parity validated) | `src/lib/model-walk.ts` + `VALIDATION.md` (enum/union row) | — |
| C7 | Emitter auto-wire of the outbound forwarded-JWT + workload-cert DI chain (replaced the dead "host MUST chain `.AddD2ServiceIdentity()`" docstring) — **this is 0023 [B4](#b--auth-pivot-existing-code-reconciliation), landed in the emitter; it is a C0-correctness fix AND a 0023 deliverable.** The generated DI extension now emits `.AddD2ForwardedJwt()` + `.AddD2WorkloadCertificate()` (`grpc-client-emitter.ts` ~lines 595/672/676) | ✅ done in 0023 (`c7264318`) | [0023 record S4](../dev/deliverables/0023-forwarded-token-auth.md) + `src/lib/grpc-client-emitter.ts` | — |
| C8 | Real Edge HTTP-idempotency-store impl behind the generated seam (the emitter generates `D2GeneratedIdempotencyStore.g.cs`; "the real Edge HTTP-idempotency middleware will implement this seam") | 📐 specified-deferred | `src/lib/idempotency-gate-emitter.ts:5-10` + [PHASE_3_EDGE.md §1](PHASE_3_EDGE.md) | a running Edge host (PHASE_3 E2 — cross-cutting middleware) |
| C9 | Formal C0 deliverable CLOSEOUT — the emitter work accumulated on `n/typespec-emitters` without a PLAN→SHIP deliverable record; PHASE_3.md still lists C0 as ☐ Next. Reconcile: a deliverable record + a SHIP, OR re-scope C0 to "remaining emitter gaps + closeout". Couples with the FINAL-REVIEW/SHIP/record steps in [C16](#c--contract-idl-emitter--c0-completion) | ✅ done (0019 deliverable record shipped to `docs/dev/deliverables/0019-typespec-emitters.md`; PHASE_3.md C0 row flipped to ✅ Shipped; C0 closed out as 0019) | [PHASE_3.md C0 row](PHASE_3.md) + [0019 record](../dev/deliverables/0019-typespec-emitters.md) | — |
| C10 | **TS client emitter (0019 Step 9c)** — per-op typed fns for BOTH surfaces: browser REST client (delegating to `apiCall`/`apiCallAnon`, ProblemDetails → `D2Result`) for `@route` ops + server/SSR gRPC client (over `handleGrpcCall`/`unaryCall` + the `@d2Resilience` predicate retry-arm consuming the C-5 TS twin via the existing `ResilientPipeline`) for `@d2GrpcMethod` ops. `src/lib/ts-grpc-client-emitter.ts` + `src/lib/ts-rest-client-emitter.ts`, dispatched from `$onEmit`; byte-gated `.g.ts` fixtures; the SSR client validated against the REAL `@d2/grpc-client` seam + REAL ts-proto types (real buf/ts-proto), the REST client against a faithful `apiCall` double. | ✅ done (built + validated on the deliverable branch; the BFF composition-root wiring is the host-gated CB7/CB8 deferral) | [ADR-0021 emitter table](../adrs/0021-unified-operation-contract-idl.md) + `server/shared/typescript/typespec-emitters/VALIDATION.md` (TS-client section) | nothing — host-independent (`@d2/grpc-client` `handleGrpcCall`/`unaryCall`/`isTransientGrpcError` + `@d2/protos` + `apiCall`/`apiCallAnon` are the seams) |
| C11 | **`@d2Resilience` custom-predicate emitter (0019 Step 9d)** — reopen the 0018 `@d2/typespec-decorators` `@d2Resilience` decorator to accept a `retryWhen`/`failWhen` arg over the op's output fields / error codes, compile-time-validated, folded into the generated resilience-pipeline config + keyed-DI registration. The 0018 `@d2Resilience` ships simple tunable params only. **Was tracked only in the 0019 workspace journal.** | ✅ done (0018 `@d2/typespec-decorators` reopened: `retryWhen`/`failWhen` optional args + AST parser + closed-registry validator + cross-language C# + TS predicate emission; keyed-DI + `RetryOptions.IsTransient` sentinel wired in generated client; 100%-coverage gate held; no regression to 16 prior decorators) | [ADR-0021](../adrs/0021-unified-operation-contract-idl.md) + `@d2/typespec-decorators` + `src/lib/grpc-client-emitter.ts` + `VALIDATION.md` (predicate rows) | — |
| C12 | **Over-the-wire integration tests (0019 Step 9e)** — real-socket gRPC, self-managed — prove transient-recovery (retry), breaker open/half-open, no amplification (callee `ValidationFailed` NOT retried), byte-fidelity. The in-memory `TestServer` harness proves correctness in-process only; this proves it over a REAL socket (real TCP + TLS + HTTP/2 + protobuf + `RpcException` propagation). **Was tracked only in the 0019 workspace journal.** | ✅ done (real over-the-wire resilience tests in `OverTheWireResilienceTests.cs` over a real Kestrel HTTPS loopback socket — transient-recovery, breaker open/half-open [deterministic via an injected breaker clock], no-amplification on `ValidationFailed` at `CallCount==1`, envelope byte-fidelity, predicate behavior; self-managed Kestrel host, no `dotnet run`. Per the locked design the service runs in-process over a REAL socket rather than a second OS process — same real transport stack, far less harness, cross-platform) | [ADR-0021](../adrs/0021-unified-operation-contract-idl.md) + `D2.Edge.Tests/…/TypeSpecGrpc/OverTheWireResilienceTests.cs` + `VALIDATION.md` (over-the-wire row) | — |
| C13 | **Transport integration-harness consolidation (0019 Step 11)** — consolidate the per-step `TestServer` / in-memory-gRPC harness paths (Steps 7a/7b/9b each spawned their own) into one cross-path end-to-end across REST + gRPC + in-process-leaf, validating generated transport against real middleware. **Was tracked only in the 0019 workspace journal.** | ✅ done (the genuinely-duplicated real-Kestrel-socket host/channel machinery — `StartServerAsync`/`RunningServer`/`ResolveEndpoint`/channel skeleton — extracted into a shared `GrpcTestHost` helper; the mTLS + over-the-wire harness classes re-pointed to it [behavior-preserving, all gates green]. Broader cross-path REST+gRPC+leaf unification was deliberately scoped to this shared-helper extraction per the locked decision) | [ADR-0021](../adrs/0021-unified-operation-contract-idl.md) + `D2.Edge.Tests/…/TypeSpecGrpc/GrpcTestHost.cs` | — |
| C14 | **Parity + byte-gate consolidation + `VALIDATION.md` finalization (0019 Step 12)** — extend regeneration byte-identity coverage to EVERY emitted file (incl. the 9c TS-client output + any SSE output); finalize the per-emitter `VALIDATION.md` replace-trigger ledger with the remaining rows. The 9b gRPC-client AND the 9c TS-client (browser REST + SSR gRPC) byte-gate + behavioral + replace-trigger (CB7/CB8) rows are now present; the consolidated-harness rows remain. **Was tracked only in the 0019 workspace journal.** | ✅ done (byte-identity coverage extended to every emitted file; `VALIDATION.md` finalized — all per-emitter rows present with real-seam vs test-double classification + replace-trigger entries for every deferred consumer binding) | `server/shared/typescript/typespec-emitters/VALIDATION.md` | — |
| C15 | **Enum / union + temporal scalar gaps** — placeholder cross-ref: the two loud-deferral capability gaps that also block full 0019 completion are tracked as [C6](#c--contract-idl-emitter--c0-completion) (enum/union, `D2TSP002`, buildable now) and [C5](#c--contract-idl-emitter--c0-completion) (temporal scalars, `D2TSP001`, mapping-decision-gated). No separate row — listed here so the 0019 finish-list reads complete. | (see C5 / C6) | [C5](#c--contract-idl-emitter--c0-completion) + [C6](#c--contract-idl-emitter--c0-completion) | (per C5 / C6) |
| C16 | **0019 FINAL-REVIEW + SHIP + deliverable record** — the whole-deliverable audit loop (fresh Final-reviewer rounds to convergence), the completeness-attestation block (currently BLANK in the 0019 workspace README), and the SHIP snapshot to `docs/dev/deliverables/0019-typespec-emitters.md`. **Was tracked only in the gitignored 0019 workspace journal — if that journal is deleted before completion, these steps evaporate with no committed record.** This is the concrete execution of the [C9](#c--contract-idl-emitter--c0-completion) closeout decision. | ✅ done (FINAL-REVIEW: 5 fresh K=12 §-cluster rounds — the substantive defect surface [HIGH/MEDIUM + all functional categories] proven CLOSED by absence; 43 findings raised / 43 closed; the residual cosmetic-LOW tail fixed [Fixer + gate-verified]; loop terminated at iteration 5/10 by explicit decision once only pre-existing cosmetic-LOW nits remained; completeness attestation written; 0019 record shipped to `docs/dev/deliverables/0019-typespec-emitters.md`) | [0019 record](../dev/deliverables/0019-typespec-emitters.md) | — |
| C17 | **Proto emitter: optional-presence wrapper path (capability gap).** The proto emitter has NO optional-presence wrapper path for any type — it always emits a bare proto3 scalar (e.g. bare `string`) for an optional field, so a gRPC op that needs proto3 optional-presence semantics cannot distinguish "absent" from "default" on the wire. Today no op exercises this path: 0019's temporal work (`nextFireUtc?`) is only in DTO/JSON form, and no `@d2GrpcMethod` op currently carries a required-absent-vs-default optional scalar. When a gRPC op first needs this, add the `google.protobuf.StringValue`-style wrapper (or proto3 `optional` keyword) path to the proto emitter **and** the corresponding transport-mapper emit path. | 📐 specified-deferred | `server/shared/typescript/typespec-emitters/src/lib/proto-emitter.ts` (emitter source — no optional-presence branch today) + `docs/wip/0019-typespec-emitters/temporal-mapping.md` (deferral note) | nothing structural — buildable now (host-independent); unblocked the moment a `@d2GrpcMethod` op first carries an optional scalar where presence matters |
| C18 | **gRPC transport-mapper: nested-model / array-of-model field path — BUILT (depth-N).** The gRPC client + service transport mappers now recurse per field into a nested proto↔DTO sub-mapper: `nested-model-mapper.ts` emits one deduped `ToProto<Model>` / `To<Model>` sub-mapper per nested model, recursive to arbitrary depth (`repeated <Message>` ↔ list/array; a nullable nested model → a bare proto3 message field — implicit presence, unset = null). The model-walker (`collectNested`) recurses arbitrary depth, **strict fail-loud** — no silent truncation (the prior one-level-truncation + silent-skip was a bug, now corrected). Proven by the **`placeOrderV2`** (depth-2 nested + array-of-model) + **`deepNest`** (depth-3: output → nested model → array-of-model) fixtures, each with a full compiling gRPC client + committed proto + byte-gates (+ drift negatives) + an in-memory-harness round-trip (`PlaceOrderV2RoundTripTests` / `DeepNestRoundTripTests`) proving nested + array fidelity survives proto↔DTO. The server-side mapper recursion is emitted + unit-tested but not committed as a separate `.g.cs` (committing both client + server sub-mappers collides in the shared `…Generated` namespace, CS0121 — matches V1's client-only commit). | ✅ done | `server/shared/typescript/typespec-emitters/src/lib/nested-model-mapper.ts` + `grpc-client-emitter.ts` + `grpc-service-emitter.ts` + `model-walk.ts` + the `…/TypeSpecGrpcPredicate/` V2 + Deep fixtures + round-trip tests | — (built + validated on the in-memory harness; no real `@d2GrpcMethod` consumer required) |

---

## F — Edge / middleware seam-binding (generated markers awaiting their real consumer)

The emitter fleet already stamps **faithful, inert seam markers** onto every generated route and seam — the
markers are present and asserted-present in tests today, but **nothing reads them yet**. When the Edge
middleware exists it MUST be wired to read each marker, or the build ships routes with correct metadata and
zero enforcement (a silent security/operability hole). These were tracked ONLY in emitter source comments /
the 0019 `VALIDATION.md` replace-trigger ledger — not in any committed cross-cutting tracker — so they are
rescued here as committed rows. The actionable seam→consumer view is consolidated in [§G](#g--edge--middleware-wire-up-checklist-the-seamreal-consumer-master-list).

**Canonical detail**: the 0019 `VALIDATION.md` "replace-trigger" ledger + the emitter source replace-trigger
comments (`route-policy-emitter.ts`, `idempotency-gate-emitter.ts`) + [PHASE_3_EDGE.md](PHASE_3_EDGE.md)
(the Edge middleware that becomes the real consumer) + [PHASE_0_AUTH.md §3.8](PHASE_0_AUTH.md) (the anon-JWT
algorithm gap).

| # | Item | Status | Canonical source | Blocked on |
| - | ---- | ------ | ---------------- | ---------- |
| F1 | **Anon-JWT `EffectiveScopes` algorithm gap (CRITICAL — security path).** The shipped `JwtAuthMiddleware` + `JwtAuthInterceptor` scope check is `RequiredScopes.Any(s => ctx.Scopes.Contains(s))` — JWT-scopes-only. Pattern A (anon visitors) requires `EffectiveScopes(ctx) = ctx.Scopes ∪ Scopes.AllAnonymousScopes` and the check changed to `RequiredScopes.Any(s => EffectiveScopes.Contains(s))`. ALSO: `ClaimsToContextMapper` must map the anon claims `d2_kind` / `d2_whois_id` / `d2_fingerprint_score` into the request context. Until this lands, **anon-JWT Pattern A cannot function** — anon visitors with a valid anon token would be denied every anon-scoped op. (Today tokenless non-harmless requests are correctly rejected because Pattern A is unbuilt.) | 📐 specified-deferred | [PHASE_0_AUTH.md §3.8 "Algorithm gap (Phase 3 followup)"](PHASE_0_AUTH.md) (the `EffectiveScopes` formula + the `ClaimsToContextMapper` additions) | the anon-JWT mint ([B8](#b2--beyond-0023-edge-gated--c0-gated)) so a real anon token exists to evaluate; `Scopes.AllAnonymousScopes` is already codegen-emitted from the scopes spec |
| F2 | **Edge rate-limit middleware must READ the generated `D2GeneratedRateLimitTier` marker.** Every generated route carries a faithful `D2GeneratedRateLimitTier` sealed-record metadata marker (asserted PRESENT on endpoint metadata in tests) but with NO enforcement logic. The 18-bucket rate-limiter must call `GetMetadata<D2GeneratedRateLimitTier>()` per route and enforce the tier. Replace-trigger lived only in `route-policy-emitter.ts` source comments. | 📐 specified-deferred | `src/lib/route-policy-emitter.ts` (marker + replace-trigger comment) + the 0019 `VALIDATION.md` replace-trigger ledger + [PHASE_3_EDGE.md](PHASE_3_EDGE.md) / [PHASE_3_RATE_LIMITING.md](PHASE_3_RATE_LIMITING.md) | the Edge rate-limit middleware (PHASE_3 E2) |
| F3 | **Edge CSRF middleware must READ the generated `D2GeneratedCsrfPosture` marker.** Every generated route carries a faithful `D2GeneratedCsrfPosture` sealed-record metadata marker (asserted PRESENT in tests) with NO enforcement. The CSRF middleware must call `GetMetadata<D2GeneratedCsrfPosture>()` per route and enforce the posture. Replace-trigger lived only in `route-policy-emitter.ts` source comments. | 📐 specified-deferred | `src/lib/route-policy-emitter.ts` (marker + replace-trigger comment) + the 0019 `VALIDATION.md` replace-trigger ledger + [PHASE_3_EDGE.md](PHASE_3_EDGE.md) | the Edge CSRF middleware (PHASE_3 E2) |
| F4 | **Keyring distribution endpoint + its consumer wiring.** `D2.Shared.Auth.Keyring` ships consumer-side types (`IKeyringClient`, `GrpcKeyringClient`, `RabbitMqRotationEventChannel`) but they have NO endpoint to talk to: the KeyCustodian gRPC `internal/keys/{domain}` distribution endpoint (PHASE_3 E5) does not exist, and `GrpcKeyringClient` is not wired to it (and `RabbitMqRotationEventChannel` needs messaging). PHASE_3.md mentions E5 but the specific keyring-distribution wiring was untracked here. | 📐 specified-deferred | [PHASE_0_AUTH.md §3.5](PHASE_0_AUTH.md) (the keyring-distribution design) + [PHASE_3.md E5 row](PHASE_3.md) + `server/shared/dotnet/auth/keyring/` README | the KeyCustodian gRPC `internal/keys/{domain}` endpoint (PHASE_3 E5) — needs a running Edge/KeyCustodian gRPC host + messaging |

---

## G — Edge / middleware wire-up checklist (the seam→real-consumer master list)

**This is THE actionable list the Edge / middleware build consumes.** Every emitter output and shared-lib
seam that exists today as a faithful test-double / inert marker, the real consumer that must wire it, and
the exact replace-trigger. When the Edge host + middleware land, walk this table top-to-bottom — each row is
"this seam is inert until you wire it." Rows here are the *consumer-binding* view of items tracked in
[§A](#a--mtls-remaining-phase-3-host-gated) / [§B](#b--auth-pivot-existing-code-reconciliation) /
[§C](#c--contract-idl-emitter--c0-completion) / [§F](#f--edge--middleware-seam-binding-generated-markers-awaiting-their-real-consumer);
the `Tracked as` column points back at the owning row so nothing is duplicated as a separate work item — this
is a *cross-cut*, not a new backlog.

**Source**: the 0019 `VALIDATION.md` replace-trigger ledger (the per-emitter "test double → real consumer"
rows) + the seam markers in the emitter sources.

| Seam / test-double / marker that exists TODAY | Real consumer that must wire it | Replace-trigger (when to do it) | Tracked as |
| --------------------------------------------- | ------------------------------- | ------------------------------- | ---------- |
| `D2GeneratedRateLimitTier` metadata marker on every generated route (faithful, asserted-present, no enforcement) | Edge 18-bucket rate-limit middleware — `GetMetadata<D2GeneratedRateLimitTier>()` per route + enforce | Edge rate-limit middleware lands (PHASE_3 E2) | [F2](#f--edge--middleware-seam-binding-generated-markers-awaiting-their-real-consumer) |
| `D2GeneratedCsrfPosture` metadata marker on every generated route (faithful, asserted-present, no enforcement) | Edge CSRF middleware — `GetMetadata<D2GeneratedCsrfPosture>()` per route + enforce | Edge CSRF middleware lands (PHASE_3 E2) | [F3](#f--edge--middleware-seam-binding-generated-markers-awaiting-their-real-consumer) |
| `D2GeneratedIdempotencyStore` generated seam + in-memory `FakeIdempotencyStore` (injectable `TimeProvider`; `TryGetAsync<TStored>` + `StoreAsync<TStored>`) | Edge HTTP-idempotency middleware — real `D2GeneratedIdempotencyStore` impl (Redis `SET NX`, 24h TTL) | Edge `Idempotency.*` middleware lands (PHASE_3 E2) | [C8](#c--contract-idl-emitter--c0-completion) |
| `JwtAuthMiddleware` / `JwtAuthInterceptor` scope check = JWT-scopes-only; `ClaimsToContextMapper` does not map `d2_kind` / `d2_whois_id` / `d2_fingerprint_score` | Edge auth — change check to `EffectiveScopes = ctx.Scopes ∪ Scopes.AllAnonymousScopes`; map the anon claims | anon-JWT mint exists (PHASE_3 A3 / [B8](#b2--beyond-0023-edge-gated--c0-gated)) | [F1](#f--edge--middleware-seam-binding-generated-markers-awaiting-their-real-consumer) |
| `D2.Shared.Auth.Keyring` `IKeyringClient` / `GrpcKeyringClient` / `RabbitMqRotationEventChannel` (client types, no endpoint) | KeyCustodian gRPC `internal/keys/{domain}` endpoint + `GrpcKeyringClient` wired to it + messaging for the rotation channel | the keyring distribution endpoint is built (PHASE_3 E5) | [F4](#f--edge--middleware-seam-binding-generated-markers-awaiting-their-real-consumer) |
| In-process `IWorkloadCertificateIssuer` delegate (harness seam); `WorkloadLeafClient` wired to it | Cross-process `IssueWorkloadCertificate` gRPC endpoint exposing the in-process issuer over the wire | a running Edge gRPC host + the C0 gRPC contract for the issuance op | [A2](#a--mtls-remaining-phase-3-host-gated) |
| `D2MutualTlsOptions.Enabled = off` by default; loopback harness proof only | Edge host — Kestrel `RequireCertificate` + SPIFFE validator + leaf-refresh client wired in; replace the in-process issuer delegate with the cross-process gRPC call | a running Edge host (PHASE_3 A1) | [A4](#a--mtls-remaining-phase-3-host-gated) |
| `AddD2WorkloadCertificate` captures the leaf at channel construction (no rebuild-on-rotation) | Edge host-lifetime policy — invalidate + rebuild long-lived gRPC channels when `WorkloadLeafRefreshHostedService` rotates the leaf | Edge host channel-lifetime policy lands (A4) | [A5](#a--mtls-remaining-phase-3-host-gated) |
| `ForwardedJwtCallCredentials` + `.AddD2ForwardedJwt()` + `AddD2ForwardedJwtOutbound()` (in-memory-proven; Edge `api/` is `.gitkeep`) | Edge host composition root — attach the outbound forwarded-JWT rails; dual-transport with the mTLS rails on the same channel | a running Edge host (PHASE_3 A1) | [B15](#b2--beyond-0023-edge-gated--c0-gated) |
| `<Module>GrpcClientOptions.Address` (required, host-supplied; the generated DI ext embeds no literal address) | Edge host composition root — supply `AddD2KeyCustodianGrpcClients(new KeyCustodianGrpcClientOptions { Address = … })` | the host composition root exists (PHASE_3 A1) | [C1a](#c--contract-idl-emitter--c0-completion) (generated client) + [A4](#a--mtls-remaining-phase-3-host-gated) |
| The generated gRPC-client DI ext auto-chains the per-channel `.AddD2ForwardedJwt()` + `.AddD2WorkloadCertificate()`; the ONE-TIME `AddD2ForwardedJwtOutbound()` + `AddD2WorkloadCertificateOutbound()` composition-root registrations are NOT auto-called | Edge host composition root — call the ONE-TIME outbound registrations the per-channel interceptors depend on | the host composition root exists (PHASE_3 A1) | [B15](#b2--beyond-0023-edge-gated--c0-gated) + [C7](#c--contract-idl-emitter--c0-completion) |
| The generated TS SSR gRPC client fns ([C10](#c--contract-idl-emitter--c0-completion) NOW BUILDS THEM — validated against the real `@d2/grpc-client` seam + real ts-proto types) run against a hand-written BFF composition root (`getChannel`, context-propagation interceptor, boundary-token cache) | BFF gRPC composition root — wire the generated TS server client fns against the real channel + interceptors | BFF rebuild (Phase 7) | [C10](#c--contract-idl-emitter--c0-completion) + [B11](#b2--beyond-0023-edge-gated--c0-gated) |
| The generated TS browser REST client fns ([C10](#c--contract-idl-emitter--c0-completion) NOW BUILDS THEM — validated against a faithful `apiCall` double) call `apiCall`/`apiCallAnon` from the BFF client lib | BFF browser integration — wire the generated typed REST client fns to the real fetch substrate | BFF browser integration (Phase 7) | [C10](#c--contract-idl-emitter--c0-completion) |
| The `text/event-stream` SSE binding is NOT generated; `@d2ServerPush` is an exposure marker only; an `ISseEmitSink` test double is the planned seam (IF the SSE emitter is built) | Edge channel gateway (the real SSE fan-out engine) — only relevant if [C4](#c--contract-idl-emitter--c0-completion) resolves "emit" | the SSE emit-vs-fringe decision ([C4](#c--contract-idl-emitter--c0-completion)) resolves "emit" AND the Edge channel gateway lands (PHASE_3 E4) | [C4](#c--contract-idl-emitter--c0-completion) |
| `PropagatedContext` (operational `x-d2-context`) is read/written on AMQP only; sync .NET hops build context from JWT claims; the call-path interceptor is not wired | A real .NET → .NET sync hop — wire `x-d2-context` read/write + the call-path interceptor (service-id + timestamp per hop) | a service-to-service .NET call path exists (Edge → backend, or a second .NET service) | [B9](#b2--beyond-0023-edge-gated--c0-gated) |

**Note on the JWKS route** — `/.well-known/jwks.json` is deliberately NOT generated (ADR-0021 keeps it in the
named hand-written fringe); only the `sign` route is generated. There is no replace-trigger — it stays
hand-written. Listed here so its absence from the generated set is not mistaken for a gap.

---

## D — Sequencing, dependencies, and the coupling points

### What can go NOW (dev-first, no running Edge host)

- **[B1–B6](#b1--the-active-deliverable-0023-forwarded-jwt-plumbing--service-identity-retirement) (0023)** — ✅ SHIPPED 2026-06-20. These items are done; the active front moves forward.
- **The host-independent C0 emitter gaps** — [C2](#c--contract-idl-emitter--c0-completion) (OpenAPI extension), [C3](#c--contract-idl-emitter--c0-completion) (parity-test), [C6](#c--contract-idl-emitter--c0-completion) (enum/union) are buildable now with no host dependency. [C5](#c--contract-idl-emitter--c0-completion) (temporal scalars) is buildable once its mapping decision is made. Resolve [C9](#c--contract-idl-emitter--c0-completion) (how to record the already-built fleet) to close out C0. **These are the active host-independent front.**
- **The 0019 emitter-fleet finish-list** — [C1a](#c--contract-idl-emitter--c0-completion) (complete the .NET gRPC-client validation harness), [C10](#c--contract-idl-emitter--c0-completion) (TS client emitter), [C11](#c--contract-idl-emitter--c0-completion) (`@d2Resilience` predicates), then [C13](#c--contract-idl-emitter--c0-completion)/[C14](#c--contract-idl-emitter--c0-completion) (harness + parity consolidation) and [C16](#c--contract-idl-emitter--c0-completion) (FINAL-REVIEW + SHIP + record) — all host-independent. [C12](#c--contract-idl-emitter--c0-completion) (over-the-wire tests) is host-independent (self-managed Testcontainers) but gated on [C10](#c--contract-idl-emitter--c0-completion) (the [C1a](#c--contract-idl-emitter--c0-completion) gRPC-client validation is now ✅ done).
- **[B12](#b2--beyond-0023-edge-gated--c0-gated)** (spec docstring + `ts-codegen` fixes) is host-independent codegen, though it pairs naturally with B9.

### What is BLOCKED on a running Edge host (PHASE_3 A1 stands the host up)

- **All of [A2–A5](#a--mtls-remaining-phase-3-host-gated)** (the four mTLS cross-process items).
- **[B7](#b2--beyond-0023-edge-gated--c0-gated)** (boundary minter — PHASE_3 A2), **[B8](#b2--beyond-0023-edge-gated--c0-gated)** (anon-mint — A3), **[B13](#b2--beyond-0023-edge-gated--c0-gated)** (over-the-wire parity test — needs a live minter+validator), **[C8](#c--contract-idl-emitter--c0-completion)** (real idempotency store — E2).
- **The [§F](#f--edge--middleware-seam-binding-generated-markers-awaiting-their-real-consumer) seam-bindings** — [F2](#f--edge--middleware-seam-binding-generated-markers-awaiting-their-real-consumer) (rate-limit middleware reads the tier marker — E2), [F3](#f--edge--middleware-seam-binding-generated-markers-awaiting-their-real-consumer) (CSRF middleware reads the posture marker — E2), [F4](#f--edge--middleware-seam-binding-generated-markers-awaiting-their-real-consumer) (keyring distribution endpoint + consumer wiring — E5). [F1](#f--edge--middleware-seam-binding-generated-markers-awaiting-their-real-consumer) (anon-`EffectiveScopes` algorithm — CRITICAL) is gated on the anon-mint ([B8](#b2--beyond-0023-edge-gated--c0-gated) / PHASE_3 A3).
- **[B15](#b2--beyond-0023-edge-gated--c0-gated)** (wire the forwarded-JWT outbound plumbing into the running Edge host — the forwarded-token sibling of the mTLS [A4](#a--mtls-remaining-phase-3-host-gated) host-wiring; both land when the Edge host exists).
- **[B9](#b2--beyond-0023-edge-gated--c0-gated)** (sync .NET operational-subset, now also absorbing the call-path interceptor deferred out of 0023) needs an actual service-to-service .NET call path to exist. ([B16](#b2--beyond-0023-edge-gated--c0-gated) — the gRPC-inbound ambient-scope adapter — was its sibling here but is now **done in 0023** dev-first: the adapter + its `AddD2AuthGrpc()` registration are proven in isolation by an in-process gRPC `TestServer` e2e; only the live forwarding into a running gRPC-inbound host rides B15 host-wiring.)
- **[B11](#b2--beyond-0023-edge-gated--c0-gated)** (BFF token rename + forwarding) is Phase-7 (BFF rebuild).

### Coupling points (where one item is load-bearing for another)

1. **The emitter auto-wire ([B4](#b--auth-pivot-existing-code-reconciliation) = [C7](#c--contract-idl-emitter--c0-completion)) is the same change in two ledgers.** It is both a 0023 deliverable step (retire the dead `.AddD2ServiceIdentity()` guidance) and a C0-emitter-correctness fix (the generated client must auto-wire mandatory outbound auth). It ships once, under 0023. C0 inherits it.
2. **[B10](#b2--beyond-0023-edge-gated--c0-gated) (build-time scope-superset check) is an additive C0 emitter output**, not standalone auth work — it rides the contract IDL's `@d2Calls`-edge emission.
3. **[A2](#a--mtls-remaining-phase-3-host-gated) (gRPC issuance endpoint) needs the C0 gRPC contract** for the `IssueWorkloadCertificate` op — it is an endpoint, so per ADR-0021's foundational ordering C0 should precede it.
4. **[B1](#b1--the-active-deliverable-0023-forwarded-jwt-plumbing--service-identity-retirement) (`D2_INTERNAL_AUDIENCE` constant) is a prerequisite** for B7 (the minter sets `aud` to it) and B13 (the validator checks it) and B14 (the TS mirror).

### Recommended next-deliverable order

1. ✅ **0023** — forwarded-JWT plumbing + service-identity retirement. SHIPPED 2026-06-20. Landed the auth-reconciliation debt and the emitter auto-wire (B4/C7). No further action.
2. **C0 closeout + the full emitter-fleet finish-list** — decide [C9](#c--contract-idl-emitter--c0-completion) (how to record the already-built fleet), complete the 0019 finish-list ([C1a](#c--contract-idl-emitter--c0-completion) gRPC-client validation → [C10](#c--contract-idl-emitter--c0-completion) TS client → [C11](#c--contract-idl-emitter--c0-completion) `@d2Resilience` predicates → [C12](#c--contract-idl-emitter--c0-completion) over-the-wire tests → [C13](#c--contract-idl-emitter--c0-completion)/[C14](#c--contract-idl-emitter--c0-completion) harness + parity consolidation → [C16](#c--contract-idl-emitter--c0-completion) FINAL-REVIEW + SHIP), and build the remaining gaps [C2](#c--contract-idl-emitter--c0-completion) (OpenAPI extension), [C3](#c--contract-idl-emitter--c0-completion) (parity-test), [C6](#c--contract-idl-emitter--c0-completion) (enum/union), [C5](#c--contract-idl-emitter--c0-completion) (temporal scalars, once mapped). This is the "complete the emitters entirely" path and it needs no host. Resolve [C4](#c--contract-idl-emitter--c0-completion) (SSE emit-vs-fringe) as part of the closeout. **This is the current active host-independent front.**
3. **PHASE_3 A1** (Edge host shell) — the gate that unblocks everything host-dependent. Tracked in [PHASE_3.md](PHASE_3.md), not here.
4. **Then, host-gated, in PHASE_3 order**: A2 (→ B7 boundary minter; A2 mTLS issuance endpoint), A3 (→ B8 anon-mint → [F1](#f--edge--middleware-seam-binding-generated-markers-awaiting-their-real-consumer) anon-`EffectiveScopes`), E2 (→ C8 idempotency store + [F2](#f--edge--middleware-seam-binding-generated-markers-awaiting-their-real-consumer)/[F3](#f--edge--middleware-seam-binding-generated-markers-awaiting-their-real-consumer) rate-limit + CSRF marker reads), E5 (→ [F4](#f--edge--middleware-seam-binding-generated-markers-awaiting-their-real-consumer) keyring distribution endpoint), and the mTLS host wiring [A4/A5](#a--mtls-remaining-phase-3-host-gated). **Walk [§G](#g--edge--middleware-wire-up-checklist-the-seamreal-consumer-master-list) when the host + middleware land** — it is the seam→consumer master list. [B9](#b2--beyond-0023-edge-gated--c0-gated)/[B13](#b2--beyond-0023-edge-gated--c0-gated) fall out once a real call path + minter exist. [B10](#b2--beyond-0023-edge-gated--c0-gated) rides C0's call-edge emission whenever a declared A-calls-B edge first appears.
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

## H — Cross-cutting deferrals tracked outside this index (pointers, deep-tracked elsewhere)

These ARE deferred work and so are surfaced here per the single-source-of-truth principle — but their deep
tracking lives in another canonical owner (a deliverable record's honest-caveats section + the project
follow-up tracker), and they are NOT auth-pivot / emitter / Edge-seam items. Listed so the index is complete
without re-homing them: the pointer is here; the owner is named.

- ~~**Pre-existing `D2.Shared.Tests` OTel/CORS flake**~~ — **FIXED**. Root cause: two
  implicit named xUnit collections (`"OtelStaticState"` / `"LogLoggerStaticState"`) had no
  `[CollectionDefinition]`, so xUnit ran them in parallel — racing the Prometheus exporter global
  HttpListener, MeterProvider registration, and `Log.Logger`. Fix: merged both into a single declared
  `[CollectionDefinition("LogLoggerStaticState", DisableParallelization = true)]`; renamed all 12
  `OtelStaticState` usages to `LogLoggerStaticState`. 3 × consecutive full-suite runs: 0 failures /
  6351 passed each. Build 0 warnings; inspectcode 0 warnings.
- **`server/web` `D2Result.<factory>` static-call gap** — latent BFF type errors (v2 factories are module
  functions, not statics); unsurfaced because `server/web` isn't host-typechecked yet. **Owner**: the project
  follow-up tracker. Sweep all `D2Result.fail/ok/...` static-call sites when the `d2-web` compose service is
  stood up; the `gateway-response.ts` site was already fixed, the rest remain. Not a pivot/emitter/Edge-seam
  item.
