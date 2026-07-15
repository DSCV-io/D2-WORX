<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# @dcsv-io/d2-service-defaults

> Parent: [`public/packages/typescript/`](../README.md)

One-call bundle that composes `@dcsv-io/d2-logging` + `@dcsv-io/d2-telemetry` plus the
`D2Env.load()` env loader. Mirrors `DcsvIo.D2.ServiceDefaults` (.NET) at
the composition role — narrower scope: no middleware aggregator, no auth
aggregator, no local-cache. The BFF is zero-privilege, so middleware
composition lives in the SvelteKit hook.

## Public API

| Export                                   | Purpose                                                                                                                          |
| ---------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------- |
| `ServiceDefaultsOptions`                 | `logger` (LoggerOptions) + `telemetry` (TelemetryOptions sans serviceName/environment which are inferred) + `telemetryDisabled`. |
| `ServiceDefaultsHandle`                  | Result: `{ logger, telemetry, shutdown }`.                                                                                       |
| `setupServiceDefaults(opts)`             | Composes logger + telemetry; returns shutdown for graceful flush.                                                                |
| `D2Env.load(opts?)`                      | Layered env loader — discovers `.env.secrets` / `.env.local` / `.env` upward + composes with `process.env` (env wins).           |
| `D2Env.discoverFile(startDir, fileName)` | Walks parent directories looking for `fileName`.                                                                                 |
| `D2Env.parseEnvFile(content)`            | Parses `KEY=VALUE` pairs (strips matching quotes, ignores comments + blank lines).                                               |

## Dependencies

- `@dcsv-io/d2-utilities` (boundary helpers)
- `@dcsv-io/d2-logging` (the bundle includes its `setupLogger`)
- `@dcsv-io/d2-telemetry` (the bundle includes its `setupTelemetry`)

## Usage example

```ts
import { D2Env, setupServiceDefaults } from "@dcsv-io/d2-service-defaults";

const env = D2Env.load();
const handle = setupServiceDefaults({
  logger: {
    serviceName: env["SVC_NAME"] ?? "my-svc",
    environment: env["NODE_ENV"] ?? "local",
    pretty: env["NODE_ENV"] !== "prod",
  },
  telemetry: {
    otlpTracesEndpoint: env["OTEL_EXPORTER_OTLP_TRACES_ENDPOINT"],
  },
  telemetryDisabled: env["OTEL_SDK_DISABLED"] === "true",
});

handle.logger.info("service starting");

process.on("SIGTERM", async () => {
  await handle.shutdown();
});
```

## Parity with .NET

Mirrors `DcsvIo.D2.ServiceDefaults`:

- `setupServiceDefaults` ↔ `services.AddD2ServiceDefaults(options)` —
  same composition role, narrower scope (Node-side has no DI container in
  this v2 scope).
- `D2Env.load` ↔ `D2Env.Load()` — same layered file lookup.
- `telemetryDisabled` ↔ `OTEL_SDK_DISABLED` short-circuit.

## Edge cases

- `D2Env.load()` with no opts uses `process.cwd()` + the standard
  `.env.secrets` / `.env.local` / `.env` layering.
- File-set values are overridden by real env vars (env wins) — matches
  conventional dotenv semantics.
- `parseEnvFile` ignores comments (`# ...`), blank lines, and lines
  without `=` (or with leading `=`).
- `setupServiceDefaults` does NOT register signal handlers — the caller
  owns shutdown sequencing.
