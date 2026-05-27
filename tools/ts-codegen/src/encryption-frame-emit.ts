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

/** One encryption-frame field entry. */
export interface EncryptionFrameFieldEntry {
  readonly constName: string;
  readonly offset: number;
  readonly length: number;
  readonly kind: string;
  readonly doc: string;
}

/** Frame-level numeric constraints. */
export interface EncryptionFrameConstraints {
  readonly minKidLength: number;
  readonly maxKidLength: number;
  readonly nonceLength: number;
  readonly tagLength: number;
  readonly minFrameSize: number;
}

/** Top-level shape of `encryption-frame.spec.json`. */
export interface EncryptionFrameSpec {
  readonly version: number;
  readonly fields: readonly EncryptionFrameFieldEntry[];
  readonly constraints: EncryptionFrameConstraints;
}

/** Validate the spec — invalid version, duplicate constName, overlap, invalid length. */
export function validateEncryptionFrameSpec(spec: EncryptionFrameSpec): {
  fields: EncryptionFrameFieldEntry[];
  diagnostics: EmitDiagnostic[];
} {
  const diagnostics: EmitDiagnostic[] = [];
  const valid: EncryptionFrameFieldEntry[] = [];
  const seenConstNames = new Set<string>();

  if (spec.version < 1) {
    diagnostics.push(
      diagError(
        DiagnosticIds.EF_INVALID_VERSION,
        `version ${spec.version} is invalid (must be >= 1)`,
      ),
    );
  }

  for (const entry of spec.fields) {
    if (entry.length < -1 || entry.length === 0) {
      diagnostics.push(
        diagError(
          DiagnosticIds.EF_INVALID_LENGTH,
          `field '${entry.constName}' has invalid length ${entry.length}`,
        ),
      );
      continue;
    }
    if (seenConstNames.has(entry.constName)) {
      diagnostics.push(
        diagError(
          DiagnosticIds.EF_DUPLICATE_FIELD_NAME,
          `field constName '${entry.constName}' is declared more than once`,
        ),
      );
      continue;
    }
    seenConstNames.add(entry.constName);
    valid.push(entry);
  }

  for (let i = 0; i < valid.length; i++) {
    for (let j = i + 1; j < valid.length; j++) {
      const a = valid[i]!;
      const b = valid[j]!;
      if (a.offset < 0 || a.length < 1 || b.offset < 0 || b.length < 1)
        continue;
      const aEnd = a.offset + a.length;
      const bEnd = b.offset + b.length;
      if (a.offset < bEnd && b.offset < aEnd) {
        diagnostics.push(
          diagError(
            DiagnosticIds.EF_OVERLAPPING_FIELDS,
            `fields '${a.constName}' and '${b.constName}' overlap at fixed offsets`,
          ),
        );
      }
    }
  }

  return { fields: valid, diagnostics };
}

/** Emit the encryption-frame `.g.ts` source. */
export function emitEncryptionFrame(spec: EncryptionFrameSpec): EmitResult {
  const v = validateEncryptionFrameSpec(spec);
  const errors = v.diagnostics.filter((d) => d.severity === "error");
  if (errors.length > 0) return { source: "", diagnostics: v.diagnostics };

  const sb = new StringBuilder();
  sb.appendLine(
    buildHeader("contracts/encryption-frame/encryption-frame.spec.json"),
  );
  sb.appendLine("/* eslint-disable */");
  sb.appendLine();
  sb.appendLine("/**");
  sb.appendLine(
    " * Spec-derived binary-layout constants for the D2 on-wire encryption frame.",
  );
  sb.appendLine(
    " * Mirrors .NET D2.Shared.Encryption.EncryptionFrameLayout (same offsets",
  );
  sb.appendLine(" * and lengths byte-for-byte).");
  sb.appendLine(" *");
  sb.appendLine(
    " * Consumed by @d2/encryption-abstractions for on-wire encryption-frame decoding.",
  );
  sb.appendLine(" */");
  sb.appendLine("export const EncryptionFrame = {");
  sb.increaseIndent();
  sb.appendLine(`CURRENT_VERSION: ${spec.version},`);
  for (const e of v.fields) {
    sb.appendLine("/**");
    for (const line of e.doc.split("\n"))
      sb.appendLine(` * ${escapeJsDoc(line)}`);
    sb.appendLine(
      ` * Kind: ${e.kind}. Offset: ${e.offset} (-1 = variable). ` +
        `Length: ${e.length} (-1 = variable).`,
    );
    sb.appendLine(" */");
    sb.appendLine(`${e.constName}_OFFSET: ${e.offset},`);
    sb.appendLine(`${e.constName}_LENGTH: ${e.length},`);
  }
  sb.appendLine(`/** Minimum allowed kid length in UTF-8 bytes. */`);
  sb.appendLine(`CONSTRAINT_MIN_KID_LENGTH: ${spec.constraints.minKidLength},`);
  sb.appendLine(`/** Maximum allowed kid length in UTF-8 bytes. */`);
  sb.appendLine(`CONSTRAINT_MAX_KID_LENGTH: ${spec.constraints.maxKidLength},`);
  sb.appendLine(
    "/** AES-GCM nonce length in bytes (mirrors the per-field NONCE_LENGTH; " +
      "prefixed to avoid collision). */",
  );
  sb.appendLine(`CONSTRAINT_NONCE_LENGTH: ${spec.constraints.nonceLength},`);
  sb.appendLine(
    "/** AES-GCM authentication tag length in bytes (trailing bytes of " +
      "CIPHERTEXT_WITH_TAG). */",
  );
  sb.appendLine(`CONSTRAINT_TAG_LENGTH: ${spec.constraints.tagLength},`);
  sb.appendLine(`/** Smallest valid frame size in bytes. */`);
  sb.appendLine(`CONSTRAINT_MIN_FRAME_SIZE: ${spec.constraints.minFrameSize},`);
  sb.decreaseIndent();
  sb.appendLine("} as const;");
  sb.appendLine();
  sb.appendLine("export type EncryptionFrameField =");
  sb.increaseIndent();
  sb.appendLine("(typeof EncryptionFrame)[keyof typeof EncryptionFrame];");
  sb.decreaseIndent();
  sb.appendLine();
  sb.appendLine(
    "export const ALL_ENCRYPTION_FRAME_FIELDS: readonly string[] = [",
  );
  sb.increaseIndent();
  for (const e of v.fields) sb.appendLine(`"${e.constName}",`);
  sb.decreaseIndent();
  sb.appendLine("];");
  sb.appendLine();

  return { source: sb.toString(), diagnostics: v.diagnostics };
}

function escapeJsDoc(value: string): string {
  return value.replace(/\*\//g, "*\\/");
}

const SPEC_PATH = contractsPath(
  "encryption-frame",
  "encryption-frame.spec.json",
);
const TARGET_PATH = tsPackagePath(
  "encryption-abstractions",
  "src",
  "encryption-frame.g.ts",
);

/** Run the encryption-frame emitter. */
export function runEncryptionFrameEmit(
  force = false,
): readonly EmitDiagnostic[] {
  if (!force && isOutputUpToDate(TARGET_PATH, [SPEC_PATH])) return [];
  const loadResult = loadSpec<EncryptionFrameSpec>(
    SPEC_PATH,
    DiagnosticIds.EF_MALFORMED_SPEC,
  );
  if (loadResult.spec === undefined) return loadResult.diagnostics;

  const result = emitEncryptionFrame(loadResult.spec);
  if (result.diagnostics.some((d) => d.severity === "error"))
    return result.diagnostics;

  writeGeneratedFile(TARGET_PATH, result.source);
  return result.diagnostics;
}

const isMain =
  import.meta.url === `file://${process.argv[1]}` ||
  process.argv[1]?.endsWith("encryption-frame-emit.ts") === true;
if (isMain) {
  const force = process.argv.includes("--force");
  const diagnostics = runEncryptionFrameEmit(force);
  for (const d of diagnostics) console.error(formatDiagnostic(d));
  if (diagnostics.some((d) => d.severity === "error")) process.exit(1);
}
