// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";

import {
  computeBumpPlans,
  type BreakingSignal,
  type BreakingSignalProvider,
} from "../src/bump-engine.js";
import type { CommitRecord, PackageDescriptor } from "../src/types.js";

// ---------------------------------------------------------------------------
// Fixtures
// ---------------------------------------------------------------------------

function makeNpmPkg(
  name: string,
  dir: string,
  version = "0.1.0",
): PackageDescriptor {
  return {
    name,
    ecosystem: "npm",
    dir,
    manifestPath: `${dir}/package.json`,
    changelogPath: `${dir}/CHANGELOG.md`,
    currentVersion: version,
  };
}

function makeNugetPkg(
  name: string,
  dir: string,
  version = "0.1.0",
): PackageDescriptor {
  return {
    name,
    ecosystem: "nuget",
    dir,
    manifestPath: `${dir}/${name}.csproj`,
    changelogPath: `${dir}/CHANGELOG.md`,
    currentVersion: version,
  };
}

function makeCommit(message: string, files: string[]): CommitRecord {
  return { message, files };
}

// Helper to build a conventional commit message with a footer block.
function commitWithFooter(
  subject: string,
  body: string,
  footer: string,
): string {
  return `${subject}\n\n${body}\n\n${footer}`;
}

// Injectable signal provider: returns a fixed BreakingSignal for any message.
function fixedSignal(signal: BreakingSignal): BreakingSignalProvider {
  return (_msg) => signal;
}

// ---------------------------------------------------------------------------
// Empty / no-op input
// ---------------------------------------------------------------------------

describe("computeBumpPlans — empty input", () => {
  it("returns empty array for no commits", () => {
    const pkgs = [makeNpmPkg("@d2/foo", "server/shared/typescript/foo")];
    expect(computeBumpPlans([], pkgs)).toHaveLength(0);
  });

  it("returns empty array for no packages", () => {
    const commits = [
      makeCommit("feat: add something", [
        "server/shared/typescript/foo/src/index.ts",
      ]),
    ];
    expect(computeBumpPlans(commits, [])).toHaveLength(0);
  });

  it("returns no plans when no commit touches a consumable package", () => {
    const pkgs = [makeNpmPkg("@d2/foo", "server/shared/typescript/foo")];
    const commits = [
      makeCommit("feat: add CI config", ["infra/ci/workflow.yml"]),
    ];
    expect(computeBumpPlans(commits, pkgs)).toHaveLength(0);
  });

  it("returns no plans for commits with unrecognized types (no file match)", () => {
    const pkgs = [makeNpmPkg("@d2/foo", "server/shared/typescript/foo")];
    const commits = [makeCommit("chore: cleanup", ["docs/README.md"])];
    expect(computeBumpPlans(commits, pkgs)).toHaveLength(0);
  });

  it("returns no plans for commits with unrecognized types even when files touch a package", () => {
    const pkgs = [makeNpmPkg("@d2/foo", "server/shared/typescript/foo")];
    const commits = [
      makeCommit("chore: update comments", [
        "server/shared/typescript/foo/src/index.ts",
      ]),
    ];
    expect(computeBumpPlans(commits, pkgs)).toHaveLength(0);
  });
});

// ---------------------------------------------------------------------------
// Pre-stable (0.x) bump rules
// ---------------------------------------------------------------------------

describe("computeBumpPlans — pre-stable (0.x)", () => {
  const pkg = makeNpmPkg(
    "@d2/result",
    "server/shared/typescript/result",
    "0.1.0",
  );

  it("0.x wire-breaking → MINOR (no valve required)", () => {
    const commits = [
      makeCommit(
        commitWithFooter(
          "feat: remove proto field",
          "Breaking wire change.",
          "WIRE-BREAKING: removed field 3 from FooRequest",
        ),
        ["server/shared/typescript/result/src/result.ts"],
      ),
    ];

    const plans = computeBumpPlans(commits, [pkg]);
    expect(plans).toHaveLength(1);
    expect(plans[0]?.bump).toBe("minor");
    expect(plans[0]?.newVersion).toBe("0.2.0");
    expect(plans[0]?.wireBreakingEntries).toEqual([
      "removed field 3 from FooRequest",
    ]);
  });

  it("0.x api-breaking → MINOR", () => {
    const commits = [
      makeCommit(
        commitWithFooter(
          "feat: remove error code",
          "Breaking api change.",
          "BREAKING CHANGE: removed DEPRECATED_CODE from catalog",
        ),
        ["server/shared/typescript/result/src/codes.ts"],
      ),
    ];

    const plans = computeBumpPlans(commits, [pkg]);
    expect(plans).toHaveLength(1);
    expect(plans[0]?.bump).toBe("minor");
    expect(plans[0]?.newVersion).toBe("0.2.0");
    expect(plans[0]?.apiBreakingEntries).toEqual([
      "removed DEPRECATED_CODE from catalog",
    ]);
  });

  it("0.x feat → MINOR", () => {
    const commits = [
      makeCommit("feat: add ok factory", [
        "server/shared/typescript/result/src/ok.ts",
      ]),
    ];

    const plans = computeBumpPlans(commits, [pkg]);
    expect(plans).toHaveLength(1);
    expect(plans[0]?.bump).toBe("minor");
    expect(plans[0]?.newVersion).toBe("0.2.0");
    expect(plans[0]?.addedEntries).toEqual(["add ok factory"]);
  });

  it("0.x fix → PATCH", () => {
    const commits = [
      makeCommit("fix: correct null check", [
        "server/shared/typescript/result/src/result.ts",
      ]),
    ];

    const plans = computeBumpPlans(commits, [pkg]);
    expect(plans).toHaveLength(1);
    expect(plans[0]?.bump).toBe("patch");
    expect(plans[0]?.newVersion).toBe("0.1.1");
    expect(plans[0]?.fixedEntries).toEqual(["correct null check"]);
  });

  it("0.x perf → PATCH", () => {
    const commits = [
      makeCommit("perf: optimize hot path", [
        "server/shared/typescript/result/src/result.ts",
      ]),
    ];

    const plans = computeBumpPlans(commits, [pkg]);
    expect(plans).toHaveLength(1);
    expect(plans[0]?.bump).toBe("patch");
    expect(plans[0]?.newVersion).toBe("0.1.1");
    expect(plans[0]?.fixedEntries).toEqual(["optimize hot path"]);
  });

  it("0.x break does NOT require the force valve (no error thrown)", () => {
    // The footer itself IS the valve. Having a breaking footer on 0.x is fine.
    const commits = [
      makeCommit(
        commitWithFooter(
          "feat: big change",
          "Wire break.",
          "WIRE-BREAKING: protocol changed",
        ),
        ["server/shared/typescript/result/src/index.ts"],
      ),
    ];

    expect(() => computeBumpPlans(commits, [pkg])).not.toThrow();
    const plans = computeBumpPlans(commits, [pkg]);
    expect(plans[0]?.bump).toBe("minor"); // MINOR, not MAJOR, for 0.x
  });
});

// ---------------------------------------------------------------------------
// Stable (≥1.0.0) bump rules
// ---------------------------------------------------------------------------

describe("computeBumpPlans — stable (≥1.0.0)", () => {
  const stablePkg = makeNpmPkg(
    "@d2/result",
    "server/shared/typescript/result",
    "1.2.3",
  );

  it("≥1 wire-breaking WITH valve → MAJOR", () => {
    const commits = [
      makeCommit(
        commitWithFooter(
          "feat: remove field",
          "Breaking.",
          "WIRE-BREAKING: removed field 3 from FooRequest",
        ),
        ["server/shared/typescript/result/src/result.ts"],
      ),
    ];

    const plans = computeBumpPlans(commits, [stablePkg]);
    expect(plans).toHaveLength(1);
    expect(plans[0]?.bump).toBe("major");
    expect(plans[0]?.newVersion).toBe("2.0.0");
  });

  it("≥1 api-breaking WITH valve → MAJOR", () => {
    const commits = [
      makeCommit(
        commitWithFooter(
          "feat: drop scope",
          "Breaking.",
          "BREAKING CHANGE: removed admin scope",
        ),
        ["server/shared/typescript/result/src/result.ts"],
      ),
    ];

    const plans = computeBumpPlans(commits, [stablePkg]);
    expect(plans[0]?.bump).toBe("major");
    expect(plans[0]?.newVersion).toBe("2.0.0");
    expect(plans[0]?.apiBreakingEntries).toEqual(["removed admin scope"]);
  });

  it("≥1 feat → MINOR (not MAJOR)", () => {
    const commits = [
      makeCommit("feat: add helper", [
        "server/shared/typescript/result/src/helpers.ts",
      ]),
    ];

    const plans = computeBumpPlans(commits, [stablePkg]);
    expect(plans[0]?.bump).toBe("minor");
    expect(plans[0]?.newVersion).toBe("1.3.0");
  });

  it("≥1 fix → PATCH", () => {
    const commits = [
      makeCommit("fix: edge case in serializer", [
        "server/shared/typescript/result/src/result.ts",
      ]),
    ];

    const plans = computeBumpPlans(commits, [stablePkg]);
    expect(plans[0]?.bump).toBe("patch");
    expect(plans[0]?.newVersion).toBe("1.2.4");
  });

  it("≥1 break WITHOUT valve (forced=false, break entries present) → ERROR (fail-loud)", () => {
    // Use a synthetic signal provider to simulate "break entries present, forced=false".
    // This is structurally impossible from parseBreakingFooters but the engine
    // enforces it independently.
    const breakWithoutValve: BreakingSignalProvider = fixedSignal({
      forced: false,
      wireBreaking: ["some breaking change"],
      apiBreaking: [],
    });

    const commits = [
      makeCommit("feat: some change", [
        "server/shared/typescript/result/src/result.ts",
      ]),
    ];

    expect(() =>
      computeBumpPlans(commits, [stablePkg], breakWithoutValve),
    ).toThrow(/force valve/);
  });

  it("≥1 break WITHOUT valve on PRE-STABLE package → no error (only stable packages throw)", () => {
    const preStablePkg = makeNpmPkg(
      "@d2/foo",
      "server/shared/typescript/foo",
      "0.5.0",
    );
    const breakWithoutValve: BreakingSignalProvider = fixedSignal({
      forced: false,
      wireBreaking: ["a change"],
      apiBreaking: [],
    });

    const commits = [
      makeCommit("feat: something", [
        "server/shared/typescript/foo/src/index.ts",
      ]),
    ];

    // Pre-stable: break-without-valve is NOT an error (no valve requirement).
    // But with forced=false, the commit is treated as a normal non-breaking
    // commit by the engine (break entries are ignored when forced=false).
    expect(() =>
      computeBumpPlans(commits, [preStablePkg], breakWithoutValve),
    ).not.toThrow();
  });
});

// ---------------------------------------------------------------------------
// Aggregation — highest bump wins
// ---------------------------------------------------------------------------

describe("computeBumpPlans — aggregation (highest bump wins)", () => {
  const pkg = makeNpmPkg("@d2/foo", "server/shared/typescript/foo", "0.1.0");
  const file = "server/shared/typescript/foo/src/index.ts";

  it("feat + fix → MINOR (feat wins over patch)", () => {
    const commits = [
      makeCommit("feat: add feature", [file]),
      makeCommit("fix: fix bug", [file]),
    ];

    const plans = computeBumpPlans(commits, [pkg]);
    expect(plans[0]?.bump).toBe("minor");
    expect(plans[0]?.newVersion).toBe("0.2.0");
    expect(plans[0]?.addedEntries).toContain("add feature");
    expect(plans[0]?.fixedEntries).toContain("fix bug");
  });

  it("break + feat → MINOR (break wins, both produce minor for 0.x)", () => {
    const commits = [
      makeCommit(
        commitWithFooter(
          "feat: drop field",
          "Body.",
          "WIRE-BREAKING: removed field",
        ),
        [file],
      ),
      makeCommit("feat: add helper", [file]),
    ];

    const plans = computeBumpPlans(commits, [pkg]);
    expect(plans[0]?.bump).toBe("minor");
    expect(plans[0]?.wireBreakingEntries).toContain("removed field");
    expect(plans[0]?.addedEntries).toContain("add helper");
  });

  it("fix + fix → PATCH (same level)", () => {
    const commits = [
      makeCommit("fix: bug one", [file]),
      makeCommit("fix: bug two", [file]),
    ];

    const plans = computeBumpPlans(commits, [pkg]);
    expect(plans[0]?.bump).toBe("patch");
    expect(plans[0]?.fixedEntries).toEqual(["bug one", "bug two"]);
  });

  it("stable: break + feat → MAJOR (break wins)", () => {
    const stablePkg = makeNpmPkg(
      "@d2/foo",
      "server/shared/typescript/foo",
      "1.0.0",
    );
    const commits = [
      makeCommit(
        commitWithFooter(
          "feat: drop field",
          "Body.",
          "WIRE-BREAKING: dropped field",
        ),
        [file],
      ),
      makeCommit("feat: add helper", [file]),
    ];

    const plans = computeBumpPlans(commits, [stablePkg]);
    expect(plans[0]?.bump).toBe("major");
    expect(plans[0]?.newVersion).toBe("2.0.0");
  });
});

// ---------------------------------------------------------------------------
// Path-containment mapping
// ---------------------------------------------------------------------------

describe("computeBumpPlans — path-containment mapping", () => {
  it("file in package subtree maps to that package", () => {
    const pkgA = makeNpmPkg("@d2/a", "server/shared/typescript/a");
    const pkgB = makeNpmPkg("@d2/b", "server/shared/typescript/b");
    const commits = [
      makeCommit("feat: change in a", [
        "server/shared/typescript/a/src/index.ts",
      ]),
    ];

    const plans = computeBumpPlans(commits, [pkgA, pkgB]);
    expect(plans).toHaveLength(1);
    expect(plans[0]?.pkg.name).toBe("@d2/a");
  });

  it("file in non-consumable path (tooling) maps to nothing — no bump", () => {
    const pkg = makeNpmPkg("@d2/a", "server/shared/typescript/a");
    const commits = [
      makeCommit("feat: update CI script", ["tools/scripts/deploy.sh"]),
    ];

    expect(computeBumpPlans(commits, [pkg])).toHaveLength(0);
  });

  it("file under service path (non-consumable) maps to nothing", () => {
    const pkg = makeNpmPkg("@d2/a", "server/shared/typescript/a");
    const commits = [
      makeCommit("feat: service change", ["server/services/edge/src/main.ts"]),
    ];

    expect(computeBumpPlans(commits, [pkg])).toHaveLength(0);
  });

  it("SourceGen shell path maps to HOST consumable (longest-prefix wins)", () => {
    // The source-gen shell is under the host's directory tree.
    // The shell's dir is not in the consumable index; the host dir is.
    // "Longest prefix wins" means if host dir is a prefix of the file,
    // the host wins (even if the shell dir would be more specific — but
    // the shell is NOT in the consumable index, so only the host matches).
    const hostPkg = makeNugetPkg(
      "D2.Shared.Result",
      "server/shared/dotnet/result/core",
      "0.1.0",
    );
    const commits = [
      // File is inside the SourceGen shell directory, which sits beside the host dir.
      // The shell dir is NOT in the consumable index.
      // Only the host's "server/shared/dotnet/result" tree would match if we used
      // a broader dir. Here we use the exact shell subdir path.
      makeCommit("fix: codegen fix", [
        "server/shared/dotnet/result/core/Generated/ErrorCodes.g.cs",
      ]),
    ];

    const plans = computeBumpPlans(commits, [hostPkg]);
    expect(plans).toHaveLength(1);
    expect(plans[0]?.pkg.name).toBe("D2.Shared.Result");
  });

  it("SourceGen shell in its own sibling directory does NOT map to host if shell dir is a separate subtree", () => {
    // Shell and host are sibling directories: result/core and result/envelope-source-gen.
    // If only the host is in the consumable list, changes to the shell (which is NOT
    // in the list) produce no bump for the host (because the shell's dir is
    // "result/envelope-source-gen" which is NOT a prefix of the host's
    // "result/core" path).
    const hostPkg = makeNugetPkg(
      "D2.Shared.Result",
      "server/shared/dotnet/result/core",
      "0.1.0",
    );
    const commits = [
      makeCommit("fix: generator fix", [
        "server/shared/dotnet/result/envelope-source-gen/Generator.cs",
      ]),
    ];

    // The shell path "result/envelope-source-gen" is NOT under "result/core",
    // so the host does NOT get bumped. The plan should be for the host to
    // handle this by placing the shell under its own subtree (same core dir)
    // or by registering the shell's parent as a broader host dir.
    // In the real seeded layout, each *.SourceGen.csproj is NOT in the
    // consumable allowlist, and commits to them don't bump anything unless
    // the host's dir is a common ancestor. Here, the test confirms behavior.
    expect(computeBumpPlans(commits, [hostPkg])).toHaveLength(0);
  });

  it("commit touching multiple packages bumps all of them independently", () => {
    const pkgA = makeNpmPkg("@d2/a", "server/shared/typescript/a");
    const pkgB = makeNpmPkg("@d2/b", "server/shared/typescript/b");
    const commits = [
      makeCommit("fix: cross-cutting fix", [
        "server/shared/typescript/a/src/index.ts",
        "server/shared/typescript/b/src/index.ts",
      ]),
    ];

    const plans = computeBumpPlans(commits, [pkgA, pkgB]);
    expect(plans).toHaveLength(2);

    const names = plans.map((p) => p.pkg.name).sort();
    expect(names).toEqual(["@d2/a", "@d2/b"]);
    expect(plans.every((p) => p.bump === "patch")).toBe(true);
  });

  it("Windows-style backslash paths normalize correctly", () => {
    const pkg = makeNpmPkg("@d2/a", "server/shared/typescript/a");
    // Simulate a commit from a Windows git client reporting backslash paths.
    const commits = [
      makeCommit("feat: change", [
        "server\\shared\\typescript\\a\\src\\index.ts",
      ]),
    ];

    const plans = computeBumpPlans(commits, [pkg]);
    expect(plans).toHaveLength(1);
    expect(plans[0]?.bump).toBe("minor");
  });
});

// ---------------------------------------------------------------------------
// Multi-package run
// ---------------------------------------------------------------------------

describe("computeBumpPlans — multi-package run", () => {
  it("returns independent plans for each touched package", () => {
    const pkgA = makeNpmPkg("@d2/a", "server/shared/typescript/a", "0.1.0");
    const pkgB = makeNugetPkg("D2.Shared.B", "server/shared/dotnet/b", "0.2.0");

    const commits = [
      makeCommit("feat: extend A API", ["server/shared/typescript/a/src/a.ts"]),
      makeCommit("fix: fix B bug", ["server/shared/dotnet/b/B.cs"]),
    ];

    const plans = computeBumpPlans(commits, [pkgA, pkgB]);
    expect(plans).toHaveLength(2);

    const planA = plans.find((p) => p.pkg.name === "@d2/a");
    const planB = plans.find((p) => p.pkg.name === "D2.Shared.B");

    expect(planA?.bump).toBe("minor");
    expect(planA?.newVersion).toBe("0.2.0");
    expect(planB?.bump).toBe("patch");
    expect(planB?.newVersion).toBe("0.2.1");
  });

  it("packages with no qualifying commits are omitted from results", () => {
    const touched = makeNpmPkg(
      "@d2/touched",
      "server/shared/typescript/touched",
    );
    const untouched = makeNpmPkg(
      "@d2/untouched",
      "server/shared/typescript/untouched",
    );

    const commits = [
      makeCommit("feat: change in touched", [
        "server/shared/typescript/touched/src/index.ts",
      ]),
    ];

    const plans = computeBumpPlans(commits, [touched, untouched]);
    expect(plans).toHaveLength(1);
    expect(plans[0]?.pkg.name).toBe("@d2/touched");
  });
});

// ---------------------------------------------------------------------------
// Single-package filter (via packageFilter in runner options)
// ---------------------------------------------------------------------------

describe("computeBumpPlans — single package input", () => {
  it("only produces plans for supplied packages (caller pre-filters)", () => {
    const pkgA = makeNpmPkg("@d2/a", "server/shared/typescript/a");
    const commits = [
      makeCommit("feat: change", ["server/shared/typescript/a/src/index.ts"]),
    ];

    // Caller supplies only pkgA (simulating --package filter applied upstream).
    const plans = computeBumpPlans(commits, [pkgA]);
    expect(plans).toHaveLength(1);
    expect(plans[0]?.pkg.name).toBe("@d2/a");
  });
});

// ---------------------------------------------------------------------------
// Adversarial — malformed messages
// ---------------------------------------------------------------------------

describe("computeBumpPlans — adversarial inputs", () => {
  const pkg = makeNpmPkg("@d2/a", "server/shared/typescript/a");
  const file = "server/shared/typescript/a/src/index.ts";

  it("completely blank commit message produces no bump", () => {
    const commits = [makeCommit("", [file])];
    expect(computeBumpPlans(commits, [pkg])).toHaveLength(0);
  });

  it("commit message with no type prefix produces no bump", () => {
    const commits = [
      makeCommit("just a description with no type prefix", [file]),
    ];
    expect(computeBumpPlans(commits, [pkg])).toHaveLength(0);
  });

  it("commit with valid files but breaking footer on empty message produces no bump", () => {
    const commits = [makeCommit("   ", [file])];
    expect(computeBumpPlans(commits, [pkg])).toHaveLength(0);
  });

  it("commit message with no colon in subject touching a package produces no bump (other type)", () => {
    // Tests the extractSubjectDescription no-colon branch (colonIdx === -1).
    const commits = [makeCommit("this has no colon at all", [file])];
    expect(computeBumpPlans(commits, [pkg])).toHaveLength(0);
  });

  it("commit with unrecognized type (chore, docs, refactor) produces no bump", () => {
    const commits = [
      makeCommit("chore: update deps", [file]),
      makeCommit("docs: improve readme", [file]),
      makeCommit("refactor: extract helper", [file]),
    ];
    expect(computeBumpPlans(commits, [pkg])).toHaveLength(0);
  });

  it("malformed version string in package descriptor throws on first touch", () => {
    const badPkg = makeNpmPkg(
      "@d2/bad",
      "server/shared/typescript/bad",
      "not-a-version",
    );
    const commits = [
      makeCommit("feat: add something", [
        "server/shared/typescript/bad/src/index.ts",
      ]),
    ];

    expect(() => computeBumpPlans(commits, [badPkg])).toThrow();
  });
});

// ---------------------------------------------------------------------------
// Breaking footer deduplication
// ---------------------------------------------------------------------------

describe("computeBumpPlans — breaking entry deduplication", () => {
  const pkg = makeNpmPkg("@d2/a", "server/shared/typescript/a", "0.1.0");
  const file = "server/shared/typescript/a/src/index.ts";

  it("duplicate WIRE-BREAKING descriptions across commits are deduplicated", () => {
    const commits = [
      makeCommit(
        commitWithFooter(
          "feat: step 1",
          "Body.",
          "WIRE-BREAKING: dropped field 3",
        ),
        [file],
      ),
      makeCommit(
        commitWithFooter(
          "feat: step 2",
          "Body.",
          "WIRE-BREAKING: dropped field 3",
        ),
        [file],
      ),
    ];

    const plans = computeBumpPlans(commits, [pkg]);
    expect(plans[0]?.wireBreakingEntries).toHaveLength(1);
    expect(plans[0]?.wireBreakingEntries[0]).toBe("dropped field 3");
  });

  it("different WIRE-BREAKING descriptions accumulate", () => {
    const commits = [
      makeCommit(
        commitWithFooter(
          "feat: step 1",
          "Body.",
          "WIRE-BREAKING: dropped field 3",
        ),
        [file],
      ),
      makeCommit(
        commitWithFooter(
          "feat: step 2",
          "Body.",
          "WIRE-BREAKING: dropped field 5",
        ),
        [file],
      ),
    ];

    const plans = computeBumpPlans(commits, [pkg]);
    expect(plans[0]?.wireBreakingEntries).toHaveLength(2);
  });

  it("duplicate API-BREAKING descriptions across commits are deduplicated", () => {
    const commits = [
      makeCommit(
        commitWithFooter(
          "feat: step 1",
          "Body.",
          "BREAKING CHANGE: removed DEPRECATED_CODE",
        ),
        [file],
      ),
      makeCommit(
        commitWithFooter(
          "feat: step 2",
          "Body.",
          "BREAKING CHANGE: removed DEPRECATED_CODE",
        ),
        [file],
      ),
    ];

    const plans = computeBumpPlans(commits, [pkg]);
    expect(plans[0]?.apiBreakingEntries).toHaveLength(1);
    expect(plans[0]?.apiBreakingEntries[0]).toBe("removed DEPRECATED_CODE");
  });

  it("different API-BREAKING descriptions accumulate", () => {
    const commits = [
      makeCommit(
        commitWithFooter(
          "feat: step 1",
          "Body.",
          "BREAKING CHANGE: removed scope A",
        ),
        [file],
      ),
      makeCommit(
        commitWithFooter(
          "feat: step 2",
          "Body.",
          "BREAKING CHANGE: removed scope B",
        ),
        [file],
      ),
    ];

    const plans = computeBumpPlans(commits, [pkg]);
    expect(plans[0]?.apiBreakingEntries).toHaveLength(2);
  });
});
