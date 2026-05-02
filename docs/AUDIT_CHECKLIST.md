<!--
Copyright (c) DCSV. All rights reserved.
-->

# Audit Checklist — D2-WORX Quality Sweep

> **Purpose**: Reusable checklist for quality audits. The user will specify the scope:
>
> - **Solution-wide sweep**: Audit ALL files in the ENTIRE solution, regardless of change history
> - **Main diff sweep**: Audit only files in `git diff main` (modified + added on the current branch)
>
> **Usage**: Copy this checklist into a new .md file at project root for tracking sweep progress. Check off each item as they are verified. All items must pass before the audit is complete.

---

## Security

- [ ] IDOR checks on every endpoint/handler that accesses resources by ID
- [ ] **Session-derived identifiers** — endpoints must NEVER accept user-provided userId, orgId, or role when those values can be resolved from the session/JWT. Derive scope from claims, not from request body/params. User-supplied identifiers for these fields = IDOR vulnerability
- [ ] Auth bypass paths — can unauthenticated requests reach protected handlers?
- [ ] Input validation completeness — every handler validates input at top of `executeAsync` (FluentValidation .NET / equivalent)
- [ ] String max lengths on all string fields
- [ ] Constant-time comparisons on all secret/key comparisons (`CryptographicOperations.FixedTimeEquals` .NET / `timingSafeEqual` Node)
- [ ] Header injection / XSS via user-controlled response data (Content-Disposition, error messages)
- [ ] gRPC service-key auth on all non-health RPCs (fail-closed when no keys configured)
- [ ] JWT claim validation — null checks before trusting claims
- [ ] CORS configuration — allowed origins, headers (incl. all `X-D2-*` custom headers Edge sets/reads), methods
- [ ] No sensitive data in error responses (no stack traces, no internal paths)
- [ ] No empty strings as data — `""` must NEVER represent absent/missing data. Use `null` (C#) or `undefined` (TS). Convert at boundaries: TS `truthyOrUndefined()`, C# `.ToNullIfEmpty()`
- [ ] Proto `optional` keyword on all nullable domain fields — proto3 defaults (`""`, `0`, `false`) are indistinguishable from "not set" without `optional`. Required fields (IDs, keys, status) stay non-optional
- [ ] Auth middleware must fail-closed on missing config — empty service-identity client mappings or missing secrets = 401 immediately
- [ ] Infrastructure paths exempt from ALL business middleware (not just some)
- [ ] Multi-column key lookups use paired predicates — `(col1=A AND col2=1) OR (col1=B AND col2=2)`, not independent `OR`s
- [ ] Custom JWT claims namespaced with `d2:` prefix (avoids future spec collisions; `d2:kind`, `d2:session_id`, etc.)

## Logic / Data Integrity

- [ ] Pipeline completeness — no gaps in multi-step flows (e.g., upload -> intake -> publish -> process -> callback -> push)
- [ ] Error propagation — no swallowed failures, no `Ok()` after unchecked downstream operations
- [ ] Never return `Ok()` after a branching operation unconditionally — if a nested handler or provider can fail, check its result. Either `BubbleFail` or explicitly handle the error
- [ ] Status state machine adherence — can entities get stuck in invalid states?
- [ ] Fire-and-forget operations properly caught (`.ContinueWith(...)` / `try/catch` with logging)
- [ ] EF Core UPDATE/DELETE checks affected rows and returns `NotFound()` when zero
- [ ] DI registration completeness — every handler registered, no missing keys, no stale registrations
- [ ] Race conditions — concurrent operations, duplicate messages, double-processing
- [ ] Resource leaks — DI scopes disposed, gRPC clients cleaned up, connections closed
- [ ] Auth flags initialize to `null`, not `false` — `IsAuthenticated`, `IsTrustedService`, `IsUserImpersonating` use `bool?`. `null` = "not yet determined" (pre-auth). `false` = "confirmed not"

## Code Quality

- [ ] `[RedactData]` attribute on every PII-bearing type / property — auto-redacted across all Serilog logging (recursive, type-cached). Verify new types touching emails / phones / IPs / addresses / names / message content carry the attribute
- [ ] Manual `logger.*` calls reviewed for PII leaks — never log fields that should be redacted via manual calls; let `[RedactData]` + structured logger handle it
- [ ] Semantic D2Result factories — no raw `Fail()` when a factory exists (Ok, Created, NotFound, Unauthorized, Forbidden, ValidationFailed, Conflict, ServiceUnavailable, UnhandledException, PayloadTooLarge, Cancelled, SomeFound)
- [ ] Validate inputs BEFORE infrastructure calls — FluentValidation at TOP of `executeAsync`, before any downstream calls
- [ ] No `!` for silencing warnings (only after `Falsey/Truthy` early return guard where value is guaranteed non-null)
- [ ] Build warnings = bugs — zero warnings on `dotnet build`, `jb inspectcode`, ESLint, Prettier
- [ ] Domain model is source of truth for nullability — if domain field is optional, proto field MUST use `optional` keyword
- [ ] C# `string.Empty` always — never `""` (StyleCop SA1122)
- [ ] C# `Falsey()` / `Truthy()` handle null — never `if (value is null || value.Falsey())`, just `if (value.Falsey())`
- [ ] C# nullable types (`string?`, `bool?`, `int?`, `DateTime?`) for optional domain fields — never `= string.Empty` on optional record properties
- [ ] C# `ToNullIfEmpty()` at boundaries — proto / DB / external strings to domain types. Returns `null` if null, empty, or whitespace-only
- [ ] (SvelteKit BFF only) Prefer `undefined` over `null` in TypeScript — use `field?: string` over `field: string | null`
- [ ] (SvelteKit BFF only) `truthyOrUndefined()` at boundaries — user input, proto values → domain types
- [ ] Structured logger (`ILogger`) not `Console.*` — all logging through the structured logger for OTel correlation

## Conventions

- [ ] C# file headers on all `.cs` files (copyright block)
- [ ] C# naming: `r_camelCase` (readonly), `_camelCase` (mutable), `sr_camelCase` (static readonly), `_UPPER_CASE` (private constants), `UPPER_CASE` (public constants). **Carve-out**: handler primary-constructor parameters omit the `r_` prefix (they're parameters, not fields)
- [ ] (SvelteKit BFF only) TS naming: `camelCase` functions/variables, `PascalCase` types/classes/interfaces, `kebab-case` files/modules
- [ ] Observability fields (traceId, correlationId, userId, orgId, service) on logs/spans
- [ ] i18n — no hardcoded user-visible strings (UI, handler messages, input errors, notifications). TK constants from `D2.Shared.I18n` (or Paraglide on SvelteKit), not bare string literals outside D2Result factories
- [ ] Git: conventional commits with scope, no `Co-Authored-By` lines (husky `commit-msg` hook enforces this)

## Cross-Service

- [ ] Proto contracts match both caller and implementor — field names, types, optional keywords
- [ ] Proto `optional` keyword for all nullable domain fields — both `.proto` definition AND generated code consumers. C# uses `HasField` pattern for proto3 optional
- [ ] Docker dependency chain — startup order, health checks, port conflicts
- [ ] Env var completeness — every var read in code exists in `.env.local` (non-secret) or `.env.secrets` (secret). Both `.example` siblings updated
- [ ] `.env.local.example` and `.env.secrets.example` placeholder values are realistic / safe (correct ports, hostnames, patterns; placeholders for secrets like `replace_with_real_value`)
- [ ] Migrations generated by EF Core (`dotnet ef migrations add <Name>`) — never hand-written SQL / snapshot / `__EFMigrationsHistory` edits. Hand-edits desync EF Core's model snapshot and silently break the runtime migrator
- [ ] Multi-replica migration safety — startup migrator acquires PG advisory lock (only one replica migrates; others wait)
- [ ] SAGA cross-service updates ordered by reversibility — write the compensable step first; compensate on later failures; `logger.fatal` on rollback failure
- [ ] At-least-once fanout consumers are idempotent — duplicate IDs are no-ops, not failures

## Test Coverage

- [ ] Every new handler has unit tests
- [ ] Adversarial cases covered (invalid input, missing fields, boundary values, garbage data) — see `docs/TESTS.md` for the 8-category checklist
- [ ] Access control tested (forbidden / unauthorized paths)
- [ ] Error propagation tested (downstream failures bubble correctly)
- [ ] Integration tests for repo handlers (Testcontainers)
- [ ] All existing tests still pass (zero regressions)
- [ ] Idempotency tested where applicable (duplicate submissions)
- [ ] Concurrency tested where applicable (race conditions)
- [ ] Key rotation integration test passes (KeyCustodian rotation flow) — required CI gate

## Documentation

- [ ] Every new handler / service / endpoint reflected in its `README.md`
- [ ] CLAUDE.md reference table includes all new docs
- [ ] No stale "Pending" or "not yet implemented" references for completed work
