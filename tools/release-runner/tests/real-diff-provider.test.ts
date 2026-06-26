// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Tests for the production DiffProvider (real-diff-provider.ts).
//
// Unit (injected extractor seams — no real build / no real api-extractor):
//   - ecosystem dispatch (nuget → nuget extractor; npm → ts seams).
//   - PackageDiff mapping incl. baselineMissing propagation.
//   - the resolvedVersions → fingerprint fold (propagation, both ecosystems).
//   - composeNugetFingerprint / buildNugetManifestMeta / substituteResolvedDeps.
//
// Integration (gated D2_VERSIONING_INTEGRATION=1 — real build + il-fingerprint):
//   - B1 IL-dump path-independence (same source, two abs paths → identical dump).
//   - B2 IL-dump build-stability (two rebuilds → identical dump).
//   - B3 impl-change detection (private body change, no API delta → dump changes).
//   - MVID/timestamp-invariance (two DLLs differing only in those → identical).
//   These shell the il-fingerprint tool as a subprocess (no test project outside
//   the solution).

import { spawnSync } from "node:child_process";
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join, resolve } from "node:path";
import { describe, expect, it } from "vitest";
import {
  buildNugetManifestMeta,
  composeNugetFingerprint,
  makeRealDiffProvider,
  readPackageJsonFile,
  substituteResolvedDeps,
  type NugetExtractor,
  type TsExtractorSeams,
} from "../src/real-diff-provider.js";
import { computeDistFingerprint } from "../src/ts-api-adapter.js";
import type {
  DotnetShell,
  NugetExtractionResult,
} from "../src/nuget-extractor.js";
import type { PackageDescriptor } from "../src/types.js";
import { repoRoot } from "./repo-root.js";

const RUN_INTEGRATION = process.env["D2_VERSIONING_INTEGRATION"] === "1";

// ---------------------------------------------------------------------------
// Synthetic descriptors
// ---------------------------------------------------------------------------

const nugetPkg: PackageDescriptor = {
  name: "D2.Shared.Result",
  ecosystem: "nuget",
  dir: "server/shared/dotnet/result/core",
  manifestPath: "/abs/result/core/D2.Shared.Result.csproj",
  changelogPath: "/abs/result/core/CHANGELOG.md",
  currentVersion: "0.1.0",
  dependencies: ["D2.Shared.Utilities"],
};

const npmPkg: PackageDescriptor = {
  name: "@d2/result",
  ecosystem: "npm",
  // Absolute dir so the provider passes it through unchanged in tests.
  dir: "/abs/ts/result",
  manifestPath: "/abs/ts/result/package.json",
  changelogPath: "/abs/ts/result/CHANGELOG.md",
  currentVersion: "0.1.0",
  dependencies: ["@d2/utilities"],
};

// ---------------------------------------------------------------------------
// Synthetic seams
// ---------------------------------------------------------------------------

function makeNugetSeam(opts: {
  ilDump?: string;
  shipped?: string;
  unshipped?: string;
  committedFingerprint?: string | undefined;
  added?: boolean;
  removed?: boolean;
}): {
  extractor: NugetExtractor;
  shell: DotnetShell;
  calls: { extractor: number };
} {
  const calls = { extractor: 0 };

  const extractor: NugetExtractor = (
    _pkg: PackageDescriptor,
  ): NugetExtractionResult => {
    calls.extractor += 1;

    return {
      apiDiff: {
        added: opts.added ?? false,
        removed: opts.removed ?? false,
        changed: false,
      },
      ilDump: opts.ilDump ?? "# il-fingerprint v1\ntype Foo\n",
      shippedTxt: opts.shipped ?? "#nullable enable\n",
      unshippedTxt: opts.unshipped ?? "#nullable enable\n",
      extractionMs: 1,
    };
  };

  const shell: DotnetShell = {
    build() {
      return { status: 0, stdout: "", stderr: "" };
    },
    readFile(filePath: string): string | undefined {
      if (filePath.endsWith(".release-fingerprint"))
        return opts.committedFingerprint;
      return undefined;
    },
    ilDump() {
      return opts.ilDump ?? "";
    },
  };

  return { extractor, shell, calls };
}

function makeTsSeams(opts: {
  apiMd?: string;
  committedApiMd?: string | undefined;
  committedFingerprint?: string | undefined;
  jsContent?: string;
  packageJson?: {
    name?: string;
    version?: string;
    dependencies?: Record<string, string>;
  };
}): { seams: TsExtractorSeams; calls: { apiExtractor: number } } {
  const calls = { apiExtractor: 0 };
  const apiMd =
    opts.apiMd ??
    '## API Report File for "@d2/result"\n\n```ts\n\n// @public\nexport const X: string;\n\n```\n';

  const seams: TsExtractorSeams = {
    baselineReader: {
      read(filePath: string): string | undefined {
        if (filePath.endsWith(".api.md")) return opts.committedApiMd;
        if (filePath.endsWith("dist-fingerprint.txt"))
          return opts.committedFingerprint;
        return undefined;
      },
    },
    apiExtractorRunner: {
      run(): string {
        calls.apiExtractor += 1;
        return apiMd;
      },
    },
    distReader: {
      listFiles(_distDir: string, extensions: string[]): string[] {
        if (extensions.includes(".js")) return ["/abs/ts/result/dist/index.js"];
        if (extensions.includes(".d.ts"))
          return ["/abs/ts/result/dist/index.d.ts"];
        return [];
      },
      readFile(filePath: string): string {
        if (filePath.endsWith(".js"))
          return opts.jsContent ?? "export const X = 1;\n";
        return "export declare const X: string;\n";
      },
    },
    readPackageJson(): {
      name?: string;
      version?: string;
      dependencies?: Record<string, string>;
    } {
      return (
        opts.packageJson ?? {
          name: "@d2/result",
          version: "0.1.0",
          dependencies: { "@d2/utilities": "workspace:*" },
        }
      );
    },
  };

  return { seams, calls };
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
    const keys = Object.keys(JSON.parse(meta).deps);

    expect(keys).toEqual(["D2.Shared.Alpha", "D2.Shared.Zebra"]);
  });
});

// ---------------------------------------------------------------------------
// composeNugetFingerprint
// ---------------------------------------------------------------------------

describe("composeNugetFingerprint", () => {
  const base = {
    shippedTxt: "#nullable enable\nD2.Foo\n",
    unshippedTxt: "#nullable enable\n",
    ilDump: "# il-fingerprint v1\ntype Foo\n",
  };

  it("is deterministic for identical inputs", () => {
    const a = composeNugetFingerprint(base, '{"v":1}');
    const b = composeNugetFingerprint(base, '{"v":1}');
    expect(a).toBe(b);
  });

  it("changes when the IL dump changes (impl-change floor)", () => {
    const a = composeNugetFingerprint(base, "{}");
    const b = composeNugetFingerprint(
      { ...base, ilDump: "# il-fingerprint v1\ntype Foo CHANGED\n" },
      "{}",
    );
    expect(a).not.toBe(b);
  });

  it("changes when the manifest metadata changes (propagation)", () => {
    const a = composeNugetFingerprint(base, '{"deps":{"x":"0.1.0"}}');
    const b = composeNugetFingerprint(base, '{"deps":{"x":"0.2.0"}}');
    expect(a).not.toBe(b);
  });

  it("changes when the PublicAPI surface changes", () => {
    const a = composeNugetFingerprint(base, "{}");
    const b = composeNugetFingerprint(
      { ...base, shippedTxt: "#nullable enable\nD2.Foo\nD2.Bar\n" },
      "{}",
    );
    expect(a).not.toBe(b);
  });

  it("is CRLF/LF-insensitive (platform stability)", () => {
    const lf = composeNugetFingerprint(base, "{}");
    const crlf = composeNugetFingerprint(
      {
        shippedTxt: "#nullable enable\r\nD2.Foo\r\n",
        unshippedTxt: "#nullable enable\r\n",
        ilDump: "# il-fingerprint v1\r\ntype Foo\r\n",
      },
      "{}",
    );
    expect(lf).toBe(crlf);
  });
});

// ---------------------------------------------------------------------------
// substituteResolvedDeps
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
});

// ---------------------------------------------------------------------------
// makeRealDiffProvider — dispatch + mapping (injected seams)
// ---------------------------------------------------------------------------

describe("makeRealDiffProvider — ecosystem dispatch + mapping", () => {
  it("nuget package → routes to the nuget extractor + maps ApiDiff", () => {
    const { extractor, shell, calls } = makeNugetSeam({
      added: true,
      committedFingerprint: "deadbeef",
    });

    const provider = makeRealDiffProvider(repoRoot, {
      nugetExtractor: extractor,
      dotnetShell: shell,
    });

    const diff = provider.getDiff({
      pkg: nugetPkg,
      resolvedVersions: new Map([["D2.Shared.Result", "0.1.0"]]),
    });

    expect(calls.extractor).toBe(1);
    expect(diff.apiDiff.added).toBe(true);
    // Committed fingerprint differs from the freshly composed one → changed.
    expect(diff.fingerprintDiff.changed).toBe(true);
    expect(diff.baselineMissing).toBe(false);
  });

  it("nuget missing baseline → baselineMissing true, fingerprint changed", () => {
    const { extractor, shell } = makeNugetSeam({
      committedFingerprint: undefined,
    });

    const provider = makeRealDiffProvider(repoRoot, {
      nugetExtractor: extractor,
      dotnetShell: shell,
    });

    const diff = provider.getDiff({
      pkg: nugetPkg,
      resolvedVersions: new Map(),
    });

    expect(diff.baselineMissing).toBe(true);
    expect(diff.fingerprintDiff.changed).toBe(true);
  });

  it("nuget unchanged baseline → no diff (fingerprint matches)", () => {
    // First compose the fingerprint the provider WILL produce, then feed it back
    // as the committed baseline so the diff is clean (a no-op).
    const ilDump = "# il-fingerprint v1\ntype Foo\n";
    const shipped = "#nullable enable\nD2.Foo\n";
    const unshipped = "#nullable enable\n";
    const manifestMeta = buildNugetManifestMeta(
      nugetPkg,
      new Map([["D2.Shared.Utilities", "0.1.0"]]),
    );
    const expectedFp = composeNugetFingerprint(
      { shippedTxt: shipped, unshippedTxt: unshipped, ilDump },
      manifestMeta,
    );

    const { extractor, shell } = makeNugetSeam({
      ilDump,
      shipped,
      unshipped,
      committedFingerprint: expectedFp,
    });

    const provider = makeRealDiffProvider(repoRoot, {
      nugetExtractor: extractor,
      dotnetShell: shell,
    });

    const diff = provider.getDiff({
      pkg: nugetPkg,
      resolvedVersions: new Map([["D2.Shared.Utilities", "0.1.0"]]),
    });

    expect(diff.fingerprintDiff.changed).toBe(false);
    expect(diff.apiDiff).toEqual({
      added: false,
      removed: false,
      changed: false,
    });
  });

  it("PROPAGATION (nuget): a changed resolved dep version flips the fingerprint", () => {
    const ilDump = "# il-fingerprint v1\ntype Foo\n";
    const shipped = "#nullable enable\n";
    const unshipped = "#nullable enable\n";

    // Baseline computed with the dep at 0.1.0.
    const baselineFp = composeNugetFingerprint(
      { shippedTxt: shipped, unshippedTxt: unshipped, ilDump },
      buildNugetManifestMeta(
        nugetPkg,
        new Map([["D2.Shared.Utilities", "0.1.0"]]),
      ),
    );

    const { extractor, shell } = makeNugetSeam({
      ilDump,
      shipped,
      unshipped,
      committedFingerprint: baselineFp,
    });

    const provider = makeRealDiffProvider(repoRoot, {
      nugetExtractor: extractor,
      dotnetShell: shell,
    });

    // Now the dep resolved to 0.2.0 (a propagated bump) → fingerprint moves.
    const diff = provider.getDiff({
      pkg: nugetPkg,
      resolvedVersions: new Map([["D2.Shared.Utilities", "0.2.0"]]),
    });

    expect(diff.fingerprintDiff.changed).toBe(true);
    // No API change — propagation manifests purely as the fingerprint floor.
    expect(diff.apiDiff.added).toBe(false);
    expect(diff.apiDiff.removed).toBe(false);
  });

  it("npm package → routes to the ts seams + maps ApiDiff", () => {
    const { seams, calls } = makeTsSeams({
      committedApiMd:
        '## API Report File for "@d2/result"\n\n```ts\n\n// @public\nexport const X: string;\n\n```\n',
      committedFingerprint: "stale",
    });

    const provider = makeRealDiffProvider(repoRoot, { tsSeams: seams });

    const diff = provider.getDiff({
      pkg: npmPkg,
      resolvedVersions: new Map(),
    });

    expect(calls.apiExtractor).toBe(1);
    // Same members → no API diff.
    expect(diff.apiDiff.added).toBe(false);
    expect(diff.apiDiff.removed).toBe(false);
    // committed fingerprint "stale" differs from fresh → changed.
    expect(diff.fingerprintDiff.changed).toBe(true);
  });

  it("npm package with a repo-root-RELATIVE dir → resolved against repoRoot", () => {
    const { seams, calls } = makeTsSeams({
      committedApiMd:
        '## API Report File for "@d2/result"\n\n```ts\n\n// @public\nexport const X: string;\n\n```\n',
      committedFingerprint: "any-committed-value",
    });

    const provider = makeRealDiffProvider(repoRoot, { tsSeams: seams });

    // A relative dir exercises the resolve(repoRoot, dir) branch.
    const relativePkg: PackageDescriptor = {
      ...npmPkg,
      dir: "server/shared/typescript/result",
    };

    const diff = provider.getDiff({
      pkg: relativePkg,
      resolvedVersions: new Map(),
    });

    // No real fs needed — all seams injected; the dispatch + resolve path runs.
    // The baseline is present (committed api.md + fingerprint both set).
    expect(calls.apiExtractor).toBe(1);
    expect(diff.baselineMissing).toBe(false);
  });

  it("npm missing api.md baseline → baselineMissing true", () => {
    const { seams } = makeTsSeams({ committedApiMd: undefined });

    const provider = makeRealDiffProvider(repoRoot, { tsSeams: seams });

    const diff = provider.getDiff({
      pkg: npmPkg,
      resolvedVersions: new Map(),
    });

    expect(diff.baselineMissing).toBe(true);
  });

  it("PROPAGATION (npm): a changed resolved @d2/* dep version flips the fingerprint", () => {
    // Mirrors the nuget propagation test: compute the REAL fingerprint at
    // dep@0.1.0, set THAT as the committed baseline, then assert:
    //   - dep still at 0.1.0 → changed: false (fingerprint unchanged)
    //   - dep moved to 0.2.0 → changed: true (fingerprint moves)
    //
    // This proves that the dep version participates in the fingerprint (via
    // substituteResolvedDeps) and is NOT a vacuous comparison against a
    // known-wrong literal.

    const API_MD =
      '## API Report File for "@d2/result"\n\n```ts\n\n// @public\nexport const X: string;\n\n```\n';

    // The base package.json the seams return (workspace:* dep, resolved later).
    const baselinePkg = {
      name: "@d2/result",
      version: "0.1.0",
      dependencies: { "@d2/utilities": "workspace:*" },
    };

    const resolvedAt010 = new Map([["@d2/utilities", "0.1.0"]]);
    const resolvedAt020 = new Map([["@d2/utilities", "0.2.0"]]);

    // --- Step 1: derive the real fingerprint at dep@0.1.0 -----------------
    // substituteResolvedDeps replaces workspace:* → 0.1.0 in the metadata.
    // computeDistFingerprint hashes dist files + substituted metadata.
    // Both are pure helpers — no api-extractor, no IO beyond the injected DistReader.
    const { seams: seamsForDerive } = makeTsSeams({ committedApiMd: API_MD });

    const realFpAt010 = computeDistFingerprint(
      npmPkg.dir,
      substituteResolvedDeps(baselinePkg, resolvedAt010),
      seamsForDerive.distReader,
    );

    // --- Step 2: set realFpAt010 as the committed baseline -----------------
    const { seams: seamsVerify } = makeTsSeams({
      committedApiMd: API_MD,
      committedFingerprint: realFpAt010,
    });
    const providerVerify = makeRealDiffProvider(repoRoot, {
      tsSeams: seamsVerify,
    });

    // dep still at 0.1.0 → fingerprint matches committed → changed: false.
    const still010 = providerVerify.getDiff({
      pkg: npmPkg,
      resolvedVersions: resolvedAt010,
    });

    // dep moved to 0.2.0 → substituted metadata changes → fingerprint differs → changed: true.
    const moved020 = providerVerify.getDiff({
      pkg: npmPkg,
      resolvedVersions: resolvedAt020,
    });

    expect(still010.fingerprintDiff.changed).toBe(false);
    expect(moved020.fingerprintDiff.changed).toBe(true);
  });

  it("throws on a package with no ecosystem discriminator", () => {
    const provider = makeRealDiffProvider(repoRoot, {
      nugetExtractor: makeNugetSeam({}).extractor,
      dotnetShell: makeNugetSeam({}).shell,
    });

    const broken = { ...nugetPkg, ecosystem: "" as unknown as "nuget" };

    expect(() =>
      provider.getDiff({ pkg: broken, resolvedVersions: new Map() }),
    ).toThrow(/no ecosystem discriminator/);
  });
});

// ---------------------------------------------------------------------------
// PROPAGATION (npm) — direct proof that the dep version moves the dist hash
// ---------------------------------------------------------------------------

describe("substituteResolvedDeps feeds computeDistFingerprint movement", () => {
  it("two resolved dep versions yield different substituted metadata", () => {
    const at010 = substituteResolvedDeps(
      {
        name: "@d2/result",
        version: "0.1.0",
        dependencies: { "@d2/utilities": "workspace:*" },
      },
      new Map([["@d2/utilities", "0.1.0"]]),
    );
    const at020 = substituteResolvedDeps(
      {
        name: "@d2/result",
        version: "0.1.0",
        dependencies: { "@d2/utilities": "workspace:*" },
      },
      new Map([["@d2/utilities", "0.2.0"]]),
    );

    expect(JSON.stringify(at010)).not.toBe(JSON.stringify(at020));
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
// Integration — IL-dumper proofs (gated; real build + il-fingerprint subprocess)
// ---------------------------------------------------------------------------

const ilFingerprintProject = resolve(repoRoot, "tools/il-fingerprint");

/** Shell the il-fingerprint tool against a built DLL; returns the dump string. */
function ilDump(dllPath: string): string {
  const result = spawnSync(
    "dotnet",
    [
      "run",
      "--project",
      ilFingerprintProject,
      "-c",
      "Release",
      "--no-build",
      "--",
      dllPath,
    ],
    { encoding: "utf-8", maxBuffer: 64 * 1024 * 1024 },
  );

  if (result.status !== 0) {
    throw new Error(
      `il-fingerprint failed (exit ${String(result.status)}): ${result.stderr}`,
    );
  }

  return result.stdout ?? "";
}

// All three proofs use SELF-CONTAINED fixture projects (explicit TargetFramework,
// no repo Directory.Build.props dependency, no ProjectReferences) built under a
// fresh temp dir per test. This isolates them from the gated nuget-extractor
// integration tests that mutate + rebuild the shared D2.Shared.Utilities project
// — vitest runs test files in parallel, so a shared-project build would collide.
describe.skipIf(!RUN_INTEGRATION)("IL-fingerprint determinism proofs", () => {
  // Ensure the il-fingerprint tool is built once before --no-build runs.
  function ensureToolBuilt(): void {
    const result = spawnSync(
      "dotnet",
      ["build", ilFingerprintProject, "-c", "Release"],
      { encoding: "utf-8", maxBuffer: 64 * 1024 * 1024 },
    );

    if (result.status !== 0) {
      throw new Error(`il-fingerprint tool build failed: ${result.stderr}`);
    }
  }

  const CSPROJ =
    '<Project Sdk="Microsoft.NET.Sdk">\n' +
    "  <PropertyGroup>\n" +
    "    <OutputType>Library</OutputType>\n" +
    "    <TargetFramework>net10.0</TargetFramework>\n" +
    "    <Nullable>enable</Nullable>\n" +
    "    <ImplicitUsings>enable</ImplicitUsings>\n" +
    "    <AssemblyName>IlFpFixture</AssemblyName>\n" +
    "    <Deterministic>true</Deterministic>\n" +
    "  </PropertyGroup>\n" +
    "</Project>\n";

  /** A fixture with a private method body whose literal varies via `bodyTag`. */
  function fixtureSource(bodyTag: string): string {
    return (
      "namespace IlFpFixture;\n\n" +
      "public static class Sample\n{\n" +
      "    public static int Add(int a, int b) => a + b;\n\n" +
      "    public static string Describe() => Inner();\n\n" +
      `    private static string Inner() => "${bodyTag}";\n` +
      "}\n"
    );
  }

  /** Write + build a fixture at `root`, return its IL dump. */
  function buildFixtureAt(root: string, bodyTag: string): string {
    const dir = join(root, "fixture");
    mkdirSync(dir, { recursive: true });
    writeFileSync(join(dir, "IlFpFixture.csproj"), CSPROJ, "utf-8");
    writeFileSync(join(dir, "Sample.cs"), fixtureSource(bodyTag), "utf-8");

    const build = spawnSync(
      "dotnet",
      ["build", join(dir, "IlFpFixture.csproj"), "-c", "Release"],
      { encoding: "utf-8", maxBuffer: 64 * 1024 * 1024 },
    );

    if (build.status !== 0) {
      throw new Error(
        `fixture build failed at ${root}: ${build.stdout}\n${build.stderr}`,
      );
    }

    return ilDump(join(dir, "bin/Release/net10.0/IlFpFixture.dll"));
  }

  it("B1: building the SAME source from a DIFFERENT absolute path → identical IL dump", () => {
    ensureToolBuilt();

    // The core path-independence proof: identical source built at two different
    // absolute paths → byte-identical IL dump (a raw DLL hash would FAIL this
    // because the source path feeds the module MVID).
    const rootA = mkdtempSync(join(tmpdir(), "il-fp-pathA-"));
    const rootB = mkdtempSync(join(tmpdir(), "il-fp-pathB-different-"));

    try {
      const dumpA = buildFixtureAt(rootA, "constant-tag");
      const dumpB = buildFixtureAt(rootB, "constant-tag");

      expect(dumpA).toBe(dumpB);
      expect(dumpA).toContain("type IlFpFixture.Sample");
    } finally {
      rmSync(rootA, { recursive: true, force: true });
      rmSync(rootB, { recursive: true, force: true });
    }
  }, 300_000);

  it("B2: two rebuilds of the same source at the same path → identical IL dump", () => {
    ensureToolBuilt();
    const root = mkdtempSync(join(tmpdir(), "il-fp-stable-"));

    try {
      const dumpA = buildFixtureAt(root, "stable-tag");
      const dumpB = buildFixtureAt(root, "stable-tag");

      expect(dumpA).toBe(dumpB);
      expect(dumpA.startsWith("# il-fingerprint v1")).toBe(true);
    } finally {
      rmSync(root, { recursive: true, force: true });
    }
  }, 300_000);

  it("B3: a private method-body change with NO public-API delta → IL dump changes", () => {
    ensureToolBuilt();
    const root = mkdtempSync(join(tmpdir(), "il-fp-implchange-"));

    try {
      // Same PUBLIC surface (Add / Describe), different PRIVATE body literal.
      const dumpClean = buildFixtureAt(root, "body-before");
      const dumpChanged = buildFixtureAt(root, "body-after-changed");

      expect(dumpChanged).not.toBe(dumpClean);
    } finally {
      rmSync(root, { recursive: true, force: true });
    }
  }, 300_000);

  it("B4: MVID/timestamp-invariance — two non-deterministic builds of identical source → identical dump", () => {
    // Direct proof of the DumpBytes seam contract: when the PE image differs only
    // in its COFF timestamp and module MVID (Deterministic=false regenerates both
    // on every build), the normalized dump is byte-identical.
    ensureToolBuilt();

    const NON_DET_CSPROJ =
      '<Project Sdk="Microsoft.NET.Sdk">\n' +
      "  <PropertyGroup>\n" +
      "    <OutputType>Library</OutputType>\n" +
      "    <TargetFramework>net10.0</TargetFramework>\n" +
      "    <Nullable>enable</Nullable>\n" +
      "    <ImplicitUsings>enable</ImplicitUsings>\n" +
      "    <AssemblyName>IlFpMvidFixture</AssemblyName>\n" +
      "    <Deterministic>false</Deterministic>\n" +
      "  </PropertyGroup>\n" +
      "</Project>\n";

    const source =
      "namespace IlFpMvidFixture;\n\n" +
      "public static class Stable\n{\n" +
      "    public static int Add(int a, int b) => a + b;\n" +
      "}\n";

    const root = mkdtempSync(join(tmpdir(), "il-fp-mvid-"));

    try {
      const dir = join(root, "fixture");
      mkdirSync(dir, { recursive: true });
      writeFileSync(
        join(dir, "IlFpMvidFixture.csproj"),
        NON_DET_CSPROJ,
        "utf-8",
      );
      writeFileSync(join(dir, "Stable.cs"), source, "utf-8");

      const dllPath = join(dir, "bin/Release/net10.0/IlFpMvidFixture.dll");

      const build1 = spawnSync(
        "dotnet",
        ["build", join(dir, "IlFpMvidFixture.csproj"), "-c", "Release"],
        { encoding: "utf-8", maxBuffer: 64 * 1024 * 1024 },
      );

      if (build1.status !== 0)
        throw new Error(
          `MVID test build1 failed: ${build1.stdout}\n${build1.stderr}`,
        );

      const dump1 = ilDump(dllPath);

      // Force a genuine second build: Deterministic=false + --no-incremental
      // guarantees a new MVID and a new COFF timestamp.
      const build2 = spawnSync(
        "dotnet",
        [
          "build",
          join(dir, "IlFpMvidFixture.csproj"),
          "-c",
          "Release",
          "--no-incremental",
        ],
        { encoding: "utf-8", maxBuffer: 64 * 1024 * 1024 },
      );

      if (build2.status !== 0)
        throw new Error(
          `MVID test build2 failed: ${build2.stdout}\n${build2.stderr}`,
        );

      const dump2 = ilDump(dllPath);

      // Despite differing MVID + timestamp, the normalized dump is identical.
      expect(dump1).toBe(dump2);
      expect(dump1).toContain("type IlFpMvidFixture.Stable");
    } finally {
      rmSync(root, { recursive: true, force: true });
    }
  }, 300_000);

  it("B5: unbox opcode (0x79) correctly sized — method body containing unbox decodes without corruption", () => {
    // Regression pin for the unbox opcode fix: 0x79 (unbox) was absent from
    // OperandSize and IsTokenOpcode. The 4 type-token bytes were consumed as 4
    // no-operand opcodes, shifting the decoder and corrupting subsequent
    // instructions. This test proves the body decodes cleanly: both the unbox
    // method AND the Sentinel method that follows it appear in the dump.
    ensureToolBuilt();

    const CSPROJ_UNBOX =
      '<Project Sdk="Microsoft.NET.Sdk">\n' +
      "  <PropertyGroup>\n" +
      "    <OutputType>Library</OutputType>\n" +
      "    <TargetFramework>net10.0</TargetFramework>\n" +
      "    <Nullable>enable</Nullable>\n" +
      "    <ImplicitUsings>enable</ImplicitUsings>\n" +
      "    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>\n" +
      "    <AssemblyName>IlFpUnboxFixture</AssemblyName>\n" +
      "    <Deterministic>true</Deterministic>\n" +
      "  </PropertyGroup>\n" +
      "</Project>\n";

    // Unsafe.Unbox<T>(object) emits `unbox T` (opcode 0x79) + use of the managed
    // pointer — not `unbox.any` (0xA5) which discards the pointer and loads value.
    const SOURCE_UNBOX =
      "using System.Runtime.CompilerServices;\n\n" +
      "namespace IlFpUnboxFixture;\n\n" +
      "public static class UnboxSample\n{\n" +
      "    public static ref int UnboxInt(object box) =>\n" +
      "        ref Unsafe.Unbox<int>(box);\n\n" +
      "    // Sentinel: if unbox was mis-sized (4 token bytes read as 4 opcodes),\n" +
      "    // the decoder shifts and this method would not appear cleanly.\n" +
      "    public static int Sentinel() => 42;\n" +
      "}\n";

    const root = mkdtempSync(join(tmpdir(), "il-fp-unbox-"));

    try {
      const dir = join(root, "fixture");
      mkdirSync(dir, { recursive: true });
      writeFileSync(
        join(dir, "IlFpUnboxFixture.csproj"),
        CSPROJ_UNBOX,
        "utf-8",
      );
      writeFileSync(join(dir, "UnboxSample.cs"), SOURCE_UNBOX, "utf-8");

      const build = spawnSync(
        "dotnet",
        ["build", join(dir, "IlFpUnboxFixture.csproj"), "-c", "Release"],
        { encoding: "utf-8", maxBuffer: 64 * 1024 * 1024 },
      );

      if (build.status !== 0)
        throw new Error(
          `unbox fixture build failed: ${build.stdout}\n${build.stderr}`,
        );

      const dump = ilDump(
        join(dir, "bin/Release/net10.0/IlFpUnboxFixture.dll"),
      );

      // Both methods must appear — decoder was not corrupted by the unbox token.
      expect(dump).toContain("method Sentinel");
      expect(dump).toContain("method UnboxInt");
      // The unbox/generic-call type token resolves to a type name, not raw hex.
      // Unsafe.Unbox<int> renders as MS:MR:…Unsafe.Unbox<Int32> in the dump.
      expect(dump).toContain("Unsafe.Unbox");
    } finally {
      rmSync(root, { recursive: true, force: true });
    }
  }, 300_000);

  it("B6: generic method type-arg change is detected (MethodSpecification resolved)", () => {
    // Regression pin for the MethodSpecification fix: the default arm returned
    // the constant string "MethodSpecification" regardless of generic type args,
    // so swapping int for string at a call site was invisible.
    // After the fix, the instantiation type args are resolved and the dumps differ.
    ensureToolBuilt();

    const CSPROJ_GENERIC =
      '<Project Sdk="Microsoft.NET.Sdk">\n' +
      "  <PropertyGroup>\n" +
      "    <OutputType>Library</OutputType>\n" +
      "    <TargetFramework>net10.0</TargetFramework>\n" +
      "    <Nullable>enable</Nullable>\n" +
      "    <ImplicitUsings>enable</ImplicitUsings>\n" +
      "    <AssemblyName>IlFpGenericFixture</AssemblyName>\n" +
      "    <Deterministic>true</Deterministic>\n" +
      "  </PropertyGroup>\n" +
      "</Project>\n";

    function genericSource(typeArg: string): string {
      return (
        "using System.Collections.Generic;\n\n" +
        "namespace IlFpGenericFixture;\n\n" +
        "public static class GenericCaller\n{\n" +
        "    public static List<T> MakeList<T>(T item) => new List<T> { item };\n\n" +
        "    public static object CallSite() =>\n" +
        `        MakeList<${typeArg}>(default!);\n` +
        "}\n"
      );
    }

    const rootInt = mkdtempSync(join(tmpdir(), "il-fp-gint-"));
    const rootStr = mkdtempSync(join(tmpdir(), "il-fp-gstr-"));

    try {
      const buildGenericFixture = (root: string, typeArg: string): string => {
        const dir = join(root, "fixture");
        mkdirSync(dir, { recursive: true });
        writeFileSync(
          join(dir, "IlFpGenericFixture.csproj"),
          CSPROJ_GENERIC,
          "utf-8",
        );
        writeFileSync(
          join(dir, "GenericCaller.cs"),
          genericSource(typeArg),
          "utf-8",
        );

        const build = spawnSync(
          "dotnet",
          ["build", join(dir, "IlFpGenericFixture.csproj"), "-c", "Release"],
          { encoding: "utf-8", maxBuffer: 64 * 1024 * 1024 },
        );

        if (build.status !== 0)
          throw new Error(
            `generic fixture build (${typeArg}) failed: ${build.stdout}\n${build.stderr}`,
          );

        return ilDump(join(dir, "bin/Release/net10.0/IlFpGenericFixture.dll"));
      };

      const dumpInt = buildGenericFixture(rootInt, "int");
      const dumpStr = buildGenericFixture(rootStr, "string");

      // Before the fix: both produced identical dumps (MethodSpecification constant string).
      // After the fix: instantiation type arg is in the dump; dumps differ.
      expect(dumpInt).not.toBe(dumpStr);
      // Sanity: both decode CallSite cleanly.
      expect(dumpInt).toContain("method CallSite");
      expect(dumpStr).toContain("method CallSite");
    } finally {
      rmSync(rootInt, { recursive: true, force: true });
      rmSync(rootStr, { recursive: true, force: true });
    }
  }, 300_000);
});
