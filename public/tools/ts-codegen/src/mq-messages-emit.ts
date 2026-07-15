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

/** One message entry parsed from `mq-messages.spec.json`. */
export interface MqMessageEntry {
  readonly constant: string;
  readonly messageType: string;
  readonly exchange: string;
  readonly exchangeType: string;
  readonly encryption: string;
  readonly encryptionReason?: string;
  readonly defaultRoutingKey?: string;
  readonly deprecated?: boolean;
  readonly deprecatedReason?: string;
}

/** Top-level shape of `mq-messages.spec.json`. */
export interface MqMessagesSpec {
  readonly messages: readonly MqMessageEntry[];
}

/** Result of validating the spec — surfaces drift / duplicate / shape errors. */
export interface ValidatedMqMessages {
  readonly messages: readonly MqMessageEntry[];
  readonly diagnostics: readonly EmitDiagnostic[];
}

// Constant identifiers are PascalCase per the schema pattern (they name the
// MqMessages.<X> string constant, NOT an UPPER_SNAKE catalog key).
const CONST_NAME_RE = /^[A-Z][A-Za-z0-9]*$/;

/**
 * Validate the spec — surface invalid-constName, duplicate constName,
 * duplicate messageType, missing encryption (default-deny), and empty
 * required-field violations. Mirrors the .NET
 * `D2.Shared.Messaging.SourceGen.MqGenerator` predicate interpretation
 * (same spec source on both sides means the same violation surface).
 */
export function validateMqMessagesSpec(
  spec: MqMessagesSpec,
): ValidatedMqMessages {
  const diagnostics: EmitDiagnostic[] = [];
  const valid: MqMessageEntry[] = [];
  const seenConstants = new Set<string>();
  const seenMessageTypes = new Set<string>();

  for (const entry of spec.messages) {
    if (!CONST_NAME_RE.test(entry.constant)) {
      diagnostics.push(
        diagError(
          DiagnosticIds.MQ_INVALID_CONST_NAME,
          `message has invalid constant '${entry.constant}' — ` +
            `must match ${CONST_NAME_RE.source}`,
        ),
      );
      continue;
    }
    if (isEmpty(entry.messageType)) {
      diagnostics.push(
        diagError(
          DiagnosticIds.MQ_EMPTY_VALUE,
          `message '${entry.constant}' has empty messageType`,
        ),
      );
      continue;
    }
    if (isEmpty(entry.exchange)) {
      diagnostics.push(
        diagError(
          DiagnosticIds.MQ_EMPTY_VALUE,
          `message '${entry.constant}' has empty exchange`,
        ),
      );
      continue;
    }
    if (isEmpty(entry.exchangeType)) {
      diagnostics.push(
        diagError(
          DiagnosticIds.MQ_EMPTY_VALUE,
          `message '${entry.constant}' has empty exchangeType`,
        ),
      );
      continue;
    }
    if (isEmpty(entry.encryption)) {
      diagnostics.push(
        diagError(
          DiagnosticIds.MQ_MISSING_ENCRYPTION,
          `message '${entry.constant}' has missing/empty encryption ` +
            `(default-deny: declare an EncryptionDomains value or 'plaintext')`,
        ),
      );
      continue;
    }
    if (seenConstants.has(entry.constant)) {
      diagnostics.push(
        diagError(
          DiagnosticIds.MQ_DUPLICATE_CONSTANT,
          `message constant '${entry.constant}' is declared more than once`,
        ),
      );
      continue;
    }
    if (seenMessageTypes.has(entry.messageType)) {
      diagnostics.push(
        diagError(
          DiagnosticIds.MQ_DUPLICATE_MESSAGE_TYPE,
          `message type '${entry.messageType}' is declared more than once`,
        ),
      );
      continue;
    }
    seenConstants.add(entry.constant);
    seenMessageTypes.add(entry.messageType);
    valid.push(entry);
  }

  return { messages: valid, diagnostics };
}

/** Emit the mq-messages descriptor-mirror `.g.ts` source. Stateless. */
export function emitMqMessages(spec: MqMessagesSpec): EmitResult {
  const v = validateMqMessagesSpec(spec);
  const errors = v.diagnostics.filter((d) => d.severity === "error");
  if (errors.length > 0) return { source: "", diagnostics: v.diagnostics };

  const sb = new StringBuilder();
  sb.appendLine(buildHeader("contracts/mq-messages/mq-messages.spec.json"));
  sb.appendLine("/* eslint-disable */");
  sb.appendLine();
  sb.appendLine("/**");
  sb.appendLine(
    " * Spec-derived RabbitMQ message-descriptor mirror. Mirrors .NET",
  );
  sb.appendLine(" * D2.Shared.Messaging.MqMessages (string constants) +");
  sb.appendLine(
    " * D2.Shared.Messaging.MqMessagesRegistry.ByConstant (constant → descriptor)",
  );
  sb.appendLine(
    " * + the D2.Shared.Messaging.MqMessageDescriptor record shape.",
  );
  sb.appendLine(" *");
  sb.appendLine(
    " * Cross-language parity: the SAME spec drives the .NET-side catalog via",
  );
  sb.appendLine(
    " * D2.Shared.Messaging.SourceGen. Both sides emit identical constants +",
  );
  sb.appendLine(
    " * descriptor field values; cross-language wire drift is impossible.",
  );
  sb.appendLine(" */");
  sb.appendLine();

  // Descriptor interface — mirrors the .NET MqMessageDescriptor record
  // (Constant / MessageTypeName / Exchange / ExchangeType / Encryption /
  // EncryptionReason / DefaultRoutingKey). The runtime descriptor carries
  // NO deprecated / replacedBy / sunset (those are spec-evolution metadata,
  // not a publisher's runtime contract).
  sb.appendLine("/**");
  sb.appendLine(
    " * Fully-resolved publisher contract for one message type. One per",
  );
  sb.appendLine(" * MqMessages.<X> constant.");
  sb.appendLine(" */");
  sb.appendLine("export interface MqMessageDescriptor {");
  sb.increaseIndent();
  sb.appendLine(
    "/** The string constant identifying this descriptor (matches MqMessages.<X>). */",
  );
  sb.appendLine("readonly constant: string;");
  sb.appendLine("/** Fully-qualified .NET type name of the message class. */");
  sb.appendLine("readonly messageType: string;");
  sb.appendLine("/** AMQP exchange name to publish to. */");
  sb.appendLine("readonly exchange: string;");
  sb.appendLine("/** AMQP exchange type — fanout / topic / direct. */");
  sb.appendLine("readonly exchangeType: string;");
  sb.appendLine(
    "/** An EncryptionDomains value (e.g. 'audit') or the literal 'plaintext'. */",
  );
  sb.appendLine("readonly encryption: string;");
  sb.appendLine(
    "/** Rationale when encryption == 'plaintext'; absent when encrypted. */",
  );
  sb.appendLine("readonly encryptionReason?: string;");
  sb.appendLine(
    "/** Routing key used when no per-publish override is supplied (empty for fanout). */",
  );
  sb.appendLine("readonly defaultRoutingKey?: string;");
  sb.decreaseIndent();
  sb.appendLine("}");
  sb.appendLine();

  // MqMessages string-constant catalog.
  sb.appendLine("/**");
  sb.appendLine(" * String constants for every message type registered in");
  sb.appendLine(
    " * contracts/mq-messages/mq-messages.spec.json. Mirrors .NET MqMessages.",
  );
  sb.appendLine(" */");
  sb.appendLine("export const MqMessages = {");
  sb.increaseIndent();
  for (const e of v.messages) {
    sb.appendLine("/**");
    sb.appendLine(` * Publisher contract for ${escapeJsDoc(e.messageType)}.`);
    if (e.deprecated === true) {
      const reason = isEmpty(e.deprecatedReason)
        ? ""
        : ` ${escapeJsDoc(e.deprecatedReason!)}`;
      sb.appendLine(` * @deprecated${reason}`);
    }
    sb.appendLine(" */");
    sb.appendLine(`${e.constant}: "${escapeStringLiteral(e.constant)}",`);
  }
  sb.decreaseIndent();
  sb.appendLine("} as const;");
  sb.appendLine();
  sb.appendLine("export type MqMessage =");
  sb.increaseIndent();
  sb.appendLine("(typeof MqMessages)[keyof typeof MqMessages];");
  sb.decreaseIndent();
  sb.appendLine();

  // MqMessagesRegistry — constant → descriptor. Mirrors .NET
  // MqMessagesRegistry.ByConstant.
  sb.appendLine("/**");
  sb.appendLine(" * Runtime registry mapping every MqMessages constant to its");
  sb.appendLine(
    " * fully-resolved MqMessageDescriptor. Mirrors .NET MqMessagesRegistry.ByConstant.",
  );
  sb.appendLine(" */");
  sb.appendLine(
    "export const MqMessagesRegistry: Readonly<Record<string, MqMessageDescriptor>> = {",
  );
  sb.increaseIndent();
  for (const e of v.messages) {
    sb.appendLine(`${e.constant}: {`);
    sb.increaseIndent();
    sb.appendLine(`constant: "${escapeStringLiteral(e.constant)}",`);
    sb.appendLine(`messageType: "${escapeStringLiteral(e.messageType)}",`);
    sb.appendLine(`exchange: "${escapeStringLiteral(e.exchange)}",`);
    sb.appendLine(`exchangeType: "${escapeStringLiteral(e.exchangeType)}",`);
    sb.appendLine(`encryption: "${escapeStringLiteral(e.encryption)}",`);
    if (!isEmpty(e.encryptionReason)) {
      sb.appendLine(
        `encryptionReason: "${escapeStringLiteral(e.encryptionReason!)}",`,
      );
    }
    if (e.defaultRoutingKey !== undefined) {
      sb.appendLine(
        `defaultRoutingKey: "${escapeStringLiteral(e.defaultRoutingKey)}",`,
      );
    }
    sb.decreaseIndent();
    sb.appendLine("},");
  }
  sb.decreaseIndent();
  sb.appendLine("};");
  sb.appendLine();

  // MqMessagesCatalog — the SAME per-message descriptor data as the registry,
  // but `as const` so every field (critically `encryption`) keeps its LITERAL
  // type. This is the TS publisher's compile-time type-witness input: the
  // publisher maps each message constant's literal `encryption` to a
  // mode-branded composer slot, so publishing to an unwired encrypted domain is
  // a COMPILE error. There is no .NET twin — .NET gets the same enforcement from
  // the descriptor's computed `IsSealed` + spec-driven composer + DI.
  sb.appendLine("/**");
  sb.appendLine(
    " * Literal-typed (`as const`) per-message catalog — the compile-time",
  );
  sb.appendLine(
    " * type-witness input for the @d2/messaging-rabbitmq publisher. Same data",
  );
  sb.appendLine(
    " * as MqMessagesRegistry, but each `encryption` keeps its literal type.",
  );
  sb.appendLine(" */");
  sb.appendLine("export const MqMessagesCatalog = {");
  sb.increaseIndent();
  for (const e of v.messages) {
    sb.appendLine(`${e.constant}: {`);
    sb.increaseIndent();
    sb.appendLine(`constant: "${escapeStringLiteral(e.constant)}",`);
    sb.appendLine(`messageType: "${escapeStringLiteral(e.messageType)}",`);
    sb.appendLine(`exchange: "${escapeStringLiteral(e.exchange)}",`);
    sb.appendLine(`exchangeType: "${escapeStringLiteral(e.exchangeType)}",`);
    sb.appendLine(`encryption: "${escapeStringLiteral(e.encryption)}",`);
    if (e.defaultRoutingKey !== undefined) {
      sb.appendLine(
        `defaultRoutingKey: "${escapeStringLiteral(e.defaultRoutingKey)}",`,
      );
    }
    sb.decreaseIndent();
    sb.appendLine("},");
  }
  sb.decreaseIndent();
  sb.appendLine("} as const;");
  sb.appendLine();
  sb.appendLine(
    "/** Union of every message constant present in the catalog. */",
  );
  sb.appendLine(
    "export type MqMessageCatalogKey = keyof typeof MqMessagesCatalog;",
  );
  sb.appendLine();

  sb.appendLine("export const ALL_MQ_MESSAGE_CONSTANTS: readonly string[] = [");
  sb.increaseIndent();
  for (const e of v.messages)
    sb.appendLine(`"${escapeStringLiteral(e.constant)}",`);
  sb.decreaseIndent();
  sb.appendLine("];");
  sb.appendLine();

  return { source: sb.toString(), diagnostics: v.diagnostics };
}

function isEmpty(value: string | undefined): boolean {
  return value === undefined || value.trim().length === 0;
}

function escapeStringLiteral(value: string): string {
  return value.replace(/\\/g, "\\\\").replace(/"/g, '\\"');
}

function escapeJsDoc(value: string): string {
  return value.replace(/\*\//g, "*\\/");
}

// ---------------------------------------------------------------------------
// CLI-runner section — mtime-check, disk-write, isMain guard.
// Excluded from unit-test coverage (requires process/fs mocking to exercise);
// the exported library functions above (validateMqMessagesSpec, emitMqMessages)
// ARE fully unit-tested in mq-messages-emit.test.ts.
// ---------------------------------------------------------------------------

/* v8 ignore start */
const SPEC_PATH = contractsPath("mq-messages", "mq-messages.spec.json");
const TARGET_PATH = tsPackagePath(
  "messaging-abstractions",
  "src",
  "mq-messages.g.ts",
);

/** Run the mq-messages descriptor-mirror emitter. */
export function runMqMessagesEmit(force = false): readonly EmitDiagnostic[] {
  if (!force && isOutputUpToDate(TARGET_PATH, [SPEC_PATH])) return [];
  const loadResult = loadSpec<MqMessagesSpec>(
    SPEC_PATH,
    DiagnosticIds.MQ_MALFORMED_SPEC,
  );
  if (loadResult.spec === undefined) return loadResult.diagnostics;

  const result = emitMqMessages(loadResult.spec);
  if (result.diagnostics.some((d) => d.severity === "error"))
    return result.diagnostics;

  writeGeneratedFile(TARGET_PATH, result.source);
  return result.diagnostics;
}

const isMain =
  import.meta.url === `file://${process.argv[1]}` ||
  process.argv[1]?.endsWith("mq-messages-emit.ts") === true;
if (isMain) {
  const force = process.argv.includes("--force");
  const diagnostics = runMqMessagesEmit(force);
  for (const d of diagnostics) console.error(formatDiagnostic(d));
  if (diagnostics.some((d) => d.severity === "error")) process.exit(1);
}
/* v8 ignore stop */
