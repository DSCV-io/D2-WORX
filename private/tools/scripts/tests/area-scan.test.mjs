// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------
import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import { mkdtempSync, mkdirSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";
import { test } from "node:test";
import { fileURLToPath } from "node:url";
import {
  AREA_REGISTRY,
  EXCLUDED_ROOTS,
  diffAreaSets,
  isExcludedSecretsPath,
  mapBackupToCurrent,
  normalizeRelative,
} from "../lib/area-scan.mjs";

const HERE = dirname(fileURLToPath(import.meta.url));
const CLI = join(HERE, "..", "area-scan.mjs");

// ---------------------------------------------------------------------------
// path-map
// ---------------------------------------------------------------------------

test("mapBackupToCurrent_rejects_null_or_empty_relative_path", () => {
  assert.throws(() => mapBackupToCurrent(null), /required/);
  assert.throws(() => mapBackupToCurrent(""), /required/);
  assert.throws(() => mapBackupToCurrent("   "), /required/);
});

test("mapBackupToCurrent_returns_unmapped_for_unknown_backup_prefix", () => {
  const r = mapBackupToCurrent("totally/unknown/prefix/foo");
  assert.equal(r.kind, "unmapped");
  assert.deepEqual(r.currentPaths, []);
});

test("mapBackupToCurrent_maps_shared_dotnet_and_services", () => {
  const d = mapBackupToCurrent("server/shared/dotnet/caching");
  assert.equal(d.kind, "mapped");
  assert.deepEqual(d.currentPaths, ["public/packages/dotnet/caching"]);

  const s = mapBackupToCurrent("server/services/edge");
  assert.deepEqual(s.currentPaths, ["private/services/edge"]);

  const w = mapBackupToCurrent("server/web");
  assert.deepEqual(w.currentPaths, ["private/services/web"]);
});

test("mapBackupToCurrent_maps_split_and_private_contracts", () => {
  const split = mapBackupToCurrent("contracts/auth-scopes");
  assert.deepEqual(split.currentPaths, [
    "public/contracts/auth-scopes",
    "private/contracts/auth-scopes",
  ]);

  const priv = mapBackupToCurrent("contracts/keycustodian-error-codes");
  assert.deepEqual(priv.currentPaths, [
    "private/contracts/keycustodian-error-codes",
  ]);

  const pub = mapBackupToCurrent("contracts/headers");
  assert.deepEqual(pub.currentPaths, ["public/contracts/headers"]);
});

// ---------------------------------------------------------------------------
// set-diff
// ---------------------------------------------------------------------------

test("diffAreaSets_empty_backup_and_current_yields_empty_missing_and_adds", () => {
  const { rows, missingCount } = diffAreaSets({
    backupIdentities: [],
    currentPresent: new Set(),
  });
  assert.equal(rows.length, 0);
  assert.equal(missingCount, 0);
});

test("diffAreaSets_mapped_backup_identity_present_at_current_is_moved", () => {
  const { rows, missingCount } = diffAreaSets({
    backupIdentities: ["server/shared/dotnet/result"],
    currentPresent: new Set(["public/packages/dotnet/result"]),
  });
  assert.equal(missingCount, 0);
  assert.equal(rows[0].disposition, "moved");
  assert.equal(rows[0].identity, "server/shared/dotnet/result");
});

test("diffAreaSets_split_catalog_requires_both_public_and_private_homes", () => {
  const { rows, missingCount } = diffAreaSets({
    backupIdentities: ["contracts/messages"],
    currentPresent: new Set([
      "public/contracts/messages",
      "private/contracts/messages",
    ]),
  });
  assert.equal(missingCount, 0);
  assert.equal(rows[0].disposition, "split");
});

test("diffAreaSets_split_with_only_one_home_is_missing_not_moved", () => {
  const { rows, missingCount } = diffAreaSets({
    backupIdentities: ["contracts/auth-scopes"],
    currentPresent: new Set(["public/contracts/auth-scopes"]),
  });
  assert.equal(missingCount, 1);
  assert.equal(rows[0].disposition, "MISSING");
  assert.match(rows[0].notes ?? "", /split incomplete/);
});

test("diffAreaSets_current_only_identity_is_post_reorg_add", () => {
  const { rows, missingCount } = diffAreaSets({
    backupIdentities: [],
    currentPresent: new Set(),
    currentOnlyIdentities: [
      "private/packages/dotnet/tests/DcsvIo.D2.Private.Packages.Tests.csproj",
    ],
  });
  assert.equal(missingCount, 0);
  assert.equal(rows[0].disposition, "post_reorg_add");
});

test("diffAreaSets_unresolved_backup_identity_surfaces_missing_row", () => {
  const { rows, missingCount } = diffAreaSets({
    backupIdentities: ["server/shared/dotnet/missing-pkg"],
    currentPresent: new Set(),
  });
  assert.equal(missingCount, 1);
  assert.equal(rows[0].disposition, "MISSING");
});

test("diffAreaSets_unchanged_home_when_identity_equals_current_path", () => {
  const { rows } = diffAreaSets({
    backupIdentities: ["infra/compose"],
    currentPresent: new Set(["infra/compose"]),
    mapFn: (id) => ({ kind: "mapped", currentPaths: [normalizeRelative(id)] }),
  });
  assert.equal(rows[0].disposition, "unchanged_home");
});

// ---------------------------------------------------------------------------
// secrets / registry
// ---------------------------------------------------------------------------

test("area_registry_excludes_secrets_and_env_secrets_roots", () => {
  assert.ok(EXCLUDED_ROOTS.includes("secrets"));
  assert.ok(EXCLUDED_ROOTS.includes(".env.secrets"));
  assert.equal(isExcludedSecretsPath("secrets/root.key"), true);
  assert.equal(isExcludedSecretsPath(".env.secrets"), true);
  assert.equal(isExcludedSecretsPath("public/packages/dotnet"), false);
  for (const id of Object.keys(AREA_REGISTRY)) {
    for (const root of AREA_REGISTRY[id].backupRoots) {
      assert.equal(isExcludedSecretsPath(root), false);
    }
  }
});

// ---------------------------------------------------------------------------
// CLI
// ---------------------------------------------------------------------------

test("cli_missing_backup_root_exits_nonzero_without_throwing_unhandled", () => {
  const r = spawnSync(process.execPath, [CLI], {
    encoding: "utf-8",
    env: { ...process.env, D2_BACKUP_ROOT: "" },
  });
  assert.notEqual(r.status, 0);
  assert.match(r.stderr, /D2_BACKUP_ROOT/);
  assert.equal(r.error, undefined);
});

test("cli_unresolved_missing_exits_nonzero_unless_report_only", () => {
  const tmp = mkdtempSync(join(tmpdir(), "area-scan-cli-"));
  try {
    // Minimal backup tree with one shared package that will not exist in real monorepo map
    // Use a deliberate package name that is not in the working tree.
    mkdirSync(
      join(tmp, "server", "shared", "dotnet", "zz-deliberate-missing-pkg"),
      {
        recursive: true,
      },
    );
    mkdirSync(join(tmp, "server", "shared", "typescript"), { recursive: true });
    mkdirSync(join(tmp, "server", "services"), { recursive: true });
    mkdirSync(join(tmp, "contracts"), { recursive: true });
    mkdirSync(join(tmp, "tools", "scripts"), { recursive: true });
    mkdirSync(join(tmp, "docs", "adrs"), { recursive: true });
    mkdirSync(join(tmp, "docs", "v2"), { recursive: true });
    mkdirSync(join(tmp, "infra"), { recursive: true });
    mkdirSync(join(tmp, ".github", "workflows"), { recursive: true });
    writeFileSync(
      join(tmp, "server", "Directory.Build.props"),
      "<Project />\n",
    );

    const fail = spawnSync(process.execPath, [CLI, "--markdown"], {
      encoding: "utf-8",
      env: { ...process.env, D2_BACKUP_ROOT: tmp },
    });
    assert.notEqual(fail.status, 0, fail.stdout + fail.stderr);
    assert.match(
      fail.stdout + fail.stderr,
      /MISSING|zz-deliberate-missing-pkg/i,
    );

    const report = spawnSync(
      process.execPath,
      [CLI, "--report-only", "--markdown"],
      {
        encoding: "utf-8",
        env: { ...process.env, D2_BACKUP_ROOT: tmp },
      },
    );
    assert.equal(report.status, 0, report.stdout + report.stderr);
    assert.match(report.stdout, /zz-deliberate-missing-pkg|MISSING/i);
  } finally {
    rmSync(tmp, { recursive: true, force: true });
  }
});

// ---------------------------------------------------------------------------
// normalizeRelative + intentional_drop
// ---------------------------------------------------------------------------

test("normalizeRelative_collapses_backslashes_and_trims_slashes", () => {
  assert.equal(normalizeRelative("a\\b\\c"), "a/b/c");
  assert.equal(normalizeRelative("./foo/bar/"), "foo/bar");
  assert.equal(normalizeRelative("foo/bar"), "foo/bar");
});

test("mapBackupToCurrent_secrets_and_env_secrets_prefix_are_intentional_drop", () => {
  const a = mapBackupToCurrent("secrets/root.key");
  assert.equal(a.kind, "intentional_drop");
  assert.deepEqual(a.currentPaths, []);

  const b = mapBackupToCurrent(".env.secrets/nested");
  assert.equal(b.kind, "intentional_drop");
  assert.deepEqual(b.currentPaths, []);

  const c = mapBackupToCurrent(".env.secrets");
  assert.equal(c.kind, "intentional_drop");
});

test("diffAreaSets_intentional_drop_map_is_not_missing", () => {
  const { rows, missingCount } = diffAreaSets({
    backupIdentities: ["secrets/root.key", ".env.secrets/x"],
    currentPresent: new Set(),
  });
  assert.equal(missingCount, 0);
  assert.equal(rows.length, 2);
  assert.ok(rows.every((r) => r.disposition === "intentional_drop"));
});
