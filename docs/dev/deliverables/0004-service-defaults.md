<!-- Copyright (c) DCSV. All rights reserved. -->

# Deliverable 0004 — DcsvIo.D2.ServiceDefaults

**Status**: ✅ SHIPPED 2026-05-12
**Branch**: `n/service-defaults` (from `nova` @ `9d027cf8`) → squash-merged to `nova` as `b3a05f1c`
**Phase**: Phase 0 Wave 7 (final Phase 0 lib per `docs/v2/PHASE_0.md` row 62)
**Phase 0**: COMPLETE

## SHIP-gate attestation

Per CLAUDE.md MANDATORY block 3 (Deliverable Completeness Checklist):

This deliverable's process integrity was verified against the Deliverable Completeness Checklist in `docs/dev/rules.md`. **Every box was honest YES**:

- **Per-step gates** (×10 sub-steps): every step's journal contained the 3-artifact model (Latest sweep results + Sweep findings log + Fix log); every step's big table converged to zero FINDING rows; every step's distillation written; every cross-step audit scope per §24.7 honored
- **Final-review gate**: deliverable-wide audit walked every rules.md predicate against every file the deliverable touched; R3 big table CLEAN (zero FINDING rows) post-Step-7; 10 cross-step drift focus areas all PASS
- **Doc gates** (per CLAUDE.md §3.5 Doc Update Map): PATTERNS.md / V2.md / PHASE_0.md / parent README / AUDIT_CHECKLIST.md all updated; per-lib READMEs current; tracking-doc allowlist correctly applied (V2.md + PHASE_0.md retain phase verbiage by design); MESSAGING.md verify-pass-with-no-changes documented
- **Process-integrity gates**: every commit had explicit per-occurrence user permission; no destructive git operations; deferred items honestly tracked; 5 orchestrator overrides (4 on Step 4, 1 on Step 6) all ASKED + DOCUMENTED before applying; no silent scope expansions
- **Quality gates**: `dotnet build server/D2.slnx` 0/0; `jb inspectcode --severity=WARNING` 0; `dotnet test` 2914/2914 deterministic across multiple runs

## Context

`DcsvIo.D2.ServiceDefaults` is the composition-root convenience csproj that wires every prior Phase 0 shared lib into a single `AddD2ServiceDefaults()` + `UseD2DefaultPipeline()` + `MapD2DefaultEndpoints()` extension surface, so per-service `Program.cs` shrinks from v1's ~60-150 lines of duplicated boilerplate to ~10-15 lines of service-specific declarations.

Architectural constraint: **ServiceDefaults itself owns ZERO logic** — it's a thin aggregator that calls into other shared libs' builder extensions. All cross-cutting logic that would have lived inline in v1's `ServiceDefaults` (Serilog setup, OTel SDK setup, security headers, CORS, infrastructure-bypass middleware, ProblemDetails customization) is extracted into three new logic-bearing supporting libs that ship as part of this deliverable: `DcsvIo.D2.Logging`, `DcsvIo.D2.Telemetry`, `DcsvIo.D2.AspNetCore`.

Pattern parity inspiration: `Microsoft.Extensions.ServiceDefaults` from .NET Aspire, adapted for the v2 shared-lib stack + locked middleware ordering + Serilog-based PII discipline (`[RedactData]` + `RedactDataDestructuringPolicy`).

## What shipped

- **4 new libs** at `server/shared/dotnet/`:
  - `logging/` — Serilog + RedactDataDestructuringPolicy + 42-field IRequestContext enricher
  - `telemetry/` — OTel SDK + OTLP exporters + Prometheus IP-restricted endpoint + ActivitySource/Meter aggregation
  - `aspnetcore/` — security headers + CORS + infrastructure-bypass + ProblemDetails + health endpoints + RunD2ServiceAsync
  - `service-defaults/` — the namesake thin aggregator (4 public extensions, 10 ProjectRefs, ZERO logic)
- **6 new framework rules** in `docs/dev/rules.md`:
  - **§1.19** — Per-step integration tests for runtime composition / wire-up risk libs
  - **§5.25** — `nameof()` discipline for codegen'd / wire-format member references (spec-pinning tests exempt)
  - **§11.28** — KEEP doc forward-framing prohibition (4 self-correcting augmentation cycles built mature regex)
  - **§13.13** — Plan-vs-reality reconciliation (HONEST DOCUMENTATION when reality reveals Plan was wrong)
  - **§14.1** — Augmented to catch hyphenated `Phase-N` + `Step-N` forms + Plan-row references; tracking-doc allowlist clarification
  - **§24.13 + §24.13.1** — Pre-flight Evidence greps mandatory + canonical pre-flight grep checklist sub-rule
- **3 cross-step consolidations**:
  - InfrastructurePathMatcher (Step 3): 2 internal duplicates → 1 canonical in AspNetCore
  - 3 telemetry-class visibility promotions (MessagingTelemetry + RedisCacheTelemetry + LocalCacheTelemetry: internal → public for Telemetry auto-aggregation)
  - SanitizedExceptionRender (Step 7): 4 internal copies → 1 canonical at `DcsvIo.D2.Utilities.Diagnostics`
- **IRequestContext spec hygiene** (Step 1A):
  - Renamed `FingerprintRiskScore` → `RiskScore` (doc body rewritten to honestly enumerate inputs)
  - Dropped `IsSyntheticEnvelope` (pivot orphan from retired `ContextEnvelope` mechanism)
  - Wire-format change: `x-d2-context` JSON key `fingerprintRiskScore` → `riskScore` (transparent via `JsonNamingPolicy.CamelCase`)
- **8 ServiceDefaults integration test classes + harness** (Step 5) covering composed pipeline E2E
- **Quality**: 0/0 build/inspect across all sub-steps; 2914/2914 tests deterministic

## Step record

| #   | Step                                                                                        | Csproj path                                                                                 | Rounds                       |
| --- | ------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------- | ---------------------------- |
| 0   | Branch checkout                                                                             | —                                                                                           | —                            |
| 1   | `DcsvIo.D2.Logging` (initial)                                                               | `server/shared/dotnet/logging/`                                                             | 2                            |
| 1A  | IRequestContext spec hygiene (rename + drop + nameof sweep)                                 | spec + cross-cutting cleanup                                                                | 1                            |
| 1B  | Logging LOG-OK enricher expansion (42 LOG-OK / 8 NOT-LOGGED)                                | `server/shared/dotnet/logging/`                                                             | 1                            |
| 1C  | Early rules.md adoption (5 predicates) + Logging KEEP doc phase-framing sweep               | rules.md + logging KEEP docs                                                                | 2                            |
| 2   | `DcsvIo.D2.Telemetry`                                                                       | `server/shared/dotnet/telemetry/`                                                           | 2                            |
| 3   | `DcsvIo.D2.AspNetCore`                                                                      | `server/shared/dotnet/aspnetcore/`                                                          | 2                            |
| 4   | `DcsvIo.D2.ServiceDefaults`                                                                 | `server/shared/dotnet/service-defaults/`                                                    | 2                            |
| 5   | Synthetic-host integration tests                                                            | `server/shared/dotnet/tests/`                                                               | 2                            |
| 6   | Doc updates                                                                                 | `docs/PATTERNS.md`, `docs/v2/PHASE_0.md`, `docs/v2/V2.md`, `server/shared/dotnet/README.md` | 1                            |
| 7   | Rules adoption (§24.13.1 + tracking-doc allowlist) + SanitizedExceptionRender consolidation | rules.md + cross-deliverable Utilities/Auth/Auth.Outbound/Messaging.RabbitMq/AspNetCore     | 1                            |
| F   | Final-review (deliverable-wide)                                                             | —                                                                                           | 3 (R1 + R2 + R3 post-Step-7) |

**18 total audit cycles** across 11 sub-steps + Final-review. Average ~1.6 rounds/step. All 5 LIVE Step-1C predicates + Step 7's §24.13.1 battle-tested across 11 cycles with 4 self-correcting augmentation iterations on §11.28/§14.1.

Convergence map: 1 (2) → 1A (1) → 1B (1) → 1C (2) → 2 (2) → 3 (2) → 4 (2) → 5 (2) → 6 (1) → F R1+R2 (2) → 7 (1) → F R3 (1).

## Locked architectural decisions

| Decision                              | Final                                                                                                                                                                                                                   |
| ------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Logging engine                        | **Serilog** (per rules.md §3.3 PII discipline; `RedactDataDestructuringPolicy` is the safety net)                                                                                                                       |
| Auth wiring in defaults               | **Auto-wire** `AddD2Auth` + `AddD2AuthHttp` + `AddD2AuthGrpc` (>95% of services need; opt-out via `SkipAuthAutoWiring=true`; THROWS at startup if `AuthConfigure` null + Skip false)                                    |
| Middleware order                      | **LOCKED** in `UseD2DefaultPipeline` — no insertion points; per RATE-LIMITING.md §4                                                                                                                                     |
| ServiceDefaults shape                 | **Single csproj**; pure aggregator; ZERO logic (verified by convention test)                                                                                                                                            |
| Encryption                            | Stays **opt-in** (per-domain keying)                                                                                                                                                                                    |
| Opt-ins                               | Services call owning libs' existing builders directly (`AddD2Postgres`, `AddD2RedisDistributedCache`, `AddD2RabbitMq`, `AddD2EncryptionFor`, `AddD2AuthOutbound`)                                                       |
| Logging vs Telemetry split            | Separate libs — Logging (Serilog + redaction) is independent of Telemetry (OTel SDK); some services may want one without the other                                                                                      |
| Local cache wiring in defaults        | Auto-wire `AddD2LocalCache()` (zero external deps; near-universal)                                                                                                                                                      |
| Per-step integration tests            | Every step ships its own integration tests with mocked inputs to verify runtime behavior end-to-end (codified as §1.19 in rules.md)                                                                                     |
| `UseD2InfrastructureBypass` default   | **SHORT-CIRCUITS** by default (`/health`, `/alive`, `/metrics`, `/.well-known/*`); `TagOnly=true` opt-out preserves marker-only mode                                                                                    |
| `RunD2ServiceAsync` exception logging | Uses `SanitizedExceptionRender.TypeName(ex)` + `FirstFrame(ex)` separately — never raw `ex.Message` (per §3.1 PII discipline)                                                                                           |
| HSTS preload                          | NOT in default — one-way-door per-service decision                                                                                                                                                                      |
| CORS default                          | Fail-closed (empty origins = no allowed origins; explicit configuration required)                                                                                                                                       |
| 42 LOG-OK fields / 8 NOT-LOGGED       | Operational data + opaque IDs + capability metadata + hashes are LOG-OK; raw IPs + sub-country geographic precision (City/Region/SubdivisionCode/PostalCode/Lat/Long/Geohash) + plaintext PII (Username) are NOT-LOGGED |

## Kinds-of-misses log (per-step distillation)

### Step 1 — DcsvIo.D2.Logging (2 rounds)

3 R1 findings, all mechanical-hygiene, closed by Fixer R1, R2 clean:

- §5.1 HIGH + §5.24 MEDIUM (same site) — foundation lib slipped on 1 of 10 sites of `Falsey()` dogfood (`string.IsNullOrWhiteSpace` at `InfrastructurePathMatcher.cs:61`)
- §7.14 LOW — 4 lines >100 chars (1 production + 3 test)

**Pattern**: all R1 misses were grep-detectable predicates already in rules.md, but only run POST-HOC by Auditor, not PRE-FLIGHT by Implementer at write-time. Mechanical hygiene leaked through; high-stakes categories (PII §3.3, transport §9.2, smart-ctor §9.4, OOTB §16) all clean on R1 thanks to Plan's pre-emptive gate checks. Surfaced the §24.13 "pre-flight Evidence greps" candidate.

### Step 1A — IRequestContext spec hygiene (1 round — ideal)

Zero R1 findings. Plan's pre-emptive gate checks (practicing Step 1's §24.13 candidate) caught every applicable predicate at write-time. Validated §24.13 (pre-flight greps) + §5.25 (nameof discipline) candidates.

### Step 1B — Logging LOG-OK enricher expansion (1 round — ideal)

Zero R1 findings. 42 LOG-OK / 8 NOT-LOGGED contract shipped clean. Implementer HONESTLY reconciled Plan-vs-reality on Concern A — Plan claimed last-writer-wins for HTTP RequestId/RequestPath; reality is Serilog 9.x's `AddPropertyIfAbsent` silently drops them. Pinned via integration test + corrected docs rather than force-fitting to wrong claim. Validated §1.19 (per-step integration tests) — caught an emergent runtime concern pure unit tests would have missed.

### Step 1C — Early rules.md adoption + Logging KEEP doc phase-framing sweep (2 rounds)

1 R1 FINDING-MEDIUM (§11.28 regex incompleteness — the predicate added in this step self-validated by surfacing its own regex gaps via Auditor manual read). Closed by Fixer R1 with 3-part fix (2 README rewrites + regex augmentation). R2 clean.

**Pattern**: self-correcting predicate loop — adding §11.28 in R1 immediately surfaced its own first-walk maintenance need; same Auditor caught it via spirit-check; Fixer augmented in same round; R2 confirmed clean. The 2-round-on-1C was the COST of adding predicates that immediately self-validated, not a regression.

§13.13 (Plan-vs-reality reconciliation) practiced TWICE within the step itself — empirical proof the predicate works as intended.

### Step 2 — DcsvIo.D2.Telemetry (2 rounds)

4 R1 findings (2 MEDIUM + 2 LOW), all closed by Fixer R1, R2 clean:

- §14.1 MEDIUM × 3 — `Phase-0` hyphenated form leaked past `'Phase [0-9]'` (spaced) regex
- §11.28 MEDIUM × 2 — `"will live in"` + `"future X aggregator"` phrasings escaped post-1C-augmented regex
- §5.1 LOW × 1 — `string.IsNullOrEmpty` in test file
- §7.15 LOW × 4 — British spellings (Centralising/Honour/Materialising/Serialise)

**Pattern**: 2nd consecutive instance of self-correcting regex-driven predicate evolution. §14.1 + §11.28 augmented; propagated to all subsequent steps + future deliverables.

### Step 3 — DcsvIo.D2.AspNetCore (2 rounds)

1 R1 LOW finding (§11.28 × 2 — `"future cross-cutting middleware"`), closed by Fixer R1, R2 clean.

**Pattern**: 3rd consecutive self-correcting cycle on §11.28. Root cause was EXECUTION-FIDELITY gap in Implementer's pre-flight grep (claimed 0 hits; original Step-2 regex actually DID match — `cross-cutting` is hyphenated single token). Step 3 R1 augmentation added `\b` word-boundary + 0-3 adjective tokens.

**Cross-step consolidation completed**: InfrastructurePathMatcher promoted to public in DcsvIo.D2.AspNetCore; both Logging + Telemetry internal duplicates DELETED; both consumers swapped; both per-lib path-matcher test files deleted (re-homed to AspNetCore tests). 12 cross-step file touches; per-step audit scope per §24.7 included them all; 0 consolidation-related findings.

**8 §13.13 reconciliations** (highest count yet): SanitizedExceptionRender duplication (later resolved in Step 7), init→set on options, slnx alphabetical placement, per-lib test deletion, bypass middleware needing `GetEndpoint().RequestDelegate`, AddD2HealthChecks idempotency marker, SELF_CHECK_NAME hoist for SA1201, Kestrel ephemeral port for test stability.

### Step 4 — DcsvIo.D2.ServiceDefaults (2 rounds) — DELIVERABLE NAMESAKE SHIPPED

1 R1 MEDIUM finding (§11.9 — README cited `docs/v2/PHASE_0.md` from KEEP doc), closed by Fixer R1, R2 clean.

**Pattern**: ENUMERATION-completeness gap — Implementer's pre-flight grep checklist didn't include §11.9's cross-doc-citation pattern. Suggested formalizing the canonical pre-flight grep checklist as §24.13.1 (later adopted in Step 7).

**4 §13.13 reconciliations** all clustered around Auth opt-out path threading: orchestrator override (THROW on null AuthConfigure) opened the door for 4 follow-on adjustments — services registration, middleware skip, options binding into DI, conditional middleware ordering.

**Step 4 step-shape achievement**: Thin aggregator (ZERO logic; pure delegation) verified by `Assembly_HasNoNonStaticPublicClassesOtherThanOptions` convention test.

### Step 5 — Synthetic-host integration tests (2 rounds)

3 R1 findings (§14.1 MEDIUM × 3 + §11.28 MEDIUM × 2 + §5.1 LOW × 1 + §7.15 LOW × 4 — counted as separate finding categories). Wait, this is incorrect summary; correct: 3 main findings — §14.1 + §14.3 MEDIUM × 12 sites (Step-N framing in test source) + §7.16 LOW × 8 sites (multi-paragraph block comments).

**Pattern**: 4th consecutive self-correcting cycle on §14.1/§11.28 family — augmented to catch `\bStep[ -][0-9]+[A-Z]?\b` form. META-pattern: deliverable-step framing leaks into TEST CODE more than into production code (test files are organized by step, so the journal narrative bleeds into source-prose).

Test infrastructure REUSE worked perfectly per Plan: 14 new files reused all 4 capture mechanisms from prior steps. 1 cross-step touch + 0 production-code touches: Step 5's COMPOSED pipeline tests passed against shipped code as-is, validating that Steps 1-4 shipped a coherent system.

MEL-bridge follow-up (orchestrator-accepted deferral): Implementer correctly identified the limit of in-process testability for the Serilog↔OTel-MEL bridge integration; deferred to future Phase 1+ E2E against a real OTel collector.

### Step 6 — Doc updates (1 round — ideal)

DOCS-ONLY scope + thorough Plan modify-table + tracking-doc allowlist explicitly called out + verify-then-decide approach for 2 docs = first-pass clean. 3 honest §13.13 reconciliations: MESSAGING.md verify-pass-with-no-changes; AUDIT_CHECKLIST.md added 3 concrete bullets; PHASE_0.md fix-on-sight `Centralise→Centralize`.

### Step 7 — Rules adoption + SanitizedExceptionRender consolidation (1 round — ideal)

Zero R1 findings. **§24.13.1 self-validated immediately** — Implementer's pre-flight grep set surfaced 2 §7.14 violations on first walk; both closed pre-handoff.

**Architectural-debt closure**: SanitizedExceptionRender drift had already happened (3-of-4 majority shape vs 1 divergent in messaging-rabbitmq). Consolidated to canonical at `DcsvIo.D2.Utilities.Diagnostics` (public). Cross-deliverable touches: 5 deliverables affected (Utilities + Auth + Auth.Outbound + Messaging.RabbitMq + AspNetCore). Per §24.7 cross-step audit scope, all in scope; clean R1.

3 §13.13 reconciliations: xmldoc cref FQN required by Roslyn; messaging consumer needed ZERO null-handling adjustments due to `string?`-typed `[LoggerMessage]` parameter backward-compat; utilities/README.md exceeds §11.21 300-line heuristic (pre-existing baseline + reconciliation deferral for future Utilities README split).

### Final-review (R1 + R2 + R3 post-Step-7)

R1 surfaced 1 LOW (§7.14 line length in TelemetryPipelineE2ETests.cs:161); R2 verified clean. Step 7 added new scope; R3 re-swept post-Step-7 deliverable-wide; clean. Deliverable Completeness Checklist all-YES.

## Architectural debt + future tracking

- **Cross-deliverable §14.1 leak** at `JwtValidatorTests.cs:411` (`pre-fix` from commit `4dc6be74` of deliverable 0002) — intentionally out-of-scope for this deliverable; future cleanup sweep target
- **utilities/README.md ≤300-line heuristic violation** — pre-existing baseline 348 lines + 20-line Diagnostics section addition; future Utilities README sub-doc split per §11.21's "split into linked sub-docs" remedy
- **MEL-bridge integration verification** — orchestrator-accepted deferral; test against real OTel collector in Phase 1+ E2E

## Process integrity

This deliverable executed under the canonical orchestrator-only main-thread workflow per CLAUDE.md MANDATORY block 0:

- Every planning, implementation, audit, and fix round = NEW fresh sub-agent (~70+ sub-agent invocations across the deliverable)
- Per-step audit loop with 10-iteration ceiling; 3-artifact journal model per step
- Final-review walked deliverable-wide (cross-step drift caught here)
- Every commit had explicit per-occurrence user permission
- Wip workspace gitignored; per-step journals retained locally only

**~25+ §13.13 Plan-vs-reality reconciliations** honestly documented across the deliverable.

**5 orchestrator overrides** on Planner open questions, all ASKED + DOCUMENTED:

- Step 1B: drop SubdivisionCode from LOG-OK (§3 conservative call)
- Step 4: Override 1-4 around Auth opt-out path threading + RunD2ServiceAsync naming
- Step 6: Include 5-layer rename safety net in PATTERNS.md scope (vs SHIP-gate defer)

**1 ESCALATION-rule application** (Step 5 MEL-bridge follow-up; orchestrator-accepted deferral).
