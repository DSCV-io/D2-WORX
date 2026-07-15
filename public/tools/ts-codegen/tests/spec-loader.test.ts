// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

import { mkdtempSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { afterEach, beforeEach, describe, expect, it } from "vitest";
import { loadSpec } from "../src/lib/spec-loader.js";

let dir: string;

beforeEach(() => {
  dir = mkdtempSync(join(tmpdir(), "spec-loader-"));
});

afterEach(() => {
  rmSync(dir, { recursive: true, force: true });
});

describe("loadSpec", () => {
  it("loads valid JSON", () => {
    const path = join(dir, "spec.json");
    writeFileSync(path, '{"hello":"world"}');
    const r = loadSpec<{ hello: string }>(path, "D2X");
    expect(r.spec).toEqual({ hello: "world" });
    expect(r.diagnostics).toEqual([]);
  });

  it("surfaces missing-file as MALFORMED_SPEC diagnostic", () => {
    const r = loadSpec(join(dir, "missing.json"), "D2X");
    expect(r.spec).toBeUndefined();
    expect(r.diagnostics).toHaveLength(1);
    expect(r.diagnostics[0]?.id).toBe("D2X");
  });

  it("surfaces malformed JSON as diagnostic", () => {
    const path = join(dir, "broken.json");
    writeFileSync(path, "{ not valid json");
    const r = loadSpec(path, "D2X");
    expect(r.spec).toBeUndefined();
    expect(r.diagnostics[0]?.message).toContain("parse");
  });
});
