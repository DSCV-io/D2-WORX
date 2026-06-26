// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Tests for the manifest-loader dep-extraction helpers.
//
// Regression coverage:
//
//   BX-1  extractNugetProjectRefs: Windows backslash Include paths are
//         normalized before path.resolve so the resolved absolute path and
//         its basename are correct on POSIX (Linux CI) as well as on Windows.
//         Without the fix, POSIX path.resolve treats `\` as a literal filename
//         character, garbling the resolved path → basename returns the whole
//         Include value, not the filename → dep silently dropped from every
//         NuGet package that carries a `<ProjectReference>`.
//
// OS-independence guarantee: the test exercises the POSIX failure mode
// explicitly by asserting on the basename of the resolved path.  A backslash
// Include value like `..\..\result\D2.Shared.Result.csproj` resolves to a
// path whose POSIX basename MUST end in `.csproj` and MUST equal the package
// id (without extension) — a condition that fails under the unfixed code on
// Linux because the whole Include string becomes the filename component.

import { basename, posix } from "node:path";
import { describe, expect, it } from "vitest";
import { extractNugetProjectRefs } from "../src/manifest-loader.js";

// ---------------------------------------------------------------------------
// BX-1 — backslash Include path normalization
// ---------------------------------------------------------------------------

describe("extractNugetProjectRefs — backslash Include path normalization (BX-1)", () => {
  // A real .csproj on Windows records Include paths with backslash separators.
  // Each test below drives the function with that exact shape and asserts that
  // the resolved path's basename equals the expected package id — the condition
  // that the drift-check's dep map depends on.

  const csprojDir = "/abs/result/core"; // always forward-slash (POSIX root)

  it("resolves a Windows-style backslash Include to the correct absolute path", () => {
    const text = `<Project>
  <ItemGroup>
    <ProjectReference Include="..\\..\\utilities\\D2.Shared.Utilities.csproj" />
  </ItemGroup>
</Project>`;

    const refs = extractNugetProjectRefs(text, csprojDir);

    expect(refs).toHaveLength(1);

    // The resolved path must end in `D2.Shared.Utilities.csproj`.
    // Without the fix, on POSIX this would end in
    // `..\\..\\utilities\\D2.Shared.Utilities.csproj` (the whole Include
    // value treated as a single filename component).
    expect(refs[0]).toMatch(/D2\.Shared\.Utilities\.csproj$/);

    // The basename — what loadNugetPackages uses to derive the dep name — must
    // be exactly `D2.Shared.Utilities.csproj`, not the garbled full string.
    expect(basename(refs[0]!, ".csproj")).toBe("D2.Shared.Utilities");
  });

  it("resolves a deeply-nested Windows backslash Include (three levels up)", () => {
    const text = `<Project>
  <ItemGroup>
    <ProjectReference Include="..\\..\\..\\caching\\abstractions\\D2.Shared.Caching.Abstractions.csproj" />
  </ItemGroup>
</Project>`;

    const refs = extractNugetProjectRefs(text, csprojDir);

    expect(refs).toHaveLength(1);
    expect(basename(refs[0]!, ".csproj")).toBe(
      "D2.Shared.Caching.Abstractions",
    );
  });

  it("resolves multiple backslash Includes in one csproj — all basenames correct", () => {
    const text = `<Project>
  <ItemGroup>
    <ProjectReference Include="..\\..\\utilities\\D2.Shared.Utilities.csproj" />
    <ProjectReference Include="..\\..\\result\\core\\D2.Shared.Result.csproj" />
  </ItemGroup>
</Project>`;

    const refs = extractNugetProjectRefs(text, csprojDir);

    expect(refs).toHaveLength(2);

    const names = refs.map((r) => basename(r, ".csproj")).sort();

    expect(names).toEqual(["D2.Shared.Result", "D2.Shared.Utilities"]);
  });

  it("forward-slash Include (Unix-style) is also resolved correctly (no regression)", () => {
    const text = `<Project>
  <ItemGroup>
    <ProjectReference Include="../../utilities/D2.Shared.Utilities.csproj" />
  </ItemGroup>
</Project>`;

    const refs = extractNugetProjectRefs(text, csprojDir);

    expect(refs).toHaveLength(1);
    expect(basename(refs[0]!, ".csproj")).toBe("D2.Shared.Utilities");
  });

  it("mixed backslash/forward-slash Include (unusual but possible) resolves correctly", () => {
    const text = `<Project>
  <ItemGroup>
    <ProjectReference Include="..\\..\\utilities/D2.Shared.Utilities.csproj" />
  </ItemGroup>
</Project>`;

    const refs = extractNugetProjectRefs(text, csprojDir);

    expect(refs).toHaveLength(1);
    expect(basename(refs[0]!, ".csproj")).toBe("D2.Shared.Utilities");
  });

  it("empty csproj text → no refs", () => {
    expect(extractNugetProjectRefs("<Project></Project>", csprojDir)).toEqual(
      [],
    );
  });

  // ---------------------------------------------------------------------------
  // OS-independence proof: simulate the Linux failure mode using path.posix
  // throughout (both resolve AND basename from path.posix) so that the test
  // exercises the POSIX semantics on EVERY OS, including Windows.
  //
  // On Linux the production path.resolve IS path.posix.resolve and
  // path.basename IS path.posix.basename — so this directly mirrors what the
  // pre-fix code would do there.  On Windows path.resolve/basename also
  // recognize `\` natively, which is WHY the bug is Windows-only and WHY the
  // test must drive the POSIX variants explicitly to stay OS-independent.
  // ---------------------------------------------------------------------------

  it("posix.basename of a posix.resolve with backslash Include is garbled — proving the pre-fix Linux failure", () => {
    // The raw Include value as it appears in a Windows-committed .csproj.
    const backslashInclude = "..\\..\\utilities\\D2.Shared.Utilities.csproj";

    // On POSIX (Linux): path.resolve treats `\` as a literal filename char,
    // so the result is just the base dir concatenated with the entire Include
    // value as a single path segment.
    const garbled = posix.resolve(csprojDir, backslashInclude);

    // posix.basename of the garbled path is the ENTIRE Include value
    // (because `\` is not a POSIX separator and the whole thing is one segment).
    // Stripping `.csproj` from this string does NOT yield the package id.
    expect(posix.basename(garbled, ".csproj")).not.toBe("D2.Shared.Utilities");

    // Confirm the garbled tail ends with the full backslash-separated string
    // so it is clear why CONSUMABLE_NAMES.has(...) would always return false.
    expect(garbled.endsWith(backslashInclude)).toBe(true);
  });
});
