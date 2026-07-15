// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

import { existsSync } from "node:fs";
import { describe, expect, it } from "vitest";
import { contractsPath, REPO_ROOT, tsPackagePath } from "../src/lib/paths.js";

describe("paths", () => {
  it("REPO_ROOT resolves to monorepo root with public/contracts", () => {
    expect(existsSync(REPO_ROOT)).toBe(true);
    expect(existsSync(`${REPO_ROOT}/public/contracts`)).toBe(true);
    expect(existsSync(`${REPO_ROOT}/D2.slnx`)).toBe(true);
  });
  it("contractsPath joins parts", () => {
    const p = contractsPath("auth-context", "IAuthContext.spec.json");
    expect(p).toContain("contracts");
    expect(p).toContain("IAuthContext.spec.json");
    expect(existsSync(p)).toBe(true);
  });
  it("tsPackagePath joins parts under public/packages/typescript/<pkg>", () => {
    const p = tsPackagePath("utilities", "package.json");
    expect(p).toContain("packages");
    expect(p).toContain("typescript");
    expect(p).toContain("utilities");
    expect(existsSync(p)).toBe(true);
  });
});
