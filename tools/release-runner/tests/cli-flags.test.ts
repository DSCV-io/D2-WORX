// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// CLI flag validation tests — mutual exclusion and --help.
//
// Invokes the CLI via `tsx src/cli.ts` in a subprocess to avoid importing
// the module-level side-effects (process.argv capture, process.exit).
// Each test targets a specific startup-validation path that should exit
// before reaching any git or filesystem IO.
//
// Note: these tests exercise the release-runner CLI's flag-parsing and
// startup-validation code paths only. They do not test git or file IO.

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

describe("release-runner CLI — --help", () => {
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
    expect(result.stdout).toContain("--apply");
    expect(result.stdout).toContain("--graduate");
    expect(result.stdout).toContain("--list");
  });

  it("--help prints exit code documentation to stdout", () => {
    const result = runCli(["--help"]);
    expect(result.stdout).toContain("Exit codes");
  });
});

// ---------------------------------------------------------------------------
// --list + --apply mutual exclusion
// ---------------------------------------------------------------------------

describe("release-runner CLI — --list and --apply are mutually exclusive", () => {
  it("exits with code 1 when both --list and --apply are passed", () => {
    const result = runCli(["--list", "--apply"]);
    expect(result.status).toBe(1);
  });

  it("writes an error message to stderr when --list and --apply are combined", () => {
    const result = runCli(["--list", "--apply"]);
    expect(result.stderr).toMatch(/mutually exclusive/i);
  });
});

// ---------------------------------------------------------------------------
// --list + --graduate mutual exclusion
// ---------------------------------------------------------------------------

describe("release-runner CLI — --list and --graduate are mutually exclusive", () => {
  it("exits with code 1 when both --list and --graduate are passed", () => {
    const result = runCli(["--list", "--graduate", "some-pkg"]);
    expect(result.status).toBe(1);
  });

  it("writes an error message to stderr when --list and --graduate are combined", () => {
    const result = runCli(["--list", "--graduate", "some-pkg"]);
    expect(result.stderr).toMatch(/mutually exclusive/i);
  });
});

// ---------------------------------------------------------------------------
// --legacy-commit-type flag
// ---------------------------------------------------------------------------

describe("release-runner CLI — --legacy-commit-type flag", () => {
  it("--help documents --legacy-commit-type", () => {
    const result = runCli(["--help"]);
    expect(result.stdout).toContain("--legacy-commit-type");
  });

  it("--legacy-commit-type is accepted without error when combined with required --against", () => {
    // Pass an invalid ref to exit early (after flag parsing). We only care that
    // --legacy-commit-type itself does not cause an argument-parse failure.
    const result = runCli([
      "--legacy-commit-type",
      "--against",
      "invalid-ref-xyz",
    ]);
    expect(result.stderr).not.toMatch(/unknown.*legacy-commit-type/i);
    expect(result.stderr).not.toMatch(/legacy-commit-type.*unknown/i);
  });
});

// ---------------------------------------------------------------------------
// --no-propagate flag
// ---------------------------------------------------------------------------

describe("release-runner CLI — --no-propagate flag", () => {
  it("--help documents --no-propagate", () => {
    const result = runCli(["--help"]);
    expect(result.stdout).toContain("--no-propagate");
  });

  it("--no-propagate is accepted without error when combined with required --against", () => {
    // Pass an invalid ref to exit early (after flag parsing) — we only care
    // that --no-propagate itself does not cause an argument-parse failure.
    // The CLI exits non-zero due to the invalid ref, not due to --no-propagate.
    const result = runCli(["--no-propagate", "--against", "invalid-ref-xyz"]);
    // The exit should not be caused by an "unknown flag" rejection.
    // A valid parse path would reach ref validation and fail there (exit 1).
    // We assert the stderr does NOT mention "--no-propagate" as unknown.
    expect(result.stderr).not.toMatch(/unknown.*no-propagate/i);
    expect(result.stderr).not.toMatch(/no-propagate.*unknown/i);
  });
});
