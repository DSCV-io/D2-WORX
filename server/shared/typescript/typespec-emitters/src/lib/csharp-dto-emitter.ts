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
//   - [property: RedactData(Reason = RedactReason.<reason>)] on every
//     @d2Redact-bearing parameter, where <reason> is the RedactReason member
//     threaded from the @d2Redact decorator — never defaulted. An unknown or
//     missing reason is a loud emit failure, so a secret-adjacent field is never
//     silently classified as PersonalInformation. The [property:] target is
//     MANDATORY — a bare positional-param attribute would NOT be seen by the
//     property-reflecting RedactDataDestructuringPolicy.
//   - Sibling `public enum` declarations for every collected enum, each carrying
//     [JsonConverter(typeof(JsonStringEnumConverter))] so the JSON wire form is
//     the member-name string (never the numeric backing). A member whose wire
//     literal differs from its C# identifier carries [JsonStringEnumMemberName("…")]
//     (the .NET 9+ attribute JsonStringEnumConverter honors — NOT [EnumMember],
//     which System.Text.Json's JsonStringEnumConverter ignores). STRICT — no
//     Unknown sentinel; an unknown wire value throws JsonException.
//   - Auto-generated banner (not the hand-authored copyright header).
//   - No phase/step/deliverable/audit-round identifiers in emitted code.

import { buildBanner } from "./banner.js";
import type { FieldInfo, NestedEnum, NestedModel } from "./model-walk.js";

// Mirrors the member names of D2.Shared.Utilities.Enums.RedactReason — the
// closed data-class taxonomy the emitter maps a @d2Redact reason onto. The
// decorator layer already validates the reason, so an unknown value reaching
// the emitter is an invariant break; the emitter fails loud rather than emit an
// un-mappable RedactReason.<value> or silently drop the redaction.
const REDACT_REASONS: ReadonlySet<string> = new Set([
  "Unspecified",
  "PersonalInformation",
  "FinancialInformation",
  "SecretInformation",
  "VerboseContent",
  "Other",
]);

/**
 * Resolve the RedactReason member for a redacted field, failing loud on an
 * unknown reason. The reason is threaded from @d2Redact — never defaulted — so a
 * secret-adjacent field can never be silently emitted as PersonalInformation.
 * Called only for fields whose `redactReason` is set (guaranteed by the caller),
 * so the reason is non-undefined here; the only failure mode is an unrecognized
 * value, which is an invariant break the decorator layer should have rejected.
 */
function resolveRedactReason(field: FieldInfo): string {
  const reason = field.redactReason!;

  if (!REDACT_REASONS.has(reason))
    throw new Error(
      `csharp-dto-emitter: @d2Redact field '${field.csName}' has an unrecognized ` +
        `RedactReason '${reason}' — expected one of: ${[...REDACT_REASONS].join(", ")}. ` +
        `Fix the @d2Redact reason on the source model.`,
    );

  return reason;
}

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
 * Sibling enum declarations are co-located in the Output file when the enum is
 * referenced by the output walk; an enum referenced ONLY by the input walk is
 * co-located in the Input file. Each enum type is therefore declared exactly
 * once across the pair (a duplicate declaration would be a C# compile error).
 *
 * @param opName       - Operation name in lowerCamelCase (e.g. "getJwks").
 * @param namespace    - Target C# namespace (from tspconfig csharp-namespace option).
 * @param sourceSpec   - Relative path to the .tsp spec file (interpolated into banner).
 * @param inputFields  - Resolved field list for the input model (empty → parameterless record).
 * @param outputFields - Resolved field list for the output model.
 * @param outputNested - Distinct nested models collected from the output walk.
 * @param inputEnums   - Distinct enums collected from the input walk (default []).
 * @param outputEnums  - Distinct enums collected from the output walk (default []).
 * @returns Array of EmittedFile (always [inputFile, outputFile]).
 */
export function emitCsharpDtos(
  opName: string,
  namespace: string,
  sourceSpec: string,
  inputFields: readonly FieldInfo[],
  outputFields: readonly FieldInfo[],
  outputNested: readonly NestedModel[],
  inputEnums: readonly NestedEnum[] = [],
  outputEnums: readonly NestedEnum[] = [],
): EmittedFile[] {
  const pascalOp = toPascalFromCamel(opName);
  const banner = buildBanner(sourceSpec);

  // An enum present in the output walk is emitted in the Output file; an enum
  // present ONLY in the input walk is emitted in the Input file — each enum type
  // is declared exactly once across the pair.
  const outputEnumNames = new Set(outputEnums.map((e) => e.name));
  const inputOnlyEnums = inputEnums.filter((e) => !outputEnumNames.has(e.name));

  const inputFile = emitInput(
    pascalOp,
    namespace,
    banner,
    inputFields,
    inputOnlyEnums,
  );
  const outputFile = emitOutput(
    pascalOp,
    namespace,
    banner,
    outputFields,
    outputNested,
    outputEnums,
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
  enums: readonly NestedEnum[],
): EmittedFile {
  const typeName = `${pascalOp}Input`;
  const needsRedactUsings = fields.some((f) => f.redactReason !== undefined);
  const content = emitRecord(
    namespace,
    banner,
    typeName,
    fields,
    needsRedactUsings,
    [],
    enums,
  );
  return { fileName: `${typeName}.g.cs`, content };
}

function emitOutput(
  pascalOp: string,
  namespace: string,
  banner: string,
  fields: readonly FieldInfo[],
  nested: readonly NestedModel[],
  enums: readonly NestedEnum[],
): EmittedFile {
  const typeName = `${pascalOp}Output`;
  const needsRedactUsings = fields.some((f) => f.redactReason !== undefined);
  const content = emitRecord(
    namespace,
    banner,
    typeName,
    fields,
    needsRedactUsings,
    nested,
    enums,
  );
  return { fileName: `${typeName}.g.cs`, content };
}

/**
 * Emit one sealed record, optionally with nested model records appended.
 *
 * `[property: RedactData(Reason = RedactReason.<reason>)]` is emitted on every
 * @d2Redact field, where `<reason>` is the RedactReason member threaded from the
 * decorator (never defaulted). The `[property:]` attribute target is
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
  enums: readonly NestedEnum[],
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

  // Conditional using directives (SA1210-sorted: D2.* before System.*).
  // [RedactData] needs the attribute + enum namespaces; an emitted enum OR a
  // [JsonPropertyName] wire-name override needs System.Text.Json.Serialization —
  // the SAME namespace supplies [JsonConverter] / JsonStringEnumConverter,
  // [JsonStringEnumMemberName] (the .NET 9+ custom-wire-name attribute), AND
  // [JsonPropertyName]. One using covers all of them; it is pushed at most once.
  const anyJsonName =
    fields.some((f) => f.jsonName !== undefined) ||
    nested.some((nm) => nm.fields.some((f) => f.jsonName !== undefined));
  const usings: string[] = [];
  if (needsRedactUsings) {
    usings.push("using D2.Shared.Utilities.Attributes;");
    usings.push("using D2.Shared.Utilities.Enums;");
  }
  if (enums.length > 0 || anyJsonName)
    usings.push("using System.Text.Json.Serialization;");
  if (usings.length > 0) {
    for (const u of usings) lines.push(u);
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

  // Sibling enum declarations, co-located below the record(s).
  for (const en of enums) {
    lines.push("");
    emitEnumBlock(lines, en);
  }

  lines.push("");
  return lines.join("\n");
}

/**
 * Emit one `public enum` block carrying
 * [JsonConverter(typeof(JsonStringEnumConverter))] so the JSON wire form is the
 * member-name string. A member whose wire literal differs from its C# identifier
 * carries [JsonStringEnumMemberName("<literal>")] so the literal is the wire form
 * (the .NET 9+ attribute JsonStringEnumConverter honors — NOT [EnumMember], which
 * System.Text.Json's JsonStringEnumConverter ignores). An explicit integer backing
 * (`= 0`) is preserved when the source declared one (the wire is still the name).
 * STRICT — no Unknown sentinel; an unknown wire value throws JsonException.
 */
function emitEnumBlock(lines: string[], en: NestedEnum): void {
  lines.push(
    `/// <summary>Generated wire enum <c>${en.name}</c> (JSON wire form is the member-name string).</summary>`,
  );
  lines.push("[JsonConverter(typeof(JsonStringEnumConverter))]");
  lines.push(`public enum ${en.name}`);
  lines.push("{");
  for (let i = 0; i < en.members.length; i++) {
    const m = en.members[i]!;
    if (i > 0) lines.push("");

    lines.push(
      `    /// <summary>The <c>${m.wireValue}</c> wire value.</summary>`,
    );
    if (m.needsEnumMember)
      lines.push(`    [JsonStringEnumMemberName("${m.wireValue}")]`);

    const assignment = m.intValue !== undefined ? ` = ${m.intValue}` : "";
    lines.push(`    ${m.csName}${assignment},`);
  }
  lines.push("}");
}

/**
 * Build the C# positional-parameter declaration string for one field.
 *
 * A field carrying a JSON wire-name override (FieldInfo.jsonName — the
 * @encodedName("application/json", "…") value, present only when it differs from
 * the default camelCase wire name) gets
 * `[property: JsonPropertyName("<jsonName>")]` prepended so System.Text.Json
 * serializes the property under the canonical wire name (e.g. "jwks_uri").
 * Redacted fields get `[property: RedactData(Reason = RedactReason.<reason>)]`,
 * where `<reason>` is the RedactReason member threaded from @d2Redact (never
 * defaulted; an unknown/missing reason is a loud emit failure).
 * Both use the `[property:]` target — mandatory because positional record
 * parameters synthesize the backing property the serializer / redaction policy
 * reflect over (a bare param-target attribute is not seen). When both are
 * present the JSON-name attribute precedes the redact attribute.
 */
function buildParam(field: FieldInfo): string {
  const jsonNameAttr =
    field.jsonName !== undefined
      ? `[property: JsonPropertyName("${field.jsonName}")] `
      : "";
  const redactAttr =
    field.redactReason !== undefined
      ? `[property: RedactData(Reason = RedactReason.${resolveRedactReason(field)})] `
      : "";
  return `${jsonNameAttr}${redactAttr}${field.csType} ${field.csName}`;
}

/** Convert lowerCamelCase op name to PascalCase type prefix. */
function toPascalFromCamel(s: string): string {
  if (s.length === 0) return s;
  return s[0]!.toUpperCase() + s.slice(1);
}
