// Copyright (c) DCSV. All rights reserved.
//
// Idempotent seeding tool: installs api-extractor.json configs and generates
// committed baselines (etc/<pkg>.api.md + etc/dist-fingerprint.txt) for all
// 29 @d2/* consumable packages under server/shared/typescript/.
//
// EXCLUDES tooling-only packages: typespec-decorators, typespec-emitters,
// contract-tests (these are dev fixtures, not consumable libraries).
//
// Run from repo root: `node tools/scripts/seed-apiextractor-baselines.mjs`
// Idempotent: safe to re-run; regenerates baselines byte-for-byte if source
// is unchanged (fingerprint and api.md are deterministic outputs).
//
// Prerequisites:
//   - All 29 packages must have a built dist/ (run `pnpm -r build` first or
//     pass --skip-build to use existing dist/).
//   - @microsoft/api-extractor must be installed in tools/release-runner
//     (it is — declared as a devDependency there).

import { createHash } from "node:crypto";
import {
  existsSync,
  mkdirSync,
  readdirSync,
  readFileSync,
  writeFileSync,
} from "node:fs";
import { dirname, join, relative } from "node:path";
import { spawnSync } from "node:child_process";
import { fileURLToPath } from "node:url";

// ---------------------------------------------------------------------------
// Repo layout
// ---------------------------------------------------------------------------

const REPO_ROOT = join(dirname(fileURLToPath(import.meta.url)), "..", "..");
const TS_SHARED = join(REPO_ROOT, "server", "shared", "typescript");
const RUNNER_DIR = join(REPO_ROOT, "tools", "release-runner");

// api-extractor binary is installed under tools/release-runner
const API_EXTRACTOR_BIN = join(
  RUNNER_DIR,
  "node_modules",
  ".bin",
  "api-extractor",
);

// ---------------------------------------------------------------------------
// The 29 consumable packages: [pkgDir, shortName] pairs.
// Derived from the package names (@d2/<shortName>) so that api.md report
// filenames are stable regardless of the directory structure.
// Excludes: typespec-decorators, typespec-emitters, contract-tests.
// ---------------------------------------------------------------------------

/** @type {Array<{dir: string, shortName: string, pkgName: string}>} */
const CONSUMABLES = [
  {
    dir: join(TS_SHARED, "auth", "abstractions"),
    shortName: "auth-abstractions",
    pkgName: "@d2/auth-abstractions",
  },
  {
    dir: join(TS_SHARED, "auth", "context-abstractions"),
    shortName: "auth-context-abstractions",
    pkgName: "@d2/auth-context-abstractions",
  },
  {
    dir: join(TS_SHARED, "encryption-abstractions"),
    shortName: "encryption-abstractions",
    pkgName: "@d2/encryption-abstractions",
  },
  {
    dir: join(TS_SHARED, "error-category"),
    shortName: "error-category",
    pkgName: "@d2/error-category",
  },
  {
    dir: join(TS_SHARED, "error-codes-registry"),
    shortName: "error-codes-registry",
    pkgName: "@d2/error-codes-registry",
  },
  {
    dir: join(TS_SHARED, "geo", "abstractions"),
    shortName: "geo-abstractions",
    pkgName: "@d2/geo-abstractions",
  },
  {
    dir: join(TS_SHARED, "geo", "default"),
    shortName: "geo-default",
    pkgName: "@d2/geo-default",
  },
  {
    dir: join(TS_SHARED, "grpc-client"),
    shortName: "grpc-client",
    pkgName: "@d2/grpc-client",
  },
  {
    dir: join(TS_SHARED, "headers", "amqp"),
    shortName: "headers-amqp",
    pkgName: "@d2/headers-amqp",
  },
  {
    dir: join(TS_SHARED, "headers", "common"),
    shortName: "headers-common",
    pkgName: "@d2/headers-common",
  },
  {
    dir: join(TS_SHARED, "headers", "core"),
    shortName: "headers",
    pkgName: "@d2/headers",
  },
  {
    dir: join(TS_SHARED, "headers", "grpc"),
    shortName: "headers-grpc",
    pkgName: "@d2/headers-grpc",
  },
  {
    dir: join(TS_SHARED, "headers", "http"),
    shortName: "headers-http",
    pkgName: "@d2/headers-http",
  },
  {
    dir: join(TS_SHARED, "i18n-abstractions"),
    shortName: "i18n-abstractions",
    pkgName: "@d2/i18n-abstractions",
  },
  {
    dir: join(TS_SHARED, "i18n-keys"),
    shortName: "i18n-keys",
    pkgName: "@d2/i18n-keys",
  },
  {
    dir: join(TS_SHARED, "i18n"),
    shortName: "i18n",
    pkgName: "@d2/i18n",
  },
  {
    dir: join(TS_SHARED, "logging"),
    shortName: "logging",
    pkgName: "@d2/logging",
  },
  {
    dir: join(TS_SHARED, "messaging-abstractions"),
    shortName: "messaging-abstractions",
    pkgName: "@d2/messaging-abstractions",
  },
  {
    dir: join(TS_SHARED, "problem-details-abstractions"),
    shortName: "problem-details-abstractions",
    pkgName: "@d2/problem-details-abstractions",
  },
  {
    dir: join(TS_SHARED, "protos"),
    shortName: "protos",
    pkgName: "@d2/protos",
  },
  {
    dir: join(TS_SHARED, "request-context-abstractions"),
    shortName: "request-context-abstractions",
    pkgName: "@d2/request-context-abstractions",
  },
  {
    dir: join(TS_SHARED, "resilience"),
    shortName: "resilience",
    pkgName: "@d2/resilience",
  },
  {
    dir: join(TS_SHARED, "result"),
    shortName: "result",
    pkgName: "@d2/result",
  },
  {
    dir: join(TS_SHARED, "service-defaults"),
    shortName: "service-defaults",
    pkgName: "@d2/service-defaults",
  },
  {
    dir: join(TS_SHARED, "telemetry"),
    shortName: "telemetry",
    pkgName: "@d2/telemetry",
  },
  {
    dir: join(TS_SHARED, "time"),
    shortName: "time",
    pkgName: "@d2/time",
  },
  {
    dir: join(TS_SHARED, "utilities"),
    shortName: "utilities",
    pkgName: "@d2/utilities",
  },
  {
    dir: join(TS_SHARED, "validation", "abstractions"),
    shortName: "validation-abstractions",
    pkgName: "@d2/validation-abstractions",
  },
  {
    dir: join(TS_SHARED, "validation", "default"),
    shortName: "validation",
    pkgName: "@d2/validation",
  },
];

// ---------------------------------------------------------------------------
// Step 1 — Write api-extractor.json (idempotent: skip if content unchanged)
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
// Step 2 — Run api-extractor --local to generate the etc/<pkg>.api.md
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

  const result = spawnSync(
    API_EXTRACTOR_BIN,
    ["run", "--local", "--config", configPath],
    {
      cwd: pkgDir,
      encoding: "utf-8",
      stdio: ["ignore", "pipe", "pipe"],
    },
  );

  const reportPath = join(pkgDir, "etc", `${shortName}.api.md`);

  if (result.status !== 0) {
    // api-extractor exits non-zero even on warnings in some cases.
    // Only treat as failure if the report file was NOT written.
    if (!existsSync(reportPath)) {
      console.error(`  [ERROR] api-extractor failed for ${shortName}`);
      console.error(result.stderr ?? "");

      return null;
    }

    // Warnings present but report was written — treat as success.
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
// Step 3 — Compute dist-fingerprint (mirrors ts-api-adapter.ts logic)
// ---------------------------------------------------------------------------

/**
 * Strip single-line and block JS comments, collapse blank lines.
 * @param {string} js
 */
function normaliseJsForFingerprint(js) {
  let s = js.replace(/\/\*[\s\S]*?\*\//g, "");
  s = s.replace(/\/\/[^\n]*/g, "");
  s = s.replace(/(\r?\n\s*){2,}/g, "\n\n");

  return s.trim();
}

/**
 * Recursively collect files with given extensions under dir.
 * @param {string} dir
 * @param {string[]} extensions
 * @param {string[]} out
 */
function walkDir(dir, extensions, out) {
  if (!existsSync(dir)) return;

  const entries = readdirSync(dir, { withFileTypes: true });

  for (const entry of entries) {
    const full = join(dir, entry.name);

    if (entry.isDirectory()) {
      walkDir(full, extensions, out);
    } else if (entry.isFile()) {
      const ext = entry.name.slice(entry.name.lastIndexOf("."));

      if (extensions.includes(ext)) out.push(full);
    }
  }
}

/**
 * Compute the dist fingerprint (SHA-256) for a package.
 * Mirrors computeDistFingerprint in ts-api-adapter.ts exactly.
 * @param {string} pkgDir
 * @param {{ name?: string; version?: string; dependencies?: Record<string,string> }} pkgJson
 */
function computeDistFingerprint(pkgDir, pkgJson) {
  const distDir = join(pkgDir, "dist");
  const hash = createHash("sha256");

  // Hash .js files (comment-stripped) in sorted path order.
  const jsFiles = [];
  walkDir(distDir, [".js"], jsFiles);
  jsFiles.sort();

  for (const filePath of jsFiles) {
    const relPath = relative(pkgDir, filePath).replace(/\\/g, "/");
    const content = readFileSync(filePath, "utf-8");
    const normalised = normaliseJsForFingerprint(content);

    hash.update(`JS:${relPath}\n${normalised}\n`);
  }

  // Hash .d.ts files (verbatim) in sorted path order.
  const dtsFiles = [];
  walkDir(distDir, [".d.ts"], dtsFiles);
  dtsFiles.sort();

  for (const filePath of dtsFiles) {
    const relPath = relative(pkgDir, filePath).replace(/\\/g, "/");
    const content = readFileSync(filePath, "utf-8");

    hash.update(`DTS:${relPath}\n${content}\n`);
  }

  // Hash package.json runtime metadata.
  const meta = JSON.stringify({
    name: pkgJson.name ?? "",
    version: pkgJson.version ?? "",
    dependencies: pkgJson.dependencies ?? {},
  });

  hash.update(`PKG:${meta}\n`);

  return hash.digest("hex");
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
    console.error(`  [ERROR] No dist/index.d.ts — build the package first.`);
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

  if (!hasPublicMembers) {
    specialHandling.push({
      pkgName,
      issue: "No public exports detected in .api.md (empty surface)",
    });
  }

  console.log(
    `  + Generated ${shortName}.api.md (${hasPublicMembers ? "has public API" : "empty surface"})`,
  );
  apiMdWritten++;

  // --- Step 3: Compute and write dist-fingerprint.txt ---
  const pkgJsonPath = join(dir, "package.json");
  const pkgJson = JSON.parse(readFileSync(pkgJsonPath, "utf-8"));
  const fingerprint = computeDistFingerprint(dir, pkgJson);
  const fpPath = join(etcDir, "dist-fingerprint.txt");
  const fpContent = fingerprint + "\n";
  const fpChanged = writeIfChanged(fpPath, fpContent);

  if (fpChanged) {
    console.log(
      `  + Wrote dist-fingerprint.txt (${fingerprint.slice(0, 16)}…)`,
    );
    fpWritten++;
  } else {
    console.log(
      `  = dist-fingerprint.txt unchanged (${fingerprint.slice(0, 16)}…)`,
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
  dist-fingerprint   : ${fpWritten} written/updated
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
