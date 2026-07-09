// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { AmqpHeaders } from "@d2/headers-amqp";
import { describe, expect, it } from "vitest";

import { readValidatedMessageId } from "../src/subscribing/message-id.js";

const VALID = "0192f8c1-1111-7000-8000-000000000001";

describe("readValidatedMessageId — mirrors SubscriberChannel.ReadMessageId", () => {
  it("prefers the AMQP message-id property", () => {
    expect(readValidatedMessageId(VALID, {})).toBe(VALID);
  });

  it("falls back to the message-id header (string)", () => {
    expect(
      readValidatedMessageId(undefined, { [AmqpHeaders.MESSAGE_ID]: VALID }),
    ).toBe(VALID);
  });

  it("falls back to the message-id header (bytes)", () => {
    const bytes = Buffer.from(VALID, "utf8");
    expect(
      readValidatedMessageId(undefined, { [AmqpHeaders.MESSAGE_ID]: bytes }),
    ).toBe(VALID);
  });

  it("returns undefined when neither property nor header is present", () => {
    expect(readValidatedMessageId(undefined, undefined)).toBeUndefined();
    expect(readValidatedMessageId("", {})).toBeUndefined();
    expect(
      readValidatedMessageId(undefined, { [AmqpHeaders.MESSAGE_ID]: 123 }),
    ).toBeUndefined();
  });

  it("rejects a pathologically long id (store-memory DoS guard)", () => {
    expect(readValidatedMessageId("x".repeat(129), {})).toBeUndefined();
  });

  it.each([
    ["control char (U+0001)", `abc${String.fromCharCode(0x01)}def`],
    ["DEL char (U+007F)", `abc${String.fromCharCode(0x7f)}def`],
    ["colon (namespace split)", "abc:def"],
    ["space", "abc def"],
  ])("rejects an id containing a %s", (_label, id) => {
    expect(readValidatedMessageId(id, {})).toBeUndefined();
  });
});
