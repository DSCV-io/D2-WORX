// Copyright (c) DCSV. All rights reserved.
//
// Seeding tool: generates the public-API surface baselines + the output
// fingerprint baseline for every .NET CONSUMABLE in D2-WORX — the 54 total:
// 53 D2.Shared.* libraries under server/shared/dotnet (excluding the source-gen
// shells + the test project) plus the in-process KeyCustodian client.
//
// Per consumable it produces three committed baseline files next to the .csproj:
//
//   PublicAPI.Shipped.txt    — the released public API surface, one canonical
//                              text line per public type/member, as emitted by
//                              Microsoft.CodeAnalysis.PublicApiAnalyzers.
//   PublicAPI.Unshipped.txt  — new-but-unreleased surface; seeded empty (just
//                              the `#nullable enable` header) since everything
//                              currently in source is the released baseline.
//   .release-fingerprint     — the SHA-256 of the SOURCE-BASED composed tuple
//                              ( committed source dump + PublicAPI.Shipped +
//                              PublicAPI.Unshipped + resolved deps + toolchain
//                              pin ), the output-changed (PATCH-floor) signal.
//
// Output fingerprint = a SOURCE-BASED, PORTABLE hash — NOT a built-output hash.
// It hashes only committed text (source files, the PublicAPI report, the
// declared toolchain pin) plus the resolved-dep map, so it is byte-identical on
// every OS/machine with NO build to compute — a Windows-generated baseline
// equals a Linux-CI recompute by construction. The composition matches the
// production release-runner's composeSourceFingerprint byte-for-byte so the
// drift check (which recomputes via the runner) compares like-for-like.
//
// Mechanism:
//   1. Ensure both .txt files exist with the `#nullable enable` header, with
//      Shipped.txt reset to header-only so the build reports the FULL current
//      surface as RS0016 (every public symbol is "missing from the baseline").
//   2. Build the package and parse the analyzer's RS0016 diagnostics — each
//      carries `Symbol '<canonical API line>' is not part of the declared
//      public API`. The exact API line is extracted from every RS0016 message.
//      This captures the COMPLETE enforced surface — hand-authored AND
//      source-generated public symbols (the `dotnet format` code-fix skips
//      generated files, so the build-diagnostic parse is the faithful source).
//      (This is the ONLY build the seed runs, and only to seed the .txt — the
//      fingerprint itself never builds.)
//   3. Write the extracted lines (sorted, deduplicated, ordinal) into
//      Shipped.txt; leave Unshipped.txt header-only. The result exactly matches
//      the current public API, so a subsequent build reports zero RS0016
//      (missing-from-baseline) / RS0017 (in-baseline-not-in-source).
//   4. FINGERPRINT: compose the source-based hash over the ordered tuple
//      ( committed source dump + Shipped.txt + Unshipped.txt + resolved deps +
//      toolchain pin ) and write the hex digest to .release-fingerprint. The
//      source dump globs every committed *.cs (incl. Generated/**/*.g.cs) + the
//      *.csproj; the deps carry the package version + every consumable
//      ProjectReference dep's pinned version (so a dependency bump moves the
//      dependent's fingerprint — propagation falls out of the fingerprint); the
//      toolchain pin hashes the declared SDK / TargetFramework / LangVersion.
//
// Run from the repo root: `node tools/scripts/seed-publicapi-baselines.mjs`.
// Optional `--package <PackageId>` limits the run to a single consumable.
//
// IDEMPOTENT: re-running regenerates byte-identical baseline files. The promote
// step sorts deterministically; the source-based hash is platform/path-
// independent, so a re-seed over unchanged source is a no-op (the script reports
// "unchanged" for each). A baseline that is already correct is left untouched.

import { spawnSync } from "node:child_process";
import { createHash } from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const REPO_ROOT = path.resolve(
  path.dirname(fileURLToPath(import.meta.url)),
  "..",
  "..",
);

const NULLABLE_HEADER = "#nullable enable";

// ---------------------------------------------------------------------------
// CLI args.
// ---------------------------------------------------------------------------

const args = process.argv.slice(2);
const packageFilterIdx = args.indexOf("--package");
const packageFilter =
  packageFilterIdx !== -1 ? args[packageFilterIdx + 1] : undefined;

// ---------------------------------------------------------------------------
// Inventory discovery — the 53 shared-tree consumables + the KC client.
// Mirrors tools/scripts/seed-package-metadata.mjs: a consumable is a .csproj
// that is NOT a *SourceGen shell and NOT the test project. The KC client is
// added explicitly (it lives outside server/shared/dotnet).
// ---------------------------------------------------------------------------

/** Recursively collect files under a directory, skipping build/output dirs. */
function walk(dir, out = []) {
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);

    if (entry.isDirectory()) {
      if (
        ["node_modules", "dist", "bin", "obj", "Generated"].includes(entry.name)
      ) {
        continue;
      }

      walk(full, out);
    } else {
      out.push(full);
    }
  }

  return out;
}

const sharedDotnetRoot = path.join(REPO_ROOT, "server", "shared", "dotnet");

const sharedConsumables = walk(sharedDotnetRoot)
  .filter((f) => f.endsWith(".csproj"))
  .filter((f) => !f.endsWith("SourceGen.csproj"))
  .filter((f) => !/D2\.Shared\.Tests\.csproj$/.test(f));

const kcClient = path.join(
  REPO_ROOT,
  "server",
  "services",
  "edge",
  "key-custodian",
  "clients",
  "D2.Edge.KeyCustodian.Clients.csproj",
);

// The FULL consumable set (never filtered) — needed to resolve dependency
// versions for the manifest-metadata fingerprint input even on a --package run.
const allConsumables = [...sharedConsumables, kcClient].sort();

let consumables = [...allConsumables];

if (packageFilter) {
  consumables = consumables.filter(
    (f) => path.basename(f, ".csproj") === packageFilter,
  );

  if (consumables.length === 0) {
    console.error(`No consumable matches --package ${packageFilter}`);
    process.exit(1);
  }
}

// ---------------------------------------------------------------------------
// Manifest-metadata model (version + consumable dependency versions).
//
// The fingerprint folds in the package version + every consumable
// ProjectReference dep's pinned version, so a dependency bump moves the
// dependent's fingerprint (propagation). This MUST byte-match the production
// release-runner's buildNugetManifestMeta / composeSourceFingerprint so the
// drift check compares like-for-like.
// ---------------------------------------------------------------------------

const CONSUMABLE_NAMES = new Set(
  allConsumables.map((f) => path.basename(f, ".csproj")),
);

/** Extract the `<Version>` element from a csproj text, or "" when absent. */
function extractVersion(csprojText) {
  const match = /<Version>([^<]+)<\/Version>/.exec(csprojText);

  return match ? match[1].trim() : "";
}

/**
 * Extract the consumable ProjectReference dependency names from a csproj text.
 * Non-consumable edges (SourceGen shells, the test project) are filtered out.
 */
function extractConsumableDeps(csprojText) {
  const deps = [];
  const refRe = /<ProjectReference\s+Include=["']([^"']+\.csproj)["']/gi;
  let match;

  while ((match = refRe.exec(csprojText)) !== null) {
    // Normalize backslash separators to forward slashes before basename extraction.
    // Windows .csproj Include attributes use backslashes; on POSIX `path.basename`
    // treats `\` as a literal filename character, returning the whole path instead
    // of the filename component and silently dropping the dependency.
    const depName = path.basename(match[1].replace(/\\/g, "/"), ".csproj");

    if (CONSUMABLE_NAMES.has(depName) && !deps.includes(depName)) {
      deps.push(depName);
    }
  }

  return deps;
}

// Build the package-id → current version map across ALL consumables.
const versionByName = new Map();

for (const csprojPath of allConsumables) {
  const text = fs.readFileSync(csprojPath, "utf8");
  versionByName.set(path.basename(csprojPath, ".csproj"), extractVersion(text));
}

/**
 * Build the deterministic manifest-metadata JSON for a package. Mirrors the
 * release-runner's buildNugetManifestMeta (deps sorted; each mapped to its
 * resolved version, which at seed time IS its committed version).
 */
function buildManifestMeta(packageId, csprojText) {
  const deps = {};

  for (const depName of extractConsumableDeps(csprojText).sort()) {
    deps[depName] = versionByName.get(depName) ?? "";
  }

  return JSON.stringify({
    packageId,
    version: versionByName.get(packageId) ?? "",
    deps,
  });
}

// ---------------------------------------------------------------------------
// Baseline file helpers.
// ---------------------------------------------------------------------------

/** Detect the line ending used by an existing file (default LF). */
function detectEol(filePath) {
  if (!fs.existsSync(filePath)) return "\n";

  return fs.readFileSync(filePath, "utf8").includes("\r\n") ? "\r\n" : "\n";
}

/** Write a baseline .txt with the header + sorted API lines, LF-normalized. */
function writeApiFile(filePath, apiLines) {
  const body = [NULLABLE_HEADER, ...apiLines].join("\n");
  fs.writeFileSync(filePath, body + "\n", "utf8");
}

/** Read the non-header, non-empty API lines from a baseline .txt. */
function readApiLines(filePath) {
  if (!fs.existsSync(filePath)) return [];

  return fs
    .readFileSync(filePath, "utf8")
    .split(/\r?\n/)
    .map((l) => l.trimEnd())
    .filter((l) => l.length > 0 && l !== NULLABLE_HEADER);
}

/** Ensure a baseline .txt exists with the `#nullable enable` header. */
function ensureHeaderFile(filePath) {
  if (!fs.existsSync(filePath)) {
    fs.writeFileSync(filePath, NULLABLE_HEADER + "\n", "utf8");
  }
}

/** Deterministic API-line set: deduplicate + ordinal sort. */
function normalizeApiLines(lines) {
  return [...new Set(lines)].sort();
}

// ---------------------------------------------------------------------------
// dotnet shell-outs.
// ---------------------------------------------------------------------------

function run(command, commandArgs) {
  return spawnSync(command, commandArgs, {
    cwd: REPO_ROOT,
    encoding: "utf8",
    maxBuffer: 64 * 1024 * 1024,
  });
}

// RS0016 diagnostic shape (severity is `warning` here because the extraction
// build runs with TreatWarningsAsErrors=false):
//   ...warning RS0016: Symbol '<canonical API line>' is not part of the declared
//   public API ...[<csproj>]
// The API line sits between `Symbol '` and the LAST `' is not part of the
// declared public API` on the line (last-occurrence guards the unlikely case of
// that phrase appearing inside a symbol signature). The severity-agnostic infix
// `RS0016: Symbol '` matches whether the diagnostic is reported as a warning or
// an error.
const RS0016_PREFIX = "RS0016: Symbol '";
const RS0016_SUFFIX = "' is not part of the declared public API";

/** Build the package and extract every public-API line from its RS0016 lines. */
function extractSurfaceViaBuild(csprojPath, packageId) {
  // Debug build with no extra flags — the analyzer runs automatically. RS0016
  // is an error under the solution-wide TreatWarningsAsErrors policy, so the
  // build "fails", but its stdout carries every RS0016 diagnostic we need.
  // A single-csproj build still compiles dependency projects; if a dependency's
  // baseline is also header-only it emits its OWN RS0016 lines into the same
  // output. Each RS0016 line ends with `[<owning-csproj-path>]`, so we filter
  // to only the lines whose owning csproj is THIS package's — never a dep's.
  // -p:WarningsNotAsErrors=RS0016;RS0017 demotes both analyzer diagnostics from
  // error to warning for THIS build invocation only (no project-file edit). This
  // lets the build SUCCEED regardless of whether a dependency's baseline is
  // header-only (→ its surface shows as RS0016) or stale (→ RS0017): the
  // dependency emits warnings without failing the build, so the order in which
  // packages are seeded does not matter. The diagnostics still appear in the
  // output; we attribute each to its owning csproj below.
  // -p:TreatWarningsAsErrors=false demotes EVERY warning (including the whole
  // PublicApiAnalyzers RS00xx family — RS0016 new-API, RS0017 removed-API,
  // RS0026/RS0027 optional-param-overload backcompat, RS0041 oblivious-nullable,
  // and any future rule) from error to warning for THIS build invocation only,
  // with no project-file edit. The extraction build therefore ALWAYS succeeds
  // regardless of the baseline state of this package or its dependencies; the
  // RS0016 diagnostics still appear in the output as warnings, attributed below
  // to their owning csproj. (Demoting the whole TWAE flag is more robust than
  // enumerating each RS00xx id, which drifts as the analyzer adds rules.)
  // RS0026/RS0027 fire only while a symbol is missing from the baseline; once
  // seeded into Shipped.txt the normal solution build treats them as already-
  // shipped and they do not fire.
  // --no-incremental forces a full recompile so the analyzer re-runs and re-
  // emits its RS0016 diagnostics. Without it MSBuild can skip the rebuild when
  // it judges outputs up-to-date (AdditionalFiles content changes are not always
  // tracked as recompile triggers), leaving zero RS0016 in the output and an
  // empty — wrong — extracted surface.
  const result = run("dotnet", [
    "build",
    csprojPath,
    "--no-restore",
    "--no-incremental",
    "-p:TreatWarningsAsErrors=false",
  ]);
  const output = (result.stdout ?? "") + "\n" + (result.stderr ?? "");

  // MSBuild appends the owning project as `[<full-csproj-path>]` — e.g.
  // `[C:\...\D2.Shared.Time.csproj]`. Match the basename immediately followed by
  // the closing bracket (`Time.csproj]`) so a diagnostic is attributed to THIS
  // package and never to a dependency whose path also appears in the build.
  const csprojBasename = path.basename(csprojPath);
  const ownerTag = `${csprojBasename}]`;

  // A build with zero RS0016 for this package means the baseline already matches
  // the source — an empty surface is the correct extraction in that case.
  const lines = output.split(/\r?\n/);
  const surface = [];

  for (const line of lines) {
    const start = line.indexOf(RS0016_PREFIX);

    if (start === -1) continue;

    // Attribute the diagnostic to its owning project. MSBuild appends
    // `[<full-csproj-path>]`; matching on the basename in brackets is enough to
    // distinguish this package from its dependencies (package ids are unique).
    if (!line.includes(ownerTag)) continue;

    const symStart = start + RS0016_PREFIX.length;
    const symEnd = line.lastIndexOf(RS0016_SUFFIX);

    if (symEnd <= symStart) continue;

    surface.push(line.slice(symStart, symEnd));
  }

  // Guard: with RS0016 demoted to a warning, a non-zero exit is a GENUINE build
  // failure (a real compile error or some other analyzer error) — surface it
  // loudly rather than silently writing a partial baseline.
  if (result.status !== 0) {
    const errorLines = lines
      .filter((l) => / error /.test(l))
      .slice(0, 20)
      .join("\n");

    throw new Error(
      `build failed for ${packageId} (exit ${result.status}):\n` +
        (errorLines.length > 0 ? errorLines : output.slice(0, 2000)),
    );
  }

  return surface;
}

/** LF-normalize so a CRLF/LF checkout difference cannot perturb the hash. */
function normalizeLf(text) {
  return text.replace(/\r\n/g, "\n");
}

// ---------------------------------------------------------------------------
// Source dump + toolchain pin (mirrors release-runner/src/source-fingerprint.ts
// BYTE-FOR-BYTE; the seed↔provider identity is pinned by a runner test).
// ---------------------------------------------------------------------------

const SKIP_DIRS = new Set([
  "bin",
  "obj",
  "dist",
  "node_modules",
  "etc",
  "tests",
]);

/** True when a package-relative path lives under a skipped directory. */
function isSkipped(relPosix) {
  return relPosix.split("/").some((seg) => SKIP_DIRS.has(seg));
}

/** Is this committed file part of the .NET source dump? (every *.cs + *.csproj) */
function isNugetSourceFile(relPosixPath) {
  const base = relPosixPath.slice(relPosixPath.lastIndexOf("/") + 1);

  if (
    base === ".release-fingerprint" ||
    base === "PublicAPI.Shipped.txt" ||
    base === "PublicAPI.Unshipped.txt" ||
    base === "CHANGELOG.md"
  ) {
    return false;
  }

  return base.endsWith(".cs") || base.endsWith(".csproj");
}

/**
 * List the package-relative POSIX paths of COMMITTED (git-tracked) .NET source
 * files. Tracked-only is mandatory: the build emits gitignored transients (e.g.
 * the non-deterministic LoggerMessage.g.cs) into Generated/ that a plain fs walk
 * would fold into the fingerprint, breaking portability. Mirrors the
 * release-runner's listSourceFiles BYTE-FOR-BYTE.
 */
function listNugetSourceFiles(packageDir) {
  const result = spawnSync("git", ["ls-files", "--", "."], {
    cwd: packageDir,
    encoding: "utf8",
    maxBuffer: 32 * 1024 * 1024,
  });

  if (result.status !== 0) return [];

  return (result.stdout ?? "")
    .split("\n")
    .map((l) => l.trim().replace(/\\/g, "/"))
    .filter((l) => l.length > 0 && !isSkipped(l) && isNugetSourceFile(l));
}

/** Ordered, LF-normalized source dump for a package dir. */
function buildSourceDump(packageDir) {
  const sorted = listNugetSourceFiles(packageDir).sort();
  let dump = "";

  for (const relPath of sorted) {
    const content = fs.readFileSync(path.join(packageDir, relPath), "utf8");
    dump += `F:${relPath}\n${normalizeLf(content)}\n`;
  }

  return dump;
}

/** Serialize keys ascending so structurally equal inputs serialize identically. */
function stableJson(obj) {
  const sortedKeys = Object.keys(obj).sort();
  const ordered = {};

  for (const key of sortedKeys) ordered[key] = obj[key] ?? "";

  return JSON.stringify(ordered);
}

/** Extract the inner text of the first <Element>...</Element> in XML text. */
function extractXmlElement(xml, element) {
  const match = new RegExp(`<${element}>([^<]+)</${element}>`).exec(xml);

  return match ? match[1].trim() : "";
}

/** The declared, committed .NET toolchain pin as deterministic sorted-key JSON. */
function readNugetToolchainPin() {
  const globalJson = JSON.parse(
    fs.readFileSync(path.join(REPO_ROOT, "server", "global.json"), "utf8"),
  );
  const buildProps = fs.readFileSync(
    path.join(REPO_ROOT, "server", "Directory.Build.props"),
    "utf8",
  );

  return stableJson({
    langVersion: extractXmlElement(buildProps, "LangVersion"),
    rollForward: globalJson.sdk?.rollForward ?? "",
    sdk: globalJson.sdk?.version ?? "",
    targetFramework: extractXmlElement(buildProps, "TargetFramework"),
  });
}

const TOOLCHAIN_PIN = readNugetToolchainPin();

/**
 * Compose the source-based fingerprint over the ordered tuple
 *   ( committed source dump + Shipped.txt + Unshipped.txt + deps + toolchain ).
 *
 * Byte-identical to the release-runner's composeSourceFingerprint so the drift
 * check (which recomputes via the runner) compares like-for-like. No build.
 */
function composeFingerprint(csprojPath, packageId, shippedTxt, unshippedTxt) {
  const packageDir = path.dirname(csprojPath);
  const sourceDump = buildSourceDump(packageDir);
  const csprojText = fs.readFileSync(csprojPath, "utf8");
  const depsJson = buildManifestMeta(packageId, csprojText);
  // Mirror the provider: apiReport = shippedTxt + unshippedTxt, LF-normalized
  // once at compose time (composeSourceFingerprint does the single normalizeLf).
  const apiReport = shippedTxt + unshippedTxt;

  const hash = createHash("sha256");
  hash.update(`SOURCE:\n${sourceDump}\n`);
  hash.update(`APIREPORT:\n${normalizeLf(apiReport)}\n`);
  hash.update(`DEPS:\n${depsJson}\n`);
  hash.update(`TOOLCHAIN:\n${TOOLCHAIN_PIN}\n`);

  return hash.digest("hex");
}

// ---------------------------------------------------------------------------
// Per-consumable seed.
// ---------------------------------------------------------------------------

function seedConsumable(csprojPath) {
  const packageId = path.basename(csprojPath, ".csproj");
  const dir = path.dirname(csprojPath);
  const shippedPath = path.join(dir, "PublicAPI.Shipped.txt");
  const unshippedPath = path.join(dir, "PublicAPI.Unshipped.txt");
  const fingerprintPath = path.join(dir, ".release-fingerprint");

  const before = {
    shipped: readApiLines(shippedPath),
    fingerprint: fs.existsSync(fingerprintPath)
      ? fs.readFileSync(fingerprintPath, "utf8").trim()
      : undefined,
  };

  // 1. Reset both .txt files to header-only. A header-only Shipped.txt makes
  //    the build report the FULL current surface as RS0016 (every public symbol
  //    is "missing from the baseline"); a header-only Unshipped.txt means no
  //    RS0017 (removed) noise.
  writeApiFile(shippedPath, []);
  writeApiFile(unshippedPath, []);

  // 2. Build + parse RS0016 to extract the complete enforced public surface
  //    (hand-authored AND source-generated symbols).
  const surface = normalizeApiLines(
    extractSurfaceViaBuild(csprojPath, packageId),
  );

  // 3. Write the surface into Shipped.txt; Unshipped.txt stays header-only.
  writeApiFile(shippedPath, surface);
  writeApiFile(unshippedPath, []);

  // 4. Compose the source-based fingerprint over the committed source dump +
  //    the EXACT written PublicAPI.* file content + the resolved deps + the
  //    toolchain pin. The release-runner's provider reads these same files
  //    verbatim, so reading the exact bytes back keeps the seeded hash equal to
  //    the runtime recompute (no build).
  const shippedTxt = fs.readFileSync(shippedPath, "utf8");
  const unshippedTxt = fs.readFileSync(unshippedPath, "utf8");
  const fingerprint = composeFingerprint(
    csprojPath,
    packageId,
    shippedTxt,
    unshippedTxt,
  );
  const fpEol = detectEol(fingerprintPath);
  fs.writeFileSync(fingerprintPath, fingerprint + fpEol, "utf8");

  const apiChanged =
    before.shipped.length !== surface.length ||
    before.shipped.some((l, i) => l !== surface[i]);
  const fpChanged = before.fingerprint !== fingerprint;

  return {
    packageId,
    apiLineCount: surface.length,
    apiChanged,
    fpChanged,
  };
}

// ---------------------------------------------------------------------------
// Run.
// ---------------------------------------------------------------------------

// Pre-pass: ensure EVERY consumable has both .txt baselines on disk BEFORE any
// build runs. The Directory.Build.props references each file by a literal
// AdditionalFiles path; a missing file becomes a CS2001 "source file could not
// be found" the moment a consumable (or any consumable that depends on it
// transitively) is built. When --package targets a single consumable, only its
// own baselines are touched and its dependencies keep their committed baselines.
// A full run (no --package) resets EVERY Shipped.txt to header-only so any
// cross-package contamination from a prior partial run is cleared before the
// per-package extraction rebuilds each surface from scratch.
const fullRun = !packageFilter;

for (const csprojPath of consumables) {
  const dir = path.dirname(csprojPath);
  const shippedPath = path.join(dir, "PublicAPI.Shipped.txt");
  const unshippedPath = path.join(dir, "PublicAPI.Unshipped.txt");

  if (fullRun) {
    writeApiFile(shippedPath, []);
    writeApiFile(unshippedPath, []);
  } else {
    ensureHeaderFile(shippedPath);
    ensureHeaderFile(unshippedPath);
  }
}

const results = [];

for (const csprojPath of consumables) {
  process.stderr.write(`seeding ${path.basename(csprojPath, ".csproj")} ... `);

  try {
    const r = seedConsumable(csprojPath);
    results.push(r);
    process.stderr.write(
      `${r.apiLineCount} api lines` +
        `${r.apiChanged || r.fpChanged ? " (updated)" : " (unchanged)"}\n`,
    );
  } catch (err) {
    process.stderr.write("FAILED\n");
    console.error(err instanceof Error ? err.message : String(err));
    process.exit(1);
  }
}

console.log(
  JSON.stringify(
    {
      consumables: results.length,
      updated: results.filter((r) => r.apiChanged || r.fpChanged).length,
      unchanged: results.filter((r) => !r.apiChanged && !r.fpChanged).length,
      packages: results.map((r) => ({
        packageId: r.packageId,
        apiLines: r.apiLineCount,
      })),
    },
    null,
    2,
  ),
);
