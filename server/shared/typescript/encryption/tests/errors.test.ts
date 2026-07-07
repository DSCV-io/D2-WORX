// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";

import {
  AuthenticationTagMismatchError,
  EncryptionError,
  FrameMalformedError,
  FrameVersionMismatchError,
  KidNotInKeyringError,
} from "../src/errors.js";

describe("encryption error taxonomy", () => {
  it("FrameMalformedError is an EncryptionError with the type name", () => {
    const err = new FrameMalformedError("bad frame");

    expect(err).toBeInstanceOf(EncryptionError);
    expect(err).toBeInstanceOf(Error);
    expect(err.name).toBe("FrameMalformedError");
    expect(err.message).toBe("bad frame");
    expect(err.cause).toBeUndefined();
  });

  it("FrameMalformedError carries an inner cause when supplied", () => {
    const inner = new Error("import failed");
    const err = new FrameMalformedError("bad eph_pub", inner);

    expect(err.cause).toBe(inner);
  });

  it("FrameVersionMismatchError exposes the unrecognized version byte", () => {
    const err = new FrameVersionMismatchError(9);

    expect(err).toBeInstanceOf(EncryptionError);
    expect(err.name).toBe("FrameVersionMismatchError");
    expect(err.version).toBe(9);
    expect(err.message).toContain("9");
  });

  it("KidNotInKeyringError exposes the missing kid", () => {
    const err = new KidNotInKeyringError("kid-x");

    expect(err).toBeInstanceOf(EncryptionError);
    expect(err.name).toBe("KidNotInKeyringError");
    expect(err.kid).toBe("kid-x");
    expect(err.message).toContain("kid-x");
  });

  it("AuthenticationTagMismatchError has a default message and an optional cause", () => {
    const def = new AuthenticationTagMismatchError();

    expect(def).toBeInstanceOf(EncryptionError);
    expect(def.name).toBe("AuthenticationTagMismatchError");
    expect(def.message).toContain("authentication tag");

    const inner = new Error("OperationError");
    const withCause = new AuthenticationTagMismatchError("custom", inner);

    expect(withCause.message).toBe("custom");
    expect(withCause.cause).toBe(inner);
  });
});
