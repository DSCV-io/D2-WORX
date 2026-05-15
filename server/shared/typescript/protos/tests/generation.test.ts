// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { existsSync, readdirSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { describe, expect, it } from "vitest";

const here = dirname(fileURLToPath(import.meta.url));
const repoRoot = join(here, "..", "..", "..", "..", "..");

describe("@d2/protos — generation surface", () => {
  it("contracts/protos/ exists at repo root", () => {
    const protosDir = join(repoRoot, "contracts", "protos");
    expect(existsSync(protosDir)).toBe(true);
  });

  it("buf.gen.yaml is present in this package", () => {
    expect(existsSync(join(here, "..", "buf.gen.yaml"))).toBe(true);
  });

  it("expected proto files exist in source tree", () => {
    const commonV1 = join(repoRoot, "contracts", "protos", "common", "v1");
    expect(existsSync(commonV1)).toBe(true);
    const files = readdirSync(commonV1);
    expect(files).toContain("d2_result.proto");
    expect(files).toContain("health.proto");
    expect(files).toContain("ping.proto");
    expect(files).toContain("jobs.proto");
  });
});
