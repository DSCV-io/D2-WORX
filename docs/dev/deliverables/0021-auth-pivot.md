<!-- Committed snapshot of the gitignored deliverable-0021 workspace root (docs/wip/0021-auth-pivot/README.md), captured at SHIP 2026-06-18. -->

# Deliverable 0021 — Auth pivot: mint-once-at-Edge + forward unchanged (+ mTLS workload identity)

**Type:** documentation-only (decide + reconcile). **No source/code changes this deliverable** — code
follow-ups are explicitly deferred (see "Deferred to code"). **Branch:** `n/auth-pivot` off
`n/typespec-emitters`; merge-commit back into `n/typespec-emitters` (NOT nova); nova merge happens later
when the whole `n/typespec-emitters` cycle is done (per-user direction). **Dates:** today = 2026-06-17.

> **Workspace ground truth.** This README is the locked plan + verified evidence base. Every sub-agent
> dispatched for this deliverable reads it first. Claims here carry `file:line` evidence so sub-agents
> cite, not re-derive.

---

## Goal

Pivot D²'s **service-to-service** auth model FROM per-hop RFC 8693 token-exchange (re-mint a
narrowed token at every backend hop) TO **mint-once-at-the-Edge-boundary + forward the token
unchanged**, with **mTLS for workload identity**. Then bring **all** documentation into parity with
the new model BEFORE any code changes ("don't gaslight ourselves / future agents"). This deliverable
ships the **decision records + reconciled docs**; the code work (remove unwired libs, build the mTLS
PKI subsystem, the Edge issuer, the emitter/spec docstring fixes) is later deliverables.

---

## The locked model (precise)

1. **Mint once at the Edge boundary.** Edge (the token issuer — it holds the RS256 signing key via
   KeyCustodian) validates the incoming cookie/edge-facing token and **mints exactly one internal
   transaction-token**: `aud=d2.internal` (single broad internal audience), `scope` = the **union**
   the request needs, `act` chain only if impersonating. This is the ONLY mint per request.
2. **Forward it unchanged** across every **cross-process** hop (Edge→A→B…). Each receiving hop
   **re-validates** it (signature vs cached JWKS, `iss`, `aud==d2.internal`, `exp`/`nbf`, RS256 pin,
   session-liveness, **per-op scopes**) **AND mTLS authenticates the calling workload**. Fine-grained
   authz = **scopes** (per-op), never audience.
3. **In-process module hops** (KeyCustodian, the Auth module — both modules *inside* Edge) pass the
   validated `IRequestContext` directly through the in-process façade — **no wire token, no mTLS**
   (same process; see PHASE_0_AUTH §13 Scenario 1 row "Edge→Auth").
4. **Async (AMQP)** is unchanged: encrypted `PropagatedContext` in the message frame, **no JWT**
   ("encryption boundary = trust boundary").
5. **mTLS is ADDITIVE, not a validation-skip.** We KEEP per-hop JWT re-validation; mTLS adds
   workload/channel authentication + defense-in-depth + replaces the (unwired) `client_credentials`
   service-identity layer. JWT + mTLS is strictly stronger than today's JWT-alone. **This is why the
   pivot does NOT reactivate ADR-0007's rejection** (which rejected mTLS *as a reason to skip JWT
   validation* — see Evidence E2).

### Survivors — DO NOT over-reconcile these (they stay / are reinforced)
- The entire **inbound validation stack**: `JwtAuthMiddleware`, `JwtAuthInterceptor`, `JwtValidator`,
  `TieredCacheSessionLivenessTracker`, `ClaimsToContextMapper`, `ActorChainParser`. The forward model
  RELIES on it.
- **`x-d2-context` / `PropagatedContext`** operational subset (ADR-0007 Decision #2) — identity rebuilds
  from the JWT each hop; only the non-identity operational subset propagates. Unchanged.
- **Anon-JWT mint (Pattern A)**, **impersonation `act`-chain** (RFC 8693 §2.1 is the wire format for the
  `act` *claim*, independent of per-hop exchange), **mint-time scope/`d2_fp` binding**. All orthogonal —
  the word "mint" is overloaded; only "downstream re-mint" is the pivot target.

### New features to design into the ADRs (from the locked Q&A)
- **Service call-path (Q2):** an explicit propagated field each cross-process hop **appends** to
  (service id + timestamp) and **logs on receipt** — gives "what services did this request hop through,
  even if a span breaks." Lives in the propagated context, **NOT the signed JWT** (immutable;
  ADR-0007 strict `act`). D²'s `act` chain is user-impersonation only, so this is genuinely new.
- **Build-time scope-consistency check (Q5):** if op contracts declare downstream calls (a
  `@d2Calls`-style annotation), codegen statically verifies **caller-required-scopes ⊇
  callee-required-scopes** for each A→B edge — catches "A calls B but A's forwarded token lacks B's
  scope" at build time. Makes the forward model provably safe.

---

## Locked decisions

| # | Decision | Rationale |
| --- | --- | --- |
| D1 | **Forwarded-token audience = single broad internal `aud=d2.internal`** | The only coherent forward-unchanged option; per-service aud requires per-hop re-mint (the thing we drop). mTLS + per-op scopes do fine-grained authz. **The `d2.internal` value is a single contract-declared named constant (`auth-audiences` contract), never a raw literal — user directive 2026-06-17, so it changes in one place.** |
| D2 | **Two ADRs**: ADR-0022 (forward-model) + ADR-0023 (mTLS workload identity) | Separable; different risk/timeline (0022 ≈ adopt-existing + drop-unwired = near-zero code; 0023 = a new KeyCustodian PKI subsystem). |
| D3 | **ADR-0007 = amend: REFRAME the mTLS passages (reject the misuse, not the technology) + cross-ref (NOT supersede)** | 0007 Decision #2 IS forward-unchanged; it stands. BUT its current wording reads as rejecting mTLS outright ("the mTLS fast path is not implemented"; "Transport-trust fast path … Rejected") — which now contradicts 0023's adoption. Reframe (user direction 2026-06-18) so 0007 rejects only the *transport-trust shortcut* — using mTLS to skip token re-validation / anchor identity in the network / "lean on it for everything" — while stating mTLS itself IS adopted as an additive workload-identity + channel layer (0023), alongside (never replacing) per-hop token re-validation. Preserve the core threat-model point (identity anchored on the re-validated token, not the transport). Reject the role, not the technology. |
| D4 | **ADR-0012 = amend** | It enshrines the (unwired) `HttpTokenExchangeClient` / service-identity as the built/intended outbound decision; re-scope to the forward model. |
| D5 | **Next ADR numbers: 0022, 0023** | 0021 (`unified-operation-contract-idl`) is already Accepted (2026-06-13). Verified by glob + ADR README. |
| D6 | **Branch:** `n/auth-pivot` → merge-commit → `n/typespec-emitters` → (later) nova | Per-user direction; the pivot is upstream of 0019's remaining Step-9 client work. |
| D7 | **RFC 8693 token-exchange is RETAINED, repurposed** | For: the single Edge **boundary mint**, cross-trust-domain calls, targeted narrowing exceptions, async scope reduction, impersonation. NOT the per-hop business default. |
| D8 | **Biscuit** noted as the upgrade path if per-hop *attenuation* ever becomes a hard requirement | Offline attenuation without a mint round-trip. |

---

## Verified evidence base (with file:line — cite these, don't re-derive)

- **E1 — ADR number.** `docs/adrs/0021-unified-operation-contract-idl.md` is Accepted 2026-06-13 →
  next free = **0022, 0023**. (`docs/adrs/README.md` index format: `| [NNNN](NNNN-title.md) | Title |
  Status | Date | \`deliverable\` |`; per-file: H1 `# ADR-NNNN: Title`, then `- **Status**` /
  `- **Date**` / `- **Deliverable**`, then `## Context` / `## Decision` / `## Consequences` /
  `## Alternatives considered`.)
- **E2 — Pivot is TOWARD ADR-0007, not against it.** `docs/adrs/0007-request-context-propagation.md`
  Decision #2 (`:35-39`) = rebuild identity from the JWT each hop; operational subset in `x-d2-context`.
  Its rejection (`:64` Alternatives, `:58` Consequences) is narrow: *"mTLS + trusted identity header,
  **skip per-hop validation**"*; `:58` verbatim parenthetical — *"identity-via-JWT and the operational
  subset are both still propagated — what we decline is **trusting** a propagated identity **without
  re-verifying its token**."* Our pivot keeps re-validation → 0007 stands; mTLS is additive.
- **E3 — What we drop is built-but-UNWIRED.** `PHASE_0_AUTH.md` §13 one-line verdict (`:1897-1902`):
  inbound validation built+strict; `ITokenExchangeClient`/`IServiceIdentityClient`/
  `ServiceIdentityCallCredentials` *"built as clients but wired into no request flow (exchange has
  test-only callers)"*; Edge `/oauth/token` issuer + anon-mint + auth-keyring *Phase-3/unbuilt*; *"end-
  to-end cross-service auth does not yet run anywhere."* (`ExchangeAsync` callers are all in
  `HttpTokenExchangeClientTests.cs`; Edge `api/app/domain/infra` are `.gitkeep`-only.) **Near-zero code risk.**
- **E4 — mTLS cert issuance is genuinely NEW KeyCustodian PKI.** `tools/scripts/gen-dev-keys.sh`
  generates only the symmetric `root.key` (+`root-next.key` under `--rotate-root`); no `openssl
  req/x509`, no CSR/CA/.crt/.pem. KeyCustodian `KeyType` = `{RsaSigning, AesPayload, Secret}` — zero
  X.509/CSR/CA/SAN surface (`domain/Rules/KeyGeneration.cs` exports raw PKCS#8/SPKI, not certs).
- **E5 — C2 audience problem is real + inherited.** Strict validator (`JwtValidator.cs:277-278`,
  `ValidateAudience=true`, single `ValidAudience`); a forwarded `aud=edge.internal` token would
  `AUDIENCE_MISMATCH` at Files. D1 (`aud=d2.internal`, all services accept it) resolves this.
- **E6 — C4: .NET sync transports are CLAIMS-ONLY today.** `x-d2-context` is wired on AMQP (.NET) +
  the TS BFF/gRPC-client, but the .NET sync HTTP/gRPC middleware builds context from JWT claims only
  (zero `PropagatedContext` reads in `auth/http` + `auth/grpc`). The forward model's identity half is
  thus already correct on .NET; any *operational-subset* on .NET→.NET sync hops is NEW .NET plumbing
  (a code follow-up, not a doc fix).

### C1–C7 triage (from PHASE_0_AUTH.md §13)
- **C1 + C4 → dissolve into the pivot** (become Scenario-B rewrites: forward the once-minted JWT;
  delete the "RequiredScopes from ContextEnvelope" + "server reconstructs from envelope" steps).
- **C2 → resolved by D1** (`aud=d2.internal`); note the over-the-wire mint↔validate parity test as a
  code follow-up.
- **C3, C5, C6, C7 → independent doc-accuracy / build-state fixes**, done in Step 4 regardless of the
  pivot (ContextEnvelope→`PropagatedContext` terminology + "AMQP-only encryption"; harmless-endpoint-
  only no-token bypass; sentinel-only `ISessionLivenessTracker`; anon-claims/`ActorKind.Anonymous`/§9#5
  marked Phase-3).

---

## Staleness sweep (2026-06-18) — full doc-set, all 3 buckets handled IN this deliverable

Reading sweep, 3 parallel passes (other ADRs / lib READMEs / dev-docs + service-READMEs). **Well-contained:**
no OTHER ADR frames the old model as current; survivor clusters (messaging/context/encryption/result/
inbound-stack/`x-d2-context`) reinforce the new model. Per-user direction 2026-06-18: handle ALL three
buckets here; bucket 3 via in-doc "to-be-done-on-removal" notes, not an external follow-up list.

**Correction (load-bearing — drove the bucket-3 reframing):** `DcsvIo.D2.Auth.Outbound` is NOT removed
wholesale. Token-exchange (`ITokenExchangeClient`) is RETAINED/repurposed (D7); only service-identity
(`IServiceIdentityClient` / `ServiceIdentityCallCredentials` / `AddD2ServiceIdentity`, `client_credentials`)
is SUPERSEDED by mTLS (the half we "yeet"; code removal = a later deliverable). The lib persists → its
lib-level telemetry/logging/inventory entries are NOT stale; only the service-identity surface is.

- **Bucket 1 (Steps 4–6) scope upgrades:** `auth/outbound/README.md` = near-TOTAL reframe; `auth/
  abstractions/README.md` §131-137 = OVER-RECONCILIATION TRAP (reframe "preserve `act` on every exchange"
  → the mint locus is the Edge boundary; KEEP the `act`-chain structure/depth-limit/parsing — survivor);
  `edge/README.md` = ADD KeyCustodian-as-mTLS-CA (0023) + Edge-mints-the-one-token (0022), not just reframe;
  `rules.md` §16 :1818 = content re-scope, not just a cross-ref. (PATTERNS.md itself read CLEAN — the KAD
  auth framing lives in `CLAUDE.md` §4, see bucket 2.)
- **Bucket 2 (NEW → reframe-now this deliverable):** ADR-0021↔0022/0023 xref [Step 3b]; `auth/
  audiences-source-gen/README.md`, `headers/http`+`headers/grpc` READMEs, TS `grpc-client/README.md` +
  `typescript/README.md` (reframe ONLY the dead `IServiceIdentityClient`-mirror xref — the BFF→Edge
  `client_credentials` token SURVIVES, BFF is an external client of Edge), `rules.md` §10.6 + incidental
  §1.1/§20.3/§20.4 symbol examples, `CLAUDE.md` §4 (KAD still says "RFC 8693 + 6749 §4.4" — lockstep update). [Step 6]
- **Bucket 3 (in-doc removal notes, added NOW — not external tracking):** concise note tied to ADR-0023 —
  "re-verify when the service-identity half of `Auth.Outbound` is removed (a later deliverable); the
  token-exchange half + its lib-level telemetry/logging are retained" — on ADR-0010 (telemetry counts/list)
  [Step 3b], ADR-0011 (LoggerMessage surfaces) [Step 3b], `server/shared/dotnet/README.md` inventory+telemetry
  + `service-defaults/README.md` [Step 6]. Plus ADR-0013 forward note: "add mTLS cert-validation +
  forwarded-token handling to the composition-root wiring when that plumbing is built (0022/0023)" [Step 3b].

---

## Steps (prerequisite order)

> 1→2→3 are the high-stakes decision records (reviewed per step). 4/5/6 apply the locked model to
> existing docs and can fan out in parallel once the ADRs exist. FINAL-REVIEW is a whole-doc-set
> parity sweep.

- **Step 1 — ADR-0022** `0022-service-auth-mint-once-forward.md` — the standalone pivot doc: the model,
  E2/E3 evidence, D1 audience, the call-path (Q2), the scope-check (Q5), retained RFC-8693 uses (D7),
  Biscuit path (D8). Cross-refs ADR-0007 + ADR-0023.
- **Step 2 — ADR-0023** `0023-mtls-workload-identity.md` — mTLS as additive workload/channel auth; the
  new KeyCustodian PKI subsystem (CA/CSR/leaf/rotation) per E4; explicitly does NOT skip JWT
  re-validation; dev-first self-signed so it runs locally (no payware/vendored mTLS). Cross-refs 0007 + 0022.
- **Step 3 — ADR reconciliation** — ADR-0007 cross-ref note (D3) + ADR-0012 amend (D4) + `docs/adrs/README.md`
  index rows for 0022 + 0023.
- **Step 3b — ADR addenda (from the sweep, buckets 2+3)** — ADR-0021↔0022/0023 bidirectional cross-ref;
  ADR-0010 + ADR-0011 service-identity-removal re-verify notes; ADR-0013 mTLS-wiring-when-built forward note.
  (See the Staleness-sweep section.)
- **Step 4 — PHASE_0_AUTH.md** — the largest rewrite: §3.2, §6.6, §8 (Scenario B), Q11, Q24, §13 +
  resolve C1–C7. Supersede the in-flight uncommitted Q24/§13 edits into final form.
- **Step 5 — Tracking/architecture docs** — `V2.md` §5.4 (827–907, 1672–74, 2502) + `PHASE_3.md`
  (A2/A6/L22) + `PHASE_3_EDGE.md` (76–106).
- **Step 6 — KEEP docs** — auth READMEs (`auth/`, `auth/core/`, `auth/outbound/`, `auth/abstractions/`),
  `rules.md:1818` (OOTB-lib catalog) + `PATTERNS.md`, `CHANGELOG.md:15`, `.env.local.example:339` +
  `.env.secrets.example:100` (rewrite to mTLS framing — edit the `.example` files only), `edge/README.md`
  + `services/README.md`.
- **FINAL-REVIEW** — whole-doc-set parity: no contradictions across the auth docs; every old-model
  reference reconciled OR explicitly tagged deferred-to-code; survivors NOT over-reconciled; ADR
  cross-refs bidirectional; completeness checklist + attestation into this README.

### Reconciliation ranking (from the reference sweep — most→least stale)
ADR-0007 (cross-ref) · ADR-0012 (amend) · PHASE_0_AUTH.md (largest) · V2.md §5.4 · PHASE_3.md A2 ·
auth READMEs · auth/abstractions/README.md:127-137 ("act on every exchange") · PHASE_3_EDGE.md ·
CHANGELOG + `.env*.example` + edge/service READMEs · rules.md:1818 + PATTERNS.md.

---

## Deferred to code (NOT this deliverable — log only, do not touch)
- Remove/repurpose the unwired `auth/outbound/` libs (`HttpTokenExchangeClient`, `IServiceIdentityClient`,
  `ServiceIdentityCallCredentials`, `AddD2ServiceIdentity`).
- **Declare `d2.internal` in the `auth-audiences` contract + emit it as a named constant in both runtimes
  (.NET + TS); no raw-literal internal audience anywhere** (user directive 2026-06-17 — single-point update).
  Candidate rules.md predicate at SHIP (the internal audience, like other spec/config values, is a
  contract-declared constant, never a scattered literal — same spirit as the TK-constant rule).
- `contracts/*.spec.json` docstrings ("Updates on every token exchange") + `tools/ts-codegen` emitters +
  regenerate `.g.*` (codegen — entangled with the `act`-chain-stops-growing-at-internal-hops semantics).
- The mTLS PKI subsystem itself, the Edge `/oauth/token` issuer, the .NET sync `x-d2-context` plumbing
  (E6), the over-the-wire mint↔validate parity test (C2), the `@d2Calls` build-time check (Q5).
- 0019 Step 9 redirect (9c/9d/middleware → forward + mTLS, no per-hop exchange).

---

## Doc-quality predicates every step must honor (pre-emptive gate)
- **Parity with current truth** — describe what IS; ADRs/KEEP docs reflect current decisions, not frozen
  history (unless explicitly designated superseded). Mark unbuilt things as Phase-3/designed-only.
- **No phase refs in KEEP docs** — no "Phase 0/3", `PHASE_*.md`, `V2.md`, v1→v2 framing in ADRs/READMEs/
  code comments. (Phase numbering lives only in `docs/v2/` tracking docs — Steps 4/5 are tracking docs,
  so phase vocab is allowed there; Steps 1/2/3/6 are KEEP docs, so it is NOT.)
- **No dead-concept framing** — don't describe what doesn't exist ("no X", "why no Y"); describe what is.
- **No CLAUDE.md refs** in committed docs; describe conventions directly.
- **No conversation-scoped IDs** (Q1–Q7, "audit decision", C1–C7 may be referenced inside PHASE_0_AUTH
  §13 since that's where they live, but ADRs state decisions directly, not "per the Q&A").
- **ADR format** exactly per `docs/adrs/README.md` (H1, Status/Date/Deliverable bullets, Context/Decision/
  Consequences/Alternatives; present-tense Decision).
- **File headers** (`Copyright (c) DCSV. All rights reserved.`) on new ADR files (match existing ADRs).
- **Bidirectional cross-refs** — if A cites B, B cites A.

---

## Risks
- **R1 — over-reconciliation.** "mint" is overloaded; a naive sweep would gut anon-mint / impersonation /
  scope-binding. Mitigated by the explicit Survivors list + the per-step "describes-old? Y/N" discipline.
- **R2 — reactivating ADR-0007's rejection.** If an ADR frames mTLS as "skip JWT validation," it
  contradicts 0007. Mitigated by D3 + E2 (mTLS additive, JWT re-validation retained) — call it out
  explicitly in 0022 + 0023.
- **R3 — PHASE_0_AUTH.md scale.** ~2000 lines, the in-flight uncommitted edits, 6 focus sections + C1–C7.
  Mitigated by isolating it as its own Step 4 with the §13 evidence already mapped.
- **R4 — doc drift between the two runtimes' docs** (.NET vs TS BFF). FINAL-REVIEW parity sweep covers it.

---

## Living state
- 2026-06-17 — Branch `n/auth-pivot` cut off `n/typespec-emitters`. Workspace scaffolded. Plan locked
  (D1–D8). Research complete (2 Opus passes + orchestrator verification of ADR-0007 + §13). Awaiting
  cadence confirmation, then Step 1 dispatch.
- 2026-06-17 — Step 1 (ADR-0022) drafted + targeted audit CLEAN (1 LOW fixed: KeyCustodian terminology,
  `:106`). User reviewed content → approved with one directive: the internal audience must be a
  named/contract-declared constant, not a raw literal (captured in D1 + Deferred-to-code; concise note
  being added to ADR-0022's audience decision). Step 1 closing; Step 2 (ADR-0023) next.
- 2026-06-18 — Step 2 (ADR-0023, mTLS workload identity) drafted + targeted audit CLEAN (0 findings;
  auditor independently re-verified KeyCustodian's no-PKI-today claims against live code). User reviewed
  → approved with one refinement: the dev/prod subsection was sharpened to state that production EXTENDS
  (not replaces) the dev model — the only fundamental dev→prod difference is **secret management** (CA-key
  custody + secure distribution of cert material); revocation folds into the rotation model and HA is a
  general-prod concern, so neither is framed as the difference. CA hierarchy + distribution mechanism stay
  deferred. Step 2 CLOSED. Step 3 (ADR reconciliation: 0007 cross-ref + 0012 amend + 0023 back-edges +
  README index rows) pending user go.
- 2026-06-18 — User go for Step 3 with a specific direction on the ADR-0007 reframe (refined into D3):
  don't reject mTLS outright (we now adopt it additively per 0023) — reframe 0007 to reject only the
  transport-trust shortcut (mTLS used to skip token validation / "lean on it for everything"), keep the
  token-as-identity-anchor point. Dispatching Step 3 as one Opus Implementer over 4 files: 0007 reframe +
  0012 amend (outbound decision re-scoped to forward model; service-identity replaced by mTLS) + 0016
  back-edge to 0023 + README index rows for 0022/0023.
- 2026-06-18 — CRLF source-gen churn fixed + committed separately (`7611b3eb`, not part of this deliverable's
  doc set). Then: staleness sweep (3 parallel passes) complete — well-contained (no other ADR frames the old
  model as current). User direction: address ALL 3 buckets in THIS deliverable; bucket 3 = in-doc removal
  notes. Load-bearing correction locked: `Auth.Outbound` token-exchange RETAINED, only service-identity
  SUPERSEDED — lib persists. Plan expanded: Step 3b added; Step 6 widened (see Staleness-sweep §). Step 3
  reframe implicitly accepted (user directed further ADR work without objection). Dispatching Step 3b next.
- 2026-06-18 — Step 3b done (ADR-0021↔0022/0023 xref + ADR-0010/0011/0013 notes; orchestrator-verified diffs).
  Step 4 done (PHASE_0_AUTH reconciled 2070→2209 lines; C1–C7 → resolved-record; build-states honest;
  ContextEnvelope→PropagatedContext confirmed, Design-A/B collapsed); targeted audit running (background).
  User decisions: (a) fold the PHASE_0_AUTH CLAUDE.md-ref strip (lines 125/138/2199) + the §10 stale
  auth-build-state (anonymous-passthrough, `TokenExchangeClient`-NotImplementedException) into the Step-4
  Fixer; (b) a broader CLAUDE.md-ref hygiene pass across ALL committed docs = tracked follow-up, own
  deliverable later (project memory written). Step-4 Fixer dispatches on the audit's return, handling its
  findings + (a). Then Step 5 (tracking docs) → Step 6 (READMEs/dev-docs, expanded) → FINAL-REVIEW → commit.
- 2026-06-18 — Step 4 CLOSED. Targeted audit found 7 findings, ALL in un-reconciled OLD-model pockets
  OUTSIDE the focus set (focus sections were clean first-pass). Fixed + orchestrator-verified: F2 (§1
  lib-purpose), F3/F4 (§10 test inventory), F6 (§6.4 mTLS note), F7 (§12 Q10 SUPERSEDED banner — history
  preserved + over-claim corrected), F5 (3 CLAUDE.md refs stripped — grep=0), F1 (§8-C/§13-5 AMQP →
  operational-subset-only, no identity reconstruction, aligned to ADR-0007 §Decision-2; user-confirmed
  the async path carries no identity). Comprehensive closure-by-absence deferred to FINAL-REVIEW (targeted
  cadence). Next: Step 5 (V2.md §5.4 / PHASE_3 A2-A6 / PHASE_3_EDGE).
- 2026-06-18 — Step 5 CLOSED. V2.md §5.4 reframed (mint-once-forward heading; retained boundary/exception
  exchange wire-shape; "Workload identity — mTLS supersedes client_credentials" subsection; boundary-token
  -vs-internal-workload blockquote; SPIFFE→optional on-ramp; mTLS promoted out of deferred at topology/
  phase-table/K8s/deferred-infra) + PHASE_3 (L22/A2 reframed; A6 impersonation = survivor) + PHASE_3_EDGE
  (scheduled-jobs service-identity→mTLS). Follow-up: 4 more V2.md internal-workload service-identity refs
  handled (3 supersession notes: KeyCustodian-custody/keyring-fetch/ops-CLI; 1 survivor reword: the
  X-D2-Internal-Token boundary-token row); ~1315 CLAUDE.md ref left for the broader sweep. Orchestrator-
  verified (§5.4 greps + follow-up before/after); comprehensive closure at FINAL-REVIEW. Next: Step 6.
- 2026-06-18 — Step 6 CLOSED; targeted audit CLEAN (0 findings). 3 clusters reconciled ~20 KEEP/dev/config
  docs: (A) auth lib READMEs — outbound near-total ⚠-banner; abstractions `act`-chain trap handled
  (structure kept, framing reframed). (B) dev-docs — rules.md §16/§10.6/§1.1/§20.x; CLAUDE.md §4 (dropped
  6749 §4.4, added mint-once-forward + mTLS + KeyCustodian-CA); PATTERNS.md verified clean; .env*.example
  (+ 3 extra per-service comments); CHANGELOG left as history. (C) service/cross-lib — edge/README
  KeyCustodian-CA + mint-once ADDITIONS; services/README; headers/http+grpc; TS grpc-client+typescript
  (dead IServiceIdentityClient-mirror reworded, BFF token survivor INTACT); bucket-3 notes on
  shared/dotnet/README + service-defaults/README (counts unchanged). Pre-existing hygiene (phase-ref @
  auth/core/README:247; off-by-one links in header READMEs) → broadened broader-hygiene-sweep memory.
  **Steps 1–6 ALL DONE.** Next: FINAL-REVIEW (whole-deliverable parity sweep) → user REVIEW + docs commit.
- 2026-06-18 — FINAL-REVIEW done (4 fresh partition reviewers). Partition-1 (ADRs) CLEAN (10/10 cross-ref
  pairs bidirectional + resolving). Per-step closures ALL re-confirmed: C1–C7 (incl. F1 AMQP verbatim),
  the `act`-chain + TS-BFF-token survivors, bucket-3 counts. 3 LOW findings → P2-1 (§6.6 bootstrap mTLS
  marker) + P3-1 (`RequestedByClientId` gloss) FIXED + orchestrator-verified; P4-1 reasoned non-defect
  (`headers/http` already forward-correct). FINAL-REVIEW CONVERGED. Completeness checklist walked +
  attestation written (below). Deliverable READY for user REVIEW.

---

## Final report (ship-prep) — 2026-06-18

### Summary
Decided + documented the service-to-service auth pivot — **mint-once-at-the-Edge + forward the token
unchanged** (ADR-0022) + **mTLS workload identity** (ADR-0023) — and reconciled the entire auth-doc set to
it, BEFORE any code (the "document everything first" scope). Artifacts: **2 new ADRs** (0022, 0023); **7
reconciled ADRs** (0007 reframe, 0012 amend, 0016 back-ref, 0010/0011/0013 removal/forward notes, 0021
xref) + README index; **PHASE_0_AUTH.md** big rewrite (§3.2/§6.x/§8/Q11/Q24/§13 + C1–C7 resolved); **3
tracking docs** (V2.md §5.4, PHASE_3, PHASE_3_EDGE); **~14 KEEP docs** (auth READMEs, rules.md, CLAUDE.md
§4, .env*.example, service/cross-lib READMEs). Locked decisions D1–D8. (The CRLF source-gen churn was fixed
separately — commit `7611b3eb`, not part of this docs set.)

### Kinds-of-misses log (self-improvement evidence)
- **Un-reframed pockets outside the focus-set** — the per-step "focus list" consistently missed stale
  pockets elsewhere in the same doc; the audits' skim-the-rest caught them (Step-4: 7 findings in
  §1/§10/§12/§6.x; Step-5/6: more V2.md/README refs). A model-change reconciliation's focus-list is
  necessary but NOT sufficient — sweep the WHOLE doc for the old model.
- **Over-reconciliation trap (survivor vs stale-framing)** — survivors (`act`-chain, BFF→Edge token,
  `x-d2-context`, anon-mint) carry old-model surface mentions; the risk was gutting the survivor while
  reframing the framing. The Survivors list + per-doc "describes-old? Y/N" discipline prevented it (the
  `auth/abstractions` act-chain trap: structure kept, framing reframed).
- **F1 — a rename surfaced a latent contradiction** — the C3 ContextEnvelope→PropagatedContext rename made
  a pre-existing ADR-0007-vs-§13 async-identity contradiction LIVE; resolved by aligning to the
  authoritative ADR-0007 (async = operational subset, no identity). A mechanical rename can surface latent
  contradictions — verify against the canonical source.
- **Scope conflation (you caught it)** — I framed "auth/outbound removed" when only the service-identity
  HALF is superseded (token-exchange retained/repurposed). Precise "superseded vs removed vs repurposed"
  per-component scoping matters.

### Candidate rule additions (for your approval → rules.md)
1. **Doc-reconciliation sweep-beyond-focus** — when reconciling KEEP/tracking docs to a model change, the
   audit MUST sweep the WHOLE doc for the old model, not just the named focus sections.
2. **Survivor-preservation in model-change reconciliation** — maintain an explicit Survivors list + a
   per-doc "describes-old? Y/N" classification; distinguish "reframe the framing" from "gut the survivor."
3. **Config/identity values are contract-declared constants** — the internal audience (and similar
   spec/config values) is a single contract-declared named constant emitted to all runtimes, never a raw
   literal (your audience-as-constant directive; same spirit as the TK-constants rule).
4. **ADR amendment convention** — codify the dated `Date`-bullet parenthetical + Status-stays-Accepted
   pattern used for the reconciled ADRs.

### Deferred-to-code + tracked follow-ups
- **Code (later deliverables):** remove the service-identity half + keep the repurposed token-exchange half
  of `auth/outbound`; the mTLS PKI subsystem (KeyCustodian CA); the Edge `/oauth/token` issuer; the
  spec/emitter "every token exchange" docstrings; the `@d2Calls` build-time scope-check; the .NET sync
  `x-d2-context` plumbing; the 0019 Step-9 client redirect (forward + mTLS).
- **Broader doc-hygiene sweep (tracked — project memory):** CLAUDE.md-refs + phase-refs-in-KEEP-docs +
  off-by-one relative links across committed docs.

### Deliverable completeness checklist — honest walk (docs-only, targeted cadence)
- **Per-step gates:** journals exist for every step ✅. The big-table / anti-laziness / PASS-cite boxes were
  satisfied via the **user-authorized targeted cadence** (scoped per-step doc-audits that each converged
  clean) — a §13.14-named deviation from the full per-step ~280-row sweep, NOT the literal format. Build /
  inspectcode / test / code-coverage / new-API ⚪ **N/A** (no source code in this deliverable).
- **Final-review gate:** final-review artifacts exist (`07-final-review/` 4 partials + fix-log) ✅; swept the
  ENTIRE deliverable (~30 docs across 4 partitions) ✅; findings+fix logs present (partitioned, not one
  ~280-row big table) ✅; converged clean (3 LOW → 2 fixed + 1 non-defect) ✅; deliverable-wide consistency
  was the explicit focus ✅.
- **Deliverable-wide doc gates:** root README final report ✅ (this section); PATTERNS.md verified clean,
  TESTS/PARITY/SRC_GEN ⚪ N/A (no code/test/parity/codegen change); per-lib READMEs reconciled ✅;
  shared/dotnet/README bucket-3 note ✅; tracking docs reconciled ✅; no NEW phase/audit verbiage in KEEP
  docs ✅ (pre-existing phase-refs → tracked sweep); no conversation-IDs in KEEP docs ✅.
- **Process-integrity gates:** no commit without per-occurrence permission ✅ (only `7611b3eb`, authorized;
  the auth docs are UNCOMMITTED pending your REVIEW); no bulk-ops-without-scope ✅; no destructive git ✅;
  no deferral without permission ✅ (all user-approved + logged); no architectural deviation without ASK ✅
  (audience / branch / F1 / bucket-handling all user-confirmed).

### Attestation (adapted — honest)
> I attest that this deliverable's process integrity has been verified against the rules.md Deliverable
> completeness checklist, **adapted for its nature**: it is a **documentation-only** deliverable (the
> code-specific gates are N/A — no source changed here; the separate CRLF source-gen fix shipped as
> `7611b3eb` with its own zero-warning build + green tests), audited under the **user-authorized targeted
> cadence** (scoped per-step audits + a full FINAL-REVIEW; a §13.14-named deviation from the full per-step
> sweep). Under that mapping every applicable box is YES: each per-step audit converged clean; the
> FINAL-REVIEW (4 fresh partition reviewers) converged (3 LOW → 2 fixed + 1 reasoned non-defect); the
> per-step closures (C1–C7, the act-chain / BFF-token / bucket-3-count survivors, the cross-ref matrix) were
> independently re-confirmed; and no commit / bulk-op / destructive-git / deferral / architectural deviation
> occurred without explicit user authorization. Pre-existing doc-hygiene (CLAUDE.md-refs / phase-refs /
> off-by-one links) is catalogued for a separate tracked sweep, not blocking. **The deliverable is ready for
> user REVIEW.**
>
> Journals: `docs/wip/0021-auth-pivot/{01-adr-0022, 02-adr-0023, 03-adr-reconciliation, 03b-adr-addenda,
> 04-phase0-auth, 05-tracking-docs, 06-keep-docs, 07-final-review}/`.
