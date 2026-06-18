// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// C# DTO emitter — pure string-template emission of sealed-record DTOs.
//
// For each operation, emits:
//   <Op>Input.g.cs   — positional sealed record for the op's input model;
//                      parameterless when the model has no properties.
//   <Op>Output.g.cs  — positional sealed record for the op's output model,
//                      plus any nested-model records co-located below it.
//
// Conventions (all per ADR-0020 + PATTERNS.md):
//   - sealed record with positional parameters.
//   - T? when ModelProperty.optional === true.
//   - IReadOnlyList<T> for collections.
//   - [property: RedactData(Reason = RedactReason.PersonalInformation)] on
//     every @d2Redact-bearing parameter (the [property:] target is MANDATORY —
//     a bare positional-param attribute would NOT be seen by the property-
//     reflecting RedactDataDestructuringPolicy).
//   - Auto-generated banner (not the hand-authored copyright header).
//   - No phase/step/deliverable/audit-round identifiers in emitted code.

import { buildBanner } from "./banner.js";
import type { FieldInfo, NestedModel } from "./model-walk.js";

// ---------------------------------------------------------------------------
// Public types
// ---------------------------------------------------------------------------

/** One emitted C# file: its relative name and full text content. */
export interface EmittedFile {
  /** File name relative to the emitter output directory. */
  readonly fileName: string;
  /** Full text content ready to write. */
  readonly content: string;
}

// ---------------------------------------------------------------------------
// Public API
// ---------------------------------------------------------------------------

/**
 * Emit the C# DTO pair (`<Op>Input.g.cs` + `<Op>Output.g.cs`) for one
 * operation. Pure function — no I/O; returns `EmittedFile[]` so tests
 * can assert content directly.
 *
 * @param opName       - Operation name in lowerCamelCase (e.g. "getJwks").
 * @param namespace    - Target C# namespace (from tspconfig csharp-namespace option).
 * @param sourceSpec   - Relative path to the .tsp spec file (interpolated into banner).
 * @param inputFields  - Resolved field list for the input model (empty → parameterless record).
 * @param outputFields - Resolved field list for the output model.
 * @param outputNested - Distinct nested models collected from the output walk.
 * @returns Array of EmittedFile (always [inputFile, outputFile]).
 */
export function emitCsharpDtos(
  opName: string,
  namespace: string,
  sourceSpec: string,
  inputFields: readonly FieldInfo[],
  outputFields: readonly FieldInfo[],
  outputNested: readonly NestedModel[],
): EmittedFile[] {
  const pascalOp = toPascalFromCamel(opName);
  const banner = buildBanner(sourceSpec);

  const inputFile = emitInput(pascalOp, namespace, banner, inputFields);
  const outputFile = emitOutput(
    pascalOp,
    namespace,
    banner,
    outputFields,
    outputNested,
  );

  return [inputFile, outputFile];
}

// ---------------------------------------------------------------------------
// Internal emitters
// ---------------------------------------------------------------------------

function emitInput(
  pascalOp: string,
  namespace: string,
  banner: string,
  fields: readonly FieldInfo[],
): EmittedFile {
  const typeName = `${pascalOp}Input`;
  const needsRedactUsings = fields.some((f) => f.redact);
  const content = emitRecord(
    namespace,
    banner,
    typeName,
    fields,
    needsRedactUsings,
    [],
  );
  return { fileName: `${typeName}.g.cs`, content };
}

function emitOutput(
  pascalOp: string,
  namespace: string,
  banner: string,
  fields: readonly FieldInfo[],
  nested: readonly NestedModel[],
): EmittedFile {
  const typeName = `${pascalOp}Output`;
  const needsRedactUsings = fields.some((f) => f.redact);
  const content = emitRecord(
    namespace,
    banner,
    typeName,
    fields,
    needsRedactUsings,
    nested,
  );
  return { fileName: `${typeName}.g.cs`, content };
}

/**
 * Emit one sealed record, optionally with nested model records appended.
 *
 * `[property: RedactData(Reason = RedactReason.PersonalInformation)]` is
 * emitted on every @d2Redact field. The `[property:]` attribute target is
 * load-bearing: positional record parameters synthesize a backing property,
 * and the RedactDataDestructuringPolicy reflects over PUBLIC PROPERTIES (not
 * constructor params). A bare attribute on the param targets the parameter,
 * not the generated property — the redaction silently no-ops. The `[property:]`
 * target forces the attribute onto the generated property.
 */
function emitRecord(
  namespace: string,
  banner: string,
  typeName: string,
  fields: readonly FieldInfo[],
  needsRedactUsings: boolean,
  nested: readonly NestedModel[],
): string {
  const lines: string[] = [];

  // Banner.
  lines.push(banner);

  // Nullable enable.
  lines.push("#nullable enable");
  lines.push("");

  // Namespace.
  lines.push(`namespace ${namespace};`);
  lines.push("");

  // Conditional using directives for [RedactData].
  if (needsRedactUsings) {
    lines.push("using D2.Shared.Utilities.Attributes;");
    lines.push("using D2.Shared.Utilities.Enums;");
    lines.push("");
  }

  // The record declaration.
  if (fields.length === 0) {
    // Parameterless sealed record (e.g. GetJwksInput).
    lines.push(
      `/// <summary>Generated input DTO. No parameters required.</summary>`,
    );
    lines.push(`public sealed record ${typeName};`);
  } else {
    lines.push(
      `/// <summary>Generated DTO for the <c>${typeName}</c> operation.</summary>`,
    );
    const params = fields.map((f) => buildParam(f)).join(",\n    ");
    lines.push(`public sealed record ${typeName}(`);
    lines.push(`    ${params});`);
  }

  // Nested model records, co-located below the owning output record.
  for (const nm of nested) {
    lines.push("");
    lines.push(
      `/// <summary>Generated nested model DTO for <c>${nm.name}</c>.</summary>`,
    );
    if (nm.fields.length === 0) {
      lines.push(`public sealed record ${nm.name};`);
    } else {
      const nestedParams = nm.fields.map((f) => buildParam(f)).join(",\n    ");
      lines.push(`public sealed record ${nm.name}(`);
      lines.push(`    ${nestedParams});`);
    }
  }

  lines.push("");
  return lines.join("\n");
}

/**
 * Build the C# positional-parameter declaration string for one field.
 *
 * Redacted fields get `[property: RedactData(Reason = RedactReason.PersonalInformation)]`
 * prepended. The `[property:]` target is mandatory — see emitRecord doc comment.
 */
function buildParam(field: FieldInfo): string {
  const redactAttr = field.redact
    ? "[property: RedactData(Reason = RedactReason.PersonalInformation)] "
    : "";
  return `${redactAttr}${field.csType} ${field.csName}`;
}

/** Convert lowerCamelCase op name to PascalCase type prefix. */
function toPascalFromCamel(s: string): string {
  if (s.length === 0) return s;
  return s[0]!.toUpperCase() + s.slice(1);
}
