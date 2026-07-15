// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { DlqFailureCauses } from "@dcsv-io/d2-messaging-abstractions";
import {
  EncryptionFrame,
  SealedFrame,
} from "@dcsv-io/d2-encryption-abstractions";

import { MessageBodyDecodeError } from "./message-body-decode-error.js";

/**
 * Decodes a raw AMQP delivery body into the object a handler consumes. An
 * injectable seam: the default handles plaintext domains; an encrypted-domain
 * opener (WebCrypto decrypt) plugs in later (its replace-trigger is tracked in
 * the ledger). An opener MUST throw {@link MessageBodyDecodeError} — never
 * silently swallow — for any body it cannot faithfully decode.
 */
export interface BodyOpener {
  /**
   * Decodes the raw body bytes into the handler's message value. May be async —
   * a decrypting opener (WebCrypto) is inherently async; the delivery pipeline
   * awaits the result. The default plaintext opener stays synchronous.
   *
   * @param body Raw AMQP body bytes (opaque `application/octet-stream`).
   * @throws MessageBodyDecodeError on an undecodable body.
   */
  open(body: Buffer): unknown | Promise<unknown>;
}

/**
 * The default {@link BodyOpener}: plaintext domains only. It parses a raw
 * UTF-8 JSON body and — critically — FAIL-LOUDS any body whose first byte is a
 * known encryption-frame version (1 = symmetric {@link EncryptionFrame}, 2 =
 * sealed {@link SealedFrame}). An encrypted body reaching this opener means no
 * decrypting opener was registered for the domain, so it routes to the DLQ with
 * `DECRYPT_FAILURE` rather than mis-parsing ciphertext as JSON (never a silent
 * drop). A malformed plaintext body routes with `DESERIALIZE_FAILURE`.
 */
export class PlaintextBodyOpener implements BodyOpener {
  open(body: Buffer): unknown {
    if (body.length > 0) {
      const firstByte = body[0];
      if (
        firstByte === EncryptionFrame.CURRENT_VERSION ||
        firstByte === SealedFrame.CURRENT_VERSION
      ) {
        throw new MessageBodyDecodeError(
          DlqFailureCauses.DECRYPT_FAILURE,
          "encrypted body received on a plaintext opener (no decrypting " +
            "opener registered for this domain)",
        );
      }
    }

    const text = body.toString("utf8");
    try {
      return JSON.parse(text);
    } catch (err) {
      throw new MessageBodyDecodeError(
        DlqFailureCauses.DESERIALIZE_FAILURE,
        "message body is not valid UTF-8 JSON",
        err,
      );
    }
  }
}
