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

/** One sealed encryption-frame field entry. */
export interface SealedFrameFieldEntry {
  readonly constName: string;
  readonly offset: number;
  readonly length: number;
  readonly kind: string;
  readonly doc: string;
}

/** Sealed frame-level numeric constraints. */
export interface SealedFrameConstraints {
  readonly minKidLength: number;
  readonly maxKidLength: number;
  readonly ephPubLengthPrefixSize: number;
  readonly maxEphPubLength: number;
  readonly nonceLength: number;
  readonly tagLength: number;
  readonly minFrameSize: number;
}

/** Top-level shape of `encryption-frame-sealed.spec.json`. */
export interface SealedFrameSpec {
  readonly version: number;
  readonly fields: readonly SealedFrameFieldEntry[];
  readonly constraints: SealedFrameConstraints;
}

// The sealed family's version floor — version 1 belongs to the symmetric
// frame, so a sealed spec below 2 would collide with its discriminator.
const MIN_SEALED_VERSION = 2;

// The closed set of field kinds the sealed decoder knows how to read.
// Mirrors the .NET SealedFrameEmitter closed set exactly.
const KNOWN_KINDS: ReadonlySet<string> = new Set([
  "byte_fixed",
  "variable_utf8",
  "variable_binary_u16be",
  "variable_remainder",
  "byte_fixed_trailing",
]);

const BINARY_U16BE_KIND = "variable_binary_u16be";
const BYTE_FIXED_KIND = "byte_fixed";

/**
 * Validate the sealed spec — invalid version, duplicate constName, unknown
 * kind, the variable_binary_u16be length-prefix pairing rule, overlap, and
 * invalid length. Mirrors the .NET SealedFrameEmitter validation exactly.
 */
export function validateSealedFrameSpec(spec: SealedFrameSpec): {
  fields: SealedFrameFieldEntry[];
  diagnostics: EmitDiagnostic[];
} {
  const diagnostics: EmitDiagnostic[] = [];
  const valid: SealedFrameFieldEntry[] = [];
  const seenConstNames = new Set<string>();

  if (spec.version < MIN_SEALED_VERSION) {
    diagnostics.push(
      diagError(
        DiagnosticIds.EFS_INVALID_VERSION,
        `version ${spec.version} is invalid (must be >= 2 — version 1 is the symmetric frame)`,
      ),
    );
    return { fields: [], diagnostics };
  }

  for (let i = 0; i < spec.fields.length; i++) {
    const entry = spec.fields[i]!;
    if (entry.length < -1 || entry.length === 0) {
      diagnostics.push(
        diagError(
          DiagnosticIds.EFS_INVALID_LENGTH,
          `field '${entry.constName}' has invalid length ${entry.length}`,
        ),
      );
      continue;
    }
    if (seenConstNames.has(entry.constName)) {
      diagnostics.push(
        diagError(
          DiagnosticIds.EFS_DUPLICATE_FIELD_NAME,
          `field constName '${entry.constName}' is declared more than once`,
        ),
      );
      continue;
    }
    seenConstNames.add(entry.constName);
    if (!KNOWN_KINDS.has(entry.kind)) {
      diagnostics.push(
        diagError(
          DiagnosticIds.EFS_UNKNOWN_FIELD_KIND,
          `field '${entry.constName}' declares unknown kind '${entry.kind}'`,
        ),
      );
      continue;
    }

    // The new-kind structural rule: variable_binary_u16be is readable ONLY
    // behind an immediately preceding byte_fixed length prefix of exactly
    // the declared prefix width.
    if (entry.kind === BINARY_U16BE_KIND) {
      const previous = i > 0 ? spec.fields[i - 1]! : undefined;
      const precededByPrefix =
        previous !== undefined &&
        previous.kind === BYTE_FIXED_KIND &&
        previous.length === spec.constraints.ephPubLengthPrefixSize;
      if (!precededByPrefix) {
        diagnostics.push(
          diagError(
            DiagnosticIds.EFS_BINARY_LENGTH_PREFIX_MISSING,
            `field '${entry.constName}' is variable_binary_u16be but is not immediately ` +
              `preceded by a byte_fixed length field of ` +
              `${spec.constraints.ephPubLengthPrefixSize} byte(s)`,
          ),
        );
        continue;
      }
    }

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
            DiagnosticIds.EFS_OVERLAPPING_FIELDS,
            `fields '${a.constName}' and '${b.constName}' overlap at fixed offsets`,
          ),
        );
      }
    }
  }

  return { fields: valid, diagnostics };
}

/** Emit the sealed encryption-frame `.g.ts` source. */
export function emitSealedFrame(spec: SealedFrameSpec): EmitResult {
  const v = validateSealedFrameSpec(spec);
  const errors = v.diagnostics.filter((d) => d.severity === "error");
  if (errors.length > 0) return { source: "", diagnostics: v.diagnostics };

  const sb = new StringBuilder();
  sb.appendLine(
    buildHeader(
      "contracts/encryption-frame-sealed/encryption-frame-sealed.spec.json",
    ),
  );
  sb.appendLine("/* eslint-disable */");
  sb.appendLine();
  sb.appendLine("/**");
  sb.appendLine(
    " * Spec-derived binary-layout constants for the D2 on-wire SEALED encryption",
  );
  sb.appendLine(
    " * frame (version 2 — the asymmetric ECDH-ES hybrid). Mirrors .NET",
  );
  sb.appendLine(
    " * D2.Shared.Encryption.SealedFrameLayout (same offsets and lengths",
  );
  sb.appendLine(" * byte-for-byte).");
  sb.appendLine(" *");
  sb.appendLine(
    " * Consumed by @d2/encryption-abstractions for on-wire sealed-frame reading.",
  );
  sb.appendLine(" */");
  sb.appendLine("export const SealedFrame = {");
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
  sb.appendLine(`/** Minimum allowed recipient-kid length in UTF-8 bytes. */`);
  sb.appendLine(`CONSTRAINT_MIN_KID_LENGTH: ${spec.constraints.minKidLength},`);
  sb.appendLine(`/** Maximum allowed recipient-kid length in UTF-8 bytes. */`);
  sb.appendLine(`CONSTRAINT_MAX_KID_LENGTH: ${spec.constraints.maxKidLength},`);
  sb.appendLine(
    "/** Byte width of the big-endian length prefix in front of the ephemeral " +
      "public key (uint16). */",
  );
  sb.appendLine(
    `CONSTRAINT_EPH_PUB_LENGTH_PREFIX_SIZE: ${spec.constraints.ephPubLengthPrefixSize},`,
  );
  sb.appendLine(
    "/** Upper cap on the declared ephemeral-public-key length (allocation " +
      "guard). */",
  );
  sb.appendLine(
    `CONSTRAINT_MAX_EPH_PUB_LENGTH: ${spec.constraints.maxEphPubLength},`,
  );
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
  sb.appendLine(`/** Smallest valid sealed frame size in bytes. */`);
  sb.appendLine(`CONSTRAINT_MIN_FRAME_SIZE: ${spec.constraints.minFrameSize},`);
  sb.decreaseIndent();
  sb.appendLine("} as const;");
  sb.appendLine();
  sb.appendLine("export type SealedFrameField =");
  sb.increaseIndent();
  sb.appendLine("(typeof SealedFrame)[keyof typeof SealedFrame];");
  sb.decreaseIndent();
  sb.appendLine();
  sb.appendLine("export const ALL_SEALED_FRAME_FIELDS: readonly string[] = [");
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
  "encryption-frame-sealed",
  "encryption-frame-sealed.spec.json",
);
const TARGET_PATH = tsPackagePath(
  "encryption-abstractions",
  "src",
  "encryption-frame-sealed.g.ts",
);

/** Run the sealed encryption-frame emitter. */
export function runSealedFrameEmit(force = false): readonly EmitDiagnostic[] {
  if (!force && isOutputUpToDate(TARGET_PATH, [SPEC_PATH])) return [];
  const loadResult = loadSpec<SealedFrameSpec>(
    SPEC_PATH,
    DiagnosticIds.EFS_MALFORMED_SPEC,
  );
  if (loadResult.spec === undefined) return loadResult.diagnostics;

  const result = emitSealedFrame(loadResult.spec);
  if (result.diagnostics.some((d) => d.severity === "error"))
    return result.diagnostics;

  writeGeneratedFile(TARGET_PATH, result.source);
  return result.diagnostics;
}

const isMain =
  import.meta.url === `file://${process.argv[1]}` ||
  process.argv[1]?.endsWith("encryption-frame-sealed-emit.ts") === true;
if (isMain) {
  const force = process.argv.includes("--force");
  const diagnostics = runSealedFrameEmit(force);
  for (const d of diagnostics) console.error(formatDiagnostic(d));
  if (diagnostics.some((d) => d.severity === "error")) process.exit(1);
}
