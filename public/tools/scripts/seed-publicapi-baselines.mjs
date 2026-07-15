// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
//
// Seeding tool: generates the public-API surface baselines + the output
// fingerprint baseline for every .NET CONSUMABLE in D2-WORX — the 54 total:
// 53 D2.Shared.* libraries under public/packages/dotnet (excluding the source-gen
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
//   1. Snapshot on-disk baselines (or HEAD if disk already wiped). Then reset
//      Shipped/Unshipped to header-only so the build reports the FULL current
//      surface as RS0016. On ANY failure after the wipe, RESTORE the snapshot
//      before rethrow/exit — never leave header-only Shipped on disk.
//   2. Build the package and parse the analyzer's RS0016 diagnostics — each
//      carries `Symbol '<canonical API line>' is not part of the declared
//      public API`. The exact API line is extracted from every RS0016 message.
//   3. Fail-loud if HEAD had a non-empty surface and extraction is empty
//      (analyzer-didn't-run signature). Then write extracted lines into
//      Shipped.txt; Unshipped stays header-only.
//   4. FINGERPRINT: source-based hash over committed source + PublicAPI.* +
//      deps + toolchain pin → .release-fingerprint.
//
// Commit gate: tools/scripts/check-publicapi-shipped.mjs (husky pre-commit +
// cycle-commit precheck) refuses header-only Shipped when HEAD still has lines.
//
// Run from the repo root: `node public/tools/scripts/seed-publicapi-baselines.mjs`.
// Optional `--package <PackageId>` limits the run to a single consumable.
//
// IDEMPOTENT: re-running regenerates byte-identical baseline files. The promote
// step sorts deterministically; the source-based hash is platform/path-
// independent, so a re-seed over unchanged source is a no-op (the script reports
// "unchanged" for each). A baseline that is already correct is left untouched.

import { spawnSync } from "node:child_process";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

import {
  NULLABLE_HEADER as GUARD_NULLABLE_HEADER,
  assertExtractionNotWrongfullyEmpty,
  assertShippedContentNotWrongfullyEmpty,
  countPublicApiLines,
} from "./lib/publicapi-empty-guard.mjs";
import { composeSourceFingerprintFromParts } from "./lib/source-fingerprint-compose.mjs";

const REPO_ROOT = path.resolve(
  path.dirname(fileURLToPath(import.meta.url)),
  "..",
  "..",
  "..",
);

// Keep in lockstep with publicapi-empty-guard.mjs (single source for the header string).
const NULLABLE_HEADER = GUARD_NULLABLE_HEADER;

// ---------------------------------------------------------------------------
// CLI args.
// ---------------------------------------------------------------------------

const args = process.argv.slice(2);
const packageFilterIdx = args.indexOf("--package");
const packageFilter =
  packageFilterIdx !== -1 ? args[packageFilterIdx + 1] : undefined;

// Escape hatch for the genuine "this package intentionally exposes no public
// API" case, which the fail-loud empty-surface guard would otherwise reject.
// Repeatable `--allow-empty <PackageId>` flags plus a single-package
// SEED_ALLOW_EMPTY=<PackageId> env var both feed the same allow-list. For
// multiple packages use repeated CLI flags; the env var accepts exactly one
// package ID (comma-split lists are not supported — §23.1). The DEFAULT (no
// opt-in) refuses to persist an empty surface over a non-empty committed one —
// that transition is the analyzer-didn't-run signature, not a real removal.
const allowEmptyPackages = new Set();

for (let i = 0; i < args.length; i++) {
  if (args[i] === "--allow-empty" && args[i + 1]) {
    allowEmptyPackages.add(args[i + 1]);
  }
}

const envAllowEmpty = (process.env.SEED_ALLOW_EMPTY ?? "").trim();

if (envAllowEmpty.length > 0) {
  allowEmptyPackages.add(envAllowEmpty);
}

// ---------------------------------------------------------------------------
// Inventory discovery — the 53 shared-tree consumables + the KC client.
// Mirrors tools/scripts/seed-package-metadata.mjs: a consumable is a .csproj
// that is NOT a *SourceGen shell and NOT the test project. The KC client is
// added explicitly (it lives outside public/packages/dotnet).
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

const sharedDotnetRoot = path.join(REPO_ROOT, "public", "packages", "dotnet");

const sharedConsumables = walk(sharedDotnetRoot)
  .filter((f) => f.endsWith(".csproj"))
  .filter((f) => !f.endsWith("SourceGen.csproj"))
  .filter((f) => !/D2\.Shared\.Tests\.csproj$/.test(f));

const kcClient = path.join(
  REPO_ROOT,
  "private",
  "services",
  "edge",
  "key-custodian",
  "client",
  "D2.Edge.KeyCustodian.Client.csproj",
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

/**
 * Read the non-header, non-empty API lines from the COMMITTED (HEAD) version of a
 * baseline .txt. This is the authoritative "prior surface" for the empty-guard:
 * the working-tree copy is unreliable (the pre-pass resets it to header-only, and
 * a prior corrupt run may already have emptied it on disk), whereas HEAD holds the
 * last genuinely-seeded surface. A file not tracked at HEAD (a brand-new package)
 * yields an empty list, so the guard correctly permits it to seed empty.
 */
function readCommittedApiLines(filePath) {
  const relPosix = path.relative(REPO_ROOT, filePath).replace(/\\/g, "/");
  const result = spawnSync("git", ["show", `HEAD:${relPosix}`], {
    cwd: REPO_ROOT,
    encoding: "utf8",
    maxBuffer: 32 * 1024 * 1024,
  });

  if (result.status !== 0) return [];

  return (result.stdout ?? "")
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

/**
 * Force the NEXT build of this package to fully recompile so the analyzer
 * re-runs and re-emits its RS0016 diagnostics. `--no-incremental` alone is an
 * UNRELIABLE trigger (proven): MSBuild's up-to-date check does not consistently
 * treat an AdditionalFiles (PublicAPI.*.txt) content change as a recompile
 * reason, so a warm bin/obj (or a toolchain that skips CoreCompile) leaves zero
 * RS0016 in the output and yields a wrong, empty extracted surface. Deleting the
 * package's own bin/ + obj/ removes the compiled outputs and the up-to-date
 * markers, so the subsequent build MUST run CoreCompile (and therefore the
 * analyzer) from scratch. Restore assets (project.assets.json) live under obj/
 * and are deleted with it, so the extraction build below must NOT pass
 * `--no-restore` — it restores first, then compiles. Only THIS package's
 * intermediates are removed; its dependencies keep their warm caches (their
 * RS0016, if any, is filtered out by owner-tag), so the extra cost is one clean
 * recompile of the target package, not the whole graph.
 */
function forceFullRecompile(csprojPath) {
  const dir = path.dirname(csprojPath);

  for (const sub of ["bin", "obj"]) {
    fs.rmSync(path.join(dir, sub), { recursive: true, force: true });
  }
}

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
  // forceFullRecompile removed this package's bin/ + obj/ up front, so the build
  // MUST run CoreCompile (and the analyzer) from scratch — the RELIABLE trigger
  // that `--no-incremental` alone is not. Because obj/ (and project.assets.json)
  // was deleted, this build must NOT pass `--no-restore`: it restores first, then
  // compiles. `--no-incremental` is kept as belt-and-suspenders.
  forceFullRecompile(csprojPath);

  const result = run("dotnet", [
    "build",
    csprojPath,
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

  // Zero RS0016 for this package is AMBIGUOUS: it means EITHER the package
  // genuinely has no public API, OR the analyzer did not re-run. forceFullRecompile
  // above makes the second case unlikely, but the caller still fail-loud guards the
  // "prior-non-empty → extracted-empty" transition (assertExtractionNotWrongfullyEmpty)
  // rather than trusting an empty extraction unconditionally.
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
// BYTE-FOR-BYTE). The final SHA-256 composition is delegated to the shared
// composeSourceFingerprintFromParts primitive, whose seed↔provider byte-identity
// is pinned by tools/release-runner/tests/seed-provider-fingerprint-identity.test.ts.
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
    fs.readFileSync(path.join(REPO_ROOT, "global.json"), "utf8"),
  );
  const buildProps = fs.readFileSync(
    path.join(REPO_ROOT, "Directory.Build.props"),
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
  const csprojText = fs.readFileSync(csprojPath, "utf8");
  const depsJson = buildManifestMeta(packageId, csprojText);

  // Delegate the final SHA-256 composition to the shared primitive so it stays
  // byte-identical to the release-runner's composeSourceFingerprint (the drift
  // check recomputes via the runner). Mirror the provider: apiReport =
  // shippedTxt + unshippedTxt, passed RAW — the primitive LF-normalizes it once.
  return composeSourceFingerprintFromParts({
    sourceDump: buildSourceDump(packageDir),
    apiReport: shippedTxt + unshippedTxt,
    depsJson,
    toolchainJson: TOOLCHAIN_PIN,
  });
}

// ---------------------------------------------------------------------------
// Per-consumable seed.
// ---------------------------------------------------------------------------

/**
 * Snapshot bytes to restore if extraction fails AFTER the intentional header-only
 * wipe.
 *
 * Policy (restore must never re-apply corruption):
 * - Prefer the on-disk Shipped bytes when they already have API lines.
 * - If on-disk is already header-only/missing but HEAD has lines, snapshot HEAD
 *   (prior failed seed left disk empty — restore must recover HEAD, not empty).
 * - If both are empty (new package / intentional empty already committed),
 *   snapshot header-only or null; restore is a no-op to empty.
 * Unshipped + fingerprint: snapshot exact disk bytes (or null if absent).
 */
function snapshotBaselineFiles(shippedPath, unshippedPath, fingerprintPath) {
  const headShippedLines = readCommittedApiLines(shippedPath);
  const diskShippedText = fs.existsSync(shippedPath)
    ? fs.readFileSync(shippedPath, "utf8")
    : "";
  const diskApiCount = countPublicApiLines(diskShippedText);

  let shippedSnap;

  if (diskApiCount > 0) {
    // Exact disk bytes (preserve EOL / trailing newline as committed on disk).
    shippedSnap = Buffer.from(diskShippedText, "utf8");
  } else if (headShippedLines.length > 0) {
    // Disk wiped or missing; HEAD is the last good surface.
    shippedSnap = Buffer.from(
      [NULLABLE_HEADER, ...headShippedLines].join("\n") + "\n",
      "utf8",
    );
  } else {
    // Genuinely no surface at HEAD and none on disk (new / already-empty package).
    shippedSnap = fs.existsSync(shippedPath)
      ? Buffer.from(diskShippedText, "utf8")
      : null;
  }

  return {
    headShippedLines,
    shipped: shippedSnap,
    unshipped: fs.existsSync(unshippedPath)
      ? fs.readFileSync(unshippedPath)
      : null,
    fingerprint: fs.existsSync(fingerprintPath)
      ? fs.readFileSync(fingerprintPath)
      : null,
  };
}

function restoreBaselineFiles(
  shippedPath,
  unshippedPath,
  fingerprintPath,
  snap,
) {
  // Only write paths we snapshotted. Never invent a "successful empty" on restore
  // when we had nothing — that would re-create the wipe bug for brand-new packages
  // that never had a file (ensureHeaderFile already handled existence pre-pass).
  if (snap.shipped !== null) {
    fs.writeFileSync(shippedPath, snap.shipped);
  }

  if (snap.unshipped !== null) {
    fs.writeFileSync(unshippedPath, snap.unshipped);
  }

  if (snap.fingerprint !== null) {
    fs.writeFileSync(fingerprintPath, snap.fingerprint);
  }
}

function seedConsumable(csprojPath) {
  const packageId = path.basename(csprojPath, ".csproj");
  const dir = path.dirname(csprojPath);
  const shippedPath = path.join(dir, "PublicAPI.Shipped.txt");
  const unshippedPath = path.join(dir, "PublicAPI.Unshipped.txt");
  const fingerprintPath = path.join(dir, ".release-fingerprint");
  const allowEmpty = allowEmptyPackages.has(packageId);

  // Snapshot BEFORE the intentional wipe so a failed build / empty-guard throw
  // can restore. Without this, process.exit(1) left header-only Shipped.txt on
  // disk (the wipe that emptied AspNetCore / Auth.Abstractions mid-run).
  const diskSnap = snapshotBaselineFiles(
    shippedPath,
    unshippedPath,
    fingerprintPath,
  );

  // The prior surface is read from HEAD (committed), NOT the working tree: the
  // pre-pass may only ensure existence; a prior corrupt run may already have
  // emptied the on-disk file, so HEAD is the empty-guard's committed truth.
  const before = {
    shipped: diskSnap.headShippedLines,
    fingerprint: fs.existsSync(fingerprintPath)
      ? fs.readFileSync(fingerprintPath, "utf8").trim()
      : undefined,
  };

  try {
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

    // 2a. FAIL-LOUD GUARD: a package that had a non-empty committed surface but
    //     extracted to zero is the analyzer-didn't-run signature (a successful
    //     build does not silently drop a whole public API). Refuse to persist the
    //     wipe unless the package is explicitly allow-listed for a genuine removal.
    assertExtractionNotWrongfullyEmpty({
      packageId,
      priorSurfaceCount: before.shipped.length,
      extractedSurfaceCount: surface.length,
      allowEmpty,
    });

    // 3. Write the surface into Shipped.txt; Unshipped.txt stays header-only.
    writeApiFile(shippedPath, surface);
    writeApiFile(unshippedPath, []);

    // 3a. Defense in depth: refuse to leave a wrongfully empty on-disk Shipped
    //     even if a future edit skips 2a.
    assertShippedContentNotWrongfullyEmpty({
      packageId,
      shippedContent: fs.readFileSync(shippedPath, "utf8"),
      headSurfaceCount: before.shipped.length,
      allowEmpty,
    });

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
  } catch (err) {
    // ALWAYS restore before rethrow — process.exit(1) at the top level must not
    // leave header-only PublicAPI.Shipped.txt on disk.
    restoreBaselineFiles(shippedPath, unshippedPath, fingerprintPath, diskSnap);
    throw err;
  }
}

// ---------------------------------------------------------------------------
// Run.
// ---------------------------------------------------------------------------

// Pre-pass: ensure EVERY consumable has both .txt baselines on disk BEFORE any
// build runs. The Directory.Build.props references each file by a literal
// AdditionalFiles path; a missing file becomes a CS2001 "source file could not
// be found" the moment a consumable (or any consumable that depends on it
// transitively) is built. This pre-pass GUARANTEES EXISTENCE ONLY — it never
// empties an existing baseline. Each package's Shipped.txt is emptied-then-
// immediately-repopulated inside seedConsumable, so at any instant at most ONE
// package is in the header-only state (the narrowest possible corruption window);
// the earlier "reset EVERY Shipped.txt up front" widened that window across the
// whole run for no benefit (per-package RS0016 is attributed by owner-tag, so a
// dependency retaining its committed surface cannot contaminate another package's
// extraction). Existence-only also preserves each package's committed surface as
// the empty-guard's HEAD-independent on-disk fallback until its own turn.
for (const csprojPath of consumables) {
  const dir = path.dirname(csprojPath);
  const shippedPath = path.join(dir, "PublicAPI.Shipped.txt");
  const unshippedPath = path.join(dir, "PublicAPI.Unshipped.txt");

  ensureHeaderFile(shippedPath);
  ensureHeaderFile(unshippedPath);
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
    console.error(
      "\n  seed-publicapi-baselines: aborted. Any in-progress PublicAPI.Shipped.txt " +
        "wipe for the failed package was RESTORED from the pre-seed snapshot/HEAD.\n" +
        "  Do NOT commit header-only Shipped files. Re-run after fixing the build error.\n",
    );
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
