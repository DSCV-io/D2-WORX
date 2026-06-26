// Copyright (c) DCSV. All rights reserved.
//
// Seeding tool: generates the public-API surface baselines + the output
// fingerprint baseline for every .NET CONSUMABLE in D2-WORX — the 53
// D2.Shared.* libraries under server/shared/dotnet (excluding the source-gen
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
//   .release-fingerprint     — the SHA-256 of the debug-stripped Release DLL,
//                              the output-changed (PATCH-floor) signal.
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
//   3. Write the extracted lines (sorted, deduplicated, ordinal) into
//      Shipped.txt; leave Unshipped.txt header-only. The result exactly matches
//      the current public API, so a subsequent build reports zero RS0016
//      (missing-from-baseline) / RS0017 (in-baseline-not-in-source).
//   4. FINGERPRINT: build the package with debug info stripped
//        `dotnet build <csproj> -c Release -p:DebugType=none -p:DebugSymbols=false
//         -p:TreatWarningsAsErrors=false --no-restore --no-incremental`
//      and SHA-256 the output DLL; write the hex digest to .release-fingerprint.
//
// Run from the repo root: `node tools/scripts/seed-publicapi-baselines.mjs`.
// Optional `--package <PackageId>` limits the run to a single consumable.
//
// IDEMPOTENT: re-running regenerates byte-identical baseline files. The promote
// step sorts deterministically; the fingerprint is a content hash of a
// debug-stripped deterministic build, so a re-seed over unchanged source is a
// no-op (the script reports "unchanged" for each). A baseline that is already
// correct is detected and left untouched.

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
const TARGET_FRAMEWORK = "net10.0";

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

let consumables = [...sharedConsumables, kcClient].sort();

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

/** Build debug-stripped + SHA-256 the output DLL. */
function fingerprintDll(csprojPath, packageId) {
  const build = run("dotnet", [
    "build",
    csprojPath,
    "-c",
    "Release",
    "-p:DebugType=none",
    "-p:DebugSymbols=false",
    "-p:TreatWarningsAsErrors=false",
    "--no-restore",
    "--no-incremental",
  ]);

  if (build.status !== 0) {
    throw new Error(
      `fingerprint build failed for ${packageId} (exit ${build.status}):\n` +
        `${(build.stdout ?? "") + (build.stderr ?? "")}`.slice(0, 2000),
    );
  }

  const dllPath = path.join(
    path.dirname(csprojPath),
    "bin",
    "Release",
    TARGET_FRAMEWORK,
    `${packageId}.dll`,
  );

  if (!fs.existsSync(dllPath)) {
    throw new Error(`Built DLL not found at ${dllPath} for ${packageId}.`);
  }

  return createHash("sha256").update(fs.readFileSync(dllPath)).digest("hex");
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

  // 4. Fingerprint the debug-stripped Release DLL.
  const fingerprint = fingerprintDll(csprojPath, packageId);
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
