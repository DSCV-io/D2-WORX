<!--
Copyright (c) DCSV. All rights reserved.
-->

# server/shared/dotnet/ — Shared .NET Libraries

Foundational libraries consumed by every D²-WORX .NET service.

Per project convention, every library has its own `README.md`. The list below points at each lib's local README. Until each lib is built out, entries are placeholder shells.

## Libraries

| Lib | Purpose | Reference |
|---|---|---|
| [`handler/`](handler/README.md) | `BaseHandler<TSelf, TInput, TOutput>` — OTel spans + 4 metrics + `[RedactData]` integration. The pattern every handler in every service inherits. | [PATTERNS.md](../../../docs/PATTERNS.md) Handler section |
| [`result/`](result/README.md) | `D2Result<T>` — errors-as-values, semantic factories, partial-success ladder, `BubbleFail` propagation, auto-injected `traceId`. | [PATTERNS.md](../../../docs/PATTERNS.md) D2Result section |
| [`i18n/`](i18n/README.md) | TK constants + `Translator` for backend message + input-error translation. | CLAUDE.md §6 translation key conventions |
| [`utilities/`](utilities/README.md) | `Truthy()` / `Falsey()` / `ToNullIfEmpty()` / `CleanStr()` / `CircuitBreaker` / `Singleflight` / retry helpers. | [PATTERNS.md](../../../docs/PATTERNS.md) Utilities section |
| [`service-defaults/`](service-defaults/README.md) | Service composition root — OTel SDK bootstrap, Serilog setup, structured request logging, `[RedactData]` destructuring policy registration. | [PATTERNS.md](../../../docs/PATTERNS.md) (RedactDataDestructuringPolicy mechanics) |
| [`caching-memory/`](caching-memory/README.md) | In-memory cache — lazy TTL + always-on LRU + max 10K default. Per-instance only. | [PATTERNS.md](../../../docs/PATTERNS.md) Cache section |
| [`caching-redis/`](caching-redis/README.md) | Redis distributed cache — pluggable `ICacheSerializer`, atomic `SetNx` / `Increment` / `AcquireLock` primitives. | [PATTERNS.md](../../../docs/PATTERNS.md) Cache section |
| [`messaging/`](messaging/README.md) | RabbitMQ wrapper — proto-canonical-JSON serialization, `[Encrypted(Domain.X)]` attribute integration with `D2.Shared.Encryption`, AMQP headers contract. | [MESSAGING.md](../../../docs/MESSAGING.md) |
| [`encryption/`](encryption/README.md) | `PayloadCryptoKeyring` (JWKS-style multi-key), `IPayloadCrypto` (AES-256-GCM), frame format. Consumes the keyring from `KeyringClient` (auth lib) at runtime. | [SECURITY-RUNBOOKS.md](../../../docs/SECURITY-RUNBOOKS.md) |
| [`geo-reference/`](geo-reference/README.md) | Embedded geographic reference data — countries, IANA timezones, currencies, locales, regions. Not a service. | — |
| [`location/`](location/README.md) | Location value objects — `AdminLocation` (country / state / city / postal), `Coordinates`, `StreetAddress`. Content-addressable hash IDs (built-in dedup + cacheability). | [OPERATIONAL-GUARANTEES.md](../../../docs/OPERATIONAL-GUARANTEES.md) (content-addressable entities) |
| [`contacts/`](contacts/README.md) | Contact entity + per-consuming-service DB pattern. Library owns its own `DbContext` + migrations; consuming service provides connection string. | [OPERATIONAL-GUARANTEES.md](../../../docs/OPERATIONAL-GUARANTEES.md) (immutability rationale) |
| [`auth/`](auth/README.md) | `Scopes` constants, JWT claim helpers, token primitives, `KeyringClient` (consumes Edge KeyCustodian). | [SECURITY-RUNBOOKS.md](../../../docs/SECURITY-RUNBOOKS.md) |
| [`tests/`](tests/README.md) | Test infrastructure for ALL shared libs (deliberately one project — overkill to spin up a separate test csproj for every lightweight lib). Testcontainers spin-up helpers, redaction-respecting test logger, mock builders, `D2Result` FluentAssertions extensions. | [TESTS.md](../../../docs/TESTS.md) |

## Conventions

- **Folder naming**: lowercase outer (`handler/`, `caching-redis/`)
- **Project naming**: PascalCase dot-separated (`D2.Shared.Handler.csproj` lives in `handler/`)
- **One handler per file** under `Handlers/{TLC}/{3LC}/` per [PATTERNS.md](../../../docs/PATTERNS.md) TLC convention
- **Every project has a `README.md`** per CLAUDE.md §6 documentation rule

## Build

```bash
dotnet build server/D2.slnx        # full solution (includes all shared libs + services)
```

Each lib registers its DI surface via an `AddXxx(IServiceCollection)` extension method — consuming services compose them at the composition root.
