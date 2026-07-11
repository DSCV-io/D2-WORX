<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Shared.Logging

> Parent: [`server/shared/dotnet/`](../README.md)

Serilog configuration + the `[RedactData]` enforcement layer + an ASP.NET Core request-logging middleware. Foundation lib that other shared libs and per-service composition roots call to wire the Serilog pipeline + the `[RedactData]` destructuring policy + the request-logging middleware (services call `AddD2Logging` instead of duplicating ~30 lines of `LoggerConfiguration` boilerplate per service). Also usable standalone by any host that wants the same Serilog setup without an aggregator.

The lib does NOT own:

- OpenTelemetry SDK setup — out of scope. Logs reach OTLP collectors via the MEL pipeline (`writeToProviders: true` routes Serilog output to other registered `ILoggerProvider`s); the OTLP log-exporter wiring is owned by separate observability infrastructure.
- `[LoggerMessage]` source-generated delegates — this lib bootstraps the log pipeline itself; logging inside its own bootstrap path would be circular.
- Activity / span enrichment (e.g. `Serilog.Enrichers.Span`) — out of scope; observability infrastructure owns activity / span emission separately.

## Public API surface

### Composition

```csharp
services.AddD2Logging(configuration, opts =>
{
    opts.ServiceName = "edge";       // optional — defaults from OTEL_SERVICE_NAME → IHostEnvironment.ApplicationName
    opts.Environment = "Production"; // optional — defaults from IHostEnvironment.EnvironmentName
    opts.MinimumLevel = LogEventLevel.Information; // optional — default Information
});

// later, inside the middleware pipeline:
app.UseRouting();
app.UseD2RequestLogging(); // optional configure callback
app.UseEndpoints(...);
```

`AddD2Logging(IConfiguration, Action<D2LoggingOptions>?)` builds the Serilog `LoggerConfiguration`, sets `Log.Logger`, and wires the MEL bridge (`AddSerilog(preserveStaticLogger: true, writeToProviders: true)`). Validates options at the first `IOptions<D2LoggingOptions>.Value` resolution (typically host-startup composition) via `ValidateOnStart()` — fail-fast on invalid config.

`UseD2RequestLogging(Action<RequestLoggingOptions>?)` wraps Serilog's `UseSerilogRequestLogging` with the D² defaults: per-source minimum-level overrides, infrastructure-path suppression (logged at `Verbose` so the default minimum-level gate filters them out), conservative diagnostic-context enrichment, and projection of the spec-driven `IRequestContext` LOG-OK fields onto the request-completion event.

### Configuration — `D2LoggingOptions`

| Property              | Type                    | Default                                                         | Notes                                                                                                                                                                                                                       |
| --------------------- | ----------------------- | --------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `ServiceName`         | `string?`               | `OTEL_SERVICE_NAME` config → `IHostEnvironment.ApplicationName` | Emitted on every log line via the `service_name` structured property. Validated non-empty / non-whitespace at startup.                                                                                                      |
| `Environment`         | `string?`               | `IHostEnvironment.EnvironmentName`                              | Emitted on every log line via the `environment` structured property. Validated non-empty / non-whitespace at startup.                                                                                                       |
| `MinimumLevel`        | `LogEventLevel`         | `Information`                                                   | Default minimum log level. Per-source overrides applied independently — see "Per-source overrides" below.                                                                                                                   |
| `InfrastructurePaths` | `IReadOnlyList<string>` | `["/health", "/alive", "/metrics", "/.well-known"]`             | Path prefixes whose request-completion events emit at `Verbose` instead of `Information`, so the default level gate filters them out. Validated non-empty (collection) and per-entry non-empty / non-whitespace at startup. |

### Constants

`D2LoggingConstants.OTEL_SERVICE_NAME_CONFIG_KEY` — the `"OTEL_SERVICE_NAME"` configuration key consulted for the `ServiceName` default. Matches the OpenTelemetry-canonical convention so log + trace + metric service names stay aligned across the log + trace + metric pipelines without this lib taking an OpenTelemetry SDK dependency itself.

### Per-source overrides

`AddD2Logging` applies these per-source minimum-level overrides on top of the configured `MinimumLevel`:

| Source                      | Override  |
| --------------------------- | --------- |
| `Microsoft.AspNetCore`      | `Warning` |
| `Microsoft.Extensions.Http` | `Warning` |
| `System.Net.Http`           | `Warning` |
| `D2`                        | `Debug`   |

Suppresses the framework / HTTP-client noise that Information-level produces by default while keeping D² code's diagnostic logs visible.

### Enrichers

| Enricher                          | What it adds                                                                             |
| --------------------------------- | ---------------------------------------------------------------------------------------- |
| `FromLogContext`                  | Per-scope properties pushed via `LogContext.PushProperty` propagate to nested log calls. |
| `WithMachineName`                 | `MachineName` structured property.                                                       |
| `WithProperty("service_name", …)` | The configured `ServiceName`.                                                            |
| `WithProperty("environment", …)`  | The configured `Environment`.                                                            |

### Output sink

`Console` with `CompactJsonFormatter` (terse JSON to stdout). When other `ILoggerProvider`s are registered against `builder.Logging` (e.g. an OTLP log-exporter from observability infrastructure), Serilog routes events through both sinks via `writeToProviders: true`.

## Network/IP enrichment design

The middleware does NOT log the request's connection-remote IP address (`HttpContext.Connection.RemoteIpAddress`). Two reasons:

- **At internal services**, that address is the upstream Edge instance's IP, not the original client's. Operators reading logs would interpret it as the user's IP, which it isn't — a data-shape footgun.
- **At Edge**, the resolved client IP is PII. Logging it as a structured-context field would bypass the `[RedactData]` destructuring policy entirely (diagnostic-context `Set` writes a `ScalarValue` directly).

Geo / network-privacy / ASN / identity / impersonation / tracing fields are projected from the spec-driven `IRequestContext` onto the request-completion log line via `D2RequestContextEnricher`. The enricher is always-on; it gracefully no-ops when `IRequestContext` isn't in DI scope. Each per-field `Set` is gated by a null check; null fields are not emitted at all (vs an emitted-null structured property). Collection fields (`Audience`, `ActorChain`, `Scopes`) gate on `Truthy()` so empty collections don't pollute logs with empty arrays on every request.

### Why log identity at the request-context axis

Internal logs are read by operators who already have tenant access to query org / user metadata via authenticated APIs; emitting identity into the request-completion log line is operationally invaluable for "show me Alice's requests across this trace" debugging without any incremental disclosure beyond what those same operators can already query directly. The trade-off is intentional: every log line that fires inside the user's own access boundary already carries identity through trace + correlation IDs anyway; the enricher just makes the linkage one hop shorter.

### Precedence note — `TraceId`, `RequestId`, `RequestPath`

The three Tracing-axis fields collide with values that the request-logging middleware emits independently. The override behavior differs per field because of how each upstream value reaches the log event.

- **`TraceId` — enricher WINS when populated.** The middleware sets `TraceId` via `IDiagnosticContext.Set("TraceId", httpContext.TraceIdentifier)` early in the `EnrichDiagnosticContext` callback. The enricher runs LAST in that callback and writes via the same `IDiagnosticContext` mechanism — last-writer-wins on the same diagnostic-context dictionary. When `IRequestContext.TraceId` is populated (carrying the W3C distributed-trace id), the enricher overrides the local `HttpContext.TraceIdentifier` value. When unpopulated, the middleware's value survives. Operators reading logs should treat `TraceId` as the W3C value when `IRequestContext` is wired and as `HttpContext.TraceIdentifier` otherwise.
- **`RequestId` — Serilog WINS on the HTTP path.** Serilog 9.x's `RequestLoggingMiddleware` emits `RequestId` via its own `ForContext` binding before the `EnrichDiagnosticContext` callback runs. Once Serilog has set the property on the `LogEvent`, subsequent `IDiagnosticContext.Set("RequestId", ...)` calls are silently dropped by Serilog's `AddPropertyIfAbsent` semantics. The enricher's emission of `IRequestContext.RequestId` is included for spec-contract completeness — on transports where Serilog does NOT pre-bind `RequestId` (custom diag-ctx integrations on non-HTTP transports), the enricher's value will appear.
- **`RequestPath` — Serilog WINS on the HTTP path.** Same rationale as `RequestId`. Serilog's request-completion message template binds `RequestPath` from `HttpContext.Request.Path` via `ForContext` before the enricher runs; the enricher's emission is silently dropped on HTTP. On AMQP / messaging transports without the same template binding, the enricher's value would appear.

The two HTTP-path limitations (`RequestId` and `RequestPath`) reflect Serilog's request-logging middleware design, not a bug in the enricher. Carrying the same emission contract across all transports (HTTP + AMQP + future) is the spec-driven discipline; the limitation is documented here so operators know `RequestPath` in HTTP logs is always the local path and `RequestId` is always the local `TraceIdentifier`.

### LOG-OK fields (emitted when populated)

42 fields organized into 11 clusters. Every field is null-gated (collections gated on `Truthy()`); unpopulated fields don't reach the log line.

#### Tracing (3 fields)

| #   | Field         | Type      | Reason                                                                                                                                                                                                                                                                          |
| --- | ------------- | --------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1   | `TraceId`     | `string?` | W3C distributed-trace id propagated across hops. Overrides the middleware's local `TraceId` when populated (see precedence note above).                                                                                                                                         |
| 2   | `RequestId`   | `string?` | Per-request unique id (`HttpContext.TraceIdentifier` for HTTP; AMQP message-id for messaging). Not PII — synthetic identifier. **HTTP-path limitation**: Serilog 9.x emits `RequestId` independently; the enricher's emission is silently dropped on HTTP. See precedence note. |
| 3   | `RequestPath` | `string?` | Request path / AMQP routing key. Route templates are operational metadata, not PII. **HTTP-path limitation**: Serilog's message template pre-binds `RequestPath`; the enricher's emission is silently dropped on HTTP. See precedence note.                                     |

#### Auth/Identity (8 fields)

| #   | Field                     | Type      | Reason                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                            |
| --- | ------------------------- | --------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 4   | `IsAuthenticated`         | `bool?`   | Trinary auth state (null=pre-auth, false=anonymous, true=authenticated). Capability metadata; not PII.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                            |
| 5   | `Subject`                 | `string?` | Raw JWT `sub` claim — opaque identifier (Guid for users; client_id for service identities). Not PII per OAuth/JWT vocabulary.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                     |
| 6   | `UserId`                  | `Guid?`   | Parsed `sub` as Guid for user tokens; null for service identities. Opaque audit identifier.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       |
| 7   | `Username`                | `string?` | Login handle (lowercase, unique). Users CHOOSE their username at signup; the value is the user's deliberate public identifier within the platform. PII consideration: a user MAY choose an email-shaped or name-shaped username (e.g. `alice.smith@example.com`, `alicemarsh`). The platform does not constrain user choice; the cost of capturing username in operational logs (where it's invaluable for "show me Alice's requests" debugging) outweighs the marginal privacy delta from a user CHOOSING to use email-shaped identifiers. The `Subject` raw-`sub` and `UserId` Guid are the canonical identifiers; `Username` is the human-readable supplement. |
| 8   | `RequestedByClientId`     | `string?` | OAuth `client_id` of the client that requested THIS specific token (RFC 8693 §4.3 / RFC 9068 §2.2). Service identifier; not PII.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                  |
| 9   | `ImmediateCallerClientId` | `string?` | The service that immediately called this handler — outermost Service entry in the act chain. Service identifier; not PII.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                         |
| 10  | `OriginatingClientId`     | `string?` | The service that started the entire call chain — most-deeply-nested Service entry. Primary audit identifier for end-to-end traceability. Service identifier; not PII.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                             |
| 11  | `IsServiceIdentity`       | `bool?`   | True when the token represents a service identity (no user). Capability metadata; not PII.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                        |

#### Auth/Token+Trust (5 fields)

| #   | Field            | Type                        | Reason                                                                                                                                                                                                                                                                                                                                                                  |
| --- | ---------------- | --------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 12  | `Audience`       | `IReadOnlyList<string>`     | JWT `aud` claim — service identifier(s) the token was minted for. Destructured as JSON array. Empty-collection gate suppresses `[]` emission.                                                                                                                                                                                                                           |
| 13  | `SessionId`      | `Guid?`                     | User's own session id in `d2-auth` (session row). Opaque audit identifier; not PII.                                                                                                                                                                                                                                                                                       |
| 14  | `TokenIssuedAt`  | `DateTimeOffset?`           | JWT `iat` claim. Operational timestamp; not PII.                                                                                                                                                                                                                                                                                                                        |
| 15  | `TokenExpiresAt` | `DateTimeOffset?`           | JWT `exp` claim. Operational timestamp; not PII.                                                                                                                                                                                                                                                                                                                        |
| 16  | `ActorChain`     | `IReadOnlyList<ActorEntry>` | RFC 8693 actor chain, flattened. Destructured as JSON array of ActorEntry objects. Every member of `ActorEntry` is a subset of fields already approved as LOG-OK on the top-level axis (Subject, OrgId, OrgName, OrgType, OrgRole, SessionId, ImpersonationKind), so destructure is §3-safe. Empty-collection gate suppresses `[]` emission for end-user-direct tokens. |

#### Auth/Org (4 fields)

| #   | Field     | Type              | Reason                                                                                                                                                                                                                                                                                                                                                                   |
| --- | --------- | ----------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| 17  | `OrgId`   | `Guid?`           | Operating organization id. Opaque identifier; not PII.                                                                                                                                                                                                                                                                                                                   |
| 18  | `OrgName` | `string?`         | Operating organization display name. Internal logs are read by operators who already have tenant access to query org metadata via authenticated APIs; there is no incremental disclosure from emitting org names into logs that those same operators authenticate to access. Same rationale applies to `ImpersonatorOrgName` (the agent's own org during impersonation). |
| 19  | `OrgType` | `OrgType?` (enum) | Org type (`Admin` / `Support` / `Customer` / `ThirdParty` / `Affiliate`). Capability metadata; not PII.                                                                                                                                                                                                                                                                  |
| 20  | `OrgRole` | `Role?` (enum)    | User's role in the org (`Auditor` / `Agent` / `Officer` / `Owner`). Capability metadata; not PII.                                                                                                                                                                                                                                                                        |

#### Auth/Impersonation (8 fields)

| #   | Field                    | Type                        | Reason                                                                                       |
| --- | ------------------------ | --------------------------- | -------------------------------------------------------------------------------------------- |
| 21  | `IsImpersonating`        | `bool?`                     | True when the act chain contains any Impersonation entry. Capability metadata; not PII.      |
| 22  | `ImpersonationKind`      | `ImpersonationKind?` (enum) | `Consent` (OTP-authorized) vs `Force` (silent, admin-only). Capability metadata.             |
| 23  | `ImpersonatedBy`         | `Guid?`                     | Agent's (impersonator's) user id. Opaque identifier; not PII.                                |
| 24  | `ImpersonationSessionId` | `Guid?`                     | Impersonation session id (separate from user's own `SessionId`). Opaque identifier; not PII. |
| 25  | `ImpersonatorOrgId`      | `Guid?`                     | Agent's own org id. Opaque identifier; not PII.                                              |
| 26  | `ImpersonatorOrgName`    | `string?`                   | Agent's own org display name. Same rationale as `OrgName` above.                             |
| 27  | `ImpersonatorOrgType`    | `OrgType?` (enum)           | Agent's own org type. Capability metadata.                                                   |
| 28  | `ImpersonatorOrgRole`    | `Role?` (enum)              | Agent's own role in their own org. Capability metadata.                                      |

#### Scopes (1 field)

| #   | Field    | Type                   | Reason                                                                                                                                                                                                                          |
| --- | -------- | ---------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 29  | `Scopes` | `IReadOnlySet<string>` | OAuth scope set. Destructured as JSON array (preserves field-array semantics for "show me requests with scope X" queries in Loki / Elasticsearch). Empty-collection gate suppresses `[]` emission for unauthenticated requests. |

#### Trust/Risk (1 field)

| #   | Field       | Type           | Reason                                                                                                 |
| --- | ----------- | -------------- | ------------------------------------------------------------------------------------------------------ |
| 30  | `RiskScore` | `int?` (0-100) | Edge composite request-risk score. Derived numeric; not PII (zero reverse path to fingerprint inputs). |

#### Fingerprints (2 fields)

| #   | Field                | Type      | Reason                                                                                                         |
| --- | -------------------- | --------- | -------------------------------------------------------------------------------------------------------------- |
| 31  | `SessionFingerprint` | `string?` | Mint-time-bound fingerprint hash (SHA-256-derived components). Opaque hash.                                    |
| 32  | `CurrentFingerprint` | `string?` | Per-request recomputed fingerprint hash. Opaque hash. Comparison with `SessionFingerprint` feeds risk scoring. |

#### WhoIs/Geo (3 fields)

| #   | Field                 | Type                           | Reason                                                                                                                                                                               |
| --- | --------------------- | ------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| 33  | `WhoIsHashId`         | `string?`                      | Content-addressable hash of the full WhoIs record. Opaque hash; not PII. Operators with auth can dereference for the full record (including suppressed sub-country geo) when needed. |
| 34  | `AdminLocationHashId` | `string?`                      | Content-addressable hash of admin-location component (city + region + country + postal). Opaque hash; not PII.                                                                       |
| 35  | `CountryCode`         | `string?` (ISO 3166-1 alpha-2) | Country grain only — explicitly above the "city + postal beyond country" PII line.                                                                                                   |

#### WhoIs/Network-Privacy (4 fields)

| #   | Field       | Type    | Reason                                                                |
| --- | ----------- | ------- | --------------------------------------------------------------------- |
| 36  | `IsVpn`     | `bool?` | Derived boolean; not PII.                                             |
| 37  | `IsProxy`   | `bool?` | Derived boolean; not PII.                                             |
| 38  | `IsTor`     | `bool?` | Derived boolean; not PII. High signal for security review.            |
| 39  | `IsHosting` | `bool?` | Derived boolean; not PII. Hosting-source requests behave differently. |

#### WhoIs/ASN (3 fields)

| #   | Field     | Type      | Reason                                                                                                   |
| --- | --------- | --------- | -------------------------------------------------------------------------------------------------------- |
| 40  | `Asn`     | `int?`    | Public AS number (published BGP data); not PII. Risk-scoring + abuse-investigation signal.               |
| 41  | `AsnName` | `string?` | Public AS organization name; not PII.                                                                    |
| 42  | `AsnType` | `string?` | Coarse classification (`business` / `isp` / `hosting` / `mobile` / `education` / `government`); not PII. |

### NOT-LOGGED fields (suppressed at default)

8 fields. The first 5 are explicitly user-pinned PII; the last 3 (lat/long/geohash) are §3-driven (geographic precision = identification primitive — geohash IS lat/long, just hex-encoded). Sub-country geographic precision escalates only via `WhoIsHashId` lookups against the WhoIs store (operators with auth can dereference when a real investigation needs it).

| Field             | §3 cite                                                      | Reason                                                     |
| ----------------- | ------------------------------------------------------------ | ---------------------------------------------------------- |
| `ClientIp`        | §3 "What counts as PII" — IP addresses (v4 + v6)             | Raw IP is PII per §3.                                      |
| `City`            | §3 "What counts as PII" — sub-country geographic precision   | Locality grain beyond country is PII.                      |
| `Region`          | §3 "What counts as PII" — sub-country geographic precision   | Locality grain beyond country is PII.                      |
| `SubdivisionCode` | §3 "What counts as PII" — sub-country geographic precision   | ISO 3166-2 subdivision is sub-country precision; PII.      |
| `PostalCode`      | §3 "What counts as PII" — postal code                        | Locality grain beyond country is PII.                      |
| `Latitude`        | §3 "What counts as PII" — geographic (lat/long)              | GPS-exact precision is named in §3 as a PII primitive.     |
| `Longitude`       | §3 "What counts as PII" — geographic (lat/long)              | GPS-exact precision is named in §3 as a PII primitive.     |
| `Geohash`         | §3 "What counts as PII" — geographic (lat/long, hex-encoded) | Street-level precision; same §3 rationale as raw lat/long. |

### Opting in to additional fields

Callers can chain into the existing `RequestLoggingOptions.EnrichDiagnosticContext` callback:

```csharp
app.UseD2RequestLogging(opts =>
{
    var existing = opts.EnrichDiagnosticContext;
    opts.EnrichDiagnosticContext = (diag, http) =>
    {
        existing?.Invoke(diag, http);
        diag.Set("MyCustomField", ResolveCustomField(http));
    };
});
```

⚠️ Caller-added structured fields BYPASS the `[RedactData]` destructuring policy (the diagnostic-context path emits `ScalarValue`s directly). Callers MUST own PII discipline for anything they add. If the data is PII, log a `[RedactData]`-decorated wrapper object using the destructuring capture mode (`{@MyObj}`) inside a `LoggerScope` instead.

## Destructuring discipline

`RedactDataDestructuringPolicy` enforces the `[RedactData]` attribute defined in `D2.Shared.Utilities.Attributes`. It runs on every `@`-captured object in every `ILogger.Log*` call across every service that wires `AddD2Logging`. Three rules to know:

1. **Capture mode matters.** Serilog's `@`-prefix invokes destructuring; the policy fires. The default `{prop}` capture (without `@`) calls `.ToString()` on the value and bypasses destructuring entirely. If you have `[RedactData]` on a record and write `logger.Information("user {User}", user)`, the record's default `ToString()` includes property values verbatim — leaks the PII the attribute was supposed to redact.
2. **Field-level `[RedactData]` is silently ignored.** The policy reflects over `BindingFlags.Public | Instance` PROPERTIES only — no fields. Use property syntax for redaction.
3. **Computed properties leak through their inputs.** A `public string DisplayName => $"{First} {Last}"` is destructured even if `First` carries `[RedactData]`. Redact the computed property too if its inputs are PII.

### Redaction modes

| Decoration                           | Effect                                                              |
| ------------------------------------ | ------------------------------------------------------------------- |
| `[RedactData]` on the type           | Entire value rendered as `[REDACTED: {Reason}]`.                    |
| `[RedactData]` on a property         | Only that property is masked; siblings destructure normally.        |
| `[RedactData(CustomReason = "...")]` | The custom string replaces the enum-name reason in the placeholder. |

Reason rendering uses the enum name (`PersonalInformation`, `FinancialInformation`, `SecretInformation`, `VerboseContent`, `Other`, `Unspecified`) unless a `CustomReason` overrides it.

## File layout

| File                                             | Role                                                                                                                  |
| ------------------------------------------------ | --------------------------------------------------------------------------------------------------------------------- |
| `D2.Shared.Logging.csproj`                       | csproj — `Microsoft.NET.Sdk.Web` + `OutputType=Library`. ProjectReferences to `utilities/` + `context/abstractions/`. |
| `D2LoggingOptions.cs`                            | Sealed record — Options-pattern config.                                                                               |
| `D2LoggingConstants.cs`                          | Public constants (`OTEL_SERVICE_NAME_CONFIG_KEY`).                                                                    |
| `LoggingServiceCollectionExtensions.cs`          | Public DI extension: `AddD2Logging`.                                                                                  |
| `WebApplicationLoggingExtensions.cs`             | Public AspNetCore extension: `UseD2RequestLogging`.                                                                   |
| `Destructuring/RedactDataDestructuringPolicy.cs` | Internal sealed `IDestructuringPolicy` impl.                                                                          |
| `Destructuring/TypeRedactionInfo.cs`             | Internal sealed record — per-type cache value.                                                                        |
| `Internal/D2RequestContextEnricher.cs`           | Internal static helper that projects `IRequestContext` LOG-OK fields onto the request-completion log line.            |

## Dependencies

| Package                         | Why                                                                                                                                                                                    |
| ------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `Serilog.AspNetCore`            | The umbrella package. Pulls Serilog, Serilog.Extensions.Logging, Serilog.Sinks.Console, Microsoft.Extensions.Logging.Abstractions transitively. `UseSerilogRequestLogging` lives here. |
| `Serilog.Formatting.Compact`    | `CompactJsonFormatter`. Listed explicitly so transitive removal can't silently drop it.                                                                                                |
| `Serilog.Enrichers.Environment` | `Enrich.WithMachineName()`.                                                                                                                                                            |
| `JetBrains.Annotations`         | `[MustDisposeResource]` annotations on disposable factory paths.                                                                                                                       |

| Project reference                | Why                                                                                                                                                                                                                                                          |
| -------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `D2.Shared.Utilities`            | `[RedactData]` attribute + `RedactReason` enum + `Falsey()` / `Truthy()` extensions.                                                                                                                                                                         |
| `D2.Shared.Context.Abstractions` | `IRequestContext` interface (the strongly-typed enricher dep).                                                                                                                                                                                               |
| `D2.Shared.AspNetCore`           | Canonical `InfrastructurePathMatcher` consumed by `UseD2RequestLogging`'s level callback to demote infrastructure-path request-completion logs to `Verbose`. Single source of truth shared with `D2.Shared.Telemetry`'s AspNetCore-instrumentation `Filter`. |

## Edge cases / gotchas

- **`Log.Logger` is a process-global static.** `AddD2Logging` SETS it. Tests that build multiple hosts in one process see the LATEST host's logger on the static. The lib's integration tests pin this behavior with `[Collection("LogLoggerStaticState")]` to serialize against any other test that touches the static.
- **`Microsoft.NET.Sdk.Web` defaults `OutputType` to `Exe`.** This csproj overrides to `Library` explicitly — same shape as sibling `D2.Shared.Auth.Http`.
- **No `Microsoft.Extensions.{DependencyInjection,Logging,Options}.Abstractions` PackageReferences.** The framework reference (`Microsoft.AspNetCore.App`) ships them; explicit references would trigger NU1510 ("will not be pruned"). Versions remain pinned via `Directory.Packages.props`.
- **`InfrastructurePathMatcher` lives in `D2.Shared.AspNetCore` — single source of truth.** `UseD2RequestLogging`'s level callback consumes the public matcher; `D2.Shared.Telemetry`'s AspNetCore-instrumentation `Filter` consumes the same one. The path set (`/health`, `/alive`, `/metrics`, `/.well-known`) stays aligned across the two consumers without per-lib duplication.
- **The integration-test contract pins the absent PII fields.** `RequestContextEnricherIntegrationTests` enumerates every `IRequestContext` NOT-LOGGED field (the 8 enumerated above: `ClientIp`, `City`, `Region`, `SubdivisionCode`, `PostalCode`, `Latitude`, `Longitude`, `Geohash`) and asserts NONE appear in the rendered JSON output. Adding a new PII field to the spec without a coverage update is a contract failure that surfaces at test time.
