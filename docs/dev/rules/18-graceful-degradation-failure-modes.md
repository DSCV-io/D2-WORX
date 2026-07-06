<!--
Copyright (c) DCSV. All rights reserved.
-->

## 18. Graceful Degradation & Failure Modes
<a name="top"></a>
_[← rules index](../rules.md) · §18 of the D2-WORX rules catalog._

Production code MUST degrade gracefully. Identify every dependency, document its failure mode, and make sure the handler doesn't crash / hang / silently break when the dependency fails.

### Predicates — §18 graceful degradation & failure modes

- **18.1** For every external dependency (DB, broker, cache, third-party API, OIDC discovery, JWKS endpoint), what happens when it's unavailable?
  - Evidence: per dependency → degradation strategy documented (fail-closed / fail-open / use stale cache / circuit-break / retry with backoff).

- **18.2** Are retryable errors classified differently from non-retryable errors?
  - **Retryable**: 5xx, network timeout, rate-limited (429), broker temporarily unreachable.
  - **Non-retryable**: 4xx client errors, validation failures, auth failures, 404s.
  - Evidence: per error path → retry decision documented.

- **18.3** Are timeouts set on EVERY network call (HTTP, gRPC, DB query, broker publish)?
  - **Why**: default infinite timeouts cause hung handlers, exhausted thread pools, eventual cascade.
  - Evidence: per network call → timeout value confirmed.

- **18.4** Are circuit breakers in place for downstream services that can become unhealthy? (Use `D2.Shared.Resilience.CircuitBreaker`.)
  - Evidence: per cross-service call → circuit breaker state.

- **18.5** Are fallback values correct when degradation kicks in? (Don't return wrong data; return `D2Result.ServiceUnavailable()` or stale-but-flagged data.)
  - Evidence: per fallback path → correctness audit.

- **18.6** Does the handler distinguish between "transient failure, retry" and "permanent failure, give up"?
  - Evidence: per failure-handling code → classification.

- **18.7** Are partial failures handled (e.g., batch operation where 8/10 succeed, 2 fail)?
  - Evidence: per batch op → partial-failure shape.

- **18.8** Does the system fail-closed on critical security checks (auth, authz)? Fail-open is unacceptable for security-critical paths.
  - Evidence: per auth/authz check → fail-closed branch confirmed.

- **18.9** When a downstream service returns malformed data (truncated JSON, unexpected schema, encoding errors), is the failure caught and converted to `D2Result` rather than propagated as a raw exception?
  - Evidence: per parse / deserialize site → catch + convert.

- **18.10** Are CancellationTokens propagated end-to-end? (Long-running operations must respect ct so they're cancelable when the request is canceled.)
  - Evidence: per long-running op → ct passed through.

- **18.11** Does shutdown drain in-flight requests within the SIGTERM grace window? (No abrupt termination of a request mid-flight; either complete or rollback.)
  - Evidence: per shutdown handler → drain mechanism.

- **18.12** Are bulkheads / concurrency limits in place to prevent one failing dependency from saturating thread pool?
  - Evidence: per heavy-async service → concurrency cap.

<sup>[↑ jump to top](#top)</sup>

---

