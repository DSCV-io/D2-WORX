<!--
Copyright (c) DCSV. All rights reserved.
-->

## 4. Concurrency / Race Conditions
<a name="top"></a>
_[← rules index](../rules.md) · §4 of the D2-WORX rules catalog._

The bugs that don't fail unit tests because unit tests are sequential.

### Predicates — §4 concurrency / race conditions

- **4.1** Does any `Singleflight.ExecuteAsync` body re-check the cache inside the singleflight before doing the expensive operation?
  - **Race**: lookup → miss → singleflight → ALSO miss because no recheck → double-fetch.
  - Evidence: per singleflight call site → confirm cache-recheck inside body.

- **4.2** Does any "atomic" operation on a distributed cache use a real atomic primitive (`SET NX`, `EVAL` Lua, transaction), or is it a sequence of non-atomic ops that look atomic in isolation?
  - Evidence: per atomic-claimed op → primitive used.

- **4.3** Does any code holding multiple locks document the lock acquisition order (preventing AB / BA deadlock)?
  - Evidence: per multi-lock site → ordering doc.

- **4.4** Does every backplane subscriber unsubscribe on dispose? (Otherwise subscriptions leak across test runs and the second test sees the first test's events.)
  - Evidence: per subscribe → corresponding unsubscribe in `IAsyncDisposable.DisposeAsync`.

- **4.5** Does every `IHostedService` respect the cancellation token in its loop body (not just at loop entry)?
  - Evidence: per hosted service → token check inside body.
  - **Specifically**: `await Task.Delay(interval, ct)` not `await Task.Delay(interval)` then check.

- **4.6** Is any `ValueTask` awaited more than once? (Documented antipattern — call `.AsTask()` once, store the `Task`, reuse.)
  - Evidence: per `ValueTask` field/local → call count check.

- **4.7** Is any code using `new Random()` instead of `Random.Shared`? (Latter is thread-safe.)
  - Evidence: `grep -rEn 'new Random\(' <scope>` → expect zero.

- **4.8** Does any database write happen WITHOUT a transaction when multiple rows are coordinated?
  - Evidence: per multi-row write → `BeginTransactionAsync` confirmed.

- **4.9** Does any cache write that needs cluster-wide L1 invalidation use the `*AndBroadcast*` variant (publishes on backplane)?
  - Evidence: per cache-write → confirm broadcast variant when other instances cache the same key.

- **4.10** Are HTTP retries idempotent? (Non-idempotent operations need an idempotency key, not just retry-on-failure.)
  - Evidence: per HTTP-retry config → idempotency strategy.

- **4.11** Does any `Task.Run` / `ConfigureAwait(false)` pattern correctly preserve / drop the synchronization context as appropriate for ASP.NET Core / library code?
  - Evidence: per `ConfigureAwait` site → correct choice.
  - **In library code**: `ConfigureAwait(false)`.
  - **In ASP.NET Core handler code**: omit (defaults work).

- **4.12** Does any code mutate shared state (static field, singleton DI service field) without a lock / `Interlocked` / `Volatile.Read`/`Write`?
  - Evidence: per static / singleton mutation → synchronization primitive confirmed.

- **4.13** When holding a lock, is the duration minimized (no I/O, no async-await, no allocation in the critical section when avoidable)?
  - Evidence: per `lock` body → I/O / await / allocation audit.

- **4.14** Does shutdown of any `IHostedService` complete in finite time? (No infinite loop without ct check; no `await Task.Delay(Timeout.InfiniteTimeSpan)` without ct.)
  - Evidence: per hosted service → shutdown trace within 30s SIGTERM grace period.

- **4.15** Do consumer / publisher channels handle reconnect cleanly? (Per `ChannelPool` / `IConnection` rebuild paths.)
  - Evidence: per channel use → reconnect scenario tested.

- **4.16** Are dictionaries / sets accessed concurrently using `ConcurrentDictionary` / `ConcurrentBag` / similar — NOT plain `Dictionary` / `HashSet` with manual locking unless justified?
  - Evidence: per concurrent collection use → type confirmed.

- **4.17** Are row-update operations that other writers may also touch protected with optimistic concurrency (rowversion / `xmin` / version column / `[ConcurrencyCheck]`)? Is `DbUpdateConcurrencyException` caught and converted to `D2Result.Conflict()` (or retried with backoff for idempotent updates)?
  - **Why**: without this, two concurrent writers can clobber each other (last-write-wins), losing data or invariants. Real example: F7.5 M2 audit fix on `IntakeFile` — concurrent intake of the same file lost the earlier write.
  - Evidence: per row-update handler → concurrency token + exception mapping confirmed.

- **4.18** Is fire-and-forget background work (raw `Task.Run` / `_ = SomeAsync()` / unmonitored async) avoided in favor of explicit hosted services or background queues?
  - **Why**: fire-and-forget swallows exceptions silently, doesn't respect SIGTERM, and obscures observability. F7.5 M3 audit fix found "raw upload cleanup fire-and-forget" — failure surfaced only via storage growing unboundedly.
  - **Allowed**: when truly best-effort AND failures are logged AND the operation has no business correctness impact. Document the "best-effort" choice in the call-site comment.
  - Evidence: per non-awaited async invocation → category (allowed best-effort with logging / use hosted service / use background queue).

<sup>[↑ jump to top](#top)</sup>

---

