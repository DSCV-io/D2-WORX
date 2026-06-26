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
//   The buf shim is resolved via `createRequire` anchored to this file —
//   `@bufbuild/buf` is a direct devDependency of this package, so the shim
//   is always findable regardless of pnpm hoisting.  The shim (`bin/buf`) is
//   a Node.js script that internally resolves the native platform binary via
//   `require.resolve`; invoking it as `node <shimPath>` works on every
//   platform (Windows and POSIX) without needing `.CMD` wrappers.
//   A `buf.yaml` at `contracts/protos/buf.yaml` defines the module for the
//   shared protos.
//
// Regex discipline (Bucket 2 per regex-redos-discipline):
//   The proto-file scanner uses PACKAGE_LINE_RE from proto-exemption.ts —
//   bounded single-line pattern, no nested quantifiers, no super-linear
//   backtracking.

import { spawnSync } from "node:child_process";
import { readFileSync, readdirSync, statSync } from "node:fs";
import { createRequire } from "node:module";
import { join, resolve } from "node:path";

import { truthy } from "@d2/utilities";

import type { BreakingFinding } from "./breaking-finding.js";
import { extractProtoPackage, isProtoGateExempt } from "./proto-exemption.js";
import { validateGitRef } from "./safe-args.js";

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

/** Options for the proto arm. */
export interface ProtoArmOptions {
  /** Absolute path to the workspace root (where `buf.yaml` lives + git root). */
  readonly repoRoot: string;
  /** The integration baseline git ref (e.g. a branch name or commit SHA). */
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

// ---------------------------------------------------------------------------
// Buf shim resolution (hoisting-independent)
// ---------------------------------------------------------------------------

/**
 * Resolve the absolute path to the `@bufbuild/buf` Node.js shim script.
 *
 * Uses `createRequire` anchored to this module so resolution searches
 * `node_modules` in this package and its ancestors — the pnpm virtual store
 * — rather than the workspace root's `.bin` symlinks.  This makes buf
 * findable regardless of whether pnpm hoisted it to the workspace root or
 * left it package-local (e.g. under `--filter` installs on CI).
 *
 * The resolved path is the Node.js shim (`@bufbuild/buf/bin/buf`), which
 * internally calls `require.resolve` to locate the native platform binary
 * (e.g. `@bufbuild/buf-linux-x64`).  Invoking it with `node <shimPath>`
 * therefore works on every platform without `.CMD` wrappers.
 */
export function resolveBufShim(): string {
  const req = createRequire(import.meta.url);
  return req.resolve("@bufbuild/buf/bin/buf");
}

/**
 * Resolve the buf invocation command and args prefix for the current platform.
 *
 * Always uses `node <shimPath>` — the shim is a Node.js script on every
 * platform and internally resolves the native binary via `require.resolve`.
 *
 * @returns `{ cmd, prefix }` — `cmd` is `node`; `prefix` is `[shimPath]` to
 *   prepend before the buf sub-command args.
 */
function resolveBufInvocation(): {
  readonly cmd: string;
  readonly prefix: readonly string[];
} {
  return { cmd: process.execPath, prefix: [resolveBufShim()] };
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

  // Defensive guard: the CLI path validates baseRef before calling this function,
  // but direct callers (e.g. integration tests, future programmatic consumers)
  // must not bypass the allowlist check. Throw early so the buf `--against` arg
  // is never constructed from an unvalidated ref.
  validateGitRef(baseRef);

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

  const { cmd, prefix } = resolveBufInvocation();
  const bufConfigPath = join(protosDir, "buf.yaml");

  // Run `buf breaking` against the integration baseline at FILE level.
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
    const lines = rawOutput.split("\n").filter((l) => truthy(l.trim()));

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
