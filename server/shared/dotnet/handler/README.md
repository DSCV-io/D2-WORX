<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Shared.Handler

> Parent: [`server/shared/dotnet/`](../README.md)

`BaseHandler<TSelf, TInput, TOutput>` — the abstract base every handler in every service inherits (CQRS handlers, repo handlers, messaging consumers, scheduled jobs, anything handler-shaped). Provides per-handler scope pre-check, OTel activity + 4 metrics + log scope + stopwatch + universal try/catch around the subclass's `ExecuteAsync`. Sibling `D2.Shared.Handler.Repo` adds EF / PG exception → `D2Result` mapping on top.

JWT signature / expiry / audience / fingerprint-binding validation is a transport-level concern handled by auth middleware (HTTP / gRPC / AMQP) BEFORE the handler runs. By the time a handler executes, the bearer token has already been validated for the host service.

---

## ⚠ PII safety — REQUIRED setup before deploying any handler

`BaseHandler` logs handler input via Serilog destructuring (`{@Input}`) at `Debug` level, gated by `HandlerOptions.LogInput` (default `true`). Without proper redaction wiring, ANY field on the input that contains PII (email, phone, name, address, raw IP, etc.) will appear verbatim in logs whenever Debug-level logging is enabled.

**Every consuming service MUST register the `[RedactData]`-aware Serilog destructuring policy at startup.** The platform's `service-defaults` lib provides this registration as part of its standard observability setup; if you bootstrap logging manually instead of using `service-defaults`, you must register the policy yourself.

If you can't guarantee the redaction policy is in place (e.g. a one-off tool, an early bootstrap path), set `LogInput = false` in `DefaultOptions` for every handler that touches PII. Better: use `service-defaults` and don't think about it.

---

## File layout

| Path                                    | Contents                                                                                                                                                       |
| --------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `D2.Shared.Handler.csproj`              | csproj — refs handler-abstractions + context-abstractions + result + DI/Logging.Abstractions                                                                   |
| `BaseHandler.cs`                        | Abstract base. Virtual `HandleAsync` (entry point) + non-virtual `RunCorePipelineAsync` (observability + try/catch) + abstract `ExecuteAsync` (subclass logic) |
| `BaseHandler.Logging.cs`                | Source-generated `LoggerMessage` delegates for the pipeline                                                                                                    |
| `HandlerContext.cs`                     | `HandlerContext<T>` — typed-logger context. Open-generic registration via `AddD2Handler`                                                                       |
| `HandlerTelemetry.cs`                   | Static OTel primitives — `ActivitySource` + `Meter` + 4 instruments (`d2.handler.invoked` / `succeeded` / `failed` counters + `d2.handler.duration` histogram) |
| `HandlerServiceCollectionExtensions.cs` | `AddD2Handler(this IServiceCollection)` — registers open-generic `HandlerContext<T>`                                                                           |

---

## BaseHandler shape

```csharp
public abstract class BaseHandler<TSelf, TInput, TOutput> : IHandler<TInput, TOutput>
    where TSelf : BaseHandler<TSelf, TInput, TOutput>
{
    protected BaseHandler(HandlerContext<TSelf> context);

    protected IHandlerContext Context { get; }
    protected virtual HandlerOptions DefaultOptions { get; }

    public virtual ValueTask<D2Result<TOutput?>> HandleAsync(
        TInput input, CancellationToken ct = default, HandlerOptions? options = null);

    protected ValueTask<(D2Result<TOutput?> Result, Exception? CapturedException)> RunCorePipelineAsync(
        TInput input, CancellationToken ct, HandlerOptions? options);

    protected abstract ValueTask<D2Result<TOutput?>> ExecuteAsync(TInput input, CancellationToken ct);
}
```

`RunCorePipelineAsync` returns the captured exception alongside the result so EF-flavored subclasses (`BaseRepoHandler`) can remap typed exceptions to `D2Result` failure codes from their own overridden `HandleAsync`.

---

## HandleAsync flow

1. **Resolve options**: per-call → `DefaultOptions` → platform defaults
2. **Scope pre-check**: every `RequiredScope` must be in `Context.Request.Scopes`; missing → `D2Result.Forbidden`
3. **Activity start**: `HandlerTelemetry.ActivitySource.StartActivity(handlerName)`. Tags emitted (when present):
   - Always: `d2.handler.name`
   - When user identity present: `d2.user_id`, `d2.org_id`, `d2.org_type`, `d2.org_role`
   - When impersonating: `d2.impersonating`, `d2.impersonation_kind`, `d2.impersonator_id`, `d2.impersonator_org_id`, `d2.impersonator_org_type`, `d2.impersonator_org_role`
4. **Counter**: `HandlerTelemetry.Invoked.Add(1, handlerNameTag)`
5. **`ExecuteAsync`** inside try/catch
6. **Stopwatch + threshold logging**: warn at `SlowThreshold`, error at `CriticalThreshold`
7. **Result handling**:
   - Success → `Succeeded` counter + debug log
   - Failure → `Failed` counter + debug log
   - `OperationCanceledException` **with our `ct` canceled** → `D2Result.Canceled` + info log (intentional caller cancellation)
   - `OperationCanceledException` **without our `ct` canceled** → `D2Result.ServiceUnavailable` + warn log (downstream timeout — HttpClient timeout, SQL command timeout, internal handler watchdog)
   - Other exception → `D2Result.UnhandledException` + error log + activity status set
8. **Duration**: `HandlerTelemetry.Duration.Record(elapsedMs, handlerNameTag)` always

`TraceId` is auto-injected on every emitted `D2Result` via `Context.Request.TraceId`.

### Why distinguish caller-canceled from downstream-timeout

A downstream timeout (e.g. `HttpClient.Timeout` firing) surfaces as `OperationCanceledException` whose token is the timeout's internal token — NOT our `ct`. Treating it as `UnhandledException` (500) implies a bug in our code; treating it as `ServiceUnavailable` (503) correctly signals "a dependency we needed isn't responding." That's the right HTTP semantic and the right operational signal.

---

## DI Registration

```csharp
services.AddD2Handler();   // registers open-generic HandlerContext<T> as Transient

// Per-handler registration (typically in {Service}.App's AddXxxApp extension):
services.AddTransient<IGetUserById, GetUserById>();
```

`AddD2Handler` does NOT register `IRequestContext` — that's transport-specific. Each consuming transport stack is responsible for constructing per-request `IRequestContext` and putting it into the DI scope before any handler resolves:

- HTTP / gRPC.AspNetCore: the consuming service's startup wires HTTP middleware that builds `IRequestContext` from the validated bearer + ambient request data
- RabbitMQ consumer: the consuming service's consumer pipeline builds `IRequestContext` from the AMQP frame headers + decrypted body

Tests provide a `MutableRequestContext` test fixture builder.

---

## Telemetry instruments

| Instrument             | Type               | Unit      | Description                                                   |
| ---------------------- | ------------------ | --------- | ------------------------------------------------------------- |
| `d2.handler.invoked`   | Counter (long)     | `{calls}` | Handler invocations attempted                                 |
| `d2.handler.succeeded` | Counter (long)     | `{calls}` | Handler invocations that returned `Success == true`           |
| `d2.handler.failed`    | Counter (long)     | `{calls}` | Handler invocations that returned `Success == false` OR threw |
| `d2.handler.duration`  | Histogram (double) | `ms`      | Handler invocation wall-clock duration                        |

All instruments tag with `d2.handler.name = typeof(TSelf).Name`. The `service-defaults` lib registers `MeterProvider` + `TracerProvider` to capture the `D2.Shared.Handler` source.

---

## Subclassing pattern

```csharp
public sealed class GetUserById(HandlerContext<GetUserById> context, IUserRepo repo)
    : BaseHandler<GetUserById, GetUserByIdInput, UserDto>, IGetUserById
{
    protected override HandlerOptions DefaultOptions => new()
    {
        RequiredScopes = new HashSet<string>(StringComparer.Ordinal) { Scopes.Self.Read },
        SlowThreshold = TimeSpan.FromMilliseconds(50),
    };

    protected override async ValueTask<D2Result<UserDto?>> ExecuteAsync(
        GetUserByIdInput input, CancellationToken ct)
    {
        var user = await repo.FindAsync(input.Id, ct);
        if (user is null)
        {
            return D2Result<UserDto?>.NotFound();
        }

        return D2Result<UserDto?>.Ok(user.ToDto());
    }
}
```

Handler primary-constructor parameters do NOT take the `r_` prefix (carve-out from the standard naming convention).

---

## Reference

- [`D2.Shared.Handler.Abstractions`](../handler-abstractions/README.md) — `IHandler` + `HandlerOptions` + `IHandlerContext`
- [`D2.Shared.Handler.Repo`](../handler-repo/README.md) — EF/PG exception remapping subclass
- [`docs/PATTERNS.md`](../../../../docs/PATTERNS.md) "Handler" section — TLC/2LC/3LC convention, primary-constructor carve-out
