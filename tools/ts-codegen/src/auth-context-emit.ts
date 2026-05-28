// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import {
  diagError,
  type EmitDiagnostic,
  DiagnosticIds,
  type EmitResult,
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
 * Spec shape for context interfaces (`IAuthContext.spec.json` /
 * `IRequestContext.spec.json`). Only fields the TS emitter needs are
 * declared.
 *
 * `extends` is a WIRE-SHAPE field: the spec JSON literally permits `null`
 * (omitted/explicit-null both mean "no parent interface"), so the parser
 * must accept `null` from the wire. Domain consumers see the resolved
 * import target via `resolveExtendsFqn` and never touch the raw value.
 * Per rules.md §6.15 wire-boundary carve-out — the spec literal forces
 * `| null` here; downstream code normalizes at the parse boundary.
 */
export interface ContextSpec {
  readonly name: string;
  readonly namespace: string;
  readonly description?: string;
  /** Wire-shape `null` permitted: see interface JSDoc. */
  readonly extends?: string | null;
  readonly sections: readonly Section[];
}

export interface Section {
  readonly name: string;
  readonly properties: readonly PropertySpec[];
}

export interface PropertySpec {
  readonly name: string;
  readonly type: string;
  readonly claim?: string;
  readonly trinaryAuth?: boolean;
  readonly derived?: string;
  readonly default?: string;
  readonly doc?: string;
  readonly propagate?: boolean;
  readonly maxLength?: number;
  readonly redact?: boolean;
}

/**
 * Maps the closed-vocabulary .NET type to its TS equivalent. Mirrors the
 * .NET `InterfaceEmitter` mapping so cross-language consumers see equivalent
 * shapes.
 *
 * Nullable spec types (e.g. `string?`, `bool?`) map to their optional TS
 * counterpart using `| undefined` (never `| null`) per the codebase convention
 * that `undefined` is the only "absent" sentinel in TypeScript. The generated
 * interface property is written as `readonly field?: T` which is equivalent to
 * `readonly field: T | undefined` and matches C#'s `T?` optional-field idiom.
 */
const TYPE_MAP: Readonly<Record<string, string>> = {
  "string?": "string | undefined",
  "bool?": "boolean | undefined",
  "int?": "number | undefined",
  "double?": "number | undefined",
  "Guid?": "string | undefined",
  "DateTimeOffset?": "string | undefined",
  "OrgType?": "OrgType | undefined",
  "Role?": "Role | undefined",
  "ActorKind?": "ActorKind | undefined",
  "ImpersonationKind?": "ImpersonationKind | undefined",
  "IReadOnlyList<ActorEntry>": "readonly ActorEntry[]",
  "IReadOnlyList<string>": "readonly string[]",
  "IReadOnlySet<string>": "ReadonlySet<string>",
};

/**
 * Pure emit logic. Stateless and unit-testable. Sorts properties within
 * each section by name to defend against input-order drift between runs.
 */
export function emitAuthContext(
  spec: ContextSpec,
  importMode: "relative" | "package" = "relative",
): EmitResult {
  const diagnostics: EmitDiagnostic[] = [];

  // Validate the closed type vocabulary; surface unknown types as errors.
  const seen = new Set<string>();
  for (const section of spec.sections) {
    for (const prop of section.properties) {
      if (seen.has(prop.name)) {
        diagnostics.push(
          diagError(
            DiagnosticIds.CTX_DUPLICATE_PROPERTY,
            `duplicate property name '${prop.name}'`,
          ),
        );
        continue;
      }
      seen.add(prop.name);
      if (TYPE_MAP[prop.type] === undefined) {
        diagnostics.push(
          diagError(
            DiagnosticIds.CTX_INVALID_TYPE,
            `property '${prop.name}' has unsupported type '${prop.type}'`,
          ),
        );
      }
    }
  }

  // Resolve `extends` clause (if any) to a TS package + interface name.
  let resolvedExtends: ResolvedExtends | undefined;
  if (
    spec.extends !== undefined &&
    spec.extends !== null &&
    spec.extends.length > 0
  ) {
    const resolved = resolveExtendsFqn(spec.extends);
    if (resolved === undefined) {
      const msg =
        `cannot resolve extends FQN '${spec.extends}' — only ` +
        `'D2.Shared.<Pkg>.<Iface>' is supported`;
      diagnostics.push(diagError(DiagnosticIds.CTX_EXTENDS_UNRESOLVED, msg));
    } else {
      resolvedExtends = resolved;
    }
  }

  if (diagnostics.length > 0) return { source: "", diagnostics };

  const usedTypes = collectUsedTypes(spec);
  const sb = new StringBuilder();
  const topic = importMode === "package" ? "request-context" : "auth-context";
  sb.appendLine(buildHeader(`contracts/${topic}/${spec.name}.spec.json`));
  sb.appendLine("/* eslint-disable */");
  sb.appendLine();
  if (resolvedExtends !== undefined) {
    const { interfaceName, packageName } = resolvedExtends;
    sb.appendLine(`import type { ${interfaceName} } from "${packageName}";`);
  }
  emitEnumImports(sb, usedTypes, importMode);
  if (resolvedExtends !== undefined || usedTypes.size > 0) sb.appendLine();
  emitInterface(sb, spec, resolvedExtends);
  sb.appendLine();
  emitRedactPaths(sb, spec);
  sb.appendLine();

  return { source: sb.toString(), diagnostics: [] };
}

/**
 * Result of resolving a spec's `extends` FQN to a TS-side import target.
 * The resolver only supports the `D2.Shared.<Pkg>.<Iface>` shape — anything
 * else surfaces a `CTX_EXTENDS_UNRESOLVED` diagnostic.
 */
interface ResolvedExtends {
  /** Bare interface name (e.g. `IAuthContext`). */
  readonly interfaceName: string;
  /** TS package import path (e.g. `@d2/auth-context-abstractions`). */
  readonly packageName: string;
}

/**
 * Resolve a .NET fully-qualified interface name to a TS import target.
 *
 * Mapping rule (mirrors the .NET-side
 * `D2.Shared.<Pkg>.<Iface>` namespace convention):
 * - Strip the leading `D2.Shared.` prefix.
 * - Take the dot-separated segments BEFORE the final `.` as the package
 *   path; convert each PascalCase segment to lowercase, joined by `-`.
 *   (e.g. `AuthContext.Abstractions` → `auth-context-abstractions`).
 * - The final segment is the bare interface name, unchanged.
 * - The package name is `@d2/<kebab>`.
 *
 * Returns `undefined` when the FQN does NOT match the supported shape so
 * the caller can surface a `CTX_EXTENDS_UNRESOLVED` diagnostic.
 */
function resolveExtendsFqn(fqn: string): ResolvedExtends | undefined {
  const PREFIX = "D2.Shared.";
  if (!fqn.startsWith(PREFIX)) return undefined;
  const rest = fqn.slice(PREFIX.length);
  const segments = rest.split(".");
  if (segments.length < 2) return undefined;
  const interfaceName = segments[segments.length - 1]!;
  if (interfaceName.length === 0) return undefined;
  const pkgSegments = segments.slice(0, -1);
  const kebab = pkgSegments.map(pascalToKebab).join("-");
  if (kebab.length === 0) return undefined;
  return { interfaceName, packageName: `@d2/${kebab}` };
}

/**
 * Convert a PascalCase / camelCase segment to kebab-case (e.g. `AuthContext`
 * → `auth-context`). Inserts a hyphen before each uppercase letter that
 * follows a lowercase letter or digit; the result is lowercased.
 */
function pascalToKebab(segment: string): string {
  if (segment.length === 0) return segment;
  const withHyphens = segment.replace(/([a-z0-9])([A-Z])/g, "$1-$2");
  return withHyphens.toLowerCase();
}

/** Where to import enum/type definitions from. */
export type ImportMode = "relative" | "package";

function collectUsedTypes(spec: ContextSpec): Set<string> {
  const used = new Set<string>();
  for (const section of spec.sections) {
    for (const prop of section.properties) {
      const t = TYPE_MAP[prop.type] ?? "";
      if (t.includes("OrgType")) used.add("OrgType");
      if (t.includes("Role")) used.add("Role");
      if (t.includes("ImpersonationKind")) used.add("ImpersonationKind");
      if (t.includes("ActorKind")) used.add("ActorKind");
      if (t.includes("ActorEntry")) used.add("ActorEntry");
    }
  }
  return used;
}

function emitEnumImports(
  sb: StringBuilder,
  used: Set<string>,
  importMode: "relative" | "package",
): void {
  if (importMode === "package") {
    if (used.size === 0) return;
    const list = [...used].sort().join(", ");
    sb.appendLine(
      `import type { ${list} } from "@d2/auth-context-abstractions";`,
    );
    sb.appendLine(`export type { ${list} };`);
    return;
  }
  if (used.has("OrgType"))
    sb.appendLine('import type { OrgType } from "./enums/org-type.g.js";');
  if (used.has("Role"))
    sb.appendLine('import type { Role } from "./enums/role.g.js";');
  if (used.has("ImpersonationKind"))
    sb.appendLine(
      'import type { ImpersonationKind } from "./enums/impersonation-kind.g.js";',
    );
  if (used.has("ActorKind"))
    sb.appendLine('import type { ActorKind } from "./enums/actor-kind.g.js";');
  if (used.has("ActorEntry"))
    sb.appendLine(
      'import type { ActorEntry } from "./types/actor-entry.g.js";',
    );
  if (used.size > 0) {
    const list = [...used].sort().join(", ");
    sb.appendLine(`export type { ${list} };`);
  }
}

function emitInterface(
  sb: StringBuilder,
  spec: ContextSpec,
  resolvedExtends: ResolvedExtends | undefined,
): void {
  if (spec.description !== undefined) {
    sb.appendLine("/**");
    for (const line of spec.description.split("\n"))
      sb.appendLine(` * ${line}`);
    sb.appendLine(" */");
  }
  const extendsClause =
    resolvedExtends === undefined
      ? ""
      : ` extends ${resolvedExtends.interfaceName}`;
  sb.appendLine(`export interface ${spec.name}${extendsClause} {`);
  sb.increaseIndent();
  for (const section of spec.sections) {
    sb.appendLine(`// --- ${section.name} ---`);
    for (const prop of section.properties) {
      if (prop.doc !== undefined) {
        sb.appendLine("/**");
        for (const line of prop.doc.split("\n")) sb.appendLine(` * ${line}`);
        sb.appendLine(" */");
      }
      const tsType = TYPE_MAP[prop.type] ?? "unknown";
      // Nullable spec types emit as optional properties (`readonly field?: T`)
      // which is equivalent to `T | undefined`. This matches C#'s `T?` idiom
      // and the codebase convention that `undefined` is the only absent sentinel.
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
}

function emitRedactPaths(sb: StringBuilder, spec: ContextSpec): void {
  const paths: string[] = [];
  for (const section of spec.sections) {
    for (const prop of section.properties) {
      if (prop.redact === true) paths.push(camelCase(prop.name));
    }
  }
  paths.sort();
  sb.appendLine("/**");
  sb.appendLine(
    ` * PII-bearing paths on ${spec.name}. Consumed by setupLogger() to`,
  );
  sb.appendLine(" * feed Pino's `redact: { paths }` config.");
  sb.appendLine(" */");
  sb.appendLine(`export const ${spec.name}RedactPaths: readonly string[] = [`);
  sb.increaseIndent();
  for (const p of paths) sb.appendLine(`"${p}",`);
  sb.decreaseIndent();
  sb.appendLine("];");
}

function camelCase(pascal: string): string {
  if (pascal.length === 0) return pascal;
  return pascal[0]!.toLowerCase() + pascal.slice(1);
}

const ENUMS = {
  "org-type": [
    ["Admin", "Administrative organization."],
    ["Support", "Support organization."],
    ["Customer", "Customer organization."],
    ["ThirdParty", "Third-party organization."],
    ["Affiliate", "Affiliate organization."],
  ],
  role: [
    ["Auditor", "Auditor — read-heavy compliance role."],
    ["Agent", "Agent — standard operational role."],
    ["Officer", "Officer — org-management role."],
    ["Owner", "Owner — full control."],
  ],
  "impersonation-kind": [
    ["Consent", "Consent-based impersonation (OTP-authorized)."],
    ["Force", "Force impersonation (admin-only, silent)."],
  ],
  "actor-kind": [
    ["Service", "Service identity (RFC 6749 §4.4)."],
    [
      "Impersonation",
      "User impersonation (Consent or Force per ImpersonationKind).",
    ],
  ],
} as const;

function emitEnumFile(
  name: string,
  members: readonly (readonly string[])[],
): string {
  const sb = new StringBuilder();
  sb.appendLine(buildHeader(`contracts/auth-context/IAuthContext.spec.json`));
  sb.appendLine("/* eslint-disable */");
  sb.appendLine();
  sb.appendLine(`export const ${name} = {`);
  sb.increaseIndent();
  for (const [k] of members) sb.appendLine(`${k}: "${k}",`);
  sb.decreaseIndent();
  sb.appendLine("} as const;");
  sb.appendLine();
  sb.appendLine(
    `export type ${name} = (typeof ${name})[keyof typeof ${name}];`,
  );
  sb.appendLine();
  // JSDoc emit per-member.
  for (const [k, doc] of members) {
    sb.appendLine(`/** ${doc} */`);
    sb.appendLine(`export const ${name}_${k}: ${name} = ${name}.${k};`);
  }
  sb.appendLine();
  return sb.toString();
}

function emitActorEntryFile(): string {
  const sb = new StringBuilder();
  sb.appendLine(buildHeader(`contracts/auth-context/IAuthContext.spec.json`));
  sb.appendLine("/* eslint-disable */");
  sb.appendLine();
  sb.appendLine('import type { ActorKind } from "../enums/actor-kind.g.js";');
  sb.appendLine(
    'import type { ImpersonationKind } from "../enums/impersonation-kind.g.js";',
  );
  sb.appendLine('import type { OrgType } from "../enums/org-type.g.js";');
  sb.appendLine('import type { Role } from "../enums/role.g.js";');
  sb.appendLine();
  sb.appendLine("/**");
  sb.appendLine(
    " * One link in the RFC 8693 actor chain. Mirrors .NET ActorEntry record.",
  );
  sb.appendLine(
    " * Per rules.md §6.15 (TS `undefined`-over-`null`): optional fields use",
  );
  sb.appendLine(
    " * the `?:` shorthand; absent links arrive as `undefined`, never `null`.",
  );
  sb.appendLine(" */");
  sb.appendLine("export interface ActorEntry {");
  sb.increaseIndent();
  sb.appendLine("readonly kind: ActorKind;");
  sb.appendLine("readonly subject: string;");
  sb.appendLine("readonly clientId?: string;");
  sb.appendLine("readonly impersonationKind?: ImpersonationKind;");
  sb.appendLine("readonly sessionId?: string;");
  sb.appendLine("readonly orgId?: string;");
  sb.appendLine("readonly orgName?: string;");
  sb.appendLine("readonly orgType?: OrgType;");
  sb.appendLine("readonly orgRole?: Role;");
  sb.appendLine("readonly act?: ActorEntry;");
  sb.decreaseIndent();
  sb.appendLine("}");
  sb.appendLine();
  return sb.toString();
}

const SPEC_PATH = contractsPath("auth-context", "IAuthContext.spec.json");
const TARGET_DIR = tsPackagePath("auth-context-abstractions", "src");
const INTERFACE_TARGET = `${TARGET_DIR}/IAuthContext.g.ts`;
const ENUM_TARGETS: Record<string, string> = {
  "org-type": `${TARGET_DIR}/enums/org-type.g.ts`,
  role: `${TARGET_DIR}/enums/role.g.ts`,
  "impersonation-kind": `${TARGET_DIR}/enums/impersonation-kind.g.ts`,
  "actor-kind": `${TARGET_DIR}/enums/actor-kind.g.ts`,
};
const ACTOR_ENTRY_TARGET = `${TARGET_DIR}/types/actor-entry.g.ts`;

const ENUM_TYPE_NAMES: Record<string, string> = {
  "org-type": "OrgType",
  role: "Role",
  "impersonation-kind": "ImpersonationKind",
  "actor-kind": "ActorKind",
};

/**
 * Run the auth-context emitter. Writes 6 .g.ts files and returns the
 * aggregated diagnostics (empty on success). Per-spec mtime check:
 * skips emit when every output is newer than the spec, so unchanged
 * builds pay only a stat-call cost. Pass `force=true` to bypass.
 */
export function runAuthContextEmit(force = false): readonly EmitDiagnostic[] {
  const allOutputs = [
    INTERFACE_TARGET,
    ACTOR_ENTRY_TARGET,
    ...Object.values(ENUM_TARGETS),
  ];
  if (!force) {
    const allUpToDate = allOutputs.every((p) =>
      isOutputUpToDate(p, [SPEC_PATH]),
    );
    if (allUpToDate) return [];
  }

  const loadResult = loadSpec<ContextSpec>(
    SPEC_PATH,
    DiagnosticIds.CTX_MALFORMED_SPEC,
  );
  if (loadResult.spec === undefined) return loadResult.diagnostics;

  const result = emitAuthContext(loadResult.spec);
  if (result.diagnostics.length > 0) return result.diagnostics;

  writeGeneratedFile(INTERFACE_TARGET, result.source);
  writeGeneratedFile(ACTOR_ENTRY_TARGET, emitActorEntryFile());
  for (const [key, members] of Object.entries(ENUMS)) {
    writeGeneratedFile(
      ENUM_TARGETS[key]!,
      emitEnumFile(ENUM_TYPE_NAMES[key]!, members),
    );
  }
  return [];
}

// CLI entry — invoked by `pnpm codegen:auth-context`.
const isMain =
  import.meta.url === `file://${process.argv[1]}` ||
  process.argv[1]?.endsWith("auth-context-emit.ts") === true;
if (isMain) {
  const force = process.argv.includes("--force");
  const diagnostics = runAuthContextEmit(force);
  for (const d of diagnostics) console.error(formatDiagnostic(d));
  if (diagnostics.some((d) => d.severity === "error")) process.exit(1);
}
