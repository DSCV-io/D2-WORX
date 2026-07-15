<!--
Copyright (c) DCSV. All rights reserved.
-->

# Shipped Deliverables — Snapshot READMEs

This folder holds the post-ship snapshot of each deliverable's root README, copied out of the local `docs/wip/<deliverable>/` workspace at SHIP time. See [../process.md](../process.md) for the loop protocol.

## What lives here

One file per shipped deliverable, named `NNNN-<name>.md` (4-digit index prefix so the directory listing sorts naturally in ship order). Each file is the post-ship snapshot of the deliverable's root tracking doc:

- High-level goal + final status (`SHIPPED YYYY-MM-DD`)
- Step list with iteration counts (e.g. "✅ 02-service-identity-stack (3 audit rounds to clean)")
- Cross-cutting decisions made during PLAN
- Final kinds-of-misses log
- Final report — what shipped, what was learned, references to the rule additions this deliverable produced

## What does NOT live here

- **Per-step journals** — those stay where they are in `docs/wip/NNNN-<name>/` (gitignored, local-only — same 4-digit index as the committed snapshot, so finding the local workspace for a past committed snapshot is trivial when the journals still exist locally). The workflow does NOT auto-delete them; the user removes them manually whenever they want. Until then, they remain available as audit-trail evidence accessible from the local file system, just never crossing the commit boundary.
- **In-flight work** — that's also in `docs/wip/NNNN-<name>/`, gitignored.
- **Code / pattern docs** — code lives in `server/`; patterns live in `docs/PATTERNS.md` and the per-lib READMEs.

## Why we keep the snapshot README

Three reasons:

1. **Audit-trail of shipped scope.** Future-you can scan past deliverables and see what each one shipped, the iteration cost (audit rounds per step), and the kinds-of-misses log distilled from the round-by-round work.
2. **Origin trace for `rules.md` predicates.** When a predicate's value is ever questioned ("why is this rule even here?"), the deliverable that surfaced the original miss is documented and citeable. Deeper detail (the actual round where the miss happened) lives in the local journals during the deliverable's life and gets manually archived or deleted by the user later.
3. **Reviewability for PR / external readers.** The snapshot README is short enough to read end-to-end and conveys both what shipped and how rigorously it was vetted. The committed surface stays compact; the wider evidence stays local.

## Index

| File | Deliverable | Rules added |
|---|---|---|
| [0001-shared-libs-review.md](0001-shared-libs-review.md) | Shared-libs review | — |
| [0002-auth-inbound.md](0002-auth-inbound.md) | Auth inbound | — |
| [0003-harmless-endpoint-rename.md](0003-harmless-endpoint-rename.md) | HarmlessEndpoint rename | — |
| [0004-service-defaults.md](0004-service-defaults.md) | Service defaults | — |
| [0005-codegen-cleanup-and-dotnet-improvements.md](0005-codegen-cleanup-and-dotnet-improvements.md) | Codegen cleanup + .NET improvements | — |
| [0006-ts-bridge.md](0006-ts-bridge.md) | TS bridge | — |
| [0007-wire-parity.md](0007-wire-parity.md) | Wire parity | — |
| [0008-geo-data-pipeline.md](0008-geo-data-pipeline.md) | Geo data pipeline | — |
| [0009-geo-libs.md](0009-geo-libs.md) | Geo libs | — |
| _(0010 — markdown audit; local-only workspace, no committed snapshot)_ | — | — |
| [0011-doc-cleanup.md](0011-doc-cleanup.md) | Doc cleanup | — |
| [0012-tk-constants-in-tests.md](0012-tk-constants-in-tests.md) | TK constants in tests | — |
| [0013-auth-hardening.md](0013-auth-hardening.md) | Auth endpoint-declaration hardening | §5.1a (ThrowIfFalsey), §5.25a (no redundant `!`), §24.19 (uncommitted working-tree) |
| [0014-data-governance.md](0014-data-governance.md) | `DcsvIo.D2.DataGovernance` anonymization foundation | §3.15 (anonymization), §5.1a (cycle-only carve-out), §24.20 (tool-invisible lenses), §24.21 (full-solution gate-verify), §24.22 (source xmldoc scan) |
| [0015-contacts.md](0015-contacts.md) | `DcsvIo.D2.Contacts` PII value-object toolkit + EF mapping + Location / Coordinates EF + codegen field-constraints catalog | §1.25 (aggregate counts ≠ test executed), §1.26 (live-DB integration for EF-mapping), §9.35 (parallel write-path converter symmetry), §11.40 (doc code-example + registry completeness + integrated gate), §24.23 (Fixer no workspace-IDs in own additions) |
| [0016-keycustodian.md](0016-keycustodian.md) | `DcsvIo.D2.Private.Edge.KeyCustodian` lifecycle state machine + EF-as-DDD persistence convention + service-project structure standard (ADR-0016, 0017, 0020) | — |
| [0017-error-codes.md](0017-error-codes.md) | Unified spec-driven error-code + closed `ErrorCategory` system + wrapped-result wire model (gRPC `D2ResultProto` envelope + HTTP ProblemDetails codecs + resilience wrappers) across .NET ↔ TS | §11.30.1 (cross-producer field-emission parity), §24.24 (auditor scope = git-verified authored surface), §26.3.2 (cross-runtime API/diagnostic parity), §26.5.1 (byte-parity gate test per committed generated file), §26.6 (error codes spec-declared), §26.7 (emitters reference the TK constant not a path literal), §26.8 (generator-read spec field exercised-or-fail-loud) |
| [0018-typespec-decorators.md](0018-typespec-decorators.md) | `@dcsv-io/d2-typespec-decorators` — productionized TypeSpec decorator suite (`@d2*` vocabulary, program state map, compile-time validation) for the Operation Contract IDL | — |
| [0019-typespec-emitters.md](0019-typespec-emitters.md) | `@dcsv-io/d2-typespec-emitters` — D²-owned emitter fleet (C# transport + contract generators) reading `.tsp` contracts annotated with `@d2*` decorators; breadth-first, independently validated | — |
| [0020-resilience-ownership.md](0020-resilience-ownership.md) | Resilience ownership consolidation — BCL `AddStandardResilienceHandler()` removal, `DcsvIo.D2.Resilience` as sole mechanism, `@dcsv-io/d2-resilience` TS parity (ADR-0014 amendment) | — |
| [0021-auth-pivot.md](0021-auth-pivot.md) | Auth pivot — mint-once-forward + mTLS (docs) | §11.41 (doc-reconciliation sweep-beyond-focus), §11.42 (survivor-preservation in model-change reconciliation) |
| [0022-mtls-workload-identity.md](0022-mtls-workload-identity.md) | mTLS workload identity (dev-first) — KeyCustodian internal CA + SPIFFE-SAN peer validator + shared transport plumbing + Docker/Linux harness proof; forwarded-token shape | §5.8a (blank-line readability + SA1510 caveat), §1.30 (platform-dependent transport tests — honest skip + cross-platform matrix + deploy-target container), §24.27 (clean-state verification of environment-touching gate claims), §26.13 (shared cross-service registry regen verified by the shared / full-solution test run) |
| [0023-forwarded-token-auth.md](0023-forwarded-token-auth.md) | Forwarded-token auth plumbing (dev-first) — request-scoped raw-JWT capture + never-logged `ForwardedJwt` wrapper + per-request `CallCredentials` forward-attach + framework-free ambient-scope port + emitter auto-wire of outbound auth + `client_credentials` service-identity retirement | §9.39 (live credential captured only after all inbound gates, symmetric across transports — CLAUDE.md §5 lockstep line), §3.16 (live forwarded credential = redacting value type proven by a leak-surface matrix), §26.14 (security-critical cross-cutting auto-wired into generated DI, never a host-must-chain docstring), §1.31 (§1.3 sharpening — DI seam resolution-tested in its own extension's test), §9.40 (shared port lives in the lowest common layer both consumers reference) |
| [0024-contract-versioning.md](0024-contract-versioning.md) | Contract & API versioning — `@d2Field` / `@d2Reserved` author-pinned proto fields + reserved emission + `@typespec/versioning` channels + wire-identity derivation; always-on breaking-change gate (proto / JSON-diff / OpenAPI arms + force valve + deprecate-not-delete); Tolerant Reader tests; per-package semver + CHANGELOGs + footer-keyed release runner; artifact-diff versioning engine with the source-based portable fingerprint + git-ref apiDiff + drift-check CI lane | §7.14 (line-length limit governs CODE lines, not string-literal content — test descriptions / diagnostic-message templates exempt), §24.30 (a Fixer's own edits must not introduce an audit/finding ID or a stale VALIDATION test-count) |
| [0025-doc-reconciliation.md](0025-doc-reconciliation.md) | Doc reconciliation — tiered doc model, dissolve POST_PIVOT into PHASE_3 deferred checklist + V2, prune stale framing | — |
| [0026-kc-crypto-surface.md](0026-kc-crypto-surface.md) | KeyCustodian complete crypto + consumer surface (symmetric + sealed) on .NET and TypeScript | §1.33, §9.45–9.46, §13.1a, §24.31–24.36, §26.22–26.24 (+ §24.0h / §24.13.1 / §26.20 strengthenings) |
| [0027-gate-scoping-ci-coverage.md](0027-gate-scoping-ci-coverage.md) | Contract-gate test-tree scoping + whole-file deletion fix + CI coverage closure (PR #51) | — (no new rules at SHIP) |
| [0028-ts-caching.md](0028-ts-caching.md) | TS full caching twin (ADR-0008 / PHASE_3 T1) — `@dcsv-io/d2-caching-*` packages + docs parity | — (no new rules at SHIP; dual-runtime constants suite landed post-SHIP) |
| [0029-audit-token-discipline.md](0029-audit-token-discipline.md) | Audit token discipline — compact evidence, wave policy (Y/dirty/FR modes), fat-step planning | §24 compact + dirty-only one-law + Plan-Audit three-way + FR_COLLAPSED (amends existing §24; process.md primary) |
| [0030-edge-host.md](0030-edge-host.md) | Edge host + transport platform slice (Landing 1) — process-kind emitters, Edge/Audit multiproc, private-CA JWKS, mTLS-only product gRPC | §9.47 (issuer in-process JWKS), §10.23 (mTLS-only product gRPC binds), §10.24 (platform Unestablished gRPC deny) |
| [0031-advisory-locks-domain-keys.md](0031-advisory-locks-domain-keys.md) | Advisory locks / domain keys | � (if snapshot present; else local-only) |
| [0032-oss-public-private.md](0032-oss-public-private.md) | OSS public/private monorepo layout + dual suites + export + law | �7.7a dual-header; �8.8��8.10 dual suite/export/publish fence; �9.48��9.49 dep direction + IP placement; �11.46��11.47 dual-docs; �26.25��26.26 dual-root codegen |
