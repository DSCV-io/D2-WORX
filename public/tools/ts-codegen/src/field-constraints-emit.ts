// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import {
  diagError,
  type EmitDiagnostic,
  type EmitResult,
  DiagnosticIds,
  formatDiagnostic,
} from "./lib/diagnostics.js";
import {
  buildHeader,
  isOutputUpToDate,
  writeGeneratedFile,
} from "./lib/file-emit.js";
import { contractsPath, tsPackagePath } from "./lib/paths.js";
import { loadSpec } from "./lib/spec-loader.js";
import { StringBuilder } from "./lib/string-builder.js";

const CONST_NAME_RE = /^[A-Z][A-Z0-9_]*$/;
const ENUM_NAME_RE = /^[A-Z][A-Za-z0-9]*$/;
const IDENTIFIER_RE = /^[A-Za-z_][A-Za-z0-9_]*$/;

const SPEC_REF = "contracts/validation/field-constraints.spec.json";

/** One field-constraint integer constant parsed from the spec. */
export interface FieldConstraintEntry {
  readonly name: string;
  readonly value: number;
  readonly doc: string;
}

/** One member of a taxonomy enum parsed from the spec. */
export interface EnumMemberEntry {
  readonly name: string;
  readonly doc: string;
}

/** One closed-list taxonomy enum parsed from the spec. */
export interface EnumEntry {
  readonly name: string;
  readonly backing: "byte";
  readonly doc: string;
  readonly members: readonly EnumMemberEntry[];
}

/** Top-level shape of `field-constraints.spec.json`. */
export interface FieldConstraintsSpec {
  readonly constraints: readonly FieldConstraintEntry[];
  readonly enums: readonly EnumEntry[];
}

/** Result of validating the constraints block. */
export interface ValidatedConstraints {
  readonly entries: readonly FieldConstraintEntry[];
  readonly diagnostics: readonly EmitDiagnostic[];
}

/** Result of validating the enums block. */
export interface ValidatedEnums {
  readonly entries: readonly EnumEntry[];
  readonly diagnostics: readonly EmitDiagnostic[];
}

/**
 * Validate the constraints block — surface invalid SCREAMING_SNAKE names,
 * duplicate names, and non-positive values. Mirrors the .NET emitter's
 * predicate set for well-formed spec input; TS adds an integer guard for
 * test-injected non-integer values (the .NET loader rejects fractional values
 * earlier via `TryGetInt32`, mapping them to D2FC001 instead of D2FC004).
 */
export function validateConstraints(
  spec: FieldConstraintsSpec,
): ValidatedConstraints {
  const diagnostics: EmitDiagnostic[] = [];
  const validEntries: FieldConstraintEntry[] = [];
  const seen = new Set<string>();

  for (const entry of spec.constraints) {
    if (!CONST_NAME_RE.test(entry.name)) {
      diagnostics.push(
        diagError(
          DiagnosticIds.FC_INVALID_CONST_NAME,
          `invalid constraint name '${entry.name}' — must match ${CONST_NAME_RE.source}`,
        ),
      );
      continue;
    }
    if (seen.has(entry.name)) {
      diagnostics.push(
        diagError(
          DiagnosticIds.FC_DUPLICATE_CONST_NAME,
          `duplicate constraint name '${entry.name}'`,
        ),
      );
      continue;
    }
    seen.add(entry.name);
    if (!Number.isInteger(entry.value) || entry.value <= 0) {
      diagnostics.push(
        diagError(
          DiagnosticIds.FC_NON_POSITIVE_VALUE,
          `constraint '${entry.name}' has non-positive value ${entry.value} (must be > 0)`,
        ),
      );
      continue;
    }
    validEntries.push(entry);
  }
  return { entries: validEntries, diagnostics };
}

/**
 * Validate the enums block — surface invalid PascalCase enum names, duplicate
 * enum names, unsupported backing types, empty member lists, invalid member
 * identifiers, and duplicate members. Mirrors the .NET emitter's predicate set
 * so cross-language drift between the two validation surfaces is structurally
 * impossible.
 */
export function validateEnums(spec: FieldConstraintsSpec): ValidatedEnums {
  const diagnostics: EmitDiagnostic[] = [];
  const validEntries: EnumEntry[] = [];
  const seenEnums = new Set<string>();

  for (const entry of spec.enums) {
    if (!ENUM_NAME_RE.test(entry.name)) {
      diagnostics.push(
        diagError(
          DiagnosticIds.FC_INVALID_ENUM_NAME,
          `invalid enum name '${entry.name}' — must match ${ENUM_NAME_RE.source}`,
        ),
      );
      continue;
    }
    if (seenEnums.has(entry.name)) {
      diagnostics.push(
        diagError(
          DiagnosticIds.FC_DUPLICATE_ENUM_NAME,
          `duplicate enum name '${entry.name}'`,
        ),
      );
      continue;
    }
    seenEnums.add(entry.name);
    if ((entry.backing as string) !== "byte") {
      diagnostics.push(
        diagError(
          DiagnosticIds.FC_MALFORMED_SPEC,
          `enum '${entry.name}' has unsupported backing ` +
            `'${entry.backing as string}' — only "byte" is supported`,
        ),
      );
      continue;
    }
    if (entry.members.length === 0) {
      diagnostics.push(
        diagError(
          DiagnosticIds.FC_EMPTY_ENUM_MEMBER_LIST,
          `enum '${entry.name}' declares an empty members list (must have >= 1)`,
        ),
      );
      continue;
    }
    if (!validateMembers(entry, diagnostics)) continue;
    validEntries.push(entry);
  }
  return { entries: validEntries, diagnostics };
}

function validateMembers(
  entry: EnumEntry,
  diagnostics: EmitDiagnostic[],
): boolean {
  const seenMembers = new Set<string>();
  let clean = true;
  for (const member of entry.members) {
    if (!IDENTIFIER_RE.test(member.name)) {
      diagnostics.push(
        diagError(
          DiagnosticIds.FC_INVALID_ENUM_MEMBER_NAME,
          `enum '${entry.name}' member '${member.name}' is not a valid identifier`,
        ),
      );
      clean = false;
      continue;
    }
    if (seenMembers.has(member.name)) {
      diagnostics.push(
        diagError(
          DiagnosticIds.FC_DUPLICATE_ENUM_MEMBER,
          `enum '${entry.name}' declares member '${member.name}' more than once`,
        ),
      );
      clean = false;
      continue;
    }
    seenMembers.add(member.name);
  }
  return clean;
}

/**
 * Emit the `field-constraints.g.ts` source — a plain numeric const-object of the
 * field-constraint bounds (matching geo's numeric `GeopoliticalEntityType` shape;
 * the values are ints, not a closed-set wire vocabulary needing a brand).
 * Stateless and unit-testable. Preserves spec order so spec edits map to
 * predictable diffs.
 */
export function emitConstraints(spec: FieldConstraintsSpec): EmitResult {
  const v = validateConstraints(spec);
  const errors = v.diagnostics.filter((d) => d.severity === "error");
  if (errors.length > 0) return { source: "", diagnostics: v.diagnostics };

  const sb = new StringBuilder();
  sb.appendLine(buildHeader(SPEC_REF));
  sb.appendLine("/* eslint-disable */");
  sb.appendLine();
  sb.appendLine("/**");
  sb.appendLine(
    " * Shared field-length / digit-count bounds enforced by the domain value",
  );
  sb.appendLine(
    " * objects (contacts + Location), the FE/BFF Zod schemas, and arbitrary",
  );
  sb.appendLine(
    " * backend modules. Mirrors .NET `DcsvIo.D2.Validation.Abstractions.FieldConstraints`",
  );
  sb.appendLine(
    " * byte-for-byte (single spec source emits both sides; cross-language drift",
  );
  sb.appendLine(" * is structurally impossible).");
  sb.appendLine(" */");
  sb.appendLine("export const FieldConstraints = {");
  sb.increaseIndent();
  for (const e of v.entries) {
    sb.appendLine(`/** ${escapeJsDoc(e.doc)} */`);
    sb.appendLine(`${e.name}: ${e.value},`);
  }
  sb.decreaseIndent();
  sb.appendLine("} as const;");
  sb.appendLine();
  sb.appendLine(
    "export type FieldConstraint = " +
      "(typeof FieldConstraints)[keyof typeof FieldConstraints];",
  );
  sb.appendLine();
  return { source: sb.toString(), diagnostics: v.diagnostics };
}

/**
 * Emit the `taxonomy.g.ts` source — for each closed-list enum: a string-valued
 * const-object, a branded derived type, a Zod `z.enum([...])` schema, and an
 * `ALL_*_SET` membership set. Mirrors the geo `emitConstObjectEnum` shape.
 */
export function emitTaxonomy(spec: FieldConstraintsSpec): EmitResult {
  const v = validateEnums(spec);
  const errors = v.diagnostics.filter((d) => d.severity === "error");
  if (errors.length > 0) return { source: "", diagnostics: v.diagnostics };

  const sb = new StringBuilder();
  sb.appendLine(buildHeader(SPEC_REF));
  sb.appendLine("/* eslint-disable */");
  sb.appendLine();
  sb.appendLine('import { z } from "zod";');
  sb.appendLine();
  for (const e of v.entries) emitEnum(sb, e);
  return { source: sb.toString(), diagnostics: v.diagnostics };
}

function emitEnum(sb: StringBuilder, entry: EnumEntry): void {
  sb.appendLine("/**");
  sb.appendLine(` * ${escapeJsDoc(entry.doc)}`);
  sb.appendLine(
    ` * Mirrors .NET \`DcsvIo.D2.Validation.Abstractions.${entry.name}\` byte-for-byte`,
  );
  sb.appendLine(
    " * over the wire (string-encoded member name in both runtimes).",
  );
  sb.appendLine(" */");
  sb.appendLine(`export const ${entry.name} = {`);
  sb.increaseIndent();
  for (const m of entry.members) {
    sb.appendLine(`/** ${escapeJsDoc(m.doc)} */`);
    sb.appendLine(`${m.name}: "${escapeStringLiteral(m.name)}",`);
  }
  sb.decreaseIndent();
  sb.appendLine("} as const;");
  sb.appendLine();
  sb.appendLine(
    `export type ${entry.name} = ` +
      `(typeof ${entry.name})[keyof typeof ${entry.name}] & ` +
      `{ readonly __brand: "${entry.name}" };`,
  );
  sb.appendLine();
  const setName = `ALL_${camelToScreaming(entry.name)}_SET`;
  sb.appendLine(`export const ${setName}: ReadonlySet<string> = new Set([`);
  sb.increaseIndent();
  for (const m of entry.members)
    sb.appendLine(`"${escapeStringLiteral(m.name)}",`);
  sb.decreaseIndent();
  sb.appendLine("]);");
  sb.appendLine();
  sb.appendLine(`export const ${entry.name}Schema = z.enum([`);
  sb.increaseIndent();
  for (const m of entry.members)
    sb.appendLine(`"${escapeStringLiteral(m.name)}",`);
  sb.decreaseIndent();
  sb.appendLine("]);");
  sb.appendLine();
}

function escapeStringLiteral(value: string): string {
  return value.replace(/\\/g, "\\\\").replace(/"/g, '\\"');
}

function escapeJsDoc(value: string): string {
  return value.replace(/\*\//g, "*\\/");
}

/** `NamePrefix` → `NAME_PREFIX`, `BiologicalSex` → `BIOLOGICAL_SEX`. */
function camelToScreaming(name: string): string {
  return name.replace(/([a-z0-9])([A-Z])/g, "$1_$2").toUpperCase();
}

const SPEC_PATH = contractsPath("validation", "field-constraints.spec.json");
const FIELD_CONSTRAINTS_TARGET = tsPackagePath(
  "validation",
  "abstractions",
  "src",
  "generated",
  "field-constraints.g.ts",
);
const TAXONOMY_TARGET = tsPackagePath(
  "validation",
  "abstractions",
  "src",
  "generated",
  "taxonomy.g.ts",
);

/**
 * Run the field-constraints emitter. Per-spec mtime check skips emit when both
 * outputs are newer than the spec; pass `force=true` to bypass.
 */
export function runFieldConstraintsEmit(
  force = false,
): readonly EmitDiagnostic[] {
  const upToDate =
    isOutputUpToDate(FIELD_CONSTRAINTS_TARGET, [SPEC_PATH]) &&
    isOutputUpToDate(TAXONOMY_TARGET, [SPEC_PATH]);
  if (!force && upToDate) return [];

  const loadResult = loadSpec<FieldConstraintsSpec>(
    SPEC_PATH,
    DiagnosticIds.FC_MALFORMED_SPEC,
  );
  if (loadResult.spec === undefined) return loadResult.diagnostics;

  const constraints = emitConstraints(loadResult.spec);
  const taxonomy = emitTaxonomy(loadResult.spec);
  const diagnostics = [...constraints.diagnostics, ...taxonomy.diagnostics];
  if (diagnostics.some((d) => d.severity === "error")) return diagnostics;

  writeGeneratedFile(FIELD_CONSTRAINTS_TARGET, constraints.source);
  writeGeneratedFile(TAXONOMY_TARGET, taxonomy.source);
  return diagnostics;
}

const isMain =
  import.meta.url === `file://${process.argv[1]}` ||
  process.argv[1]?.endsWith("field-constraints-emit.ts") === true;
if (isMain) {
  const force = process.argv.includes("--force");
  const diagnostics = runFieldConstraintsEmit(force);
  for (const d of diagnostics) console.error(formatDiagnostic(d));
  if (diagnostics.some((d) => d.severity === "error")) process.exit(1);
}
