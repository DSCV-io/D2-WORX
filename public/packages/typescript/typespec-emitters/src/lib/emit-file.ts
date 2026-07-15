// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { emitFile, resolvePath } from "@typespec/compiler";
import type { EmitContext, Program } from "@typespec/compiler";

// Emit-file wrapper — the single choke-point all emitters use to write
// generated files. Keeping every write behind this function means:
//   - banner-prepend policy changes land in one place, not scattered across emitters;
//   - future byte-parity hooks (e.g. CRLF normalization, BOM stripping) live here;
//   - tests can assert that the wrapper was the write path.
//
// Design decision: the wrapper is content-neutral — it does NOT auto-prepend the
// banner. Each C#/proto emitter concatenates buildBanner(...) explicitly before
// calling emitGeneratedFile. JSON outputs (e.g. the smoke manifest) have no
// comment syntax and correctly omit the banner.

/**
 * Write generated content to `path` using the TypeSpec compiler's emitFile API.
 * Path must be fully resolved (use resolveOutputPath to build it from the emitter
 * output directory).
 */
export async function emitGeneratedFile(
  program: Program,
  path: string,
  content: string,
): Promise<void> {
  await emitFile(program, { path, content });
}

/**
 * Resolve an output path relative to the emitter's output directory.
 *
 * @param context  - The EmitContext supplied to $onEmit.
 * @param segments - Path segments appended to context.emitterOutputDir.
 * @returns Absolute path string suitable for emitGeneratedFile.
 */
export function resolveOutputPath(
  context: EmitContext,
  ...segments: string[]
): string {
  return resolvePath(context.emitterOutputDir, ...segments);
}
