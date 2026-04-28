import type { ContentCategory } from "@d2/files-domain";
import type { VariantConfig } from "@d2/files-domain";

/**
 * Access resolution strategy for upload operations.
 *
 * - `jwt_owner` — requestContext.userId must match relatedEntityId
 * - `jwt_org` — requestContext.orgId must match relatedEntityId
 * - `callback` — gRPC CanAccess check via callbackAddress
 */
export type UploadResolution = "jwt_owner" | "jwt_org" | "callback";

/**
 * Access resolution strategy for fetching a single file by id.
 *
 * - `jwt_owner` — requestContext.userId must match relatedEntityId
 * - `jwt_org` — requestContext.orgId must match relatedEntityId
 * - `authenticated` — any authenticated user can read (the file content is
 *   public-by-id, e.g. avatars or org brochures intended for anyone with
 *   the link)
 * - `callback` — gRPC CanAccess check via callbackAddress
 */
export type ReadResolution = "jwt_owner" | "jwt_org" | "authenticated" | "callback";

/**
 * Access resolution strategy for LISTING files under a contextKey + relatedEntityId.
 *
 * Deliberately narrower than `ReadResolution` — `authenticated` is excluded.
 * "This single file is public-by-id" and "the inventory of all files under
 * this scope is public to enumerate" are different security questions, and
 * defaulting them to the same answer would let any contextKey ship with
 * `readResolution: "authenticated"` accidentally enable cross-tenant
 * enumeration via the list endpoint (e.g. competitor doing recon on org
 * brochures or document uploads).
 *
 * - `jwt_owner` — relatedEntityId must equal session userId (you list yours)
 * - `jwt_org`   — relatedEntityId must equal session orgId  (org members list theirs)
 * - `callback`  — owning service decides per-list via gRPC CanAccess
 *
 * To intentionally expose a public-listing surface (e.g. an Instagram-style
 * profile gallery), wire it through `callback` and have the owning service
 * approve any caller.
 */
export type ListResolution = "jwt_owner" | "jwt_org" | "callback";

/**
 * Per-context-key runtime configuration for the Files service.
 *
 * Parsed from indexed env vars at startup. Defines access control,
 * allowed content categories, size limits, gRPC callback address, and variant definitions.
 */
export interface ContextKeyConfig {
  readonly contextKey: string;
  readonly uploadResolution: UploadResolution;
  readonly readResolution: ReadResolution;
  readonly listResolution: ListResolution;
  readonly callbackAddress: string;
  readonly allowedCategories: readonly ContentCategory[];
  readonly maxSizeBytes: number;
  readonly variants: readonly VariantConfig[];
}

/**
 * Immutable map of contextKey → ContextKeyConfig.
 * Populated once at startup, injected into handlers via DI.
 */
export type ContextKeyConfigMap = ReadonlyMap<string, ContextKeyConfig>;

const VALID_UPLOAD_RESOLUTIONS: readonly string[] = ["jwt_owner", "jwt_org", "callback"];
const VALID_READ_RESOLUTIONS: readonly string[] = [
  "jwt_owner",
  "jwt_org",
  "authenticated",
  "callback",
];
const VALID_LIST_RESOLUTIONS: readonly string[] = ["jwt_owner", "jwt_org", "callback"];
const VALID_CATEGORIES: readonly string[] = ["image", "document", "video", "audio"];

/**
 * Parses indexed environment variables into a ContextKeyConfigMap.
 *
 * Env var convention (fully indexed):
 * ```
 * FILES_CK__0__KEY=user_avatar
 * FILES_CK__0__UPLOAD_RESOLUTION=jwt_owner
 * FILES_CK__0__READ_RESOLUTION=jwt_owner
 * FILES_CK__0__CALLBACK_ADDR=auth:5101
 * FILES_CK__0__CATEGORY__0=image
 * FILES_CK__0__MAX_SIZE_BYTES=5242880
 * FILES_CK__0__VARIANT__0__NAME=thumb
 * FILES_CK__0__VARIANT__0__MAX_DIM=64
 * FILES_CK__0__VARIANT__1__NAME=original
 * ```
 *
 * @throws Error on invalid or incomplete config (fail-fast at startup)
 */
export function parseContextKeyConfigs(
  env: Record<string, string | undefined>,
): ContextKeyConfigMap {
  const map = new Map<string, ContextKeyConfig>();
  const prefix = "FILES_CK";

  for (let i = 0; ; i++) {
    const key = env[`${prefix}__${i}__KEY`];
    if (key === undefined) break;

    const uploadResolution = env[`${prefix}__${i}__UPLOAD_RESOLUTION`];
    const readResolution = env[`${prefix}__${i}__READ_RESOLUTION`];
    // List resolution defaults to read resolution when explicit and read is
    // narrow enough (jwt_owner/jwt_org/callback). If read is `authenticated`
    // the operator MUST set list explicitly — there is no safe auto-derive.
    const listResolutionRaw = env[`${prefix}__${i}__LIST_RESOLUTION`];
    const callbackAddr = env[`${prefix}__${i}__CALLBACK_ADDR`];
    const maxSizeBytesRaw = env[`${prefix}__${i}__MAX_SIZE_BYTES`];

    if (!key.trim()) {
      throw new Error(`FILES_CK__${i}__KEY is empty.`);
    }

    if (!uploadResolution || !VALID_UPLOAD_RESOLUTIONS.includes(uploadResolution)) {
      throw new Error(
        `FILES_CK__${i}__UPLOAD_RESOLUTION must be one of: ${VALID_UPLOAD_RESOLUTIONS.join(", ")}. Got: '${uploadResolution}'.`,
      );
    }

    if (!readResolution || !VALID_READ_RESOLUTIONS.includes(readResolution)) {
      throw new Error(
        `FILES_CK__${i}__READ_RESOLUTION must be one of: ${VALID_READ_RESOLUTIONS.join(", ")}. Got: '${readResolution}'.`,
      );
    }

    let listResolution: string;
    if (listResolutionRaw !== undefined) {
      if (!VALID_LIST_RESOLUTIONS.includes(listResolutionRaw)) {
        throw new Error(
          `FILES_CK__${i}__LIST_RESOLUTION must be one of: ${VALID_LIST_RESOLUTIONS.join(", ")}. Got: '${listResolutionRaw}'. (Note: 'authenticated' is intentionally excluded — list endpoints would otherwise enable cross-tenant enumeration; route public-listing through 'callback' if needed.)`,
        );
      }
      listResolution = listResolutionRaw;
    } else if (VALID_LIST_RESOLUTIONS.includes(readResolution)) {
      // Auto-derive when read is already narrow enough.
      listResolution = readResolution;
    } else {
      // read = 'authenticated' and no explicit list set — fail closed.
      throw new Error(
        `FILES_CK__${i}__LIST_RESOLUTION is required when READ_RESOLUTION='authenticated'. ` +
          `Set explicitly to one of: ${VALID_LIST_RESOLUTIONS.join(", ")}.`,
      );
    }

    if (!callbackAddr?.trim()) {
      throw new Error(`FILES_CK__${i}__CALLBACK_ADDR is required.`);
    }

    // Parse indexed categories: FILES_CK__i__CATEGORY__j
    const allowedCategories: string[] = [];
    for (let j = 0; ; j++) {
      const cat = env[`${prefix}__${i}__CATEGORY__${j}`];
      if (cat === undefined) break;
      const trimmed = cat.trim();
      if (!VALID_CATEGORIES.includes(trimmed)) {
        throw new Error(
          `FILES_CK__${i}__CATEGORY__${j} contains invalid category '${trimmed}'. Valid: ${VALID_CATEGORIES.join(", ")}.`,
        );
      }
      allowedCategories.push(trimmed);
    }

    if (allowedCategories.length === 0) {
      throw new Error(`FILES_CK__${i} must have at least one CATEGORY.`);
    }

    if (!maxSizeBytesRaw?.trim()) {
      throw new Error(`FILES_CK__${i}__MAX_SIZE_BYTES is required.`);
    }

    const maxSizeBytes = Number(maxSizeBytesRaw);
    if (!Number.isFinite(maxSizeBytes) || maxSizeBytes <= 0) {
      throw new Error(
        `FILES_CK__${i}__MAX_SIZE_BYTES must be a positive number. Got: '${maxSizeBytesRaw}'.`,
      );
    }

    // Parse indexed variants: FILES_CK__i__VARIANT__j__NAME, optional __MAX_DIM
    const variants: VariantConfig[] = [];
    for (let j = 0; ; j++) {
      const name = env[`${prefix}__${i}__VARIANT__${j}__NAME`];
      if (name === undefined) break;

      const trimmedName = name.trim();
      if (!trimmedName) {
        throw new Error(`FILES_CK__${i}__VARIANT__${j}__NAME is empty.`);
      }

      const maxDimRaw = env[`${prefix}__${i}__VARIANT__${j}__MAX_DIM`];
      let maxDimension: number | undefined;

      if (maxDimRaw !== undefined) {
        maxDimension = Number(maxDimRaw);
        if (!Number.isFinite(maxDimension) || maxDimension <= 0) {
          throw new Error(
            `FILES_CK__${i}__VARIANT__${j}__MAX_DIM must be a positive number. Got: '${maxDimRaw}'.`,
          );
        }
      }

      variants.push(
        maxDimension !== undefined ? { name: trimmedName, maxDimension } : { name: trimmedName },
      );
    }

    if (variants.length === 0) {
      throw new Error(`FILES_CK__${i} must have at least one VARIANT.`);
    }

    if (map.has(key)) {
      throw new Error(`Duplicate context key '${key}' at index ${i}.`);
    }

    map.set(key, {
      contextKey: key,
      uploadResolution: uploadResolution as UploadResolution,
      readResolution: readResolution as ReadResolution,
      listResolution: listResolution as ListResolution,
      callbackAddress: callbackAddr.trim(),
      allowedCategories: allowedCategories as ContentCategory[],
      maxSizeBytes,
      variants,
    });
  }

  return map;
}
