// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
//
// Static path / identity asserts for test.yml + dual-suite surface.

import assert from "node:assert/strict";
import { readFileSync, existsSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { test } from "node:test";

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
const testYml = readFileSync(
  join(repoRoot, ".github", "workflows", "test.yml"),
  "utf-8",
);

test("TestYml_PublicSharedTests_TargetsPublicCsproj", () => {
  assert.match(
    testYml,
    /public\/packages\/dotnet\/tests\/DcsvIo\.D2\.Tests\.csproj/,
  );
  assert.match(testYml, /DcsvIo\.D2\.Tests\.Unit\*/);
  assert.match(testYml, /DcsvIo\.D2\.Tests\.Integration\*/);
  assert.match(testYml, /Category=ContractFixtures/);

  // No pre-identity debt in active steps
  assert.equal(testYml.includes("D2.Shared.Tests.csproj"), false);
  assert.equal(testYml.includes("D2.Shared.Tests.Unit"), false);
  assert.equal(testYml.includes("D2.Shared.Result"), false);
});

test("TestYml_EdgeFilters_PrivateNamespace", () => {
  assert.match(testYml, /DcsvIo\.D2\.Private\.Edge\.Tests\.Unit\*/);
  assert.match(testYml, /DcsvIo\.D2\.Private\.Edge\.Tests\.Integration\*/);
  assert.equal(testYml.includes("D2.Edge.Tests.Unit"), false);
});

test("TestYml_PackSmoke_ScopedTgzAndNupkg", () => {
  assert.match(testYml, /dcsv-io-d2-result-\*\.tgz/);
  assert.match(testYml, /DcsvIo\.D2\.Result\.\*\.nupkg/);
  assert.match(
    testYml,
    /public\/packages\/dotnet\/result\/core\/DcsvIo\.D2\.Result\.csproj/,
  );
  assert.match(testYml, /@dcsv-io\/d2-result/);
  // Bare pre-scope tgz glob must not appear (scoped name contains d2-result substring).
  assert.equal(/(?<!dcsv-io-)d2-result-\*\.tgz/.test(testYml), false);
  assert.equal(/(?<![@\w/])@d2\/result(?!-)/.test(testYml), false);
});

test("TestYml_ContractGateAndProtoPaths", () => {
  assert.match(testYml, /public\/tools\/contract-gate\/dist\/cli\.js/);
  assert.match(testYml, /public\/contracts\/protos/);
  assert.match(testYml, /public\/tools\/scripts\/tests\/\*\.test\.mjs/);
  assert.match(testYml, /@dcsv-io\/d2-typespec-emitters/);
  // Monorepo-root tools/contract-gate (without public/) must not be the CLI path.
  assert.equal(/(?:^|[\s"`'])tools\/contract-gate\/dist/m.test(testYml), false);
  assert.equal(/buf lint contracts\/protos(?!\/)/.test(testYml), false);
});

test("TestYml_KeyCustodianPrivateClientId", () => {
  assert.match(testYml, /@dcsv-io\/d2-private-key-custodian-client/);
  assert.equal(
    /(?<!@dcsv-io\/d2-private-)@d2\/key-custodian-client/.test(testYml),
    false,
  );
});

test("TestYml_PublicBuildJob_PublicSlnx", () => {
  assert.match(testYml, /name: Public build \(D2\.Public\.slnx\)/);
  assert.match(testYml, /dotnet build public\/D2\.Public\.slnx/);
});

test("PublicOnlySuite_BuildsWithoutPrivateRefs", () => {
  // Structural: public slnx exists and must not ProjectReference private/
  const slnxPath = join(repoRoot, "public", "D2.Public.slnx");
  assert.ok(existsSync(slnxPath), "public/D2.Public.slnx must exist");
  const slnx = readFileSync(slnxPath, "utf-8");
  assert.equal(/private[\\/]/.test(slnx), false);
});

test("PublicParity_MatchesPublicOnlyCommand", () => {
  // Public-parity display names retained for H5
  const names = [
    ".NET Shared Unit Tests",
    ".NET Shared Integration Tests",
    "TS Shared Unit Tests",
    "Pack smoke (.NET nupkg + TS tgz)",
    "Public build (D2.Public.slnx)",
  ];

  for (const n of names) {
    assert.ok(testYml.includes(`name: ${n}`), `missing display name: ${n}`);
  }
});

test("ExportDryRunWorkflow_ContentsRead_NoPush", () => {
  const path = join(repoRoot, ".github", "workflows", "export-dry-run.yml");
  assert.ok(existsSync(path));
  const yaml = readFileSync(path, "utf-8");
  assert.match(yaml, /contents:\s*read/);
  assert.equal(/git\s+push/i.test(yaml), false);
  assert.equal(/contents:\s*write/i.test(yaml), false);
  assert.match(yaml, /export-dry-run\.mjs/);
});
