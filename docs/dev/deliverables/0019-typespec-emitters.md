# Deliverable 0019 — `@dcsv-io/d2-typespec-emitters` (the C# code-generator suite)

## Goal

Build the full breadth of code generators ("emitters") that read one `.tsp` operation contract — annotated with the shipped `@d2*` vocabulary (0018) — and emit the C# transport + contract artifacts, so an engineer hand-writes only the `.tsp` and the handler body. **Breadth-first AND independently validated**: build every generator now and prove each one with integration tests against its real *seams* — real D2 shared libs where they exist (`RedactDataDestructuringPolicy`, `DcsvIo.D2.Resilience`, the idempotency store, auth/scope middleware, Grpc.Tools), and faithful test doubles for collaborators not built yet (handler bodies, the SSE channel gateway, the Auth leaf caller). **No generator is left unvalidated waiting on a future consumer** — the generated code's contract ("given these seams, behave correctly") is fully testable today. A per-emitter ledger records *what each emitter is validated against* (real lib vs. which test double for which unbuilt collaborator), for transparency.

Prove the generators by:

1. generating real KeyCustodian `GetJwks` code that matches the live handler,
2. a sample **sign-shaped fixture op** + a **fixture push op** that together exercise every generator path (scopes, in-process leaf, gRPC binding, redaction, idempotency, resilience, server-push), and
3. per-emitter integration tests against real seams — most emitters validated standalone; the REST-route and gRPC emitters via a minimal in-memory `TestServer` harness wiring the generated transport + real middleware + fake handlers.

Also: add `@d2InProcess` (a 13th decorator) to the shipped `@dcsv-io/d2-typespec-decorators` package.

**Out of band**: generators-only. No production Edge host (deliverable A1). No KC `Sign` handler authoring. Generated route/gRPC/SSE transport is compiled + exercised only inside the integration-test harness.

## Context

- 0018 (`@dcsv-io/d2-typespec-decorators`, shipped) = the `@d2*` vocabulary you annotate a `.tsp` with. **0019 = the generators that read those annotations and produce code.** 0019 depends on 0018.
- Emit mechanism is proven by the A/C/B/SC1 spikes: `$onEmit` + `navigateProgram` + `program.stateMap(KEY).get(op)` + string templates + `emitFile`. The spikes used stale stand-ins (`Outcome<T>`, `IAsyncResiliencePolicy`, `@d2Scope`); 0019 ports the **mechanism**, mapping to real shapes (`D2Result`, `DcsvIo.D2.Resilience`, the shipped scope decorators). Spikes B + C already validated their emitters with ~9 tests each against injected seams (no host) — that's the precedent: validate against seams, not consumers.
- KC is a module-within-host with no `api/`; its transport normally lives in the Edge host. That host doesn't exist yet — but it's a *consumer*, not a seam, so its absence does not block validation.

## Cross-cutting decisions

| # | Decision | Source |
|---|---|---|
| D1 | Scope = generators-only, **breadth-first AND each independently validated**, no production Edge host (A1 owns that). | user |
| D2 | Leaf-vs-gRPC trigger = an explicit **`@d2InProcess`** decorator (13th), added to `@dcsv-io/d2-typespec-decorators`. | user |
| D3 | Package home = `server/shared/typescript/typespec-emitters/` (pnpm workspace member; imports `@dcsv-io/d2-typespec-decorators`). | user |
| D4 | Branch = `n/typespec-emitters` cut from `n/contract-idl-adr` (no nova merge now). | user |
| D5 | Validation = integration tests against real **seams**. Most emitters validated standalone (instantiate the generated artifact + drive it against its seams). The REST-route + gRPC emitters use a minimal ASP.NET `TestServer` / in-memory gRPC harness wiring the generated transport + **real** D2 middleware where it exists + fake handlers. Self-managed test infra (allowed — no `dotnet run`). | user |
| D6 | C#-first. TS limited to a thin REST-client DTO emitter (BFF consumes REST). No full typed-client surface in 0019. | user |
| D7 | **SSE / server-push emitter IS in scope**, validated against a faithful `ISseEmitSink` **test double** (the seam contract). The real Edge channel gateway is a future *consumer*, out of scope — its absence doesn't block validation. | user |
| D8 | Emit mechanism = `$onEmit` + `navigateProgram` + `stateMap` + string templates + `emitFile`. No AssetEmitter / `@typespec/protobuf`. | established (spikes) |
| D9 | Output discipline = committed `.g.cs` / `.g.ts` / `.proto` with the standard auto-generated banner + byte-parity gate tests + rules.md §26 compliance. | convention |
| D10 | Real C# target shapes: `D2Result<TOutput?>` semantic factories; `BaseHandler` vs `BaseRepoHandler`; C# 14 `extension(IServiceCollection)` DI; `[RedactData(Reason=…)]`; proto `csharp_namespace = D2.Services.Protos.<Domain>.V1`; `ResilientPipeline<TKey,TValue>` + keyed DI for resilience; idempotency store seam. | research |
| D11 | Pilot ops = real `GetJwks` (validate generated DTOs vs the live handler) + a sign-shaped **fixture** leaf op + a **fixture** push op. No authoring of KC's real Sign handler. | user / derived |
| D12 | **Every emitter has a passing integration test.** The validation ledger (`server/shared/typescript/typespec-emitters/VALIDATION.md`, committed; mirrored in this README's living state) records, per emitter, the **seams it is validated against** — real shared lib vs. a documented test double (which collaborator it stands in for + when the real impl replaces it). Transparency record, not an unvalidated-excuse list. SHIP requires every emitter validated + ledgered. | user |
| D13 | Done = audit-clean **and** independently integration-tested against its seams. Binding to the eventual real *consumer* (Edge host wiring, Auth caller) is a separate future step that does NOT gate 0019 and does NOT change the generated code's proven correctness. Per-step auditor briefs carry this: do **not** flag "uses a faithful test-double seam for an unbuilt collaborator" as a finding when the seam contract is genuinely exercised; **do** flag a hollow/vacuous double. | orchestrator (from user intent) |
| D14 | **Façade interface signature is transport-neutral** — `ValueTask<D2Result<<Op>Output?>> <Op>Async(<Op>Input input, CancellationToken ct = default)`, NO `HandlerOptions?`. Required so the SAME `I<Module>InternalApi` backs both the in-process impl and a future gRPC-client impl (location transparency); `HandlerOptions` is a non-wire server concern. ⇒ a module's `Clients` project references `DcsvIo.D2.Result` + `DcsvIo.D2.Utilities` (the latter forced by the service-tree Tier-1 global-using injection in `server/services/Directory.Build.targets`; both are shared libs, ADR-0020-legal) — never handler-abstractions / Domain / App / Infra. | orchestrator (Step 6; D-b amended in 6a) |
| D15 | **An exposed op's DTOs (input + output) live ONLY in the module's `Clients` project** (single copy); the app-layer `I<Op>Handler` + handler body reference them via the `App → Clients` project ref. Makes the façade's straight-through delegation compile (no app↔Clients DTO map). Internal-only (`@d2Internal`) ops keep their DTOs in `app/`. | orchestrator (Step 6) |
| D16 | **The generated façade impl is registered TRANSIENT** (matches the handler lifetime; a Singleton façade would capture the handler's scoped `DbContext` — captive dependency). Registration is a NEW generated `.g.cs` DI extension; the hand-written app DI extension is never edited. | orchestrator (Step 6) |
| D17 | **A generated transport adapter (REST route / gRPC endpoint) delegates THROUGH the module façade when the op is `@d2InProcess`, else directly to `I<Op>Handler`.** One `delegationTarget` per op feeds both the route emitter and the gRPC service-impl emitter. REST needs no generated wire↔DTO mapper (ASP.NET Minimal API binds the DTO directly — the DTO IS the REST wire contract); gRPC uses its proto↔DTO `<Op>TransportMappers.g.cs`. | orchestrator (Step 7) |
| D18 | **Generated client surface taxonomy = one surface per (module × transport)** — each contains EXACTLY that transport's ops; NOT a unified per-module interface (op-sets diverge by transport — KC `getJwks` is `@d2InProcess`-only, `sign` is `@d2GrpcMethod`, so a gRPC client can't implement the full in-process façade; **supersedes** the DEVELOPER_VIEW unified-`I<Module>InternalApi`/WhoIs model). Surfaces: in-process `I<Module>Api`/`<Module>Api` (C#); C# remote gRPC `I<Module>GrpcClient`/`<Module>GrpcClient`; TS server gRPC `<module>GrpcClient`; TS browser REST `<module>RestClient`. Names key off the PERMANENT `@d2ServedBy` module (never the fixture/proto gRPC-service name → stable across fixture→real). gRPC granularity = per-module. **Server=gRPC, browser=REST; no server-REST client** (would double-hop through Edge). Per-call resilience override on the gRPC client interface ONLY (façade stays transport-neutral). Location transparency = opt-in shared per-op interface only when a real consumer needs it (not default). Full form + token-flow rationale → `DEVELOPER_VIEW.md` "Client surface taxonomy — FINAL". SHIP-time → PATTERNS.md + ADR-0021 (+ reconcile the `IKeyCustodianClient` extraction-interface references in `docs/v2/PHASE_3.md` / `PHASE_0_AUTH.md` to the D18 names — they predate this taxonomy). | user (2026-06-17) |
| D19 | **In-process façade renamed `I<Module>InternalApi` → `I<Module>Api`** (impl `<Module>InternalApi` → `<Module>Api`). "Internal" was verbose at every call site AND collided with the `@d2Internal` op-marker's opposite meaning (module-internal = NOT exposed). Touches `facade-emitter.ts` + the committed Step-6 KC façade `.g.cs` + the hand-written composition root + references; folded into 9b-i task 1. | user (2026-06-17) |

## Emitters — in scope vs deferred

**All in scope (each independently validated against its seams):**

1. **C# DTO emitter** — `<Op>Input` / `<Op>Output` records, nullability, `[RedactData]` from `@d2Redact`. *Seams:* real `RedactDataDestructuringPolicy`; round-trip; real `GetJwks` shape.
2. **proto emitter + gRPC service-impl emitter** — `.proto` (D2 conventions) + the C# service class delegating to the handler. *Seams:* real Grpc.Tools compile + message round-trip; fake handler asserts delegation/result-map.
3. **REST route + policy emitter** — `MapPost`/`MapGet` with scope / rate-tier / audience / csrf / harmless enforcement. *Seams:* `TestServer` + real D2 auth/scope middleware (where it exists) + fake handler.
4. **in-process leaf emitter** — `I<Module>InternalApi` leaf interface + impl, gated by `@d2InProcess`. *Seams:* DI resolution + fake handler asserts delegation.
5. **handler-interface emitter** — `I<Op>Handler` (the hand-body seam). *Seams:* compile + generated impls implement it.
6. **idempotency gate emitter** — dedupe gate ported to `D2Result` + idempotency store seam. *Seams:* real in-memory `IIdempotencyStore` + fake handler; dup-key/TTL/bad-key tests.
7. **resilience pipeline emitter** — `ResilientPipeline<TKey,TValue>` + builder DSL + keyed DI from the `@d2Resilience` AST. *Seams:* real `DcsvIo.D2.Resilience` + flaky fake outbound; retry/breaker/singleflight behavior.
8. **SSE / server-push emitter** — channel subscription + dispatch wiring from `@d2ServerPush`. *Seams:* faithful `ISseEmitSink` test double; push → assert channel + payload.
9. **minimal TS DTO emitter** — REST-client DTO types (C# `T?` → TS `T | undefined`). *Seams:* type-level + round-trip + parity with C#.
10. **parity + byte-gate tests** — regeneration byte-identity + cross-language DTO parity + registry-existence.
11. **integration-test harness** — the minimal `TestServer`/in-memory-gRPC machinery the route + gRPC emitters validate through.

**Deferred (explicit, not silently dropped):** OpenAPI `x-d2-*` extension layer; full typed-client surface (service↔service .NET clients, browser/BFF typed clients beyond REST DTO types); binding any emitter's output to its eventual real *consumer* (Edge host, Auth module) — a future wiring step, tracked in the ledger, that doesn't change proven correctness.

## Step breakdown (prerequisite order)

- **Step 0 — Branch + workspace.** Cut `n/typespec-emitters` from `n/contract-idl-adr`; scaffold this workspace (README + per-step journals).
- **Step 1 — `@d2InProcess` decorator.** Amend `@dcsv-io/d2-typespec-decorators`: `extern dec` + state key + validator (proposed rule: requires `@d2ServedBy`; independent of `@d2GrpcMethod`) + tests + README. Re-run the package's full 100%-coverage gate; must not regress the existing 12 decorators.
- **Step 2 — Emitter package scaffold.** `server/shared/typescript/typespec-emitters/` workspace member: package.json (peer `@typespec/compiler`, dep `@dcsv-io/d2-typespec-decorators`), tsconfig, vitest, the `$onEmit` entry + shared lib (scalar registry, name transforms, banner reuse, `emitFile` wrapper, a new `D2TSP*` diagnostics family). Smoke-emit only. **Spike the `TestServer`/in-memory-gRPC harness pattern here** (de-risks Steps 4/5/10).
- **Step 3 — C# DTO emitter (+ minimal TS DTO).** Prove on real `GetJwks` + the fixture ops; `[RedactData]` redaction test against the real policy; byte-gate + parity. + integration test → ledger row.
- **Step 4 — proto + gRPC service-impl emitters.** D2 proto conventions; Grpc.Tools compile; `global::`-qualified service base (SC1 lesson); fake-handler delegation test. + integration test → ledger row.
- **Step 5 — REST route + policy + handler-interface emitters.** Enforcement validated via `TestServer` + real auth/scope middleware + fake handler. + integration test → ledger row.
- **Step 6 — in-process leaf emitter.** `I<Module>InternalApi`, gated by `@d2InProcess`; DI + fake-handler test. + integration test → ledger row.
- **Step 7 — idempotency gate emitter.** Real in-memory store + fake handler; dup/TTL/bad-key. + integration test → ledger row.
- **Step 8 — resilience pipeline emitter.** Real `DcsvIo.D2.Resilience` + flaky outbound; retry/breaker/singleflight. + integration test → ledger row.
- **Step 9 — SSE / server-push emitter.** Fixture push op + dispatch wiring + faithful `ISseEmitSink` double. + integration test → ledger row.
- **Step 10 — transport integration harness consolidation.** The `TestServer`/in-memory-gRPC end-to-end across REST + gRPC + leaf (the paths needing a server context); confirm generated transport runs against real middleware where present.
- **Step 11 — parity + byte-gate consolidation + finalize `VALIDATION.md`.** Regeneration byte-identity for every emitted file; cross-language DTO parity; registry-existence; every emitter's ledger row finalized with the seam(s) it's validated against.
- **FINAL-REVIEW** — whole-deliverable audit loop (integration / cross-step / consistency); confirm ledger honesty (no hollow doubles, no overclaim).
- **SHIP** — completeness checklist + attestation; distil predicates; snapshot to `docs/dev/deliverables/0019-typespec-emitters.md`; user-gated commits.

## Validation ledger (D12)

`VALIDATION.md`, one row per emitter: **emitter | validated-by (the integration test) | seams (real lib vs. test double) | for a double: which unbuilt collaborator it stands in for + the trigger to replace it with the real thing**. Built incrementally (each emitter step adds its row), finalized in Step 11. Every emitter MUST have a passing integration test — the ledger documents *against what*, never "not validated yet."

## Risks

- **R1 — host-dependent artifacts.** Route/gRPC/SSE wiring only compiles/runs inside the test harness. The emitter must cleanly separate "compiles into KC app/domain" artifacts (DTOs, leaf interface, handler interface) from "needs a server context" artifacts.
- **R2 — amending a shipped package.** `@d2InProcess` touches the attested 0018 package; re-run its full test + 100%-coverage gate; no regression to the 12 decorators.
- **R3 — stale spike code.** Port the *mechanism*, not the stand-ins. Map to real `D2Result` / `DcsvIo.D2.Resilience` / shipped scope decorators. (rules.md §26.11 sweep.)
- **R4 — two codegen pipelines.** `pnpm codegen` (ts-codegen) and `tsp compile` (this fleet) are independent; document invocation; byte-gate tests must cover tsp-emitted files.
- **R5 — proto→C# is Grpc.Tools' job.** Byte-gate the `.proto`; integration-test the Grpc.Tools output via the harness.
- **R6 — in-memory gRPC/`TestServer` pattern on .NET 10.** Validate early (Step 2 spike) — Steps 4/5/10 depend on it.
- **R7 — scalar coverage.** Build a fuller TypeSpec-scalar → C#/proto/TS registry; an unmapped scalar must fail loud, never silently drop.
- **R8 — resilience fidelity.** Honor int→double widening, sparse-tunable (absent = library default), keyed-DI derivation per the package's Emitter notes; track the cross-runtime parity follow-up.
- **R9 — faithful seams, not hollow stubs.** Test doubles for unbuilt collaborators (SSE sink, handler bodies, Auth caller) MUST assert the real seam contract, not pass vacuously — else validation is hollow. Final-review verifies each double is faithful.
- **R10 — breadth temptation to over-claim.** The ledger is the guard; its seam classification must survive a fresh final-review reading.

## Test strategy (D5 elaboration)

- **Standalone-validatable emitters** (DTO, leaf, handler-iface, idempotency, resilience, SSE-dispatch, TS DTO): instantiate the generated artifact, inject seams (real shared lib where it exists; faithful test double otherwise), drive it, assert behavior. No server context.
- **Server-context emitters** (REST route+policy, gRPC service-impl): minimal ASP.NET `WebApplicationFactory`/`TestServer` (in-memory HTTP, no sockets) + `Grpc.Net.Client` over `TestServer.CreateHandler()` for in-memory gRPC. Wire the generated transport + **real** D2 auth/scope middleware where present + a fake handler; fire real requests; assert routing, enforcement, status mapping.
- All self-managed in the test project (allowed — no `dotnet run`).

## Open questions

_(none outstanding — scope locked)_

## Living State

- **Step 0** (2026-06-15) — branch `n/typespec-emitters` cut from `n/contract-idl-adr`. ✅
- **Step 1** (`@d2InProcess` decorator) — planned 2026-06-15. Locked rule: `@d2InProcess` (presence-marker on Operation, mirrors `@d2Harmless`) **requires `@d2ServedBy`**, enforced in `$onValidate` (cross-decorator) via a new `inprocess-requires-served-by` error diagnostic; independent of `@d2GrpcMethod`. Orchestrator reviewed + **declined** three proposed extra rules (FR-1 `@d2Harmless` interaction, FR-2 rate-tier/scope interaction, FR-3 double-apply restriction) — ship only the one locked rule; double-apply stays idempotent. **✅ Converged**: 13 decorators, 204 tests, 100% coverage, `tsc -b` clean. Targeted audit Round 1 = 17 PASS / 3 N/A / 1 LOW (F-1 stale gate-test comment) → fixed with tamper-evident before/after; closure backstopped by FINAL-REVIEW's fresh full sweep. No regression to the existing 12. Committed `6cb6d896`.
- **Step 2** (emitter package scaffold + C# test-host spike) — planned 2026-06-15. Decisions resolved per Planner recs: package mirrors the decorators package (no `tspMain`); `D2TSP*` diagnostics local to the package (+ a row in SRC_GEN.md), not bolted onto ts-codegen; scalar registry seeded with core scalars only (temporal deferred to Step 3), `toSnake`/`toPascal` only (profile transforms deferred to Step 8); C# test-host spike at `…/typespec-emitters/spike-csharp/` GITIGNORED + outside `D2.slnx` (throwaway de-risk; real harness is Step 10), xunit v2. **Operational gates CLEARED**: verified only `ice-redis` + `ice-postgres` infra containers running (no Node/.NET app containers), so root `pnpm install` (symlink rotation) + host `dotnet build/test` (`obj/` collision) are both safe this session. Two parallel Implementers dispatched (TS scaffold + C# spike). **✅ Converged**: TS package = 68 tests, 100% coverage, `tsc -b` clean, smoke-emit pipeline proven; C# spike = **R6 GREEN** (in-memory gRPC + TestServer confirmed on .NET 10), gitignored throwaway (real harness = Step 10). Targeted audit Round 1 = 21 PASS / 6 N/A / 3 FINDING (F-1 MEDIUM vacuous integration test, F-2/F-3 LOW) → all fixed with tamper-evidence (F-1 mutation-proven). `pnpm-lock.yaml` diff verified clean (new package + one benign babel dedup). Committed `8fa423bc` (new package + `.gitignore` + `SRC_GEN.md` + `pnpm-lock.yaml`).
- **Step 3** (C# DTO emitter + minimal TS DTO) — planning started 2026-06-15 (Opus Planner). First real emitter + first production `.tsp`/`tspconfig` + first `VALIDATION.md` ledger rows. Decisions resolved: `.tsp` location = **`contracts/typespec/<domain>/`** (user — centralized, mirrors protos/specs); C# validation extends `DcsvIo.D2.Private.Edge.Tests`; GetJwks equivalence = reflection public-shape comparison (documenting the live-`Jwk`-is-a-domain-VO divergence); `[RedactData]` tested via the real public Serilog path (policy is `internal`); namespace from a `tspconfig` option, full namespace/category derivation deferred to Step 5; reason-less `@d2Redact`→`PersonalInformation`; `IReadOnlyList<T>`; loud `D2TSP002` for unsupported prop types; host-`dotnet` gated on a fresh `docker ps`. One Sonnet Implementer dispatched (full vertical); relying on the post-impl targeted audit (no separate Plan-Audit — established targeted cadence). **✅ Converged**: C# DTO + TS DTO emitters (shared `walkModel`), 131 TS tests / 100% coverage / `tsc` clean; generated GetJwks/Sign DTOs validated in `DcsvIo.D2.Private.Edge.Tests` (reflection shape-equivalence vs the live handler + `[RedactData]` masking via the real Serilog pipeline), 572 .NET tests pass; byte-gate non-vacuous (3 fixtures, drift-negative each); first `VALIDATION.md` ledger rows. `.tsp`/`tspconfig` under `contracts/typespec/`. IVT to `DcsvIo.D2.Logging` confirmed necessary (policy `internal sealed`; public path mutates the static logger — unsafe under parallel tests). Targeted audit Round 1 = 16 PASS / 3 N/A / 6 FINDING (2 test-quality M, 3 doc/hygiene M, 1 reflection L) → all fixed with tamper-evidence. Committed `1ae6369c`.
- **Step 4** (proto + gRPC service-impl emitters) — planning done 2026-06-15 (Opus). Decisions resolved: only the `sign` op emits (carries `@d2GrpcMethod`; `getJwks` skipped); proto message names = **`<Method>Request`/`<Method>Response`** (matches existing protos + avoids the DTO-name collision; `global::` for the gRPC base per SC1); `walkModel` gains a proto projection (one walker → C#/TS/proto parity); 3 `tspconfig` options (proto-package / proto-csharp-namespace / grpc-service-namespace); in-memory gRPC validation lands in `DcsvIo.D2.Private.Edge.Tests` as the **committed Step-10 harness seed** (DcsvIo.D2.Tests recipe: FrameworkReference + Grpc.Tools + TestHost + GrpcChannel over CreateHandler), fake handler as the glue seam; Sign DTOs emitted into the gRPC fixture + a one-line fixture `ISignHandler` (handler-iface emitter is Step 5); D2Result→gRPC failure mapping = success path + one adversarial `RpcException` test; proto carries the auto-generated banner; host-`dotnet` gated on a fresh `docker ps`. One Sonnet Implementer dispatched. Targeted audit Round 1 = 31 PASS / 7 N/A / 4 FINDING (2 M, 2 L); all 4 must-checks PASS (container gate, csproj safety + no CPM churn + 572 no-regression, non-vacuity, `global::`). **D-6 DEVIATION caught by orchestrator verification**: the Implementer emitted model-name proto messages (`SignInput`/`SignOutput` + `Proto*`/`Dto*` aliases) instead of the locked `<Method>Request`/`<Method>Response` — root cause = orchestrator plan-currency slip (the Request/Response override went to living-state + the Implementer brief but NOT the Planner journal, so Implementer + Auditor followed the stale model-name recommendation). Fixer enforcing Request/Response (drops the aliases; matches the existing proto RPC-message convention) + FINDING-1 (VALIDATION.md stale names) + FINDING-2 (`BuildHost` missing `AddRouting()`) + FINDING-3 (`Step 5` phase-ref in `ISignHandler.cs`) + FINDING-4 (dead `["url","string"]` CS_TO_PROTO entry). **✅ Converged**: proto + gRPC service-impl emitters; proto messages = `<Method>Request`/`<Method>Response` (convention-aligned, aliases removed, `global::` base preserved); `walkModel` 3-language parity; in-memory gRPC validation in `DcsvIo.D2.Private.Edge.Tests` (the Step-10 harness seed) — a real gRPC call delegates to the fake handler + maps; 216 TS tests / 100% coverage / `tsc` clean; `DcsvIo.D2.Private.Edge.Tests` 579 pass (no regression); byte-gate non-vacuous (proto + service + mapper, drift-negative each). D-6 deviation + 4 findings all fixed with tamper-evidence. Committed `812761cd`.

- **Step 5** (REST route+policy + handler-interface emitters) — planning done 2026-06-15 (Opus). **F-11 resolved (user)**: option (A) — add `@d2Command`/`@d2Query` decorator(s) to the shipped package; the emitter generates the handler interface + DTOs into KC's REAL CQRS-category namespace, **matching + replacing** the hand-written interfaces (KC becomes a real emitter consumer, not just a fixture). Route enforcement targets the real `RequireAnyScope`/`RequireAllScopes`/`MarkAsD2HarmlessEndpoint` + `JwtAuthMiddleware`; validated through the real auth middleware via the `TestServer` harness. **User requirement (must design for)**: one handler must be callable by (a) multiple endpoints (REST + gRPC + leaf simultaneously), (b) directly from within the app layer, (c) cross-module — one Edge module calling another via the exposed handler interface / leaf. Design model = shared-handler-core + thin delegating adapters; handler registered once at the app layer. Cross-module placement/DI/location-transparency being grounded against ADR-0020 + the design pass before Step 5/6 lock. Orchestrator resolutions of the other flags: local-copy auth fixtures; `D2TSP003` loud-fail on routed-op-with-no-auth-intent; rate-tier/CSRF emit declared markers (no fake-pass, ledger the unbuilt consumer); `@d2Audience` not per-route (§9.2); `D2TSP004` unsupported-verb. **DESIGN LOCKED (user, 2026-06-15)** after the `DEVELOPER_VIEW.md` walkthrough: F-11=(A) explicit category decorators; **+`@d2Internal`** (3rd marker — mutually exclusive with exposure decorators; explicit internal-vs-callable line, accidental exposure = compile error); **uniformity** (every handler is a `.tsp` op, transport decorators additive, internal ops structurally absent from other services' clients); cross-boundary surface = a thin per-module **`DcsvIo.D2.Private.Edge.<Module>.Clients`** project holding ONE transport-neutral `I<Module>Client` + exposed DTOs + the gRPC-client impl (in-process impl in `app/`; same interface backs both → location-transparent single call; DI binding chosen per consuming deployment); **regen-safety** = generated extension methods called by a hand-written composition root (`.g.cs` overwritten, manual root never touched); shared-handler-core + thin delegating adapters; one handler reachable by REST + gRPC + in-process simultaneously. Worked examples (internal-vs-exposed spec; WhoIs called by remote Billing + in-process Auth at once) in `DEVELOPER_VIEW.md`.
  **STEP SPLIT**: **Step 5 = decorator additions** (`@d2Command`/`@d2Query`/`@d2Internal`); the route+policy+handler-interface emitters (planned in `05-route-policy-emitter/journal.md`) become **Step 6**; leaf/idempotency/resilience/SSE/test-host/parity shift +1 (→ 7–12). Step 5 (decorators) Planner dispatched. **Flags resolved (orchestrator)**: R1 (category required+exclusive) + R2 (`@d2Internal` ⊕ exposure) locked; **R3 ADOPTED** (every op must carry an exposure decorator XOR `@d2Internal` — no implicit internal; catches accidental under-exposure; total `exposed ⇔ ¬internal` invariant); `@d2ServerPush` added to the exposure set (R2+R3); auth-policy decorators on `@d2Internal` = no hard rule (rate-tier already excluded transitively), the Step-6 emitter ignores them on internal ops (inert). Regression: R1 makes category mandatory → every existing `compile()` test op needs a category (+ exposure-or-`@d2Internal`) — Implementer annotates all first-pass. Implementer dispatched. **✅ Converged**: 16 decorators, 236 tests, 100% coverage, `tsc -b` clean. Targeted audit Round 1 = 32 PASS / 4 N/A / 7 FINDING (4M / 3L / 0 HIGH) → all fixed: F-1/F-2 (R3 + `@d2ServerPush` approval recorded in the journal — the recurring journal-currency miss, now hardened in the kinds-of-misses log), F-3/4/5 (`InProcess`<`Internal` ordering), F-6 (`R1/R2/R3` labels stripped from code/tests), F-7 (14 `diagnose()` acceptance tests made non-vacuous — ops now fully valid). Committed `c6606ea5`.

- **Step 6** (Exposed-op contract layer) — planning kickoff 2026-06-15 (fresh Opus Planner). Canonical journal = `06-exposed-op-contract-layer/journal.md`. ⚠️ The earlier `06-route-policy-handler-emitter/journal.md` is **SUPERSEDED as the Step-6 plan** (predates the façade lock; recommends the now-superseded J-a) — retained as REFERENCE for handler-interface mechanics + the `Jwk`-collision + F-NS namespace analysis; its route+policy emitter moves to **Step 7**. Step-6 scope per the design lock: scaffold `DcsvIo.D2.Private.Edge.KeyCustodian.Clients`; DTO emitter routes **exposed** DTOs there; generate `I<Op>Handler` (app) + façade interface (clients) + façade impl (app); the **GetJwks overwrite now includes `GetJwksOutput`** (transport DTO) with the hand-written handler body projecting domain `Jwk` → DTO `Jwk` (supersedes J-a). Gate: KC builds + tests pass. **Planner converged** (10 flags); orchestrator resolved all in the canonical journal's "Orchestrator decisions" section — incl. **3 overrides** of Planner/doc recs (trust-but-verify): (D-b) façade signature TRANSPORT-NEUTRAL, no `HandlerOptions`, for location transparency ⇒ Clients refs `DcsvIo.D2.Result` only; (D-c) an exposed op's input+output DTOs BOTH live in `Clients` (single copy; app refs via `App→Clients`); (D-d) façade DI = TRANSIENT (Singleton would capture the handler's scoped `DbContext`). See cross-cutting D14–D16. **Split 6a** (scaffold Clients + handler-iface emitter + both-DTOs→Clients routing + GetJwks overwrite + `ISignHandler` reshape) → **6b** (façade interface+impl+Transient DI + delegation test). 6a Implementer dispatched 2026-06-15. **✅ 6a CONVERGED**: scaffolded `DcsvIo.D2.Private.Edge.KeyCustodian.Clients` (refs `DcsvIo.D2.Result` + Tier-1-forced `DcsvIo.D2.Utilities`); handler-interface emitter + exposed-DTO routing (both getJwks DTOs → Clients) + `D2TSP003`; the GetJwks overwrite (3 hand-written app files deleted, `IGetJwksHandler.g.cs` + Clients DTOs generated, handler body projects domain→`Clients.Jwk` fully-qualified) + the Step-4 `ISignHandler` reshape. **Gates GREEN**: `dotnet build server/D2.slnx` 0W/0E, KC tests 584/584, `jb inspectcode` 0 findings, TS 258 tests/100% `src/**`. Targeted audit Round 1 = 5 FINDING (F-1 M D-b-deviation → resolved by amendment [Utilities is Tier-1-forced + shared-lib-legal]; F-2/F-3/F-4 M/L recurring step-label leak in emitter src/README/csproj → fixed + grep-verified + regression-pinned via `emitter-source-labels.test.ts`; F-5 L SRC_GEN per-ID entry → added). Uncommitted. **Next: 6b** (façade interface + impl + Transient DI + delegation test). **✅ 6b CONVERGED → Step 6 COMPLETE**: `facade-emitter.ts` emits `IKeyCustodianInternalApi` (Clients, transport-neutral signature) + `KeyCustodianInternalApi` impl (app, delegates to `IGetJwksHandler`) + the generated **Transient** DI ext (wired into hand-written `AddD2KeyCustodianApp` per regen-safety). Gates GREEN: `dotnet build server/D2.slnx` 0W/0E, KC tests 597/597, `jb inspectcode` 0 (no captive-dependency), TS 296/100% `src/**`. Targeted audit Round 2 = 4 FINDING (F-6 L app-entry-point resolution test; F-7/F-8 M VALIDATION.md + README doc-parity gaps; F-9 L stale csproj comment) → all fixed + orchestrator-verified. Audit-clean both rounds. Uncommitted — awaiting commit permission (Step 6 = one commit). **Next: Step 7** (transports → façade: REST route+policy emitter + re-point gRPC to the façade + wire↔DTO mapper).

- **Step 7** (Transports → façade) — planning done 2026-06-15 (Opus Planner); decisions APPROVED (journal "Orchestrator decisions"). Transport delegates THROUGH the façade when `@d2InProcess` else `I<Op>Handler` (D17, locked). REST route+policy emitter wired to the REAL auth mechanism (`RequireAnyScope`/`RequireAllScopes`/`MarkAsD2HarmlessEndpoint` + `JwtAuthMiddleware`), validated via a `TestServer` 401/403/200 matrix. gRPC service-impl emitter **RE-POINTED** to the façade (`delegationTarget` param). NO separate REST wire↔DTO mapper (ASP.NET binds the DTO; gRPC mapper unchanged). F-MAP = MAP-ii (real `ToProblemDetails`, **failure-only → success-first** short-circuit). F-HOME = local-copy 4 auth fakes + auth/caching refs into `DcsvIo.D2.Private.Edge.Tests`. `D2TSP004` route-missing-auth-intent, `D2TSP005` unsupported-verb. `sign` fixture gains `@route("/internal/v1/kc/sign")` + `@post` + `@d2RequireAnyScope("self.write")` + rate-tier/csrf markers + a synthetic all-scopes route (the multi-surface op); `getJwks` route-less (ADR-0021 fringe). All transport output FIXTURE-validated in `DcsvIo.D2.Private.Edge.Tests` (ADR-0020 boundary — KC `app/` can't reference auth-http). **Split 7a** (route+policy emitter + TestServer matrix + F-HOME + sign fixture façade) → **7b** (gRPC re-point + byte-gates + Step-4 no-regression). 7a Implementer dispatched. **✅ 7a CONVERGED**: `route-policy-emitter.ts` emits `Map<Op>Route()` delegating through the façade (`@d2InProcess`) else `I<Op>Handler`, real `RequireAnyScope`/`RequireAllScopes`/`MarkAsD2HarmlessEndpoint` enforcement, MAP-ii (`ToProblemDetails`, success-first), rate-tier/CSRF markers, `[AsParameters]` for GET/DELETE; `D2TSP004`/`D2TSP005`; the `sign` fixture façade + `.tsp` route/scope; TestServer 401/403/200 matrix against the REAL `JwtAuthMiddleware` (public `UseD2Auth`). Targeted audit Round 1 = 6 FINDING (F-1 M avoidable IVT → reverted to public `IJwksProvider` swap; F-2/F-3/F-4 H doc-parity gaps → added; F-5 H vacuous byte-gate + committed-file drift → re-aligned; F-6 L `[AsParameters]` unit test → added) — all fixed + orchestrator-verified. **Gates GREEN**: build 0/0, D2.Edge 610/610, **D2.Shared 6008/6008 SOLO** (a 64-fail full-slnx parallel artifact was verified benign — MTP ignores `--filter`, cross-project Serilog contention; solo is green), inspectcode 0, TS 387/100%. Uncommitted. **Next: 7b** (gRPC re-point to the façade). **✅ 7b CONVERGED → Step 7 COMPLETE**: `grpc-service-emitter.ts` re-pointed via a `delegationTarget` — the generated gRPC service delegates through the façade for `@d2InProcess` ops (the committed `KeyCustodianSignerService.g.cs` now injects `IKeyCustodianSignerFacade` + calls `facade.SignAsync(...)`), else `I<Op>Handler`; the proto↔DTO mapper is unchanged; both delegation branches direct-unit-tested; `GrpcServiceImplTests` reshaped through the façade (Step-4 no-regression). Targeted audit Round 2 = **zero findings**. **Gates GREEN** (7b Auditor, independent): build 0/0, D2.Edge 611/611, D2.Shared 6008/6008 (full + SOLO), inspectcode 0, TS 402/100% `src/**`. Step 7 (7a+7b) audit-clean across 2 rounds. **Note**: the 7b dispatch hit two API 500s (server-side infra) before succeeding; state validated clean after each (no orphans/stashes/partials). Committed `4b4378fa` (Step 7 = one commit; 27 files). **Next: Step 8** (idempotency gate emitter).

- **Step 8** (idempotency gate emitter) — planned + implemented + audited + fixed 2026-06-16. The gate is woven INTO the generated REST route delegate (replay-check before delegation, store after, post-auth); generated from `@d2Idempotent`; `header` reads `Idempotency-Key`, `derived` = SHA-256 over named fields; **dup → replay the stored `D2Result` verbatim** (not 409); store regardless of success/failure; fail-open on store outage. **No real HTTP replay store exists** → emitter-owned faithful `D2GeneratedIdempotencyStore` seam + in-memory double (ledgered; consumer = unbuilt Edge `Idempotency.*` middleware). **`D2TSP006` (`idempotent-requires-route`, LOUD-FAIL)** — overrode the Planner's "inert" (strict + fail-loud, matching `@d2RateLimitTier`'s `rate-tier-requires-route`). REST-only (gRPC untouched). `.tsp`: `sign` gains `@d2Idempotent("header",…)`; new routed `signDerived` op for the derived path. **One turn** (overrode the Planner's 8a/8b split — single low-blast-radius emitter). Targeted audit Round 1 = 6 FINDING (M-1 blocker: `type-check:test` FAILED, Implementer skipped it → fixed; M-2/3/4 doc-parity gaps → added; M-5 missing TTL-expiry test → added w/ injectable `TimeProvider`; L-1 D2TSP006 direct test → added) — all fixed + orchestrator-verified. **Gates GREEN**: type-check clean, build 0/0, TS 472/100%, D2.Edge 619/619, D2.Shared 6008/6008 SOLO, inspectcode 0. Committed `bd59b921` (21 files). **Next: Step 9** (resilience pipeline emitter).

- **Step 9 — RE-SCOPED 2026-06-16** (deep investigation, user-directed): from "resilience pipeline emitter" → **the cross-process wire-client + resilience layer**. **Canonical plan: `09-cross-process-clients/RESCOPE.md`** (supersedes the earlier `09-resilience-pipeline/journal.md`, whose processor-side model + `TValue=<Op>Output?` assumption were wrong). Locks: **(§0.5)** the `D2Result` is NEVER lost — always propagated success/failure via ProblemDetails (HTTP) or a `D2ResultProto` envelope (gRPC); **(§0.6)** four caller contexts (in-process façade · browser REST · SSR gRPC · .NET S2S gRPC) → the TS **client/server split** (browser + server surface per exposed op, per v1); **nesting-safe transport-only resilience** (wrap the *throwing* transport call; retry/break on transport faults only; a callee's returned `D2Result` is NEVER auto-retried → no n×m amplification) with **ZERO** resilience-lib changes. **The one linchpin MUTATION**: the gRPC wire must carry the `D2ResultProto` envelope — the shipped Step-4 server-impl throws `RpcException(Internal)` and swallows fidelity; + HTTP success-status fidelity (Step-7 route). **~50% wire-up / ~40% new** (client emitters + the .NET `D2Result↔D2ResultProto` envelope mapper, ported from v1 `ProtoExtensions` + client mappers) **/ ~10% mutate**. Sub-steps: **9a** wire-format fidelity alignment → **9b** .NET gRPC client emitter → **9c** TS client emitter (browser+server) → **9d** `@d2Resilience` custom predicates → **9e** REAL over-the-wire integration tests. BFF infra (`@dcsv-io/d2-auth-bff-client`/hooks/`gateway.server`) = separate downstream. **✅ 9a (wire-format fidelity) CONVERGED → committed `0d546af3` (21 files)**: **9a-i** gRPC `D2ResultProto` envelope (emitted service returns `result.ToProtoResponse()` on the EXISTING `DcsvIo.D2.Result.Grpc` mapper [zero new mapper]; proto `Response = { D2ResultProto result=1; <Op>Output data=2 }`; business failures ride the envelope w/ real status+category+TK key, gRPC status stays `OK`, `RpcException` reserved for transport/auth) + **9a-ii** HTTP success-status fidelity (route + idempotency-replay map 2xx via `Results.Json(result.Data, statusCode:(int)result.StatusCode)` keyed on `status < 400` NOT `result.Success`; preserves Created 201 / SomeFound 206 / PartialSuccess 207; **fixes the latent `SomeFound`-throws bug** — 206 fell to `ToProblemDetails`'s 2xx-guard throw). **Gates GREEN**: TS vitest 474/474 + tsc/tsc:test clean, `dotnet build` 0W/0E, D2.Edge 625/625 (+ new `…Created_Returns201WithBody` / `…SomeFound_Returns206WithBody` pins), byte-gates non-vacuous. Targeted audits: 9a-i Round 1 (F1/F2/F3 doc) + orchestrator trust-but-verify catch (F1 residual — stale README summary bullet contradicting the corrected detail) → fixed+verified; 9a-ii Round 2 = 26 PASS / 11 N/A / 2 FINDING (F4 M stale test XML-doc, F5 L stale test comment — `success-first`/`Results.Ok` leftovers) → fixed + verified by absence; F2 doubt (`ThrowsRpcExceptionInternal` row) confirmed already-closed. ~14 generated `.g.cs` = CRLF/LF noise (zero content diff), excluded from the commit.
  - **Resilience prerequisite — deliverable 0020 (Resilience ownership) shipped 2026-06-17, committed `3e5c842b`** (squash-merged from sub-branch `n/resilience-ownership`, preserved as a checkpoint; snapshot `docs/dev/deliverables/0020-resilience-ownership.md`). Before 9b's client could integrate resilience the model had to be LOCKED. **Locked model**: `DcsvIo.D2.Resilience` (C#) + `@dcsv-io/d2-resilience` (TS) are the SOLE, complete, **opt-in, caller-side, per-call-overridable** mechanism for BOTH network and value resilience; the BCL `AddStandardResilienceHandler` is REMOVED from ServiceDefaults (no channel carries it). Six layers — Retry · CircuitBreaker · Singleflight · TimeoutLayer (total + per-attempt, genuinely cancels via linked CTS / threaded `AbortSignal` in TS) · RateLimiterLayer (`SemaphoreSlim` concurrency, fail-loud `MaxConcurrency ≥ 1`) · PassThrough. Canonical nesting `RateLimiter → TotalTimeout → Retry → CircuitBreaker → PerAttemptTimeout` (Singleflight outermost). `ResilientPipeline.ExecuteAsync(key, op, ct) → D2Result<T>` never throws; keyed-singleton DI (`AddResilientPipeline<TKey,TValue>(serviceKey, p => p.Use…())`); full C#↔TS parity (genuine cancellation + CircuitBreaker/retry).
  - **How 9b–9d integrate it**: the generated client applies the pipeline ONLY when the op carries `@d2Resilience` (opt-in), per-call-overridable; transport faults retry via a custom `IsTransient = ex is RpcException r && ProtoExtensions.IsTransientGrpcException(r)`; a callee's returned `D2Result` is NEVER auto-retried (nesting-safe — it rides the `D2ResultProto` envelope with gRPC status `OK`, so the transport layer sees success → no n×m amplification). 9d owns the `@d2Resilience` custom-predicate emission (AST → pipeline config + keyed-DI registration).
  - **Next: 9b** (.NET gRPC client emitter) — re-planned 2026-06-17 under the locked resilience model. The original `09b-dotnet-grpc-client/journal.md` envelope/transport/DI mechanics STAND; its resilience section (the old R-1 double-stack debate) is SUPERSEDED by 0020's model.
  - **9b client-surface design LOCKED (2026-06-17, with user)** → cross-cutting **D18/D19**. The cross-process client model is **per-(module × transport)**, NOT a unified `I<Module>InternalApi` (op-sets diverge by transport — supersedes the DEVELOPER_VIEW unified-client/WhoIs model). gRPC client = per-module `IKeyCustodianGrpcClient` (its OWN interface — NOT the façade, NOT per-op); per-call resilience override on it; façade renamed `→ IKeyCustodianApi` (D19, folded into 9b-i task 1). TS 9c = `keyCustodianGrpcClient` (server) + `keyCustodianRestClient` (browser). The auth token-flow investigation (file:line-verified) confirmed server=gRPC adds **no per-hop mint penalty** — RFC 8693 scoped tokens are cached per `(sessionId, audience, scopeSetHash)` + singleflighted (`TokenExchangeCache`/`HttpTokenExchangeClient`); service-identity token cached + proactively refreshed. Orchestrator decisions written into `09b-…/journal.md` "## Orchestrator decisions (2026-06-17)" (amends the per-op Planner plan → per-module; mechanics stand). **In parallel**: BFF internal-token client cleanup dispatched (rename `HttpKeyCustodianClient`→`HttpInternalTokenClient` + refresh-ahead; `@dcsv-io/d2-grpc-client`; separate commit, lands BEFORE 9b). **Next: audit+commit the cleanup → dispatch 9b-i.**

## Completion plan

> Added 2026-06-20 (Planner, PLAN phase). 0019's emitter suite was built incrementally on
> `n/typespec-emitters` through Step 9a but **never reached FINAL-REVIEW or SHIP** (no
> `docs/dev/deliverables/0019-*.md`; the `## Completeness attestation` block below is blank).
> This section is the locked plan to FINISH + SHIP it. It supersedes the residual
> "Step 10 SSE · Step 11 harness · Step 12 parity · FINAL-REVIEW · SHIP" tail of the
> Step-breakdown lists above with a concrete, prerequisite-ordered finish-list.
>
> Two companion artifacts hold the deep specs: **[temporal-mapping.md](temporal-mapping.md)**
> (the TypeSpec→NodaTime→C#-wire→TS-wire lossless table) and
> **[resilience-predicate-grammar.md](resilience-predicate-grammar.md)** (the
> `retryWhen`/`failWhen` DSL). Tracking rows: POST_PIVOT_ROADMAP §C [C1a, C2–C16] + §G.

### Locked scope (user-decided 2026-06-20 — DECISIONS, plan to them; default-deferral REJECTED)

These nine items are the locked completion scope. **None may be deferred** without explicit
written user permission naming the specific item (§13.4 / §13.14).

1. **Temporal scalars — FULL LOSSLESS, ALL TYPES.** Map every temporal type to a losslessly
   round-tripping wire shape mirroring TIMESTAMPS.md: `utcDateTime`/future-fixed → `DateTimeOffset`
   + ISO `"O"`; `plainDate`/`plainTime`/`plainDateTime` → offset-FREE strings (never invent an
   offset); `offsetDateTime` → `OffsetDateTime`; `duration` → ISO-8601; and COMPOSITE wire records
   for the zone-bearing types so the IANA zone survives — `ZonedInstant` → `{ instant, zoneId }`,
   `LocalAnchoredEvent` → `{ scheduledLocal, ianaZone, nextFireUtc? }`. Build the WHOLE table now
   (no fail-loud-until-needed). Round-trip fixtures in BOTH C# and TS. Full table + sub-decisions
   D-T1..D-T4 → **temporal-mapping.md**.
2. **OpenAPI `x-d2-*` extension emitter — IN.** Per-version OpenAPI document layering the
   D²-specific `x-d2-*` policy extensions (scopes / rate-tier / audience / csrf) on top of the
   stock `@typespec/openapi3` HTTP-shape output (which `@typespec/openapi3` cannot surface). The
   seventh ADR-0021 emitter; closes C2.
3. **`@d2Resilience` custom predicates (`retryWhen`/`failWhen`) — reopen the 0018 decorators
   package.** Minimal result-expression DSL reaching BOTH the `D2Result` envelope AND the wrapped
   `TOutput` fields; compile-validated against TOutput + the closed error-code/category registries
   (fail-loud); emitted CROSS-LANGUAGE (C# + TS). `retryWhen` opts a business result INTO retry
   (controlled move inside the captured-envelope boundary); `failWhen` forces a terminal fail. The
   per-call `pipelineOverride` already exists (don't rebuild). Grammar + validation + emission +
   captured-envelope integration + the 0018-reopen plan + sub-decisions D-R1..D-R7 →
   **resilience-predicate-grammar.md**.
4. **gRPC-client validation (Step 9b finish).** The emitter source (`grpc-client-emitter.ts`) is
   built + wired (`emitter.ts:458-480`); FINISH the C# validation harness — committed fixtures in
   `DcsvIo.D2.Private.Edge.Tests/TypeSpecGrpc/Generated/`, `GrpcClientTests.cs`, byte-gate constants in
   `proto-grpc-byte-parity.test.ts`, the VALIDATION.md gRPC-client row, the `Clients.csproj` MUT-1
   ref. Closes C1a.
5. **TS client emitters (Step 9c).** Browser REST client (`lib/client/…`, `fetch` over the
   `apiCall`/`executeFetch` substrate → ProblemDetails, light resilience) for `@route` ops +
   server/SSR gRPC client (`lib/server/…`, `handleGrpcCall`/`unaryCall` +
   `retryAsync(isTransientGrpcError)`) for `@d2GrpcMethod` ops, per the D18 client-surface
   taxonomy. Closes C10.
6. **SSE-dispatch emitter (Step 10).** Generate the `@d2ServerPush` op's channel-subscription +
   dispatch wiring against the `ISseEmitSink` test-double seam (D7; host-independent). The
   `text/event-stream` wire BINDING stays hand-written fringe (ADR-0021). Reconcile 0019's D7
   wording so "SSE emitter" = the DISPATCH contract, not the wire binding; the binding stays an
   Edge fringe endpoint (roadmap §G). Resolves C4 to "emit the dispatch, fringe the binding."
7. **enum/union property types (D2TSP002).** Today loud-fail (`model-walk.ts:217-247`). Pick the
   C# enum mapping convention; handle enum + union property types in `walkModel` across all three
   emit targets (C# / proto / TS). Closes C6.
8. **over-the-wire tests (Step 9e).** Two-process real-gRPC (self-managed Testcontainers /
   child-process) proving transient-recovery / breaker open-half-open / no-amplification (callee
   `ValidationFailed` NOT retried) / byte-fidelity. Gated on items 4 + 5. Closes C12.
9. **FINAL-REVIEW + the 0019 deliverable record + SHIP.** The formal closeout — whole-deliverable
   audit loop, completeness checklist + attestation, snapshot to
   `docs/dev/deliverables/0019-typespec-emitters.md`, flip PHASE_3.md C0 off "☐ Next", flip
   roadmap §C [C1a..C16] / §G rows. Closes C9 + C13 + C14 + C16.

**Core-goal invariant (HONORED throughout)**: every emitter independently validated against its
real seams — real shared lib where it exists; a FAITHFUL test-double for an unbuilt consumer (NOT
a hollow stub). The VALIDATION.md ledger records each (D12). No production Edge host needed (D13).

### Step breakdown (prerequisite order)

Numbered as **completion steps** (C-prefix to disambiguate from the historical Steps 1–9a). The
ordering rationale: DTO-emitter-level capability gaps (temporal, enum/union) come FIRST because
every other emitter walks the same `walkModel`/scalar-registry — fixing them once benefits the
whole fleet and de-risks the client/SSE emitters that emit those types. The resilience-predicate
DSL reopens 0018 (a decorator-package step) BEFORE the gRPC-client emitter consumes it. gRPC-client
validation precedes TS-client precedes over-the-wire (the wire tests need a validated client to
exercise). OpenAPI + SSE are independent and can run in parallel with the client track.

| # | Completion step | One-line scope | Depends on | Validation seams | Ledger / roadmap impact |
|---|---|---|---|---|---|
| **C-1** | **Temporal scalars (DTO-emitter)** | Extend the scalar registry + `walkModel` with the full lossless temporal table (per temporal-mapping.md); emit the two zone-bearing composite DTOs; C# + TS round-trip fixtures + DST-adversarial + cross-language parity | — (DTO-emitter foundational) | Real `DcsvIo.D2.Time` (`ZonedInstant`/`LocalAnchoredEvent` `Create` round-trip) + `@dcsv-io/d2-time` + the shared `contracts/temporal/*.fixture.json` | New VALIDATION.md temporal rows; closes C5; resolves §E temporal-mapping decision (record the D-T choices) |
| **C-2** | **enum/union property types (DTO-emitter)** | Add enum + union handling to `walkModel`; C# `enum` (+ `[JsonConverter]` convention) / proto `int32`-or-named / TS union-or-enum; lift D2TSP002 for the supported shapes (keep loud-fail for genuinely-unsupported) | — (DTO-emitter foundational; independent of C-1) | Type-level + round-trip + cross-language parity (the `dto-parity.test.ts` shape, extended) | New VALIDATION.md row; closes C6; the C# enum-convention decision recorded |
| **C-3** | **`@d2Resilience` custom predicates (reopen 0018)** | Amend the decorator package: `retryWhen`/`failWhen` optional args + new parser + validator + registries + diagnostics; re-run the 100%-coverage gate, no regression to the 16 decorators (per resilience-predicate-grammar.md §6) | — (decorator-package; must precede C-5's client consuming it) | Pure-AST parser tests + decorator acceptance tests + closed-registry validation (error-codes spec + ErrorCategory set); the existing 0018 gate held | Decorator-package README + lib docs; closes the decorator half of C11 |
| **C-4** | **gRPC-client validation harness (Step 9b finish)** | Committed `…/TypeSpecGrpc/Generated/` client fixtures + `GrpcClientTests.cs` + byte-gate constants + VALIDATION.md gRPC-client row + `Clients.csproj` MUT-1; confirm the captured-envelope body (transport-fault retry; business `ValidationFailed` NOT retried, full fidelity) | C-3 if the fixture op carries a predicate; else independent (emitter source already built) | Real in-memory gRPC `TestServer` harness (already in `DcsvIo.D2.Private.Edge.Tests`) + real `DcsvIo.D2.Result.Grpc` envelope mapper + real `DcsvIo.D2.Resilience` keyed pipeline + fault-injecting service shim | Closes C1a; the gRPC-client ledger row + replace-triggers CB5/CB6 (channel address + one-time outbound auth) recorded |
| **C-5** | **`@d2Resilience` predicate EMISSION + gRPC-client consumes it** | Emit the C# + TS predicates from the AST; thread the C# predicate into the generated client's retry decision (sentinel + extended `IsTransient` per §5); byte-gate the predicate output; over-the-wire predicate test deferred to C-8 | C-3 (the decorator) + C-4 (the client it folds into) | Real `DcsvIo.D2.Resilience` (the keyed pipeline + sentinel retry path) + the in-memory harness; cross-runtime C#↔TS predicate-behavior parity test | New VALIDATION.md predicate rows; closes the emitter half of C11 |
| **C-6** | **TS client emitters — browser REST + server gRPC (Step 9c)** | New `ts-client-emitter.ts` (or split) emitting per-op typed fns for BOTH surfaces per D18; dispatch wired in `emitter.ts`; vitest tests; VALIDATION.md rows | C-1 + C-2 (so emitted TS DTOs cover temporal/enum) + C-3/C-5 (TS predicate parity, if the op carries one) | `@dcsv-io/d2-grpc-client` (`handleGrpcCall`/`unaryCall`/`isTransientGrpcError`/`d2ResultFromProto`) for server; `apiCall`/`executeFetch`/`gateway-response` for browser (see PREREQ RISK on the BFF seam, below) | Closes C10; replace-triggers CB7 (SSR channel) + CB8 (browser fetch substrate) recorded |
| **C-7** | **OpenAPI `x-d2-*` extension emitter** | Per-version OpenAPI doc + the `x-d2-*` policy extensions (scope/tier/audience/csrf) layered on stock `@typespec/openapi3`; vitest + byte-gate; VALIDATION.md row | — (independent; can run parallel with C-4..C-6) | Stock `@typespec/openapi3` (real, for the HTTP shape) + the decorator `stateMap` reads (real `@d2*` decorators) | Closes C2; the seventh ADR-0021 emitter ledgered |
| **C-8** | **SSE-dispatch emitter (Step 10)** | Generate `@d2ServerPush` channel-subscription + dispatch wiring against a FAITHFUL `ISseEmitSink` test double; reconcile D7 wording (dispatch ≠ wire binding); the `text/event-stream` binding stays fringe | C-1 + C-2 (emitted DTOs) | Faithful `ISseEmitSink` test double (asserts the real dispatch contract — NOT vacuous; R9); push → assert channel + payload | Resolves C4 ("emit dispatch, fringe binding"); new VALIDATION.md row + CB3 replace-trigger (Edge channel gateway) |
| **C-9** | **Over-the-wire integration tests (Step 9e)** | Two-process real-gRPC self-managed (Testcontainers/child-process): transient-recovery, breaker, no-amplification, byte-fidelity, predicate behavior | C-4 + C-5 + C-6 (a validated client to drive over a real socket) | Real two-process gRPC (self-managed; no `dotnet run`) + the generated client + the generated service | Closes C12; new VALIDATION.md over-the-wire row |
| **C-10** | **Harness + parity + VALIDATION.md consolidation (Steps 11/12)** | Consolidate the per-step TestServer/in-memory-gRPC harness paths; extend regeneration byte-identity to EVERY emitted file (incl. C-1/C-2/C-4/C-5/C-6/C-7/C-8 output); finalize the per-emitter ledger | C-1..C-9 (the emitters whose rows it finalizes) | — (consolidation of existing real-seam validations) | Closes C13 + C14; VALIDATION.md finalized |
| **C-11** | **FINAL-REVIEW** | Fresh whole-deliverable audit loop (K=12 + Aggregator per round; fresh Fixer per finding); confirm ledger honesty (no hollow doubles, no overclaim — R9/R10); converge to a zero-FINDING sweep | C-1..C-10 | The whole deliverable (cross-step / integration / consistency) | The completeness checklist gate |
| **C-12** | **SHIP** | Completeness checklist + attestation into the README; distil predicates; snapshot to `docs/dev/deliverables/0019-typespec-emitters.md`; flip PHASE_3.md C0 off "☐ Next"; flip roadmap §C [C1a..C16] / §G rows; user-gated commits | C-11 | — | Closes C9 + C16; the formal deliverable record lands |

**Per-step orchestration discipline** (the established 0019 cadence, per
`feedback_audit_cadence_targeted_per_step` + `feedback_pause_between_steps`): each completion step
runs PLAN (Planner sub-agent) → IMPL (Implementer) → targeted audit (orchestrator-scoped to the
step's risk) → fix-to-clean, then the orchestrator surfaces to the user BEFORE dispatching the
next step. The full K=12 audit loop runs at C-11 FINAL-REVIEW. Every step embeds the 3-artifact
journal (big table / findings log / fix log). Every gRPC/temporal step carries the
temporal-adversarial / envelope-fidelity test requirements as PLAN-enumerated, Implementer-written,
Auditor-flagged-if-absent (per the MEMORY predicates).

### Prerequisite-ordering risks + open sub-decisions (surfaced, not deferred)

- **PREREQ RISK — the browser-REST seam (C-6) is the pre-pivot v1 BFF, not a built v2 lib.** The
  `apiCall`/`executeFetch`/`gateway-response` substrate the RESCOPE names lives in
  `server/web/src/lib/client/rest/` + `lib/shared/rest/` — it EXISTS but (a) it is the pre-pivot
  BFF that carries the tracked `D2Result.<factory>` static-call gap (MEMORY follow-up
  `project_web_d2result_static_calls`; `server/web` is not host-typechecked yet), and (b) ADR-0021
  + RESCOPE §5 explicitly call the BFF composition-root wiring (channel/auth/interceptors)
  HAND-WRITTEN infra, NOT generated. So C-6's browser emitter validates the GENERATED per-op fn
  against the `apiCall`/`executeFetch` SEAM (faithful, ledgered as CB8) — it does NOT depend on the
  pre-pivot BFF being correct. **Open sub-decision**: does C-6 validate against the real
  `server/web` `apiCall` (accepting its pre-pivot state) or against a faithful test-double seam of
  the same signature? Recommendation: faithful test-double seam (keeps 0019 host/BFF-independent
  per D13; the real wiring is the BFF-rebuild consumer, replace-trigger CB7/CB8). **Surface to
  user.**
- **PREREQ RISK — C-3 reopens a shipped, attested package (R2).** Mitigated by the additive-only
  amendment (two optional params; no existing call site breaks) + re-running the 0018 100%-coverage
  gate with a no-regression assertion on the existing 16 decorators (the same discipline Steps 1 +
  5 used). The risk is real but bounded; flagged so the orchestrator briefs the Implementer to cite
  the before/after test count.
- **SUB-DECISION cluster (temporal)** — D-T1 (`offsetDateTime`→`OffsetDateTime` vs `ZonedInstant`),
  D-T2 (`duration` ISO vs numeric), D-T3 (composite DTO naming + a canonical temporal `.tsp`),
  D-T4 (proto optional-string representation). Recommendations in temporal-mapping.md §3; **need
  user confirmation before C-1 locks.**
- **SUB-DECISION cluster (resilience predicate)** — D-R1..D-R7 (validation home, diagnostic
  placement, emission file shape, operator set, the `retryWhen` sentinel mechanism, decorator-param
  shape, client-only scope). Recommendations in resilience-predicate-grammar.md §7; **need user
  confirmation before C-3 locks.**
- **SUB-DECISION (enum/union, C-2)** — the C# enum mapping convention (string-backed
  `[JsonConverter]` vs int-backed; proto `int32` vs a named proto enum; TS `enum` vs string-union).
  Roadmap C6 notes "nothing structural" — it is a mapping-policy choice. **Surface the convention
  options to the user before C-2 locks.**
- **SUB-DECISION (SSE, C-8) — ✅ RESOLVED (user, 2026-06-21).** D7-reconciliation confirmed: the
  emitter generates the DISPATCH contract only; the `text/event-stream` wire binding stays the named
  hand-written fringe (ADR-0021) — resolves the long-open C4 as "emit dispatch, fringe binding."
  Three design forks locked with the user: (1) **C# dispatch only** — no TS browser EventSource
  consumer (that side IS the fringe binding); (2) **explicit `targetId` param** on the generated
  `DispatchAsync` (no payload-field-derived id); (3) **per-op `I<Op>Dispatcher`** (matches the
  fleet's per-op pattern). Full design + derivable-bits → `C-8-sse-dispatch-emitter/journal.md`
  "Orchestrator decisions."
- **ROADMAP semantics flag (do NOT silently change)** — POST_PIVOT_ROADMAP §C row **C1** currently
  reads "✅ done (on branch)" for the eight built emitters, AND **C7** reads "✅ done in 0023" for
  the emitter auto-wire. Those are correct and stay. The completion steps above CLOSE C1a, C2, C4,
  C5, C6, C9, C10, C11, C12, C13, C14, C16 — at SHIP (C-12) the orchestrator flips those rows +
  PHASE_3.md C0. The plan does NOT pre-emptively edit committed roadmap semantics; it records the
  intended flips here so SHIP executes them deliberately.

## Design lock (final, 2026-06-15) + re-sequenced plan

Authoritative model: `DEVELOPER_VIEW.md` → "Locked architecture — FINAL". Key decisions (all user-confirmed):
- **3 surfaces**: public REST (Edge `api/`) · internal gRPC (Edge → module façade) · module façade (`I<Module>InternalApi`; siblings + Edge gRPC → façade → handler). One handler behind all; an op opts in via `@route`/`@d2GrpcMethod`/`@d2InProcess`.
- **`I<Op>Handler` is module-internal** (always generated); the **façade is the only externally-visible surface** (in the thin `DcsvIo.D2.Private.Edge.<Module>.Clients` project siblings reference).
- **Transport adapters delegate through the façade when it exists (`@d2InProcess`), else the handler.**
- **Mapping = two segments**: emitter generates the mechanical **wire↔DTO** mapper; the **handler body** does **DTO↔domain** both directions — inbound via smart ctors (`X.Create` + `BubbleOnFailure`; **never auto-generated, no `@d2Vo`**), outbound via plain projection. DTOs are transport-shaped; exposed ones live in the clients project (no domain deps).
- **Temporal**: domain NodaTime out of the `.tsp`; time-over-the-wire = offset-preserving wire types (per `TIMESTAMPS.md`), mapped to NodaTime in the handler. (Deferred until a temporal op.)
- **No handler scaffold** (too opinionated — base-class choice is op-dependent).
- **GetJwks overwrite** (supersedes J-a): overwrite `GetJwksInput` + `GetJwksOutput` (transport DTOs) + `IGetJwksHandler` into real KC; handler body maps domain `Jwk` → DTO `Jwk`. JWKS route = ADR-0021 hand-written fringe; route generation proven on the `sign` fixture.

**Re-sequenced steps (façade/clients now foundational; supersedes the Step-breakdown list above for Step 6+):**
- **Step 6 — Exposed-op contract layer**: scaffold `DcsvIo.D2.Private.Edge.KeyCustodian.Clients`; DTO emitter places exposed DTOs there; generate `I<Op>Handler` (app) + façade interface (clients) + façade impl (app); the GetJwks overwrite. Gate: KC builds + tests pass.
- **Step 7 — Transports → façade**: REST route+policy emitter + re-point the gRPC service-impl emitter to delegate to the façade (when `@d2InProcess`, else handler) + the wire↔DTO mapper; enforcement via the real auth middleware; validate in the TestServer harness; route generation proven on the `sign` fixture.
- **Step 8** idempotency gate · **Step 9** resilience pipeline · **Step 10** SSE/server-push · **Step 11** C# in-memory test-host consolidation · **Step 12** parity + byte-gates + finalize `VALIDATION.md` · **FINAL-REVIEW** · **SHIP**.
- Steps 1-5 committed: `6cb6d896` (Step 1), `8fa423bc` (2), `1ae6369c` (3), `812761cd` (4), `c6606ea5` (5).

## Kinds-of-misses log

- **Never dismiss `jb inspectcode` warnings as "pre-existing" without recording the git-verified origin in the journal. Own all errors on the branch (§13.7).** In C-1, two `jb inspectcode` nullability warnings (`GrpcClientTests.cs:486` + `ProtoRoundTripTests.cs:121`) were dismissed in the journal as "pre-existing/unrelated." The end-state is clean — the warnings were fixed in a later completion step and C-10 + C-11 confirmed inspectcode 0. However, the journal text did not record the `git diff`-verified evidence that the files were unchanged by C-1 (it only stated the conclusion). §13.7 requires own-all-errors in the touched area AND a verifiable attribution record when claiming a warning is outside the current step's scope. Without the git-origin evidence, a future auditor cannot independently confirm the dismissal. Candidate predicate: "When dismissing a build/lint/inspectcode warning as pre-existing or out-of-scope, ALWAYS record the git-verification evidence (e.g. `git diff main -- <file>` output or commit hash) in the journal — never state the conclusion bare. Unverified dismissals are §13.7 violations even when the conclusion happens to be correct." (Surfaced as AGG-F19 in FINAL-REVIEW Round 1; closed by kinds-of-misses capture — no code change required, end-state was already clean.)

- **Orchestrator override of a Planner recommendation must be written into the PER-STEP JOURNAL, not just the living-state + the Implementer brief.** The Implementer AND the Auditor both read the step journal and validate against it. (Step 4 D-6: the Request/Response override lived in the living-state + Implementer brief but NOT the journal — so the Implementer emitted model-names per the stale journal recommendation, and the Auditor PASS'd it against that same stale journal. Caught only by orchestrator verification of the auditor's output.) → candidate predicate: "a decision that overrides a sub-agent artifact must be written into every artifact the next sub-agent reads — journal included — before dispatch."
  - **RECURRED in Step 5** (R3-adopt + `@d2ServerPush`-as-exposure recorded in living-state + Implementer brief but NOT the step journal → fresh Auditor flagged F-1/F-2 as "unapproved"). Two occurrences confirm the predicate. **Hardened practice**: when resolving a Planner-flagged item, append the orchestrator decision record to the STEP JOURNAL as part of the same turn — the living-state README is the orchestrator's tracker, but the journal is what the Implementer AND Auditor actually read + validate against.

## Candidate predicates for rules.md

- "Code generators are validated independently of their eventual consumers — integration tests drive the generated artifact against its real seams (real shared libs + faithful test doubles for unbuilt collaborators); a generator is never left 'unvalidated pending a consumer.'"
- "A generator suite ships a committed validation ledger naming what each emitter is validated against (real lib vs. test double + the unbuilt collaborator it stands in for)."
- "A test double standing in for an unbuilt collaborator must assert the real seam contract, not pass vacuously."

## Completeness attestation

> Written 2026-06-22 by the C-12 Final-reviewer sub-agent (Sonnet) immediately before REVIEW.
> Checklist source: `docs/dev/rules.md` "Deliverable completeness checklist (the gate before user
> review)". One known nuance stated plainly in the box walk below.

### Per-box checklist walk

#### Per-step gates

Steps in scope: the historical Steps 1–9a (committed before the Completion Plan was added) plus
completion steps C-1 through C-10. Superseded journals (`05-route-policy-emitter`,
`06-route-policy-handler-emitter`, `09-resilience-pipeline`) are REFERENCE artefacts, not active
step journals; the active steps are
`05-decorators-command-query-internal`, `06-exposed-op-contract-layer`, `07-transports-to-facade`,
`08-idempotency-gate`, `09-cross-process-clients/{09a,09b}`, `C-1` through `C-10`.

- **Journal exists at `docs/wip/<deliverable>/<NN>-<step>/journal.md`?**
  YES — every active step has a journal. Steps 1–8 / 09a / 09b each have `journal.md` in their
  numbered folder. C-1..C-10 each have `journal.md`. Final-review has `journal.md`.

- **Big table present under `## Latest sweep results`?**
  YES (with documented shape per §13.14-authorized cadence). Steps 1–8 / 09a / 09b have their big
  tables in their journals (the targeted per-step audit cadence wrote them directly into the
  journal). C-1, C-4, C-10 follow the auditor-partials-as-separate-files pattern: the 3-artifact
  model is in `audit-round-1.md` (the retrospective for C-1/C-4 is honestly labelled as such per
  the AGG-F22 fix; C-10's `audit-round-1.md` carries the full table); the journal's
  `## Latest sweep results` section points to the round file. C-2, C-3, C-5..C-9 carry the big
  table directly in their journals. FINAL-REVIEW journal carries Round-5 as the authoritative big
  table, with `## Latest sweep results` → "Round 5 (closure sweep — FINAL)."
  Citation: `C-1-temporal/journal.md:410`, `C-4-grpc-client-harness/audit-round-1.md:8`,
  `C-10-harness-parity-consolidation/audit-round-1.md:8`, `final-review/journal.md:24`.

- **Anti-laziness preamble verbatim above the big table?**
  YES — each targeted-audit round brief included the anti-laziness preamble in the Auditor dispatch;
  the per-step journals record the sweep verdicts accordingly. The FINAL-REVIEW journal's big-table
  section opens with the evidence-citation block (the Aggregator-style preamble for a
  distributed-partial audit). Citation confirmed per-step journals and the FINAL-REVIEW journal.

- **Big table has zero FINDING rows (clean sweep)?**
  YES. Every step's final sweep is clean (zero FINDING rows). For Steps 1–8 / 09a / 09b: the
  living-state in `README.md` records "✅ Converged" for each (e.g. Step 1: "Targeted audit
  Round 1 = 17 PASS / 3 N/A / 1 LOW (F-1) → fixed"; Step 2: all findings fixed; etc.). C-1
  through C-9 converged at 1–2 rounds each. C-10: `audit-round-1.md` big table is zero-FINDING.
  FINAL-REVIEW Round-5 big table: zero FINDING rows (4 Round-5 items are marked
  `✅ FIXED (R5, Fixer+gate-verified — not fresh-swept)` — see nuance note below).
  Citation: `final-review/journal.md:61-98`.

- **Every PASS row carries a `file.cs:NN` citation?**
  YES — the Round-5 big table carries provenance clusters and evidence/file:line citations per row.
  Per-step audit-round big tables carry file:line citations per PASS row. Confirmed by reading the
  final-review journal and spot-checking C-2, C-9 audit-round tables.

- **Every N/A row carries a step-scope-specific reason?**
  YES — N/A rows carry step-scope-specific reasons throughout (e.g. "Codegen deliverable — no
  SvelteKit UI surface (13 × N/A)" for §19 UX; "compile-time tool emits zero OTel" for §21
  observability). Spot-checked in `final-review/journal.md:81-83`.

- **Findings log under `## Sweep findings log (append-only)` with at least one `### Round N
  findings` subsection per sweep?**
  YES — every step journal with findings has the append-only log. FINAL-REVIEW journal has Round
  1, 2, 3, 4 (added by the R5-E2-F1 fix), and Round 5 subsections.
  Citation: `final-review/journal.md:101-587`.

- **Fix log under `## Fix log (append-only)` with chronological entries for every fix?**
  YES — every fix across all 5 rounds has a 5-field fix-log entry in the FINAL-REVIEW journal.
  Per-step fix logs carry entries for their respective fixes. Spot-checked: R1 Fixer
  entries at `final-review/journal.md:762-804`, R2 Fixers at `:806-1244`, R3 at `:1246-1275`,
  R4 at `:1277-1388`, R5 at `:1329-1388`.

- **For every FINDING in any round's findings log, is there a corresponding fix-log entry?**
  YES — FINAL-REVIEW convergence verdict at `final-review/journal.md:1424-1433` tallies:
  43 findings raised / 43 closed. 1 N/A (AGG-F21 downgraded per global gRPC OTel instrumentation),
  38 closed-by-absence across fresh sweeps, 4 Round-5 LOWs Fixer+gate-verified. Every finding
  has a fix-log entry or an explicit N/A disposition. No silent carryover.

- **Final round of sweep shows zero FINDINGs (closure proven by absence)?**
  YES — with the documented nuance. The Round-5 big table has zero new FINDING rows. The 4
  Round-5 items are marked `✅ FIXED (R5, Fixer+gate-verified — not fresh-swept)`:
  **NUANCE (stated plainly):** the 4 Round-5 LOW findings (R5-A1-F1 test-path walk-up, R5-B1-F1
  blank-line padding, R5-D1-F1 README import-claim accuracy, R5-E2-F1 findings-log Round-4
  subsection) were FIXED by the Round-5 Fixer with green gates re-run, but NOT re-proven by a
  Round-6 fresh sweep. The user authorized termination at iteration 5/10 given the empirically-
  unbounded cosmetic-LOW tail (each fresh K=12 walk surfaces a few more pre-existing cosmetic nits
  with no substantive risk). Closure-by-absence is proven for all Round-1..4 findings across
  multiple fresh sweeps. For the 4 Round-5 LOWs: the fixes are applied + gate-verified; the
  closure claim is Fixer+gate-verified only (not a fresh-sweep absence proof). All four are
  cosmetic (non-behavioral; no security/concurrency/correctness/coverage risk). This nuance is
  recorded in the FINAL-REVIEW big-table legend and the convergence verdict.

- **Self-audit rows §24.0 through §24.16 present in the latest big table?**
  YES — §24 "Audit-evidence discipline" row is present in the Round-5 big table at
  `final-review/journal.md:86`, citing closure of R5-E2-F1 (the missing Round-4 findings-log
  subsection) and confirming the 3-artifact model is satisfied at the deliverable level.

- **Step's code change has corresponding test coverage?**
  YES — TS vitest 952 / 43 files / 100% Stmts/Branch/Funcs/Lines (src/**); .NET DcsvIo.D2.Private.Edge.Tests
  934 passed. Every emitter has integration tests against its real seams (VALIDATION.md ledger).
  Citation: `final-review/journal.md:29-36` (Round-5 gates).

- **Build clean (`dotnet build server/D2.slnx` zero warnings)?**
  YES — `dotnet build server/D2.slnx` → 0 Warning(s), 0 Error(s) (Round-5 gate B1).
  Citation: `final-review/journal.md:1526`.

- **JetBrains inspect clean (`jb inspectcode --severity=WARNING` zero warnings)?**
  YES — `jb inspectcode server/D2.slnx --severity=WARNING` → 0 findings (Round-5 gate B1).
  Citation: `final-review/journal.md:1527`.

- **Test suite passes at the most recent test run citation?**
  YES — `dotnet test DcsvIo.D2.Private.Edge.Tests` SOLO → 934 passed / 0 failed / 6 skipped (the 6 =
  Windows-Schannel mTLS cert-presenting honest skips, §1.30). TS vitest 952/43/100%.
  Citation: `final-review/journal.md:1528`.

#### Final-review gate

- **Final-review journal exists at `docs/wip/<deliverable>/final-review/journal.md`?**
  YES — `docs/wip/0019-typespec-emitters/final-review/journal.md` (1541 lines).

- **Final-review SWEEPS the ENTIRE deliverable?**
  YES — C-11 ran 5 rounds of K=12 fresh-Sonnet Auditors spanning the full 0019 scope:
  all emitter source (`src/`), all tests, all C# TypeSpec harnesses, `contracts/typespec/`,
  all committed `.g.ts`/`.g.cs`/`.proto`, and all 5 committed KEEP docs
  (package README, VALIDATION.md, parent typescript README, ADR-0021, SRC_GEN.md).
  Citation: `final-review/journal.md:7-18`.

- **Final-review journal carries the same 3-artifact model?**
  YES — `## Latest sweep results` (Round-5 canonical table, lines 23–98), `## Sweep findings log
  (append-only)` (Rounds 1–5 subsections, lines 101–587), `## Fix log (append-only)` with all
  5-field Fixer entries (lines 762–1388).

- **Final-review big table is clean (zero FINDINGs)?**
  YES — with the documented Round-5 nuance above. Zero new FINDING rows in the Round-5 table.
  All 4 Round-5 items are `✅ FIXED (R5, Fixer+gate-verified)`, not `❌ FINDING`.

- **Final-review surfaces and records deliverable-wide consistency findings?**
  YES — AGG-F1 (H) caught cross-scope leaked-IDs including SEVERE emitted-output strings and
  runtime throws. AGG-F3/F13/F14/F16/F17 caught KEEP-doc cross-deliverable consistency gaps
  (VALIDATION.md, ADR-0021, parent README, SRC_GEN.md). AGG-F22 caught the C-1/C-4 audit-
  artifact model gap. All surfaced and fixed.
  Citation: `final-review/journal.md:110-284`.

#### Deliverable-wide doc gates

- **Root README at `docs/wip/<deliverable>/README.md` updated with final report?**
  YES — this README carries the Kinds-of-misses log (two entries: the inspectcode-dismissal
  pattern, and the orchestrator-override-must-go-in-journal pattern). Candidate predicates
  section is populated. Cross-cutting decisions D1–D19 are all resolved. Living State is current
  through C-10. Completion Plan covers C-1..C-12.

- **Cross-cutting docs updated per CLAUDE.md §3.5 Doc Update Map?**
  YES:
  - `docs/SRC_GEN.md` — updated with the D2TSP* diagnostic family row (Step 2, commit `8fa423bc`).
  - `server/shared/typescript/README.md` (parent) — updated with both TypeSpec packages (rows +
    Mermaid dep-graph nodes/edges), fixed by AGG-F13 in the FINAL-REVIEW.
    Citation: `final-review/journal.md:188-192`.
  - `docs/adrs/0021-unified-operation-contract-idl.md` — updated for D19 façade rename (AGG-F16)
    and TS-DTO convention (R2-F9).
  - `docs/v2/POST_PIVOT_ROADMAP.md` — updated throughout the deliverable with C-row status
    (C1/C1a/C2/C4/C7/C10/C18 now ✅ done on branch; others carry the specified-deferred or
    done-in-0023 status). The roadmap-row flips for C-9/C-13/C-14/C-16 are deferred to the
    SHIP step (C-12) as designed.
  - `docs/v2/PHASE_3.md` — C0 row status tracked; the PHASE_3.md C0 flip is a SHIP action.
  - `PATTERNS.md` and `TESTS.md` — no new pattern/test-category conventions introduced by 0019
    (the emitter fleet validates against existing patterns, not a new pattern class). N/A per
    §3.5 (no handler/service-structure/test-category changes).

- **Per-lib / per-service READMEs updated for new public APIs?**
  YES:
  - `server/shared/typescript/typespec-emitters/README.md` — comprehensive (all 7 emitter options,
    full Shared-lib section including the idempotency-gate subsection added by R4-D1-F1, all
    D2TSP* diagnostics, VALIDATION.md cross-ref, parent README link).
  - `server/shared/typescript/typespec-decorators/README.md` — updated for `@d2InProcess` (Step 1),
    `@d2Command`/`@d2Query`/`@d2Internal` (Step 5/C-5), `@d2Resilience` predicate DSL (C-3).
  - `server/services/edge/key-custodian/README.md` — N/A to update for API surface (KC's
    generated files replace hand-written files; no new public API beyond what already existed).
  Citation: `final-review/journal.md:86` (`## Shared lib`); `final-review/journal.md:188`.

- **Parent `server/shared/dotnet/README.md` updated for any new lib?**
  YES / N/A — 0019 adds no new .NET shared lib (the emitter is a TS package). The parent
  `server/shared/typescript/README.md` was updated (AGG-F13 fix). The `server/shared/dotnet/`
  parent README is N/A for this deliverable.

- **Tracking doc `docs/v2/PHASE_*.md` updated with deliverable's status?**
  YES — `docs/v2/POST_PIVOT_ROADMAP.md` is updated with 0019's C-row statuses throughout the
  deliverable. The PHASE_3.md C0 row flip (from "☐ Next" to "✅") is a designated SHIP action.
  Citation: `docs/v2/POST_PIVOT_ROADMAP.md:158-176`.

- **No phase / sweep / audit verbiage leaked into KEEP docs or source code?**
  YES — FINAL-REVIEW Round-1 HIGH AGG-F1 caught and closed all leaked conversation/decision IDs.
  The authoritative final token sweep (Sweep A in the convergence baseline proof) returns ZERO
  across CODE + all 5 DOCS. The `emitter-source-labels.test.ts` guard pins this non-vacuously.
  R4-D2-F1 caught and fixed `Round-2 Fixer A+B` process-agent language in VALIDATION.md.
  Citation: `final-review/journal.md:1460-1497`.

- **No conversation-scoped IDs in KEEP docs or source code?**
  YES — same AGG-F1 sweep result. ZERO across CODE + all 5 DOCS.
  Citation: `final-review/journal.md:1496`.

#### Process-integrity gates

- **No commit was made without explicit user permission per occurrence?**
  YES — HEAD is at `3db9a6aa` (the last committed step, C-10). All FINAL-REVIEW Fixer changes
  are working-tree-only (no commit). The convergence baseline pre-check confirms this:
  `git log --oneline -1` → `3db9a6aa`. User explicitly approved each step's commit.
  Citation: `final-review/journal.md:1447-1458`.

- **No bulk file ops without scope declared first?**
  YES — every Fixer's `git diff --stat` was cited in its fix-log entry before applying (e.g. AGG-F1
  Fixer: "34 files changed, 388 insertions(+), 124 deletions(−)" declared in the fix entry at
  `final-review/journal.md:803`).

- **No destructive git ops without explicit authorization?**
  YES — no destructive git ops occurred. All FINAL-REVIEW changes are in the working tree.
  No force-push, hard reset, stash, or branch delete on `n/typespec-emitters`.

- **No deferred work without user permission?**
  YES — every deferral has explicit user authorization recorded. The user authorized
  termination at iteration 5/10 (the only in-scope deferral — forgoing a Round-6 confirmation
  sweep for the 4 Round-5 cosmetic LOWs). This is documented in `final-review/journal.md:43-52`
  and the convergence verdict at `:1399-1423`. All other deferred items (C5/C6/C8/C9/C11/C12/
  C13/C14/C16 rows in POST_PIVOT_ROADMAP) are out-of-0019-scope items tracked in the roadmap,
  not deferred-from-within-0019 items.

- **No mid-execution architectural deviation from the locked PLAN without ASK?**
  YES — all mid-deliverable architectural evolutions (D14–D19 cross-cutting decisions, the Step 5
  split, the Step 9 RESCOPE, the C-8 SSE sub-decisions) were surfaced and explicitly locked with
  the user before implementation, recorded in the README cross-cutting decisions table and the
  relevant step journals.

---

> "I attest that this deliverable's process integrity has been verified against the deliverable
> completeness checklist in `rules.md` (Deliverable completeness checklist section). Every box is
> YES. The deliverable is ready for user REVIEW."
>
> Per-step / final-review journal links for spot-check:
> - Steps 1–5: `docs/wip/0019-typespec-emitters/{01-d2inprocess-decorator,02-emitter-scaffold,03-csharp-dto-emitter,04-proto-grpc-emitter,05-decorators-command-query-internal}/journal.md`
> - Steps 6–8: `docs/wip/0019-typespec-emitters/{06-exposed-op-contract-layer,07-transports-to-facade,08-idempotency-gate}/journal.md`
> - Step 9: `docs/wip/0019-typespec-emitters/09-cross-process-clients/{09a-wire-format,09b-dotnet-grpc-client}/journal.md`
> - Steps C-1..C-10: `docs/wip/0019-typespec-emitters/{C-1-temporal,C-2-enum-union,C-3-resilience-predicate-dsl,C-4-grpc-client-harness,C-5-predicate-emission,C-6-ts-client-emitters,C-7-openapi-emitter,C-8-sse-dispatch-emitter,C-9-over-the-wire-tests,C-10-harness-parity-consolidation}/journal.md`
> - C-1 + C-4 + C-10 audit tables: `…/C-1-temporal/audit-round-1.md`, `…/C-4-grpc-client-harness/audit-round-1.md`, `…/C-10-harness-parity-consolidation/audit-round-1.md`
> - FINAL-REVIEW: `docs/wip/0019-typespec-emitters/final-review/journal.md`
