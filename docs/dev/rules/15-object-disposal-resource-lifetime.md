<!--
Copyright (c) DCSV. All rights reserved.
-->

## 15. Object Disposal & Resource Lifetime
<a name="top"></a>
_[← rules index](../rules.md) · §15 of the D2-WORX rules catalog._

Resource leaks are silent in dev, costly in production. Every `IDisposable` / `IAsyncDisposable` MUST have its lifetime documented and enforced.

### Predicates — §15 object disposal & resource lifetime

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

<sup>[↑ jump to top](#top)</sup>

---

