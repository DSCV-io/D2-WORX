# CLAUDE.md — D²-WORX Development Guide

**D²-WORX** — Microservices SaaS framework. C# 14 / .NET 10, TypeScript 5.9, Svelte 5. Pre-Alpha. PolyForm Strict license (reference implementation, non-commercial).

> **⚠️ MANDATORY: Every code change MUST follow the Development Workflow (§1). No exceptions.**

---

## §1. Development Workflow

This process applies to every change — bug fix, feature, refactor, or test. Follow it sequentially.

### Step 1: Research

Before writing any code, understand what you're changing and what it touches.

- Read the ENTIRE CLAUDE.md so you know WHICH reference docs (§3) are relevant to the task
- Read the relevant `.md` docs for the areas being touched (§3 tells you which and when)
- Find similar existing implementations (§4 has the patterns)
- Check [PLANNING.md](PLANNING.md) for current phase, resolved ADRs, status
- Identify ALL affected files (`findReferences`, Grep, Glob)
- Check existing utilities before creating helpers ([`D2.Shared.Utilities`](backends/dotnet/shared/Utilities/) / [`@d2/utilities`](backends/node/shared/utilities/))
- **If uncertain → ASK. Do not guess. Do not assume. This is the #1 rule.**

### Step 2: Plan

**ALWAYS use Plan Mode** (`/plan` or `EnterPlanMode`) for planning. Plan Mode creates a persistent plan file that survives context compaction, keeps the plan visible throughout implementation, and allows iterative refinement before any code is written. Never plan inline in chat — always use the dedicated planning tool.

Design your approach before touching code. Plans must address:

- **Scope**: Files to create/modify
- **Pattern adherence**: Which established patterns apply (§4)? Identify explicitly. Note the correct TLC/2LC/3LC layer and operation verbiage.
- **Risks**: What could break? Side effects? Hard to reverse?
- **Test plan**: Happy path + adversarial cases (→ case coverage checklist in [TESTS.md](backends/dotnet/shared/Tests/TESTS.md))
- **i18n impact**: Does this change add or modify user-visible strings? This includes:
  - SvelteKit UI (Paraglide translations)
  - Backend handler messages (`D2Result` `messages` array — end users can see these)
  - Backend input errors (`D2Result` `inputErrors` — field-level errors shown in forms)
  - Comms service notification content (email/SMS templates via `contracts/messages/`)
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

- [ ] **Builds clean** — zero warnings/errors on ALL affected platforms:
  - `.NET`: `dotnet build` — zero StyleCop (SA\***\*), CS\*\*** warnings, null ref warnings
  - `.NET`: `jb inspectcode` — zero Rider/ReSharper warnings (see §2 for command)
  - `Node.js @d2/*`: `pnpm --filter @d2/xxx exec tsc` (full build if consumers need `dist/`)
  - `SvelteKit`: `pnpm --filter d2-sveltekit exec svelte-check`
- [ ] **Lint/style clean** — zero warnings:
  - `pnpm lint` — ESLint across all TS/JS/Svelte
  - `pnpm format:check` — Prettier formatting
  - (StyleCop is part of `dotnet build` above)
- [ ] **Tests pass** — existing tests still pass + new tests for new behavior
- [ ] **Pattern adherence** — code follows established patterns (§4), correct TLC/2LC/3LC structure
- [ ] **Zero tolerance** — ALL errors/warnings encountered anywhere in the project are fixed, not just in branch-modified files. If you see it, fix it.
- [ ] **i18n** — no hardcoded user-visible strings (UI, handler messages, input errors, notifications). All locale files in sync.
- [ ] **Documentation** — affected `.md` files updated
- [ ] **TS diagnostics** — `mcp__cclsp__get_diagnostics` clean for edited TS files
- [ ] **Container health** — if Docker Compose is running, verify affected containers are healthy (`docker compose --env-file .env.local ps`). Restart any containers that are unhealthy or have stale code (`docker compose --env-file .env.local restart <service>`). Changes to shared packages, dependencies, or service code can leave containers in a broken state until restarted.

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
make up                                             # Start all services (detached)
make down                                           # Stop all services
docker compose --env-file .env.local up -d          # Start with explicit env file
docker compose --env-file .env.local down           # Stop with explicit env file
```

**Build:**

```bash
dotnet build                                        # Full .NET solution
pnpm --filter @d2/xxx exec tsc                      # Single Node.js package (emits dist/)
pnpm --filter @d2/xxx exec tsc --noEmit             # Type-check only (no dist/ output)
```

> ⚠️ **NEVER run `dotnet build` or `dotnet restore` on the host while `d2-geo`, `d2-gateway`, or `d2-signalr` are running.** All three .NET dev containers run `dotnet watch run` against `./backends/dotnet` mounted live; a host build writes to the same shared `obj/` and races with the containers' parallel restore. Symptoms: NETSDK1064, `Handler.csproj.nuget.g.targets already exists`, all 3 containers crash, Cloudflare 502 / browser CORS failures on `https://t-d2-ws.dcsv.io`. Build inside the container instead (`docker exec d2-geo dotnet build /src/backends/dotnet/services/Geo/Geo.API`), or stop all 3 .NET containers first, build, clean `obj/` + `bin/`, then sequential restart `geo → gateway → signalr` with health waits between.

**Rider/ReSharper Inspections (.NET):**

```bash
# Full solution (WARNING+ severity, text output, no build — run after dotnet build)
jb inspectcode D2.sln --severity=WARNING --format=Text --no-build --output=inspectcode.log && cat inspectcode.log

# Single project (faster — use during focused work)
jb inspectcode D2.sln --project="Geo.App" --severity=WARNING --format=Text --no-build --output=inspectcode.log && cat inspectcode.log
```

These catch warnings that `dotnet build` does NOT surface: `[MustDisposeResource]` misuse, captured variable/closure issues, object initialization suggestions, and other JetBrains-specific inspections. Must be zero warnings.

**Test:**

```bash
# Node.js (Vitest 4.x — run from repo root)
pnpm vitest run                                     # All test projects
pnpm vitest run --project shared-tests              # Specific project
pnpm vitest run --project auth-tests                # Auth service tests
pnpm --filter @d2/auth-bff-client exec vitest run   # Package-local tests (bff-client)

# .NET (xUnit)
dotnet test                                         # Full solution
dotnet test --filter FindWhoIs                      # Specific test filter

# SvelteKit
pnpm --filter d2-sveltekit exec vitest run          # Unit tests (browser mode)
pnpm --filter d2-sveltekit exec playwright test     # Mocked Playwright tests

# E2E (full-stack, self-contained via Testcontainers)
pnpm vitest run --project e2e-tests                 # API-level E2E tests
cd backends/node/services/e2e && npx playwright test  # Browser E2E tests
```

**Lint/Style:**

```bash
pnpm lint                                           # ESLint (all TS/JS/Svelte)
pnpm format:check                                   # Prettier check
```

**Important:** After modifying a `@d2/*` package's source, always `tsc` (full build, not `--noEmit`) so `dist/` is updated. Consumers import from `dist/` — stale output causes silent runtime failures.

---

## §3. Reference Documents

Read these docs BEFORE working in the relevant area. Each doc is the authority for its domain. The summary tells you enough to know whether you need the full doc.

| Document                                                                                                                   | Summary                                                                                                                                                                                                   | When to Read                                    |
| -------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------- |
| [PLANNING.md](PLANNING.md)                                                                                                 | Current phase, ADRs (1-18), resolved decisions, implementation status, open issues, roadmap                                                                                                               | **Always first** — before any task              |
| [BACKENDS.md](backends/BACKENDS.md)                                                                                        | TLC/2LC/3LC folder convention, handler categories (Q/C/U/X), layer-specific verbiage, DI registration, Node.js workspace, package dependency graph, Dkron jobs                                            | Any backend work (services, handlers, repos)    |
| [.NET HANDLER.md](backends/dotnet/shared/Handler/HANDLER.md)                                                               | BaseHandler pattern, IHandlerContext, HandlerOptions, OTel metrics (4), IRequestContext field tables, implementation example with using aliases                                                           | Adding/modifying any .NET handler               |
| [Node.js HANDLER.md](backends/node/shared/handler/HANDLER.md)                                                              | BaseHandler, RedactionSpec, validateInput (Zod), HandlerOptions, OTel metrics, ambient context (AsyncLocalStorage), IRequestContext fields, createServiceScope, implementation + interface + DI examples  | Adding/modifying any Node.js handler            |
| [.NET RESULT.md](backends/dotnet/shared/Result/RESULT.md)                                                                  | D2Result factory methods (12+), BubbleFail/Bubble error propagation, CheckSuccess/CheckFailure pattern matching, best practices                                                                           | .NET error handling, result patterns            |
| [Node.js RESULT.md](backends/node/shared/result/RESULT.md)                                                                 | D2Result factory methods (camelCase), bubbleFail/bubble, checkSuccess/checkFailure, error codes table, retry helpers (retryResultAsync/retryExternalAsync), transient detection                           | Node.js error handling, result patterns         |
| [.NET TESTS.md](backends/dotnet/shared/Tests/TESTS.md)                                                                     | Case coverage checklist (8 categories), test naming conventions, form/endpoint testing patterns, frameworks per platform                                                                                  | Writing any tests                               |
| [Node.js TESTING.md](backends/node/shared/testing/TESTING.md)                                                              | Custom Vitest matchers (7), PostgresTestHelper (Testcontainers), test project architecture, vitest monorepo setup, createTestContext pattern                                                              | Node.js test infrastructure, Vitest setup       |
| [GEO_SERVICE.md](backends/dotnet/services/Geo/GEO_SERVICE.md)                                                              | Geo service architecture — domain model, caching strategy, validation, DB design. **Primary reference implementation**                                                                                    | Understanding established service structure     |
| [GEO_CLIENT.md](backends/dotnet/services/Geo/Geo.Client/GEO_CLIENT.md)                                                     | Client library pattern: inter-service gRPC calls, multi-tier caching (Mem→Redis→DB→Disk), singleflight dedup, circuit breaker, DI registration, usage examples                                            | Client library work, caching, gRPC calls        |
| [AUTH.md](backends/node/services/auth/AUTH.md)                                                                             | Auth architecture (BetterAuth + Hono), sessions (3-tier: cookie→Redis→PG), JWT (RS256/JWKS), request flow (Hybrid C), SvelteKit proxy, S2S trust, secure endpoint checklist, cookie signing               | Any auth, security, session, or JWT work        |
| [AUTH_BFF_CLIENT.md](backends/node/services/auth/bff-client/AUTH_BFF_CLIENT.md)                                            | SvelteKit BFF auth client: SessionResolver, JwtManager, AuthProxy, route guards (`requireAuth`, `requireOrg`, `redirectIfAuthenticated`), cookie signing, config                                          | SvelteKit auth hooks, SSR session, route guards |
| [JWT_AUTH.md](backends/node/shared/implementations/middleware/jwt-auth/default/JWT_AUTH.md)                                | JWT validation middleware for Hono: JWKS provider, RS256 verification, fingerprint check, IRequestContext population, middleware factory                                                                  | JWT auth for public-facing Node.js services     |
| [AUTH_POLICY.md](backends/node/shared/implementations/middleware/auth-policy/default/AUTH_POLICY.md)                       | Hono route-level policy middleware (`requireAuth`, `requireOrg`, `requireRole`, etc.). Mirrors .NET `AuthPolicy.Default`. Reads identity from `c.var.requestContext`                                      | Adding a route-level auth gate on Node          |
| [JWT_AUTH.md (.NET)](backends/dotnet/shared/Implementations/Middleware/JwtAuth.Default/JWT_AUTH.md)                        | .NET JWT Bearer middleware: JWKS retrieval, fp claim binding, `AddJwtAuth`/`UseJwtAuth` extensions. Mirrors `@d2/jwt-auth`                                                                                | JWT auth on .NET gateway / SignalR              |
| [SERVICE_KEY.md (.NET)](backends/dotnet/shared/Implementations/Middleware/ServiceKey.Default/SERVICE_KEY.md)               | .NET S2S API key middleware: `X-Api-Key` validation (constant-time), `IsTrustedService` flag, `.RequireServiceKey()` endpoint filter. Mirrors `@d2/service-key`                                           | S2S authentication on .NET                      |
| [AUTH_POLICY.md (.NET)](backends/dotnet/shared/Implementations/Middleware/AuthPolicy.Default/AUTH_POLICY.md)               | .NET route-level policy extensions (`.RequireAuth`, `.RequireOrg`, `.RequireRole`, etc.). Wraps named `AuthPolicies` + builds parameterized policies inline. Mirrors `@d2/auth-policy`                    | Adding a route-level auth gate on .NET          |
| [AUTH_APP.md](backends/node/services/auth/app/AUTH_APP.md)                                                                 | Auth CQRS handlers (18): sign-in events, throttle, emulation consent, org contacts, user contacts, 4 job handlers. Repository interfaces, DI registration, service keys                                   | Auth handler work, auth DI, org contacts        |
| [COMMS.md](backends/node/services/comms/COMMS.md)                                                                          | Comms service architecture: delivery engine (RabbitMQ), in-app notifications, conversational messaging (threads), entity model, channel resolution, retry topology, SignalR gateway, client library usage | Any notification, messaging, or delivery work   |
| [COMMS_CLIENT.md](backends/node/services/comms/client/COMMS_CLIENT.md)                                                     | Thin RabbitMQ publisher client: `Notify` handler with universal message shape, DI registration (`addCommsClient`), Auth caller example, fire-and-forget via fanout exchange                               | Sending notifications from any service          |
| [COMMS_APP.md](backends/node/services/comms/app/COMMS_APP.md)                                                              | Comms CQRS handlers (7): Deliver orchestrator, RecipientResolver, channel preferences, 2 job handlers. Provider interfaces, repository bundles, DI registration                                           | Comms handler work, delivery pipeline           |
| [FILES.md](backends/node/services/files/FILES.md)                                                                          | Files service architecture — context keys, access control, MinIO topology, processing pipeline, gRPC callbacks, phasing                                                                                   | Understanding the Files service as a whole      |
| [FILES_DOMAIN.md](backends/node/services/files/domain/FILES_DOMAIN.md)                                                     | Files domain: File entity, FileVariant, VariantConfig, content type rules, status state machine, messaging constants, error codes                                                                         | Files service domain model, business rules      |
| [FILES_APP.md](backends/node/services/files/app/FILES_APP.md)                                                              | Files app layer: 11 CQRS handlers (6C/4Q/1U), 35 service keys, context key config, provider/repo/outbound/realtime interfaces, DI registration                                                            | Files handler work, upload/processing pipeline  |
| [FILES_INFRA.md](backends/node/services/files/infra/FILES_INFRA.md)                                                        | Files infra: 8 repo handlers (Drizzle), 7 S3 storage, ClamAV scanning, Sharp processing, 2 gRPC outbound, SignalR push, 2 consumers, Drizzle schema                                                       | Files infrastructure implementations            |
| [SignalR.md](backends/dotnet/gateways/SignalR/SignalR.md)                                                                  | SignalR real-time gateway: authenticated hub, channel-based routing, gRPC push API, Redis backplane, JWT via query param                                                                                  | SignalR gateway work, realtime push             |
| [REQUEST_ENRICHMENT.md](backends/dotnet/shared/Implementations/Middleware/RequestEnrichment.Default/REQUEST_ENRICHMENT.md) | IP resolution (CF-Connecting-IP→X-Real-IP→X-Forwarded-For→RemoteIp), fingerprinting (server/client/device SHA-256), WhoIs lookup, IRequestContext population, config                                      | Middleware, request context, fingerprinting     |
| [RATE_LIMIT.md](backends/dotnet/shared/Implementations/Middleware/RateLimit.Default/RATE_LIMIT.md)                         | Sliding window approximation algorithm, 4 dimensions (fingerprint 100/min, IP 5K, city 25K, country 100K), fail-open, country whitelist, trusted service bypass, config                                   | Rate limiting changes                           |
| [IDEMPOTENCY.md](backends/dotnet/shared/Implementations/Middleware/Idempotency.Default/IDEMPOTENCY.md)                     | Idempotency-Key header, Redis SET NX with sentinel, response caching, edge cases table, Node.js orchestrator, config                                                                                      | Idempotency middleware                          |
| [CONTRIBUTING.md](CONTRIBUTING.md)                                                                                         | Branch naming, conventional commits, PR process, license notice                                                                                                                                           | PR preparation                                  |

---

## §4. Patterns & Architecture

**Rule: Follow existing patterns. Do not invent new ones when established patterns apply. If no pattern fits, ASK before inventing. Behavioral Guidelines (§7) apply to ALL work in this section — especially: ask when uncertain, research first, follow existing conventions.**

### TLC/2LC/3LC Folder Convention

Three-tier folder hierarchy for all backend code. TLC = architectural concern, 2LC = implementation type, 3LC = operation type. **3LC verbiage varies by layer:**

| TLC            | 3LC Verbiage                                              | Meaning                   |
| -------------- | --------------------------------------------------------- | ------------------------- |
| **CQRS**       | `C/` Commands, `Q/` Queries, `U/` Utilities, `X/` Complex | Business operation intent |
| **Messaging**  | `Pub/` Publishers, `Sub/` Subscribers                     | Message direction         |
| **Repository** | `C/` Create, `R/` Read, `U/` Update, `D/` Delete          | CRUD operation            |
| **Caching**    | `C/` Create, `R/` Read, `U/` Update, `D/` Delete          | CRUD operation            |

Interfaces live in `Interfaces/{TLC}/Handlers/{3LC}/`. Implementations live in `Implementations/{TLC}/Handlers/{3LC}/` (app layer) or `{TLC}/Handlers/{3LC}/` (infra layer). Full details → [BACKENDS.md](backends/BACKENDS.md)

### CQRS Handler Categories

| Type        | Distributed Cache | DB Write | External API | Message Publish | Key Test                                                   |
| ----------- | ----------------- | -------- | ------------ | --------------- | ---------------------------------------------------------- |
| **Query**   | No                | No       | No           | No              | "If the process dies after, would state persist?" → **No** |
| **Command** | Yes               | Yes      | Yes          | Yes             | Primary intent = mutation of persistent/shared state       |
| **Complex** | Yes               | Yes      | Yes          | Yes             | Primary intent = retrieval, but may mutate as side effect  |

Local/in-memory caching is always OK (instance-scoped, ephemeral — doesn't affect other instances).

### Verb Semantics

- **Find** = "Resolve this for me" — may fetch from external source, may cache/persist. Example: `FindWhoIs`
- **Get** = "Give me this by ID" — direct lookup, read-only. Example: `GetWhoIsByIds`

### Handler Pattern

`.NET`: `BaseHandler<TSelf, TInput, TOutput>` with using aliases (`H`, `I`, `O`), `IHandlerContext`, `DefaultOptions` override. Implementation example → [.NET HANDLER.md](backends/dotnet/shared/Handler/HANDLER.md)

`Node.js`: `BaseHandler<Input, Output>` with `implements` interface, `override get redaction()`, `executeAsync`. Implementation + interface examples → [Node.js HANDLER.md](backends/node/shared/handler/HANDLER.md)

### D2Result Pattern

Result objects replace exceptions for control flow. **Always use semantic factories** — never raw `Fail()` with manual status codes when a factory exists. Available: `Ok`, `Created`, `NotFound`, `Unauthorized`, `Forbidden`, `ValidationFailed`, `Conflict`, `ServiceUnavailable`, `UnhandledException`, `PayloadTooLarge`, `Cancelled`, `SomeFound`. Raw `Fail` only when no factory matches (e.g., re-mapping arbitrary upstream status codes).

Partial success: `NOT_FOUND` (none found) → `SOME_FOUND` (partial, data returned) → `OK` (all found). Full examples → [RESULT.md](backends/dotnet/shared/Result/RESULT.md)

### Partial Interface Extension

Interfaces are `partial`, split by operation. `ICommands.cs` (base) + `ICommands.DoSomething.cs` (per-handler). One file per operation for discoverability. Full example → [BACKENDS.md](backends/BACKENDS.md) § Extension Pattern

### DI Registration

`.NET`: `services.AddTransient<IXxx, Xxx>()`. `Node.js`: `@d2/di` ([`backends/node/shared/di/`](backends/node/shared/di/)) `ServiceCollection` with `ServiceKey<T>` branded tokens as DI keys. Each layer exports `addXxx(services)`. Full examples → [BACKENDS.md](backends/BACKENDS.md) § Handler Registration

### Other Established Patterns

- **Options pattern**: `IOptions<T>` with defaults. Config section = `SERVICE_LAYER` (e.g., `"GEO_APP"`, `"GATEWAY_AUTH"`). Never hardcode batch sizes or cache expirations.
- **Multi-tier caching**: Memory → Redis → Database → Disk. Populate upward on miss. Key convention: `EntityName:{id}`. → [GEO_CLIENT.md](backends/dotnet/services/Geo/Geo.Client/GEO_CLIENT.md)
- **Content-addressable entities**: `Location` and `WhoIs` use SHA-256 hash IDs (64-char hex). Factory method computes hash. Enables dedup.
- **Mappers**: C# 14 extension members: `extension(Entity e) { public DTO ToDTO() { ... } }`. Live in `ServiceName.App/Mappers/`.
- **Batch operations**: `input.HashIds.Chunk(_BATCH_SIZE)` via Options pattern (default 500).
- **Health checks must use the same code path as production** — DB health checks go through the ORM (Drizzle/EF Core), not raw `pool.query()`. A check that bypasses the ORM won't detect ORM-layer issues.

### Key Architecture Decisions

- **Auth**: BetterAuth (Node.js + Hono). **NO Keycloak** — removed, do not reference. → [AUTH.md](backends/node/services/auth/AUTH.md)
- **JWT**: RS256 only. **NO EdDSA**. JWKS at `/api/auth/jwks`. 15min expiry. → [AUTH.md](backends/node/services/auth/AUTH.md)
- **SvelteKit auth**: Proxy pattern (`/api/auth/*` → Auth Service). Cookie sessions for browser ↔ SvelteKit.
- **Dependencies**: ALL npm versions pinned exactly. No `^`, no `~`. Enforced by [`.npmrc`](.npmrc).
- **DI**: `@d2/di` mirrors .NET `IServiceCollection/IServiceProvider`. `ServiceKey<T>` as DI tokens. → [BACKENDS.md](backends/BACKENDS.md)
- **Sync**: gRPC between services (HTTP/2). **Async**: RabbitMQ for side effects (emails, events).
- **Notifications**: ALL deliveries through `@d2/comms-client` → Comms service → Geo contact resolution. No direct emails/texts. → [COMMS.md](backends/node/services/comms/COMMS.md), [COMMS_CLIENT.md](backends/node/services/comms/client/COMMS_CLIENT.md)
- **Sessions**: 3-tier (cookie cache 5min → Redis → PostgreSQL dual-write). → [AUTH.md](backends/node/services/auth/AUTH.md)
- **SvelteKit BFF auth**: `@d2/auth-bff-client` — SessionResolver, JwtManager, AuthProxy, route guards. → [AUTH_BFF_CLIENT.md](backends/node/services/auth/bff-client/AUTH_BFF_CLIENT.md)

---

## §5. Code Quality Rules

**These rules are mandatory — not suggestions. Behavioral Guidelines (§7) are equally binding: ask when uncertain, never leave broken things behind, always write adversarial tests. Violations of §5 or §7 are equally unacceptable.**

### Cross-Platform

- **D2Result semantic factories**: Never raw `Fail()` with manual `statusCode` when a factory exists. See list in §4.
- **RedactionSpec on PII handlers**: Every handler touching PII (emails, phones, IPs, addresses, names, message content, filenames, presigned URLs) MUST declare a `RedactionSpec`. Applies to BOTH app AND repo handlers — each `BaseHandler` independently logs its I/O. Redact actual PII and secrets (displayName, presignedUrl, email, IP), NOT opaque identifiers (userId, orgId, relatedEntityId — these are UUIDs, not PII). Use `suppressOutput: true` when output contains nested PII (e.g., File objects with displayNames).
- **Input validation on all handlers**: Node.js = Zod via `this.validateInput()`. .NET = FluentValidation/DataAnnotations. All string fields need max length.
- **Build warnings = bugs**: Fix ALL warnings — StyleCop (SA\***\*), CS\*\*** (null refs, hiding), ESLint, `svelte-check`. Never suppress with `#pragma warning disable`, `!` (for silencing warnings), or `@ts-ignore`.
- **Lint/style warnings = bugs**: `pnpm lint` (ESLint) and `pnpm format:check` (Prettier) must be zero warnings.
- **Zero tolerance for warnings/errors**: Fix ALL errors and warnings encountered anywhere in the project — not just in branch-modified files. Never dismiss as "pre-existing." If you see it during your work, fix it. Every session leaves the codebase cleaner.
- **Tests are adversarial**: Happy path + garbage input + boundary values + cross-field deps + error propagation + idempotency + concurrency. Full checklist → [TESTS.md](backends/dotnet/shared/Tests/TESTS.md).
- **Validate inputs BEFORE infrastructure calls** — never let Redis/DB be the first to reject invalid data. Call `this.validateInput(schema, input)` (Node.js) or validate with FluentValidation (.NET) at the TOP of `executeAsync`, before any downstream calls. → [AUTH_APP.md](backends/node/services/auth/app/AUTH_APP.md) § Handler Implementation Patterns
- **RedactionSpec covers automatic I/O logging only** — any `this.context.logger.*` calls inside `executeAsync()` must be manually reviewed. Never log fields that appear in your `inputFields`/`outputFields` redaction list via manual log calls. → [AUTH_APP.md](backends/node/services/auth/app/AUTH_APP.md) § Handler Implementation Patterns, [Node.js HANDLER.md](backends/node/shared/handler/HANDLER.md)
- **Verify DI registration when adding handlers** — missing registrations are silent at compile time and only crash at runtime. After creating a handler, immediately add its registration in the corresponding `registration.ts` / `Extensions.cs`.
- **Never return `ok()` after a branching operation unconditionally** — if a nested handler or provider can fail, check its result. Returning `ok()` after a try/catch that swallows failures is almost always a bug. Either `bubbleFail` or explicitly handle the error.
- **Cross-platform enum/constant changes in one commit** — when renaming `OrgType`, `Role`, or any enum stored as text in the database, update BOTH .NET and Node.js simultaneously. Mismatches are data integrity bugs.
- **Auth flags initialize to `null`, not `false`** — `isAuthenticated`, `isTrustedService`, `isOrgEmulating`, `isUserImpersonating` on `IRequestContext` use `boolean | null` (.NET: `bool?`). `null` = "not yet determined" (pre-auth). `false` = "confirmed not." Never treat `null` as `false` in logic.
- **Domain model is source of truth for nullability** — if a domain field is optional, the proto field MUST use the `optional` keyword. Never rely on `""`, `0`, or `false` as "not set" sentinels. See per-platform rules below.
- **Proto3 `optional` keyword for all nullable fields** — proto3 defaults strings to `""`, numbers to `0`, bools to `false`. Without `optional`, receivers cannot distinguish "not provided" from the zero value. Every field that is nullable in the domain model (`string?` in C#, `field?: string` in TS) MUST be `optional` in the `.proto` definition. Required fields (IDs, keys, status) stay as plain (non-optional).
- **No empty strings as data** — `""` must NEVER represent absent/missing data. Use `null` (C#) or `undefined` (TS). The ONLY acceptable uses of `""` are: Svelte form field `bind:value` initialization, string concatenation building, `string.Empty` in C# hash/fingerprint computation (where null would break), and OTel span attributes (SDK requires non-null). At all other boundaries (user input, DB, proto mapping), convert empty strings: TS `truthyOrUndefined()`, C# `.ToNullIfEmpty()`.
- **NEVER hand-write database migrations** — applies to ALL stacks (Drizzle / EF Core / Flyway / etc.). Always use the framework's migration generator (`pnpm db:generate` for Drizzle, `dotnet ef migrations add <Name>` for EF Core). Do NOT manually create or edit migration SQL/`.cs` files, snapshot files (`meta/{N}_snapshot.json`, `*ModelSnapshot.cs`), journal files, or invent timestamps. Hand-writing puts the framework's internal model snapshot out of sync with the actual schema, and arbitrary timestamps poison the runtime migrator (e.g., Drizzle's `migrate()` silently skips any migration whose journal `when` ≤ the latest applied `created_at`; EF Core compares against `__EFMigrationsHistory` similarly). One bad timestamp blocks every subsequent migration. If the generator fails (Windows pnpm symlink rotation for Drizzle, missing design-time factory for EF Core, etc.), STOP and ask. Do not patch by hand. The Drizzle recovery on Windows is `pnpm install` on host → `pnpm db:generate` → `docker compose up -d --force-recreate d2-node-init`.
- **Don't create patterns**: Follow existing ones (§4). If no pattern fits, ask before inventing.
- **Don't leave broken things behind**: Fix ALL issues you encounter in the project — not just in files you touched. Every session leaves the codebase cleaner.

### C#

- **Falsey()/Truthy() handle null**: Never `if (value is null || value.Falsey())`. Just `if (value.Falsey())`. After early return, use `value!` — the value is guaranteed non-null. This is one of the few valid uses of `!`.
- **`string.Empty`**: Always. Never `""`. (StyleCop SA1122)
- **`ToNullIfEmpty()` at boundaries** — use `.ToNullIfEmpty()` when converting proto/DB/external strings to domain types. Returns `null` if the string is null, empty, or whitespace-only (trims first). Prevents empty strings from polluting domain models. Defined in `D2.Shared.Utilities.Extensions.StringExtensions`.
- **Nullable types for optional domain fields** — use `string?`, `bool?`, `int?`, `DateTime?` for optional fields. Never `= string.Empty` on optional record properties. `null` = "not provided."
- **C# 14 extension members**: `extension(T target) { ... }` — NOT old `this T` parameter style.
- **File headers**: Required on all `.cs` files (see §6).
- **Record types for entities**: `record`, `required init`, empty collection initializers (`[]`).
- **Field prefixes**: `_camelCase` (mutable), `r_camelCase` (readonly), `s_camelCase` (static), `sr_camelCase` (static readonly). Full table → §6.
- **XML docs**: Required for public APIs.
- **Implement the interface**: Handlers MUST implement their interface for DI registration.
- **`ValueTask` must not be awaited more than once** — call `.AsTask()` once, store the `Task` reference, reuse it for `Task.WhenAll()` and subsequent `await`.
- **`Random.Shared`** — never `new Random()` in static/singleton contexts. `Random.Shared` is thread-safe.
- **`[MustDisposeResource]`** (JetBrains.Annotations): `true` = caller is responsible for disposal (factory methods returning `IDisposable`). `false` = framework/DI manages lifetime (DI-injected services, `IHostedService` subclasses, test fixtures with `IAsyncLifetime`). Apply to classes, constructors, and factory methods as appropriate. Audit existing usage when touching disposable types.
- **Rider inspections are NOT optional**: `jb inspectcode` catches warnings invisible to `dotnet build` — `[MustDisposeResource]` misuse, captured variable/closure issues, `AccessToModifiedClosure`, `AccessToDisposedClosure`. Run after `dotnet build` and fix all warnings. Use `// ReSharper disable once AccessToModifiedClosure` (or `AccessToDisposedClosure`) only for intentional test patterns where the closure capture is by design.

### TypeScript / Node.js

- **Strict mode**: Always enabled.
- **Type imports**: `import type { ... }` for type-only imports.
- **Error handling**: `@d2/result` (D2Result) — same semantics as .NET.
- **ESM only**: All packages `"type": "module"`.
- **Prefer `undefined` over `null`** — `undefined` is JS's native "absent" value. Use optional syntax (`field?: string`) instead of `field: string | null` or `field: string | undefined`. The ONLY exception is `IRequestContext` auth flags which use `boolean | null` for three-state pre-auth semantics. Never use `null` for "not provided" in domain entities — use `undefined` (omit the field).
- **`truthyOrUndefined()` at boundaries** — use `truthyOrUndefined()` from `@d2/utilities` when converting user input, DB rows, or proto values to domain types. Returns `undefined` if the string is null, empty, or whitespace-only (trims first). Prevents empty strings from polluting domain models.
- **Zod schemas use `.optional()` not `.nullable()`** — since domain types use `?: T` (undefined), Zod schemas must use `.optional()`. Never `.nullable()` or `.nullish()` for domain-aligned validation.
- **After editing**: Check `mcp__cclsp__get_diagnostics`. Fix type errors and missing imports immediately.
- **After modifying @d2/\* source**: Full `tsc` build (not `--noEmit`) so `dist/` is updated. Stale output = silent runtime failures.
- **Drizzle UPDATE/DELETE must chain `.returning()`** — check the result array. Empty = row didn't exist → return `notFound()`, not `ok()`. → [AUTH_INFRA.md](backends/node/services/auth/infra/AUTH_INFRA.md) § Repository Handler Patterns
- **`.d.ts` files in `src/` are NOT emitted to `dist/`** — module augmentations (`declare module`) and ambient declarations must be inside `.ts` source files, not standalone `.d.ts`.
- **Non-TS assets (SQL migrations, JSON fixtures) not copied by `tsc`** — use path construction relative to `src/` at runtime, not `import.meta.dirname` which resolves to `dist/`.

### SvelteKit

- **i18n everywhere**: ALL user-visible strings MUST use Paraglide translations (`m.key_name()` from `$lib/paraglide/messages.js`). Includes `<title>`, meta tags, OG tags, headings, labels, placeholders, error messages. Never hardcode — not even for dev/debug pages.
- **i18n is NOT just frontend**: Backend handler messages (`D2Result.messages`), input errors (`D2Result.inputErrors`), and comms notification content also use translation keys from [`contracts/messages/`](contracts/messages/). End users can see ALL of these. When adding i18n keys, consider all consumers.
- **Adding translation keys**: Add to ALL present locale files ([`contracts/messages/*.json`](contracts/messages/)). They MUST stay in sync. Run Paraglide compile from [`clients/web/`](clients/web/) for frontend keys.
- **New pages MUST include** in `<svelte:head>`: translated `<title>`, `<meta name="description">`, OG tags (`og:title`, `og:description`, `og:type="website"`), `noindex` if not indexable.
- **`resolve()` from `$app/paths`**: Only typed pathnames. Query strings appended separately: `` `${resolve("/path")}?key=value` ``.
- **Never write bare `href="/path"` or `goto("/path")`** — always wrap with `resolve("/path")` from `$app/paths`. Without this, i18n locale routing breaks for non-default locales. → [clients/web/README.md](clients/web/README.md) § Navigation & resolve()
- **Client-side telemetry must never include PII** — Faro user identity is limited to `userId` + `username`. Never email, real name, or contact details.
- **Playwright screenshots**: Save to [`clients/web/screenshots/`](clients/web/screenshots/), never project root.
- **REST client modules own all fetch calls** — never use raw `fetch` outside of `*-client.ts` files in `$lib/client/rest/`. Components and pages call client functions (`updateName()`, `removeAvatar()`, etc.), never `fetch("/api/...")` directly. Clients handle headers (CSRF `Content-Type`), credentials, timeouts, and D2Result parsing in one place.
- **Real-time session refresh via SignalR** — when user data changes (name, username, avatar), the backend invalidates BetterAuth's Redis session cache (via `InvalidateUserSessionCache` command) then pushes a `user:updated` event via SignalR. The root layout listener calls `get-session?disableCookieCache=true` → `invalidateAll()` to refresh all components reactively. Components NEVER call `invalidateAll()` themselves after mutations — they rely on the SignalR event. Pattern: `invalidateCache → then pushSignalR`, always chained, always fire-and-forget. Resolve DI handlers upfront (before async chains) to avoid scope disposal issues.
- **Skeleton loading states** — every component that displays async or server-loaded data must show a `<Skeleton>` placeholder until the data is ready. Use a `loaded` flag (set by `$effect` after data sync) rather than checking `!user` (which is never null with SSR). For async resources like presigned URLs, initialize the loading flag from the data: `let avatarLoading = $state(!!user.image)` so SSR renders the skeleton immediately. Match skeleton dimensions to the actual controls (`h-5` for labels, `h-9` for inputs, `rounded-full` for avatars).
- **Avatar `{#key}` blocks** — shadcn's `Avatar.Root` caches internal "image loaded" state. When the image URL changes (upload, remove), wrap `Avatar.Root` in `{#key url}` to force re-mount so the Fallback renders correctly. Apply to all avatar locations (profile uploader, header menu, mobile nav).
- **DI service keys must be abstract** — app layers import cache/lock service keys from `@d2/interfaces` (e.g., `DistributedCache.IDistributedCacheGetKey`), NEVER from implementation packages like `@d2/cache-redis`. Composition roots call `addRedisCaching(services, redis, context)` to register concrete implementations against generic keys.

### Security (New Endpoints)

Full checklist → [AUTH.md](backends/node/services/auth/AUTH.md) § "Secure Endpoint Construction Checklist". Key points:

- **IDOR prevention** — derive org/user scope from session/claims, never from user-supplied input. Endpoints must NEVER accept userId, orgId, or role as request parameters when those values are available from the session/JWT. User-supplied identifiers for session-resolvable fields = IDOR vulnerability
- **Pagination limits** — default 50, max 100 on all list queries
- **DB constraint errors** — catch PG `23505` → 409 Conflict, not 500
- **Auth middleware visible at route declaration** — Node.js: `requireOrg()`, `requireRole()`. .NET: `RequireAuthorization()`, `RequireServiceKey()`
- **New JWT claims** → add to BOTH Node.js (`JWT_CLAIM_TYPES`) and .NET (`JwtClaimTypes`), update [AUTH.md](backends/node/services/auth/AUTH.md)
- **No sensitive IDs in JWT** — admin user IDs, internal audit data stays server-side (session only)
- **Per-user caches keyed by identity** — any cache storing user-specific data (JWTs, session state) MUST be keyed by session identity, not shared
- **API key comparisons must be constant-time** — .NET: `CryptographicOperations.FixedTimeEquals`. Node.js: `timingSafeEqual` from `node:crypto`. Plain `===` is vulnerable to timing attacks. → [REST.md](backends/dotnet/gateways/REST/REST.md) § Service Key Validation
- **Auth middleware must fail-closed on missing config** — empty API key mappings or missing secrets = 401 immediately. Never silently bypass.
- **Sign-out must clear ALL auth state**: (1) server session via BetterAuth `signOut()`, (2) `invalidateToken()` for client-side in-memory JWT, (3) `invalidateAll()` for SvelteKit data loaders. → [clients/web/README.md](clients/web/README.md) § Sign-Out Flow
- **CORS `allowHeaders` must include every custom header** any middleware reads — when adding a new header to security middleware (CSRF, fingerprint), verify CORS allows it. Missing = preflight blocks the request.
- **Multi-column key lookups must use paired predicates** — `(col1=A AND col2=1) OR (col1=B AND col2=2)`. Independent `OR`s produce cross-product false positives.
- **Infrastructure paths must be exempt from ALL business middleware** — use shared `InfrastructurePaths.IsInfrastructure()` (.NET) or equivalent. Never add a new infra path bypass to only one middleware. → [REQUEST_ENRICHMENT.md](backends/dotnet/shared/Implementations/Middleware/RequestEnrichment.Default/REQUEST_ENRICHMENT.md) § Infrastructure Path Bypass

---

## §6. Code Conventions

### C# Naming

| Element                          | Convention      | Example             |
| -------------------------------- | --------------- | ------------------- |
| Classes/Records/Interfaces       | `PascalCase`    | `GetReferenceData`  |
| Methods/Properties               | `PascalCase`    | `HandleAsync`       |
| Private instance fields          | `_camelCase`    | `_memoryCache`      |
| Private readonly instance fields | `r_camelCase`   | `r_getFromMem`      |
| Private static fields            | `s_camelCase`   | `s_instance`        |
| Private static readonly fields   | `sr_camelCase`  | `sr_activitySource` |
| Static readonly (non-private)    | `SR_PascalCase` | `SR_ActivitySource` |
| Private constants                | `_UPPER_CASE`   | `_BATCH_SIZE`       |
| Public/Internal constants        | `UPPER_CASE`    | `MAX_ATTEMPTS`      |
| Local constants (tests)          | `snake_case`    | `expected_count`    |
| Local variables                  | `camelCase`     | `result`            |

### C# File Header (required on all .cs files)

```csharp
// -----------------------------------------------------------------------
// <copyright file="FileName.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------
```

### TypeScript Naming

- `camelCase` for variables/functions
- `PascalCase` for types/classes/interfaces/components
- `kebab-case` for modules/files

### Translation Key Conventions

- Auth pages: `auth_{feature}_{purpose}` (e.g., `auth_sign_in_title`)
- App pages: `webclient_app_{page}_{purpose}` (e.g., `webclient_app_profile_title`)
- Design/demo/debug: `webclient_{section}_{purpose}` (e.g., `webclient_debug_session_title`)
- Common UI/errors: `common_ui_*` / `common_errors_*`
- Backend handler messages: Use `common_errors_*` keys where possible
- Reuse existing keys where they match (e.g., `common_ui_dashboard` for Dashboard title)

### Observability

All logs and spans MUST include these fields for cross-service correlation:

| Field         | Source                                           | Purpose                    |
| ------------- | ------------------------------------------------ | -------------------------- |
| traceId       | `IRequestContext.traceId` (auto via BaseHandler) | End-to-end request tracing |
| correlationId | `Idempotency-Key` / RabbitMQ header              | Async message tracking     |
| userId        | JWT `sub` claim / session                        | User audit trail           |
| orgId         | JWT `activeOrganizationId` / session             | Multi-tenant context       |
| service       | `OTEL_SERVICE_NAME`                              | Service origin             |

### Git

- **Branch naming**: `feat/...`, `fix/...`, `docs/...`, `refactor/...`, `test/...`, `infra/...`
- **Commits**: Conventional commits with scope: `feat(geo): add primary locales`
- **No `Co-Authored-By` lines** in commit messages
- **Markdown tables**: Aligned columns for plain-text readability

### Documentation

- Every project/module has a corresponding `.md` file (`ProjectName/PROJECT_NAME.md`)
- Update docs as part of completing a feature — it's part of the definition of done
- When adding new handlers, entities, config options, or public APIs → update the relevant `.md`

---

## §7. Behavioral Guidelines

> **⚠️ These guidelines are MANDATORY — equally binding as §4 (Patterns) and §5 (Code Quality). They are not aspirational. Every item below MUST be followed on every task. Violations here are treated the same as a build failure or a broken test.**

1. **ALWAYS ask when uncertain** — Non-negotiable. Do not guess. Do not assume. If requirements, approach, or tradeoffs are unclear — **ask**. Every time.
2. **Read freely** — Explore any files needed for context.
3. **Ask before changing** — Do not modify files without explicit user approval.
4. **Research first** — Check related files (tests, interfaces, existing implementations) before proposing changes.
5. **Follow existing conventions** — Geo service ([GEO_SERVICE.md](backends/dotnet/services/Geo/GEO_SERVICE.md)) is the primary reference implementation.
6. **Never leave broken things behind** — Fix ALL issues in files you touch.
7. **Always write tests** — Adversarial, not just happy-path. Every behavioral change needs coverage. → [TESTS.md](backends/dotnet/shared/Tests/TESTS.md)
8. **Check [PLANNING.md](PLANNING.md)** — For current phase, status, and resolved decisions.
9. **Provide options** — When multiple approaches exist, present them for user decision.
10. **Maximize parallelization** — Spawn as many sub-agents as makes sense to complete tasks as fast as possible. Independent work (file reads, doc updates, code fixes, test runs, audits) should run in parallel, not sequentially. Use background agents for non-blocking work. The user values speed — don't serialize work that can be parallelized.
11. **Never defer work without explicit permission** — Do NOT unilaterally decide to defer, skip, or deprioritize any planned work. If you think something should be deferred, **ASK the user first** and present the tradeoff. If the user approves deferral, **document it** in PLANNING.md as a tracked issue with rationale. Never silently omit planned work or rationalize skipping it with "not blocking" or "can add later." Any work item that is deferred for any reason MUST appear as a documented issue in PLANNING.md.
12. **Never commit without explicit permission** — Do NOT create git commits unless the user explicitly asks you to commit. Present changes for review first. Committing without permission is as serious as pushing without permission.

### Code Intelligence Tools

**TypeScript**: Use `mcp__cclsp__*` tools (`get_hover`, `find_definition`, `find_references`, `find_workspace_symbols`, `get_diagnostics`). The built-in `LSP` tool's `workspaceSymbol` works but `hover`/`documentSymbol` return empty results.

**C#**: `csharp-ls` via built-in `LSP` tool — `workspaceSymbol` works, diagnostics flow automatically, but `hover`/`documentSymbol` time out (30s limit on large solution). Fall back to Grep/Glob/Read.

Before renaming or changing a function signature, use `findReferences` to find all call sites first. Use Grep/Glob for text/pattern searches (comments, strings, config values) where LSP doesn't help.

After writing or editing TS code, check `mcp__cclsp__get_diagnostics` before moving on. Fix type errors and missing imports immediately.

After writing or editing .NET code, run `dotnet build` (zero warnings) AND `jb inspectcode` (zero warnings). The two tools catch different issues — Roslyn analyzers vs JetBrains inspections. Both must be clean.

### Windows LSP Workaround

Edit `~/.claude/plugins/marketplaces/claude-plugins-official/.claude-plugin/marketplace.json`: Change `"command"` to `"cmd"` with `"args": ["/c", "<binary>", ...originalArgs]` for `typescript-language-server`, `csharp-ls`, `gopls`. **Must reapply after `claude plugin marketplace update`** — the update overwrites the file.

### Project Structure

Key roots: [`contracts/protos/`](contracts/protos/) (proto source of truth), [`backends/dotnet/`](backends/dotnet/) (.NET services + shared), [`backends/node/`](backends/node/) (Node.js services + shared `@d2/*` packages), [`clients/web/`](clients/web/) (SvelteKit), [`observability/`](observability/) (LGTM configs), [`D2.sln`](D2.sln) (.NET solution). See [`README.md`](README.md) for the full tree.
