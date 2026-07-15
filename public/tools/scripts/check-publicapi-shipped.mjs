// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
//
// Commit / precheck gate: refuse a working tree where any .NET consumable's
// PublicAPI.Shipped.txt is missing/header-only while HEAD still has API lines.
//
// NOT the same as "package deleted" or "intentional empty surface":
//   - Package deleted: .csproj gone → not in inventory (delete baselines with it).
//   - Brand-new / already-empty at HEAD: head line count 0 → OK.
//   - Intentional N→0 API: seed --allow-empty Pkg, then one commit with
//     SEED_ALLOW_EMPTY=Pkg (or --allow-empty Pkg on this CLI). After HEAD is
//     empty, later commits need no allow.
//   - Failure wipe: empty disk + non-empty HEAD + no allow → FAIL (this gate).
//
// Usage (repo root):
//   node public/tools/scripts/check-publicapi-shipped.mjs
//   SEED_ALLOW_EMPTY=DcsvIo.D2.Foo node public/tools/scripts/check-publicapi-shipped.mjs
//
// Exit 0 = OK; exit 1 = one or more packages wrongfully empty (table on stderr).

import { spawnSync } from "node:child_process";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

import {
  NULLABLE_HEADER,
  assertShippedContentNotWrongfullyEmpty,
  countPublicApiLines,
} from "./lib/publicapi-empty-guard.mjs";

// Monorepo root: public/tools/scripts → ../../../ (not public/ — dual-tree layout).
const REPO_ROOT = path.resolve(
  path.dirname(fileURLToPath(import.meta.url)),
  "..",
  "..",
  "..",
);

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

function readCommittedApiLineCount(shippedPath) {
  const relPosix = path.relative(REPO_ROOT, shippedPath).replace(/\\/g, "/");
  const result = spawnSync("git", ["show", `HEAD:${relPosix}`], {
    cwd: REPO_ROOT,
    encoding: "utf8",
    maxBuffer: 32 * 1024 * 1024,
  });

  if (result.status !== 0) return 0;

  return countPublicApiLines(result.stdout ?? "");
}

const sharedDotnetRoot = path.join(REPO_ROOT, "public", "packages", "dotnet");

const sharedConsumables = walk(sharedDotnetRoot)
  .filter((f) => f.endsWith(".csproj"))
  .filter((f) => !f.endsWith("SourceGen.csproj"))
  .filter((f) => !/DcsvIo\.D2\.Tests\.csproj$/.test(f));

const kcClient = path.join(
  REPO_ROOT,
  "private",
  "services",
  "edge",
  "key-custodian",
  "client",
  "DcsvIo.D2.Private.Edge.KeyCustodian.Client.csproj",
);

const consumables = [...sharedConsumables, kcClient].sort();

// Optional --allow-empty PackageId (repeatable) for genuine zero-API packages.
const args = process.argv.slice(2);
const allowEmpty = new Set();

for (let i = 0; i < args.length; i++) {
  if (args[i] === "--allow-empty" && args[i + 1]) {
    allowEmpty.add(args[i + 1]);
  }
}

const envAllow = (process.env.SEED_ALLOW_EMPTY ?? "").trim();

if (envAllow.length > 0) {
  allowEmpty.add(envAllow);
}

const failures = [];

for (const csprojPath of consumables) {
  const packageId = path.basename(csprojPath, ".csproj");
  const shippedPath = path.join(
    path.dirname(csprojPath),
    "PublicAPI.Shipped.txt",
  );
  const headCount = readCommittedApiLineCount(shippedPath);
  const diskContent = fs.existsSync(shippedPath)
    ? fs.readFileSync(shippedPath, "utf8")
    : "";

  try {
    assertShippedContentNotWrongfullyEmpty({
      packageId,
      shippedContent: diskContent,
      headSurfaceCount: headCount,
      allowEmpty: allowEmpty.has(packageId),
    });
  } catch (err) {
    failures.push({
      packageId,
      headCount,
      diskCount: countPublicApiLines(diskContent),
      path: path.relative(REPO_ROOT, shippedPath).replace(/\\/g, "/"),
      message: err instanceof Error ? err.message : String(err),
    });
  }
}

if (failures.length === 0) {
  process.stdout.write(
    `PublicAPI.Shipped empty-check: OK (${consumables.length} consumables)\n`,
  );
  process.exit(0);
}

process.stderr.write(
  "\n  ✖  Commit / tree rejected: PublicAPI.Shipped.txt is EMPTY vs non-empty HEAD\n\n",
);
process.stderr.write("  package | head_lines | disk_lines | path\n");
process.stderr.write("  ------- | ---------- | ---------- | ----\n");

for (const f of failures) {
  process.stderr.write(
    `  ${f.packageId} | ${f.headCount} | ${f.diskCount} | ${f.path}\n`,
  );
}

process.stderr.write(
  "\n  Failure wipe → restore:\n" +
    "    git checkout HEAD -- <path>/PublicAPI.Shipped.txt\n" +
    "  Re-seed only if source changed:\n" +
    "    node tools/scripts/seed-publicapi-baselines.mjs --package <PackageId>\n\n" +
    "  Intentional full-surface removal (first empty commit only):\n" +
    "    node tools/scripts/seed-publicapi-baselines.mjs --package <Id> --allow-empty <Id>\n" +
    "    SEED_ALLOW_EMPTY=<Id> git commit ...\n" +
    "  (After that commit HEAD is empty; later commits need no allow.)\n\n" +
    "  Entire package deletion: remove .csproj + PublicAPI.* + fingerprint together.\n" +
    `  (${NULLABLE_HEADER} alone is NOT a valid baseline while HEAD still has API lines.)\n\n`,
);

// First failure message for greppable detail.
process.stderr.write(failures[0].message + "\n");
process.exit(1);
