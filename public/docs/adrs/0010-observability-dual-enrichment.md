<!--
Copyright (c) DCSV. All rights reserved.
-->


> **Visibility: PUBLIC** — ships with the open surface (`public/`).  
> Do not add product IP, private paths, or non-exportable runbooks.
# ADR-0010: Observability — OTel aggregation + dual span-tag and log-scope enrichment

- **Status**: Accepted
- **Date**: 2026-05-30 (service-identity-removal re-verify note: 2026-06-18)
- **Deliverable**: D2 shared libraries (backfilled)

## Context

Every service ships two independent observability signal paths that must carry the same request-context fields simultaneously: distributed traces to Tempo (via OTLP) and structured log lines to Loki (via Serilog + OTLP). These are separate backends with separate query languages; a field set only on a span does not appear on a log line, and vice versa.

The codebase spans multiple shared libraries (`Handler`, `Auth`, `Auth.Outbound`, `Messaging.RabbitMq`, `Caching.Distributed.Redis`, `Caching.Local.Default`), each owning its own `ActivitySource` and/or `Meter`. Each service's composition root previously had to know the exact string names of every library's source/meter to subscribe — leaking internal naming across the boundary and silently breaking on any rename. Infrastructure paths (`/health`, `/alive`, `/metrics`, `/.well-known`) required filtering in two places (logging middleware + tracer instrumentation), risking divergence.

## Decision

**1. Dual enrichment: span tags AND log scope are set independently per handler invocation.** `BaseHandler.RunCorePipelineAsync` (ADR-0005) sets OTel span tags on the current `Activity` and opens a Serilog log scope in two separate, explicit blocks within the same pipeline pass. Tags (`d2.handler.name`, `d2.user_id`, `d2.org_id`, `d2.org_type`, `d2.org_role`, impersonation context) propagate to Tempo; an overlapping field set passed to `Logger.BeginScope(...)` (also carrying `d2.trace_id`) propagates to every Serilog event inside `ExecuteAsync` — including through the MEL bridge to the OTLP log exporter. Neither mechanism is treated as a proxy for the other; both are always exercised. The same dual-write discipline applies at the HTTP boundary: `UseD2RequestLogging` opens a Serilog diagnostic context enriched by `D2RequestContextEnricher` (projecting the 42 LOG-OK fields from `IRequestContext`), while OTel AspNetCore instrumentation records spans on the same requests — independent codepaths, neither subsuming the other.

**2. Cross-library `ActivitySource`/`Meter` aggregation via compile-time symbol references with spec-pin drift tests.** `AggregatedTelemetrySources` is the single place collecting all cross-library telemetry registrations, enumerating the four `ActivitySource` names and six `Meter` names by referencing each owning library's `public const string` directly (`HandlerTelemetry.SourceName`, `AuthTelemetry.ACTIVITY_SOURCE_NAME`, …). `AddD2Telemetry` iterates these to register all sources in one call. A rename of any owning `const` surfaces immediately as a compile error here; separate spec-pin tests assert the literal wire values (catching the distinct failure where a value drifts without a rename).

**3. Per-signal OTLP exporters are env-var-gated; `OTEL_SDK_DISABLED` symmetrically suppresses all registration.** OTLP exporters for traces/metrics/logs register independently, each only when its canonical endpoint env var resolves to a non-falsey URI. Log export runs through `AddSerilog(writeToProviders: true)`, routing Serilog into the OTel `OpenTelemetryLoggerProvider`. The Prometheus endpoint at `/metrics` (`MapD2PrometheusEndpoint`) is protected by `InternalIpFilter` (403 for any remote IP not loopback or RFC 1918). `OTEL_SDK_DISABLED=true` short-circuits both `AddD2Telemetry` and `MapD2PrometheusEndpoint` symmetrically; the HttpClient instrumentation filter suppresses spans for outbound calls to configured OTLP endpoints (preventing `HttpClient → OTLP → HttpClient` loops).

**4. Telemetry tag constants are codegen'd from the spec** (an instance of ADR-0002). The generator in `telemetry/tags-source-gen/` reads `public/contracts/telemetry/telemetry.spec.json` and emits per-meter `*TelemetryTags.g.cs` typed constants; cross-spec references resolve via `CrossSpecResolver` (e.g. the `d2.auth.problem.emitted` tag values are sourced from `auth-error-codes.spec.json`, not duplicated). Hand-written tag-name literals are forbidden for any tag with a spec entry.

**5. One canonical `InfrastructurePathMatcher`** (`D2.Shared.AspNetCore`) is the single source of truth for the infra path set. The telemetry instrumentation `Filter` and the logging `UseD2RequestLogging` level callback both call the same static matcher with the same default path list — the two filters cannot diverge.

## Consequences

**Positive.**

- Operators correlate handler-level context (user, org, impersonation) in Tempo traces and Loki logs independently, without a cross-backend join; both signals are always populated.
- A library rename of a telemetry `const` is a build break at `AggregatedTelemetrySources`, not a silent dashboard gap found post-deploy.
- Infra path filtering — the noisiest probe traffic — is suppressed identically in logging and tracing from one definition.
- The Prometheus endpoint is inaccessible from public addresses by construction; no per-deployment firewall rule needed for that surface.
- `OTEL_SDK_DISABLED=true` gives test harnesses a clean, complete no-op with no dangling providers or routes.

**Negative / risks.**

- Dual-write constructs two overlapping data structures (Activity tags + log-scope dictionary) per invocation — a modest allocation that auto-instrumentation-only approaches avoid.
- `AggregatedTelemetrySources` is a manual registry: adding a new library's telemetry needs a PR there, and forgetting produces no compile error — only missing dashboards (partially mitigated by the spec-pin presence tests, which must be updated in the same PR). When the service-identity components of `D2.Shared.Auth.Outbound` are removed (superseded by mTLS workload identity per [ADR-0023](0023-mtls-workload-identity.md), a later deliverable), the enumerated `ActivitySource` / `Meter` counts and the owning-library list here are re-verified against the trimmed lib; the token-exchange half of `Auth.Outbound` and its lib-level telemetry are retained, so the lib stays in the list and the current counts hold until that removal lands.
- `writeToProviders: true` means each Serilog line is processed twice (Serilog pipeline + MEL→OTel bridge); operators must size the OTLP log collector accordingly.
- `InternalIpFilter` uses byte-prefix matching with no `IPNetwork` abstraction; IPv6 private ranges other than `::1` are not admitted — deliberate for now, but a revisit point on IPv6-primary pod networks.

## Alternatives considered

**Rely on auto-instrumentation only.** AspNetCore auto-instrumentation emits HTTP-level span attributes but not application context (user/org/impersonation), which the instrumentation library cannot see. Filtering traces by authenticated subject would require a custom processor, re-introducing most of the dual-enrichment code.

**Per-library telemetry registration (each lib calls `AddOpenTelemetry()` itself).** Removes the aggregation layer but takes an OTel SDK dependency into every library, lets whichever-registers-first own SDK init, and pushes export configuration into library authors who should not own it. Aggregation keeps SDK/export knowledge in one place and keeps libraries free of export concerns (consistent with ADR-0006).

**Set span tags only; derive logs from span export.** Some stacks generate logs from spans, reducing dual-write. D²-WORX's Loki lines carry 42 LOG-OK `IRequestContext` fields a span carries only partly; a log-from-span transform for every field relies on infrastructure outside the application boundary. Explicit log scope is more predictable and self-contained.

**Hand-written tag-name constants per library.** A plain `Tags` class is straightforward but creates two independent maintenance surfaces (the constant + the dashboard query referencing the wire value). Spec-driven codegen (ADR-0002) gives compile-time symbol safety *and* a literal-drift test; hand-written constants give only the former.

## References

- `public/packages/dotnet/telemetry/core/` — `TelemetryServiceCollectionExtensions.cs` (`AddD2Telemetry`, OTLP wiring, self-referential HttpClient filter), `Internal/AggregatedTelemetrySources.cs` (4 `ActivitySource` + 6 `Meter` via symbol refs), `Internal/OtelSdkDisabledGate.cs`, `Internal/InternalIpFilter.cs`, `WebApplicationTelemetryExtensions.cs` (`MapD2PrometheusEndpoint`), `D2TelemetryOptions.cs`.
- `public/packages/dotnet/telemetry/tags-source-gen/` — `TelemetryTagsGenerator.cs`, `TelemetryTagsEmitter.cs`, `CrossSpecResolver.cs`.
- `public/packages/dotnet/handler/core/BaseHandler.cs` + `HandlerTelemetry.cs` — dual enrichment (`SetTag` + `BeginScope`) and the four handler instruments.
- `public/packages/dotnet/logging/` — `LoggingServiceCollectionExtensions.cs` (`AddSerilog(writeToProviders: true)`), `WebApplicationLoggingExtensions.cs` (`UseD2RequestLogging`), `Internal/D2RequestContextEnricher.cs` (42 LOG-OK fields).
- `docs/PATTERNS.md` (Telemetry / Logging / AspNetCore sections).
- [ADR-0011](0011-pii-redaction-logging-safety.md) — PII-redaction safety (`[RedactData]` destructuring policy, `SanitizedExceptionRender`, the LOG-OK / NOT-LOGGED split). [ADR-0006](0006-abstractions-implementation-split.md) — libraries publish telemetry names as `const`, not SDK registrations. [ADR-0005](0005-handler-pipeline.md) — the pipeline that performs dual enrichment. [ADR-0002](0002-spec-driven-codegen.md) — the codegen pattern for tag constants. [ADR-0013](0013-service-defaults-composition-root.md) — the composition-root aggregator.
