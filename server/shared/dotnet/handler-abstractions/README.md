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
    public TimeSpan? SlowThreshold { get; init; }
    public TimeSpan? CriticalThreshold { get; init; }
    public bool ValidateAudience { get; init; } = true;
    public IReadOnlySet<string>? RequiredScopes { get; init; }
}
```

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

- [`D2.Shared.Handler`](../handler/) — the concrete `BaseHandler` + `HandlerContext` + DI extension
- [`D2.Shared.Handler.Repo`](../handler-repo/) — EF-flavored handler that maps PG/EF exceptions to `D2Result` failure codes
- [`docs/PATTERNS.md`](../../../../docs/PATTERNS.md) "Handler" section — full mechanics, TLC/2LC/3LC convention
