<!--
Copyright (c) DCSV. All rights reserved.
-->

## 16. OOTB Shared-Lib Tooling — Use What's There
<a name="top"></a>
_[← rules index](../rules.md) · §16 of the D2-WORX rules catalog._

**Predicate index:** §16.1–§16.5 · 5 predicates.

This codebase has a substantial shared-lib stack. Reaching for raw .NET / npm primitives when a shared lib exists is the #2 cost driver after deferred testing. **Always check the shared libs before hand-rolling.**

### What's available (catalog)

| Lib                                            | When to reach for it                                                                                                                                                                                                |
| ---------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `D2.Shared.Result` (`@d2/result`)              | Every operation that can fail — never throw + catch + return for control flow. Use semantic factories (§5.3).                                                                                                       |
| `D2.Shared.Utilities` (`@d2/utilities`)        | String / Guid / Enum / collection helpers. `Falsey()`, `Truthy()`, `ToNullIfEmpty()`, `TryParseTruthyNull()`, `CleanStr()`, `TryParseEmail()`, `TryParsePhoneNumber()`. Cache constants. Array / UUID helpers (TS). |
| `D2.Shared.Caching.Abstractions`               | Inject `ILocalCache` (per-process), `IDistributedCache` (cluster-wide), or `ITieredCache` (composed L1+L2). Every op returns `D2Result<T>`. Null/empty inputs → `ValidationFailed`.                                 |
| `D2.Shared.Caching.Local.Default`              | The default local cache implementation. Atomic ops (CAS-style) supported.                                                                                                                                           |
| `D2.Shared.Caching.Distributed.Redis`          | Redis-backed distributed cache. Includes pub/sub `ICacheInvalidationBackplane` for L1 coherency. `*AndBroadcast*` write variants publish on the backplane.                                                          |
| `D2.Shared.Resilience`                         | `ResilientPipeline` for retries / circuit breakers / timeouts. Don't hand-roll retry loops.                                                                                                                         |
| `D2.Shared.Encryption`                         | AES-256-GCM payload encryption. Use for sensitive RMQ payloads (per §9.11) and any persistence of secret-equivalent data.                                                                                           |
| `D2.Shared.Messaging`                          | RabbitMQ pub/sub via `[MqPub]` / `[MqSub]` attributes + spec-driven codegen. Don't hand-roll AMQP channel management.                                                                                               |
| `D2.Shared.Handler`                            | `BaseHandler<TSelf, TInput, TOutput>` with using aliases (`H`, `I`, `O`), `IHandlerContext`, `DefaultOptions` override. Per-handler PII redaction via `[RedactData]` + `DefaultOptions` overrides.                  |
| `D2.Shared.Handler.Repo` (+ `.Postgres`)       | EF→D2Result classification (PG `23505` → Conflict, FK violation → ValidationFailed, etc.). Use for any repository handler.                                                                                          |
| `D2.Shared.RequestContext` (+ `.Abstractions`) | `MutableRequestContext` filled by middleware; injected as `IRequestContext` everywhere. Carries traceId, userId, orgId, scopes, fingerprints.                                                                       |
| `D2.Shared.Auth.Abstractions`                  | `ActorEntry`, `ImpersonationKind`, `ActionSensitivity`, `OrgType`, `Role`, `JwtClaimTypes`, `RequestHeaders`, codegen-emitted `Scopes` + `Audiences`.                                                               |
| `D2.Shared.Auth.Outbound`                      | The per-request forwarded-transaction-token `CallCredentials` (the forward-unchanged service-to-service default — internal hops forward the boundary-minted token unchanged, ADR-0022), the workload-certificate mTLS leaf presentation (the calling workload's identity, ADR-0023), and `ITokenExchangeClient` (RFC 8693) for the cases that genuinely re-mint — the Edge boundary mint + the deliberate exceptions (cross-trust-domain, narrowing, async scope reduction, impersonation); NOT a per-hop default. Don't hand-roll OAuth flows or per-hop token mints.                                                                                                                                                                                                                                                                                                                                                                                  |
| `D2.Shared.Service.Defaults`                   | One-call OTel SDK bootstrap (`setupTelemetry`). Standard service config.                                                                                                                                            |
| `D2.Shared.I18n` (+ `.Abstractions`)           | Translation key constants (`TK.*`) — see §12.5.                                                                                                                                                                     |
| `D2.Shared.Logging` (Node)                     | `ILogger` + Pino impl, auto-instrumented via OTel.                                                                                                                                                                  |
| `@d2/handler` (Node)                           | `BaseHandler` parity with .NET — auto-injects traceId, OTel spans + 4 metrics.                                                                                                                                      |
| `D2.Shared.Tests` (`@d2/testing`)              | Custom xUnit / Vitest matchers, fixtures.                                                                                                                                                                           |

### Predicates — §16 OOTB shared-lib tooling

- **16.1** When a need arises that one of the shared libs covers, is the shared lib used (not hand-rolled)?
  - Evidence: per non-trivial helper / pattern → matched against the catalog above; if duplicating capability → justify or refactor to use shared lib.

- **16.2** When a needed extension doesn't exist yet in `D2.Shared.Utilities.Extensions`, is the new extension proposed (don't hand-roll inline)? Check the v1 (`/old/v1/`) and DeCAF (`/old/DeCAF-DCSV/`) snapshots first — they often had the helper and the pattern was carried forward intentionally.
  - Evidence: per inline helper hand-roll → "checked utilities, not present, proposing addition" or "found in /old/, ported forward."

- **16.3** Are caching tier choices appropriate?
  - `ILocalCache` — per-process, ephemeral. OK for read-cache that doesn't need cluster coherency.
  - `IDistributedCache` — cluster-wide. Use for cross-instance state (sessions, rate-limit counters, idempotency keys).
  - `ITieredCache` — composed L1+L2. Reads check L1 → fall through to L2 → populate L1. Writes go L2-first. Use when read-heavy + cluster-coherent.
  - `*AndBroadcast*` write variants — publish on backplane to invalidate other instances' L1.
  - Evidence: per cache injection → tier justified.

- **16.4** When a `netstandard2.0` Roslyn source generator can't reference `D2.Shared.Utilities` (TFM mismatch — Roslyn analyzers can't load `net10`-targeted assemblies), is the missing helper provided via a local `Polyfills/StringExt.cs` (or equivalent) that mirrors the real semantics exactly?
  - **Required pattern**: a polyfill class under `Polyfills/`, namespace-scoped to the source-gen project, with the SAME signature and semantics as the canonical helper (e.g. `Falsey()` covers null + empty + whitespace, no `string.IsNullOrEmpty` shortcut). Implement via primitive operations (a `for` loop over chars) — never via the BCL helpers the convention forbids.
  - **Forbidden**: using `string.IsNullOrEmpty` / `string.IsNullOrWhiteSpace` inside the source-gen because "we can't reference utilities anyway." Polyfill it; the convention is universal regardless of TFM.
  - Evidence: per source-gen project that needs Falsey-class behavior → `Polyfills/StringExt.cs` (or named equivalent) confirmed; per use site → polyfill called, not the BCL forbidden helper.

- **16.5** Is `ResilientPipeline` (or equivalent) used for any retryable network call, NOT a hand-rolled `for (int i = 0; i < 3; i++) try {...}`?
  - Evidence: per retry site → `ResilientPipeline` confirmed.

<sup>[↑ jump to top](#top)</sup>

---

