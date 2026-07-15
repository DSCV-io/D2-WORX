// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";

import type { InputError } from "../src/input-error.js";
import {
  renderInputErrors,
  renderMessage,
  renderMessages,
  type TranslateFn,
} from "../src/render-messages.js";
import type { TKMessage } from "@d2/i18n-abstractions";

/** Synthetic translator — echoes "<key>(p1=v1,p2=v2)" so tests can assert on rendered output. */
const echo: TranslateFn = (key, params) => {
  if (params === undefined) return key;
  const entries = Object.entries(params)
    .map(([k, v]) => `${k}=${String(v)}`)
    .join(",");
  return `${key}(${entries})`;
};

describe("renderMessage", () => {
  it("renders a TKMessage with no params via translator", () => {
    const m: TKMessage = { key: "TK.X" };
    expect(renderMessage(m, echo)).toBe("TK.X");
  });

  it("passes params through to translator", () => {
    const m: TKMessage = { key: "TK.Y", params: { name: "Alice", age: 30 } };
    expect(renderMessage(m, echo)).toBe("TK.Y(name=Alice,age=30)");
  });

  it("returns the verbatim key on unknown-key fallback translator", () => {
    const fallback: TranslateFn = (k) => k;
    expect(renderMessage({ key: "TK.UNKNOWN" }, fallback)).toBe("TK.UNKNOWN");
  });

  it("handles boolean / number / null param values", () => {
    const m: TKMessage = {
      key: "TK.Z",
      params: { enabled: true, count: 5, missing: null },
    };
    expect(renderMessage(m, echo)).toBe(
      "TK.Z(enabled=true,count=5,missing=null)",
    );
  });
});

describe("renderMessages", () => {
  it("returns empty array for empty input", () => {
    expect(renderMessages([], echo)).toEqual([]);
  });

  it("returns empty array for undefined", () => {
    expect(renderMessages(undefined, echo)).toEqual([]);
  });

  it("returns empty array for null", () => {
    expect(renderMessages(null, echo)).toEqual([]);
  });

  it("renders single message", () => {
    expect(renderMessages([{ key: "TK.A" }], echo)).toEqual(["TK.A"]);
  });

  it("renders multiple messages preserving order", () => {
    const messages: TKMessage[] = [
      { key: "TK.A" },
      { key: "TK.B", params: { x: 1 } },
      { key: "TK.C" },
    ];
    expect(renderMessages(messages, echo)).toEqual([
      "TK.A",
      "TK.B(x=1)",
      "TK.C",
    ]);
  });
});

describe("renderInputErrors", () => {
  it("returns empty object for empty input", () => {
    expect(renderInputErrors([], echo)).toEqual({});
  });

  it("returns empty object for undefined", () => {
    expect(renderInputErrors(undefined, echo)).toEqual({});
  });

  it("returns empty object for null", () => {
    expect(renderInputErrors(null, echo)).toEqual({});
  });

  it("converts single InputError with single error", () => {
    const errors: InputError[] = [
      { field: "email", errors: [{ key: "TK.email.INVALID" }] },
    ];
    expect(renderInputErrors(errors, echo)).toEqual({
      email: ["TK.email.INVALID"],
    });
  });

  it("converts single InputError with multiple errors", () => {
    const errors: InputError[] = [
      {
        field: "email",
        errors: [{ key: "TK.email.REQUIRED" }, { key: "TK.email.INVALID" }],
      },
    ];
    expect(renderInputErrors(errors, echo)).toEqual({
      email: ["TK.email.REQUIRED", "TK.email.INVALID"],
    });
  });

  it("renders multiple fields", () => {
    const errors: InputError[] = [
      { field: "email", errors: [{ key: "TK.REQUIRED" }] },
      { field: "name", errors: [{ key: "TK.TOO_SHORT" }] },
    ];
    expect(renderInputErrors(errors, echo)).toEqual({
      email: ["TK.REQUIRED"],
      name: ["TK.TOO_SHORT"],
    });
  });

  it("merges duplicate field entries", () => {
    const errors: InputError[] = [
      { field: "email", errors: [{ key: "TK.A" }] },
      { field: "email", errors: [{ key: "TK.B" }] },
    ];
    expect(renderInputErrors(errors, echo)).toEqual({
      email: ["TK.A", "TK.B"],
    });
  });

  it("preserves dot-notation field names", () => {
    const errors: InputError[] = [
      { field: "address.city", errors: [{ key: "TK.REQUIRED" }] },
    ];
    expect(renderInputErrors(errors, echo)).toEqual({
      "address.city": ["TK.REQUIRED"],
    });
  });

  it("renders TKMessage params via translator", () => {
    const errors: InputError[] = [
      {
        field: "password",
        errors: [{ key: "TK.MIN_LENGTH", params: { min: 12 } }],
      },
    ];
    expect(renderInputErrors(errors, echo)).toEqual({
      password: ["TK.MIN_LENGTH(min=12)"],
    });
  });

  it("skips entries with empty field name", () => {
    const errors: InputError[] = [{ field: "", errors: [{ key: "TK.X" }] }];
    expect(renderInputErrors(errors, echo)).toEqual({});
  });

  it("skips entries with zero errors", () => {
    const errors: InputError[] = [{ field: "email", errors: [] }];
    expect(renderInputErrors(errors, echo)).toEqual({});
  });
});
