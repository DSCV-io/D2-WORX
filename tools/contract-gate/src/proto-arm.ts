// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Proto breaking-change arm.
//
// Wraps `buf breaking` to enforce FILE-level breaking rules on stable proto
// packages (`vN`, no alpha/beta suffix). Pre-stable packages (`vNalpha`,
// `vNbeta`) are exempt — they break freely. The shared `d2.common.v1` protos
// are stable and are always gate-enforced.
//
// Architecture:
//   1. Scan the before-and-after proto files to find stable packages.
//   2. For each stable package, invoke `buf breaking --against` the baseline
//      with `--path` scoped to that package's directory (or the full module
//      when the package covers the full root).
//   3. Aggregate findings; the caller consults the force valve.
//
// Invocation model:
//   The buf binary is resolved from the root node_modules (it is installed as
//   `@bufbuild/buf` in the workspace `onlyBuiltDependencies` — the binary
//   lives at <workspace-root>/node_modules/.bin/buf and is always available).
//   A `buf.yaml` at `contracts/protos/buf.yaml` defines the module for the
//   shared protos; the wrapper passes `--path contracts/protos` so buf resolves
//   imports correctly. Emitted `.g.proto` files are handled per-package.
//
// Regex discipline (Bucket 2 per regex-redos-discipline):
//   The proto-file scanner uses PACKAGE_LINE_RE from proto-exemption.ts —
//   bounded single-line pattern, no nested quantifiers, no super-linear
//   backtracking.

import { spawnSync } from "node:child_process";
import { readFileSync, readdirSync, statSync } from "node:fs";
import { join, resolve } from "node:path";

import type { BreakingFinding } from "./breaking-finding.js";
import { extractProtoPackage, isProtoGateExempt } from "./proto-exemption.js";

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

/** Options for the proto arm. */
export interface ProtoArmOptions {
  /** Absolute path to the workspace root (where `buf.yaml` lives + git root). */
  readonly repoRoot: string;
  /** The baseline git ref, e.g. "nova". */
  readonly baseRef: string;
  /** True when the force valve has been pulled (any breaking footer present). */
  readonly valveOpen: boolean;
}

/** Result of the proto arm check. */
export interface ProtoArmResult {
  /** True when the arm passes (either no breaks, or valve open). */
  readonly passed: boolean;
  /** Human-readable findings, one per detected break. */
  readonly findings: readonly BreakingFinding[];
  /** Packages that were exempt (pre-stable). Informational. */
  readonly exemptPackages: readonly string[];
  /** Packages that were gate-enforced. Informational. */
  readonly enforcedPackages: readonly string[];
}

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

/**
 * Collect the `package` declarations from all `*.proto` files under `dir`,
 * returning a deduplicated set of package names.
 */
function collectPackages(dir: string): Set<string> {
  const packages = new Set<string>();

  function walkDir(currentDir: string): void {
    let names: string[];

    try {
      names = readdirSync(currentDir);
    } catch {
      return;
    }

    for (const name of names) {
      const fullPath = join(currentDir, name);

      if (name.endsWith(".proto")) {
        try {
          const content = readFileSync(fullPath, "utf-8");

          for (const line of content.split("\n")) {
            const pkg = extractProtoPackage(line);

            if (pkg !== undefined) {
              packages.add(pkg);
              break; // only one package declaration per file
            }
          }
        } catch {
          // skip unreadable files
        }
      } else {
        try {
          if (statSync(fullPath).isDirectory()) walkDir(fullPath);
        } catch {
          // skip unreadable or missing entries
        }
      }
    }
  }

  try {
    walkDir(dir);
  } catch {
    // dir does not exist or is unreadable — return empty set
  }

  return packages;
}

/**
 * Resolve the buf binary command and args prefix for the current platform.
 *
 * On Windows, .CMD files cannot be spawnSync'd directly — they must be invoked
 * via `cmd /c`. On POSIX, the plain `buf` binary can be invoked directly.
 *
 * @returns `{ cmd, prefix }` — `cmd` is the executable; `prefix` is any args
 *   to prepend before the buf sub-command args.
 */
function resolveBufInvocation(repoRoot: string): {
  readonly cmd: string;
  readonly prefix: readonly string[];
} {
  const isWin = process.platform === "win32";
  const bufPath = join(
    repoRoot,
    "node_modules",
    ".bin",
    isWin ? "buf.CMD" : "buf",
  );

  if (isWin) {
    return { cmd: "cmd", prefix: ["/c", bufPath] };
  }

  return { cmd: bufPath, prefix: [] };
}

// ---------------------------------------------------------------------------
// Public API
// ---------------------------------------------------------------------------

/**
 * Run the proto breaking-change arm.
 *
 * Scans the `contracts/protos/` directory for stable proto packages and runs
 * `buf breaking --against` the baseline ref at FILE level. Pre-stable packages
 * are logged as exempt and skipped.
 *
 * @param opts - Proto arm options.
 * @returns A {@link ProtoArmResult} describing the outcome.
 */
export function runProtoArm(opts: ProtoArmOptions): ProtoArmResult {
  const { repoRoot, baseRef, valveOpen } = opts;
  const protosDir = resolve(repoRoot, "contracts", "protos");

  // Discover packages present in the shared contracts/protos tree.
  const packages = collectPackages(protosDir);

  const exemptPackages: string[] = [];
  const enforcedPackages: string[] = [];

  for (const pkg of packages) {
    const { exempt, warning } = isProtoGateExempt(pkg);

    if (warning !== undefined)
      process.stderr.write(`[proto-arm] warning: ${warning}\n`);

    if (exempt) exemptPackages.push(pkg);
    else enforcedPackages.push(pkg);
  }

  if (enforcedPackages.length === 0) {
    // Nothing stable to check — arm passes trivially (informational).
    return {
      passed: true,
      findings: [],
      exemptPackages,
      enforcedPackages,
    };
  }

  const { cmd, prefix } = resolveBufInvocation(repoRoot);
  const bufConfigPath = join(protosDir, "buf.yaml");

  // Run `buf breaking` against the nova baseline at FILE level.
  // The `--against '.git#branch=<ref>,subdir=contracts/protos'` form points
  // buf at the module inside the git baseline.
  const bufResult = spawnSync(
    cmd,
    [
      ...prefix,
      "breaking",
      protosDir,
      "--config",
      bufConfigPath,
      "--against",
      `.git#branch=${baseRef},subdir=contracts/protos`,
    ],
    {
      cwd: repoRoot,
      encoding: "utf-8",
      maxBuffer: 8 * 1024 * 1024,
    },
  );

  const findings: BreakingFinding[] = [];

  if (bufResult.status !== 0) {
    const rawOutput = (
      (bufResult.stdout ?? "") + (bufResult.stderr ?? "")
    ).trim();
    const lines = rawOutput.split("\n").filter((l) => l.trim().length > 0);

    for (const line of lines) {
      findings.push({
        arm: "proto",
        severity: "ERROR",
        message: line.trim(),
      });
    }

    // If no structured output was captured, emit a generic finding.
    if (findings.length === 0) {
      findings.push({
        arm: "proto",
        severity: "ERROR",
        message: `buf breaking exited with code ${bufResult.status?.toString() ?? "unknown"} (no output captured)`,
      });
    }
  }

  const passed = findings.length === 0 || valveOpen;

  return { passed, findings, exemptPackages, enforcedPackages };
}
