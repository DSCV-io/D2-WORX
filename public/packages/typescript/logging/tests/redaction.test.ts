// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { afterEach, describe, expect, it } from "vitest";
import {
  clearRedactedFieldsRegistry,
  collectAllRedactedFields,
  getRedactedFieldsFor,
  markRedactedFields,
} from "../src/redaction.js";

describe("markRedactedFields / getRedactedFieldsFor", () => {
  afterEach(() => clearRedactedFieldsRegistry());

  it("registers and looks up by symbol identifier", () => {
    const id = Symbol("MyType");
    markRedactedFields(id, ["email", "phone"]);
    expect(getRedactedFieldsFor(id)).toEqual(["email", "phone"]);
  });

  it("returns empty array for unregistered symbol (no throw)", () => {
    expect(getRedactedFieldsFor(Symbol("X"))).toEqual([]);
  });

  it("re-registration replaces prior paths (idempotent)", () => {
    const id = Symbol("MyType");
    markRedactedFields(id, ["a"]);
    markRedactedFields(id, ["b", "c"]);
    expect(getRedactedFieldsFor(id)).toEqual(["b", "c"]);
  });

  it("trims paths and drops falsey ones", () => {
    const id = Symbol("X");
    markRedactedFields(id, [" email ", "", "  ", "phone"]);
    expect(getRedactedFieldsFor(id)).toEqual(["email", "phone"]);
  });

  it.each([null, undefined, "not-a-symbol", 42, {}])(
    "throws on non-symbol identifier %j",
    (badId) => {
      expect(() => markRedactedFields(badId as never, ["x"])).toThrow(
        TypeError,
      );
    },
  );
});

describe("collectAllRedactedFields", () => {
  afterEach(() => clearRedactedFieldsRegistry());

  it("returns empty when no registrations", () => {
    expect(collectAllRedactedFields()).toEqual([]);
  });

  it("flattens across all registered types", () => {
    markRedactedFields(Symbol("A"), ["x.email"]);
    markRedactedFields(Symbol("B"), ["y.phone", "y.ip"]);
    const out = collectAllRedactedFields();
    expect(out).toContain("x.email");
    expect(out).toContain("y.phone");
    expect(out).toContain("y.ip");
    expect(out).toHaveLength(3);
  });
});
