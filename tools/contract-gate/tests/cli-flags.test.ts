// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// CLI flag validation tests — mutual exclusion, arm suppression, and --help.
//
// Invokes the CLI via `tsx src/cli.ts` in a subprocess to avoid importing
// the module-level side-effects (process.argv capture, process.exit).
//
// Test groups:
//   - Guard tests: mutual-exclusion pairs caught before any git IO (exit 2
//     at the guard step).
//   - Arm-isolation tests: verify that arm-suppression flags prevent the
//     correct section header from appearing in stdout. These tests pass
//     --repo-root pointing to the real git root so the CLI proceeds past
//     the .git check; they assert on stdout section header presence/absence
//     and are indifferent to the ultimate exit code.

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
// --skip-proto + --skip-json mutual exclusion (leave no arm running)
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

// ---------------------------------------------------------------------------
// --json-only + --skip-json mutual exclusion (leave no arm running)
// ---------------------------------------------------------------------------

describe("contract-gate CLI — --json-only and --skip-json leave no arm to run", () => {
  it("exits nonzero when both --json-only and --skip-json are passed", () => {
    const result = runCli(["--against", "nova", "--json-only", "--skip-json"]);
    expect(result.status).not.toBe(0);
  });

  it("writes an error message to stderr when --json-only and --skip-json are both set", () => {
    const result = runCli(["--against", "nova", "--json-only", "--skip-json"]);
    expect(result.stderr).toMatch(/mutually exclusive/i);
  });

  it("exits with code 2 when --json-only + --skip-json are both set", () => {
    const result = runCli(["--against", "nova", "--json-only", "--skip-json"]);
    expect(result.status).toBe(2);
  });
});

// ---------------------------------------------------------------------------
// --proto-only + --skip-proto mutual exclusion (leave no arm running)
// ---------------------------------------------------------------------------

describe("contract-gate CLI — --proto-only and --skip-proto leave no arm to run", () => {
  it("exits nonzero when both --proto-only and --skip-proto are passed", () => {
    const result = runCli([
      "--against",
      "nova",
      "--proto-only",
      "--skip-proto",
    ]);
    expect(result.status).not.toBe(0);
  });

  it("writes an error message to stderr when --proto-only and --skip-proto are both set", () => {
    const result = runCli([
      "--against",
      "nova",
      "--proto-only",
      "--skip-proto",
    ]);
    expect(result.stderr).toMatch(/mutually exclusive/i);
  });

  it("exits with code 2 when --proto-only + --skip-proto are both set", () => {
    const result = runCli([
      "--against",
      "nova",
      "--proto-only",
      "--skip-proto",
    ]);
    expect(result.status).toBe(2);
  });
});

// ---------------------------------------------------------------------------
// Arm-suppression isolation tests
//
// These tests verify that arm-suppression flags suppress exactly the correct
// arm. They pass --repo-root pointing to the real git root so the CLI
// proceeds past the .git existence check; the assertion is on whether the
// arm's section header appears (or not) in stdout.
//
// Section headers (from printSection() in cli.ts):
//   Proto arm   : "Proto arm (buf breaking — FILE level)"
//   JSON arm    : "Spec/i18n/OpenAPI arm (JSON-diff gate)"
// ---------------------------------------------------------------------------

const REPO_ROOT = resolve(__dirname, "../../..");

describe("contract-gate CLI — --skip-proto suppresses the proto arm", () => {
  it("omits the proto arm section header from stdout when --skip-proto is passed", () => {
    const result = runCli([
      "--against",
      "nova",
      "--skip-proto",
      "--repo-root",
      REPO_ROOT,
    ]);

    expect(result.stdout).not.toContain("Proto arm (buf breaking");
  });

  it("includes the JSON arm section header in stdout when --skip-proto is passed", () => {
    const result = runCli([
      "--against",
      "nova",
      "--skip-proto",
      "--repo-root",
      REPO_ROOT,
    ]);

    expect(result.stdout).toContain("Spec/i18n/OpenAPI arm");
  });
});

describe("contract-gate CLI — --json-only suppresses the proto arm", () => {
  it("omits the proto arm section header from stdout when --json-only is passed", () => {
    const result = runCli([
      "--against",
      "nova",
      "--json-only",
      "--repo-root",
      REPO_ROOT,
    ]);

    expect(result.stdout).not.toContain("Proto arm (buf breaking");
  });

  it("includes the JSON arm section header in stdout when --json-only is passed", () => {
    const result = runCli([
      "--against",
      "nova",
      "--json-only",
      "--repo-root",
      REPO_ROOT,
    ]);

    expect(result.stdout).toContain("Spec/i18n/OpenAPI arm");
  });
});

// The --proto-only and --skip-json tests run the proto arm (buf breaking over
// the repo), which can take 15–25 s. Each test carries an explicit 30 s timeout.
describe("contract-gate CLI — --proto-only suppresses the JSON arm", () => {
  it("omits the JSON arm section header from stdout when --proto-only is passed", () => {
    const result = runCli([
      "--against",
      "nova",
      "--proto-only",
      "--repo-root",
      REPO_ROOT,
    ]);

    expect(result.stdout).not.toContain("Spec/i18n/OpenAPI arm");
  }, 30_000);

  it("includes the proto arm section header in stdout when --proto-only is passed", () => {
    const result = runCli([
      "--against",
      "nova",
      "--proto-only",
      "--repo-root",
      REPO_ROOT,
    ]);

    expect(result.stdout).toContain("Proto arm (buf breaking");
  }, 30_000);
});

describe("contract-gate CLI — --skip-json suppresses the JSON arm", () => {
  it("omits the JSON arm section header from stdout when --skip-json is passed", () => {
    const result = runCli([
      "--against",
      "nova",
      "--skip-json",
      "--repo-root",
      REPO_ROOT,
    ]);

    expect(result.stdout).not.toContain("Spec/i18n/OpenAPI arm");
  }, 30_000);

  it("includes the proto arm section header in stdout when --skip-json is passed", () => {
    const result = runCli([
      "--against",
      "nova",
      "--skip-json",
      "--repo-root",
      REPO_ROOT,
    ]);

    expect(result.stdout).toContain("Proto arm (buf breaking");
  }, 30_000);
});
