// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Contract breaking-change gate CLI.
//
// Orchestrates all three gate arms (proto, spec/i18n, OpenAPI) over the
// baseline diff, reads the force valve via `parseBreakingFooters` over the
// PR's commits, and exits non-zero on an unforced break.
//
// Usage:
//   node dist/cli.js [--against <ref>] [--repo-root <path>]
//
// Flags:
//   --against <ref>      Baseline git ref (default: "nova")
//   --repo-root <path>   Repo root directory (default: cwd)
//   --proto-only         Run only the proto arm
//   --json-only          Run only the spec/i18n/openapi arms
//   --skip-proto         Skip the proto arm (run json arms only)
//   --skip-json          Skip the json arms (run proto arm only)
//
// Exit codes:
//   0  All arms pass (or all breaks are valve-forced).
//   1  At least one unforced break detected.
//   2  Internal error (bad config, git failure, etc.).

import { resolve } from "node:path";
import { existsSync } from "node:fs";

import { commitMessagesInRange } from "./git.js";
import { parseBreakingFooters } from "./footer-parser.js";
import { runProtoArm } from "./proto-arm.js";
import { runSpecGate } from "./run-spec-gate.js";
import type { BreakingFinding } from "./breaking-finding.js";

// ---------------------------------------------------------------------------
// Argument parsing (no dependency on a CLI framework — keep it minimal)
// ---------------------------------------------------------------------------

interface CliArgs {
  readonly baseRef: string;
  readonly repoRoot: string;
  readonly protoOnly: boolean;
  readonly jsonOnly: boolean;
  readonly skipProto: boolean;
  readonly skipJson: boolean;
}

function parseArgs(argv: string[]): CliArgs {
  let baseRef = "nova";
  let repoRoot = process.cwd();
  let protoOnly = false;
  let jsonOnly = false;
  let skipProto = false;
  let skipJson = false;

  for (let i = 0; i < argv.length; i++) {
    const arg = argv[i] ?? "";

    if (arg === "--against" && argv[i + 1] !== undefined) {
      baseRef = argv[++i]!;
    } else if (arg === "--repo-root" && argv[i + 1] !== undefined) {
      repoRoot = argv[++i]!;
    } else if (arg === "--proto-only") {
      protoOnly = true;
    } else if (arg === "--json-only") {
      jsonOnly = true;
    } else if (arg === "--skip-proto") {
      skipProto = true;
    } else if (arg === "--skip-json") {
      skipJson = true;
    }
  }

  return { baseRef, repoRoot, protoOnly, jsonOnly, skipProto, skipJson };
}

// ---------------------------------------------------------------------------
// Output helpers
// ---------------------------------------------------------------------------

function printSection(title: string): void {
  process.stdout.write(`\n${"─".repeat(60)}\n${title}\n${"─".repeat(60)}\n`);
}

function printFindings(
  findings: readonly BreakingFinding[],
  valveOpen: boolean,
): void {
  for (const f of findings) {
    process.stdout.write(`${f.message}\n`);
  }

  if (valveOpen && findings.length > 0) {
    process.stdout.write(
      `\n  ↑ Force valve is OPEN (WIRE-BREAKING:/BREAKING CHANGE: footer detected).\n` +
        `  The break above is ALLOWED for this PR — ensure semver MAJOR bump + CHANGELOG entry.\n`,
    );
  }
}

// ---------------------------------------------------------------------------
// Main
// ---------------------------------------------------------------------------

async function main(): Promise<void> {
  const args = parseArgs(process.argv.slice(2));
  const repoRoot = resolve(args.repoRoot);

  if (!existsSync(resolve(repoRoot, ".git"))) {
    process.stderr.write(
      `[contract-gate] error: '${repoRoot}' does not appear to be a git repository (.git not found)\n`,
    );
    process.exit(2);
  }

  // ── 1. Resolve the force valve ───────────────────────────────────────────
  let valveOpen = false;
  let valveSummary =
    "Force valve: CLOSED (no breaking footer detected in commit range)";

  try {
    const messages = commitMessagesInRange(args.baseRef, "HEAD");
    const valve = parseBreakingFooters(messages);
    valveOpen = valve.forced;

    if (valveOpen) {
      const parts: string[] = [];

      if (valve.wireBreaking.length > 0)
        parts.push(`WIRE-BREAKING: ${valve.wireBreaking.join(", ")}`);
      if (valve.apiBreaking.length > 0)
        parts.push(`BREAKING CHANGE: ${valve.apiBreaking.join(", ")}`);

      valveSummary = `Force valve: OPEN (${parts.join(" | ")})`;
    }
  } catch (err) {
    process.stderr.write(
      `[contract-gate] warning: could not read commit range (${String(err)}); treating valve as CLOSED\n`,
    );
  }

  printSection("Contract breaking-change gate");
  process.stdout.write(`Baseline ref : ${args.baseRef}\n`);
  process.stdout.write(`Repo root    : ${repoRoot}\n`);
  process.stdout.write(`${valveSummary}\n`);

  const allFindings: BreakingFinding[] = [];

  // ── 2. Proto arm ─────────────────────────────────────────────────────────
  const runProto = !args.jsonOnly && !args.skipProto;

  if (runProto) {
    printSection("Proto arm (buf breaking — FILE level)");

    const protoResult = runProtoArm({
      repoRoot,
      baseRef: args.baseRef,
      valveOpen,
    });

    if (protoResult.exemptPackages.length > 0) {
      process.stdout.write(
        `  Exempt (pre-stable): ${protoResult.exemptPackages.join(", ")}\n`,
      );
    }

    if (protoResult.enforcedPackages.length > 0) {
      process.stdout.write(
        `  Enforced (stable)  : ${protoResult.enforcedPackages.join(", ")}\n`,
      );
    }

    if (protoResult.findings.length === 0) {
      process.stdout.write(`  ✓ Proto arm: no breaking changes detected\n`);
    } else {
      printFindings(protoResult.findings, valveOpen);

      if (!protoResult.passed) {
        // gate will fail below on allFindings
      }

      allFindings.push(...protoResult.findings);
    }
  }

  // ── 3. Spec/i18n/OpenAPI arms ────────────────────────────────────────────
  const runJson = !args.protoOnly && !args.skipJson;

  if (runJson) {
    printSection("Spec/i18n/OpenAPI arm (JSON-diff gate)");

    const specResult = await runSpecGate({
      repoRoot,
      baseRef: args.baseRef,
      valveOpen,
    });

    if (specResult.findings.length === 0) {
      process.stdout.write(
        `  ✓ Spec/i18n/OpenAPI arm: no breaking changes detected\n`,
      );
    } else {
      printFindings(specResult.findings, valveOpen);
      allFindings.push(...specResult.findings);
    }
  }

  // ── 4. Final verdict ─────────────────────────────────────────────────────
  printSection("Gate verdict");

  const totalBreaks = allFindings.length;

  if (totalBreaks === 0) {
    process.stdout.write(`✓ PASSED — no breaking changes detected\n`);
    process.exit(0);
  }

  if (valveOpen) {
    process.stdout.write(
      `✓ PASSED (forced) — ${totalBreaks} breaking change(s) detected but force valve is open.\n` +
        `  Ensure: (1) semver MAJOR bumped, (2) CHANGELOG.md breaking entry added.\n`,
    );
    process.exit(0);
  }

  process.stdout.write(
    `✗ FAILED — ${totalBreaks} unforced breaking change(s) detected.\n` +
      `\nTo allow this break: add a commit footer  WIRE-BREAKING: <reason>  or  BREAKING CHANGE: <reason>\n` +
      `AND bump the package semver MAJOR + add a CHANGELOG.md entry.\n`,
  );
  process.exit(1);
}

main().catch((err) => {
  process.stderr.write(`[contract-gate] fatal: ${String(err)}\n`);
  process.exit(2);
});
