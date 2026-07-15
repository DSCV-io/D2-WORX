// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

// Tests for the production DiffProvider (real-diff-provider.ts) — source-based,
// build-free. All seams (committed-file reader, git-baseline reader, toolchain
// reader) are injected, so the provider's dispatch + git-ref apiDiff + source
// fingerprint composition + propagation fold are asserted with NO real build,
// no api-extractor, no real git.
//
//   - ecosystem dispatch (nuget vs npm)
//   - git-ref apiDiff (S7): baseline-ref report vs HEAD report
//   - source-based fingerprint: changed source / report / deps / toolchain → moves
//   - propagation (S4): a changed resolved dep version flips the fingerprint
//   - baselineMissing (no committed fingerprint / report)
//   - provider source-dump determinism (S9) against the real D2.Shared.Utilities tree
//     (the seed↔provider byte-identity of the COMPOSITION is pinned separately in
//     seed-provider-fingerprint-identity.test.ts)
//   - the DEPS helpers (buildNugetManifestMeta / substituteResolvedDeps / buildNpmManifestMeta)
//   - the default real readers (makeRealFileReader) + readPackageJsonFile

import { mkdtempSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join, resolve } from "node:path";
import { describe, expect, it } from "vitest";
import {
  buildNpmManifestMeta,
  buildNugetManifestMeta,
  makeRealDiffProvider,
  makeRealFileReader,
  readPackageJsonFile,
  substituteResolvedDeps,
  type FileReader,
  type SourceLister,
} from "../src/real-diff-provider.js";
import {
  buildSourceDump,
  composeSourceFingerprint,
  listSourceFiles,
  readToolchainPin,
} from "../src/source-fingerprint.js";
import type { BaselineReader } from "../src/ts-api-adapter.js";
import type { RepoFileReader } from "../src/source-fingerprint.js";
import type { PackageDescriptor } from "../src/types.js";
import { repoRoot } from "./repo-root.js";

// ---------------------------------------------------------------------------
// Synthetic descriptors (absolute dirs so the provider passes them through)
// ---------------------------------------------------------------------------

const nugetPkg: PackageDescriptor = {
  name: "D2.Shared.Result",
  ecosystem: "nuget",
  dir: "/abs/result/core",
  manifestPath: "/abs/result/core/D2.Shared.Result.csproj",
  changelogPath: "/abs/result/core/CHANGELOG.md",
  currentVersion: "0.1.0",
  dependencies: ["D2.Shared.Utilities"],
};

const npmPkg: PackageDescriptor = {
  name: "@d2/result",
  ecosystem: "npm",
  dir: "/abs/ts/result",
  manifestPath: "/abs/ts/result/package.json",
  changelogPath: "/abs/ts/result/CHANGELOG.md",
  currentVersion: "0.1.0",
  dependencies: ["@d2/utilities"],
};

// A stable toolchain reader for both ecosystems.
const TOOLCHAIN_READER: RepoFileReader = (p) => {
  if (p === "global.json")
    return JSON.stringify({
      sdk: { version: "10.0.200", rollForward: "latestFeature" },
    });

  if (p === "Directory.Build.props")
    return "<Project><PropertyGroup><TargetFramework>net10.0</TargetFramework><LangVersion>latest</LangVersion></PropertyGroup></Project>";

  if (p === "package.json")
    return JSON.stringify({ devDependencies: { typescript: "5.9.3" } });

  if (p === "public/packages/typescript/tsconfig.base.json")
    return JSON.stringify({
      compilerOptions: { target: "ES2022", module: "ESNext" },
    });

  throw new Error(`unexpected toolchain read ${p}`);
};

/**
 * Build a FileReader from an absolute-path → content map. Missing paths return
 * undefined (the file-not-present branch).
 */
function fileReaderFrom(files: Record<string, string>): FileReader {
  return (absPath: string): string | undefined => {
    const norm = absPath.replace(/\\/g, "/");

    return files[norm];
  };
}

/**
 * Derive repo-relative source paths under `packageDir` from a file map, applying
 * the same per-ecosystem allowlist the real `listSourceFiles` does. Lets a
 * provider test inject a deterministic file set without a real package tree, and
 * compose the same fingerprint the calibration helper recomputes.
 */
function sourceListerFrom(
  files: Record<string, string>,
  packageDir: string,
): SourceLister {
  return (dir: string, ecosystem) => {
    const target = dir.replace(/\\/g, "/");

    if (target !== packageDir) return [];

    return Object.keys(files)
      .filter((p) => p.startsWith(packageDir + "/"))
      .map((p) => p.slice(packageDir.length + 1))
      .filter((rel) => {
        if (ecosystem === "nuget")
          return rel.endsWith(".cs") || rel.endsWith(".csproj");

        return (
          (rel.endsWith(".ts") && !rel.endsWith(".test.ts")) ||
          rel === "package.json" ||
          rel === "api-extractor.json" ||
          (rel.startsWith("tsconfig") && rel.endsWith(".json"))
        );
      });
  };
}

/** A BaselineReader from a (path, ref) → content map (keyed by `ref|path`). */
function baselineReaderFrom(
  byRef: Record<string, string | undefined>,
): BaselineReader {
  return {
    read(filePath: string, ref = "HEAD"): string | undefined {
      const key = `${ref}|${filePath.replace(/\\/g, "/")}`;

      return byRef[key];
    },
  };
}

// ---------------------------------------------------------------------------
// buildNugetManifestMeta
// ---------------------------------------------------------------------------

describe("buildNugetManifestMeta", () => {
  it("uses resolved dep version when present", () => {
    const meta = buildNugetManifestMeta(
      nugetPkg,
      new Map([
        ["D2.Shared.Result", "0.1.0"],
        ["D2.Shared.Utilities", "0.3.0"],
      ]),
    );

    expect(JSON.parse(meta)).toEqual({
      packageId: "D2.Shared.Result",
      version: "0.1.0",
      deps: { "D2.Shared.Utilities": "0.3.0" },
    });
  });

  it("falls back to package currentVersion when own version absent", () => {
    const meta = buildNugetManifestMeta(nugetPkg, new Map());

    expect(JSON.parse(meta).version).toBe("0.1.0");
    expect(JSON.parse(meta).deps).toEqual({ "D2.Shared.Utilities": "" });
  });

  it("sorts deps deterministically", () => {
    const pkg: PackageDescriptor = {
      ...nugetPkg,
      dependencies: ["D2.Shared.Zebra", "D2.Shared.Alpha"],
    };
    const meta = buildNugetManifestMeta(
      pkg,
      new Map([
        ["D2.Shared.Zebra", "1.0.0"],
        ["D2.Shared.Alpha", "2.0.0"],
      ]),
    );

    expect(Object.keys(JSON.parse(meta).deps)).toEqual([
      "D2.Shared.Alpha",
      "D2.Shared.Zebra",
    ]);
  });
});

// ---------------------------------------------------------------------------
// substituteResolvedDeps + buildNpmManifestMeta
// ---------------------------------------------------------------------------

describe("substituteResolvedDeps", () => {
  it("replaces a consumable dep literal with its resolved version", () => {
    const out = substituteResolvedDeps(
      {
        name: "@d2/result",
        version: "0.1.0",
        dependencies: { "@d2/utilities": "workspace:*" },
      },
      new Map([["@d2/utilities", "0.5.0"]]),
    );

    expect(out.dependencies).toEqual({ "@d2/utilities": "0.5.0" });
  });

  it("keeps the original literal for an unresolved dep", () => {
    const out = substituteResolvedDeps(
      { dependencies: { vitest: "^1.0.0" } },
      new Map(),
    );

    expect(out.dependencies).toEqual({ vitest: "^1.0.0" });
  });

  it("substitutes the package's own resolved version", () => {
    const out = substituteResolvedDeps(
      { name: "@d2/result", version: "0.1.0", dependencies: {} },
      new Map([["@d2/result", "0.2.0"]]),
    );

    expect(out.version).toBe("0.2.0");
  });

  it("handles a package.json with no dependencies", () => {
    const out = substituteResolvedDeps(
      { name: "@d2/x", version: "1.0.0" },
      new Map(),
    );

    expect(out.dependencies).toEqual({});
  });

  it("keeps own version when not in the resolved map (and no name)", () => {
    const out = substituteResolvedDeps({ version: "9.9.9" }, new Map());

    expect(out.version).toBe("9.9.9");
  });
});

describe("buildNpmManifestMeta", () => {
  it("serializes {name, version, dependencies} with defaults", () => {
    expect(buildNpmManifestMeta({})).toBe(
      '{"name":"","version":"","dependencies":{}}',
    );
    expect(
      JSON.parse(
        buildNpmManifestMeta({
          name: "@d2/x",
          version: "1.0.0",
          dependencies: { "@d2/y": "1.0.0" },
        }),
      ),
    ).toEqual({
      name: "@d2/x",
      version: "1.0.0",
      dependencies: { "@d2/y": "1.0.0" },
    });
  });
});

// ---------------------------------------------------------------------------
// makeRealDiffProvider — nuget dispatch + git-ref apiDiff + fingerprint
// ---------------------------------------------------------------------------

describe("makeRealDiffProvider — nuget", () => {
  const shippedAbs = "/abs/result/core/PublicAPI.Shipped.txt";
  const unshippedAbs = "/abs/result/core/PublicAPI.Unshipped.txt";
  const fpAbs = "/abs/result/core/.release-fingerprint";
  const sourceAbs = "/abs/result/core/Result.cs";
  const csprojAbs = "/abs/result/core/D2.Shared.Result.csproj";

  const cleanFiles: Record<string, string> = {
    [shippedAbs]: "#nullable enable\nD2.Foo\n",
    [unshippedAbs]: "#nullable enable\n",
    [sourceAbs]: "namespace D2;\n",
    [csprojAbs]: "<Project></Project>\n",
  };

  // The source set (.cs / .csproj) is identical across all nuget tests — only
  // the Shipped / fingerprint baselines vary, and those are not source files —
  // so one lister matches every test's source dump (and the calibration helper).
  const lister = sourceListerFrom(cleanFiles, "/abs/result/core");

  it("dispatches to the nuget branch + maps an added-line apiDiff", () => {
    const files: Record<string, string> = {
      ...cleanFiles,
      [shippedAbs]: "#nullable enable\nD2.Foo\nD2.Bar\n",
      [fpAbs]: "irrelevant-committed",
    };

    const provider = makeRealDiffProvider(repoRoot, {
      fileReader: fileReaderFrom(files),
      sourceLister: lister,
      baselineReader: baselineReaderFrom({
        // Baseline ref had only D2.Foo; HEAD adds D2.Bar → added.
        [`HEAD|${shippedAbs}`]: "#nullable enable\nD2.Foo\n",
      }),
      toolchainReader: TOOLCHAIN_READER,
    });

    const diff = provider.getDiff({
      pkg: nugetPkg,
      resolvedVersions: new Map([["D2.Shared.Result", "0.1.0"]]),
    });

    expect(diff.apiDiff.added).toBe(true);
    expect(diff.apiDiff.removed).toBe(false);
    // committed fp "irrelevant-committed" != freshly composed → changed.
    expect(diff.fingerprintDiff.changed).toBe(true);
    expect(diff.baselineMissing).toBe(false);
  });

  it("clean no-op: committed fp == fresh composed → fingerprintDiff.changed false", () => {
    const resolved = new Map([["D2.Shared.Utilities", "0.1.0"]]);
    const committed = composeNugetFpDirect(cleanFiles, resolved);

    const provider = makeRealDiffProvider(repoRoot, {
      fileReader: fileReaderFrom({ ...cleanFiles, [fpAbs]: committed }),
      sourceLister: lister,
      baselineReader: baselineReaderFrom({
        [`HEAD|${shippedAbs}`]: cleanFiles[shippedAbs]!,
      }),
      toolchainReader: TOOLCHAIN_READER,
    });

    const diff = provider.getDiff({
      pkg: nugetPkg,
      resolvedVersions: resolved,
    });

    expect(diff.fingerprintDiff.changed).toBe(false);
    expect(diff.apiDiff).toEqual({
      added: false,
      removed: false,
      changed: false,
    });
    expect(diff.baselineMissing).toBe(false);
  });

  it("missing committed fingerprint → baselineMissing true + fingerprint changed", () => {
    const provider = makeRealDiffProvider(repoRoot, {
      // no fpAbs committed
      fileReader: fileReaderFrom(cleanFiles),
      sourceLister: lister,
      baselineReader: baselineReaderFrom({
        [`HEAD|${shippedAbs}`]: cleanFiles[shippedAbs]!,
      }),
      toolchainReader: TOOLCHAIN_READER,
    });

    const diff = provider.getDiff({
      pkg: nugetPkg,
      resolvedVersions: new Map(),
    });

    expect(diff.baselineMissing).toBe(true);
    expect(diff.fingerprintDiff.changed).toBe(true);
  });

  it("missing baseline-ref Shipped report → added-from-empty + baselineMissing", () => {
    const provider = makeRealDiffProvider(repoRoot, {
      fileReader: fileReaderFrom({ ...cleanFiles, [fpAbs]: "committed" }),
      sourceLister: lister,
      // baselineReader returns undefined for everything (no committed report at ref).
      baselineReader: baselineReaderFrom({}),
      toolchainReader: TOOLCHAIN_READER,
    });

    const diff = provider.getDiff({
      pkg: nugetPkg,
      resolvedVersions: new Map(),
    });

    // HEAD has members, baseline-ref report absent → treated as added.
    expect(diff.apiDiff.added).toBe(true);
    expect(diff.baselineMissing).toBe(true);
  });

  it("absent head Shipped/Unshipped + an unreadable listed source file → empty fallbacks (no throw)", () => {
    // fileReader returns undefined for EVERY path (head reports + the listed
    // source file + the committed fp), exercising the `?? ""` fallbacks. The
    // lister still returns a source path, but its content read returns undefined.
    const provider = makeRealDiffProvider(repoRoot, {
      fileReader: () => undefined,
      sourceLister: () => ["Result.cs"],
      baselineReader: baselineReaderFrom({
        [`HEAD|${shippedAbs}`]: "#nullable enable\nD2.Foo\n",
      }),
      toolchainReader: TOOLCHAIN_READER,
    });

    const diff = provider.getDiff({
      pkg: nugetPkg,
      resolvedVersions: new Map(),
    });

    // Baseline-ref report had D2.Foo; head is empty → removed.
    expect(diff.apiDiff.removed).toBe(true);
    // No committed fp → missing + changed.
    expect(diff.baselineMissing).toBe(true);
    expect(diff.fingerprintDiff.changed).toBe(true);
  });

  it("PROPAGATION (nuget): a changed resolved dep version flips the fingerprint", () => {
    const files: Record<string, string> = {
      ...cleanFiles,
      [shippedAbs]: "#nullable enable\n",
    };
    const baseline = baselineReaderFrom({
      [`HEAD|${shippedAbs}`]: "#nullable enable\n",
    });

    // Committed fp == the fp at dep@0.1.0 → still 0.1.0 unchanged; 0.2.0 changed.
    const committed = composeNugetFpDirect(
      files,
      new Map([["D2.Shared.Utilities", "0.1.0"]]),
    );

    const provider = makeRealDiffProvider(repoRoot, {
      fileReader: fileReaderFrom({ ...files, [fpAbs]: committed }),
      sourceLister: lister,
      baselineReader: baseline,
      toolchainReader: TOOLCHAIN_READER,
    });

    const still010 = provider.getDiff({
      pkg: nugetPkg,
      resolvedVersions: new Map([["D2.Shared.Utilities", "0.1.0"]]),
    });
    const moved020 = provider.getDiff({
      pkg: nugetPkg,
      resolvedVersions: new Map([["D2.Shared.Utilities", "0.2.0"]]),
    });

    expect(still010.fingerprintDiff.changed).toBe(false);
    expect(moved020.fingerprintDiff.changed).toBe(true);
  });
});

// ---------------------------------------------------------------------------
// Direct fp recomputation via the public composer (feeds committed test values)
// ---------------------------------------------------------------------------

/** Recompute the nuget fp exactly as the provider does, for test calibration. */
function composeNugetFpDirect(
  files: Record<string, string>,
  resolved: ReadonlyMap<string, string>,
): string {
  const packageDir = "/abs/result/core";
  const shipped = files[`${packageDir}/PublicAPI.Shipped.txt`] ?? "";
  const unshipped = files[`${packageDir}/PublicAPI.Unshipped.txt`] ?? "";

  const sourceRead = (relPosix: string): string =>
    files[`${packageDir}/${relPosix}`] ?? "";
  const relPaths = Object.keys(files)
    .filter((p) => p.startsWith(packageDir + "/"))
    .map((p) => p.slice(packageDir.length + 1))
    .filter((rel) => rel.endsWith(".cs") || rel.endsWith(".csproj"));

  const sourceDump = buildSourceDump(relPaths, sourceRead);

  return composeSourceFingerprint({
    sourceDump,
    apiReport: shipped + unshipped,
    depsJson: buildNugetManifestMeta(nugetPkg, resolved),
    toolchainJson: readToolchainPin("nuget", TOOLCHAIN_READER),
  });
}

// ---------------------------------------------------------------------------
// makeRealDiffProvider — npm dispatch
// ---------------------------------------------------------------------------

describe("makeRealDiffProvider — npm", () => {
  const apiMdAbs = "/abs/ts/result/etc/result.api.md";
  const fpAbs = "/abs/ts/result/etc/.release-fingerprint";
  const pkgJsonAbs = "/abs/ts/result/package.json";
  const srcAbs = "/abs/ts/result/src/index.ts";

  const API_MD =
    '## API Report File for "@d2/result"\n\n```ts\n\n// @public\nexport const X: string;\n\n```\n';
  const PKG_JSON = JSON.stringify({
    name: "@d2/result",
    version: "0.1.0",
    dependencies: { "@d2/utilities": "workspace:*" },
  });

  // The source set (package.json + src/index.ts) is identical across npm tests.
  const lister = sourceListerFrom(
    { [pkgJsonAbs]: PKG_JSON, [srcAbs]: "export const X = '';\n" },
    "/abs/ts/result",
  );

  it("dispatches to the npm branch + maps an unchanged apiDiff", () => {
    const files: Record<string, string> = {
      [apiMdAbs]: API_MD,
      [fpAbs]: "stale",
      [pkgJsonAbs]: PKG_JSON,
      [srcAbs]: "export const X = '';\n",
    };

    const provider = makeRealDiffProvider(repoRoot, {
      fileReader: fileReaderFrom(files),
      sourceLister: lister,
      baselineReader: baselineReaderFrom({ [`HEAD|${apiMdAbs}`]: API_MD }),
      toolchainReader: TOOLCHAIN_READER,
    });

    const diff = provider.getDiff({
      pkg: npmPkg,
      resolvedVersions: new Map(),
    });

    expect(diff.apiDiff.added).toBe(false);
    expect(diff.apiDiff.removed).toBe(false);
    expect(diff.fingerprintDiff.changed).toBe(true); // committed "stale" != fresh
  });

  it("a repo-root-RELATIVE dir is resolved against repoRoot", () => {
    // The provider resolves a relative dir; we only need it not to throw and to
    // dispatch to the npm branch (the source files won't exist → empty dump).
    const provider = makeRealDiffProvider(repoRoot, {
      fileReader: () => undefined,
      baselineReader: baselineReaderFrom({}),
      toolchainReader: TOOLCHAIN_READER,
    });

    const relativePkg: PackageDescriptor = {
      ...npmPkg,
      dir: "public/packages/typescript/result",
    };

    const diff = provider.getDiff({
      pkg: relativePkg,
      resolvedVersions: new Map(),
    });

    // No committed report/fp → baselineMissing + added-from-empty=false (no HEAD report).
    expect(diff.baselineMissing).toBe(true);
  });

  it("missing committed api.md baseline-ref → baselineMissing true", () => {
    const files: Record<string, string> = {
      [apiMdAbs]: API_MD,
      [fpAbs]: "x",
      [pkgJsonAbs]: PKG_JSON,
      [srcAbs]: "export const X = '';\n",
    };

    const provider = makeRealDiffProvider(repoRoot, {
      fileReader: fileReaderFrom(files),
      sourceLister: lister,
      baselineReader: baselineReaderFrom({}), // no committed report at ref
      toolchainReader: TOOLCHAIN_READER,
    });

    const diff = provider.getDiff({
      pkg: npmPkg,
      resolvedVersions: new Map(),
    });

    expect(diff.baselineMissing).toBe(true);
  });

  it("PROPAGATION (npm): a changed resolved @d2/* dep version flips the fingerprint", () => {
    const files: Record<string, string> = {
      [apiMdAbs]: API_MD,
      [pkgJsonAbs]: PKG_JSON,
      [srcAbs]: "export const X = '';\n",
    };
    const baseline = baselineReaderFrom({ [`HEAD|${apiMdAbs}`]: API_MD });

    const committed = composeNpmFpDirect(
      files,
      new Map([["@d2/utilities", "0.1.0"]]),
    );

    const provider = makeRealDiffProvider(repoRoot, {
      fileReader: fileReaderFrom({ ...files, [fpAbs]: committed }),
      sourceLister: lister,
      baselineReader: baseline,
      toolchainReader: TOOLCHAIN_READER,
    });

    const still010 = provider.getDiff({
      pkg: npmPkg,
      resolvedVersions: new Map([["@d2/utilities", "0.1.0"]]),
    });
    const moved020 = provider.getDiff({
      pkg: npmPkg,
      resolvedVersions: new Map([["@d2/utilities", "0.2.0"]]),
    });

    expect(still010.fingerprintDiff.changed).toBe(false);
    expect(moved020.fingerprintDiff.changed).toBe(true);
  });

  /** Recompute the npm fp exactly as the provider does, for test calibration. */
  function composeNpmFpDirect(
    files: Record<string, string>,
    resolved: ReadonlyMap<string, string>,
  ): string {
    const packageDir = "/abs/ts/result";
    const apiMd = files[`${packageDir}/etc/result.api.md`] ?? "";
    const pkgJson = JSON.parse(files[`${packageDir}/package.json`] ?? "{}") as {
      name?: string;
      version?: string;
      dependencies?: Record<string, string>;
    };
    const substituted = substituteResolvedDeps(pkgJson, resolved);

    const sourceRead = (relPosix: string): string =>
      files[`${packageDir}/${relPosix}`] ?? "";
    const relPaths = Object.keys(files)
      .filter((p) => p.startsWith(packageDir + "/"))
      .map((p) => p.slice(packageDir.length + 1))
      .filter(
        (rel) =>
          (rel.endsWith(".ts") && !rel.endsWith(".test.ts")) ||
          rel === "package.json" ||
          rel === "api-extractor.json" ||
          (rel.startsWith("tsconfig") && rel.endsWith(".json")),
      );

    const sourceDump = buildSourceDump(relPaths, sourceRead);

    return composeSourceFingerprint({
      sourceDump,
      apiReport: apiMd,
      depsJson: buildNpmManifestMeta(substituted),
      toolchainJson: readToolchainPin("npm", TOOLCHAIN_READER),
    });
  }
});

// ---------------------------------------------------------------------------
// Ecosystem discriminator guard
// ---------------------------------------------------------------------------

describe("makeRealDiffProvider — guards", () => {
  it("throws on a package with no ecosystem discriminator", () => {
    const provider = makeRealDiffProvider(repoRoot, {
      fileReader: () => undefined,
      baselineReader: baselineReaderFrom({}),
      toolchainReader: TOOLCHAIN_READER,
    });

    const broken = { ...nugetPkg, ecosystem: "" as unknown as "nuget" };

    expect(() =>
      provider.getDiff({ pkg: broken, resolvedVersions: new Map() }),
    ).toThrow(/no ecosystem discriminator/);
  });

  it("constructs with all default real seams (no options)", () => {
    const provider = makeRealDiffProvider(repoRoot);

    expect(typeof provider.getDiff).toBe("function");
  });
});

// ---------------------------------------------------------------------------
// makeRealFileReader — default seam (real temp file, both branches)
// ---------------------------------------------------------------------------

describe("makeRealFileReader", () => {
  it("reads an existing file; returns undefined for a missing file", () => {
    const dir = mkdtempSync(join(tmpdir(), "rfr-"));

    try {
      const file = join(dir, "a.txt");
      writeFileSync(file, "hello", "utf-8");

      const read = makeRealFileReader();

      expect(read(file)).toBe("hello");
      expect(read(join(dir, "missing.txt"))).toBeUndefined();
    } finally {
      rmSync(dir, { recursive: true, force: true });
    }
  });
});

// ---------------------------------------------------------------------------
// readPackageJsonFile — default seam (real temp file, both branches)
// ---------------------------------------------------------------------------

describe("readPackageJsonFile", () => {
  it("reads + parses a real package.json subset", () => {
    const dir = mkdtempSync(join(tmpdir(), "rdp-pkgjson-"));

    try {
      writeFileSync(
        join(dir, "package.json"),
        JSON.stringify({
          name: "@d2/x",
          version: "1.2.3",
          dependencies: { "@d2/utilities": "workspace:*" },
          private: true,
        }),
        "utf-8",
      );

      const meta = readPackageJsonFile(dir);

      expect(meta.name).toBe("@d2/x");
      expect(meta.version).toBe("1.2.3");
      expect(meta.dependencies).toEqual({ "@d2/utilities": "workspace:*" });
    } finally {
      rmSync(dir, { recursive: true, force: true });
    }
  });

  it("throws fail-loud when package.json is absent", () => {
    const dir = mkdtempSync(join(tmpdir(), "rdp-nopkg-"));

    try {
      expect(() => readPackageJsonFile(dir)).toThrow(/package.json not found/);
    } finally {
      rmSync(dir, { recursive: true, force: true });
    }
  });
});

// ---------------------------------------------------------------------------
// S9 — provider source-dump determinism against the REAL D2.Shared.Utilities tree
//
// This proves the provider's source-dump GLOB + build are deterministic over a
// real committed tree — NOT the seed↔provider byte-identity of the fingerprint
// COMPOSITION (that is pinned by feeding synthetic inputs to both the seed
// primitive and the provider's composeSourceFingerprint in
// seed-provider-fingerprint-identity.test.ts). The end-to-end seed↔runtime
// identity over real trees is additionally proven by the currency check
// (fingerprint-currency-cli), which recomputes every baseline via the runner.
// ---------------------------------------------------------------------------

describe("provider source-dump determinism (S9)", () => {
  it("the provider's nuget source dump over the real D2.Shared.Utilities tree is non-empty + deterministic", () => {
    // listSourceFiles walks the real committed tree; this proves the glob + dump
    // are deterministic against a real package (the seed uses the identical dump
    // algorithm). The COMPOSITION byte-identity is pinned separately; the drift
    // check pins the end-to-end identity.
    const utilDir = resolve(repoRoot, "public/packages/dotnet/utilities");
    const files = listSourceFiles(utilDir, "nuget");

    expect(files.length).toBeGreaterThan(0);
    expect(files.some((f) => f.endsWith(".csproj"))).toBe(true);
    expect(
      files.every((f) => !f.includes("/bin/") && !f.includes("/obj/")),
    ).toBe(true);

    const read = (rel: string): string =>
      makeRealFileReader()(join(utilDir, rel)) ?? "";

    expect(buildSourceDump(files, read)).toBe(buildSourceDump(files, read));
  });
});
