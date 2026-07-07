// Copyright (c) DCSV. All rights reserved.
//
// Fail-loud guard for the api-extractor baseline seeder
// (tools/scripts/seed-apiextractor-baselines.mjs). The npm twin of the .NET
// seeder's publicapi-empty-guard.mjs.
//
// The seeder generates each package's etc/<pkg>.api.md by running api-extractor
// over dist/index.d.ts, then composes a fingerprint over that report. A report
// carrying NO `export ` line is DEGENERATE: api-extractor saw an empty or
// missing dist/index.d.ts (the package was not built, or its rollup produced
// nothing), so it analyzed zero exports. Composing a fingerprint over that
// degenerate report and committing it makes the drift/currency check pass
// against the degenerate baseline — the corruption is then invisible, exactly
// the silent-wipe class the .NET seeder's empty-guard defends against.
//
// A consumable dropping to zero exports via a successful extract is the
// degenerate-report signature, not a real zero-export library. This guard
// refuses that transition by default and offers an explicit escape hatch for a
// package that intentionally exposes no public API.

/**
 * Refuse to compose a fingerprint over a degenerate (no-export) .api.md unless
 * the caller explicitly allow-listed the package.
 *
 * @param {object} args
 * @param {string} args.pkgName          The consumable package name (@d2/*).
 * @param {boolean} args.hasPublicMembers True when the .api.md holds >=1 `export ` line.
 * @param {boolean} args.allowEmpty       True to permit a genuine zero-export surface.
 * @throws {Error} when a degenerate report is not explicitly allowed.
 */
export function assertApiReportNotDegenerate({
  pkgName,
  hasPublicMembers,
  allowEmpty,
}) {
  if (!hasPublicMembers && !allowEmpty) {
    throw new Error(
      `refusing to write a fingerprint over a degenerate .api.md for ${pkgName}: ` +
        `api-extractor detected NO exports (an empty or missing dist/index.d.ts). ` +
        `A consumable dropping to zero exports via a successful extract is the ` +
        `degenerate-report signature, not a real zero-export library — build the ` +
        `package first (pnpm -r build) so dist/index.d.ts carries its exports, then ` +
        `re-seed. If this package genuinely exposes no public API, re-run with ` +
        `--allow-empty ${pkgName} (or set SEED_ALLOW_EMPTY=${pkgName}).`,
    );
  }
}
