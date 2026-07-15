// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

/**
 * Client-side (browser) gateway client for calling the .NET REST Gateway.
 *
 * Two entry points:
 * - `apiCall` — authenticated (JWT from auth token endpoint, auto-retries once on 401)
 * - `apiCallAnon` — anonymous (no auth headers)
 *
 * JWT is obtained by calling `/api/auth/token` (proxied to Auth service via SvelteKit).
 * The fingerprint for rate-limiting is passed from the server via the root layout
 * and set with `setClientFingerprint()`.
 *
 * Browser-only module — uses PUBLIC_GATEWAY_URL and fetch with credentials.
 */
import { env } from "$env/dynamic/public";
import { D2Result, unauthorized } from "@d2/result";
import { getLocale } from "$lib/paraglide/runtime.js";
import { executeFetch } from "$lib/shared/rest/gateway-response.js";

const DEFAULT_TIMEOUT_MS = 30_000;

export interface ApiCallOptions {
  method?: "GET" | "POST" | "PUT" | "PATCH" | "DELETE";
  body?: unknown;
  idempotencyKey?: string;
  signal?: AbortSignal;
  timeout?: number;
}

let fingerprint: string | undefined;

/**
 * Set the client fingerprint for the X-Client-Fingerprint rate-limiting header.
 * Called from the root layout when the server passes fingerprint data.
 */
export function setClientFingerprint(fp: string): void {
  fingerprint = fp;
}

/**
 * Cached JWT token and its expiry time (epoch ms).
 * Kept in memory only — never persisted (XSS safety).
 */
let cachedToken: string | undefined;
let tokenExpiresAt = 0;

/** Buffer before expiry to trigger refresh (2 minutes). */
const REFRESH_BUFFER_MS = 2 * 60 * 1000;

/**
 * Fetch a JWT from the auth token endpoint.
 * Returns null if the user is not authenticated.
 */
async function fetchToken(): Promise<string | null> {
  try {
    const response = await fetch("/api/auth/token", {
      method: "GET",
      credentials: "include",
      signal: AbortSignal.timeout(5_000),
    });

    if (!response.ok) return null;

    const json = await response.json();
    const token = json?.token as string | undefined;
    if (!token) return null;

    // Parse expiry from JWT payload (base64url-encoded middle segment)
    try {
      const payload = JSON.parse(atob(token.split(".")[1].replace(/-/g, "+").replace(/_/g, "/")));
      tokenExpiresAt = (payload.exp ?? 0) * 1000;
    } catch {
      // Fallback: assume 15 minutes from now
      tokenExpiresAt = Date.now() + 15 * 60 * 1000;
    }

    cachedToken = token;
    return token;
  } catch {
    return null;
  }
}

/**
 * Get a valid JWT, using cache when possible.
 * Returns null if the user is not authenticated.
 */
export async function getToken(): Promise<string | null> {
  if (cachedToken && Date.now() < tokenExpiresAt - REFRESH_BUFFER_MS) {
    return cachedToken;
  }
  return fetchToken();
}

/** Invalidate the cached JWT (e.g. on sign-out). */
export function invalidateToken(): void {
  cachedToken = undefined;
  tokenExpiresAt = 0;
}

/**
 * Build common headers for gateway requests.
 *
 * Always includes `D2-Locale` so the gateway's TranslationMiddleware can
 * translate D2Result messages into the user's language. The locale is
 * resolved by Paraglide from the cookie / global variable.
 */
function buildHeaders(options?: ApiCallOptions): Headers {
  const headers = new Headers();

  headers.set("D2-Locale", getLocale());

  if (options?.body !== undefined) {
    headers.set("Content-Type", "application/json");
  }

  if (options?.idempotencyKey) {
    headers.set("Idempotency-Key", options.idempotencyKey);
  }

  if (fingerprint) {
    headers.set("X-Client-Fingerprint", fingerprint);
  }

  return headers;
}

/**
 * Execute a fetch to the gateway.
 */
async function executeApiFetch<TData>(
  url: string,
  headers: Headers,
  options?: ApiCallOptions,
): Promise<D2Result<TData>> {
  return executeFetch<TData>(url, {
    method: options?.method,
    headers,
    body: options?.body !== undefined ? JSON.stringify(options.body) : undefined,
    signal: options?.signal,
    timeout: options?.timeout ?? DEFAULT_TIMEOUT_MS,
  });
}

function getGatewayBaseUrl(): string {
  const url = env.PUBLIC_GATEWAY_URL;
  if (!url) {
    throw new Error(
      "[d2-sveltekit] Missing required env var PUBLIC_GATEWAY_URL. " +
        "The public gateway URL must be configured for browser-side API calls.",
    );
  }
  return url.replace(/\/+$/, "");
}

/**
 * Authenticated client-side gateway call.
 *
 * Obtains a JWT from the auth token endpoint (cached in memory) and
 * auto-retries once on 401 (token may have expired between cache and use).
 *
 * @param path - Gateway endpoint path (e.g. "/api/v1/auth/profile")
 * @param options - Request options (method, body, timeout, etc.)
 */
export async function apiCall<TData = void>(
  path: string,
  options?: ApiCallOptions,
): Promise<D2Result<TData>> {
  const token = await getToken();
  if (!token) {
    return unauthorized<TData>();
  }

  const headers = buildHeaders(options);
  headers.set("Authorization", `Bearer ${token}`);

  const url = `${getGatewayBaseUrl()}${path}`;
  const result = await executeApiFetch<TData>(url, headers, options);

  // Single retry on 401 — token may have expired between cache check and use
  if (result.statusCode === 401) {
    invalidateToken();
    const freshToken = await fetchToken();
    if (!freshToken) return result;

    headers.set("Authorization", `Bearer ${freshToken}`);
    return executeApiFetch<TData>(url, headers, options);
  }

  return result;
}

/**
 * Anonymous client-side gateway call.
 *
 * No JWT, no fingerprint header. Used for public endpoints.
 *
 * @param path - Gateway endpoint path (e.g. "/api/v1/geo/countries")
 * @param options - Request options (method, body, timeout, etc.)
 */
export async function apiCallAnon<TData = void>(
  path: string,
  options?: ApiCallOptions,
): Promise<D2Result<TData>> {
  const headers = buildHeaders(options);
  const url = `${getGatewayBaseUrl()}${path}`;
  return executeApiFetch<TData>(url, headers, options);
}
