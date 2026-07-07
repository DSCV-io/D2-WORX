// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Source-based portable fingerprint — the single home of the per-package
// output-fingerprint composition.
//
// The fingerprint is a SHA-256 over an ordered, LF-normalized tuple of
// COMMITTED inputs only — no build, byte-identical on every OS/machine:
//
//   SHA-256(
//     "SOURCE:\n"    + <ordered, LF-normalized committed source dump> + "\n" +
//     "APIREPORT:\n"  + <LF-normalized committed API report(s)>        + "\n" +
//     "DEPS:\n"       + <deterministic resolved-deps JSON>             + "\n" +
//     "TOOLCHAIN:\n"  + <deterministic toolchain-pin JSON>             + "\n"
//   )
//
// Because every input is read from committed text (source files, the API
// report, the declared toolchain pin) and the resolved-dep map, a contributor
// on any host recomputes the identical hash with no build — this is what lets
// a Windows-seeded baseline equal a Linux-CI recompute.
//
// The ONLY signal it cannot detect is a float-within-pin rebuild: identical
// committed source + identical resolved deps + a patch-level SDK/compiler drift
// inside the declared pin's roll-forward window, with no version bump. That is
// an accepted, rare, footer-forceable residual of the portable design (a
// republish-worthy toolchain bump is one that edits the committed pin, which
// DOES move the hash).
//
// Single-source rule: the seed scripts (seed-publicapi-baselines.mjs /
// seed-apiextractor-baselines.mjs) delegate their final composition to the
// shared composeSourceFingerprintFromParts primitive
// (tools/scripts/lib/source-fingerprint-compose.mjs), a byte-for-byte
// re-implementation of composeSourceFingerprint below, so a no-op drift
// recompute matches the committed baseline. The seed↔provider byte-identity of
// that primitive vs composeSourceFingerprint is pinned by
// tools/release-runner/tests/seed-provider-fingerprint-identity.test.ts.

import { createHash } from "node:crypto";
import { readFileSync } from "node:fs";
import { join } from "node:path";
import { spawnSync } from "node:child_process";

// ---------------------------------------------------------------------------
// LF normalization
// ---------------------------------------------------------------------------

/**
 * LF-normalize text so a CRLF/LF checkout difference cannot perturb the hash.
 *
 * `.gitattributes` (`* text=auto eol=lf`) already stores blobs as LF; this only
 * guards a CRLF working-tree checkout on Windows.
 */
export function normalizeLf(text: string): string {
  return text.replace(/\r\n/g, "\n");
}

// ---------------------------------------------------------------------------
// Per-ecosystem source-dump globs
// ---------------------------------------------------------------------------

/**
 * The committed-source allowlist per ecosystem (file extensions / basenames the
 * source dump includes). Generated `*.g.cs` / `*.g.ts` are committed,
 * reproducible, and DO change the compiled output, so they participate.
 */
export type SourceEcosystem = "nuget" | "npm";

/** Directory names never descended into during the source walk. */
const _SKIP_DIRS = new Set([
  "bin",
  "obj",
  "dist",
  "node_modules",
  "etc",
  "tests",
]);

/**
 * Decide whether a file (by its repo-relative POSIX path + basename) is part of
 * the committed-source dump for the given ecosystem.
 *
 * Includes, per ecosystem:
 *   nuget — every committed `.cs` (incl. generated `.g.cs` under `Generated`) +
 *           the `.csproj`.
 *   npm   — every committed `.ts` under `src` (incl. generated `.g.ts`) +
 *           `package.json` + any `tsconfig*.json` + `api-extractor.json`.
 *
 * Excludes (both): the baseline files themselves (`.release-fingerprint`,
 * `PublicAPI.*.txt`), `CHANGELOG.md`, test files, and anything under a skipped
 * directory. Excluding the baseline files is mandatory — hashing the
 * fingerprint's own prior value would make re-seeding non-idempotent.
 */
function isSourceFile(
  relPosixPath: string,
  ecosystem: SourceEcosystem,
): boolean {
  const base = relPosixPath.slice(relPosixPath.lastIndexOf("/") + 1);

  // Never fold a baseline file or the changelog into the source dump.
  if (
    base === ".release-fingerprint" ||
    base === "PublicAPI.Shipped.txt" ||
    base === "PublicAPI.Unshipped.txt" ||
    base === "CHANGELOG.md"
  ) {
    return false;
  }

  if (ecosystem === "nuget") {
    return base.endsWith(".cs") || base.endsWith(".csproj");
  }

  // npm
  if (base.endsWith(".test.ts")) return false;
  if (base.endsWith(".ts")) return true;
  if (base === "package.json") return true;
  if (base === "api-extractor.json") return true;

  return base.startsWith("tsconfig") && base.endsWith(".json");
}

/**
 * Enumerate the package-relative POSIX paths of every COMMITTED (git-tracked)
 * file under `packageDir`. Returns paths relative to `packageDir`.
 *
 * Tracked-only is mandatory: the build emits gitignored transients into
 * `Generated/` (e.g. the non-deterministic `LoggerMessage.g.cs` that the
 * loggermessage-splitter splits into committed per-class files). A plain fs walk
 * would fold those build artifacts into the fingerprint, making it depend on
 * build state and breaking portability. `git ls-files` lists exactly the
 * committed source, so the dump is byte-identical whether or not a build ran.
 *
 * The default reads from `git ls-files`; injectable for unit-testing against a
 * synthetic file set.
 */
export type TrackedFileLister = (packageDir: string) => string[];

/** Default TrackedFileLister — `git ls-files` rooted at `packageDir`. */
export function makeGitTrackedLister(): TrackedFileLister {
  return (packageDir: string): string[] => {
    const result = spawnSync("git", ["ls-files", "--", "."], {
      cwd: packageDir,
      encoding: "utf-8",
      maxBuffer: 32 * 1024 * 1024,
    });

    // On a non-zero exit (e.g. dir outside any repo) git emits no usable list.
    // `encoding: "utf-8"` guarantees a string stdout on success.
    if (result.status !== 0) return [];

    return result.stdout
      .split("\n")
      .map((l) => l.trim())
      .filter((l) => l.length > 0);
  };
}

/**
 * Collect the repo-relative-to-package POSIX paths of committed-source files
 * under `packageDir` (git-tracked + matching the per-ecosystem allowlist).
 *
 * @param packageDir   - Absolute path to the package root.
 * @param ecosystem    - Selects the source allowlist.
 * @param trackedLister - Lists git-tracked paths (default: `git ls-files`).
 * @returns Package-relative POSIX paths, NOT yet sorted (the caller sorts).
 */
export function listSourceFiles(
  packageDir: string,
  ecosystem: SourceEcosystem,
  trackedLister: TrackedFileLister = makeGitTrackedLister(),
): string[] {
  return trackedLister(packageDir)
    .map((p) => p.replace(/\\/g, "/"))
    .filter(
      (relPosix) => !isSkipped(relPosix) && isSourceFile(relPosix, ecosystem),
    );
}

/** True when a package-relative path lives under a skipped directory. */
function isSkipped(relPosix: string): boolean {
  return relPosix.split("/").some((seg) => _SKIP_DIRS.has(seg));
}

// ---------------------------------------------------------------------------
// Source-dump composition
// ---------------------------------------------------------------------------

/**
 * Seam for reading a source file's content. The default reads from disk;
 * tests inject a synthetic reader so the composer is unit-testable without a
 * real package tree.
 */
export type SourceFileReader = (relPosixPath: string) => string;

/**
 * Build the ordered, LF-normalized source dump for a set of repo-relative
 * paths. Files are sorted by their POSIX path (ordinal) so the dump is
 * deterministic regardless of directory-walk order, and each file is emitted as
 * `F:<relPath>\n<lf-normalized content>\n` so a boundary shift between two files
 * cannot collide with a content change.
 *
 * @param relPaths - Repo-relative POSIX source paths (unsorted is fine).
 * @param read     - Reads a file's raw content by its repo-relative path.
 * @returns The deterministic source-dump string.
 */
export function buildSourceDump(
  relPaths: readonly string[],
  read: SourceFileReader,
): string {
  const sorted = [...relPaths].sort();
  let dump = "";

  for (const relPath of sorted) {
    dump += `F:${relPath}\n${normalizeLf(read(relPath))}\n`;
  }

  return dump;
}

// ---------------------------------------------------------------------------
// Toolchain pin
// ---------------------------------------------------------------------------

/**
 * Seam for reading a committed repo file's text by its repo-root-relative path.
 * The default reads from disk; tests inject a synthetic reader.
 */
export type RepoFileReader = (repoRelativePath: string) => string;

/** Default RepoFileReader — reads from disk relative to `repoRoot`. */
export function makeRepoFileReader(repoRoot: string): RepoFileReader {
  return (repoRelativePath: string): string =>
    readFileSync(join(repoRoot, repoRelativePath), "utf-8");
}

/**
 * Read the DECLARED, COMMITTED toolchain pin for an ecosystem and return it as
 * deterministic sorted-key JSON. Never reads a runtime-resolved SDK/compiler
 * version (that would reintroduce host variance) — only committed declarations.
 *
 *   nuget — server/global.json (sdk.version + sdk.rollForward) +
 *           server/Directory.Build.props (TargetFramework + LangVersion).
 *   npm   — root package.json devDependencies.typescript +
 *           server/shared/typescript/tsconfig.base.json (target + module).
 *
 * @param ecosystem - Selects the pin sources.
 * @param read      - Reads a committed repo file by its repo-root-relative path.
 * @returns Deterministic sorted-key JSON of the declared pin.
 */
export function readToolchainPin(
  ecosystem: SourceEcosystem,
  read: RepoFileReader,
): string {
  if (ecosystem === "nuget") {
    const globalJson = JSON.parse(read("server/global.json")) as {
      sdk?: { version?: string; rollForward?: string };
    };
    const buildProps = read("server/Directory.Build.props");

    return stableJson({
      langVersion: extractXmlElement(buildProps, "LangVersion") ?? "",
      rollForward: globalJson.sdk?.rollForward ?? "",
      sdk: globalJson.sdk?.version ?? "",
      targetFramework: extractXmlElement(buildProps, "TargetFramework") ?? "",
    });
  }

  // npm
  const rootPkg = JSON.parse(read("package.json")) as {
    devDependencies?: Record<string, string>;
  };
  const tsconfigBase = JSON.parse(
    read("server/shared/typescript/tsconfig.base.json"),
  ) as { compilerOptions?: { target?: string; module?: string } };

  return stableJson({
    module: tsconfigBase.compilerOptions?.module ?? "",
    target: tsconfigBase.compilerOptions?.target ?? "",
    typescript: rootPkg.devDependencies?.typescript ?? "",
  });
}

/** Extract the inner text of the first `<Element>...</Element>` in XML text. */
function extractXmlElement(xml: string, element: string): string | undefined {
  const re = new RegExp(`<${element}>([^<]+)</${element}>`);
  const match = re.exec(xml);

  return match?.[1]?.trim();
}

/**
 * Serialize an object to JSON with keys in ascending order, so two structurally
 * equal inputs always serialize to byte-identical text.
 */
export function stableJson(obj: Record<string, string>): string {
  const sortedKeys = Object.keys(obj).sort();
  const ordered: Record<string, string> = {};

  for (const key of sortedKeys) ordered[key] = obj[key] ?? "";

  return JSON.stringify(ordered);
}

// ---------------------------------------------------------------------------
// Final composition
// ---------------------------------------------------------------------------

/**
 * The four ordered components of the source-based fingerprint.
 */
export interface SourceFingerprintInput {
  /** Ordered, LF-normalized committed source dump (from `buildSourceDump`). */
  readonly sourceDump: string;
  /** The committed API report text(s) — already LF-normalized + concatenated. */
  readonly apiReport: string;
  /** Deterministic resolved-deps JSON (manifest metadata). */
  readonly depsJson: string;
  /** Deterministic toolchain-pin JSON (from `readToolchainPin`). */
  readonly toolchainJson: string;
}

/**
 * Compose the source-based output fingerprint. SHA-256 over the ordered,
 * prefixed, LF-terminated tuple (SOURCE, APIREPORT, DEPS, TOOLCHAIN).
 *
 * Each component is read by the caller from committed inputs; this function is
 * pure over those bytes so it is exhaustively unit-testable with synthetic
 * inputs and is the SINGLE composition the seed scripts replicate byte-for-byte.
 *
 * @param input - The four ordered components.
 * @returns The SHA-256 hex digest.
 */
export function composeSourceFingerprint(
  input: SourceFingerprintInput,
): string {
  const hash = createHash("sha256");

  hash.update(`SOURCE:\n${input.sourceDump}\n`);
  hash.update(`APIREPORT:\n${normalizeLf(input.apiReport)}\n`);
  hash.update(`DEPS:\n${input.depsJson}\n`);
  hash.update(`TOOLCHAIN:\n${input.toolchainJson}\n`);

  return hash.digest("hex");
}
