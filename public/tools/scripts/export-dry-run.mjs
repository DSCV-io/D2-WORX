// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------
//
// Export dry-run / IP fence for the public OSS surface.
//
// Default mode validates that the monorepo export set is exactly:
//   - public/**  (product export surface)
//   - closed extras (stage/export clone build props only):
//       Directory.Build.props, Directory.Packages.props, global.json,
//       NuGet.config, .editorconfig
// Hard denylist always wins (secrets, private/**, docs/dev, infra, …).
// Optional --stage-dir materializes stage-root monorepo-mirror topology and
// can optionally run a public solution build. Never pushes remotes.
//
// Usage:
//   node public/tools/scripts/export-dry-run.mjs
//   node public/tools/scripts/export-dry-run.mjs --stage-dir <isolated-tmp>
//   node public/tools/scripts/export-dry-run.mjs --stage-dir <tmp> --build
//   node public/tools/scripts/export-dry-run.mjs --ip-fence
//
// Exit: 0 clean; non-zero on allowlist/denylist/IP/push-refusal failures.

import {
  cpSync,
  existsSync,
  mkdirSync,
  readdirSync,
  readFileSync,
  realpathSync,
  statSync,
  writeFileSync,
} from "node:fs";
import { join, relative, resolve, sep } from "node:path";
import { fileURLToPath } from "node:url";
import { spawnSync } from "node:child_process";

// ---------------------------------------------------------------------------
// Constants
// ---------------------------------------------------------------------------

/** Closed extras staged as siblings of `public/` (not under public/). */
export const CLOSED_EXTRAS = Object.freeze([
  "Directory.Build.props",
  "Directory.Packages.props",
  "global.json",
  "NuGet.config",
  ".editorconfig",
]);

/** Hard denylist prefixes / exact names (always-on, even if listed). */
export const HARD_DENYLIST = Object.freeze([
  "secrets",
  ".env.secrets",
  ".env.local",
  "private",
  "docs/dev",
  "infra",
  "gen-dev-keys",
]);

/** Product-IP markers that must never appear under public package trees. */
export const PRODUCT_IP_MARKERS = Object.freeze([
  "DcsvIo.D2.Private",
  "d2-private-",
  "keycustodian-error-codes",
  "gen-dev-keys",
]);

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

/**
 * Resolve monorepo root by walking upward for sentinels.
 *
 * @param {string} [start]
 * @returns {string}
 */
export function findMonorepoRoot(start = process.cwd()) {
  let dir = resolve(start);

  for (let i = 0; i < 24; i++) {
    if (
      existsSync(join(dir, "D2.slnx")) ||
      existsSync(join(dir, "pnpm-workspace.yaml"))
    ) {
      return dir;
    }

    const parent = resolve(dir, "..");

    if (parent === dir) break;

    dir = parent;
  }

  throw new Error(
    `export-dry-run: could not locate monorepo root from ${start}`,
  );
}

/**
 * Normalize to repo-relative forward-slash path.
 *
 * @param {string} repoRoot
 * @param {string} absOrRel
 * @returns {string}
 */
export function toRepoRelative(repoRoot, absOrRel) {
  const abs = resolve(repoRoot, absOrRel);
  return relative(repoRoot, abs).replace(/\\/g, "/");
}

/**
 * True when a relative path is under hard denylist.
 * Any path segment equal to a single-token deny (secrets, private, infra, …)
 * or multi-segment deny (docs/dev) wins — including terminal leaf segments
 * such as `public/secrets` / `public/foo/private`.
 *
 * @param {string} relPath
 * @returns {boolean}
 */
export function isHardDenylisted(relPath) {
  const n = relPath.replace(/\\/g, "/").replace(/^\.\//, "");

  if (n === "" || n === ".") return false;

  const segments = n.split("/").filter((s) => s.length > 0);
  const base = segments[segments.length - 1] ?? n;

  // Exact env file names anywhere (basename)
  if (base === ".env.secrets" || base === ".env.local") return true;

  for (const deny of HARD_DENYLIST) {
    // Tooling substring (script names may embed the token)
    if (deny === "gen-dev-keys") {
      if (n.includes("gen-dev-keys")) return true;
      continue;
    }

    // Multi-segment deny (e.g. docs/dev)
    if (deny.includes("/")) {
      if (
        n === deny ||
        n.startsWith(`${deny}/`) ||
        n.includes(`/${deny}/`) ||
        n.endsWith(`/${deny}`)
      ) {
        return true;
      }

      continue;
    }

    // Single-token deny: exact, prefix, mid-path, terminal segment, or any segment
    if (
      n === deny ||
      n.startsWith(`${deny}/`) ||
      n.includes(`/${deny}/`) ||
      n.endsWith(`/${deny}`) ||
      segments.includes(deny)
    ) {
      return true;
    }
  }

  return false;
}

/**
 * True when path is allowlisted for export/stage.
 *
 * @param {string} relPath
 * @returns {boolean}
 */
export function isAllowlisted(relPath) {
  const n = relPath.replace(/\\/g, "/").replace(/^\.\//, "");

  if (n === "public" || n.startsWith("public/")) return true;

  if (CLOSED_EXTRAS.includes(n)) return true;

  return false;
}

/**
 * Detect path-traversal / escape attempts relative to repo root.
 *
 * @param {string} repoRoot
 * @param {string} candidate
 * @returns {boolean} true when unsafe
 */
export function isPathTraversal(repoRoot, candidate) {
  const n = candidate.replace(/\\/g, "/");

  if (n.includes("\0")) return true;

  // Absolute path outside repo
  if (
    resolve(candidate) === candidate ||
    /^[A-Za-z]:\//.test(n) ||
    n.startsWith("/")
  ) {
    const abs = resolve(candidate);
    const rootAbs = resolve(repoRoot);
    const rel = relative(rootAbs, abs);

    if (rel.startsWith("..") || (rel === "" && abs !== rootAbs)) {
      // outside or equal root without public segment — treat absolute outside as traversal
      if (!abs.startsWith(rootAbs + sep) && abs !== rootAbs) return true;
    }
  }

  if (n.includes("..")) {
    const abs = resolve(repoRoot, candidate);
    const rootAbs = resolve(repoRoot);
    const rel = relative(rootAbs, abs).replace(/\\/g, "/");

    if (rel.startsWith("..") || rel.includes("../")) return true;

    // public/../private style that lands outside public
    if (!isAllowlisted(rel) || isHardDenylisted(rel)) return true;
  }

  return false;
}

/**
 * Validate an explicit allowlist entry (adversarial empty/malformed).
 *
 * @param {string} entry
 * @returns {{ ok: boolean, reason?: string }}
 */
export function validateAllowlistEntry(entry) {
  if (entry === undefined || entry === null) {
    return { ok: false, reason: "allowlist entry is null/undefined" };
  }

  if (typeof entry !== "string") {
    return { ok: false, reason: "allowlist entry is not a string" };
  }

  const trimmed = entry.trim();

  if (trimmed.length === 0) {
    return { ok: false, reason: "empty allowlist entry" };
  }

  if (trimmed.includes("\0") || trimmed.includes("\\0")) {
    return { ok: false, reason: "malformed allowlist entry (NUL)" };
  }

  // Reject path traversal forms as allowlist entries
  if (
    trimmed.includes("..") ||
    trimmed.startsWith("/") ||
    /^[A-Za-z]:/.test(trimmed)
  ) {
    return { ok: false, reason: "malformed allowlist entry (escape/absolute)" };
  }

  if (!isAllowlisted(trimmed) || isHardDenylisted(trimmed)) {
    return {
      ok: false,
      reason: `allowlist entry outside closed set or denylisted: ${trimmed}`,
    };
  }

  return { ok: true };
}

/**
 * Walk public/** and report denylist / IP markers (path-level).
 *
 * @param {string} repoRoot
 * @returns {{ failures: string[] }}
 */
export function scanPublicTree(repoRoot) {
  const failures = [];
  const publicRoot = join(repoRoot, "public");

  if (!existsSync(publicRoot)) {
    failures.push("public/ missing at monorepo root");
    return { failures };
  }

  function walk(dir) {
    let entries;

    try {
      entries = readdirSync(dir, { withFileTypes: true });
    } catch {
      return;
    }

    for (const entry of entries) {
      const full = join(dir, entry.name);
      const rel = toRepoRelative(repoRoot, full);

      if (isHardDenylisted(rel)) {
        failures.push(`denylist path under public scan: ${rel}`);
        continue;
      }

      if (entry.isDirectory()) {
        if (
          entry.name === "node_modules" ||
          entry.name === "bin" ||
          entry.name === "obj"
        ) {
          continue;
        }

        walk(full);
      } else if (entry.isFile()) {
        // Path-level product IP under public packages
        for (const marker of PRODUCT_IP_MARKERS) {
          if (rel.includes(marker)) {
            failures.push(
              `product IP marker in public path: ${rel} (${marker})`,
            );
          }
        }
      }
    }
  }

  walk(publicRoot);

  return { failures };
}

/**
 * Validate a candidate path list against allowlist + denylist + traversal.
 *
 * @param {string} repoRoot
 * @param {readonly string[]} candidates
 * @returns {{ ok: boolean, failures: string[] }}
 */
export function validateCandidatePaths(repoRoot, candidates) {
  const failures = [];

  for (const raw of candidates) {
    if (typeof raw !== "string" || raw.trim() === "") {
      failures.push("empty or non-string candidate path");
      continue;
    }

    if (isPathTraversal(repoRoot, raw)) {
      failures.push(`path traversal / escape: ${raw}`);
      continue;
    }

    const rel = toRepoRelative(repoRoot, resolve(repoRoot, raw));

    if (isHardDenylisted(rel) || isHardDenylisted(raw.replace(/\\/g, "/"))) {
      failures.push(`hard denylist: ${raw}`);
      continue;
    }

    if (!isAllowlisted(rel)) {
      failures.push(`outside allowlist (public/** + closed extras): ${raw}`);
    }
  }

  return { ok: failures.length === 0, failures };
}

/**
 * Stage export surface into an isolated directory (stage-root topology).
 *
 * Layout:
 *   stage/
 *     Directory.Build.props  (etc. closed extras)
 *     public/
 *
 * @param {string} repoRoot
 * @param {string} stageDir absolute path OUTSIDE monorepo preferred
 * @returns {{ ok: boolean, failures: string[], stageDir: string }}
 */
export function stageExportTree(repoRoot, stageDir) {
  const failures = [];
  const stageAbs = resolve(stageDir);

  // Prefer isolation: warn if stage is inside monorepo without being a pure temp name
  const monorepoAbs = resolve(repoRoot);
  const relToRepo = relative(monorepoAbs, stageAbs).replace(/\\/g, "/");

  if (!relToRepo.startsWith("..") && relToRepo !== "") {
    // Stage inside monorepo — still allowed but must have stage-root props to
    // avoid GetPathOfFileAbove walk-out. We always write extras at stage root.
  }

  mkdirSync(stageAbs, { recursive: true });

  for (const extra of CLOSED_EXTRAS) {
    const src = join(repoRoot, extra);

    if (!existsSync(src)) {
      failures.push(`closed extra missing at monorepo root: ${extra}`);
      continue;
    }

    cpSync(src, join(stageAbs, extra));
  }

  const publicSrc = join(repoRoot, "public");

  if (!existsSync(publicSrc)) {
    failures.push("public/ missing — cannot stage");
    return { ok: false, failures, stageDir: stageAbs };
  }

  cpSync(publicSrc, join(stageAbs, "public"), {
    recursive: true,
    filter: (src) => {
      const base = src.split(/[/\\]/).pop() ?? "";
      // Skip heavy/local dirs in stage
      if (base === "node_modules" || base === "bin" || base === "obj") {
        return false;
      }

      // Hard denylist wins inside public/** (fail-closed materialization)
      const rel = toRepoRelative(repoRoot, src);

      if (isHardDenylisted(rel)) {
        return false;
      }

      return true;
    },
  });

  // Anti-walk-out marker: record resolved Directory.Build.props expected path
  const stagedProps = join(stageAbs, "Directory.Build.props");

  if (!existsSync(stagedProps)) {
    failures.push("stage missing Directory.Build.props (anti-walk-out)");
  }

  writeFileSync(
    join(stageAbs, ".export-stage-meta.json"),
    JSON.stringify(
      {
        monorepoRoot: monorepoAbs,
        stageRoot: stageAbs,
        closedExtras: CLOSED_EXTRAS,
        stagedAt: new Date().toISOString(),
      },
      undefined,
      2,
    ) + "\n",
    "utf-8",
  );

  return { ok: failures.length === 0, failures, stageDir: stageAbs };
}

/**
 * Assert stage Directory.Build.props is inside stage (not host walk-out).
 *
 * @param {string} stageDir
 * @returns {{ ok: boolean, resolvedProps?: string, reason?: string }}
 */
export function assertStagePropsIsolation(stageDir) {
  const expected = resolve(stageDir, "Directory.Build.props");

  if (!existsSync(expected)) {
    return { ok: false, reason: "stage Directory.Build.props missing" };
  }

  const resolved = realpathSync(expected);
  const stageRoot = realpathSync(stageDir);
  const rel = relative(stageRoot, resolved).replace(/\\/g, "/");

  if (rel.startsWith("..") || rel.includes("..")) {
    return {
      ok: false,
      resolvedProps: resolved,
      reason: "Directory.Build.props resolves outside stage (walk-out)",
    };
  }

  return { ok: true, resolvedProps: resolved };
}

/**
 * Optional public solution build inside stage.
 *
 * @param {string} stageDir
 * @returns {{ ok: boolean, output: string }}
 */
export function buildStagedPublicSlnx(stageDir) {
  const slnx = join(stageDir, "public", "D2.Public.slnx");

  if (!existsSync(slnx)) {
    return { ok: false, output: `missing ${slnx}` };
  }

  const result = spawnSync(
    "dotnet",
    ["build", slnx, "--configuration", "Release"],
    {
      cwd: stageDir,
      encoding: "utf-8",
      maxBuffer: 16 * 1024 * 1024,
    },
  );

  const output = `${result.stdout ?? ""}\n${result.stderr ?? ""}`;

  return { ok: result.status === 0, output };
}

// ---------------------------------------------------------------------------
// CLI
// ---------------------------------------------------------------------------

/**
 * Parse argv into flags.
 *
 * @param {string[]} argv
 */
export function parseExportArgs(argv) {
  /** @type {{ stageDir?: string, build: boolean, ipFence: boolean, push: boolean, remote?: string, help: boolean, allowlistEntries?: string[] }} */
  const out = {
    build: false,
    ipFence: true,
    push: false,
    help: false,
  };

  for (let i = 0; i < argv.length; i++) {
    const a = argv[i] ?? "";

    if (a === "--help" || a === "-h") {
      out.help = true;
    } else if (a === "--stage-dir") {
      out.stageDir = argv[++i];
    } else if (a === "--build") {
      out.build = true;
    } else if (a === "--ip-fence") {
      out.ipFence = true;
    } else if (a === "--no-ip-fence") {
      out.ipFence = false;
    } else if (a === "--push") {
      out.push = true;
    } else if (a === "--remote") {
      out.remote = argv[++i];
      out.push = true;
    } else if (a === "--allowlist-entry") {
      out.allowlistEntries ??= [];
      out.allowlistEntries.push(argv[++i] ?? "");
    }
  }

  return out;
}

const HELP = `export-dry-run — validate / stage the public OSS export surface

Usage:
  node public/tools/scripts/export-dry-run.mjs [options]

Options:
  --stage-dir <path>   Materialize stage-root topology (extras + public/**)
  --build              After stage, run dotnet build public/D2.Public.slnx
  --ip-fence           Scan public/** for product IP path markers (default on)
  --no-ip-fence        Skip IP path scan
  --allowlist-entry x  Validate a single allowlist entry (adversarial tests)
  --push / --remote    REFUSED — exit non-zero (never pushes)
  --help, -h           This help

Allowlist: public/** + closed extras (${CLOSED_EXTRAS.join(", ")}).
Hard denylist: secrets, .env.secrets, .env.local, private/**, docs/dev, infra, gen-dev-keys.
`;

/**
 * Main CLI entry.
 *
 * @param {string[]} argv process.argv.slice(2)
 * @param {{ repoRoot?: string }} [opts]
 * @returns {number} exit code
 */
export function runExportDryRun(argv, opts = {}) {
  const args = parseExportArgs(argv);

  if (args.help) {
    process.stdout.write(HELP);
    return 0;
  }

  // Refuse push/remote — always fail-closed
  if (args.push || args.remote !== undefined) {
    process.stderr.write(
      "export-dry-run: refusing --push / --remote (dry-run never writes remotes)\n",
    );
    return 2;
  }

  let repoRoot;

  try {
    repoRoot = opts.repoRoot ?? findMonorepoRoot();
  } catch (err) {
    process.stderr.write(`${String(err)}\n`);
    return 2;
  }

  const failures = [];

  // Adversarial allowlist entry validation mode
  if (args.allowlistEntries !== undefined) {
    for (const entry of args.allowlistEntries) {
      const v = validateAllowlistEntry(entry);

      if (!v.ok) {
        failures.push(v.reason ?? "allowlist entry failed");
      }
    }

    if (failures.length > 0) {
      for (const f of failures) process.stderr.write(`FAIL: ${f}\n`);
      return 1;
    }

    process.stdout.write("export-dry-run: allowlist entries OK\n");
    return 0;
  }

  // Default candidate set = closed extras + public tree presence
  const candidates = [...CLOSED_EXTRAS, "public"];
  const pathResult = validateCandidatePaths(repoRoot, candidates);

  if (!pathResult.ok) {
    failures.push(...pathResult.failures);
  }

  // Ensure closed extras exist at monorepo root
  for (const extra of CLOSED_EXTRAS) {
    if (!existsSync(join(repoRoot, extra))) {
      failures.push(`closed extra missing: ${extra}`);
    }
  }

  if (args.ipFence) {
    const scan = scanPublicTree(repoRoot);
    failures.push(...scan.failures);
  }

  // Fail-closed: never materialize stage when scan/allowlist already dirty
  if (args.stageDir !== undefined && failures.length > 0) {
    process.stderr.write(
      "export-dry-run: skipping --stage-dir (denylist/IP/allowlist failures already present)\n",
    );
  } else if (args.stageDir !== undefined) {
    if (args.stageDir.trim() === "") {
      failures.push("empty --stage-dir");
    } else {
      const staged = stageExportTree(repoRoot, args.stageDir);

      if (!staged.ok) {
        failures.push(...staged.failures);
      } else {
        const iso = assertStagePropsIsolation(staged.stageDir);

        if (!iso.ok) {
          failures.push(iso.reason ?? "stage props isolation failed");
        } else {
          process.stdout.write(`export-dry-run: staged → ${staged.stageDir}\n`);
          process.stdout.write(
            `export-dry-run: stage props isolation OK (${iso.resolvedProps})\n`,
          );
        }

        if (args.build && failures.length === 0) {
          process.stdout.write(
            "export-dry-run: building staged public/D2.Public.slnx …\n",
          );
          const built = buildStagedPublicSlnx(staged.stageDir);

          if (!built.ok) {
            failures.push("staged public/D2.Public.slnx build failed");
            process.stderr.write(built.output.slice(-4000));
          } else {
            process.stdout.write("export-dry-run: staged public build OK\n");
          }
        }
      }
    }
  }

  if (failures.length > 0) {
    for (const f of failures) {
      process.stderr.write(`FAIL: ${f}\n`);
    }

    process.stderr.write(
      `export-dry-run: ${String(failures.length)} failure(s)\n`,
    );
    return 1;
  }

  process.stdout.write(
    "export-dry-run: OK (public/** + closed extras; denylist clean; no push)\n",
  );
  return 0;
}

const isEntryPoint =
  process.argv[1] !== undefined &&
  fileURLToPath(import.meta.url) === resolve(process.argv[1]);

if (isEntryPoint) {
  const code = runExportDryRun(process.argv.slice(2));
  process.exit(code);
}
