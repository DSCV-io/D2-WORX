<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Shared.Handler.Abstractions

> Parent: [`server/shared/dotnet/`](../README.md)

Domain-safe slice of the handler stack: `IHandler<in TInput, TOutput>`, `IHandlerContext`, `HandlerOptions`. Domain code references this; the runtime piece (`BaseHandler`, `HandlerContext`, `HandlerTelemetry`, `AddD2Handler`) lives in `D2.Shared.Handler`.

---

## Public surface

```csharp
public interface IHandler<in TInput, TOutput>
{
    ValueTask<D2Result<TOutput?>> HandleAsync(
        TInput input,
        CancellationToken ct = default,
        HandlerOptions? options = null);
}

public interface IHandlerContext
{
    IRequestContext Request { get; }
    ILogger Logger { get; }
}

public sealed record HandlerOptions
{
    public bool LogInput { get; init; } = true;
    public bool LogOutput { get; init; } = true;
    public TimeSpan? SlowThreshold { get; init; } = TimeSpan.FromMilliseconds(100);
    public TimeSpan? CriticalThreshold { get; init; } = TimeSpan.FromMilliseconds(500);
    public IReadOnlySet<string>? RequiredScopes { get; init; }
}
```

> **JWT signature / expiry / audience / fingerprint-binding validation are NOT per-handler.** They're transport-level concerns handled by auth middleware (HTTP / gRPC / AMQP) BEFORE the handler runs. Per-handler scope requirements (`RequiredScopes`) ARE here because they vary by operation; audience / signature / etc. are per-service constants and putting them on `HandlerOptions` would be a footgun. See `HandlerOptions.cs` `<remarks>` for the fuller rationale.

---

## Why split from `D2.Shared.Handler`?

Domain code (entities + value objects + domain services) shouldn't depend on `Microsoft.Extensions.DependencyInjection`, `OpenTelemetry`, or any infrastructure package. The split lets domain projects reference only this lib (and pick up `IHandler` for handler-shaped domain services); the concrete + DI extension live in the sibling `D2.Shared.Handler`.

---

## Dependencies

Project references:

- `D2.Shared.Result` — `D2Result<T>` return type
- `D2.Shared.Context.Abstractions` — `IRequestContext` on the context

Package references:

- `Microsoft.Extensions.Logging.Abstractions` — `ILogger` on the context

---

## Reference

- [`D2.Shared.Handler`](../handler/README.md) — the concrete `BaseHandler` + `HandlerContext` + DI extension
- [`D2.Shared.Handler.Repo`](../handler-repo/README.md) — EF-flavored handler that maps PG/EF exceptions to `D2Result` failure codes
- [`docs/PATTERNS.md`](../../../../docs/PATTERNS.md) "Handler" section — full mechanics, TLC/2LC/3LC convention
