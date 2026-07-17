// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

// Unit tests for pure discovery collectors (spec / i18n / OpenAPI).
// All synthetic trees are built in per-test mkdtemp dirs — no committed
// fixtures. Baseline-union cases inject path lists (no git in this file).

import {
  mkdtempSync,
  mkdirSync,
  writeFileSync,
  rmSync,
  existsSync,
} from "node:fs";
import { join } from "node:path";
import { tmpdir } from "node:os";
import { afterEach, describe, expect, it } from "vitest";

import {
  SKIP_DIR_NAMES,
  PUBLIC_CONTRACTS_ROOT,
  PRIVATE_CONTRACTS_ROOT,
  collectOpenApiFiles,
  collectSpecFiles,
  collectI18nFiles,
  formatScopeAnnouncement,
  pathHasSkippedSegment,
  remapBaselineContractPath,
  type GateScope,
} from "../src/discovery.js";
import { repoRoot } from "./repo-root.js";

// ---------------------------------------------------------------------------
// Temp-tree helpers
// ---------------------------------------------------------------------------

const tempDirs: string[] = [];

afterEach(() => {
  while (tempDirs.length > 0) {
    const dir = tempDirs.pop();

    if (dir !== undefined) {
      rmSync(dir, { recursive: true, force: true, maxRetries: 5 });
    }
  }
});

function makeRoot(): string {
  const dir = mkdtempSync(join(tmpdir(), "cg-discovery-"));
  tempDirs.push(dir);

  return dir;
}

function writeFile(root: string, relPath: string, content = "{}\n"): void {
  const abs = join(root, relPath);
  mkdirSync(join(abs, ".."), { recursive: true });
  writeFileSync(abs, content, "utf-8");
}

// ---------------------------------------------------------------------------
// collectOpenApiFiles — input validation / error propagation
// ---------------------------------------------------------------------------

describe("collectOpenApiFiles — input validation", () => {
  it("returns empty when neither contracts nor server exists under the root", () => {
    const root = makeRoot();
    const result = collectOpenApiFiles(root);

    expect(result.files).toEqual([]);
    expect(result.excludedTestFiles).toEqual([]);
  });

  it("discovers from server when contracts is absent", () => {
    const root = makeRoot();
    writeFile(root, "public/contracts/svc/api/doc.openapi.g.json");

    const result = collectOpenApiFiles(root);

    expect(result.files).toContain(
      "public/contracts/svc/api/doc.openapi.g.json",
    );
  });

  it("discovers from contracts when server is absent", () => {
    const root = makeRoot();
    writeFile(root, "public/contracts/api/doc.openapi.g.json");

    const result = collectOpenApiFiles(root);

    expect(result.files).toContain("public/contracts/api/doc.openapi.g.json");
  });

  it("returns empty for empty directory trees", () => {
    const root = makeRoot();
    mkdirSync(join(root, "contracts"), { recursive: true });
    mkdirSync(join(root, "server"), { recursive: true });

    const result = collectOpenApiFiles(root);

    expect(result.files).toEqual([]);
  });

  it("collects only files ending exactly .openapi.g.json", () => {
    const root = makeRoot();
    writeFile(root, "public/contracts/svc/api/ok.openapi.g.json");
    writeFile(root, "public/contracts/svc/api/near.openapi.json");
    writeFile(root, "public/contracts/svc/api/near.openapi.g.jsonx");
    writeFile(root, "public/contracts/svc/api/bare.g.json");

    const result = collectOpenApiFiles(root);

    expect(result.files).toEqual([
      "public/contracts/svc/api/ok.openapi.g.json",
    ]);
  });
});

// ---------------------------------------------------------------------------
// collectOpenApiFiles — domain-specific (skip set + paths)
// ---------------------------------------------------------------------------

describe("collectOpenApiFiles — skip set and paths", () => {
  it("returns repo-relative paths with forward slashes", () => {
    const root = makeRoot();
    writeFile(root, "public/contracts/svc/api/doc.openapi.g.json");

    const result = collectOpenApiFiles(root);

    expect(result.files).toEqual([
      "public/contracts/svc/api/doc.openapi.g.json",
    ]);
    expect(result.files.every((p) => !p.includes("\\"))).toBe(true);
  });

  it("excludes any directory named tests at any depth", () => {
    const root = makeRoot();
    writeFile(root, "public/contracts/svc/tests/Unit/doc.openapi.g.json");
    writeFile(
      root,
      "public/contracts/svc/api/deep/nested/tests/x.openapi.g.json",
    );
    writeFile(root, "public/contracts/tests/fixture.openapi.g.json");
    writeFile(root, "public/contracts/svc/api/live.openapi.g.json");

    const result = collectOpenApiFiles(root);

    expect(result.files).toEqual([
      "public/contracts/svc/api/live.openapi.g.json",
    ]);
    expect(result.excludedTestFiles).toEqual(
      expect.arrayContaining([
        "public/contracts/svc/tests/Unit/doc.openapi.g.json",
        "public/contracts/svc/api/deep/nested/tests/x.openapi.g.json",
        "public/contracts/tests/fixture.openapi.g.json",
      ]),
    );
  });

  it("does not exclude directories whose names merely contain tests", () => {
    const root = makeRoot();
    writeFile(root, "public/contracts/svc/tests-data/a.openapi.g.json");
    writeFile(root, "public/contracts/svc/unit-tests/b.openapi.g.json");
    writeFile(root, "public/contracts/svc/my tests/c.openapi.g.json");

    const result = collectOpenApiFiles(root);

    expect(result.files).toEqual(
      expect.arrayContaining([
        "public/contracts/svc/tests-data/a.openapi.g.json",
        "public/contracts/svc/unit-tests/b.openapi.g.json",
        "public/contracts/svc/my tests/c.openapi.g.json",
      ]),
    );
    expect(result.files).toHaveLength(3);
  });

  it("does not exclude PascalCase Tests directories", () => {
    const root = makeRoot();
    writeFile(root, "public/contracts/svc/Tests/doc.openapi.g.json");

    const result = collectOpenApiFiles(root);

    expect(result.files).toContain(
      "public/contracts/svc/Tests/doc.openapi.g.json",
    );
  });

  it("excludes node_modules, obj, bin and .git directories", () => {
    const root = makeRoot();
    writeFile(root, "public/contracts/svc/node_modules/x/a.openapi.g.json");
    writeFile(root, "public/contracts/svc/obj/b.openapi.g.json");
    writeFile(root, "public/contracts/svc/bin/c.openapi.g.json");
    writeFile(root, "public/contracts/svc/.git/d.openapi.g.json");
    writeFile(root, "public/contracts/svc/api/live.openapi.g.json");

    const result = collectOpenApiFiles(root);

    expect(result.files).toEqual([
      "public/contracts/svc/api/live.openapi.g.json",
    ]);
    expect(result.excludedTestFiles).toEqual([]);
  });

  it("prunes package/build dirs inside tests trees without counting them", () => {
    const root = makeRoot();
    writeFile(root, "public/contracts/svc/tests/Unit/keep.openapi.g.json");
    writeFile(
      root,
      "public/contracts/svc/tests/node_modules/pkg/skip.openapi.g.json",
    );
    writeFile(root, "public/contracts/svc/tests/obj/skip.openapi.g.json");
    writeFile(root, "public/contracts/svc/api/live.openapi.g.json");

    const result = collectOpenApiFiles(root);

    expect(result.files).toEqual([
      "public/contracts/svc/api/live.openapi.g.json",
    ]);
    expect(result.excludedTestFiles).toEqual([
      "public/contracts/svc/tests/Unit/keep.openapi.g.json",
    ]);
  });

  it("does not census a non-directory entry named tests", () => {
    const root = makeRoot();
    // A file named `tests` is pruned by name but is not a directory census root.
    writeFile(root, "public/contracts/svc/tests", "not-a-dir");
    writeFile(root, "public/contracts/svc/api/live.openapi.g.json");

    const result = collectOpenApiFiles(root);

    expect(result.files).toEqual([
      "public/contracts/svc/api/live.openapi.g.json",
    ]);
    expect(result.excludedTestFiles).toEqual([]);
  });

  it("excludes baseline paths outside public/contracts/ and server/ roots", () => {
    const root = makeRoot();
    const result = collectOpenApiFiles(root, [
      "docs/not-collected.openapi.g.json",
      "public/contracts/svc/api/live.openapi.g.json",
    ]);

    // Baseline union uses the same walk roots as the WT pass (public/contracts/ +
    // server/) — a docs/ path with the openapi suffix is never a candidate.
    expect(result.files).toContain(
      "public/contracts/svc/api/live.openapi.g.json",
    );
    expect(result.files).not.toContain("docs/not-collected.openapi.g.json");
  });

  it("walks deeply nested directories", () => {
    const root = makeRoot();
    writeFile(root, "public/contracts/a/b/c/d/e/f/g/deep.openapi.g.json");

    const result = collectOpenApiFiles(root);

    expect(result.files).toContain(
      "public/contracts/a/b/c/d/e/f/g/deep.openapi.g.json",
    );
  });

  it("returns the same result set across repeated invocations", () => {
    const root = makeRoot();
    writeFile(root, "public/contracts/svc/api/doc.openapi.g.json");
    writeFile(root, "public/contracts/svc/tests/fix.openapi.g.json");

    const a = collectOpenApiFiles(root);
    const b = collectOpenApiFiles(root);

    expect(a.files).toEqual(b.files);
    expect(a.excludedTestFiles).toEqual(b.excludedTestFiles);
  });
});

// ---------------------------------------------------------------------------
// collectOpenApiFiles — real-tree regression (incident pin)
// ---------------------------------------------------------------------------

describe("collectOpenApiFiles — real repository tree", () => {
  const edgeGen =
    "private/services/edge/tests/Unit/KeyCustodian/TypeSpecOpenApi/Generated";
  const EDGE_FIXTURES = [
    `${edgeGen}/open-api-fixtures.openapi.g.json`,
    `${edgeGen}/open-api-versioned-fixtures.1-0.openapi.g.json`,
    `${edgeGen}/open-api-versioned-fixtures.2-0.openapi.g.json`,
  ] as const;

  it("does not treat edge service fixtures as contract-root candidates", () => {
    // Dual-root walks public/contracts + private/contracts only — service test
    // trees under private/services/** are outside contract roots (not candidates
    // and not tests-census under contract roots).
    for (const rel of EDGE_FIXTURES) {
      expect(
        existsSync(join(repoRoot, rel)),
        `fixture must exist on disk (non-vacuity): ${rel}`,
      ).toBe(true);
    }

    const result = collectOpenApiFiles(repoRoot);

    for (const rel of EDGE_FIXTURES) {
      expect(result.files).not.toContain(rel);
      expect(result.excludedTestFiles).not.toContain(rel);
    }
  });
});

// ---------------------------------------------------------------------------
// collectOpenApiFiles — excludedTestFiles (scope announcement census)
// ---------------------------------------------------------------------------

describe("collectOpenApiFiles — excludedTestFiles", () => {
  it("reports suffix-matching files under excluded tests trees in excludedTestFiles", () => {
    const root = makeRoot();
    writeFile(root, "public/contracts/svc/tests/Unit/a.openapi.g.json");
    writeFile(root, "public/contracts/svc/api/live.openapi.g.json");

    const result = collectOpenApiFiles(root);

    expect(result.excludedTestFiles).toContain(
      "public/contracts/svc/tests/Unit/a.openapi.g.json",
    );
    expect(result.files).toContain(
      "public/contracts/svc/api/live.openapi.g.json",
    );
  });

  it("excludedTestFiles is empty when no tests directory exists", () => {
    const root = makeRoot();
    writeFile(root, "public/contracts/svc/api/live.openapi.g.json");

    const result = collectOpenApiFiles(root);

    expect(result.excludedTestFiles).toEqual([]);
  });
});

// ---------------------------------------------------------------------------
// collectOpenApiFiles — baseline ∪ WT pure-union cases
// ---------------------------------------------------------------------------

describe("collectOpenApiFiles — baseline ∪ WT union", () => {
  it("enumerates a baseline-only path deleted from the working tree", () => {
    const root = makeRoot();
    // WT has nothing; baseline lists a production openapi path.
    const result = collectOpenApiFiles(root, [
      "public/contracts/svc/api/deleted.openapi.g.json",
    ]);

    expect(result.files).toContain(
      "public/contracts/svc/api/deleted.openapi.g.json",
    );
  });

  it("enumerates a WT-only (additive) path", () => {
    const root = makeRoot();
    writeFile(root, "public/contracts/svc/api/new.openapi.g.json");

    const result = collectOpenApiFiles(root, []);

    expect(result.files).toContain(
      "public/contracts/svc/api/new.openapi.g.json",
    );
  });

  it("enumerates the union when a path is on both sides", () => {
    const root = makeRoot();
    writeFile(root, "public/contracts/svc/api/both.openapi.g.json");

    const result = collectOpenApiFiles(root, [
      "public/contracts/svc/api/both.openapi.g.json",
    ]);

    expect(
      result.files.filter(
        (p) => p === "public/contracts/svc/api/both.openapi.g.json",
      ),
    ).toHaveLength(1);
  });

  it("excludes a baseline-tracked path under a tests directory", () => {
    const root = makeRoot();
    const result = collectOpenApiFiles(root, [
      "public/contracts/svc/tests/Unit/fix.openapi.g.json",
    ]);

    expect(result.files).not.toContain(
      "public/contracts/svc/tests/Unit/fix.openapi.g.json",
    );
  });

  it("excludes a baseline-tracked path under node_modules (and obj/bin/.git)", () => {
    const root = makeRoot();
    const result = collectOpenApiFiles(root, [
      "public/contracts/svc/node_modules/x/a.openapi.g.json",
      "public/contracts/svc/obj/b.openapi.g.json",
      "public/contracts/svc/bin/c.openapi.g.json",
      "public/contracts/svc/.git/d.openapi.g.json",
      "public/contracts/svc/api/live.openapi.g.json",
    ]);

    expect(result.files).toEqual([
      "public/contracts/svc/api/live.openapi.g.json",
    ]);
  });
});

// ---------------------------------------------------------------------------
// collectSpecFiles
// ---------------------------------------------------------------------------

describe("collectSpecFiles", () => {
  it("excludes any directory named tests under contracts", () => {
    const root = makeRoot();
    writeFile(root, "public/contracts/domain/tests/x.spec.json");
    writeFile(root, "public/contracts/domain/deep/nested/tests/y.spec.json");
    writeFile(root, "public/contracts/domain/live.spec.json");

    const result = collectSpecFiles(root);

    expect(result.files).toEqual(["public/contracts/domain/live.spec.json"]);
    expect(result.excludedTestFiles).toEqual(
      expect.arrayContaining([
        "public/contracts/domain/tests/x.spec.json",
        "public/contracts/domain/deep/nested/tests/y.spec.json",
      ]),
    );
  });

  it("excludes node_modules directories under contracts", () => {
    const root = makeRoot();
    writeFile(root, "public/contracts/typespec/node_modules/pkg/x.spec.json");
    writeFile(root, "public/contracts/domain/live.spec.json");

    const result = collectSpecFiles(root);

    expect(result.files).toEqual(["public/contracts/domain/live.spec.json"]);
  });

  it("discovers spec files in non-excluded contracts directories", () => {
    const root = makeRoot();
    writeFile(root, "public/contracts/error-codes/error-codes.spec.json");

    const result = collectSpecFiles(root);

    expect(result.files).toContain(
      "public/contracts/error-codes/error-codes.spec.json",
    );
    expect(result.files.every((p) => !p.includes("\\"))).toBe(true);
  });

  it("collects only files ending exactly .spec.json", () => {
    const root = makeRoot();
    writeFile(root, "public/contracts/domain/ok.spec.json");
    writeFile(root, "public/contracts/domain/near.spec.jsonx");
    writeFile(root, "public/contracts/domain/x-spec.json");

    const result = collectSpecFiles(root);

    expect(result.files).toEqual(["public/contracts/domain/ok.spec.json"]);
  });

  it("returns empty when contracts does not exist under the root", () => {
    const root = makeRoot();
    const result = collectSpecFiles(root);

    expect(result.files).toEqual([]);
  });

  it("does not exclude directories whose names merely contain tests", () => {
    const root = makeRoot();
    writeFile(root, "public/contracts/tests-data/a.spec.json");
    writeFile(root, "public/contracts/unit-tests/b.spec.json");

    const result = collectSpecFiles(root);

    expect(result.files).toEqual(
      expect.arrayContaining([
        "public/contracts/tests-data/a.spec.json",
        "public/contracts/unit-tests/b.spec.json",
      ]),
    );
  });

  it("enumerates a baseline-only path deleted from the working tree", () => {
    const root = makeRoot();
    const result = collectSpecFiles(root, [
      "public/contracts/domain/error-codes.spec.json",
    ]);

    expect(result.files).toContain(
      "public/contracts/domain/error-codes.spec.json",
    );
  });

  it("excludes a baseline-tracked path under a tests directory", () => {
    const root = makeRoot();
    const result = collectSpecFiles(root, [
      "public/contracts/domain/tests/x.spec.json",
    ]);

    expect(result.files).not.toContain(
      "public/contracts/domain/tests/x.spec.json",
    );
  });

  it("ignores baseline paths outside public/contracts/", () => {
    const root = makeRoot();
    const result = collectSpecFiles(root, [
      "server/elsewhere/error-codes.spec.json",
      "public/contracts/domain/live.spec.json",
    ]);

    expect(result.files).toEqual(["public/contracts/domain/live.spec.json"]);
  });
});

// ---------------------------------------------------------------------------
// collectI18nFiles
// ---------------------------------------------------------------------------

describe("collectI18nFiles", () => {
  it("collects locale .json under public/contracts/messages/", () => {
    const root = makeRoot();
    writeFile(root, "public/contracts/messages/en-US.json", '{"a":"b"}');
    writeFile(root, "public/contracts/messages/fr-FR.json", '{"a":"b"}');

    const result = collectI18nFiles(root);

    expect(result.files).toEqual(
      expect.arrayContaining([
        "public/contracts/messages/en-US.json",
        "public/contracts/messages/fr-FR.json",
      ]),
    );
    expect(result.files).toHaveLength(2);
  });

  it("skips $schema-style locale names", () => {
    const root = makeRoot();
    writeFile(root, "public/contracts/messages/$schema.json", "{}");
    writeFile(root, "public/contracts/messages/en-US.json", "{}");

    const result = collectI18nFiles(root);

    expect(result.files).toEqual(["public/contracts/messages/en-US.json"]);
  });

  it("baseline-only locale when messages dir is absent", () => {
    const root = makeRoot();
    // No public/contracts/messages/ on WT.
    const result = collectI18nFiles(root, [
      "public/contracts/messages/en-US.json",
      "public/contracts/messages/$schema.json",
    ]);

    expect(result.files).toEqual(["public/contracts/messages/en-US.json"]);
  });

  it("enumerates the union when a locale is on both sides", () => {
    const root = makeRoot();
    writeFile(root, "public/contracts/messages/en-US.json", "{}");

    const result = collectI18nFiles(root, [
      "public/contracts/messages/en-US.json",
    ]);

    expect(
      result.files.filter((p) => p === "public/contracts/messages/en-US.json"),
    ).toHaveLength(1);
  });

  it("skips $schema-style names on the baseline side", () => {
    const root = makeRoot();
    const result = collectI18nFiles(root, [
      "public/contracts/messages/$schema.json",
      "public/contracts/messages/en-US.json",
    ]);

    expect(result.files).toEqual(["public/contracts/messages/en-US.json"]);
  });

  it("excludedTestFiles is always empty for the flat i18n layout", () => {
    const root = makeRoot();
    writeFile(root, "public/contracts/messages/en-US.json", "{}");

    const result = collectI18nFiles(root);

    expect(result.excludedTestFiles).toEqual([]);
  });

  it("treats a non-directory messages path as empty (readdir degrades)", () => {
    const root = makeRoot();
    // public/contracts/messages as a FILE — existsSync true, readdir throws → empty.
    mkdirSync(join(root, "contracts"), { recursive: true });
    writeFileSync(join(root, "contracts", "messages"), "not-a-dir", "utf-8");

    const result = collectI18nFiles(root, [
      "public/contracts/messages/en-US.json",
    ]);

    // WT side empty; baseline still enumerates.
    expect(result.files).toEqual(["public/contracts/messages/en-US.json"]);
  });

  it("ignores nested paths under messages on the baseline side", () => {
    const root = makeRoot();
    const result = collectI18nFiles(root, [
      "public/contracts/messages/nested/en-US.json",
      "public/contracts/messages/en-US.json",
    ]);

    expect(result.files).toEqual(["public/contracts/messages/en-US.json"]);
  });

  it("ignores baseline paths outside public/contracts/messages/", () => {
    const root = makeRoot();
    const result = collectI18nFiles(root, [
      "public/contracts/other/en-US.json",
      "server/messages/en-US.json",
      "public/contracts/messages/en-US.json",
    ]);

    expect(result.files).toEqual(["public/contracts/messages/en-US.json"]);
  });

  it("returns the same result set across repeated invocations", () => {
    const root = makeRoot();
    writeFile(root, "public/contracts/messages/en-US.json", '{"a":"b"}');
    writeFile(root, "public/contracts/messages/$schema.json", "{}");

    const a = collectI18nFiles(root, ["public/contracts/messages/fr-FR.json"]);
    const b = collectI18nFiles(root, ["public/contracts/messages/fr-FR.json"]);

    expect(a.files).toEqual(b.files);
    expect(a.excludedTestFiles).toEqual(b.excludedTestFiles);
  });
});

// ---------------------------------------------------------------------------
// formatScopeAnnouncement + pathHasSkippedSegment
// ---------------------------------------------------------------------------

describe("formatScopeAnnouncement", () => {
  it("renders the skip set and per-arm counts", () => {
    const scope: GateScope = {
      skipDirs: SKIP_DIR_NAMES,
      excludedSpecTestFiles: ["public/contracts/x/tests/a.spec.json"],
      excludedOpenApiTestFiles: [
        "public/contracts/s/tests/a.openapi.g.json",
        "public/contracts/s/tests/b.openapi.g.json",
      ],
    };

    const line = formatScopeAnnouncement(scope);

    expect(line).toBe(
      "  Discovery scope: skip [node_modules, obj, bin, .git, tests]; " +
        "excluded under tests — spec: 1, openapi: 2",
    );
  });

  it("renders zero counts when no tests trees hold matching files", () => {
    const scope: GateScope = {
      skipDirs: SKIP_DIR_NAMES,
      excludedSpecTestFiles: [],
      excludedOpenApiTestFiles: [],
    };

    const line = formatScopeAnnouncement(scope);

    expect(line).toBe(
      "  Discovery scope: skip [node_modules, obj, bin, .git, tests]; " +
        "excluded under tests — spec: 0, openapi: 0",
    );
  });
});

describe("pathHasSkippedSegment", () => {
  it("detects a tests segment at any depth", () => {
    expect(
      pathHasSkippedSegment("public/contracts/svc/tests/Unit/x.json"),
    ).toBe(true);
    expect(pathHasSkippedSegment("public/contracts/svc/api/x.json")).toBe(
      false,
    );
  });

  it("detects package/build skip segments", () => {
    expect(pathHasSkippedSegment("a/node_modules/b")).toBe(true);
    expect(pathHasSkippedSegment("a/obj/b")).toBe(true);
    expect(pathHasSkippedSegment("a/bin/b")).toBe(true);
    expect(pathHasSkippedSegment("a/.git/b")).toBe(true);
  });
});

// ---------------------------------------------------------------------------
// Dual roots + baseline continuity (X11) — named matrix
// ---------------------------------------------------------------------------

describe("ContractGate_DiscoversDualRoots", () => {
  it("unions public and private contract trees on combined mode", () => {
    const root = makeRoot();
    writeFile(root, `${PUBLIC_CONTRACTS_ROOT}/error-codes/a.spec.json`);
    writeFile(
      root,
      `${PRIVATE_CONTRACTS_ROOT}/keycustodian-error-codes/b.spec.json`,
    );

    const result = collectSpecFiles(root);

    expect(result.files).toEqual(
      expect.arrayContaining([
        `${PUBLIC_CONTRACTS_ROOT}/error-codes/a.spec.json`,
        `${PRIVATE_CONTRACTS_ROOT}/keycustodian-error-codes/b.spec.json`,
      ]),
    );
  });
});

describe("ContractGate_PrivateOnlySpecChange_DetectedOnCombined", () => {
  it("includes a private-only WT spec on combined discovery", () => {
    const root = makeRoot();
    writeFile(
      root,
      `${PRIVATE_CONTRACTS_ROOT}/advisory-locks/advisory-locks.spec.json`,
    );

    const combined = collectSpecFiles(root);
    expect(combined.files).toContain(
      `${PRIVATE_CONTRACTS_ROOT}/advisory-locks/advisory-locks.spec.json`,
    );
  });
});

describe("ContractGate_PublicOnlyMode_IgnoresPrivate", () => {
  it("excludes private/contracts when publicOnly is true", () => {
    const root = makeRoot();
    writeFile(root, `${PUBLIC_CONTRACTS_ROOT}/error-codes/a.spec.json`);
    writeFile(
      root,
      `${PRIVATE_CONTRACTS_ROOT}/keycustodian-error-codes/b.spec.json`,
    );

    const result = collectSpecFiles(root, undefined, { publicOnly: true });

    expect(result.files).toContain(
      `${PUBLIC_CONTRACTS_ROOT}/error-codes/a.spec.json`,
    );
    expect(result.files).not.toContain(
      `${PRIVATE_CONTRACTS_ROOT}/keycustodian-error-codes/b.spec.json`,
    );
  });
});

describe("ContractGate_MovedPathBaseline_NoFalseFullCatalogBreak", () => {
  it("remaps legacy contracts/** baseline paths to public/contracts/**", () => {
    expect(
      remapBaselineContractPath("contracts/error-codes/error-codes.spec.json"),
    ).toBe("public/contracts/error-codes/error-codes.spec.json");
    expect(remapBaselineContractPath("public/contracts/x.spec.json")).toBe(
      "public/contracts/x.spec.json",
    );
  });

  it("joins legacy baseline path with modern WT path as one candidate", () => {
    const root = makeRoot();
    writeFile(root, "public/contracts/domain/live.spec.json");

    // Pre-reorg baseline path — remapped for identity join.
    const result = collectSpecFiles(root, ["contracts/domain/live.spec.json"]);

    expect(
      result.files.filter(
        (p) => p === "public/contracts/domain/live.spec.json",
      ),
    ).toHaveLength(1);
  });

  it("enumerates remapped baseline-only path without inventing a second identity", () => {
    const root = makeRoot();
    const result = collectSpecFiles(root, [
      "contracts/domain/deleted.spec.json",
    ]);

    expect(result.files).toContain("public/contracts/domain/deleted.spec.json");
    expect(result.files).not.toContain("contracts/domain/deleted.spec.json");
  });
});

describe("CombinedSuite_DiscoversDualContractRoots", () => {
  it("discovers real monorepo public + private specs", () => {
    const result = collectSpecFiles(repoRoot);
    const hasPublic = result.files.some((p) =>
      p.startsWith(`${PUBLIC_CONTRACTS_ROOT}/`),
    );
    const hasPrivate = result.files.some((p) =>
      p.startsWith(`${PRIVATE_CONTRACTS_ROOT}/`),
    );

    expect(hasPublic).toBe(true);
    expect(hasPrivate).toBe(true);
  });
});
