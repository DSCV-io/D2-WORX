<!--
Copyright (c) DCSV. All rights reserved.
-->

# PHASE_8_REFERENCE.md — dkron-mgr (.NET)

> **Phase 8** per V2.md §4. Reference doc preserved during rebuild — **delete this file after Phase 8 ships**.
>
> **Source**: distilled from v1 `backends/node/services/dkron-mgr/DKRON_MGR.md` (preserved in `/old/v1/D2-WORX/`).
>
> **Open question per V2.md §4 Phase 8**: dkron-mgr might be ported as-is (.NET rewrite), replaced by `Hangfire`, or replaced by built-in `IHostedService` cron. Investigate during Phase 8. This doc captures the v1 reconciler pattern in case the port-as-is path is chosen.

---

## What dkron-mgr Does

dkron-mgr is a **declarative reconciler** for Dkron jobs. Code defines the desired set of scheduled jobs; dkron-mgr ensures Dkron's actual job state matches.

Eliminates "configure jobs by hand in the Dkron UI" — jobs are version-controlled with the code that owns them.

---

## Reconciler Pattern (preserved from v1)

Every 5 minutes (configurable):

1. **Fetch desired state** — read job definitions from code (a list in `dkron-mgr` itself, derived from each service's job declarations)
2. **Fetch actual state** — `GET /v1/jobs` from Dkron API
3. **Filter to managed jobs** — only consider jobs with `metadata.managed_by == "d2-dkron-mgr"` (don't touch jobs created manually outside dkron-mgr)
4. **Diff** — compare desired vs actual on a per-job basis
5. **Apply**:
   - Job in desired but not actual → CREATE (POST `/v1/jobs`)
   - Job in actual but not desired → DELETE
   - Job in both with field drift → UPDATE (POST `/v1/jobs` — Dkron upserts on `name`)
   - Job in both with no drift → no-op

Idempotent — safe to run any number of times.

---

## Change-Detection Field List

dkron-mgr considers these fields when diffing actual vs desired:

| Field | Notes |
|---|---|
| `displayname` | Human-readable label |
| `schedule` | Cron expression (Dkron format — note: 6-field including seconds) |
| `timezone` | IANA timezone (e.g., `America/New_York`) |
| `executor` | Always `http` for D² jobs |
| `executor_config` | HTTP method, URL, headers (including service-key auth header) |
| `concurrency` | `allow` or `forbid` (forbid = skip if previous instance still running) |
| `retries` | Retry count on failure |
| `disabled` | Whether the job is currently disabled |

Other Dkron fields (last execution time, success/failure counts, etc.) are not considered — those reflect runtime state, not desired config.

If ANY tracked field differs, dkron-mgr issues an UPDATE.

---

## Job Definition Shape (v2 — TBD per chosen approach)

If port-as-is, jobs declared in dkron-mgr's own code:

```csharp
public static readonly JobDefinition[] DesiredJobs = new[]
{
    new JobDefinition
    {
        Name = "edge-cleanup-deleted-users",
        Displayname = "Cleanup soft-deleted users (30d grace)",
        Schedule = "0 0 3 * * *",         // 3:00 AM UTC
        Timezone = "UTC",
        Executor = "http",
        ExecutorConfig = new HttpExecutorConfig
        {
            Url = $"http://{EdgeServiceName}:{EdgePort}/api/v1/auth/jobs/cleanup-deleted-users",
            Method = "POST",
            Headers = new Dictionary<string, string>
            {
                ["X-D2-Service-Key"] = ServiceKey,
            },
        },
        Concurrency = "forbid",
        Retries = 3,
    },
    // ... more job definitions
};
```

Per V2.md §5.4 the service-key auth approach is transitional; eventually replaced by RFC 6749 §4.4 client_credentials JWTs (KeyCustodian-issued).

---

## Operational Behavior

- **First run after Dkron is empty** — creates all desired jobs
- **First run after dkron-mgr is upgraded with new jobs** — adds new ones, leaves existing ones alone
- **First run after a job definition changes** — updates the job
- **First run after a job is removed from code** — deletes the actual job in Dkron
- **Dkron-side manual edits** (UI changes to a managed job) — UNDONE on next reconciler tick (dkron-mgr is the source of truth for managed jobs)

---

## Alternative Approaches (per V2.md §4 Phase 8 — to investigate)

### `Hangfire`

DB-backed background jobs in .NET. Trade-offs vs Dkron:
- **Pro**: Eliminates the dkron daemon container entirely; jobs live with the service that owns the work
- **Pro**: Mature, well-supported in .NET ecosystem
- **Con**: PG-backed (more DB load); Dkron uses its own embedded storage
- **Con**: Less ops surface (no separate dashboard like Dkron's UI)

### `IHostedService` cron pattern

Built-in `IHostedService` with a cron-like loop in each service.
- **Pro**: No extra infrastructure at all; jobs are part of the service
- **Pro**: Most code-local (no separate scheduler config)
- **Con**: Each service has to handle cross-replica deduplication (Redis distributed lock — same pattern as today, see OPERATIONAL-GUARANTEES.md)
- **Con**: No central UI to see "what jobs ran when across all services"

### Port dkron-mgr to .NET (preserve current architecture)

- **Pro**: Minimal architectural change; current operations team already knows Dkron
- **Pro**: Keeps the central scheduler UI for ops visibility
- **Con**: Two extra containers (dkron + dkron-mgr) for one capability
- **Con**: Dkron itself isn't .NET — adds a Go process to the deployment

Decision deferred to Phase 8. This doc documents the pattern so any of the three paths can preserve the **declarative reconciler + managed-by metadata** approach (which is the actual valuable design, regardless of executor choice).

---

## When This Doc Gets Deleted

Phase 8 completion criteria includes:
- [ ] Phase 8 architecture chosen (port / Hangfire / IHostedService / something else)
- [ ] Implementation embodies the declarative reconciler pattern (or documented why not)
- [ ] All scheduled jobs migrated from v1 to v2 with the same `managed_by` metadata semantics
- [ ] Per-service README captures how scheduled jobs are declared (could be `tools/scripts/README.md` if jobs are central, or per-service README if distributed)

Once the chosen approach is documented + operational, this reference doc has served its purpose. Move to `docs/archive/PHASE_8_REFERENCE.md` or delete.
