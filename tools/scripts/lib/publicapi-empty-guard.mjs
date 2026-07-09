// Copyright (c) DCSV. All rights reserved.
//
// Fail-loud guards for PublicAPI.Shipped baselines.
//
// ## Two different empty transitions (do not conflate them)
//
// 1) FAILURE / corruption
//    Seeder wipes Shipped to header-only BEFORE the RS0016 extraction build.
//    If that build crashes, or extraction returns 0 while HEAD still has a
//    surface (analyzer didn't re-run), the tree must NOT stay empty.
//    → seed restores the pre-wipe snapshot and exits non-zero.
//    → commit gate refuses empty-disk-vs-non-empty-HEAD without an explicit
//      allow (defense in depth if restore is skipped or process is killed).
//
// 2) INTENTIONAL zero public API
//    a) Brand-new package that never had a surface: HEAD line count is 0;
//       header-only Shipped is legitimate (no allow needed).
//    b) Existing package whose entire surface is removed on purpose:
//       seed with --allow-empty <PackageId> (or SEED_ALLOW_EMPTY), which
//       writes header-only + a matching fingerprint; then commit once with
//       the same allow so empty-vs-HEAD is permitted for that package only.
//       After that commit, HEAD is empty → later commits need no allow.
//    c) Entire package deleted: remove .csproj + baselines together; the
//       inventory walk no longer lists it (not an "empty Shipped" case).
//
// Partial API removals (N→M, M>0) are normal reseed territory and never hit
// these guards.

export const NULLABLE_HEADER = "#nullable enable";

/**
 * Count non-header, non-empty PublicAPI lines in a Shipped/Unshipped file body.
 *
 * @param {string} content File text (any EOL).
 * @returns {number}
 */
export function countPublicApiLines(content) {
  if (typeof content !== "string" || content.length === 0) return 0;

  return content
    .split(/\r?\n/)
    .map((l) => l.trimEnd())
    .filter((l) => l.length > 0 && l !== NULLABLE_HEADER).length;
}

/**
 * Build the canonical on-disk text for a Shipped/Unshipped file from API lines.
 *
 * @param {string[]} apiLines
 * @returns {string}
 */
export function formatPublicApiFile(apiLines) {
  return [NULLABLE_HEADER, ...apiLines].join("\n") + "\n";
}

/**
 * Refuse to *persist an extraction* that collapses a non-empty HEAD surface to
 * zero without an explicit allow. Used only on the seeder success path after a
 * completed build (not on build failure — that path restores and aborts).
 *
 * @param {object} args
 * @param {string} args.packageId
 * @param {number} args.priorSurfaceCount     HEAD (committed) API line count.
 * @param {number} args.extractedSurfaceCount Lines produced by RS0016 scrape.
 * @param {boolean} args.allowEmpty           Intentional zero-API opt-in.
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
        `analyzer-didn't-run signature, not a real removal.\n` +
        `  Intentional full-surface removal: re-run with\n` +
        `    node tools/scripts/seed-publicapi-baselines.mjs --package ${packageId} --allow-empty ${packageId}\n` +
        `  then commit once with the same allow:\n` +
        `    SEED_ALLOW_EMPTY=${packageId} git commit ...\n` +
        `  (After HEAD is empty, later commits need no allow.)`,
    );
  }
}

/**
 * Refuse a working-tree PublicAPI.Shipped that is missing/header-only while HEAD
 * still has API lines — unless allowEmpty (intentional first empty commit).
 *
 * Does NOT fire when HEAD is already empty (brand-new package, or empty already
 * committed). Does NOT fire on partial reductions (disk still has lines).
 *
 * @param {object} args
 * @param {string} args.packageId
 * @param {string} args.shippedContent   Working-tree text ("" if missing).
 * @param {number} args.headSurfaceCount HEAD API line count (0 if untracked).
 * @param {boolean} [args.allowEmpty=false]
 */
export function assertShippedContentNotWrongfullyEmpty({
  packageId,
  shippedContent,
  headSurfaceCount,
  allowEmpty = false,
}) {
  const diskCount = countPublicApiLines(shippedContent ?? "");

  if (headSurfaceCount > 0 && diskCount === 0 && !allowEmpty) {
    throw new Error(
      `PublicAPI.Shipped.txt for ${packageId} is EMPTY (missing or header-only) ` +
        `but HEAD still has ${headSurfaceCount} public-API lines.\n` +
        `  Failure signature: seed wiped Shipped then aborted without restore, ` +
        `or a bad empty reseed. Do not commit this.\n` +
        `  Restore:  git checkout HEAD -- <path>/PublicAPI.Shipped.txt\n` +
        `  Re-seed only if source changed:\n` +
        `    node tools/scripts/seed-publicapi-baselines.mjs --package ${packageId}\n` +
        `  Intentional full-surface removal (first empty commit only):\n` +
        `    node tools/scripts/seed-publicapi-baselines.mjs --package ${packageId} --allow-empty ${packageId}\n` +
        `    SEED_ALLOW_EMPTY=${packageId} git commit ...\n` +
        `  Entire package deletion: remove the .csproj and baselines together — ` +
        `not an empty-Shipped case.`,
    );
  }
}
