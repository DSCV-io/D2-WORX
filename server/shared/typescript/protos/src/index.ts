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

// common/v1 — D2ResultProto + TKMessageProto + InputErrorProto (+ Fns helpers)
export {
  D2ResultProto,
  TKMessageProto,
  InputErrorProto,
} from "./generated/common/v1/d2_result.js";
