// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { EncryptionFrame, SealedFrame } from "@d2/encryption-abstractions";
import { describe, expect, it } from "vitest";

import {
  FrameMalformedError,
  FrameVersionMismatchError,
} from "../src/errors.js";
import { decodeFrame, encodeFrame } from "../src/frame.js";
import { decodeSealedFrame, encodeSealedFrame } from "../src/sealed-frame.js";

const nonce12 = new Uint8Array(Array.from({ length: 12 }, (_v, i) => i));
const tag16 = new Uint8Array(16).fill(0xaa);
const ct = new Uint8Array([1, 2, 3, 4]);
const ctWithTag = new Uint8Array([...ct, ...tag16]);

describe("v1 encryption frame codec", () => {
  it("round-trips a well-formed frame", () => {
    const frame = encodeFrame("k1", nonce12, ctWithTag);
    const view = decodeFrame(frame);

    expect(view.version).toBe(EncryptionFrame.CURRENT_VERSION);
    expect(view.kid).toBe("k1");
    expect([...view.nonce]).toEqual([...nonce12]);
    expect([...view.ciphertextWithTag]).toEqual([...ctWithTag]);
  });

  it("encode rejects an out-of-range kid, a wrong nonce, and a too-short body", () => {
    expect(() => encodeFrame("", nonce12, ctWithTag)).toThrow(RangeError);
    expect(() => encodeFrame("k".repeat(65), nonce12, ctWithTag)).toThrow(
      RangeError,
    );
    expect(() => encodeFrame("k1", new Uint8Array(11), ctWithTag)).toThrow(
      RangeError,
    );
    expect(() => encodeFrame("k1", nonce12, new Uint8Array(15))).toThrow(
      RangeError,
    );
  });

  it("decode rejects a too-short frame", () => {
    expect(() => decodeFrame(new Uint8Array(29))).toThrow(FrameMalformedError);
  });

  it("decode rejects a wrong version byte", () => {
    const frame = encodeFrame("k1", nonce12, ctWithTag);
    frame[EncryptionFrame.VERSION_OFFSET] = 2;

    expect(() => decodeFrame(frame)).toThrow(FrameVersionMismatchError);
  });

  it("decode rejects a zero kid_length", () => {
    const frame = encodeFrame("k1", nonce12, ctWithTag);
    frame[EncryptionFrame.KID_LENGTH_OFFSET] = 0;

    expect(() => decodeFrame(frame)).toThrow(FrameMalformedError);
  });

  it("decode rejects a declared kid_length that overruns the buffer", () => {
    const frame = encodeFrame("k1", nonce12, ctWithTag);
    // Claim a kid far longer than the buffer can hold.
    frame[EncryptionFrame.KID_LENGTH_OFFSET] = 250;

    expect(() => decodeFrame(frame)).toThrow(FrameMalformedError);
  });

  it("decode is lenient on non-UTF-8 kid bytes (replacement, mirroring .NET default)", () => {
    // A 1-byte kid of 0xFF decodes to U+FFFD rather than throwing.
    const frame = encodeFrame("k", nonce12, ctWithTag);
    frame[EncryptionFrame.KID_OFFSET] = 0xff;
    const view = decodeFrame(frame);

    expect(view.kid).toBe("�");
  });
});

describe("v2 sealed frame codec", () => {
  const eph = new Uint8Array(91).fill(0x04);

  it("round-trips a well-formed sealed frame", () => {
    const frame = encodeSealedFrame("rk-1", eph, nonce12, ctWithTag);
    const view = decodeSealedFrame(frame);

    expect(view.version).toBe(SealedFrame.CURRENT_VERSION);
    expect(view.recipientKid).toBe("rk-1");
    expect([...view.ephemeralPublicSpki]).toEqual([...eph]);
    expect([...view.nonce]).toEqual([...nonce12]);
    expect([...view.ciphertextWithTag]).toEqual([...ctWithTag]);
  });

  it("encode rejects out-of-range kid / eph_pub / nonce / body", () => {
    expect(() => encodeSealedFrame("", eph, nonce12, ctWithTag)).toThrow(
      RangeError,
    );
    expect(() =>
      encodeSealedFrame("k".repeat(65), eph, nonce12, ctWithTag),
    ).toThrow(RangeError);
    expect(() =>
      encodeSealedFrame("rk", new Uint8Array(0), nonce12, ctWithTag),
    ).toThrow(RangeError);
    expect(() =>
      encodeSealedFrame("rk", new Uint8Array(257), nonce12, ctWithTag),
    ).toThrow(RangeError);
    expect(() =>
      encodeSealedFrame("rk", eph, new Uint8Array(11), ctWithTag),
    ).toThrow(RangeError);
    expect(() =>
      encodeSealedFrame("rk", eph, nonce12, new Uint8Array(15)),
    ).toThrow(RangeError);
  });

  it("decode rejects a too-short frame and a wrong version byte", () => {
    expect(() => decodeSealedFrame(new Uint8Array(33))).toThrow(
      FrameMalformedError,
    );

    const frame = encodeSealedFrame("rk-1", eph, nonce12, ctWithTag);
    frame[SealedFrame.VERSION_OFFSET] = 1;
    expect(() => decodeSealedFrame(frame)).toThrow(FrameVersionMismatchError);
  });

  it("decode rejects an out-of-range recipient_kid_length", () => {
    const frame = encodeSealedFrame("rk-1", eph, nonce12, ctWithTag);
    frame[SealedFrame.RECIPIENT_KID_LENGTH_OFFSET] = 0;
    expect(() => decodeSealedFrame(frame)).toThrow(FrameMalformedError);

    const frame2 = encodeSealedFrame("rk-1", eph, nonce12, ctWithTag);
    frame2[SealedFrame.RECIPIENT_KID_LENGTH_OFFSET] = 65;
    expect(() => decodeSealedFrame(frame2)).toThrow(FrameMalformedError);
  });

  it("decode rejects a kid_length whose eph_pub length prefix overruns", () => {
    // A short-but-valid frame minimum, then claim a kid that pushes the eph_len
    // prefix past the end.
    const frame = encodeSealedFrame("rk-1", eph, nonce12, ctWithTag);
    frame[SealedFrame.RECIPIENT_KID_LENGTH_OFFSET] = 64;
    const truncated = frame.subarray(0, SealedFrame.RECIPIENT_KID_OFFSET + 64);
    expect(() => decodeSealedFrame(truncated)).toThrow(FrameMalformedError);
  });

  it("decode rejects a zero and an oversized eph_pub_len", () => {
    const frame = encodeSealedFrame("rk-1", eph, nonce12, ctWithTag);
    const view = new DataView(frame.buffer);
    const ephLenOffset = SealedFrame.RECIPIENT_KID_OFFSET + 4; // "rk-1"
    view.setUint16(ephLenOffset, 0, false);
    expect(() => decodeSealedFrame(frame)).toThrow(/eph_pub_len is zero/);

    const frame2 = encodeSealedFrame("rk-1", eph, nonce12, ctWithTag);
    const view2 = new DataView(frame2.buffer);
    view2.setUint16(ephLenOffset, 257, false);
    expect(() => decodeSealedFrame(frame2)).toThrow(/exceeds the cap/);
  });

  it("decode rejects a frame too short for the declared eph_pub_len", () => {
    const frame = encodeSealedFrame("rk-1", eph, nonce12, ctWithTag);
    // Chop the trailing ciphertext so the ct+tag region underruns the tag.
    const truncated = frame.subarray(0, frame.length - 8);
    expect(() => decodeSealedFrame(truncated)).toThrow(FrameMalformedError);
  });

  it("decode rejects a non-UTF-8 recipient kid (strict, unlike v1)", () => {
    const frame = encodeSealedFrame("rk", eph, nonce12, ctWithTag);
    frame[SealedFrame.RECIPIENT_KID_OFFSET] = 0xff;
    expect(() => decodeSealedFrame(frame)).toThrow(/not valid UTF-8/);
  });
});
