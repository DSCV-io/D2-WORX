// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import {
  DISPLAY_NAME_INVALID_RE,
  EMAIL_RE,
  EMPTY_UUID,
  UUID_RE,
  WHITESPACE_RE,
} from "../src/regex.js";

describe("WHITESPACE_RE", () => {
  it("matches whitespace runs", () => {
    expect("a  b\tc\nd".replace(WHITESPACE_RE, "_")).toBe("a_b_c_d");
  });
});

describe("DISPLAY_NAME_INVALID_RE", () => {
  it("matches anything outside the allowlist", () => {
    expect("a<b>c".replace(DISPLAY_NAME_INVALID_RE, "")).toBe("abc");
    expect("ä-中.".replace(DISPLAY_NAME_INVALID_RE, "")).toBe("ä-中.");
  });
});

describe("EMAIL_RE", () => {
  it.each(["a@b.c", "user.name@example.com", "x@y.z"])(
    "matches %s",
    (email) => {
      expect(EMAIL_RE.test(email)).toBe(true);
    },
  );
  it.each(["", "no-at", "a@b", "@b.c", "a b@c.d"])("rejects %s", (email) => {
    expect(EMAIL_RE.test(email)).toBe(false);
  });
});

describe("UUID_RE / EMPTY_UUID", () => {
  it("UUID_RE matches canonical form", () => {
    expect(UUID_RE.test("550e8400-e29b-41d4-a716-446655440000")).toBe(true);
  });
  it("UUID_RE rejects malformed", () => {
    expect(UUID_RE.test("not-a-uuid")).toBe(false);
  });
  it("EMPTY_UUID is the all-zero canonical form", () => {
    expect(EMPTY_UUID).toBe("00000000-0000-0000-0000-000000000000");
    expect(UUID_RE.test(EMPTY_UUID)).toBe(true);
  });
});
