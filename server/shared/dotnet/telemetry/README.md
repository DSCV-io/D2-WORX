<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Shared.Telemetry

> Parent: [`server/shared/dotnet/`](../README.md)

OpenTelemetry SDK setup (traces / metrics / logs) + OTLP exporters (per-signal opt-in via canonical env vars) + IP-restricted Prometheus scraping endpoint + aggregation of every shared lib's `ActivitySource` and `Meter` into a single `AddD2Telemetry()` call. Foundation lib that the composition-root aggregator and per-service `Program.cs` files call to wire the OTel SDK without each service duplicating ~120 lines of `OpenTelemetryBuilder` / `ConfigureResource` / per-instrumentation registration boilerplate.

The lib is intentionally independent of [`D2.Shared.Logging`](../logging/README.md). The two libs cooperate at runtime via the MEL bridge — Serilog's `writeToProviders: true` (set by `AddD2Logging`) routes through the OTLP log exporter that `AddD2Telemetry` registers as an `ILoggerProvider` — without either lib referencing the other at compile time. Hosts may wire one without the other.

The lib does NOT own:

- Serilog configuration / sinks — `D2.Shared.Logging` owns them.
- The `[RedactData]` enforcement pipeline — `D2.Shared.Logging` owns it.
- `[LoggerMessage]` source-generated delegates — bootstrapping the OTel logs MEL provider during MEL bootstrap is circular.
- Self-monitoring counters for export failures — the OTel SDK's own internal diagnostics suffice for the foundation tier; consumers can wire `EventListener` against the SDK if lib-internal export-failure visibility is needed.

## Public API surface

### Composition

```csharp
services.AddD2Telemetry(configuration, opts =>
{
    opts.ServiceName = "edge";                       // optional — defaults from OTEL_SERVICE_NAME → IHostEnvironment.ApplicationName
    opts.OtlpTracesEndpoint = "https://otlp.example.com/v1/traces";  // optional — defaults from OTEL_EXPORTER_OTLP_TRACES_ENDPOINT
    opts.AdditionalActivitySources = ["D2.Edge"];    // optional — service-specific activity sources
});

// later, inside the endpoint-routing pipeline:
app.MapD2PrometheusEndpoint();
```

`AddD2Telemetry(IConfiguration, Action<D2TelemetryOptions>?)` registers the OpenTelemetry SDK builder, the OTLP exporters whose corresponding endpoint env vars are truthy, the in-process Prometheus exporter (when `EnablePrometheusExporter` is true), and the standard auto-instrumentations (AspNetCore, HttpClient, GrpcNetClient, Process, Runtime). Validates options at the first `IOptions<D2TelemetryOptions>.Value` resolution via `ValidateOnStart()` — fail-fast on invalid config.

`MapD2PrometheusEndpoint()` maps `/metrics` via OpenTelemetry's `MapPrometheusScrapingEndpoint`, attaches an endpoint filter that returns `403 Forbidden` for requests whose connection-remote IP is neither loopback nor RFC 1918 private.

### `OTEL_SDK_DISABLED` short-circuit

When the canonical OpenTelemetry env var `OTEL_SDK_DISABLED` is set to `"true"` (case-insensitive), BOTH `AddD2Telemetry` and `MapD2PrometheusEndpoint` short-circuit:

- `AddD2Telemetry` returns the unmutated `services` collection — no providers / exporters / instrumentations registered.
- `MapD2PrometheusEndpoint` returns the unmutated `endpoints` builder — no `/metrics` route mapped.

Consumers MUST NOT rely on `MeterProvider` / `TracerProvider` resolution from DI after `AddD2Telemetry` returns when the env var is set. Used in E2E tests where the OTel SDK is undesired (the SDK's per-process singleton state otherwise contaminates tests that build multiple hosts in sequence).

### Configuration — `D2TelemetryOptions`

| Property | Type | Default | Notes |
|---|---|---|---|
| `ServiceName` | `string?` | `OTEL_SERVICE_NAME` config → `IHostEnvironment.ApplicationName` | `service.name` resource attribute on every span / metric / log record. Validated non-empty / non-whitespace at startup. |
| `OtlpTracesEndpoint` | `string?` | `OTEL_EXPORTER_OTLP_TRACES_ENDPOINT` config | Falsey value suppresses traces OTLP exporter; spans still emit to in-process listeners. URI-shape validated when truthy. |
| `OtlpMetricsEndpoint` | `string?` | `OTEL_EXPORTER_OTLP_METRICS_ENDPOINT` config | Falsey value suppresses metrics OTLP exporter; Prometheus scraping remains active when enabled. URI-shape validated when truthy. |
| `OtlpLogsEndpoint` | `string?` | `OTEL_EXPORTER_OTLP_LOGS_ENDPOINT` config | Falsey value suppresses logs OTLP exporter; logs still flow through other MEL providers. URI-shape validated when truthy. |
| `InstrumentationExcludedPaths` | `IReadOnlyList<string>` | `["/health", "/alive", "/metrics", "/.well-known"]` | Path prefixes excluded from AspNetCore-instrumentation auto-spans (matches `D2.Shared.Logging`'s default infrastructure-path set so spans and logs stay aligned). Validated non-empty (collection) and per-entry non-empty / non-whitespace at startup. |
| `AdditionalActivitySources` | `IReadOnlyList<string>` | `[]` | Names of additional `ActivitySource`s registered with the tracer provider on top of the standard aggregation set. Validated per-entry non-empty when populated. |
| `AdditionalMeters` | `IReadOnlyList<string>` | `[]` | Names of additional `Meter`s registered with the meter provider on top of the standard aggregation set. Same shape. |
| `EnableAspNetCoreInstrumentation` | `bool` | `true` | Inbound HTTP request spans + metrics. |
| `EnableHttpClientInstrumentation` | `bool` | `true` | Outbound HttpClient spans + metrics. Self-referential filter suppresses spans for outbound calls to the configured OTLP exporter endpoints (prevents infinite-loop instrumentation). |
| `EnableGrpcNetClientInstrumentation` | `bool` | `true` | Outbound gRPC client spans. |
| `EnableProcessInstrumentation` | `bool` | `true` | Process metrics (CPU, memory, fd count). |
| `EnableRuntimeInstrumentation` | `bool` | `true` | .NET runtime metrics (GC, threadpool, JIT). |
| `EnablePrometheusExporter` | `bool` | `true` | In-process Prometheus exporter; off when host wants OTLP-only metrics push. |

### Constants

| Constant | Value |
|---|---|
| `D2TelemetryConstants.OTEL_SERVICE_NAME_CONFIG_KEY` | `"OTEL_SERVICE_NAME"` |
| `D2TelemetryConstants.OTLP_TRACES_ENDPOINT_CONFIG_KEY` | `"OTEL_EXPORTER_OTLP_TRACES_ENDPOINT"` |
| `D2TelemetryConstants.OTLP_METRICS_ENDPOINT_CONFIG_KEY` | `"OTEL_EXPORTER_OTLP_METRICS_ENDPOINT"` |
| `D2TelemetryConstants.OTLP_LOGS_ENDPOINT_CONFIG_KEY` | `"OTEL_EXPORTER_OTLP_LOGS_ENDPOINT"` |
| `D2TelemetryConstants.OTEL_SDK_DISABLED_ENV_VAR` | `"OTEL_SDK_DISABLED"` |
| `D2TelemetryConstants.PROMETHEUS_ENDPOINT_PATH` | `"/metrics"` |
| `D2TelemetryConstants.HEALTH_ENDPOINT_PATH` | `"/health"` |
| `D2TelemetryConstants.ALIVE_ENDPOINT_PATH` | `"/alive"` |
| `D2TelemetryConstants.WELL_KNOWN_ENDPOINT_PATH` | `"/.well-known"` |

The `OTEL_SERVICE_NAME_CONFIG_KEY` value matches `D2.Shared.Logging`'s constant of the same name — both reference the OpenTelemetry-canonical env var so log + trace + metric service names stay aligned. Re-declared here rather than depending on `D2.Shared.Logging` (the two libs are independent).

## Telemetry surface coverage

`AddD2Telemetry` aggregates every shared lib that publishes its `ActivitySource` / `Meter` name through a `public const string` so a single call wires the entire shared-lib telemetry surface into the configured exporters.

### `ActivitySource` aggregation (4 sources — tracer provider)

| Lib | Source name | Const reference |
|---|---|---|
| `D2.Shared.Handler` | `D2.Shared.Handler` | `HandlerTelemetry.SourceName` |
| `D2.Shared.Auth` | `D2.Shared.Auth` | `AuthTelemetry.ACTIVITY_SOURCE_NAME` |
| `D2.Shared.Auth.Outbound` | `D2.Shared.Auth.Outbound` | `OutboundTelemetry.ACTIVITY_SOURCE_NAME` |
| `D2.Shared.Messaging.RabbitMq` | `D2.Shared.Messaging.RabbitMq` | `MessagingTelemetry.SOURCE_NAME` |

### `Meter` aggregation (6 meters — meter provider)

| Lib | Meter name | Const reference |
|---|---|---|
| `D2.Shared.Handler` | `D2.Shared.Handler` | `HandlerTelemetry.SourceName` |
| `D2.Shared.Auth` | `D2.Shared.Auth` | `AuthTelemetry.METER_NAME` |
| `D2.Shared.Auth.Outbound` | `D2.Shared.Auth.Outbound` | `OutboundTelemetry.METER_NAME` |
| `D2.Shared.Messaging.RabbitMq` | `D2.Shared.Messaging.RabbitMq` | `MessagingTelemetry.SOURCE_NAME` |
| `D2.Shared.Caching.Distributed.Redis` | `D2.Shared.Caching.Distributed.Redis` | `RedisCacheTelemetry.METER_NAME` |
| `D2.Shared.Caching.Local` | `D2.Shared.Caching.Local` | `LocalCacheTelemetry.METER_NAME` |

The two cache libs publish counters only — no spans — so they appear in the meter list but not the activity-source list. Each cross-lib reference uses the source-of-truth `const string` symbol so a rename in any owning lib surfaces as a build break here at compile time. Spec-pinning unit tests in `AggregatedTelemetrySourcesTests` and `D2TelemetryConstantsTests` additionally pin the literal wire values so an in-place value change (without a symbol rename) surfaces as a test failure — operators querying Loki / Tempo / Prometheus by literal source name (`service="D2.Shared.Auth"`) are protected from silent wire-format drift.

### Auto-instrumentations

| Instrumentation | Default | Notes |
|---|---|---|
| AspNetCore inbound | enabled | Filter callback excludes `/health`, `/alive`, `/metrics`, `/.well-known` (configurable via `InstrumentationExcludedPaths`). `RecordException = true` captures exception type + stack trace as span events (no PII — diagnostic-class data). |
| HttpClient outbound | enabled | `FilterHttpRequestMessage` callback suppresses spans for outbound calls whose URI starts with any configured OTLP endpoint (prevents infinite-loop instrumentation). `EnrichWithHttpRequestMessage` re-sets `url.full` to scheme + host + port + path (no query string) as defense-in-depth against future SDK regressions on query-string PII. |
| GrpcNetClient outbound | enabled | Standard span tags. |
| Process metrics | enabled | CPU, memory, fd count. |
| Runtime metrics | enabled | GC, threadpool, JIT. |
| Prometheus exporter | enabled | In-process scraping endpoint mapped via `MapD2PrometheusEndpoint`. |

## Dependencies

| Package | Why |
|---|---|
| `OpenTelemetry.Api` | Pinned at `1.15.3` — surgical override per GHSA-g94r-2vxg-569j on the 1.15.0 line. Direct ref required to override the transitive 1.15.0 pulled by Instrumentation.* packages. |
| `OpenTelemetry` | Core SDK (`OpenTelemetryBuilder`, `BatchExportProcessor`, etc.). |
| `OpenTelemetry.Extensions.Hosting` | `services.AddOpenTelemetry()` extension + `WithTracing` / `WithMetrics` overloads. |
| `OpenTelemetry.Exporter.OpenTelemetryProtocol` | Pinned at `1.15.3` — surgical override per GHSA-mr8r-92fq-pj8p + GHSA-q834-8qmm-v933. OTLP HTTP/Protobuf exporter for traces / metrics / logs. |
| `OpenTelemetry.Exporter.Prometheus.AspNetCore` | Pinned at `1.15.0-beta.1` — long-running beta in OTel .NET; >18mo of v1 production stability supports the pin. Required for `MapPrometheusScrapingEndpoint`. |
| `OpenTelemetry.Instrumentation.AspNetCore` | Inbound HTTP request spans + metrics. |
| `OpenTelemetry.Instrumentation.GrpcNetClient` | Pinned at `1.15.0-beta.1`. Outbound gRPC client spans. |
| `OpenTelemetry.Instrumentation.Http` | Outbound HttpClient spans + metrics. |
| `OpenTelemetry.Instrumentation.Process` | Pinned at `1.15.0-beta.1`. Process metrics. |
| `OpenTelemetry.Instrumentation.Runtime` | .NET runtime metrics. |
| `JetBrains.Annotations` | `[MustDisposeResource]` annotations on disposable factory paths (none currently; consumed transitively). |

| Project reference | Why |
|---|---|
| `D2.Shared.Utilities` | `Falsey()` / `Truthy()` / `ToNullIfEmpty()` extensions for options validation + env-var resolution. |
| `D2.Shared.AspNetCore` | Canonical `InfrastructurePathMatcher` (consumed by the AspNetCore-instrumentation `Filter` callback) + `D2AspNetCoreConstants` (re-exported HEALTH / ALIVE / METRICS / WELL_KNOWN endpoint path constants + `DEFAULT_INFRASTRUCTURE_PATHS`). Single source of truth shared with `D2.Shared.Logging`'s request-logging middleware. |
| `D2.Shared.Handler` | `HandlerTelemetry.SourceName` const referenced by the aggregation table. |
| `D2.Shared.Auth` | `AuthTelemetry.ACTIVITY_SOURCE_NAME` / `METER_NAME` consts. |
| `D2.Shared.Auth.Outbound` | `OutboundTelemetry.ACTIVITY_SOURCE_NAME` / `METER_NAME` consts. |
| `D2.Shared.Messaging.RabbitMq` | `MessagingTelemetry.SOURCE_NAME` const. |
| `D2.Shared.Caching.Distributed.Redis` | `RedisCacheTelemetry.METER_NAME` const. |
| `D2.Shared.Caching.Local.Default` | `LocalCacheTelemetry.METER_NAME` const. |

## File layout

| File | Role |
|---|---|
| `D2.Shared.Telemetry.csproj` | csproj — `Microsoft.NET.Sdk.Web` + `OutputType=Library`. Project references to the seven aggregated libs + `Utilities`. |
| `D2TelemetryOptions.cs` | Sealed record — Options-pattern config. |
| `D2TelemetryConstants.cs` | Public constants (config keys, env-var names, infrastructure endpoint paths). |
| `TelemetryServiceCollectionExtensions.cs` | Public DI extension: `AddD2Telemetry`. |
| `WebApplicationTelemetryExtensions.cs` | Public AspNetCore extension: `MapD2PrometheusEndpoint`. |
| `Internal/InternalIpFilter.cs` | Internal RFC 1918 + loopback predicate for the Prometheus endpoint filter. |
| `Internal/OtelSdkDisabledGate.cs` | Internal env-var gate predicate consumed by both `AddD2Telemetry` and `MapD2PrometheusEndpoint`. |
| `Internal/AggregatedTelemetrySources.cs` | Internal source-of-truth list of `ActivitySource` / `Meter` names registered by `AddD2Telemetry`. |

## Edge cases / gotchas

- **OTel SDK builds singleton `MeterProvider` / `TracerProvider` per process.** Tests that build multiple hosts in sequence see SDK static state contamination across host lifetimes. The lib's integration tests pin the contract via `[Collection("OtelStaticState")]` to serialize against any other test that touches the SDK.
- **Infrastructure-path matching lives in `D2.Shared.AspNetCore`.** The AspNetCore-instrumentation `Filter` callback consumes the public `D2.Shared.AspNetCore.InfrastructurePathMatcher`; `D2.Shared.Logging`'s request-logging middleware consumes the same one. The path set (`/health`, `/alive`, `/metrics`, `/.well-known`) stays aligned across the two consumers without per-lib literal duplication.
- **Cache libs (`Redis`, `Local`) expose only their `Meter` — no spans.** The libs publish aggregate counters (hits, misses, sets, evictions) without per-call spans because cache work is sub-microsecond and per-call instrumentation would dominate it. The aggregation table reflects this — meter list includes both cache libs; activity-source list does not.
- **`MessagingTelemetry` class visibility was promoted from `internal static` to `public static` so its `SOURCE_NAME` const is reachable cross-assembly.** The const itself was already `public`; promoting the class adds no new public surface beyond what the const already contributed. Same promotion applies to `RedisCacheTelemetry` and `LocalCacheTelemetry`.
- **Per-signal OTLP exporter is registered ONLY when its endpoint env var is truthy.** When all three are absent, the SDK still builds — spans emit to in-process listeners; metrics flow to the Prometheus endpoint when enabled; logs flow through other MEL providers. This is the v1 fail-soft behavior: production sets all three; dev / test omits any combination cleanly.
- **HttpClient instrumentation's self-referential filter prevents infinite-loop instrumentation.** Outbound calls whose URI starts with any configured OTLP endpoint are suppressed from span emission (else `HttpClient → OTLP → HttpClient → OTLP` loops forever).
- **`url.full` query-string stripping is defense-in-depth.** The OTel 1.15.x HttpClient instrumentation default already strips query strings from the standard span tags, but the `EnrichWithHttpRequestMessage` callback explicitly re-sets `url.full` to scheme + host + port + path so a future SDK regression doesn't silently leak query-string PII.
- **OTLP collector unavailable → SDK retries internally + drops on full buffer.** The OTel SDK's `BatchExportProcessor` owns retry / drop semantics; the lib does not add another layer. Operators notice unavailability via missing data in dashboards (the SDK's own internal diagnostics + `EventListener` against `OpenTelemetry.EventSource` provide drop-rate visibility for operators who wire it).
