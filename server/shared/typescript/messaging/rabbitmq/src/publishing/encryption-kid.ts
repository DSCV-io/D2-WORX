// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { EncryptionFrame, SealedFrame } from "@d2/encryption-abstractions";

const _decoder = new TextDecoder("utf-8");

/**
 * Extracts the kid from an encryption frame's header (used to populate the
 * `x-d2-encryption-kid` AMQP header for DLQ triage). The twin of .NET
 * `EncryptedBodyComposer.ReadKidFromFrame`: a symmetric (v1) frame yields its
 * keyring kid; a sealed (v2) frame yields its recipient kid — both share the
 * `[version:1][kid_len:1][kid:UTF-8]` prefix, so the read is identical and only
 * the version dispatch differs. An unknown version fails loud rather than
 * emitting a garbage triage header.
 *
 * @param frame The frame produced by a composer's encrypt/seal.
 * @returns The kid string from the frame header.
 * @throws When the frame is too short or carries an unknown version.
 */
export function readEncryptionKid(frame: Uint8Array): string {
  if (frame.length < 2) {
    throw new Error("Frame too short to read kid header.");
  }

  const view = new DataView(frame.buffer, frame.byteOffset, frame.byteLength);
  const version = view.getUint8(0);

  if (
    version !== EncryptionFrame.CURRENT_VERSION &&
    version !== SealedFrame.CURRENT_VERSION
  ) {
    throw new Error(
      `Unknown encryption frame version: ${version}. Expected ` +
        `${EncryptionFrame.CURRENT_VERSION} (symmetric) or ` +
        `${SealedFrame.CURRENT_VERSION} (sealed).`,
    );
  }

  const kidLen = view.getUint8(1);

  if (frame.length < 2 + kidLen) {
    throw new Error("Frame too short for declared kid length.");
  }

  return _decoder.decode(frame.subarray(2, 2 + kidLen));
}
