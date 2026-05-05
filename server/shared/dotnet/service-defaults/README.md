<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Shared.ServiceDefaults

> Parent: [`server/shared/dotnet/`](../README.md)

> **Status**: placeholder — not yet implemented.

## Purpose

Service composition root — one-call OTel SDK bootstrap (`SetupTelemetry()`) + Serilog configuration + `[RedactData]` destructuring policy registration + structured request logging. Every service's `Program.cs` calls these to get consistent telemetry + logging without per-service ceremony.

## Public API surface

- `SetupTelemetry(IServiceCollection, ServiceTelemetryOptions)` — registers OTel tracing + logging + metrics with OTLP/HTTP exporters to Alloy
- `RedactDataDestructuringPolicy` — Serilog destructuring policy that respects `[RedactData]` attributes (type-level + property-level, recursive, reflection-cached)
- `AddD2Logging(IServiceCollection)` — registers Serilog with the destructuring policy + standard sinks (console + OTLP)
- `AddD2RequestLogging(IApplicationBuilder)` — middleware for structured request logs with traceId / correlationId / userId / orgId fields
- `AddInfrastructurePathFiltering(IApplicationBuilder)` — exempts health / metrics / observability paths from business middleware
- Configuration helpers: `parseEnvArray()` for indexed env-var convention (`PREFIX__0`, `PREFIX__1`); URL parsers (`postgres://`, `redis://`, `amqp://`)

## Dependencies

- `D2.Shared.Result` (request logging emits result info)
- `D2.Shared.Utilities` (`Truthy` / `Falsey` for env var parsing)
- `OpenTelemetry.Sdk` + exporters
- `Serilog` + `Serilog.Sinks.OpenTelemetry`

## References

- `[RedactData]` attribute — this lib's `RedactDataDestructuringPolicy` is the mechanism that makes it work
- [docs/PATTERNS.md](../../../../docs/PATTERNS.md) "RedactDataDestructuringPolicy mechanics" — full design notes
- [docs/PATTERNS.md](../../../../docs/PATTERNS.md) "Configuration" section — `parseEnvArray()` + URL parsers
