<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2-WORX Rules

The complete, verbose, authoritative requirements for ANY code change in this repository. **Read this entire document during the PLAN phase of every deliverable** so you know upfront what you're being held to. Use it as the audit checklist after every step (looped until a pass returns zero findings) and again at final-review (scope = whole deliverable).

---

> ## ⚠️ MISSION CONTEXT — READ FIRST
>
> **D²-WORX is being built as an enterprise-level, production-ready, robust SaaS framework.** Every line of code that ships from this repository is held to that standard — not "works on my machine," not "good enough for now," not "we'll harden it later."
>
> Code that ships under this standard MUST:
> - Survive bad input, hostile input, malformed input, and oversized input without crashing or leaking
> - Survive infrastructure failure (DB down, broker unreachable, cache miss + downstream timeout, JWKS endpoint slow, network partition) gracefully — degrade, retry, circuit-break, or fail-closed; never silently swallow signal
> - Never leak PII (user input, broker URIs, presigned URLs, file names, IPs, emails, addresses, message bodies) into logs, metrics, traces, or message-broker headers
> - Survive concurrent access without races, double-fetches, deadlocks, or torn writes
> - Be testable, observable, and maintainable by future engineers who weren't in the room when it was written
> - Follow the established patterns and conventions of THIS codebase, not generic best practices from training data
>
> **If a predicate in this document feels like overkill for the task at hand, that's the discipline working — the cost of reading and applying it is minutes; the cost of skipping it is a production incident, a security disclosure, or a multi-week rework.** Don't optimize for short-term speed at the expense of robustness. The user's value is design + architectural review; the agent's value is delivering work that doesn't need user-side bug-hunting.

---

## How to use this doc

1. **PLAN phase** — read end-to-end. Understanding the requirements upfront prevents architectural mistakes that cost rework later.
2. **Pre-execute pass** — before writing each step's code, walk the categories with intent: which predicates apply to this step? Surface the relevant ones in the step journal under "Pre-emptive gate checks" so you write code that passes the audit on round 1.
3. **Audit loop** — after writing the code, walk every category, every predicate. Answer Y/N with required evidence (grep results, file:line lists, "checked X by Y, found Z"). Vibes are not evidence. Findings get fixed in the same round; the next round runs against post-fix state. Loop until a round produces zero findings across every category. 10-iteration ceiling per scope; iteration 11 means escalate to user.
4. **Final-review** — same loop, scope = whole deliverable. Catches cross-step inconsistencies.

> **Verbose by design.** Every predicate exists because of a real past failure. The cost of reading the catalog is minutes per round; the cost of skipping a predicate is a future audit round (or a bug shipped). New predicates get appended at deliverable ship via the self-improvement loop ([workflow.md](workflow.md) §SHIP).

> **Companion docs**: [workflow.md](workflow.md) (the loop protocol), [deliverables/](deliverables/) (past final reports + lessons), [../PATTERNS.md](../PATTERNS.md) (what each pattern IS — this doc enforces THAT they're followed).

## Categories

1. [Test Discipline](#1-test-discipline)
2. [Bug-Fix Regression Testing](#2-bug-fix-regression-testing)
3. [PII / Logging Safety](#3-pii--logging-safety)
4. [Concurrency / Race Conditions](#4-concurrency--race-conditions)
5. [C# Code Conventions](#5-c-code-conventions)
6. [TypeScript / SvelteKit Code Conventions](#6-typescript--sveltekit-code-conventions)
7. [Naming, File Headers, Folder Casing](#7-naming-file-headers-folder-casing)
8. [Build & Tooling Hygiene](#8-build--tooling-hygiene)
9. [Architectural Layer Hygiene](#9-architectural-layer-hygiene)
10. [Security (Endpoints / Auth / Secrets / Input)](#10-security-endpoints--auth--secrets--input)
11. [Documentation Parity & Best Practices](#11-documentation-parity--best-practices)
12. [i18n Discipline](#12-i18n-discipline)
13. [Permission / Action Discipline](#13-permission--action-discipline)
14. [Phase / Audit / Conversation Verbiage Hygiene](#14-phase--audit--conversation-verbiage-hygiene)
15. [Object Disposal & Resource Lifetime](#15-object-disposal--resource-lifetime)
16. [OOTB Shared-Lib Tooling — Use What's There](#16-ootb-shared-lib-tooling--use-whats-there)
17. [D2Result Usage & Extensions](#17-d2result-usage--extensions)
18. [Graceful Degradation & Failure Modes](#18-graceful-degradation--failure-modes)
19. [User Experience (UX)](#19-user-experience-ux)
20. [Developer Experience (DX)](#20-developer-experience-dx)
21. [Observability Completeness](#21-observability-completeness)
22. [Idempotency & Exactly-Once Semantics](#22-idempotency--exactly-once-semantics)
23. [Configuration Hygiene](#23-configuration-hygiene)

---

## 1. Test Discipline

The #1 cost driver of multi-pass audits is "thin glue" code (DI extensions, gRPC plumbing, factory wrappers, source-generator emitters) shipped without tests. Every public path needs at least one test on the FIRST pass.

### Predicates

- **1.1** Does every `public` method introduced in this scope have ≥1 test?
  - Evidence: `<method qualified-name> -> <test file:line>` for each, or "no public methods in scope."
  - **Why**: Real example — `GrpcClientBuilderExtensions.AddD2ServiceIdentity()` shipped with an unused `<TClient>` generic; no test exercised the call so the bug hid until a second audit pass. Same round: OIDC `ConfigurationManager` was constructed via the 2-arg ctor that uses a static default `HttpClient` instead of our `IHttpClientFactory` — no test verified our HttpClient was actually being used.
  - **How**: "hard to test in isolation" is a smell, not an excuse. Find a way: compile-time test (existence of test file proves call shape compiles), DI smoke test, runtime registration assert.

- **1.2** Does every test cover the happy path AND at least one adversarial input (null / empty / whitespace / oversized / malformed / wrong type / wrong format / boundary value)?
  - Evidence: per-test, list of input categories exercised.
  - **Why**: Happy-path-only tests pin behavior under good input but not under garbage input — which is what production actually delivers. An enterprise framework MUST survive malicious / malformed / accidental garbage gracefully.

- **1.3** Are all DI extension methods (`Add*`, `Use*`, `Configure*`) tested via composition resolution (build a `ServiceProvider`, resolve the registered service, assert shape)?
  - Evidence: `<extension method> -> <test file:line that resolves it>`.

- **1.4** Are all gRPC client/server registration helpers tested (call shape compiles + dispatches)?
  - Evidence: `<helper> -> <test that calls through it>`.

- **1.5** Are all source-generator emitters covered by snapshot tests (input spec → expected source, AST-equivalent)?
  - Evidence: emitter → snapshot test file.

- **1.6** Do reflection-based tests pin every `[LoggerMessage]` partial-method signature against accepting `Exception` (since `ex.Message` leaks broker URIs / user input)?
  - Evidence: `<partial method> -> <reflection test asserting param list>`.
  - **Pattern**: `Phase8FixVerificationTests.F3F4L5_LogDelegate_DoesNotTakeRawException` was the canonical one (now redistributed to `Telemetry/LoggerMessageDelegateContractTests.cs`).

- **1.7** Are all factory wrappers (`From*` static methods on records / classes) tested with both happy and degenerate inputs?
  - Evidence: list factory → test.

- **1.8** Do test method names use **behavior-descriptive** names? No `F2_`, `AuditN_`, `PhaseN_`, `Audit{Letter}_`, `R5_`, `H4_`, `M2_`, `L3_`, `O1_`, `S2_`, `Q1_`, combo labels like `F3F4L5_`.
  - Evidence: `grep -rEn 'public[[:space:]]+(async[[:space:]]+)?(void|Task|ValueTask)[[:space:]]+(Audit[0-9]+_|Audit[A-Z]_|Phase[0-9]+_|[HMFLORSQ][0-9]+_|F[0-9]+F[0-9]+L[0-9]+_)' <test files>` returns empty.
  - **Why**: future readers don't care which audit pass added the test — they care what behavior it pins.

- **1.9** Are test files named after the FEATURE they cover, not the audit round? Forbidden: `Phase*Tests.cs`, `*Audit*Tests.cs`, `*Sweep*Tests.cs`, `*Round[0-9]*Tests.cs`, `AuditFixesRegressionTests.cs`, `SecondAuditFixesRegressionTests.cs`.
  - Evidence: `git ls-files` against the forbidden glob set returns empty.

- **1.10** Does the test project's `<ProjectReference>` graph include every new lib introduced in this scope?
  - Evidence: list new libs → `<ProjectReference>` line in test csproj.

- **1.11** Do integration tests exist for behaviors that genuinely need them (race conditions, broker behavior, DB constraint behavior, end-to-end gRPC dispatch, Redis pub/sub backplane, multi-instance coordination)? Unit tests against mocks aren't enough for those.
  - Evidence: per integration-needing behavior → integration test file.

- **1.12** Do tests cover graceful degradation under outages (DB unavailable, broker unreachable, cache miss + downstream timeout, JWKS endpoint slow, OIDC discovery failure)?
  - Evidence: per graceful-degradation behavior → test.

- **1.13** Are private helpers made `internal` (with `[InternalsVisibleTo]`) when needed for direct unit testing rather than fragile end-to-end tests?
  - Evidence: per `internal` promotion → corresponding test + `InternalsVisibleTo` attribute.

- **1.14** Do tests use `Random.Shared` (thread-safe) instead of `new Random()` for any randomness?
  - Evidence: `grep -rEn 'new Random\(' tests/` → expect zero.

- **1.15** Are test fixtures (counter listeners, HTTP message handlers, time providers) one-type-per-file under `Fixtures/`?
  - Evidence: per fixture type → dedicated file.

- **1.16** Do test fixtures that touch the file system, env vars, or network use ISOLATED paths (TempDir, explicit non-existent file names, mocked env, sandboxed network)? Tests MUST NEVER load real-environment data into the test process.
  - Evidence: per file-system / env-var / network-touching fixture → isolation mechanism.
  - **Why**: a test that calls `.env`-discovery with default file names will walk up the working directory to the repo root and silently load `.env.secrets` into the test process state — real secrets in test process memory, cross-test contamination, log-grep exposure surface. Same class: tests that resolve `IConfiguration` from a real file, hit a real localhost port, or call into `Environment.GetEnvironmentVariable` without a fixture-scoped override.
  - **Pattern**: pass an explicit non-existent file name (so discovery short-circuits cleanly) OR scope to a `TempDir` fixture OR mock the env-var accessor.
  - **Critical security gap class.**

- **1.17** Tests with names of the form "X bypasses Y" / "X overrides Y" / "X wins over Y" must construct the scenario such that BOTH X and Y would individually trigger — proving the precedence, not just observing one outcome.
  - Evidence: per "bypasses" / "overrides" / "wins" test → setup confirms both predicates would fire individually.
  - **Why**: a test named `OceFromCt_BypassesTransientClassifier` that runs against a classifier which wouldn't classify `OperationCanceledException` as transient anyway passes WITHOUT actually exercising the bypass. It claims to pin the catch filter; it's actually pinning the classifier. To genuinely prove the bypass, invert the classifier (e.g. `IsTransient: _ => true`) so the test PROVES the catch filter is what bails out, not the classifier.

- **1.18** Do tests pin per-public-VALUE for every constant / enum value / static-class member (not just per-class smoke)? `nameof(X)`-captured constants need explicit wire-value `Should().Be("X")` pinning so a refactor that replaces `nameof(X)` with a literal can't silently change the wire value.
  - Evidence: per public constant / enum value → dedicated pin test (e.g. `[Theory] [InlineData(...)]` matrix or per-value `[Fact]`).
  - **Why**: a per-class smoke test that asserts "the constants exist and aren't empty" passes after a rename like `CLIENT_ID` → `clientid` because no test pins the actual wire value. Same anti-pattern: `ErrorCodes.X = nameof(X)` constants — the wire format consumed by clients, audit-log queries, alerting rules is the contract; a `nameof(X)` → literal replacement would silently change the wire value without breaking the build.

---

## 2. Bug-Fix Regression Testing

Every bug fix in this scope must land with a regression test that **fails-without-fix** and **passes-with-fix** in the same change. Without it, "fixed" is unverifiable and a future refactor can silently regress the same bug.

### Predicates

- **2.1** Does every fix made during this audit loop have a regression test?
  - Evidence: per finding fixed in any round → test file:line.
  - **No fix without a test, no exceptions.**

- **2.2** Does each regression test name encode the issue (descriptive, not audit-prefixed)? Format: `<Behavior>_<Condition>` (e.g. `MarkSeenFails_MessageRoutesToDlq_HandlerNotReplayed`, `DlqDetail_DropsExceptionMessage`, `ReadAttemptCount_OnlyCountsExpiredAndRejected`).
  - Evidence: per regression test → name format check.

- **2.3** Was the regression test confirmed-failing BEFORE the fix was applied (or, when not separable, does the test comment document the fail-without-fix expectation)?
  - Evidence: per test → confirmation note in journal. Ideally the test goes in FIRST and is confirmed failing before the fix code is applied — empirical proof the test actually covers the bug.

- **2.4** Is the test type (unit / integration) appropriate for what the bug exercises?
  - PII-safety on log delegates → reflection unit test (fast, reliable)
  - Race condition / consumer pipeline behavior / cross-service interaction → integration test (real broker / real DB)
  - Composition-time validation → unit test resolving from `ServiceProvider`
  - Evidence: per test → unit/integration classification + 1-sentence justification.

- **2.5** When a private helper needed to be made `internal` (with `[InternalsVisibleTo]`) for direct testing, is that change documented in the journal as part of the fix?
  - Evidence: per `internal` promotion → journal entry.
  - **Example**: `SubscriberChannel.ReadAttemptCount` was made `internal` so x-death-reason filter could be unit-tested across 7 cases.

---

## 3. PII / Logging Safety

User input, message bodies, presigned URLs, AMQP URIs, broker passwords, IPs, emails, addresses, names, file metadata, fingerprints — all PII. The biggest leak vector in this codebase is logging exception messages (broker libs embed credentials in their `ex.Message`).

### What counts as PII (non-exhaustive)

- Email addresses (any form, even hashed if reversible)
- Phone numbers
- IP addresses (v4 + v6)
- Geographic / location data (lat/long, addresses, city+postal beyond country)
- Names (first, last, display, business)
- User-generated content (messages, posts, descriptions, comments)
- File names, file content, file metadata
- Presigned URLs (contain credentials in query params)
- AMQP / DB / Redis connection strings (contain passwords)
- JWT tokens, session tokens, API keys, refresh tokens
- Browser fingerprints, device fingerprints
- Authentication state (user IDs in some contexts)
- Audit trail content (who did what when)

### Predicates

- **3.1** Does any `[LoggerMessage]` partial-method declaration in this scope accept `Exception` as a parameter? (Y = bug; the log sink will format `ex.ToString()` and leak `ex.Message`.)
  - Evidence: `grep -rEn '\[LoggerMessage' <scope>` → for each hit, inspect parameter list → confirm none take `Exception`.
  - **Real-example leak**: `BrokerUnreachableException.Message` embeds the AMQP URI INCLUDING the password. Same class: `OperationInterruptedException.Message` from RabbitMQ.Client includes broker-side text (PRECONDITION_FAILED arg dumps); `RedisException.Message` from StackExchange.Redis can include connection-string fragments + command details; `Npgsql.PostgresException.Message` includes row data in constraint-violation messages; ANY user-handler exception in a subscriber-isolation `catch` carries arbitrary user-input.
  - **No per-call-site carve-out.** The receiving log delegate signature IS the contract; future callers will pass real exceptions even if today's call site synthesizes a controlled one (e.g. `new InvalidOperationException(result.ErrorCode ?? "unknown")`). The next refactor that adds an inline-catch site through the same delegate inherits the leak risk for free.
  - **Carve-out** (rare, must be pinned by a contract test): `ContinueWith`-only fault sinks where the handled exception path NEVER sees user-input-derived content (e.g. unobserved background-task faults). When a delegate has BOTH a sanitized inline-catch site AND a fault-sink ContinueWith site, SPLIT it into two delegates with separate names so each shape is independently pinned — never share.
  - **Canonical enforcement**: a reflection-based contract test (`LeakProneLogDelegates` `TheoryData` listing every leak-prone delegate + per-delegate carve-out `[Fact]`s for the explicit fault-sink exceptions). See `tests/Unit/Messaging/Telemetry/LoggerMessageDelegateContractTests.cs` for the pattern.

- **3.2** Does any `try/catch` log `ex.Message` directly (string interpolation, structured field, format arg) without going through a sanitized renderer (e.g. `SanitizedExceptionRender.TypeName(ex)` + `FirstFrame(ex)`)?
  - Evidence: `grep -rEn 'ex\.Message\|exception\.Message' <scope>` → per hit, classify safe/unsafe.
  - **Pattern**: emit `TypeName` + `FirstFrame` as separate structured fields; never the raw `Message`.

- **3.3** Does any logged structured field carry user input (email, IP, address, message body, AMQP URI, presigned URL, file name) without `[RedactData]` on its declaring type?
  - Evidence: per logged field → type declaration → `[RedactData]` check.
  - **How**: `[RedactData]` lives on the type, applies to ALL Serilog logging recursively, reflection-cached. Don't reach for per-handler RedactionSpec when `[RedactData]` does the job.

- **3.4** Does any AMQP message header carry sensitive context plaintext (instead of being inside the encrypted `ContextEnvelope` payload)? (Brokers store headers as plaintext at-rest.)
  - Evidence: per `IBasicProperties.Headers` write → classify field as routing-only (W3C `traceparent`/`tracestate`) or context.
  - **Sensitive context → encrypted envelope.** Routing tracing → plaintext header.

- **3.5** Does any OTel span attribute or metric tag carry PII?
  - Evidence: per `Activity.SetTag` / `Counter.Add(..., new TagList { ... })` → classify each tag value.
  - **Allowed**: outcome (`success` / `fail` / `not_found`), code-path identifiers, error categories. **Forbidden**: emails, IPs, names, message bodies.

- **3.6** Does any test fixture log a raw exception via a code path that production also reaches?
  - Evidence: walk test fixtures touching `ILogger` → confirm no production code path shares the same logging sink.

- **3.7** Are user-input strings (HTTP body, query params, JWT claim values, message payloads) length-validated upstream of any storage / log emission?
  - Evidence: per user-input boundary → length-validation call site (e.g. `MAX_AUDIENCE_LENGTH` checks, `_MAX_JWT_PAYLOAD_SEGMENT_LENGTH` cap on token-exchange).

- **3.8** Does sign-out / logout clear ALL auth state (server session via Auth, in-memory JWT invalidation, SvelteKit `invalidateAll()`, Redis session entries, cookie cache)?
  - Evidence: per sign-out path → trace each surface.

- **3.9** Are API key / secret / token comparisons constant-time (`CryptographicOperations.FixedTimeEquals`)? Plain `==` is a timing-attack vector.
  - Evidence: per `apiKey == ` / `secret == ` / `token == ` → switch to `FixedTimeEquals`.

- **3.10** Does client-side telemetry (Faro user identity) include only `userId` + `username`? No email, real name, contact details?
  - Evidence: per Faro identity setter → fields confirmed.

- **3.11** Was `Grep` run against `secrets/` or `.env.secrets` by name? (Behavioral rule — never grep these.)
  - Evidence: tool-call history check → expect no hits.
  - **If a secret accidentally enters context** (runtime output, grep match), STOP and tell the operator immediately so they can rotate the exposed value.

- **3.12** When a presigned URL is generated, does it have the shortest reasonable TTL? (Long TTLs become long-lived secrets.)
  - Evidence: per presigned URL generation → TTL value + justification.

- **3.13** Are tokens / secrets / fingerprints in flight always encrypted (TLS) and at rest in cache layers always treated as sensitive (encrypted in Redis where possible; ephemeral TTL otherwise)?
  - Evidence: per cache write of token → encryption + TTL confirmed.

- **3.14** Do log messages include enough context to debug WITHOUT including PII? (e.g. log `userId` hash + outcome instead of `email`.)
  - Evidence: per log message in scope → information sufficiency vs PII trade-off documented.

---

## 4. Concurrency / Race Conditions

The bugs that don't fail unit tests because unit tests are sequential.

### Predicates

- **4.1** Does any `Singleflight.ExecuteAsync` body re-check the cache inside the singleflight before doing the expensive operation?
  - **Race**: lookup → miss → singleflight → ALSO miss because no recheck → double-fetch.
  - Evidence: per singleflight call site → confirm cache-recheck inside body.

- **4.2** Does any "atomic" operation on a distributed cache use a real atomic primitive (`SET NX`, `EVAL` Lua, transaction), or is it a sequence of non-atomic ops that look atomic in isolation?
  - Evidence: per atomic-claimed op → primitive used.

- **4.3** Does any code holding multiple locks document the lock acquisition order (preventing AB / BA deadlock)?
  - Evidence: per multi-lock site → ordering doc.

- **4.4** Does every backplane subscriber unsubscribe on dispose? (Otherwise subscriptions leak across test runs and the second test sees the first test's events.)
  - Evidence: per subscribe → corresponding unsubscribe in `IAsyncDisposable.DisposeAsync`.

- **4.5** Does every `IHostedService` respect the cancellation token in its loop body (not just at loop entry)?
  - Evidence: per hosted service → token check inside body.
  - **Specifically**: `await Task.Delay(interval, ct)` not `await Task.Delay(interval)` then check.

- **4.6** Is any `ValueTask` awaited more than once? (Documented antipattern — call `.AsTask()` once, store the `Task`, reuse.)
  - Evidence: per `ValueTask` field/local → call count check.

- **4.7** Is any code using `new Random()` instead of `Random.Shared`? (Latter is thread-safe.)
  - Evidence: `grep -rEn 'new Random\(' <scope>` → expect zero.

- **4.8** Does any database write happen WITHOUT a transaction when multiple rows are coordinated?
  - Evidence: per multi-row write → `BeginTransactionAsync` confirmed.

- **4.9** Does any cache write that needs cluster-wide L1 invalidation use the `*AndBroadcast*` variant (publishes on backplane)?
  - Evidence: per cache-write → confirm broadcast variant when other instances cache the same key.

- **4.10** Are HTTP retries idempotent? (Non-idempotent operations need an idempotency key, not just retry-on-failure.)
  - Evidence: per HTTP-retry config → idempotency strategy.

- **4.11** Does any `Task.Run` / `ConfigureAwait(false)` pattern correctly preserve / drop the synchronization context as appropriate for ASP.NET Core / library code?
  - Evidence: per `ConfigureAwait` site → correct choice.
  - **In library code**: `ConfigureAwait(false)`.
  - **In ASP.NET Core handler code**: omit (defaults work).

- **4.12** Does any code mutate shared state (static field, singleton DI service field) without a lock / `Interlocked` / `Volatile.Read`/`Write`?
  - Evidence: per static / singleton mutation → synchronization primitive confirmed.

- **4.13** When holding a lock, is the duration minimized (no I/O, no async-await, no allocation in the critical section when avoidable)?
  - Evidence: per `lock` body → I/O / await / allocation audit.

- **4.14** Does shutdown of any `IHostedService` complete in finite time? (No infinite loop without ct check; no `await Task.Delay(Timeout.InfiniteTimeSpan)` without ct.)
  - Evidence: per hosted service → shutdown trace within 30s SIGTERM grace period.

- **4.15** Do consumer / publisher channels handle reconnect cleanly? (Per `ChannelPool` / `IConnection` rebuild paths.)
  - Evidence: per channel use → reconnect scenario tested.

- **4.16** Are dictionaries / sets accessed concurrently using `ConcurrentDictionary` / `ConcurrentBag` / similar — NOT plain `Dictionary` / `HashSet` with manual locking unless justified?
  - Evidence: per concurrent collection use → type confirmed.

- **4.17** Are row-update operations that other writers may also touch protected with optimistic concurrency (rowversion / `xmin` / version column / `[ConcurrencyCheck]`)? Is `DbUpdateConcurrencyException` caught and converted to `D2Result.Conflict()` (or retried with backoff for idempotent updates)?
  - **Why**: without this, two concurrent writers can clobber each other (last-write-wins), losing data or invariants. Real example: F7.5 M2 audit fix on `IntakeFile` — concurrent intake of the same file lost the earlier write.
  - Evidence: per row-update handler → concurrency token + exception mapping confirmed.

- **4.18** Is fire-and-forget background work (raw `Task.Run` / `_ = SomeAsync()` / unmonitored async) avoided in favor of explicit hosted services or background queues?
  - **Why**: fire-and-forget swallows exceptions silently, doesn't respect SIGTERM, and obscures observability. F7.5 M3 audit fix found "raw upload cleanup fire-and-forget" — failure surfaced only via storage growing unboundedly.
  - **Allowed**: when truly best-effort AND failures are logged AND the operation has no business correctness impact. Document the "best-effort" choice in the call-site comment.
  - Evidence: per non-awaited async invocation → category (allowed best-effort with logging / use hosted service / use background queue).

---

## 5. C# Code Conventions

The set of in-language rules that show up everywhere. Memory of these is the difference between first-pass clean and round-3 cleanup.

### Null / empty / parse helpers (highest-frequency)

- **5.1** Are all null / empty / whitespace / `Guid.Empty` checks using `Falsey()` / `Truthy()` extensions from `D2.Shared.Utilities.Extensions`?
  - **Forbidden**: `string.IsNullOrEmpty(s)`, `string.IsNullOrWhiteSpace(s)`, `coll is null || coll.Count == 0`, `coll?.Any() != true`, `guid == Guid.Empty`, `s != null && s != ""`.
  - **Required**: `s.Falsey()`, `coll.Falsey()`, `guid.Falsey()` (and `Truthy()` inverses) — they handle null themselves so never combine with `is null` checks. After early return on `Falsey()`, use `value!` (one of the few valid `!` uses).
  - **Also forbidden**: redundant size check after `.Falsey()` (`coll.Falsey()` already covers null + empty; a follow-up `coll.Count == 0` is dead code).
  - Evidence: `grep -rEn 'IsNullOrEmpty\|IsNullOrWhiteSpace\|== Guid\.Empty' <scope>` → expect zero (or justify each).
  - **Where defined**: `D2.Shared.Utilities.Extensions` — `StringExtensions.cs`, `GuidExtensions.cs`, `EnumerableExtensions.cs`.

- **5.2** Are all `TryParse` patterns using `D2.Shared.Utilities.Extensions` (`str.TryParseTruthyNull(out Guid? r)` / `str.TryParseTruthyNull<TEnum>(out var r)`)?
  - **Forbidden**: hand-rolled `if (str is not null && Guid.TryParse(...))` / `Enum.TryParse<T>(...)`.
  - **Required**: the extension that collapses null/empty/whitespace/Guid.Empty/unparseable → `null` in one call.
  - Evidence: `grep -rEn 'Guid\.TryParse\|Enum\.TryParse' <scope>` → for each, justify or convert.
  - **Applies**: hand-written code AND codegen emitter output. When MutableEmitter / TKEmitter / ScopesEmitter generate code that touches strings or Guids, the emission should produce code that calls these extensions.

- **5.3** Are all `D2Result` failure constructions using semantic factories (`Ok`, `Created`, `NotFound`, `Unauthorized`, `Forbidden`, `ValidationFailed`, `Conflict`, `ServiceUnavailable`, `UnhandledException`, `PayloadTooLarge`, `Canceled`, `SomeFound`)?
  - **Forbidden**: raw `Fail()` with manual `statusCode` when a factory exists.
  - **Allowed**: raw `Fail` ONLY when no factory matches (e.g. re-mapping arbitrary upstream status codes).
  - Evidence: `grep -rEn '\.Fail\(' <scope>` → per hit, justify or convert.
  - **Partial-success pattern**: `NOT_FOUND` (none found) → `SOME_FOUND` (partial, data returned) → `OK` (all found).
  - **If a typed/generic semantic factory is missing** that should exist (e.g. `D2Result<T>.ServiceUnavailable()`): that's a bug in `D2.Shared.Result`, not a justification for raw `Fail`. Add the factory.

- **5.4** Does code at boundaries (proto/DB/external) use `.ToNullIfEmpty()` instead of letting `""` survive into domain types?
  - Evidence: per boundary → `ToNullIfEmpty` confirmed.
  - **Returns**: `null` if the string is null, empty, or whitespace-only (trims first).

- **5.5** Is `string.Empty` used everywhere instead of `""` (StyleCop SA1122)?
  - Evidence: build clean confirms.

### Syntax and structure

- **5.6** Are extension methods using C# 14 extension-members syntax (`extension(T target) { ... }`) instead of the old `this T` parameter style?
  - Evidence: per new extension → syntax confirmed.

- **5.7** Are all concrete classes / records / exceptions / attributes marked `sealed`?
  - **Carve-outs**: (1) types that are explicit base classes for other types in the codebase (e.g. `D2Result` stays unsealed because `D2Result<TData>` derives from it); (2) static classes (already implicitly sealed). Applies to test classes too (xUnit instantiates reflectively but does not subclass).
  - **Why**: enables JIT devirtualization on virtual / interface call sites and signals "this is not an extension point." Unsealing later is cheap; sealing retroactively is not.
  - Evidence: per new concrete type → `sealed` confirmed or carve-out documented.

- **5.8** Are single-line `if`/`while`/`for`/`foreach` bodies WITHOUT braces, and multi-line bodies WITH braces?
  - **Rule**: visually multi-line bodies (body wraps onto multiple lines because the body itself wraps, or a constructor / method call breaks across lines) ALWAYS get braces, regardless of how many statements they logically contain.
  - **Why**: a multi-line body without braces lets the next sibling statement visually merge with the body — the C dangling-`else` footgun, real source of bugs when refactoring.
  - **Two acceptable brace-less forms — use one or the other, no in-between**:
    - **Form A — single line**: `if (cond) return foo;` — entire if + body on one source line. No padding required (this is the standard guard-clause pattern at the top of methods).
    - **Form B — two-liner with `if` on its own line**: permitted ONLY with blank lines BOTH above AND below the if-block. The brace-less body needs visual breathing room; without padding, the body reads as a continuation of the surrounding sequential code.
    - Three-or-more-line bodies (or wrapped/multi-line bodies) MUST have braces — SA1519 already enforces this.
  - Evidence: spot-check new control flow; `grep -rEn '^[[:space:]]+if[[:space:]]\([^)]+\)[[:space:]]*$' <scope>` finds Form-B candidates whose padding then needs visual verification.

- **5.9** Is `this.` qualifier absent? (Codebase doesn't use it; field prefixes already disambiguate.)
  - Evidence: `grep -rEn 'this\.' <scope C# files>` → expect zero in introduced code.

- **5.10** Is `namespace` declared BEFORE `using` directives in every `.cs` file? (Codebase convention: file-scoped `namespace X;` on line N, blank line, then `using` block.)
  - Evidence: per new `.cs` file → ordering confirmed.

### Records, collections, options patterns

- **5.11** Are entities using `record` types with `required init` properties + empty collection initializers (`[]`)?
  - Evidence: per new entity → record + `required init` + collection-expression defaults.

- **5.12** Are collection expressions (`[a, b, c]` / `[]`) used instead of `new T { ... }` / `new[] { ... }` / `Array.Empty<T>()`?
  - **Required when** target type is `IEnumerable<T>` / `IReadOnlyList<T>` / `IList<T>` / `T[]` / `Span<T>` (or any constructible collection). Compiler picks the best concrete type for the slot (often a stack-allocated span or a single allocation) and call sites read at half the noise level.
  - **Allowed**: explicit `new List<T>()` / `new T[N]` when you need that exact concrete type, a specific capacity hint, or a mutable list reference.
  - **Hot-spots**: defaults for `IReadOnlyList<T>` parameters, fallback values in ternaries (`x ? values : [defaultValue]`), single-item array args.
  - Evidence: per collection literal → expression form.

- **5.13** Do small Options records (≤4 properties) use the nullable-param ctor + `?? default` body pattern? Pattern at canonical `D2.Shared.Resilience.CircuitBreaker.CircuitBreakerOptions`.
  - **Shape**: parameterized ctor with EVERY param nullable + `?? default` body assignment, plus a parameterless ctor that chains `: this(null, null, ...)` to inherit defaults. Yields `new(failureThreshold: 3)` / `new()` / `new(3, TimeSpan.FromSeconds(5))` call sites.
  - **For 5+ properties**: stay on init-only properties + object initializer (positional ordering becomes hard to read).
  - **Sentinel-free**: explicit non-null values (including `0` / `TimeSpan.Zero`) pass through unchanged.
  - Evidence: per new Options record → pattern confirmed.

- **5.14** Are nullable types used for optional domain fields (`string?`, `bool?`, `int?`, `DateTime?`)? Never `= string.Empty` on optional record properties. `null` = "not provided."
  - Evidence: per optional field → `?` form + no empty-string default.

### Async / threading / asynchrony

- **5.15** Is `ValueTask` not awaited more than once? (Cross-ref §4.6.)

- **5.16** Are async methods consistently named with the `Async` suffix?
  - Evidence: per async method → suffix confirmed.

- **5.17** Are `await`-ed calls passed a `CancellationToken` whenever the API supports it?
  - Evidence: per `await` of a ct-accepting API → ct passed.

### Public-API surface

- **5.18** Do all `public` types / methods have XML doc comments?
  - **Format**: `<summary>`, `<param>`, `<returns>`, `<exception>` as appropriate. Wrap onto multiple lines if needed for line-length compliance.
  - **Synchronously-invoked callback parameters** (`onX`, `onSomething`, `Action<>` / `Func<>` ctor args invoked inside `lock` / on the calling thread) MUST document throw-behavior in their XML `<param>` block — specifically, what happens to the upstream exception if the callback throws. Default platform behavior is "the thrown exception REPLACES the upstream exception that triggered the invocation" — a buggy logger inside the callback can silently swap a meaningful "TimeoutException from upstream X" for "InvalidOperationException from logger" and make outage diagnosis painful. Document loudly so callers wrap in their own try/catch (or stick to log/metric calls that won't throw).
  - Evidence: per new public symbol → `<summary>` confirmed; per callback param → throw-behavior `<para>` block confirmed.

- **5.19** Does each handler implement its declared interface (for DI registration)?
  - Evidence: per new handler → interface implementation confirmed.

### Regex (ReDoS discipline)

- **5.20** Do `[GeneratedRegex]` patterns classify into the right backtracking bucket and apply timeout discipline accordingly?
  - **Bucket 1 — no backtracking → NO timeout.** Single greedy quantifier with no following pattern (`\s+`); single char-class match with no quantifier (`[^\d]`, `[^\p{L}\p{N}\s\-'.,]`); quantifier whose char class is disjoint from the next required token (`\w+\}` — `\w` can't match `}`).
  - **Bucket 2 — linear backtracking AND input upstream-bounded → NO timeout.** Greedy quantifier followed by an overlapping required token, but each backtrack attempt is O(1) and total attempts grow at most linearly with input length. Example: `[^@\s]+\.[^@\s]+`. Document the linear-time guarantee + the input-length assumption in the pattern's XML doc so future-you can audit when adding new call sites.
  - **Bucket 3 — super-linear backtracking → set tight `matchTimeoutMilliseconds` (10–25 ms) AND pre-warm the JIT.** Nested quantifiers (`(a+)+`, `(a*)*`); alternation with overlap (`(a|aa)+`); polynomial / exponential backtracking. Pre-warm via `static readonly bool sr_jitWarmedUp = WarmUpHelper();` field initializer so first user-visible call doesn't pay JIT cost inside the timeout window.
  - **Document the bucket** in the pattern's `<summary>`. A pattern change that promotes Bucket-1/2 → Bucket-3 needs a timeout + pre-warm added in the same edit.
  - **Why**: ReDoS attacks rely on super-linear backtracking. A *tight* timeout on linear patterns occasionally fails under GC pauses / scheduling jitter even on sub-microsecond matches.
  - Evidence: per new `[GeneratedRegex]` → bucket classification + matching timeout discipline.

### Build cleanliness (zero tolerance)

- **5.21** Does `dotnet build server/D2.slnx` produce zero StyleCop (SA****), CS**** warnings, null ref warnings? Never suppress with `#pragma warning disable`, `!` (for silencing warnings), or analogous.
  - Evidence: build output.

- **5.22** Does `jb inspectcode server/D2.slnx --severity=WARNING` produce zero JetBrains/Rider warnings? (Catches `[MustDisposeResource]` misuse, captured variable/closure issues, `AccessToModifiedClosure`, `AccessToDisposedClosure` — invisible to `dotnet build`.)
  - Evidence: inspectcode output.

- **5.23** Are ALL warnings/errors encountered ANYWHERE in the project fixed (zero-tolerance)? Never dismiss as "pre-existing."
  - Evidence: `git diff main` cross-check confirms no leftover warnings.

- **5.24** Foundational shared libs (the lib that DEFINES a convention) MUST eat their own dogfood. The lib that exports `Falsey()` cannot use `string.IsNullOrEmpty` internally; the lib that exports `TryParseTruthyNull` cannot hand-roll `Guid.TryParse` + null check; the lib that exports the `[RedactData]` attribute cannot log raw user input. Foundation libs are the strictest dogfood site in the codebase — any lib that publishes a "use this not that" helper must be the canonical demonstration of using it.
  - Evidence: when auditing a foundation lib, grep its OWN source for the forbidden patterns the convention prohibits; expect zero hits.

---

## 6. TypeScript / SvelteKit Code Conventions

### Predicates

- **6.1** Is TypeScript `strict` mode enabled in every `tsconfig.json`?
  - Evidence: per `tsconfig.json` touched → `"strict": true` confirmed.

- **6.2** Are type-only imports using `import type { ... }` syntax?
  - Evidence: per import touching only types → `import type` form.

- **6.3** Is `undefined` preferred over `null` for "absent" semantics? Use optional syntax (`field?: string`) instead of `field: string | null`.
  - **Exception**: explicit three-state semantics for pre-auth flags (`boolean | null`).
  - Evidence: per new optional field → `?: T` form (not `T | null`).

- **6.4** Is `truthyOrUndefined()` used at boundaries (user input, DB rows, proto values → domain types)? Returns `undefined` if the string is null, empty, or whitespace-only.
  - Evidence: per boundary → call confirmed.

- **6.5** Do Zod schemas use `.optional()` (not `.nullable()` / `.nullish()`) for domain-aligned validation?
  - **Why**: domain types use `?: T` (undefined), so Zod must match.
  - Evidence: per new Zod schema → `.optional()` form.

- **6.6** Does `pnpm exec svelte-check` produce zero errors / warnings in `server/web/`?
  - Evidence: command output.

- **6.7** Does `pnpm exec eslint .` produce zero warnings in `server/web/`?
  - Evidence: command output.

- **6.8** Does `pnpm exec prettier --check .` produce zero formatting failures in `server/web/`?
  - Evidence: command output.

- **6.9** Were diagnostics checked via `mcp__cclsp__get_diagnostics` after every TS edit?
  - Evidence: tool-call history shows diagnostic checks.

### SvelteKit BFF specifics

- **6.10** Are REST client modules the ONLY place that calls `fetch`? Components and pages call client functions, never `fetch("/api/...")` directly.
  - **Layout**: `$lib/client/rest/*-client.ts` modules expose per-feature client API and own credentials / headers / timeouts; `$lib/shared/rest/` holds isomorphic low-level helpers (e.g. `gateway-response.ts` — gateway response parser used by both server-side and browser-side clients). Raw `fetch()` allowed inside `$lib/shared/rest/` helpers AND inside `*-client.ts` files; NOT in components, pages, or any other path.
  - Evidence: `grep -rEn 'fetch\(' <scope>` → per hit, classify (allowed/forbidden).

- **6.11** Do components displaying async or server-loaded data show a `<Skeleton>` placeholder until the data is ready?
  - Evidence: per new async-data component → Skeleton confirmed.

- **6.12** Does every navigation use `resolve("/path")` from `$app/paths` instead of bare `href="/path"` / `goto("/path")`? (Without this, i18n locale routing breaks for non-default locales.)
  - Evidence: `grep -rEn 'href="/\|goto\("/' <scope>` → per hit, confirm `resolve` wrap or justify.

- **6.13** Are query strings appended outside the typed pathname call? `` `${resolve("/path")}?key=value` `` (NOT inside `resolve(...)`).
  - Evidence: per query-string usage → form confirmed.

- **6.14** Is the SvelteKit BFF pure SSR? (Browser → Edge directly for auth state mutations. Server-side route guards (`requireAuth`, `requireOrg`, etc.) at `server/web/src/lib/server/auth/`. Browser-side `authClient` at `server/web/src/lib/client/auth/`.)
  - Evidence: per new auth surface → location confirmed.

---

## 7. Naming, File Headers, Folder Casing

### C# Naming

- **7.1** Do C# identifiers follow the convention table?

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

- **Carve-out**: handlers using **primary constructors** — constructor parameters do NOT take `r_` prefix (they're parameters, not fields, even though they're accessed like fields inside the class body). The carve-out applies ONLY to handler primary-constructor parameters; regular fields keep their prefixes.
  - Evidence: per new field / property / class → convention check.

- **Test-local naming clarification (avoiding the most common slip)**: in test code, `snake_case` is permitted ONLY for `const` declarations (e.g. `const int expected_count = 5;`). NON-`const` test locals — `var foo = ...`, `string[] foo = ...`, `out var foo`, etc. — MUST use `camelCase` per the "Local variables" row of the table above. Examples:
  - ✅ `const int expected_count = 5;` (test-local const → snake_case)
  - ✅ `var sessionId = Guid.NewGuid();` (test-local non-const → camelCase)
  - ❌ `var session_id = Guid.NewGuid();` (snake_case is for consts only)
  - ❌ `out var claim_value` (out-vars are not consts → camelCase)
  - **Why**: `snake_case` for non-const locals reads ambiguously (data fixture? const? something else?) and the rule table is explicit. The `_` is a marker for compile-time inlining (constants) — using it on mutable / out-bound locals dilutes the signal.

### TypeScript Naming

- **7.2** Do TypeScript identifiers follow: `camelCase` for variables/functions, `PascalCase` for types/classes/interfaces/components, `kebab-case` for module file names?
  - Evidence: per new identifier / file → convention check.

### Folder casing

- **7.3** Are folders OUTSIDE a project (csproj-grouping, organizational) lowercase / kebab-case for multi-word? (`server/`, `services/`, `edge/`, `app/`, `clients/`, `dotnet/`, `caching-distributed-redis/`, `geo-reference/`, `service-defaults/`, `infra/`, `tools/`, `docs/`)
- **7.4** Are folders INSIDE a project (namespace-mapping, where Rider auto-creates folders from namespace operations) PascalCase? (`Implementations/`, `Interfaces/`, `CQRS/`, `Handlers/`, `C/`, `Q/`, `U/`, `X/`, `Repository/`, `Messaging/`)
- **7.5** Are `.cs` file names PascalCase (matching the type they contain — one-class-per-file)?
- **7.6** Are `.csproj` file names PascalCase, dot-separated (`D2.Shared.Handler.csproj`) — the csproj filename IS the assembly name?

> **The rule**: if Rider auto-generates a folder from a namespace operation, that folder must be PascalCase. Anything else is lowercase.

### File headers

- **7.7** Does every source file you created or modified carry the standard copyright header for its language? Files that don't support comments (`.json`, `.lock`, `.snap`) and machine-generated files (EF migrations, paraglide compile output, proto-codegen, husky shims, JetBrains `.idea/`, lock files) are exempt.

#### Header forms by comment family

**`//` line comments** — C#, TypeScript, JavaScript, Proto, Go, Rust, Java (`.cs`, `.ts`, `.tsx`, `.js`, `.cjs`, `.mjs`, `.proto`, `.go`, `.rs`, `.java`):

For C# (`.cs`) — StyleCop SA1633 enforces XML `<copyright>` element:
```csharp
// -----------------------------------------------------------------------
// <copyright file="FileName.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------
```

Everything else in this family:
```ts
// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------
```

**`/* */` block comments** — CSS / SCSS / LESS:
```css
/* -----------------------------------------------------------------------
 * Copyright (c) DCSV. All rights reserved.
 * ----------------------------------------------------------------------- */
```

**`#` line comments** — Bash, YAML, Dockerfile, PowerShell, Makefile, Grafana Alloy, env files, gitignore-family, editorconfig, npmrc, prettierignore, TOML, INI, Python, Ruby, R.

For shebang-bearing files, shebang stays on line 1; header follows:
```bash
#!/usr/bin/env bash
# -----------------------------------------------------------------------
# Copyright (c) DCSV. All rights reserved.
# -----------------------------------------------------------------------
```

For files without shebang, header is line 1.

**`<!-- -->` HTML-comment block** — Markdown, HTML, Svelte, Vue:
```markdown
<!--
Copyright (c) DCSV. All rights reserved.
-->
```

For `.svelte` / `.vue`, the header lives at the very top of the file before `<script>` / `<template>`.

**`<!-- -->` XML-comment block** — XML, csproj, slnx, props, targets:
```xml
<Project>
  <!--
  Copyright (c) DCSV. All rights reserved.
  -->
  ...
</Project>
```
(or before the root element when an `<?xml ... ?>` declaration is present)

**`--` line comments** — SQL, Lua, Haskell, Ada:
```sql
-- -----------------------------------------------------------------------
-- Copyright (c) DCSV. All rights reserved.
-- -----------------------------------------------------------------------
```

Evidence: per new/modified file → header line 1 confirmed (or shebang + line 2).

#### Adding a new language

If you encounter a language not listed above and it supports comments, the header content stays `Copyright (c) DCSV. All rights reserved.` — only the comment delimiter changes.

### Translation key naming

- **7.8** Do translation keys follow the convention?
  - Auth pages: `auth_{feature}_{purpose}` (e.g., `auth_sign_in_title`)
  - App pages: `webclient_app_{page}_{purpose}` (e.g., `webclient_app_profile_title`)
  - Design/demo/debug: `webclient_{section}_{purpose}` (e.g., `webclient_debug_session_title`)
  - Common UI/errors: `common_ui_*` / `common_errors_*`
  - Backend handler messages: use `common_errors_*` keys where possible
  - Reuse existing keys where they match
  - Evidence: per new key → convention check.

### Scope vs Permission terminology

- **7.9** Does code use **"scope"** as the primary term throughout (not "permission")? JWT carries them as the OAuth-canonical `scope` claim (space-separated string). Code references them as constants in `D2.Shared.Auth.Scopes`.
  - Evidence: per new code touching authz → "scope" terminology.

### Git conventions

- **7.10** Do branch names follow the prefix convention? `feat/...`, `fix/...`, `docs/...`, `refactor/...`, `test/...`, `infra/...`, `chore/...`, `ci/...`.

- **7.11** Do commits use conventional-commit format with scope? (`feat(edge): add primary locales`).

- **7.12** Are `Co-Authored-By` lines absent? (Enforced by `.husky/commit-msg` hook; will reject if present.)

- **7.13** Are markdown tables in committed docs aligned for plain-text readability?
  - Evidence: per touched markdown table → alignment check.

### Universal style (applies across all source + KEEP docs)

- **7.14** Are lines ≤ 100 chars in `.cs` / `.ts` / `.tsx` source?
  - **Apply to**: source code, XML doc summaries, parameter lists, string literals.
  - **Wrap strategies**: break long XML doc summaries onto multiple lines; split long parameter lists across lines; break long string literals into concatenations or interpolations across lines; extract long expressions into named locals.
  - **Allowlist**: rare unbreakable long URLs / connection strings / encoded strings — note the reason in the surrounding comment (`// long URL — cannot wrap`).
  - **Why**: enforces visual scannability; reflects on a 13" laptop without horizontal scroll; review diffs are sane.
  - Evidence: `awk 'length > 100' <new/modified .cs/.ts files>` returns expected/empty.

- **7.15** Is all spelling American English (no British / Canadian variants)?
  - **Apply EVERYWHERE**: comments, doc strings, identifier names, README text, log messages, error messages, test names, commit messages.
  - **Common splits**:
    - `behavior` not `behaviour` (and `behaviors`, `behavioral`)
    - `color` not `colour` (and `colors`, `colored`, `coloring`)
    - `analyze` not `analyse` (and `analyzed`, `analyzing`, `analyzer`, `analysis` is identical)
    - `honor` not `honour` (and `honored`, `honoring`)
    - `canceled` not `cancelled` (single L — matches BCL `OperationCanceledException`); `canceling` not `cancelling`; `cancellation` is identical (double L is correct here)
    - `favorite` not `favourite`
    - `defense` not `defence`
    - `recognize` not `recognise` (and `recognized`, `recognizing`)
    - `optimize` not `optimise` (and `optimized`, `optimizer`, `optimization`)
    - `organization` not `organisation` (and `organize`, `organized`)
    - `prioritize`, `customize`, `categorize`, `utilize`, `realize`, `minimize`, `maximize`, `emphasize`, `criticize`, `summarize`
    - `program` not `programme`
    - `modeled`, `modeling` (single L) — not `modelled`, `modelling`
    - `signaled`, `signaling`, `labeled`, `labeling`, `traveled`, `traveling`
    - `neighbor` not `neighbour`
  - **Allowlist**: proper nouns, third-party identifiers (e.g. a UK org's name), quoted user content. Note inline (`// proper noun — keep British spelling`).
  - **Audit grep**: word-bounded grep for the exact list above (`grep -wEn 'analyse|colour|behaviour|cancelled|honour|synchronise|recognise|organisation|favourite|defence|programme|neighbour|labelled|labelling|modelled|modelling|travelled|travelling|signalled|signalling'`) per scope returns expected/empty.
  - Evidence: per-scope grep result.

- **7.16** Are comments minimal? Default to writing **NO comments**. Add one only when the WHY is non-obvious — a hidden constraint, a subtle invariant, a workaround for a specific bug, behavior that would surprise a reader.
  - **Forbidden**:
    - Explaining WHAT the code does (well-named identifiers do that — `_userMaxRetries = 3` doesn't need `// max retries for user`)
    - Referencing the current task / fix / callers (`// used by X`, `// added for the Y flow`, `// handles the case from issue #123`) — those belong in the PR description and rot as the codebase evolves
    - Multi-paragraph docstrings on non-public symbols
    - Multi-line comment blocks (>1 line) for trivial explanations
    - Commented-out code (delete it; git remembers)
    - Conversation-scoped IDs (`// F2_ regression`, `// audit decision X`) — see §14
  - **Allowed**:
    - One short line max for non-obvious WHY
    - XML doc comments on public APIs (per §5.18)
    - Long-form `<remarks>` blocks on public APIs explaining edge cases / invariants / when-to-call-vs-not
    - Bucket classification on `[GeneratedRegex]` (per §5.20)
  - Evidence: per new/modified file → comment audit (every comment justifies its existence).

- **7.17** Are commit messages following conventional-commit format AND describing the "why" (not just the "what")?
  - **Format**: `type(scope): summary`. Types: `feat`, `fix`, `docs`, `refactor`, `test`, `infra`, `chore`, `ci`. Scope: lib / service / area touched.
  - **Body** (when needed): explains motivation, trade-offs, what alternatives were rejected, what to watch for. Wrap at 72 chars in body.
  - **Forbidden**: `Co-Authored-By` lines (enforced by husky hook).
  - Evidence: per commit → format + body audit.

- **7.18** Is the commit `type` correctly classified? Confusing `fix` with `chore` / `refactor` is the most common slip.
  - `feat` — wholly new feature / capability that didn't exist before.
  - `fix` — something was BROKEN (incorrect behavior, exception, security flaw, regression) and is now fixed. Reserved for actual bug fixes.
  - `chore` — dependency removal, version bumps, pipeline consolidation (e.g. removing direct Loki sink in favor of Alloy-only), config cleanup, tooling updates. NOT a bug fix even if it removes a problematic dep.
  - `refactor` — restructuring without observable behavior change (rename, file move, extract method, change internal data structure, no new feature, no bug fix).
  - `docs` — documentation-only changes (README, comments, doc files).
  - `test` — test-only changes (adding coverage, refactoring tests, no production code change).
  - `infra` — infrastructure / deployment / Docker Compose / CI runtime / observability stack.
  - `ci` — CI workflow / GitHub Actions / pre-commit hook config (NOT runtime infra).
  - **When in doubt**: lean `chore` over `fix`. `fix` should be defensible as "something user-observable was broken; now it isn't."
  - Evidence: per commit → type classification justified.

- **7.19** When creating a PR, does the body follow `.github/pull_request_template.md` (Summary / Changes / Details / Testing / Checklist sections)?
  - **Why**: reviewers (including any GitHub Copilot review bot) expect the standard format; deviating creates friction.
  - **How**: read `.github/pull_request_template.md` first; fill in each section; use the predefined Changes list (Documentation / New feature / Bug fix / etc.) rather than freeform bullets.
  - Evidence: PR body matches template structure.

---

## 8. Build & Tooling Hygiene

### Predicates

- **8.1** Was any service started manually (`dotnet run`, `pnpm dev`, `pnpm preview`, any long-running server) outside of a test that self-manages its infrastructure (Testcontainers, child processes with cleanup)?
  - **Why**: services are managed by Docker Compose; manual starts collide with the supervised processes.
  - Evidence: tool-call history check.

- **8.2** Did any host `dotnet build` run while .NET containers were active? (Crashes geo/gateway/signalr via shared `obj/` mount; always build inside container or stop all .NET containers first.)
  - Evidence: `docker compose ps` state check around build commands.

- **8.3** Did any `pnpm install` run mid-session without coordinating Node container restarts? (Rotates symlinks; breaks every Node container.)
  - Evidence: tool-call history check + container restart trace.

- **8.4** If Docker Compose was running, were affected containers verified healthy (`docker compose --env-file .env.local --env-file .env.secrets ps`) after changes? Were any unhealthy containers restarted?
  - Evidence: `ps` output + restart trace.

- **8.5** When editing shared `.NET` libs in `server/shared/dotnet/`, was `dotnet build server/D2.slnx` run to verify all consumers still compile?
  - Evidence: build output.

- **8.6** Are dependencies / NuGet packages added intentionally? (Don't add a new dep when an existing utility / shared lib covers the need. New deps need explicit justification — security surface, license, maintenance burden.)
  - Evidence: per new `<PackageReference>` / `<dependency>` → justification + `Directory.Packages.props` entry.

- **8.7** When verifying SvelteKit / Node code, was `pnpm build` (root) used (which runs `pnpm run format && pnpm run lint && pnpm -r run build` — auto-fixing formatting, linting, then building all packages) — NOT just `pnpm format:check` + `pnpm lint` + individual `tsc`?
  - **Why**: `pnpm format:check` only reports issues without fixing them. `pnpm build` is the canonical verification step.
  - Evidence: tool-call history shows `pnpm build` invocation.

---

## 9. Architectural Layer Hygiene

The most expensive class of failure — wrong layer chosen at design time, code already written, refactor cost is high. The goal is to catch this at the PLAN phase via "alternatives considered" notes; the audit catches what slipped through.

### Predicates

- **9.1** For every public-API decision made in this scope (which constructor of a third-party type, which interface to implement, which layer to put a check at, which lib to depend on), are alternatives documented in the journal with rejection rationale?
  - Evidence: per design decision → journal entry showing alternatives + why rejected.
  - **Real example**: OIDC `ConfigurationManager` 2-arg ctor uses default static HttpClient (bypasses `IHttpClientFactory`). 3-arg ctor takes our named client. Without "alternatives considered" notes, the wrong ctor was picked silently.

- **9.2** Are JWT signature / expiry / audience / fingerprint-binding validations at the TRANSPORT layer (auth middleware), NOT on per-handler `HandlerOptions`?
  - **Why**: same validation running twice is redundancy, not depth. Per-handler opt-out becomes a token-relay vector. Audience doesn't VARY per handler; it's a per-service constant.
  - **Allowed per-handler**: `RequiredScopes` (varies by operation), `LogInput`/`LogOutput` toggles, slow/critical thresholds.
  - **Reading `IAuthContext.Audience` from a handler is fine** — handlers may inspect "what audience was this token minted for" for audit logging. The validation toggle doesn't belong; the property is just data exposure.
  - Evidence: per `HandlerOptions` consumer → confirm `ValidateAudience` not per-handler.

- **9.3** Do endpoints derive org/user scope from session/claims, NEVER from user-supplied input? (IDOR prevention.)
  - Evidence: per new endpoint → request shape confirmed; no `userId`/`orgId`/`role` in body when session has them.

- **9.4** Do handlers validate input via smart-constructor `Domain.Create(input) → D2Result<Domain>` at the TOP of `ExecuteAsync`, then `BubbleFail` on the result? Primitive-level rules use `string?.TryParse*` from `D2.Shared.Utilities`; cross-field rules belong in the composite `Create`.
  - **Why**: never let Redis / DB be the first to reject invalid data.
  - Evidence: per new handler → first 3 lines of `ExecuteAsync` confirm.

- **9.5** Do health checks use the SAME code path as production (e.g. EF Core, not raw `pool.query()`)?
  - **Why**: a check that bypasses the ORM won't detect ORM-layer issues.
  - Evidence: per health check → code path confirmed.

- **9.6** Do gRPC outbound calls use `handleGrpcCall` / equivalent error wrapper, NOT manual try/catch per handler?
  - Evidence: per gRPC outbound site → wrapper confirmed.

- **9.7** Do RabbitMQ publishes use `handlePublish` / equivalent error wrapper, NOT manual try/catch per publisher?
  - Evidence: per publish site → wrapper confirmed.

- **9.8** Has any new shared lib's `<ProjectReference>` set been checked against the dep graph in `server/shared/dotnet/README.md`? Was that graph updated in the SAME change?
  - **Note**: chart uses solid arrows for `<ProjectReference>` and dashed arrows for `OutputItemType="Analyzer"` (build-time-only) edges.
  - Evidence: graph diff matches code diff.

- **9.9** Does multi-instance migration safety use a PG advisory lock (only one replica migrates, others wait)?
  - Evidence: per migration runner → lock acquisition confirmed.

- **9.10** Was every database migration generated via the appropriate generator? NEVER hand-write migration `.cs` / `.sql` / snapshot / journal files for any platform.
  - **.NET (EF Core)**: `dotnet ef migrations add <Name>`. Hand-writing puts EF Core's internal model snapshot out of sync with actual schema — future diffs miscompute silently.
  - **Node (Drizzle)**: `pnpm db:generate --name <short_description>` from the package directory (e.g. `cd backends/node/services/auth/infra && pnpm db:generate --name short_description`). NEVER hand-write `_journal.json` entries, `meta/{N}_snapshot.json`, or migration SQL files. Picking a `when` timestamp arbitrarily (e.g. far-future) silently blocks every subsequent generated migration — drizzle's runtime applied-check is `Number(lastDbMigration.created_at) < migration.folderMillis`.
  - **If the generator fails**: STOP and ask. Do not patch by hand.
  - **If you already poisoned a Drizzle journal** (one-time repair only, with explicit user approval): edit `_journal.json` `when` to a real past timestamp slotted between neighbors → `UPDATE drizzle.__drizzle_migrations SET created_at = <new_when> WHERE id = <row>` → restart service.
  - Evidence: per new migration file → `git log` shows generator output, not hand-edits.

- **9.11** Does sync between services go via gRPC (HTTP/2)? Async via RabbitMQ? Sensitive RMQ payloads encrypted via `D2.Shared.Encryption`?
  - Evidence: per inter-service call → transport confirmed; per sensitive payload → encryption confirmed.

- **9.12** Do all notification deliveries go through D2.Courier → contact resolution? (No direct emails / texts from any other service.)
  - Evidence: per delivery site → courier path confirmed.

- **9.13** Do auth flags (`IsAuthenticated`, `IsTrustedService`, `IsUserImpersonating`) initialize to `null` (not `false`)? `null` = "not yet determined"; `false` = "confirmed not."
  - Evidence: per declaration → `bool?` confirmed.

- **9.14** For every nullable domain field, is the corresponding proto field declared `optional`? (Receivers must distinguish "not provided" from `""` / `0` / `false`.)
  - Evidence: per nullable domain field → proto declaration.

- **9.15** Are empty strings (`""`) NEVER used to represent absent data? Allowed only: Svelte `bind:value` form init, string concatenation building, `string.Empty` in C# hash/fingerprint computation, OTel span attributes (SDK requires non-null).
  - **At all other boundaries** (user input, DB, proto mapping): convert empty strings via TS `truthyOrUndefined()` or C# `.ToNullIfEmpty()`.
  - Evidence: per `""` literal in scope → classify (allowed/convert).

- **9.16** Does CORS `allowHeaders` include every custom `X-D2-*` header any middleware reads in this scope? (Missing → preflight blocks.)
  - Evidence: per new `X-D2-*` read → CORS config check.

- **9.17** Are infrastructure paths (health, metrics, OIDC discovery) exempt from ALL business middleware via shared `InfrastructurePaths.IsInfrastructure()`? (No per-middleware bypass.)
  - Evidence: per new infra path → `InfrastructurePaths` membership confirmed.

- **9.18** Are multi-column key lookups using paired predicates `(col1=A AND col2=1) OR (col1=B AND col2=2)`, NOT independent `OR`s producing cross-product false positives?
  - Evidence: per multi-column lookup → predicate shape.

- **9.19** Does any code introduce a NEW pattern when an existing one would fit? (Don't invent — follow existing patterns. If no pattern fits, ASK before inventing.)
  - Evidence: per new pattern shape → justification.

- **9.20** Does any handler return `Ok()` after a branching operation unconditionally? (If a nested handler / provider can fail, check its result. Returning `Ok()` after a try/catch that swallows failures is almost always a bug. Either `BubbleFail` or explicitly handle the error.)
  - Evidence: per `ExecuteAsync` returning `Ok` → branching trace confirmed.

- **9.21** Was DI registration verified for every new handler? (Missing registrations are silent at compile time and only crash at runtime. After creating a handler, immediately add its registration.)
  - Evidence: per new handler → registration line in extension method.

- **9.22** Does each layer export an `AddXxx(services)` extension method for its DI registration?
  - Evidence: per layer → extension method.

- **9.23** Is the SAGA pattern used for cross-service updates? (E.g. Geo-first → Auth-second → compensate Geo on auth failure → fatal log if rollback fails.)
  - Evidence: per cross-service update → SAGA shape confirmed.

- **9.24** Is the TLC/2LC/3LC folder convention followed?
  - **TLC** = architectural concern (CQRS / Messaging / Repository / Caching)
  - **2LC** = implementation type (Implementations / Interfaces)
  - **3LC** = operation type — varies by TLC:
    - CQRS: `C/` Commands, `Q/` Queries, `U/` Utilities, `X/` Complex
    - Messaging: `Pub/` Publishers, `Sub/` Subscribers
    - Repository: `C/` Create, `R/` Read, `U/` Update, `D/` Delete
    - Caching: `C/` Create, `R/` Read, `U/` Update, `D/` Delete
  - Interfaces live in `Interfaces/{TLC}/Handlers/{3LC}/`. Implementations live in `Implementations/{TLC}/Handlers/{3LC}/` (app layer) or `{TLC}/Handlers/{3LC}/` (infra layer).
  - Evidence: per new handler → folder placement matches.

- **9.25** Are CQRS handler categories correctly chosen?
  - **Query**: no distributed cache write, no DB write, no external API, no message publish. Test: "if process dies after, would state persist?" → No.
  - **Command**: any of those happen. Primary intent = mutation of persistent/shared state.
  - **Complex**: primary intent = retrieval, but may mutate as side effect.
  - **Local/in-memory caching is always OK** (instance-scoped, ephemeral).
  - Evidence: per new handler → category justification.

- **9.26** Are verb semantics correct? **Find** = "Resolve this for me" (may fetch from external source, may cache/persist; e.g., `FindWhoIs`). **Get** = "Give me this by ID" (direct lookup, read-only; e.g., `GetWhoIsByIds`).
  - Evidence: per new method → verb semantic confirmed.

- **9.27** Are content-addressable entities (`Location`, `WhoIs`) using SHA-256 hash IDs (64-char hex) computed via factory method? (Enables dedup.)
  - Evidence: per content-addressable entity → factory + hash form.

- **9.28** Are mappers using C# 14 extension members (`extension(Entity e) { public DTO ToDTO() { ... } }`) and living in `{Service}.App/Mappers/`?
  - Evidence: per new mapper → location + syntax.

- **9.29** Are batch operations using `input.HashIds.Chunk(_BATCH_SIZE)` via Options pattern (default 500)?
  - Evidence: per batch op → chunking confirmed.

- **9.30** Is .NET ↔ Node platform parity preserved for any cross-platform shared concept?
  - **Applicability**: this predicate fires only when BOTH platforms are in active build for the relevant area. If the Node side hasn't been built yet (current state for most shared libs), the .NET side is NOT blocked — but the parity gap MUST be tracked so the Node mirror is on the roadmap when that platform comes online. Re-flagging the same gap on every shared-lib audit before Node is being built is noise; tracking it once with a documented disposition is sufficient.
  - **Rule (when applicable)**: if a concept is a separate project / package on one platform (e.g. `D2.Shared.Translation.Default`), it MUST be a separate package on the other (e.g. `@d2/translation`). Same naming theme, same responsibility boundaries.
  - **Why**: developers working in either platform transfer mental models instantly when packages mirror each other. Divergence creates confusion + duplicate concept names that drift over time.
  - **When creating any new shared package on one platform**: check if the corresponding package exists on the other. If both platforms are active, create the mirror in the same change. If only one platform is active (current state for v2 .NET-only libs), document the parity gap as a tracked deliverable for when the other platform builds.
  - **Cross-ref**: [docs/PARITY.md](../PARITY.md) tracks intentional cross-platform parity + the "Why exclusive?" framework for genuinely platform-specific tools.
  - Evidence: per new shared package → corresponding-platform package exists, OR is in tracked plan, OR §9.30 is documented as not-yet-applicable for this lib's domain.

---

## 10. Security (Endpoints / Auth / Secrets / Input)

Cross-references [docs/AUDIT_CHECKLIST.md](../AUDIT_CHECKLIST.md) "Security" section. Predicates here recur and need explicit checking. **D²-WORX is being built to ship to production with real users; security predicates are non-negotiable.**

### Predicates

- **10.1** Do all list queries enforce pagination limits (default 50, max 100)?
  - Evidence: per list query → limit cap confirmed.

- **10.2** Are PG constraint errors caught and mapped to appropriate HTTP status (PG `23505` → 409 Conflict, not 500)?
  - Evidence: per PG-touching path → catch + mapping confirmed.

- **10.3** Is auth middleware visible at the route declaration (`.RequireAuth()`, `.RequireServiceKey()`, `.RequireOrg()`)? Not implicit.
  - Evidence: per new route → middleware decoration visible.

- **10.4** Are new JWT custom claims namespaced with `d2_` (snake_case — `act["d2_kind"]`, `d2_session_id`)? Documented in `docs/JWT-CLAIMS.md` (when published)?
  - Evidence: per new claim → prefix + doc.

- **10.5** Are sensitive IDs absent from JWT (admin user IDs, internal audit data stays server-side / session only)?
  - Evidence: per JWT mint → claim audit.

- **10.6** Does auth middleware fail-closed on missing config (empty service-identity client mappings or missing secrets = 401 immediately, never silently bypass)?
  - Evidence: per auth middleware → fail-closed branch confirmed.

- **10.7** Does sign-out clear ALL auth state? (Cross-ref §3.8.)

- **10.8** Are API key / token / secret comparisons constant-time? (Cross-ref §3.9.)

- **10.9** Is multi-instance migration safety enforced? (Cross-ref §9.9.)

- **10.10** Are SQL queries parameterized (no string concatenation building SQL)?
  - Evidence: per `dbContext.FromSqlRaw` / `db.Database.ExecuteSqlRaw` → parameterization confirmed.

- **10.11** Is user-rendered HTML escaped / sanitized (XSS prevention)? (Svelte handles this natively for `{interpolated}` content; raw HTML via `{@html ...}` requires explicit sanitization.)
  - Evidence: per `{@html}` use → sanitizer call.

- **10.12** Are CSRF protections in place for state-mutating browser forms? (Built-in to SvelteKit form actions when used correctly; bypassed if you call mutating APIs directly via fetch with credentials but no CSRF token.)
  - Evidence: per state-mutating endpoint → CSRF strategy.

- **10.13** Does rate limiting protect every public endpoint (per the rate-limit tier system in `docs/RATE-LIMITING.md`)?
  - Evidence: per new endpoint → rate-limit tier assignment.

- **10.14** Are session cookies `HttpOnly` + `Secure` + `SameSite=Strict` (or `Lax` if cross-site links needed)?
  - Evidence: per cookie set → flag check.

- **10.15** Are uploaded files scanned for malware (via ClamAV / equivalent) before persistent storage?
  - Evidence: per upload path → scan step confirmed.

- **10.16** Are file-type validations done by content-sniffing (magic bytes), not just extension or `Content-Type` header (which are user-controlled)?
  - Evidence: per upload validation → content-sniffing confirmed.

- **10.17** Does session rotation happen on auth-state change (login, sign-out, password change, MFA enrollment)?
  - Evidence: per auth-state-change handler → session rotation confirmed.

- **10.18** Is JWT signing key rotation supported via the JWKS overlap pattern (old key valid during overlap window after new key published)?
  - Evidence: per key rotation flow → overlap window confirmed.

- **10.19** Are user passwords never logged, never sent in error messages, never persisted in session state?
  - Evidence: per password-touching code → secrecy audit.

- **10.20** Does the codebase NEVER log JWTs / API keys / OAuth tokens (even truncated)?
  - Evidence: per token-handling code → log audit.

- **10.21** Are URL parameters validated as untrusted? (Path traversal `../` blocked; ID-shaped params parsed via TryParseTruthyNull.)
  - Evidence: per URL param read → validation step.

- **10.22** Are admin / staff actions audit-logged with userId + targetId + action + timestamp + outcome?
  - Evidence: per admin/staff action → audit log entry.

---

## 11. Documentation Parity & Best Practices

Doc drift is constant unless the doc edit lives in the SAME change as the code edit. Checklist enumerations (telemetry tags, counter lists, config tables, public API surfaces) drift the moment you defer.

This category covers BOTH (a) keeping docs in sync with code (parity) AND (b) writing docs that are actually useful — structure, style, accuracy, brevity, and the absence of common anti-patterns.

### Predicates

- **11.1** Are doc edits in the SAME change as the code edits (not a separate commit)?
  - Evidence: per code change → corresponding doc change in same commit.
  - **Within-step ordering**: write code → write tests → verify tests pass → THEN write docs (in the same commit as the code, not as a separate follow-up). Docs reflect the FINAL state of the code, not the planned state — and the planned state often shifts during implementation. Updating docs first means re-updating them after the code stabilized; updating last means doing it once correctly. The "same commit" rule means same change, NOT "docs first." Rationale per `feedback_docs_after_tests`.

- **11.2** Do telemetry tag enumerations / counter lists / metric tables in READMEs match the code in this scope?
  - Evidence: per telemetry-related doc → enumeration matches `Counter.Add` call sites.

- **11.3** Does the per-lib `README.md` public-API list match the actual public surface (no removed methods listed, no added methods missing)?
  - Evidence: per lib → API list diff vs `public` symbol scan.
  - **Includes filenames**: every `.cs` filename mentioned in a README must exist at the named path. Codegen output paths (`obj/Generated/...`) are particularly drift-prone — a file rename in the source generator silently breaks every consuming README's "File layout" table without a build error. Audit grep: every `` `*.cs` `` token in README → corresponding `Glob` result confirming the file exists where the README says it does.
  - **Includes Required helpers**: when a per-lib README enumerates "Public API" or "Required helpers", the enumeration must cross-reference §5 / §16 — every "Required" / "Use this not that" helper has a section. A lib that publishes `Falsey()` / `TryParseTruthyNull` / `[RedactData]` cannot ship a README that omits them; the README is the discovery surface for new consumers.

- **11.4** Was [PATTERNS.md](../PATTERNS.md) updated for any new pattern introduced (handler / TLC / DI registration / `D2Result` factory usage / RedactionSpec / mapper / repo pattern)?
  - Evidence: per pattern introduced → PATTERNS.md edit.

- **11.5** Was the relevant doc per CLAUDE.md §3.5 Doc Update Map updated for cross-cutting changes (MESSAGING, OPERATIONAL-GUARANTEES, RATE-LIMITING, AUDIT_CHECKLIST, PARITY, SECURITY-RUNBOOKS)?
  - Evidence: per change → matching Doc Update Map row → doc edit.

- **11.6** Does the `server/shared/dotnet/README.md` Mermaid dep-graph reflect actual `<ProjectReference>` reality (solid arrows for `<ProjectReference>`, dashed for `OutputItemType="Analyzer"`)?
  - Evidence: graph diff vs csproj diff.

- **11.7** When phase progression / wipe state / open question / new tracked issue lands, is `docs/v2/PHASE_0.md` (or current tracking doc) updated?
  - Evidence: per tracking change → doc edit.

- **11.8** When an architectural decision overrides prior v2 plan, is `docs/v2/V2.md` updated AND noted in PHASE_0.md?
  - Evidence: per overriding decision → both docs.

- **11.9** Does any KEEP doc / README / source comment cite "CLAUDE.md §X" or reference `PHASE_*.md` / `V2.md` from outside `docs/v2/`?
  - **Why**: KEEP docs describe current reality, not the journey. CLAUDE.md is internal-to-Claude; readers don't need to know it exists.
  - Evidence: `grep -rEn 'CLAUDE\.md\|PHASE_[0-9_]*\.md\|V2\.md' <scope KEEP files>` → expect zero.

- **11.10** Does any doc describe what doesn't exist ("no org emulation", "no v1 X", "Why no Y" sections)? Describe what IS, not what isn't.
  - Evidence: scan.

- **11.11** Does any doc misframe shared infrastructure as scope-limited ("BaseHandler is for CQRS handlers", "D2Result is for ...")? Frame broadly or list multiple consumers (CQRS handlers, repo handlers, messaging consumers, scheduled jobs, anything handler-shaped).
  - Evidence: scan summary lines on shared infra types.

- **11.12** Does every project / module have a `README.md` (`server/services/{service}/README.md`, `server/shared/dotnet/{lib}/README.md`)? When adding new handlers / entities / config options / public APIs → was the relevant README updated?
  - Evidence: per change touching public surface → README edit.

- **11.13** Are user-facing copy strings (toasts, emails, modals) free of brand names? "Your account" not "your {ProductName}". Brand changes shouldn't require translation migrations.
  - Evidence: per user-facing string → brand-name audit.

- **11.14** Are XML doc summaries on public symbols accurate (describe what the method does + when to call it + what it returns + edge cases)?
  - Evidence: per public symbol → summary review.

### Documentation best practices (style, structure, brevity)

- **11.15** Do per-lib `README.md` files follow the standard structure?
  - **Required sections**:
    1. **Title + one-line purpose** at the top — what problem does this lib solve, in one sentence
    2. **Public API** — primary types / methods, what they do, when to call them
    3. **Configuration / Options** — Options records + their defaults + when to override
    4. **Dependencies** — what this lib depends on (project refs + external NuGet packages with versions)
    5. **Usage examples** — at least one realistic call site
    6. **Telemetry** — counters / spans / metrics emitted, with tag enumerations
    7. **Edge cases / gotchas** — known limits, failure modes, anything that surprised the author
  - **Optional sections**: Architecture diagram, Performance notes, Migration notes (when relevant)
  - Evidence: per new/touched lib README → section presence.

- **11.16** Do per-service `README.md` files include operational sections beyond the per-lib ones?
  - **Additional required**:
    1. **Run locally** — how to start (Docker Compose target, env vars needed)
    2. **Health check / debugging** — health endpoint URL, how to inspect logs / DLQ / DB state
    3. **External dependencies** — DB names, broker queues / exchanges, downstream services
  - Evidence: per service README → section presence.

- **11.17** Are XML doc comments on public types / methods complete?
  - **Required**: `<summary>` (what + when-to-call), `<param>` per parameter (purpose + constraints), `<returns>` (what's returned + edge case shapes), `<exception>` per documented throw (when), `<remarks>` for non-obvious invariants / threading guarantees / disposal expectations.
  - **Quality bar**: NOT just `<summary>does the thing</summary>`. Explain the WHY / WHEN / EDGE CASES that aren't obvious from the signature.
  - **Wrap to 100 chars** within doc comments (per §7.14); long summaries get multiple `///` lines.
  - Evidence: per public symbol → XML doc completeness check.

- **11.18** Do markdown docs use consistent style?
  - **Headings**: ATX-style (`#`, `##`, `###`); one H1 per file (the doc title).
  - **Tables**: aligned columns for plain-text readability (per §7.13).
  - **Links**: relative paths for in-repo links (`[label](../path/to.md)`); absolute URLs for external.
  - **Code fences**: triple-backtick with language tag (`` ```csharp ``, `` ```ts ``, `` ```bash ``) for syntax highlighting.
  - **Lists**: `-` for unordered (consistent), `1.` for ordered.
  - **Emphasis**: `**bold**` for strong, `*italic*` sparingly. No ALL-CAPS for emphasis (use bold).
  - **Line length**: markdown is NOT subject to §7.14's 100-char limit (long table rows + long URL refs are normal in docs); but try to keep prose paragraphs under ~120 chars for readability.
  - Evidence: per new/touched doc → style check.

- **11.19** Are docs free of these common anti-patterns?
  - ❌ **Historical narration** ("This used to use X but we switched to Y because...") — describe what IS, not what WAS.
  - ❌ **Comparison to nonexistent alternatives** ("unlike most libraries, this one...") — the reader doesn't know "most libraries"; just describe what THIS does.
  - ❌ **"Why no X" / "Why we don't have Y" sections** — describing absent things implies a reader who knows about them; doc the present, not the absent. (Exception: explicit migration / decision-record docs in `docs/v2/`.)
  - ❌ **Marketing prose** ("powerful", "robust", "elegant", "modern") — describe capabilities concretely.
  - ❌ **Multi-paragraph filler** in summary sentences — first sentence should hook + summarize.
  - ❌ **Stale code examples** — every code example must compile against current `main`.
  - ❌ **Unexplained acronyms** in user-facing docs (DLQ / TLC / 2LC OK in technical docs since they're project vocabulary; clarify on first use in onboarding docs).
  - ❌ **Self-references** ("see this very document below" / "as mentioned above") when a section anchor or link would do.
  - ❌ **CLAUDE.md / PHASE_*.md / V2.md citations from KEEP docs** (per §11.9).
  - Evidence: scan for each pattern.

- **11.20** Do docs describe what IS, not what isn't? (Reinforcement of 11.10.)
  - Forbidden framings: "no org emulation", "no v1 X", "Why no Y", "We don't do Z".
  - **Allowed**: explicit "Out of scope" sections in deliverable / planning docs (`docs/v2/`, deliverable READMEs) where rejection rationale is the point.
  - Evidence: scan.

- **11.21** Are docs brief? Long docs aren't more rigorous; they're harder to read and easier to drift.
  - **Heuristic**: per-lib README ≤ 300 lines, per-service README ≤ 500 lines, XML doc summary ≤ 5 lines (use `<remarks>` for longer).
  - **When more is needed**: split into linked sub-docs rather than one wall.
  - Evidence: line counts on touched docs.

- **11.22** Do all code examples in docs compile / run against the current codebase?
  - **Why**: stale examples are worse than no examples — they teach the wrong thing and erode trust in all docs.
  - Evidence: per touched code example → compile / mental-run check.

- **11.23** Are link cross-references valid (no broken in-repo links, no broken anchor refs)?
  - Evidence: per touched doc → link audit (file exists, anchor present in target).

- **11.24** Do CHANGELOG entries (when `versionize` runs) accurately reflect the change scope, with conventional-commit-derived categorization?
  - **Note**: don't hand-edit CHANGELOG.md; let `dotnet versionize` generate from conventional commits.
  - Evidence: post-versionize CHANGELOG → matches commit log.

- **11.25** When introducing a NEW concept / pattern, is it explained ONCE in the canonical doc (PATTERNS.md / per-lib README) with the explanation linked from elsewhere — not re-explained in every consumer?
  - **Why**: prevents drift; one source of truth for each concept.
  - Evidence: per concept → single-source-of-truth confirmed.

- **11.26** Are README universal claims ("never throws X", "always returns Y", "no Z accepted", "the result is binary") either GREP-VERIFIED at audit time OR qualified at write time with an explicit carve-out reference?
  - **Why**: a README that says "implementations never throw `ArgumentException` for caller mistakes" rots silently the moment a sibling method introduces a registration-time `InvalidOperationException` carve-out (or any other not-quite-fits exception path). Readers will rely on the universal claim; the eventual surprise costs more than qualifying the claim upfront would.
  - **Acceptable forms**: (a) "never throws X **for per-call mistakes** — construction-time / DI registration is a separate lifecycle concern, see [link]"; (b) "always returns Y **except for the documented carve-outs in [link]**"; (c) at audit time, run `grep` to confirm zero counter-examples and note the grep result inline.
  - **Forbidden**: bare unqualified universal claims that don't survive a grep gate.
  - Evidence: per universal claim in a touched README → grep result OR carve-out reference confirmed.

- **11.27** Are README "X% coverage" / "100% lines / 100% branches" prose claims either backed by a coverage tool gate (codecov, coverlet threshold, or equivalent CI fail-on-drop) OR rephrased qualitatively ("adversarial coverage across every public surface")?
  - **Why**: unverified percentage claims drift the instant a new method ships without a test. A "100% lines" claim with no enforcement reads as marketing prose and erodes trust in the rest of the README.
  - **Acceptable forms**: (a) coverage gate wired up + percentage claim + reference to the gate; (b) qualitative "adversarial coverage across every public surface" framing without numbers.
  - Evidence: per coverage claim → gate reference OR qualitative rephrasing.

---

## 12. i18n Discipline

ALL user-visible strings — UI, backend handler messages (`D2Result.messages`), input errors (`D2Result.inputErrors`), notification content (D2.Courier) — go through translation keys. No hardcoded strings, not even for dev/debug pages.

### Predicates

- **12.1** Are all user-visible UI strings using Paraglide translations (`m.key_name()` from `$lib/paraglide/messages.js`)? Includes `<title>`, meta tags, OG tags, headings, labels, placeholders, error messages.
  - Evidence: `grep -rEn '"[A-Z][a-z][a-z]+ [a-z]' <scope .svelte files>` (English-looking literals) → per hit, justify or convert.

- **12.2** Are backend handler messages (`D2Result.messages`) and input errors (`D2Result.inputErrors`) using translation keys from `contracts/messages/` (not hardcoded strings)?
  - Evidence: per `D2Result.*` call → key reference confirmed.

- **12.3** Are D2.Courier notification templates using translation keys (not hardcoded content)?
  - Evidence: per template → key reference confirmed.

- **12.4** When adding translation keys, are they added to ALL present locale files in `contracts/messages/*.json` (kept in sync)?
  - Evidence: `ls contracts/messages/*.json | xargs jq 'keys' | sort | uniq -c` → key sets identical across locales. Run Paraglide compile from `server/web/` for frontend keys.

- **12.5** Are translation keys referenced via `TK.*` constants from `@d2/i18n` / `D2.Shared.I18n` (instead of bare TK key strings)? Outside `D2Result` factories, where bare strings are the API.
  - Evidence: `grep -rEn '"common_errors_\|"webclient_\|"auth_' <scope>` → per hit, justify or convert.

- **12.6** Do new SvelteKit pages include `<svelte:head>` with translated `<title>`, `<meta name="description">`, OG tags (`og:title`, `og:description`, `og:type="website"`), `noindex` if not indexable?
  - Evidence: per new page → svelte:head block.

- **12.7** Does every navigation use `resolve("/path")` from `$app/paths` instead of bare `href="/path"` / `goto("/path")`? (Without this, i18n locale routing breaks for non-default locales.)
  - Evidence: `grep -rEn 'href="/\|goto\("/' <scope>` → per hit, confirm `resolve` wrap or justify.

- **12.8** When using SVG flags for locales, are `/static/flags/4x3/{code}.svg` assets used instead of emoji flags? (Windows doesn't render flag emoji.)
  - Evidence: per locale-flag display → SVG asset.

- **12.9** Are translation keys reused when an existing key matches the meaning?
  - Evidence: per new key → search for prior similar keys done.

---

## 13. Permission / Action Discipline

Inferring permission from prior turns is a class of bug that compounds quickly. Each occurrence of a high-blast-radius action needs explicit fresh permission.

### Predicates

- **13.1** Was any commit created during this scope without explicit user permission for THIS commit (not "go ahead" from earlier)?
  - Evidence: `git log` of commits in scope → cross-reference with user messages → confirm explicit ask + approval per commit.

- **13.2** Was any bulk file operation (sed across N files, mass rename, multi-file delete, bulk format-write) executed without first declaring scope (file count, glob, what changes) and giving the user the chance to redirect?
  - Evidence: per bulk op → journal entry with pre-execution scope statement.

- **13.3** Was any destructive git operation (force push, hard reset, branch delete, checkout that overwrites uncommitted work) used without explicit user authorization? Was `git stash` used by a sub-agent? (Sub-agents must NEVER use stash or other destructive git ops.)
  - Evidence: per destructive op → journal entry with authorization quote.

- **13.4** Was any planned work deferred / skipped without explicit user permission? (Default is to ASK, not unilaterally skip.)
  - Evidence: per planned-but-not-shipped item → journal entry with "asked, user said skip."

- **13.5** Was any architectural decision change made mid-execution without ASKING (when implementation surfaced a reason to deviate from the locked PLAN)?
  - Evidence: per deviation → journal entry with question + user response.

- **13.6** When the user gave feedback during REVIEW, was each item captured first and confirmed before fixing (not fixed-on-sight)?
  - Evidence: per review feedback → capture entry → user confirmation → fix.

- **13.7** Were ALL errors / warnings encountered anywhere in the project fixed (not just in branch-modified files)? (Zero-tolerance rule — never dismiss as "pre-existing.")
  - Evidence: per error/warning seen → fix or escalation note.

### Sub-agent discipline (when delegating work to spawned agents)

- **13.8** Did sub-agents avoid running tests during their work? (Tests run ONLY at the end from the main thread, after all sub-agent changes complete.)
  - **Why**: parallel agents running tests cause file-lock conflicts + spurious failures + wasted compute.
  - Evidence: per sub-agent prompt → "Do NOT run tests" instruction included; agent's tool history shows no test invocations.

- **13.9** Did sub-agents build only their specific project (`dotnet build ProjectName.csproj`), NOT the full solution?
  - **Why**: full-solution builds from parallel agents cause `obj/` file-lock conflicts and cascade failures across other agents' work.
  - Evidence: per sub-agent prompt → per-project build instruction; agent's tool history shows scoped builds only.

- **13.10** Were sub-agents launched with `run_in_background: true` so the main thread stays responsive to user communication?
  - **Why**: blocking the main thread on a long-running agent prevents the user from communicating, providing course corrections, or seeing progress. Real incident: hour-long block during CA1848 fix.
  - Evidence: per sub-agent launch → `run_in_background: true` confirmed.

- **13.11** Did sub-agents avoid ALL destructive git operations (`git stash`, `git checkout --`, `git restore`, `git clean`, `git reset`, etc.)?
  - **Why**: parallel agents using `git stash` nuked other agents' completed work. Real incident cost an hour of work.
  - Evidence: per sub-agent prompt → "Only use Read/Edit/Write tools, no git commands" instruction; agent's tool history confirms.

### Audit / sweep technique discipline

- **13.12** When sweeping docs for cross-cutting patterns (phase refs, V2 mentions, transitional framing, deprecated concepts, doc-cleanup tasks), was each file READ individually (via the Read tool) — NOT primarily discovered by Grep?
  - **Why**: grep matches exact patterns and misses oblique references ("see the phase 5 reference doc" without `.md`), section-style refs (`§5.4`, `per the v2 architecture`), prose mentions, and context that changes whether a match is actually problematic. User's exact words: "don't grep, manually look thru these docs - you keep missing shit."
  - **How**: enumerate files via Glob → Read each one individually (batch in parallel for speed) → use Grep ONLY as a final verification pass after manual reads, never as the source of truth for content audits.
  - Evidence: per content-sweep task → tool history shows Read calls per file, Grep only as verification.

---

## 14. Phase / Audit / Conversation Verbiage Hygiene

KEEP docs (READMEs, CLAUDE.md, AUDIT_CHECKLIST.md, source comments, test names) describe CURRENT reality. Phase tracking lives in `docs/v2/` exclusively.

### Predicates

- **14.1** Is there NO phase / wave / sweep / audit verbiage in source or KEEP docs? Forbidden tokens: `Phase N`, `Wave N`, `Sweep N`, `Audit pass`, `audit decision`, `audit row`, `Step N.N`, `gap closure`, `pre-fix`, `post-fix`, `temporary for`, `previously lacked`.
  - **Allowlisted paths**: `docs/v2/`, `docs/dev/deliverables/`, `MEMORY.md`, `CHANGELOG.md`.
  - Evidence: `grep -rEn 'Phase [0-9]\|Wave [0-9]\|Sweep [0-9]\|audit pass\|audit decision\|audit row\|gap closure\|pre-fix\|post-fix\|previously lacked' <scope minus allowlist>` → expect zero.

- **14.2** Is `TODO` / `FIXME` / `HACK` absent from committed code? (Use a tracked issue instead.)
  - Evidence: `grep -rEn 'TODO\|FIXME\|HACK' <scope>` → expect zero.

- **14.3** Are conversation-scoped IDs (`F2_`, `R2`, `Audit3_`, `PhaseX_`) absent from code, tests, and docs?
  - Evidence: scan + 14.1 overlaps.

- **14.4** Are comments minimal? Default to writing NO comments. Add one only when WHY is non-obvious (hidden constraint, subtle invariant, workaround for a specific bug, surprising behavior).
  - **Forbidden**: explaining WHAT the code does (well-named identifiers do that). Referencing current task, fix, or callers ("used by X", "added for the Y flow", "handles the case from issue #123") — those belong in PR description and rot.
  - Evidence: per new comment → WHY-non-obvious justification.

---

## 15. Object Disposal & Resource Lifetime

Resource leaks are silent in dev, costly in production. Every `IDisposable` / `IAsyncDisposable` MUST have its lifetime documented and enforced.

### Predicates

- **15.1** Are `[MustDisposeResource]` annotations correct? `true` = caller disposes (factory methods returning `IDisposable`). `false` = framework/DI manages lifetime (DI-injected services, `IHostedService` subclasses, test fixtures with `IAsyncLifetime`).
  - Evidence: per factory returning `IDisposable` → annotation present + correct.

- **15.2** Does every type that owns an `IDisposable` field implement `IDisposable` (or `IAsyncDisposable` if any field is `IAsyncDisposable`)?
  - Evidence: per type holding disposable field → dispose pattern.

- **15.3** Does dispose cascade correctly to all owned `IDisposable` fields (no missed dispose)?
  - Evidence: per `Dispose` / `DisposeAsync` → field enumeration.

- **15.4** Is dispose idempotent (calling twice doesn't throw)?
  - Evidence: per `Dispose` → idempotency confirmed (typical pattern: bool `_disposed` flag).

- **15.5** Does dispose synchronously NEVER block on async work? (Use `IAsyncDisposable` / `DisposeAsync` for async cleanup.)
  - Evidence: per `Dispose` body → no `.Result` / `.Wait()` calls.

- **15.6** Are factory methods returning disposables annotated `[MustDisposeResource(true)]`? Are DI-managed services annotated `[MustDisposeResource(false)]` (or unannotated, default behavior)?
  - Evidence: per factory / DI service → annotation confirmed.

- **15.7** Are scoped DI services correctly disposed at end of scope (per ASP.NET Core's automatic scope disposal)?
  - Evidence: per scoped service → no leak by inspection.

- **15.8** Do `using` statements / `using` declarations bracket every locally-acquired disposable?
  - Evidence: per disposable acquisition → `using` confirmed.

- **15.9** Are tests using fixtures that implement `IAsyncLifetime` / `IClassFixture` correctly to share / clean up resources?
  - Evidence: per test fixture → lifecycle.

- **15.10** Does `IHostedService.StopAsync` actually clean up resources (cancel loops, dispose channels, close connections) within the SIGTERM grace window?
  - Evidence: per hosted service → `StopAsync` audit.

---

## 16. OOTB Shared-Lib Tooling — Use What's There

This codebase has a substantial shared-lib stack. Reaching for raw .NET / npm primitives when a shared lib exists is the #2 cost driver after deferred testing. **Always check the shared libs before hand-rolling.**

### What's available (catalog)

| Lib | When to reach for it |
|---|---|
| `D2.Shared.Result` (`@d2/result`) | Every operation that can fail — never throw + catch + return for control flow. Use semantic factories (§5.3). |
| `D2.Shared.Utilities` (`@d2/utilities`) | String / Guid / Enum / collection helpers. `Falsey()`, `Truthy()`, `ToNullIfEmpty()`, `TryParseTruthyNull()`, `CleanStr()`, `TryParseEmail()`, `TryParsePhoneNumber()`. Cache constants. Array / UUID helpers (TS). |
| `D2.Shared.Caching.Abstractions` | Inject `ILocalCache` (per-process), `IDistributedCache` (cluster-wide), or `ITieredCache` (composed L1+L2). Every op returns `D2Result<T>`. Null/empty inputs → `ValidationFailed`. |
| `D2.Shared.Caching.Local.Default` | The default local cache implementation. Atomic ops (CAS-style) supported. |
| `D2.Shared.Caching.Distributed.Redis` | Redis-backed distributed cache. Includes pub/sub `ICacheInvalidationBackplane` for L1 coherency. `*AndBroadcast*` write variants publish on the backplane. |
| `D2.Shared.Resilience` | `ResilientPipeline` for retries / circuit breakers / timeouts. Don't hand-roll retry loops. |
| `D2.Shared.Encryption` | AES-256-GCM payload encryption. Use for sensitive RMQ payloads (per §9.11) and any persistence of secret-equivalent data. |
| `D2.Shared.Messaging` | RabbitMQ pub/sub via `[MqPub]` / `[MqSub]` attributes + spec-driven codegen. Don't hand-roll AMQP channel management. |
| `D2.Shared.Handler` | `BaseHandler<TSelf, TInput, TOutput>` with using aliases (`H`, `I`, `O`), `IHandlerContext`, `DefaultOptions` override. Per-handler PII redaction via `[RedactData]` + `DefaultOptions` overrides. |
| `D2.Shared.Handler.Repo` (+ `.Postgres`) | EF→D2Result classification (PG `23505` → Conflict, FK violation → ValidationFailed, etc.). Use for any repository handler. |
| `D2.Shared.RequestContext` (+ `.Abstractions`) | `MutableRequestContext` filled by middleware; injected as `IRequestContext` everywhere. Carries traceId, userId, orgId, scopes, fingerprints. |
| `D2.Shared.Auth.Abstractions` | `ActorEntry`, `ImpersonationKind`, `ActionSensitivity`, `OrgType`, `Role`, `JwtClaimTypes`, `RequestHeaders`, codegen-emitted `Scopes` + `Audiences`. |
| `D2.Shared.Auth.Outbound` | `IServiceIdentityClient`, `ITokenExchangeClient`, `ServiceIdentityCallCredentials` for gRPC. Don't hand-roll OAuth flows. |
| `D2.Shared.Service.Defaults` | One-call OTel SDK bootstrap (`setupTelemetry`). Standard service config. |
| `D2.Shared.I18n` (+ `.Abstractions`) | Translation key constants (`TK.*`) — see §12.5. |
| `D2.Shared.Logging` (Node) | `ILogger` + Pino impl, auto-instrumented via OTel. |
| `@d2/di` (Node) | Lightweight DI container mirroring .NET `IServiceCollection` / `IServiceProvider`. |
| `@d2/handler` (Node) | `BaseHandler` parity with .NET — auto-injects traceId, OTel spans + 4 metrics. |
| `D2.Shared.Tests` (`@d2/testing`) | Custom xUnit / Vitest matchers, fixtures. |

### Predicates

- **16.1** When a need arises that one of the shared libs covers, is the shared lib used (not hand-rolled)?
  - Evidence: per non-trivial helper / pattern → matched against the catalog above; if duplicating capability → justify or refactor to use shared lib.

- **16.2** When a needed extension doesn't exist yet in `D2.Shared.Utilities.Extensions`, is the new extension proposed (don't hand-roll inline)? Check the v1 (`/old/v1/`) and DeCAF (`/old/DeCAF-DCSV/`) snapshots first — they often had the helper and the pattern was carried forward intentionally.
  - Evidence: per inline helper hand-roll → "checked utilities, not present, proposing addition" or "found in /old/, ported forward."

- **16.3** Are caching tier choices appropriate?
  - `ILocalCache` — per-process, ephemeral. OK for read-cache that doesn't need cluster coherency.
  - `IDistributedCache` — cluster-wide. Use for cross-instance state (sessions, rate-limit counters, idempotency keys).
  - `ITieredCache` — composed L1+L2. Reads check L1 → fall through to L2 → populate L1. Writes go L2-first. Use when read-heavy + cluster-coherent.
  - `*AndBroadcast*` write variants — publish on backplane to invalidate other instances' L1.
  - Evidence: per cache injection → tier justified.

- **16.4** When a `netstandard2.0` Roslyn source generator can't reference `D2.Shared.Utilities` (TFM mismatch — Roslyn analyzers can't load `net10`-targeted assemblies), is the missing helper provided via a local `Polyfills/StringExt.cs` (or equivalent) that mirrors the real semantics exactly?
  - **Required pattern**: a polyfill class under `Polyfills/`, namespace-scoped to the source-gen project, with the SAME signature and semantics as the canonical helper (e.g. `Falsey()` covers null + empty + whitespace, no `string.IsNullOrEmpty` shortcut). Implement via primitive operations (a `for` loop over chars) — never via the BCL helpers the convention forbids.
  - **Forbidden**: using `string.IsNullOrEmpty` / `string.IsNullOrWhiteSpace` inside the source-gen because "we can't reference utilities anyway." Polyfill it; the convention is universal regardless of TFM.
  - Evidence: per source-gen project that needs Falsey-class behavior → `Polyfills/StringExt.cs` (or named equivalent) confirmed; per use site → polyfill called, not the BCL forbidden helper.

- **16.4** Are content-addressable entities using `Location` / `WhoIs` / similar SHA-256 hash ID pattern (cross-ref §9.27)?
  - Evidence: per content-addressable entity → factory + hash form.

- **16.5** Is `ResilientPipeline` (or equivalent) used for any retryable network call, NOT a hand-rolled `for (int i = 0; i < 3; i++) try {...}`?
  - Evidence: per retry site → `ResilientPipeline` confirmed.

---

## 17. D2Result Usage & Extensions

`D2Result` replaces exceptions for control flow. Every operation that can fail returns a `D2Result<T>`. Master the extension methods so call sites stay clean.

### Predicates

- **17.1** Is `D2Result.BubbleFail` / `BubbleOnFailure` used to early-return from a handler when a nested operation fails? (Not manual `if (!result.Success) return D2Result<TOut>.Fail(...)`.)
  - Evidence: per nested handler call → bubble pattern confirmed.

- **17.2** Is `D2Result.Combine` used to aggregate multiple parallel `D2Result<T>` values into a single tuple / list result with combined errors?
  - **5 fixed-arity overloads (2-5)** + `IEnumerable<T>` overload.
  - **Eager evaluation**. All-success → tuple/list of unwrapped values + first non-null traceId. Any-failure → aggregated `ValidationFailed` with concatenated messages + inputErrors. Empty `IEnumerable` → `Ok` empty.
  - Evidence: per multi-result aggregation → `Combine` use.

- **17.3** Are partial-success paths handled correctly? `NOT_FOUND` (none found) → `SOME_FOUND` (partial, data returned) → `OK` (all found).
  - Evidence: per multi-fetch handler → tri-state outcome.

- **17.4** Does every `D2Result` carry `traceId` (auto-populated by `BaseHandler`)?
  - Evidence: per result → traceId presence.

- **17.5** Are typed factories preferred? `D2Result<string>.ServiceUnavailable()` instead of `BubbleFail(D2Result.ServiceUnavailable())`.
  - Evidence: per factory call → typed form when available.

- **17.6** Do extension methods on `D2Result` follow the `extension(D2Result<T> r) { ... }` C# 14 syntax (per §5.6)?
  - Evidence: per new D2Result extension → syntax confirmed.

- **17.7** When mapping arbitrary upstream status codes (e.g., from a third-party HTTP API), is raw `D2Result.Fail(statusCode, ...)` justified in journal? Otherwise convert to a semantic factory.
  - Evidence: per raw `Fail` use → justification.

---

## 18. Graceful Degradation & Failure Modes

Production code MUST degrade gracefully. Identify every dependency, document its failure mode, and make sure the handler doesn't crash / hang / silently break when the dependency fails.

### Predicates

- **18.1** For every external dependency (DB, broker, cache, third-party API, OIDC discovery, JWKS endpoint), what happens when it's unavailable?
  - Evidence: per dependency → degradation strategy documented (fail-closed / fail-open / use stale cache / circuit-break / retry with backoff).

- **18.2** Are retryable errors classified differently from non-retryable errors?
  - **Retryable**: 5xx, network timeout, rate-limited (429), broker temporarily unreachable.
  - **Non-retryable**: 4xx client errors, validation failures, auth failures, 404s.
  - Evidence: per error path → retry decision documented.

- **18.3** Are timeouts set on EVERY network call (HTTP, gRPC, DB query, broker publish)?
  - **Why**: default infinite timeouts cause hung handlers, exhausted thread pools, eventual cascade.
  - Evidence: per network call → timeout value confirmed.

- **18.4** Are circuit breakers in place for downstream services that can become unhealthy? (Use `D2.Shared.Resilience.CircuitBreaker`.)
  - Evidence: per cross-service call → circuit breaker state.

- **18.5** Are fallback values correct when degradation kicks in? (Don't return wrong data; return `D2Result.ServiceUnavailable()` or stale-but-flagged data.)
  - Evidence: per fallback path → correctness audit.

- **18.6** Does the handler distinguish between "transient failure, retry" and "permanent failure, give up"?
  - Evidence: per failure-handling code → classification.

- **18.7** Are partial failures handled (e.g., batch operation where 8/10 succeed, 2 fail)?
  - Evidence: per batch op → partial-failure shape.

- **18.8** Does the system fail-closed on critical security checks (auth, authz)? Fail-open is unacceptable for security-critical paths.
  - Evidence: per auth/authz check → fail-closed branch confirmed.

- **18.9** When a downstream service returns malformed data (truncated JSON, unexpected schema, encoding errors), is the failure caught and converted to `D2Result` rather than propagated as a raw exception?
  - Evidence: per parse / deserialize site → catch + convert.

- **18.10** Are CancellationTokens propagated end-to-end? (Long-running operations must respect ct so they're cancelable when the request is canceled.)
  - Evidence: per long-running op → ct passed through.

- **18.11** Does shutdown drain in-flight requests within the SIGTERM grace window? (No abrupt termination of a request mid-flight; either complete or rollback.)
  - Evidence: per shutdown handler → drain mechanism.

- **18.12** Are bulkheads / concurrency limits in place to prevent one failing dependency from saturating thread pool?
  - Evidence: per heavy-async service → concurrency cap.

---

## 19. User Experience (UX)

Production-ready UX means: data shows up when expected, loading states tell the user something's happening, errors are actionable, empty states make sense, the UI doesn't crash on weird inputs.

### Predicates

- **19.1** Does every component displaying async / server-loaded data show a `<Skeleton>` placeholder until the data is ready?
  - Evidence: per async-data component → Skeleton confirmed.

- **19.2** Does every list / grid / table have a defined "empty state" message when there's nothing to show? (Not blank.)
  - Evidence: per list view → empty-state component.

- **19.3** Does every form field show validation errors inline (not swallowed silently and not blocking submit without explanation)?
  - Evidence: per form field → error-display surface.

- **19.4** Do form errors clear when the user fixes them (blur → re-validate → if valid, error clears)?
  - Evidence: per form → blur/clear cycle tested.

- **19.5** Does every page have a translated `<title>` so the browser tab is meaningful?
  - Evidence: per page → svelte:head title.

- **19.6** Are loading states distinguishable from empty states (don't show "no data" while still loading)?
  - Evidence: per data-display component → loading-vs-empty branching.

- **19.7** Are error states actionable? ("Something went wrong" with retry button beats a blank screen.)
  - Evidence: per error boundary → recovery action.

- **19.8** Do destructive actions (delete, irreversible state change) require explicit confirmation?
  - Evidence: per destructive action → confirm dialog.

- **19.9** Are keyboard shortcuts and accessibility attributes (aria-*, role) present on interactive elements?
  - Evidence: per interactive component → a11y audit.

- **19.10** Does the UI work on small viewports (mobile-first responsive, or explicit non-responsive justification)?
  - Evidence: per page → viewport scan.

- **19.11** Does the UI handle slow networks (loading spinners during longer-than-skeleton operations, no double-submit, optimistic updates where appropriate)?
  - Evidence: per slow-prone operation → UX accommodation.

- **19.12** Are toast notifications used for transient feedback (success / failure / info)?
  - Evidence: per state-changing user action → toast confirmation.

- **19.13** Do pages use semantic HTML (`<nav>`, `<main>`, `<article>`, `<section>`, `<header>`, `<footer>`) so screen readers / SEO work?
  - Evidence: per page → semantic structure.

---

## 20. Developer Experience (DX)

Code that future engineers (including future-you) can read, debug, extend, and refactor without reverse-engineering. Sensible defaults. Ergonomic call sites. No footguns.

### Predicates

- **20.1** Are sensible defaults provided for every Options record, so call sites can be `new()` for "I'll take the defaults"?
  - **Pattern**: §5.13 (nullable-param ctor + `?? default`).
  - Evidence: per Options record → defaults hold up to inspection.

- **20.2** Are footguns absent from public API surface? (No methods that look right but silently do the wrong thing — e.g., accepting `string?` and treating null as a special sentinel without documenting.)
  - Evidence: per public API → footgun audit.

- **20.3** Are call sites concise? `client.GetCurrentTokenAsync(ct)` beats `client.GetCurrentTokenAsync(new GetCurrentTokenOptions { ... }, ct)` when defaults work.
  - Evidence: per public method → call-site readability.

- **20.4** Do error messages include enough context to debug WITHOUT including PII? (e.g., "ServiceIdentity fetch failed for issuer=<issuer>; status=<httpStatus>; outcome=<outcome>" — issuer is config, not PII).
  - Evidence: per error message → context sufficiency.

- **20.5** Are exceptions thrown only for truly exceptional conditions (system invariant violations, programmer errors), not for control flow? (`D2Result` is the control-flow type.)
  - Evidence: per `throw` → exceptional-condition justification.

- **20.6** Do public APIs follow the principle of least surprise? (Given the type signature, what does a developer expect? Does the implementation deliver that?)
  - Evidence: per public API → "would I be surprised?" check.

- **20.7** Are dependency injections explicit (constructor params), not service-locator style (`provider.GetService<T>()` mid-method)?
  - Evidence: per service resolution → injection point.

- **20.8** Are interfaces minimal (Interface Segregation Principle — small, focused interfaces beat fat ones)?
  - Evidence: per new interface → method count + coherence.

- **20.9** Do tests serve as documentation (clear names, focused assertions, "given-when-then" structure where helpful)?
  - Evidence: per test → readability check.

- **20.10** Are XML doc summaries on public APIs accurate and helpful for IntelliSense / hover? (Not just `<summary>does the thing</summary>`.)
  - Evidence: per public symbol → summary quality.

- **20.11** Is debug logging available at appropriate verbosity? (Production logs at INFO; DEBUG / TRACE available for troubleshooting.)
  - Evidence: per non-trivial flow → logging coverage.

- **20.12** Are sensible config defaults documented in the README so an onboarding engineer can run the service without reading the source?
  - Evidence: per service README → defaults documented.

- **20.13** Are common debugging scenarios covered in the README (how to inspect DLQ, how to read Tempo / Loki traces, how to query the audit log)?
  - Evidence: per service README → ops docs.

- **20.14** Are public extension methods discoverable (named clearly, namespaced consistently, not hidden in obscure namespaces)?
  - Evidence: per new extension → namespace + name.

---

## 21. Observability Completeness

Production code that you can't observe is production code you can't debug, can't optimize, and can't trust. Every operation must emit traces + metrics + logs at the right granularity.

### Predicates

- **21.1** Does every handler emit an OTel span via `BaseHandler` (auto)?
  - Evidence: per handler → span emission confirmed.

- **21.2** Do all logs and spans include the universal correlation fields (per §7.9 table)?
  - `traceId`, `correlationId`, `userId`, `orgId`, `service`.
  - Evidence: per log/span site → fields present.

- **21.3** Does every counter / gauge / histogram have a documented unit + meaningful tag set?
  - Evidence: per `Meter.Create*` → unit + tags documented.

- **21.4** Are span attributes structured (key-value pairs), not concatenated strings?
  - Evidence: per `Activity.SetTag` → key-value form.

- **21.5** Is every long-running operation (> 1s typical) instrumented with a histogram for duration?
  - Evidence: per long-running op → histogram present.

- **21.6** Are error counters tagged with outcome categories (e.g., `outcome=fetch_success`, `outcome=fetch_fail_network`, `outcome=fetch_fail_validation`)?
  - Evidence: per error counter → outcome taxonomy.

- **21.7** Are debug logs sufficient to reconstruct a failed request's flow without reproducing it?
  - Evidence: per non-trivial flow → log-trace audit.

- **21.8** Are health checks comprehensive (DB connectivity, broker connectivity, downstream service availability, key custodian readiness)?
  - Evidence: per service → health check enumeration.

- **21.9** Are slow-operation thresholds set per handler (via `HandlerOptions`)?
  - Evidence: per latency-sensitive handler → threshold value.

---

## 22. Idempotency & Exactly-Once Semantics

Distributed systems retry. Operations must tolerate retries without doubling effects.

### Predicates

- **22.1** Does every state-mutating HTTP endpoint accept an `Idempotency-Key` header (or equivalent) and dedupe on it?
  - Evidence: per state-mutating endpoint → idempotency middleware.

- **22.2** Does every RabbitMQ consumer use the idempotency-store pattern (check `MarkSeen` before processing; re-route to DLQ if seen)?
  - Evidence: per consumer → MarkSeen check.

- **22.3** Are external API calls (especially payment, notification dispatch) protected by idempotency keys forwarded to the upstream when supported?
  - Evidence: per external call → key forwarding.

- **22.4** Is exactly-once semantics achieved via the SAGA pattern for cross-service updates? (E.g., Geo-first → Auth-second → compensate Geo on auth failure → fatal log if rollback fails.)
  - Evidence: per cross-service update → SAGA shape.

- **22.5** Are idempotency-key TTLs sensible (long enough to cover client retry windows; short enough not to bloat storage)?
  - Evidence: per idempotency store → TTL value + justification.

- **22.6** Do INCR-class atomic ops (`IncrementAsync` and equivalents — anything that does read-modify-write on a numeric counter via an atomic primitive) PRESERVE existing TTL on subsequent calls? The TTL applied at first creation must NOT be re-applied on every call (that turns rate-limit / throttling counters into "ever" instead of "5 minutes" under sustained load).
  - **Pattern (Redis)**: gate `PEXPIRE` on `redis.call('PTTL', KEYS[1]) < 0` so it fires only when the key has no existing TTL.
  - **Pattern (in-process)**: read existing absolute expiration from the parallel TTL-tracking dictionary and re-apply it verbatim instead of falling back to the default-TTL helper.
  - **Required regression test**: SET with a short TTL (e.g. 2 minutes) → INCR → assert `GetTtl` reports remaining ≤ original window (NOT default expiration). Pin per impl.
  - **Why**: the bug is silent — counter values keep working; only the TTL window stretches. Rate-limit / throttle counters under sustained load never expire, so the rate becomes effectively "ever."
  - Evidence: per atomic-op impl → Lua / locked-block code path inspected for TTL-preservation gate + regression test linked.

---

## 23. Configuration Hygiene

Secrets, env vars, defaults, and the `.env.local` / `.env.secrets` split.

### Predicates

- **23.1** Are env vars indexed correctly when representing a list? `PREFIX__0`, `PREFIX__1` (matching .NET `IConfiguration` array binding) AND parsed via `parseEnvArray()` in Node.
  - **Forbidden**: comma-separated lists in env vars.
  - Evidence: per array-shaped config → indexed convention.

- **23.2** Do services read env vars directly via `D2Env.Load()`? (NOT via AppHost injection — AppHost is only for container infra.)
  - Evidence: per service init → env-loading site.

- **23.3** Are secrets never committed to git? (`.env.secrets` is gitignored; `.env.secrets.example` is the template.)
  - Evidence: `git log` of `.env.secrets` → expect no commits.

- **23.4** Are new secrets added via the standard workflow?
  1. Edit `.env.secrets.example` adding `NEW_THING_API_KEY=replace_with_real_value`
  2. Update `infra/compose/compose.yml` to load it into the right service
  3. Tell the operator: "Added `NEW_THING_API_KEY` — copy into `.env.secrets`, set the real value, restart the service"
  4. Operator manually syncs (Claude cannot edit `.env.secrets` — deny rule)
  - Evidence: per new secret → workflow followed.

- **23.5** Are encryption keys generated via `tools/scripts/gen-dev-keys.sh` (not hand-typed)?
  - Evidence: per new key domain → generator script updated.

- **23.6** Are config defaults sane for production (not "works in dev, breaks in prod" surprises)?
  - Evidence: per Options default → production-applicability check.

- **23.7** Are config validations done at startup (fail fast) rather than on first use (fail late)?
  - Evidence: per config-using service → startup validation.

---

## Self-improvement loop

This catalog grows. Per [workflow.md](workflow.md) §SHIP, every deliverable's distillation produces proposed predicate additions. Approved additions land here. Over time the catalog approaches "every kind of miss we've ever made has a corresponding gate-check," and the audit loop converges in fewer rounds because predicates fire pre-emptively (the agent sees the predicate during PLAN's pre-emptive gate checks and avoids the miss in the first place).

### Format for proposing a new predicate

In the deliverable's root README "Proposed rule additions to rules.md" section:

```
Category: <existing category number + name, or "NEW: <name>">
Predicate: <Y/N question with required evidence>
Origin:    <which deliverable / step / round surfaced the underlying miss>
Why permanent: <not a one-off; class of miss that will recur without a gate-check>
Examples:  <1-2 specific past instances>
```

User approves / tweaks / rejects per proposal. Approved proposals get appended to this doc as part of ship's commit batch.

Rejected proposals (one-off mistakes not worth a permanent rule) get noted in the deliverable's final report so the reasoning survives.

---

## Final reminder

**This catalog exists because D²-WORX is being built to ship to production with real users, real money, and real consequences for failure.** The verbose discipline upfront is the cost of robustness; the alternative is shipping bugs and burning trust. When in doubt about a predicate, default to applying it — the cost is minutes of reading, the cost of skipping is incidents.
