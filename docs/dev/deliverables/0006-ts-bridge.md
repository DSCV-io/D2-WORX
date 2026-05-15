# Deliverable 0006 — TS bridge

**Branch**: `n/ts-bridge` (from `nova` @ `6115584e`)
**Status**: ✅ READY FOR USER REVIEW (Final-review converged in 3 rounds; Completeness Checklist 147/147 YES; attestation signed; SHIP gate awaiting user authorization)
**Started**: 2026-05-14
**Type**: Cross-language bridge — first TS shared lib stack on v2; consumes specs migrated by 0005

## Context

Second of two pre-Phase-1 deliverables locked in by commit `0eeb8e81` (per `docs/v2/PHASE_0.md` Pre-Phase-1 Plan section). Establishes the TS shared-lib stack at `server/shared/typescript/` so the SvelteKit BFF + future Node frontends consume the same vocabulary as the .NET services.

**Inherits from 0005**: four spec catalogs in lockstep with .NET — `contracts/auth-error-codes/auth-error-codes.spec.json` (codegen-emitted constants + factories) + `contracts/telemetry/telemetry.spec.json` (closed-set tag enumerations). Plus the existing `contracts/{auth-scopes,auth-audiences,auth-context,request-context,mq-messages}/` catalogs.

**Architectural shape locked by V2.md §5.8**:
- BFF zero-privilege boundary (BFF→Edge only; internal token; propagated context envelope)
- 13 Tier-1 packages (drops `cache-redis` / `messaging-rabbitmq` / `cache-memory` / per-service backend gRPC clients / all v1 middleware packages / `@d2/handler` / `@d2/di` / `@d2/result-extensions` / all `@d2/repo-*-pg`)
- Sibling Node scripts at `tools/ts-codegen/` (NOT extending Roslyn)
- Vitest fixture-driven cross-language parity tests
- Paraglide for i18n (no TS-side TK constants — Paraglide IS that)

**Important constraint**: `server/web/` is broken-by-design and stays that way past 0006 — its `package.json` declares 16 `@d2/*` `workspace:*` deps that don't resolve, and `pnpm-workspace.yaml` deliberately omits `server/web` (per Step 1 §13.13 reconciliation #1) so workspace-level installs succeed. The BFF rewire that restores `server/web` is deferred to a future SvelteKit-focused deliverable per the scope reframe section below.

## Step plan

| # | Step | Status | Rounds | Prerequisites |
|---|---|---|---|---|
| 0 | Branch checkout (`n/ts-bridge` from `nova` @ `6115584e`) | ✅ | — | — |
| 1 | Workspace bootstrap + v1-leftover BFF cleanup | ✅ | 1 | step 0 |
| 2 | Foundation packages + codegen runner (11 of 13 Tier-1) | ✅ | 3 | step 1 |
| 3 | Spec-driven catalog migration (headers + JwtClaimTypes + HttpContextItems) | ✅ | 2 | step 2 |
| 4 | .NET codegen output committed (DX consistency with TS) | ✅ | 2 | step 3 |
| 5 | Edge boundary packages (final 2 of 13 Tier-1) | ✅ | 2 | step 4 |
| 6 | TS codegen completeness (IRequestContext extends + JwtPayload emission) | ✅ | 1 | step 5 |
| 7 | Parity test infrastructure (fixture-driven one-way; D2 scope) | ✅ | 4 | step 6 |
| 8 | Codegen dedup (Phase 1+2 shared infra via Compile Include) + 24-findings cleanup pre-Final-review | ✅ | 3 | step 7 |
| F | Final-review (deliverable-wide) | ⏸ | — | all above |

## Cumulative state (pre-Final-review)

All 8 substantive steps converged. Deliverable substantively complete; Final-review + SHIP gate remaining.

**Shipped surface**:
- 13 of 13 Tier-1 TS packages (`@d2/{result,utilities,resilience,i18n,logging,telemetry,service-defaults,protos,auth-context-abstractions,request-context-abstractions,auth-abstractions,headers,grpc-client}`)
- 4 cross-transport TS header catalogs (`@d2/headers-{common,http,amqp,grpc}`)
- 1 cross-language parity test package (`@d2/contract-tests` — private; 6 catalogs / 21 fixtures / 726 assertions)
- All 4 .NET catalog csprojs (`D2.Shared.Headers.{Common,Http,Amqp,Grpc}`)
- All 10 .NET source-gens deduplicated via `source-gen-shared/` shared dir + Compile Include (5 shared files: 2 polyfills + EmitDiagnostic + LoadResult + SpecFile)
- 3 NEW spec catalogs added by Step 3 NEW (`contracts/{headers,jwt-claims,in-process-keys}/`)
- All .NET SourceGen output now committed to `<csproj-dir>/Generated/` (DX consistency with TS `.g.ts` committed surface)
- Cross-spec consistency tests for header catalogs + JwtClaimTypes vs IAuthContext + HttpContextItems vs GrpcUserStateKeys
- `.gitattributes` `linguist-generated=true` for `*.g.cs` + `*.g.ts`
- ~19+ hand-mirrored type/catalog instances eliminated across both languages

**Cumulative test counts**: 3095+ .NET tests pass (up from 3024 baseline at branch base); 726 cross-language parity tests pass; per-package coverage thresholds (100/100/100/100 hand-written) preserved across all TS packages; all builds + jb inspectcode + eslint + prettier clean.

**9 rules.md augmentations LIVE** (4 from 0005 deliverable + 5 from 0006):
1. §14.1 Amendment / Round / Phase / Step / Wave / Sweep / Audit enumeration (post-Step 2)
2. §24.13.2 regex-as-TOOL meta-predicate (post-Step 2)
3. §13.13 clarifying note — distinction from §13.4 self-imposed scope deferral (post-Step 2)
4. §11.29 cross-doc dep parity for csproj/package.json edits (post-Step 2)
5. §11.30 spec-driven catalogs mandatory (post-Step 3 NEW)
6. §24.13.1 deletion-aware README sweep (post-Step 5)
7. §24.13.1 historical-narration tokens grep (post-Step 5)
8. §1.20 Phase C/D fail-path proof discipline for parity / contract / security test infra (post-Step 7)
9. §24.13.1 emit-targets enumeration in pre-flight greps (post-Step 7)
10. §24.13.3 Fixer sister-sweep scope = predicate's applicability scope, not original finding's narrow file location (post-Step 8)

**Process integrity**: every commit on this branch authorized per-occurrence; no destructive git ops; per-step audit loops converged cleanly (Steps 1-8 cumulative: 17 total audit rounds across 8 steps; 2-3 round average is appropriate for the deliverable's scale).

**Carry-forward to future deliverables** (NOT 0006's responsibility):
- BFF rewire (Sub-concern E originally in 0006; deferred per scope reframe section below) → future SvelteKit-focused deliverable
- Step 1 §13.13 reconciliation #1 (server/web pnpm-workspace.yaml re-add) → carries with the BFF rewire deliverable
- Paraglide-translation-pattern decision → BFF rewire deliverable
- Phase 2 codegen-dedup follow-up (Option β extraction of DiagnosticHelpers / SharedSpecFileLoader if scale demands) → deferred per Step 8 distillation
- ProblemDetailsExtensionKeys + PROBLEM_TYPE_URI_PREFIX + programmer-error code constants → flagged as §11.30 borderline candidates for follow-up deliverable (Step 6 NEW distillation + Step 8 self-audit)

## Scope reframe — BFF rewire DEFERRED (post-Step 6 NEW user decision)

The original 0006 plan included a Sub-concern E "BFF rewire" — replace 16 broken `workspace:*` deps in `server/web/`, install 5 server-side guards in SvelteKit hooks, build browser-side `authClient` for direct Edge calls, etc. **DROPPED from 0006 scope** per user reframe: the BFF rewire cannot be validated end-to-end without Edge existing (Phase 4 builds Edge), and decisions like the Paraglide translation pattern (which can't take runtime keys; v1 used a server-side translation middleware to map `userMessageKey` → Paraglide functions) need to be made in context of the actual SvelteKit-focused deliverable, not speculatively here.

**Carry-forward items** for the future BFF rewire deliverable:
- `server/web/` stays broken-by-design (16 `workspace:*` deps unmatched + missing 4 new headers catalogs)
- `pnpm-workspace.yaml` stays WITHOUT `server/web` (Step 1 §13.13 reconciliation #1 carryover task carries forward)
- 5 server-side guards from `@d2/headers` not yet wired into hooks.server.ts
- Browser-side `authClient` not yet built (`src/lib/client/auth/`)
- Paraglide-translation-pattern decision (v1 had a gateway-translation middleware; v2 needs to pick: replicate v1, pass-through-key + browser translates, or codegen-emit-switch-table from spec)
- Faro init verification at `src/lib/client/telemetry/faro.ts` post-Step-1-cleanup (pending)
- `@d2/grpc-client` wiring into hooks.server.ts for SSR loaders calling Edge
- gRPC channel teardown signal (`process.on('SIGTERM', closeChannel)` or SvelteKit hook)

**Sequence trigger**: future BFF rewire deliverable should be sequenced AFTER (a) Edge exists, (b) Paraglide-translation-pattern decision is made, (c) we're focused on SvelteKit DX.

## Step details

### Step 1 — Workspace bootstrap + v1-leftover BFF cleanup

**Sub-concerns**: A (workspace bootstrap) + F (BFF cleanup)

**Files created**:
- `pnpm-workspace.yaml` at repo root (points at `server/shared/typescript/*`, `server/web/`, `tools/ts-codegen/`, `server/shared/typescript/contract-tests/`)
- `package.json` at repo root — devDeps + workspace-wide tool pins + `packageManager: "pnpm@10.15.0"` + `pnpm.onlyBuiltDependencies: ["@bufbuild/buf"]`
- `server/shared/typescript/tsconfig.base.json` — extended by all 13 packages + contract-tests
- `tools/ts-codegen/` directory (placeholder; populated in Step 2)

**Files deleted** (v1-leftover from `server/web/`):
- `src/hooks.server.ts` (4 v1-leftover handles)
- `src/lib/server/{auth,form-actions,middleware,request-logger,geo-ref-data,logger}.server.ts` + co-located test files
- `src/lib/server/hooks/` (4 files + 4 tests)
- `src/lib/server/middleware.mock.server.ts`
- `src/lib/server/rest/gateway.server.ts`
- `src/routes/api/auth/[...path]/` + `src/routes/api/account/[...path]/` (proxy routes — V2.md §5.8 explicitly removes "All `+server.ts` endpoints, all form actions, all proxying")
- `src/paraglide/` outdir (canonical is `src/lib/paraglide/`)

**Files explicitly PRESERVED** (NOT in deletion list):
- `src/lib/client/telemetry/faro.ts` — Faro init (D3 decision: stays inline)
- `src/lib/paraglide/` — canonical Paraglide outdir
- `src/routes/` everything except the two proxy paths above

**Critical sequencing** (per PHASE_0.md `:640`): stop all Node containers → write workspace + tsconfig + delete files → restart. Symlink rotation on `pnpm install` mid-run breaks any running Node container.

### Step 2 — Foundation packages + codegen runner

**Sub-concerns**: B (partial — 11 of 13 packages) + C (codegen runner)

**Foundation packages** (8 of 13 — zero/low external dep):
| Package | Mirrors | Notes |
|---|---|---|
| `@d2/result` | `D2.Shared.Result` | D2Result port + Combine overloads |
| `@d2/utilities` | `D2.Shared.Utilities` | Falsey/Truthy + TryParseTruthyNull-style helpers |
| `@d2/resilience` | `D2.Shared.Resilience` | Polly-equivalent retry/breaker (thin wrapper acceptable) |
| `@d2/i18n` | `D2.Shared.I18n` | Paraglide consumer surface; reads `contracts/messages/{locale}.json` |
| `@d2/logging` | `D2.Shared.Logging` | Pino + ILogger interface mirroring .NET shape |
| `@d2/telemetry` | `D2.Shared.Telemetry` | OTLP setup helper |
| `@d2/service-defaults` | `D2.Shared.ServiceDefaults` | One-call bootstrap |
| `@d2/protos` | `D2.Shared.Protos` | Buf-generated proto types + gRPC stubs (own `pnpm generate` workflow) |

**Codegen-emitted abstractions** (3 of 13):
| Package | Emitted from | Mirrors |
|---|---|---|
| `@d2/auth-context-abstractions` | `contracts/auth-context/IAuthContext.spec.json` | `D2.Shared.AuthContext.Abstractions` |
| `@d2/request-context-abstractions` | `contracts/request-context/IRequestContext.spec.json` | `D2.Shared.RequestContext.Abstractions` (extends IAuthContext) — includes 1:1 `PropagatedContextSerializer` class with `Serialize`/`Deserialize` |
| `@d2/auth-abstractions` | `contracts/auth-scopes/scopes.spec.json` + `contracts/auth-error-codes/auth-error-codes.spec.json` | `D2.Shared.Auth.Abstractions` + `D2.Shared.Auth.Errors.{AuthErrorCodes,AuthFailures}` consolidated (matches .NET assembly placement) |

**Codegen runner** (`tools/ts-codegen/`):
- Per-topic `tsx` scripts using string builders (mirrors Roslyn emitter pattern):
  - `auth-context-emit.ts`
  - `request-context-emit.ts` (includes `PropagatedContextSerializer.g.ts`)
  - `auth-scopes-emit.ts`
  - `auth-error-codes-emit.ts`
  - `auth-failures-emit.ts`
- Each script reads spec.json, emits target `.g.ts` file with same diagnostic discipline as .NET SourceGens (validation + error reporting)
- Top-level `pnpm codegen` invokes all scripts; per-package `pnpm generate` invokes only its own
- `@d2/protos` codegen via Buf — separate from `tools/ts-codegen/` per #9 decision

### Step 5 — Edge boundary packages (final 2 of 13 Tier-1)

**Sub-concerns**: B (remainder)

| Package | Surface |
|---|---|
| `@d2/headers` | `X-D2-*` constants (mirrors `D2.Shared.Auth.Abstractions.RequestHeaders`) + 5 server-side guards: `requireAuth` / `requireOrg` / `requireRole` / `requireScope` / `redirectIfAuthenticated`. Strict-mode rejections (missing `X-D2-Trace-Id` etc.) return RFC 7807 ProblemDetails (matches Edge's response shape) |
| `@d2/grpc-client` | Singleton-per-process channel to Edge via `getChannel()` accessor (matches .NET `services.AddGrpcClient<T>()` pattern). Internal-token interceptor: KeyCustodian-issued JWT, audience `d2.edge`, 15-min TTL, module-singleton cache, refresh-on-401 |

**Test coverage required** (per §1.1): every public path on first pass — guards (happy + each rejection branch), channel accessor, interceptor (happy + 401-refresh + propagation).

### Step 6 — Parity tests + BFF rewire

**Sub-concerns**: D (parity infrastructure) + E (BFF rewire)

**Parity infrastructure**:
- `server/shared/typescript/contract-tests/` — `private: true` workspace package (D2 decision)
- Vitest harness with parity-fixture loader
- `dotnet test --filter Category=ContractFixtures` — emits fixture JSON consumed by Vitest
- JSON-RPC host child process: TS spawns .NET host; each side encodes a context envelope; round-trip asserts byte-for-byte / field-for-field equality
- CI gate: build fails on parity drift

**BFF rewire** (`server/web/`):
- `src/hooks.server.ts` — thin handle composing `@d2/headers`-based session reader (no proxying, no privileged calls)
- `src/lib/server/auth/` — install 5 server-side guards (re-exported from `@d2/headers`)
- `src/lib/client/auth/` — install browser-side `authClient` for direct Edge calls (auth state mutations bypass BFF entirely per V2.md §5.8)
- `src/lib/client/telemetry/faro.ts` — verify Faro init survives Step 1 cleanup; test against running Alloy
- `package.json` — replace 16 broken `workspace:*` deps with the 13 actually-shipped packages (drops `auth-bff-client`, `cache-memory`, `cache-redis`, `di`, `geo-client`, `handler`, `idempotency`, `interfaces`, `ratelimit`, `request-enrichment` per V2.md §5.8 drops list)

**Definition of Step 6 done**: `pnpm install` on `server/web/` succeeds; `pnpm exec svelte-check` zero errors; `pnpm exec vitest run` green; manual smoke against running Edge stack proves browser auth flow works.

**Step-1 carryover task** (per Step 1 §13.13 reconciliation #1): Step 6 Plan MUST include adding `server/web` back to `pnpm-workspace.yaml` globs after the 16 broken `workspace:*` deps in `server/web/package.json` are replaced with the 13 actually-shipped packages. Step 1 omitted `server/web` from workspace globs because pnpm 10.15 validates the FULL workspace dep graph regardless of `--filter` flags — keeping `server/web` in the workspace would have failed `pnpm install` for Steps 1-3. Re-adding is a one-line edit that the Step 6 Planner must explicitly schedule.

## Locked decisions (consolidated PLAN discussion)

| Decision | Final |
|---|---|
| Step packaging | 4 substantive steps (workspace + foundations + boundary + parity/rewire) — folds 6 PHASE_0 sub-concerns by dep graph |
| Branch | `n/ts-bridge` from `nova` @ `6115584e` |
| Internal token | KeyCustodian-issued JWT, audience `d2.edge`, 15-min TTL, BFF module-singleton cache, refresh-on-401 |
| `PropagatedContextSerializer` shape | 1:1 type-named class with `Serialize`/`Deserialize` methods |
| Codegen runner | Per-topic `tsx` scripts at `tools/ts-codegen/`; `@d2/protos` separate Buf workflow |
| `@d2/grpc-client` channel model | Singleton-per-process via `getChannel()` accessor (matches .NET) |
| `@d2/headers` strict-mode failure | RFC 7807 ProblemDetails (matches Edge) |
| Toolchain pinning | v1 versions verbatim (table below); `packageManager: "pnpm@10.15.0"` for Corepack auto-pin; pnpm `onlyBuiltDependencies: ["@bufbuild/buf"]` |
| `tsconfig.base.json` location | `server/shared/typescript/tsconfig.base.json`; `server/web/` does NOT extend it (keeps SvelteKit's `.svelte-kit/tsconfig.json` extends-chain clean) |
| Publish posture | 100% internal `private: true`; broader OSS discussion deferred — revisit per-package if/when individual libs graduate |
| `@d2/protos` codegen | Own `pnpm generate` script via Buf (separate from `tools/ts-codegen/`) |
| AuthErrorCodes/AuthFailures organization | Both emitted into `@d2/auth-abstractions` (matches .NET `D2.Shared.Auth.Errors` placement) |
| `@d2/contract-tests` packaging | `private: true` workspace package (NOT exported); not part of 13 Tier-1 count |
| `@d2/faro-browser` packaging | DEFERRED — stays inline in `server/web/src/lib/client/telemetry/`; revisit if 2nd Node frontend appears |
| LGTM coupling | None — .NET emits OTLP to Alloy gateway; Faro emits OTLP-over-HTTP; vendor-swap-able |
| Cross-language parity testing | Vitest fixture-driven; `dotnet test --filter Category=ContractFixtures` emits JSON; JSON-RPC child process for round-trip |
| i18n | Paraglide 2.x — no TS-side TK constants; both .NET TK SourceGen + Paraglide consume `contracts/messages/{locale}.json` |
| Drift resolution policy | v1's two version drifts (`@opentelemetry/sdk-metrics`, `better-auth`/`hono`/`import-in-the-middle`) resolved to higher version — affects only OTel SDK pin since auth/hono not in 0006 scope |

## Pinned versions (lifted from v1 — supply chain hardened)

| Tool | Pin |
|---|---|
| Node engines | `>=24.0.0` |
| pnpm engines + `packageManager` field | `pnpm@10.15.0` (Corepack auto-pin) |
| TypeScript | `5.9.3` |
| Vitest | `4.0.18` |
| `@vitest/coverage-v8` | `4.0.18` |
| ESLint | `10.0.2` |
| `@eslint/js` | `10.0.1` |
| `typescript-eslint` (meta) | `8.56.1` |
| `eslint-config-prettier` | `10.1.8` |
| `globals` | `16.4.0` |
| Prettier | `3.8.1` |
| `@bufbuild/buf` | `1.65.0` |
| `@bufbuild/protobuf` | `2.11.0` |
| `ts-proto` | `2.11.2` |
| `@grpc/grpc-js` | `1.14.3` |
| `pino` | `10.3.1` |
| `pino-pretty` (devDep) | `13.1.3` |
| OTel `api` | `1.9.0` |
| OTel SDK packages | `0.212.0` (logs / sdk-node / exporters / instrumentation) |
| OTel SDK packages | `2.5.1` (core / metrics / resources — drift resolved to higher) |
| OTel `semantic-conventions` | `1.39.0` |
| OTel `auto-instrumentations-node` | `0.70.1` |

`pnpm.onlyBuiltDependencies` allowlist for 0006 root: `["@bufbuild/buf"]` (pnpm 10's safe-by-default install requires explicit opt-in for post-install scripts).
`server/web/` will need its own allowlist later: `["@bufbuild/buf", "@tailwindcss/oxide", "esbuild", "sharp"]`.

## Risks

1. **`server/web/` is broken-by-design until Step 4 lands.** The 16 `workspace:*` deps in `server/web/package.json` won't resolve until the 13 packages exist + Step 4 rewires. Step 1 should NOT attempt `pnpm install` on `server/web/` — use `--filter !server/web` for any global install. **Mitigation**: Step 4's Plan includes explicit gate-check that `pnpm install` succeeds for `server/web/` before declaring done.

2. **Cross-language parity test infrastructure is novel.** No prior deliverable has built a JSON-RPC child-process bridge between Vitest and .NET test fixtures. Risk: integration friction (process spawn, stdin/stdout protocol, env var propagation, Windows path quoting, ASCII vs UTF-8 line buffering). **Mitigation**: Step 4's Plan stands up the harness with a SINGLE trivial round-trip first; only after that proves itself does the harness get more parity assertions.

3. **Faro inline file might rot during Step 1 cleanup.** `src/lib/client/telemetry/faro.ts` is NOT in the deletion list, but the broad `lib/server/` and `routes/api/` deletes could miss-target. **Mitigation**: Step 1 Plan includes explicit "files NOT deleted" preservation list; Step 1 Auditor verifies the file is still present + still references `PUBLIC_FARO_COLLECTOR_URL`.

4. **pnpm 10's `onlyBuiltDependencies` requirement is easy to miss.** `@bufbuild/buf` install will silently skip its post-install script without the allowlist, leaving `buf` not on PATH. **Mitigation**: Step 1 Plan explicitly checks `pnpm exec buf --version` after install — fail fast if `buf` not installed.

5. **Codegen drift between .NET SourceGens and TS sibling-script emitters is structurally possible.** Both consume the same spec but emit independently. If the two emitters interpret a spec field differently, parity breaks silently until Step 4's parity tests run. **Mitigation**: Step 2 Plan includes a Plan-time review of each emitter's spec-interpretation against the .NET SourceGen's emitter logic, file-by-file. Cross-spec consistency tests (mirroring `AuthErrorCodesVsTelemetrySpecConsistencyTests`) catch the rest.

6. **JSON-RPC host process cancellation discipline.** Vitest test cleanup must reliably kill the .NET child process — orphan processes accumulate over CI runs. **Mitigation**: Step 4 Plan includes explicit `afterAll` hook + signal handler verification.

## Process integrity

This deliverable executes under the canonical orchestrator-only main-thread workflow per CLAUDE.md MANDATORY block 0:

- Every planning, implementation, audit, and fix round = NEW fresh sub-agent
- Per-step audit loop with 10-iteration ceiling; 3-artifact journal model (latest big table REPLACED each sweep + append-only findings log + append-only fix log)
- Final-review walks deliverable-wide
- All commits require explicit per-occurrence user permission
- Wip workspace gitignored; orchestrator updates this README's tracking sections only
- All LIVE rules.md predicates from deliverables 0001-0005 binding (esp the new §24.13.1 + §7.15 conjugation regex from 0005, §1.19 / §5.25 / §11.28 / §13.13 / §14.1 / §11.9 / §11.21 / §24.13)

## Kinds-of-misses log

(Populated by per-step distillations after each step's audit converges.)

## Proposed `rules.md` additions

(Populated by per-step distillations + final-review distillation; presented to user at SHIP gate.)

## Mid-execution rules.md augmentations applied

Applied per user authorization ("feel free to dispatch someone to make those rules updates now"); 4 candidates from Step 2 per-step distillation (journal `02-foundation-packages-and-codegen-runner/journal.md:1198-1258`).

| # | Augmentation | rules.md location | Lines changed |
|---|---|---|---|
| 1 | §14.1 + §24.13.1 enumerate `Amendment [A-Z0-9]` / `Plan Amendment` / `Round N` / `R N findings` as forbidden tokens (with conjugations + hyphenated forms) | §14.1 inline forbidden-tokens list + Evidence regex (rules.md ~1291); §24.13.1 canonical pre-flight checklist §14.1 entry (rules.md ~1854) | 3 line replacements |
| 2 | NEW §24.13.2 meta-predicate "regex is a TOOL, not a SOURCE OF TRUTH" — Pre-flight Evidence greps and §24.13.1 canonical checklist are MECHANICAL AIDS to the predicate walk; manual reading of the modified source is required to verify the SPIRIT of each predicate; "grep returned zero" is necessary but NOT sufficient evidence of compliance | §24 after §24.13.1 (rules.md ~1855-1858) | 4 new lines (predicate body) |
| 3 | §13.13 clarifying note (§13.4 / §13.5 vs §13.13 distinction) — added Implementer-side reminder paragraph + legitimate-use examples + misuse examples; clarifies that §13.13 applies when REALITY (runtime / framework / library) diverges from Plan, NOT when Implementer-self-imposed scope limits diverge from Plan | §13.13 existing predicate (rules.md ~1276-1278) | 3 new bullet sub-items appended |
| 4 | NEW §11.29 cross-doc dep parity for csproj/package.json — When any project file's dependency set changes (`<ProjectReference>` / `<PackageReference>` in `.csproj`; `dependencies` / `devDependencies` / `workspace:*` in `package.json`), the corresponding parent overview README's Mermaid dep-graph + descriptive cross-subgraph dep list MUST be updated in the SAME change. Generalizes existing .NET §9.8 + §11.6 to cover NuGet edits + the TS-workspace analog. | §11 Documentation Parity, after §11.28 (rules.md ~1177-1181) | 5 new lines (predicate body) |

**Pre-flight Evidence greps against the diff** (`/tmp/added-lines.txt` = 16 substantive lines / 11.6 KB extracted via `git diff HEAD docs/dev/rules.md | awk '/^\+[^+]/ {sub(/^\+/,""); print}'`):

| Grep | Result |
|---|---|
| §7.15 American English (canonical regex) | One hit: line 15 of diff (§24.13.2 Why prose) — `cancelled`/`organising` appear inside backticks as DEFINITIONAL EXAMPLES of British forms the regex misses. Same self-reference pattern §7.15 itself uses. Legitimate. |
| §11.9 (CLAUDE.md / PHASE_*.md / V2.md citations) | Zero hits |
| §11.28 (forward-framing) | One hit: line 10 of diff (§14.1 allowlist text) — `"future X lib" / "will live in"` in QUOTES as definition examples; identical self-reference to original §14.1 text I edited. Legitimate. |
| §14.1 (phase / wave / sweep / round / step / amendment / audit verbiage) | Five hits: lines 9, 11, 12, 14, 15 of diff — all are §14.1 / §24.13.1 / §24.13.2 predicate text quoting forbidden tokens inside backticks/quotes to DEFINE them (e.g. `Round 1`, `Plan Amendment B`, `R1 findings`). Same self-reference pattern existing §14.1 uses. Legitimate. |
| §14.2 (TODO/FIXME/HACK) | Zero hits |
| §7.14 line length ≤ 100 | Markdown predicates per §11.18 are NOT subject to §7.14's 100-char limit; long prose lines per bullet match every other predicate body in rules.md. Legitimate. |

**rules.md size**: 2070 lines before → 2084 lines after (+14 net lines, accounting for line-substitutions on the §14.1 forbidden-tokens list / Evidence regex / §24.13.1 entry plus the new §11.29 + §13.13 sub-bullets + §24.13.2 predicate body; per §11.21 the ≤300-line ceiling is per-doc-README, not the central rules.md catalog — rules.md size is unconstrained by §11.21).

**Verified**: rules.md still well-formed (Markdown structure intact, predicate numbering consistent, table-of-contents entries unaffected since augmentations are sub-numbered under existing top-level sections §11 / §13 / §14 / §24). No existing predicate references broken (cross-refs to §9.8 / §11.6 / §13.4 / §13.5 / §14.1 / §24.13.1 all still resolve to valid sections).

Status: complete; ready for user review at SHIP-gate aggregation OR earlier if user wants to spot-check now.

## Mid-execution rules.md augmentations applied (Step 3 NEW + Step 4 NEW window)

Applied per user authorization ("let's add those rules"); 2 candidates from Step 3 NEW per-step distillation (corroborated by Step 4 NEW R1-1):

| # | Augmentation | rules.md location | Lines changed |
|---|---|---|---|
| 1 | §24.13.1 augmentation: deletion-aware README sweep | §24.13.1 canonical pre-flight grep checklist (rules.md line 1858, between §11.9 and §11.28 entries) | 1 new line (predicate body) |
| 2 | §24.13.1 augmentation: historical-narration tokens grep | §24.13.1 canonical pre-flight grep checklist (rules.md line 1860, after §11.28 entry) | 1 new line (predicate body) |

Pre-flight Evidence greps against the diff (only the 2 newly-added lines isolated from the staged §11.30 augmentation already present): zero hits each — §7.15 American English (canonical regex) zero hits; §11.9 (CLAUDE.md / PHASE_*.md / V2.md citations) zero hits; §11.28 (forward-framing forbidden tokens) zero hits; §14.1 (phase / wave / sweep / round / step / amendment / audit verbiage) zero hits; §14.2 (TODO / FIXME / HACK) zero hits. §7.14 line length: 934 / 734 chars — markdown carve-out per §11.18 applies (every existing §24.13.1 checklist entry is similarly long prose; the 100-char limit covers C# / TS source, not markdown predicates).

**rules.md size**: 2097 lines before → 2099 lines after (+2 net lines; the 2 augmentations are sibling entries in the existing §24.13.1 canonical checklist list, not new predicate bodies).

**Verified**: rules.md still well-formed (Markdown structure intact, predicate numbering consistent, table-of-contents entries unaffected since augmentations are sub-entries under existing §24.13.1). No existing predicate references broken (the §11.3 / §11.5 / §11.9 / §11.10 / §11.19 / §11.20 / §11.28 / §24.13.2 cross-refs in the new entries all resolve to valid sections).

Status: complete; ready for orchestrator commit + user review at SHIP-gate aggregation OR earlier if user wants to spot-check now.

## Mid-execution rules.md augmentations applied (post-Step 7 window)

Applied per user authorization ("let's add those 2 rules"); 2 candidates from Step 7 per-step distillation:

| # | Augmentation | rules.md location | Lines changed |
|---|---|---|---|
| 1 | NEW §1.20 — Phase C/D fail-path proof discipline for test infra | §1 Test Discipline section (rules.md lines 168-172, after §1.19) | 6 new lines (1 predicate + Why/How/When/Evidence sub-bullets + spacer) |
| 2 | §24.13.1 augmentation — pre-flight greps must enumerate EMIT TARGETS | §24.13.1 canonical pre-flight grep checklist (rules.md line 1871, after §14.2 entry) | 1 new line (predicate body) |

Pre-flight Evidence greps against the diff: §7.15 American English (canonical regex) zero hits; §11.9 (CLAUDE.md / PHASE_*.md / V2.md citations) zero hits; §11.28 (forward-framing forbidden tokens) zero hits; §14.1 strict (`Phase [A-E]` / `Phase [0-9]` / `Step [0-9]` / `Round [0-9]` / `R[0-9]+`) zero hits; §14.2 (TODO / FIXME / HACK) zero hits. §7.14 line length: predicate prose lines exceed 100 chars (matches the existing §1.18 / §1.19 / §24.13.1 markdown convention; 100-char limit covers C# / TS source, not markdown predicate bullets).

**rules.md size**: 2099 lines before → 2106 lines after (+7 net lines).

**Verified**: rules.md still well-formed (Markdown structure intact, §1.x predicate sequence continuous through §1.20, §24.13.1 canonical-checklist list extended cleanly, table-of-contents entries unaffected since augmentations are extensions of existing top-level sections). No existing predicate cross-references broken.

## Mid-execution rules.md augmentations applied (mid-Step-8 window)

Applied per user authorization ("update the rules if need be, additional unneccessary sweeps are slow and expensive"); 1 candidate from Step 8 R1+R2 multi-round closure pattern + Step 3 R1-2 historical pattern:

| # | Augmentation | rules.md location | Lines changed |
|---|---|---|---|
| 1 | NEW §24.13.3 — Fixer sister-sweep scope MUST match predicate's applicability scope, not original finding's narrow file location | §24 Audit Evidence Discipline (rules.md lines 1880-1888, immediately after §24.13.2) | 9 new lines (1 predicate + Evidence + applicability-scope examples list + Why + How sub-bullets + spacer) |

Pre-flight Evidence greps against the diff: §7.15 American English (canonical regex) zero hits; §11.9 (CLAUDE.md / PHASE_*.md / V2.md citations) zero hits; §11.28 historical-narration tokens (no longer / previously / moved to / renamed to / formerly / used to / deprecated in favor of / now lives in) zero hits; §14.1 strict (`Phase [0-9]` / `Wave [0-9]` / `Sweep [0-9]` / `Round [0-9]` / `R[0-9]+ findings` / `Step [0-9]` / `Plan Amendment` / `Amendment [A-Z0-9]` / `audit pass` / `audit decision` / `audit row` / `gap closure` / `pre-fix` / `post-fix` / `previously lacked` / `Plan's Risk #N`) zero hits in predicate body; §14.2 (TODO / FIXME / HACK) zero hits. §7.14 line length: predicate prose lines exceed 100 chars (matches the existing §24.13.1 / §24.13.2 markdown convention; 100-char limit covers C# / TS source, not markdown predicate bullets). The "Why" example cites `deliverable 0006` for empirical justification per the established §1.18 / §1.19 / §24.13.1 convention (deliverable NUMBER is allowed; Step / Round numbers are not).

**rules.md size**: 2106 lines before → 2119 lines after (+13 net lines).

**Verified**: rules.md still well-formed (Markdown structure intact, §24.13.x predicate sequence continuous through §24.13.3, table-of-contents entries unaffected since augmentation is an extension of existing §24.13.x family). No existing predicate cross-references broken.

## Mid-execution rules.md augmentations applied (Final-review R1 window)

Applied during Final-review Round 1 Fixer pass (pending orchestrator confirmation); 1 candidate from Final-review Auditor R1 finding §24.13.1 / §11.9 (`PropagatedContextSerializer.g.cs:68` rules.md citation):

| # | Augmentation | rules.md location | Lines changed |
|---|---|---|---|
| 1 | §24.13.1 augmentation #9 (NEW EMIT SURFACES) extension — pre-flight Evidence greps against generated emit output MUST cover the full text-based predicate set (§7.14 / §7.15 / §11.9 / §11.28 / §14.1 / §14.2), not just prettier-format. Adds explicit `grep -rEn 'rules\.md|CLAUDE\.md|PHASE_[0-9_]+\.md|V2\.md' server/shared/dotnet/**/Generated/` example + Auditor independent-verify expectation against `git ls-files '*.g.cs' '*.g.ts'` | §24.13.1 NEW EMIT SURFACES bullet (rules.md line 1871) | 1 line replacement (predicate body extended in-place) |

The augmentation closes the empirical gap surfaced by Final-review R1: emit-targets enumeration (added post-Step-7) covered prettier-format checks but not cross-doc citation greps against generated `.g.cs` content. The §11.9 violation in `PropagatedContextSerializer.g.cs:68` (emitted from `PropagatedEmitter.cs:270`) slipped through every per-step audit + the pre-Final-review docs accuracy sweep specifically because the rules.md / CLAUDE.md citation grep was applied to source files but never to committed generated files.

**rules.md size**: 2119 lines before → 2119 lines after (0 net lines — predicate body extended in-place, no new bullet).

**Verified**: rules.md still well-formed (predicate body unchanged in structure; only enumeration extended). No existing predicate cross-references broken.

## Final-review convergence summary

Final-review walked the deliverable-wide rules.md sweep across `6115584e..HEAD` (6 commits, 543 files) plus uncommitted Fixer R1+R2 changes. Converged in 3 rounds:

| Round | Findings | Severity breakdown | Closure |
|---|---|---|---|
| R1 | 7 | 1 MEDIUM (§5.25/§11.9 rules.md citation in `.g.cs`) + 6 LOW (§7.14 line length, §11.28 historical-narration in 2 READMEs, §11.21 line count in 2 READMEs, §24.13.1 META augmentation gap) | All 7 closed in R2 sweep |
| R2 | 2 NEW + 1 sister-swept | All LOW META (§14.1 inherited Phase/V2.md tokens in `.github/workflows/test.yml`, §14.2 literal TODO in `contract-tests/README.md` + `docs/PARITY.md`) | All 3 closed in R3 sweep |
| R3 | 0 | — | CLEAN — convergence reached |

Process integrity: §24.13.3 sister-sweep discipline (just-landed mid-Step-8) honored rigorously by both Fixers — R1 surfaced 16 additional §7.14 sites beyond Auditor citation; R2 surfaced 1 additional §14.2 site (`docs/PARITY.md:131`) Auditor R2 missed. Both validate the discipline's empirical value.

Final-review journal: [`docs/wip/0006-ts-bridge/final-review/journal.md`](final-review/journal.md) (1512 lines, 3-artifact model intact: Plan + 3 sweep big tables + append-only findings log + append-only fix log).

Completeness Checklist: [`docs/wip/0006-ts-bridge/final-review/completeness-checklist.md`](final-review/completeness-checklist.md) — 147/147 boxes YES with file:line citations (120 per-step + 5 Final-review + 13 cross-cutting docs + 5 process integrity + 4 build/test/inspect).

## Final attestation

> "I attest that this deliverable's process integrity has been verified against the deliverable completeness checklist in `rules.md` (Deliverable completeness checklist section). Every box is YES. The deliverable is ready for user REVIEW."

Spot-check links:

- Per-step journals (3-artifact model + clean big tables + distillations):
  - Step 1: [`01-workspace-bootstrap-and-bff-cleanup/journal.md`](01-workspace-bootstrap-and-bff-cleanup/journal.md)
  - Step 2: [`02-foundation-packages-and-codegen-runner/journal.md`](02-foundation-packages-and-codegen-runner/journal.md)
  - Step 3 NEW: [`03-spec-driven-catalog-migration/journal.md`](03-spec-driven-catalog-migration/journal.md)
  - Step 4 NEW: [`04-dotnet-codegen-output-committed/journal.md`](04-dotnet-codegen-output-committed/journal.md)
  - Step 5: [`05-edge-boundary-packages/journal.md`](05-edge-boundary-packages/journal.md)
  - Step 6 NEW: [`06-ts-codegen-completeness/journal.md`](06-ts-codegen-completeness/journal.md)
  - Step 7: [`07-parity-test-infrastructure/journal.md`](07-parity-test-infrastructure/journal.md)
  - Step 8 NEW: [`08-codegen-dedup-and-pre-final-cleanup/journal.md`](08-codegen-dedup-and-pre-final-cleanup/journal.md)
- Final-review journal: [`final-review/journal.md`](final-review/journal.md)
- Completeness Checklist: [`final-review/completeness-checklist.md`](final-review/completeness-checklist.md)

10 mid-execution rules.md augmentations applied per user authorization (enumerated in the augmentations log sections above). Carry-forward items enumerated in the Cumulative state + Scope reframe sections.

**Awaiting user REVIEW + SHIP authorization.** SHIP gate steps (per workflow.md §SHIP): commit Final-review fixes + apply approved candidate rules.md additions if any pending + present deliverable root README to user + copy snapshot to `docs/dev/deliverables/0006-ts-bridge.md` + squash-merge `n/ts-bridge` → `nova` + update PHASE_0.md.
