// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { webcrypto } from "node:crypto";

import {
  PayloadCrypto,
  PayloadCryptoKeyring,
  PayloadOpener,
  PayloadSealer,
  RecipientPrivateKeyring,
  RecipientPublicKeyring,
} from "@dcsv-io/d2-encryption";
import { AmqpHeaders } from "@dcsv-io/d2-headers-amqp";
import {
  MqMessagesRegistry,
  type MqMessageDescriptor,
} from "@dcsv-io/d2-messaging-abstractions";
import type { Connection } from "rabbitmq-client";
import { describe, expect, it } from "vitest";

import { composeBody } from "../src/publishing/body-composer.js";
import { readEncryptionKid } from "../src/publishing/encryption-kid.js";
import {
  createPublisher,
  type PublishEnvelope,
  type PublishSender,
  publishVia,
} from "../src/publishing/publisher.js";
import { RecordingLogger } from "./helpers.js";

const utf8 = new TextEncoder();

async function makeSealerOpener(
  serviceId: string,
  kid: string,
): Promise<{ sealer: PayloadSealer; opener: PayloadOpener }> {
  const pair = await webcrypto.subtle.generateKey(
    { name: "ECDH", namedCurve: "P-256" },
    true,
    ["deriveBits"],
  );
  const spki = new Uint8Array(
    await webcrypto.subtle.exportKey("spki", pair.publicKey),
  );
  const pkcs8 = new Uint8Array(
    await webcrypto.subtle.exportKey("pkcs8", pair.privateKey),
  );
  const pub = await RecipientPublicKeyring.create(
    serviceId,
    kid,
    new Map([[kid, spki]]),
  );
  const priv = await RecipientPrivateKeyring.create(
    serviceId,
    new Map([[kid, pkcs8]]),
  );

  return { sealer: new PayloadSealer(pub), opener: new PayloadOpener(priv) };
}

function makeSymmetric(kid: string): PayloadCrypto {
  return new PayloadCrypto(
    new PayloadCryptoKeyring(
      kid,
      new Map([[kid, new Uint8Array(32).fill(7)]]),
      utf8.encode("d2/audit"),
    ),
  );
}

function recordingSender(): {
  sender: PublishSender;
  calls: { envelope: PublishEnvelope; body: Buffer }[];
} {
  const calls: { envelope: PublishEnvelope; body: Buffer }[] = [];
  const sender: PublishSender = async (envelope, body) => {
    calls.push({ envelope, body });
  };

  return { sender, calls };
}

const AUDIT_DESCRIPTOR: MqMessageDescriptor = {
  constant: "AuditWrittenFixture",
  messageType: "DcsvIo.D2.Private.Audit.Events.AuditWrittenFixture",
  exchange: "d2.audit.written",
  exchangeType: "fanout",
  encryption: "payload-fixture-sealed",
};

describe("composeBody (runtime default-deny second lock)", () => {
  const plainDescriptor = MqMessagesRegistry["AuthKeyRotated"]!;

  it("passes a plaintext body through unencrypted with no kid", async () => {
    const json = utf8.encode('{"a":1}');
    const composed = await composeBody(plainDescriptor, json, {});
    expect(composed.body).toBe(json);
    expect(composed.kid).toBeUndefined();
  });

  it("seals a sealed-domain message via the wired sealer", async () => {
    const { sealer, opener } = await makeSealerOpener(
      "payload-fixture-sealed",
      "seal-1",
    );
    const json = utf8.encode('{"secret":"x"}');
    const composed = await composeBody(AUDIT_DESCRIPTOR, json, {
      "payload-fixture-sealed": sealer,
    });
    expect(composed.body[0]).toBe(2);
    expect(composed.kid).toBe("seal-1");
    expect(new Uint8Array(await opener.open(composed.body))).toEqual(json);
  });

  it("encrypts a symmetric-mode domain via the wired crypto", async () => {
    // The production catalog has no symmetric-mode domain yet; inject a
    // fixture mode-map so the symmetric compose arm is exercised (it stays
    // available for any future symmetric domain).
    const crypto = makeSymmetric("sym-1");
    const symDescriptor: MqMessageDescriptor = {
      ...AUDIT_DESCRIPTOR,
      encryption: "payload-fixture-a",
    };
    const composed = await composeBody(
      symDescriptor,
      utf8.encode('{"n":1}'),
      { "payload-fixture-a": crypto },
      { "payload-fixture-a": "symmetric" },
    );
    expect(composed.body[0]).toBe(1);
    expect(composed.kid).toBe("sym-1");
    expect(new Uint8Array(await crypto.decrypt(composed.body))).toEqual(
      utf8.encode('{"n":1}'),
    );
  });

  it("default-denies an encrypted domain with no wired composer", async () => {
    await expect(
      composeBody(AUDIT_DESCRIPTOR, utf8.encode("{}"), {}),
    ).rejects.toThrow(/No composer wired/);
  });

  it("default-denies an unknown encryption domain", async () => {
    const bogus: MqMessageDescriptor = {
      ...AUDIT_DESCRIPTOR,
      encryption: "not-a-domain",
    };
    await expect(composeBody(bogus, utf8.encode("{}"), {})).rejects.toThrow(
      /not a known encryption domain/,
    );
  });
});

describe("publishVia", () => {
  it("default-denies an unknown message constant", async () => {
    const { sender, calls } = recordingSender();
    const logger = new RecordingLogger();
    const result = await publishVia(
      sender,
      MqMessagesRegistry,
      {},
      logger,
      "NoSuchMessage",
      {},
    );
    expect(result.failed).toBe(true);
    expect(calls).toHaveLength(0);
    expect(logger.logged("unknown message constant")).toBe(true);
  });

  it("publishes a plaintext message with the right envelope + no kid header", async () => {
    const { sender, calls } = recordingSender();
    const result = await publishVia(
      sender,
      MqMessagesRegistry,
      {},
      new RecordingLogger(),
      "AuthKeyRotated",
      { domain: "payload-fixture-sealed", kid: "audit-1" },
    );
    expect(result.failed).toBe(false);
    expect(calls).toHaveLength(1);
    const { envelope, body } = calls[0]!;
    expect(envelope.exchange).toBe("d2.security.key-rotated");
    expect(envelope.routingKey).toBe("");
    expect(envelope.contentType).toBe("application/octet-stream");
    expect(envelope.headers[AmqpHeaders.PROTO_TYPE]).toBe(
      "DcsvIo.D2.Auth.Events.KeyRotatedEvent",
    );
    expect(envelope.headers[AmqpHeaders.ENCRYPTION_KID]).toBeUndefined();
    expect(envelope.messageId).toMatch(
      /^[0-9a-f]{8}-[0-9a-f]{4}-7[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/,
    );
    expect(JSON.parse(body.toString("utf8"))).toEqual({
      domain: "payload-fixture-sealed",
      kid: "audit-1",
    });
  });

  it("seals + publishes a sealed message with the encryption-kid header (compose-once)", async () => {
    const { sealer, opener } = await makeSealerOpener(
      "payload-fixture-sealed",
      "seal-1",
    );
    const { sender, calls } = recordingSender();
    const registry = {
      ...MqMessagesRegistry,
      AuditWrittenFixture: AUDIT_DESCRIPTOR,
    };

    const result = await publishVia(
      sender,
      registry,
      { "payload-fixture-sealed": sealer },
      new RecordingLogger(),
      "AuditWrittenFixture",
      { secret: "value" },
    );

    expect(result.failed).toBe(false);
    expect(calls).toHaveLength(1);
    const { envelope, body } = calls[0]!;
    expect(envelope.exchange).toBe("d2.audit.written");
    expect(envelope.headers[AmqpHeaders.ENCRYPTION_KID]).toBe("seal-1");
    expect(body[0]).toBe(2);
    // Compose-once: the single composed body opens to the message.
    expect(
      JSON.parse(Buffer.from(await opener.open(body)).toString("utf8")),
    ).toEqual({ secret: "value" });
  });

  it("maps a broker send failure to a service-unavailable result", async () => {
    const logger = new RecordingLogger();
    const failing: PublishSender = () =>
      Promise.reject(new Error("broker down"));
    const result = await publishVia(
      failing,
      MqMessagesRegistry,
      {},
      logger,
      "AuthKeyRotated",
      {},
    );
    expect(result.failed).toBe(true);
    expect(logger.logged("publish failed")).toBe(true);
  });

  it("bounds a wedged broker confirm and maps it to a transient failure", async () => {
    const logger = new RecordingLogger();
    // A never-resolving send models a broker confirm that never acks.
    const wedged: PublishSender = () => new Promise<void>(() => {});
    const result = await publishVia(
      wedged,
      MqMessagesRegistry,
      {},
      logger,
      "AuthKeyRotated",
      {},
      20, // 20ms confirm bound — a wedged confirm must not hang forever
    );

    expect(result.failed).toBe(true);
    expect(logger.logged("publish confirm timed out")).toBe(true);
  });

  it("createPublisher wires a confirm-publisher and publishes + closes through it", async () => {
    const sent: { envelope: PublishEnvelope; body: Buffer }[] = [];
    let closed = false;
    const fakePublisher = {
      send: async (envelope: PublishEnvelope, body: Buffer) => {
        sent.push({ envelope, body });
      },
      close: async () => {
        closed = true;
      },
    };
    const fakeConnection = {
      createPublisher: () => fakePublisher,
    } as unknown as Connection;

    const publisher = createPublisher(fakeConnection, {
      crypto: {},
      logger: new RecordingLogger(),
    });
    const result = await publisher.publish("AuthKeyRotated", {
      domain: "payload-fixture-sealed",
    });

    expect(result.failed).toBe(false);
    expect(sent).toHaveLength(1);
    expect(sent[0]!.envelope.exchange).toBe("d2.security.key-rotated");

    await publisher.close();
    expect(closed).toBe(true);
  });
});

describe("readEncryptionKid", () => {
  it("reads the kid from v1 and v2 frames and fails loud otherwise", async () => {
    const crypto = makeSymmetric("v1-kid");
    expect(readEncryptionKid(await crypto.encrypt(utf8.encode("x")))).toBe(
      "v1-kid",
    );

    const { sealer } = await makeSealerOpener(
      "payload-fixture-sealed",
      "v2-kid",
    );
    expect(readEncryptionKid(await sealer.seal(utf8.encode("x")))).toBe(
      "v2-kid",
    );

    expect(() => readEncryptionKid(new Uint8Array([2]))).toThrow(/too short/);
    expect(() => readEncryptionKid(new Uint8Array([9, 1, 65]))).toThrow(
      /Unknown encryption frame version/,
    );
    expect(() => readEncryptionKid(new Uint8Array([1, 40, 65]))).toThrow(
      /too short for declared kid/,
    );
  });
});
