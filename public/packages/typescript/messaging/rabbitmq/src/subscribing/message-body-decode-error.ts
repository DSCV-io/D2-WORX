// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import {
  type DlqFailureCause,
  DlqFailureCauses,
} from "@d2/messaging-abstractions";

/**
 * Raised by a {@link BodyOpener} when it cannot faithfully decode a delivery
 * body. Carries the DLQ cause discriminator so the consumer routes the message
 * to its dead-letter queue with the correct `DlqFailureMetadata.cause`
 * (`DESERIALIZE_FAILURE` for a JSON-parse failure, `DECRYPT_FAILURE` for an
 * encrypted body with no registered opener) — the twin of the .NET
 * `MessageBodyDecodeException`.
 */
export class MessageBodyDecodeError extends Error {
  /** The DLQ cause (`DESERIALIZE_FAILURE` or `DECRYPT_FAILURE`). */
  readonly decodeCause: DlqFailureCause;

  /**
   * @param decodeCause The DLQ cause discriminator.
   * @param message Operator-facing summary (never carries body bytes / PII).
   * @param innerError The underlying error, when one exists.
   */
  constructor(
    decodeCause: DlqFailureCause,
    message: string,
    innerError?: unknown,
  ) {
    super(
      message,
      innerError === undefined ? undefined : { cause: innerError },
    );
    this.name = "MessageBodyDecodeError";
    this.decodeCause = decodeCause;
  }

  /** True when the cause is a JSON-deserialization failure. */
  get isDeserializeFailure(): boolean {
    return this.decodeCause === DlqFailureCauses.DESERIALIZE_FAILURE;
  }
}
