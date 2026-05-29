// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
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

const VALID_SENSITIVITIES = new Set(["Routine", "Sensitive", "Critical"]);

/**
 * Spec shape — one entry per scope (`auth.user.impersonate.consent` etc.).
 */
export interface ScopesSpec {
  readonly scopes: readonly ScopeEntry[];
}

export interface ScopeEntry {
  readonly name: string;
  readonly description?: string;
  readonly actionSensitivity: string;
  readonly impersonationBlocked: boolean;
  readonly grantedTo?: Readonly<Record<string, readonly string[]>>;
}

/**
 * Pure emit logic. Surfaces duplicates / invalid sensitivity / invalid
 * name-shape via diagnostics; all-or-nothing emit.
 */
export function emitAuthScopes(spec: ScopesSpec): EmitResult {
  const diagnostics: EmitDiagnostic[] = [];
  const seen = new Set<string>();
  for (const s of spec.scopes) {
    if (seen.has(s.name)) {
      diagnostics.push(
        diagError(DiagnosticIds.SCP_DUPLICATE, `duplicate scope '${s.name}'`),
      );
      continue;
    }
    seen.add(s.name);
    if (!/^[a-z][a-z0-9_]*(\.[a-z0-9_]+)+$/.test(s.name)) {
      diagnostics.push(
        diagError(
          DiagnosticIds.SCP_INVALID_NAME,
          `scope '${s.name}' is not dot-segmented lowercase`,
        ),
      );
    }
    if (!VALID_SENSITIVITIES.has(s.actionSensitivity)) {
      diagnostics.push(
        diagError(
          DiagnosticIds.SCP_INVALID_SENSITIVITY,
          `scope '${s.name}' has invalid actionSensitivity '${s.actionSensitivity}'`,
        ),
      );
    }
  }
  if (diagnostics.length > 0) return { source: "", diagnostics };

  // Sort by name so emit order is deterministic regardless of input order.
  const sorted = [...spec.scopes].sort((a, b) => a.name.localeCompare(b.name));

  const sb = new StringBuilder();
  sb.appendLine(buildHeader("contracts/auth-scopes/scopes.spec.json"));
  sb.appendLine("/* eslint-disable */");
  sb.appendLine();
  emitTree(sb, sorted);
  sb.appendLine();
  emitAllScopesArray(sb, sorted);
  sb.appendLine();
  return { source: sb.toString(), diagnostics: [] };
}

interface TreeNode {
  readonly children: Map<string, TreeNode>;
  fullName?: string;
}

function buildTree(scopes: readonly ScopeEntry[]): TreeNode {
  const root: TreeNode = { children: new Map() };
  for (const s of scopes) {
    const segments = s.name.split(".");
    let cur = root;
    for (const seg of segments) {
      let next = cur.children.get(seg);
      if (next === undefined) {
        next = { children: new Map() };
        cur.children.set(seg, next);
      }
      cur = next;
    }
    cur.fullName = s.name;
  }
  return root;
}

function emitTree(sb: StringBuilder, scopes: readonly ScopeEntry[]): void {
  const root = buildTree(scopes);
  sb.appendLine("/**");
  sb.appendLine(
    " * OAuth scope catalog emitted from contracts/auth-scopes/scopes.spec.json.",
  );
  sb.appendLine(
    " * Tree-emitted nested constants — `Scopes.auth.user.impersonate.consent` resolves",
  );
  sb.appendLine(" * to the dot-segmented spec name.");
  sb.appendLine(" */");
  sb.appendLine("export const Scopes = {");
  sb.increaseIndent();
  emitNodeChildren(sb, root);
  sb.decreaseIndent();
  sb.appendLine("} as const;");
}

function emitNodeChildren(sb: StringBuilder, node: TreeNode): void {
  const keys = [...node.children.keys()].sort();
  for (const k of keys) {
    const child = node.children.get(k)!;
    if (child.fullName !== undefined && child.children.size === 0) {
      sb.appendLine(`${k}: "${child.fullName}",`);
    } else {
      sb.appendLine(`${k}: {`);
      sb.increaseIndent();
      if (child.fullName !== undefined)
        sb.appendLine(`_self: "${child.fullName}",`);
      emitNodeChildren(sb, child);
      sb.decreaseIndent();
      sb.appendLine("},");
    }
  }
}

function emitAllScopesArray(
  sb: StringBuilder,
  scopes: readonly ScopeEntry[],
): void {
  sb.appendLine("/**");
  sb.appendLine(" * All declared scopes in spec-sorted order.");
  sb.appendLine(" */");
  sb.appendLine("export const ALL_SCOPES: readonly string[] = [");
  sb.increaseIndent();
  for (const s of scopes) sb.appendLine(`"${s.name}",`);
  sb.decreaseIndent();
  sb.appendLine("];");
}

const SPEC_PATH = contractsPath("auth-scopes", "scopes.spec.json");
const TARGET = tsPackagePath("auth", "abstractions", "src", "scopes.g.ts");

export function runAuthScopesEmit(force = false): readonly EmitDiagnostic[] {
  if (!force && isOutputUpToDate(TARGET, [SPEC_PATH])) return [];
  const loadResult = loadSpec<ScopesSpec>(
    SPEC_PATH,
    DiagnosticIds.SCP_MALFORMED_SPEC,
  );
  if (loadResult.spec === undefined) return loadResult.diagnostics;
  const result = emitAuthScopes(loadResult.spec);
  if (result.diagnostics.length > 0) return result.diagnostics;
  writeGeneratedFile(TARGET, result.source);
  return [];
}

const isMain =
  import.meta.url === `file://${process.argv[1]}` ||
  process.argv[1]?.endsWith("auth-scopes-emit.ts") === true;
if (isMain) {
  const force = process.argv.includes("--force");
  const diagnostics = runAuthScopesEmit(force);
  for (const d of diagnostics) console.error(formatDiagnostic(d));
  if (diagnostics.some((d) => d.severity === "error")) process.exit(1);
}
