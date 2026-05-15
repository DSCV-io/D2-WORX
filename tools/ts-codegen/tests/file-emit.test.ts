// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import {
  mkdtempSync,
  readFileSync,
  rmSync,
  utimesSync,
  writeFileSync,
} from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { afterEach, beforeEach, describe, expect, it } from "vitest";
import {
  buildHeader,
  isOutputUpToDate,
  writeGeneratedFile,
} from "../src/lib/file-emit.js";

let dir: string;

beforeEach(() => {
  dir = mkdtempSync(join(tmpdir(), "file-emit-"));
});
afterEach(() => {
  rmSync(dir, { recursive: true, force: true });
});

describe("buildHeader", () => {
  it("includes the spec relative path", () => {
    const h = buildHeader("contracts/auth-context/IAuthContext.spec.json");
    expect(h).toContain("auto-generated");
    expect(h).toContain("contracts/auth-context/IAuthContext.spec.json");
  });
});

describe("writeGeneratedFile", () => {
  it("writes to a new file (returns true)", () => {
    const target = join(dir, "out.g.ts");
    expect(writeGeneratedFile(target, "hello")).toBe(true);
    expect(readFileSync(target, "utf8")).toBe("hello");
  });

  it("returns false when content matches existing (no write)", () => {
    const target = join(dir, "out.g.ts");
    writeGeneratedFile(target, "hello");
    expect(writeGeneratedFile(target, "hello")).toBe(false);
  });

  it("overwrites differing content", () => {
    const target = join(dir, "out.g.ts");
    writeGeneratedFile(target, "v1");
    expect(writeGeneratedFile(target, "v2")).toBe(true);
    expect(readFileSync(target, "utf8")).toBe("v2");
  });

  it("creates intermediate directories", () => {
    const target = join(dir, "deep", "nested", "out.g.ts");
    writeGeneratedFile(target, "x");
    expect(readFileSync(target, "utf8")).toBe("x");
  });
});

describe("isOutputUpToDate", () => {
  it("false when target missing", () => {
    expect(isOutputUpToDate(join(dir, "missing"), [])).toBe(false);
  });

  it("true when target newer than every source", () => {
    const src = join(dir, "src");
    const out = join(dir, "out");
    writeFileSync(src, "1");
    writeFileSync(out, "2");
    const past = new Date(Date.now() - 5_000);
    utimesSync(src, past, past);
    const future = new Date(Date.now() + 1_000);
    utimesSync(out, future, future);
    expect(isOutputUpToDate(out, [src])).toBe(true);
  });

  it("false when any source newer than target", () => {
    const src = join(dir, "src");
    const out = join(dir, "out");
    writeFileSync(out, "old");
    const past = new Date(Date.now() - 5_000);
    utimesSync(out, past, past);
    writeFileSync(src, "new");
    expect(isOutputUpToDate(out, [src])).toBe(false);
  });

  it("false when source missing", () => {
    const out = join(dir, "out");
    writeFileSync(out, "x");
    expect(isOutputUpToDate(out, [join(dir, "missing")])).toBe(false);
  });
});
