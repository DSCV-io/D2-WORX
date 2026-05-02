<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Shared.Handler

> **Status**: placeholder — not yet implemented.

## Purpose

The `BaseHandler<TSelf, TInput, TOutput>` pattern that every handler in every service inherits. Auto-emits 4 OTel metrics (invocation count, success count, failure count, duration histogram) per call. Provides `IHandlerContext` + `DefaultOptions` override + `[RedactData]` integration via Serilog destructuring.

## Public API surface

- `BaseHandler<TSelf, TInput, TOutput>` — abstract base; consumers override `executeAsync` (or `HandleAsync` per .NET conventions)
- `IHandlerContext` — request-scoped context (traceId, correlationId, userId, orgId, scope, WhoIs)
- `HandlerOptions` — per-call options (LogInput, LogOutput, custom Serilog scope properties, etc.)
- `BaseHandler.DefaultOptions` — handler-level override (e.g., `LogInput=false` for proto-DTOs that can't carry `[RedactData]`)

## Dependencies

- `D2.Shared.Result` (returns `D2Result<T>`)
- `Serilog` (structured logging + `[RedactData]` destructuring)
- `OpenTelemetry` (metrics + spans)

## References

- Primary constructors for handlers (carve-out — handler params don't take `r_` prefix)
- `[RedactData]` attribute (canonical PII redaction; works with this lib's destructuring policy)
- [docs/PATTERNS.md](../../../../docs/PATTERNS.md) "Handler" section — full mechanics
