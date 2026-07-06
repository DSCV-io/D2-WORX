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

const _NAME_PATTERN = /^[A-Z][A-Z0-9_]*$/;

/**
 * Spec shape — one entry per protocol audience (d2.internal, d2-edge). These are
 * the PROTOCOL audiences (bare-token aud values), distinct from the URL-shaped
 * token-exchange targets in contracts/auth-audiences/audiences.spec.json.
 */
export interface ProtocolAudiencesSpec {
  readonly protocolAudiences: readonly ProtocolAudienceEntry[];
}

export interface ProtocolAudienceEntry {
  readonly name: string;
  readonly value: string;
  readonly description?: string;
}

/**
 * Pure emit logic for the TS `ProtocolAudiences` const-object + the
 * `ALL_PROTOCOL_AUDIENCES` value array. Surfaces duplicate name / invalid name /
 * duplicate value / empty value via diagnostics; all-or-nothing emit.
 */
export function emitProtocolAudiences(spec: ProtocolAudiencesSpec): EmitResult {
  const diagnostics: EmitDiagnostic[] = [];
  const seenNames = new Set<string>();
  const seenValues = new Map<string, string>();
  for (const a of spec.protocolAudiences) {
    if (seenNames.has(a.name)) {
      diagnostics.push(
        diagError(
          DiagnosticIds.PAUD_DUPLICATE_NAME,
          `duplicate protocol-audience name '${a.name}'`,
        ),
      );
      continue;
    }
    seenNames.add(a.name);
    if (!_NAME_PATTERN.test(a.name)) {
      diagnostics.push(
        diagError(
          DiagnosticIds.PAUD_INVALID_NAME,
          `protocol-audience name '${a.name}' is not SCREAMING_SNAKE_CASE`,
        ),
      );
    }
    if (a.value.length === 0) {
      diagnostics.push(
        diagError(
          DiagnosticIds.PAUD_EMPTY_VALUE,
          `protocol-audience '${a.name}' has an empty value`,
        ),
      );
    } else if (seenValues.has(a.value)) {
      diagnostics.push(
        diagError(
          DiagnosticIds.PAUD_DUPLICATE_VALUE,
          `protocol-audiences '${seenValues.get(a.value)!}' and '${a.name}' both map to value '${a.value}'`,
        ),
      );
    } else {
      seenValues.set(a.value, a.name);
    }
  }
  if (diagnostics.length > 0) return { source: "", diagnostics };

  // Sort by name so emit order is deterministic regardless of input order.
  const sorted = [...spec.protocolAudiences].sort((a, b) =>
    a.name.localeCompare(b.name),
  );

  const sb = new StringBuilder();
  sb.appendLine(
    buildHeader(
      "contracts/auth-protocol-audiences/protocol-audiences.spec.json",
    ),
  );
  sb.appendLine("/* eslint-disable */");
  sb.appendLine();
  emitConstObject(sb, sorted);
  sb.appendLine();
  emitAllArray(sb, sorted);
  sb.appendLine();
  return { source: sb.toString(), diagnostics: [] };
}

function emitConstObject(
  sb: StringBuilder,
  entries: readonly ProtocolAudienceEntry[],
): void {
  sb.appendLine("/**");
  sb.appendLine(" * Protocol-audience constants emitted from");
  sb.appendLine(
    " * contracts/auth-protocol-audiences/protocol-audiences.spec.json. These are the",
  );
  sb.appendLine(
    " * bare-token aud values (d2.internal, d2-edge) — NOT the URL-shaped",
  );
  sb.appendLine(
    " * token-exchange targets in @d2/auth-abstractions' Audiences catalog. Mirrors",
  );
  sb.appendLine(
    " * the .NET D2.Shared.Auth.Abstractions.WellKnownAudiences constants.",
  );
  sb.appendLine(" */");
  sb.appendLine("export const ProtocolAudiences = {");
  sb.increaseIndent();
  for (const a of entries) {
    if (a.description !== undefined && a.description.length > 0)
      sb.appendLine(`/** ${a.description} */`);
    sb.appendLine(`${a.name}: "${a.value}",`);
  }
  sb.decreaseIndent();
  sb.appendLine("} as const;");
  sb.appendLine();
  sb.appendLine(
    "export type ProtocolAudience = (typeof ProtocolAudiences)[keyof typeof ProtocolAudiences];",
  );
}

function emitAllArray(
  sb: StringBuilder,
  entries: readonly ProtocolAudienceEntry[],
): void {
  sb.appendLine("/**");
  sb.appendLine(
    " * All declared protocol-audience VALUES in spec-name-sorted order.",
  );
  sb.appendLine(" */");
  sb.appendLine("export const ALL_PROTOCOL_AUDIENCES: readonly string[] = [");
  sb.increaseIndent();
  for (const a of entries) sb.appendLine(`"${a.value}",`);
  sb.decreaseIndent();
  sb.appendLine("];");
}

// ---------------------------------------------------------------------------
// CLI-runner section — mtime-check, disk-write, isMain guard.
// Excluded from unit-test coverage (requires process/fs mocking to exercise);
// the exported library function above (emitProtocolAudiences) IS fully
// unit-tested in protocol-audiences-emit.test.ts.
// ---------------------------------------------------------------------------

/* v8 ignore start */
const SPEC_PATH = contractsPath(
  "auth-protocol-audiences",
  "protocol-audiences.spec.json",
);
const TARGET = tsPackagePath(
  "auth",
  "abstractions",
  "src",
  "protocol-audiences.g.ts",
);

export function runProtocolAudiencesEmit(
  force = false,
): readonly EmitDiagnostic[] {
  if (!force && isOutputUpToDate(TARGET, [SPEC_PATH])) return [];
  const loadResult = loadSpec<ProtocolAudiencesSpec>(
    SPEC_PATH,
    DiagnosticIds.PAUD_MALFORMED_SPEC,
  );
  if (loadResult.spec === undefined) return loadResult.diagnostics;
  const result = emitProtocolAudiences(loadResult.spec);
  if (result.diagnostics.length > 0) return result.diagnostics;
  writeGeneratedFile(TARGET, result.source);
  return [];
}

const isMain =
  import.meta.url === `file://${process.argv[1]}` ||
  process.argv[1]?.endsWith("protocol-audiences-emit.ts") === true;
if (isMain) {
  const force = process.argv.includes("--force");
  const diagnostics = runProtocolAudiencesEmit(force);
  for (const d of diagnostics) console.error(formatDiagnostic(d));
  if (diagnostics.some((d) => d.severity === "error")) process.exit(1);
}
/* v8 ignore stop */
