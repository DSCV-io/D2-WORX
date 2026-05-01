/**
 * Avatar display URL resolver with in-memory cache.
 *
 * Resolves a fileId + variant to a presigned S3 URL via the Files API.
 * The presigned URL is cached in memory for the page session — browser
 * HTTP cache handles the actual content (Cache-Control: immutable).
 *
 * If the presigned URL expires (1 hour), the browser cache still has
 * the content. A fresh presigned URL is only needed if the browser
 * cache is cleared.
 */
import { getVariantUrl } from "$lib/client/rest/files-client.js";

const cache = new Map<string, string>();

/**
 * Get a display URL for an avatar image.
 *
 * @param fileId - The file ID from user.image
 * @param variant - The variant size (e.g. "medium", "thumb")
 * @returns Presigned S3 URL for use in <img src>
 */
export async function getAvatarDisplayUrl(
  fileId: string,
  variant: string = "medium",
): Promise<string> {
  const cacheKey = `${fileId}:${variant}`;

  const cached = cache.get(cacheKey);
  if (cached) return cached;

  const url = await getVariantUrl(fileId, variant);
  cache.set(cacheKey, url);
  return url;
}

/** Clear a specific entry (e.g. after avatar re-upload). */
export function invalidateAvatarUrl(fileId: string): void {
  for (const key of cache.keys()) {
    if (key.startsWith(`${fileId}:`)) {
      cache.delete(key);
    }
  }
}

/** Clear all cached URLs (e.g. on sign-out). */
export function clearAvatarUrlCache(): void {
  cache.clear();
}
