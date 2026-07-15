// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

// Tests for the manifest-loader dep-extraction helpers + dual inventory (I12).
//
// Regression coverage:
//
//   BX-1  extractNugetProjectRefs: Windows backslash Include paths are
//         normalized before path.resolve so the resolved absolute path and
//         its basename are correct on POSIX (Linux CI) as well as on Windows.
//   I12   Public publish lane rejects d2-private- / .Private. / private tree;
//         private consumable lane still sees KC clients without public leak.

import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { basename, join, posix } from "node:path";
import { afterEach, beforeEach, describe, expect, it } from "vitest";
import {
  extractNugetProjectRefs,
  isOpenPublicNpmName,
  isOpenPublicNugetName,
  loadAllPackages,
  loadNpmPackages,
  loadNugetPackages,
  loadPrivateConsumableNpmPackages,
  loadPrivateConsumableNugetPackages,
} from "../src/manifest-loader.js";

// ---------------------------------------------------------------------------
// BX-1 — backslash Include path normalization
// ---------------------------------------------------------------------------

describe("extractNugetProjectRefs — backslash Include path normalization (BX-1)", () => {
  const csprojDir = "/abs/result/core"; // always forward-slash (POSIX root)

  it("resolves a Windows-style backslash Include to the correct absolute path", () => {
    const text = `<Project>
  <ItemGroup>
    <ProjectReference Include="..\\..\\utilities\\DcsvIo.D2.Utilities.csproj" />
  </ItemGroup>
</Project>`;

    const refs = extractNugetProjectRefs(text, csprojDir);

    expect(refs).toHaveLength(1);
    expect(refs[0]).toMatch(/DcsvIo\.D2\.Utilities\.csproj$/);
    expect(basename(refs[0]!, ".csproj")).toBe("DcsvIo.D2.Utilities");
  });

  it("resolves a deeply-nested Windows backslash Include (three levels up)", () => {
    const text = `<Project>
  <ItemGroup>
    <ProjectReference Include="..\\..\\..\\caching\\abstractions\\DcsvIo.D2.Caching.Abstractions.csproj" />
  </ItemGroup>
</Project>`;

    const refs = extractNugetProjectRefs(text, csprojDir);

    expect(refs).toHaveLength(1);
    expect(basename(refs[0]!, ".csproj")).toBe(
      "DcsvIo.D2.Caching.Abstractions",
    );
  });

  it("resolves multiple backslash Includes in one csproj — all basenames correct", () => {
    const text = `<Project>
  <ItemGroup>
    <ProjectReference Include="..\\..\\utilities\\DcsvIo.D2.Utilities.csproj" />
    <ProjectReference Include="..\\..\\result\\core\\DcsvIo.D2.Result.csproj" />
  </ItemGroup>
</Project>`;

    const refs = extractNugetProjectRefs(text, csprojDir);

    expect(refs).toHaveLength(2);

    const names = refs.map((r) => basename(r, ".csproj")).sort();

    expect(names).toEqual(["DcsvIo.D2.Result", "DcsvIo.D2.Utilities"]);
  });

  it("forward-slash Include (Unix-style) is also resolved correctly (no regression)", () => {
    const text = `<Project>
  <ItemGroup>
    <ProjectReference Include="../../utilities/DcsvIo.D2.Utilities.csproj" />
  </ItemGroup>
</Project>`;

    const refs = extractNugetProjectRefs(text, csprojDir);

    expect(refs).toHaveLength(1);
    expect(basename(refs[0]!, ".csproj")).toBe("DcsvIo.D2.Utilities");
  });

  it("mixed backslash/forward-slash Include (unusual but possible) resolves correctly", () => {
    const text = `<Project>
  <ItemGroup>
    <ProjectReference Include="..\\..\\utilities/DcsvIo.D2.Utilities.csproj" />
  </ItemGroup>
</Project>`;

    const refs = extractNugetProjectRefs(text, csprojDir);

    expect(refs).toHaveLength(1);
    expect(basename(refs[0]!, ".csproj")).toBe("DcsvIo.D2.Utilities");
  });

  it("empty csproj text → no refs", () => {
    expect(extractNugetProjectRefs("<Project></Project>", csprojDir)).toEqual(
      [],
    );
  });

  it("posix.basename of a posix.resolve with backslash Include is garbled — POSIX path.resolve treats backslash as a literal filename character", () => {
    const backslashInclude = "..\\..\\utilities\\DcsvIo.D2.Utilities.csproj";
    const garbled = posix.resolve(csprojDir, backslashInclude);

    expect(posix.basename(garbled, ".csproj")).not.toBe("DcsvIo.D2.Utilities");
    expect(garbled.endsWith(backslashInclude)).toBe(true);
  });
});

// ---------------------------------------------------------------------------
// Open name predicates (T5.4 / T5.1 fence)
// ---------------------------------------------------------------------------

describe("isOpenPublicNpmName / isOpenPublicNugetName", () => {
  it("PublicNpmInventory_AcceptsOpenDcsvIoD2AndRejectsD2Private", () => {
    expect(isOpenPublicNpmName("@dcsv-io/d2-result")).toBe(true);
    expect(
      isOpenPublicNpmName("@dcsv-io/d2-private-key-custodian-client"),
    ).toBe(false);
    expect(isOpenPublicNpmName("@dcsv-io/d2-private-evil")).toBe(false);
    expect(isOpenPublicNpmName("@d2/result")).toBe(false);
    expect(isOpenPublicNpmName("@dcsv-io/d2-typespec-decorators")).toBe(false);
    expect(isOpenPublicNpmName("@dcsv-io/d2-contract-tests")).toBe(false);
    expect(isOpenPublicNpmName("")).toBe(false);
  });

  it("PublicNugetInventory rejects Private and Tests basenames", () => {
    expect(isOpenPublicNugetName("DcsvIo.D2.Result")).toBe(true);
    expect(
      isOpenPublicNugetName("DcsvIo.D2.Private.Encryption.Extensions"),
    ).toBe(false);
    expect(isOpenPublicNugetName("DcsvIo.D2.Private.Edge.Api")).toBe(false);
    expect(isOpenPublicNugetName("DcsvIo.D2.Tests")).toBe(false);
    expect(isOpenPublicNugetName("DcsvIo.D2.Foo.SourceGen")).toBe(false);
    expect(isOpenPublicNugetName("D2.Shared.Result")).toBe(false);
  });
});

// ---------------------------------------------------------------------------
// Dual inventory — public list never sees private tree / closed markers
// ---------------------------------------------------------------------------

describe("dual inventory — public vs private consumable lanes", () => {
  let repoRoot: string;

  beforeEach(() => {
    repoRoot = mkdtempSync(join(tmpdir(), "d2-manifest-loader-"));

    writePackage(
      join(repoRoot, "public/packages/typescript/result"),
      '{ "name": "@dcsv-io/d2-result", "version": "1.2.3" }',
    );

    // Closed name under public tree must be rejected by public lane.
    writePackage(
      join(repoRoot, "public/packages/typescript/evil-private"),
      '{ "name": "@dcsv-io/d2-private-evil", "version": "0.0.1" }',
    );

    writePackage(
      join(repoRoot, "private/services/edge/key-custodian/client-ts"),
      '{ "name": "@dcsv-io/d2-private-key-custodian-client", "version": "0.1.0" }',
    );

    writePackage(
      join(
        repoRoot,
        "private/services/edge/key-custodian/client-ts/node_modules/@dcsv-io/d2-leaked",
      ),
      '{ "name": "@dcsv-io/d2-leaked", "version": "9.9.9" }',
    );

    // Open NuGet packable
    writeCsproj(
      join(repoRoot, "public/packages/dotnet/result/core"),
      "DcsvIo.D2.Result.csproj",
      `<Project><PropertyGroup><Version>1.0.0</Version></PropertyGroup></Project>`,
    );

    // Private Extensions under private packages — must not appear on public list
    writeCsproj(
      join(repoRoot, "private/packages/dotnet/encryption/extensions"),
      "DcsvIo.D2.Private.Encryption.Extensions.csproj",
      `<Project><PropertyGroup><Version>1.0.0</Version></PropertyGroup></Project>`,
    );

    // KC .NET client private consumable
    writeCsproj(
      join(repoRoot, "private/services/edge/key-custodian/client"),
      "DcsvIo.D2.Private.Edge.KeyCustodian.Client.csproj",
      `<Project><PropertyGroup><Version>0.1.0</Version></PropertyGroup></Project>`,
    );

    // Open-looking product without Private under private tree — public list still
    // must not see private roots.
    writeCsproj(
      join(repoRoot, "private/services/edge/api"),
      "DcsvIo.D2.Private.Edge.Api.csproj",
      `<Project><PropertyGroup><Version>0.1.0</Version></PropertyGroup></Project>`,
    );
  });

  afterEach(() => {
    rmSync(repoRoot, { recursive: true, force: true });
  });

  it("PublicList_ExcludesPrivateTreeAndD2PrivateAndTypespecExclusions", () => {
    const all = loadAllPackages(repoRoot);
    const names = all.map((p) => p.name);

    expect(names).toContain("@dcsv-io/d2-result");
    expect(names).toContain("DcsvIo.D2.Result");
    expect(names).not.toContain("@dcsv-io/d2-private-key-custodian-client");
    expect(names).not.toContain("@dcsv-io/d2-private-evil");
    expect(names).not.toContain("DcsvIo.D2.Private.Encryption.Extensions");
    expect(names).not.toContain("DcsvIo.D2.Private.Edge.KeyCustodian.Client");
    expect(names).not.toContain("DcsvIo.D2.Private.Edge.Api");
    expect(names.every((n) => !n.includes("d2-private-"))).toBe(true);
    expect(names.every((n) => !n.includes(".Private."))).toBe(true);
    expect(names.every((n) => !n.includes("private/"))).toBe(true);
    expect(all.every((p) => !p.dir.startsWith("private/"))).toBe(true);
  });

  it("PrivateConsumableLane_CanSeeKcClients_WithoutPublicListLeak", () => {
    const publicNpm = loadNpmPackages(repoRoot).map((p) => p.name);
    const privateNpm = loadPrivateConsumableNpmPackages(repoRoot).map(
      (p) => p.name,
    );
    const privateNuget = loadPrivateConsumableNugetPackages(repoRoot).map(
      (p) => p.name,
    );

    expect(publicNpm).toContain("@dcsv-io/d2-result");
    expect(publicNpm).not.toContain("@dcsv-io/d2-private-key-custodian-client");
    expect(privateNpm).toContain("@dcsv-io/d2-private-key-custodian-client");
    expect(privateNpm).not.toContain("@dcsv-io/d2-leaked");
    expect(privateNuget).toContain(
      "DcsvIo.D2.Private.Edge.KeyCustodian.Client",
    );

    const publicList = loadAllPackages(repoRoot);
    expect(
      publicList.find(
        (p) => p.name === "@dcsv-io/d2-private-key-custodian-client",
      ),
    ).toBeUndefined();
  });

  it("PublicNugetInventory_IsPublicPackagesTreeOnly_ExcludesPrivateAndProduct", () => {
    const nuget = loadNugetPackages(repoRoot);
    expect(nuget.map((p) => p.name)).toEqual(["DcsvIo.D2.Result"]);
    expect(nuget.every((p) => p.dir.startsWith("public/packages/dotnet"))).toBe(
      true,
    );
  });

  function writePackage(dir: string, packageJson: string): void {
    mkdirSync(dir, { recursive: true });
    writeFileSync(join(dir, "package.json"), packageJson);
  }

  function writeCsproj(dir: string, fileName: string, body: string): void {
    mkdirSync(dir, { recursive: true });
    writeFileSync(join(dir, fileName), body);
  }
});
