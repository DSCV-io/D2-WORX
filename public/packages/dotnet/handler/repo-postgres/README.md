<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.Handler.Repo.Postgres

> Parent: [`public/packages/dotnet/`](../../README.md)

PostgreSQL implementation of `IDbExceptionClassifier` from `DcsvIo.D2.Handler.Repo.Abstractions`. Plugs into `BaseRepoHandler` via DI — composition roots call `services.AddD2Postgres()` once.

Provider-specific knowledge lives behind `IDbExceptionClassifier`; alternate-provider implementations (where they exist) follow the same shape: one `IDbExceptionClassifier` impl + one `services.AddD2X()` extension. `BaseRepoHandler` itself stays provider-agnostic.

---

## File layout

| Path                                     | Contents                                                                 |
| ---------------------------------------- | ------------------------------------------------------------------------ |
| `DcsvIo.D2.Handler.Repo.Postgres.csproj` | csproj — depends on `handler/repo-abstractions` + EF Core + Npgsql       |
| `PgErrorCodes.cs`                        | SQLSTATE string constants + `TryGetPgException(Exception)` unwrap helper |
| `PostgresDbExceptionClassifier.cs`       | Maps PG exceptions → `DbFailureKind`                                     |
| `PostgresServiceCollectionExtensions.cs` | `services.AddD2Postgres()`                                               |

---

## SQLSTATE → DbFailureKind matrix

| SQLSTATE | Name                               | `DbFailureKind`       |
| -------- | ---------------------------------- | --------------------- |
| `23505`  | unique_violation                   | `UniqueViolation`     |
| `23503`  | foreign_key_violation              | `ForeignKeyViolation` |
| `23502`  | not_null_violation                 | `NotNullViolation`    |
| `23514`  | check_violation                    | `CheckViolation`      |
| `40001`  | serialization_failure              | `Deadlock`            |
| `40P01`  | deadlock_detected                  | `Deadlock`            |
| `57014`  | query_canceled (statement_timeout) | `Timeout`             |
| `57P03`  | cannot_connect_now                 | `ConnectionFailure`   |
| `53300`  | too_many_connections               | `ConnectionFailure`   |
| `08***`  | connection_exception class         | `ConnectionFailure`   |

`DbUpdateConcurrencyException` (BCL-typed) is handled directly by `BaseRepoHandler` and never reaches the classifier.

Codes per <https://www.postgresql.org/docs/current/errcodes-appendix.html>.

---

## Three exception shapes handled uniformly

```csharp
public DbFailureKind? Classify(Exception exception)
```

The classifier accepts any `Exception` and walks three shapes:

1. **`DbUpdateException` wrapping a `PostgresException`** — the EF save-pipeline path. The unwrap helper finds the inner `PostgresException` and dispatches on its `SqlState`.
2. **Raw `PostgresException`** — thrown directly during a read / query / DDL when EF doesn't wrap it. Same `SqlState` switch.
3. **Network-level failure** — `SocketException` / `IOException` anywhere in the inner-exception chain (connection refused, socket reset, host unreachable, transport-stream fault). Returns `ConnectionFailure`. The walk descends up to 10 inner-exception levels so `AggregateException` / `TargetInvocationException` / EF wrappers don't hide the underlying network error.

Bare `NpgsqlException` instances with no recognizable inner cause (bad connection-string parse, SSL handshake failure, concurrent-connection misuse, internal Npgsql state errors) return `null` — `BaseRepoHandler` then preserves the original `UnhandledException` result. These are programmer / config failures and should NOT be silently treated as transient and retried.

**Precedence**: when both shapes apply (e.g., a `PostgresException` whose inner exception chain also contains a `SocketException`), pass 1 (SQLSTATE classification) wins. Pass 2 (network detection) only fires when pass 1 yields `null` — either the `PostgresException` had no recognized SQLSTATE, the SQLSTATE was `null`, or no `PostgresException` was found at all. Tests pin this ordering.

Client-side `NpgsqlCommand.CommandTimeout` blowups are NOT classified here — Npgsql surfaces them as `OperationCanceledException`, which `BaseHandler.RunCorePipelineAsync` already handles before `BaseRepoHandler` sees the captured exception. Server-side `statement_timeout` cancellations DO reach this classifier as `PostgresException` SQLSTATE `57014` (pass 1) → `DbFailureKind.Timeout`.

---

## DI registration

```csharp
// Composition root
services.AddD2Handler();
services.AddD2Postgres();           // ← registers PostgresDbExceptionClassifier
services.AddDbContext<AppDbContext>(o => o.UseNpgsql(...));
```

`AddD2Postgres()` uses `TryAddSingleton` — calling it multiple times is safe (no duplicate registration). To override the default classifier with a custom implementation, register the custom BEFORE the call:

```csharp
services.AddSingleton<IDbExceptionClassifier, MyCustomClassifier>();
services.AddD2Postgres();   // no-op — TryAdd sees an existing registration
```

**If a service genuinely needs more than one classifier** (e.g. one connection to Postgres, one to an embedded SQLite for tests) don't try to register two `IDbExceptionClassifier`s into the unkeyed slot — DI's last-registered-wins resolution makes the active impl call-order-dependent and leaves the other as an orphaned singleton in the graph. Use **keyed registration + keyed resolution** instead:

```csharp
services.AddKeyedSingleton<IDbExceptionClassifier, PostgresDbExceptionClassifier>("primary");
services.AddKeyedSingleton<IDbExceptionClassifier, SqliteDbExceptionClassifier>("scratch");

// Inject via [FromKeyedServices("primary")] IDbExceptionClassifier classifier
```

Keyed services are .NET 8+ — the codebase targets .NET 10, so the API is available.

---

## Dependencies

Project references:

- `DcsvIo.D2.Handler.Repo.Abstractions` — `IDbExceptionClassifier` + `DbFailureKind`

NuGet packages:

- `Microsoft.EntityFrameworkCore` — for `DbUpdateException` type
- `Npgsql` — for `PostgresException` + `NpgsqlException` types
- `Microsoft.Extensions.DependencyInjection.Abstractions` — for `IServiceCollection`

---

## Reference

- [`DcsvIo.D2.Handler.Repo.Abstractions`](../repo-abstractions/README.md) — interface + `DbFailureKind` enum + extension factories
- [`DcsvIo.D2.Handler.Repo`](../repo/README.md) — `BaseRepoHandler` consumes the classifier
- [PostgreSQL error codes](https://www.postgresql.org/docs/current/errcodes-appendix.html)
