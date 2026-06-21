// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Shared proto-string ↔ C#-enum mapper-helper emission for the gRPC service
// and client mapper emitters.
//
// An enum/string-literal-union DTO field maps to a proto `string` field
// carrying the member-name (or [EnumMember]-literal) wire string — the SAME
// string the JSON wire uses (full cross-language value parity; type safety
// lives at the boundary mapper, exactly like JSON). These helpers emit the
// per-enum bridge:
//
//   internal static string ToWire(this <Enum> value)
//       — outbound: each member → its wire string (a closed switch; total over
//         the C# enum). The default arm is defensive (an out-of-range cast) and
//         falls back to value.ToString().
//
//   internal static D2Result<<Enum>> Parse<Enum>Wire(string? value)
//       — inbound: each wire string → its member; the default arm fails LOUD
//         with D2Result<<Enum>>.ValidationFailed (an unknown wire value is a 400
//         ValidationFailed — strict, NO fallback sentinel, matching the JSON
//         JsonStringEnumConverter policy).
//
// Both helpers are emitted into the same mapper file (server or client) as the
// transport mappers that call them.

import type { FieldInfo, NestedEnum } from "./model-walk.js";

/**
 * Collect the distinct enums referenced by a field list, in first-encounter
 * order (deduped by enum name).
 */
export function collectFieldEnums(
  fields: readonly FieldInfo[],
): readonly NestedEnum[] {
  const seen = new Map<string, NestedEnum>();
  for (const f of fields)
    if (f.enumRef !== undefined && !seen.has(f.enumRef.name))
      seen.set(f.enumRef.name, f.enumRef);
  return [...seen.values()];
}

/**
 * Build the global::-rooted using-aliases for the enum types referenced across
 * the given enums. The enum types live in the DTO namespace. Returns sorted
 * `using <Enum> = global::<dtoNs>.<Enum>;` lines, or [] when the DTO namespace
 * is the local namespace.
 */
export function enumAliasUsings(
  enums: readonly NestedEnum[],
  dtoCsharpNs: string,
  localNs: string,
): string[] {
  if (dtoCsharpNs === localNs) return [];

  return enums
    .map((e) => `using ${e.name} = global::${dtoCsharpNs}.${e.name};`)
    .sort();
}

/**
 * Emit the per-enum ToWire + Parse<Enum>Wire helper extension blocks for the
 * given enums, appended (indented 4 spaces) to a mapper static class body.
 * `pushLine` receives each line; the caller controls placement inside the class.
 */
export function emitEnumMapperHelpers(
  pushLine: (line: string) => void,
  enums: readonly NestedEnum[],
): void {
  for (const e of enums) {
    pushLine("");

    // Outbound: enum → wire string.
    pushLine(`    extension(${e.name} value)`);
    pushLine("    {");
    pushLine(
      `        /// <summary>Maps <see cref="${e.name}"/> to its wire string.</summary>`,
    );
    pushLine(`        internal string ToWire()`);
    pushLine("        {");
    pushLine("            return value switch");
    pushLine("            {");
    for (const m of e.members)
      pushLine(`                ${e.name}.${m.csName} => "${m.wireValue}",`);
    // Defensive default — an out-of-range cast falls back to the member name.
    pushLine(`                _ => value.ToString(),`);
    pushLine("            };");
    pushLine("        }");
    pushLine("    }");
    pushLine("");

    // Inbound: wire string → enum (fail-loud ValidationFailed on unknown).
    pushLine(`    extension(string)`);
    pushLine("    {");
    pushLine(
      `        /// <summary>Parses a wire string to <see cref="${e.name}"/>; an unknown value fails loud (400 ValidationFailed).</summary>`,
    );
    pushLine(`        internal static D2Result<${e.name}> Parse${e.name}Wire(string? value)`);
    pushLine("        {");
    pushLine("            return value switch");
    pushLine("            {");
    for (const m of e.members)
      pushLine(
        `                "${m.wireValue}" => D2Result<${e.name}>.Ok(${e.name}.${m.csName}),`,
      );
    pushLine(
      `                _ => D2Result<${e.name}>.ValidationFailed(`,
    );
    pushLine(
      `                    inputErrors: [new InputError(nameof(value), [TK.Common.Errors.VALIDATION_FAILED])]),`,
    );
    pushLine("            };");
    pushLine("        }");
    pushLine("    }");
  }
}
