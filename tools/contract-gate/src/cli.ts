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
//   node dist/cli.js --against <ref> [--repo-root <path>]
//
// Flags:
//   --against <ref>      Baseline git ref (integration baseline branch).
//                        Resolution order: --against arg, then D2_GATE_BASELINE
//                        env var. Error if neither is provided.
//   --repo-root <path>   Repo root directory (default: cwd)
//   --proto-only         Run only the proto arm (mutually exclusive with --json-only)
//   --json-only          Run only the spec/i18n/openapi arms (mutually exclusive
//                        with --proto-only)
//   --skip-proto         Skip the proto arm (run json arms only)
//   --skip-json          Skip the json arms (run proto arm only)
//   --help, -h           Print this help message and exit.
//
// Exit codes:
//   0  All arms pass (or all breaks are valve-forced).
//   1  At least one unforced break detected.
//   2  Internal error (bad config, git failure, etc.).

import { resolve } from "node:path";
import { existsSync } from "node:fs";

import { resolveBaseline } from "./baseline.js";
import { validateGitRef } from "./safe-args.js";
import { commitMessagesInRange } from "./git.js";
import { parseBreakingFooters } from "./footer-parser.js";
import { runProtoArm } from "./proto-arm.js";
import { runSpecGate } from "./run-spec-gate.js";
import { formatScopeAnnouncement } from "./discovery.js";
import type { BreakingFinding } from "./breaking-finding.js";

// ---------------------------------------------------------------------------
// Argument parsing (no dependency on a CLI framework — keep it minimal)
// ---------------------------------------------------------------------------

interface CliArgs {
  readonly baseRef?: string;
  readonly repoRoot: string;
  readonly protoOnly: boolean;
  readonly jsonOnly: boolean;
  readonly skipProto: boolean;
  readonly skipJson: boolean;
  readonly help: boolean;
}

function parseArgs(argv: string[]): CliArgs {
  let baseRef: string | undefined = undefined;
  let repoRoot = process.cwd();
  let protoOnly = false;
  let jsonOnly = false;
  let skipProto = false;
  let skipJson = false;
  let help = false;

  for (let i = 0; i < argv.length; i++) {
    const arg = argv[i] ?? "";

    if (arg === "--against") {
      // Value-taking flag: fail with missing-value (not "unrecognized") when
      // the next token is absent or itself a flag.
      const next = argv[i + 1];

      if (next === undefined || next.startsWith("-")) {
        throw new Error(
          "[contract-gate] error: --against requires a <ref> argument",
        );
      }

      baseRef = next;
      i++;
    } else if (arg === "--repo-root") {
      const next = argv[i + 1];

      if (next === undefined || next.startsWith("-")) {
        throw new Error(
          "[contract-gate] error: --repo-root requires a <path> argument",
        );
      }

      repoRoot = next;
      i++;
    } else if (arg === "--proto-only") {
      protoOnly = true;
    } else if (arg === "--json-only") {
      jsonOnly = true;
    } else if (arg === "--skip-proto") {
      skipProto = true;
    } else if (arg === "--skip-json") {
      skipJson = true;
    } else if (arg === "--help" || arg === "-h") {
      help = true;
    } else if (arg.startsWith("-")) {
      // Fail-loud on unrecognized flags (incl. short forms). Positional
      // args without a leading dash are still ignored (none are defined).
      throw new Error(
        `[contract-gate] error: unrecognized flag '${arg}'. ` +
          `Pass --help for the supported flag list.`,
      );
    }
  }

  return { baseRef, repoRoot, protoOnly, jsonOnly, skipProto, skipJson, help };
}

// ---------------------------------------------------------------------------
// Help text
// ---------------------------------------------------------------------------

const HELP_TEXT = `\
contract-gate — contract breaking-change gate

Usage:
  node dist/cli.js --against <ref> [options]

Options:
  --against <ref>    Baseline git ref (required; or set D2_GATE_BASELINE env var)
  --repo-root <path> Repo root directory (default: cwd)
  --proto-only       Run only the proto arm (mutually exclusive with --json-only)
  --json-only        Run only the spec/i18n/openapi arms (mutually exclusive with
                     --proto-only)
  --skip-proto       Skip the proto arm
  --skip-json        Skip the json arms
  --help, -h         Print this help message and exit

Exit codes:
  0  All arms pass (or all breaks are valve-forced).
  1  At least one unforced break detected.
  2  Internal error (bad config, git failure, etc.).
`;

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
  let args: CliArgs;

  try {
    args = parseArgs(process.argv.slice(2));
  } catch (err) {
    process.stderr.write(`${String(err)}\n`);
    process.exit(2);
  }

  // ── 0a. --help ──
  if (args.help) {
    process.stdout.write(HELP_TEXT);
    process.exit(0);
  }

  // ── 0b. Mutual-exclusion: --proto-only and --json-only are contradictory ──
  if (args.protoOnly && args.jsonOnly) {
    process.stderr.write(
      "[contract-gate] error: --proto-only and --json-only are mutually exclusive" +
        " — they leave no arm to run.\n",
    );
    process.exit(2);
  }

  // ── 0c. No-op guards: combinations that leave no arm to run ──
  //
  // --skip-proto + --skip-json: each independently suppresses an arm, and
  //   together they leave nothing to run (same outcome as the explicit pair).
  // --json-only  + --skip-json: --json-only suppresses proto; --skip-json
  //   suppresses json; no arm runs.
  // --proto-only + --skip-proto: --proto-only suppresses json; --skip-proto
  //   suppresses proto; no arm runs.
  // (--proto-only + --json-only is already caught by 0b above.)
  if (args.skipProto && args.skipJson) {
    process.stderr.write(
      "[contract-gate] error: --skip-proto and --skip-json are mutually exclusive" +
        " — they leave no arm to run.\n",
    );
    process.exit(2);
  }

  if (args.jsonOnly && args.skipJson) {
    process.stderr.write(
      "[contract-gate] error: --json-only and --skip-json are mutually exclusive" +
        " — they leave no arm to run.\n",
    );
    process.exit(2);
  }

  if (args.protoOnly && args.skipProto) {
    process.stderr.write(
      "[contract-gate] error: --proto-only and --skip-proto are mutually exclusive" +
        " — they leave no arm to run.\n",
    );
    process.exit(2);
  }

  // ── 0. Baseline resolution: --against arg > D2_GATE_BASELINE env var ──
  const baseRef = resolveBaseline(
    args.baseRef,
    process.env["D2_GATE_BASELINE"],
  );

  if (baseRef === undefined) {
    process.stderr.write(
      "[contract-gate] error: no baseline ref supplied. Pass --against <ref> or set D2_GATE_BASELINE.\n",
    );
    process.exit(2);
  }

  try {
    validateGitRef(baseRef);
  } catch (err) {
    process.stderr.write(
      `[contract-gate] error: invalid baseline ref — ${String(err)}\n`,
    );
    process.exit(2);
  }

  const repoRoot = resolve(args.repoRoot);

  if (!existsSync(resolve(repoRoot, ".git"))) {
    process.stderr.write(
      `[contract-gate] error: '${repoRoot}' does not appear to be a git repository (.git not found)\n`,
    );
    process.exit(2);
  }

  // ── 1. Resolve the force valve ──
  let valveOpen = false;
  let valveSummary =
    "Force valve: CLOSED (no breaking footer detected in commit range)";

  try {
    const messages = commitMessagesInRange(baseRef, "HEAD", repoRoot);
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
  process.stdout.write(`Baseline ref : ${baseRef}\n`);
  process.stdout.write(`Repo root    : ${repoRoot}\n`);
  process.stdout.write(`${valveSummary}\n`);

  const allFindings: BreakingFinding[] = [];

  // ── 2. Proto arm ──
  const runProto = !args.jsonOnly && !args.skipProto;

  if (runProto) {
    printSection("Proto arm (buf breaking — FILE level)");

    const protoResult = runProtoArm({
      repoRoot,
      baseRef,
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

  // ── 3. Spec/i18n/OpenAPI arms ──
  const runJson = !args.protoOnly && !args.skipJson;

  if (runJson) {
    printSection("Spec/i18n/OpenAPI arm (JSON-diff gate)");

    // Scope announcement is written AFTER the gate returns (runSpecGate owns
    // discovery). A prior version printed only after await; under parallel
    // vitest CLI subprocesses on CI, capture sometimes lost the tail — the
    // unit-tested formatter + live gate job still pin the line shape.
    const specResult = await runSpecGate({
      repoRoot,
      baseRef,
      valveOpen,
    });

    process.stdout.write(`${formatScopeAnnouncement(specResult.scope)}\n`);

    if (specResult.findings.length === 0) {
      process.stdout.write(
        `  ✓ Spec/i18n/OpenAPI arm: no breaking changes detected\n`,
      );
    } else {
      printFindings(specResult.findings, valveOpen);
      allFindings.push(...specResult.findings);
    }
  }

  // ── 4. Final verdict ──
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
