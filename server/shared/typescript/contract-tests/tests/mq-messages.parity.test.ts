// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { MqMessagesRegistry } from "@d2/messaging-abstractions";
import { describe, expect, it } from "vitest";

import { canonicalize, loadFixture } from "../src/index.js";

type DescriptorMap = Readonly<Record<string, Record<string, unknown>>>;

describe("mq-messages descriptor mirror parity (.NET ↔ TS MqMessagesRegistry)", () => {
  const fixture = loadFixture<DescriptorMap>("mq-messages", "registry");
  const fixtureMap = fixture.data;
  const fixtureKeys = Object.keys(fixtureMap).sort();
  // MqMessagesRegistry is Readonly<Record<string, MqMessageDescriptor>>; the
  // comparison below is via canonicalize(unknown), so no generic-map cast is
  // needed (a `Record<string, Record<string, unknown>>` cast is rejected by TS
  // because MqMessageDescriptor has no index signature).
  const tsRegistry = MqMessagesRegistry;
  const tsKeys = Object.keys(tsRegistry).sort();

  it("has a non-empty descriptor set on both sides (anti-vacuous guard)", () => {
    // Without this, an empty fixture map + empty TS registry would pass every
    // membership / field-by-field assertion vacuously (the per-constant loop
    // below would generate zero cases). Mirrors the .NET anti-vacuous guards.
    expect(fixtureKeys.length).toBeGreaterThan(0);
    expect(tsKeys.length).toBeGreaterThan(0);
  });

  it("has identical message-constant membership", () => {
    expect(tsKeys).toEqual(fixtureKeys);
  });

  for (const constant of fixtureKeys) {
    it(`descriptor ${constant} matches the .NET record field-by-field`, () => {
      expect(canonicalize(tsRegistry[constant])).toEqual(
        canonicalize(fixtureMap[constant]),
      );
    });
  }

  it("the whole registry is canonically byte-equal to the .NET source", () => {
    expect(canonicalize(tsRegistry)).toEqual(canonicalize(fixtureMap));
  });
});
