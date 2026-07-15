// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
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
  readonly mode?: string;
  readonly consumerService?: string;
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
const CONSUMER_SERVICE_RE = /^[a-z0-9-]{1,64}$/;
const MODE_SYMMETRIC = "symmetric";
const MODE_SEALED = "sealed";

/** True when `mode` is the sealed literal. */
function isSealed(entry: EncryptionDomainEntry): boolean {
  return entry.mode === MODE_SEALED;
}

/** True for a null / undefined / whitespace-only string (Falsey twin). */
function isFalsey(value: string | undefined): boolean {
  return value === undefined || value.trim().length === 0;
}

/**
 * Validate the spec — surface invalid-constName, duplicate constName,
 * duplicate wire value, empty value, and the mode / consumerService
 * consistency rules (D2ED006-009), mirroring the .NET emitter byte-for-byte.
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
    if (!validateModeAndConsumer(entry, diagnostics)) continue;
    seenConstNames.add(entry.constName);
    seenValues.add(entry.value);
    valid.push(entry);
  }

  return { domains: valid, diagnostics };
}

/**
 * Validate an entry's optional `mode` / `consumerService` pair. Pushes the
 * relevant fail-loud diagnostic and returns `false` when inconsistent.
 */
function validateModeAndConsumer(
  entry: EncryptionDomainEntry,
  diagnostics: EmitDiagnostic[],
): boolean {
  const mode = entry.mode;
  if (mode !== undefined && mode !== MODE_SYMMETRIC && mode !== MODE_SEALED) {
    diagnostics.push(
      diagError(
        DiagnosticIds.ED_INVALID_MODE,
        `domain '${entry.constName}' has invalid mode '${mode}' — ` +
          `must be '${MODE_SYMMETRIC}' or '${MODE_SEALED}'`,
      ),
    );
    return false;
  }

  const consumer = entry.consumerService;
  if (isSealed(entry)) {
    if (isFalsey(consumer)) {
      diagnostics.push(
        diagError(
          DiagnosticIds.ED_MISSING_CONSUMER_SERVICE,
          `domain '${entry.constName}' is sealed but declares no consumerService`,
        ),
      );
      return false;
    }
    if (!CONSUMER_SERVICE_RE.test(consumer!)) {
      diagnostics.push(
        diagError(
          DiagnosticIds.ED_INVALID_CONSUMER_SERVICE,
          `domain '${entry.constName}' consumerService '${consumer}' must ` +
            `match ${CONSUMER_SERVICE_RE.source}`,
        ),
      );
      return false;
    }
    return true;
  }

  if (!isFalsey(consumer)) {
    diagnostics.push(
      diagError(
        DiagnosticIds.ED_UNEXPECTED_CONSUMER_SERVICE,
        `domain '${entry.constName}' declares consumerService '${consumer}' ` +
          `but is not sealed`,
      ),
    );
    return false;
  }

  return true;
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
    " * Mirrors .NET DcsvIo.D2.Encryption.EncryptionDomains (same wire values).",
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

  // Per-domain encryption mode (literal-typed so the messaging publisher's
  // DomainCryptoMap type-witness can brand each domain slot). Mirrors .NET
  // EncryptionDomainModes.ModeFor over the catalog.
  sb.appendLine("/**");
  sb.appendLine(
    " * Per-domain payload encryption mode, keyed by wire value. `symmetric` =",
  );
  sb.appendLine(
    " * shared-keyring AES-256-GCM (v1 frame); `sealed` = per-consumer-service",
  );
  sb.appendLine(
    " * ECDH (v2 frame). Literal-typed for the publisher type-witness. Mirrors",
  );
  sb.appendLine(" * .NET EncryptionDomainModes.");
  sb.appendLine(" */");
  sb.appendLine("export const EncryptionDomainModes = {");
  sb.increaseIndent();
  for (const e of v.domains) {
    const mode = isSealed(e) ? MODE_SEALED : MODE_SYMMETRIC;
    sb.appendLine(`"${escapeStringLiteral(e.value)}": "${mode}",`);
  }
  sb.decreaseIndent();
  sb.appendLine("} as const;");
  sb.appendLine();
  sb.appendLine(
    `export type EncryptionDomainMode = "${MODE_SYMMETRIC}" | "${MODE_SEALED}";`,
  );
  sb.appendLine();

  // Consumer ServiceId per SEALED domain (the single decryptor). Only sealed
  // domains appear. Mirrors .NET EncryptionDomainModes.ConsumerServiceByDomain.
  sb.appendLine("/**");
  sb.appendLine(
    " * Consumer ServiceId per SEALED domain (the single decryptor). Only",
  );
  sb.appendLine(" * sealed domains appear. Mirrors .NET");
  sb.appendLine(" * EncryptionDomainModes.ConsumerServiceByDomain.");
  sb.appendLine(" */");
  sb.appendLine("export const ConsumerServiceByDomain = {");
  sb.increaseIndent();
  for (const e of v.domains) {
    if (!isSealed(e)) continue;
    sb.appendLine(
      `"${escapeStringLiteral(e.value)}": "${escapeStringLiteral(e.consumerService!)}",`,
    );
  }
  sb.decreaseIndent();
  sb.appendLine("} as const;");
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
