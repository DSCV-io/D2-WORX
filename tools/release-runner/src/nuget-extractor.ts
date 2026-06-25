// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// NuGet extraction adapter — derives ApiDiff + FingerprintDiff for a single
// NuGet consumable package using:
//
//   A. PublicApiAnalyzers (RS0016 / RS0017) — compares the package's public API
//      surface against its committed PublicAPI.Shipped.txt baseline to produce
//      the ApiDiff signal.
//
//   B. Deterministic DLL hash (DebugType=none) — builds the package with
//      debug-info stripped so the DLL bytes are determined solely by the IL /
//      metadata content; SHA-256-hashes the output DLL and compares it against
//      the committed baseline hash to produce the FingerprintDiff signal.
//
// Fingerprint mechanism: `dotnet build -c Release -p:DebugType=none -p:DebugSymbols=false
// --no-incremental` produces a DLL whose bytes are identical for comment-only /
// whitespace edits (those affect only debug-info, which is stripped), and differ
// for any IL or metadata change (method body edits, type changes, dependency-version
// changes). `--no-incremental` is required because without it MSBuild skips a
// rebuild if the output DLL is already newer than the source inputs — which means
// a preceding API-diff build (which produces a DLL WITH debug info) would cause
// the fingerprint build to re-hash the WRONG DLL and see a non-stable hash.
// The hash is path-stable because no source path is embedded in the DLL when
// DebugType=none is set.
//
// Injectable design: the `DotnetShell` seam isolates all subprocess calls so
// the real extraction runs against the actual csproj while tests can supply
// synthetic outputs without spawning child processes.
//
// Shell-out cost: ~1–2 s per package on an incremental build (with --no-restore),
// scaling linearly with dependency count. Estimated 1–2 min total for the 54-
// NuGet package set (83 packages total across both ecosystems).

import { createHash } from "node:crypto";
import { existsSync, readFileSync } from "node:fs";
import { join } from "node:path";
import { spawnSync, type SpawnSyncReturns } from "node:child_process";
import { truthy } from "@d2/utilities";
import type { ApiDiff, FingerprintDiff } from "./diff-bump.js";
import type { PackageDescriptor } from "./types.js";

// ---------------------------------------------------------------------------
// Injectable shell seam
// ---------------------------------------------------------------------------

/**
 * Result of a single subprocess invocation.
 *
 * Maps to `spawnSync` return shape; injectable for tests so no real dotnet
 * process is spawned during unit-testing.
 */
export interface ShellResult {
  /** Process exit code, or null when the process could not be started. */
  readonly status: number | null;
  /** stdout captured from the subprocess. */
  readonly stdout: string;
  /** stderr captured from the subprocess. */
  readonly stderr: string;
}

/**
 * Seam that wraps all subprocess calls made by the extractor.
 *
 * The real implementation delegates to `spawnSync`. Tests inject a synthetic
 * implementation that returns pre-canned outputs without spawning child
 * processes, enabling deterministic assertions against the ApiDiff /
 * FingerprintDiff mapping logic.
 */
export interface DotnetShell {
  /**
   * Run `dotnet build <csprojPath>` with the given extra arguments.
   *
   * @returns A ShellResult capturing stdout + stderr + exit code.
   */
  build(csprojPath: string, extraArgs: readonly string[]): ShellResult;

  /**
   * Read a file from the filesystem, or return undefined when the file does
   * not exist. Used to read PublicAPI.Shipped.txt, PublicAPI.Unshipped.txt,
   * and the committed fingerprint baseline.
   */
  readFile(filePath: string): string | undefined;

  /**
   * SHA-256 the bytes of a file and return the hex digest, or return
   * undefined when the file does not exist. Used to hash the built DLL.
   */
  sha256File(filePath: string): string | undefined;
}

// ---------------------------------------------------------------------------
// Real shell implementation (production path)
// ---------------------------------------------------------------------------

/**
 * The real DotnetShell implementation — delegates to spawnSync and node:fs.
 *
 * Pass this to `extractNugetDiff` in production; inject a synthetic
 * implementation in tests.
 */
export const realDotnetShell: DotnetShell = {
  build(csprojPath, extraArgs) {
    const result: SpawnSyncReturns<string> = spawnSync(
      "dotnet",
      ["build", csprojPath, ...extraArgs],
      {
        encoding: "utf-8",
        maxBuffer: 10 * 1024 * 1024,
      },
    );

    return {
      status: result.status,
      stdout: result.stdout ?? "",
      stderr: result.stderr ?? "",
    };
  },

  readFile(filePath) {
    if (!existsSync(filePath)) return undefined;

    return readFileSync(filePath, "utf-8");
  },

  sha256File(filePath) {
    if (!existsSync(filePath)) return undefined;

    const bytes = readFileSync(filePath);
    return createHash("sha256").update(bytes).digest("hex");
  },
};

// ---------------------------------------------------------------------------
// Internal — ApiDiff derivation from PublicAPI.Unshipped.txt
// ---------------------------------------------------------------------------

/**
 * Parse the content of a `PublicAPI.Unshipped.txt` file into an `ApiDiff`.
 *
 * The PublicApiAnalyzers tool writes new API lines as plain symbol strings
 * and REMOVED API lines prefixed with `*REMOVED*`. This function maps those
 * lines to the `{added, removed, changed}` shape consumed by `deriveBump`.
 *
 * A rename shows up as a `*REMOVED*` line (old name) + a plain line (new
 * name); the function treats that as both `removed` AND `added`. The bump
 * engine's transition matrix handles the combination correctly (break wins
 * over minor, so a rename on a stable package → major).
 *
 * @param unshippedContent - The raw text of `PublicAPI.Unshipped.txt`.
 * @returns The derived ApiDiff.
 */
export function parseUnshippedTxt(unshippedContent: string): ApiDiff {
  // The file may begin with `#nullable enable` — skip that header line.
  const lines = unshippedContent
    .split("\n")
    .map((l) => l.trim())
    .filter((l) => truthy(l) && !l.startsWith("#"));

  let added = false;
  let removed = false;

  for (const line of lines) {
    if (line.startsWith("*REMOVED*")) {
      removed = true;
    } else {
      added = true;
    }
  }

  // A member whose signature changed appears as a *REMOVED* line (old sig) +
  // a plain line (new sig). We model that as removed+added, which the bump
  // engine maps to a break (same outcome as `changed`). The `changed` flag
  // itself is not separately derivable from PublicApiAnalyzers output, so we
  // leave it false and rely on the removed+added combination.
  return { added, removed, changed: false };
}

// ---------------------------------------------------------------------------
// Internal — build + extract fingerprint
// ---------------------------------------------------------------------------

/**
 * Build the package with debug-info stripped and return the DLL output path.
 *
 * Uses `dotnet build -c Release -p:DebugType=none -p:DebugSymbols=false
 * --no-restore --no-incremental` so the build is always performed with the
 * correct properties regardless of what the preceding API-diff build produced.
 * The caller supplies the `DotnetShell` seam.
 *
 * @param csprojPath - Absolute path to the .csproj file.
 * @param shell      - The shell seam to use.
 * @returns The absolute path to the built DLL file.
 * @throws {Error} When the build fails or the DLL cannot be located.
 */
function buildAndLocateDll(
  csprojPath: string,
  packageName: string,
  shell: DotnetShell,
): string {
  // The fingerprint build must strip debug info (for hash stability) and must
  // suppress RS0016/RS0017 so the build succeeds even when the source has API
  // changes (those are captured by the API-diff build, not this one). Setting
  // WarningsAsErrors to an empty string disables the solution-wide
  // TreatWarningsAsErrors=true only for this invocation without permanently
  // altering the project file.
  // --no-incremental forces a full rebuild even when outputs are newer than
  // inputs. Without it, MSBuild skips the rebuild if a preceding API-diff
  // build already produced a DLL (with debug info), leaving the fingerprint
  // build reading the wrong DLL (not DebugType=none). The extra ~1s per
  // package is acceptable for the 54-package set.
  const result = shell.build(csprojPath, [
    "-c",
    "Release",
    "-p:DebugType=none",
    "-p:DebugSymbols=false",
    "-p:TreatWarningsAsErrors=false",
    "--no-restore",
    "--no-incremental",
  ]);

  if (result.status !== 0) {
    throw new Error(
      `dotnet build failed for ${packageName} (exit ${result.status?.toString() ?? "null"}):\n${result.stderr}`,
    );
  }

  // Derive the DLL path from the csproj directory + convention:
  //   <csprojDir>/bin/Release/net10.0/<PackageName>.dll
  // The framework moniker (net10.0) is the solution-wide target from
  // server/Directory.Build.props. PackageName = basename of the .csproj.
  const csprojDir = csprojPath.replace(/[\\/][^\\/]+\.csproj$/, "");
  const dllPath = join(
    csprojDir,
    "bin",
    "Release",
    "net10.0",
    `${packageName}.dll`,
  );

  if (shell.readFile(dllPath) === undefined) {
    throw new Error(
      `Built DLL not found at expected path: ${dllPath}. ` +
        `Check that <AssemblyName> matches the csproj filename.`,
    );
  }

  return dllPath;
}

// ---------------------------------------------------------------------------
// Internal — build diagnostics + extract API diff from RS0016/RS0017 lines
// ---------------------------------------------------------------------------

/**
 * Build with PublicApiAnalyzers active and parse the RS0016/RS0017 diagnostics
 * from the build output to derive the ApiDiff.
 *
 * Because TreatWarningsAsErrors is set solution-wide, RS0016/RS0017 are errors
 * and appear in the build output even on a failed build. We intentionally
 * allow build failure here (status !== 0) — the diagnostics are what we need.
 *
 * @param csprojPath  - Absolute path to the .csproj file.
 * @param packageName - The NuGet package identity (for error messages).
 * @param shell       - The shell seam.
 * @returns The derived ApiDiff from the RS0016/RS0017 diagnostic lines.
 */
function extractApiDiff(
  csprojPath: string,
  packageName: string,
  shell: DotnetShell,
): ApiDiff {
  // Build with no additional flags — the analyzer runs automatically because
  // <PackageReference Include="Microsoft.CodeAnalysis.PublicApiAnalyzers"> is
  // already in the csproj (opted-in individually, not globally).
  const result = shell.build(csprojPath, ["-c", "Release", "--no-restore"]);

  const output = result.stdout + "\n" + result.stderr;

  // RS0016 = new API not in Shipped/Unshipped → added.
  const rs0016 = /\berror RS0016\b/.test(output);
  // RS0017 = API in Shipped but missing from source → removed.
  const rs0017 = /\berror RS0017\b/.test(output);

  if (!rs0016 && !rs0017 && result.status !== 0) {
    // Build failed for a reason OTHER than RS0016/RS0017 — surface the error.
    throw new Error(
      `dotnet build failed for ${packageName} (exit ${result.status?.toString() ?? "null"}) ` +
        `with no RS0016/RS0017 diagnostics:\n${output.slice(0, 2000)}`,
    );
  }

  return { added: rs0016, removed: rs0017, changed: false };
}

// ---------------------------------------------------------------------------
// Public API — extraction result
// ---------------------------------------------------------------------------

/**
 * The combined extraction result for a single NuGet package.
 */
export interface NugetExtractionResult {
  /** The public API surface diff vs the committed PublicAPI.Shipped.txt baseline. */
  readonly apiDiff: ApiDiff;
  /**
   * The output fingerprint diff vs the committed baseline hash.
   *
   * `changed: true` when the DLL SHA-256 differs from the committed
   * `.fingerprint-baseline` file, or when no baseline exists (first run).
   */
  readonly fingerprintDiff: FingerprintDiff;
  /**
   * Wall-clock time in milliseconds for the extraction (both builds).
   * Relevant for the 83-package rollout feasibility assessment.
   */
  readonly extractionMs: number;
}

// ---------------------------------------------------------------------------
// Public API — fingerprint baseline file naming convention
// ---------------------------------------------------------------------------

/**
 * Return the path to the committed fingerprint baseline file for a package.
 *
 * Convention: `.release-fingerprint` in the same directory as the .csproj.
 * This file is committed to git and updated by the release runner after each
 * version bump. Its absence is treated as `changed: true` (first run).
 *
 * @param csprojPath - Absolute path to the .csproj file.
 */
export function fingerprintBaselinePath(csprojPath: string): string {
  const dir = csprojPath.replace(/[\\/][^\\/]+\.csproj$/, "");
  return join(dir, ".release-fingerprint");
}

// ---------------------------------------------------------------------------
// Public API — main extraction function
// ---------------------------------------------------------------------------

/**
 * Extract the ApiDiff and FingerprintDiff for a single NuGet package.
 *
 * Runs two `dotnet build` invocations:
 *   1. A normal build to capture RS0016/RS0017 diagnostics (ApiDiff).
 *   2. A debug-stripped build to hash the DLL (FingerprintDiff).
 *
 * The two builds are sequenced (not parallel) because MSBuild locks the output
 * directory during a build. On an incremental build (artifacts up-to-date),
 * build 1 exits quickly from the MSBuild cache.
 *
 * @param pkg   - The package descriptor from the manifest loader.
 * @param shell - The shell seam. Pass `realDotnetShell` in production.
 *                Inject a synthetic shell in tests.
 * @returns The extraction result containing ApiDiff, FingerprintDiff, and timing.
 * @throws {Error} When the build fails for a reason other than RS0016/RS0017,
 *                 or when the built DLL cannot be located.
 */
export function extractNugetDiff(
  pkg: PackageDescriptor,
  shell: DotnetShell = realDotnetShell,
): NugetExtractionResult {
  const start = Date.now();

  // --- Step A: API diff via RS0016/RS0017 diagnostics ---------------------

  const apiDiff = extractApiDiff(pkg.manifestPath, pkg.name, shell);

  // --- Step B: fingerprint diff via deterministic DLL hash ----------------

  const dllPath = buildAndLocateDll(pkg.manifestPath, pkg.name, shell);
  const currentHash = shell.sha256File(dllPath);

  if (currentHash === undefined) {
    throw new Error(
      `sha256File returned undefined for ${dllPath} — ` +
        `DLL was built but cannot be read.`,
    );
  }

  const baselinePath = fingerprintBaselinePath(pkg.manifestPath);
  const committedHash = shell.readFile(baselinePath)?.trim();

  // If no baseline exists yet (first run after enabling fingerprinting),
  // treat as changed so a PATCH bump is recorded and the baseline is seeded.
  const fingerprintChanged =
    committedHash === undefined || committedHash !== currentHash;

  const extractionMs = Date.now() - start;

  return {
    apiDiff,
    fingerprintDiff: { changed: fingerprintChanged },
    extractionMs,
  };
}
