<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# @dcsv-io/d2-telemetry

> Parent: [`public/packages/typescript/`](../README.md)

One-call OTel SDK bootstrap for Node services. Wires traces, metrics, logs
OTLP exporters + W3C propagator stack. Mirrors `DcsvIo.D2.Telemetry` (.NET).

## Public API

| Export                 | Purpose                                                                            |
| ---------------------- | ---------------------------------------------------------------------------------- |
| `TelemetryOptions`     | `serviceName` + optional `environment` / per-signal OTLP endpoints / `disabled`.   |
| `TelemetryHandle`      | Result handle: `shutdown(): Promise<void>` + `disabled: boolean`.                  |
| `setupTelemetry(opts)` | Bootstraps the OTel SDK; honors `disabled` for tests + `OTEL_SDK_DISABLED` parity. |
| `buildPropagators()`   | Returns the W3C trace-context + baggage composite propagator.                      |

## Dependencies

- `@dcsv-io/d2-utilities` (boundary helpers — dependency boundary for env-var
  configuration helpers)
- `@dcsv-io/d2-logging` (dependency boundary for telemetry-side log enrichment;
  the integration itself is out of scope for this package)
- `@opentelemetry/api`, `@opentelemetry/sdk-node`, OTLP exporters
  (traces / metrics / logs over HTTP), `@opentelemetry/resources`,
  `@opentelemetry/semantic-conventions`

## Usage example

```ts
import { setupTelemetry } from "@dcsv-io/d2-telemetry";

const handle = setupTelemetry({
  serviceName: "my-svc",
  environment: process.env.NODE_ENV,
  otlpTracesEndpoint: process.env.OTEL_EXPORTER_OTLP_TRACES_ENDPOINT,
});

// On shutdown:
process.on("SIGTERM", async () => {
  await handle.shutdown();
});
```

## Parity with .NET

Mirrors `DcsvIo.D2.Telemetry`:

- `setupTelemetry` ↔ `services.AddD2Telemetry(options)`.
- `buildPropagators` ↔ `Propagators.SetDefault(...)`.
- `TelemetryHandle.shutdown` ↔ `IServiceProvider.GetRequiredService<TracerProvider>().ShutdownAsync()`.
- `TelemetryOptions.disabled` ↔ `OTEL_SDK_DISABLED` env var short-circuit.

## Edge cases

- `disabled=true` returns a no-op handle — `shutdown()` resolves immediately.
- Missing per-signal endpoint URL → exporter falls back to OTLP env-var
  defaults (`OTEL_EXPORTER_OTLP_*_ENDPOINT`).
- Multiple `setupTelemetry` calls in one process are unsupported by the
  underlying SDK — call once at process boot.
- `shutdown()` is idempotent at the SDK level — multiple calls return
  resolved promises after the first flushes.
