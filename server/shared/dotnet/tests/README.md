<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Shared.Tests

> Parent: [`server/shared/dotnet/`](../README.md)

Test infrastructure for ALL `D2.Shared.*` libraries. Deliberately one test csproj rather than per-lib — the foundational shared libs are small enough that per-lib test projects would be overkill.

Per-service tests (Edge, Audit, Courier, Notifications, Files) live separately at `server/services/{service}/tests/D2.{Service}.Tests.csproj` — this project covers shared libs only.

---

## Layout

```
server/shared/dotnet/tests/
├─ D2.Shared.Tests.csproj
├─ Unit/                                                # in-process unit + behavior tests
│  ├─ Auth/                                             # → auth/abstractions enums + records + JwtClaimTypes
│  │  └─ SourceGen/                                     # → auth/scopes-source-gen + auth/audiences-source-gen
│  ├─ AuthContext/                                      # → auth/context-abstractions
│  ├─ AuthOutbound/                                     # → auth/outbound (ServiceIdentity / TokenExchange / Grpc / Telemetry)
│  │  └─ Fixtures/                                      # stubs + jwt builder + counter listeners
│  ├─ Caching/                                          # → caching/abstractions + caching/local-default
│  │  ├─ Abstractions/                                  # InputFailures + LocalCacheOptions
│  │  ├─ Distributed/                                   # JsonCacheSerializer
│  │  └─ Local/                                         # DefaultLocalCache (unit + behavior with real IMemoryCache)
│  ├─ Context/                                          # → context/abstractions (request-context interfaces + propagation)
│  │  └─ SourceGen/                                     # → context/source-gen
│  ├─ Encryption/                                       # → encryption (PayloadCrypto + keyring + frame)
│  ├─ Handler/                                          # → handler (BaseHandler + telemetry + DI)
│  ├─ HandlerRepo/                                      # → handler/repo (BaseRepoHandler)
│  │  ├─ Abstractions/                                  # → handler/repo-abstractions (D2ResultDb* + DbErrorCodes + IDbExceptionClassifier)
│  │  └─ Postgres/                                      # → handler/repo-postgres (PgErrorCodes + PostgresDbExceptionClassifier)
│  ├─ I18n/                                             # → i18n/core + i18n/abstractions
│  │  └─ SourceGen/                                     # → i18n/source-gen
│  ├─ Messaging/                                        # → messaging/abstractions + messaging/rabbitmq
│  │  ├─ Channels/, Connection/, Encryption/, Idempotency/, Publishing/, Subscribing/, Telemetry/, Topology/
│  │  └─ SourceGen/                                     # → messaging/source-gen
│  ├─ RequestContext/, RequestContextAbstractions/      # → context/abstractions
│  ├─ Resilience/                                       # → resilience (CircuitBreaker / Retry / Singleflight / Pipeline)
│  ├─ Result/                                           # → result (D2Result + factories + monadic + guard + Combine + Unit + ErrorCodes)
│  └─ Utilities/                                        # → utilities (Falsey/Truthy + TryParseTruthyNull + RedactDataAttribute + D2Env + ConnectionStringHelper)
└─ Integration/                                         # Testcontainers-backed real-infrastructure tests
   ├─ Caching/
   │  ├─ Distributed/                                   # RedisDistributedCache + RedisCacheInvalidationBackplane (Redis container)
   │  └─ Tiered/                                        # DefaultTieredCache (Redis container shared with Distributed)
   ├─ ContractFixtures/                                 # [Trait("Category","ContractFixtures")] — emits cross-language parity fixture JSON to server/shared/typescript/contract-tests/fixtures/ (consumed by @d2/contract-tests Vitest)
   └─ Messaging/                                        # RabbitMQ container — publish/consume + idempotency + DLQ + topology + adversarial
```

The tree mostly mirrors the source layout (`Unit/{LibName}/{LibSourceFile}Tests.cs` per lib). Two structural notes:

- `Unit/RequestContext/` and `Unit/RequestContextAbstractions/` cover code that lives under `context/abstractions/`. The two test folders preserve the historical split between request-context entities and request-context abstractions for reviewer navigation; both subtrees compile against the same csproj.
- `Unit/AuthOutbound/Fixtures/` is the only sub-folder under `Unit/` that explicitly groups fixtures into a `Fixtures/` directory; everywhere else, fixture types live next to the tests that use them. Consistent with the prevailing codebase pattern.

Integration tests use xUnit collection fixtures (`[Collection("Redis")]`, `[Collection("RabbitMq")]`) so the heavyweight container fixtures spin up exactly once per test run.

---

## Stack

| Tool                                               | Version     | Purpose                                                                                                                                                                |
| -------------------------------------------------- | ----------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `xunit.v3`                                         | per CPM     | Test framework. `[Fact]`, `[Theory]`, `Assert`.                                                                                                                        |
| `xunit.runner.visualstudio`                        | per CPM     | Test discovery for Rider / VS / `dotnet test`.                                                                                                                         |
| `Microsoft.NET.Test.Sdk`                           | per CPM     | MSBuild test integration.                                                                                                                                              |
| `AwesomeAssertions`                                | per CPM     | Fluent assertion API (`result.Should().BeOk()`). MIT-licensed fork of FluentAssertions; v8+ of FA went commercial, AwesomeAssertions preserves the Apache 2.0 lineage. |
| `JetBrains.Annotations`                            | per CPM     | `[MustDisposeResource]`, `[Pure]`, etc. on test fixtures.                                                                                                              |
| `Testcontainers.Redis` / `Testcontainers.RabbitMq` | per CPM     | Integration test containers (real Redis + real RabbitMQ via Docker). Skipped when Docker isn't reachable.                                                              |
| `Microsoft.Data.Sqlite`                            | per CPM     | EF / DbException test fakes (e.g. PgExceptionFactory shaping for handler/repo-postgres tests).                                                                         |
| `FakeItEasy`                                       | per CPM     | Lightweight fake / stub framework where hand-rolled stubs would be too verbose.                                                                                        |
| MTP (Microsoft Testing Platform)                   | SDK-bundled | Modern test runner — `<UseMicrosoftTestingPlatformRunner>true</UseMicrosoftTestingPlatformRunner>` in csproj. Replaces VSTest.                                         |

Test packages are pinned in `server/Directory.Packages.props` (Central Package Management); this csproj references by ID only.

---

## Conventions

### Test naming

`MethodName_Scenario_ExpectedResult`. The method name IS the documentation:

```csharp
[Fact]
public void Ok_WithTraceId_PreservesTraceId()
[Fact]
public void Bind_OnFailure_DoesNotInvokeNext()
[Fact]
public void IsTransientRetryable_ExplicitlyFalseOnUnhandledException()
```

CS1591 / SA1600 (missing XML doc) are suppressed in this csproj only; non-test libs still enforce XML doc on every public member. The reasoning lives in the csproj `<NoWarn>` comment.

### Local constants

`snake_case` for local consts in test bodies:

```csharp
const string trace_id = "trace-abc-123";
```

Non-const test-locals (var, out var, etc.) MUST be `camelCase` per `docs/dev/rules.md §7.1`.

### Adversarial coverage

Per `docs/TESTS.md`, every test file aims at the 8-category checklist where applicable:

1. **Happy path** — every factory creates the expected shape
2. **Garbage input** — null/empty/whitespace, oversized, malformed
3. **Boundary values** — empty list vs single-item vs multi-item; default(T) vs explicit; off-by-one
4. **Format validation** — regex / length / type checks
5. **Cross-field deps** — interaction between two arguments / two state slots
6. **Error propagation** — `BubbleFail` chain preservation, `Bind` short-circuiting, exception → D2Result mapping
7. **Idempotency** — repeat-call semantics on mutating ops
8. **Concurrency** — thread-stress on mutable shared state (`Parallel.ForEachAsync`, `Task.WhenAll`)

For pure value types like `D2Result`, categories 4/7/8 are degenerate. For libs with I/O (caching, messaging, repository) those categories are live and pinned.

### Lazy-evaluation tests

Monadic / async-chain tests assert that continuations are NOT invoked on upstream failure. Pattern:

```csharp
var nextInvoked = false;
var result = upstream.Bind(x =>
{
    nextInvoked = true;
    return D2Result<int>.Ok(x);
});
nextInvoked.Should().BeFalse();
```

Standard for any chaining test — both sync and async.

### Monadic laws

For each monad-shaped type (currently just `D2Result<T>`'s `Bind`), verify:

- **Left identity**: `M.Ok(x).Bind(f) ≡ f(x)`
- **Right identity**: `m.Bind(M.Ok) ≡ m`
- **Associativity**: `m.Bind(f).Bind(g) ≡ m.Bind(x => f(x).Bind(g))`

Both success and failure paths exercised.

### `[LoggerMessage]` PII contract

Every `[LoggerMessage]` partial method whose source-side call sites observe potentially PII-bearing exceptions (broker URIs, connection strings, OAuth tokens, raw user input) is reflection-pinned by `Unit/Messaging/Telemetry/LoggerMessageDelegateContractTests.cs` to forbid `Exception` parameters. The pattern is the standard guard for `docs/dev/rules.md §3.1` across the messaging, caching/distributed-redis, caching/tiered, handler, and auth/outbound libs. Sibling libs follow the same rule even where a contract test isn't (yet) in place.

---

## Running

```bash
dotnet test server/shared/dotnet/tests                # all tests in this project
dotnet test server/D2.slnx                             # full solution (all test projects)
```

Test discovery is via xunit.v3 + MTP. Rider / VS Test Explorer pick up tests automatically. Integration tests skip cleanly when Docker isn't reachable.

### Coverage

`coverlet.msbuild` is wired into the csproj. Run from the repo root:

```
dotnet test server/shared/dotnet/tests -property:CollectCoverage=true -property:CoverletOutputFormat=cobertura -property:CoverletOutput=./coverage/
```

(Use the `-property:` form, not `/p:` — bash strips the leading `/`. One line, works in CMD / PowerShell / bash.)

**Where the result is:**

The full Cobertura XML lands at `server/shared/dotnet/tests/coverage/coverage.cobertura.xml`. The top of the file has the summary attributes — read these for the at-a-glance result:

```xml
<coverage line-rate="0.99" branch-rate="0.95" version="..." timestamp="..."
          lines-covered="..." lines-valid="..."
          branches-covered="..." branches-valid="...">
```

`line-rate="1"` = 100%. Lines / branches covered vs valid are the absolute counts.

A summary table also prints to stdout when the test run goes through the MSBuild VSTest target (e.g. when source files changed and a build is required). When the build is up-to-date and MTP runs the test exe directly, the table is suppressed but the XML is still written.

**For line-by-line view:** open `coverage.cobertura.xml` in Rider via `Tools → Unit Tests → Show Coverage Tree → Add Coverage Snapshot`. Or just use Rider's "Cover Unit Tests" gutter button which produces the same data interactively.

**Use this BEFORE committing** a new lib's tests to catch:

- Lib lines / branches / methods not exercised by any test
- Branches missed because tests cover only one side of a `??` / ternary / nullable check (the most common gap)

> The `(int)` 0% entries you'll see in Rider coverage on `_OnFailure_ShortCircuits` / `_DoesNotInvokeProjection` tests are intentional — they're the lambda body that the test asserts is never invoked. Coverage tooling can't distinguish "untested" from "asserted-never-called"; the 0% there IS the test result. Lib coverage (the only coverage that matters for the Definition of Done) is what the `D2.Shared.{LibName}` row in the cobertura XML reports.

---

## When to expand this project

A new shared lib lands in `server/shared/dotnet/{lib}/` → create `Unit/{Lib}/` here with one test file per source file (`{SourceFile}Tests.cs`). If the lib needs real infrastructure (a real DB, a real broker, a real cache), add `Integration/{Lib}/` with a Testcontainers fixture instead. Project reference to the lib goes in `D2.Shared.Tests.csproj`.
