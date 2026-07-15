// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { existsSync, readdirSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { describe, expect, it } from "vitest";

const here = dirname(fileURLToPath(import.meta.url));

function findRepoRoot(): string {
  // Walk up from this test file looking for the repo root marker
  // (a directory containing contracts/protos/).
  let dir = here;
  for (let i = 0; i < 12; i++) {
    if (existsSync(join(dir, "contracts", "protos"))) return dir;
    const parent = resolve(dir, "..");
    if (parent === dir) break;
    dir = parent;
  }
  throw new Error(
    "could not locate repo root (expected contracts/protos/ ancestor)",
  );
}

const repoRoot = findRepoRoot();

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
