# @d2/dkron-mgr-tests

Test suite for the Dkron Manager service (`@d2/dkron-mgr`). 64 tests across 6 files covering config parsing, REST API client, job definitions, reconciliation logic, and full process lifecycle.

## Purpose

Validates the Dkron Manager's reconciliation pipeline end-to-end: environment config parsing and validation, the thin `fetch` wrapper for Dkron's REST API, desired job definition correctness, diff-based reconciliation logic, and the actual `main.ts` process startup against a real Dkron container. Tests are **separate** from the source package (mirrors .NET convention) -- the source package has zero test dependencies.

## Design Decisions

| Decision                    | Rationale                                                                  |
| --------------------------- | -------------------------------------------------------------------------- |
| Separate test package       | Source package stays lean -- no test deps in production builds             |
| Unit + integration + E2E    | Unit tests mock `globalThis.fetch`, integration + E2E use Testcontainers   |
| `globalThis.fetch` mocking  | Service uses native `fetch` -- mocking at global level avoids module stubs |
| Testcontainers Dkron 4.0.9  | Real Dkron container for integration and E2E tests                         |
| Child process spawn for E2E | Catches TDZ bugs, top-level await failures, env parsing, graceful shutdown |
| Sequential test execution   | `fileParallelism: false` -- container tests must not race                  |
| Shared setup file           | `src/setup.ts` registers custom D2Result matchers from `@d2/testing`       |

## Test Organization

```
vitest.config.ts                 Vitest config (merges vitest.shared.ts, project name: dkron-mgr-tests)
package.json                     Test-only devDependencies
tsconfig.json                    TypeScript config
src/
  setup.ts                       Registers @d2/testing custom Vitest matchers
  unit/
    config.test.ts               parseConfig + logConfig (env parsing, defaults, validation, redaction)
    dkron-client.test.ts         checkHealth, listJobs, upsertJob, deleteJob (mocked fetch)
    job-definitions.test.ts      getDesiredJobs (8 jobs, scheduling, gateway URL injection, invariants)
    reconciler.test.ts           buildComparableKey + reconcile (create, update, delete, unchanged, errors)
  integration/
    reconciler-e2e.test.ts       Full reconciliation against real Dkron container (Testcontainers)
  e2e/
    main-process.test.ts         Spawns main.ts as child process against real Dkron container
```

## Test Counts

| Category    | Files | Description                                              |
| ----------- | ----- | -------------------------------------------------------- |
| Unit        | 4     | Config, Dkron client, job definitions, reconciler        |
| Integration | 1     | Reconciliation cycle against real Dkron (Testcontainers) |
| E2E         | 1     | Full process lifecycle (spawn, reconcile, shutdown)      |
| **Total**   | **6** | **64 tests**                                             |

## Test Categories

### Unit Tests (4 files)

**config.test.ts** (11 tests) -- Validates `parseConfig` and `logConfig`. Covers: required env var parsing, default reconcile interval (300s), custom interval parsing, trailing slash stripping on URLs, `ConfigError` on missing required vars (individual + aggregate), fallback to default on non-numeric/zero/negative interval values, frozen config object. Also tests service key redaction in log output (partial redaction for long keys, full redaction for short keys).

**dkron-client.test.ts** (12 tests) -- Tests the thin `fetch` wrapper functions against mocked `globalThis.fetch`. `checkHealth`: 200 with leader address, non-2xx, empty body (Raft not ready), network error. `listJobs`: parsed job array, null response, empty array, non-2xx error. `upsertJob`: POST payload shape, 200 as update success, non-2xx error. `deleteJob`: DELETE by name, 404 as graceful success with warning log, non-2xx error, URL-encoding of special characters in job names.

**job-definitions.test.ts** (14 tests) -- Validates `getDesiredJobs` output correctness. Covers: exactly 9 jobs returned, all expected job names present, unique names, gateway URL resolution in `executor_config`, service key injection into headers, UTC timezone for all jobs, http executor, `forbid` concurrency, 2 retries, all jobs enabled, POST method, 30s timeout, staggered schedules (no duplicates), and correct ordering (geo WhoIs purge before orphaned location cleanup).

**reconciler.test.ts** (7 tests) -- Tests `buildComparableKey` (identical keys for identical jobs, key-order independence in `executor_config`, key changes on schedule/URL change, name excluded from comparison) and `reconcile` (create all 8 when empty, all unchanged when state matches, update on schedule drift, delete orphaned managed jobs, ignore unmanaged jobs, accumulate errors without blocking other operations).

### Integration Tests (1 file)

**reconciler-e2e.test.ts** (11 tests) -- Runs against a real Dkron 4.0.9 Testcontainer. Tests the full client-to-reconciler pipeline:

| Test                       | What It Validates                                     |
| -------------------------- | ----------------------------------------------------- |
| Health check               | `checkHealth` returns true on live container          |
| Empty job list             | Fresh container has no jobs                           |
| Create via upsert          | Job created and retrievable with correct fields       |
| Update via upsert          | Existing job updated (schedule, retries, displayname) |
| Delete                     | Job removed and no longer in list                     |
| Delete non-existent        | 404 handled gracefully (no throw)                     |
| First reconciliation       | All 8 managed jobs created in Dkron                   |
| Second reconciliation      | All 8 reported as unchanged (idempotent)              |
| Schedule drift detection   | Tampered schedule detected and corrected              |
| Orphan cleanup             | Extra managed job deleted, desired jobs untouched     |
| Unmanaged job preservation | Jobs without `managed_by` metadata left untouched     |

### E2E Tests (1 file)

**main-process.test.ts** (1 test) -- Spawns the actual `main.ts` entry point as a child process via `node --import tsx` against a real Dkron container. Validates end-to-end: process starts without crash, environment variables parse correctly, health wait loop succeeds, first reconciliation completes (all 9 jobs created in Dkron), process is terminable. Catches issues that module-level imports miss: TDZ bugs, top-level await failures, env parsing errors, and shutdown handling.

## Running Tests

```bash
# All dkron-mgr tests (unit + integration + E2E)
pnpm vitest run --project dkron-mgr-tests

# Watch mode
pnpm vitest --project dkron-mgr-tests

# Unit tests only (fast, no containers)
pnpm vitest run --project dkron-mgr-tests src/unit/

# Integration tests only (requires Docker)
pnpm vitest run --project dkron-mgr-tests src/integration/

# E2E tests only (requires Docker)
pnpm vitest run --project dkron-mgr-tests src/e2e/
```

## Infrastructure Requirements

| Requirement       | Details                                                                    |
| ----------------- | -------------------------------------------------------------------------- |
| Docker            | Required for integration and E2E tests (Testcontainers)                    |
| Dkron 4.0.9       | Container image `dkron/dkron:4.0.9` pulled automatically by Testcontainers |
| Container startup | ~10-30s for Dkron agent (waits for `/health` 200 + `/v1/leader` 200)       |
| No external deps  | Unit tests run without Docker -- only `globalThis.fetch` mocking           |

## Configuration

Vitest config at `dkron-mgr/tests/vitest.config.ts`:

- **Project name**: `dkron-mgr-tests`
- **Shared config**: Inherits from `backends/node/vitest.shared.ts`
- **Setup file**: `src/setup.ts` (registers `@d2/testing` custom matchers)
- **File parallelism**: Disabled (`fileParallelism: false`) -- container tests must not race

## Dependencies

| Package          | Purpose                                                 |
| ---------------- | ------------------------------------------------------- |
| `@d2/dkron-mgr`  | Source package under test (config, client, reconciler)  |
| `@d2/logging`    | `ILogger` type for silent logger construction           |
| `@d2/testing`    | Custom Vitest matchers for D2Result                     |
| `testcontainers` | Dkron container lifecycle for integration and E2E tests |
| `vitest`         | Test runner                                             |
