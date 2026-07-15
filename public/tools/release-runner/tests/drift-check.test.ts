// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

// Tests for the baseline-drift checker (drift-check.ts).
//
// The drift check is the production DiffProvider run with the resolved-version
// map seeded to each package's CURRENT version (a no-op PR): drift ⇔ any package
// shows a non-empty diff against its committed baseline. Tests inject a synthetic
// DiffProvider that returns canned per-package diffs so the drift detection +
// reporting is asserted deterministically (no real build / api-extractor).

import { describe, expect, it } from "vitest";
import { checkBaselineDrift, formatDriftReport } from "../src/drift-check.js";
import type {
  DiffProvider,
  DiffProviderInput,
  PackageDiff,
} from "../src/diff-runner.js";
import type { PackageDescriptor } from "../src/types.js";

// ---------------------------------------------------------------------------
// Fixtures
// ---------------------------------------------------------------------------

function pkg(name: string, ecosystem: "npm" | "nuget"): PackageDescriptor {
  return {
    name,
    ecosystem,
    dir: `dir/${name}`,
    manifestPath: `/abs/${name}`,
    changelogPath: `/abs/${name}/CHANGELOG.md`,
    currentVersion: "0.1.0",
    dependencies: [],
  };
}

const noDiff: PackageDiff = {
  apiDiff: { added: false, removed: false, changed: false },
  fingerprintDiff: { changed: false },
  baselineMissing: false,
};

/**
 * A DiffProvider that returns a per-package canned diff keyed by package name.
 * Asserts the REAL drift mapping (a faithful seam — not a hollow stub).
 */
function makeProvider(diffs: Record<string, PackageDiff>): DiffProvider {
  return {
    getDiff(input: DiffProviderInput): PackageDiff {
      // The drift check seeds the resolved-version map with each package's
      // CURRENT version (no bump on a no-op PR) so the recompute matches the
      // seed's per-dep-at-committed-version composition. The package under
      // inspection must be present at its current version.
      expect(input.resolvedVersions.get(input.pkg.name)).toBe(
        input.pkg.currentVersion,
      );

      return diffs[input.pkg.name] ?? noDiff;
    },
  };
}

// ---------------------------------------------------------------------------
// checkBaselineDrift
// ---------------------------------------------------------------------------

describe("checkBaselineDrift", () => {
  it("empty package set → clean", () => {
    const result = checkBaselineDrift([], makeProvider({}));
    expect(result.clean).toBe(true);
    expect(result.drifted).toHaveLength(0);
  });

  it("all baselines current → clean, no drift", () => {
    const packages = [
      pkg("D2.Shared.Result", "nuget"),
      pkg("@d2/result", "npm"),
    ];
    const result = checkBaselineDrift(
      packages,
      makeProvider({
        "D2.Shared.Result": noDiff,
        "@d2/result": noDiff,
      }),
    );

    expect(result.clean).toBe(true);
    expect(result.results).toHaveLength(2);
    expect(result.results.every((r) => !r.drifted)).toBe(true);
    expect(result.results[0]!.detail).toBe("ok");
  });

  it("a nuget package with a fingerprint drift → FAIL, package named", () => {
    const packages = [pkg("D2.Shared.Result", "nuget")];
    const result = checkBaselineDrift(
      packages,
      makeProvider({
        "D2.Shared.Result": {
          apiDiff: { added: false, removed: false, changed: false },
          fingerprintDiff: { changed: true },
          baselineMissing: false,
        },
      }),
    );

    expect(result.clean).toBe(false);
    expect(result.drifted).toHaveLength(1);
    expect(result.drifted[0]!.name).toBe("D2.Shared.Result");
    expect(result.drifted[0]!.fingerprintDrift).toBe(true);
    expect(result.drifted[0]!.detail).toContain("fingerprint changed");
  });

  it("a TS package with an API drift → FAIL with the api axes named", () => {
    const packages = [pkg("@d2/result", "npm")];
    const result = checkBaselineDrift(
      packages,
      makeProvider({
        "@d2/result": {
          apiDiff: { added: true, removed: false, changed: true },
          fingerprintDiff: { changed: false },
          baselineMissing: false,
        },
      }),
    );

    expect(result.clean).toBe(false);
    expect(result.drifted[0]!.apiDrift).toBe(true);
    expect(result.drifted[0]!.detail).toContain("api: added+changed");
  });

  it("a missing baseline → FAIL flagged as baseline missing", () => {
    const packages = [pkg("D2.Shared.New", "nuget")];
    const result = checkBaselineDrift(
      packages,
      makeProvider({
        "D2.Shared.New": {
          apiDiff: { added: false, removed: false, changed: false },
          fingerprintDiff: { changed: true },
          baselineMissing: true,
        },
      }),
    );

    expect(result.clean).toBe(false);
    expect(result.drifted[0]!.baselineMissing).toBe(true);
    expect(result.drifted[0]!.detail).toContain("baseline missing");
  });

  it("MULTIPLE drifted packages → ALL reported (not first-fail)", () => {
    const packages = [
      pkg("D2.Shared.A", "nuget"),
      pkg("D2.Shared.B", "nuget"),
      pkg("@d2/c", "npm"),
    ];
    const result = checkBaselineDrift(
      packages,
      makeProvider({
        "D2.Shared.A": {
          apiDiff: { added: false, removed: true, changed: false },
          fingerprintDiff: { changed: true },
          baselineMissing: false,
        },
        "D2.Shared.B": noDiff,
        "@d2/c": {
          apiDiff: { added: false, removed: false, changed: false },
          fingerprintDiff: { changed: true },
          baselineMissing: false,
        },
      }),
    );

    expect(result.clean).toBe(false);
    expect(result.drifted).toHaveLength(2);
    expect(result.drifted.map((r) => r.name).sort()).toEqual([
      "@d2/c",
      "D2.Shared.A",
    ]);
  });
});

// ---------------------------------------------------------------------------
// formatDriftReport
// ---------------------------------------------------------------------------

describe("formatDriftReport", () => {
  it("clean result → 'no drift' summary", () => {
    const report = formatDriftReport({
      results: [
        {
          name: "x",
          ecosystem: "npm",
          apiDrift: false,
          fingerprintDrift: false,
          baselineMissing: false,
          drifted: false,
          detail: "ok",
        },
      ],
      drifted: [],
      clean: true,
    });

    expect(report).toContain("no drift");
    expect(report).toContain("1 package baselines are current");
  });

  it("drifted result → table naming the drifted packages", () => {
    const report = formatDriftReport({
      results: [],
      drifted: [
        {
          name: "D2.Shared.Result",
          ecosystem: "nuget",
          apiDrift: false,
          fingerprintDrift: true,
          baselineMissing: false,
          drifted: true,
          detail: "fingerprint changed",
        },
      ],
      clean: false,
    });

    expect(report).toContain("DRIFT DETECTED in 1 package(s)");
    expect(report).toContain("D2.Shared.Result | nuget | fingerprint changed");
    expect(report).toContain("Re-seed the baselines");
  });
});
