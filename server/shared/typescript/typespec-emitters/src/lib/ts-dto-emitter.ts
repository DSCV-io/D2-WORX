// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// TypeScript DTO emitter — pure string-template emission of TS interface pairs.
//
// For each operation emits one `<op>-dto.g.ts` file containing:
//   - export interface <Op>Input  { ... }
//   - export interface <Op>Output { ... }
//   - export interface <NestedModel> { ... } (co-located, deduped)
//
// Conventions:
//   - Optional field → `name?: T` (never `T | null`).
//   - Collection    → `readonly T[]`.
//   - Nested model  → its own interface, emitted once.
//   - @d2Redact fields are emitted normally — redaction is a server-log
//     concern; the TS DTO is a wire-shape only (documented behavior).
//   - Auto-generated banner via buildBanner().

import { buildBanner } from "./banner.js";
import type { FieldInfo, NestedModel } from "./model-walk.js";

// ---------------------------------------------------------------------------
// Public types
// ---------------------------------------------------------------------------

/** One emitted TypeScript DTO file. */
export interface EmittedTsFile {
  /** File name relative to the emitter output directory. */
  readonly fileName: string;
  /** Full text content ready to write. */
  readonly content: string;
}

// ---------------------------------------------------------------------------
// Public API
// ---------------------------------------------------------------------------

/**
 * Emit the TS DTO file (`<op>-dto.g.ts`) for one operation. Pure function —
 * no I/O; returns `EmittedTsFile` so tests can assert content directly.
 *
 * @param opName       - Operation name in lowerCamelCase.
 * @param sourceSpec   - Relative path to the .tsp spec file (for banner).
 * @param inputFields  - Resolved field list for the input model.
 * @param outputFields - Resolved field list for the output model.
 * @param outputNested - Distinct nested models from the output walk.
 * @returns One EmittedTsFile.
 */
export function emitTsDtos(
  opName: string,
  sourceSpec: string,
  inputFields: readonly FieldInfo[],
  outputFields: readonly FieldInfo[],
  outputNested: readonly NestedModel[],
): EmittedTsFile {
  const pascalOp = toPascalFromCamel(opName);
  const banner = buildBanner(sourceSpec);
  const lines: string[] = [];

  lines.push(banner);

  // Nested model interfaces first (referenced by Output).
  for (const nm of outputNested) {
    lines.push(emitInterface(nm.name, nm.fields));
    lines.push("");
  }

  // Input interface.
  lines.push(emitInterface(`${pascalOp}Input`, inputFields));
  lines.push("");

  // Output interface.
  lines.push(emitInterface(`${pascalOp}Output`, outputFields));
  lines.push("");

  return {
    fileName: `${kebab(opName)}-dto.g.ts`,
    content: lines.join("\n"),
  };
}

// ---------------------------------------------------------------------------
// Internal helpers
// ---------------------------------------------------------------------------

function emitInterface(typeName: string, fields: readonly FieldInfo[]): string {
  const lines: string[] = [];
  lines.push(`/** Generated DTO interface for \`${typeName}\`. */`);
  lines.push(`export interface ${typeName} {`);
  for (const f of fields) {
    const optMark = f.optional ? "?" : "";
    lines.push(`  readonly ${f.tsName}${optMark}: ${f.tsType};`);
  }
  lines.push("}");
  return lines.join("\n");
}

/** Convert lowerCamelCase to PascalCase. */
function toPascalFromCamel(s: string): string {
  if (s.length === 0) return s;
  return s[0]!.toUpperCase() + s.slice(1);
}

/**
 * Convert lowerCamelCase to kebab-case for file naming.
 *
 * Both regexes are linear with bounded input (identifier strings) —
 * Bucket 2 per regex-redos-discipline; no matchTimeout needed.
 */
function kebab(s: string): string {
  return s
    .replace(/([a-z0-9])([A-Z])/g, "$1-$2")
    .toLowerCase();
}
