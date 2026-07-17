// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

// End-to-end synthetic-git integration test for runSpecGate.
//
// Builds an isolated temp git repo per case (ambient global/system git
// config neutralized), mutates the working tree, and invokes runSpecGate
// in-process against the baseline branch ref. Exercises real fileAtRef +
// listTrackedPathsAtRef seams (git show / git ls-tree).

import { spawnSync } from "node:child_process";
import {
  mkdtempSync,
  mkdirSync,
  writeFileSync,
  rmSync,
  unlinkSync,
} from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { afterEach, describe, expect, it } from "vitest";

import { runSpecGate } from "../src/run-spec-gate.js";
import { SKIP_DIR_NAMES } from "../src/discovery.js";

// ---------------------------------------------------------------------------
// Synthetic-repo helper (ambient-git-config isolation)
// ---------------------------------------------------------------------------

const BASE_BRANCH = "baseline";
const tempRepos: string[] = [];

afterEach(() => {
  while (tempRepos.length > 0) {
    const dir = tempRepos.pop();

    if (dir !== undefined) {
      rmSync(dir, { recursive: true, force: true, maxRetries: 5 });
      // Sibling empty-config file created by emptyGitConfigPath.
      rmSync(`${dir}.empty-gitconfig`, { force: true, maxRetries: 5 });
    }
  }
});

interface SyntheticRepo {
  readonly root: string;
  readonly baseRef: typeof BASE_BRANCH;
}

/**
 * Empty config path used as GIT_CONFIG_GLOBAL / GIT_CONFIG_SYSTEM so ambient
 * operator config never applies. `os.devNull` is not a readable config file
 * on Windows (`\\.\nul` → "Invalid argument"); an empty file is portable.
 * Placed OUTSIDE the repo root so `git add` never stages it.
 */
function emptyGitConfigPath(repoRoot: string): string {
  const path = `${repoRoot}.empty-gitconfig`;
  writeFileSync(path, "", "utf-8");

  return path;
}

function git(cwd: string, args: string[], emptyConfig: string): void {
  const result = spawnSync("git", args, {
    cwd,
    encoding: "utf-8",
    env: {
      ...process.env,
      GIT_CONFIG_GLOBAL: emptyConfig,
      GIT_CONFIG_SYSTEM: emptyConfig,
      GIT_CONFIG_NOSYSTEM: "1",
    },
  });

  if (result.status !== 0) {
    throw new Error(
      `git ${args.join(" ")} failed (exit ${String(result.status)}):` +
        ` ${(result.stderr ?? result.stdout ?? "").trim()}`,
    );
  }
}

function writeRel(root: string, relPath: string, content: string): void {
  const abs = join(root, relPath);
  mkdirSync(join(abs, ".."), { recursive: true });
  writeFileSync(abs, content, "utf-8");
}

/**
 * Build a temp git repo with a baseline commit containing the given files.
 * Registers the path for afterEach cleanup BEFORE git init so a mid-construction
 * throw cannot leak the directory.
 */
function makeSyntheticRepo(
  files: Readonly<Record<string, string>>,
): SyntheticRepo {
  const root = mkdtempSync(join(tmpdir(), "cg-e2e-"));
  tempRepos.push(root);

  const emptyConfig = emptyGitConfigPath(root);

  git(root, ["init", "-b", BASE_BRANCH], emptyConfig);
  git(root, ["config", "user.name", "contract-gate-fixture"], emptyConfig);
  git(root, ["config", "user.email", "fixture@example.invalid"], emptyConfig);

  for (const [relPath, content] of Object.entries(files)) {
    writeRel(root, relPath, content);
    git(root, ["add", "--", relPath], emptyConfig);
  }
  git(
    root,
    [
      "-c",
      "commit.gpgsign=false",
      "-c",
      "core.hooksPath=",
      "commit",
      "-m",
      "baseline",
    ],
    emptyConfig,
  );

  return { root, baseRef: BASE_BRANCH };
}

// ---------------------------------------------------------------------------
// Shared baseline content
// ---------------------------------------------------------------------------

const ERROR_CODES_BASELINE = JSON.stringify(
  {
    errorCodes: [
      { code: "ERR_A", httpStatus: 400, category: "client" },
      { code: "ERR_B", httpStatus: 500, category: "server" },
    ],
  },
  null,
  2,
);

const ERROR_CODES_ONE_REMOVED = JSON.stringify(
  {
    errorCodes: [{ code: "ERR_A", httpStatus: 400, category: "client" }],
  },
  null,
  2,
);

const LOCALE_BASELINE = JSON.stringify(
  { greeting: "Hello", farewell: "Goodbye" },
  null,
  2,
);

const LOCALE_KEY_REMOVED = JSON.stringify({ greeting: "Hello" }, null, 2);

const OPENAPI_BASELINE = JSON.stringify(
  {
    openapi: "3.0.0",
    info: { title: "Svc", version: "1.0" },
    paths: {
      "/v1/report": {
        get: {
          operationId: "getReport",
          responses: { "200": { description: "OK" } },
        },
      },
      "/v1/export": {
        post: {
          operationId: "exportReport",
          responses: { "200": { description: "OK" } },
        },
      },
    },
  },
  null,
  2,
);

const OPENAPI_PATH_REMOVED = JSON.stringify(
  {
    openapi: "3.0.0",
    info: { title: "Svc", version: "1.0" },
    paths: {
      "/v1/report": {
        get: {
          operationId: "getReport",
          responses: { "200": { description: "OK" } },
        },
      },
    },
  },
  null,
  2,
);

const OPENAPI_FIXTURE_RENAMED = JSON.stringify(
  {
    openapi: "3.0.0",
    info: { title: "Fixture", version: "1.0" },
    paths: {},
    components: {
      schemas: {
        OpenApiSignFixtureInput: { type: "object" },
      },
    },
  },
  null,
  2,
);

const DEFAULT_BASELINE_FILES: Readonly<Record<string, string>> = {
  "public/contracts/domain/error-codes.spec.json": ERROR_CODES_BASELINE,
  "public/contracts/messages/en-US.json": LOCALE_BASELINE,
  "public/contracts/svc/api/svc.openapi.g.json": OPENAPI_BASELINE,
  "public/contracts/svc/tests/Unit/fix.openapi.g.json": OPENAPI_FIXTURE_RENAMED,
  "public/contracts/domain/tests/fixture.spec.json": ERROR_CODES_BASELINE,
};

// ---------------------------------------------------------------------------
// Cases
// ---------------------------------------------------------------------------

describe("runSpecGate — e2e synthetic git", () => {
  it("clean working tree vs baseline passes with zero findings and scope data", async () => {
    const repo = makeSyntheticRepo(DEFAULT_BASELINE_FILES);

    const result = await runSpecGate({
      repoRoot: repo.root,
      baseRef: repo.baseRef,
      valveOpen: false,
    });

    expect(result.passed).toBe(true);
    expect(result.findings).toHaveLength(0);
    expect(result.scope.skipDirs).toEqual([...SKIP_DIR_NAMES]);
    expect(result.scope.excludedOpenApiTestFiles).toContain(
      "public/contracts/svc/tests/Unit/fix.openapi.g.json",
    );
    expect(result.scope.excludedSpecTestFiles).toContain(
      "public/contracts/domain/tests/fixture.spec.json",
    );
  });

  it("registered spec-catalog entry removed produces a BREAKING finding", async () => {
    const repo = makeSyntheticRepo(DEFAULT_BASELINE_FILES);
    writeRel(
      repo.root,
      "public/contracts/domain/error-codes.spec.json",
      ERROR_CODES_ONE_REMOVED,
    );

    const result = await runSpecGate({
      repoRoot: repo.root,
      baseRef: repo.baseRef,
      valveOpen: false,
    });

    expect(result.passed).toBe(false);
    expect(
      result.findings.some(
        (f) =>
          f.arm === "spec" &&
          f.file === "public/contracts/domain/error-codes.spec.json" &&
          f.message.includes("BREAKING"),
      ),
    ).toBe(true);
  });

  it("committed spec file DELETED from the working tree is BREAKING", async () => {
    const repo = makeSyntheticRepo(DEFAULT_BASELINE_FILES);
    unlinkSync(
      join(repo.root, "public/contracts/domain/error-codes.spec.json"),
    );

    const result = await runSpecGate({
      repoRoot: repo.root,
      baseRef: repo.baseRef,
      valveOpen: false,
    });

    expect(result.passed).toBe(false);
    expect(
      result.findings.some(
        (f) =>
          f.arm === "spec" &&
          f.file === "public/contracts/domain/error-codes.spec.json" &&
          f.message.includes("File was deleted"),
      ),
    ).toBe(true);
  });

  it("spec file NEW at HEAD (absent on baseline) is fully additive", async () => {
    const repo = makeSyntheticRepo(DEFAULT_BASELINE_FILES);
    // Registered basename under a new directory — fully additive (no baseline).
    writeRel(
      repo.root,
      "public/contracts/new-domain/error-codes.spec.json",
      ERROR_CODES_BASELINE,
    );

    const result = await runSpecGate({
      repoRoot: repo.root,
      baseRef: repo.baseRef,
      valveOpen: false,
    });

    expect(result.passed).toBe(true);
    expect(result.findings).toHaveLength(0);
  });

  it("UNREGISTERED *.spec.json basename produces a gate-error finding", async () => {
    const repo = makeSyntheticRepo(DEFAULT_BASELINE_FILES);
    writeRel(
      repo.root,
      "public/contracts/domain/totally-unknown-catalog.spec.json",
      JSON.stringify({ items: [{ id: "x" }] }),
    );

    const result = await runSpecGate({
      repoRoot: repo.root,
      baseRef: repo.baseRef,
      valveOpen: false,
    });

    expect(result.passed).toBe(false);
    expect(
      result.findings.some(
        (f) =>
          f.arm === "spec" &&
          f.file ===
            "public/contracts/domain/totally-unknown-catalog.spec.json" &&
          f.message.includes("unregistered"),
      ),
    ).toBe(true);
  });

  it("key removed from a committed locale file produces an i18n BREAKING finding", async () => {
    const repo = makeSyntheticRepo(DEFAULT_BASELINE_FILES);
    writeRel(
      repo.root,
      "public/contracts/messages/en-US.json",
      LOCALE_KEY_REMOVED,
    );

    const result = await runSpecGate({
      repoRoot: repo.root,
      baseRef: repo.baseRef,
      valveOpen: false,
    });

    expect(result.passed).toBe(false);
    expect(
      result.findings.some(
        (f) =>
          f.arm === "i18n" &&
          f.file === "public/contracts/messages/en-US.json" &&
          f.message.includes("BREAKING"),
      ),
    ).toBe(true);
  });

  it("committed locale file DELETED from the working tree is BREAKING", async () => {
    const repo = makeSyntheticRepo(DEFAULT_BASELINE_FILES);
    unlinkSync(join(repo.root, "public/contracts/messages/en-US.json"));

    const result = await runSpecGate({
      repoRoot: repo.root,
      baseRef: repo.baseRef,
      valveOpen: false,
    });

    expect(result.passed).toBe(false);
    expect(
      result.findings.some(
        (f) =>
          f.arm === "i18n" &&
          f.file === "public/contracts/messages/en-US.json" &&
          f.message.includes("Locale file deleted"),
      ),
    ).toBe(true);
  });

  it("working-tree JSON corrupted on a registered spec file throws", async () => {
    const repo = makeSyntheticRepo(DEFAULT_BASELINE_FILES);
    writeRel(
      repo.root,
      "public/contracts/domain/error-codes.spec.json",
      "{ not valid json",
    );

    await expect(
      runSpecGate({
        repoRoot: repo.root,
        baseRef: repo.baseRef,
        valveOpen: false,
      }),
    ).rejects.toThrow(/failed to parse JSON/);
  });

  it("spec file under public/contracts/.../tests/ mutated produces NO findings", async () => {
    const repo = makeSyntheticRepo(DEFAULT_BASELINE_FILES);
    writeRel(
      repo.root,
      "public/contracts/domain/tests/fixture.spec.json",
      ERROR_CODES_ONE_REMOVED,
    );

    const result = await runSpecGate({
      repoRoot: repo.root,
      baseRef: repo.baseRef,
      valveOpen: false,
    });

    expect(result.passed).toBe(true);
    expect(result.findings).toHaveLength(0);
  });

  it("openapi under tests schema-renamed produces no findings; counted in scope", async () => {
    const repo = makeSyntheticRepo(DEFAULT_BASELINE_FILES);
    // Mutate the fixture schema name (pure rename under tests — not contract surface).
    writeRel(
      repo.root,
      "public/contracts/svc/tests/Unit/fix.openapi.g.json",
      JSON.stringify(
        {
          openapi: "3.0.0",
          info: { title: "Fixture", version: "1.0" },
          paths: {},
          components: {
            schemas: {
              OpenApiSignInput: { type: "object" },
            },
          },
        },
        null,
        2,
      ),
    );

    const result = await runSpecGate({
      repoRoot: repo.root,
      baseRef: repo.baseRef,
      valveOpen: false,
    });

    expect(result.passed).toBe(true);
    expect(result.findings).toHaveLength(0);
    expect(result.scope.excludedOpenApiTestFiles).toContain(
      "public/contracts/svc/tests/Unit/fix.openapi.g.json",
    );
  });

  it("openapi doc at a production path with a removed path entry is BREAKING", async () => {
    const repo = makeSyntheticRepo(DEFAULT_BASELINE_FILES);
    writeRel(
      repo.root,
      "public/contracts/svc/api/svc.openapi.g.json",
      OPENAPI_PATH_REMOVED,
    );

    const result = await runSpecGate({
      repoRoot: repo.root,
      baseRef: repo.baseRef,
      valveOpen: false,
    });

    expect(result.passed).toBe(false);
    expect(
      result.findings.some(
        (f) =>
          f.arm === "openapi" &&
          f.file === "public/contracts/svc/api/svc.openapi.g.json" &&
          f.message.includes("BREAKING"),
      ),
    ).toBe(true);
  });

  it("committed openapi doc at a production path DELETED is BREAKING", async () => {
    const repo = makeSyntheticRepo(DEFAULT_BASELINE_FILES);
    unlinkSync(join(repo.root, "public/contracts/svc/api/svc.openapi.g.json"));

    const result = await runSpecGate({
      repoRoot: repo.root,
      baseRef: repo.baseRef,
      valveOpen: false,
    });

    expect(result.passed).toBe(false);
    expect(
      result.findings.some(
        (f) =>
          f.arm === "openapi" &&
          f.file === "public/contracts/svc/api/svc.openapi.g.json" &&
          f.message.includes("OpenAPI document deleted"),
      ),
    ).toBe(true);
  });

  it("removed-entry case with valveOpen true passes WITH findings", async () => {
    const repo = makeSyntheticRepo(DEFAULT_BASELINE_FILES);
    writeRel(
      repo.root,
      "public/contracts/domain/error-codes.spec.json",
      ERROR_CODES_ONE_REMOVED,
    );

    const result = await runSpecGate({
      repoRoot: repo.root,
      baseRef: repo.baseRef,
      valveOpen: true,
    });

    expect(result.passed).toBe(true);
    expect(result.findings.length).toBeGreaterThan(0);
  });
});
