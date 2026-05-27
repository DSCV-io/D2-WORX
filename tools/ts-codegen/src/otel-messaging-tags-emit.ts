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

/** One activity-tag entry parsed from the spec. */
export interface OtelMessagingTagEntry {
  readonly constName: string;
  readonly value: string;
  readonly doc: string;
}

/** Top-level shape of `otel-messaging-tags.spec.json`. */
export interface OtelMessagingTagsSpec {
  readonly tags: readonly OtelMessagingTagEntry[];
}

/** Result of validating the spec — surfaces drift / duplicate / shape errors. */
export interface ValidatedOtelMessagingTags {
  readonly tags: readonly OtelMessagingTagEntry[];
  readonly diagnostics: readonly EmitDiagnostic[];
}

const CONST_NAME_RE = /^[A-Z][A-Z0-9_]*$/;

/**
 * Validate the spec — surface invalid-constName, duplicate constName,
 * duplicate wire value, empty value. Mirrors the .NET emitter's predicate
 * set byte-for-byte.
 */
export function validateOtelMessagingTagsSpec(
  spec: OtelMessagingTagsSpec,
): ValidatedOtelMessagingTags {
  const diagnostics: EmitDiagnostic[] = [];
  const valid: OtelMessagingTagEntry[] = [];
  const seenConstNames = new Set<string>();
  const seenValues = new Set<string>();

  for (const entry of spec.tags) {
    if (!CONST_NAME_RE.test(entry.constName)) {
      diagnostics.push(
        diagError(
          DiagnosticIds.OMT_INVALID_CONST_NAME,
          `tag has invalid constName '${entry.constName}' — ` +
            `must match ${CONST_NAME_RE.source}`,
        ),
      );
      continue;
    }
    if (entry.value === undefined || entry.value.trim().length === 0) {
      diagnostics.push(
        diagError(
          DiagnosticIds.OMT_EMPTY_VALUE,
          `tag '${entry.constName}' has empty or whitespace-only wire value`,
        ),
      );
      continue;
    }
    if (seenConstNames.has(entry.constName)) {
      diagnostics.push(
        diagError(
          DiagnosticIds.OMT_DUPLICATE_CONST_NAME,
          `tag constName '${entry.constName}' is declared more than once`,
        ),
      );
      continue;
    }
    if (seenValues.has(entry.value)) {
      diagnostics.push(
        diagError(
          DiagnosticIds.OMT_DUPLICATE_VALUE,
          `tag wire value '${entry.value}' is declared more than once`,
        ),
      );
      continue;
    }
    seenConstNames.add(entry.constName);
    seenValues.add(entry.value);
    valid.push(entry);
  }

  return { tags: valid, diagnostics };
}

/** Emit the OTel messaging tags `.g.ts` source. Stateless. */
export function emitOtelMessagingTags(spec: OtelMessagingTagsSpec): EmitResult {
  const v = validateOtelMessagingTagsSpec(spec);
  const errors = v.diagnostics.filter((d) => d.severity === "error");
  if (errors.length > 0) return { source: "", diagnostics: v.diagnostics };

  const sb = new StringBuilder();
  sb.appendLine(
    buildHeader("contracts/otel-messaging-tags/otel-messaging-tags.spec.json"),
  );
  sb.appendLine("/* eslint-disable */");
  sb.appendLine();
  sb.appendLine("/**");
  sb.appendLine(
    " * Spec-derived OTel messaging activity-tag attribute-name constants.",
  );
  sb.appendLine(
    " * Mirrors .NET D2.Shared.Messaging.RabbitMq.MessagingActivityTags",
  );
  sb.appendLine(" * (same wire values).");
  sb.appendLine(" *");
  sb.appendLine(
    " * Cross-language parity: the SAME spec drives the .NET-side catalog",
  );
  sb.appendLine(
    " * via D2.Shared.OtelMessagingTags.SourceGen. Both sides emit identical",
  );
  sb.appendLine(
    " * attribute names byte-for-byte; cross-language wire drift is impossible.",
  );
  sb.appendLine(" */");
  sb.appendLine("export const MessagingActivityTags = {");
  sb.increaseIndent();
  for (const e of v.tags) {
    sb.appendLine("/**");
    for (const line of e.doc.split("\n"))
      sb.appendLine(` * ${escapeJsDoc(line)}`);
    sb.appendLine(" */");
    sb.appendLine(`${e.constName}: "${escapeStringLiteral(e.value)}",`);
  }
  sb.decreaseIndent();
  sb.appendLine("} as const;");
  sb.appendLine();
  sb.appendLine("export type MessagingActivityTag =");
  sb.increaseIndent();
  sb.appendLine(
    "(typeof MessagingActivityTags)[keyof typeof MessagingActivityTags];",
  );
  sb.decreaseIndent();
  sb.appendLine();
  sb.appendLine(
    "export const ALL_MESSAGING_ACTIVITY_TAGS: readonly string[] = [",
  );
  sb.increaseIndent();
  for (const e of v.tags) sb.appendLine(`"${escapeStringLiteral(e.value)}",`);
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

const SPEC_PATH = contractsPath(
  "otel-messaging-tags",
  "otel-messaging-tags.spec.json",
);
const TARGET_PATH = tsPackagePath(
  "telemetry",
  "src",
  "otel-messaging-tags.g.ts",
);

/** Run the OTel messaging tags emitter. */
export function runOtelMessagingTagsEmit(
  force = false,
): readonly EmitDiagnostic[] {
  if (!force && isOutputUpToDate(TARGET_PATH, [SPEC_PATH])) return [];
  const loadResult = loadSpec<OtelMessagingTagsSpec>(
    SPEC_PATH,
    DiagnosticIds.OMT_MALFORMED_SPEC,
  );
  if (loadResult.spec === undefined) return loadResult.diagnostics;

  const result = emitOtelMessagingTags(loadResult.spec);
  if (result.diagnostics.some((d) => d.severity === "error"))
    return result.diagnostics;

  writeGeneratedFile(TARGET_PATH, result.source);
  return result.diagnostics;
}

const isMain =
  import.meta.url === `file://${process.argv[1]}` ||
  process.argv[1]?.endsWith("otel-messaging-tags-emit.ts") === true;
if (isMain) {
  const force = process.argv.includes("--force");
  const diagnostics = runOtelMessagingTagsEmit(force);
  for (const d of diagnostics) console.error(formatDiagnostic(d));
  if (diagnostics.some((d) => d.severity === "error")) process.exit(1);
}
