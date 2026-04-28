import type { ContextKeyConfig, ContextKeyConfigMap } from "@d2/files-app";

export const TEST_USER_AVATAR_CONFIG: ContextKeyConfig = {
  contextKey: "user_avatar",
  uploadResolution: "jwt_owner",
  readResolution: "jwt_owner",
  listResolution: "jwt_owner",
  callbackAddress: "auth:5101",
  allowedCategories: ["image"],
  maxSizeBytes: 5 * 1024 * 1024, // 5 MB
  variants: [
    { name: "thumb", maxDimension: 64 },
    { name: "small", maxDimension: 128 },
    { name: "medium", maxDimension: 256 },
    { name: "original" },
  ],
};

export const TEST_ORG_LOGO_CONFIG: ContextKeyConfig = {
  contextKey: "org_logo",
  uploadResolution: "jwt_org",
  readResolution: "jwt_org",
  listResolution: "jwt_org",
  callbackAddress: "auth:5101",
  allowedCategories: ["image"],
  maxSizeBytes: 10 * 1024 * 1024, // 10 MB
  variants: [
    { name: "thumb", maxDimension: 64 },
    { name: "medium", maxDimension: 512 },
    { name: "original" },
  ],
};

export const TEST_THREAD_ATTACHMENT_CONFIG: ContextKeyConfig = {
  contextKey: "thread_attachment",
  uploadResolution: "callback",
  readResolution: "authenticated",
  // List delegated to Comms — public-by-id read for thread attachments is
  // fine, but listing all attachments under a thread requires the owning
  // service to confirm thread membership.
  listResolution: "callback",
  callbackAddress: "comms:3200",
  allowedCategories: ["image", "document", "video", "audio"],
  maxSizeBytes: 25 * 1024 * 1024, // 25 MB
  variants: [
    { name: "thumb", maxDimension: 150 },
    { name: "preview", maxDimension: 800 },
    { name: "original" },
  ],
};

export function createTestConfigMap(...configs: ContextKeyConfig[]): ContextKeyConfigMap {
  const map = new Map<string, ContextKeyConfig>();
  for (const config of configs) {
    map.set(config.contextKey, config);
  }
  return map;
}

export const TEST_CONFIG_MAP: ContextKeyConfigMap = createTestConfigMap(
  TEST_USER_AVATAR_CONFIG,
  TEST_ORG_LOGO_CONFIG,
  TEST_THREAD_ATTACHMENT_CONFIG,
);
