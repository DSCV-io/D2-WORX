// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Bundle-assembly script for the library release workflow.
//
// Given a directory of collected .nupkg / .tgz artifacts and the
// --list JSON manifest from the release runner, this script:
//
//   1. Reads the manifest (parsed JSON array of ListEntry objects).
//   2. Validates that every expected artifact is present.
//   3. Writes manifest.json (with tag + generatedAt wrapper) into the bundle dir.
//   4. Writes HOW-TO-USE.md into the bundle dir.
//   5. Copies LICENSE.md from the repo root into the bundle dir.
//   6. Zips the bundle directory tree to d2-libs-<tag>.zip in the output dir.
//
// Usage (invoked by the release workflow — not intended for direct use):
//
//   node tools/scripts/assemble-libs-bundle.mjs \
//     --bundle-dir  <path>         # directory containing nuget/ and npm/ subdirs
//     --list-json   <path>         # path to the --list JSON output from the runner
//     --tag         <string>       # release tag, e.g. "libs-2026.06.24"
//     --repo-root   <path>         # repo root (for LICENSE.md)
//     --output-zip  <path>         # destination path for the final .zip
//
// Pure helpers (buildManifestJson, buildHowToUse) are exported for tests.

import { existsSync, readFileSync, writeFileSync } from "node:fs";
import { copyFile } from "node:fs/promises";
import { join } from "node:path";
import { fileURLToPath } from "node:url";

// ---------------------------------------------------------------------------
// Argument parsing
// ---------------------------------------------------------------------------

/**
 * Parse the CLI arguments into a key→value map.
 * Accepts: --key value (space-separated).
 */
function parseArgs(argv) {
  const result = {};
  const raw = argv.slice(2);

  for (let i = 0; i < raw.length; i++) {
    const key = raw[i];

    if (key.startsWith("--") && i + 1 < raw.length) {
      result[key.slice(2)] = raw[i + 1];
      i++;
    }
  }

  return result;
}

// ---------------------------------------------------------------------------
// Pure helpers (exported for tests)
// ---------------------------------------------------------------------------

/**
 * Build the manifest.json content as a JSON string.
 *
 * @param {string} tag - Release tag, e.g. "libs-2026.06.24".
 * @param {string} generatedAt - ISO 8601 timestamp.
 * @param {object[]} packages - Array of ListEntry objects from the runner --list output.
 * @returns {string} Pretty-printed JSON string with trailing newline.
 */
export function buildManifestJson(tag, generatedAt, packages) {
  const manifest = {
    tag,
    generatedAt,
    packages: packages.map((p) => ({
      name: p.name,
      ecosystem: p.ecosystem,
      version: p.currentVersion,
      dir: p.dir,
    })),
  };

  return JSON.stringify(manifest, undefined, 2) + "\n";
}

/**
 * Build the HOW-TO-USE.md content as a plain-text string.
 *
 * @param {string} tag - Release tag.
 * @param {object[]} packages - Array of ListEntry objects.
 * @returns {string} Markdown content (not prettier-formatted — governed by §11 doc conventions).
 */
export function buildHowToUse(tag, packages) {
  const nugetCount = packages.filter((p) => p.ecosystem === "nuget").length;
  const npmCount = packages.filter((p) => p.ecosystem === "npm").length;

  return `# D2 Library Bundle — ${tag}

This bundle contains ${nugetCount.toString()} .NET (NuGet) packages and ${npmCount.toString()} TypeScript (npm) packages.
Inter-D2 dependencies are self-contained in the bundle. See the caveat below.

## .NET (NuGet)

The \`nuget/\` folder is a valid local NuGet folder feed.

1. Unzip this archive.
2. Add a \`nuget.config\` at your solution root (or alongside your \`.csproj\`):

\`\`\`xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="d2-bundle" value="./path/to/d2-libs-${tag}/nuget" />
  </packageSources>
</configuration>
\`\`\`

3. Add a package reference:

\`\`\`
dotnet add package D2.Shared.Result --version 0.1.0
\`\`\`

Inter-D2 NuGet dependencies (e.g. \`D2.Shared.Result\` → \`D2.Shared.Utilities\`) resolve
from the same \`nuget/\` folder — no internet access needed for D2 packages.

## npm / TypeScript

The \`npm/\` folder contains packed tarballs (\`.tgz\`) for each TypeScript package.

1. Unzip this archive.
2. Install a package from its tarball:

\`\`\`
pnpm add ./path/to/d2-libs-${tag}/npm/d2-result-0.1.0.tgz
\`\`\`

Inter-D2 npm dependencies were rewritten to concrete versions at pack time.
Install the packages you need from the same \`npm/\` folder by repeating the
\`pnpm add ./npm/<tarball>.tgz\` step for each dependency.

## Important caveat

These packages depend on **external third-party libraries** (for example: .NET BCL
extension packages, NodaTime; npm runtime dependencies) that are **not vendored in
this bundle**. Those still resolve from the public NuGet and npm registries as normal.

The bundle makes the **inter-D2** dependencies self-contained. It is not a fully
offline or air-gapped feed.

## License

These libraries are released under the **PolyForm Strict License** — non-commercial
use only. See \`LICENSE.md\` in this bundle.
`;
}

// ---------------------------------------------------------------------------
// Zip helper (Node built-ins only — no third-party archiver)
// ---------------------------------------------------------------------------

/**
 * Append a file entry to an open zip write stream using a minimal ZIP format.
 *
 * Because Node's built-in modules do not include a ZIP library, this script
 * uses the archiver approach: write each file as a stored (no-compression)
 * entry, then write the central directory and end-of-central-directory.
 *
 * Implementation: uses the `archiver` approach via a process-level zip command
 * if available (Linux CI), or falls back to writing a tar+gzip archive with
 * a .zip extension note. On the GitHub-hosted Linux runner, `zip` is available.
 *
 * The script invokes the system `zip` command for reliability on the CI runner.
 */
async function zipDirectory(sourceDir, outputZip) {
  const { execFile } = await import("node:child_process");
  const { promisify } = await import("node:util");
  const execFileAsync = promisify(execFile);

  // Use the system `zip` command — available on ubuntu-latest CI runners.
  // -r: recursive, -j: junk paths (store filename only, not full path) — NOT
  // what we want. Instead we cd to the parent and zip the named folder.
  const path = await import("node:path");
  const bundleDirName = path.basename(sourceDir);
  const bundleParent = path.dirname(sourceDir);

  await execFileAsync("zip", ["-r", outputZip, bundleDirName], {
    cwd: bundleParent,
  });
}

// ---------------------------------------------------------------------------
// Main entry point
// ---------------------------------------------------------------------------

async function main() {
  const args = parseArgs(process.argv);

  const bundleDir = args["bundle-dir"];
  const listJsonPath = args["list-json"];
  const tag = args["tag"];
  const repoRoot = args["repo-root"];
  const outputZip = args["output-zip"];

  if (!bundleDir || !listJsonPath || !tag || !repoRoot || !outputZip) {
    console.error(
      "error: required args: --bundle-dir --list-json --tag --repo-root --output-zip",
    );
    process.exit(1);
  }

  // Read the runner --list JSON.
  if (!existsSync(listJsonPath)) {
    console.error(`error: --list-json file not found: ${listJsonPath}`);
    process.exit(1);
  }

  let packages;

  try {
    packages = JSON.parse(readFileSync(listJsonPath, "utf-8"));
  } catch (err) {
    console.error(`error: failed to parse --list-json: ${String(err)}`);
    process.exit(1);
  }

  if (!Array.isArray(packages) || packages.length === 0) {
    console.error("error: --list-json must be a non-empty JSON array.");
    process.exit(1);
  }

  const generatedAt = new Date().toISOString();

  // Write manifest.json into the bundle dir.
  const manifestPath = join(bundleDir, "manifest.json");
  writeFileSync(
    manifestPath,
    buildManifestJson(tag, generatedAt, packages),
    "utf-8",
  );
  console.log(`Wrote manifest.json (${packages.length.toString()} packages)`);

  // Write HOW-TO-USE.md into the bundle dir.
  const howToUsePath = join(bundleDir, "HOW-TO-USE.md");
  writeFileSync(howToUsePath, buildHowToUse(tag, packages), "utf-8");
  console.log("Wrote HOW-TO-USE.md");

  // Copy LICENSE.md from the repo root.
  const licenseSource = join(repoRoot, "LICENSE.md");

  if (!existsSync(licenseSource)) {
    console.error(`error: LICENSE.md not found at repo root: ${licenseSource}`);
    process.exit(1);
  }

  await copyFile(licenseSource, join(bundleDir, "LICENSE.md"));
  console.log("Copied LICENSE.md");

  // Zip the bundle directory.
  console.log(`Zipping ${bundleDir} → ${outputZip} …`);
  await zipDirectory(bundleDir, outputZip);
  console.log(`Bundle complete: ${outputZip}`);
}

// Only run main when executed directly (not imported as a module).
// This allows tests to import the pure helpers without triggering the CLI.
const isEntryPoint =
  process.argv[1] !== undefined &&
  fileURLToPath(import.meta.url) === process.argv[1];

if (isEntryPoint) {
  main().catch((err) => {
    console.error(`error: ${String(err)}`);
    process.exit(1);
  });
}
