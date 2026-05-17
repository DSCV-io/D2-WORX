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

/** One encryption-domain entry parsed from the spec. */
export interface EncryptionDomainEntry {
  readonly constName: string;
  readonly value: string;
  readonly doc: string;
}

/** Top-level shape of `encryption-domains.spec.json`. */
export interface EncryptionDomainsSpec {
  readonly domains: readonly EncryptionDomainEntry[];
}

/** Result of validating the spec. */
export interface ValidatedEncryptionDomains {
  readonly domains: readonly EncryptionDomainEntry[];
  readonly diagnostics: readonly EmitDiagnostic[];
}

const CONST_NAME_RE = /^[A-Z][A-Z0-9_]*$/;

/**
 * Validate the spec — surface invalid-constName, duplicate constName,
 * duplicate wire value, empty value.
 */
export function validateEncryptionDomainsSpec(
  spec: EncryptionDomainsSpec,
): ValidatedEncryptionDomains {
  const diagnostics: EmitDiagnostic[] = [];
  const valid: EncryptionDomainEntry[] = [];
  const seenConstNames = new Set<string>();
  const seenValues = new Set<string>();

  for (const entry of spec.domains) {
    if (!CONST_NAME_RE.test(entry.constName)) {
      diagnostics.push(
        diagError(
          DiagnosticIds.ED_INVALID_CONST_NAME,
          `domain has invalid constName '${entry.constName}' — ` +
            `must match ${CONST_NAME_RE.source}`,
        ),
      );
      continue;
    }
    if (entry.value === undefined || entry.value.trim().length === 0) {
      diagnostics.push(
        diagError(
          DiagnosticIds.ED_EMPTY_VALUE,
          `domain '${entry.constName}' has empty or whitespace-only wire value`,
        ),
      );
      continue;
    }
    if (seenConstNames.has(entry.constName)) {
      diagnostics.push(
        diagError(
          DiagnosticIds.ED_DUPLICATE_CONST_NAME,
          `domain constName '${entry.constName}' is declared more than once`,
        ),
      );
      continue;
    }
    if (seenValues.has(entry.value)) {
      diagnostics.push(
        diagError(
          DiagnosticIds.ED_DUPLICATE_VALUE,
          `domain wire value '${entry.value}' is declared more than once`,
        ),
      );
      continue;
    }
    seenConstNames.add(entry.constName);
    seenValues.add(entry.value);
    valid.push(entry);
  }

  return { domains: valid, diagnostics };
}

/** Emit the encryption-domains `.g.ts` source. */
export function emitEncryptionDomains(spec: EncryptionDomainsSpec): EmitResult {
  const v = validateEncryptionDomainsSpec(spec);
  const errors = v.diagnostics.filter((d) => d.severity === "error");
  if (errors.length > 0) return { source: "", diagnostics: v.diagnostics };

  const sb = new StringBuilder();
  sb.appendLine(
    buildHeader("contracts/encryption-domains/encryption-domains.spec.json"),
  );
  sb.appendLine("/* eslint-disable */");
  sb.appendLine();
  sb.appendLine("/**");
  sb.appendLine(
    " * Spec-derived closed catalog of encryption-domain identifiers.",
  );
  sb.appendLine(
    " * Mirrors .NET D2.Shared.Encryption.EncryptionDomains (same wire values).",
  );
  sb.appendLine(" *");
  sb.appendLine(
    " * Use these constants instead of raw strings so a typo can't silently",
  );
  sb.appendLine(
    " * route a message to a non-existent keyring. The PLAINTEXT sentinel is",
  );
  sb.appendLine(
    " * included so callers can distinguish 'no encryption' from a real domain.",
  );
  sb.appendLine(" */");
  sb.appendLine("export const EncryptionDomains = {");
  sb.increaseIndent();
  for (const e of v.domains) {
    sb.appendLine("/**");
    for (const line of e.doc.split("\n"))
      sb.appendLine(` * ${escapeJsDoc(line)}`);
    sb.appendLine(" */");
    sb.appendLine(`${e.constName}: "${escapeStringLiteral(e.value)}",`);
  }
  sb.decreaseIndent();
  sb.appendLine("} as const;");
  sb.appendLine();
  sb.appendLine("export type EncryptionDomain =");
  sb.increaseIndent();
  sb.appendLine("(typeof EncryptionDomains)[keyof typeof EncryptionDomains];");
  sb.decreaseIndent();
  sb.appendLine();
  sb.appendLine("export const ALL_ENCRYPTION_DOMAINS: readonly string[] = [");
  sb.increaseIndent();
  for (const e of v.domains)
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

const SPEC_PATH = contractsPath(
  "encryption-domains",
  "encryption-domains.spec.json",
);
const TARGET_PATH = tsPackagePath(
  "encryption-abstractions",
  "src",
  "encryption-domains.g.ts",
);

/** Run the encryption-domains emitter. */
export function runEncryptionDomainsEmit(
  force = false,
): readonly EmitDiagnostic[] {
  if (!force && isOutputUpToDate(TARGET_PATH, [SPEC_PATH])) return [];
  const loadResult = loadSpec<EncryptionDomainsSpec>(
    SPEC_PATH,
    DiagnosticIds.ED_MALFORMED_SPEC,
  );
  if (loadResult.spec === undefined) return loadResult.diagnostics;

  const result = emitEncryptionDomains(loadResult.spec);
  if (result.diagnostics.some((d) => d.severity === "error"))
    return result.diagnostics;

  writeGeneratedFile(TARGET_PATH, result.source);
  return result.diagnostics;
}

const isMain =
  import.meta.url === `file://${process.argv[1]}` ||
  process.argv[1]?.endsWith("encryption-domains-emit.ts") === true;
if (isMain) {
  const force = process.argv.includes("--force");
  const diagnostics = runEncryptionDomainsEmit(force);
  for (const d of diagnostics) console.error(formatDiagnostic(d));
  if (diagnostics.some((d) => d.severity === "error")) process.exit(1);
}
