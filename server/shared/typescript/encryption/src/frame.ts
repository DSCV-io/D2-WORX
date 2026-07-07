// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { EncryptionFrame } from "@d2/encryption-abstractions";

import { FrameMalformedError, FrameVersionMismatchError } from "./errors.js";

/**
 * Codec for the on-wire SYMMETRIC (version-1) encryption frame — the twin of
 * .NET `D2.Shared.Encryption.EncryptionFrame`. Layout:
 * `[version=1:1][kid_len:1][kid:UTF-8 N][nonce:12][ciphertext+tag:M]`.
 *
 * Version dispatch is structural: this codec hard-rejects the sealed (v2)
 * version byte, so the two formats can never cross-parse.
 */

const _utf8Encoder = new TextEncoder();

// v1 kid decode mirrors .NET `Encoding.UTF8.GetString` (lenient replacement),
// NOT the strict decode the sealed frame uses.
const _utf8LenientDecoder = new TextDecoder("utf-8");

/** Decoded view over a version-1 frame buffer. Spans alias the source. */
export interface FrameView {
  /** The frame's version byte. */
  readonly version: number;
  /** The kid declared in the frame header. */
  readonly kid: string;
  /** The GCM nonce. */
  readonly nonce: Uint8Array;
  /** The ciphertext bytes followed by the auth tag. */
  readonly ciphertextWithTag: Uint8Array;
}

/**
 * Encodes a version-1 frame around an already-encrypted ciphertext+tag span.
 *
 * @param kid The kid to embed in the frame header.
 * @param nonce The nonce used for encryption (exactly 12 bytes).
 * @param ciphertextWithTag The encrypted bytes followed by the auth tag.
 * @returns The complete frame.
 */
export function encodeFrame(
  kid: string,
  nonce: Uint8Array,
  ciphertextWithTag: Uint8Array,
): Uint8Array {
  const kidBytes = _utf8Encoder.encode(kid);

  if (
    kidBytes.length < EncryptionFrame.CONSTRAINT_MIN_KID_LENGTH ||
    kidBytes.length > EncryptionFrame.CONSTRAINT_MAX_KID_LENGTH
  ) {
    throw new RangeError(
      `kid UTF-8 byte length must be in [${EncryptionFrame.CONSTRAINT_MIN_KID_LENGTH}, ` +
        `${EncryptionFrame.CONSTRAINT_MAX_KID_LENGTH}].`,
    );
  }

  if (nonce.length !== EncryptionFrame.CONSTRAINT_NONCE_LENGTH) {
    throw new RangeError(
      `nonce must be exactly ${EncryptionFrame.CONSTRAINT_NONCE_LENGTH} bytes ` +
        `(got ${nonce.length}).`,
    );
  }

  if (ciphertextWithTag.length < EncryptionFrame.CONSTRAINT_TAG_LENGTH) {
    throw new RangeError(
      `ciphertextWithTag must be at least ${EncryptionFrame.CONSTRAINT_TAG_LENGTH} ` +
        "bytes (the tag).",
    );
  }

  const total =
    EncryptionFrame.VERSION_LENGTH +
    EncryptionFrame.KID_LENGTH_LENGTH +
    kidBytes.length +
    EncryptionFrame.CONSTRAINT_NONCE_LENGTH +
    ciphertextWithTag.length;
  const frame = new Uint8Array(total);

  frame[EncryptionFrame.VERSION_OFFSET] = EncryptionFrame.CURRENT_VERSION;
  frame[EncryptionFrame.KID_LENGTH_OFFSET] = kidBytes.length;
  frame.set(kidBytes, EncryptionFrame.KID_OFFSET);

  const nonceOffset = EncryptionFrame.KID_OFFSET + kidBytes.length;
  frame.set(nonce, nonceOffset);
  frame.set(
    ciphertextWithTag,
    nonceOffset + EncryptionFrame.CONSTRAINT_NONCE_LENGTH,
  );

  return frame;
}

/**
 * Decodes a version-1 frame, returning component views into the input.
 *
 * @param framed The complete frame buffer.
 * @returns A view of the parsed components, aliasing `framed`.
 * @throws {FrameMalformedError} On any structural error.
 * @throws {FrameVersionMismatchError} When the version byte is not 1.
 */
export function decodeFrame(framed: Uint8Array): FrameView {
  if (framed.length < EncryptionFrame.CONSTRAINT_MIN_FRAME_SIZE) {
    throw new FrameMalformedError(
      `Frame too short: ${framed.length} bytes ` +
        `(min ${EncryptionFrame.CONSTRAINT_MIN_FRAME_SIZE}).`,
    );
  }

  const view = new DataView(
    framed.buffer,
    framed.byteOffset,
    framed.byteLength,
  );
  const version = view.getUint8(EncryptionFrame.VERSION_OFFSET);

  if (version !== EncryptionFrame.CURRENT_VERSION) {
    throw new FrameVersionMismatchError(version);
  }

  const kidLength = view.getUint8(EncryptionFrame.KID_LENGTH_OFFSET);

  if (kidLength < EncryptionFrame.CONSTRAINT_MIN_KID_LENGTH) {
    throw new FrameMalformedError(
      `Frame kid_length ${kidLength} is below minimum ` +
        `${EncryptionFrame.CONSTRAINT_MIN_KID_LENGTH}.`,
    );
  }

  const headerSize =
    EncryptionFrame.VERSION_LENGTH +
    EncryptionFrame.KID_LENGTH_LENGTH +
    kidLength +
    EncryptionFrame.CONSTRAINT_NONCE_LENGTH;

  if (framed.length < headerSize + EncryptionFrame.CONSTRAINT_TAG_LENGTH) {
    throw new FrameMalformedError(
      `Frame too short for declared kid_length=${kidLength}: have ${framed.length}, ` +
        `need >= ${headerSize + EncryptionFrame.CONSTRAINT_TAG_LENGTH}.`,
    );
  }

  const kid = _utf8LenientDecoder.decode(
    framed.subarray(
      EncryptionFrame.KID_OFFSET,
      EncryptionFrame.KID_OFFSET + kidLength,
    ),
  );
  const nonceOffset = EncryptionFrame.KID_OFFSET + kidLength;

  return {
    version,
    kid,
    nonce: framed.subarray(
      nonceOffset,
      nonceOffset + EncryptionFrame.CONSTRAINT_NONCE_LENGTH,
    ),
    ciphertextWithTag: framed.subarray(
      nonceOffset + EncryptionFrame.CONSTRAINT_NONCE_LENGTH,
    ),
  };
}
