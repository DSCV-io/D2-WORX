// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

// Seed↔provider fingerprint BYTE-IDENTITY (real pin, both ecosystems).
//
// The release baselines are seeded by two .mjs scripts
// (public/tools/scripts/seed-publicapi-baselines.mjs for .NET,
//  public/tools/scripts/seed-apiextractor-baselines.mjs for npm) whose SHA-256
// composition MUST match the release-runner provider's composeSourceFingerprint
// byte-for-byte — otherwise a freshly-seeded baseline and the runner's no-op
// drift recompute would disagree, and the currency gate would false-alarm (or,
// worse, a real drift would hide behind a matching-by-luck digest).
//
// Both seed scripts delegate their final composition to the shared primitive
// composeSourceFingerprintFromParts (public/tools/scripts/lib/source-fingerprint-compose.mjs),
// a SEPARATE implementation from the runner's composeSourceFingerprint. These
// tests feed identical synthetic inputs (a small fixed source dump + API report
// + resolved deps + toolchain pin, shaped per ecosystem) to BOTH implementations
// and assert the hex digests are ===. If either implementation ever drifts, the
// digests diverge and these tests fail — the pin the two now-corrected inline
// "pinned by a runner test" comments actually refer to.

import { describe, expect, it } from "vitest";
import { composeSourceFingerprint } from "../src/source-fingerprint.js";
import {
  composeSourceFingerprintFromParts,
  normalizeLf,
} from "../../scripts/lib/source-fingerprint-compose.mjs";

// ---------------------------------------------------------------------------
// .NET (seed-publicapi-baselines.mjs) — inputs shaped like the nuget seed:
//   sourceDump over .cs/.csproj, apiReport = Shipped.txt + Unshipped.txt,
//   depsJson = {packageId, version, deps}, toolchainJson = declared .NET pin.
// ---------------------------------------------------------------------------

describe("seed↔provider byte-identity — .NET (nuget) composition", () => {
  const nugetInput = {
    sourceDump:
      "F:DcsvIo.D2.Time.csproj\n<Project></Project>\n" +
      "F:Clock.cs\nnamespace DcsvIo.D2.Time;\npublic sealed class Clock {}\n",
    apiReport:
      "#nullable enable\nDcsvIo.D2.Time.Clock\nDcsvIo.D2.Time.Clock.Clock() -> void\n" +
      "#nullable enable\n",
    depsJson:
      '{"packageId":"DcsvIo.D2.Time","version":"0.1.0","deps":{"DcsvIo.D2.Primitives":"0.1.0"}}',
    toolchainJson:
      '{"langVersion":"latest","rollForward":"latestFeature","sdk":"10.0.200","targetFramework":"net10.0"}',
  };

  it("the seed primitive and the runner provider yield the SAME hex digest", () => {
    expect(composeSourceFingerprintFromParts(nugetInput)).toBe(
      composeSourceFingerprint(nugetInput),
    );
  });

  it("a source-dump change moves BOTH digests together (still ===)", () => {
    const changed = {
      ...nugetInput,
      sourceDump: nugetInput.sourceDump.replace("Clock", "Watch"),
    };

    expect(composeSourceFingerprintFromParts(changed)).toBe(
      composeSourceFingerprint(changed),
    );
    expect(composeSourceFingerprintFromParts(changed)).not.toBe(
      composeSourceFingerprintFromParts(nugetInput),
    );
  });
});

// ---------------------------------------------------------------------------
// npm (seed-apiextractor-baselines.mjs) — inputs shaped like the npm seed:
//   sourceDump over .ts/package.json, apiReport = the etc/<pkg>.api.md text,
//   depsJson = {name, version, dependencies}, toolchainJson = declared TS pin.
// ---------------------------------------------------------------------------

describe("seed↔provider byte-identity — npm composition", () => {
  const npmInput = {
    sourceDump:
      'F:package.json\n{"name":"@dcsv-io/d2-result","version":"0.1.0"}\n' +
      "F:src/index.ts\nexport const ok = true;\n",
    apiReport:
      '## API Report File for "@dcsv-io/d2-result"\n\n```ts\nexport const ok: boolean;\n```\n',
    depsJson:
      '{"name":"@dcsv-io/d2-result","version":"0.1.0","dependencies":{"@dcsv-io/d2-error-category":"0.1.0"}}',
    toolchainJson: '{"module":"ESNext","target":"ES2022","typescript":"5.9.3"}',
  };

  it("the seed primitive and the runner provider yield the SAME hex digest", () => {
    expect(composeSourceFingerprintFromParts(npmInput)).toBe(
      composeSourceFingerprint(npmInput),
    );
  });

  it("a CRLF vs LF API report yields the SAME digest through BOTH (both normalize)", () => {
    const crlf = { ...npmInput, apiReport: normalizeLf(npmInput.apiReport) };
    const withCrlf = {
      ...npmInput,
      apiReport: npmInput.apiReport.replace(/\n/g, "\r\n"),
    };

    // Seed primitive: LF and CRLF report agree; and it agrees with the provider.
    expect(composeSourceFingerprintFromParts(withCrlf)).toBe(
      composeSourceFingerprintFromParts(crlf),
    );
    expect(composeSourceFingerprintFromParts(withCrlf)).toBe(
      composeSourceFingerprint(withCrlf),
    );
  });

  it("a source-dump change moves BOTH digests together (still ===)", () => {
    const changed = {
      ...npmInput,
      sourceDump: npmInput.sourceDump.replace("ok = true", "ok = false"),
    };

    expect(composeSourceFingerprintFromParts(changed)).toBe(
      composeSourceFingerprint(changed),
    );
    expect(composeSourceFingerprintFromParts(changed)).not.toBe(
      composeSourceFingerprintFromParts(npmInput),
    );
  });
});
