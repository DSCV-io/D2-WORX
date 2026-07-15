// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import { emitAuthScopes, type ScopesSpec } from "../src/auth-scopes-emit.js";

const spec: ScopesSpec = {
  scopes: [
    {
      name: "self.read",
      actionSensitivity: "Routine",
      impersonationBlocked: false,
      description: "Self read.",
    },
    {
      name: "auth.user.impersonate.consent",
      actionSensitivity: "Sensitive",
      impersonationBlocked: true,
      description: "Consent imp.",
    },
  ],
};

describe("emitAuthScopes", () => {
  it("emits nested-tree constants", () => {
    const r = emitAuthScopes(spec);
    expect(r.diagnostics).toEqual([]);
    expect(r.source).toContain("export const Scopes =");
    expect(r.source).toContain("auth: {");
    expect(r.source).toContain("user: {");
    expect(r.source).toContain("impersonate: {");
    expect(r.source).toContain('consent: "auth.user.impersonate.consent"');
    expect(r.source).toContain("self: {");
    expect(r.source).toContain('read: "self.read"');
  });

  it("emits ALL_SCOPES array", () => {
    const r = emitAuthScopes(spec);
    expect(r.source).toContain("export const ALL_SCOPES");
    expect(r.source).toContain('"self.read"');
    expect(r.source).toContain('"auth.user.impersonate.consent"');
  });

  it("flags duplicate scope names", () => {
    const r = emitAuthScopes({
      scopes: [spec.scopes[0]!, spec.scopes[0]!],
    });
    expect(r.diagnostics[0]?.id).toBe("D2SCP001");
  });

  it("flags malformed scope name", () => {
    const r = emitAuthScopes({
      scopes: [{ ...spec.scopes[0]!, name: "Self.Read" }],
    });
    expect(r.diagnostics[0]?.id).toBe("D2SCP002");
  });

  it("flags invalid sensitivity", () => {
    const r = emitAuthScopes({
      scopes: [{ ...spec.scopes[0]!, actionSensitivity: "Mild" }],
    });
    expect(r.diagnostics[0]?.id).toBe("D2SCP003");
  });
});
