import { describe, it, expect } from "vitest";
import { parseContextKeyConfigs } from "@d2/files-app";

function makeEnv(index: number, overrides: Record<string, string> = {}): Record<string, string> {
  return {
    [`FILES_CK__${index}__KEY`]: "user_avatar",
    [`FILES_CK__${index}__UPLOAD_RESOLUTION`]: "jwt_owner",
    [`FILES_CK__${index}__READ_RESOLUTION`]: "jwt_owner",
    [`FILES_CK__${index}__CALLBACK_ADDR`]: "auth:5101",
    [`FILES_CK__${index}__CATEGORY__0`]: "image",
    [`FILES_CK__${index}__MAX_SIZE_BYTES`]: "5242880",
    [`FILES_CK__${index}__VARIANT__0__NAME`]: "thumb",
    [`FILES_CK__${index}__VARIANT__0__MAX_DIM`]: "64",
    [`FILES_CK__${index}__VARIANT__1__NAME`]: "original",
    ...overrides,
  };
}

describe("parseContextKeyConfigs", () => {
  // --- Happy path ---

  it("should parse a single valid config with variants", () => {
    const env = makeEnv(0);
    const map = parseContextKeyConfigs(env);
    expect(map.size).toBe(1);
    const config = map.get("user_avatar")!;
    expect(config.contextKey).toBe("user_avatar");
    expect(config.uploadResolution).toBe("jwt_owner");
    expect(config.readResolution).toBe("jwt_owner");
    // listResolution auto-derives from readResolution when read is narrow
    // enough (jwt_owner/jwt_org/callback). Saves operators from repeating
    // the value when the semantics line up.
    expect(config.listResolution).toBe("jwt_owner");
    expect(config.callbackAddress).toBe("auth:5101");
    expect(config.allowedCategories).toEqual(["image"]);
    expect(config.maxSizeBytes).toBe(5242880);
    expect(config.variants).toHaveLength(2);
    expect(config.variants[0]).toEqual({ name: "thumb", maxDimension: 64 });
    expect(config.variants[1]).toEqual({ name: "original" });
  });

  it("should auto-derive listResolution from readResolution when read is narrow", () => {
    const env = makeEnv(0, {
      FILES_CK__0__READ_RESOLUTION: "jwt_org",
    });
    const map = parseContextKeyConfigs(env);
    expect(map.get("user_avatar")!.listResolution).toBe("jwt_org");
  });

  it("should respect explicit LIST_RESOLUTION override", () => {
    const env = makeEnv(0, {
      FILES_CK__0__READ_RESOLUTION: "jwt_owner",
      FILES_CK__0__LIST_RESOLUTION: "callback",
    });
    const map = parseContextKeyConfigs(env);
    expect(map.get("user_avatar")!.listResolution).toBe("callback");
  });

  it("should require explicit LIST_RESOLUTION when READ_RESOLUTION=authenticated (fail-closed)", () => {
    // Regression for B6: a contextKey that is read-public-by-id MUST opt
    // into a list strategy explicitly. Without this, list would inherit
    // `authenticated` and any signed-in user could enumerate the inventory
    // for any relatedEntityId.
    const env = makeEnv(0, {
      FILES_CK__0__READ_RESOLUTION: "authenticated",
      // No LIST_RESOLUTION
    });
    expect(() => parseContextKeyConfigs(env)).toThrow(/LIST_RESOLUTION is required/);
  });

  it("should reject explicit LIST_RESOLUTION='authenticated' (type-level guard)", () => {
    const env = makeEnv(0, {
      FILES_CK__0__LIST_RESOLUTION: "authenticated",
    });
    expect(() => parseContextKeyConfigs(env)).toThrow(/LIST_RESOLUTION must be one of/);
  });

  it("should parse multiple configs", () => {
    const env = {
      ...makeEnv(0),
      ...makeEnv(1, {
        FILES_CK__1__KEY: "org_logo",
        FILES_CK__1__UPLOAD_RESOLUTION: "jwt_org",
        FILES_CK__1__READ_RESOLUTION: "jwt_org",
        FILES_CK__1__CALLBACK_ADDR: "auth:5101",
        FILES_CK__1__CATEGORY__0: "image",
        FILES_CK__1__VARIANT__0__NAME: "original",
      }),
    };
    const map = parseContextKeyConfigs(env);
    expect(map.size).toBe(2);
    expect(map.has("user_avatar")).toBe(true);
    expect(map.has("org_logo")).toBe(true);
  });

  it("should parse multiple indexed categories", () => {
    const env = makeEnv(0, {
      FILES_CK__0__CATEGORY__0: "image",
      FILES_CK__0__CATEGORY__1: "document",
      FILES_CK__0__CATEGORY__2: "video",
    });
    const map = parseContextKeyConfigs(env);
    expect(map.get("user_avatar")!.allowedCategories).toEqual(["image", "document", "video"]);
  });

  it("should parse callback resolution with callbackAddress", () => {
    const env = makeEnv(0, {
      FILES_CK__0__KEY: "thread_attachment",
      FILES_CK__0__UPLOAD_RESOLUTION: "callback",
      FILES_CK__0__READ_RESOLUTION: "authenticated",
      FILES_CK__0__LIST_RESOLUTION: "callback",
      FILES_CK__0__CALLBACK_ADDR: "comms:3200",
      FILES_CK__0__CATEGORY__0: "image",
      FILES_CK__0__CATEGORY__1: "document",
    });
    const map = parseContextKeyConfigs(env);
    const config = map.get("thread_attachment")!;
    expect(config.callbackAddress).toBe("comms:3200");
    expect(config.uploadResolution).toBe("callback");
    expect(config.readResolution).toBe("authenticated");
    expect(config.listResolution).toBe("callback");
  });

  it("should return empty map when no env vars exist", () => {
    const map = parseContextKeyConfigs({});
    expect(map.size).toBe(0);
  });

  // --- Variant parsing ---

  it("should parse variant with maxDimension", () => {
    const env = makeEnv(0);
    const map = parseContextKeyConfigs(env);
    const config = map.get("user_avatar")!;
    expect(config.variants[0]).toEqual({ name: "thumb", maxDimension: 64 });
  });

  it("should parse variant without maxDimension (original)", () => {
    const env = makeEnv(0);
    const map = parseContextKeyConfigs(env);
    const config = map.get("user_avatar")!;
    const original = config.variants.find((v) => v.name === "original");
    expect(original).toBeDefined();
    expect(original!.maxDimension).toBeUndefined();
  });

  it("should parse multiple variants with different maxDimensions", () => {
    const env = makeEnv(0, {
      FILES_CK__0__VARIANT__0__NAME: "thumb",
      FILES_CK__0__VARIANT__0__MAX_DIM: "64",
      FILES_CK__0__VARIANT__1__NAME: "small",
      FILES_CK__0__VARIANT__1__MAX_DIM: "128",
      FILES_CK__0__VARIANT__2__NAME: "medium",
      FILES_CK__0__VARIANT__2__MAX_DIM: "512",
      FILES_CK__0__VARIANT__3__NAME: "original",
    });
    const map = parseContextKeyConfigs(env);
    const config = map.get("user_avatar")!;
    expect(config.variants).toHaveLength(4);
    expect(config.variants[0]).toEqual({ name: "thumb", maxDimension: 64 });
    expect(config.variants[1]).toEqual({ name: "small", maxDimension: 128 });
    expect(config.variants[2]).toEqual({ name: "medium", maxDimension: 512 });
    expect(config.variants[3]).toEqual({ name: "original" });
  });

  // --- Validation: key ---

  it("should throw on empty key", () => {
    const env = makeEnv(0, { FILES_CK__0__KEY: "  " });
    expect(() => parseContextKeyConfigs(env)).toThrow("FILES_CK__0__KEY is empty");
  });

  // --- Validation: resolutions ---

  it("should throw on invalid upload resolution", () => {
    const env = makeEnv(0, { FILES_CK__0__UPLOAD_RESOLUTION: "invalid" });
    expect(() => parseContextKeyConfigs(env)).toThrow("UPLOAD_RESOLUTION");
  });

  it("should throw on invalid read resolution", () => {
    const env = makeEnv(0, { FILES_CK__0__READ_RESOLUTION: "invalid" });
    expect(() => parseContextKeyConfigs(env)).toThrow("READ_RESOLUTION");
  });

  // --- Validation: callbackAddress ---

  it("should throw on missing callbackAddress", () => {
    const env = makeEnv(0);
    delete (env as Record<string, string | undefined>)[`FILES_CK__0__CALLBACK_ADDR`];
    expect(() => parseContextKeyConfigs(env)).toThrow("CALLBACK_ADDR is required");
  });

  // --- Validation: categories ---

  it("should throw on invalid category", () => {
    const env = makeEnv(0, { FILES_CK__0__CATEGORY__0: "invalid" });
    expect(() => parseContextKeyConfigs(env)).toThrow("invalid category 'invalid'");
  });

  it("should throw when no categories defined", () => {
    const env = makeEnv(0);
    delete (env as Record<string, string | undefined>)[`FILES_CK__0__CATEGORY__0`];
    expect(() => parseContextKeyConfigs(env)).toThrow("at least one CATEGORY");
  });

  // --- Validation: maxSizeBytes ---

  it("should throw on missing max size bytes", () => {
    const env = makeEnv(0);
    delete (env as Record<string, string | undefined>)[`FILES_CK__0__MAX_SIZE_BYTES`];
    expect(() => parseContextKeyConfigs(env)).toThrow("MAX_SIZE_BYTES is required");
  });

  it("should throw on non-numeric max size bytes", () => {
    const env = makeEnv(0, { FILES_CK__0__MAX_SIZE_BYTES: "abc" });
    expect(() => parseContextKeyConfigs(env)).toThrow("MAX_SIZE_BYTES must be a positive number");
  });

  it("should throw on zero max size bytes", () => {
    const env = makeEnv(0, { FILES_CK__0__MAX_SIZE_BYTES: "0" });
    expect(() => parseContextKeyConfigs(env)).toThrow("MAX_SIZE_BYTES must be a positive number");
  });

  // --- Validation: variants ---

  it("should throw when no variants defined", () => {
    const env = makeEnv(0);
    delete (env as Record<string, string | undefined>)[`FILES_CK__0__VARIANT__0__NAME`];
    delete (env as Record<string, string | undefined>)[`FILES_CK__0__VARIANT__0__MAX_DIM`];
    delete (env as Record<string, string | undefined>)[`FILES_CK__0__VARIANT__1__NAME`];
    expect(() => parseContextKeyConfigs(env)).toThrow("at least one VARIANT");
  });

  it("should throw on empty variant name", () => {
    const env = makeEnv(0, { FILES_CK__0__VARIANT__0__NAME: "  " });
    expect(() => parseContextKeyConfigs(env)).toThrow("VARIANT__0__NAME is empty");
  });

  it("should throw on non-positive maxDimension", () => {
    const env = makeEnv(0, { FILES_CK__0__VARIANT__0__MAX_DIM: "0" });
    expect(() => parseContextKeyConfigs(env)).toThrow("MAX_DIM must be a positive number");
  });

  it("should throw on non-numeric maxDimension", () => {
    const env = makeEnv(0, { FILES_CK__0__VARIANT__0__MAX_DIM: "abc" });
    expect(() => parseContextKeyConfigs(env)).toThrow("MAX_DIM must be a positive number");
  });

  // --- Validation: duplicates ---

  it("should throw on duplicate context keys", () => {
    const env = {
      ...makeEnv(0),
      ...makeEnv(1, { FILES_CK__1__KEY: "user_avatar" }),
    };
    expect(() => parseContextKeyConfigs(env)).toThrow("Duplicate context key 'user_avatar'");
  });
});
