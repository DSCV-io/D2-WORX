<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# telemetry/

> Parent: [`public/packages/dotnet/`](../README.md)

The OpenTelemetry composition root for D2 services — the runtime SDK setup (traces + metrics + logs, OTLP exporters, Prometheus endpoint, auto-instrumentations) plus the source generator that emits the per-meter telemetry-tag constants from `contracts/telemetry/telemetry.spec.json`. The runtime aggregates each owning lib's `ActivitySource` and `Meter` through compile-time symbol references so a rename in any owning lib surfaces here as a build break.

OTel-messaging tags codegen → [`messaging/otel-messaging-tags-source-gen/`](../messaging/otel-messaging-tags-source-gen/README.md).

## Packages

| Package                                   | Description                                                                                                                          |
| ----------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------- |
| [`core/`](core/README.md)                 | OpenTelemetry SDK setup — per-signal OTLP exporters, `MapD2PrometheusEndpoint`, auto-instrumentations, and the single `AddD2Telemetry()` aggregation call. |
| [`tags-source-gen/`](tags-source-gen/README.md) | Roslyn generator emitting the per-meter `*TelemetryTags.g.cs` typed-constant classes from `contracts/telemetry/telemetry.spec.json`. |
