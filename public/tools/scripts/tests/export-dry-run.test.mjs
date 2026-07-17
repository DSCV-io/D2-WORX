// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
//
// Export dry-run adversarial matrix (named §1.22 tests).

import assert from "node:assert/strict";
import {
  mkdtempSync,
  mkdirSync,
  rmSync,
  writeFileSync,
  existsSync,
  readFileSync,
} from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import { test, after } from "node:test";

const here = dirname(fileURLToPath(import.meta.url));

function findMonorepoRoot(start = here) {
  let dir = resolve(start);

  for (let i = 0; i < 24; i++) {
    if (existsSync(join(dir, "D2.slnx"))) return dir;
    const parent = resolve(dir, "..");
    if (parent === dir) break;
    dir = parent;
  }

  throw new Error("monorepo root not found");
}

const repoRoot = findMonorepoRoot();
const scriptPath = join(
  repoRoot,
  "public",
  "tools",
  "scripts",
  "export-dry-run.mjs",
);

const {
  CLOSED_EXTRAS,
  isAllowlisted,
  isHardDenylisted,
  isPathTraversal,
  validateAllowlistEntry,
  validateCandidatePaths,
  scanPublicTree,
  stageExportTree,
  assertStagePropsIsolation,
  buildStagedPublicSlnx,
  runExportDryRun,
  parseExportArgs,
} = await import(pathToFileURL(scriptPath).href);

const tempDirs = [];

after(() => {
  for (const d of tempDirs) {
    try {
      rmSync(d, { recursive: true, force: true, maxRetries: 5 });
    } catch {
      // best-effort
    }
  }
});

function makeTemp(prefix = "export-dry-") {
  const d = mkdtempSync(join(tmpdir(), prefix));
  tempDirs.push(d);
  return d;
}

/**
 * Minimal monorepo-shaped tree for isolated scan/stage/CLI tests.
 *
 * @param {string} root
 * @param {{ productIpRel?: string, denylistRel?: string }} [opts]
 */
function seedSyntheticMonorepo(root, opts = {}) {
  writeFileSync(join(root, "D2.slnx"), "<Solution />\n");
  writeFileSync(join(root, "pnpm-workspace.yaml"), "packages: []\n");

  for (const extra of CLOSED_EXTRAS) {
    writeFileSync(join(root, extra), `<!-- synthetic ${extra} -->\n`);
  }

  mkdirSync(join(root, "public", "packages"), { recursive: true });
  writeFileSync(join(root, "public", "D2.Public.slnx"), "<Solution />\n");
  writeFileSync(join(root, "public", "LICENSE"), "Apache-2.0\n");
  writeFileSync(join(root, "public", "packages", "clean.txt"), "ok\n");

  if (opts.productIpRel !== undefined) {
    const abs = join(root, ...opts.productIpRel.split("/"));
    mkdirSync(dirname(abs), { recursive: true });
    writeFileSync(abs, "product-ip-marker\n");
  }

  if (opts.denylistRel !== undefined) {
    const abs = join(root, ...opts.denylistRel.split("/"));
    mkdirSync(dirname(abs), { recursive: true });
    writeFileSync(abs, "denied\n");
  }
}

test("ExportDryRun_AllowsPublicAndClosedExtras", () => {
  for (const extra of CLOSED_EXTRAS) {
    assert.equal(isAllowlisted(extra), true, extra);
  }

  assert.equal(isAllowlisted("public"), true);
  assert.equal(isAllowlisted("public/packages/dotnet/result"), true);

  const result = validateCandidatePaths(repoRoot, ["public", ...CLOSED_EXTRAS]);

  assert.equal(result.ok, true, result.failures.join("; "));
});

test("ExportDryRun_FailsOnPrivatePath", () => {
  const result = validateCandidatePaths(repoRoot, ["private/services/edge"]);
  assert.equal(result.ok, false);
  assert.ok(result.failures.some((f) => /denylist|outside allowlist/i.test(f)));
  assert.equal(isHardDenylisted("private/foo"), true);
  assert.equal(isAllowlisted("private/foo"), false);
});

test("ExportDryRun_FailsOnPathTraversal", () => {
  assert.equal(isPathTraversal(repoRoot, "public/../private"), true);

  const result = validateCandidatePaths(repoRoot, [
    "public/../private/secrets",
  ]);
  assert.equal(result.ok, false);
  assert.ok(result.failures.some((f) => /traversal|denylist|outside/i.test(f)));
});

test("ExportDryRun_FailsOnSecretsPath", () => {
  assert.equal(isHardDenylisted("secrets"), true);
  assert.equal(isHardDenylisted("secrets/dev-key.pem"), true);

  const result = validateCandidatePaths(repoRoot, ["secrets/x"]);
  assert.equal(result.ok, false);
});

test("ExportDryRun_FailsOnEnvSecrets", () => {
  assert.equal(isHardDenylisted(".env.secrets"), true);
  assert.equal(isHardDenylisted("foo/.env.secrets"), true);
  assert.equal(isHardDenylisted(".env.local"), true);

  const result = validateCandidatePaths(repoRoot, [".env.secrets"]);
  assert.equal(result.ok, false);
});

test("ExportDryRun_FailsOnEmptyAllowlistEntry", () => {
  const v = validateAllowlistEntry("");
  assert.equal(v.ok, false);
  assert.match(v.reason ?? "", /empty/i);

  const v2 = validateAllowlistEntry("   ");
  assert.equal(v2.ok, false);
});

test("ExportDryRun_FailsOnMalformedAllowlistEntry", () => {
  const abs = validateAllowlistEntry("/etc/passwd");
  assert.equal(abs.ok, false);

  const trav = validateAllowlistEntry("public/../private");
  assert.equal(trav.ok, false);

  const privateEntry = validateAllowlistEntry("private/contracts");
  assert.equal(privateEntry.ok, false);
});

test("ExportDryRun_RejectsExtrasOutsideClosedSet", () => {
  assert.equal(isAllowlisted("stylecop.json"), false);
  assert.equal(isAllowlisted("AGENTS.md"), false);
  assert.equal(isAllowlisted("docs/COMMANDS.md"), false);

  const result = validateCandidatePaths(repoRoot, ["AGENTS.md"]);
  assert.equal(result.ok, false);
});

test("ExportDryRun_FailsOnHardDenylistPath", () => {
  assert.equal(isHardDenylisted("docs/dev/rules.md"), true);
  assert.equal(isHardDenylisted("infra/compose/compose.yml"), true);
  assert.equal(isHardDenylisted("private/tools/scripts/gen-dev-keys.sh"), true);
});

test("ExportDryRun_FailsOnPublicSecretsLeaf", () => {
  assert.equal(isHardDenylisted("public/secrets"), true);
  assert.equal(isHardDenylisted("public/foo/secrets"), true);
  assert.equal(isHardDenylisted("a/secrets"), true);

  const result = validateCandidatePaths(repoRoot, ["public/foo/secrets"]);
  assert.equal(result.ok, false);
  assert.ok(result.failures.some((f) => /denylist/i.test(f)));
});

test("ExportDryRun_FailsOnPublicPrivateSegment", () => {
  assert.equal(isHardDenylisted("public/private"), true);
  assert.equal(isHardDenylisted("x/private"), true);
  assert.equal(isHardDenylisted("public/foo/private/bar"), true);

  const result = validateCandidatePaths(repoRoot, ["public/private"]);
  assert.equal(result.ok, false);
});

test("ExportDryRun_FailsOnPublicInfraSegment", () => {
  assert.equal(isHardDenylisted("public/infra"), true);
  assert.equal(isHardDenylisted("x/infra"), true);

  const result = validateCandidatePaths(repoRoot, ["public/infra"]);
  assert.equal(result.ok, false);
});

test("ExportDryRun_FailsOnPublicDocsDev", () => {
  assert.equal(isHardDenylisted("public/docs/dev"), true);
  assert.equal(isHardDenylisted("public/docs/dev/rules.md"), true);
  assert.equal(isHardDenylisted("docs/dev"), true);
});

test("ExportDryRun_DoesNotPush", () => {
  const code = runExportDryRun(["--push"], { repoRoot });
  assert.equal(code, 2);

  const code2 = runExportDryRun(["--remote", "git@example.com:x.git"], {
    repoRoot,
  });
  assert.equal(code2, 2);

  const args = parseExportArgs(["--push"]);
  assert.equal(args.push, true);
});

test("ExportDryRun_FailsOnProductIpFixture", () => {
  // Temp monorepo with product-IP path under public/** — must fail scan.
  const root = makeTemp("export-ip-repo-");
  seedSyntheticMonorepo(root, {
    productIpRel: "public/packages/typescript/d2-private-leak.txt",
  });

  const scan = scanPublicTree(root);
  assert.ok(
    scan.failures.length > 0,
    "scanPublicTree must report product IP marker",
  );
  assert.ok(
    scan.failures.some(
      (f) =>
        /product IP marker/i.test(f) &&
        f.includes("d2-private-") &&
        f.includes("public/packages/typescript/d2-private-leak.txt"),
    ),
    scan.failures.join("; "),
  );

  const code = runExportDryRun([], { repoRoot: root });
  assert.equal(code, 1, "CLI must exit non-zero on product IP under public");
});

test("ExportDryRun_SkipsStageWhenDenylistDirty", () => {
  const root = makeTemp("export-dirty-repo-");
  seedSyntheticMonorepo(root, {
    denylistRel: "public/foo/secrets/key.pem",
  });

  const stageDir = makeTemp("export-should-not-materialize-");
  const code = runExportDryRun(["--stage-dir", stageDir], { repoRoot: root });
  assert.equal(code, 1);

  // Fail-closed: denylist material must not land under stage
  assert.equal(
    existsSync(join(stageDir, "public", "foo", "secrets", "key.pem")),
    false,
  );
  // Stage public tree should not have been fully copied either
  assert.equal(existsSync(join(stageDir, "public", "LICENSE")), false);
});

test("ExportDryRun_StagedTree_BuildsPublicSlnx_Isolation", () => {
  // Stage into isolated temp OUTSIDE monorepo (tmpdir) — anti walk-out.
  const stageDir = makeTemp("export-stage-");
  const staged = stageExportTree(repoRoot, stageDir);

  assert.equal(staged.ok, true, staged.failures.join("; "));
  assert.ok(existsSync(join(stageDir, "Directory.Build.props")));
  assert.ok(existsSync(join(stageDir, ".editorconfig")));
  assert.ok(existsSync(join(stageDir, "public", "D2.Public.slnx")));
  assert.ok(existsSync(join(stageDir, "public", "LICENSE")));

  const iso = assertStagePropsIsolation(stageDir);
  assert.equal(iso.ok, true, iso.reason);
  assert.ok(
    (iso.resolvedProps ?? "").includes(stageDir) ||
      (iso.resolvedProps ?? "")
        .replace(/\\/g, "/")
        .includes(stageDir.replace(/\\/g, "/")),
    `resolved props must be inside stage: ${iso.resolvedProps}`,
  );

  // Meta records monorepo vs stage separation
  const meta = JSON.parse(
    readFileSync(join(stageDir, ".export-stage-meta.json"), "utf-8"),
  );
  assert.ok(meta.stageRoot);
  assert.ok(meta.monorepoRoot);
  assert.notEqual(meta.stageRoot, meta.monorepoRoot);
});

test("ExportDryRun_StagedTree_BuildsPublicSlnx", { timeout: 600_000 }, () => {
  // Outside-monorepo stage + real dotnet build of staged Public.slnx
  // (export-clone proof — not monorepo dual-suite alone).
  const stageDir = makeTemp("export-stage-build-");
  const staged = stageExportTree(repoRoot, stageDir);
  assert.equal(staged.ok, true, staged.failures.join("; "));

  const iso = assertStagePropsIsolation(stageDir);
  assert.equal(iso.ok, true, iso.reason);

  const built = buildStagedPublicSlnx(stageDir);
  assert.equal(
    built.ok,
    true,
    `staged Public.slnx build failed:\n${built.output.slice(-4000)}`,
  );
});

test("ExportDryRun_DefaultCli_ExitsZeroOnCleanMonorepo", () => {
  const code = runExportDryRun([], { repoRoot });
  assert.equal(code, 0);
});

test("ExportDryRun_AllowlistEntryCli_RejectsEmpty", () => {
  const code = runExportDryRun(["--allowlist-entry", ""], { repoRoot });
  assert.equal(code, 1);
});
