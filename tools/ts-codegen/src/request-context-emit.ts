// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import {
  type ContextSpec,
  emitAuthContext,
  type PropertySpec,
} from "./auth-context-emit.js";
import {
  type EmitDiagnostic,
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

/**
 * Spec-driven property → IPropagatedContext field type. Strings map to
 * `string | undefined`; numbers map to `number | undefined`; booleans map to
 * `boolean | undefined`. Uses `undefined` (not `null`) per the codebase
 * convention. Mirrors the .NET PropagatedEmitter mapping.
 */
function tsTypeFor(prop: PropertySpec): string {
  if (prop.type === "int?" || prop.type === "double?")
    return "number | undefined";
  if (prop.type === "bool?") return "boolean | undefined";
  return "string | undefined";
}

/**
 * Camel-cases a PascalCase property name.
 */
function camelCase(pascal: string): string {
  return pascal.length === 0
    ? pascal
    : pascal[0]!.toLowerCase() + pascal.slice(1);
}

/**
 * Emit `IPropagatedContext.g.ts` — the cross-hop subset (`propagate: true`
 * properties only).
 */
export function emitPropagatedContextInterface(spec: ContextSpec): string {
  const sb = new StringBuilder();
  sb.appendLine(
    buildHeader(`contracts/request-context/${spec.name}.spec.json`),
  );
  sb.appendLine("/* eslint-disable */");
  sb.appendLine();
  sb.appendLine("/**");
  sb.appendLine(
    " * Cross-hop propagated subset of IRequestContext. Identity fields",
  );
  sb.appendLine(
    " * (UserId / OrgId / Scopes / ActorChain) are NOT included — they",
  );
  sb.appendLine(" * rebuild from the JWT at every sync hop.");
  sb.appendLine(" */");
  sb.appendLine("export interface IPropagatedContext {");
  sb.increaseIndent();
  for (const section of spec.sections) {
    for (const prop of section.properties) {
      if (prop.propagate !== true) continue;
      const tsType = tsTypeFor(prop);
      // Nullable spec types emit as optional properties (`readonly field?: T`)
      // matching the auth-context-emit.ts convention: undefined is the only
      // absent sentinel in TypeScript.
      if (tsType.endsWith("| undefined")) {
        const baseType = tsType.slice(0, -" | undefined".length).trimEnd();
        sb.appendLine(`readonly ${camelCase(prop.name)}?: ${baseType};`);
      } else {
        sb.appendLine(`readonly ${camelCase(prop.name)}: ${tsType};`);
      }
    }
  }
  sb.decreaseIndent();
  sb.appendLine("}");
  sb.appendLine();
  return sb.toString();
}

/**
 * Emit `PropagatedContextSerializer.g.ts` — `serialize` + `tryDecode`
 * with per-field `maxLength` enforcement on decode. Mirrors .NET
 * `PropagatedEmitter` 1:1 named class with `Serialize`/`Deserialize`.
 */
export function emitPropagatedSerializer(spec: ContextSpec): string {
  const sb = new StringBuilder();
  sb.appendLine(
    buildHeader(`contracts/request-context/${spec.name}.spec.json`),
  );
  sb.appendLine("/* eslint-disable */");
  sb.appendLine();
  sb.appendLine(
    'import type { IPropagatedContext } from "./IPropagatedContext.g.js";',
  );
  sb.appendLine();
  sb.appendLine("/**");
  sb.appendLine(
    " * Serializer for the IPropagatedContext envelope. Round-trip-safe:",
  );
  sb.appendLine(
    " * `serialize(deserialize(s)) === s` for any well-formed envelope.",
  );
  sb.appendLine(
    " * `tryDecode` enforces per-field maxLength caps from the spec; a",
  );
  sb.appendLine(
    " * forged envelope with any cap exceeded is dropped wholesale.",
  );
  sb.appendLine(" */");
  sb.appendLine("export class PropagatedContextSerializer {");
  sb.increaseIndent();
  emitSerializeMethod(sb, spec);
  sb.appendLine();
  emitTryDecodeMethod(sb, spec);
  sb.decreaseIndent();
  sb.appendLine("}");
  sb.appendLine();
  return sb.toString();
}

function emitSerializeMethod(sb: StringBuilder, spec: ContextSpec): void {
  sb.appendLine("static serialize(ctx: IPropagatedContext): string {");
  sb.increaseIndent();
  sb.appendLine("const o: Record<string, unknown> = {};");
  for (const section of spec.sections) {
    for (const prop of section.properties) {
      if (prop.propagate !== true) continue;
      const camel = camelCase(prop.name);
      sb.appendLine(
        `if (ctx.${camel} !== null && ctx.${camel} !== undefined) o["${camel}"] = ctx.${camel};`,
      );
    }
  }
  sb.appendLine("return JSON.stringify(o);");
  sb.decreaseIndent();
  sb.appendLine("}");
}

function emitTryDecodeMethod(sb: StringBuilder, spec: ContextSpec): void {
  sb.appendLine("/**");
  sb.appendLine(
    " * Decode an envelope from a wire string. Wire-boundary carve-out per",
  );
  sb.appendLine(
    " * rules.md §6.15: the input is `string | null | undefined` because the",
  );
  sb.appendLine(
    " * primary caller passes `Headers.get(...)` directly, and the Web `Headers`",
  );
  sb.appendLine(
    " * API contract returns `string | null` (`null` for absent headers). The",
  );
  sb.appendLine(
    " * tryDecode boundary normalizes to `undefined` immediately — no `null`",
  );
  sb.appendLine(" * propagates inward.");
  sb.appendLine(" */");
  sb.appendLine(
    "static tryDecode(input: string | null | undefined): IPropagatedContext | undefined {",
  );
  sb.increaseIndent();
  sb.appendLine(
    'if (input === null || input === undefined || input === "") return undefined;',
  );
  sb.appendLine("let parsed: Record<string, unknown>;");
  sb.appendLine("try {");
  sb.increaseIndent();
  sb.appendLine("parsed = JSON.parse(input) as Record<string, unknown>;");
  sb.decreaseIndent();
  sb.appendLine("} catch {");
  sb.increaseIndent();
  sb.appendLine("return undefined;");
  sb.decreaseIndent();
  sb.appendLine("}");
  sb.appendLine(
    'if (parsed === null || typeof parsed !== "object") return undefined;',
  );
  sb.appendLine("const out: Record<string, unknown> = {};");

  for (const section of spec.sections) {
    for (const prop of section.properties) {
      if (prop.propagate !== true) continue;
      const camel = camelCase(prop.name);
      const isString =
        prop.type !== "int?" &&
        prop.type !== "double?" &&
        prop.type !== "bool?";
      sb.appendLine(`{`);
      sb.increaseIndent();
      sb.appendLine(`const v = parsed["${camel}"];`);
      sb.appendLine(`if (v === undefined || v === null) {`);
      sb.increaseIndent();
      // Absent wire values produce undefined (not null) — omit from the output
      // object so the deserialized IPropagatedContext follows the `T | undefined`
      // convention. The property is simply not set on `out`, which is equivalent
      // to `undefined` when accessed.
      sb.appendLine(`// absent — leave out["${camel}"] unset (undefined)`);
      sb.decreaseIndent();
      sb.appendLine(`} else {`);
      sb.increaseIndent();
      if (isString) {
        sb.appendLine(`if (typeof v !== "string") return undefined;`);
        if (prop.maxLength !== undefined)
          sb.appendLine(`if (v.length > ${prop.maxLength}) return undefined;`);
        sb.appendLine(`out["${camel}"] = v;`);
      } else if (prop.type === "bool?") {
        sb.appendLine(`if (typeof v !== "boolean") return undefined;`);
        sb.appendLine(`out["${camel}"] = v;`);
      } else {
        sb.appendLine(
          `if (typeof v !== "number" || !Number.isFinite(v)) return undefined;`,
        );
        sb.appendLine(`out["${camel}"] = v;`);
      }
      sb.decreaseIndent();
      sb.appendLine(`}`);
      sb.decreaseIndent();
      sb.appendLine(`}`);
    }
  }

  sb.appendLine("return out as unknown as IPropagatedContext;");
  sb.decreaseIndent();
  sb.appendLine("}");
}

const SPEC_PATH = contractsPath("request-context", "IRequestContext.spec.json");
const TARGET_DIR = tsPackagePath("request-context-abstractions", "src");
const INTERFACE_TARGET = `${TARGET_DIR}/IRequestContext.g.ts`;
const PROPAGATED_TARGET = `${TARGET_DIR}/IPropagatedContext.g.ts`;
const SERIALIZER_TARGET = `${TARGET_DIR}/PropagatedContextSerializer.g.ts`;

/**
 * Run the request-context emitter. Writes 3 .g.ts files. Per-spec
 * mtime check skips emit when every output is newer than the spec.
 * Pass `force=true` to bypass.
 */
export function runRequestContextEmit(
  force = false,
): readonly EmitDiagnostic[] {
  const outputs = [INTERFACE_TARGET, PROPAGATED_TARGET, SERIALIZER_TARGET];
  if (!force && outputs.every((p) => isOutputUpToDate(p, [SPEC_PATH])))
    return [];

  const loadResult = loadSpec<ContextSpec>(
    SPEC_PATH,
    DiagnosticIds.CTX_MALFORMED_SPEC,
  );
  if (loadResult.spec === undefined) return loadResult.diagnostics;

  // Reuse the auth-context emit logic for the interface (same shape).
  // importMode='package' tells the emitter to import enum/type defs
  // from @d2/auth-context-abstractions instead of relative ./enums/.
  const interfaceResult = emitAuthContext(loadResult.spec, "package");
  if (interfaceResult.diagnostics.length > 0)
    return interfaceResult.diagnostics;

  writeGeneratedFile(INTERFACE_TARGET, interfaceResult.source);
  writeGeneratedFile(
    PROPAGATED_TARGET,
    emitPropagatedContextInterface(loadResult.spec),
  );
  writeGeneratedFile(
    SERIALIZER_TARGET,
    emitPropagatedSerializer(loadResult.spec),
  );
  return [];
}

const isMain =
  import.meta.url === `file://${process.argv[1]}` ||
  process.argv[1]?.endsWith("request-context-emit.ts") === true;
if (isMain) {
  const force = process.argv.includes("--force");
  const diagnostics = runRequestContextEmit(force);
  for (const d of diagnostics) console.error(formatDiagnostic(d));
  if (diagnostics.some((d) => d.severity === "error")) process.exit(1);
}
