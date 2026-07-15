// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

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
import { HttpHeaders } from "@dcsv-io/d2-headers-http";
import * as m from "$lib/paraglide/messages.js";

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
  headers.set(HttpHeaders.AUTHORIZATION, `Bearer ${token}`);
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
  if (!token) throw new Error(m.common_errors_NOT_AUTHENTICATED());

  // 1. Request presigned URL from Files API
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
    const msg =
      translateBackendMessage(body?.messages?.[0]) ??
      m.files_errors_UPLOAD_FAILED({ status: String(response.status) });
    throw new Error(msg);
  }

  const result = await response.json();
  const data = result.data as UploadResult | undefined;
  if (!data?.presignedUrl || !data?.fileId) {
    throw new Error(m.files_errors_UPLOAD_INVALID_RESPONSE());
  }

  // 2. PUT blob directly to MinIO via presigned URL
  const putResponse = await fetch(data.presignedUrl, {
    method: "PUT",
    headers: { "Content-Type": blob.type || "image/webp" },
    body: blob,
    signal: AbortSignal.timeout(60_000), // 60s for large files
  });

  if (!putResponse.ok) {
    throw new Error(m.files_errors_STORAGE_UPLOAD_FAILED({ status: String(putResponse.status) }));
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
  if (!token) throw new Error(m.common_errors_NOT_AUTHENTICATED());

  const baseUrl = getFilesBaseUrl();
  const response = await fetch(`${baseUrl}/api/v1/files/${fileId}/${variantName}/url`, {
    method: "GET",
    headers: { [HttpHeaders.AUTHORIZATION]: `Bearer ${token}` },
    signal: AbortSignal.timeout(DEFAULT_TIMEOUT_MS),
  });

  if (!response.ok) {
    const body = await response.json().catch(() => null);
    const msg =
      translateBackendMessage(body?.messages?.[0]) ??
      m.files_errors_VARIANT_URL_FAILED({ status: String(response.status) });
    throw new Error(msg);
  }

  const result = await response.json();
  const url = result.data?.url as string | undefined;
  if (!url) throw new Error(m.files_errors_VARIANT_URL_INVALID_RESPONSE());

  return url;
}

/**
 * Backend `D2Result.messages` entries are TKMessage objects
 * (`{key, params?}`) per the spec-derived wire shape at
 * `contracts/tk-message/tk-message.spec.json`. Resolve to a localized
 * string at the runtime locale; return undefined when we can't map the
 * key so the caller falls back to a typed Paraglide message. Also
 * tolerates a bare `string` (caller already has the raw key and no params to bind).
 */
function translateBackendMessage(message: unknown): string | undefined {
  if (message === undefined || message === null) return undefined;
  let key: string;
  let params: Record<string, unknown> | undefined;
  if (typeof message === "string") {
    if (!message) return undefined;
    key = message;
    params = undefined;
  } else if (
    typeof message === "object" &&
    "key" in message &&
    typeof (message as { key: unknown }).key === "string"
  ) {
    const tk = message as { key: string; params?: Record<string, unknown> };
    if (!tk.key) return undefined;
    key = tk.key;
    params = tk.params;
  } else {
    return undefined;
  }

  const registry = m as unknown as Record<
    string,
    ((args?: Record<string, unknown>) => string) | undefined
  >;
  const fn = registry[key];
  if (typeof fn !== "function") return undefined;
  try {
    return fn(params);
  } catch {
    return undefined;
  }
}
