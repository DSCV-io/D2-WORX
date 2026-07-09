// Copyright (c) DCSV. All rights reserved.
//
// Fail-loud guard for the PublicAPI baseline seeder
// (tools/scripts/seed-publicapi-baselines.mjs).
//
// The seeder extracts a package's public-API surface by building it and scraping
// the analyzer's RS0016 "not part of the declared public API" diagnostics. Zero
// RS0016 is ambiguous: it means EITHER "the package genuinely has no public API"
// OR "the analyzer did not re-run" (MSBuild up-to-date-skipped the recompile —
// warm obj/, a locked toolchain, csharp-ls holding a source-gen DLL). Writing an
// empty Shipped.txt in the second case silently wipes a real baseline, and — the
// fingerprint being recomposed over the now-empty file — the release gate reports
// CURRENT, so the corruption is invisible. This is how 30 packages were emptied
// under a green gate.
//
// A library going from N committed public symbols to zero via a SUCCESSFUL build
// is the analyzer-didn't-run signature, not a real API removal. This guard treats
// that transition as fail-loud by default and offers an explicit escape hatch for
// the genuine "this package intentionally exposes no public API" case.

/**
 * Refuse to persist an empty public-API surface when the package had a non-empty
 * committed baseline, unless the caller explicitly allow-listed the package.
 *
 * @param {object} args
 * @param {string} args.packageId            The consumable package id.
 * @param {number} args.priorSurfaceCount    Committed (HEAD) public-API line count.
 * @param {number} args.extractedSurfaceCount Line count the extraction build produced.
 * @param {boolean} args.allowEmpty          True to permit an intentional empty surface.
 * @throws {Error} when a prior-non-empty package extracts to empty without opt-in.
 */
export function assertExtractionNotWrongfullyEmpty({
  packageId,
  priorSurfaceCount,
  extractedSurfaceCount,
  allowEmpty,
}) {
  if (priorSurfaceCount > 0 && extractedSurfaceCount === 0 && !allowEmpty) {
    throw new Error(
      `refusing to write empty PublicAPI for ${packageId}: had ${priorSurfaceCount} ` +
        `lines at HEAD, extraction produced 0 — the analyzer likely did not re-run; ` +
        `check the build/toolchain (csharp-ls DLL lock, warm bin/obj, locked SDK). ` +
        `A successful build that drops a whole surface to zero is the ` +
        `analyzer-didn't-run signature, not a real removal. If this package ` +
        `genuinely exposes no public API, re-run with --allow-empty ${packageId} ` +
        `(or set SEED_ALLOW_EMPTY=${packageId}).`,
    );
  }
}
