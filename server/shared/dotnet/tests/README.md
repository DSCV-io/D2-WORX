<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Shared.Tests

Test infrastructure for ALL `D2.Shared.*` libraries. Deliberately one test csproj rather than per-lib — the foundational shared libs are small enough that per-lib test projects would be overkill.

Per-service tests (Edge, Audit, Courier, Notifications, Files) live separately at `server/services/{service}/tests/D2.{Service}.Tests.csproj` — this project covers shared libs only.

---

## Layout

```
server/shared/dotnet/tests/
├─ D2.Shared.Tests.csproj
└─ Unit/
   └─ Result/                                # tests for D2.Shared.Result
      ├─ D2ResultTests.cs                    # non-generic factories + ctor
      ├─ D2ResultBooleansTests.cs            # per-code booleans + combined helpers
      ├─ D2ResultGenericTests.cs             # generic factories + Check* + BubbleFail / Bubble
      ├─ D2ResultMonadicTests.cs             # Bind / Map / Match + monadic laws
      ├─ D2ResultAsyncExtensionsTests.cs     # BindAsync / MapAsync / ThenAsync (Task + ValueTask)
      └─ D2ResultGuardTests.cs               # BubbleOnFailure
```

The tree mirrors the source layout: `Unit/{LibName}/{LibSourceFile}Tests.cs` per lib being tested. New libs add their own subdirectory under `Unit/`.

---

## Stack

| Tool | Version | Purpose |
|---|---|---|
| `xunit.v3` | per CPM | Test framework. `[Fact]`, `[Theory]`, `Assert`. |
| `xunit.runner.visualstudio` | per CPM | Test discovery for Rider / VS / `dotnet test`. |
| `Microsoft.NET.Test.Sdk` | per CPM | MSBuild test integration. |
| `AwesomeAssertions` | per CPM | Fluent assertion API (`result.Should().BeOk()`). MIT-licensed fork of FluentAssertions; v8+ of FA went commercial, AwesomeAssertions preserves the Apache 2.0 lineage. |
| `JetBrains.Annotations` | per CPM | `[MustDisposeResource]`, `[Pure]`, etc. on test fixtures. |
| MTP (Microsoft Testing Platform) | SDK-bundled | Modern test runner — `<UseMicrosoftTestingPlatformRunner>true</UseMicrosoftTestingPlatformRunner>` in csproj. Replaces VSTest. |

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

`snake_case` for local consts in test bodies (per CLAUDE.md §6 naming table):

```csharp
const string trace_id = "trace-abc-123";
```

### Adversarial coverage

Per CLAUDE.md §5 + `docs/TESTS.md`, every test file aims at the 8-category checklist where applicable:

1. **Happy path** — every factory creates the expected shape
2. **Garbage input** — null/empty messages, null traceId, default ErrorCode
3. **Boundary values** — empty list vs single-item vs multi-item; default(T) for value types vs null for ref types
4. **Format validation** — N/A for value types
5. **Cross-field deps** — Success vs Data nullability, errorCode-override breaking the per-code boolean
6. **Error propagation** — `BubbleFail` chain preservation, `Bind` short-circuiting, async chain mid-failure
7. **Idempotency** — N/A for immutable value types
8. **Concurrency** — N/A for immutable value types (verified by inspection — all properties are init-only / readonly)

For pure value types like `D2Result`, categories 4/7/8 are degenerate. For libs with I/O (caching, messaging, repository) that join this test project later, those categories become live.

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

---

## Running

```bash
dotnet test server/shared/dotnet/tests                # all tests in this project
dotnet test server/shared/dotnet/tests --filter Result # only Result tests
dotnet test server/D2.slnx                             # full solution (all test projects)
```

Test discovery is via xunit.v3 + MTP. Rider / VS Test Explorer pick up tests automatically.

### Coverage

`coverlet.msbuild` is wired into the csproj. Run from the repo root:

```
dotnet test server/shared/dotnet/tests -property:CollectCoverage=true -property:CoverletOutputFormat=cobertura -property:CoverletOutput=./coverage/
```

(Use the `-property:` form, not `/p:` — bash strips the leading `/`. One line, works in CMD / PowerShell / bash.)

**Where the result is:**

The full Cobertura XML lands at `server/shared/dotnet/tests/coverage/coverage.cobertura.xml`. The top of the file has the summary attributes — read these for the at-a-glance result:

```xml
<coverage line-rate="1" branch-rate="1" version="..." timestamp="..."
          lines-covered="324" lines-valid="324"
          branches-covered="84" branches-valid="84">
```

`line-rate="1"` = 100%. `branch-rate="0.988"` = 98.8%. Lines / branches covered vs valid are the absolute counts.

A summary table also prints to stdout when the test run goes through the MSBuild VSTest target (e.g. when source files changed and a build is required). When the build is up-to-date and MTP runs the test exe directly, the table is suppressed but the XML is still written.

**For line-by-line view:** open `coverage.cobertura.xml` in Rider via `Tools → Unit Tests → Show Coverage Tree → Add Coverage Snapshot`. Or just use Rider's "Cover Unit Tests" gutter button which produces the same data interactively.

**Use this BEFORE committing** a new lib's tests to catch:
- Lib lines / branches / methods not exercised by any test
- Branches missed because tests cover only one side of a `??` / ternary / nullable check (the most common gap)

> The `(int)` 0% entries you'll see in Rider coverage on `_OnFailure_ShortCircuits` / `_DoesNotInvokeProjection` tests are intentional — they're the lambda body that the test asserts is never invoked. Coverage tooling can't distinguish "untested" from "asserted-never-called"; the 0% there IS the test result. Lib coverage (the only coverage that matters for the Definition of Done) is what the `D2.Shared.{LibName}` row in the cobertura XML reports.

---

## When to expand this project

A new shared lib lands in `server/shared/dotnet/{lib}/` → create `Unit/{Lib}/` here with one test file per source file (`{SourceFile}Tests.cs`). Project reference to the lib goes in `D2.Shared.Tests.csproj`.

Integration tests (Testcontainers spin-up of Postgres, Redis, RabbitMQ) will land in `Integration/` subdirectory when libs that need real infrastructure (caching-redis, messaging) ship.
