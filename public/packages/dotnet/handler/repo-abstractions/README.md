<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Shared.Handler.Repo.Abstractions

> Parent: [`public/packages/dotnet/`](../../README.md)

Vocabulary for repo-flavored handlers — what app-layer code touches when it needs to discriminate database failures (unique violation, FK violation, deadlock, concurrency conflict, connection failure, etc.). Pure abstractions: zero infrastructure dependencies. EF Core, Npgsql, and any provider knowledge live in sibling packages (`D2.Shared.Handler.Repo`, `D2.Shared.Handler.Repo.Postgres`).

---

## File layout

| Path                                         | Contents                                                                                                                                          |
| -------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------- |
| `D2.Shared.Handler.Repo.Abstractions.csproj` | csproj — depends only on `D2.Shared.Result` + `D2.Shared.I18n`                                                                                    |
| `DbFailureKind.cs`                           | Enum — `ConcurrencyConflict / UniqueViolation / ForeignKeyViolation / NotNullViolation / CheckViolation / Timeout / Deadlock / ConnectionFailure` |
| `IDbExceptionClassifier.cs`                  | The provider seam — `DbFailureKind? Classify(Exception)`                                                                                          |
| `DbErrorCodes.cs`                            | String constants (`UNIQUE_VIOLATION`, `FOREIGN_KEY_VIOLATION`, etc.) used as `D2Result.ErrorCode` values                                          |
| `D2ResultDbFactories.cs`                     | Static C# 14 extension factories on `D2Result` — `D2Result.UniqueViolation()`, `.ConcurrencyConflict()`, `.DbDeadlock()`, etc.                    |
| `D2ResultDbGenericFactories.cs`              | Same surface on `D2Result<TData>`                                                                                                                 |
| `D2ResultDbBooleans.cs`                      | C# 14 instance extension properties — `result.IsUniqueViolation`, `result.IsConcurrencyConflict`, `result.IsTransientDbFailure`, etc.             |

---

## Emitting + discriminating DB failures

Handler code returns typed DB failures via the extension factories:

```csharp
return D2Result<User>.UniqueViolation(
    messages: [TK.Auth.Errors.EMAIL_ALREADY_TAKEN],
    inputErrors: [new InputError("email", "EMAIL_ALREADY_TAKEN")]);
```

Caller code discriminates via the boolean discriminators:

```csharp
var result = await createUser.HandleAsync(input);
if (result.IsUniqueViolation)
    return Conflict(result.Messages);
if (result.IsTransientDbFailure)
    return await retryPolicy.RetryAsync(() => createUser.HandleAsync(input));
if (result.IsConcurrencyConflict)
    return await ReloadAndMergeAsync(input);
```

The `IsTransientDbFailure` predicate is a roll-up — `IsDbDeadlock || IsDbTimeout || IsDbConnectionFailure`. `IsConcurrencyConflict` is intentionally excluded — concurrency conflicts need reload-then-merge logic, not a blind retry.

`IsTransientDbFailure` is a separate axis from the built-in `IsTransientRetryable` (`IsServiceUnavailable || IsRateLimited`) on `D2Result` in the result lib. They live in separate libs because not every consumer of `D2Result` deals with a database; the DB roll-up only loads when the consumer references this package. A generic retry policy that wants to catch BOTH HTTP-flavored AND DB-flavored transient failures should check the union: `result.IsTransientRetryable || result.IsTransientDbFailure`.

---

## Default messages

Each factory accepts an optional `messages` parameter; when omitted, the generic fallback from `TK.Common.Errors.*` is used:

| Factory                 | Default `TKMessage`                      |
| ----------------------- | ---------------------------------------- |
| `ConcurrencyConflict()` | `TK.Common.Errors.CONCURRENCY_CONFLICT`  |
| `UniqueViolation()`     | `TK.Common.Errors.UNIQUE_VIOLATION`      |
| `ForeignKeyViolation()` | `TK.Common.Errors.FOREIGN_KEY_VIOLATION` |
| `NotNullViolation()`    | `TK.Common.Errors.NOT_NULL_VIOLATION`    |
| `CheckViolation()`      | `TK.Common.Errors.CHECK_VIOLATION`       |
| `DbTimeout()`           | `TK.Common.Errors.DB_TIMEOUT`            |
| `DbDeadlock()`          | `TK.Common.Errors.DB_DEADLOCK`           |
| `DbConnectionFailure()` | `TK.Common.Errors.DB_CONNECTION_FAILURE` |

These defaults are deliberately generic ("This value is already in use"). Handlers that know the constraint identity should override the message — e.g. `[TK.Auth.Errors.EMAIL_ALREADY_TAKEN]` instead — and attach an `InputError` so the form UI can highlight the offending field.

---

## Status code mapping

| Factory                                                                          | HTTP status             |
| -------------------------------------------------------------------------------- | ----------------------- |
| `ConcurrencyConflict` / `UniqueViolation` / `ForeignKeyViolation` / `DbDeadlock` | 409 Conflict            |
| `NotNullViolation` / `CheckViolation`                                            | 400 Bad Request         |
| `DbTimeout` / `DbConnectionFailure`                                              | 503 Service Unavailable |

---

## Dependencies

Project references:

- `D2.Shared.Result` — base `D2Result` type that the factories extend
- `D2.Shared.I18n` — `TKMessage` + `TK` codegen entry point

Zero external NuGet packages. No EF Core, no Npgsql.

---

## Reference

- [`D2.Shared.Handler.Repo`](../repo/README.md) — `BaseRepoHandler` consumes the classifier interface
- [`D2.Shared.Handler.Repo.Postgres`](../repo-postgres/README.md) — PostgreSQL classifier implementation
