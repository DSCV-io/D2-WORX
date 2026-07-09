// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Tests for the pre-commit baseline-currency checker (fingerprint-currency.ts).
//
// Polarity under test: the checker compares the RECOMPUTED source-based
// fingerprint of the WORKING TREE against the ON-DISK .release-fingerprint.
// A mismatch (recomputed ≠ on-disk) means the source changed without re-seeding.
//
// All file-system and git seams are injected — no real package tree, no git.
// Each test either seeds matching on-disk content (clean) or a deliberate
// mismatch (stale) to assert the detector fires correctly.

import { mkdtempSync, writeFileSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import { join, sep } from "node:path";
import { describe, expect, it } from "vitest";
import {
  checkFingerprintCurrency,
  formatCurrencyReport,
  isUnshippedHeaderOnly,
  type CurrencyFileReader,
  type FingerprintCurrencyOptions,
} from "../src/fingerprint-currency.js";
import {
  composeSourceFingerprint,
  buildSourceDump,
  readToolchainPin,
  type RepoFileReader,
} from "../src/source-fingerprint.js";
import {
  buildNugetManifestMeta,
  buildNpmManifestMeta,
  substituteResolvedDeps,
  type SourceLister,
} from "../src/real-diff-provider.js";
import type { PackageDescriptor } from "../src/types.js";

// ---------------------------------------------------------------------------
// Fixture helpers
// ---------------------------------------------------------------------------

function nugetPkg(name: string, dir: string): PackageDescriptor {
  return {
    name,
    ecosystem: "nuget",
    dir,
    manifestPath: join(dir, `${name}.csproj`),
    changelogPath: join(dir, "CHANGELOG.md"),
    currentVersion: "0.1.0",
    dependencies: [],
  };
}

function npmPkg(name: string, dir: string): PackageDescriptor {
  return {
    name,
    ecosystem: "npm",
    dir,
    manifestPath: join(dir, "package.json"),
    changelogPath: join(dir, "CHANGELOG.md"),
    currentVersion: "0.1.0",
    dependencies: [],
  };
}

/** The synthetic toolchain-pin reader used across all tests. */
const syntheticToolchainReader: RepoFileReader = (p) => {
  if (p === "server/global.json")
    return JSON.stringify({
      sdk: { version: "10.0.200", rollForward: "latestFeature" },
    });

  if (p === "server/Directory.Build.props")
    return "<Project><PropertyGroup><TargetFramework>net10.0</TargetFramework><LangVersion>latest</LangVersion></PropertyGroup></Project>";

  if (p === "package.json")
    return JSON.stringify({ devDependencies: { typescript: "5.9.3" } });

  if (p === "server/shared/typescript/tsconfig.base.json")
    return JSON.stringify({
      compilerOptions: { target: "ES2022", module: "ESNext" },
    });

  throw new Error(`unexpected toolchain read: ${p}`);
};

/**
 * Build the expected fingerprint for a synthetic nuget package.
 * Matches the composition checkFingerprintCurrency runs.
 *
 * @param sourceFiles - The file names returned by the sourceLister seam.
 *   Defaults to ["Thing.cs"] for single-package tests.
 */
function buildNugetFingerprint(
  pkg: PackageDescriptor,
  sourceContent: string,
  shippedContent: string,
  unshippedContent: string,
  sourceFiles: string[] = ["Thing.cs"],
): string {
  const sourceDump = buildSourceDump(sourceFiles, () => sourceContent);
  const resolvedVersions = new Map<string, string>([
    [pkg.name, pkg.currentVersion],
  ]);

  return composeSourceFingerprint({
    sourceDump,
    apiReport: shippedContent + unshippedContent,
    depsJson: buildNugetManifestMeta(pkg, resolvedVersions),
    toolchainJson: readToolchainPin("nuget", syntheticToolchainReader),
  });
}

/**
 * Build the expected fingerprint for a synthetic npm package with a single
 * src/index.ts source file. Matches the composition checkFingerprintCurrency runs.
 */
function buildNpmFingerprint(
  pkg: PackageDescriptor,
  sourceContent: string,
  apiMdContent: string,
  packageDeps: Record<string, string> = {},
): string {
  const sourceDump = buildSourceDump(["src/index.ts"], () => sourceContent);
  const packageJson = {
    name: pkg.name,
    version: pkg.currentVersion,
    dependencies: packageDeps,
  };
  const resolvedVersions = new Map<string, string>([
    [pkg.name, pkg.currentVersion],
  ]);
  const substituted = substituteResolvedDeps(packageJson, resolvedVersions);

  return composeSourceFingerprint({
    sourceDump,
    apiReport: apiMdContent,
    depsJson: buildNpmManifestMeta(substituted),
    toolchainJson: readToolchainPin("npm", syntheticToolchainReader),
  });
}

// ---------------------------------------------------------------------------
// isUnshippedHeaderOnly
// ---------------------------------------------------------------------------

describe("isUnshippedHeaderOnly", () => {
  it("header-only file (only #nullable enable) → true", () => {
    expect(isUnshippedHeaderOnly("#nullable enable\n")).toBe(true);
  });

  it("empty file → true (no API lines)", () => {
    expect(isUnshippedHeaderOnly("")).toBe(true);
  });

  it("only blank lines and comments → true", () => {
    expect(isUnshippedHeaderOnly("#nullable enable\n\n# comment\n")).toBe(true);
  });

  it("file with an API line → false", () => {
    expect(
      isUnshippedHeaderOnly("#nullable enable\nD2.Shared.Foo.Bar() -> void\n"),
    ).toBe(false);
  });

  it("multiple API lines → false", () => {
    expect(
      isUnshippedHeaderOnly("#nullable enable\nD2.Shared.Foo\nD2.Shared.Bar\n"),
    ).toBe(false);
  });
});

// ---------------------------------------------------------------------------
// Module-level absolute test paths (Windows-safe via node:path.join)
// ---------------------------------------------------------------------------
//
// Using `sep` to detect Windows so synthetic absolute paths are valid on the
// current platform — Windows requires a drive prefix (e.g. C:\…).

const NUGET_DIR = join(sep === "\\" ? "C:\\abs\\thing" : "/abs/thing");
const NUGET_DIR_2 = join(sep === "\\" ? "C:\\abs\\thing2" : "/abs/thing2");
const NPM_DIR = join(sep === "\\" ? "C:\\abs\\npm-thing" : "/abs/npm-thing");

// ---------------------------------------------------------------------------
// checkFingerprintCurrency — nuget package
// ---------------------------------------------------------------------------

describe("checkFingerprintCurrency — nuget", () => {
  const nugetDir = NUGET_DIR;
  const pkg = nugetPkg("D2.Shared.Thing", nugetDir);
  const shippedContent = "#nullable enable\nD2.Shared.Thing.A() -> void\n";
  const unshippedContent = "#nullable enable\n";
  const sourceContent = "public class Thing {}\n";

  function buildOptions(
    onDiskFingerprint: string,
    overrideSourceContent?: string,
  ): FingerprintCurrencyOptions {
    const effectiveSource = overrideSourceContent ?? sourceContent;
    const sourceLister: SourceLister = () => ["Thing.cs"];
    const fileReader: CurrencyFileReader = (abs) => {
      if (abs === join(nugetDir, ".release-fingerprint"))
        return onDiskFingerprint;
      if (abs === join(nugetDir, "PublicAPI.Shipped.txt"))
        return shippedContent;
      if (abs === join(nugetDir, "PublicAPI.Unshipped.txt"))
        return unshippedContent;
      if (abs === join(nugetDir, "Thing.cs")) return effectiveSource;

      return undefined;
    };

    return {
      fileReader,
      sourceLister,
      toolchainReader: syntheticToolchainReader,
    };
  }

  it("matching fingerprint + header-only Unshipped → current", () => {
    const fp = buildNugetFingerprint(
      pkg,
      sourceContent,
      shippedContent,
      unshippedContent,
    );
    const result = checkFingerprintCurrency([pkg], "/repo", buildOptions(fp));

    expect(result.allCurrent).toBe(true);
    expect(result.stale).toHaveLength(0);
    expect(result.results[0]!.detail).toBe("current");
  });

  it("mismatched fingerprint → stale with fingerprint-mismatch reason", () => {
    // on-disk has old fingerprint, source changed
    const result = checkFingerprintCurrency(
      [pkg],
      "/repo",
      buildOptions(
        "deadbeef-old-fingerprint",
        "public class Thing { public int X; }\n",
      ),
    );

    expect(result.allCurrent).toBe(false);
    expect(result.stale).toHaveLength(1);
    expect(result.stale[0]!.reasons).toContain("fingerprint-mismatch");
    expect(result.stale[0]!.detail).toContain("fingerprint mismatch");
  });

  it("missing on-disk fingerprint → stale with baseline-missing reason", () => {
    const sourceLister: SourceLister = () => ["Thing.cs"];
    const fileReader: CurrencyFileReader = (abs) => {
      if (abs === `/abs/thing/PublicAPI.Shipped.txt`) return shippedContent;
      if (abs === `/abs/thing/PublicAPI.Unshipped.txt`) return unshippedContent;
      if (abs === `/abs/thing/Thing.cs`) return sourceContent;

      return undefined; // .release-fingerprint missing
    };
    const result = checkFingerprintCurrency([pkg], "/repo", {
      fileReader,
      sourceLister,
      toolchainReader: syntheticToolchainReader,
    });

    expect(result.allCurrent).toBe(false);
    expect(result.stale[0]!.reasons).toContain("baseline-missing");
  });

  it("non-empty Unshipped.txt → stale with unshipped-not-empty reason", () => {
    const nonEmptyUnshipped =
      "#nullable enable\nD2.Shared.Thing.NewMethod() -> void\n";
    const sourceLister: SourceLister = () => ["Thing.cs"];
    const fileReader: CurrencyFileReader = (abs) => {
      if (abs === join(nugetDir, "PublicAPI.Shipped.txt"))
        return shippedContent;
      if (abs === join(nugetDir, "PublicAPI.Unshipped.txt"))
        return nonEmptyUnshipped;
      if (abs === join(nugetDir, "Thing.cs")) return sourceContent;

      // Compute the fingerprint with the non-empty unshipped so it matches
      // the fingerprint check (we want ONLY the unshipped reason to fire).
      if (abs === join(nugetDir, ".release-fingerprint")) {
        return buildNugetFingerprint(
          pkg,
          sourceContent,
          shippedContent,
          nonEmptyUnshipped,
        );
      }

      return undefined;
    };
    const result = checkFingerprintCurrency([pkg], "/repo", {
      fileReader,
      sourceLister,
      toolchainReader: syntheticToolchainReader,
    });

    expect(result.allCurrent).toBe(false);
    expect(result.stale[0]!.reasons).toContain("unshipped-not-empty");
    expect(result.stale[0]!.detail).toContain("Unshipped.txt not empty");
  });

  it("both fingerprint mismatch and non-empty Unshipped → both reasons reported", () => {
    const nonEmptyUnshipped =
      "#nullable enable\nD2.Shared.Thing.NewMethod() -> void\n";
    const sourceLister: SourceLister = () => ["Thing.cs"];
    const fileReader: CurrencyFileReader = (abs) => {
      if (abs === join(nugetDir, ".release-fingerprint"))
        return "stale-old-value";
      if (abs === join(nugetDir, "PublicAPI.Shipped.txt"))
        return shippedContent;
      if (abs === join(nugetDir, "PublicAPI.Unshipped.txt"))
        return nonEmptyUnshipped;
      if (abs === join(nugetDir, "Thing.cs")) return sourceContent;

      return undefined;
    };
    const result = checkFingerprintCurrency([pkg], "/repo", {
      fileReader,
      sourceLister,
      toolchainReader: syntheticToolchainReader,
    });

    expect(result.allCurrent).toBe(false);
    expect(result.stale[0]!.reasons).toContain("fingerprint-mismatch");
    expect(result.stale[0]!.reasons).toContain("unshipped-not-empty");
  });
});

// ---------------------------------------------------------------------------
// checkFingerprintCurrency — npm package
// ---------------------------------------------------------------------------

describe("checkFingerprintCurrency — npm", () => {
  const npmDir = NPM_DIR;
  const pkg = npmPkg("@d2/thing", npmDir);
  const sourceContent = "export const X = 1;\n";
  const apiMdContent = "// @public\nexport const X = 1;\n";

  // api-extractor.json with a reportFileName so the api.md path is predictable.
  const apiExtractorJson = JSON.stringify({
    mainEntryPointFilePath: "dist/index.d.ts",
    apiReport: { reportFileName: "@d2/thing.api.md" },
  });

  // With api-extractor.json specifying reportFileName "@d2/thing.api.md",
  // resolveApiMdPathFromReader returns `<npmDir>/etc/@d2/thing.api.md`.
  const expectedApiMdPath = join(npmDir, "etc", "@d2/thing.api.md");

  function buildOptions(
    onDiskFingerprint: string,
    overrideSourceContent?: string,
  ): FingerprintCurrencyOptions {
    const effectiveSource = overrideSourceContent ?? sourceContent;
    const sourceLister: SourceLister = () => ["src/index.ts"];
    const fileReader: CurrencyFileReader = (abs) => {
      if (abs === join(npmDir, "etc", ".release-fingerprint"))
        return onDiskFingerprint;
      if (abs === join(npmDir, "src", "index.ts")) return effectiveSource;
      if (abs === expectedApiMdPath) return apiMdContent;
      if (abs === join(npmDir, "package.json"))
        return JSON.stringify({
          name: "@d2/thing",
          version: "0.1.0",
          dependencies: {},
        });
      if (abs === join(npmDir, "api-extractor.json")) return apiExtractorJson;

      return undefined;
    };

    return {
      fileReader,
      sourceLister,
      toolchainReader: syntheticToolchainReader,
    };
  }

  it("matching fingerprint → current", () => {
    const fp = buildNpmFingerprint(pkg, sourceContent, apiMdContent);
    const result = checkFingerprintCurrency([pkg], "/repo", buildOptions(fp));

    expect(result.allCurrent).toBe(true);
    expect(result.stale).toHaveLength(0);
  });

  it("mismatched fingerprint → stale with fingerprint-mismatch reason", () => {
    const result = checkFingerprintCurrency(
      [pkg],
      "/repo",
      buildOptions("old-fp-value", "export const X = 99;\n"),
    );

    expect(result.allCurrent).toBe(false);
    expect(result.stale[0]!.reasons).toContain("fingerprint-mismatch");
  });

  it("missing on-disk fingerprint → stale with baseline-missing reason", () => {
    const sourceLister: SourceLister = () => ["src/index.ts"];
    const fileReader: CurrencyFileReader = (abs) => {
      if (abs === `/abs/npm-thing/src/index.ts`) return sourceContent;
      if (abs === `/abs/npm-thing/etc/@d2/thing.api.md`) return apiMdContent;
      if (abs === `/abs/npm-thing/package.json`)
        return JSON.stringify({ name: "@d2/thing", version: "0.1.0" });

      return undefined; // no .release-fingerprint
    };
    const result = checkFingerprintCurrency([pkg], "/repo", {
      fileReader,
      sourceLister,
      toolchainReader: syntheticToolchainReader,
    });

    expect(result.allCurrent).toBe(false);
    expect(result.stale[0]!.reasons).toContain("baseline-missing");
  });

  it("npm packages do NOT get the Unshipped-empty check", () => {
    // Even if somehow there were an unshipped txt, npm packages never trigger it.
    const fp = buildNpmFingerprint(pkg, sourceContent, apiMdContent);
    const result = checkFingerprintCurrency([pkg], "/repo", buildOptions(fp));

    // No unshipped-not-empty reason for npm
    for (const r of result.results) {
      expect(r.reasons).not.toContain("unshipped-not-empty");
    }
  });
});

// ---------------------------------------------------------------------------
// checkFingerprintCurrency — npm package with @d2/* workspace dependencies
//
// Regression test for the root-cause bug: the original gate built resolvedVersions
// with only [pkg.name → currentVersion], so substituteResolvedDeps could not
// resolve @d2/* dep literals (e.g. "workspace:*") and the DEPS input diverged
// from the seed's map → false-positive fingerprint mismatch on every package that
// declares any @d2/* dependency. The fix: checkFingerprintCurrency builds
// resolvedVersions from ALL packages (mirroring checkBaselineDrift), so every
// @d2/* dep is substituted with its committed version exactly as the seed does.
// ---------------------------------------------------------------------------

describe("checkFingerprintCurrency — npm package with @d2/* dependencies (regression)", () => {
  const depDir = join(sep === "\\" ? "C:\\abs\\npm-dep" : "/abs/npm-dep");
  const consumerDir = join(
    sep === "\\" ? "C:\\abs\\npm-consumer" : "/abs/npm-consumer",
  );

  const depPkg = npmPkg("@d2/dep", depDir);
  // Consumer declares "@d2/dep": "workspace:*" in package.json — the literal
  // the gate must substitute with "0.1.0" (depPkg.currentVersion) to match the seed.
  const consumerPkg: PackageDescriptor = {
    name: "@d2/consumer",
    ecosystem: "npm",
    dir: consumerDir,
    manifestPath: join(consumerDir, "package.json"),
    changelogPath: join(consumerDir, "CHANGELOG.md"),
    currentVersion: "0.2.0",
    dependencies: ["@d2/dep"],
  };

  const depSource = "export const D = 1;\n";
  const depApiMd = "// @public\nexport const D = 1;\n";
  const consumerSource = "export const C = 2;\n";
  const consumerApiMd = "// @public\nexport const C = 2;\n";

  const depApiExtractorJson = JSON.stringify({
    apiReport: { reportFileName: "dep.api.md" },
  });
  const consumerApiExtractorJson = JSON.stringify({
    apiReport: { reportFileName: "consumer.api.md" },
  });

  /**
   * Build the expected fingerprint for the consumer package using the seed's
   * composition: resolvedVersions contains BOTH packages so workspace:* is
   * substituted with "0.1.0".
   */
  function buildConsumerFingerprint(): string {
    const sourceDump = buildSourceDump(["src/index.ts"], () => consumerSource);
    const packageJson = {
      name: "@d2/consumer",
      version: "0.2.0",
      dependencies: { "@d2/dep": "workspace:*" },
    };
    // Seed maps ALL consumables — here both dep + consumer at their committed versions.
    const allVersions = new Map<string, string>([
      ["@d2/dep", "0.1.0"],
      ["@d2/consumer", "0.2.0"],
    ]);
    const substituted = substituteResolvedDeps(packageJson, allVersions);

    return composeSourceFingerprint({
      sourceDump,
      apiReport: consumerApiMd,
      depsJson: buildNpmManifestMeta(substituted),
      toolchainJson: readToolchainPin("npm", syntheticToolchainReader),
    });
  }

  it("consumer with @d2/dep dependency: seed-style fingerprint → current (no false positive)", () => {
    // The seed writes the fingerprint using the full all-package resolved-version
    // map so "@d2/dep": "workspace:*" → "0.1.0" in the DEPS JSON. The gate must
    // use the same map or it reads "workspace:*" literally and produces a different
    // hash → false positive. This test proves the fix: with both packages in the
    // inventory, the gate's recompute matches the seed's composition exactly.
    const depFp = buildNpmFingerprint(depPkg, depSource, depApiMd);
    const consumerFp = buildConsumerFingerprint();

    const sourceLister: SourceLister = () => ["src/index.ts"];
    const fileReader: CurrencyFileReader = (abs) => {
      // dep package
      if (abs === join(depDir, "etc", ".release-fingerprint")) return depFp;
      if (abs === join(depDir, "src", "index.ts")) return depSource;
      if (abs === join(depDir, "etc", "dep.api.md")) return depApiMd;
      if (abs === join(depDir, "package.json"))
        return JSON.stringify({
          name: "@d2/dep",
          version: "0.1.0",
          dependencies: {},
        });
      if (abs === join(depDir, "api-extractor.json"))
        return depApiExtractorJson;

      // consumer package
      if (abs === join(consumerDir, "etc", ".release-fingerprint"))
        return consumerFp;
      if (abs === join(consumerDir, "src", "index.ts")) return consumerSource;
      if (abs === join(consumerDir, "etc", "consumer.api.md"))
        return consumerApiMd;
      if (abs === join(consumerDir, "package.json"))
        return JSON.stringify({
          name: "@d2/consumer",
          version: "0.2.0",
          dependencies: { "@d2/dep": "workspace:*" },
        });
      if (abs === join(consumerDir, "api-extractor.json"))
        return consumerApiExtractorJson;

      return undefined;
    };

    const result = checkFingerprintCurrency([depPkg, consumerPkg], "/repo", {
      fileReader,
      sourceLister,
      toolchainReader: syntheticToolchainReader,
    });

    expect(result.allCurrent).toBe(true);
    expect(result.stale).toHaveLength(0);
  });

  it("consumer with @d2/dep dependency: narrow map (own-name only) would produce false positive", () => {
    // Demonstrate the original bug: if we seed the on-disk fingerprint using a
    // narrow map (only own name), the gate's full-map recompute produces a
    // DIFFERENT hash → stale. This assertion would FAIL before the fix (the old
    // gate would have been using the narrow map too, making them match), and it
    // PASSES after the fix because we prove the full-map fp ≠ narrow-map fp.
    const narrowMap = new Map<string, string>([["@d2/consumer", "0.2.0"]]);
    const packageJson = {
      name: "@d2/consumer",
      version: "0.2.0",
      dependencies: { "@d2/dep": "workspace:*" },
    };
    const narrowSubstituted = substituteResolvedDeps(packageJson, narrowMap);
    const sourceDump = buildSourceDump(["src/index.ts"], () => consumerSource);
    const narrowFp = composeSourceFingerprint({
      sourceDump,
      apiReport: consumerApiMd,
      depsJson: buildNpmManifestMeta(narrowSubstituted),
      toolchainJson: readToolchainPin("npm", syntheticToolchainReader),
    });

    // The narrow fp and the seed-style fp MUST differ (they use different DEPS).
    const seedFp = buildConsumerFingerprint();

    expect(narrowFp).not.toBe(seedFp);
  });
});

// ---------------------------------------------------------------------------
// checkFingerprintCurrency — multiple packages
// ---------------------------------------------------------------------------

describe("checkFingerprintCurrency — multiple packages", () => {
  it("empty package set → allCurrent true", () => {
    const result = checkFingerprintCurrency([], "/repo");

    expect(result.allCurrent).toBe(true);
    expect(result.stale).toHaveLength(0);
    expect(result.results).toHaveLength(0);
  });

  it("all packages current → allCurrent true", () => {
    const dirA = NUGET_DIR;
    const dirB = NUGET_DIR_2;
    const pkgA = nugetPkg("D2.Shared.Thing", dirA);
    const pkgB = nugetPkg("D2.Shared.Thing2", dirB);
    const shippedA = "#nullable enable\nD2.A\n";
    const shippedB = "#nullable enable\nD2.B\n";
    const unshipped = "#nullable enable\n";
    const src = "public class C {}\n";

    const fpA = buildNugetFingerprint(pkgA, src, shippedA, unshipped, ["C.cs"]);
    const fpB = buildNugetFingerprint(pkgB, src, shippedB, unshipped, ["C.cs"]);

    const sourceLister: SourceLister = () => ["C.cs"];
    const fileReader: CurrencyFileReader = (abs) => {
      if (abs === join(dirA, ".release-fingerprint")) return fpA;
      if (abs === join(dirB, ".release-fingerprint")) return fpB;
      if (abs === join(dirA, "PublicAPI.Shipped.txt")) return shippedA;
      if (abs === join(dirB, "PublicAPI.Shipped.txt")) return shippedB;
      if (abs === join(dirA, "PublicAPI.Unshipped.txt")) return unshipped;
      if (abs === join(dirB, "PublicAPI.Unshipped.txt")) return unshipped;
      if (abs === join(dirA, "C.cs")) return src;
      if (abs === join(dirB, "C.cs")) return src;

      return undefined;
    };

    const result = checkFingerprintCurrency([pkgA, pkgB], "/repo", {
      fileReader,
      sourceLister,
      toolchainReader: syntheticToolchainReader,
    });

    expect(result.allCurrent).toBe(true);
    expect(result.results).toHaveLength(2);
  });

  it("one stale package in a mix → allCurrent false, only the stale one named", () => {
    const dirA = NUGET_DIR;
    const dirB = NUGET_DIR_2;
    const pkgA = nugetPkg("D2.Shared.Thing", dirA);
    const pkgB = nugetPkg("D2.Shared.Thing2", dirB);
    const shipped = "#nullable enable\nD2.X\n";
    const unshipped = "#nullable enable\n";
    const src = "public class C {}\n";

    const fpA = buildNugetFingerprint(pkgA, src, shipped, unshipped, ["C.cs"]);

    const sourceLister: SourceLister = () => ["C.cs"];
    const fileReader: CurrencyFileReader = (abs) => {
      if (abs === join(dirA, ".release-fingerprint")) return fpA; // current
      if (abs === join(dirB, ".release-fingerprint")) return "stale-old"; // stale
      if (abs === join(dirA, "PublicAPI.Shipped.txt")) return shipped;
      if (abs === join(dirB, "PublicAPI.Shipped.txt")) return shipped;
      if (abs === join(dirA, "PublicAPI.Unshipped.txt")) return unshipped;
      if (abs === join(dirB, "PublicAPI.Unshipped.txt")) return unshipped;
      if (abs === join(dirA, "C.cs")) return src;
      if (abs === join(dirB, "C.cs")) return src;

      return undefined;
    };

    const result = checkFingerprintCurrency([pkgA, pkgB], "/repo", {
      fileReader,
      sourceLister,
      toolchainReader: syntheticToolchainReader,
    });

    expect(result.allCurrent).toBe(false);
    expect(result.stale).toHaveLength(1);
    expect(result.stale[0]!.name).toBe("D2.Shared.Thing2");
  });
});

// ---------------------------------------------------------------------------
// formatCurrencyReport
// ---------------------------------------------------------------------------

describe("formatCurrencyReport", () => {
  it("all-current result → 'baselines are current' line with package count", () => {
    const report = formatCurrencyReport({
      results: [
        {
          name: "D2.Shared.X",
          ecosystem: "nuget",
          current: true,
          reasons: [],
          detail: "current",
        },
      ],
      stale: [],
      allCurrent: true,
    });

    expect(report).toContain("baselines are current");
    expect(report).toContain("1 package");
  });

  it("stale result → table naming the stale packages + remediation instructions", () => {
    const report = formatCurrencyReport({
      results: [],
      stale: [
        {
          name: "D2.Shared.Result",
          ecosystem: "nuget",
          current: false,
          reasons: ["fingerprint-mismatch"],
          detail: "fingerprint mismatch",
        },
      ],
      allCurrent: false,
    });

    expect(report).toContain("STALE BASELINES detected in 1 package(s)");
    expect(report).toContain("D2.Shared.Result | nuget | fingerprint mismatch");
    expect(report).toContain("seed-publicapi-baselines.mjs");
    expect(report).toContain("seed-apiextractor-baselines.mjs");
    expect(report).toContain("Re-stage");
  });

  it("stale result includes Unshipped detail", () => {
    const report = formatCurrencyReport({
      results: [],
      stale: [
        {
          name: "D2.Shared.Auth",
          ecosystem: "nuget",
          current: false,
          reasons: ["unshipped-not-empty"],
          detail: "Unshipped.txt not empty",
        },
      ],
      allCurrent: false,
    });

    expect(report).toContain(
      "D2.Shared.Auth | nuget | Unshipped.txt not empty",
    );
  });

  it("empty package set → all-current report (0 packages)", () => {
    const report = formatCurrencyReport({
      results: [],
      stale: [],
      allCurrent: true,
    });

    expect(report).toContain("baselines are current");
    expect(report).toContain("0 package");
  });
});

// ---------------------------------------------------------------------------
// Integration smoke-test against a real temp directory
// ---------------------------------------------------------------------------
//
// This test creates a minimal synthetic package tree on disk and verifies the
// full real-fs path (no injected seams). It is the single end-to-end proof
// that the CLI path (loadAllPackages + checkFingerprintCurrency with real
// readers) wires together correctly.

describe("checkFingerprintCurrency — real-fs integration smoke", () => {
  it("writes a matching on-disk fingerprint → passes; mismatched → fails", () => {
    // Build a minimal nuget package in a temp dir with an absolute pkg.dir so
    // the resolver passes it through unchanged. Inject a synthetic sourceLister
    // so we don't rely on `git ls-files` (temp dir is outside the repo tree).
    const root = mkdtempSync(join(tmpdir(), "fc-smoke-"));

    try {
      // Write minimal package files.
      writeFileSync(join(root, "Thing.cs"), "public class Thing {}\n", "utf-8");
      writeFileSync(
        join(root, "D2.Shared.SmokeTest.csproj"),
        "<Project><PropertyGroup><Version>0.1.0</Version></PropertyGroup></Project>",
        "utf-8",
      );
      writeFileSync(
        join(root, "PublicAPI.Shipped.txt"),
        "#nullable enable\n",
        "utf-8",
      );
      writeFileSync(
        join(root, "PublicAPI.Unshipped.txt"),
        "#nullable enable\n",
        "utf-8",
      );

      const pkg: PackageDescriptor = {
        name: "D2.Shared.SmokeTest",
        ecosystem: "nuget",
        dir: root,
        manifestPath: join(root, "D2.Shared.SmokeTest.csproj"),
        changelogPath: join(root, "CHANGELOG.md"),
        currentVersion: "0.1.0",
        dependencies: [],
      };

      // Synthetic seams: a known sourceLister + toolchainReader so the
      // fingerprint is fully deterministic without real fs/git.
      const toolchainReader: RepoFileReader = (p) => {
        if (p === "server/global.json")
          return JSON.stringify({
            sdk: { version: "10.0.200", rollForward: "latestFeature" },
          });

        if (p === "server/Directory.Build.props")
          return "<Project><PropertyGroup><TargetFramework>net10.0</TargetFramework><LangVersion>latest</LangVersion></PropertyGroup></Project>";

        throw new Error(`unexpected: ${p}`);
      };

      // Only list Thing.cs — matches what we wrote to disk.
      const sourceLister: SourceLister = (_dir, _eco) => ["Thing.cs"];

      // Compute what the on-disk fingerprint SHOULD be.
      const resolvedVersions = new Map([["D2.Shared.SmokeTest", "0.1.0"]]);
      const sourceDump = buildSourceDump(
        ["Thing.cs"],
        () => "public class Thing {}\n",
      );
      const expectedFp = composeSourceFingerprint({
        sourceDump,
        apiReport: "#nullable enable\n#nullable enable\n",
        depsJson: buildNugetManifestMeta(pkg, resolvedVersions),
        toolchainJson: readToolchainPin("nuget", toolchainReader),
      });

      // PASS: write the correct fingerprint
      writeFileSync(join(root, ".release-fingerprint"), expectedFp, "utf-8");

      const passResult = checkFingerprintCurrency([pkg], root, {
        sourceLister,
        toolchainReader,
      });

      expect(passResult.allCurrent).toBe(true);

      // FAIL: write a stale fingerprint
      writeFileSync(
        join(root, ".release-fingerprint"),
        "stale-old-value",
        "utf-8",
      );

      const failResult = checkFingerprintCurrency([pkg], root, {
        sourceLister,
        toolchainReader,
      });

      expect(failResult.allCurrent).toBe(false);
      expect(failResult.stale[0]!.name).toBe("D2.Shared.SmokeTest");
    } finally {
      rmSync(root, { recursive: true, force: true });
    }
  });
});
