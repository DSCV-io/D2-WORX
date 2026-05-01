/**
 * Server-side gateway client for calling the .NET REST Gateway.
 *
 * Lazy singleton pattern matching auth.server.ts and middleware.server.ts.
 * Hard-fails if SVELTEKIT_GATEWAY__URL is missing — gateway is required.
 *
 * Two entry points:
 * - `gatewayFetch` — authenticated (JWT + service key for trusted-service bypass)
 * - `gatewayFetchAnon` — anonymous (no auth headers)
 *
 * Server-side calls use a service key (X-Api-Key) so the gateway's
 * JwtFingerprintMiddleware skips fingerprint validation (trusted service).
 *
 * The `.server.ts` suffix ensures this module is never included in
 * client-side bundles (SvelteKit convention).
 */
import { D2Result } from "@d2/result";
import { getLocale } from "$lib/paraglide/runtime.js";
import { getAuthContext } from "../auth.server.js";
import { executeFetch } from "$lib/shared/rest/gateway-response.js";

const DEFAULT_TIMEOUT_MS = 10_000;

export interface GatewayRequestOptions {
  method?: "GET" | "POST" | "PUT" | "PATCH" | "DELETE";
  body?: unknown;
  idempotencyKey?: string;
  signal?: AbortSignal;
  timeout?: number;
}

interface GatewayContext {
  baseUrl: string;
  serviceKey: string | undefined;
}

let cached: GatewayContext | undefined;

/**
 * Returns the shared gateway context (lazy singleton).
 * Throws if SVELTEKIT_GATEWAY__URL is not set.
 */
export function getGatewayContext(): GatewayContext {
  if (cached) return cached;

  const baseUrl = process.env.SVELTEKIT_GATEWAY__URL;

  if (!baseUrl) {
    throw new Error(
      "[d2-sveltekit] FATAL: Missing required env var SVELTEKIT_GATEWAY__URL. " +
        "The gateway URL must be configured. Check your .env.local file.",
    );
  }

  const serviceKey = process.env.SVELTEKIT_GATEWAY__SERVICE_KEY || undefined;

  cached = { baseUrl: baseUrl.replace(/\/+$/, ""), serviceKey };
  return cached;
}

/**
 * Build common headers for gateway requests.
 *
 * Always includes `D2-Locale` so the gateway's TranslationMiddleware can
 * translate D2Result messages into the user's language. The locale is
 * resolved by Paraglide's AsyncLocalStorage context (set by the
 * paraglideMiddleware in hooks.server.ts).
 */
function buildHeaders(options?: GatewayRequestOptions): Headers {
  const headers = new Headers();

  headers.set("D2-Locale", getLocale());

  if (options?.body !== undefined) {
    headers.set("Content-Type", "application/json");
  }

  if (options?.idempotencyKey) {
    headers.set("Idempotency-Key", options.idempotencyKey);
  }

  return headers;
}

/**
 * Execute a fetch to the gateway with timeout handling.
 */
async function executeGatewayFetch<TData>(
  url: string,
  headers: Headers,
  options?: GatewayRequestOptions,
): Promise<D2Result<TData>> {
  return executeFetch<TData>(url, {
    method: options?.method,
    headers,
    body: options?.body !== undefined ? JSON.stringify(options.body) : undefined,
    signal: options?.signal,
    timeout: options?.timeout ?? DEFAULT_TIMEOUT_MS,
  });
}

/**
 * Authenticated server-side gateway call.
 *
 * Obtains a JWT via JwtManager and includes the service key (if configured)
 * for trusted-service bypass of fingerprint validation.
 *
 * @param path - Gateway endpoint path (e.g. "/api/v1/auth/profile")
 * @param cookies - Raw cookie header from the incoming browser request
 * @param options - Request options (method, body, timeout, etc.)
 */
export async function gatewayFetch<TData = void>(
  path: string,
  cookies: string,
  options?: GatewayRequestOptions,
): Promise<D2Result<TData>> {
  const ctx = getGatewayContext();
  const authCtx = getAuthContext();

  if (!authCtx) {
    return D2Result.serviceUnavailable<TData>();
  }

  const jwt = await authCtx.jwtManager.getToken(cookies);
  if (!jwt) {
    return D2Result.unauthorized<TData>();
  }

  const headers = buildHeaders(options);
  headers.set("Authorization", `Bearer ${jwt}`);

  if (ctx.serviceKey) {
    headers.set("X-Api-Key", ctx.serviceKey);
  }

  const url = `${ctx.baseUrl}${path}`;
  return executeGatewayFetch<TData>(url, headers, options);
}

/**
 * Anonymous server-side gateway call.
 *
 * No JWT, no service key. Used for public endpoints (e.g. reference data).
 *
 * @param path - Gateway endpoint path (e.g. "/api/v1/geo/countries")
 * @param options - Request options (method, body, timeout, etc.)
 */
export async function gatewayFetchAnon<TData = void>(
  path: string,
  options?: GatewayRequestOptions,
): Promise<D2Result<TData>> {
  const ctx = getGatewayContext();

  const headers = buildHeaders(options);
  const url = `${ctx.baseUrl}${path}`;

  return executeGatewayFetch<TData>(url, headers, options);
}
