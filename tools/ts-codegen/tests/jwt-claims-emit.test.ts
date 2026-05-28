// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";

import {
  emitJwtClaims,
  emitJwtPayload,
  type JwtClaimsSpec,
  validateJwtClaimsSpec,
} from "../src/jwt-claims-emit.js";

const validSpec: JwtClaimsSpec = {
  claims: [
    {
      constName: "SUB",
      value: "sub",
      kind: "standard",
      description: "Subject.",
    },
    {
      constName: "SESSION_ID",
      value: "d2_session_id",
      kind: "d2-custom",
      description: "User session id.",
    },
    {
      constName: "ACT_KIND",
      value: "d2_kind",
      kind: "inside-act",
      description: "Impersonation flavor.",
    },
  ],
};

describe("validateJwtClaimsSpec", () => {
  it("happy path returns all entries with no diagnostics", () => {
    const v = validateJwtClaimsSpec(validSpec);
    expect(v.entries).toHaveLength(3);
    expect(v.diagnostics).toEqual([]);
  });

  it("flags invalid constName (not UPPER_SNAKE_CASE)", () => {
    const v = validateJwtClaimsSpec({
      claims: [{ ...validSpec.claims[0]!, constName: "lowercase" }],
    });
    expect(v.diagnostics[0]?.id).toBe("D2JWT003");
  });

  it("flags unknown kind", () => {
    const v = validateJwtClaimsSpec({
      claims: [{ ...validSpec.claims[0]!, kind: "weird" as never }],
    });
    expect(v.diagnostics[0]?.id).toBe("D2JWT002");
  });

  it("flags duplicate constName", () => {
    const v = validateJwtClaimsSpec({
      claims: [validSpec.claims[0]!, validSpec.claims[0]!],
    });
    expect(v.diagnostics.some((d) => d.id === "D2JWT004")).toBe(true);
  });

  it("ALLOWS duplicate values across different kind buckets", () => {
    // SESSION_ID and ACT_SESSION_ID both have value "d2_session_id" — the
    // production spec relies on this; lookup paths differ (top-level vs
    // nested under act).
    const v = validateJwtClaimsSpec({
      claims: [
        validSpec.claims[1]!,
        {
          constName: "ACT_SESSION_ID",
          value: "d2_session_id",
          kind: "inside-act",
          description: "Inside-act session id.",
        },
      ],
    });
    expect(v.diagnostics).toEqual([]);
    expect(v.entries).toHaveLength(2);
  });
});

describe("emitJwtClaims — snapshot pin", () => {
  it("emits JwtClaimTypes constants in spec order", () => {
    const r = emitJwtClaims(validSpec);
    expect(r.diagnostics).toEqual([]);
    expect(r.source).toContain('SUB: "sub"');
    expect(r.source).toContain('SESSION_ID: "d2_session_id"');
    expect(r.source).toContain('ACT_KIND: "d2_kind"');
    expect(r.source).toContain("export const JwtClaimTypes =");
    expect(r.source).toContain(
      "export type JwtClaimType = (typeof JwtClaimTypes)[keyof typeof JwtClaimTypes];",
    );
  });

  it("returns empty source on validation error", () => {
    const r = emitJwtClaims({
      claims: [{ ...validSpec.claims[0]!, constName: "bad-name" }],
    });
    expect(r.source).toBe("");
    expect(r.diagnostics.some((d) => d.id === "D2JWT003")).toBe(true);
  });
});

describe("emitJwtPayload", () => {
  // Production-shape fixture mirroring the contracts/jwt-claims spec — all
  // 17 entries (8 standard + 7 d2-custom + 2 inside-act) so the per-VALUE
  // pin tests assert against realistic data.
  const productionLikeSpec: JwtClaimsSpec = {
    claims: [
      { constName: "SUB", value: "sub", kind: "standard", description: "S." },
      { constName: "AUD", value: "aud", kind: "standard", description: "A." },
      { constName: "IAT", value: "iat", kind: "standard", description: "I." },
      { constName: "EXP", value: "exp", kind: "standard", description: "E." },
      { constName: "AZP", value: "azp", kind: "standard", description: "Z." },
      {
        constName: "SCOPE",
        value: "scope",
        kind: "standard",
        description: "Sc.",
      },
      {
        constName: "ACT",
        value: "act",
        kind: "standard",
        description: "Act.",
      },
      {
        constName: "CLIENT_ID",
        value: "client_id",
        kind: "standard",
        description: "Client id.",
      },
      {
        constName: "SESSION_ID",
        value: "d2_session_id",
        kind: "d2-custom",
        description: "Session id.",
      },
      {
        constName: "USERNAME",
        value: "d2_username",
        kind: "d2-custom",
        description: "Username.",
      },
      {
        constName: "FINGERPRINT",
        value: "d2_fp",
        kind: "d2-custom",
        description: "Fingerprint.",
      },
      {
        constName: "ORG_ID",
        value: "d2_org_id",
        kind: "d2-custom",
        description: "Org id.",
      },
      {
        constName: "ORG_NAME",
        value: "d2_org_name",
        kind: "d2-custom",
        description: "Org name.",
      },
      {
        constName: "ORG_TYPE",
        value: "d2_org_type",
        kind: "d2-custom",
        description: "Org type.",
      },
      {
        constName: "ORG_ROLE",
        value: "d2_org_role",
        kind: "d2-custom",
        description: "Org role.",
      },
      {
        constName: "ACT_KIND",
        value: "d2_kind",
        kind: "inside-act",
        description: "Act kind.",
      },
      {
        constName: "ACT_SESSION_ID",
        value: "d2_session_id",
        kind: "inside-act",
        description: "Act session id.",
      },
    ],
  };

  it("emits a JwtPayload interface declaration", () => {
    const r = emitJwtPayload(productionLikeSpec);
    expect(r.diagnostics).toEqual([]);
    expect(r.source).toContain("export interface JwtPayload {");
  });

  it("emits each standard claim with its canonical TS type (per-VALUE pin)", () => {
    const r = emitJwtPayload(productionLikeSpec);
    // Per rules.md §6.15: optional claims use `?:` shorthand (no `| null`).
    // `aud` is the lone required claim (always populated as readonly string[]).
    expect(r.source).toContain("readonly sub?: string;");
    expect(r.source).toContain("readonly aud: readonly string[];");
    expect(r.source).toContain("readonly iat?: number;");
    expect(r.source).toContain("readonly exp?: number;");
    expect(r.source).toContain("readonly azp?: string;");
    expect(r.source).toContain("readonly scope?: string;");
    expect(r.source).toContain(
      "readonly act?: Readonly<Record<string, unknown>>;",
    );
    expect(r.source).toContain("readonly client_id?: string;");
  });

  it("emits each d2-custom claim with the default optional string type (per-VALUE pin)", () => {
    const r = emitJwtPayload(productionLikeSpec);
    expect(r.source).toContain("readonly d2_session_id?: string;");
    expect(r.source).toContain("readonly d2_username?: string;");
    expect(r.source).toContain("readonly d2_fp?: string;");
    expect(r.source).toContain("readonly d2_org_id?: string;");
    expect(r.source).toContain("readonly d2_org_name?: string;");
    expect(r.source).toContain("readonly d2_org_type?: string;");
    expect(r.source).toContain("readonly d2_org_role?: string;");
  });

  it("does NOT emit inside-act claims as top-level fields", () => {
    const r = emitJwtPayload(productionLikeSpec);
    // ACT_KIND has wire value "d2_kind" — must NOT appear as a top-level field.
    expect(r.source).not.toContain("readonly d2_kind");
    // ACT_SESSION_ID's wire value "d2_session_id" collides with the
    // d2-custom SESSION_ID; the d2-custom emits, but the inside-act entry
    // must NOT cause a duplicate emission. Regex tolerates the `?:`
    // shorthand form (rules.md §6.15).
    const sessionIdCount = (r.source.match(/readonly d2_session_id\??:/g) ?? [])
      .length;
    expect(sessionIdCount).toBe(1);
  });

  it("emits a trailing raw escape-hatch field", () => {
    const r = emitJwtPayload(productionLikeSpec);
    expect(r.source).toContain(
      "readonly raw: Readonly<Record<string, unknown>>;",
    );
  });

  it("includes JSDoc with the wire claim name and kind", () => {
    const r = emitJwtPayload(productionLikeSpec);
    expect(r.source).toContain("Claim wire name: sub (kind: standard).");
    expect(r.source).toContain(
      "Claim wire name: d2_session_id (kind: d2-custom).",
    );
  });

  it("returns empty source on validation error", () => {
    const r = emitJwtPayload({
      claims: [{ ...productionLikeSpec.claims[0]!, constName: "bad-name" }],
    });
    expect(r.source).toBe("");
    expect(r.diagnostics.some((d) => d.id === "D2JWT003")).toBe(true);
  });

  it("handles spec with only inside-act entries (interface with raw only)", () => {
    const r = emitJwtPayload({
      claims: [
        {
          constName: "ACT_KIND",
          value: "d2_kind",
          kind: "inside-act",
          description: "Act kind.",
        },
      ],
    });
    expect(r.diagnostics).toEqual([]);
    expect(r.source).toContain("export interface JwtPayload {");
    expect(r.source).toContain(
      "readonly raw: Readonly<Record<string, unknown>>;",
    );
    expect(r.source).not.toContain("readonly d2_kind:");
  });

  it("d2-custom claim NOT in standard table defaults to optional string", () => {
    const r = emitJwtPayload({
      claims: [
        {
          constName: "NEW_CUSTOM",
          value: "d2_new_custom",
          kind: "d2-custom",
          description: "Hypothetical new claim.",
        },
      ],
    });
    expect(r.diagnostics).toEqual([]);
    expect(r.source).toContain("readonly d2_new_custom?: string;");
  });
});
