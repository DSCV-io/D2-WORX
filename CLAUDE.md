# CLAUDE.md — D²-WORX Development Guide

**D²-WORX** — Microservices SaaS framework. C# 14 / .NET 10 backend, SvelteKit BFF (TypeScript 5.9 / Svelte 5). Pre-Alpha. PolyForm Strict license (reference implementation, non-commercial).

> **Architectural source of truth**: [V2.md](V2.md). This doc covers process, patterns, and code rules. V2.md covers what we're building.
> **Current execution state**: [PHASE_0.md](PHASE_0.md). Tracks the v1 → v2 wipe and Phase 0 work.

> **⚠️ MANDATORY: Every code change MUST follow the Development Workflow (§1). No exceptions.**

---

## §1. Development Workflow

This process applies to every change — bug fix, feature, refactor, or test. Follow it sequentially.

### Step 1: Research

Before writing any code, understand what you're changing and what it touches.

- Read CLAUDE.md so you know WHICH reference docs (§3) are relevant to the task
- Read the relevant `.md` docs for the areas being touched (§3 tells you which and when)
- Check [V2.md](V2.md) for architectural intent (§5 for stack decisions, §6 for code patterns, §7 for versioning)
- Find similar existing implementations
- Identify ALL affected files (`mcp__cclsp__find_references`, Grep, Glob)
- For historical v1 reference (patterns, prior decisions), check `/old/v1/D2-WORX/`
- **If uncertain → ASK. Do not guess. Do not assume. This is the #1 rule.**

### Step 2: Plan

**ALWAYS use Plan Mode** (`/plan` or `EnterPlanMode`) for planning. Plan Mode creates a persistent plan file that survives context compaction, keeps the plan visible throughout implementation, and allows iterative refinement before any code is written. Never plan inline in chat — always use the dedicated planning tool.

Design your approach before touching code. Plans must address:

- **Scope**: Files to create/modify
- **Pattern adherence**: Which V2.md §6 patterns apply? Identify explicitly. Note the correct TLC/2LC/3LC layer and operation verbiage.
- **Risks**: What could break? Side effects? Hard to reverse?
- **Test plan**: Happy path + adversarial cases (→ case coverage checklist in [docs/AUDIT_CHECKLIST.md](docs/AUDIT_CHECKLIST.md) and (when published) `docs/TESTS.md`)
- **i18n impact**: Does this change add or modify user-visible strings? This includes:
  - SvelteKit UI (Paraglide translations)
  - Backend handler messages (`D2Result` `messages` array — end users can see these)
  - Backend input errors (`D2Result` `inputErrors` — field-level errors shown in forms)
  - D2.Courier notification content (email/SMS templates)
  - If YES → add keys to ALL present locale files in `contracts/messages/`
- **Documentation**: Which `.md` files need updating?

### Step 3: Plan Review

Present the plan to the user. Iterate until approved. Do NOT start implementation until the plan is reviewed and approved.

### Step 4: Implement

Write code following §5 (Code Quality Rules) and §6 (Code Conventions).

- Track deviations from the plan — if something changes, note it
- Fix bugs/warnings immediately when discovered — anywhere in the project, not just in files you modified
- After editing TS code → check `mcp__cclsp__get_diagnostics`
- After editing .NET code → `dotnet build` (zero warnings) + `jb inspectcode` (zero warnings)

### Step 5: Verify (Definition of Done)

Every item MUST pass before a change is "done":

- [ ] **Builds clean** — zero warnings/errors:
  - `.NET`: `dotnet build server/D2.slnx` — zero StyleCop (SA****), CS**** warnings, null ref warnings
  - `.NET`: `jb inspectcode server/D2.slnx --severity=WARNING` — zero Rider/ReSharper warnings (see §2)
  - `SvelteKit`: `cd server/web && pnpm exec svelte-check`
- [ ] **Lint/style clean** — zero warnings:
  - `cd server/web && pnpm exec eslint .`
  - `cd server/web && pnpm exec prettier --check .`
  - (StyleCop is part of `dotnet build` above)
- [ ] **Tests pass** — existing tests still pass + new tests for new behavior
- [ ] **Pattern adherence** — code follows established patterns (V2.md §6), correct TLC/2LC/3LC structure
- [ ] **Zero tolerance** — ALL errors/warnings encountered anywhere in the project are fixed, not just in branch-modified files. If you see it, fix it.
- [ ] **i18n** — no hardcoded user-visible strings (UI, handler messages, input errors, notifications). All locale files in sync.
- [ ] **Documentation** — affected `.md` files updated
- [ ] **TS diagnostics** — `mcp__cclsp__get_diagnostics` clean for edited TS files
- [ ] **Container health** — if Docker Compose is running, verify affected containers are healthy (`docker compose --env-file .env.local --env-file .env.secrets ps`). Restart any containers that are unhealthy.

### Step 6: Report

After completing a task, briefly report:

1. What was completed
2. Any deviations from the plan
3. Any bugs found and fixed (or flagged)

---

## §2. Commands

> ⚠️ **DO NOT START SERVICES MANUALLY** — Never run `dotnet run`, `pnpm dev`, `pnpm preview`, or any long-running server directly. Services are managed by Docker Compose.
> E2E tests that self-manage their infrastructure (Testcontainers, child processes with cleanup) ARE allowed — they start and stop their own services.

**Docker Compose (service lifecycle):**

```bash
make up                                                                    # Start all services (detached)
make down                                                                  # Stop all services
docker compose -f infra/compose/compose.yml --env-file .env.local --env-file .env.secrets up -d      # Direct invocation
```

**Build:**

```bash
dotnet build server/D2.slnx                                                # Full .NET solution
dotnet build server/services/{service}/api/{service}.API.csproj            # Single project
cd server/web && pnpm install && pnpm exec svelte-check                    # SvelteKit type check
```

**Rider/ReSharper Inspections (.NET):**

```bash
# Full solution (WARNING+ severity, text output, no build — run after dotnet build)
jb inspectcode server/D2.slnx --severity=WARNING --format=Text --no-build --output=inspectcode.log && cat inspectcode.log

# Single project (faster — use during focused work)
jb inspectcode server/D2.slnx --project="Edge.App" --severity=WARNING --format=Text --no-build --output=inspectcode.log && cat inspectcode.log
```

These catch warnings that `dotnet build` does NOT surface: `[MustDisposeResource]` misuse, captured variable/closure issues, object initialization suggestions, and other JetBrains-specific inspections. Must be zero warnings.

**Test:**

```bash
# .NET (xUnit)
dotnet test server/D2.slnx                                                 # Full solution
dotnet test server/D2.slnx --filter Category=Unit                          # Unit only
dotnet test server/services/edge/tests                                      # Specific service

# SvelteKit
cd server/web && pnpm exec vitest run                                       # Unit tests (browser mode)
cd server/web && pnpm exec playwright test                                  # Playwright (mocked by default)
```

**Lint/Style:**

```bash
cd server/web && pnpm exec eslint .                                         # ESLint
cd server/web && pnpm exec prettier --check .                               # Prettier check
```

**Versioning (per V2.md §7):**

```bash
dotnet tool restore                                                        # First-time setup
dotnet versionize --dry-run                                                # Preview bump (always do this first)
dotnet versionize                                                          # Bump version + update CHANGELOG + tag
git push --follow-tags
```

**Important:** When editing shared `.NET` libs in `server/shared/dotnet/`, run `dotnet build server/D2.slnx` to verify all consumers still compile. SvelteKit changes are isolated — `cd server/web && pnpm exec svelte-check`.

---

## §3. Reference Documents

Read these docs BEFORE working in the relevant area. Each doc is the authority for its domain.

| Document | Summary | When to Read |
|---|---|---|
| [V2.md](V2.md) | Architectural source of truth — phasing, stack decisions per layer, code patterns (§6), versioning (§7), testing strategy (§8), wipe plan (§12) | **Always first** — before any architectural work |
| [PHASE_0.md](PHASE_0.md) | Wipe + Phase 0 execution tracking. Defines doc pass + per-lib + per-service placeholder READMEs. Archived once Phase 0 ships. | Reference during the wipe / Phase 0 work |
| [CONTRIBUTING.md](CONTRIBUTING.md) | Branch naming, conventional commits, PR process, license notice | PR preparation |
| [CHANGELOG.md](CHANGELOG.md) | Conventional-commits-driven (versionize). Don't hand-edit. | Reference only |
| [docs/AUDIT_CHECKLIST.md](docs/AUDIT_CHECKLIST.md) | Quality audit checklist — Security / Logic / Code Quality / Conventions / Cross-Service / Tests / Docs | Before merging substantial work |
| [docs/OPERATIONAL-GUARANTEES.md](docs/OPERATIONAL-GUARANTEES.md) | Idempotency, rate limiting, session consistency, RabbitMQ patterns, SAGA, multi-instance scaling | Any cross-service correctness work |
| (TBD) [docs/PATTERNS.md](docs/PATTERNS.md) | Distilled tribal knowledge — TLC/2LC/3LC, handler, D2Result, middleware, repo, cache, RedactionSpec, i18n. To be created in the doc pass per PHASE_0.md. | Planned — see PHASE_0.md |
| (TBD) [docs/TESTS.md](docs/TESTS.md) | 8-category adversarial Case Coverage Checklist. To be created in the doc pass per PHASE_0.md. | Planned — see PHASE_0.md |
| (TBD) [docs/MESSAGING.md](docs/MESSAGING.md) | Proto-canonical-JSON wire format, exchange naming, queue patterns, AMQP headers. To be created in the doc pass per PHASE_0.md. | Planned — see PHASE_0.md |
| (TBD) [docs/ARCHITECTURAL_PRINCIPLES.md](docs/ARCHITECTURAL_PRINCIPLES.md) | Cross-cutting principles. To be created in the doc pass per PHASE_0.md. | Planned — see PHASE_0.md |
| (TBD) [docs/SECURITY-RUNBOOKS.md](docs/SECURITY-RUNBOOKS.md) | KeyCustodian compromise runbooks. Filled in detail during Phase 3. | Planned — see PHASE_0.md |
| `/old/v1/D2-WORX/` | Frozen v1 snapshot. Historical reference for any v1 patterns / decisions / docs that don't have v2 equivalents yet. **Read-only — never modify.** | When researching how v1 did something or hunting for tribal knowledge not yet extracted |

Per-service / per-library `README.md` files appear in `server/services/{service}/` and `server/shared/dotnet/{lib}/` as those are built per V2.md §4 phases. Consult them when working on a specific service/lib.

---

## §4. Patterns & Architecture

**Rule: Follow existing patterns. Do not invent new ones when established patterns apply. If no pattern fits, ASK before inventing. Behavioral Guidelines (§7) apply to ALL work in this section — especially: ask when uncertain, research first, follow existing conventions.**

**Architectural intent for v2 lives in [V2.md](V2.md) §5 (Stack Decisions) and §6 (Library & Code Patterns). This section summarizes the operational rules.**

### TLC/2LC/3LC Folder Convention

Three-tier folder hierarchy for all backend code. TLC = architectural concern, 2LC = implementation type, 3LC = operation type. **3LC verbiage varies by layer:**

| TLC | 3LC Verbiage | Meaning |
|---|---|---|
| **CQRS** | `C/` Commands, `Q/` Queries, `U/` Utilities, `X/` Complex | Business operation intent |
| **Messaging** | `Pub/` Publishers, `Sub/` Subscribers | Message direction |
| **Repository** | `C/` Create, `R/` Read, `U/` Update, `D/` Delete | CRUD operation |
| **Caching** | `C/` Create, `R/` Read, `U/` Update, `D/` Delete | CRUD operation |

Interfaces live in `Interfaces/{TLC}/Handlers/{3LC}/`. Implementations live in `Implementations/{TLC}/Handlers/{3LC}/` (app layer) or `{TLC}/Handlers/{3LC}/` (infra layer).

### CQRS Handler Categories

| Type | Distributed Cache | DB Write | External API | Message Publish | Key Test |
|---|---|---|---|---|---|
| **Query** | No | No | No | No | "If the process dies after, would state persist?" → **No** |
| **Command** | Yes | Yes | Yes | Yes | Primary intent = mutation of persistent/shared state |
| **Complex** | Yes | Yes | Yes | Yes | Primary intent = retrieval, but may mutate as side effect |

Local/in-memory caching is always OK (instance-scoped, ephemeral — doesn't affect other instances).

### Verb Semantics

- **Find** = "Resolve this for me" — may fetch from external source, may cache/persist. Example: `FindWhoIs`
- **Get** = "Give me this by ID" — direct lookup, read-only. Example: `GetWhoIsByIds`

### Handler Pattern

`.NET`: `BaseHandler<TSelf, TInput, TOutput>` with using aliases (`H`, `I`, `O`), `IHandlerContext`, `DefaultOptions` override. Per-handler PII redaction via the `[RedactData]` attribute on data types (KEEP — see V2.md §6.10) + `DefaultOptions` overrides for proto-generated DTOs that can't carry the attribute.

### D2Result Pattern

Result objects replace exceptions for control flow. **Always use semantic factories** — never raw `Fail()` with manual status codes when a factory exists. Available: `Ok`, `Created`, `NotFound`, `Unauthorized`, `Forbidden`, `ValidationFailed`, `Conflict`, `ServiceUnavailable`, `UnhandledException`, `PayloadTooLarge`, `Cancelled`, `SomeFound`. Raw `Fail` only when no factory matches (e.g., re-mapping arbitrary upstream status codes).

Partial success: `NOT_FOUND` (none found) → `SOME_FOUND` (partial, data returned) → `OK` (all found).

### Partial Interface Extension

Interfaces are `partial`, split by operation. `ICommands.cs` (base) + `ICommands.DoSomething.cs` (per-handler). One file per operation for discoverability.

### DI Registration

`.NET`: `services.AddTransient<IXxx, Xxx>()` via `Microsoft.Extensions.DependencyInjection`. Each layer exports `AddXxx(services)` extension method.

### Other Established Patterns

- **Options pattern**: `IOptions<T>` with defaults. Config section per V2.md §5. Never hardcode batch sizes or cache expirations.
- **Multi-tier caching** (in client libraries): Memory → Redis → Database → Disk. Populate upward on miss. Key convention: `EntityName:{id}`.
- **Content-addressable entities**: `Location` and `WhoIs` use SHA-256 hash IDs (64-char hex). Factory method computes hash. Enables dedup.
- **Mappers**: C# 14 extension members: `extension(Entity e) { public DTO ToDTO() { ... } }`. Live in `{Service}.App/Mappers/`.
- **Batch operations**: `input.HashIds.Chunk(_BATCH_SIZE)` via Options pattern (default 500).
- **Health checks must use the same code path as production** — DB health checks go through EF Core, not raw `pool.query()`. A check that bypasses the ORM won't detect ORM-layer issues.

### Key Architecture Decisions (per V2.md §5)

- **Auth**: self-rolled .NET auth as a module within Edge (V2.md §5.4). RFC 8693 token exchange + RFC 6749 §4.4 client_credentials for service identity. JWKS at the OIDC-canonical `/.well-known/jwks.json`.
- **JWT**: RS256 only. 15min expiry. Custom claims namespaced with `d2:` prefix.
- **KeyCustodian**: module within Auth — owns lifecycle of ALL long-lived secrets (JWKS, message payload encryption keys, cookie signing, service-identity client_secrets). State machine + JWKS-style overlap rotation. See V2.md §5.4.
- **SvelteKit BFF**: pure SSR. Browser → Edge directly for auth state mutations. Server-side route guards (`requireAuth`, `requireOrg`, etc.) at `server/web/src/lib/server/auth/`. Browser-side `authClient` at `server/web/src/lib/client/auth/`. NOT separate packages.
- **Sync**: gRPC between services (HTTP/2). **Async**: RabbitMQ for side effects (emails, events). Sensitive RMQ payloads encrypted via `D2.Shared.Encryption` (V2.md §5.7).
- **Notifications**: ALL deliveries through D2.Courier → contact resolution. No direct emails/texts.
- **Sessions**: 3-tier (cookie cache 5min → Redis → PostgreSQL `auth_db` dual-write).
- **Database topology**: one PG server, many DBs (auth_db, files_db, courier_db, notifications_db, audit_db, seaweedfs_filer_db, plus per-service contacts DBs). Multi-replica migration safety via PG advisory locks.
- **Object storage**: SeaweedFS for user files (V2.md §5.6). MinIO retained as backend for LGTM block storage.
- **Production deployment**: eventually Docker Swarm + Portainer; pre-launch is Compose on a VPS (V2.md §5.9).

---

## §5. Code Quality Rules

**These rules are mandatory — not suggestions. Behavioral Guidelines (§7) are equally binding: ask when uncertain, never leave broken things behind, always write adversarial tests. Violations of §5 or §7 are equally unacceptable.**

### Cross-Platform (.NET + SvelteKit)

- **D2Result semantic factories**: Never raw `Fail()` with manual `statusCode` when a factory exists. See list in §4.
- **`[RedactData]` on PII types**: Every data type carrying PII (emails, phones, IPs, addresses, names, message content, filenames, presigned URLs) MUST have the `[RedactData]` attribute (per V2.md §6.10). Lives on the type, applies to ALL Serilog logging recursively, reflection-cached. Don't reach for per-handler RedactionSpec when `[RedactData]` does the job.
- **Input validation on all handlers**: Validate inputs BEFORE infrastructure calls (FluentValidation .NET / equivalent). Never let Redis/DB be the first to reject invalid data.
- **Build warnings = bugs**: Fix ALL warnings — StyleCop (SA****), CS**** (null refs, hiding), ESLint, `svelte-check`. Never suppress with `#pragma warning disable`, `!` (for silencing warnings), or `@ts-ignore`.
- **Lint/style warnings = bugs**: ESLint and Prettier must be zero warnings.
- **Zero tolerance for warnings/errors**: Fix ALL errors and warnings encountered anywhere in the project — not just in branch-modified files. Never dismiss as "pre-existing." If you see it during your work, fix it. Every session leaves the codebase cleaner.
- **Tests are adversarial**: Happy path + garbage input + boundary values + cross-field deps + error propagation + idempotency + concurrency. Full checklist → [docs/AUDIT_CHECKLIST.md](docs/AUDIT_CHECKLIST.md) "Test Coverage" + (planned) `docs/TESTS.md` 8-category checklist.
- **Verify DI registration when adding handlers** — missing registrations are silent at compile time and only crash at runtime. After creating a handler, immediately add its registration in the corresponding extension method.
- **Never return `Ok()` after a branching operation unconditionally** — if a nested handler or provider can fail, check its result. Returning `Ok()` after a try/catch that swallows failures is almost always a bug. Either `BubbleFail` or explicitly handle the error.
- **Auth flags initialize to `null`, not `false`** — `IsAuthenticated`, `IsTrustedService`, `IsUserImpersonating` use `bool?`. `null` = "not yet determined" (pre-auth). `false` = "confirmed not." Never treat `null` as `false` in logic.
- **Domain model is source of truth for nullability** — if a domain field is optional, the proto field MUST use the `optional` keyword. Never rely on `""`, `0`, or `false` as "not set" sentinels.
- **Proto3 `optional` keyword for all nullable fields** — proto3 defaults strings to `""`, numbers to `0`, bools to `false`. Without `optional`, receivers cannot distinguish "not provided" from the zero value. Required fields (IDs, keys, status) stay as plain (non-optional).
- **No empty strings as data** — `""` must NEVER represent absent/missing data. Use `null` (C#) or `undefined` (TS). The ONLY acceptable uses of `""` are: Svelte form field `bind:value` initialization, string concatenation building, `string.Empty` in C# hash/fingerprint computation (where null would break), and OTel span attributes (SDK requires non-null). At all other boundaries (user input, DB, proto mapping), convert empty strings: TS `truthyOrUndefined()`, C# `.ToNullIfEmpty()`.
- **NEVER hand-write database migrations** — use `dotnet ef migrations add <Name>`. Do NOT manually create or edit migration `.cs` files, snapshot files (`*ModelSnapshot.cs`), or `__EFMigrationsHistory` rows. Hand-writing puts EF Core's internal model snapshot out of sync with the actual schema. If the generator fails, STOP and ask. **Multi-replica safety**: startup migrator acquires PG advisory lock per V2.md §5.6 — only one replica migrates, others wait.
- **Don't create patterns**: Follow existing ones (§4 + V2.md §6). If no pattern fits, ask before inventing.
- **Don't leave broken things behind**: Fix ALL issues you encounter in the project — not just in files you touched. Every session leaves the codebase cleaner.

### C#

- **Falsey()/Truthy() handle null**: Never `if (value is null || value.Falsey())`. Just `if (value.Falsey())`. After early return, use `value!` — the value is guaranteed non-null. This is one of the few valid uses of `!`.
- **`string.Empty`**: Always. Never `""`. (StyleCop SA1122)
- **`ToNullIfEmpty()` at boundaries** — use `.ToNullIfEmpty()` when converting proto/DB/external strings to domain types. Returns `null` if the string is null, empty, or whitespace-only (trims first). Defined in `D2.Shared.Utilities`.
- **Nullable types for optional domain fields** — use `string?`, `bool?`, `int?`, `DateTime?` for optional fields. Never `= string.Empty` on optional record properties. `null` = "not provided."
- **C# 14 extension members**: `extension(T target) { ... }` — NOT old `this T` parameter style.
- **File headers**: Required on all `.cs` files (see §6).
- **Record types for entities**: `record`, `required init`, empty collection initializers (`[]`).
- **Field prefixes**: `_camelCase` (mutable), `r_camelCase` (readonly), `s_camelCase` (static), `sr_camelCase` (static readonly). Full table → §6.
  - **Carve-out**: handlers using **primary constructors** per V2.md §6.1 omit the `r_` prefix on constructor parameters (they're not fields, they're params accessed directly). The carve-out applies ONLY to handler primary-constructor parameters. Regular fields keep their prefixes.
- **XML docs**: Required for public APIs.
- **Implement the interface**: Handlers MUST implement their interface for DI registration.
- **`ValueTask` must not be awaited more than once** — call `.AsTask()` once, store the `Task` reference, reuse it for `Task.WhenAll()` and subsequent `await`.
- **`Random.Shared`** — never `new Random()` in static/singleton contexts. `Random.Shared` is thread-safe.
- **`[MustDisposeResource]`** (JetBrains.Annotations): `true` = caller is responsible for disposal (factory methods returning `IDisposable`). `false` = framework/DI manages lifetime (DI-injected services, `IHostedService` subclasses, test fixtures with `IAsyncLifetime`). Apply to classes, constructors, and factory methods as appropriate.
- **Rider inspections are NOT optional**: `jb inspectcode` catches warnings invisible to `dotnet build` — `[MustDisposeResource]` misuse, captured variable/closure issues, `AccessToModifiedClosure`, `AccessToDisposedClosure`. Run after `dotnet build` and fix all warnings.

### TypeScript / SvelteKit

- **Strict mode**: Always enabled.
- **Type imports**: `import type { ... }` for type-only imports.
- **ESM only**: SvelteKit is `"type": "module"`.
- **Prefer `undefined` over `null`** — `undefined` is JS's native "absent" value. Use optional syntax (`field?: string`) instead of `field: string | null`. Exception: explicit three-state semantics for pre-auth flags (`boolean | null`).
- **`truthyOrUndefined()` at boundaries** — use when converting user input, DB rows, or proto values to domain types. Returns `undefined` if the string is null, empty, or whitespace-only (trims first).
- **Zod schemas use `.optional()` not `.nullable()`** — since domain types use `?: T` (undefined), Zod schemas must use `.optional()`. Never `.nullable()` or `.nullish()` for domain-aligned validation.
- **After editing**: Check `mcp__cclsp__get_diagnostics`. Fix type errors and missing imports immediately.

### SvelteKit (BFF)

- **i18n everywhere**: ALL user-visible strings MUST use Paraglide translations (`m.key_name()` from `$lib/paraglide/messages.js`). Includes `<title>`, meta tags, OG tags, headings, labels, placeholders, error messages. Never hardcode — not even for dev/debug pages.
- **i18n is NOT just frontend**: Backend handler messages (`D2Result.messages`), input errors (`D2Result.inputErrors`), and notification content (D2.Courier) also use translation keys from `contracts/messages/`. End users can see ALL of these. When adding i18n keys, consider all consumers.
- **Adding translation keys**: Add to ALL present locale files (`contracts/messages/*.json`). They MUST stay in sync. Run Paraglide compile from `server/web/` for frontend keys.
- **New pages MUST include** in `<svelte:head>`: translated `<title>`, `<meta name="description">`, OG tags (`og:title`, `og:description`, `og:type="website"`), `noindex` if not indexable.
- **`resolve()` from `$app/paths`**: Only typed pathnames. Query strings appended separately: `` `${resolve("/path")}?key=value` ``.
- **Never write bare `href="/path"` or `goto("/path")`** — always wrap with `resolve("/path")` from `$app/paths`. Without this, i18n locale routing breaks for non-default locales.
- **Client-side telemetry must never include PII** — Faro user identity is limited to `userId` + `username`. Never email, real name, or contact details.
- **REST client modules own all fetch calls** — never use raw `fetch` outside of `*-client.ts` files in `$lib/client/rest/`. Components and pages call client functions, not `fetch("/api/...")` directly. Clients handle headers, credentials, timeouts, and D2Result parsing in one place.
- **Skeleton loading states** — every component that displays async or server-loaded data must show a `<Skeleton>` placeholder until the data is ready.

### Security (New Endpoints)

Full checklist → [docs/AUDIT_CHECKLIST.md](docs/AUDIT_CHECKLIST.md) "Security" section. Key points:

- **IDOR prevention** — derive org/user scope from session/claims, never from user-supplied input. Endpoints must NEVER accept userId, orgId, or role as request parameters when those values are available from the session/JWT.
- **Pagination limits** — default 50, max 100 on all list queries
- **DB constraint errors** — catch PG `23505` → 409 Conflict, not 500
- **Auth middleware visible at route declaration** — `.RequireAuth()`, `.RequireServiceKey()`, `.RequireOrg()`
- **New JWT claims** → custom claims MUST be namespaced with `d2:` per V2.md §5.4 (`act["d2:kind"]`, `d2:session_id`, etc.). Document in `docs/JWT-CLAIMS.md` (created at Phase 3).
- **No sensitive IDs in JWT** — admin user IDs, internal audit data stays server-side (session only)
- **API key comparisons must be constant-time** — `CryptographicOperations.FixedTimeEquals`. Plain `==` is vulnerable to timing attacks.
- **Auth middleware must fail-closed on missing config** — empty service-identity client mappings or missing secrets = 401 immediately. Never silently bypass.
- **Sign-out must clear ALL auth state**: server session via Auth, in-memory JWT invalidation, SvelteKit `invalidateAll()` for data loaders.
- **CORS `allowHeaders` must include every custom `X-D2-*` header** any middleware reads — when adding a new header, verify CORS allows it. Missing = preflight blocks the request.
- **Multi-column key lookups must use paired predicates** — `(col1=A AND col2=1) OR (col1=B AND col2=2)`. Independent `OR`s produce cross-product false positives.
- **Infrastructure paths must be exempt from ALL business middleware** — use shared `InfrastructurePaths.IsInfrastructure()`. Never add a new infra path bypass to only one middleware.

---

## §6. Code Conventions

### C# Naming

| Element | Convention | Example |
|---|---|---|
| Classes/Records/Interfaces | `PascalCase` | `GetReferenceData` |
| Methods/Properties | `PascalCase` | `HandleAsync` |
| Private instance fields | `_camelCase` | `_memoryCache` |
| Private readonly instance fields | `r_camelCase` | `r_getFromMem` |
| Private static fields | `s_camelCase` | `s_instance` |
| Private static readonly fields | `sr_camelCase` | `sr_activitySource` |
| Static readonly (non-private) | `SR_PascalCase` | `SR_ActivitySource` |
| Private constants | `_UPPER_CASE` | `_BATCH_SIZE` |
| Public/Internal constants | `UPPER_CASE` | `MAX_ATTEMPTS` |
| Local constants (tests) | `snake_case` | `expected_count` |
| Local variables | `camelCase` | `result` |

**Primary-constructor handlers (V2.md §6.1)**: Constructor parameters do NOT take the `r_` prefix — they're parameters, not fields, even though they're accessed like fields inside the class body. The carve-out applies ONLY to handler primary-constructor parameters; regular fields keep their prefixes.

### C# File Header (required on all .cs files)

```csharp
// -----------------------------------------------------------------------
// <copyright file="FileName.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------
```

### TypeScript Naming (SvelteKit BFF)

- `camelCase` for variables/functions
- `PascalCase` for types/classes/interfaces/components
- `kebab-case` for modules/files

### Folder Casing Convention (per V2.md §2)

- **Folders OUTSIDE a project** (csproj-grouping, organizational) → **lowercase**, kebab-case for multi-word: `server/`, `services/`, `edge/`, `app/`, `clients/`, `dotnet/`, `caching-redis/`, `geo-reference/`, `service-defaults/`, `infra/`, `tools/`, `docs/`
- **Folders INSIDE a project** (namespace-mapping, where Rider auto-creates folders from namespace operations) → **PascalCase**: `Implementations/`, `Interfaces/`, `CQRS/`, `Handlers/`, `C/`, `Q/`, `U/`, `X/`, `Repository/`, `Messaging/`
- **`.cs` file names** → **PascalCase** (matches the type they contain — one-class-per-file)
- **`.csproj` file names** → **PascalCase**, dot-separated (`D2.Shared.Handler.csproj`) — the csproj filename IS the assembly name
- **Namespaces and type names** → **PascalCase** (C# language convention)

The rule: **if Rider auto-generates a folder from a namespace operation, that folder must be PascalCase. Anything else is lowercase.**

### Scope vs Permission Terminology (per V2.md §5.4)

**These are the same thing in our model.** JWT carries them as the OAuth-canonical `scope` claim (space-separated string). Code references them as constants in `D2.Shared.Auth.Scopes` (a static class with `const string` members). The string values match exactly.

We use **"scope"** as the primary term throughout because it's universal in OAuth/OIDC specs and tooling, it's the name of the JWT claim, and every off-the-shelf library and policy attribute speaks "scope." "Permission" survives only in informal/conversational usage; in code and docs the term is "scope."

### Translation Key Conventions

- Auth pages: `auth_{feature}_{purpose}` (e.g., `auth_sign_in_title`)
- App pages: `webclient_app_{page}_{purpose}` (e.g., `webclient_app_profile_title`)
- Design/demo/debug: `webclient_{section}_{purpose}` (e.g., `webclient_debug_session_title`)
- Common UI/errors: `common_ui_*` / `common_errors_*`
- Backend handler messages: Use `common_errors_*` keys where possible
- Reuse existing keys where they match

### Observability

All logs and spans MUST include these fields for cross-service correlation:

| Field | Source | Purpose |
|---|---|---|
| traceId | `IRequestContext.traceId` (auto via BaseHandler) | End-to-end request tracing |
| correlationId | `Idempotency-Key` / RabbitMQ header | Async message tracking |
| userId | JWT `sub` claim / session | User audit trail |
| orgId | JWT `org` claim / session | Multi-tenant context |
| service | `OTEL_SERVICE_NAME` | Service origin |

### Git

- **Branch naming**: `feat/...`, `fix/...`, `docs/...`, `refactor/...`, `test/...`, `infra/...`, `chore/...`, `ci/...`
- **Commits**: Conventional commits with scope: `feat(edge): add primary locales`
- **No `Co-Authored-By` lines** in commit messages (enforced by `.husky/commit-msg` hook — will reject if present)
- **Markdown tables**: Aligned columns for plain-text readability

### Documentation

- Every project/module has a corresponding `README.md` (`server/services/{service}/README.md`, `server/shared/dotnet/{lib}/README.md`)
- Update docs as part of completing a feature — it's part of the definition of done
- When adding new handlers, entities, config options, or public APIs → update the relevant `README.md`

---

## §7. Behavioral Guidelines

> **⚠️ These guidelines are MANDATORY — equally binding as §4 (Patterns) and §5 (Code Quality). They are not aspirational. Every item below MUST be followed on every task. Violations here are treated the same as a build failure or a broken test.**

1. **ALWAYS ask when uncertain** — Non-negotiable. Do not guess. Do not assume. If requirements, approach, or tradeoffs are unclear — **ask**. Every time.
2. **Read freely** — Explore any files needed for context.
3. **Ask before changing** — Do not modify files without explicit user approval.
4. **Research first** — Check related files (tests, interfaces, existing implementations) before proposing changes.
5. **Follow existing conventions** — V2.md §5/§6 documents the v2 patterns. `/old/v1/D2-WORX/` is reference for any historical patterns not yet captured in v2 docs.
6. **Never leave broken things behind** — Fix ALL issues in files you touch.
7. **Always write tests** — Adversarial, not just happy-path. Every behavioral change needs coverage.
8. **Check [V2.md](V2.md)** — For current phase, status, and resolved decisions.
9. **Provide options** — When multiple approaches exist, present them for user decision.
10. **Maximize parallelization** — Spawn as many sub-agents as makes sense to complete tasks as fast as possible. Independent work (file reads, doc updates, code fixes, test runs, audits) should run in parallel, not sequentially. Use background agents for non-blocking work. The user values speed — don't serialize work that can be parallelized.
11. **Never defer work without explicit permission** — Do NOT unilaterally decide to defer, skip, or deprioritize any planned work. If you think something should be deferred, **ASK the user first** and present the tradeoff. If the user approves deferral, **document it** in PHASE_0.md (or successor) as a tracked issue with rationale. Any work item that is deferred for any reason MUST appear as a documented issue.
12. **Never commit without explicit permission** — Do NOT create git commits unless the user explicitly asks you to commit. Present changes for review first. Committing without permission is as serious as pushing without permission.

### Code Intelligence Tools

**TypeScript**: Use `mcp__cclsp__*` tools (`get_hover`, `find_definition`, `find_references`, `find_workspace_symbols`, `get_diagnostics`). The built-in `LSP` tool's `workspaceSymbol` works but `hover`/`documentSymbol` return empty results.

**C#**: `csharp-ls` via built-in `LSP` tool — `workspaceSymbol` works, diagnostics flow automatically, but `hover`/`documentSymbol` time out (30s limit on large solution). Fall back to Grep/Glob/Read.

Before renaming or changing a function signature, use `find_references` to find all call sites first. Use Grep/Glob for text/pattern searches (comments, strings, config values) where LSP doesn't help.

After writing or editing TS code, check `mcp__cclsp__get_diagnostics` before moving on. Fix type errors and missing imports immediately.

After writing or editing .NET code, run `dotnet build server/D2.slnx` (zero warnings) AND `jb inspectcode server/D2.slnx --severity=WARNING` (zero warnings). The two tools catch different issues — Roslyn analyzers vs JetBrains inspections. Both must be clean.

### Windows LSP Workaround

Edit `~/.claude/plugins/marketplaces/claude-plugins-official/.claude-plugin/marketplace.json`: Change `"command"` to `"cmd"` with `"args": ["/c", "<binary>", ...originalArgs]` for `typescript-language-server`, `csharp-ls`, `gopls`. **Must reapply after `claude plugin marketplace update`** — the update overwrites the file.

### Project Structure

See [V2.md](V2.md) §2 for the full v2 tree layout. Key roots:

- `contracts/` — proto source of truth + i18n message files + fixtures
- `server/` — all trusted code (.NET services + SvelteKit BFF + .NET shared libs)
- `infra/` — deployment + observability (compose, docker, observability)
- `tools/` — dev tooling (scripts, generators)
- `docs/` — non-essential project documentation (audit checklist, operational guarantees, planned PATTERNS.md / TESTS.md / etc.)
- `secrets/` — gitignored + Claude-deny-ruled key material (root key, encryption keys, dev TLS certs). Populated by `tools/scripts/gen-dev-keys.sh`.
- `.claude/` — project-level Claude Code settings (`settings.json` with deny rules per V2.md §12)
- `old/v1/D2-WORX/` — frozen v1 snapshot, read-only reference

---

## §8. Local Secrets & Claude Deny Rules

Per V2.md §12, environment configuration is split:

| File | Contents | Committed? | Claude can read? | Claude can edit? |
|---|---|---|---|---|
| `.env.local` | Non-secret config — service URLs, ports, log levels, feature flags, CORS origins | No (gitignored) | **Yes** | **Yes** |
| `.env.local.example` | Template with safe defaults | **Yes** | Yes | Yes |
| `.env.secrets` | Real third-party creds — Twilio, Resend, IPinfo, OAuth client secrets, prod-like DB passwords | No (gitignored) | **No (deny-ruled)** | **No (deny-ruled)** |
| `.env.secrets.example` | Template with placeholder values like `TWILIO_AUTH_TOKEN=replace_with_real_value` | **Yes** | Yes | Yes |
| `secrets/` | Key material — root key, dev encryption keys, dev TLS certs | No (gitignored, populated by `tools/scripts/gen-dev-keys.sh`) | **No (deny-ruled)** | **No (deny-ruled)** |

Compose loads both env files (`.env.local` first, `.env.secrets` second so secrets override placeholders if any collision):

```yaml
services:
  edge:
    env_file:
      - .env.local
      - .env.secrets
```

**Workflow when adding a new secret**:
1. Edit `.env.secrets.example` adding `NEW_THING_API_KEY=replace_with_real_value`
2. Update `infra/compose/compose.yml` to load it into the right service
3. Tell the operator: "Added `NEW_THING_API_KEY` — copy into `.env.secrets`, set the real value, restart the service"
4. Operator manually syncs (Claude cannot edit `.env.secrets` — deny rule)

Same pattern for encryption keys: update `tools/scripts/gen-dev-keys.sh` to generate keys for new domains; operator runs the script; output lands in `secrets/`.

The deny rules live in `.claude/settings.json` (committed). The exact-match `**/.env.secrets` deliberately does NOT match `.env.secrets.example` — the template file remains fully editable.

**Behavioral rule**: never `Grep` the `secrets/` directory or `.env.secrets` file by name. If a secret accidentally enters context (runtime output, grep match), STOP and tell the operator immediately so they can rotate the exposed value.
