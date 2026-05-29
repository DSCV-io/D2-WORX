// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { writeFile } from "node:fs/promises";
import prettier from "prettier";

/* eslint-disable no-irregular-whitespace -- NBSP in the JSDoc example below is intentional */
/**
 * Code points that JSON.stringify renders as their literal character but which look
 * invisible (or look identical to ASCII space) in editors — making them invisible footguns
 * for human reviewers.
 *
 * Escaping them as `\uXXXX` is purely cosmetic: `JSON.parse('" "')` and
 * `JSON.parse('"\xa0"')` both produce the same string at runtime. Consumers see the actual
 * character; reviewers see the explicit escape in source files.
 *
 * Targets all known invisible/ambiguous space + bidi + zero-width characters used in CLDR
 * data (NBSP for fr/pl/kk thousands separator; bidi marks in ar date patterns; etc.).
 */
/* eslint-enable no-irregular-whitespace */
const INVISIBLE_CODE_POINTS: readonly number[] = [
  0x00a0, // NO-BREAK SPACE (CLDR fr/pl/kk thousands separator)
  0x1680, // OGHAM SPACE MARK
  0x2000, // EN QUAD
  0x2001, // EM QUAD
  0x2002, // EN SPACE
  0x2003, // EM SPACE
  0x2004, // THREE-PER-EM SPACE
  0x2005, // FOUR-PER-EM SPACE
  0x2006, // SIX-PER-EM SPACE
  0x2007, // FIGURE SPACE
  0x2008, // PUNCTUATION SPACE
  0x2009, // THIN SPACE
  0x200a, // HAIR SPACE
  0x200b, // ZERO WIDTH SPACE
  0x200c, // ZERO WIDTH NON-JOINER
  0x200d, // ZERO WIDTH JOINER
  0x200e, // LEFT-TO-RIGHT MARK (CLDR ar date patterns)
  0x200f, // RIGHT-TO-LEFT MARK (CLDR ar date patterns)
  0x2028, // LINE SEPARATOR
  0x2029, // PARAGRAPH SEPARATOR
  0x202a, // LEFT-TO-RIGHT EMBEDDING
  0x202b, // RIGHT-TO-LEFT EMBEDDING
  0x202c, // POP DIRECTIONAL FORMATTING
  0x202d, // LEFT-TO-RIGHT OVERRIDE
  0x202e, // RIGHT-TO-LEFT OVERRIDE
  0x202f, // NARROW NO-BREAK SPACE
  0x205f, // MEDIUM MATHEMATICAL SPACE
  0x2060, // WORD JOINER
  0xfeff, // ZERO WIDTH NO-BREAK SPACE (BOM)
];

const INVISIBLE_REGEX: RegExp = (() => {
  const escaped = INVISIBLE_CODE_POINTS.map(
    (cp) => `\\u${cp.toString(16).padStart(4, "0")}`,
  ).join("");
  return new RegExp(`[${escaped}]`, "g");
})();

/**
 * Escapes invisible Unicode code points (NBSP, bidi marks, zero-width chars, etc.) as
 * their `\uXXXX` form so they're visible to reviewers. Round-trip safe: `JSON.parse` of
 * the escaped form yields the original character.
 */
export function escapeInvisibles(jsonText: string): string {
  return jsonText.replace(INVISIBLE_REGEX, (ch) => {
    const cp = ch.codePointAt(0)!;
    return `\\u${cp.toString(16).padStart(4, "0")}`;
  });
}

/**
 * Writes an object as pretty-printed JSON, normalized through Prettier so output is
 * formatting-stable against a later `prettier --check` (eliminates noise diffs from
 * subsequent format passes — see rules.md §26.5 fix-at-the-pipeline-source mandate).
 *
 * Pipeline:
 *   1. `JSON.stringify(obj, null, 2)` — initial pretty-print.
 *   2. `escapeInvisibles` — convert NBSP / bidi marks / zero-width chars to `\uXXXX` so
 *      reviewers see the escape rather than an invisible glyph. Prettier preserves these
 *      escape sequences as literal source characters (verified — it does not decode +
 *      re-emit).
 *   3. `prettier.format(..., { parser: "json", ...resolvedConfig })` — apply workspace
 *      Prettier rules (defaults at the repo root; per-directory `.prettierrc` resolved
 *      via `prettier.resolveConfig(path)` so any future override is honored automatically).
 *   4. `writeFile` — Prettier's output already ends in `\n`; do NOT append another newline.
 *
 * Drop-in replacement for `writeFile(path, JSON.stringify(obj, null, 2) + "\n")`.
 */
export async function writeSpecJson(path: string, obj: unknown): Promise<void> {
  const json = JSON.stringify(obj, null, 2);
  const escaped = escapeInvisibles(json);
  const config = await prettier.resolveConfig(path);
  const formatted = await prettier.format(escaped, {
    ...config,
    parser: "json",
    filepath: path,
  });
  await writeFile(path, formatted);
}
