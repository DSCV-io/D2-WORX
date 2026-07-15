// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

// Tests for the source-based portable fingerprint (source-fingerprint.ts).
//
// The whole win of the source-based design: the fingerprint is byte-stable BY
// CONSTRUCTION (hashing committed LF text), so every proof needs NO build and
// runs in the plain unit lane.
//
//   S1 recompute determinism            S6 CRLF/LF normalization
//   S2 source change moves it           S7 git-ref apiDiff (see nuget/ts tests)
//   S3 API-report change moves it       S8 source-dump glob correctness
//   S4 dep-version change moves it       S9 provider source-dump determinism (provider test)
//   S5 toolchain-pin change moves it
//
// S4/S7/S9 are exercised in real-diff-provider.test.ts (they need the provider
// composition / the git-ref reader). This file pins the pure composer + walker +
// toolchain reader against synthetic inputs (S1, S2, S3, S5, S6, S8).

import { mkdtempSync, mkdirSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { describe, expect, it } from "vitest";
import {
  buildSourceDump,
  composeSourceFingerprint,
  listSourceFiles,
  makeGitTrackedLister,
  makeRepoFileReader,
  normalizeLf,
  readToolchainPin,
  stableJson,
  type RepoFileReader,
  type SourceFileReader,
} from "../src/source-fingerprint.js";
import { repoRoot } from "./repo-root.js";

// ---------------------------------------------------------------------------
// normalizeLf
// ---------------------------------------------------------------------------

describe("normalizeLf", () => {
  it("converts CRLF to LF", () => {
    expect(normalizeLf("a\r\nb\r\n")).toBe("a\nb\n");
  });

  it("leaves LF-only text unchanged + is idempotent", () => {
    const lf = "a\nb\n";
    expect(normalizeLf(lf)).toBe(lf);
    expect(normalizeLf(normalizeLf("a\r\nb"))).toBe("a\nb");
  });
});

// ---------------------------------------------------------------------------
// stableJson
// ---------------------------------------------------------------------------

describe("stableJson", () => {
  it("serializes keys in ascending order regardless of input order", () => {
    expect(stableJson({ b: "2", a: "1" })).toBe(stableJson({ a: "1", b: "2" }));
    expect(stableJson({ b: "2", a: "1" })).toBe('{"a":"1","b":"2"}');
  });

  it("coerces an undefined value to empty string", () => {
    expect(stableJson({ a: undefined as unknown as string })).toBe('{"a":""}');
  });
});

// ---------------------------------------------------------------------------
// buildSourceDump — ordering + LF normalization + boundary prefixing
// ---------------------------------------------------------------------------

describe("buildSourceDump", () => {
  const read: SourceFileReader = (relPath) => {
    const files: Record<string, string> = {
      "src/b.ts": "export const B = 2;\n",
      "src/a.ts": "export const A = 1;\n",
      "package.json": '{"name":"@dcsv-io/d2-x"}\n',
    };

    return files[relPath] ?? "";
  };

  it("sorts files by POSIX path (ordinal) regardless of input order", () => {
    const a = buildSourceDump(["src/b.ts", "package.json", "src/a.ts"], read);
    const b = buildSourceDump(["src/a.ts", "src/b.ts", "package.json"], read);

    expect(a).toBe(b);
    // package.json sorts before src/* (ordinal 'p' < 's'), a before b.
    expect(a.indexOf("F:package.json")).toBeLessThan(a.indexOf("F:src/a.ts"));
    expect(a.indexOf("F:src/a.ts")).toBeLessThan(a.indexOf("F:src/b.ts"));
  });

  it("prefixes each file with F:<relPath> + terminates with a trailing LF so a boundary shift cannot collide", () => {
    const dump = buildSourceDump(["src/a.ts"], read);

    // F:<path>\n + LF-normalized content (already ends \n) + the per-file \n.
    expect(dump).toBe("F:src/a.ts\nexport const A = 1;\n\n");
  });

  it("LF-normalizes file content (CRLF source → same dump as LF source)", () => {
    const crlf: SourceFileReader = () => "export const A = 1;\r\n";
    const lf: SourceFileReader = () => "export const A = 1;\n";

    expect(buildSourceDump(["src/a.ts"], crlf)).toBe(
      buildSourceDump(["src/a.ts"], lf),
    );
  });
});

// ---------------------------------------------------------------------------
// listSourceFiles — S8 glob correctness (injected git-tracked file set)
// ---------------------------------------------------------------------------
//
// listSourceFiles enumerates COMMITTED (git-tracked) files via a TrackedFileLister
// then applies the per-ecosystem allowlist. The default lister shells `git
// ls-files`; here we inject a synthetic tracked set to assert the allowlist (incl.
// the mandatory exclusion of gitignored build transients — they never appear in
// the tracked set, so the test set deliberately omits them).

describe("listSourceFiles — glob correctness (S8)", () => {
  it("npm: includes src .ts (incl .g.ts) + package.json + tsconfig*.json + api-extractor.json; excludes dist/etc/tests + .test.ts", () => {
    const tracked = (): string[] => [
      "src/index.ts",
      "src/generated/thing.g.ts",
      "src/skip.test.ts",
      "package.json",
      "tsconfig.json",
      "tsconfig.test.json",
      "api-extractor.json",
      "CHANGELOG.md",
      "dist/index.js",
      "etc/.release-fingerprint",
      "etc/x.api.md",
      "tests/a.ts",
    ];

    const found = listSourceFiles("/pkg", "npm", tracked).sort();

    expect(found).toEqual([
      "api-extractor.json",
      "package.json",
      "src/generated/thing.g.ts",
      "src/index.ts",
      "tsconfig.json",
      "tsconfig.test.json",
    ]);
  });

  it("nuget: includes .cs (incl Generated .g.cs) + .csproj; excludes obj + the baseline files + CHANGELOG", () => {
    const tracked = (): string[] => [
      "Thing.cs",
      "Generated/codes.g.cs",
      "DcsvIo.D2.Thing.csproj",
      "PublicAPI.Shipped.txt",
      "PublicAPI.Unshipped.txt",
      ".release-fingerprint",
      "CHANGELOG.md",
      "obj/Thing.g.cs",
    ];

    const found = listSourceFiles("/pkg", "nuget", tracked).sort();

    expect(found).toEqual([
      "DcsvIo.D2.Thing.csproj",
      "Generated/codes.g.cs",
      "Thing.cs",
    ]);
  });

  it("normalizes backslash paths from the tracked lister to POSIX", () => {
    const tracked = (): string[] => ["src\\index.ts"];

    expect(listSourceFiles("/pkg", "npm", tracked)).toEqual(["src/index.ts"]);
  });

  it("returns empty when the tracked lister returns nothing", () => {
    expect(listSourceFiles("/pkg", "npm", () => [])).toEqual([]);
  });
});

// ---------------------------------------------------------------------------
// makeGitTrackedLister — default seam (real `git ls-files` against the repo)
// ---------------------------------------------------------------------------

describe("makeGitTrackedLister", () => {
  it("lists tracked files under a real package dir (excludes gitignored build transients)", () => {
    const utilDir = join(repoRoot, "public/packages/dotnet/utilities");
    const tracked = makeGitTrackedLister()(utilDir);

    expect(tracked.length).toBeGreaterThan(0);
    expect(tracked.some((f) => f.endsWith(".csproj"))).toBe(true);
    // The gitignored, non-deterministic LoggerMessage.g.cs is never tracked.
    expect(
      tracked.some((f) =>
        f.endsWith("/LoggerMessageGenerator/LoggerMessage.g.cs"),
      ),
    ).toBe(false);
  });

  it("returns empty for a non-existent dir (git fails → [])", () => {
    expect(
      makeGitTrackedLister()(join(repoRoot, "no-such-dir-xyz-123")),
    ).toEqual([]);
  });
});

// ---------------------------------------------------------------------------
// makeRepoFileReader — default seam (real temp file)
// ---------------------------------------------------------------------------

describe("makeRepoFileReader", () => {
  it("reads a repo-relative file from disk", () => {
    const root = mkdtempSync(join(tmpdir(), "sf-repo-"));

    try {
      mkdirSync(join(root, "server"), { recursive: true });
      writeFileSync(join(root, "server", "x.json"), '{"k":"v"}', "utf-8");

      const read = makeRepoFileReader(root);

      expect(read("server/x.json")).toBe('{"k":"v"}');
    } finally {
      rmSync(root, { recursive: true, force: true });
    }
  });
});

// ---------------------------------------------------------------------------
// readToolchainPin — both ecosystems (injected reader)
// ---------------------------------------------------------------------------

describe("readToolchainPin", () => {
  const nugetReader: RepoFileReader = (p) => {
    if (p === "global.json")
      return JSON.stringify({
        sdk: { version: "10.0.200", rollForward: "latestFeature" },
      });

    if (p === "Directory.Build.props")
      return "<Project><PropertyGroup><TargetFramework>net10.0</TargetFramework><LangVersion>latest</LangVersion></PropertyGroup></Project>";

    throw new Error(`unexpected read ${p}`);
  };

  const npmReader: RepoFileReader = (p) => {
    if (p === "package.json")
      return JSON.stringify({ devDependencies: { typescript: "5.9.3" } });

    if (p === "public/packages/typescript/tsconfig.base.json")
      return JSON.stringify({
        compilerOptions: { target: "ES2022", module: "ESNext" },
      });

    throw new Error(`unexpected read ${p}`);
  };

  it("nuget: reads sdk + rollForward + TargetFramework + LangVersion into sorted JSON", () => {
    const pin = readToolchainPin("nuget", nugetReader);

    expect(JSON.parse(pin)).toEqual({
      langVersion: "latest",
      rollForward: "latestFeature",
      sdk: "10.0.200",
      targetFramework: "net10.0",
    });
    // Keys are ascending (stable across hosts).
    expect(pin).toBe(
      '{"langVersion":"latest","rollForward":"latestFeature","sdk":"10.0.200","targetFramework":"net10.0"}',
    );
  });

  it("npm: reads typescript + target + module into sorted JSON", () => {
    const pin = readToolchainPin("npm", npmReader);

    expect(JSON.parse(pin)).toEqual({
      module: "ESNext",
      target: "ES2022",
      typescript: "5.9.3",
    });
  });

  it("tolerates missing fields (empty strings, no throw)", () => {
    const emptyNuget: RepoFileReader = (p) =>
      p === "global.json" ? "{}" : "<Project></Project>";

    const pin = readToolchainPin("nuget", emptyNuget);

    expect(JSON.parse(pin)).toEqual({
      langVersion: "",
      rollForward: "",
      sdk: "",
      targetFramework: "",
    });
  });

  it("npm: tolerates missing typescript devDep + missing compilerOptions", () => {
    const emptyNpm: RepoFileReader = (p) =>
      p === "package.json" ? "{}" : "{}";

    const pin = readToolchainPin("npm", emptyNpm);

    expect(JSON.parse(pin)).toEqual({ module: "", target: "", typescript: "" });
  });
});

// ---------------------------------------------------------------------------
// composeSourceFingerprint — S1, S2, S3, S5, S6
// ---------------------------------------------------------------------------

describe("composeSourceFingerprint", () => {
  const base = {
    sourceDump: "F:src/index.ts\nexport const X = 1;\n",
    apiReport: "#nullable enable\nD2.Foo\n",
    depsJson: '{"packageId":"X","version":"0.1.0","deps":{}}',
    toolchainJson: '{"sdk":"10.0.200"}',
  };

  it("S1 — is deterministic for identical inputs (recompute determinism)", () => {
    expect(composeSourceFingerprint(base)).toBe(composeSourceFingerprint(base));
  });

  it("S2 — a source-dump change moves the fingerprint", () => {
    const changed = {
      ...base,
      sourceDump: "F:src/index.ts\nexport const X = 2;\n",
    };

    expect(composeSourceFingerprint(base)).not.toBe(
      composeSourceFingerprint(changed),
    );
  });

  it("S3 — an API-report change moves the fingerprint", () => {
    const changed = {
      ...base,
      apiReport: "#nullable enable\nD2.Foo\nD2.Bar\n",
    };

    expect(composeSourceFingerprint(base)).not.toBe(
      composeSourceFingerprint(changed),
    );
  });

  it("S4 — a deps change moves the fingerprint (propagation input)", () => {
    const changed = {
      ...base,
      depsJson: '{"packageId":"X","version":"0.1.0","deps":{"d":"0.2.0"}}',
    };

    expect(composeSourceFingerprint(base)).not.toBe(
      composeSourceFingerprint(changed),
    );
  });

  it("S5 — a toolchain-pin change moves the fingerprint", () => {
    const changed = { ...base, toolchainJson: '{"sdk":"10.0.300"}' };

    expect(composeSourceFingerprint(base)).not.toBe(
      composeSourceFingerprint(changed),
    );
  });

  it("S6 — CRLF vs LF in the API report yields the SAME fingerprint", () => {
    const lf = composeSourceFingerprint(base);
    const crlf = composeSourceFingerprint({
      ...base,
      apiReport: "#nullable enable\r\nD2.Foo\r\n",
    });

    expect(lf).toBe(crlf);
  });

  it("a boundary shift between components cannot collide (prefixed + LF-terminated)", () => {
    // Moving a byte from SOURCE into APIREPORT must change the hash.
    const a = composeSourceFingerprint({
      ...base,
      sourceDump: "AB",
      apiReport: "C",
    });
    const b = composeSourceFingerprint({
      ...base,
      sourceDump: "A",
      apiReport: "BC",
    });

    expect(a).not.toBe(b);
  });
});
