// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Drift-guard: pins the WorkloadLeafClient default tunables to the literal values
// they mirror from the .NET twin. The .NET side pins the same three defaults in
// DcsvIo.D2.Tests.Unit.AuthOutbound.AuthOutboundDefaultsParityTests; a change to
// either runtime's default reds that runtime's pin test and points at the twin.
//
//   DEFAULT_REFRESH_MARGIN_MS  <- AuthOutboundOptions.WorkloadLeafRefreshLeadTime (5 min)
//   DEFAULT_FAILURE_THRESHOLD  <- AuthOutboundResilienceDefaults.FAILURE_THRESHOLD (5)
//   DEFAULT_COOLDOWN_MS        <- AuthOutboundResilienceDefaults.SR_CooldownDuration (30 s)

import { describe, expect, it } from "vitest";
import {
  DEFAULT_REFRESH_MARGIN_MS,
  DEFAULT_FAILURE_THRESHOLD,
  DEFAULT_COOLDOWN_MS,
} from "../src/issuance/workload-leaf-client.js";

describe("WorkloadLeafClient defaults - .NET parity pins", () => {
  it("refresh margin mirrors .NET WorkloadLeafRefreshLeadTime (5 min)", () => {
    expect(DEFAULT_REFRESH_MARGIN_MS).toBe(5 * 60 * 1000);
  });

  it("circuit failure threshold mirrors .NET FAILURE_THRESHOLD (5)", () => {
    expect(DEFAULT_FAILURE_THRESHOLD).toBe(5);
  });

  it("circuit cooldown mirrors .NET SR_CooldownDuration (30 s)", () => {
    expect(DEFAULT_COOLDOWN_MS).toBe(30 * 1000);
  });
});
