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

/**
 * One trailer-key entry parsed from
 * `contracts/grpc-trailers/grpc-trailers.spec.json`.
 */
export interface GrpcTrailerEntry {
  readonly constName: string;
  readonly value: string;
  readonly doc: string;
}

/** Top-level shape of `grpc-trailers.spec.json`. */
export interface GrpcTrailersSpec {
  readonly trailers: readonly GrpcTrailerEntry[];
}

/** Result of validating the spec — surfaces drift / duplicate / shape errors. */
export interface ValidatedGrpcTrailers {
  readonly trailers: readonly GrpcTrailerEntry[];
  readonly diagnostics: readonly EmitDiagnostic[];
}

const CONST_NAME_RE = /^[A-Z][A-Z0-9_]*$/;

/**
 * Validate the spec — surface invalid-constName, duplicate constName,
 * duplicate wire value, empty value. Mirrors the .NET emitter's predicate
 * set (same spec → same predicate surface byte-for-byte).
 */
export function validateGrpcTrailersSpec(
  spec: GrpcTrailersSpec,
): ValidatedGrpcTrailers {
  const diagnostics: EmitDiagnostic[] = [];
  const valid: GrpcTrailerEntry[] = [];
  const seenConstNames = new Set<string>();
  const seenValues = new Set<string>();

  for (const entry of spec.trailers) {
    if (!CONST_NAME_RE.test(entry.constName)) {
      diagnostics.push(
        diagError(
          DiagnosticIds.GT_INVALID_CONST_NAME,
          `trailer has invalid constName '${entry.constName}' — ` +
            `must match ${CONST_NAME_RE.source}`,
        ),
      );
      continue;
    }
    if (entry.value === undefined || entry.value.trim().length === 0) {
      diagnostics.push(
        diagError(
          DiagnosticIds.GT_EMPTY_VALUE,
          `trailer '${entry.constName}' has empty or whitespace-only wire value`,
        ),
      );
      continue;
    }
    if (seenConstNames.has(entry.constName)) {
      diagnostics.push(
        diagError(
          DiagnosticIds.GT_DUPLICATE_CONST_NAME,
          `trailer constName '${entry.constName}' is declared more than once`,
        ),
      );
      continue;
    }
    if (seenValues.has(entry.value)) {
      diagnostics.push(
        diagError(
          DiagnosticIds.GT_DUPLICATE_VALUE,
          `trailer wire value '${entry.value}' is declared more than once`,
        ),
      );
      continue;
    }
    seenConstNames.add(entry.constName);
    seenValues.add(entry.value);
    valid.push(entry);
  }

  return { trailers: valid, diagnostics };
}

/**
 * Emit the gRPC trailers `.g.ts` source. Stateless and unit-testable.
 * Preserves spec order so spec edits map to predictable diffs in the
 * emitted file.
 */
export function emitGrpcTrailers(spec: GrpcTrailersSpec): EmitResult {
  const v = validateGrpcTrailersSpec(spec);
  const errors = v.diagnostics.filter((d) => d.severity === "error");
  if (errors.length > 0) return { source: "", diagnostics: v.diagnostics };

  const sb = new StringBuilder();
  sb.appendLine(buildHeader("contracts/grpc-trailers/grpc-trailers.spec.json"));
  sb.appendLine("/* eslint-disable */");
  sb.appendLine();
  sb.appendLine("/**");
  sb.appendLine(" * Spec-derived gRPC trailer-key constants. Mirrors .NET");
  sb.appendLine(
    " * D2.Shared.Auth.Grpc.Status.D2GrpcTrailers (same wire values).",
  );
  sb.appendLine(" *");
  sb.appendLine(
    " * Cross-language parity: the SAME spec drives the .NET-side catalog",
  );
  sb.appendLine(
    " * via D2.Shared.Grpc.Trailers.SourceGen. Both sides emit identical",
  );
  sb.appendLine(
    " * trailer keys byte-for-byte; cross-language wire drift is impossible.",
  );
  sb.appendLine(" */");
  sb.appendLine("export const D2GrpcTrailers = {");
  sb.increaseIndent();
  for (const e of v.trailers) {
    sb.appendLine("/**");
    for (const line of e.doc.split("\n"))
      sb.appendLine(` * ${escapeJsDoc(line)}`);
    sb.appendLine(" */");
    sb.appendLine(`${e.constName}: "${escapeStringLiteral(e.value)}",`);
  }
  sb.decreaseIndent();
  sb.appendLine("} as const;");
  sb.appendLine();
  sb.appendLine("export type D2GrpcTrailer =");
  sb.increaseIndent();
  sb.appendLine("(typeof D2GrpcTrailers)[keyof typeof D2GrpcTrailers];");
  sb.decreaseIndent();
  sb.appendLine();
  sb.appendLine("export const ALL_D2_GRPC_TRAILERS: readonly string[] = [");
  sb.increaseIndent();
  for (const e of v.trailers)
    sb.appendLine(`"${escapeStringLiteral(e.value)}",`);
  sb.decreaseIndent();
  sb.appendLine("];");
  sb.appendLine();

  return { source: sb.toString(), diagnostics: v.diagnostics };
}

function escapeStringLiteral(value: string): string {
  return value.replace(/\\/g, "\\\\").replace(/"/g, '\\"');
}

function escapeJsDoc(value: string): string {
  return value.replace(/\*\//g, "*\\/");
}

const SPEC_PATH = contractsPath("grpc-trailers", "grpc-trailers.spec.json");
const TARGET_PATH = tsPackagePath("grpc-client", "src", "grpc-trailers.g.ts");

/**
 * Run the gRPC trailers emitter. Per-spec mtime check skips emit when the
 * output is newer than the spec; pass `force=true` to bypass.
 */
export function runGrpcTrailersEmit(force = false): readonly EmitDiagnostic[] {
  if (!force && isOutputUpToDate(TARGET_PATH, [SPEC_PATH])) return [];
  const loadResult = loadSpec<GrpcTrailersSpec>(
    SPEC_PATH,
    DiagnosticIds.GT_MALFORMED_SPEC,
  );
  if (loadResult.spec === undefined) return loadResult.diagnostics;

  const result = emitGrpcTrailers(loadResult.spec);
  if (result.diagnostics.some((d) => d.severity === "error"))
    return result.diagnostics;

  writeGeneratedFile(TARGET_PATH, result.source);
  return result.diagnostics;
}

const isMain =
  import.meta.url === `file://${process.argv[1]}` ||
  process.argv[1]?.endsWith("grpc-trailers-emit.ts") === true;
if (isMain) {
  const force = process.argv.includes("--force");
  const diagnostics = runGrpcTrailersEmit(force);
  for (const d of diagnostics) console.error(formatDiagnostic(d));
  if (diagnostics.some((d) => d.severity === "error")) process.exit(1);
}
