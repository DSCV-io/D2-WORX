// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import {
  AREA_IDS,
  PRIVATE_ADRS,
  PRIVATE_ONLY_CONTRACTS,
  PRIVATE_SCRIPT_LEAVES,
  PRIVATE_TOOLS,
  PUBLIC_SCRIPT_LEAVES,
  SPLIT_CONTRACTS,
  diffAreaSets,
  isExcludedSecretsPath,
  mapBackupToCurrent,
  normalizeRelative,
} from "./lib/area-scan.mjs";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const REPO_ROOT = path.resolve(__dirname, "../../..");

const args = new Set(process.argv.slice(2));
const reportOnly = args.has("--report-only");
const asJson = args.has("--json");
const asMarkdown = args.has("--markdown") || !asJson;

/**
 * @param {string} root
 * @param {string} rel
 * @returns {boolean}
 */
function existsUnder(root, rel) {
  return fs.existsSync(path.join(root, ...normalizeRelative(rel).split("/")));
}

/**
 * List immediate child directory names (non-hidden).
 * @param {string} abs
 * @returns {string[]}
 */
function listDirs(abs) {
  if (!fs.existsSync(abs)) {
    return [];
  }
  return fs
    .readdirSync(abs, { withFileTypes: true })
    .filter((d) => d.isDirectory() && !d.name.startsWith("."))
    .map((d) => d.name)
    .sort((a, b) => a.localeCompare(b));
}

/**
 * List files (non-recursive) basenames.
 * @param {string} abs
 * @returns {string[]}
 */
function listFiles(abs) {
  if (!fs.existsSync(abs)) {
    return [];
  }
  return fs
    .readdirSync(abs, { withFileTypes: true })
    .filter((d) => d.isFile())
    .map((d) => d.name)
    .sort((a, b) => a.localeCompare(b));
}

/**
 * Recursive list of *.md relative paths under dir.
 * @param {string} abs
 * @param {string} prefix
 * @returns {string[]}
 */
function listMdRelative(abs, prefix = "") {
  if (!fs.existsSync(abs)) {
    return [];
  }
  /** @type {string[]} */
  const out = [];
  for (const ent of fs.readdirSync(abs, { withFileTypes: true })) {
    if (ent.name === "node_modules" || ent.name.startsWith(".")) {
      continue;
    }
    const rel = prefix ? `${prefix}/${ent.name}` : ent.name;
    const full = path.join(abs, ent.name);
    if (ent.isDirectory()) {
      out.push(...listMdRelative(full, rel));
    } else if (ent.isFile() && ent.name.endsWith(".md")) {
      out.push(rel.replace(/\\/g, "/"));
    }
  }
  return out.sort((a, b) => a.localeCompare(b));
}

/**
 * Collect *.csproj relative paths under cluster (skip bin/obj/node_modules).
 * @param {string} absRoot
 * @param {string} relPrefix backup-relative prefix
 * @returns {string[]}
 */
function listCsproj(absRoot, relPrefix) {
  /** @type {string[]} */
  const out = [];
  /**
   * @param {string} abs
   * @param {string} rel
   */
  function walk(abs, rel) {
    if (!fs.existsSync(abs)) {
      return;
    }
    for (const ent of fs.readdirSync(abs, { withFileTypes: true })) {
      if (
        ent.name === "node_modules" ||
        ent.name === "bin" ||
        ent.name === "obj" ||
        ent.name === "old" ||
        ent.name.startsWith(".")
      ) {
        continue;
      }
      const childAbs = path.join(abs, ent.name);
      const childRel = rel ? `${rel}/${ent.name}` : ent.name;
      if (ent.isDirectory()) {
        walk(childAbs, childRel);
      } else if (ent.isFile() && ent.name.endsWith(".csproj")) {
        out.push(normalizeRelative(`${relPrefix}/${childRel}`));
      }
    }
  }
  walk(absRoot, "");
  return out.sort((a, b) => a.localeCompare(b));
}

/**
 * Build currentPresent set of paths that exist under monorepo.
 * @param {string[]} candidates
 * @returns {Set<string>}
 */
function buildPresent(candidates) {
  const present = new Set();
  for (const c of candidates) {
    if (existsUnder(REPO_ROOT, c)) {
      present.add(normalizeRelative(c));
    }
  }
  return present;
}

/**
 * Scan all areas against backup + current.
 * @param {string} backupRoot
 */
function scanAll(backupRoot) {
  /** @type {Array<{ area: string, identity: string, backup_path: string, current_path: string, disposition: string, evidence: string, notes?: string }>} */
  const ledgerRows = [];

  /**
   * @param {string} area
   * @param {string[]} backupIds backup-relative identities
   * @param {string} [evidence]
   * @param {string[]} [currentOnly]
   */
  function runArea(
    area,
    backupIds,
    evidence = "area-scan set-diff",
    currentOnly = [],
  ) {
    // expand mapped paths for presence check
    /** @type {string[]} */
    const candidates = [];
    for (const id of backupIds) {
      try {
        const mapped = mapBackupToCurrent(id);
        for (const p of mapped.currentPaths ?? []) {
          candidates.push(p);
        }
      } catch {
        // presence builder ignores invalid
      }
    }
    for (const add of currentOnly) {
      candidates.push(add);
    }

    // also add known dual homes for split contracts
    for (const name of Object.keys(SPLIT_CONTRACTS)) {
      candidates.push(`public/contracts/${name}`, `private/contracts/${name}`);
    }
    for (const name of PRIVATE_ONLY_CONTRACTS) {
      candidates.push(`private/contracts/${name}`);
    }

    const present = buildPresent(candidates);
    // also mark any current path that exists for identities we map
    for (const id of backupIds) {
      try {
        const mapped = mapBackupToCurrent(id);
        for (const p of mapped.currentPaths ?? []) {
          if (existsUnder(REPO_ROOT, p)) {
            present.add(normalizeRelative(p));
          }
        }
      } catch {
        /* skip */
      }
    }

    const { rows } = diffAreaSets({
      backupIdentities: backupIds,
      currentPresent: present,
      currentOnlyIdentities: currentOnly,
      presenceMode: "path",
    });

    for (const row of rows) {
      ledgerRows.push({
        area,
        identity: row.identity,
        backup_path: row.identity,
        current_path: (row.currentPaths ?? []).join(" + ") || "(none)",
        disposition: row.disposition,
        evidence,
        notes: row.notes,
      });
    }
  }

  // --- packages-dotnet top-level dirs ---
  {
    const dirs = listDirs(path.join(backupRoot, "server", "shared", "dotnet"));
    runArea(
      "packages-dotnet",
      dirs.map((d) => `server/shared/dotnet/${d}`),
      "packages-dotnet top-level dir set-diff",
    );
    const csprojs = listCsproj(
      path.join(backupRoot, "server", "shared", "dotnet"),
      "server/shared/dotnet",
    );
    runArea("packages-dotnet", csprojs, "packages-dotnet csproj set-diff");
  }

  // --- packages-ts ---
  {
    const dirs = listDirs(
      path.join(backupRoot, "server", "shared", "typescript"),
    );
    runArea(
      "packages-ts",
      dirs.map((d) => `server/shared/typescript/${d}`),
      "packages-ts top-level dir set-diff",
    );
  }

  // --- services ---
  {
    const dirs = listDirs(path.join(backupRoot, "server", "services"));
    runArea(
      "services",
      dirs.map((d) => `server/services/${d}`),
      "services dir set-diff",
    );
    const csprojs = listCsproj(
      path.join(backupRoot, "server", "services"),
      "server/services",
    );
    runArea("services", csprojs, "services csproj set-diff");
  }

  // --- web ---
  {
    const webRoot = path.join(backupRoot, "server", "web");
    if (fs.existsSync(webRoot)) {
      runArea("web", ["server/web"], "web root marker");
      if (fs.existsSync(path.join(webRoot, "package.json"))) {
        runArea("web", ["server/web/package.json"], "web package.json");
      }
    }
  }

  // --- contracts ---
  {
    const dirs = listDirs(path.join(backupRoot, "contracts"));
    runArea(
      "contracts",
      dirs.map((d) => `contracts/${d}`),
      "contracts top-level folder set-diff",
    );
  }

  // --- tools top-level ---
  {
    const dirs = listDirs(path.join(backupRoot, "tools"));
    const ids = dirs.filter((d) => d !== "scripts").map((d) => `tools/${d}`);
    // scripts dual home as special identity
    ids.push("tools/scripts");
    runArea("tools", ids, "tools top-level set-diff");
  }

  // --- scripts-leaves ---
  {
    const leaves = listFiles(path.join(backupRoot, "tools", "scripts"));
    runArea(
      "scripts-leaves",
      leaves.map((f) => `tools/scripts/${f}`),
      "scripts leaf file set-diff",
    );
    // lib leaves
    const libLeaves = listFiles(
      path.join(backupRoot, "tools", "scripts", "lib"),
    );
    runArea(
      "scripts-leaves",
      libLeaves.map((f) => `tools/scripts/lib/${f}`),
      "scripts/lib leaf set-diff",
    );
    const testLeaves = listFiles(
      path.join(backupRoot, "tools", "scripts", "tests"),
    );
    runArea(
      "scripts-leaves",
      testLeaves.map((f) => `tools/scripts/tests/${f}`),
      "scripts/tests leaf set-diff",
    );
  }

  // --- docs-adrs ---
  {
    const files = listFiles(path.join(backupRoot, "docs", "adrs")).filter((f) =>
      f.endsWith(".md"),
    );
    runArea(
      "docs-adrs",
      files.map((f) => `docs/adrs/${f}`),
      "ADR filename set-diff",
    );
  }

  // --- docs-v2 ---
  {
    const mds = listMdRelative(path.join(backupRoot, "docs", "v2"));
    runArea(
      "docs-v2",
      mds.map((f) => `docs/v2/${f}`),
      "docs/v2 md set-diff",
    );
  }

  // --- docs-keep-root ---
  {
    const keep = [
      "docs/COMMANDS.md",
      "docs/PATTERNS.md",
      "docs/TESTS.md",
      "docs/PARITY.md",
      "docs/SRC_GEN.md",
      "docs/TIMESTAMPS.md",
      "docs/dev",
    ];
    // path-existence: map is identity → same path; backup_path for keep is monorepo-relative
    // For keep files that live at monorepo root both backup and current, use unchanged_home check
    for (const k of keep) {
      const backupHas =
        k === "docs/dev"
          ? fs.existsSync(path.join(backupRoot, "docs", "dev"))
          : fs.existsSync(path.join(backupRoot, ...k.split("/")));
      const currentHas =
        k === "docs/dev"
          ? fs.existsSync(path.join(REPO_ROOT, "docs", "dev"))
          : existsUnder(REPO_ROOT, k);
      if (!backupHas) {
        continue;
      }
      ledgerRows.push({
        area: "docs-keep-root",
        identity: k,
        backup_path: k,
        current_path: k,
        disposition: currentHas ? "unchanged_home" : "MISSING",
        evidence: "docs-keep-root path existence",
        notes: currentHas ? undefined : "KEEP doc missing from monorepo",
      });
    }
  }

  // --- ci ---
  {
    const wfs = listFiles(path.join(backupRoot, ".github", "workflows"));
    for (const wf of wfs) {
      const id = `.github/workflows/${wf}`;
      const currentHas = existsUnder(REPO_ROOT, id);
      ledgerRows.push({
        area: "ci",
        identity: wf,
        backup_path: id,
        current_path: id,
        disposition: currentHas ? "unchanged_home" : "MISSING",
        evidence: "workflow filename set-diff",
      });
    }
  }

  // --- tests (csproj under backup excluding node_modules/old) ---
  {
    const testIds = [
      "server/shared/dotnet/tests/DcsvIo.D2.Tests.csproj",
      "server/services/edge/tests/DcsvIo.D2.Private.Edge.Tests.csproj",
      "server/services/audit/tests/DcsvIo.D2.Private.Audit.Tests.csproj",
    ];
    runArea("tests", testIds, "test csproj mapped homes", [
      "private/packages/dotnet/tests/DcsvIo.D2.Private.Packages.Tests.csproj",
    ]);
  }

  // --- infra top-level ---
  {
    const dirs = listDirs(path.join(backupRoot, "infra"));
    for (const d of dirs) {
      const id = `infra/${d}`;
      const currentHas = existsUnder(REPO_ROOT, id);
      ledgerRows.push({
        area: "infra",
        identity: d,
        backup_path: id,
        current_path: id,
        disposition: currentHas ? "unchanged_home" : "MISSING",
        evidence: "infra top-level dir set-diff",
      });
    }
  }

  // --- msbuild-root ---
  {
    const critical = [
      {
        backup: "server/Directory.Build.props",
        current: "Directory.Build.props",
      },
      {
        backup: "server/Directory.Packages.props",
        current: "Directory.Packages.props",
      },
      { backup: "server/D2.slnx", current: "D2.slnx" },
    ];
    for (const c of critical) {
      const backupHas = fs.existsSync(
        path.join(backupRoot, ...c.backup.split("/")),
      );
      if (!backupHas) {
        continue;
      }
      const map = mapBackupToCurrent(c.backup);
      const paths = map.currentPaths ?? [c.current];
      const allPresent = paths.every((p) => existsUnder(REPO_ROOT, p));
      ledgerRows.push({
        area: "msbuild-root",
        identity: path.posix.basename(c.backup),
        backup_path: c.backup,
        current_path: paths.join(" + "),
        disposition: allPresent
          ? paths.length > 1
            ? "split"
            : "moved"
          : "MISSING",
        evidence: "msbuild critical file map",
      });
    }
    // public solution is post-reorg add relative to single server/D2.slnx
    if (existsUnder(REPO_ROOT, "public/D2.Public.slnx")) {
      ledgerRows.push({
        area: "msbuild-root",
        identity: "D2.Public.slnx",
        backup_path: "(none — post-reorg)",
        current_path: "public/D2.Public.slnx",
        disposition: "post_reorg_add",
        evidence: "L18 dual solution",
      });
    }
  }

  // --- d2-version ---
  {
    runArea("d2-version", ["server/d2-version"], "d2-version dir map");
  }

  // intentional structural drops (pre-seed catalog rows as ledger markers)
  for (const drop of [
    {
      identity: "server/** product SoT",
      backup_path: "server/",
      reason: "dissolved into public/private (plan:L1)",
    },
    {
      identity: "root contracts/ SoT",
      backup_path: "contracts/",
      reason: "dual public/private contracts (plan:L13)",
    },
    {
      identity: "root tools/ SoT",
      backup_path: "tools/",
      reason: "dual public/private tools (plan:L11)",
    },
  ]) {
    ledgerRows.push({
      area: "msbuild-root",
      identity: drop.identity,
      backup_path: drop.backup_path,
      current_path: "(dissolved)",
      disposition: "intentional_drop",
      evidence: "pre-seed intentional-drop catalog",
      notes: drop.reason,
    });
  }

  // secrets exclusion self-check
  if (isExcludedSecretsPath("secrets/x") === false) {
    throw new Error("secrets exclusion broken");
  }

  return ledgerRows;
}

function main() {
  const backupRoot =
    process.env.D2_BACKUP_ROOT || process.env.BACKUP_ROOT || "";

  if (!backupRoot || !String(backupRoot).trim()) {
    console.error(
      "area-scan: D2_BACKUP_ROOT is required (absolute path to READ-ONLY backup).",
    );
    process.exitCode = 2;
    return;
  }

  if (!fs.existsSync(backupRoot)) {
    console.error(`area-scan: backup root does not exist: ${backupRoot}`);
    process.exitCode = 2;
    return;
  }

  // refuse to treat backup as writable — we never open write handles
  const rows = scanAll(backupRoot);
  const missing = rows.filter((r) => r.disposition === "MISSING");

  if (asJson) {
    console.log(
      JSON.stringify(
        {
          backupRoot,
          repoRoot: REPO_ROOT,
          areaIds: AREA_IDS,
          rowCount: rows.length,
          missingCount: missing.length,
          rows,
        },
        null,
        2,
      ),
    );
  }

  if (asMarkdown) {
    console.log(`# area-scan report`);
    console.log(``);
    console.log(`- backup: \`${backupRoot}\``);
    console.log(`- monorepo: \`${REPO_ROOT}\``);
    console.log(`- rows: ${rows.length}`);
    console.log(`- MISSING: ${missing.length}`);
    console.log(``);
    console.log(
      `| area | identity | backup_path | current_path | disposition | evidence | notes |`,
    );
    console.log(`| --- | --- | --- | --- | --- | --- | --- |`);
    for (const r of rows) {
      const notes = (r.notes ?? "").replace(/\|/g, "\\|");
      console.log(
        `| ${r.area} | ${r.identity} | ${r.backup_path} | ${r.current_path} | ${r.disposition} | ${r.evidence} | ${notes} |`,
      );
    }
    if (missing.length > 0) {
      console.log(``);
      console.log(`## MISSING (${missing.length})`);
      for (const m of missing) {
        console.log(`- **${m.area}** \`${m.identity}\` → ${m.notes ?? ""}`);
      }
    }
  }

  // reference private tool/script constants so pure exports stay used by CLI path maps
  void PRIVATE_ADRS;
  void PRIVATE_TOOLS;
  void PRIVATE_SCRIPT_LEAVES;
  void PUBLIC_SCRIPT_LEAVES;

  if (missing.length > 0 && !reportOnly) {
    console.error(
      `area-scan: ${missing.length} unresolved MISSING row(s). Use --report-only to capture without fail.`,
    );
    process.exitCode = 1;
    return;
  }

  process.exitCode = 0;
}

main();
