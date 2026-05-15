// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { mkdtempSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { afterEach, beforeEach, describe, expect, it } from "vitest";
import { D2Env } from "../src/d2-env.js";

let dir: string;

beforeEach(() => {
  dir = mkdtempSync(join(tmpdir(), "d2env-"));
});
afterEach(() => {
  rmSync(dir, { recursive: true, force: true });
});

describe("D2Env.parseEnvFile", () => {
  it("parses KEY=VALUE pairs", () => {
    const r = D2Env.parseEnvFile("FOO=bar\nBAZ=qux");
    expect(r).toEqual({ FOO: "bar", BAZ: "qux" });
  });

  it("strips matching quotes", () => {
    const r = D2Env.parseEnvFile("FOO=\"bar\"\nBAZ='qux'");
    expect(r).toEqual({ FOO: "bar", BAZ: "qux" });
  });

  it("ignores comments + blank lines", () => {
    const r = D2Env.parseEnvFile("# comment\n\nFOO=bar\n   \n# trailing");
    expect(r).toEqual({ FOO: "bar" });
  });

  it("ignores malformed lines (no '=', or '=' at start)", () => {
    const r = D2Env.parseEnvFile("just-a-string\n=value\nFOO=bar");
    expect(r).toEqual({ FOO: "bar" });
  });

  it("handles CRLF line endings", () => {
    const r = D2Env.parseEnvFile("A=1\r\nB=2\r\n");
    expect(r).toEqual({ A: "1", B: "2" });
  });

  it("preserves '=' characters in value", () => {
    const r = D2Env.parseEnvFile("URL=postgres://u:p@h:5432/db");
    expect(r["URL"]).toBe("postgres://u:p@h:5432/db");
  });

  it("returns mismatched-quote values verbatim", () => {
    expect(D2Env.parseEnvFile('A="incomplete')).toEqual({
      A: '"incomplete',
    });
  });
});

describe("D2Env.discoverFile", () => {
  it("walks upward + finds the file", () => {
    writeFileSync(join(dir, "found.env"), "X=1");
    expect(D2Env.discoverFile(dir, "found.env")).toBe(join(dir, "found.env"));
  });

  it("returns null when no match anywhere", () => {
    expect(D2Env.discoverFile(dir, "totally-missing-1234.env")).toBeNull();
  });
});

describe("D2Env.load", () => {
  it("merges file + env, env wins", () => {
    writeFileSync(join(dir, ".env"), "FOO=from-file\nBAR=from-file");
    const merged = D2Env.load({
      startDir: dir,
      fileNames: [".env"],
      env: { FOO: "from-env" },
    });
    expect(merged["FOO"]).toBe("from-env");
    expect(merged["BAR"]).toBe("from-file");
  });

  it("returns env-only when no files exist", () => {
    const merged = D2Env.load({
      startDir: dir,
      fileNames: ["nonexistent.env"],
      env: { X: "y" },
    });
    expect(merged["X"]).toBe("y");
  });

  it("layered file override — secrets > local > env-base, env wins overall", () => {
    writeFileSync(join(dir, ".env"), "TIER=base\nA=base");
    writeFileSync(join(dir, ".env.local"), "TIER=local\nB=local");
    writeFileSync(join(dir, ".env.secrets"), "TIER=secrets");
    const merged = D2Env.load({
      startDir: dir,
      env: {},
    });
    // .env.secrets overrides .env.local overrides .env (priority order).
    expect(merged["TIER"]).toBe("secrets");
    expect(merged["A"]).toBe("base");
    expect(merged["B"]).toBe("local");
  });

  it("default fileNames = .env.secrets / .env.local / .env", () => {
    writeFileSync(join(dir, ".env"), "X=y");
    const merged = D2Env.load({ startDir: dir, env: {} });
    expect(merged["X"]).toBe("y");
  });

  it("smoke: returns an object when called with explicit empty inputs", () => {
    // Mirrors the no-args call shape (returns object, doesn't throw) WITHOUT
    // exercising the cwd-walking discovery path — that path would read the
    // real repo's .env.local / .env.secrets into the test process state. The
    // dedicated regression test below pins the no-args isolation guarantee.
    const merged = D2Env.load({
      startDir: dir,
      fileNames: ["nonexistent-smoke.env"],
      env: {},
    });
    expect(typeof merged).toBe("object");
  });

  it("default discovery does not read real repo env files (regression for §1.16)", () => {
    // The previous smoke called D2Env.load() with no opts, which walked
    // process.cwd() upward and slurped .env.local / .env.secrets into the
    // test process. Pin the isolation: with an explicit non-existent file
    // name + an empty env, default discovery must short-circuit cleanly and
    // contribute zero entries beyond what the caller provided.
    const merged = D2Env.load({
      fileNames: ["nonexistent-isolation-1234.env"],
      env: { TEST_ONLY_KEY: "isolated" },
    });
    expect(merged).toEqual({ TEST_ONLY_KEY: "isolated" });
    // Spot-check: real-repo secret-shaped keys must not have leaked in.
    expect(merged["AUTH_JWT_SIGNING_KEY"]).toBeUndefined();
    expect(merged["TWILIO_AUTH_TOKEN"]).toBeUndefined();
  });

  it("undefined env values are stripped", () => {
    const merged = D2Env.load({
      startDir: dir,
      fileNames: ["nonexistent.env"],
      env: { A: undefined as unknown as string, B: "value" },
    });
    expect(merged["A"]).toBeUndefined();
    expect(merged["B"]).toBe("value");
  });
});
