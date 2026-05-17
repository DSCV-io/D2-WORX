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

/** One extension-key entry parsed from the spec. */
export interface ExtensionKeyEntry {
  readonly constName: string;
  readonly value: string;
  readonly doc: string;
}

/** One title entry parsed from the spec. */
export interface TitleEntry {
  readonly constName: string;
  readonly httpStatus: number | null;
  readonly value: string;
  readonly doc: string;
}

/** Top-level shape of `problem-details.spec.json`. */
export interface ProblemDetailsSpec {
  readonly typeUriPrefix: string;
  readonly contentType: string;
  readonly extensionKeys: readonly ExtensionKeyEntry[];
  readonly titles: readonly TitleEntry[];
}

const CONST_NAME_RE = /^[A-Z][A-Z0-9_]*$/;

/** Result of validating the spec. */
export interface ValidatedProblemDetails {
  readonly typeUriPrefix: string;
  readonly contentType: string;
  readonly extensionKeys: readonly ExtensionKeyEntry[];
  readonly titles: readonly TitleEntry[];
  readonly diagnostics: readonly EmitDiagnostic[];
}

/**
 * Validate the spec — surface trailing-slash violation on the URI prefix,
 * duplicate constNames / wire values across extension keys, and duplicate
 * constNames / httpStatuses across titles. Mirrors the .NET emitter's
 * predicate set (same spec → same predicate surface byte-for-byte).
 */
export function validateProblemDetailsSpec(
  spec: ProblemDetailsSpec,
): ValidatedProblemDetails {
  const diagnostics: EmitDiagnostic[] = [];

  if (!spec.typeUriPrefix.endsWith("/")) {
    diagnostics.push(
      diagError(
        DiagnosticIds.PRB_TYPE_URI_PREFIX_MISSING_TRAILING_SLASH,
        `typeUriPrefix '${spec.typeUriPrefix}' must end with a trailing slash; ` +
          `the runtime appends the kebab-cased error code directly`,
      ),
    );
  }

  const validExtensions: ExtensionKeyEntry[] = [];
  const seenExtConstNames = new Set<string>();
  const seenExtValues = new Set<string>();
  for (const entry of spec.extensionKeys) {
    if (!CONST_NAME_RE.test(entry.constName)) {
      diagnostics.push(
        diagError(
          DiagnosticIds.PRB_MALFORMED_SPEC,
          `extension key has invalid constName '${entry.constName}' — ` +
            `must match ${CONST_NAME_RE.source}`,
        ),
      );
      continue;
    }
    if (seenExtConstNames.has(entry.constName)) {
      diagnostics.push(
        diagError(
          DiagnosticIds.PRB_DUPLICATE_EXTENSION_KEY_CONST_NAME,
          `extension key constName '${entry.constName}' is declared more than once`,
        ),
      );
      continue;
    }
    if (seenExtValues.has(entry.value)) {
      diagnostics.push(
        diagError(
          DiagnosticIds.PRB_DUPLICATE_EXTENSION_KEY_VALUE,
          `extension key wire value '${entry.value}' is declared more than once`,
        ),
      );
      continue;
    }
    seenExtConstNames.add(entry.constName);
    seenExtValues.add(entry.value);
    validExtensions.push(entry);
  }

  const validTitles: TitleEntry[] = [];
  const seenTitleConstNames = new Set<string>();
  const seenTitleStatuses = new Set<string>();
  for (const entry of spec.titles) {
    if (!CONST_NAME_RE.test(entry.constName)) {
      diagnostics.push(
        diagError(
          DiagnosticIds.PRB_MALFORMED_SPEC,
          `title has invalid constName '${entry.constName}' — ` +
            `must match ${CONST_NAME_RE.source}`,
        ),
      );
      continue;
    }
    if (seenTitleConstNames.has(entry.constName)) {
      diagnostics.push(
        diagError(
          DiagnosticIds.PRB_DUPLICATE_TITLE_CONST_NAME,
          `title constName '${entry.constName}' is declared more than once`,
        ),
      );
      continue;
    }
    const statusKey =
      entry.httpStatus === null ? "null" : `${entry.httpStatus}`;
    if (seenTitleStatuses.has(statusKey)) {
      diagnostics.push(
        diagError(
          DiagnosticIds.PRB_DUPLICATE_TITLE_HTTP_STATUS,
          `title httpStatus '${statusKey}' is declared more than once ` +
            `(only one entry may map to each HTTP status; null is the singular fallback)`,
        ),
      );
      continue;
    }
    seenTitleConstNames.add(entry.constName);
    seenTitleStatuses.add(statusKey);
    validTitles.push(entry);
  }

  return {
    typeUriPrefix: spec.typeUriPrefix,
    contentType: spec.contentType,
    extensionKeys: validExtensions,
    titles: validTitles,
    diagnostics,
  };
}

/**
 * Emit the `problem-details.g.ts` source. Stateless and unit-testable.
 * Preserves spec order so spec edits map to predictable diffs in the
 * emitted file (mirrors the .NET emitter's preserve-order discipline).
 */
export function emitProblemDetails(spec: ProblemDetailsSpec): EmitResult {
  const v = validateProblemDetailsSpec(spec);
  const errors = v.diagnostics.filter((d) => d.severity === "error");
  if (errors.length > 0) return { source: "", diagnostics: v.diagnostics };

  const sb = new StringBuilder();
  sb.appendLine(
    buildHeader("contracts/problem-details/problem-details.spec.json"),
  );
  sb.appendLine("/* eslint-disable */");
  sb.appendLine();
  sb.appendLine("/**");
  sb.appendLine(
    " * D2 RFC 7807 ProblemDetails wire-format catalog. Mirrors the .NET",
  );
  sb.appendLine(
    " * D2.Shared.ProblemDetails.D2ProblemDetailsKeys constants byte-for-byte",
  );
  sb.appendLine(
    " * (single spec source emits both sides; cross-language drift is",
  );
  sb.appendLine(" * structurally impossible).");
  sb.appendLine(" */");

  sb.appendLine();
  sb.appendLine("/**");
  sb.appendLine(
    " * Base URI prefix for the RFC 7807 ProblemDetails 'type' field.",
  );
  sb.appendLine(
    " * Runtime concatenates the kebab-cased error code directly onto this prefix.",
  );
  sb.appendLine(" */");
  sb.appendLine(
    `export const PROBLEM_TYPE_URI_PREFIX = ` +
      `"${escapeStringLiteral(v.typeUriPrefix)}";`,
  );
  sb.appendLine();

  sb.appendLine("/**");
  sb.appendLine(
    " * MIME type per RFC 7807 §6.1 for JSON ProblemDetails bodies. Single source",
  );
  sb.appendLine(
    " * of truth so .NET middleware AND TS BFF rejection responses write",
  );
  sb.appendLine(" * byte-identical Content-Type headers.");
  sb.appendLine(" */");
  sb.appendLine(
    `export const PROBLEM_DETAILS_CONTENT_TYPE = ` +
      `"${escapeStringLiteral(v.contentType)}";`,
  );
  sb.appendLine();

  sb.appendLine("/**");
  sb.appendLine(
    " * Extension keys on the ProblemDetails JSON body. These keys are",
  );
  sb.appendLine(
    " * emitted by the .NET D2ProblemDetailsKeys-consuming sites (auth-http +",
  );
  sb.appendLine(
    " * aspnetcore Customizer) on the Edge side; the BFF mirrors them on the",
  );
  sb.appendLine(
    " * rejection envelopes it returns to the browser. Codegen-emitted from",
  );
  sb.appendLine(" * problem-details.spec.json.");
  sb.appendLine(" */");
  sb.appendLine("export const ProblemDetailsExtensionKeys = {");
  sb.increaseIndent();
  for (const e of v.extensionKeys) {
    sb.appendLine("/**");
    for (const line of e.doc.split("\n"))
      sb.appendLine(` * ${escapeJsDoc(line)}`);
    sb.appendLine(" */");
    sb.appendLine(`${e.constName}: "${escapeStringLiteral(e.value)}",`);
  }
  sb.decreaseIndent();
  sb.appendLine("} as const;");
  sb.appendLine();

  sb.appendLine("/**");
  sb.appendLine(
    " * Coarse English titles per HTTP status. Locale-NEUTRAL — the",
  );
  sb.appendLine(
    " * client-side locale-aware messages ride the d2_messages extension.",
  );
  sb.appendLine(" * Codegen-emitted from problem-details.spec.json.");
  sb.appendLine(" */");
  sb.appendLine("export const ProblemDetailsTitles = {");
  sb.increaseIndent();
  for (const e of v.titles) {
    sb.appendLine("/**");
    for (const line of e.doc.split("\n"))
      sb.appendLine(` * ${escapeJsDoc(line)}`);
    sb.appendLine(" */");
    sb.appendLine(`${e.constName}: "${escapeStringLiteral(e.value)}",`);
  }
  sb.decreaseIndent();
  sb.appendLine("} as const;");
  sb.appendLine();

  emitDefaultTitleForStatus(sb, v.titles);

  return { source: sb.toString(), diagnostics: v.diagnostics };
}

function emitDefaultTitleForStatus(
  sb: StringBuilder,
  titles: readonly TitleEntry[],
): void {
  sb.appendLine("/**");
  sb.appendLine(
    " * Returns the spec-declared title for the given HTTP status code,",
  );
  sb.appendLine(
    " * or the fallback title (httpStatus=null entry in the spec) when no",
  );
  sb.appendLine(" * per-status entry matches.");
  sb.appendLine(" */");
  sb.appendLine(
    "export function defaultTitleForStatus(status: number): string {",
  );
  sb.increaseIndent();
  sb.appendLine("switch (status) {");
  sb.increaseIndent();

  let fallback: TitleEntry | null = null;
  for (const e of titles) {
    if (e.httpStatus === null) {
      fallback = e;
      continue;
    }
    sb.appendLine(`case ${e.httpStatus}:`);
    sb.increaseIndent();
    sb.appendLine(`return ProblemDetailsTitles.${e.constName};`);
    sb.decreaseIndent();
  }

  sb.appendLine("default:");
  sb.increaseIndent();
  if (fallback === null) sb.appendLine('return "";');
  else sb.appendLine(`return ProblemDetailsTitles.${fallback.constName};`);
  sb.decreaseIndent();

  sb.decreaseIndent();
  sb.appendLine("}");
  sb.decreaseIndent();
  sb.appendLine("}");
  sb.appendLine();
}

function escapeStringLiteral(value: string): string {
  return value.replace(/\\/g, "\\\\").replace(/"/g, '\\"');
}

function escapeJsDoc(value: string): string {
  return value.replace(/\*\//g, "*\\/");
}

const SPEC_PATH = contractsPath("problem-details", "problem-details.spec.json");

const TARGET_PATH = tsPackagePath("headers", "src", "problem-details.g.ts");

/**
 * Run the problem-details emitter. Per-spec mtime check skips emit when
 * the output is newer than the spec; pass `force=true` to bypass.
 */
export function runProblemDetailsEmit(
  force = false,
): readonly EmitDiagnostic[] {
  if (!force && isOutputUpToDate(TARGET_PATH, [SPEC_PATH])) return [];
  const loadResult = loadSpec<ProblemDetailsSpec>(
    SPEC_PATH,
    DiagnosticIds.PRB_MALFORMED_SPEC,
  );
  if (loadResult.spec === undefined) return loadResult.diagnostics;

  const result = emitProblemDetails(loadResult.spec);
  if (result.diagnostics.some((d) => d.severity === "error"))
    return result.diagnostics;

  writeGeneratedFile(TARGET_PATH, result.source);
  return result.diagnostics;
}

const isMain =
  import.meta.url === `file://${process.argv[1]}` ||
  process.argv[1]?.endsWith("problem-details-emit.ts") === true;
if (isMain) {
  const force = process.argv.includes("--force");
  const diagnostics = runProblemDetailsEmit(force);
  for (const d of diagnostics) console.error(formatDiagnostic(d));
  if (diagnostics.some((d) => d.severity === "error")) process.exit(1);
}
