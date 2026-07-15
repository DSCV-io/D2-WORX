<!--
Copyright (c) DCSV. All rights reserved.
-->

# Deliverable 0003 — `[D2AllowAnonymous]` → `[D2HarmlessEndpoint]` rename

**Status**: ✅ READY FOR USER REVIEW (shipped to `nova` @ `9d027cf8` — 2026-05-12; retroactive snapshot backfill)
**Started**: 2026-05-11
**Branch**: `n/handler` → squash-merged to `nova` as `9d027cf8`
**Predecessor**: `0002-auth-inbound` (introduced the original `[D2AllowAnonymous]` surface)
**Successor**: `0004-service-defaults` (branched from `nova` @ `9d027cf8`, i.e. directly off this rename's ship commit)
**Type**: Mechanical symbol rename across the .NET auth surface (attribute + fluent extension + metadata-record factory + boolean property + interceptor/middleware internals + tests + READMEs + 1 cross-cutting doc) with load-bearing xmldoc rewrite

**Retroactive snapshot note**: This SHIP-time README was authored after the fact (2026-05-19) from the original PLAN-form workspace README + the converged per-step journal at `01-rename/journal.md` + the ship commit `9d027cf8` (git stat + commit body). The SHIP step's snapshot copy to `docs/dev/deliverables/` was skipped at the original SHIP time; this backfill closes that gap. No facts have been retro-fitted — every claim below is grounded in the workspace evidence or the committed diff. Per `rules.md` §11 KEEP-doc definition, `docs/dev/deliverables/` is allowlisted-OUT (deliverable IDs + commit refs PERMITTED here).

## Context

The pre-rename attribute `[D2AllowAnonymous]` (shipped under deliverable `0002-auth-inbound`) reads at the call site as a generic "this endpoint accepts anonymous callers" annotation — the kind of attribute a developer reaches for casually when an endpoint "should be public." The runtime semantic is much narrower: the attribute SKIPS the entire JWT validation pipeline (signature + claims + session liveness + scope check). If misused on an endpoint that returns sensitive data, the result is sensitive data exposed without any authentication — a security bug at the deployment boundary.

The rename to `[D2HarmlessEndpoint]` is deliberately odd-sounding so it is HARDER to ignore at use sites. A name that flows naturally ("AllowAnonymous") gets approved on auto-pilot during code review; a name that sounds weird ("HarmlessEndpoint") forces a moment of "wait, what's a 'harmless endpoint'?" The xmldoc on every renamed surface enumerates the legitimate use cases verbatim (k8s/Docker liveness + readiness probes; intra-cluster service-to-service health/info endpoints with NO sensitive data; OIDC discovery endpoints) and warns explicitly that ANY data exposure beyond those cases is a security bug. The friction at the call site is the entire point.

**No behavioral change** — pure rename + xmldoc rewrite + doc updates. The interceptor + middleware treat `[D2HarmlessEndpoint]` exactly as `[D2AllowAnonymous]` did; the precedence chain (fluent > attribute > deny-by-default), the LAST-declaration tiebreak helper, and the pattern-match short-circuit are all preserved through the rename.

## Scope

**IN — surfaces renamed**:

1. **Attribute** — `[D2AllowAnonymous]` (gRPC, `DcsvIo.D2.Auth.Grpc.Endpoints`) → `[D2HarmlessEndpoint]`. HTTP has no parallel attribute (HTTP-side opt-in is fluent-only).
2. **Fluent extensions** — `.AllowD2Anonymous<TBuilder>()` on both `RequireD2ScopeExtensions` (HTTP) + `RequireD2GrpcScopeExtensions` (gRPC) → `.MarkAsD2HarmlessEndpoint<TBuilder>()`.
3. **Metadata-record factory + boolean property** — `EndpointScopeMetadata.Anonymous` + `IsAnonymous` (HTTP), `MethodScopeMetadata.Anonymous` + `IsAnonymous` (gRPC) → `.HarmlessEndpoint` + `IsHarmlessEndpoint` on both.
4. **Interceptor + middleware internals** — private helper `IsAnonymousLastDeclaration` → `IsHarmlessEndpointLastDeclaration`; locals `anon` / `lastAnon` → `harmlessEndpoint` / `lastHarmless`; pattern-match `metadata is { IsAnonymous: true }` → `metadata is { IsHarmlessEndpoint: true }`.
5. **Tests** — file rename (`D2AllowAnonymousAttributeTests.cs` → `D2HarmlessEndpointAttributeTests.cs`), class rename, method renames, route literals (`/anon` → `/harmless`, `anon-ok` → `harmless-ok`), test proto comment.
6. **xmldoc rewrites** — load-bearing on attribute, fluent extension (HTTP+gRPC), metadata-factory singleton (HTTP+gRPC), boolean property (HTTP+gRPC); every rewrite enumerates the legitimate use cases + sensitive-data warning + odd-name framing rationale.
7. **READMEs** — `auth-grpc/README.md` (Footguns + Per-method-scope sections), `auth-http/README.md` (Per-endpoint-scope + Quickstart sections), `server/shared/dotnet/README.md` (parent libs catalog rows).
8. **Cross-cutting doc** — `docs/PATTERNS.md` JwtAuth section (lib-summary lines + the anonymous-opt-out line at L604, which carried a factual error about deny-by-default that the audit surfaced + fixed in the same change).
9. **Pinning tests** — 3 new tests across 2 transports (attribute name + 2 metadata-factory factory/property names) using LITERAL-STRING reflection so a future Phase 3 build-error analyzer can pin against the type-name string and the metadata factory/property names without `nameof()` silently following any future rename.

**OUT — explicit non-goals**:

- The `Anonymous` namespace identifier in the `Scopes.Auth.Anon` codegen family — that's a different concept (scope-string classification per `Scopes.IsAnonymous`), not the auth-bypass attribute.
- The `TestHealth` / `TestEcho` proto service identifiers — they describe what the test SERVICE does, not how it's wired. Only the wiring comment was updated.
- Versioning — the rename is a BREAKING change to two shipped libs (`DcsvIo.D2.Auth.Grpc` + `DcsvIo.D2.Auth.Http`), but no separate release lands before the surrounding `n/handler` stack's own breaking changes do, so the major version bump applies to the combined SHIP.
- Node-side mirror of `D2HarmlessEndpoint` — the Node auth runtime is not yet built; gap documented, not in scope.

## Decisions resolved during PLAN

| Decision                                              | Value                                                                                                                                 | Rationale                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                          |
| ----------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| Attribute name                                        | `[D2HarmlessEndpoint]` (not `[D2HealthCheck]`)                                                                                        | `D2HealthCheck` over-narrows — forecloses OIDC discovery + intra-cluster info endpoints + codegen anon family. `D2HarmlessEndpoint` is deliberately odd-sounding to force a pause at the use site. The "Endpoint" suffix anchors the framing to the endpoint's nature, not the request's auth state.                                                                                                                                                                                                               |
| Fluent extension name                                 | `MarkAsD2HarmlessEndpoint()` (not `D2HarmlessEndpoint()` or `AllowD2HarmlessEndpoint()`)                                              | `D2HarmlessEndpoint()` reads aloud identically to `[D2HarmlessEndpoint]` (ambiguous in conversation); `AllowD2HarmlessEndpoint()` keeps the "Allow" verb the rename is trying to retire. `MarkAs*` is BCL-idiomatic (`MarkAsExternalSurrogate`, `MarkAsTopLevel`) and signals an explicit declarative annotation.                                                                                                                                                                                                  |
| Metadata-factory singleton name                       | `EndpointScopeMetadata.HarmlessEndpoint` (HTTP) / `MethodScopeMetadata.HarmlessEndpoint` (gRPC)                                       | Leaving `.Anonymous` while the attribute is `[D2HarmlessEndpoint]` lets the old "anonymous" framing survive at the metadata layer, defeating the rename's friction goal.                                                                                                                                                                                                                                                                                                                                           |
| Boolean property name                                 | `IsHarmlessEndpoint` (renamed from `IsAnonymous`)                                                                                     | `IsAnonymous` reads as "anonymous = OK to reach without auth = no big deal." `IsHarmlessEndpoint` reads as "the operator has actively asserted no harm."                                                                                                                                                                                                                                                                                                                                                           |
| Internal locals + private helper                      | RENAME (`anon` → `harmlessEndpoint`; `lastAnon` → `lastHarmless`; `IsAnonymousLastDeclaration` → `IsHarmlessEndpointLastDeclaration`) | Internal naming is part of the friction surface for future engineers reading the interceptor/middleware code.                                                                                                                                                                                                                                                                                                                                                                                                      |
| Test method names                                     | RENAME in lockstep (`AnonymousService_NoBearer_Succeeds` → `HarmlessEndpointService_NoBearer_Succeeds`; same shape for the other 7)   | Same rationale.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                    |
| Test proto comments                                   | UPDATE the `.AllowD2Anonymous()` references; KEEP the `TestHealth` / `TestEcho` service identifiers                                   | The service models a "health-check" endpoint (canonical legitimate harmless case); only the wiring comment changed.                                                                                                                                                                                                                                                                                                                                                                                                |
| README Footguns sections                              | REWRITE around `HarmlessEndpoint` terminology with the security warning verbatim from the xmldoc                                      | The legitimate-use-cases enumeration moves into the README as the primary framing; the BCL-attribute-not-honored note becomes a secondary footgun under it.                                                                                                                                                                                                                                                                                                                                                        |
| `PHASE_0_AUTH.md` + `PHASE_0.md` + `RATE-LIMITING.md` | NO changes (verified grep-clean during PLAN; `RATE-LIMITING.md:69` references the BCL `[AllowAnonymous]`, not the D2 attribute)       | Tracking docs already aligned; no drift to fix.                                                                                                                                                                                                                                                                                                                                                                                                                                                                    |
| 0002 `REVIEW-MANIFEST.md`                             | LEAVE UNTOUCHED (local-only gitignored artifact)                                                                                      | Future readers searching for `D2AllowAnonymous` in 0002 will see the historical state, which is correct for a shipped manifest.                                                                                                                                                                                                                                                                                                                                                                                    |
| Pinning test approach                                 | New test per transport asserting the type name + metadata factory/property names                                                      | Future Phase 3 work will add a Roslyn analyzer that errors on `[D2HarmlessEndpoint]` use outside an allowlist; the analyzer pins against the literal type-name string. The pinning test fails if a future rename silently breaks the analyzer's contract. (Implementation note: original Plan spec used `nameof()`; the per-step audit surfaced that `nameof()` compile-time-resolves alongside renames and defeats the pin — Fixer corrected to literal-string reflection lookup. See Audit cycle history below.) |
| Versioning                                            | BREAKING change; `versionize` will bump major on next release; combined with surrounding `n/handler` work                             | Ships under the umbrella of the WIP `n/handler` branch.                                                                                                                                                                                                                                                                                                                                                                                                                                                            |

## Step plan + execution

Single step (`01-rename/`); no separate FINAL-REVIEW step because the per-step audit IS the deliverable audit (only one step).

| #   | Step                                                                                                                                                | Status       | Rounds                                                       | Workspace                                      |
| --- | --------------------------------------------------------------------------------------------------------------------------------------------------- | ------------ | ------------------------------------------------------------ | ---------------------------------------------- |
| 1   | Rename `[D2AllowAnonymous]` → `[D2HarmlessEndpoint]` + all consequential renames + xmldoc rewrites + README + PATTERNS.md updates + 3 pinning tests | ✅ CONVERGED | 2 (R1 found 2 findings + 1 design defect → Fixer → R2 CLEAN) | [`01-rename/journal.md`](01-rename/journal.md) |

**Convergence map**: R1 (2 FINDING + 1 design observation) → Fixer R1 → R2 (0 FINDING) → SHIP.

## What shipped (verified against `git show 9d027cf8 --stat`)

**26 files changed, +437 / -298** across the following directories:

- `server/shared/dotnet/auth-grpc/`:
  - `Endpoints/D2AllowAnonymousAttribute.cs` DELETED → `Endpoints/D2HarmlessEndpointAttribute.cs` CREATED (74 lines; full xmldoc with legitimate-use-cases + sensitive-data warning + odd-name rationale + precedence + BCL cross-ref)
  - `Endpoints/D2RequireScopeAttribute.cs` (xmldoc cross-ref)
  - `Endpoints/MethodScopeMetadata.cs` (factory rename `.Anonymous` → `.HarmlessEndpoint`; property rename `IsAnonymous` → `IsHarmlessEndpoint`; xmldoc rewrite on both)
  - `Endpoints/RequireD2GrpcScopeExtensions.cs` (`.AllowD2Anonymous` → `.MarkAsD2HarmlessEndpoint`; xmldoc rewrite)
  - `Interceptors/JwtAuthInterceptor.cs` (private helper rename + locals rename + pattern-match update)
  - `Interceptors/ServerCallContextRequestContextExtensions.cs` (xmldoc cross-ref)
  - `README.md` (Footguns + Per-method-scope sections rewritten around `HarmlessEndpoint`; Tests-section enumeration appended with pinning-test notes per R1 Fixer)
- `server/shared/dotnet/auth-http/`:
  - `Endpoints/EndpointScopeMetadata.cs` (same shape as gRPC metadata)
  - `Endpoints/RequireD2ScopeExtensions.cs` (fluent rename + xmldoc rewrite)
  - `Middleware/JwtAuthMiddleware.cs` (xmldoc + pattern-match update)
  - `Middleware/HttpContextRequestContextExtensions.cs` (xmldoc cross-ref)
  - `README.md` (Per-endpoint-scope + Quickstart sections updated; Tests-section enumeration appended per R1 Fixer)
- `server/shared/dotnet/tests/`:
  - `Protos/d2_test_auth.proto` (header comment substitution `.AllowD2Anonymous()` → `.MarkAsD2HarmlessEndpoint()`)
  - `Unit/Auth/Inbound/Grpc/Endpoints/D2AllowAnonymousAttributeTests.cs` DELETED → `Unit/Auth/Inbound/Grpc/Endpoints/D2HarmlessEndpointAttributeTests.cs` CREATED (includes new pinning test `Attribute_TypeName_PinnedForFutureAnalyzer` using LITERAL-STRING reflection)
  - `Unit/Auth/Inbound/Grpc/Endpoints/MethodScopeMetadataTests.cs` (factory + property name renames + new pinning test `HarmlessEndpoint_FactoryAndPropertyName_PinnedForFutureAnalyzer` using `Type.GetField("HarmlessEndpoint", ...)` + `Type.GetProperty("IsHarmlessEndpoint")` literal lookups; explicit comment forbidding `nameof()` and explaining why)
  - `Unit/Auth/Inbound/Grpc/Endpoints/RequireD2GrpcScopeExtensionsTests.cs` (4 method names renamed)
  - `Unit/Auth/Inbound/Grpc/GrpcAuthIntegrationTests.cs` (test method `HarmlessEndpointService_NoBearer_Succeeds`)
  - `Unit/Auth/Inbound/Grpc/Interceptors/JwtAuthInterceptorTests.cs` (test method `UnaryServerHandler_HarmlessEndpointMetadata_SkipsValidatorAndCallsContinuation`)
  - `Unit/Auth/Inbound/Http/Endpoints/EndpointScopeMetadataTests.cs` (factory + property renames + new pinning test mirroring gRPC shape)
  - `Unit/Auth/Inbound/Http/Endpoints/RequireD2ScopeExtensionsTests.cs` (4 method names renamed)
  - `Unit/Auth/Inbound/Http/AuthAppBuilderExtensionsTests.cs` (route literal `/anon` → `/harmless`; test method `UseD2Auth_HarmlessEndpoint_PassesWithoutBearer`)
  - `Unit/Auth/Inbound/Http/Middleware/JwtAuthMiddlewareTests.cs` (1 reference)
- `server/shared/dotnet/README.md` (parent libs catalog rows)
- `docs/PATTERNS.md` (JwtAuth section lib-summary lines L565-566 substitution; L604 deny-by-default semantic rewritten from factually-wrong "endpoint with no scope metadata is rejected at startup" to a correct three-state enumeration: no metadata = full pipeline + any authenticated caller; `[D2RequireScope]` = full pipeline + any-of scope match; `[D2HarmlessEndpoint]` = bypasses pipeline)

**Build + test verification at SHIP** (from commit body):

- `dotnet build server/D2.slnx` → 0 warnings (StyleCop + JetBrains inspectcode)
- `dotnet test server/shared/dotnet/tests` → 2441 / 2441 pass (was 2438 pre-rename; +3 new pinning tests)

## Audit cycle history

Single step; per-step audit converged in 2 rounds. (Predates the K=5 + Aggregator dispatch pattern formalized later in `0007-wire-parity` — this deliverable used the simpler single-Auditor-per-round model that was canonical at the time.)

### Round 1 (Auditor, 2026-05-11) — 300 rules.md rows walked

**Status counts**: 85 ✅ PASS / 197 ⚪ N/A / 2 ❌ FINDING (1 MEDIUM + 1 LOW) / 16 🟡 (carry-overs + unverified-at-this-stage)

**FINDING-MEDIUM (1)**:

- **§11.20 — `docs/PATTERNS.md:604` factual error about deny-by-default semantic**. The rewrite said: "An endpoint with no scope metadata is rejected at startup." That is FACTUALLY WRONG — per `EndpointScopeMetadata.cs:20-26` xmldoc + actual middleware/interceptor behavior, an endpoint with no metadata runs the FULL pipeline and accepts ANY authenticated caller (the empty required-scope set matches every authenticated request). Either pre-existing drift survived the rewrite, or the rewrite introduced it. Either way it shipped if not fixed.

**FINDING-LOW (1)**:

- **§11.3 — Per-lib README Tests-section enumeration omits the new pinning tests**. `auth-grpc/README.md:269` `MethodScopeMetadataTests.cs` enumeration + `:271` `D2HarmlessEndpointAttributeTests.cs` enumeration + `auth-http/README.md:188` `EndpointScopeMetadataTests.cs` enumeration did not mention the new pinning tests. The whole point of the pinning tests is to document the contract; READMEs not listing them defeated half the documentation value.

**Design defect (auditor observation, NOT a FINDING because Implementer followed Plan spec verbatim — but the Plan spec was wrong)**:

- **Pinning-test mechanism gap on the metadata-side tests**. The gRPC attribute pin (`D2HarmlessEndpointAttributeTests.cs:54-65`) used LITERAL strings via `Should().Be("D2HarmlessEndpointAttribute")` — correct, would catch a silent rename. But the metadata-side pins (`MethodScopeMetadataTests.cs` + `EndpointScopeMetadataTests.cs`) used `nameof(MethodScopeMetadata.HarmlessEndpoint)` / `nameof(MethodScopeMetadata.IsHarmlessEndpoint)` for the reflection lookup — `nameof()` compile-time-resolves, so a rename of `HarmlessEndpoint` → `Harmless` would update the `nameof()` call AND the property simultaneously, and the test would still pass. The test only proves "the field/property exists with whatever name it currently has," not "the field/property has THIS specific name." A future Phase-3 analyzer pinning against the literal string would NOT be protected. Plan revision required.

### Round 1 Fixer (2026-05-11)

- **§11.20 MEDIUM** — `docs/PATTERNS.md:604` rewritten with the correct three-state enumeration (no metadata = full pipeline + any authenticated caller; `[D2RequireScope]` = full pipeline + any-of scope match; `[D2HarmlessEndpoint]` = bypasses pipeline).
- **§11.3 LOW** — `auth-grpc/README.md:269,271` + `auth-http/README.md:188` Tests-section enumerations appended with pinning-test notes (literal-string reflection pin on factory + property; literal-string type-name pin on attribute).
- **Pinning-test design defect** — both metadata-side pinning tests rewritten from `nameof()`-based assertions to literal-string reflection assertions: `Type.GetField("HarmlessEndpoint", ...)` + `Type.GetProperty("IsHarmlessEndpoint")` with bare string literals. Explicit comment block in each test forbids `nameof()` and explains the failure mode.

**Verification after fixes**:

- `dotnet build server/D2.slnx --nologo` → 0 warnings, 0 errors ✓
- `jb inspectcode server/D2.slnx --severity=WARNING --format=Text --no-build` → 0 warnings ✓
- `dotnet test server/shared/dotnet/tests --no-build` → 2441 / 2441 pass ✓

### Round 2 (Auditor, 2026-05-11) — CONVERGED

**Status counts**: 87 ✅ PASS / 200 ⚪ N/A / **0 ❌ FINDING** / 13 🟡 (residual non-blocking carry-overs — none R1-introduced)

**Closure verification** for the three R1 fix entries:

1. **§11.20** — CLOSED. Direct file read of `docs/PATTERNS.md:604-608` confirms the three-state enumeration is in place. The "rejected at startup" / "fall-through to anonymous" phrases are gone (`Grep "rejected at startup\|fall-through to .anonymous"` → zero hits).
2. **§11.3** — CLOSED. All three README Tests-section rows append the appropriate pinning-test note verbatim.
3. **Pinning-test design defect** — CLOSED. Both metadata tests now use `typeof(...).GetField("HarmlessEndpoint", ...)` + `typeof(...).GetProperty("IsHarmlessEndpoint")` literal-string reflection. Repo-grep `nameof\(.*HarmlessEndpoint` against the entire `tests` tree returns zero hits. The tests would now FAIL if either symbol were renamed without simultaneously updating the test's literal string — pinning purpose achieved.

**Residual 🟡 (non-blocking)**: commit/PR convention rules unverified (resolve at husky hook); `auth-grpc/README.md` line count carry-over from 0002 (354 lines over the 300-line heuristic — pre-existing); `[D2HarmlessEndpoint] does NOT honor the BCL [AllowAnonymous]` framing carry-over (defensible explicit footgun-prevention with remediation value); `MarkAsD2HarmlessEndpoint<TBuilder>(this TBuilder builder)` uses classic `this T` extension style for consistency with sibling `RequireD2Scope` in the same file (would require coordinated `RequireD2*ScopeExtensions` family migration to switch to C# 14 `extension(TBuilder builder)` block form); Node-side mirror documented as a gap (Node auth runtime not yet built).

**Audit verdict**: CONVERGED. Zero FINDING rows; all R1 actionables CLOSED; no NEW findings surfaced by independent re-walk of the rules.md catalog.

## Kinds-of-misses log (distilled for future rule candidates)

The per-step audit surfaced two classes of miss worth flagging for `rules.md` predicate consideration in subsequent deliverables:

1. **Pinning tests using `nameof()` defeat the pinning purpose** — `nameof(Type.Member)` compile-time-resolves alongside any rename of `Member`, so the test silently follows the rename and never fails. A pinning test that protects against silent rename MUST use a LITERAL string. The implementer followed the Plan spec, which was the source of the gap; the audit caught it. **Candidate predicate area**: §1.x test-discipline or §5.x C# conventions — "pinning tests for wire-format identifiers, type names, or analyzer-contract names MUST use literal strings, not `nameof()`."

2. **Doc-rewrite passes can introduce factually-wrong content** — when rewriting `PATTERNS.md:604` around the new attribute name, the rewrite paraphrase "endpoint with no scope metadata is rejected at startup" was factually wrong against the runtime semantic. Either pre-existing drift survived or the rewrite introduced it. **Candidate predicate area**: §11.x doc-discipline — "doc rewrites that paraphrase runtime semantic MUST be cross-checked against the runtime code path; cite the source-file lines being summarized." (Subsequent deliverables — notably `0004-service-defaults` — codified §13.13 "Plan-vs-reality reconciliation" in a related spirit.)

Neither candidate was formally adopted into `rules.md` as part of this deliverable (the deliverable was scoped tight and shipped before the kinds-of-misses log surfacing → predicate authoring loop was running as a SHIP-time discipline). Both are captured here for future deliverable distillation.

## Carry-forward to follow-on deliverables (post-SHIP)

| #   | Item                                                                                                  | Severity | Notes                                                                                                                                                                                                                                      |
| --- | ----------------------------------------------------------------------------------------------------- | -------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| 1   | Phase 3 Roslyn analyzer for endpoints declaring neither `[D2RequireScope]` nor `[D2HarmlessEndpoint]` | MEDIUM   | Tracked Phase 3 follow-up per ship commit body. The new pinning tests defend the analyzer's contract (the literal type-name + factory + property names) so the analyzer can pin against those strings without a silent rename breaking it. |
| 2   | Node-side mirror of `D2HarmlessEndpoint`                                                              | LOW      | Node auth runtime not yet built; documented gap, no current parity violation.                                                                                                                                                              |
| 3   | `RequireD2*ScopeExtensions` family migration to C# 14 `extension(TBuilder builder)` block form        | LOW      | `MarkAsD2HarmlessEndpoint<TBuilder>(this TBuilder builder)` retained the classic `this T` extension style for consistency with sibling `RequireD2Scope` in the same file. Coordinated migration deferred.                                  |
| 4   | `auth-grpc/README.md` line count (354 lines, over 300-line heuristic)                                 | LOW      | Pre-existing carry-over from `0002-auth-inbound`; not introduced by this rename.                                                                                                                                                           |

## Snapshot provenance (retroactive backfill — 2026-05-19)

This SHIP-time README is a retroactive backfill, authored after the deliverable shipped to `nova`. Provenance:

- **Workspace source**: `docs/wip/0003-harmless-endpoint-rename/README.md` (PLAN form, 84 lines) — supplied the cross-cutting decisions, symbol-rename mapping outline, locked Q&A, and SHIP checklist scaffolding.
- **Per-step journal**: `docs/wip/0003-harmless-endpoint-rename/01-rename/journal.md` (758 lines) — supplied the Round 1 / Round 2 audit-cycle history, finding severities + file:line citations, Fixer entries, and verification command outputs.
- **Ship commit**: `git show 9d027cf8 --stat` (26 files, +437/-298) + `git show 9d027cf8 --pretty=full -1` (commit body) — confirmed file-rename targets, build/test counts (2441/2441), and ratified the audit-cycle pattern recorded in the journal (Planner → Implementer → Auditor R1 → Fixer → Auditor R2 CLEAN; each round a fresh sub-agent).
- **Cross-references**: `docs/dev/deliverables/0004-service-defaults.md:6` cites `nova @ 9d027cf8` as the branch base for the successor deliverable, anchoring 0003's ship commit in the deliverable chain.

No claim above has been retro-fitted beyond what the workspace + commit evidence supports. The "CONVERGED at R2 with zero findings" verdict, the "2441/2441 tests pass" verification, and the "no behavioral change" attestation are all reproductions of explicit journal + commit-body text. The SHIP-step snapshot copy that was skipped at original ship time is now closed by the parallel write of this file to `docs/dev/deliverables/0003-harmless-endpoint-rename.md`.
