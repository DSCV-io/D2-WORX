<!--
Copyright (c) DCSV. All rights reserved.
-->

# TESTS.md — D²-WORX Testing Discipline

> **Purpose**: evergreen rules for writing tests in D²-WORX. The single most-reused checklist in the codebase. Without it, tests drift back to happy-path-only.
>
> **Frameworks per platform**:
>
> - **.NET**: xUnit + AwesomeAssertions + Testcontainers
> - **SvelteKit BFF**: Vitest (browser mode) + Playwright (mocked by default)

This doc covers HOW to write each test well. CI lane shape (which jobs run when) lives in `.github/workflows/test.yml`.

---

## The Principle

**"If it accepts user input, try to break it."**

Tests aren't reassurance that the happy path works — they're an adversarial probe of every assumption. Every behavioral change needs coverage. Every input boundary needs a test that pushes past it.

---

## 8-Category Case Coverage Checklist

For every behavioral change (new handler, new endpoint, new validation rule, etc.), explicitly cover all 8 categories. If a category doesn't apply, state why in the test file or PR.

### 1. Happy Path

The "it works as designed" baseline. One test per success scenario.

- All required inputs present + valid
- Output matches expectations (shape + values)
- Side effects fired (DB row created, message published, cache populated)
- Returns the right `D2Result` factory (`Ok`, `Created`, `SomeFound` for partial, etc.)

### 2. Garbage Input

Hostile / nonsensical input. The handler should reject CLEANLY, not crash.

- `null` where a value is expected
- Empty strings (`""`) — treated as missing (no empty strings as data anywhere in the system)
- Whitespace-only strings
- Wrong type entirely (object where string expected, array where number expected)
- Malformed UUIDs / IDs
- Strings with leading/trailing whitespace (should be trimmed by `truthyOrUndefined()` / `ToNullIfEmpty()`)
- Negative numbers where positive required
- Special characters (`<script>`, SQL injection patterns, control chars)

Expected: `D2Result.ValidationFailed` with the right `inputErrors`. NOT `UnhandledException` (handler-level safety net only).

### 3. Boundary Values

Off-by-one is real. Test each boundary.

- Max string length + 1 (over)
- Max string length (at)
- Max string length - 1 (under)
- Empty collection (length 0)
- Single-element collection (length 1)
- Max collection size + 1 (over — pagination + batch limits)
- Numeric min / max / zero / negative (where applicable)
- Date boundaries (epoch, far future, leap years if relevant)

### 4. Format Validation

For typed fields with format constraints.

- Email: missing @, missing TLD, multiple @, leading/trailing spaces, internationalized
- Phone: country-code formats per `libphonenumber-js`
- URL: missing scheme, double scheme, path traversal (`..`), unicode
- ISO 8601 dates: invalid days (Feb 30), wrong format, timezone variants
- Hex IDs (content-addressable): wrong length, non-hex chars

### 5. Cross-Field Dependencies

Fields whose validity depends on other fields.

- Conditional required (field A required only if field B = X)
- Mutual exclusion (A and B can't both be set)
- Ordering (start ≤ end)
- Sum constraints (parts must total whole)

### 6. Error Propagation

Downstream failures bubble correctly. **`Ok()` after a failed downstream call is a critical bug.**

- Mock the inner handler to return `D2Result.NotFound` → outer handler returns `BubbleFail`
- Mock the inner handler to return `ServiceUnavailable` → outer handler bubbles, doesn't swallow
- Database constraint violation (PG `23505`) → outer handler returns `Conflict`, not `UnhandledException`
- External API timeout → outer handler returns `ServiceUnavailable` or `Canceled`, not silent success

### 7. Idempotency

Duplicate submissions must produce duplicate-safe outcomes.

- Run the same operation twice with the same input + same `Idempotency-Key` → second call returns the cached response (per `Idempotency.Default`)
- Run the same content-addressable creation twice (e.g., `CreateLocation` with same address) → second call returns the existing entity, no duplicate row
- Run a fanout consumer with the same payload twice → second call is a no-op (the at-least-once fanout contract requires consumer idempotency; see [`public/packages/dotnet/messaging/rabbitmq/README.md`](../public/packages/dotnet/messaging/rabbitmq/README.md))

### 8. Concurrency

Race conditions, double-processing, lock contention.

- Two concurrent requests with the same `Idempotency-Key` → exactly one executes the handler, the other gets the cached response
- Two replicas processing the same Dkron job → only one acquires the Redis lock, the other returns early
- Two concurrent migrations (multi-replica startup) → only one runs (PG advisory lock at startup migrator)
- Two consumers competing for the same RabbitMQ message → exactly one delivers (RMQ competing-consumer semantics)

---

## Test Naming

Format: `MethodName_Scenario_ExpectedResult`.

Examples:

- `HandleAsync_HappyPath_ReturnsOk`
- `HandleAsync_EmailMissingAt_ReturnsValidationFailed`
- `HandleAsync_DownstreamReturnsNotFound_BubblesFailure`
- `HandleAsync_DuplicateIdempotencyKey_ReturnsCachedResponse`
- `HandleAsync_ConcurrentExecution_OnlyOneAcquiresLock`

Local constants in test methods use `snake_case`:

```csharp
const string expected_email = "test@example.com";
const int expected_count = 5;
```

(`snake_case` for local test constants is the carve-out from the standard `camelCase` for locals.)

---

## Form / Endpoint Testing Patterns

### Form fields (SvelteKit)

For every form field that accepts user input:

- Unit test (Zod schema): all 8 categories on the schema directly
- E2E test (Playwright + mocks): blur validation, error display, error clearing on fix, submit rejection while errors present, successful submit after fix
- Cross-field interaction (if applicable): cleanly transitions between valid + invalid states

Don't test form fields with only happy-path Playwright. The schema test covers garbage; the Playwright test covers UX.

### REST endpoints

For every endpoint:

- 8 categories on the handler (unit + integration)
- Auth tests: unauthenticated (401), wrong scope (403), wrong org (403), correct (200)
- Pagination: max + 1 → 400 ValidationFailed (default 50, max 100 on every list endpoint)
- Idempotency-Key: duplicate returns cached response

### gRPC RPCs

For every RPC:

- 8 categories on the handler
- Service-key auth tests: missing key (401), wrong key (401, fail-closed), correct key (200)
- Proto field handling: optional fields default-correctly, `HasField` predicates work

---

## Vitest Custom Matchers

For SvelteKit BFF tests against `D2Result` shapes, prefer custom matchers over inline assertion. Equivalent xUnit assertion helpers should exist on the .NET side so both stacks read the same way.

| Matcher                        | Purpose                                                      |
| ------------------------------ | ------------------------------------------------------------ |
| `toBeSuccess()`                | Asserts `result.success === true`                            |
| `toBeFailure()`                | Asserts `result.success === false`                           |
| `toHaveData(expected)`         | Asserts `result.data` matches `expected` (deep equality)     |
| `toHaveErrorCode(code)`        | Asserts `result.errorCode === code`                          |
| `toHaveStatusCode(code)`       | Asserts `result.statusCode === code`                         |
| `toHaveMessages(...messages)`  | Asserts `result.messages` contains the expected TK keys      |
| `toHaveInputErrors(...fields)` | Asserts `result.inputErrors` covers the expected field names |

Example:

```typescript
const result = await handler.handle(input);
expect(result).toBeSuccess();
expect(result).toHaveData({ id: expected_id });
```

vs. raw:

```typescript
expect(result.success).toBe(true);
expect(result.data).toEqual({ id: expected_id });
```

The matcher version produces better failure messages and forces consistency.

---

## Asserting translation keys

When a test asserts a translation/message key (e.g. `result.messages[0].key`, `error.Key`), reference the generated catalog constant — `TK.common.errors.NOT_FOUND` (TS) / `TK.Common.Errors.NOT_FOUND.Key` (.NET) — never a bare `"common_errors_NOT_FOUND"` literal. The constant gives compile-time safety + rename support: a key rename then fails the test at compile time instead of silently asserting a stale string.

Carve-outs — keep the bare literal, because there the literal IS the test:

- **Catalog self-tests** that pin `constant === "the_string"` (converting → `expect(c).toBe(c)`, which tests nothing).
- **Cross-language wire-contract / parity tests** — the literal is a drift tripwire independent of the catalog; a coordinated key rename must FAIL the test, so the assertion cannot reference the catalog.
- **Simulated-wire input fixtures** — a raw external (e.g. .NET) payload literal fed into a parser/transform; it represents external data, not our code referencing our catalog.
- **Orphan keys** with no catalog entry (test-only placeholders — no constant exists to reference).

(`rules.md` carries the full predicate + the evidence grep.)

---

## Test Categories

CI runs each category in parallel. Don't lump categories together — separation enables faster failure feedback.

| Category                    | Speed         | Spins up                                                               | Where                                                                 |
| --------------------------- | ------------- | ---------------------------------------------------------------------- | --------------------------------------------------------------------- |
| **Unit**                    | ms            | Pure functions, mocked deps                                            | `private/services/{svc}/tests/Unit/` and `private/services/web/src/**/*.test.ts` |
| **Per-service integration** | seconds–1 min | ONE service + its direct deps (PG, Redis, RabbitMQ) via Testcontainers | `private/services/{svc}/tests/Integration/`                            |
| **Web component tests**     | ms–seconds    | JSDOM-like browser env, mocked fetch                                   | `private/services/web/src/**/*.test.ts` (Vitest browser mode)                   |
| **Web Playwright (mocked)** | seconds–min   | Real browser, ALL `fetch()` mocked                                     | `private/services/web/tests/`                                                   |

Explicitly **NOT** in scope:

- ~~System spin-up tests~~
- ~~Cross-service integration tests with multiple services~~
- ~~Browser E2E with real backend~~

These tiers added wall-clock time without commensurate value at our scale.

---

## Platform-dependent transport tests

A test that exercises a real socket or a platform-dependent transport path — a mutual-TLS handshake over a real TCP connection, a Unix-domain-socket bind, a platform-specific TLS-stack behavior — sometimes CANNOT run on the developer's OS (the OS TLS stack rejects the handshake, the socket capability is unavailable). The tempting wrong fix is to make the test pass unconditionally on that OS. **Never mark a platform-incompatible test green on an OS where it cannot actually exercise the path** — a fake green reports coverage that does not exist, so the gap stays invisible until the deploy-target container (or production) surfaces the untested path. A fake green is strictly worse than an honest skip.

The correct shape ships all three artifacts, then proves the full path in the deploy-target container (Docker / Linux):

1. **Honest platform-skip on the incompatible OS** — report SKIPPED via a real skip primitive (`[SkippableFact]` + `Skip.IfNot(OperatingSystem.IsLinux())`, an xUnit `Skip`, a runtime platform guard). The incompatible-OS run must show SKIPPED in the runner output — never a green assertion (`Assert.True(true)`), never a `[Fact(Skip = ...)]` masking a path that was never made to work. The honest skip keeps the gap VISIBLE.
2. **Deterministic cross-platform unit matrix for the underlying logic** — extract the platform-independent decision logic (the certificate / token validator, the wire parser, the handshake-policy evaluator) and unit-test it on EVERY OS, decoupled from the socket. The adversarial coverage (malformed cert, expired cert, wrong SAN, untrusted issuer) lives HERE — it must not be gated behind the platform-skipped socket test.
3. **Same-platform canary for the platform-independent slice** — a test covering the parts that DO run on the dev OS (constructing the TLS options, building the cert chain in memory, pre-handshake validation) proves the non-socket portion on the developer's machine.

The real-socket path itself is then proven in the deploy-target container — the Docker / Linux CI job (or `docker run`) that actually exercises the handshake where it CAN run. The container test self-provisions its own trust material (a test-scoped fixture) so the pass depends on the fixture, not on the developer's machine state. Canonical predicate: [rules.md §1.30](dev/rules/01-test-discipline.md#1-test-discipline); the orchestrator re-runs environment-touching gates from a clean state per [rules.md §24.27](dev/rules/24-audit-evidence-discipline-meta-how-to-audit.md#24-audit-evidence-discipline-meta--how-to-audit) so a green that depended on a diagnostic-installed trust root cannot be mistaken for real coverage.

---

## Tracked CI gate — key-rotation integration (NOT IMPLEMENTED)

`integration-key-rotation` is a **tracked deliverable (NOT IMPLEMENTED)** — no workflow job for it is present in `.github/workflows/test.yml` (active or commented). The KeyCustodian state machine and key lifecycle are shipped (see [KeyCustodian README](../private/services/edge/key-custodian/README.md)); the compromise-response runbook (executable CLI invocations, detection criteria, recovery procedures) and the non-skippable CI gate that pins them remain open design work. Intended coverage for that gate:

- Graceful rotation under load (publishers + consumers; no message loss; in-flight old-kid messages still decrypt during grace)
- Grace expiry (retired kids removed from production keyring; stale messages → DLQ with explicit error)
- Emergency rotation (compromise marking is terminal; cannot be promoted back)
- Race conditions (rotation while N replicas publishing concurrently)
- Archive decryption (ops CLI fetches retired/compromised kids on demand)

---

## What We Accept Losing

By dropping the cross-service tier we lose:

- **Cross-service contract drift detection in CI** — caught by code review + the proto versioning policy + production observability
- **Full-flow happy-path verification** — caught by manual testing (you click through critical flows after meaningful changes)

For pre-alpha (no users), this is acceptable. The criteria for adding a pre-merge cross-service gate remain a tracked open deliverable decision.
