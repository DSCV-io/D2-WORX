// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";

import {
  emitMqMessages,
  type MqMessagesSpec,
  validateMqMessagesSpec,
} from "../src/mq-messages-emit.js";

const validSpec: MqMessagesSpec = {
  messages: [
    {
      constant: "AuthKeyRotated",
      messageType: "DcsvIo.D2.Auth.Events.KeyRotatedEvent",
      exchange: "d2.security.key-rotated",
      exchangeType: "fanout",
      encryption: "plaintext",
      encryptionReason:
        "Rotation events deliver the (domain, kid) tuple consumers need.",
      defaultRoutingKey: "",
    },
  ],
};

describe("validateMqMessagesSpec", () => {
  it("happy path returns all entries with no error diagnostics", () => {
    const v = validateMqMessagesSpec(validSpec);
    expect(v.messages).toHaveLength(1);
    expect(v.diagnostics.filter((d) => d.severity === "error")).toEqual([]);
  });

  it("flags invalid constant (not PascalCase)", () => {
    const v = validateMqMessagesSpec({
      messages: [{ ...validSpec.messages[0]!, constant: "not_pascal" }],
    });
    expect(v.diagnostics[0]?.id).toBe("D2MQ002");
    expect(v.messages).toHaveLength(0);
  });

  it("flags empty messageType", () => {
    const v = validateMqMessagesSpec({
      messages: [{ ...validSpec.messages[0]!, messageType: "  " }],
    });
    expect(v.diagnostics[0]?.id).toBe("D2MQ006");
  });

  it("flags empty exchange", () => {
    const v = validateMqMessagesSpec({
      messages: [{ ...validSpec.messages[0]!, exchange: "" }],
    });
    expect(v.diagnostics[0]?.id).toBe("D2MQ006");
  });

  it("flags empty exchangeType", () => {
    const v = validateMqMessagesSpec({
      messages: [{ ...validSpec.messages[0]!, exchangeType: "" }],
    });
    expect(v.diagnostics[0]?.id).toBe("D2MQ006");
  });

  it("flags missing/empty encryption (default-deny)", () => {
    const v = validateMqMessagesSpec({
      messages: [{ ...validSpec.messages[0]!, encryption: "" }],
    });
    expect(v.diagnostics[0]?.id).toBe("D2MQ004");
  });

  it("flags duplicate constant", () => {
    const v = validateMqMessagesSpec({
      messages: [
        validSpec.messages[0]!,
        {
          ...validSpec.messages[0]!,
          messageType: "DcsvIo.D2.Auth.Events.OtherEvent",
        },
      ],
    });
    expect(v.diagnostics.some((d) => d.id === "D2MQ003")).toBe(true);
  });

  it("flags duplicate messageType", () => {
    const v = validateMqMessagesSpec({
      messages: [
        validSpec.messages[0]!,
        { ...validSpec.messages[0]!, constant: "AuthKeyRotatedTwin" },
      ],
    });
    expect(v.diagnostics.some((d) => d.id === "D2MQ005")).toBe(true);
  });
});

describe("emitMqMessages — snapshot pin", () => {
  it("emits the descriptor mirror with constant + registry + interface", () => {
    const r = emitMqMessages(validSpec);
    expect(r.diagnostics.filter((d) => d.severity === "error")).toEqual([]);
    expect(r.source).toContain("export interface MqMessageDescriptor");
    expect(r.source).toContain("export const MqMessages = {");
    expect(r.source).toContain('AuthKeyRotated: "AuthKeyRotated"');
    expect(r.source).toContain("export type MqMessage");
    expect(r.source).toContain(
      "export const MqMessagesRegistry: Readonly<Record<string, MqMessageDescriptor>>",
    );
    expect(r.source).toContain('exchange: "d2.security.key-rotated"');
    expect(r.source).toContain('exchangeType: "fanout"');
    expect(r.source).toContain('encryption: "plaintext"');
    expect(r.source).toContain(
      'messageType: "DcsvIo.D2.Auth.Events.KeyRotatedEvent"',
    );
    expect(r.source).toContain('defaultRoutingKey: ""');
    expect(r.source).toContain("ALL_MQ_MESSAGE_CONSTANTS");
  });

  it("emits the literal-typed MqMessagesCatalog (publisher type-witness input)", () => {
    const r = emitMqMessages(validSpec);
    // The catalog is `as const` (literal types), distinct from the
    // Readonly<Record> registry — the publisher reads each message's literal
    // `encryption` to brand its composer slot.
    expect(r.source).toContain("export const MqMessagesCatalog = {");
    expect(r.source).toContain("} as const;");
    expect(r.source).toContain(
      "export type MqMessageCatalogKey = keyof typeof MqMessagesCatalog;",
    );
  });

  it("omits encryptionReason key when absent (encrypted domain)", () => {
    const r = emitMqMessages({
      messages: [
        {
          constant: "AuditWritten",
          messageType: "DcsvIo.D2.Private.Audit.Events.AuditWritten",
          exchange: "d2.audit.written",
          exchangeType: "fanout",
          encryption: "audit",
        },
      ],
    });
    expect(r.source).toContain('encryption: "audit"');
    // No registry value-assignment for the omitted optional fields (the
    // interface still DECLARES them as optional; only the emitted descriptor
    // object must omit them). Distinguish `encryptionReason: "..."` (value)
    // from `encryptionReason?: string;` (interface field).
    expect(r.source).not.toContain('encryptionReason: "');
    expect(r.source).not.toContain('defaultRoutingKey: "');
  });

  it("emits a @deprecated JSDoc tag for a deprecated entry", () => {
    const r = emitMqMessages({
      messages: [
        {
          ...validSpec.messages[0]!,
          deprecated: true,
          deprecatedReason: "Superseded by AuthKeyRolled.",
        },
      ],
    });
    expect(r.source).toContain("@deprecated Superseded by AuthKeyRolled.");
  });

  it("emits a bare @deprecated tag for a deprecated entry with no reason", () => {
    // `deprecated: true` with no `deprecatedReason` — exercises the TRUE arm of
    // the emit loop's `isEmpty(e.deprecatedReason)` guard: the tag is written
    // with no trailing reason text.
    const r = emitMqMessages({
      messages: [{ ...validSpec.messages[0]!, deprecated: true }],
    });
    expect(r.diagnostics.filter((d) => d.severity === "error")).toEqual([]);
    expect(r.source).toContain("* @deprecated");
    // No reason → nothing follows the tag on the line.
    expect(r.source).not.toContain("@deprecated ");
  });

  it("returns empty source on validation error", () => {
    const r = emitMqMessages({
      messages: [{ ...validSpec.messages[0]!, constant: "bad-name" }],
    });
    expect(r.source).toBe("");
    expect(r.diagnostics.some((d) => d.id === "D2MQ002")).toBe(true);
  });
});
