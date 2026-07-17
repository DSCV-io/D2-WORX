// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

// CLI-flip regression pins.
//
// cli.ts is coverage-excluded (process.exit / process.argv), so the bump-source
// flip is pinned at the unit level: the default bump path constructs the
// ARTIFACT-DIFF provider (makeRealDiffProvider) and routes through runDiffRelease,
// NOT the commit-type runRelease. These tests prove the wiring the CLI default
// now uses is the artifact-diff engine.
//
// Also covers the --legacy-commit-type flag: when set, runRelease (not
// runDiffRelease) must be the active bump source.

import { describe, expect, it } from "vitest";
import * as runnerIndex from "../src/index.js";
import { makeRealDiffProvider } from "../src/real-diff-provider.js";
import { runDiffRelease } from "../src/diff-runner.js";
import { runRelease } from "../src/runner.js";
import { repoRoot } from "./repo-root.js";
import type {
  DiffProvider,
  DiffProviderInput,
  PackageDiff,
} from "../src/diff-runner.js";
import type { PackageDescriptor } from "../src/types.js";

describe("CLI default-path wiring (artifact-diff engine)", () => {
  it("makeRealDiffProvider returns a DiffProvider with a getDiff function", () => {
    const provider = makeRealDiffProvider(repoRoot);

    expect(typeof provider.getDiff).toBe("function");
  });

  it("index.ts re-exports makeRealDiffProvider + runDiffRelease + the drift check (resolvability)", () => {
    // §1.3-style seam resolvability: the public surface the CLI imports must be
    // present + callable from the barrel.
    expect(typeof runnerIndex.makeRealDiffProvider).toBe("function");
    expect(typeof runnerIndex.runDiffRelease).toBe("function");
    expect(typeof runnerIndex.checkBaselineDrift).toBe("function");
    expect(typeof runnerIndex.composeSourceFingerprint).toBe("function");
  });

  it("the default path drives the engine through the provider (not computeBumpPlans)", () => {
    // Prove that runDiffRelease consults the injected DiffProvider — i.e. the
    // artifact diff is the bump source — by asserting the provider IS called and
    // its diff drives the bump.
    const calls: string[] = [];

    const provider: DiffProvider = {
      getDiff(input: DiffProviderInput): PackageDiff {
        calls.push(input.pkg.name);

        return {
          // An added public member → MINOR (diff-derived, not commit-type-derived).
          apiDiff: { added: true, removed: false, changed: false },
          fingerprintDiff: { changed: true },
          baselineMissing: false,
        };
      },
    };

    const pkg: PackageDescriptor = {
      name: "DcsvIo.D2.Result",
      ecosystem: "nuget",
      dir: "public/packages/dotnet/result/core",
      manifestPath: "/abs/result/core/DcsvIo.D2.Result.csproj",
      changelogPath: "/abs/result/core/CHANGELOG.md",
      currentVersion: "0.1.0",
      dependencies: [],
    };

    const result = runDiffRelease(
      // No commits at all — the bump must come from the DIFF, not a commit type.
      [],
      [pkg],
      { today: "2026-06-25", dryRun: true, propagate: true },
      provider,
    );

    expect(calls).toEqual(["DcsvIo.D2.Result"]);
    expect(result.plans).toHaveLength(1);
    expect(result.plans[0]!.bump).toBe("minor");
    expect(result.plans[0]!.newVersion).toBe("0.2.0");
  });
});

// ---------------------------------------------------------------------------
// --legacy-commit-type routing
// ---------------------------------------------------------------------------

describe("--legacy-commit-type routing (unit)", () => {
  const pkg: PackageDescriptor = {
    name: "@dcsv-io/d2-utilities",
    ecosystem: "npm",
    dir: "public/packages/typescript/utilities",
    manifestPath: "public/packages/typescript/utilities/package.json",
    changelogPath: "public/packages/typescript/utilities/CHANGELOG.md",
    currentVersion: "0.1.0",
    dependencies: [],
  };

  it("WITHOUT --legacy-commit-type: runDiffRelease is the bump source (diff drives bump)", () => {
    // The default path routes through runDiffRelease, which consults the
    // DiffProvider. An added API member with no commit drives a MINOR bump.
    const diffCalls: string[] = [];

    const provider: DiffProvider = {
      getDiff(input: DiffProviderInput): PackageDiff {
        diffCalls.push(input.pkg.name);

        return {
          apiDiff: { added: true, removed: false, changed: false },
          fingerprintDiff: { changed: true },
          baselineMissing: false,
        };
      },
    };

    const result = runDiffRelease(
      [],
      [pkg],
      { today: "2026-06-25", dryRun: true, propagate: true },
      provider,
    );

    // Provider was consulted — diff is the bump source.
    expect(diffCalls).toContain("@dcsv-io/d2-utilities");
    expect(result.plans).toHaveLength(1);
    expect(result.plans[0]!.bump).toBe("minor");
  });

  it("WITH --legacy-commit-type: runRelease is the bump source (commit type drives bump)", () => {
    // The legacy path routes through runRelease, which uses commit-type parsing.
    // A feat: commit produces MINOR; no DiffProvider is consulted.
    const commitRecord = {
      message: "feat(@dcsv-io/d2-utilities): add new helper\n\nSome detail.",
      files: ["public/packages/typescript/utilities/src/index.ts"],
    };

    const result = runRelease([commitRecord], [pkg], {
      today: "2026-06-25",
      dryRun: true,
      propagate: true,
    });

    // runRelease does not use a DiffProvider — bump comes from commit type.
    expect(result.plans).toHaveLength(1);
    expect(result.plans[0]!.bump).toBe("minor");
  });

  it("legacy path is accessible via the public index", () => {
    expect(typeof runnerIndex.runRelease).toBe("function");
  });
});
