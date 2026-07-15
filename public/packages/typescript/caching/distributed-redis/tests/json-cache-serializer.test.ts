// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { ErrorCodes, HttpStatusCode } from "@d2/result";
import { describe, expect, it } from "vitest";

import { JsonCacheSerializer } from "../src/index.js";

const serializer = new JsonCacheSerializer();

describe("JsonCacheSerializer", () => {
  it("contentType_isApplicationJson", () => {
    expect(serializer.contentType).toBe("application/json");
  });

  it("serialize_null_roundTrips", () => {
    const ser = serializer.serialize(null);
    expect(ser.success).toBe(true);
    const deser = serializer.deserialize<null>(ser.data!);
    expect(deser.success).toBe(true);
    expect(deser.data).toBeNull();
  });

  it("serialize_primitives_roundTrip", () => {
    for (const value of [1, "x", true, false, 0] as const) {
      const ser = serializer.serialize(value);
      expect(ser.success).toBe(true);
      const deser = serializer.deserialize(ser.data!);
      expect(deser.success).toBe(true);
      expect(deser.data).toEqual(value);
    }
  });

  it("serialize_object_propertyNamesAreCamelCase", () => {
    const ser = serializer.serialize({
      DisplayName: "Ada",
      Nested: { Age: 1 },
    });
    expect(ser.success).toBe(true);
    const text = new TextDecoder().decode(ser.data!);
    expect(text).toContain("displayName");
    expect(text).toContain("nested");
    expect(text).toContain("age");
    expect(text).not.toContain("DisplayName");
  });

  it("serialize_cycle_omitsCyclicRef_doesNotThrow", () => {
    const a: { name: string; self?: unknown } = { name: "a" };
    a.self = a;
    const ser = serializer.serialize(a);
    expect(ser.success).toBe(true);
    const text = new TextDecoder().decode(ser.data!);
    expect(text).toContain("name");
  });

  it("serialize_largeValue_roundTrips", () => {
    const large = { blob: "x".repeat(50_000) };
    const ser = serializer.serialize(large);
    expect(ser.success).toBe(true);
    const deser = serializer.deserialize<typeof large>(ser.data!);
    expect(deser.success).toBe(true);
    expect(deser.data?.blob.length).toBe(50_000);
  });

  it("deserialize_garbage_returnsCouldNotBeDeserialized", () => {
    const bytes = new TextEncoder().encode("{not-json");
    const result = serializer.deserialize(bytes);
    expect(result.success).toBe(false);
    expect(result.errorCode).toBe(ErrorCodes.COULD_NOT_BE_DESERIALIZED);
    expect(result.statusCode).toBe(HttpStatusCode.InternalServerError);
  });

  it("deserialize_emptyBytes_returnsCouldNotBeDeserialized", () => {
    const result = serializer.deserialize(new Uint8Array());
    expect(result.success).toBe(false);
    expect(result.errorCode).toBe(ErrorCodes.COULD_NOT_BE_DESERIALIZED);
  });

  it("serialize_bigint_returnsCouldNotBeSerialized", () => {
    const result = serializer.serialize({ n: 1n });
    expect(result.success).toBe(false);
    expect(result.errorCode).toBe(ErrorCodes.COULD_NOT_BE_SERIALIZED);
    expect(result.statusCode).toBe(HttpStatusCode.InternalServerError);
  });

  it("serialize_array_preservesElements_andCamelCasesNestedObjects", () => {
    const ser = serializer.serialize([{ DisplayName: "Ada" }, 2]);
    expect(ser.success).toBe(true);
    const text = new TextDecoder().decode(ser.data!);
    expect(text).toContain("displayName");
    expect(text).toContain("Ada");
  });

  it("serialize_emptyPropertyName_roundTrips", () => {
    const ser = serializer.serialize({ "": "empty-key" });
    expect(ser.success).toBe(true);
    const deser = serializer.deserialize<Record<string, string>>(ser.data!);
    expect(deser.success).toBe(true);
    expect(deser.data?.[""]).toBe("empty-key");
  });
});
