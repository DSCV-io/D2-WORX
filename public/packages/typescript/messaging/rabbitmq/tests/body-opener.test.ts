// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { DlqFailureCauses } from "@d2/messaging-abstractions";
import { EncryptionFrame, SealedFrame } from "@d2/encryption-abstractions";
import { describe, expect, it } from "vitest";

import { PlaintextBodyOpener } from "../src/subscribing/body-opener.js";
import { MessageBodyDecodeError } from "../src/subscribing/message-body-decode-error.js";

describe("PlaintextBodyOpener", () => {
  const opener = new PlaintextBodyOpener();

  it("parses a valid UTF-8 JSON body", () => {
    const body = Buffer.from(
      JSON.stringify({ domain: "audit", kid: "k1" }),
      "utf8",
    );
    expect(opener.open(body)).toEqual({ domain: "audit", kid: "k1" });
  });

  it("FAIL-LOUDS a symmetric encryption frame (version 1) as DECRYPT_FAILURE", () => {
    const body = Buffer.from([
      EncryptionFrame.CURRENT_VERSION,
      0x02,
      0x6b,
      0x31,
    ]);
    try {
      opener.open(body);
      expect.unreachable("expected a MessageBodyDecodeError");
    } catch (err) {
      expect(err).toBeInstanceOf(MessageBodyDecodeError);
      expect((err as MessageBodyDecodeError).decodeCause).toBe(
        DlqFailureCauses.DECRYPT_FAILURE,
      );
    }
  });

  it("FAIL-LOUDS a sealed encryption frame (version 2) as DECRYPT_FAILURE", () => {
    const body = Buffer.from([SealedFrame.CURRENT_VERSION, 0x02, 0x6b, 0x31]);
    expect(() => opener.open(body)).toThrowError(MessageBodyDecodeError);
  });

  it("routes a garbage (non-JSON) body as DESERIALIZE_FAILURE", () => {
    const body = Buffer.from("{ this is not json", "utf8");
    try {
      opener.open(body);
      expect.unreachable("expected a MessageBodyDecodeError");
    } catch (err) {
      const decodeError = err as MessageBodyDecodeError;
      expect(decodeError.decodeCause).toBe(
        DlqFailureCauses.DESERIALIZE_FAILURE,
      );
      expect(decodeError.isDeserializeFailure).toBe(true);
      expect(decodeError.cause).toBeInstanceOf(Error);
    }
  });

  it("routes an empty body as DESERIALIZE_FAILURE (empty is not valid JSON)", () => {
    expect(() => opener.open(Buffer.alloc(0))).toThrowError(
      MessageBodyDecodeError,
    );
  });
});
