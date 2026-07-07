// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { SealedFrame } from "@d2/encryption-abstractions";

import { FrameMalformedError, FrameVersionMismatchError } from "./errors.js";

/**
 * Codec for the on-wire SEALED (version-2, asymmetric) encryption frame — the
 * twin of .NET `D2.Shared.Encryption.SealedFrame`. Layout:
 * `[version=2:1][recipient_kid_len:1][recipient_kid:UTF-8 N][eph_pub_len:2 BE]`
 * `[eph_pub:SPKI M][nonce:12][ciphertext+tag:K]`.
 *
 * Version dispatch is structural: this codec hard-rejects the symmetric (v1)
 * version byte, so the two formats can never cross-parse.
 */

const _utf8Encoder = new TextEncoder();

// Strict decoder: an invalid UTF-8 recipient kid must THROW (surface as
// FrameMalformedError), never silently decode to U+FFFD replacement characters
// that would then fail keyring lookup with a misleading kid.
const _utf8StrictDecoder = new TextDecoder("utf-8", { fatal: true });

/** Decoded view over a version-2 sealed frame buffer. Spans alias the source. */
export interface SealedFrameView {
  /** The frame's version byte. */
  readonly version: number;
  /** The recipient kid declared in the frame header. */
  readonly recipientKid: string;
  /** The ephemeral public key bytes (SubjectPublicKeyInfo DER). */
  readonly ephemeralPublicSpki: Uint8Array;
  /** The GCM nonce. */
  readonly nonce: Uint8Array;
  /** The ciphertext bytes followed by the auth tag. */
  readonly ciphertextWithTag: Uint8Array;
}

/**
 * Encodes a sealed frame around an already-encrypted ciphertext+tag span.
 *
 * @param recipientKid The recipient kid to embed in the frame header.
 * @param ephemeralPublicSpki The per-message ephemeral public key (SPKI DER).
 * @param nonce The nonce used for encryption (exactly 12 bytes).
 * @param ciphertextWithTag The encrypted bytes followed by the auth tag.
 * @returns The complete sealed frame.
 */
export function encodeSealedFrame(
  recipientKid: string,
  ephemeralPublicSpki: Uint8Array,
  nonce: Uint8Array,
  ciphertextWithTag: Uint8Array,
): Uint8Array {
  const kidBytes = _utf8Encoder.encode(recipientKid);

  if (
    kidBytes.length < SealedFrame.CONSTRAINT_MIN_KID_LENGTH ||
    kidBytes.length > SealedFrame.CONSTRAINT_MAX_KID_LENGTH
  ) {
    throw new RangeError(
      `recipientKid UTF-8 byte length must be in ` +
        `[${SealedFrame.CONSTRAINT_MIN_KID_LENGTH}, ${SealedFrame.CONSTRAINT_MAX_KID_LENGTH}].`,
    );
  }

  if (
    ephemeralPublicSpki.length < 1 ||
    ephemeralPublicSpki.length > SealedFrame.CONSTRAINT_MAX_EPH_PUB_LENGTH
  ) {
    throw new RangeError(
      `ephemeralPublicSpki byte length must be in ` +
        `[1, ${SealedFrame.CONSTRAINT_MAX_EPH_PUB_LENGTH}] (got ${ephemeralPublicSpki.length}).`,
    );
  }

  if (nonce.length !== SealedFrame.CONSTRAINT_NONCE_LENGTH) {
    throw new RangeError(
      `nonce must be exactly ${SealedFrame.CONSTRAINT_NONCE_LENGTH} bytes ` +
        `(got ${nonce.length}).`,
    );
  }

  if (ciphertextWithTag.length < SealedFrame.CONSTRAINT_TAG_LENGTH) {
    throw new RangeError(
      `ciphertextWithTag must be at least ${SealedFrame.CONSTRAINT_TAG_LENGTH} ` +
        "bytes (the tag).",
    );
  }

  const total =
    SealedFrame.VERSION_LENGTH +
    SealedFrame.RECIPIENT_KID_LENGTH_LENGTH +
    kidBytes.length +
    SealedFrame.EPH_PUB_LENGTH_LENGTH +
    ephemeralPublicSpki.length +
    SealedFrame.CONSTRAINT_NONCE_LENGTH +
    ciphertextWithTag.length;
  const frame = new Uint8Array(total);
  const view = new DataView(frame.buffer);

  frame[SealedFrame.VERSION_OFFSET] = SealedFrame.CURRENT_VERSION;
  frame[SealedFrame.RECIPIENT_KID_LENGTH_OFFSET] = kidBytes.length;
  frame.set(kidBytes, SealedFrame.RECIPIENT_KID_OFFSET);

  const ephLenOffset = SealedFrame.RECIPIENT_KID_OFFSET + kidBytes.length;
  view.setUint16(ephLenOffset, ephemeralPublicSpki.length, false);

  const ephOffset = ephLenOffset + SealedFrame.EPH_PUB_LENGTH_LENGTH;
  frame.set(ephemeralPublicSpki, ephOffset);

  const nonceOffset = ephOffset + ephemeralPublicSpki.length;
  frame.set(nonce, nonceOffset);
  frame.set(
    ciphertextWithTag,
    nonceOffset + SealedFrame.CONSTRAINT_NONCE_LENGTH,
  );

  return frame;
}

/**
 * Decodes a sealed frame, returning component views into the input.
 *
 * @param framed The complete sealed frame buffer.
 * @returns A view of the parsed components, aliasing `framed`.
 * @throws {FrameMalformedError} On any structural error.
 * @throws {FrameVersionMismatchError} When the version byte is not 2.
 */
export function decodeSealedFrame(framed: Uint8Array): SealedFrameView {
  if (framed.length < SealedFrame.CONSTRAINT_MIN_FRAME_SIZE) {
    throw new FrameMalformedError(
      `Sealed frame too short: ${framed.length} bytes ` +
        `(min ${SealedFrame.CONSTRAINT_MIN_FRAME_SIZE}).`,
    );
  }

  const view = new DataView(
    framed.buffer,
    framed.byteOffset,
    framed.byteLength,
  );
  const version = view.getUint8(SealedFrame.VERSION_OFFSET);

  if (version !== SealedFrame.CURRENT_VERSION) {
    throw new FrameVersionMismatchError(version);
  }

  const kidLength = view.getUint8(SealedFrame.RECIPIENT_KID_LENGTH_OFFSET);

  if (
    kidLength < SealedFrame.CONSTRAINT_MIN_KID_LENGTH ||
    kidLength > SealedFrame.CONSTRAINT_MAX_KID_LENGTH
  ) {
    throw new FrameMalformedError(
      `Sealed frame recipient_kid_length ${kidLength} is outside ` +
        `[${SealedFrame.CONSTRAINT_MIN_KID_LENGTH}, ${SealedFrame.CONSTRAINT_MAX_KID_LENGTH}].`,
    );
  }

  const ephLenOffset = SealedFrame.RECIPIENT_KID_OFFSET + kidLength;

  if (framed.length < ephLenOffset + SealedFrame.EPH_PUB_LENGTH_LENGTH) {
    throw new FrameMalformedError(
      `Sealed frame too short for declared recipient_kid_length=${kidLength}: ` +
        "the eph_pub length prefix overruns the buffer.",
    );
  }

  const ephLength = view.getUint16(ephLenOffset, false);

  if (ephLength < 1) {
    throw new FrameMalformedError(
      "Sealed frame eph_pub_len is zero — an empty ephemeral public key " +
        "cannot exist in a valid frame.",
    );
  }

  if (ephLength > SealedFrame.CONSTRAINT_MAX_EPH_PUB_LENGTH) {
    throw new FrameMalformedError(
      `Sealed frame eph_pub_len ${ephLength} exceeds the cap ` +
        `${SealedFrame.CONSTRAINT_MAX_EPH_PUB_LENGTH}.`,
    );
  }

  const ephOffset = ephLenOffset + SealedFrame.EPH_PUB_LENGTH_LENGTH;
  const nonceOffset = ephOffset + ephLength;
  const ciphertextOffset = nonceOffset + SealedFrame.CONSTRAINT_NONCE_LENGTH;

  if (framed.length < ciphertextOffset + SealedFrame.CONSTRAINT_TAG_LENGTH) {
    throw new FrameMalformedError(
      `Sealed frame too short for declared eph_pub_len=${ephLength}: have ` +
        `${framed.length}, need >= ${ciphertextOffset + SealedFrame.CONSTRAINT_TAG_LENGTH}.`,
    );
  }

  let recipientKid: string;

  try {
    recipientKid = _utf8StrictDecoder.decode(
      framed.subarray(
        SealedFrame.RECIPIENT_KID_OFFSET,
        SealedFrame.RECIPIENT_KID_OFFSET + kidLength,
      ),
    );
  } catch (err) {
    throw new FrameMalformedError(
      "Sealed frame recipient_kid is not valid UTF-8.",
      err,
    );
  }

  return {
    version,
    recipientKid,
    ephemeralPublicSpki: framed.subarray(ephOffset, ephOffset + ephLength),
    nonce: framed.subarray(
      nonceOffset,
      nonceOffset + SealedFrame.CONSTRAINT_NONCE_LENGTH,
    ),
    ciphertextWithTag: framed.subarray(ciphertextOffset),
  };
}
