// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
//
// Idempotent seeding tool: installs api-extractor.json configs and generates
// committed baselines (etc/<pkg>.api.md + etc/.release-fingerprint) for all
// 36 @dcsv-io/d2-* consumable packages: 35 under public/packages/typescript/ plus the
// KeyCustodian client twin under private/services/edge/key-custodian/client-ts/.
//
// The fingerprint is SOURCE-BASED + PORTABLE - a SHA-256 over committed text
// only ( committed src dump + the .api.md report + resolved deps + the declared
// toolchain pin ), byte-identical on every OS/machine with NO build to compute.
// It matches the release-runner's composeSourceFingerprint byte-for-byte so the
// drift check (which recomputes via the runner) compares like-for-like. The
// committed home is `etc/.release-fingerprint` (mirrors the .NET filename for a
// single mental model across both ecosystems).
//
// EXCLUDES tooling-only packages: typespec-decorators, typespec-emitters,
// contract-tests (these are dev fixtures, not consumable libraries).
//
// Run from repo root: `node tools/scripts/seed-apiextractor-baselines.mjs`
// Idempotent: safe to re-run; regenerates baselines byte-for-byte if source
// is unchanged (fingerprint and api.md are deterministic outputs).
//
// Prerequisites:
//   - All 36 packages must have a built dist/ - api-extractor consumes
//     dist/index.d.ts to generate the .api.md report (the fingerprint itself
//     does NOT read dist/). Run `pnpm -r build` first.
//   - @microsoft/api-extractor must be installed in tools/release-runner
//     (it is - declared as a devDependency there).

import {
  existsSync,
  mkdirSync,
  readFileSync,
  rmSync,
  writeFileSync,
} from "node:fs";
import { dirname, join } from "node:path";
import { spawnSync } from "node:child_process";
import { fileURLToPath } from "node:url";

import { assertApiReportNotDegenerate } from "./lib/apiextractor-empty-guard.mjs";
import { composeSourceFingerprintFromParts } from "./lib/source-fingerprint-compose.mjs";

// ---------------------------------------------------------------------------
// Repo layout
// ---------------------------------------------------------------------------

const REPO_ROOT = join(
  dirname(fileURLToPath(import.meta.url)),
  "..",
  "..",
  "..",
);
const TS_SHARED = join(REPO_ROOT, "public", "packages", "typescript");
const RUNNER_DIR = join(REPO_ROOT, "public", "tools", "release-runner");

// api-extractor binary is installed under tools/release-runner
const API_EXTRACTOR_BIN = join(
  RUNNER_DIR,
  "node_modules",
  ".bin",
  "api-extractor",
);

// ---------------------------------------------------------------------------
// Empty-surface escape hatch (mirrors the .NET seeder's --allow-empty).
// ---------------------------------------------------------------------------

// A degenerate .api.md with NO `export ` line is the fail-loud default: it is
// the "api-extractor saw an empty/missing dist/index.d.ts" signature, not a
// real zero-export library. A package that LEGITIMATELY exposes zero exports
// opts in via a repeatable `--allow-empty <pkgName>` flag or a single-package
// SEED_ALLOW_EMPTY=<pkgName> env var. For multiple packages use repeated CLI
// flags; the env var accepts exactly one package name (comma-split lists are
// not supported Ã¢â‚¬â€ Ã‚23.1). As of this writing NO @dcsv-io/d2-* consumable legitimately
// has zero exports, so the hatch defaults to refuse Ã¢â‚¬â€ it exists for symmetry
// with the .NET seeder so both ecosystems fail loud on the same corruption class.
const CLI_ARGS = process.argv.slice(2);
const ALLOW_EMPTY_PACKAGES = new Set();

for (let i = 0; i < CLI_ARGS.length; i++) {
  if (CLI_ARGS[i] === "--allow-empty" && CLI_ARGS[i + 1]) {
    ALLOW_EMPTY_PACKAGES.add(CLI_ARGS[i + 1]);
  }
}

const envAllowEmpty = (process.env.SEED_ALLOW_EMPTY ?? "").trim();

if (envAllowEmpty.length > 0) {
  ALLOW_EMPTY_PACKAGES.add(envAllowEmpty);
}

// ---------------------------------------------------------------------------
// The 36 consumable packages: [pkgDir, shortName] pairs.
// Derived from the package names (@dcsv-io/d2-<shortName>) so that api.md report
// filenames are stable regardless of the directory structure.
// Excludes: typespec-decorators, typespec-emitters, contract-tests.
// ---------------------------------------------------------------------------

/** @type {Array<{dir: string, shortName: string, pkgName: string}>} */
const CONSUMABLES = [
  {
    dir: join(TS_SHARED, "auth", "abstractions"),
    shortName: "auth-abstractions",
    pkgName: "@dcsv-io/d2-auth-abstractions",
  },
  {
    dir: join(TS_SHARED, "auth", "context-abstractions"),
    shortName: "auth-context-abstractions",
    pkgName: "@dcsv-io/d2-auth-context-abstractions",
  },
  {
    dir: join(TS_SHARED, "caching", "abstractions"),
    shortName: "caching-abstractions",
    pkgName: "@dcsv-io/d2-caching-abstractions",
  },
  {
    dir: join(TS_SHARED, "caching", "distributed-redis"),
    shortName: "caching-distributed-redis",
    pkgName: "@dcsv-io/d2-caching-distributed-redis",
  },
  {
    dir: join(TS_SHARED, "caching", "local-default"),
    shortName: "caching-local-default",
    pkgName: "@dcsv-io/d2-caching-local-default",
  },
  {
    dir: join(TS_SHARED, "caching", "tiered"),
    shortName: "caching-tiered",
    pkgName: "@dcsv-io/d2-caching-tiered",
  },
  {
    dir: join(TS_SHARED, "encryption"),
    shortName: "encryption",
    pkgName: "@dcsv-io/d2-encryption",
  },
  {
    dir: join(TS_SHARED, "encryption-abstractions"),
    shortName: "encryption-abstractions",
    pkgName: "@dcsv-io/d2-encryption-abstractions",
  },
  {
    dir: join(TS_SHARED, "error-category"),
    shortName: "error-category",
    pkgName: "@dcsv-io/d2-error-category",
  },
  {
    dir: join(TS_SHARED, "error-codes-registry"),
    shortName: "error-codes-registry",
    pkgName: "@dcsv-io/d2-error-codes-registry",
  },
  {
    dir: join(TS_SHARED, "geo", "abstractions"),
    shortName: "geo-abstractions",
    pkgName: "@dcsv-io/d2-geo-abstractions",
  },
  {
    dir: join(TS_SHARED, "geo", "default"),
    shortName: "geo-default",
    pkgName: "@dcsv-io/d2-geo-default",
  },
  {
    dir: join(TS_SHARED, "grpc-client"),
    shortName: "grpc-client",
    pkgName: "@dcsv-io/d2-grpc-client",
  },
  {
    dir: join(TS_SHARED, "headers", "amqp"),
    shortName: "headers-amqp",
    pkgName: "@dcsv-io/d2-headers-amqp",
  },
  {
    dir: join(TS_SHARED, "headers", "common"),
    shortName: "headers-common",
    pkgName: "@dcsv-io/d2-headers-common",
  },
  {
    dir: join(TS_SHARED, "headers", "core"),
    shortName: "headers",
    pkgName: "@dcsv-io/d2-headers",
  },
  {
    dir: join(TS_SHARED, "headers", "grpc"),
    shortName: "headers-grpc",
    pkgName: "@dcsv-io/d2-headers-grpc",
  },
  {
    dir: join(TS_SHARED, "headers", "http"),
    shortName: "headers-http",
    pkgName: "@dcsv-io/d2-headers-http",
  },
  {
    dir: join(TS_SHARED, "i18n-abstractions"),
    shortName: "i18n-abstractions",
    pkgName: "@dcsv-io/d2-i18n-abstractions",
  },
  {
    dir: join(TS_SHARED, "i18n-keys"),
    shortName: "i18n-keys",
    pkgName: "@dcsv-io/d2-i18n-keys",
  },
  {
    dir: join(TS_SHARED, "i18n"),
    shortName: "i18n",
    pkgName: "@dcsv-io/d2-i18n",
  },
  {
    dir: join(TS_SHARED, "logging"),
    shortName: "logging",
    pkgName: "@dcsv-io/d2-logging",
  },
  {
    dir: join(TS_SHARED, "messaging-abstractions"),
    shortName: "messaging-abstractions",
    pkgName: "@dcsv-io/d2-messaging-abstractions",
  },
  {
    dir: join(TS_SHARED, "messaging", "rabbitmq"),
    shortName: "messaging-rabbitmq",
    pkgName: "@dcsv-io/d2-messaging-rabbitmq",
  },
  {
    dir: join(TS_SHARED, "problem-details-abstractions"),
    shortName: "problem-details-abstractions",
    pkgName: "@dcsv-io/d2-problem-details-abstractions",
  },
  {
    dir: join(TS_SHARED, "protos"),
    shortName: "protos",
    pkgName: "@dcsv-io/d2-protos",
  },
  {
    dir: join(TS_SHARED, "request-context-abstractions"),
    shortName: "request-context-abstractions",
    pkgName: "@dcsv-io/d2-request-context-abstractions",
  },
  {
    dir: join(TS_SHARED, "resilience"),
    shortName: "resilience",
    pkgName: "@dcsv-io/d2-resilience",
  },
  {
    dir: join(TS_SHARED, "result"),
    shortName: "result",
    pkgName: "@dcsv-io/d2-result",
  },
  {
    dir: join(TS_SHARED, "service-defaults"),
    shortName: "service-defaults",
    pkgName: "@dcsv-io/d2-service-defaults",
  },
  {
    dir: join(TS_SHARED, "telemetry"),
    shortName: "telemetry",
    pkgName: "@dcsv-io/d2-telemetry",
  },
  {
    dir: join(TS_SHARED, "time"),
    shortName: "time",
    pkgName: "@dcsv-io/d2-time",
  },
  {
    dir: join(TS_SHARED, "utilities"),
    shortName: "utilities",
    pkgName: "@dcsv-io/d2-utilities",
  },
  {
    dir: join(TS_SHARED, "validation", "abstractions"),
    shortName: "validation-abstractions",
    pkgName: "@dcsv-io/d2-validation-abstractions",
  },
  {
    dir: join(TS_SHARED, "validation", "default"),
    shortName: "validation",
    pkgName: "@dcsv-io/d2-validation",
  },
  // Consumable outside public/packages/typescript/: the KeyCustodian workload-leaf
  // client twin lives beside its service. Same baseline mechanism (git-tracked src
  // dump + api.md report), addressed by an explicit repo-relative dir.
  {
    dir: join(
      REPO_ROOT,
      "private",
      "services",
      "edge",
      "key-custodian",
      "client-ts",
    ),
    shortName: "key-custodian-client",
    pkgName: "@dcsv-io/d2-private-key-custodian-client",
  },
];

// ---------------------------------------------------------------------------
// Step 1 Ã¢â‚¬â€ Write api-extractor.json (idempotent: skip if content unchanged)
// ---------------------------------------------------------------------------

/**
 * Return the canonical api-extractor.json content for a package.
 * @param {string} shortName
 */
function apiExtractorConfig(shortName) {
  return JSON.stringify(
    {
      $schema:
        "https://developer.microsoft.com/json-schemas/api-extractor/v7/api-extractor.schema.json",
      mainEntryPointFilePath: "<projectFolder>/dist/index.d.ts",
      bundledPackages: [],
      apiReport: {
        enabled: true,
        reportFileName: `${shortName}.api.md`,
        reportFolder: "<projectFolder>/etc/",
      },
      docModel: {
        enabled: false,
      },
      dtsRollup: {
        enabled: false,
      },
      tsdocMetadata: {
        enabled: false,
      },
      messages: {
        compilerMessageReporting: {
          default: {
            logLevel: "warning",
          },
        },
        extractorMessageReporting: {
          default: {
            logLevel: "warning",
          },
          "ae-missing-release-tag": {
            logLevel: "none",
          },
        },
        tsdocMessageReporting: {
          default: {
            logLevel: "none",
          },
        },
      },
    },
    null,
    2,
  );
}

/**
 * Write a file only if its content would change (idempotency guard).
 * @param {string} filePath
 * @param {string} content
 */
function writeIfChanged(filePath, content) {
  if (existsSync(filePath)) {
    const existing = readFileSync(filePath, "utf-8");

    if (existing === content) return false;
  }

  writeFileSync(filePath, content, "utf-8");

  return true;
}

// ---------------------------------------------------------------------------
// Step 2 Ã¢â‚¬â€ Run api-extractor --local to generate the etc/<pkg>.api.md
// ---------------------------------------------------------------------------

/**
 * Run api-extractor in local mode for the given package.
 * Returns the api.md content string, or null on failure.
 * @param {string} pkgDir
 * @param {string} shortName
 */
function runApiExtractor(pkgDir, shortName) {
  const configPath = join(pkgDir, "api-extractor.json");

  if (!existsSync(configPath)) {
    console.error(`  [ERROR] No api-extractor.json at ${configPath}`);

    return null;
  }

  const reportPath = join(pkgDir, "etc", `${shortName}.api.md`);

  // Delete any STALE report from a prior seed BEFORE running. api-extractor
  // `run --local` regenerates the report on every successful invocation, so on
  // the success (or warnings-but-wrote) path a fresh report reappears; on a
  // genuine failure the extractor writes nothing and the existsSync checks
  // below correctly return null. Without this delete, a NON-ZERO extractor exit
  // that left the prior report on disk would return that stale content as if
  // freshly generated (silent stale-not-fresh) Ã¢â‚¬â€ the fingerprint would then be
  // composed over an out-of-date report. Destroying the prior artifact first
  // makes "report present after the run" unambiguously mean "produced by THIS
  // run" (the same guarantee the .NET seeder's forceFullRecompile buys by
  // deleting bin/obj so the analyzer MUST re-run).
  rmSync(reportPath, { force: true });

  // shell:true is REQUIRED for cross-platform launch. On Windows the
  // node_modules/.bin/api-extractor entry is a POSIX shell shim that Node's
  // spawnSync cannot exec directly (the runnable form is the sibling
  // api-extractor.CMD, and modern Node refuses to spawn a .cmd without a shell);
  // routing through the shell lets cmd.exe resolve the .CMD via PATHEXT. On POSIX
  // the shim is a node-shebang script the shell runs directly. Without this the
  // spawn silently ENOENTs (status !== 0, no report written) and only packages
  // whose etc/<pkg>.api.md already exists appear to "succeed" Ã¢â‚¬â€ a NEW package's
  // report never gets generated, and a changed-surface package's report is never
  // refreshed. The whole invocation is passed as ONE quoted command string (not a
  // command + args array) so shell:true does not trip DEP0190 and the quoting
  // tolerates spaces in either path.
  const command = `"${API_EXTRACTOR_BIN}" run --local --config "${configPath}"`;
  const result = spawnSync(command, {
    cwd: pkgDir,
    encoding: "utf-8",
    stdio: ["ignore", "pipe", "pipe"],
    shell: true,
  });

  if (result.status !== 0) {
    // api-extractor exits non-zero even on warnings in some cases. The report
    // is trustworthy ONLY if THIS run just (re)wrote it Ã¢â‚¬â€ the stale prior copy
    // was deleted above, so its presence here means the extractor produced it
    // despite the non-zero exit (a warnings-only run). If it is ABSENT the
    // extractor genuinely failed: return null so the caller fails loud, never
    // stale content.
    if (!existsSync(reportPath)) {
      console.error(`  [ERROR] api-extractor failed for ${shortName}`);
      console.error(result.stderr ?? "");

      return null;
    }

    // Warnings present but report was freshly written Ã¢â‚¬â€ treat as success.
    if (result.stderr?.includes("Warning:")) {
      console.warn(`  [WARN] api-extractor warnings for ${shortName}:`);
      console.warn(result.stderr);
    }
  }

  if (!existsSync(reportPath)) {
    console.error(`  [ERROR] Report not written: ${reportPath}`);

    return null;
  }

  return readFileSync(reportPath, "utf-8");
}

// ---------------------------------------------------------------------------
// Step 3 Ã¢â‚¬â€ Compute the source-based fingerprint. The final SHA-256 composition
// is delegated to the shared composeSourceFingerprintFromParts primitive (a
// byte-for-byte re-implementation of the release-runner provider's
// composeSourceFingerprint); the seedÃ¢â€ â€provider byte-identity of that primitive
// is pinned by tools/release-runner/tests/seed-provider-fingerprint-identity.test.ts.
// ---------------------------------------------------------------------------

/** LF-normalize so a CRLF/LF checkout difference cannot perturb the hash. */
function normalizeLf(text) {
  return text.replace(/\r\n/g, "\n");
}

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

/** Is this committed file part of the TS source dump? */
function isNpmSourceFile(relPosixPath) {
  const base = relPosixPath.slice(relPosixPath.lastIndexOf("/") + 1);

  if (base === ".release-fingerprint" || base === "CHANGELOG.md") return false;
  if (base.endsWith(".test.ts")) return false;
  if (base.endsWith(".ts")) return true;
  if (base === "package.json") return true;
  if (base === "api-extractor.json") return true;

  return base.startsWith("tsconfig") && base.endsWith(".json");
}

/**
 * List the package-relative POSIX paths of COMMITTED (git-tracked) TS source
 * files. Tracked-only is mandatory (the build can emit gitignored transients);
 * mirrors the release-runner's listSourceFiles BYTE-FOR-BYTE.
 */
function listNpmSourceFiles(packageDir) {
  const result = spawnSync("git", ["ls-files", "--", "."], {
    cwd: packageDir,
    encoding: "utf-8",
    maxBuffer: 32 * 1024 * 1024,
  });

  if (result.status !== 0) return [];

  return (result.stdout ?? "")
    .split("\n")
    .map((l) => l.trim().replace(/\\/g, "/"))
    .filter((l) => l.length > 0 && !isSkipped(l) && isNpmSourceFile(l));
}

/** Ordered, LF-normalized source dump for a package dir. */
function buildSourceDump(packageDir) {
  const sorted = listNpmSourceFiles(packageDir).sort();
  let dump = "";

  for (const relPath of sorted) {
    const content = readFileSync(join(packageDir, relPath), "utf-8");
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

/** The declared, committed TS toolchain pin as deterministic sorted-key JSON. */
function readNpmToolchainPin() {
  const rootPkg = JSON.parse(
    readFileSync(join(REPO_ROOT, "package.json"), "utf-8"),
  );
  const tsconfigBase = JSON.parse(
    readFileSync(join(TS_SHARED, "tsconfig.base.json"), "utf-8"),
  );

  return stableJson({
    module: tsconfigBase.compilerOptions?.module ?? "",
    target: tsconfigBase.compilerOptions?.target ?? "",
    typescript: rootPkg.devDependencies?.typescript ?? "",
  });
}

const TOOLCHAIN_PIN = readNpmToolchainPin();

/**
 * Build the DEPS (manifest-metadata) JSON for a TS package: substitute each
 * @dcsv-io/d2-* dep literal with its resolved version (at seed time = committed
 * version), then serialize {name, version, dependencies}. Mirrors the provider's
 * substituteResolvedDeps + buildNpmManifestMeta.
 *
 * @param {{ name?: string; version?: string; dependencies?: Record<string,string> }} pkgJson
 * @param {Map<string,string>} resolvedVersions
 */
function buildNpmDepsJson(pkgJson, resolvedVersions) {
  const deps = pkgJson.dependencies ?? {};
  const substituted = {};

  for (const [name, literal] of Object.entries(deps)) {
    substituted[name] = resolvedVersions.get(name) ?? literal;
  }

  const ownVersion =
    (pkgJson.name !== undefined
      ? resolvedVersions.get(pkgJson.name)
      : undefined) ?? pkgJson.version;

  return JSON.stringify({
    name: pkgJson.name ?? "",
    version: ownVersion ?? "",
    dependencies: substituted,
  });
}

/**
 * Compose the source-based fingerprint over the ordered tuple
 *   ( committed source dump + the .api.md report + resolved deps + toolchain ).
 *
 * Delegates the final SHA-256 composition to the shared primitive so it is
 * byte-identical to the release-runner's composeSourceFingerprint (the drift
 * check recomputes via the runner and compares like-for-like). No build. The
 * primitive LF-normalizes the apiMd report, so the raw report text is passed.
 *
 * @param {string} pkgDir
 * @param {string} apiMd
 * @param {string} depsJson
 */
function composeSourceFingerprint(pkgDir, apiMd, depsJson) {
  return composeSourceFingerprintFromParts({
    sourceDump: buildSourceDump(pkgDir),
    apiReport: apiMd,
    depsJson,
    toolchainJson: TOOLCHAIN_PIN,
  });
}

// ---------------------------------------------------------------------------
// Resolved-version map Ã¢â‚¬â€ each consumable @dcsv-io/d2-* at its committed version.
// At seed time resolvedVersions == the committed versions, matching how the
// provider seeds its map on a no-op drift recompute (every dep at its current
// version), so the seeded fingerprint equals the runtime recompute.
// ---------------------------------------------------------------------------

const RESOLVED_VERSIONS = new Map();

for (const { dir, pkgName } of CONSUMABLES) {
  const pkgJsonPath = join(dir, "package.json");

  if (existsSync(pkgJsonPath)) {
    const pkgJson = JSON.parse(readFileSync(pkgJsonPath, "utf-8"));
    RESOLVED_VERSIONS.set(pkgName, pkgJson.version ?? "");
  }
}

// ---------------------------------------------------------------------------
// Main
// ---------------------------------------------------------------------------

let configsWritten = 0;
let configsSkipped = 0;
let apiMdWritten = 0;
let fpWritten = 0;
let errors = 0;

/** @type {Array<{pkgName: string, issue: string}>} */
const specialHandling = [];

for (const { dir, shortName, pkgName } of CONSUMABLES) {
  console.log(`\n[${pkgName}]`);

  // --- Step 1: Write api-extractor.json ---
  const configPath = join(dir, "api-extractor.json");
  const configContent = apiExtractorConfig(shortName) + "\n";
  const configChanged = writeIfChanged(configPath, configContent);

  if (configChanged) {
    console.log(`  + Wrote api-extractor.json`);
    configsWritten++;
  } else {
    console.log(`  = api-extractor.json unchanged`);
    configsSkipped++;
  }

  // --- Step 2: Ensure etc/ dir exists ---
  const etcDir = join(dir, "etc");
  mkdirSync(etcDir, { recursive: true });

  // --- Step 2: Run api-extractor to generate etc/<pkg>.api.md ---
  const distIndexDts = join(dir, "dist", "index.d.ts");

  if (!existsSync(distIndexDts)) {
    console.error(
      `  [ERROR] No dist/index.d.ts Ã¢â‚¬â€ build the package first.`,
    );
    specialHandling.push({ pkgName, issue: "Missing dist/index.d.ts" });
    errors++;
    continue;
  }

  const apiMdContent = runApiExtractor(dir, shortName);

  if (apiMdContent === null) {
    specialHandling.push({ pkgName, issue: "api-extractor failed" });
    errors++;
    continue;
  }

  const reportPath = join(etcDir, `${shortName}.api.md`);

  // Check if any public members were found.
  const hasPublicMembers = apiMdContent.includes("export ");
  const allowEmpty = ALLOW_EMPTY_PACKAGES.has(pkgName);

  // FAIL-LOUD GUARD: a degenerate .api.md with NO `export ` line means
  // api-extractor analyzed an empty/missing dist/index.d.ts. Composing a
  // fingerprint over that degenerate content (and committing it) would let the
  // currency check pass against the degenerate baseline Ã¢â‚¬â€ invisible corruption,
  // the same class as the .NET silent empty-wipe. The guard throws in that case
  // (unless the package is explicitly allow-listed); on a throw we skip the
  // fingerprint write and count an error so the run FAILS LOUD (exit 1).
  try {
    assertApiReportNotDegenerate({ pkgName, hasPublicMembers, allowEmpty });
  } catch (err) {
    console.error(
      `  [ERROR] ${err instanceof Error ? err.message : String(err)}`,
    );
    specialHandling.push({
      pkgName,
      issue:
        "No public exports in .api.md (degenerate surface) Ã¢â‚¬â€ refused",
    });
    errors++;
    continue;
  }

  if (!hasPublicMembers) {
    specialHandling.push({
      pkgName,
      issue: "No public exports in .api.md Ã¢â‚¬â€ permitted via --allow-empty",
    });
  }

  console.log(
    `  + Generated ${shortName}.api.md (${hasPublicMembers ? "has public API" : "empty surface (allow-empty)"})`,
  );
  apiMdWritten++;

  // --- Step 3: Compose + write the source-based etc/.release-fingerprint ---
  // The fingerprint reads the FRESHLY-WRITTEN committed .api.md back from disk
  // (the provider reads the same committed file), so the seeded hash equals the
  // runtime recompute.
  const pkgJsonPath = join(dir, "package.json");
  const pkgJson = JSON.parse(readFileSync(pkgJsonPath, "utf-8"));
  const committedApiMd = readFileSync(reportPath, "utf-8");
  const depsJson = buildNpmDepsJson(pkgJson, RESOLVED_VERSIONS);
  const fingerprint = composeSourceFingerprint(dir, committedApiMd, depsJson);
  const fpPath = join(etcDir, ".release-fingerprint");
  const fpContent = fingerprint + "\n";
  const fpChanged = writeIfChanged(fpPath, fpContent);

  if (fpChanged) {
    console.log(
      `  + Wrote .release-fingerprint (${fingerprint.slice(0, 16)}Ã¢â‚¬Â¦)`,
    );
    fpWritten++;
  } else {
    console.log(
      `  = .release-fingerprint unchanged (${fingerprint.slice(0, 16)}Ã¢â‚¬Â¦)`,
    );
  }
}

// ---------------------------------------------------------------------------
// Summary
// ---------------------------------------------------------------------------

console.log(`
=== Seed complete ===
  Packages processed : ${CONSUMABLES.length}
  api-extractor.json : ${configsWritten} written, ${configsSkipped} unchanged
  etc/<pkg>.api.md   : ${apiMdWritten} generated
  .release-fingerprint : ${fpWritten} written/updated
  Errors             : ${errors}
`);

if (specialHandling.length > 0) {
  console.log("Special handling:");

  for (const { pkgName, issue } of specialHandling) {
    console.log(`  ${pkgName}: ${issue}`);
  }

  console.log();
}

if (errors > 0) {
  process.exit(1);
}
