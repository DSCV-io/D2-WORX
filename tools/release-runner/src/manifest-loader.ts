// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Manifest loader — discovers consumable packages from the repository tree.
//
// A package is consumable iff it matches the classification rule:
//   npm:   a package.json under server/shared/typescript/**  that is NOT
//          private tooling (typespec-decorators, typespec-emitters) and NOT
//          a test harness (contract-tests). Specifically: every package whose
//          name starts with "@d2/" (scoped) and is not in the exclusion list.
//   nuget: a .csproj under server/shared/dotnet/** that is NOT a SourceGen
//          shell (filename ends *SourceGen.csproj) and NOT the shared test
//          project (D2.Shared.Tests.csproj). Additionally, the consumable
//          csproj must carry a <Version> element (it was seeded in Wave A).
//
// The loader also accepts the KC client csproj if present under
// server/services/edge/key-custodian/clients/.
//
// Both the npm and nuget loaders read the version from the manifest so the
// caller does not need a separate readManifestVersion call.

import { existsSync, readdirSync, readFileSync } from "node:fs";
import { basename, dirname, join, resolve } from "node:path";
import { falsey } from "@d2/utilities";
import { readNpmVersion, readNugetVersion } from "./manifest-editor.js";
import type { PackageDescriptor } from "./types.js";

// ---------------------------------------------------------------------------
// Non-consumable name exclusion list (npm)
// ---------------------------------------------------------------------------

const NPM_EXCLUDED_NAMES = new Set([
  "@d2/typespec-decorators",
  "@d2/typespec-emitters",
  "@d2/contract-tests",
]);

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
 * Extract all `@d2/*` dependency names from a package.json text.
 *
 * Scans both `"dependencies"` and `"devDependencies"` blocks. Returns every
 * key that starts with `"@d2/"`. The caller filters the result against the
 * consumable name set.
 */
function extractNpmDeps(text: string): string[] {
  // Match both "dependencies" and "devDependencies" blocks.
  const deps: string[] = [];
  // Regex matches "@d2/something": "..." in any JSON object property.
  // Using a simple per-key match is sufficient since package names are well-formed.
  const DEP_RE = /"(@d2\/[^"]+)"\s*:/g;
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
 */
function extractNugetProjectRefs(text: string, csprojDir: string): string[] {
  const refs: string[] = [];
  // Matches both single and double quotes, Windows and Unix path separators.
  const REF_RE = /<ProjectReference\s+Include=["']([^"']+\.csproj)["']/gi;
  let match: RegExpExecArray | null;

  while ((match = REF_RE.exec(text)) !== null) {
    const relPath = match[1];

    if (relPath !== undefined) refs.push(resolve(csprojDir, relPath));
  }

  return refs;
}

// ---------------------------------------------------------------------------
// Public API
// ---------------------------------------------------------------------------

/**
 * Load the consumable npm package inventory from the repo tree.
 *
 * @param repoRoot - Absolute path to the repository root.
 * @returns PackageDescriptor[] for all consumable @d2/* packages.
 */
export function loadNpmPackages(repoRoot: string): PackageDescriptor[] {
  const tsSharedRoot = resolve(repoRoot, "server/shared/typescript");

  if (!existsSync(tsSharedRoot)) return [];

  const packageJsonFiles = walk(
    tsSharedRoot,
    (f) => f.endsWith("package.json") && !f.includes("node_modules"),
  );

  // First pass: collect name + raw text so we can resolve deps after building
  // the consumable name set.
  interface RawEntry {
    manifestPath: string;
    name: string;
    text: string;
    currentVersion: string;
    dir: string;
    changelogPath: string;
  }

  const rawEntries: RawEntry[] = [];

  for (const manifestPath of packageJsonFiles) {
    const text = readFileSync(manifestPath, "utf-8");
    const name = extractNpmName(text);

    if (name === undefined) continue;
    if (!name.startsWith("@d2/")) continue;
    if (NPM_EXCLUDED_NAMES.has(name)) continue;

    let currentVersion: string;

    try {
      currentVersion = readNpmVersion(manifestPath);
    } catch {
      continue; // skip packages without a parseable version
    }

    const dir = dirname(manifestPath)
      .replace(/\\/g, "/")
      .replace(repoRoot.replace(/\\/g, "/") + "/", "");

    const changelogPath = join(dirname(manifestPath), "CHANGELOG.md");

    rawEntries.push({
      manifestPath,
      name,
      text,
      currentVersion,
      dir,
      changelogPath,
    });
  }

  // Build consumable name set for edge filtering.
  const nameSet = new Set(rawEntries.map((e) => e.name));

  // Second pass: resolve dependencies against the consumable set.
  const descriptors: PackageDescriptor[] = rawEntries.map((e) => ({
    name: e.name,
    ecosystem: "npm",
    dir: e.dir,
    manifestPath: e.manifestPath,
    changelogPath: e.changelogPath,
    currentVersion: e.currentVersion,
    dependencies: extractNpmDeps(e.text).filter((d) => nameSet.has(d)),
  }));

  return descriptors;
}

/**
 * Load the consumable NuGet package inventory from the repo tree.
 *
 * Includes:
 *   - All D2.Shared.*.csproj under server/shared/dotnet/** that carry
 *     <Version> and are NOT source-gen shells or the test project.
 *   - The KC client csproj if present (carries <Version>).
 *
 * @param repoRoot - Absolute path to the repository root.
 * @returns PackageDescriptor[] for all consumable NuGet packages.
 */
export function loadNugetPackages(repoRoot: string): PackageDescriptor[] {
  const dotnetSharedRoot = resolve(repoRoot, "server/shared/dotnet");
  const kcClientPath = resolve(
    repoRoot,
    "server/services/edge/key-custodian/clients",
  );

  const searchRoots: string[] = [];

  if (existsSync(dotnetSharedRoot)) searchRoots.push(dotnetSharedRoot);
  if (existsSync(kcClientPath)) searchRoots.push(kcClientPath);

  if (falsey(searchRoots)) return [];

  const csprojFiles: string[] = [];

  for (const root of searchRoots) {
    walk(
      root,
      (f) =>
        f.endsWith(".csproj") &&
        !f.endsWith("SourceGen.csproj") &&
        !f.endsWith("D2.Shared.Tests.csproj"),
      csprojFiles,
    );
  }

  // First pass: gather raw entries to build the consumable name set.
  interface RawEntry {
    manifestPath: string;
    name: string;
    text: string;
    currentVersion: string;
    dir: string;
    changelogPath: string;
  }

  const rawEntries: RawEntry[] = [];

  for (const manifestPath of csprojFiles) {
    const text = readFileSync(manifestPath, "utf-8");

    // Skip csprojs without a Version element (unseeded or non-consumable).
    if (!csprojHasVersion(text)) continue;

    let currentVersion: string;

    try {
      currentVersion = readNugetVersion(manifestPath);
    } catch {
      continue;
    }

    // Derive package name from the manifest filename (strip extension).
    const name = basename(manifestPath, ".csproj");

    const dir = dirname(manifestPath)
      .replace(/\\/g, "/")
      .replace(repoRoot.replace(/\\/g, "/") + "/", "");

    const changelogPath = join(dirname(manifestPath), "CHANGELOG.md");

    rawEntries.push({
      manifestPath,
      name,
      text,
      currentVersion,
      dir,
      changelogPath,
    });
  }

  // Build consumable name set for edge filtering.
  const nameSet = new Set(rawEntries.map((e) => e.name));

  // Second pass: resolve <ProjectReference> edges against the consumable set.
  const descriptors: PackageDescriptor[] = rawEntries.map((e) => {
    const csprojDir = dirname(e.manifestPath);
    const refPaths = extractNugetProjectRefs(e.text, csprojDir);
    const dependencies = refPaths
      .map((p) => basename(p, ".csproj"))
      .filter((n) => nameSet.has(n));

    return {
      name: e.name,
      ecosystem: "nuget",
      dir: e.dir,
      manifestPath: e.manifestPath,
      changelogPath: e.changelogPath,
      currentVersion: e.currentVersion,
      dependencies,
    };
  });

  return descriptors;
}

/**
 * Load all consumable packages (npm + nuget) from the repo tree.
 *
 * @param repoRoot - Absolute path to the repository root.
 * @returns Combined PackageDescriptor[] sorted by name.
 */
export function loadAllPackages(repoRoot: string): PackageDescriptor[] {
  const npm = loadNpmPackages(repoRoot);
  const nuget = loadNugetPackages(repoRoot);

  return [...npm, ...nuget].sort((a, b) => a.name.localeCompare(b.name));
}
