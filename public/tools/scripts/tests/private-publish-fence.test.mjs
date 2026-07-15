// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
//
// Private publish fence — static parse of release-libs.yml.
// Named matrix incl. PrivatePublishFence_NoGitHubReleaseOfPublicIds.

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
const releaseLibsPath = join(
  repoRoot,
  ".github",
  "workflows",
  "release-libs.yml",
);

function readReleaseLibs() {
  assert.ok(existsSync(releaseLibsPath), "release-libs.yml must exist");
  return readFileSync(releaseLibsPath, "utf-8");
}

/** Soft-equivalent GH Release patterns that must not appear. */
const GH_RELEASE_SOFT_EQUIV = [
  /gh\s+release\s+create\b/i,
  /gh\s+release\s+upload\b/i,
  /actions\/create-release/i,
  /softprops\/action-gh-release/i,
  /gh\s+api\s+.*\/releases/i,
];

test("PrivatePublishFence_NoGitHubReleaseOfPublicIds", () => {
  const yaml = readReleaseLibs();

  for (const re of GH_RELEASE_SOFT_EQUIV) {
    assert.equal(
      re.test(yaml),
      false,
      `release-libs.yml must not contain soft-equiv GH Release: ${re}`,
    );
  }

  // Deliberate-drift pin: the fence fails if these reappear
  assert.equal(yaml.includes("gh release create"), false);
  assert.equal(yaml.includes("Create GitHub Release"), false);
});

test("PrivatePublishFence_AllowsArtifactOnlyPack", () => {
  const yaml = readReleaseLibs();

  assert.match(yaml, /upload-artifact@v4/);
  assert.match(yaml, /dotnet pack/i);
  assert.match(yaml, /pnpm.*pack|Pack all TypeScript/i);
  assert.match(yaml, /public\/tools\/scripts\/assemble-libs-bundle\.mjs/);
});

test("PrivatePublishFence_HardFailsPublicIdsToPublicFeeds", () => {
  const yaml = readReleaseLibs();

  assert.equal(/nuget\s+push/i.test(yaml), false);
  assert.equal(/npm\s+publish/i.test(yaml), false);
  assert.equal(/NUGET_API_KEY/.test(yaml), false);
  assert.equal(/NPM_TOKEN/.test(yaml), false);
  assert.equal(/registry\.npmjs\.org/.test(yaml), false);
  assert.equal(/api\.nuget\.org/.test(yaml), false);
});

test("PrivatePublishFence_ContentsReadOnly_NoWrite", () => {
  const yaml = readReleaseLibs();

  assert.match(yaml, /permissions:\s*\n\s*contents:\s*read/m);
  assert.equal(/contents:\s*write/i.test(yaml), false);
});

test("PrivatePublishFence_ApacheNotesNotPolyForm", () => {
  const yaml = readReleaseLibs();

  assert.match(yaml, /Apache License 2\.0|Apache-2\.0/);
  assert.equal(yaml.includes("PolyForm Strict"), false);
});

test("PrivatePublishFence_DeliberateDrift_ReinsertGhReleaseWouldMatch", () => {
  // Soft-equivalent deliberate-drift: a synthetic dirty yaml fails the fence regexes.
  const dirty = `
jobs:
  x:
    steps:
      - run: gh release create v1 foo.nupkg
`;

  assert.equal(GH_RELEASE_SOFT_EQUIV[0].test(dirty), true);

  const softprops = `uses: softprops/action-gh-release@v2`;
  assert.equal(GH_RELEASE_SOFT_EQUIV[3].test(softprops), true);

  const nugetPush = `run: nuget push bundle/nuget/*.nupkg`;
  assert.equal(/nuget\s+push/i.test(nugetPush), true);
});
