<!--
Copyright (c) DCSV. All rights reserved.
-->

# 0013 — Auth endpoint-declaration hardening (explicit scope match-mode + deny-by-default boot guard)

- **Status**: COMPLETE — review-ready (final-review converged; gates green).
- **Branch**: `n/auth-hardening-explicit-scopes`
- **Commits**: `815af202` (explicit match-mode) · `512b1c2c` (boot guard + integration tests) · final-review fixes + S7 docs uncommitted on top.

## Goal

Close two footguns in the shipped `D2.Shared.Auth` per-endpoint authorization:

1. **Silent under-protection** — a business endpoint mapped without a scope or a harmless marker was not rejected: the JWT was validated and then any authenticated caller passed with no scope check. "Forgot to annotate" === "reachable by every logged-in user," with no error.
2. **Confusing scope semantics** — "required scopes" silently meant *any-of* at the endpoint but *all-of* at the handler, under the same name; the HTTP README documented it wrong.

## What shipped

| Step | Component |
|---|---|
| S1 | **HTTP transport** — `ScopeMatch {Any,All}`; `EndpointScopeMetadata` carries `Scopes` + `Match`; `RequireAnyScope`/`RequireAllScopes` replace `RequireD2Scope`; match-aware `JwtAuthMiddleware`. |
| S2 | **gRPC transport** — `MethodScopeMetadata` (Scopes+Match); `RequireAnyScope`/`RequireAllScopes` fluent (constrained to `GrpcServiceEndpointConventionBuilder`); `[D2RequireScope]` split into `[D2RequireAnyScope]`/`[D2RequireAllScopes]`; match-aware interceptor with a single-pass last-declared-wins 3-attribute precedence; gRPC `grpcunimplemented` catch-all skip. |
| S3 | **`ThrowIfFalsey` guard utility** — `D2.Shared.Utilities.GuardExtensions` (4 overloads: `string?`/`IEnumerable<T>?`/`Guid?`/`Guid`; C#14 block form; BCL-split — `ArgumentNullException` for null, `ArgumentException` for present-but-falsey; `[CallerArgumentExpression]` + `[NotNull]`). Plain-object null-guards stay on BCL `ThrowIfNull`. |
| S4 | **Codebase sweep** — 20 string-guard sites converted to `ThrowIfFalsey` across auth/messaging/i18n/encryption; Encryption gained a Utilities reference (+ §9.8 dep-graph edge); ~15 test-flips (null → `ArgumentNullException`). |
| S5 | **Handler layer** — `HandlerScopeMatch` + `ScopeRequirement(Match, Scopes)` (parallel enum keeps the handler layer auth-free); match-aware `BaseHandler` pre-check. |
| S6 | **Deny-by-default boot guard** — new `D2.Shared.Auth.Startup` project + `AuthEndpointGuardStartupFilter` (`IStartupFilter`) that walks the populated `EndpointDataSource` before traffic and throws if any endpoint lacks a declared intent (scope-match or harmless); infra paths + gRPC catch-alls exempt; ServiceDefaults wiring + `SkipAuthEndpointGuard` (default off ⇒ guard on). |
| — | **Runtime integration tests** — real-host (real RS256 JWT + real validator) proofs of gRPC-attribute scope enforcement, HTTP-middleware scope enforcement, and the handler `ScopeRequirement` defense-in-depth. |
| S7 | **Docs** — auth/grpc + auth/http + new auth/startup + service-defaults READMEs; ADR-0012 §5 (+§4); ADR-0005; PATTERNS.md deny-by-default boot-guard section. |
| S8 | **Final review** — K=12 sweep → fixes → clean-confirmation → certification gates. |

## Kinds-of-misses log (self-improvement evidence)

Bugs/gaps caught by the audit loop + investigations, each now regression-pinned:

- **Boot guard was a silent no-op in production** (caught by the user-requested integration tests → investigation). As an `IHostedService` it read an empty DI `EndpointDataSource` in the `WebApplication` model (mapped endpoints live in a separate collection until the pipeline builds). Fixed → `IStartupFilter` that walks after `next(app)`. Pinned by `AuthEndpointGuardWebApplicationTests` (the two undeclared-throws tests fail on the old `IHostedService` code).
- **gRPC catch-all false-positive** — `MapGrpcService<T>()` adds `{grpcunimplemented}` catch-all endpoints with no auth metadata; the guard would fail-boot every gRPC service. Fixed → skip via the `grpcunimplemented` route constraint. Pinned by `AuthEndpointGuardGrpcBootTests`.
- **`ThrowIfFalsey` generic overload swallowed concrete collections** — an empty `List<string>` bound to the generic `T:class` overload (identity conversion) over `IEnumerable<T>`, passing the guard silently. Fixed → dropped the generic overload (plain objects use BCL `ThrowIfNull`). Pinned by `EmptyList_BindsToEnumerableOverload` + `EmptyString_BindsToStringOverload`.
- **Cross-transport metadata acceptance (A12-F1)** — the guard accepted gRPC `MethodScopeMetadata` on an HTTP endpoint (passed boot, not enforced). Fixed → gRPC fluent compile-constrained to `GrpcServiceEndpointConventionBuilder`; the narrow attribute-on-HTTP residual is documented.
- Empirically settled (not assumed): gRPC class/method attributes **do** project onto endpoint metadata via `MapGrpcService<T>()` (proof test retained).
- Audit-methodology: a fresh auditor reading committed HEAD (not the on-disk working tree of an uncommitted deliverable) reported stale findings — surfaced the "audit the working tree" rule for uncommitted deliverables.

## Deliverable Completeness Checklist

- [x] **Per-step audit loops converged** — each step (S1–S6 + runtime-integration) ran a targeted audit; findings fixed to a clean sweep (per-step journals under `docs/wip/0013-auth-hardening/<step>/journal.md`).
- [x] **Final-review sweep converged** — full K=12 sweep ran; every finding fixed; a fresh closure auditor verified ALL closed (closure-by-absence); the clean-confirmation's residual doc findings fixed + re-verified (`09-final-review/journal.md`).
- [x] **Build clean** — `dotnet build server/D2.slnx` → 0 warnings / 0 errors.
- [x] **JetBrains clean** — `jb inspectcode server/D2.slnx --severity=WARNING` → 0 findings.
- [x] **Tests green** — `dotnet test` → 4691 passed / 0 failed (1 flaky CORS E2E confirmed flaky via re-run, unrelated).
- [x] **Every public path tested first-pass** — verified by the §1 final-review auditor (incl. `ScopeMatch`/`HandlerScopeMatch`/`ScopeRequirement`/metadata/attributes/fluent/`GuardExtensions`/the filter).
- [x] **Every bug fix regression-pinned** — see the kinds-of-misses log; each fix has a fails-without/passes-with test.
- [x] **Runtime enforcement proven by integration tests** — gRPC attribute + fluent, HTTP middleware (any/all), handler `ScopeRequirement`.
- [x] **Doc parity** — READMEs/ADRs/PATTERNS verified against shipped code by the §11 auditor; no stale API names; no phase/CLAUDE.md/provenance refs in keep docs.
- [x] **No generated file hand-edited** — verified by the §26 auditor (sweep + all steps excluded `*.g.cs`/`Generated/`/emitter strings).
- [x] **Layer hygiene + dep graph** — handler layer has no auth dependency; the Mermaid dep graph reflects every new edge (§9.8); verified by the §9 auditor.
- [x] **Observability intact** — telemetry counters reused correctly; the EventId collision fixed; verified by the §10 auditor.
- [ ] **Cross-language parity** — N/A: scope enforcement is a .NET-transport concern (Edge is .NET); the SvelteKit BFF does not enforce scopes. No TS counterpart in scope.

## Attestation

I attest that every box in the Deliverable Completeness Checklist above is an honest YES (the cross-language-parity box is a justified N/A). The per-step audit loops and the full K=12 final-review loop converged to finding-free sweeps; all findings — including the boot-guard no-op, the gRPC catch-all false-positive, the `ThrowIfFalsey` overload-resolution defect, and the cross-transport metadata acceptance — were fixed and regression-pinned; the final certification gates (build 0 warnings, `jb inspectcode` 0 findings, 4691 tests passing) are green on the current working tree. This deliverable is ready for user REVIEW.

## Rule additions produced by this deliverable

Three rules added to `docs/dev/rules.md` + lockstep duplications at SHIP:

1. **§5.1a — `ThrowIfFalsey` guard predicate**: required-argument guards on string / collection / Guid use `x.ThrowIfFalsey()` (BCL-split null vs falsey) instead of raw `ArgumentNullException.ThrowIfNull` / `ArgumentException.ThrowIfNullOrWhiteSpace` / hand-rolled throws. Lockstep: CLAUDE.md §5 short-list bullet + `server/shared/dotnet/utilities/Extensions/README.md` `GuardExtensions` entry.
2. **§24.19 — Audit dispatch briefs for uncommitted deliverables MUST instruct sub-agents to read the on-disk working tree**: dispatch briefs include an explicit "Working-tree note" when the deliverable's latest Implementer/Fixer output is uncommitted; sub-agents must not rely on `git diff HEAD` or `git show HEAD:path`. Lockstep: `docs/dev/process.md §4 Per-round dispatch protocol` Step 1 contents.
3. **§5.25a — No redundant `!` after AwesomeAssertions `.Should().NotBeNull()`**: after `.Should().NotBeNull()`, the non-null post-condition flows to both the compiler and `jb inspectcode`; a following `x!.Member` is redundant and flagged by `jb`. Use `x.Member`.
