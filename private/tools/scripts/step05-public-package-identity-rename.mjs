// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// TEMP one-shot bulk rename for step 05 public-package-identity.
// §13.2 scope: see docs/wip/0032-oss-public-private/05-public-package-identity/rename-ledger.md
// -----------------------------------------------------------------------
import { execSync } from "node:child_process";
import {
  existsSync,
  readdirSync,
  readFileSync,
  renameSync,
  statSync,
  writeFileSync,
} from "node:fs";
import { basename, dirname, join, relative, sep } from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = dirname(fileURLToPath(import.meta.url));
const REPO = join(__dirname, "../../..");

const SKIP_DIR_NAMES = new Set([
  "node_modules",
  "bin",
  "obj",
  ".git",
  "dist",
  "coverage",
  ".turbo",
  ".svelte-kit",
  "TestResults",
]);

// History + meta-law allowlist (do not rewrite package-id samples here).
const SKIP_PATH_PREFIXES = [
  "docs/dev/deliverables/",
  "docs/dev/rules/",
  "docs/dev/process.md",
  "docs/wip/",
  "old/",
  "AGENTS.md",
  "Claude.md",
  "CLAUDE.md",
  ".claude/",
  ".grok/",
  ".codex/",
];

const TEXT_EXTS = new Set([
  ".cs",
  ".csproj",
  ".props",
  ".targets",
  ".sln",
  ".slnx",
  ".json",
  ".ts",
  ".tsx",
  ".js",
  ".mjs",
  ".cjs",
  ".svelte",
  ".md",
  ".yml",
  ".yaml",
  ".xml",
  ".txt",
  ".Dockerfile",
  ".dockerignore",
  ".editorconfig",
  ".http",
  ".proto",
  ".tsp",
  ".graphql",
  ".cshtml",
  ".razor",
  ".ps1",
  ".sh",
  ".cmd",
  ".bat",
  ".toml",
  ".cfg",
  ".config",
]);

const TEXT_BASENAMES = new Set([
  "Dockerfile",
  "Dockerfile.dev",
  "Dockerfile.prod",
  "Makefile",
  "pnpm-workspace.yaml",
  "Directory.Build.props",
  "Directory.Build.targets",
  "Directory.Packages.props",
  "stylecop.json",
  "GlobalUsings.cs",
  "PublicAPI.Shipped.txt",
  "PublicAPI.Unshipped.txt",
]);

function norm(p) {
  return p.replace(/\\/g, "/");
}

function rel(p) {
  return norm(relative(REPO, p));
}

function shouldSkipPath(abs) {
  const r = rel(abs);
  if (r.startsWith("..")) return true;
  for (const pref of SKIP_PATH_PREFIXES) {
    if (r === pref.replace(/\/$/, "") || r.startsWith(pref)) return true;
  }
  // Never touch secrets
  if (r === "secrets" || r.startsWith("secrets/") || r.includes(".env.secrets"))
    return true;
  return false;
}

function walkFiles(dir, out = []) {
  if (!existsSync(dir)) return out;
  let entries;
  try {
    entries = readdirSync(dir, { withFileTypes: true });
  } catch {
    return out;
  }
  for (const e of entries) {
    if (SKIP_DIR_NAMES.has(e.name)) continue;
    const full = join(dir, e.name);
    if (shouldSkipPath(full)) continue;
    if (e.isDirectory()) {
      walkFiles(full, out);
    } else if (e.isFile()) {
      out.push(full);
    }
  }
  return out;
}

function isTextFile(abs) {
  const base = basename(abs);
  // Lockfiles: regenerate via pnpm install (do not bulk-edit).
  if (
    base === "pnpm-lock.yaml" ||
    base === "package-lock.json" ||
    base === "yarn.lock"
  )
    return false;
  // Baseline artifacts: seeders only after compile (§26.5 / §26.20).
  if (base === ".release-fingerprint" || base.endsWith(".api.md")) return false;
  if (base.endsWith(".min.js") || base.endsWith(".map")) return false;
  if (TEXT_BASENAMES.has(base)) return true;
  if (base.startsWith("Dockerfile")) return true;
  const i = base.lastIndexOf(".");
  if (i < 0) return false;
  const ext = base.slice(i).toLowerCase();
  return TEXT_EXTS.has(ext);
}

/**
 * Identity content map — order is load-bearing (longest / special first).
 * All CLR/PackageId rewrites use (?<![\w.]) so we never double-apply inside
 * already-rewritten `DcsvIo.D2.Private.*` tokens.
 */
function mapIdentityContent(text) {
  let s = text;

  // --- npm (KC closed first) ---
  s = s.replaceAll(
    "@dcsv-io/d2-private-key-custodian-client",
    "@dcsv-io/d2-private-key-custodian-client",
  );
  s = s.replaceAll("@dcsv-io/d2-", "@dcsv-io/d2-");

  // --- Extensions PackageIds / assemblies (before Shared strip) ---
  s = s.replace(
    /(?<![\w.])DcsvIo\.D2\.Auth\.Abstractions\.Extensions/g,
    "DcsvIo.D2.Private.Auth.Abstractions.Extensions",
  );
  s = s.replace(
    /(?<![\w.])DcsvIo\.D2\.Encryption\.Extensions/g,
    "DcsvIo.D2.Private.Encryption.Extensions",
  );
  s = s.replace(
    /(?<![\w.])DcsvIo\.D2\.I18n\.Keys\.Extensions/g,
    "DcsvIo.D2.Private.I18n.Keys.Extensions",
  );

  // --- Product hosts (KeyCustodian before Edge) ---
  s = s.replace(
    /(?<![\w.])D2\.Edge\.KeyCustodian/g,
    "DcsvIo.D2.Private.Edge.KeyCustodian",
  );
  s = s.replace(/(?<![\w.])D2\.Edge\./g, "DcsvIo.D2.Private.Edge.");
  s = s.replace(/(?<![\w.])D2\.Edge(?![\w.])/g, "DcsvIo.D2.Private.Edge");
  s = s.replace(/(?<![\w.])D2\.Audit\./g, "DcsvIo.D2.Private.Audit.");
  s = s.replace(/(?<![\w.])D2\.Audit(?![\w.])/g, "DcsvIo.D2.Private.Audit");

  // --- Open Shared strip ---
  s = s.replace(/(?<![\w.])DcsvIo\.D2\./g, "DcsvIo.D2.");
  s = s.replace(/(?<![\w.])D2\.Shared(?![\w.])/g, "DcsvIo.D2");

  // --- Private emit NS + private packages tests ---
  s = s.replace(/(?<![\w.])D2\.Private\./g, "DcsvIo.D2.Private.");
  s = s.replace(/(?<![\w.])D2\.Private(?![\w.])/g, "DcsvIo.D2.Private");

  // --- GitHub org casing ---
  s = s.replaceAll(
    "https://github.com/dcsv-io/d2-public",
    "https://github.com/dcsv-io/d2-public",
  );
  s = s.replaceAll("github.com/dcsv-io/", "github.com/dcsv-io/");

  return s;
}

function mapCsprojBasename(name) {
  // name without .csproj
  let n = name;
  if (n === "DcsvIo.D2.Private.Auth.Abstractions.Extensions")
    return "DcsvIo.D2.Private.Auth.Abstractions.Extensions";
  if (n === "DcsvIo.D2.Private.Encryption.Extensions")
    return "DcsvIo.D2.Private.Encryption.Extensions";
  if (n === "DcsvIo.D2.Private.I18n.Keys.Extensions")
    return "DcsvIo.D2.Private.I18n.Keys.Extensions";
  if (n.startsWith("DcsvIo.D2.Private.Edge.KeyCustodian"))
    return (
      "DcsvIo.D2.Private.Edge.KeyCustodian" +
      n.slice("DcsvIo.D2.Private.Edge.KeyCustodian".length)
    );
  if (n.startsWith("DcsvIo.D2.Private.Edge."))
    return (
      "DcsvIo.D2.Private.Edge." + n.slice("DcsvIo.D2.Private.Edge.".length)
    );
  if (n.startsWith("DcsvIo.D2.Private.Audit."))
    return (
      "DcsvIo.D2.Private.Audit." + n.slice("DcsvIo.D2.Private.Audit.".length)
    );
  if (n.startsWith("DcsvIo.D2."))
    return "DcsvIo.D2." + n.slice("DcsvIo.D2.".length);
  if (n.startsWith("DcsvIo.D2.Private."))
    return "DcsvIo.D2.Private." + n.slice("DcsvIo.D2.Private.".length);
  return n;
}

function gitMv(fromAbs, toAbs) {
  const fromRel = rel(fromAbs);
  const toRel = rel(toAbs);
  if (fromRel === toRel) return;
  if (!existsSync(fromAbs)) {
    console.warn("SKIP missing:", fromRel);
    return;
  }
  if (existsSync(toAbs)) {
    console.warn("SKIP target exists:", toRel);
    return;
  }
  try {
    execSync(`git mv "${fromRel}" "${toRel}"`, {
      cwd: REPO,
      stdio: "pipe",
      shell: true,
    });
    console.log("git mv", fromRel, "->", toRel);
  } catch (e) {
    // Fallback filesystem rename if not tracked
    renameSync(fromAbs, toAbs);
    console.log("fs rename", fromRel, "->", toRel, "(not git tracked?)");
  }
}

function renameAllCsproj() {
  const roots = [
    join(REPO, "public/packages/dotnet"),
    join(REPO, "private/packages/dotnet"),
    join(REPO, "private/services"),
  ];
  const files = [];
  for (const r of roots) walkFiles(r, files);
  const csprojs = files.filter((f) => f.endsWith(".csproj"));
  let n = 0;
  for (const abs of csprojs) {
    const base = basename(abs, ".csproj");
    const next = mapCsprojBasename(base);
    if (next === base) continue;
    const dest = join(dirname(abs), next + ".csproj");
    gitMv(abs, dest);
    n++;
  }
  console.log(`Renamed ${n} csproj files`);
}

function rewriteContentFiles() {
  const roots = [
    join(REPO, "public"),
    join(REPO, "private"),
    join(REPO, "docs"),
    join(REPO, "infra"),
    join(REPO, "CONTRIBUTING.md"),
    join(REPO, "D2.slnx"),
    join(REPO, "D2.sln.DotSettings"),
    join(REPO, "Directory.Build.props"),
    join(REPO, "Directory.Build.targets"),
    join(REPO, "Directory.Packages.props"),
    join(REPO, "global.json"),
    join(REPO, "package.json"),
    join(REPO, "pnpm-workspace.yaml"),
    join(REPO, "stylecop.json"),
  ];

  const files = [];
  for (const r of roots) {
    if (!existsSync(r)) continue;
    const st = statSync(r);
    if (st.isFile()) {
      if (!shouldSkipPath(r)) files.push(r);
    } else {
      walkFiles(r, files);
    }
  }

  let changed = 0;
  let scanned = 0;
  for (const abs of files) {
    if (!isTextFile(abs)) continue;
    // Do not rewrite web SEO product brand tests' host strings as package ids —
    // only scrub d2-worx.dev under public/contracts for schema $id.
    scanned++;
    let text;
    try {
      text = readFileSync(abs, "utf8");
    } catch {
      continue;
    }
    // Skip binary-looking
    if (text.includes("\u0000")) continue;

    let next = mapIdentityContent(text);

    const r = rel(abs);
    // Schema $id scrub only under public/contracts (and matching schema copies)
    if (
      r.startsWith("public/contracts/") ||
      (r.includes("/geo/") && r.endsWith(".schema.json"))
    ) {
      next = next
        .replaceAll("https://d2-worx.dev/", "https://schemas.d2.dcsv.io/")
        .replaceAll("http://d2-worx.dev/", "https://schemas.d2.dcsv.io/");
    }

    // Preserve d2-sveltekit package name if accidental rewrite happened
    // (it does not match @dcsv-io/d2- so should be fine)

    if (next !== text) {
      writeFileSync(abs, next, "utf8");
      changed++;
    }
  }
  console.log(`Content rewrite: scanned ${scanned}, changed ${changed}`);
}

function main() {
  const phase = process.argv[2] ?? "all";
  console.log("REPO", REPO, "phase", phase);

  if (phase === "csproj" || phase === "all") {
    console.log("=== Phase: csproj git mv ===");
    renameAllCsproj();
  }
  if (phase === "content" || phase === "all") {
    console.log("=== Phase: content rewrite ===");
    rewriteContentFiles();
  }
  if (phase === "content-only") {
    rewriteContentFiles();
  }
}

main();
