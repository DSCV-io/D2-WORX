// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

/**
 * Transforms a `userMessageKey` symbol path (`TK.<Domain>.<Category>.<CONST>`)
 * into its two derived forms. The TS twin of the .NET `TkKeyTransform` — both
 * runtimes invert the `KeyDecomposer.Decompose` mapping the same way so a spec
 * `userMessageKey` resolves identically on both sides.
 *
 * The canonical `userMessageKey` regex is
 * `^TK(\.[A-Za-z][A-Za-z0-9]*){2}\.[A-Z][A-Z0-9_]*$`, i.e. exactly the `TK`
 * literal, a Domain segment, a Category segment, and a SCREAMING constant. The
 * `KeyDecomposer` only PascalCases the FIRST char of the domain + category
 * segments (the rest of each segment is unchanged), so the inverse lowercases
 * ONLY the first char of each of those two segments.
 */

/** The two derived forms of a parsed `userMessageKey`. */
export interface TkKeyParts {
  /**
   * The en-US.json wire key the message resolves to
   * (e.g. `auth_errors_UNAUTHORIZED`). The form a TKMessage rides the wire as.
   */
  readonly snakeKey: string;

  /**
   * The TS TK-constant access path that references the generated `tk-keys.g.ts`
   * constant (e.g. `TK.auth.errors.UNAUTHORIZED`). Lowercases the domain +
   * category segments' first char; keeps the SCREAMING constant segment.
   */
  readonly tkConstantPath: string;
}

/**
 * Parse a `TK.<Domain>.<Category>.<CONST>` symbol path into its derived forms.
 * Returns `undefined` for any input that does not match the canonical shape
 * (null / empty / wrong segment count / a zero-length segment) — the caller
 * treats `undefined` as "does not resolve" and surfaces a diagnostic.
 */
export function parseTkKey(
  userMessageKey: string | undefined,
): TkKeyParts | undefined {
  if (userMessageKey === undefined || userMessageKey.length === 0)
    return undefined;

  const segments = userMessageKey.split(".");
  // Exactly: "TK", Domain, Category, CONST.
  if (segments.length !== 4) return undefined;
  if (segments[0] !== "TK") return undefined;

  const domain = segments[1]!;
  const category = segments[2]!;
  const constant = segments[3]!;
  if (domain.length === 0 || category.length === 0 || constant.length === 0)
    return undefined;

  const lowerDomain = lowerFirst(domain);
  const lowerCategory = lowerFirst(category);

  return {
    snakeKey: `${lowerDomain}_${lowerCategory}_${constant}`,
    tkConstantPath: `TK.${lowerDomain}.${lowerCategory}.${constant}`,
  };
}

function lowerFirst(segment: string): string {
  return segment[0]!.toLowerCase() + segment.slice(1);
}
