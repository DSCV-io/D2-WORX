// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

/**
 * Re-export of generated proto module surface. Generated TS lives under
 * `src/generated/` and is regenerated via `pnpm generate` (Buf + ts-proto).
 *
 * The barrel re-export is intentionally narrow at this stage — only the
 * `common/v1` namespace ships in this iteration. As more contracts (auth,
 * geo, events, etc.) land in `contracts/protos/` they get re-exported here.
 */

// Generated re-exports — populated by `pnpm generate`. The tests/smoke
// test ensures at least one expected module path resolves.
export {};
