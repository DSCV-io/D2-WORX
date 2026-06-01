<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Shared.Utilities

> Parent: [`server/shared/dotnet/`](../README.md)

Foundational helpers used at every boundary across D²-WORX. The "no value too small to centralize" library — preventing whole classes of bugs (empty-string-as-data, env-var collisions, JSON cycles) from ever entering domain code.

Runtime dependencies are kept minimal so this lib stays domain-safe:

- `dotenv.net` — only loaded when `D2Env.Load()` is called
- `JetBrains.Annotations` — compile-time markers
- `D2.Shared.Result` + `D2.Shared.I18n.Abstractions` — both zero-runtime-dep themselves; pulled in so `TryParseEmail` / `TryParsePhoneNumber` can return `D2Result<string>` with `TK.*`-keyed messages

Consumed by every other shared lib + service.

---

## Sub-folder index

The public API is grouped by concern. Each sub-folder ships its own README with the per-helper signatures, examples, and gotchas; this top-level page is the routing table.

| Sub-folder                                  | Covers                                                                                                                                                                                                                                                                                                           |
| ------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| [`Attributes/`](Attributes/README.md)       | `[RedactData]` marker attribute consumed by the Serilog destructuring policy.                                                                                                                                                                                                                                    |
| [`Configuration/`](Configuration/README.md) | `ConnectionStringHelper` (URI ↔ wire-format conversion for Redis / Postgres / RabbitMQ) + `D2Env` (host-side `.env*` file loader).                                                                                                                                                                               |
| [`Diagnostics/`](Diagnostics/README.md)     | `SanitizedExceptionRender` — PII-safe exception rendering for log + telemetry + DLQ-header surfaces.                                                                                                                                                                                                             |
| [`Enums/`](Enums/README.md)                 | `IsolationLevel` (SQL transaction isolation taxonomy) + `RedactReason` (closed enum used by `[RedactData]`).                                                                                                                                                                                                     |
| [`Extensions/`](Extensions/README.md)       | The most-used surface: `Truthy()` / `Falsey()` / `ToNullIfEmpty()` boundary checks; `ThrowIfFalsey()` required-argument guards (BCL-split null vs falsey); `TryParseTruthyNull` Guid + enum parsers; `CleanStr` / `CleanDisplayStr` display cleaners; `TryParseEmail` / `TryParsePhoneNumber` `D2Result`-returning validators; `GetNormalizedStrForHashing`; `Clean()` for enumerables. |
| [`Serialization/`](Serialization/README.md) | `SerializerOptions` — frozen `JsonSerializerOptions` presets (`SR_IgnoreCycles`, `SR_Web`, `SR_WebIgnoreNull`).                                                                                                                                                                                                  |

---

## Tests

`server/shared/dotnet/tests/Unit/Utilities/` — adversarial coverage across every public surface. Categories:

- All `Truthy`/`Falsey` overloads — null, empty, whitespace-only, multi-element, boundary values.
- `ToNullIfEmpty` — null/empty/whitespace/trim/identity paths.
- `CleanStr` / `CleanDisplayStr` — Unicode multi-script preservation, allowed-char retention, all-stripped → null.
- `TryParseEmail` / `TryParsePhoneNumber` — happy paths + every documented failure condition (null, empty, whitespace, no `@`, no dot, double `@`, embedded space, length out of bounds, non-digit input, etc.). Asserts `D2Result.IsValidationFailed` and the carried `TK.Common.Validation.EMAIL_INVALID` / `PHONE_INVALID` key.
- `GuidExtensions.TryParseTruthyNull` — happy path + null/empty/whitespace/`Guid.Empty`-string/malformed/oversized adversarial coverage; verifies the null-out-Guid? semantic.
- `EnumExtensions.TryParseTruthyNull<TEnum>` — case-insensitive matching, comma-separated `[Flags]` syntax, numeric-string pass-through (documents the BCL "no `Enum.IsDefined`" behavior), null/empty/whitespace/unknown-name adversarial coverage.
- `GetNormalizedStrForHashing` — empty array, all-falsey, mixed, position-preservation property.
- `EnumerableExtensions.Clean` — every (3 × 2) combination of empty-behavior and value-null-behavior, plus generator-backed single-enumeration property.
- `RedactDataAttribute` — defaults, init-only setters, `AttributeUsage` target, reflective attribute discovery.
- `SerializerOptions` — camelCase, string-enums, null preservation/omission, cycle tolerance.
- `ConnectionStringHelper` — pass-through cases, URI parsing for both Redis and Postgres, default ports, URL-encoded credentials, env-var resolution including missing-var throws (with collection-isolated env-mutating tests).
- `D2Env` — `ApplyVars` precedence (process-env wins, later files override earlier), file discovery ("first dir with any match wins"), depth-limit exhaustion, platform comparer test seam, `Load()` idempotency. Discovery tests use explicit non-existent file names to avoid loading the repo's real `.env.secrets` into the test process.
- `Diagnostics/SanitizedExceptionRender` — type-name fallback, first-frame format, never-thrown sentinel, anti-leak invariants on both `TypeName` and `FirstFrame` (rendered strings never contain `Exception.Message` even with sensitive bait values), thrown-exception frame identification, `BrokerUnreachableException`-shaped adversarial coverage (AMQP URI password leak prevention), empty-stack-trace edge case.

Run: `dotnet test server/shared/dotnet/tests`

CLI coverage one-liner (writes a Cobertura XML; coverlet.console's stdout summary shows totals):

```bash
cd server/shared/dotnet/tests
coverlet bin/Debug/net10.0/D2.Shared.Tests.dll \
  --target dotnet --targetargs "test --no-build" \
  --include "[D2.Shared.Utilities]*" \
  --exclude-by-attribute "GeneratedCode" \
  --format cobertura --output ./coverage/utilities.cobertura.xml
```
