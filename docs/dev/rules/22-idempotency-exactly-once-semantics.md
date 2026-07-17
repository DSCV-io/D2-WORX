<!--
Copyright (c) DCSV. All rights reserved.
-->

## 22. Idempotency & Exactly-Once Semantics
<a name="top"></a>
_[← rules index](../rules.md) · §22 of the D2-WORX rules catalog._

**Predicate index:** §22.1–§22.6 · 6 predicates.

Distributed systems retry. Operations must tolerate retries without doubling effects.

### Predicates — §22 idempotency & exactly-once semantics

- **22.1** Does every state-mutating HTTP endpoint accept an `Idempotency-Key` header (or equivalent) and dedupe on it?
  - Evidence: per state-mutating endpoint → idempotency middleware.

- **22.2** Does every RabbitMQ consumer use the idempotency-store pattern (check `MarkSeen` before processing; re-route to DLQ if seen)?
  - Evidence: per consumer → MarkSeen check.

- **22.3** Are external API calls (especially payment, notification dispatch) protected by idempotency keys forwarded to the upstream when supported?
  - Evidence: per external call → key forwarding.

- **22.4** Is exactly-once semantics achieved via the SAGA pattern for cross-service updates? (E.g., Geo-first → Auth-second → compensate Geo on auth failure → fatal log if rollback fails.)
  - Evidence: per cross-service update → SAGA shape.

- **22.5** Are idempotency-key TTLs sensible (long enough to cover client retry windows; short enough not to bloat storage)?
  - Evidence: per idempotency store → TTL value + justification.

- **22.6** Do INCR-class atomic ops (`IncrementAsync` and equivalents — anything that does read-modify-write on a numeric counter via an atomic primitive) PRESERVE existing TTL on subsequent calls? The TTL applied at first creation must NOT be re-applied on every call (that turns rate-limit / throttling counters into "ever" instead of "5 minutes" under sustained load).
  - **Pattern (Redis)**: gate `PEXPIRE` on `redis.call('PTTL', KEYS[1]) < 0` so it fires only when the key has no existing TTL.
  - **Pattern (in-process)**: read existing absolute expiration from the parallel TTL-tracking dictionary and re-apply it verbatim instead of falling back to the default-TTL helper.
  - **Required regression test**: SET with a short TTL (e.g. 2 minutes) → INCR → assert `GetTtl` reports remaining ≤ original window (NOT default expiration). Pin per impl.
  - **Why**: the bug is silent — counter values keep working; only the TTL window stretches. Rate-limit / throttle counters under sustained load never expire, so the rate becomes effectively "ever."
  - Evidence: per atomic-op impl → Lua / locked-block code path inspected for TTL-preservation gate + regression test linked.

<sup>[↑ jump to top](#top)</sup>

---

