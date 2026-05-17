// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import { MessagingActivityTags } from "@d2/telemetry";
import { canonicalize, loadFixture } from "../src/index.js";

interface CatalogMap {
  readonly [constName: string]: string;
}

describe("OTel messaging tags parity (.NET ↔ TS MessagingActivityTags)", () => {
  const fixture = loadFixture<CatalogMap>("otel-messaging-tags", "tags");
  const fixtureMap = fixture.data;
  const fixtureKeys = Object.keys(fixtureMap).sort();
  const tsCatalog = MessagingActivityTags as Readonly<Record<string, string>>;
  const tsKeys = Object.keys(tsCatalog).sort();

  it("has identical constName membership", () => {
    expect(tsKeys).toEqual(fixtureKeys);
  });

  for (const constName of fixtureKeys) {
    it(`constant ${constName} has identical wire value`, () => {
      const fixtureValue = fixtureMap[constName];
      const tsValue = tsCatalog[constName];
      expect(tsValue).toBe(fixtureValue);
    });
  }

  it("canonical maps are byte-equal", () => {
    const tsAsMap: Record<string, string> = {};
    for (const k of tsKeys) tsAsMap[k] = tsCatalog[k]!;
    expect(canonicalize(tsAsMap)).toEqual(canonicalize(fixtureMap));
  });

  // Regression pin for the publisher / consumer OTel sem-conv drift:
  // OTel canonical attribute is "messaging.operation.type", NOT "messaging.operation".
  it("MESSAGING_OPERATION_TYPE pins canonical OTel attribute (not 'messaging.operation')", () => {
    expect(MessagingActivityTags.MESSAGING_OPERATION_TYPE).toBe(
      "messaging.operation.type",
    );
    expect(MessagingActivityTags.MESSAGING_OPERATION_TYPE).not.toBe(
      "messaging.operation",
    );
  });
});
