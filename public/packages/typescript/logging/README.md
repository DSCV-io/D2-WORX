<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# @dcsv-io/d2-logging

> Parent: [`public/packages/typescript/`](../README.md)

Pino-backed `ILogger` interface + `markRedactedFields()` PII registration
helper + `sanitizedErrorRender()` for safe error logging. Mirrors
`DcsvIo.D2.Logging` (.NET).

## Public API

| Export                                               | Purpose                                                                                                                 |
| ---------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------- |
| `ILogger` (interface)                                | `trace`/`debug`/`info`/`warn`/`error`/`fatal` per-level methods + `child(bindings)`. NEVER accepts an `Error` directly. |
| `LogBindings`                                        | Structured binding shape — plain object, no `Error` instances.                                                          |
| `LoggerOptions`                                      | `serviceName` + optional `environment` / `minLevel` / `pretty` / `redactPaths`.                                         |
| `setupLogger(opts)`                                  | Builds the Pino-backed `ILogger`. Merges `redactPaths` + the registry to feed Pino `redact`.                            |
| `markRedactedFields(symbol, paths)`                  | Registers PII paths for a hand-written type.                                                                            |
| `getRedactedFieldsFor(symbol)`                       | Returns the registered paths for one identifier.                                                                        |
| `collectAllRedactedFields()`                         | Returns all registered paths (used by `setupLogger`).                                                                   |
| `clearRedactedFieldsRegistry()`                      | Test-only registry reset.                                                                                               |
| `SanitizedErrorRender` / `sanitizedErrorRender(err)` | Safe error metadata: `{ name, firstFrame }` only — NEVER `.message`.                                                    |

## PII safety contract

The `ILogger` API forbids passing `Error` directly. `error.message` can
carry untrusted data (broker URIs with embedded credentials, user input
echoed back, error messages with PII fields). Always extract a safe shape:

```ts
catch (e) {
  log.error("rabbit publish failed", sanitizedErrorRender(e));
  // logs { name: "AmqpConnectError", firstFrame: "at publish (...)" }
  // — NOT the raw .message which might be "amqp://user:secret@host"
}
```

PII fields on data types are redacted via `markRedactedFields(typeId, paths)`
for hand-written types or via the codegen-emitted `<TypeName>RedactPaths`
constant for spec-driven types (`@dcsv-io/d2-auth-context-abstractions`,
`@dcsv-io/d2-request-context-abstractions`). `setupLogger` merges both into Pino's
`redact: { paths }` config so emitted JSON has the fields removed.

## Dependencies

- `@dcsv-io/d2-utilities` (boundary helpers)
- `@dcsv-io/d2-result` (no direct usage; dependency boundary retained for
  D2Result-aware log-helper composition)
- `pino` (runtime logger)

## Usage example

```ts
import {
  setupLogger,
  markRedactedFields,
  sanitizedErrorRender,
} from "@dcsv-io/d2-logging";

// Codegen-emitted types contribute via setupLogger redactPaths arg.
import { IRequestContextRedactPaths } from "@dcsv-io/d2-request-context-abstractions";

// Hand-written types register at module load.
const SESSION_TYPE = Symbol("Session");
markRedactedFields(SESSION_TYPE, ["sessionToken", "refreshToken"]);

const log = setupLogger({
  serviceName: "my-svc",
  environment: "prod",
  redactPaths: [IRequestContextRedactPaths],
});

log.info("user signed in", { userId: ctx.userId });
```

## Parity with .NET

Mirrors `DcsvIo.D2.Logging`:

- `ILogger` ↔ .NET `ILogger` consumer surface, with the PII-safe per-level
  shape (no `Exception` parameter — analog of the .NET
  `LeakProneLogDelegates` rule).
- `setupLogger` ↔ `services.AddD2Logging(options)`.
- `markRedactedFields` ↔ `[RedactData]` attribute (.NET hand-written
  approach; the spec-driven approach is shared via codegen-emitted
  `<TypeName>RedactPaths` constants).
- `sanitizedErrorRender` ↔ `SanitizedExceptionRender.{TypeName, FirstFrame}`.

## Edge cases

- `setupLogger` always succeeds — invalid options surface at first log call.
- Empty `redactPaths` + empty registry → Pino runs without a redact config
  (no overhead).
- Re-registering the same symbol replaces the prior paths (idempotent).
- `sanitizedErrorRender` accepts non-`Error` inputs (`null`, `undefined`,
  primitives) and returns `{ name: typeof }` (`firstFrame` omitted).
