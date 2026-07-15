// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------
/**
 * Roots that scanners must never open (secrets / env secret files).
 * @type {readonly string[]}
 */
export const EXCLUDED_ROOTS = Object.freeze([
  "secrets",
  ".env.secrets",
  "secrets/",
  ".env.secrets/",
]);

/**
 * Dual-home contract catalogs (both public + private homes required for `split`).
 * @type {Readonly<Record<string, true>>}
 */
export const SPLIT_CONTRACTS = Object.freeze({
  "auth-scopes": true,
  "auth-audiences": true,
  "encryption-domains": true,
  messages: true,
});

/**
 * Private-only contract top-level folders (moved → private/contracts only).
 * @type {readonly string[]}
 */
export const PRIVATE_ONLY_CONTRACTS = Object.freeze([
  "keycustodian-error-codes",
  "advisory-locks",
]);

/**
 * Private ADR basenames (excl README).
 * @type {readonly string[]}
 */
export const PRIVATE_ADRS = Object.freeze([
  "0016-keycustodian-lifecycle-store.md",
  "0023-mtls-workload-identity.md",
]);

/**
 * Private top-level tool package dirs.
 * @type {readonly string[]}
 */
export const PRIVATE_TOOLS = Object.freeze(["typespec-spike"]);

/**
 * Script leaf basenames that map to private/tools/scripts/.
 * @type {readonly string[]}
 */
export const PRIVATE_SCRIPT_LEAVES = Object.freeze([
  "gen-dev-keys.sh",
  "regen-typespec-emitters.mjs",
  "audit-lint.sh",
  "count-inspectcode-findings.sh",
  "run-mtls-proof.sh",
  "seed-package-metadata.mjs",
]);

/**
 * Script leaf basenames that map to public/tools/scripts/ (top-level only).
 * @type {readonly string[]}
 */
export const PUBLIC_SCRIPT_LEAVES = Object.freeze([
  "assemble-libs-bundle.mjs",
  "seed-publicapi-baselines.mjs",
  "seed-apiextractor-baselines.mjs",
  "check-publicapi-shipped.mjs",
]);

/**
 * Script leaf basenames intentionally not mirrored as product SoT.
 * @type {readonly string[]}
 */
export const INTENTIONAL_DROP_SCRIPT_LEAVES = Object.freeze([".gitkeep"]);

/**
 * Area registry IDs (must match plan T4.2 area set).
 * @type {readonly string[]}
 */
export const AREA_IDS = Object.freeze([
  "packages-dotnet",
  "packages-ts",
  "services",
  "web",
  "contracts",
  "tools",
  "docs-adrs",
  "docs-v2",
  "docs-keep-root",
  "ci",
  "tests",
  "infra",
  "msbuild-root",
  "d2-version",
  "scripts-leaves",
]);

/**
 * @typedef {"moved"|"split"|"intentional_drop"|"post_reorg_add"|"unchanged_home"|"MISSING"|"unmapped"} Disposition
 */

/**
 * @typedef {object} PathMapResult
 * @property {Disposition|"mapped"} kind
 * @property {string[]} [currentPaths] relative to monorepo root
 * @property {string} [reason]
 */

/**
 * Normalize a monorepo-relative path to forward-slash form without leading slash.
 * @param {string} relativePath
 * @returns {string}
 */
export function normalizeRelative(relativePath) {
  return String(relativePath)
    .replace(/\\/g, "/")
    .replace(/^\.?\//, "")
    .replace(/\/+$/, "");
}

/**
 * Map a backup-relative path identity to expected current path(s).
 * Throws on null/empty/whitespace-only input.
 *
 * @param {string} relativePath backup-relative path
 * @returns {PathMapResult}
 */
export function mapBackupToCurrent(relativePath) {
  if (relativePath == null || String(relativePath).trim() === "") {
    throw new TypeError(
      "relative path is required (null/empty/whitespace rejected)",
    );
  }

  const p = normalizeRelative(relativePath);

  // Keep mapBackupToCurrent aligned with isExcludedSecretsPath (secrets/* and .env.secrets/*).
  if (isExcludedSecretsPath(p)) {
    return {
      kind: "intentional_drop",
      currentPaths: [],
      reason: "excluded secrets root — never scan",
    };
  }

  // server/shared/dotnet/<X>
  let m = /^server\/shared\/dotnet\/([^/]+)(?:\/(.*))?$/.exec(p);
  if (m) {
    const rest = m[2] ? `/${m[2]}` : "";
    return {
      kind: "mapped",
      currentPaths: [`public/packages/dotnet/${m[1]}${rest}`],
    };
  }

  // server/shared/typescript/<X>
  m = /^server\/shared\/typescript\/([^/]+)(?:\/(.*))?$/.exec(p);
  if (m) {
    const rest = m[2] ? `/${m[2]}` : "";
    return {
      kind: "mapped",
      currentPaths: [`public/packages/typescript/${m[1]}${rest}`],
    };
  }

  // server/services/<S>
  m = /^server\/services\/([^/]+)(?:\/(.*))?$/.exec(p);
  if (m) {
    const rest = m[2] ? `/${m[2]}` : "";
    return {
      kind: "mapped",
      currentPaths: [`private/services/${m[1]}${rest}`],
    };
  }

  // server/web
  m = /^server\/web(?:\/(.*))?$/.exec(p);
  if (m) {
    const rest = m[1] ? `/${m[1]}` : "";
    return {
      kind: "mapped",
      currentPaths: [`private/services/web${rest}`],
    };
  }

  // server/d2-version
  m = /^server\/d2-version(?:\/(.*))?$/.exec(p);
  if (m) {
    const rest = m[1] ? `/${m[1]}` : "";
    return {
      kind: "mapped",
      currentPaths: [`private/tools/d2-version${rest}`],
    };
  }

  // server/D2.slnx → dual solutions (intentional split of single solution)
  if (p === "server/D2.slnx") {
    return {
      kind: "mapped",
      currentPaths: ["D2.slnx", "public/D2.Public.slnx"],
    };
  }

  // server/Directory.* → monorepo root
  m = /^server\/(Directory\..+)$/.exec(p);
  if (m) {
    return { kind: "mapped", currentPaths: [m[1]] };
  }

  // contracts/<C>
  m = /^contracts\/([^/]+)(?:\/(.*))?$/.exec(p);
  if (m) {
    const name = m[1];
    const rest = m[2] ? `/${m[2]}` : "";
    if (SPLIT_CONTRACTS[name]) {
      return {
        kind: "mapped",
        currentPaths: [
          `public/contracts/${name}${rest}`,
          `private/contracts/${name}${rest}`,
        ],
      };
    }
    if (PRIVATE_ONLY_CONTRACTS.includes(name)) {
      return {
        kind: "mapped",
        currentPaths: [`private/contracts/${name}${rest}`],
      };
    }
    // typespec mixed: top-level folder may exist in both; treat top-level as public+private if split-ish
    if (name === "typespec") {
      return {
        kind: "mapped",
        currentPaths: [
          `public/contracts/typespec${rest}`,
          `private/contracts/typespec${rest}`,
        ],
      };
    }
    return {
      kind: "mapped",
      currentPaths: [`public/contracts/${name}${rest}`],
    };
  }

  // tools/scripts/<leaf> (file)
  m = /^tools\/scripts\/([^/]+)$/.exec(p);
  if (m) {
    const leaf = m[1];
    if (INTENTIONAL_DROP_SCRIPT_LEAVES.includes(leaf)) {
      return {
        kind: "intentional_drop",
        currentPaths: [],
        reason: "empty scripts marker — not product SoT",
      };
    }
    if (PRIVATE_SCRIPT_LEAVES.includes(leaf)) {
      return {
        kind: "mapped",
        currentPaths: [`private/tools/scripts/${leaf}`],
      };
    }
    if (PUBLIC_SCRIPT_LEAVES.includes(leaf)) {
      return { kind: "mapped", currentPaths: [`public/tools/scripts/${leaf}`] };
    }
    // unknown leaf — still map attempt to both for scan visibility
    return {
      kind: "mapped",
      currentPaths: [
        `public/tools/scripts/${leaf}`,
        `private/tools/scripts/${leaf}`,
      ],
    };
  }

  // tools/scripts/lib/* → public
  m = /^tools\/scripts\/lib\/(.+)$/.exec(p);
  if (m) {
    return {
      kind: "mapped",
      currentPaths: [`public/tools/scripts/lib/${m[1]}`],
    };
  }

  // tools/scripts/tests/* — public empty-guards vs private harness
  m = /^tools\/scripts\/tests\/(.+)$/.exec(p);
  if (m) {
    const leaf = m[1];
    if (leaf.includes("empty-guard")) {
      return {
        kind: "mapped",
        currentPaths: [`public/tools/scripts/tests/${leaf}`],
      };
    }
    return {
      kind: "mapped",
      currentPaths: [`private/tools/scripts/tests/${leaf}`],
    };
  }

  // tools/<T> package dir
  m = /^tools\/([^/]+)(?:\/(.*))?$/.exec(p);
  if (m) {
    const name = m[1];
    if (name === "scripts") {
      // tree root — dual scripts homes
      return {
        kind: "mapped",
        currentPaths: ["public/tools/scripts", "private/tools/scripts"],
      };
    }
    if (name === "README.md" || name === "readme.md") {
      return {
        kind: "intentional_drop",
        currentPaths: [],
        reason: "root tools README retired as product SoT (L11)",
      };
    }
    const rest = m[2] ? `/${m[2]}` : "";
    if (PRIVATE_TOOLS.includes(name)) {
      return {
        kind: "mapped",
        currentPaths: [`private/tools/${name}${rest}`],
      };
    }
    return {
      kind: "mapped",
      currentPaths: [`public/tools/${name}${rest}`],
    };
  }

  // docs/adrs/<file>
  m = /^docs\/adrs\/([^/]+)$/.exec(p);
  if (m) {
    const file = m[1];
    if (file === "README.md") {
      return {
        kind: "mapped",
        currentPaths: [
          "public/docs/adrs/README.md",
          "private/docs/adrs/README.md",
        ],
      };
    }
    if (PRIVATE_ADRS.includes(file)) {
      return { kind: "mapped", currentPaths: [`private/docs/adrs/${file}`] };
    }
    return { kind: "mapped", currentPaths: [`public/docs/adrs/${file}`] };
  }

  // docs/v2/**
  m = /^docs\/v2(?:\/(.*))?$/.exec(p);
  if (m) {
    const rest = m[1] ? `/${m[1]}` : "";
    return { kind: "mapped", currentPaths: [`private/docs/v2${rest}`] };
  }

  // KEEP root docs / infra / ci — same relative path
  if (
    p.startsWith("docs/dev/") ||
    p === "docs/COMMANDS.md" ||
    p === "docs/PATTERNS.md" ||
    p === "docs/TESTS.md" ||
    p === "docs/PARITY.md" ||
    p === "docs/SRC_GEN.md" ||
    p === "docs/TIMESTAMPS.md" ||
    p.startsWith("infra/") ||
    p === "infra" ||
    p.startsWith(".github/workflows/") ||
    p === "Directory.Build.props" ||
    p === "Directory.Packages.props" ||
    p === "D2.slnx" ||
    p === "pnpm-workspace.yaml" ||
    p === "package.json" ||
    p === "AGENTS.md" ||
    p === "CONTRIBUTING.md"
  ) {
    return { kind: "mapped", currentPaths: [p] };
  }

  // server/** residual product tree (dissolved) — intentional drop of SoT tree
  if (p === "server" || p.startsWith("server/")) {
    return {
      kind: "intentional_drop",
      currentPaths: [],
      reason: "server product tree dissolved into public/private (plan:L1)",
    };
  }

  // root contracts/ / tools/ as SoT markers
  if (p === "contracts" || p === "tools") {
    return {
      kind: "intentional_drop",
      currentPaths: [],
      reason: "root cluster retired as product SoT (dual homes)",
    };
  }

  return {
    kind: "unmapped",
    currentPaths: [],
    reason: "unknown backup prefix",
  };
}

/**
 * @typedef {object} DiffRow
 * @property {string} identity
 * @property {Disposition} disposition
 * @property {string[]} currentPaths
 * @property {string} [notes]
 */

/**
 * Pure set-diff for one area.
 *
 * @param {object} args
 * @param {string[]} args.backupIdentities identity keys present in backup
 * @param {Set<string>|string[]} args.currentPresent set of current relative paths that exist (or identity keys for simple set compare)
 * @param {(id: string) => PathMapResult} [args.mapFn] defaults to mapBackupToCurrent
 * @param {string[]} [args.currentOnlyIdentities] current-only identities → post_reorg_add
 * @param {"path"|"identity"} [args.presenceMode]
 *   - `path` (default): currentPresent holds monorepo-relative paths; map produces paths
 *   - `identity`: currentPresent holds identity keys already (after mapping) — for simple fixtures
 * @returns {{ rows: DiffRow[], missingCount: number }}
 */
export function diffAreaSets({
  backupIdentities,
  currentPresent,
  mapFn = mapBackupToCurrent,
  currentOnlyIdentities = [],
  presenceMode = "path",
}) {
  const present =
    currentPresent instanceof Set
      ? currentPresent
      : new Set(Array.from(currentPresent));

  /** @type {DiffRow[]} */
  const rows = [];
  let missingCount = 0;

  for (const rawId of backupIdentities) {
    const identity = normalizeRelative(rawId);
    let mapResult;
    try {
      mapResult = mapFn(identity);
    } catch (err) {
      rows.push({
        identity,
        disposition: "MISSING",
        currentPaths: [],
        notes: err instanceof Error ? err.message : String(err),
      });
      missingCount += 1;
      continue;
    }

    if (mapResult.kind === "intentional_drop") {
      rows.push({
        identity,
        disposition: "intentional_drop",
        currentPaths: [],
        notes: mapResult.reason,
      });
      continue;
    }

    if (mapResult.kind === "unmapped") {
      rows.push({
        identity,
        disposition: "MISSING",
        currentPaths: [],
        notes: mapResult.reason ?? "unmapped backup prefix",
      });
      missingCount += 1;
      continue;
    }

    const paths = mapResult.currentPaths ?? [];

    if (presenceMode === "identity") {
      const ok = present.has(identity);
      if (ok) {
        rows.push({
          identity,
          disposition: "moved",
          currentPaths: paths,
        });
      } else {
        rows.push({
          identity,
          disposition: "MISSING",
          currentPaths: paths,
          notes: "unresolved backup identity",
        });
        missingCount += 1;
      }
      continue;
    }

    // path mode: for multi-path (split), ALL homes must exist
    if (paths.length === 0) {
      rows.push({
        identity,
        disposition: "MISSING",
        currentPaths: [],
        notes: "mapped to empty path list",
      });
      missingCount += 1;
      continue;
    }

    const missingHomes = paths.filter(
      (hp) => !present.has(normalizeRelative(hp)),
    );

    if (missingHomes.length === 0) {
      const disposition =
        paths.length > 1
          ? "split"
          : identity === paths[0]
            ? "unchanged_home"
            : "moved";
      rows.push({
        identity,
        disposition,
        currentPaths: paths,
      });
    } else if (paths.length > 1 && missingHomes.length < paths.length) {
      // incomplete split — NOT moved
      rows.push({
        identity,
        disposition: "MISSING",
        currentPaths: paths,
        notes: `split incomplete; missing homes: ${missingHomes.join(", ")}`,
      });
      missingCount += 1;
    } else {
      rows.push({
        identity,
        disposition: "MISSING",
        currentPaths: paths,
        notes: `unresolved; missing: ${missingHomes.join(", ")}`,
      });
      missingCount += 1;
    }
  }

  for (const add of currentOnlyIdentities) {
    const identity = normalizeRelative(add);
    rows.push({
      identity,
      disposition: "post_reorg_add",
      currentPaths: [identity],
      notes: "current-only (not backup loss)",
    });
  }

  return { rows, missingCount };
}

/**
 * True when a path is under a secrets-excluded root.
 * @param {string} relativePath
 * @returns {boolean}
 */
export function isExcludedSecretsPath(relativePath) {
  const p = normalizeRelative(relativePath);
  if (p === "secrets" || p === ".env.secrets") {
    return true;
  }
  if (p.startsWith("secrets/") || p.startsWith(".env.secrets/")) {
    return true;
  }
  return false;
}

/**
 * Area registry metadata for scanners (no machine paths).
 * @type {Readonly<Record<string, { grain: string, backupRoots: string[] }>>}
 */
export const AREA_REGISTRY = Object.freeze({
  "packages-dotnet": {
    grain: "top-level-dir + csproj",
    backupRoots: ["server/shared/dotnet"],
  },
  "packages-ts": {
    grain: "top-level-dir",
    backupRoots: ["server/shared/typescript"],
  },
  services: {
    grain: "service-dir + csproj",
    backupRoots: ["server/services"],
  },
  web: {
    grain: "markers",
    backupRoots: ["server/web"],
  },
  contracts: {
    grain: "top-level-folder",
    backupRoots: ["contracts"],
  },
  tools: {
    grain: "top-level-tool-dir",
    backupRoots: ["tools"],
  },
  "docs-adrs": {
    grain: "adr-filename",
    backupRoots: ["docs/adrs"],
  },
  "docs-v2": {
    grain: "md-relative",
    backupRoots: ["docs/v2"],
  },
  "docs-keep-root": {
    grain: "path-existence",
    backupRoots: ["docs"],
  },
  ci: {
    grain: "workflow-filename",
    backupRoots: [".github/workflows"],
  },
  tests: {
    grain: "tests-csproj",
    backupRoots: ["server"],
  },
  infra: {
    grain: "top-level-dir",
    backupRoots: ["infra"],
  },
  "msbuild-root": {
    grain: "named-critical-files",
    backupRoots: ["server"],
  },
  "d2-version": {
    grain: "project-dir",
    backupRoots: ["server/d2-version"],
  },
  "scripts-leaves": {
    grain: "script-basename",
    backupRoots: ["tools/scripts"],
  },
});
