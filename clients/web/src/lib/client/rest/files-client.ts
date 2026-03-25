/**
 * Client-side (browser) REST client for the Files service.
 *
 * The Files service has its own public REST API — requests go directly
 * from the browser, NOT through the SvelteKit proxy. JWT auth is required.
 *
 * Upload flow:
 *   1. POST /api/v1/avatar → get { fileId, presignedUrl }
 *   2. PUT presignedUrl with file blob → direct to MinIO
 *
 * Display flow:
 *   GET /api/v1/files/:fileId/:variant/url → presigned GET URL for <img src>
 */
import { env } from "$env/dynamic/public";

import { getToken } from "./gateway-client.js";

const DEFAULT_TIMEOUT_MS = 30_000;

function getFilesBaseUrl(): string {
  const url = env.PUBLIC_FILES_URL;
  if (!url) {
    throw new Error(
      "[d2-sveltekit] Missing required env var PUBLIC_FILES_URL. " +
        "The public Files service URL must be configured for avatar uploads.",
    );
  }
  return url.replace(/\/+$/, "");
}

function buildHeaders(token: string): Headers {
  const headers = new Headers();
  headers.set("Authorization", `Bearer ${token}`);
  headers.set("Content-Type", "application/json");
  return headers;
}

interface UploadResult {
  fileId: string;
  presignedUrl: string;
}

/**
 * Upload a file to the Files service.
 *
 * 1. Requests a presigned PUT URL from the Files API
 * 2. PUTs the blob directly to MinIO via the presigned URL
 * 3. Returns the fileId (processing happens async via RabbitMQ → SignalR)
 *
 * @param contextKey - Upload target (e.g. "avatar" for user avatars)
 * @param blob - File content (cropped image blob)
 * @param displayName - Original filename for display
 */
export async function uploadFile(
  contextKey: string,
  blob: Blob,
  displayName: string,
): Promise<{ fileId: string }> {
  const token = await getToken();
  if (!token) throw new Error("Not authenticated.");

  // Step 1: Request presigned URL from Files API
  const baseUrl = getFilesBaseUrl();
  const response = await fetch(`${baseUrl}/api/v1/${contextKey}`, {
    method: "POST",
    headers: buildHeaders(token),
    body: JSON.stringify({
      contentType: blob.type || "image/webp",
      displayName,
      sizeBytes: blob.size,
    }),
    signal: AbortSignal.timeout(DEFAULT_TIMEOUT_MS),
  });

  if (!response.ok) {
    const body = await response.json().catch(() => null);
    const msg = body?.messages?.[0] ?? `Upload failed (${response.status})`;
    throw new Error(msg);
  }

  const result = await response.json();
  const data = result.data as UploadResult | undefined;
  if (!data?.presignedUrl || !data?.fileId) {
    throw new Error("Invalid upload response from Files service.");
  }

  // Step 2: PUT blob directly to MinIO via presigned URL
  const putResponse = await fetch(data.presignedUrl, {
    method: "PUT",
    headers: { "Content-Type": blob.type || "image/webp" },
    body: blob,
    signal: AbortSignal.timeout(60_000), // 60s for large files
  });

  if (!putResponse.ok) {
    throw new Error(`File upload to storage failed (${putResponse.status})`);
  }

  return { fileId: data.fileId };
}

/**
 * Get a presigned download URL for a file variant.
 *
 * The returned URL can be used directly in `<img src>`. It's time-limited
 * (1 hour) but the browser HTTP cache stores the content immutably, so
 * the URL only needs to work once.
 */
export async function getVariantUrl(fileId: string, variantName: string): Promise<string> {
  const token = await getToken();
  if (!token) throw new Error("Not authenticated.");

  const baseUrl = getFilesBaseUrl();
  const response = await fetch(`${baseUrl}/api/v1/files/${fileId}/${variantName}/url`, {
    method: "GET",
    headers: { Authorization: `Bearer ${token}` },
    signal: AbortSignal.timeout(DEFAULT_TIMEOUT_MS),
  });

  if (!response.ok) {
    const body = await response.json().catch(() => null);
    const msg = body?.messages?.[0] ?? `Failed to get variant URL (${response.status})`;
    throw new Error(msg);
  }

  const result = await response.json();
  const url = result.data?.url as string | undefined;
  if (!url) throw new Error("Invalid variant URL response.");

  return url;
}
