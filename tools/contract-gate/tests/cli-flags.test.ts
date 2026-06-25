// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// CLI flag validation tests — mutual exclusion and --help.
//
// Invokes the CLI via `tsx src/cli.ts` in a subprocess to avoid importing
// the module-level side-effects (process.argv capture, process.exit).
// Each test targets a specific startup-validation path that should exit
// before reaching any git or buf IO.
//
// Note: these tests exercise the contract-gate CLI's flag-parsing and
// startup-validation code paths only. They do not test git or buf invocation.

import { spawnSync } from "node:child_process";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { describe, expect, it } from "vitest";

const __dirname = dirname(fileURLToPath(import.meta.url));
const CLI_PATH = resolve(__dirname, "../src/cli.ts");

function runCli(args: string[]): {
  status: number | null;
  stdout: string;
  stderr: string;
} {
  const result = spawnSync(
    process.execPath,
    ["--import", "tsx/esm", CLI_PATH, ...args],
    {
      encoding: "utf-8",
      env: { ...process.env, NODE_NO_WARNINGS: "1" },
    },
  );

  return {
    status: result.status,
    stdout: result.stdout ?? "",
    stderr: result.stderr ?? "",
  };
}

// ---------------------------------------------------------------------------
// --help flag
// ---------------------------------------------------------------------------

describe("contract-gate CLI — --help", () => {
  it("--help exits with code 0", () => {
    const result = runCli(["--help"]);
    expect(result.status).toBe(0);
  });

  it("-h exits with code 0", () => {
    const result = runCli(["-h"]);
    expect(result.status).toBe(0);
  });

  it("--help prints flag descriptions to stdout", () => {
    const result = runCli(["--help"]);
    expect(result.stdout).toContain("--against");
    expect(result.stdout).toContain("--proto-only");
    expect(result.stdout).toContain("--json-only");
  });

  it("--help prints exit code documentation to stdout", () => {
    const result = runCli(["--help"]);
    expect(result.stdout).toContain("Exit codes");
  });
});

// ---------------------------------------------------------------------------
// --proto-only + --json-only mutual exclusion
// ---------------------------------------------------------------------------

describe("contract-gate CLI — --proto-only and --json-only are mutually exclusive", () => {
  it("exits nonzero when both --proto-only and --json-only are passed", () => {
    const result = runCli(["--against", "nova", "--proto-only", "--json-only"]);
    expect(result.status).not.toBe(0);
  });

  it("writes an error message to stderr on contradictory flag pair", () => {
    const result = runCli(["--against", "nova", "--proto-only", "--json-only"]);
    expect(result.stderr).toMatch(/mutually exclusive/i);
  });

  it("exits with code 2 on contradictory flag pair (internal error class)", () => {
    const result = runCli(["--against", "nova", "--proto-only", "--json-only"]);
    expect(result.status).toBe(2);
  });
});

// ---------------------------------------------------------------------------
// --skip-proto + --skip-json no-op guard (E1-NEW-M-1)
// ---------------------------------------------------------------------------

describe("contract-gate CLI — --skip-proto and --skip-json leave no arm to run", () => {
  it("exits nonzero when both --skip-proto and --skip-json are passed", () => {
    const result = runCli(["--against", "nova", "--skip-proto", "--skip-json"]);
    expect(result.status).not.toBe(0);
  });

  it("writes an error message to stderr when both skip flags are set", () => {
    const result = runCli(["--against", "nova", "--skip-proto", "--skip-json"]);
    expect(result.stderr).toMatch(/mutually exclusive/i);
  });

  it("exits with code 2 when both skip flags are set (internal error class)", () => {
    const result = runCli(["--against", "nova", "--skip-proto", "--skip-json"]);
    expect(result.status).toBe(2);
  });
});
