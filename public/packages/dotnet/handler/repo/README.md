<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.Handler.Repo

> Parent: [`public/packages/dotnet/`](../../README.md)

EF-flavored `BaseRepoHandler<TSelf, TInput, TOutput>` — sits on top of `BaseHandler` from `DcsvIo.D2.Handler`. Overrides `HandleAsync` to convert any database exception captured during `ExecuteAsync` into a typed `D2Result` failure (concurrency conflict, unique violation, deadlock, connection failure, etc.) so callers can branch on what actually went wrong instead of getting a generic 500.

Provider-agnostic by design: catches the BCL-typed `DbUpdateConcurrencyException` directly and routes everything else through an injected `IDbExceptionClassifier`. Provider-specific knowledge lives in sibling packages (e.g. `DcsvIo.D2.Handler.Repo.Postgres`).

---

## File layout

| Path                            | Contents                                                                                                                                                                                                                                                                                           |
| ------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `DcsvIo.D2.Handler.Repo.csproj` | csproj — refs `handler/core` + `handler/abstractions` + `handler/repo-abstractions` + `result` + `Microsoft.EntityFrameworkCore`. Zero provider deps.                                                                                                                                           |
| `BaseRepoHandler.cs`            | Abstract `BaseRepoHandler<TSelf, TInput, TOutput> : BaseHandler<TSelf, TInput, TOutput>`. Constructor takes an injected `IDbExceptionClassifier`. Override `HandleAsync` calls `RunCorePipelineAsync` then dispatches the captured exception through the classifier to a typed `D2Result` factory. |

---

## Mapping

| Captured exception                                              | Classified as                                  | Default `D2Result`                                             |
| --------------------------------------------------------------- | ---------------------------------------------- | -------------------------------------------------------------- |
| `DbUpdateConcurrencyException`                                  | `ConcurrencyConflict` (BCL — handled directly) | `D2Result.ConcurrencyConflict()`                               |
| `IDbExceptionClassifier.Classify(ex)` returns `UniqueViolation` | `UniqueViolation`                              | `D2Result.UniqueViolation()`                                   |
| Returns `ForeignKeyViolation`                                   | `ForeignKeyViolation`                          | `D2Result.ForeignKeyViolation()`                               |
| Returns `NotNullViolation`                                      | `NotNullViolation`                             | `D2Result.NotNullViolation()`                                  |
| Returns `CheckViolation`                                        | `CheckViolation`                               | `D2Result.CheckViolation()`                                    |
| Returns `Timeout`                                               | `Timeout`                                      | `D2Result.DbTimeout()`                                         |
| Returns `Deadlock`                                              | `Deadlock`                                     | `D2Result.DbDeadlock()`                                        |
| Returns `ConnectionFailure`                                     | `ConnectionFailure`                            | `D2Result.DbConnectionFailure()`                               |
| Classifier returns `null`                                       | unknown                                        | Falls through — `BaseHandler`'s `UnhandledException` preserved |

`OperationCanceledException` is intentionally NOT remapped here — `BaseHandler.RunCorePipelineAsync` already handles it (returns `D2Result.Canceled` for caller-initiated cancellation, `D2Result.ServiceUnavailable` for downstream timeouts not tied to the request token).

---

## Per-handler refinement

The default factory dispatch produces a generic message ("This value is already in use") with no field-level information — useful for diagnostics + programmatic discrimination, but weak UX for form-driven flows.

Handlers that know their constraint identity should override `MapDbException` to attach a domain-specific `TKMessage` + `InputError`:

```csharp
public sealed class CreateUser(
    HandlerContext<CreateUser> context,
    IDbExceptionClassifier classifier,
    IAppDbContext db)
    : BaseRepoHandler<CreateUser, CreateUserInput, UserDto>(context, classifier), ICreateUser
{
    protected override async ValueTask<D2Result<UserDto?>> ExecuteAsync(
        CreateUserInput input, CancellationToken ct)
    {
        var user = User.Create(input);
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        return D2Result<UserDto?>.Created(user.ToDto());
    }

    protected override D2Result<UserDto?>? MapDbException(Exception ex, DbFailureKind kind)
    {
        // The DB-side unique index `users_email_key` covers the email column.
        if (kind == DbFailureKind.UniqueViolation && IsEmailIndex(ex))
        {
            return D2Result<UserDto?>.UniqueViolation(
                messages: [TK.Auth.Errors.EMAIL_ALREADY_TAKEN],
                inputErrors: [new InputError("email", "EMAIL_ALREADY_TAKEN")]);
        }

        return null; // fall back to the generic factory
    }
}
```

Returning `null` from the override means "use the default" — handlers only customize the cases they care about.

---

## Caller-side discrimination

Callers branch on the typed booleans from `DcsvIo.D2.Handler.Repo.Abstractions`:

```csharp
var result = await createUser.HandleAsync(input);

if (result.IsUniqueViolation)        return Conflict(result);                  // 409, surface to user
if (result.IsConcurrencyConflict)    return await ReloadAndMergeAsync(input);  // optimistic-concurrency retry
if (result.IsTransientDbFailure)     return await retry.RetryAsync(...);       // deadlock / timeout / connection
if (result.IsForeignKeyViolation)    return BadRequest(result);                // referenced item missing
```

The roll-up `IsTransientDbFailure` covers deadlock + timeout + connection-failure (caller may safely retry). Concurrency conflicts are intentionally excluded from the roll-up — they need reload-then-merge, not a blind retry.

`IsTransientDbFailure` is a separate axis from the built-in `IsTransientRetryable` (`IsServiceUnavailable || IsRateLimited`) on `D2Result` from the result lib. A generic retry policy that wants to catch BOTH the HTTP-flavored AND DB-flavored transient sets should check the union: `result.IsTransientRetryable || result.IsTransientDbFailure`.

---

## DI registration

`BaseRepoHandler` requires an `IDbExceptionClassifier` from DI. The composition root registers a provider-specific implementation:

```csharp
services.AddD2Handler();
services.AddD2Postgres();   // registers PostgresDbExceptionClassifier as IDbExceptionClassifier
services.AddDbContext<AppDbContext>(o => o.UseNpgsql(...));
services.AddTransient<ICreateUser, CreateUser>();
```

Without a registered classifier, resolving any `BaseRepoHandler` subclass fails fast at the container.

---

## Dependencies

Project references:

- `DcsvIo.D2.Handler` — base + `HandlerContext<T>`
- `DcsvIo.D2.Handler.Abstractions` — `IHandler`, `HandlerOptions`
- `DcsvIo.D2.Handler.Repo.Abstractions` — `IDbExceptionClassifier`, `DbFailureKind`, `D2Result.X` extension factories
- `DcsvIo.D2.Result` — base `D2Result`

Package references:

- `Microsoft.EntityFrameworkCore` — `DbUpdateConcurrencyException`

No `Npgsql`, no provider-specific deps.

---

## Reference

- [`DcsvIo.D2.Handler.Repo.Abstractions`](../repo-abstractions/README.md) — vocabulary + extension factories + booleans
- [`DcsvIo.D2.Handler.Repo.Postgres`](../repo-postgres/README.md) — PG classifier impl
- [`DcsvIo.D2.Handler`](../core/README.md) — base
- [PostgreSQL error code reference](https://www.postgresql.org/docs/current/errcodes-appendix.html)
