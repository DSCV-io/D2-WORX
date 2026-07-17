// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

// Manifest loader — discovers consumable packages from the repository tree.
//
// Dual inventory (step 05 / I12):
//   PUBLIC publish lane (loadNpmPackages / loadNugetPackages / loadAllPackages):
//     roots = public/packages/typescript/** and public/packages/dotnet/** ONLY.
//     open npm: startsWith("@dcsv-io/d2-") AND NOT containing "d2-private-".
//     open NuGet: basename must NOT contain ".Private."; exclude SourceGen shells
//       and mega-tests assembly DcsvIo.D2.Tests; require <Version>.
//     exclusions: typespec-decorators, typespec-emitters, contract-tests.
//   PRIVATE consumable lane (§26.19 versioning — NOT mixed into public --list):
//     loadPrivateConsumableNpmPackages / loadPrivateConsumableNugetPackages
//     → KeyCustodian client-ts + KC .NET Client under private/services only.
//
// Bare startsWith("@dcsv-io/d2-") alone is FORBIDDEN as the sole public filter
// (superset of closed @dcsv-io/d2-private-* names).

import { existsSync, readdirSync, readFileSync } from "node:fs";
import { basename, dirname, join, resolve } from "node:path";
import { falsey } from "@dcsv-io/d2-utilities";
import { readNpmVersion, readNugetVersion } from "./manifest-editor.js";
import type { PackageDescriptor } from "./types.js";

// ---------------------------------------------------------------------------
// Non-consumable name exclusion list (npm) — public tooling / harnesses
// ---------------------------------------------------------------------------

const NPM_EXCLUDED_NAMES = new Set([
  "@dcsv-io/d2-typespec-decorators",
  "@dcsv-io/d2-typespec-emitters",
  "@dcsv-io/d2-contract-tests",
]);

/** Open public npm name: scoped d2 leaf without closed d2-private marker. */
export function isOpenPublicNpmName(name: string): boolean {
  return (
    name.startsWith("@dcsv-io/d2-") &&
    !name.includes("d2-private-") &&
    !NPM_EXCLUDED_NAMES.has(name)
  );
}

/** Closed private-framework npm name under @dcsv-io with d2-private marker. */
export function isPrivateConsumableNpmName(name: string): boolean {
  return name.startsWith("@dcsv-io/d2-") && name.includes("d2-private-");
}

/** Open public NuGet basename: DcsvIo.D2.* without .Private. segment. */
export function isOpenPublicNugetName(name: string): boolean {
  return (
    name.startsWith("DcsvIo.D2.") &&
    !name.includes(".Private.") &&
    name !== "DcsvIo.D2.Tests" &&
    !name.endsWith(".SourceGen")
  );
}

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

/**
 * Recursively walk a directory and collect all files matching a predicate.
 * Does not follow symlinks.
 */
function walk(
  dir: string,
  predicate: (filePath: string) => boolean,
  results: string[] = [],
): string[] {
  const entries = readdirSync(dir, { withFileTypes: true });

  for (const entry of entries) {
    const full = join(dir, entry.name);

    if (entry.isDirectory()) {
      walk(full, predicate, results);
    } else if (entry.isFile() && predicate(full)) {
      results.push(full);
    }
  }

  return results;
}

/**
 * Extract the `"name"` field from a package.json text, or return undefined
 * if the field is absent.
 */
function extractNpmName(text: string): string | undefined {
  const match = /"name"\s*:\s*"([^"]+)"/.exec(text);

  return match?.[1];
}

/**
 * Check whether a .csproj text carries a `<Version>` element.
 */
function csprojHasVersion(text: string): boolean {
  return /<Version>[^<]+<\/Version>/.test(text);
}

/**
 * Extract all `@dcsv-io/d2-*` dependency names from a package.json text.
 *
 * Scans both `"dependencies"` and `"devDependencies"` blocks. Returns every
 * key that starts with `"@dcsv-io/d2-"`. The caller filters the result against the
 * consumable name set.
 */
function extractNpmDeps(text: string): string[] {
  const deps: string[] = [];
  // Match scoped d2 package keys (open + private-marker forms).
  const DEP_RE = /"(@dcsv-io\/d2-[^"]+)"\s*:/g;
  let match: RegExpExecArray | null;

  while ((match = DEP_RE.exec(text)) !== null) {
    const name = match[1];

    if (name !== undefined && !deps.includes(name)) deps.push(name);
  }

  return deps;
}

/**
 * Extract all `<ProjectReference Include="...path...">` target csproj paths
 * from a `.csproj` text. Returns absolute paths resolved against `csprojDir`.
 *
 * Exported for testing only — the production consumers are inside this module.
 */
export function extractNugetProjectRefs(
  text: string,
  csprojDir: string,
): string[] {
  const refs: string[] = [];
  // Matches both single and double quotes, Windows and Unix path separators.
  const REF_RE = /<ProjectReference\s+Include=["']([^"']+\.csproj)["']/gi;
  let match: RegExpExecArray | null;

  while ((match = REF_RE.exec(text)) !== null) {
    const relPath = match[1];

    // Normalize backslash separators to forward slashes before resolving.
    // Windows .csproj Include attributes use backslashes; on POSIX `path.resolve`
    // treats `\` as a literal filename character, garbling the resolved path and
    // silently dropping the dependency.
    if (relPath !== undefined)
      refs.push(resolve(csprojDir, relPath.replace(/\\/g, "/")));
  }

  return refs;
}

function toRepoRelDir(manifestPath: string, repoRoot: string): string {
  return dirname(manifestPath)
    .replace(/\\/g, "/")
    .replace(repoRoot.replace(/\\/g, "/") + "/", "");
}

interface RawNpmEntry {
  manifestPath: string;
  name: string;
  text: string;
  currentVersion: string;
  dir: string;
  changelogPath: string;
}

function collectNpmFromRoots(
  repoRoot: string,
  searchRoots: string[],
  acceptName: (name: string) => boolean,
): PackageDescriptor[] {
  if (searchRoots.length === 0) return [];

  const packageJsonFiles = searchRoots.flatMap((root) =>
    walk(
      root,
      (f) => f.endsWith("package.json") && !f.includes("node_modules"),
    ),
  );

  const rawEntries: RawNpmEntry[] = [];

  for (const manifestPath of packageJsonFiles) {
    const text = readFileSync(manifestPath, "utf-8");
    const name = extractNpmName(text);

    if (name === undefined) continue;
    if (!acceptName(name)) continue;

    let currentVersion: string;

    try {
      currentVersion = readNpmVersion(manifestPath);
    } catch {
      continue;
    }

    rawEntries.push({
      manifestPath,
      name,
      text,
      currentVersion,
      dir: toRepoRelDir(manifestPath, repoRoot),
      changelogPath: join(dirname(manifestPath), "CHANGELOG.md"),
    });
  }

  const nameSet = new Set(rawEntries.map((e) => e.name));

  return rawEntries.map((e) => ({
    name: e.name,
    ecosystem: "npm" as const,
    dir: e.dir,
    manifestPath: e.manifestPath,
    changelogPath: e.changelogPath,
    currentVersion: e.currentVersion,
    dependencies: extractNpmDeps(e.text).filter((d) => nameSet.has(d)),
  }));
}

interface RawNugetEntry {
  manifestPath: string;
  name: string;
  text: string;
  currentVersion: string;
  dir: string;
  changelogPath: string;
}

function collectNugetFromRoots(
  repoRoot: string,
  searchRoots: string[],
  acceptName: (name: string) => boolean,
): PackageDescriptor[] {
  if (falsey(searchRoots)) return [];

  const csprojFiles: string[] = [];

  for (const root of searchRoots) {
    walk(
      root,
      (f) =>
        f.endsWith(".csproj") &&
        !f.endsWith("SourceGen.csproj") &&
        !f.endsWith("DcsvIo.D2.Tests.csproj"),
      csprojFiles,
    );
  }

  const rawEntries: RawNugetEntry[] = [];

  for (const manifestPath of csprojFiles) {
    const text = readFileSync(manifestPath, "utf-8");

    if (!csprojHasVersion(text)) continue;

    let currentVersion: string;

    try {
      currentVersion = readNugetVersion(manifestPath);
    } catch {
      continue;
    }

    const name = basename(manifestPath, ".csproj");

    if (!acceptName(name)) continue;

    rawEntries.push({
      manifestPath,
      name,
      text,
      currentVersion,
      dir: toRepoRelDir(manifestPath, repoRoot),
      changelogPath: join(dirname(manifestPath), "CHANGELOG.md"),
    });
  }

  const nameSet = new Set(rawEntries.map((e) => e.name));

  return rawEntries.map((e) => {
    const csprojDir = dirname(e.manifestPath);
    const refPaths = extractNugetProjectRefs(e.text, csprojDir);
    const dependencies = refPaths
      .map((p) => basename(p, ".csproj"))
      .filter((n) => nameSet.has(n));

    return {
      name: e.name,
      ecosystem: "nuget" as const,
      dir: e.dir,
      manifestPath: e.manifestPath,
      changelogPath: e.changelogPath,
      currentVersion: e.currentVersion,
      dependencies,
    };
  });
}

// ---------------------------------------------------------------------------
// Public publish lane
// ---------------------------------------------------------------------------

/**
 * Load PUBLIC consumable npm packages (open @dcsv-io/d2-* only).
 * Roots: public/packages/typescript only. Rejects d2-private- names.
 */
export function loadNpmPackages(repoRoot: string): PackageDescriptor[] {
  const tsSharedRoot = resolve(repoRoot, "public/packages/typescript");
  const searchRoots = [tsSharedRoot].filter(existsSync);

  return collectNpmFromRoots(repoRoot, searchRoots, isOpenPublicNpmName);
}

/**
 * Load PUBLIC consumable NuGet packages under public/packages/dotnet only.
 * Rejects basenames containing `.Private.` and the mega-tests project.
 */
export function loadNugetPackages(repoRoot: string): PackageDescriptor[] {
  const dotnetSharedRoot = resolve(repoRoot, "public/packages/dotnet");
  const searchRoots = [dotnetSharedRoot].filter(existsSync);

  return collectNugetFromRoots(repoRoot, searchRoots, (name) =>
    isOpenPublicNugetName(name),
  );
}

/**
 * Load all PUBLIC consumable packages (npm + nuget). Used by public `--list`
 * / publish discovery. Never includes private/** or d2-private- / .Private.
 */
export function loadAllPackages(repoRoot: string): PackageDescriptor[] {
  const npm = loadNpmPackages(repoRoot);
  const nuget = loadNugetPackages(repoRoot);

  return [...npm, ...nuget].sort((a, b) => a.name.localeCompare(b.name));
}

// ---------------------------------------------------------------------------
// Private consumable lane (§26.19 — versioned closed clients; not public list)
// ---------------------------------------------------------------------------

/**
 * Private consumable npm packages (e.g. KC client-ts). Separate from public
 * publish inventory; must never leak into loadNpmPackages / loadAllPackages.
 */
export function loadPrivateConsumableNpmPackages(
  repoRoot: string,
): PackageDescriptor[] {
  const kcClientTsRoot = resolve(
    repoRoot,
    "private/services/edge/key-custodian/client-ts",
  );
  const searchRoots = [kcClientTsRoot].filter(existsSync);

  return collectNpmFromRoots(repoRoot, searchRoots, isPrivateConsumableNpmName);
}

/**
 * Private consumable NuGet packages (e.g. KC .NET Client). Separate lane.
 */
export function loadPrivateConsumableNugetPackages(
  repoRoot: string,
): PackageDescriptor[] {
  const kcClientPath = resolve(
    repoRoot,
    "private/services/edge/key-custodian/client",
  );
  const searchRoots = [kcClientPath].filter(existsSync);

  return collectNugetFromRoots(
    repoRoot,
    searchRoots,
    (name) => name.includes(".Private.") && name.startsWith("DcsvIo.D2."),
  );
}

/**
 * Combined private consumable inventory (npm + nuget). Not used by public
 * `--list`; retained for §26.19 private versioning tooling.
 */
export function loadPrivateConsumablePackages(
  repoRoot: string,
): PackageDescriptor[] {
  const npm = loadPrivateConsumableNpmPackages(repoRoot);
  const nuget = loadPrivateConsumableNugetPackages(repoRoot);

  return [...npm, ...nuget].sort((a, b) => a.name.localeCompare(b.name));
}
