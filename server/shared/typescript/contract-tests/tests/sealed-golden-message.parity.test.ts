// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import { PayloadOpener, RecipientPrivateKeyring } from "@d2/encryption";
import { loadFixture } from "../src/index.js";

// The .NET → TS sealed golden-MESSAGE gate. The fixture body is a REAL sealed
// (version-2) message composed by the production .NET EncryptedBodyComposer.Compose
// sealed branch (MqGoldenMessageFixtureEmitter.Emit_SealedAuditMessage), so opening
// it here proves the TS consumer decodes a genuinely .NET-composed sealed body — the
// message-layer twin of the sealed-crypto-kat raw-frame gate.
//
// It opens through the exact production consume path @d2/messaging-rabbitmq's
// CryptoBodyOpener runs (PayloadOpener.open → UTF-8 → JSON.parse); the wrapper is
// replicated inline (not imported) because contract-tests depends on @d2/encryption
// but not @d2/messaging-rabbitmq, keeping this suite dependency-faithful to its
// sealed-crypto-kat sibling. CryptoBodyOpener itself is covered in
// @d2/messaging-rabbitmq's own crypto-body-opener.test.ts.
interface SealedGoldenMessage {
  readonly domain: string;
  readonly consumerService: string;
  readonly recipientServiceId: string;
  readonly recipientKid: string;
  readonly recipientPrivatePkcs8Base64: string;
  readonly bodyBase64: string;
  readonly headers: Readonly<Record<string, string>>;
  readonly expectedDecoded: Readonly<Record<string, unknown>>;
}

const fromB64 = (b64: string): Uint8Array =>
  new Uint8Array(Buffer.from(b64, "base64"));

describe("sealed golden message parity (.NET EncryptedBodyComposer ↔ TS PayloadOpener)", () => {
  const golden = loadFixture<SealedGoldenMessage>(
    "mq-messages-golden",
    "sealed-audit-message",
  ).data;

  it("opens a real .NET-composed sealed message body byte-for-byte", async () => {
    const ring = await RecipientPrivateKeyring.create(
      golden.recipientServiceId,
      new Map([
        [golden.recipientKid, fromB64(golden.recipientPrivatePkcs8Base64)],
      ]),
    );
    const opener = new PayloadOpener(ring);

    const frame = fromB64(golden.bodyBase64);
    expect(frame[0]).toBe(2); // sealed version-2 frame

    const opened = await opener.open(frame);
    const decoded = JSON.parse(
      new TextDecoder("utf-8").decode(opened),
    ) as Record<string, unknown>;

    expect(decoded).toEqual(golden.expectedDecoded);
  });
});
