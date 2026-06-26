// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// NuGet extraction adapter — derives the public-API diff signal + the
// normalized IL dump for a single NuGet consumable package using:
//
//   A. PublicApiAnalyzers (RS0016 / RS0017) — compares the package's public API
//      surface against its committed PublicAPI.Shipped.txt baseline to produce
//      the ApiDiff signal.
//
//   B. Normalized IL/metadata dump — builds the package, then shells the
//      in-box `tools/il-fingerprint` console tool against the built DLL to get a
//      platform-independent text dump of the assembly's metadata + IL. The dump
//      is path/MVID/timestamp-independent BY CONSTRUCTION (the tool never reads
//      those fields), so a baseline generated on one host equals a recompute on
//      another — unlike a raw DLL SHA-256, which embeds the source path / module
//      MVID / build timestamp and therefore differs build-to-build and
//      host-to-host.
//
// This adapter RETURNS the IL-dump STRING (not a pre-hashed fingerprint). The
// production DiffProvider (real-diff-provider.ts) composes the final per-package
// fingerprint as SHA-256(PublicAPI.Shipped+Unshipped + il-dump + manifest), so
// the manifest metadata (incl. resolved dependency versions for propagation) is
// folded in at the provider layer, not here. Keeping the IL dump un-hashed here
// is what lets the provider be the single home of the fingerprint composition.
//
// Injectable design: the `DotnetShell` seam isolates all subprocess calls so
// the real extraction runs against the actual csproj + il-fingerprint tool
// while tests can supply synthetic outputs without spawning child processes.
//
// Shell-out cost: ~1–2 s per package on an incremental build (with --no-restore)
// plus a fast il-fingerprint pass, scaling linearly with dependency count.

import { existsSync, readFileSync } from "node:fs";
import { join } from "node:path";
import { spawnSync, type SpawnSyncReturns } from "node:child_process";
import { truthy } from "@d2/utilities";
import type { ApiDiff } from "./diff-bump.js";
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
 * Seam that wraps all subprocess + filesystem calls made by the extractor.
 *
 * The real implementation delegates to `spawnSync` + node:fs. Tests inject a
 * synthetic implementation that returns pre-canned outputs without spawning
 * child processes, enabling deterministic assertions against the ApiDiff +
 * IL-dump extraction logic.
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
   * not exist. Used to read PublicAPI.Shipped.txt + to confirm the built DLL
   * exists at the expected path.
   */
  readFile(filePath: string): string | undefined;

  /**
   * Shell the in-box `tools/il-fingerprint` console tool against a built DLL
   * and return its normalized stdout dump, or undefined when the tool fails.
   *
   * The dump is the platform-independent "compiled output changed" signal. The
   * provider composes it with the manifest metadata into the final fingerprint.
   */
  ilDump(dllPath: string): string | undefined;
}

// ---------------------------------------------------------------------------
// Real shell implementation (production path)
// ---------------------------------------------------------------------------

/**
 * Construct the real DotnetShell — delegates to spawnSync and node:fs.
 *
 * `repoRoot` is required so the il-fingerprint tool can be located via
 * `dotnet run --project <repoRoot>/tools/il-fingerprint`.
 *
 * @param repoRoot - Absolute path to the repository root.
 */
export function makeRealDotnetShell(repoRoot: string): DotnetShell {
  const ilFingerprintProject = join(repoRoot, "tools", "il-fingerprint");

  return {
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

    ilDump(dllPath) {
      const result: SpawnSyncReturns<string> = spawnSync(
        "dotnet",
        [
          "run",
          "--project",
          ilFingerprintProject,
          "-c",
          "Release",
          "--",
          dllPath,
        ],
        {
          encoding: "utf-8",
          maxBuffer: 64 * 1024 * 1024,
        },
      );

      if (result.status !== 0) return undefined;

      return result.stdout ?? undefined;
    },
  };
}

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
// Internal — build + extract the normalized IL dump
// ---------------------------------------------------------------------------

/**
 * Build the package (Release) and return the built DLL path.
 *
 * Uses `dotnet build -c Release -p:TreatWarningsAsErrors=false --no-restore`.
 * Debug info is irrelevant to the IL dump (the dumper never reads the
 * debug-directory), so `DebugType=none` is no longer needed. `--no-incremental`
 * is NOT required either: the IL dump reads the metadata + IL of whatever DLL is
 * present, and an incremental build still re-emits IL for changed source.
 *
 * @param csprojPath  - Absolute path to the .csproj file.
 * @param packageName - The NuGet package identity (for error messages).
 * @param shell       - The shell seam to use.
 * @returns The absolute path to the built DLL file.
 * @throws {Error} When the build fails or the DLL cannot be located.
 */
function buildAndLocateDll(
  csprojPath: string,
  packageName: string,
  shell: DotnetShell,
): string {
  // The fingerprint build suppresses RS0016/RS0017 (captured by the API-diff
  // build, not this one) so the build succeeds even when the source has API
  // changes. Setting TreatWarningsAsErrors=false disables the solution-wide
  // TreatWarningsAsErrors=true only for this invocation without permanently
  // altering the project file.
  const result = shell.build(csprojPath, [
    "-c",
    "Release",
    "-p:TreatWarningsAsErrors=false",
    "--no-restore",
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
   * The normalized IL/metadata dump of the built assembly.
   *
   * This is the platform-independent "compiled output changed" signal. The
   * production DiffProvider composes it with PublicAPI.* + manifest metadata
   * into the final per-package fingerprint, then compares against the committed
   * `.release-fingerprint`. Returned un-hashed so the provider is the single
   * home of fingerprint composition (and can fold in resolvedVersions).
   */
  readonly ilDump: string;
  /** The committed PublicAPI.Shipped.txt content (for fingerprint composition). */
  readonly shippedTxt: string;
  /** The committed PublicAPI.Unshipped.txt content (for fingerprint composition). */
  readonly unshippedTxt: string;
  /**
   * Wall-clock time in milliseconds for the extraction (build + IL dump).
   * Relevant for the 83-package rollout feasibility assessment.
   */
  readonly extractionMs: number;
}

// ---------------------------------------------------------------------------
// Public API — baseline file naming conventions
// ---------------------------------------------------------------------------

/**
 * Return the path to the committed fingerprint baseline file for a package.
 *
 * Convention: `.release-fingerprint` in the same directory as the .csproj.
 * This file is committed to git and updated by the release runner after each
 * version bump. Its absence is treated as a first run.
 *
 * @param csprojPath - Absolute path to the .csproj file.
 */
export function fingerprintBaselinePath(csprojPath: string): string {
  const dir = csprojPath.replace(/[\\/][^\\/]+\.csproj$/, "");
  return join(dir, ".release-fingerprint");
}

/**
 * Return the path to the committed PublicAPI.Shipped.txt for a package.
 *
 * @param csprojPath - Absolute path to the .csproj file.
 */
export function shippedTxtPath(csprojPath: string): string {
  const dir = csprojPath.replace(/[\\/][^\\/]+\.csproj$/, "");
  return join(dir, "PublicAPI.Shipped.txt");
}

/**
 * Return the path to the committed PublicAPI.Unshipped.txt for a package.
 *
 * @param csprojPath - Absolute path to the .csproj file.
 */
export function unshippedTxtPath(csprojPath: string): string {
  const dir = csprojPath.replace(/[\\/][^\\/]+\.csproj$/, "");
  return join(dir, "PublicAPI.Unshipped.txt");
}

// ---------------------------------------------------------------------------
// Public API — main extraction function
// ---------------------------------------------------------------------------

/**
 * Extract the ApiDiff + the normalized IL dump for a single NuGet package.
 *
 * Runs two `dotnet build` invocations + one il-fingerprint pass:
 *   1. A normal build to capture RS0016/RS0017 diagnostics (ApiDiff).
 *   2. A Release build to produce the DLL.
 *   3. An il-fingerprint pass over the built DLL (the normalized IL dump).
 *
 * The builds are sequenced (not parallel) because MSBuild locks the output
 * directory during a build. On an incremental build (artifacts up-to-date),
 * build 1 exits quickly from the MSBuild cache.
 *
 * The composition of the IL dump + manifest metadata into the final fingerprint
 * (and the comparison against the committed baseline) happens in the production
 * DiffProvider, NOT here — this function returns the raw IL dump + the committed
 * PublicAPI.* content so the provider can compose deterministically.
 *
 * @param pkg   - The package descriptor from the manifest loader.
 * @param shell - The shell seam (construct via makeRealDotnetShell in production;
 *                inject a synthetic shell in tests).
 * @returns The extraction result (ApiDiff, IL dump, PublicAPI.* content, timing).
 * @throws {Error} When the build fails for a reason other than RS0016/RS0017,
 *                 the DLL cannot be located, or the IL dump fails.
 */
export function extractNugetDiff(
  pkg: PackageDescriptor,
  shell: DotnetShell,
): NugetExtractionResult {
  const start = Date.now();

  // --- Step A: API diff via RS0016/RS0017 diagnostics ---------------------

  const apiDiff = extractApiDiff(pkg.manifestPath, pkg.name, shell);

  // --- Step B: IL dump via the il-fingerprint tool ------------------------

  const dllPath = buildAndLocateDll(pkg.manifestPath, pkg.name, shell);
  const ilDump = shell.ilDump(dllPath);

  if (ilDump === undefined) {
    throw new Error(
      `il-fingerprint returned no dump for ${dllPath} — ` +
        `the DLL was built but the IL-dump tool failed.`,
    );
  }

  // --- Step C: read the committed PublicAPI.* content ---------------------

  const shippedTxt = shell.readFile(shippedTxtPath(pkg.manifestPath)) ?? "";
  const unshippedTxt = shell.readFile(unshippedTxtPath(pkg.manifestPath)) ?? "";

  const extractionMs = Date.now() - start;

  return {
    apiDiff,
    ilDump,
    shippedTxt,
    unshippedTxt,
    extractionMs,
  };
}
