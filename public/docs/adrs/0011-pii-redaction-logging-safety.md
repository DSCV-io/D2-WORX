<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->


> **Visibility: PUBLIC** — ships with the open surface (`public/`).  
> Do not add product IP, private paths, or non-exportable runbooks.
# ADR-0011: PII redaction and logging safety — `[RedactData]` + destructuring policy + `SanitizedExceptionRender` + explicit LOG-OK / NOT-LOGGED field split

- **Status**: Accepted
- **Date**: 2026-05-30 (service-identity-removal re-verify note: 2026-06-18)
- **Deliverable**: D2 shared libraries (backfilled)

## Context

D2 services log structured events using Serilog (.NET) and Pino (TypeScript). Both runtimes receive objects that frequently carry PII: email addresses, phone numbers, IP addresses, sub-country geographic precision, names, message bodies, file names, presigned URLs with credentials in query parameters, and AMQP/DB connection strings that embed passwords. Left unguarded, any of these can leak into log aggregation (Loki), operator dashboards, or off-cluster forwarding.

Three concrete leak vectors drove the decisions below:

1. **Structured object capture.** `logger.LogInformation("{@User}", user)` serializes every public property of `User` into structured JSON; without active redaction, PII reaches the sink verbatim.
2. **Exception messages.** Broker and database libraries embed runtime secrets in `Exception.Message`: RabbitMQ.Client's `BrokerUnreachableException.Message` contains the full AMQP URI including password; StackExchange.Redis includes connection-string fragments; Npgsql includes constraint-violation row excerpts; subscriber-isolation `catch` blocks capture arbitrary user-handler exceptions. Passing an `Exception` to a `[LoggerMessage]` delegate causes the pipeline to call `ex.ToString()`, which includes `ex.Message`.
3. **Request-context over-projection.** The spec-driven `IRequestContext` (ADR-0007) contains 50+ fields; projecting all of them onto every request-completion line would emit raw client IPs, city/postal/subdivision coordinates, and GPS coordinates — all PII — without per-call-site review.

The codebase spans two runtimes; a redaction decision expressed only in .NET leaves TypeScript-side Pino logging unprotected unless the same intent is re-expressed there, preferably from the same authoritative source.

## Decision

**1. `[RedactData]` attribute + `RedactDataDestructuringPolicy`: structural, type-driven PII redaction.** A `[RedactData]` marker (`public/packages/dotnet/utilities/Attributes/RedactDataAttribute.cs`), carrying a `RedactReason` enum and optional `CustomReason`, is placed on any type or public property carrying PII. `RedactDataDestructuringPolicy` (`public/packages/dotnet/logging/Destructuring/RedactDataDestructuringPolicy.cs`), registered as a Serilog `IDestructuringPolicy` by `AddD2Logging`, reflects over each `@`-captured object once (caching the result in a `ConcurrentDictionary<Type, TypeRedactionInfo>`): a type carrying `[RedactData]` is replaced wholesale with `[REDACTED: {Reason}]`; specific annotated properties are masked and siblings recursively destructured. Two documented limitations: the policy covers public instance **properties** only (field-level `[RedactData]` is silently ignored), and capture without `@` (e.g. `{User}`) calls `.ToString()` and bypasses destructuring — both enforced by `rules.md §3.3` call-site discipline.

**2. No `Exception` parameter in any `[LoggerMessage]` delegate; use `SanitizedExceptionRender` instead.** `SanitizedExceptionRender` (`public/packages/dotnet/utilities/Diagnostics/SanitizedExceptionRender.cs`) exposes `TypeName(ex)` (the exception type's `FullName`) and `FirstFrame(ex)` (`"{Method} at {File}:{Line}"`) — method names and source paths are developer-controlled, so both are safe to log. Every `[LoggerMessage]` declaration across every surface (`BaseHandlerLog`, `AuthLog`, outbound auth, RabbitMQ subscriber channels) accepts `string exceptionType` + `string firstFrame`, never `Exception`; call sites pass a sanitized exception-type string (`SanitizedExceptionRender.TypeName(ex)`, or `ex.GetType().Name` as `BaseHandler` does) — never the raw `Exception` or `ex.Message`. The contract is pinned by reflection-based tests (e.g. `HandlerLogDelegateContractTests` enumerates every `BaseHandlerLog` method and asserts none has a parameter assignable from `Exception`); `rules.md §3.1`/`§3.2` encode it as mandatory audit predicates. When the service-identity components of `DcsvIo.D2.Auth.Outbound` are removed (superseded by mTLS workload identity per [ADR-0023](0023-mtls-workload-identity.md), a later deliverable), the enumerated surface list above is re-verified against the trimmed lib; the token-exchange half of `Auth.Outbound` and its outbound-auth `[LoggerMessage]` logging are retained, so the outbound-auth surface stays in the list. The no-`Exception`-parameter rule itself is unaffected — it binds every `[LoggerMessage]` surface regardless of which surfaces exist.

**3. Explicit LOG-OK allowlist + NOT-LOGGED suppression for `IRequestContext`, pinned by integration test.** `D2RequestContextEnricher` (`public/packages/dotnet/logging/Internal/D2RequestContextEnricher.cs`) projects exactly 42 named LOG-OK fields onto the Serilog diagnostic context (each null-gated; collection fields additionally `Truthy()`-gated). Seven fields are NEVER emitted regardless of population: `ClientIp` (raw IP is PII), `City`/`SubdivisionIso31662Code`/`PostalCode` (sub-country precision), `Latitude`/`Longitude`/`Geohash` (GPS-exact). Country grain (`CountryIso31661Alpha2Code`) is LOG-OK; sub-country precision is not (available to operators via an authenticated, auditable `WhoIsHashId` lookup instead). The NOT-LOGGED contract is pinned by an integration test that populates all seven suppressed fields in a full `TestHost` and asserts none appears in the rendered output; a positive test pins that all 42 LOG-OK fields appear when populated.

**4. Cross-language redaction intent is spec-driven from the same source.** On the TypeScript side, `sanitizedErrorRender` (`public/packages/typescript/logging/src/sanitized-error-render.ts`) mirrors `SanitizedExceptionRender` and never exposes `.message`; `@dcsv-io/d2-logging`'s `ILogger` forbids passing `Error` objects directly. For data-type redaction, the spec-driven codegen decision (ADR-0002) generates `<TypeName>RedactPaths` constants (e.g. `IRequestContextRedactPaths`) that consumers pass to `setupLogger` as Pino `redactPaths`. The source of truth for which context fields carry `redact: true` is the same spec that drives both the .NET codegen and the TS constant — so .NET's `[RedactData]` posture and TS's Pino redaction derive from the same annotation, not independent per-runtime curation.

## Consequences

**Positive.**

- PII redaction is enforced structurally at the type level: annotate a record once, and protection applies across every service, call site, and future caller for `@`-captured annotated types.
- Exception-message leaks are blocked at the type-system boundary; reflection contract tests catch any future regression immediately, structurally preventing broker-credential and user-input leaks.
- The LOG-OK / NOT-LOGGED split is reviewed once, pinned by integration tests; drift surfaces at CI.
- Cross-language alignment: .NET attribute redaction and TS `redactPaths` derive from the same spec annotation.
- Reflection results are cached per type — no repeated reflection cost in production.

**Negative / risks.**

- **Capture-mode footgun is not mechanically prevented**: `{User}` (no `@`) bypasses destructuring; a `[RedactData]` property whose type overrides `ToString()` to include it will leak. Enforced by `rules.md §3.3` audits, not the compiler.
- **Field-only `[RedactData]` is silently ignored** (properties only) — no compile-time warning.
- **`[RedactData]` cannot apply to proto-generated DTOs** — the compensating control is `DefaultOptions.LogInput/LogOutput=false` on the handler (per-handler opt-out rather than per-type opt-in).
- **`IDiagnosticContext.Set` calls bypass destructuring** — callers adding structured fields via the request-logging extension own their own PII discipline.
- **The NOT-LOGGED test is only as complete as its field enumeration** — a new PII `IRequestContext` field not added to the assertion list is not caught; mitigated by the spec-driven codegen review gate and the `rules.md §3` audit.

## Alternatives considered

**Manual per-call-site scrubbing.** Developers decide per `logger.Log*` call which fields to include. Does not scale across the team or future contributors, requires per-call-site rather than per-type correctness, and a single missed `{@ctx}` sends the full context to the sink. Rejected: one miss is a PII incident.

**Log `ex.Message` and rely on log-pipeline scrubbing.** Regex secret-detection at ingest has a well-known false-negative profile (unusual AMQP URIs, non-standard credential encoding, novel user-input patterns) and does not help logs already exported. Credentials must be excluded before reaching the sink, not after.

**Log all `IRequestContext` fields and suppress at query time.** The PII is still in the pipeline and at-rest in the index regardless of query-time filtering. Data-minimization requires PII not reach the index in the first place.

## References

- `public/packages/dotnet/utilities/Attributes/RedactDataAttribute.cs`; `public/packages/dotnet/utilities/Diagnostics/SanitizedExceptionRender.cs`.
- `public/packages/dotnet/logging/Destructuring/RedactDataDestructuringPolicy.cs` + `TypeRedactionInfo.cs`; `public/packages/dotnet/logging/Internal/D2RequestContextEnricher.cs`; `public/packages/dotnet/logging/README.md` (LOG-OK / NOT-LOGGED tables).
- `public/packages/dotnet/handler/core/BaseHandler.Logging.cs` (no-`Exception` delegates); `public/packages/dotnet/tests/Unit/Handler/HandlerLogDelegateContractTests.cs`; `public/packages/dotnet/tests/Integration/Logging/RequestContextEnricherIntegrationTests.cs`.
- `public/packages/typescript/logging/src/sanitized-error-render.ts` + README.
- `docs/PATTERNS.md` (Logging / PII sections); `docs/dev/rules.md §3` (`§3.1` no-`Exception`, `§3.2` no `ex.Message`, `§3.3` `[RedactData]` coverage).
- [ADR-0002](0002-spec-driven-codegen.md) — `<TypeName>RedactPaths` constants are codegen'd from the same spec annotations. [ADR-0006](0006-abstractions-implementation-split.md) — the enricher depends only on the `IRequestContext` abstraction. [ADR-0010](0010-observability-dual-enrichment.md) — PII safety of the 42 LOG-OK fields is the prerequisite for always-on enrichment.
