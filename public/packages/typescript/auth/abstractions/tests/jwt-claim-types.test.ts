// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import { JwtClaimTypes } from "../src/jwt-claim-types.g.js";

describe("JwtClaimTypes — per-VALUE pin (mirrors .NET constants)", () => {
  it.each([
    ["SUB", "sub"],
    ["AUD", "aud"],
    ["IAT", "iat"],
    ["EXP", "exp"],
    ["AZP", "azp"],
    ["SCOPE", "scope"],
    ["ACT", "act"],
    ["CLIENT_ID", "client_id"],
    ["AMR", "amr"],
    ["SESSION_ID", "d2_session_id"],
    ["USERNAME", "d2_username"],
    ["FINGERPRINT", "d2_fp"],
    ["ORG_ID", "d2_org_id"],
    ["ORG_NAME", "d2_org_name"],
    ["ORG_TYPE", "d2_org_type"],
    ["ORG_ROLE", "d2_org_role"],
    ["ACT_KIND", "d2_kind"],
    ["ACT_SESSION_ID", "d2_session_id"],
    ["STEP_UP_AT", "d2_step_up_at"],
  ])("JwtClaimTypes.%s = %s", (key, value) => {
    expect(JwtClaimTypes[key as keyof typeof JwtClaimTypes]).toBe(value);
  });
});
