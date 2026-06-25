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

import { existsSync, readdirSync, readFileSync, statSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
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

  const descriptors: PackageDescriptor[] = [];

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

    descriptors.push({
      name,
      ecosystem: "npm",
      dir,
      manifestPath,
      changelogPath,
      currentVersion,
    });
  }

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

  if (searchRoots.length === 0) return [];

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

  const descriptors: PackageDescriptor[] = [];

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
    const filename = manifestPath.replace(/\\/g, "/").split("/").pop() ?? "";
    const name = filename.replace(/\.csproj$/, "");

    const dir = dirname(manifestPath)
      .replace(/\\/g, "/")
      .replace(repoRoot.replace(/\\/g, "/") + "/", "");

    const changelogPath = join(dirname(manifestPath), "CHANGELOG.md");

    descriptors.push({
      name,
      ecosystem: "nuget",
      dir,
      manifestPath,
      changelogPath,
      currentVersion,
    });
  }

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
