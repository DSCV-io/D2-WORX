<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.Utilities — Diagnostics

> Part of [`DcsvIo.D2.Utilities`](../README.md).

PII-safe rendering primitives consumed by every lib whose `[LoggerMessage]` delegates carry exception-derived strings. Keeps `Exception.Message` (which can interpolate JWT bytes, AMQP URIs with passwords, request URIs, etc.) out of the log pipeline at the type level.

| File                          | Contents                                                                                                                             |
| ----------------------------- | ------------------------------------------------------------------------------------------------------------------------------------ |
| `SanitizedExceptionRender.cs` | PII-safe exception rendering for log + telemetry + DLQ-header surfaces. `TypeName(ex)` + `FirstFrame(ex)` only — never `ex.Message`. |

## `SanitizedExceptionRender` — PII-safe exception rendering

`Diagnostics/SanitizedExceptionRender.cs` (namespace `DcsvIo.D2.Utilities.Diagnostics`) is the canonical helper consumed by every lib whose `[LoggerMessage]` delegates carry exception-derived strings — pair with the no-`Exception`-parameter `[LoggerMessage]` contract (enforced by per-lib reflection-based contract tests across each log surface) to keep `Exception.Message` out of the log pipeline at the type level.

```csharp
catch (Exception ex)
{
    // Type FullName + first stack frame only; never ex.Message.
    r_logger.SomeLogDelegate(
        SanitizedExceptionRender.TypeName(ex),     // "System.InvalidOperationException"
        SanitizedExceptionRender.FirstFrame(ex));  // "Foo.Bar at C:\...:42" or "<no frame>"
}
```

`TypeName(ex)` returns `ex.GetType().FullName ?? ex.GetType().Name`. `FirstFrame(ex)` returns `"{Method} at {File}:{Line}"` for the first stack frame, or the literal sentinel `"<no frame>"` when no stack is available (caller doesn't need to null-check before string interpolation). Both outputs are derived purely from developer-controlled metadata — user input cannot influence either, so they're safe to log / attach to a broker header / record on a span.

**Why "never `ex.Message`"**: exception messages can interpolate JWT bytes, request URIs, response bodies, configured secrets, AMQP connection URIs with embedded passwords, connection strings — any of which would land in the log pipeline / metric tags / DLQ headers verbatim if the raw `Exception` were passed.
